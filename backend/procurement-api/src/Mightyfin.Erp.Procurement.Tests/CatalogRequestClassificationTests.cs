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
}
