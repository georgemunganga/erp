using System.Text.Json;
using Mightyfin.Erp.Core.Approvals.Domain;
using Mightyfin.Erp.Procurement.Domain;

namespace Mightyfin.Erp.Procurement.Application;

public sealed record ApprovedPurchaseSource(PurchaseRequest Request, PurchaseRequestLine Line,
    PurchaseRequestApprovalSubmission Submission, PurchaseRequestApprovalOutcome Outcome,
    ApprovalCase CoreCase, ApprovalDecision FinalDecision, PolicyEvaluation Evaluation,
    PolicyVersion Policy, BudgetCheckSnapshot Finance, ProcurementMethod Method,
    IReadOnlyList<PurchaseRequestAllocation> Allocations);
public sealed record PurchaseSourceAvailability(decimal AvailableQuantity, decimal ApprovedUnitPrice,
    decimal ApprovedLineBase, string CurrencyCode, Guid SourceSubmissionId, Guid SourceCaseId);

/// <summary>
/// Procurement quantity invariants only. Caller resolves current identity/permissions and
/// locks source requests, then PO, in a single shared SERIALIZABLE transaction before loading
/// the complete allocation ledger and saving these effects with audit/outbox/retry receipts.
/// </summary>
public static class PurchaseOrderSourceAllocation
{
    public static PurchaseSourceAvailability Check(ApprovedPurchaseSource source,
        string tenant, Guid company, DateTimeOffset at)
    {
        var request = source.Request; var line = source.Line;
        if (request.TenantId != tenant || request.LegalEntityId != company || line.TenantId != tenant
            || line.LegalEntityId != company || line.PurchaseRequestId != request.Id
            || request.Lines.Count(x => x.Id == line.Id) != 1
            || !ReferenceEquals(request.Lines.Single(x => x.Id == line.Id), line)) Fail("OrderSourceScopeMismatch");
        if (request.Status is not ("Approved" or "PartiallyOrdered" or "Ordered")
            || request.ApprovalState != "Approved" || line.ApprovalStatus != "Approved"
            || line.ApprovedQuantity <= 0 || line.ApprovedQuantity > line.RequestedQuantity
            || line.Route is not ("Unassigned" or "DirectPurchase"))
            Fail("OrderSourceNotApproved");
        var pin = source.Submission; var outcome = source.Outcome; var core = source.CoreCase;
        var decision = source.FinalDecision;
        if (!PurchaseRequestApprovalPinning.VerifyBusinessEvidence(pin, request, source.Evaluation,
                source.Policy, source.Finance, tenant, company).IsValid
            || outcome.TenantId != tenant || outcome.LegalEntityId != company
            || outcome.PurchaseRequestId != request.Id || outcome.SubmissionId != pin.Id
            || outcome.SubmittedBusinessVersion != pin.SubmittedBusinessVersion
            || outcome.CoreApprovalCaseId != pin.CoreApprovalCaseId || outcome.Outcome != "Approved"
            || core.Id != pin.CoreApprovalCaseId || core.TenantId != tenant || core.LegalEntityId != company
            || core.SourceModule != "procurement" || core.SourceType != "purchase_request"
            || core.SourceId != request.Id || core.SourceVersion != pin.SubmittedBusinessVersion || core.State != "Approved"
            || core.PolicyVersionId != pin.PolicyVersionId || core.PolicyVersionNumber != pin.PolicyVersionNumber
            || core.MakerSubjectId != request.CreatedBy || core.CompletedAt is null || at < core.CompletedAt
            || decision.Id != outcome.CoreDecisionId || decision.CaseId != core.Id || decision.Action != "Approve"
            || decision.TenantId != tenant || decision.LegalEntityId != company
            || decision.ActorSubjectId == core.MakerSubjectId || outcome.DecisionCommandKey != decision.IdempotencyKey
            || core.CompletionKey != decision.IdempotencyKey)
            Fail("OrderSourceAuthorityMismatch");
        if (source.Method.TenantId != tenant || source.Method.LegalEntityId != company
            || source.Method.Status != "Active" || source.Method.ExecutionRoute != "DirectPurchase")
            Fail("OrderSourceMethodNotDirect");
        using (var json = JsonDocument.Parse(source.Evaluation.DecisionJson))
        {
            var matched = json.RootElement.GetProperty("lines").EnumerateArray()
                .Where(x => x.GetProperty("lineId").GetGuid() == line.Id).ToArray();
            if (matched.Length != 1) Fail("OrderSourcePolicyLineMismatch");
            var route = matched[0].GetProperty("decision");
            if (route.GetProperty("status").GetString() != "Match"
                || !route.TryGetProperty("sourcingMethodCode", out var methodCode)
                || methodCode.ValueKind != JsonValueKind.String || methodCode.GetString() != source.Method.Code
                || !route.TryGetProperty("minimumQuoteCount", out var quoteCount)
                || quoteCount.ValueKind != JsonValueKind.Number || !quoteCount.TryGetInt32(out var quotes) || quotes != 0)
                Fail("OrderSourceCompetitionRequired");
        }
        var ledger = source.Allocations;
        if (ledger.Any(x => x.TenantId != tenant || x.LegalEntityId != company
                || x.PurchaseRequestId != request.Id || x.PurchaseRequestLineId != line.Id
                || x.CurrencyCode != request.CurrencyCode || x.Quantity <= 0
                || x.Status is not ("Reserved" or "Ordered" or "Released")
                || x.TargetType != "PurchaseOrder" || x.TargetId is null)
            || ledger.Select(x => x.Id).Distinct().Count() != ledger.Count
            || ExactDecimalArithmetic.Sum(ledger.Where(x => x.Status == "Reserved").Select(x => x.Quantity)) != line.AllocatedQuantity
            || ExactDecimalArithmetic.Sum(ledger.Where(x => x.Status == "Ordered").Select(x => x.Quantity)) != line.OrderedQuantity
            || line.CancelledQuantity < 0 || line.AcceptedQuantity < 0 || line.AcceptedQuantity > line.OrderedQuantity)
            Fail("OrderSourceAllocationUnreconciled");
        var available = ExactDecimalArithmetic.Sum(new[] { line.ApprovedQuantity, -line.CancelledQuantity, -line.AllocatedQuantity, -line.OrderedQuantity });
        if (available < 0) Fail("OrderSourceOverallocated");
        return new(available, line.EstimatedUnitPrice, ExactDecimalArithmetic.Multiply(line.ApprovedQuantity, line.EstimatedUnitPrice),
            request.CurrencyCode, pin.Id, core.Id);
    }

