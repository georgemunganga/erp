using System.Text.Json;
using Mightyfin.Erp.Core.Approvals.Domain;
using Mightyfin.Erp.Procurement.Domain;

namespace Mightyfin.Erp.Procurement.Application;

/// <summary>A server-authorized, current independent Finance attestation, never an accounting entry.
/// Caller locks complete source ledgers/order/Core case and checks deployed manual evidence policy.</summary>
public static class PurchaseOrderFinanceResolutionWorkflow
{
    public static PurchaseOrderFinanceResolution Resolve(PurchaseOrder order, IReadOnlyList<PurchaseOrderSourceLine> lines,
        PurchaseOrderSubmission pin, PolicyVersion policy, PurchaseOrderFinanceEvidence finance, ApprovalCase workflow,
        IReadOnlyList<PurchaseOrderAssignmentAuthorityEvidence> assignments, string evidenceReference,
        string policyReference, decimal releasedGross, string currency, IReadOnlyList<string> restoredReservations,
        string verifier, Guid verifierWorker, string reason, DateTimeOffset confirmedAt, DateTimeOffset now)
    {
        if (order.Status is not ("Returned" or "Rejected") || workflow.State != order.Status
            || workflow.Id != pin.CoreApprovalCaseId || workflow.SourceId != order.Id
            || workflow.SourceModule != "procurement" || workflow.SourceType != "purchase_order"
            || workflow.TenantId != order.TenantId || workflow.LegalEntityId != order.LegalEntityId
            || workflow.SourceVersion != pin.CommercialRevision || pin.PurchaseOrderId != order.Id
            || pin.TenantId != order.TenantId || pin.LegalEntityId != order.LegalEntityId
            || pin.FinanceEvidenceId != finance.Id || finance.TenantId != order.TenantId
            || finance.LegalEntityId != order.LegalEntityId || finance.PurchaseOrderId != order.Id
            || order.FinanceCommitmentRef != finance.CommitmentReference
            || releasedGross != pin.GrossAmount || releasedGross != finance.ConfirmedGrossAmount
            || currency != order.CurrencyCode || currency != finance.CurrencyCode
            || confirmedAt == default || confirmedAt > now || workflow.CompletedAt is null || confirmedAt < workflow.CompletedAt
            || new[] { evidenceReference, policyReference, verifier, reason }.Any(string.IsNullOrWhiteSpace)
            || !PurchaseOrderWorkerIdentity.Verify(pin, order, lines)
            || !PurchaseOrderWorkerIdentity.IsIndependent(pin.ParticipantWorkersJson, verifierWorker))
            Fail("OrderFinanceReleaseEvidenceInvalid");
        PurchaseOrderCommercialWorkflow.VerifyForReturn(order, lines, pin, policy, finance);
        var tasks = workflow.Stages.SelectMany(x => x.Assignments).ToArray();
        if (assignments.Count != tasks.Length || tasks.Any(x => !assignments.Any(a => a.CoreAssignmentId == x.Id
            && a.CoreStageId == x.StageId && a.PrincipalId == x.AssigneeSubjectId))) Fail("OrderFinanceReleaseAssignmentSetChanged");
        foreach (var assignment in assignments)
            if (assignment.SubmissionId != pin.Id || assignment.CoreApprovalCaseId != workflow.Id
                || PurchaseOrderAssignmentIdentity.ReadVerified(assignment) is not { } retained
                || retained.AssigneeWorkerId == verifierWorker) Fail("OrderFinanceReleaseIndependenceRequired");
        var expected = JsonSerializer.Deserialize<string[]>(finance.SourceReservationsJson) ?? [];
        if (expected.Length == 0 || restoredReservations.Count != expected.Length
            || restoredReservations.Distinct(StringComparer.Ordinal).Count() != restoredReservations.Count
            || !expected.Order(StringComparer.Ordinal).SequenceEqual(restoredReservations.Order(StringComparer.Ordinal)))
            Fail("OrderFinanceReservationRestorationRequired");
        // Check all ledgers before changing one; disabled current sourcing policies must
        // not prevent closing a rejected commercial obligation after Finance releases it.
        foreach (var item in lines)
            if (item.Allocation.Status != "Reserved" || item.Allocation.TargetId != order.Id
                || item.Allocation.PurchaseRequestLineId != item.Source.Line.Id
                || item.Allocation.TenantId != order.TenantId || item.Allocation.LegalEntityId != order.LegalEntityId
                || item.Source.Line.AllocatedQuantity != ExactDecimalArithmetic.Sum(item.Source.Allocations.Where(x => x.Status == "Reserved").Select(x => x.Quantity))
                || item.Allocation.Quantity != item.OrderLine.Quantity) Fail("OrderFinanceReleaseAllocationChanged");
        var outcome = order.Status == "Returned" ? "ReopenedDraft" : "CancelledRejectedOrder";
        var resolution = new PurchaseOrderFinanceResolution { TenantId = order.TenantId, LegalEntityId = order.LegalEntityId,
            PurchaseOrderId = order.Id, SubmissionId = pin.Id, FinanceEvidenceId = finance.Id,
            CommitmentReference = finance.CommitmentReference, EvidenceReference = evidenceReference,
            ApprovedManualPolicyReference = policyReference, SourceReservationsJson = finance.SourceReservationsJson,
            ReleasedGrossAmount = releasedGross, CurrencyCode = currency, Outcome = outcome, Reason = reason,
            VerifiedByPrincipalId = verifier, VerifiedByWorkerId = verifierWorker,
            ConfirmedAt = ProcurementEvidenceEncoding.DatabaseTimestamp(confirmedAt)!.Value };
        if (order.Status == "Rejected") ReleaseReservedSources(lines, reason, now);
        order.Status = outcome == "ReopenedDraft" ? "Draft" : "Cancelled";
        order.FinanceCommitmentRef = null; order.ApprovalInstanceRef = null;
        order.SubmittedBusinessVersion = null; order.SubmittedAt = null; order.UpdatedAt = now;
        if (order.Status == "Cancelled") { order.ClosedAt = now; order.ClosureReason = reason; }
        return resolution;
    }
    public static void CancelUnfundedDraft(PurchaseOrder order, IReadOnlyList<PurchaseOrderSourceLine> lines,
        PurchaseOrderFinanceEvidence? latestFinance, PurchaseOrderFinanceResolution? previousResolution,
        string reason, DateTimeOffset now)
    {
        if (order.Status != "Draft" || order.FinanceCommitmentRef is not null || string.IsNullOrWhiteSpace(reason)
            || order.ApprovalInstanceRef is not null || latestFinance is not null && (previousResolution is null
                || previousResolution.FinanceEvidenceId != latestFinance.Id || previousResolution.PurchaseOrderId != order.Id
                || previousResolution.TenantId != order.TenantId || previousResolution.LegalEntityId != order.LegalEntityId))
            Fail("OrderDraftStillHasFinanceCommitment");
        ValidateReservedSources(order, lines);
        ReleaseReservedSources(lines, reason, now);
        order.Status = "Cancelled"; order.ClosedAt = now; order.ClosureReason = reason; order.UpdatedAt = now;
    }

