using System.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Mightyfin.Erp.Core.Approvals.Domain;
using Mightyfin.Erp.Procurement.Application;
using Mightyfin.Erp.Procurement.Domain;

namespace Mightyfin.Erp.Procurement.Infrastructure.Data;

public sealed partial class ProcurementDbContext
{
    /// <summary>Admit a scoped Procurement capacity lease only after the original
    /// company baseline, independent Core approval, complete current company set
    /// and all live target-PO reservations agree in this shared transaction.
    /// Host must obtain both witnesses from its authoritative Core/issued loaders.
    /// The default IInvoiceHistoricalExposureOwner remains unavailable.</summary>
    public async Task<InvoiceHistoricalExposure> BuildPrincipalInvoiceOwnerLeaseAsync(
        InvoiceExposureReadContext context, PrincipalInvoiceIssuedWitness issuedWitness,
        InvoiceProcurementApprovedBaselineWitness approvedBaseline,
        InvoiceMatchExposureState exposure, CancellationToken ct)
    {
        if (!Database.IsNpgsql() || Database.CurrentTransaction is null
            || Database.CurrentTransaction.GetDbTransaction().IsolationLevel != IsolationLevel.Serializable
            || sharedApprovalCore is null || sharedApprovalTransaction is null
            || !ReferenceEquals(sharedApprovalTransaction, Database.CurrentTransaction.GetDbTransaction())
            || !ReferenceEquals(sharedApprovalCore.Database.GetDbConnection(), Database.GetDbConnection())
            || approvedBaseline is null || approvedBaseline.CoreCase is null
            || approvedBaseline.Decisions is null || issuedWitness is null || exposure is null
            || sharedApprovalCore.Entry(approvedBaseline.CoreCase).State != EntityState.Unchanged
            || approvedBaseline.Decisions.Any(x => x is null
                || sharedApprovalCore.Entry(x).State != EntityState.Unchanged)
            || context is null || context.At == default || context.At.Offset != TimeSpan.Zero
            || context.TenantId != scope.TenantId || context.LegalEntityId != scope.LegalEntityId
            || context.BranchId != issuedWitness.BranchId
            || context.PurchaseOrderId != issuedWitness.PurchaseOrderId
            || context.IssuedVersionId != issuedWitness.IssuedVersionId
            || context.SupplierId != issuedWitness.SupplierId
            || context.CurrencyCode != issuedWitness.CurrencyCode
            || context.PolicyRulesSha256 is not { Length: 64 }
            || exposure.Id == Guid.Empty || exposure.TenantId != scope.TenantId
            || exposure.LegalEntityId != scope.LegalEntityId
            || exposure.PurchaseOrderId != context.PurchaseOrderId
            || exposure.IssuedVersionId != context.IssuedVersionId
            || exposure.SourceSchemaVersion != 3
            || exposure.SupplierId != context.SupplierId
            || exposure.CurrencyCode != context.CurrencyCode || exposure.Sequence < 0)
            throw new InvalidOperationException("InvoiceOwnerLeaseTransactionRequired");

        var coreCase = approvedBaseline.CoreCase;
        var baseline = await InvoiceProcurementOwnerBaselines.AsNoTracking()
            .SingleAsync(x => x.Id == coreCase.SourceId && x.CoreCaseId == coreCase.Id, ct);
        var rows = await InvoiceProcurementOwnerBaselineRows.AsNoTracking()
            .Where(x => x.BaselineId == baseline.Id).ToArrayAsync(ct);
        _ = InvoiceProcurementStoredBaselineEvidence.VerifyStored(baseline, rows);
        var decision = approvedBaseline.Decisions.SingleOrDefault(x =>
            x.IdempotencyKey == coreCase.CompletionKey && x.Action == "Approve")
            ?? throw new InvalidOperationException("InvoiceOwnerCoreDecisionUnavailable");
        var audit = await AuditEvents.AsNoTracking().SingleAsync(x =>
            x.RecordType == InvoiceProcurementBaselineEvidence.SourceType
            && x.RecordId == baseline.Id && x.Action == "InvoiceOwnerBaselineApproved", ct);
        InvoiceProcurementBaselineEvidence.VerifyCoreReview(baseline, coreCase,
            approvedBaseline.Decisions, audit, approvedBaseline.ReviewerPrincipalId,
            approvedBaseline.ReviewerWorkerAliasId, context.At);
        var receipts = await InvoiceProcurementOwnerCommandReceipts.AsNoTracking()
            .Where(x => x.BaselineId == baseline.Id).ToArrayAsync(ct);
        if (receipts.Length != 2 || receipts.Select(x => x.CommandKey).Distinct().Count() != 2
            || receipts.Count(x => x.Operation == "Open") != 1
            || receipts.Count(x => x.Operation == "Decide") != 1)
            throw new InvalidOperationException("InvoiceOwnerBaselineReceiptSetChanged");
        foreach (var receipt in receipts)
        {
            var original = InvoiceProcurementOwnerReceiptEvidence.Verify(receipt, baseline);
            if (receipt.Operation == "Decide"
                && (receipt.CoreDecisionId != decision.Id
                    || receipt.ActorPrincipalId != approvedBaseline.ReviewerPrincipalId
                    || receipt.ActorWorkerAliasId != approvedBaseline.ReviewerWorkerAliasId
                    || original.CoreCaseState != "Approved"
                    || original.CoreCaseVersion != coreCase.Version))
                throw new InvalidOperationException("InvoiceOwnerBaselineDecisionReceiptChanged");
        }

        var current = await ReadPrincipalInvoiceCurrentClaimsAsync(issuedWitness, ct);
        // A new, tracked zero-sequence exposure is permitted for the first
        // comparison. Every later lease must use the unchanged stored row from
        // this same transaction; a request-shaped exposure cannot set a sequence.
        var persistedExposure = await InvoiceMatchExposureStates.AsNoTracking()
            .SingleOrDefaultAsync(x => x.PurchaseOrderId == context.PurchaseOrderId
                && x.IssuedVersionId == context.IssuedVersionId, ct);
        var exposureState = Entry(exposure).State;
        if (persistedExposure is null
            ? exposureState != EntityState.Added || exposure.Sequence != 0
                || current.ActiveClaims.Count != 0
            : exposureState != EntityState.Unchanged
                || persistedExposure.Id != exposure.Id
                || persistedExposure.Version != exposure.Version
                || persistedExposure.Sequence != exposure.Sequence
                || persistedExposure.TenantId != exposure.TenantId
                || persistedExposure.LegalEntityId != exposure.LegalEntityId
                || persistedExposure.PurchaseOrderId != exposure.PurchaseOrderId
                || persistedExposure.IssuedVersionId != exposure.IssuedVersionId
                || persistedExposure.SourceSchemaVersion != exposure.SourceSchemaVersion
                || persistedExposure.SupplierId != exposure.SupplierId
                || persistedExposure.CurrencyCode != exposure.CurrencyCode)
            throw new InvalidOperationException("InvoiceOwnerCurrentExposureChanged");
        if (!InvoiceProcurementStoredBaselineEvidence.NoRetainedExposureForOrder(
                baseline, rows, current.Capture, context.PurchaseOrderId,
                context.SupplierId, context.CurrencyCode))
            throw new InvalidOperationException("InvoiceOwnerHistoricalTargetRequiresReconciliation");
        PrincipalInvoiceCurrentOwnerScope.RequireNoUnclassifiedEffect(current.Capture,
            context.PurchaseOrderId, context.SupplierId, issuedWitness.BranchId,
            context.CurrencyCode, current.ActiveClaims.Select(x => x.Allocation.Id).ToHashSet());
        var local = current.ActiveClaims.Select(x =>
            InvoiceMatchExecutionEvidence.Consumption(x.Allocation, x.Invoice.BranchRef)).ToArray();
        if (local.Where((claim, index) => claim.BranchRef != context.BranchId
                || claim.AcceptanceId != current.ActiveClaims[index].Allocation.PrincipalReceivingFactId).Any())
            throw new InvalidOperationException("PrincipalInvoiceActiveFactClaimChanged");
        if (exposure.Sequence == 0 && local.Length != 0
            || exposure.Sequence > 0 && local.Length == 0)
            throw new InvalidOperationException("InvoiceOwnerCurrentExposureClaimChanged");
        var currentPolicies = await PolicyVersions.AsNoTracking()
            .Where(x => x.PolicyKey == InvoiceMatchingPolicyRules.PolicyKey
                && x.State == "Published" && x.EffectiveFrom <= context.At
                && (x.EffectiveTo == null || context.At < x.EffectiveTo))
            .Take(2).ToArrayAsync(ct);
        if (currentPolicies.Length != 1
            || ProcurementEvidenceEncoding.PolicyJsonDigest(currentPolicies[0].RulesJson)
                != context.PolicyRulesSha256)
            throw new InvalidOperationException("InvoiceOwnerCurrentPolicyChanged");
        _ = InvoiceMatchingPolicyRules.Project(currentPolicies[0]);
        return InvoiceProcurementOwnerLeaseEvidence.Encode(context, baseline, decision.Id,
            current.Inventory, local, exposure.Id, exposure.Sequence,
            currentPolicies[0].EffectiveTo);
    }
}
