using Mightyfin.Erp.Procurement.Application;
using Mightyfin.Erp.Procurement.Domain;

namespace Mightyfin.Erp.Procurement.Tests;

public sealed class CatalogRequestClassificationTests
{
    private static CatalogItem Item() => new()
    { Kind = "Goods", CategoryCode = "IT", Status = "Active", IsPurchasable = true };

    [Fact]
    public void Editable_omissions_and_matching_display_codes_bind_to_owned_item_without_mutating_it()
    {
        var item = Item();
        Assert.Equal(new CatalogRequestCodes("IT", "GOODS"),
            CatalogRequestClassification.Bind(item, null, null, allowMissing: true));
        Assert.Equal(new CatalogRequestCodes("IT", "GOODS"),
            CatalogRequestClassification.Bind(item, " it ", " Goods ", allowMissing: true));
        Assert.Equal("Goods", item.Kind);
        Assert.Equal("IT", item.CategoryCode);
    }

    [Theory]
    [InlineData("IT", "SERVICE")]
    [InlineData("OTHER", "GOODS")]
    [InlineData("IT", "GOOD")]
    [InlineData("IT", "GOODS-SERVICE")]
    public void Requester_cannot_select_a_different_policy_or_infer_a_synonym(string category, string type)
    {
        Assert.Null(CatalogRequestClassification.Bind(Item(), category, type, allowMissing: true));
        Assert.Null(CatalogRequestClassification.Bind(Item(), category, type, allowMissing: false));
    }

    [Theory]
    [InlineData(null, null)]
    [InlineData("IT", "Goods")]
    [InlineData("it", "GOODS")]
    [InlineData("IT", "SERVICE")]
    public void Submission_and_new_commitment_never_repair_historical_evidence(string? category, string? type)
    {
        Assert.Null(CatalogRequestClassification.Bind(Item(), category, type, allowMissing: false));
    }

    [Fact]
    public void Exact_stored_classification_is_valid_but_unknown_or_unavailable_owner_is_not()
    {
        var item = Item();
        Assert.NotNull(CatalogRequestClassification.Bind(item, "IT", "GOODS", allowMissing: false));
        item.Kind = "Goods / Services";
        Assert.Null(CatalogRequestClassification.Bind(item, null, null, allowMissing: true));
        item.Kind = "";
        Assert.Null(CatalogRequestClassification.Bind(item, null, null, allowMissing: true));
        item.Kind = "Goods"; item.CategoryCode = "";
        Assert.Null(CatalogRequestClassification.Bind(item, null, null, allowMissing: true));
        item.CategoryCode = "IT"; item.Status = "Inactive";
        Assert.Null(CatalogRequestClassification.Bind(item, "IT", "GOODS", allowMissing: false));
        item.Status = "Active"; item.IsPurchasable = false;
        Assert.Null(CatalogRequestClassification.Bind(item, "IT", "GOODS", allowMissing: false));
    }

    [Fact]
    public void Selected_offer_requires_current_publication_version_time_and_exact_terms()
    {
        var now = DateTimeOffset.UtcNow; var item = Guid.NewGuid(); var supplier = Guid.NewGuid();
        var entry = new CatalogEntry { CatalogItemId = item, SupplierId = supplier,
            Status = "Published", PublishedVersion = 2, EffectiveFrom = now.AddDays(-1),
            EffectiveTo = now.AddDays(1), UnitCode = "each", CurrencyCode = "ZMW", UnitPrice = 100,
            MinimumQuantity = 1, MaximumQuantity = 2 };
        bool Matches(int? version = 2) => CatalogRequestClassification.OfferMatches(entry, item, supplier,
            "each", "ZMW", 1, 100, version, now);
        Assert.True(Matches());
        Assert.True(Matches(null)); // new draft selects current publication, then pins it.
        Assert.False(Matches(1));
        entry.Status = "Withdrawn"; Assert.False(Matches()); entry.Status = "Published";
        entry.EffectiveTo = now; Assert.False(Matches()); entry.EffectiveTo = now.AddDays(1);
        entry.EffectiveFrom = now.AddSeconds(1); Assert.False(Matches()); entry.EffectiveFrom = now.AddDays(-1);
        entry.UnitPrice = 101; Assert.False(Matches()); entry.UnitPrice = 100;
        entry.SupplierId = Guid.NewGuid(); Assert.False(Matches()); entry.SupplierId = supplier;
        entry.CatalogItemId = Guid.NewGuid(); Assert.False(Matches()); entry.CatalogItemId = item;
        entry.MinimumQuantity = 2; Assert.False(Matches()); entry.MinimumQuantity = 1;
        entry.AudienceCode = "RESTRICTED"; Assert.False(Matches()); entry.AudienceCode = null;
        entry.SiteRef = Guid.NewGuid(); Assert.False(Matches()); entry.SiteRef = null;
        entry.ContractRef = Guid.NewGuid(); Assert.False(Matches());
    }

    [Fact]
    public void Actual_commercial_quantity_and_terms_must_qualify_even_when_original_request_does()
    {
        var now = DateTimeOffset.UtcNow; var item = Guid.NewGuid(); var supplier = Guid.NewGuid();
        var offer = new CatalogEntry { CatalogItemId = item, SupplierId = supplier, UnitCode = "each",
            CurrencyCode = "ZMW", UnitPrice = 100, MinimumQuantity = 10, MaximumQuantity = 20,
            Status = "Published", PublishedVersion = 1, EffectiveFrom = now.AddDays(-1) };
        bool Actual(decimal quantity = 10, decimal price = 100, string unit = "EACH", Guid? vendor = null, string currency = "ZMW") =>
            CatalogRequestClassification.OfferMatches(offer, item, vendor ?? supplier, unit, currency,
                quantity, price, 1, now, commercialUnitComparison: true);
        Assert.True(CatalogRequestClassification.OfferMatches(offer, item, supplier, "each", "ZMW", 10, 100, 1, now));
        Assert.True(Actual()); // existing PO unit owner folds case; no EA/EACH synonym inference.
        Assert.False(Actual(quantity: 1));
        Assert.False(Actual(quantity: 21));
        Assert.False(Actual(price: 99));
        Assert.False(Actual(vendor: Guid.NewGuid()));
        Assert.False(Actual(unit: "BOX"));
        Assert.False(Actual(unit: "EA"));
        Assert.False(Actual(unit: " EACH "));
        Assert.False(Actual(currency: "USD"));
        Assert.False(CatalogRequestClassification.OfferMatches(offer, item, supplier, "EACH", "ZMW", 10, 100, 1, now));
    }
}
