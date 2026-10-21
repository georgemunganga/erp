using Mightyfin.Erp.Procurement.Domain;

namespace Mightyfin.Erp.Procurement.Application;

/// <summary>Owner facts derived from scoped persisted records, never a client-supplied authority flag.
/// Retained inactive lines must be loaded too: inactive does not mean released.</summary>
public sealed record PurchaseDemandAllocationOwner
{
    public string TargetType { get; }
    public Guid TargetId { get; }
    public Guid TargetLineId { get; }
    public string TenantId { get; }
    public Guid LegalEntityId { get; }
    public Guid PurchaseRequestLineId { get; }
    private PurchaseDemandAllocationOwner(string type, ProcurementRecord parent, ProcurementRecord line, Guid requestLine)
    {
        if (parent.Id == Guid.Empty || line.Id == Guid.Empty || requestLine == Guid.Empty
            || parent.TenantId != line.TenantId || parent.LegalEntityId != line.LegalEntityId)
            throw new InvalidOperationException("DemandAllocationUnreconciled");
        TargetType = type; TargetId = parent.Id; TargetLineId = line.Id; TenantId = parent.TenantId;
        LegalEntityId = parent.LegalEntityId; PurchaseRequestLineId = requestLine;
    }
    public static PurchaseDemandAllocationOwner FromOrder(PurchaseOrder order, PurchaseOrderLine line)
        => line.PurchaseOrderId == order.Id && line.PurchaseRequestLineId is { } requestLine
            ? new("PurchaseOrder", order, line, requestLine) : throw new InvalidOperationException("DemandAllocationUnreconciled");
    public static PurchaseDemandAllocationOwner FromSourcing(SourcingEvent sourcing, SourcingLine line)
        => line.SourcingEventId == sourcing.Id && line.PurchaseRequestLineId is { } requestLine
            ? new("SourcingEvent", sourcing, line, requestLine) : throw new InvalidOperationException("DemandAllocationUnreconciled");
}
public sealed record PurchaseDemandBalance(decimal AvailableQuantity, decimal ReservedQuantity, decimal OrderedQuantity);

/// <summary>One quantity reducer for the complete known-target allocation ledger. It does not
/// approve RFQ creation, bypass competition, create rows or release a Finance reservation.</summary>
public static class PurchaseDemandAllocationBalance
{
    public static PurchaseDemandBalance Evaluate(PurchaseRequest request, PurchaseRequestLine line,
        IReadOnlyList<PurchaseRequestAllocation> allocations, IReadOnlyList<PurchaseDemandAllocationOwner> owners,
        string tenant, Guid company)
    {
        if (string.IsNullOrWhiteSpace(tenant) || company == Guid.Empty || request.Id == Guid.Empty || line.Id == Guid.Empty
            || request.TenantId != tenant || request.LegalEntityId != company || line.TenantId != tenant
            || line.LegalEntityId != company || line.PurchaseRequestId != request.Id
            || allocations.Select(x => x.Id).Distinct().Count() != allocations.Count
            || allocations.Any(x => x.Id == Guid.Empty || x.TenantId != tenant || x.LegalEntityId != company
                || x.PurchaseRequestId != request.Id || x.PurchaseRequestLineId != line.Id || x.CurrencyCode != request.CurrencyCode
                || x.Quantity <= 0 || !ExactDecimalJsonCodec.IsRepresentable(x.Quantity, 18, 4)
                || x.EstimatedValue < 0 || !ExactDecimalJsonCodec.IsRepresentable(x.EstimatedValue, 38, 8) || x.TargetId is null || x.TargetId == Guid.Empty
                || x.TargetType is not ("PurchaseOrder" or "SourcingEvent")
                || x.Status is not ("Reserved" or "Released") && !(x.TargetType == "PurchaseOrder" && x.Status == "Ordered")
                || owners.Count(owner => owner.TargetType == x.TargetType && owner.TargetId == x.TargetId
                    && owner.TenantId == tenant && owner.LegalEntityId == company && owner.PurchaseRequestLineId == line.Id) != 1)
            || line.CancelledQuantity < 0 || line.AcceptedQuantity < 0 || line.AcceptedQuantity > line.OrderedQuantity)
            Fail("DemandAllocationUnreconciled");
        var reserved = ExactDecimalArithmetic.Sum(allocations.Where(x => x.Status == "Reserved").Select(x => x.Quantity));
        var ordered = ExactDecimalArithmetic.Sum(allocations.Where(x => x.TargetType == "PurchaseOrder" && x.Status == "Ordered").Select(x => x.Quantity));
        if (reserved != line.AllocatedQuantity || ordered != line.OrderedQuantity) Fail("DemandAllocationUnreconciled");
        var available = ExactDecimalArithmetic.Sum(new[] { line.ApprovedQuantity, -line.CancelledQuantity, -reserved, -ordered });
        if (available < 0) Fail("DemandOverallocated");
        return new(available, reserved, ordered);
    }
    private static void Fail(string code) => throw new InvalidOperationException(code);
}
