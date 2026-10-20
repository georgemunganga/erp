using System.Text.Json;
using Mightyfin.Erp.Core.Approvals.Domain;
using Mightyfin.Erp.Procurement.Domain;

namespace Mightyfin.Erp.Procurement.Application;

public sealed record ApprovedPurchaseSource(PurchaseRequest Request, PurchaseRequestLine Line,
    PurchaseRequestApprovalSubmission Submission, PurchaseRequestApprovalOutcome Outcome,
    ApprovalCase CoreCase, ApprovalDecision FinalDecision, PolicyEvaluation Evaluation,
    PolicyVersion Policy, BudgetCheckSnapshot Finance, ProcurementMethod Method,
    IReadOnlyList<PurchaseRequestAllocation> Allocations,
    IReadOnlyList<PurchaseDemandAllocationOwner>? AllocationOwners = null);
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
        try { ApprovedPurchaseDemand.Verify(source, tenant, company, at); }
        catch (InvalidOperationException error) when (error.Message is "ApprovedDemandScopeMismatch" or "ApprovedDemandNotApproved" or "ApprovedDemandAuthorityMismatch")
        {
            Fail(error.Message switch { "ApprovedDemandScopeMismatch" => "OrderSourceScopeMismatch",
                "ApprovedDemandNotApproved" => "OrderSourceNotApproved", _ => "OrderSourceAuthorityMismatch" });
        }
        if (line.Route is not ("Unassigned" or "DirectPurchase")) Fail("OrderSourceNotApproved");
        var pin = source.Submission; var core = source.CoreCase;
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
        decimal available;
        try { available = PurchaseDemandAllocationBalance.Evaluate(request, line, source.Allocations,
            source.AllocationOwners ?? [], tenant, company).AvailableQuantity; }
        catch (InvalidOperationException error) when (error.Message is "DemandAllocationUnreconciled" or "DemandOverallocated")
        { Fail(error.Message == "DemandOverallocated" ? "OrderSourceOverallocated" : "OrderSourceAllocationUnreconciled"); throw; }
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
            || line.Quantity != quantity || line.UnitPrice < 0 || decimal.Round(line.UnitPrice, 4) != line.UnitPrice
            || !ExactDecimalJsonCodec.IsRepresentable(quantity, 18, 4)
            || !ExactDecimalJsonCodec.IsRepresentable(line.UnitPrice, 18, 4)
            || line.TaxAmount < 0 || !ExactDecimalJsonCodec.IsRepresentable(line.TaxAmount, 18, 4))
            Fail("OrderSourceAllocationInvalid");
        var key = $"order.reserve.{commandKey:N}.{source.Line.Id:N}";
        if (source.Allocations.Any(x => x.IdempotencyKey == key || x.TargetType == "PurchaseOrder" && x.TargetId == order.Id && x.Status != "Released"))
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
            || !ExactDecimalJsonCodec.IsRepresentable(quantity, 18, 4)
            || !ExactDecimalJsonCodec.IsRepresentable(line.UnitPrice, 18, 4)
            || line.TaxAmount < 0 || !ExactDecimalJsonCodec.IsRepresentable(line.TaxAmount, 18, 4)
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
