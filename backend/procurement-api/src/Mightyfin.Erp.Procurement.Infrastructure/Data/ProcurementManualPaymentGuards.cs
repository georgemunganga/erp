using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Mightyfin.Erp.Procurement.Application;
using Mightyfin.Erp.Procurement.Domain;

namespace Mightyfin.Erp.Procurement.Infrastructure.Data;

public sealed partial class ProcurementDbContext
{
    // Storage invariants, not current Core authorization or aggregate payment capacity.
    // The owning API must apply the pure workflow and Core checks within its transaction.
    private void GuardManualPayments()
    {
        foreach (var entry in ChangeTracker.Entries<ManualPaymentRequest>()
            .Where(x => x.State is EntityState.Added or EntityState.Modified))
        {
            var row = entry.Entity;
            ManualPaymentWriteScope(row.BranchId);
            if (!ProcurementPrincipalEvidence.Canonical(row.CreatorPrincipalId)
                || !ProcurementPrincipalEvidence.Canonical(row.ApproverPrincipalId)
                || !ProcurementPrincipalEvidence.Canonical(row.RecorderPrincipalId)
                || !ProcurementPrincipalEvidence.Canonical(row.ConfirmerPrincipalId)
                || row.CreatorPrincipalId == row.ApproverPrincipalId || row.RecorderPrincipalId == row.ConfirmerPrincipalId
                || row.Amount <= 0 || !ExactDecimalJsonCodec.IsRepresentable(row.Amount, 18, 4)
                || row.CurrencyCode is not { Length: 3 } || row.CurrencyCode.Any(c => c is < 'A' or > 'Z'))
                throw new InvalidOperationException("ManualPaymentStorageValuesInvalid");
            if (new[] { row.CreatorCoreLinkedWorkerId, row.ApproverCoreLinkedWorkerId, row.RecorderCoreLinkedWorkerId,
                row.ConfirmerCoreLinkedWorkerId, row.ApprovedByCoreLinkedWorkerId, row.RecordedByCoreLinkedWorkerId,
                row.ConfirmedByCoreLinkedWorkerId }.Any(x => !ProcurementPrincipalEvidence.OptionalWorker(x)))
                throw new InvalidOperationException("ManualPaymentStorageValuesInvalid");
            if (entry.State == EntityState.Added)
            {
                if (row.CreatorPrincipalId != scope.SubjectId || row.CreatorCoreLinkedWorkerId != scope.CoreLinkedWorkerId
                    || row.Status != "Draft" || row.Version != 1 || row.ApprovedAmount is not null
                    || row.RecordedAmount is not null || row.ConfirmedAt is not null)
                    throw new InvalidOperationException("ManualPaymentCreatorEvidenceRequired");
                var invoice = SupplierInvoices.AsNoTracking().SingleOrDefault(x => x.Id == row.SupplierInvoiceId);
                if (invoice is null || invoice.SupplierId != row.SupplierId || invoice.BranchRef != row.BranchId
                    || invoice.CurrencyCode != row.CurrencyCode)
                    throw new InvalidOperationException("ManualPaymentInvoiceBindingRequired");
                RequireManualPaymentBundle(row, "None", 0);
                continue;
            }
            var stored = ManualPaymentRequests.AsNoTracking().SingleOrDefault(x => x.Id == row.Id);
            if (stored is null || stored.Version != entry.Property(x => x.Version).OriginalValue)
                throw new DbUpdateConcurrencyException("The payment request changed or is outside this scope.");
            if (stored.CreatorPrincipalId != row.CreatorPrincipalId || stored.CreatorCoreLinkedWorkerId != row.CreatorCoreLinkedWorkerId
                || stored.BranchId != row.BranchId || stored.SupplierId != row.SupplierId
                || stored.SupplierInvoiceId != row.SupplierInvoiceId || stored.CurrencyCode != row.CurrencyCode
                || stored.CreatedAt != row.CreatedAt || stored.CreatedBy != row.CreatedBy)
                throw new InvalidOperationException("ManualPaymentSourceEvidenceImmutable");
            RequireManualPaymentTransitionActor(stored, row);
            RequireManualPaymentBundle(row, stored.Status, stored.Version);
            if (stored.ApprovedAt is not null && (stored.ApprovedAmount != row.ApprovedAmount
                || stored.ApprovedByPrincipalId != row.ApprovedByPrincipalId || stored.ApprovedAt != row.ApprovedAt
                || stored.ApprovedByCoreLinkedWorkerId != row.ApprovedByCoreLinkedWorkerId))
                throw new InvalidOperationException("ManualPaymentApprovalEvidenceImmutable");
            if (stored.Status is "ConfirmedManually" or "Rejected" or "Cancelled" or "Disputed")
                throw new InvalidOperationException("ManualPaymentTerminalImmutable");
            if (stored.Status != "Draft" && (stored.Amount != row.Amount || stored.Purpose != row.Purpose
                || stored.ApproverPrincipalId != row.ApproverPrincipalId || stored.ApproverCoreLinkedWorkerId != row.ApproverCoreLinkedWorkerId
                || stored.RecorderPrincipalId != row.RecorderPrincipalId || stored.RecorderCoreLinkedWorkerId != row.RecorderCoreLinkedWorkerId
                || stored.ConfirmerPrincipalId != row.ConfirmerPrincipalId || stored.ConfirmerCoreLinkedWorkerId != row.ConfirmerCoreLinkedWorkerId))
                throw new InvalidOperationException("ManualPaymentSubmittedTermsImmutable");
            if (stored.Status == "PaymentRecorded" && (stored.RecordedByPrincipalId != row.RecordedByPrincipalId
                || stored.RecordedByCoreLinkedWorkerId != row.RecordedByCoreLinkedWorkerId || stored.RecordedAmount != row.RecordedAmount
                || stored.RecordedAt != row.RecordedAt || stored.PaidOn != row.PaidOn
                || stored.ExternalMethod != row.ExternalMethod || stored.ExternalReference != row.ExternalReference
                || stored.EvidenceDocumentRefId != row.EvidenceDocumentRefId))
                throw new InvalidOperationException("ManualPaymentRecordedEvidenceImmutable");
        }
        foreach (var entry in ChangeTracker.Entries<ManualPaymentDecision>()
            .Where(x => x.State is EntityState.Added or EntityState.Modified or EntityState.Deleted))
        {
            if (entry.State != EntityState.Added) throw new InvalidOperationException("ManualPaymentDecisionAppendOnly");
            var row = entry.Entity; ManualPaymentWriteScope(row.BranchId);
            if (!ChangeTracker.Entries<ManualPaymentRequest>().Any(x => x.Entity.Id == row.ManualPaymentRequestId
                && x.State is EntityState.Added or EntityState.Modified))
                throw new InvalidOperationException("ManualPaymentTransitionRequestRequired");
            if (row.ActorPrincipalId != scope.SubjectId || row.ActorCoreLinkedWorkerId != scope.CoreLinkedWorkerId
                || row.FromVersion < 0 || row.ToVersion != row.FromVersion + 1)
                throw new InvalidOperationException("ManualPaymentDecisionActorEvidenceRequired");
        }
        foreach (var entry in ChangeTracker.Entries<ManualPaymentCommandReceipt>()
            .Where(x => x.State is EntityState.Added or EntityState.Modified or EntityState.Deleted))
        {
            if (entry.State != EntityState.Added) throw new InvalidOperationException("ManualPaymentReceiptAppendOnly");
            var row = entry.Entity; ManualPaymentWriteScope(row.BranchId);
            if (row.ActorPrincipalId != scope.SubjectId || row.CommandKey == Guid.Empty || row.ResultVersion <= 0
                || row.RequestSha256 is not { Length: 64 } || !Regex.IsMatch(row.RequestSha256, "^[0-9a-f]{64}$"))
                throw new InvalidOperationException("ManualPaymentCommandReceiptInvalid");
            if (!ChangeTracker.Entries<ManualPaymentDecision>().Any(x => x.State == EntityState.Added
                && x.Entity.ManualPaymentRequestId == row.ManualPaymentRequestId && x.Entity.BranchId == row.BranchId
                && x.Entity.ToVersion == row.ResultVersion && x.Entity.Action == row.Action))
                throw new InvalidOperationException("ManualPaymentReceiptTransitionRequired");
            using var json = JsonDocument.Parse(row.ResponseJson);
            var body = json.RootElement;
            var parent = ChangeTracker.Entries<ManualPaymentRequest>().Single(x => x.Entity.Id == row.ManualPaymentRequestId).Entity;
            if (body.ValueKind != JsonValueKind.Object
                || !body.TryGetProperty("id", out var id) || id.ValueKind != JsonValueKind.String || !id.TryGetGuid(out var parsed) || parsed != row.ManualPaymentRequestId
                || !body.TryGetProperty("version", out var version) || version.ValueKind != JsonValueKind.Number || !version.TryGetInt64(out var parsedVersion) || parsedVersion != row.ResultVersion
                || !body.TryGetProperty("status", out var status) || status.ValueKind != JsonValueKind.String || status.GetString() != parent.Status)
                throw new InvalidOperationException("ManualPaymentCommandReceiptInvalid");
        }
    }

