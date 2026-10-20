using System.Text.Json;
using Mightyfin.Erp.Hrm.Api;
using Mightyfin.Erp.Procurement.Application;

namespace Mightyfin.Erp.Hrm.Tests;

public sealed class ProcurementOriginalValueJsonTests
{
    private static readonly JsonSerializerOptions Json=new(JsonSerializerDefaults.Web);
    [Theory]
    [InlineData("unitPrice")][InlineData("minimumQuantity")][InlineData("maximumQuantity")]
    public void Catalog_price_and_tiers_reject_raw_precision_and_range_before_decimal_conversion(string field)
    {
        foreach(var token in new[] { "0.00001","1.00000000000000000000000000001","\"100000000000000\"" })
            Assert.Throws<JsonException>(()=>JsonSerializer.Deserialize<EntryInput>("{\""+field+"\":"+token+"}",Json));
    }
    [Theory]
    [InlineData("minimumOrderQuantity","0.00001")]
    [InlineData("purchaseUnitConversion","1.0000001")]
    [InlineData("purchaseUnitConversion","1000000000000")]
    public void Optional_item_fields_use_their_actual_different_storage_precision(string field,string token)
        =>Assert.Throws<JsonException>(()=>JsonSerializer.Deserialize<ItemInput>("{\""+field+"\":"+token+"}",Json));
    [Theory]
    [InlineData("requestedQuantity")][InlineData("estimatedUnitPrice")][InlineData("discountPercent")]
    public void Request_fields_cannot_hide_excess_precision_in_default_decimal_rounding(string field)
        =>Assert.Throws<JsonException>(()=>JsonSerializer.Deserialize<ProcurementRequestRoutes.RequestLineInput>("{\""+field+"\":1.00000000000000000000000000001}",Json));
    [Fact]
    public void Planning_contract_and_budget_original_amounts_reject_raw_excess_precision()
    {
        const string raw="1.00000000000000000000000000001";
        Assert.Throws<JsonException>(()=>JsonSerializer.Deserialize<ProcurementPlanLineInput>("{\"estimatedQuantity\":"+raw+"}",Json));
        Assert.Throws<JsonException>(()=>JsonSerializer.Deserialize<ProcurementPlanLineInput>("{\"estimatedAmount\":"+raw+"}",Json));
        Assert.Throws<JsonException>(()=>JsonSerializer.Deserialize<ContractDraftInput>("{\"ceilingAmount\":"+raw+"}",Json));
        Assert.Throws<JsonException>(()=>JsonSerializer.Deserialize<ProcurementBudgetRoutes.BudgetDecision>("{\"checkedAmount\":"+raw+"}",Json));
    }
    [Fact]
    public void Existing_numeric_and_exact_string_values_nullability_and_six_place_conversions_round_trip()
    {
        var item=JsonSerializer.Deserialize<ItemInput>("{\"purchaseUnitConversion\":\"999999999999.999999\",\"minimumOrderQuantity\":null}",Json)!;
        Assert.Equal(999999999999.999999m,item.PurchaseUnitConversion);Assert.Null(item.MinimumOrderQuantity);
        var price=JsonSerializer.Deserialize<EntryInput>("{\"unitPrice\":\"99999999999999.9999\",\"minimumQuantity\":0.0001,\"maximumQuantity\":null}",Json)!;
        Assert.Equal(99999999999999.9999m,price.UnitPrice);Assert.Equal(.0001m,price.MinimumQuantity);Assert.Null(price.MaximumQuantity);
        using var encoded=JsonDocument.Parse(JsonSerializer.Serialize(item,Json));
        Assert.Equal(JsonValueKind.Number,encoded.RootElement.GetProperty("purchaseUnitConversion").ValueKind);
        Assert.Equal(JsonValueKind.Null,encoded.RootElement.GetProperty("minimumOrderQuantity").ValueKind);
        var request=JsonSerializer.Deserialize<ProcurementRequestRoutes.RequestLineInput>("{\"requestedQuantity\":1,\"estimatedUnitPrice\":\"0.0001\",\"discountPercent\":\"100.0000\"}",Json)!;
        Assert.Equal(100m,request.DiscountPercent);Assert.Equal(.0001m,request.EstimatedUnitPrice);
    }
}
