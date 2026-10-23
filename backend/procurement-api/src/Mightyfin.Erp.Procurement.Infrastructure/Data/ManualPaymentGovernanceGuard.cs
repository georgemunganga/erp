using System.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Mightyfin.Erp.Core.Approvals.Domain;
using Mightyfin.Erp.Core.Approvals.Infrastructure;
using Mightyfin.Erp.Procurement.Application;
using Mightyfin.Erp.Procurement.Domain;

namespace Mightyfin.Erp.Procurement.Infrastructure.Data;

public sealed partial class ProcurementDbContext
{
    private bool manualPaymentGovernanceRequired;
    internal void RequireManualPaymentGovernance() => manualPaymentGovernanceRequired = true;

    private void GuardManualPaymentGovernance()
    {
        var added = ChangeTracker.Entries<ProcurementRecord>().Where(x => x.State == EntityState.Added)
            .Select(x => x.Entity).ToArray();
        var entries = ChangeTracker.Entries<ProcurementRecord>().Where(x => x.Entity is ManualPaymentWorkflowCaseLink
            or ManualPaymentCoreDecisionLink or ManualPaymentBusinessDateObservation).ToArray();
        foreach (var entry in entries)
        {
            if (entry.State is EntityState.Modified or EntityState.Deleted)
                throw new InvalidOperationException("ManualPaymentGovernanceAppendOnly");
            if (entry.State != EntityState.Added) continue;
            if (entry.Entity.TenantId != scope.TenantId || entry.Entity.LegalEntityId != scope.LegalEntityId || entry.Entity.Version != 1)
                throw new InvalidOperationException("ManualPaymentGovernanceScopeRequired");
            ManualPaymentWriteScope(entry.Entity switch {
                ManualPaymentWorkflowCaseLink row => row.BranchId,
                ManualPaymentCoreDecisionLink row => row.BranchId,
                ManualPaymentBusinessDateObservation row => row.BranchId,
                _ => Guid.Empty });
        }
        if (!manualPaymentGovernanceRequired && !entries.Any(x => x.State == EntityState.Added)) return;
        if (!Database.IsNpgsql() || Database.CurrentTransaction is null || sharedApprovalCore?.Database.CurrentTransaction is null
            || sharedApprovalTransaction is null || sharedApprovalTransaction.IsolationLevel != IsolationLevel.Serializable
            || !ReferenceEquals(sharedApprovalTransaction, Database.CurrentTransaction.GetDbTransaction())
            || !ReferenceEquals(sharedApprovalTransaction, sharedApprovalCore.Database.CurrentTransaction.GetDbTransaction())
            || !ReferenceEquals(sharedApprovalConnection, Database.GetDbConnection())
            || !ReferenceEquals(sharedApprovalConnection, sharedApprovalCore.Database.GetDbConnection())
            || !ReferenceEquals(sharedApprovalTransaction.Connection, sharedApprovalConnection))
            throw new InvalidOperationException("ManualPaymentCoreTransactionRequired");
        using var core = new ApprovalDbContext(new DbContextOptionsBuilder<ApprovalDbContext>().UseNpgsql(Database.GetDbConnection()).Options,
            new ManualPaymentApprovalScope(scope.TenantId, scope.LegalEntityId, scope.SubjectId));
        core.Database.UseTransaction(Database.CurrentTransaction.GetDbTransaction());
        foreach (var link in added.OfType<ManualPaymentWorkflowCaseLink>())
        {
            var transition = Opening(link.OpeningManualDecisionId, link.ManualPaymentRequestId);
            var parent = ChangedRequest(link.ManualPaymentRequestId);
            var workflow = ReadCase(core, link.CoreApprovalCaseId);
            if (!sharedApprovalInsertedCases.Contains(workflow.Id) || workflow.State != "Pending"
                || core.Decisions.Any(x => x.CaseId == workflow.Id)
                || transition.ActorPrincipalId != scope.SubjectId || link.CreatedBy != scope.SubjectId
                || (link.Purpose == ManualPaymentCoreWorkflow.Approval ? transition.Action != "Submit" : transition.Action != "Record")
                || transition.ToVersion != link.OpenedRequestVersion || parent.Version != link.OpenedRequestVersion
                || link.OpenedAt != ProcurementEvidenceEncoding.DatabaseTimestamp(workflow.OpenedAt))
                throw new InvalidOperationException("ManualPaymentCoreOpeningInsertRequired");
            ManualPaymentCoreWorkflow.VerifyCurrentCase(parent, link, workflow);
            var invoice = SupplierInvoices.AsNoTracking().Single(x => x.Id == parent.SupplierInvoiceId);
            if (invoice.Version != link.InvoiceVersionAtOpening || invoice.GrossAmount != link.InvoiceGrossAmountAtOpening)
                throw new InvalidOperationException("ManualPaymentCoreInvoiceSnapshotRequired");
            var policy = PolicyVersions.AsNoTracking().Single(x => x.Id == link.PolicyVersionId);
            VerifyPublishedOperationalPolicyIdentity(policy, link.Purpose == ManualPaymentCoreWorkflow.Confirmation);
            if (policy.PolicyKey != ManualPaymentReviewPolicyRules.PolicyKey || policy.PolicyVersionNumber != link.PolicyVersionNumber
                || ProcurementEvidenceEncoding.PolicyJsonDigest(policy.RulesJson) != link.PolicyRulesSha256
                || !ManualPaymentReviewPolicyRules.WithinConfiguredMaximum(ManualPaymentReviewPolicyRules.Parse(policy.RulesJson), parent.CurrencyCode, parent.Amount))
                throw new InvalidOperationException("ManualPaymentCorePolicyPinRequired");
            if (link.Purpose == ManualPaymentCoreWorkflow.Approval)
                _ = ManualPaymentReviewPolicyRules.ResolveEffective(policy, scope.TenantId, scope.LegalEntityId, link.OpenedAt);
            else
            {
                var approval = ManualPaymentWorkflowCases.AsNoTracking().Single(x => x.ManualPaymentRequestId == parent.Id
                    && x.Purpose == ManualPaymentCoreWorkflow.Approval && x.OpenedRequestVersion == transition.FromVersion - 1
                    && x.PolicyVersionId == link.PolicyVersionId);
                var approvedCase = ReadCase(core, approval.CoreApprovalCaseId);
                if (approvedCase.State != "Approved") throw new InvalidOperationException("ManualPaymentOriginalApprovalRequired");
                ManualPaymentCoreWorkflow.VerifyCurrentCase(parent, approval, approvedCase);
            }
        }
        foreach (var link in added.OfType<ManualPaymentCoreDecisionLink>())
        {
            var transition = Opening(link.ManualPaymentDecisionId, link.ManualPaymentRequestId);
            var parent = ChangedRequest(link.ManualPaymentRequestId);
            var source = ManualPaymentWorkflowCases.AsNoTracking().Single(x => x.Id == link.WorkflowCaseLinkId);
            var workflow = ReadCase(core, source.CoreApprovalCaseId);
            var decision = core.Decisions.AsNoTracking().Single(x => x.Id == link.CoreApprovalDecisionId);
            var expected = ManualPaymentCoreWorkflow.LinkDecision(new(parent, transition), source, workflow, decision);
            var receipt = added.OfType<ManualPaymentCommandReceipt>().Single(x => x.ManualPaymentRequestId == parent.Id
                && x.ResultVersion == transition.ToVersion);
            if (!sharedApprovalInsertedDecisions.Contains(decision.Id) || decision.IdempotencyKey != receipt.CommandKey.ToString("D")
                || link.BranchId != expected.BranchId || link.ManualAction != expected.ManualAction
                || link.ActorPrincipalId != scope.SubjectId || link.ActorPrincipalId != expected.ActorPrincipalId
                || link.ActorCoreLinkedWorkerId != scope.CoreLinkedWorkerId || link.ActorCoreLinkedWorkerId != expected.ActorCoreLinkedWorkerId
                || link.FromRequestVersion != expected.FromRequestVersion || link.ToRequestVersion != expected.ToRequestVersion
                || ProcurementEvidenceEncoding.DatabaseTimestamp(link.DecidedAt) != expected.DecidedAt)
                throw new InvalidOperationException("ManualPaymentCoreDecisionInsertRequired");
        }
        foreach (var observation in added.OfType<ManualPaymentBusinessDateObservation>())
        {
            var transition = Opening(observation.ManualPaymentDecisionId, observation.ManualPaymentRequestId);
            var parent = ChangedRequest(observation.ManualPaymentRequestId);
            var expected = ManualPaymentCoreWorkflow.ObserveDate(new(parent, transition), observation.CompanyBusinessDate,
                observation.CompanyTimeZoneId, observation.ObservedAtUtc);
            if (observation.BranchId != expected.BranchId || observation.RequestVersion != expected.RequestVersion
                || observation.Action != expected.Action || observation.CreatedBy != scope.SubjectId)
                throw new InvalidOperationException("ManualPaymentCompanyDateObservationInvalid");
        }
        if (!manualPaymentGovernanceRequired) return;
        foreach (var transition in added.OfType<ManualPaymentDecision>())
        {
            var caseRequired = transition.Action is "Submit" or "Record";
            var decisionRequired = transition.Action is "Approve" or "Return" or "Reject" or "Confirm" or "Dispute"
                || transition.Action == "Cancel" && transition.FromStatus == "PendingApproval";
            var dateRequired = transition.Action is "Record" or "Confirm";
            if (caseRequired && added.OfType<ManualPaymentWorkflowCaseLink>().Count(x => x.OpeningManualDecisionId == transition.Id) != 1
                || decisionRequired && added.OfType<ManualPaymentCoreDecisionLink>().Count(x => x.ManualPaymentDecisionId == transition.Id) != 1
                || dateRequired && added.OfType<ManualPaymentBusinessDateObservation>().Count(x => x.ManualPaymentDecisionId == transition.Id) != 1)
                throw new InvalidOperationException("ManualPaymentGovernanceCompanionRequired");
        }
        return;

        ManualPaymentDecision Opening(Guid decisionId, Guid requestId) => added.OfType<ManualPaymentDecision>().SingleOrDefault(x =>
            x.Id == decisionId && x.ManualPaymentRequestId == requestId) ?? throw new InvalidOperationException("ManualPaymentSameSaveTransitionRequired");
        ManualPaymentRequest ChangedRequest(Guid id) => ChangeTracker.Entries<ManualPaymentRequest>().Where(x =>
            x.Entity.Id == id && x.State is EntityState.Added or EntityState.Modified).Select(x => x.Entity).SingleOrDefault()
            ?? throw new InvalidOperationException("ManualPaymentSameSaveRequestRequired");
    }

    private static ApprovalCase ReadCase(ApprovalDbContext core, Guid id) => core.Cases.AsNoTracking()
        .Include(x => x.Stages).ThenInclude(x => x.Assignments).Single(x => x.Id == id);
    private sealed record ManualPaymentApprovalScope(string TenantId, Guid LegalEntityId, string SubjectId) : IApprovalScope;
}