    private void RequireManualPaymentBundle(ManualPaymentRequest row, string fromState, long fromVersion)
    {
        var decisions = ChangeTracker.Entries<ManualPaymentDecision>().Where(x => x.State == EntityState.Added
            && x.Entity.ManualPaymentRequestId == row.Id).Select(x => x.Entity).ToArray();
        if (decisions.Length != 1 || decisions[0].BranchId != row.BranchId || decisions[0].FromStatus != fromState
            || decisions[0].ToStatus != row.Status || decisions[0].FromVersion != fromVersion || decisions[0].ToVersion != fromVersion + 1)
            throw new InvalidOperationException("ManualPaymentAtomicTransitionRequired");
        var expectedAction = (fromState, row.Status) switch {
            ("None", "Draft") => "Create", ("Draft", "Draft") => "Revise", ("Draft", "PendingApproval") => "Submit",
            ("PendingApproval", "Approved") => "Approve", ("PendingApproval", "Draft") => "Return", ("PendingApproval", "Rejected") => "Reject",
            ("Approved", "PaymentRecorded") => "Record", ("PaymentRecorded", "ConfirmedManually") => "Confirm",
            ("Draft" or "PendingApproval" or "Approved", "Cancelled") => "Cancel", ("PaymentRecorded", "Disputed") => "Dispute",
            _ => ""
        };
        if (decisions[0].Action != expectedAction) throw new InvalidOperationException("ManualPaymentTransitionActionInvalid");
        var receipts = ChangeTracker.Entries<ManualPaymentCommandReceipt>().Where(x => x.State == EntityState.Added
            && x.Entity.ManualPaymentRequestId == row.Id).Select(x => x.Entity).ToArray();
        if (receipts.Length != 1 || receipts[0].BranchId != row.BranchId || receipts[0].ResultVersion != decisions[0].ToVersion
            || receipts[0].Action != decisions[0].Action)
            throw new InvalidOperationException("ManualPaymentAtomicReceiptRequired");
    }

