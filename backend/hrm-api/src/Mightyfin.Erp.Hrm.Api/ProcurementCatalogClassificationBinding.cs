using Microsoft.EntityFrameworkCore;
using Mightyfin.Erp.Procurement.Application;
using Mightyfin.Erp.Procurement.Domain;
using Mightyfin.Erp.Procurement.Infrastructure.Data;

namespace Mightyfin.Erp.Hrm.Api;

internal static class ProcurementCatalogClassificationBinding
{
    // Caller holds shared catalog session lease BEFORE its snapshot/owner reads.
    internal static async Task<string?> StoredIssue(ProcurementDbContext db,
        IEnumerable<PurchaseRequestLine> lines, CancellationToken ct, string? currency = null)
    {
        var selected = lines.Where(x => x.CatalogItemId.HasValue || x.CatalogEntryId.HasValue).ToArray();
        if (selected.Length == 0) return null;
        var ids = selected.Where(x => x.CatalogItemId.HasValue).Select(x => x.CatalogItemId!.Value).Distinct().ToArray();
        var items = await db.CatalogItems.AsNoTracking().Where(x => ids.Contains(x.Id)).ToDictionaryAsync(x => x.Id, ct);
        if (selected.Any(line => line.CatalogItemId is not Guid itemId || !items.TryGetValue(itemId, out var item)
                || CatalogRequestClassification.Bind(item, line.CategoryCode, line.PurchaseType, allowMissing: false) is null))
            return "OrderSourceCatalogClassificationUnverified";
        var entryIds = selected.Where(x => x.CatalogEntryId.HasValue).Select(x => x.CatalogEntryId!.Value).Distinct().ToArray();
        var entries = await db.CatalogEntries.AsNoTracking().Where(x => entryIds.Contains(x.Id)).ToDictionaryAsync(x => x.Id, ct);
        var now = DateTimeOffset.UtcNow;
        if (selected.Where(x => x.CatalogEntryId.HasValue).Any(line =>
                line.CatalogPublishedVersion is null || !entries.TryGetValue(line.CatalogEntryId!.Value, out var entry)
                || !CatalogRequestClassification.OfferMatches(entry, line.CatalogItemId, line.PreferredSupplierId,
                    line.UnitCode, currency ?? line.EstimatedCurrencyCode, line.RequestedQuantity,
                    line.EstimatedUnitPrice, line.CatalogPublishedVersion, now)))
            return "OrderSourceCatalogOfferUnavailable";
        return null;
    }

    internal static async Task<string?> CommercialIssue(ProcurementDbContext db, Guid supplierId, string currency,
        IEnumerable<(PurchaseRequestLine Source, PurchaseOrderLine OrderLine)> selections, CancellationToken ct)
    {
        var lines = selections.ToArray();
        var sourceIssue = await StoredIssue(db, lines.Select(x => x.Source), ct);
        if (sourceIssue is not null) return sourceIssue;
        var selected = lines.Where(x => x.Source.CatalogEntryId.HasValue).ToArray();
        if (selected.Length == 0) return null;
        var ids = selected.Select(x => x.Source.CatalogEntryId!.Value).Distinct().ToArray();
        var offers = await db.CatalogEntries.AsNoTracking().Where(x => ids.Contains(x.Id)).ToDictionaryAsync(x => x.Id, ct);
        var now = DateTimeOffset.UtcNow;
        // Per-line commercial tiers only: no implicit aggregation across POs, lines or requests.
        return selected.Any(x => !offers.TryGetValue(x.Source.CatalogEntryId!.Value, out var offer)
                || x.Source.CatalogPublishedVersion is null
                || x.OrderLine.CatalogItemId is Guid itemId && itemId != x.Source.CatalogItemId
                || !CatalogRequestClassification.OfferMatches(offer, x.Source.CatalogItemId, supplierId,
                    x.OrderLine.UnitCode, currency, x.OrderLine.Quantity, x.OrderLine.UnitPrice,
                    x.Source.CatalogPublishedVersion, now, commercialUnitComparison: true))
            ? "OrderCatalogCommercialTermsMismatch" : null;
    }
}
