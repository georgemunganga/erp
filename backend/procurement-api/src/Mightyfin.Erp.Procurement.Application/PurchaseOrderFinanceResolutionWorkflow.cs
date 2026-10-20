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
                || item.Source.Line.AllocatedQuantity != item.Source.Allocations.Where(x => x.Status == "Reserved").Sum(x => x.Quantity)
                || item.Allocation.Quantity != item.OrderLine.Quantity) Fail("OrderFinanceReleaseAllocationChanged");
        var outcome = order.Status == "Returned" ? "ReopenedDraft" : "CancelledRejectedOrder";
        var resolution = new PurchaseOrderFinanceResolution { TenantId = order.TenantId, LegalEntityId = order.LegalEntityId,
            PurchaseOrderId = order.Id, SubmissionId = pin.Id, FinanceEvidenceId = finance.Id,
            CommitmentReference = finance.CommitmentReference, EvidenceReference = evidenceReference,
            ApprovedManualPolicyReference = policyReference, SourceReservationsJson = finance.SourceReservationsJson,
            ReleasedGrossAmount = releasedGross, CurrencyCode = currency, Outcome = outcome, Reason = reason,
            VerifiedByPrincipalId = verifier, VerifiedByWorkerId = verifierWorker,
            ConfirmedAt = ProcurementEvidenceEncoding.DatabaseTimestamp(confirmedAt)!.Value };
        if (order.Status == "Rejected")
            foreach (var item in lines)
            {
                item.Source.Line.AllocatedQuantity -= item.Allocation.Quantity;
                item.Source.Request.UpdatedAt = now;
                item.Allocation.Status = "Released"; item.Allocation.Reason = reason;
            }
        order.Status = outcome == "ReopenedDraft" ? "Draft" : "Cancelled";
        order.FinanceCommitmentRef = null; order.ApprovalInstanceRef = null;
        order.SubmittedBusinessVersion = null; order.SubmittedAt = null; order.UpdatedAt = now;
        if (order.Status == "Cancelled") { order.ClosedAt = now; order.ClosureReason = reason; }
        return resolution;
    }
    private static void Fail(string code) => throw new InvalidOperationException(code);
}