    public static PurchaseRequestAllocation Reserve(ApprovedPurchaseSource source, PurchaseOrder order,
        PurchaseOrderLine line, decimal quantity, Guid commandKey, string actor, DateTimeOffset now)
    {
        var available = Check(source, order.TenantId, order.LegalEntityId, now);
        if (order.Status != "Draft" || line.PurchaseOrderId != order.Id || !line.IsActive
            || line.PurchaseRequestLineId != source.Line.Id || order.CurrencyCode != available.CurrencyCode
            || !string.Equals(line.UnitCode, source.Line.UnitCode, StringComparison.OrdinalIgnoreCase)
            || commandKey == Guid.Empty || string.IsNullOrWhiteSpace(actor)
            || quantity <= 0 || decimal.Round(quantity, 4) != quantity || quantity > available.AvailableQuantity
            || line.Quantity != quantity || line.UnitPrice < 0 || decimal.Round(line.UnitPrice, 4) != line.UnitPrice)
            Fail("OrderSourceAllocationInvalid");
        var key = $"order.reserve.{commandKey:N}.{source.Line.Id:N}";
        if (source.Allocations.Any(x => x.IdempotencyKey == key || x.TargetId == order.Id && x.Status != "Released"))
            Fail("OrderSourceAllocationAlreadyRecorded");
        // Compute every fallible value before touching the tracked aggregate.
        var nextAllocated = ExactDecimalArithmetic.Add(source.Line.AllocatedQuantity, quantity);
        var estimatedValue = ExactDecimalArithmetic.Multiply(quantity, line.UnitPrice);
        source.Request.UpdatedAt = now;
        source.Line.Route = "DirectPurchase";
        source.Line.AllocatedQuantity = nextAllocated;
        return new PurchaseRequestAllocation
        {
            TenantId = order.TenantId, LegalEntityId = order.LegalEntityId,
            PurchaseRequestId = source.Request.Id, PurchaseRequestLineId = source.Line.Id,
            Method = source.Method.Code, Status = "Reserved", TargetType = "PurchaseOrder", TargetId = order.Id,
            Quantity = quantity, EstimatedValue = estimatedValue, CurrencyCode = order.CurrencyCode,
            DecidedBy = actor, DecidedAt = now, IdempotencyKey = key,
        };
    }

