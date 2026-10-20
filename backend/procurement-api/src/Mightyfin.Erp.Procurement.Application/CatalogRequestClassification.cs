using Mightyfin.Erp.Procurement.Domain;

namespace Mightyfin.Erp.Procurement.Application;

public sealed record CatalogRequestCodes(string CategoryCode, string PurchaseType);

/// <summary>Owned catalog classification cannot be overridden by a requester's policy inputs.</summary>
public static class CatalogRequestClassification
{
    public static CatalogRequestCodes? Bind(CatalogItem item, string? category, string? type, bool allowMissing)
    {
        if (item.Status != "Active" || !item.IsPurchasable
            || !ProcurementPolicyCode.TryNormalizeOptional(item.CategoryCode, out var ownedCategory)
            || ownedCategory is null
            || !ProcurementPolicyCode.TryNormalizeOptional(item.Kind, out var ownedType)
            || ownedType is null || ownedType.Length > 40) return null;
        if (allowMissing)
        {
            if (!ProcurementPolicyCode.TryNormalizeOptional(category, out var suppliedCategory)
                || !ProcurementPolicyCode.TryNormalizeOptional(type, out var suppliedType)
                || suppliedCategory is not null && suppliedCategory != ownedCategory
                || suppliedType is not null && suppliedType != ownedType) return null;
        }
        // Existing evidence must already be exact; never silently repair it at submit/commit.
        else if (category != ownedCategory || type != ownedType) return null;
        return new(ownedCategory, ownedType);
    }

    public static bool OfferMatches(CatalogEntry entry, Guid? itemId, Guid? supplierId, string? unit,
        string? currency, decimal quantity, decimal unitPrice, int? publishedVersion, DateTimeOffset now) =>
        entry.Status == "Published" && entry.PublishedVersion > 0
        && (publishedVersion is null || publishedVersion == entry.PublishedVersion)
        && entry.EffectiveFrom <= now && (entry.EffectiveTo is null || entry.EffectiveTo > now)
        && entry.AudienceCode is null && entry.SiteRef is null && entry.ContractRef is null
        && entry.CatalogItemId == itemId && entry.SupplierId == supplierId
        && entry.UnitCode == unit && entry.CurrencyCode == currency && entry.UnitPrice == unitPrice
        && (entry.MinimumQuantity is null || quantity >= entry.MinimumQuantity)
        && (entry.MaximumQuantity is null || quantity <= entry.MaximumQuantity);
}