    public static PurchaseOrderFinanceResolution ResolveUnsubmittedDraft(PurchaseOrder order,
        IReadOnlyList<PurchaseOrderSourceLine> lines, PurchaseOrderFinanceEvidence finance,
        string evidenceReference, string policyReference, decimal releasedGross, string currency,
        IReadOnlyList<string> restoredReservations, string verifier, Guid verifierWorker, string reason,
        DateTimeOffset confirmedAt, DateTimeOffset now)
    {
        if (order.Status != "Draft" || order.ApprovalInstanceRef is not null
            || finance.PurchaseOrderId != order.Id || finance.TenantId != order.TenantId
            || finance.LegalEntityId != order.LegalEntityId || finance.CommercialRevision != order.CurrentRevision + 1
            || finance.CommitmentReference != order.FinanceCommitmentRef || releasedGross != finance.ConfirmedGrossAmount
            || currency != finance.CurrencyCode || currency != order.CurrencyCode
            || finance.CommercialSnapshotSha256 != ProcurementEvidenceEncoding.Digest(finance.CommercialSnapshotJson)
            || finance.WorkerIdentitySha256 != PurchaseOrderWorkerIdentity.Digest(order, finance.CommercialRevision,
                Guid.Empty, finance.BuyerWorkerId, finance.ParticipantWorkersJson)
            || finance.ParticipantWorkersJson != PurchaseOrderWorkerIdentity.Participants(lines, finance.BuyerWorkerId)
            || !PurchaseOrderWorkerIdentity.IsIndependent(finance.ParticipantWorkersJson, verifierWorker)
            || confirmedAt < finance.ConfirmedAt || confirmedAt > now
            || new[] { evidenceReference, policyReference, verifier, reason }.Any(string.IsNullOrWhiteSpace))
            Fail("OrderUnsubmittedFinanceReleaseInvalid");
        using var snapshot = JsonDocument.Parse(finance.CommercialSnapshotJson);
        var supplier = new Supplier { Id = order.SupplierId, TenantId = order.TenantId, LegalEntityId = order.LegalEntityId,
            LegalName = snapshot.RootElement.GetProperty("LegalName").GetString()!,
            DisplayName = snapshot.RootElement.GetProperty("DisplayName").GetString()!,
            CountryCode = snapshot.RootElement.GetProperty("CountryCode").GetString()! };
        if (finance.CommercialSnapshotJson != PurchaseOrderCommercialWorkflow.Snapshot(order,
            lines.Select(x => x.OrderLine).ToArray(), supplier, finance.CommercialRevision)) Fail("OrderUnsubmittedFinanceInputsChanged");
        var expected = JsonSerializer.Deserialize<string[]>(finance.SourceReservationsJson) ?? [];
        if (expected.Length == 0 || restoredReservations.Count != expected.Length
            || restoredReservations.Distinct(StringComparer.Ordinal).Count() != restoredReservations.Count
            || !expected.Order(StringComparer.Ordinal).SequenceEqual(restoredReservations.Order(StringComparer.Ordinal)))
            Fail("OrderFinanceReservationRestorationRequired");
        ValidateReservedSources(order, lines);
        var resolution = new PurchaseOrderFinanceResolution { TenantId = order.TenantId, LegalEntityId = order.LegalEntityId,
            PurchaseOrderId = order.Id, SubmissionId = null, FinanceEvidenceId = finance.Id,
            CommitmentReference = finance.CommitmentReference, EvidenceReference = evidenceReference,
            ApprovedManualPolicyReference = policyReference, SourceReservationsJson = finance.SourceReservationsJson,
            ReleasedGrossAmount = releasedGross, CurrencyCode = currency, Outcome = "CancelledUnsubmittedOrder",
            Reason = reason, VerifiedByPrincipalId = verifier, VerifiedByWorkerId = verifierWorker,
            ConfirmedAt = ProcurementEvidenceEncoding.DatabaseTimestamp(confirmedAt)!.Value };
        ReleaseReservedSources(lines, reason, now);
        order.FinanceCommitmentRef = null; order.Status = "Cancelled"; order.UpdatedAt = now;
        order.ClosedAt = now; order.ClosureReason = reason;
        return resolution;
    }