    public static void ResizeReservation(ApprovedPurchaseSource source, PurchaseOrder order,
        PurchaseOrderLine line, PurchaseRequestAllocation allocation, decimal quantity, string reason, DateTimeOffset now)
    {
        var available = Check(source, order.TenantId, order.LegalEntityId, now);
        RequireAllocation(source, order, allocation, "Reserved");
        if (order.Status is not ("Draft" or "Returned") || quantity <= 0 || decimal.Round(quantity, 4) != quantity
            || quantity > ExactDecimalArithmetic.Add(available.AvailableQuantity, allocation.Quantity) || string.IsNullOrWhiteSpace(reason)
            || line.PurchaseRequestLineId != source.Line.Id || line.PurchaseOrderId != order.Id
            || line.UnitPrice < 0 || decimal.Round(line.UnitPrice, 4) != line.UnitPrice
            || order.CurrencyCode != source.Request.CurrencyCode
            || !string.Equals(line.UnitCode, source.Line.UnitCode, StringComparison.OrdinalIgnoreCase))
            Fail("OrderSourceResizeInvalid");
        var nextAllocated = ExactDecimalArithmetic.Sum(new[] { source.Line.AllocatedQuantity, quantity, -allocation.Quantity });
        var estimatedValue = ExactDecimalArithmetic.Multiply(quantity, line.UnitPrice);
        source.Request.UpdatedAt = now;
        source.Line.AllocatedQuantity = nextAllocated;
        allocation.Quantity = quantity;
        allocation.EstimatedValue = estimatedValue;
        allocation.Reason = reason;
        line.Quantity = quantity;
    }

    public static void ConfirmOrdered(ApprovedPurchaseSource source, PurchaseOrder order,
        PurchaseOrderLine line, PurchaseRequestAllocation allocation, DateTimeOffset now)
    {
        _ = Check(source, order.TenantId, order.LegalEntityId, now);
        RequireAllocation(source, order, allocation, "Reserved");
        if (order.Status != "Approved" || line.PurchaseOrderId != order.Id
            || line.PurchaseRequestLineId != source.Line.Id || allocation.Quantity != line.Quantity)
            Fail("OrderSourceIssueMismatch");
        var nextAllocated = ExactDecimalArithmetic.Subtract(source.Line.AllocatedQuantity, allocation.Quantity);
        var nextOrdered = ExactDecimalArithmetic.Add(source.Line.OrderedQuantity, allocation.Quantity);
        source.Request.UpdatedAt = now;
        source.Line.AllocatedQuantity = nextAllocated;
        source.Line.OrderedQuantity = nextOrdered;
        allocation.Status = "Ordered";
    }

    public static void ReleaseDraft(ApprovedPurchaseSource source, PurchaseOrder order,
        PurchaseRequestAllocation allocation, string reason, DateTimeOffset now)
    {
        _ = Check(source, order.TenantId, order.LegalEntityId, now);
        RequireAllocation(source, order, allocation, "Reserved");
        if (order.Status is not ("Draft" or "Returned" or "Rejected") || string.IsNullOrWhiteSpace(reason)
            || order.FinanceCommitmentRef is not null) Fail("OrderFinanceReleaseRequired");
        var nextAllocated = ExactDecimalArithmetic.Subtract(source.Line.AllocatedQuantity, allocation.Quantity);
        source.Request.UpdatedAt = now;
        source.Line.AllocatedQuantity = nextAllocated;
        allocation.Status = "Released"; allocation.Reason = reason;
    }

    private static void RequireAllocation(ApprovedPurchaseSource source, PurchaseOrder order,
        PurchaseRequestAllocation allocation, string status)
    {
        if (allocation.TenantId != order.TenantId || allocation.LegalEntityId != order.LegalEntityId
            || allocation.PurchaseRequestId != source.Request.Id || allocation.PurchaseRequestLineId != source.Line.Id
            || allocation.TargetType != "PurchaseOrder" || allocation.TargetId != order.Id || allocation.Status != status
            || source.Allocations.Count(x => x.Id == allocation.Id && ReferenceEquals(x, allocation)) != 1)
            Fail("OrderSourceAllocationMismatch");
    }
    private static void Fail(string code) => throw new InvalidOperationException(code);
}
