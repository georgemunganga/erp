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
}