    private static void ValidateReservedSources(PurchaseOrder order, IReadOnlyList<PurchaseOrderSourceLine> lines)
    {
        if (lines.Count == 0 || lines.Select(x => x.Source.Line.Id).Distinct().Count() != lines.Count) Fail("OrderReleaseSourceSetInvalid");
        foreach (var item in lines)
            if (item.Allocation.Status != "Reserved" || item.Allocation.TargetType != "PurchaseOrder"
                || item.Allocation.TargetId != order.Id || item.Allocation.PurchaseRequestId != item.Source.Request.Id
                || item.Allocation.PurchaseRequestLineId != item.Source.Line.Id || item.Allocation.Quantity <= 0
                || item.Allocation.Quantity != item.OrderLine.Quantity || item.Allocation.TenantId != order.TenantId
                || item.Allocation.LegalEntityId != order.LegalEntityId
                || item.Source.Allocations.Count(x => x.Id == item.Allocation.Id && ReferenceEquals(x, item.Allocation)) != 1
                || item.Source.Line.AllocatedQuantity != ExactDecimalArithmetic.Sum(item.Source.Allocations.Where(x => x.Status == "Reserved").Select(x => x.Quantity))
                || !PurchaseRequestApprovalPinning.VerifyBusinessEvidence(item.Source.Submission, item.Source.Request,
                    item.Source.Evaluation, item.Source.Policy, item.Source.Finance, order.TenantId, order.LegalEntityId).IsValid)
                Fail("OrderReleaseSourceAllocationChanged");
    }
    private static void ReleaseReservedSources(IReadOnlyList<PurchaseOrderSourceLine> lines, string reason, DateTimeOffset now)
    {
        // Plan all counter results before mutating any source aggregate.
        var changes = lines.Select(item => (Item: item,
            NextAllocated: ExactDecimalArithmetic.Subtract(item.Source.Line.AllocatedQuantity, item.Allocation.Quantity))).ToArray();
        foreach (var change in changes)
        {
            change.Item.Source.Line.AllocatedQuantity = change.NextAllocated;
            change.Item.Source.Request.UpdatedAt = now;
            change.Item.Allocation.Status = "Released"; change.Item.Allocation.Reason = reason;
        }
    }
    private static void Fail(string code) => throw new InvalidOperationException(code);
}
