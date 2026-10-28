using Mightyfin.Erp.Procurement.Domain;
using System.Text.Json;
namespace Mightyfin.Erp.Procurement.Application;

/// <summary>Normalized projection parity only. The owning store must independently
/// validate retained principal commercial/Core evidence before calling this check.</summary>
public static class InvoiceUnitReviewPrincipalIssuedProjection
{
    public static void Verify(InvoiceIssuedOrder projected, PurchaseOrder order,
        PrincipalOrderIssuedVersion issued, IReadOnlyList<PurchaseOrderLine> lines,
        IReadOnlyDictionary<Guid, string> categories)
    {
        if (order.Id == Guid.Empty || issued.Id == Guid.Empty || order.SupplierId == Guid.Empty
            || order.BranchRef is null || order.BranchRef == Guid.Empty || string.IsNullOrWhiteSpace(order.TenantId)
            || order.LegalEntityId == Guid.Empty || string.IsNullOrWhiteSpace(order.CurrencyCode)
            || issued.AuthorizedAt == default || order.PrincipalDraftSchemaVersion != 2 || order.Status != "Issued"
            || issued.SchemaVersion != 2 || issued.PurchaseOrderId != order.Id || issued.CommercialRevision != 1
            || issued.TenantId != order.TenantId || issued.LegalEntityId != order.LegalEntityId
            || projected.Id != order.Id || projected.RevisionId != issued.Id
            || projected.RevisionNumber != issued.CommercialRevision || projected.RevisionState != "Issued"
            || projected.Version != order.Version
            || projected.TenantId != order.TenantId || projected.LegalEntityId != order.LegalEntityId
            || projected.BranchRef != order.BranchRef || projected.SupplierId != order.SupplierId
            || projected.CurrencyCode != order.CurrencyCode || projected.IssuedAt != issued.AuthorizedAt
            || projected.IssuedReference != issued.CommercialSnapshotSha256
            || lines.Count is < 1 or > 100 || projected.Lines.Count != lines.Count
            || lines.Select(x => x.Id).Distinct().Count() != lines.Count
            || projected.Lines.Select(x => x.Id).Distinct().Count() != lines.Count
            || categories.Count != lines.Count
            || lines.Any(line => line.PurchaseOrderId != order.Id || !line.IsActive
                || line.TenantId != order.TenantId || line.LegalEntityId != order.LegalEntityId
                || !categories.TryGetValue(line.Id, out var category) || string.IsNullOrWhiteSpace(category)
                || !projected.Lines.Any(p => p.Id == line.Id && p.Version == line.Version
                    && p.Quantity == line.Quantity && p.UnitPrice == line.UnitPrice && p.TaxAmount == line.TaxAmount
                    && p.UnitCode == line.UnitCode && p.CategoryCode == category)))
            throw new InvalidOperationException("InvoiceUnitReviewPrincipalIssuedProjectionMismatch");
    }
    /// <summary>Only call with the independently revalidated retained demand snapshot
    /// and full owning allocation ledger. Current editable PR labels are not used.</summary>
    public static string RetainedCategory(PurchaseOrder order, PurchaseOrderLine line,
        PrincipalOrderSourceBinding binding, IReadOnlyList<PurchaseRequestAllocation> allocations,
        string requestSnapshotJson)
    {
        var allocation = allocations.SingleOrDefault(x => x.Id == binding.AllocationId);
        if (binding.PurchaseOrderId != order.Id || binding.PurchaseOrderLineId != line.Id
            || binding.PurchaseRequestLineId != line.PurchaseRequestLineId || binding.BranchId != order.BranchRef
            || binding.TenantId != order.TenantId || binding.LegalEntityId != order.LegalEntityId
            || allocation is null || allocation.Status != "Ordered" || allocation.TargetType != "PurchaseOrder"
            || allocation.TargetId != order.Id || allocation.PurchaseRequestId != binding.PurchaseRequestId
            || allocation.PurchaseRequestLineId != binding.PurchaseRequestLineId
            || allocation.TenantId != order.TenantId || allocation.LegalEntityId != order.LegalEntityId
            || allocation.Quantity != binding.Quantity || allocation.EstimatedValue != binding.EstimatedValue
            || allocation.CurrencyCode != binding.CurrencyCode)
            throw new InvalidOperationException("InvoiceUnitReviewPrincipalDemandAllocationRequired");
        try
        {
            using var snapshot = JsonDocument.Parse(requestSnapshotJson);
            var pinned = snapshot.RootElement.GetProperty("legacyBusiness").GetProperty("lines").EnumerateArray()
                .Where(x => x.GetProperty("Id").GetGuid() == binding.PurchaseRequestLineId).ToArray();
            if (pinned.Length != 1 || string.IsNullOrWhiteSpace(pinned[0].GetProperty("CategoryCode").GetString()))
                throw new InvalidOperationException("InvoiceUnitReviewPrincipalDemandSnapshotRequired");
            return pinned[0].GetProperty("CategoryCode").GetString()!;
        }
        catch (Exception error) when (error is JsonException or KeyNotFoundException)
        {
            throw new InvalidOperationException("InvoiceUnitReviewPrincipalDemandSnapshotRequired", error);
        }
    }

}
