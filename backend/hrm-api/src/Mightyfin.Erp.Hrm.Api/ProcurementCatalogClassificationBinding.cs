using Microsoft.EntityFrameworkCore;
using Mightyfin.Erp.Procurement.Application;
using Mightyfin.Erp.Procurement.Domain;
using Mightyfin.Erp.Procurement.Infrastructure.Data;

namespace Mightyfin.Erp.Hrm.Api;

internal static class ProcurementCatalogClassificationBinding
{
    internal static async Task<bool> StoredMatches(ProcurementDbContext db,
        IEnumerable<PurchaseRequestLine> lines, CancellationToken ct)
    {
        var selected = lines.Where(x => x.CatalogItemId.HasValue).ToArray();
        if (selected.Length == 0) return true;
        var ids = selected.Select(x => x.CatalogItemId!.Value).Distinct().ToArray();
        var items = await db.CatalogItems.AsNoTracking().Where(x => ids.Contains(x.Id)).ToDictionaryAsync(x => x.Id, ct);
        return selected.All(line => items.TryGetValue(line.CatalogItemId!.Value, out var item)
            && CatalogRequestClassification.Bind(item, line.CategoryCode, line.PurchaseType, allowMissing: false) is not null);
    }
}