    private void RequireManualPaymentTransitionActor(ManualPaymentRequest stored, ManualPaymentRequest next)
    {
        var allowed = (stored.Status, next.Status) switch {
            ("Draft", "Draft" or "PendingApproval") => scope.SubjectId == stored.CreatorPrincipalId,
            ("Draft" or "PendingApproval" or "Approved", "Cancelled") => scope.SubjectId == stored.CreatorPrincipalId || scope.SubjectId == stored.ApproverPrincipalId,
            ("PendingApproval", "Approved" or "Draft" or "Rejected") => scope.SubjectId == stored.ApproverPrincipalId,
            ("Approved", "PaymentRecorded") => scope.SubjectId == stored.RecorderPrincipalId,
            ("PaymentRecorded", "ConfirmedManually" or "Disputed") => scope.SubjectId == stored.ConfirmerPrincipalId,
            _ => false
        };
        if (!allowed) throw new InvalidOperationException("ManualPaymentTransitionActorInvalid");
        if (next.Status == "Approved" && (next.ApprovedByPrincipalId != scope.SubjectId || next.ApprovedByCoreLinkedWorkerId != scope.CoreLinkedWorkerId)
            || next.Status == "PaymentRecorded" && (next.RecordedByPrincipalId != scope.SubjectId || next.RecordedByCoreLinkedWorkerId != scope.CoreLinkedWorkerId)
            || next.Status == "ConfirmedManually" && (next.ConfirmedByPrincipalId != scope.SubjectId || next.ConfirmedByCoreLinkedWorkerId != scope.CoreLinkedWorkerId))
            throw new InvalidOperationException("ManualPaymentTransitionPersonEvidenceRequired");
    }

    private void ManualPaymentWriteScope(Guid branch)
    {
        if (!ProcurementPrincipalEvidence.Canonical(scope.SubjectId) || branch == Guid.Empty
            || !ProcurementPrincipalEvidence.OptionalWorker(scope.CoreLinkedWorkerId)
            || scope.WorkLocationId is { } selected && selected != branch
            || scope.IsConfined && scope.WorkLocationId is null || scope.OrgUnitId is not null)
            throw new InvalidOperationException("ManualPaymentWriteScopeInvalid");
    }
}
