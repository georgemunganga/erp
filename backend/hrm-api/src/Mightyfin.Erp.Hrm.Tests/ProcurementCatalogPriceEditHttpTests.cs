using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Mightyfin.Erp.Hrm.Api;

namespace Mightyfin.Erp.Hrm.Tests;

/// <summary>Actual retained-release catalog handlers, synthetic outer auth/InMemory store.
/// No PostgreSQL or catalog publication claim.</summary>
public sealed partial class ProcurementRequestHttpTests
{
    [Fact]
    public async Task Catalog_draft_edit_roundtrips_exact_amounts_and_previously_unprojected_terms()
    {
        await using var fixture = await Fixture.Start(); var supplier = await fixture.AddInvoiceSupplier();
        var itemResponse = await SendJson(fixture.Client, HttpMethod.Post, "/api/procurement/v1/items", fixture.Owner,
            new { code = "PRICE-EDIT", name = "Catalog draft item", kind = "Goods", categoryCode = "OFFICE", unitCode = "each" }, "procurement_buyer");
        Assert.Equal(HttpStatusCode.Created, itemResponse.StatusCode);
        var item = await itemResponse.Content.ReadFromJsonAsync<JsonElement>(); var path = $"/api/procurement/v1/items/{item.GetProperty("id").GetGuid()}/catalog-entries";
        var draft = new { supplierId = supplier, currencyCode = "ZMW", unitCode = "each", unitPrice = "12345678901234.5678",
            minimumQuantity = "1.2345", maximumQuantity = "99999999999999.9999", taxBasis = "Exclusive", audienceCode = "STAFF",
            effectiveFrom = "2026-10-22T12:34:56Z", effectiveTo = "2027-10-22T12:34:56Z" };
        var createdResponse = await SendJson(fixture.Client, HttpMethod.Post, path, fixture.Owner, draft, "procurement_buyer");
        Assert.Equal(HttpStatusCode.Created, createdResponse.StatusCode); var created = await createdResponse.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(1, created.GetProperty("catalogEditVersion").GetInt32());
        Assert.Equal("12345678901234.5678", created.GetProperty("exactAmounts").GetProperty("unitPrice").GetString());
        var changed = new { draft.supplierId, draft.currencyCode, draft.unitCode, unitPrice = "99999999999999.9999",
            draft.minimumQuantity, draft.maximumQuantity, draft.taxBasis, draft.audienceCode, draft.effectiveFrom, draft.effectiveTo };
        var entryPath = path + "/" + created.GetProperty("id").GetGuid();
        var updatedResponse = await SendJson(fixture.Client, HttpMethod.Put, entryPath, fixture.Owner, new { version = 1, entry = changed }, "procurement_buyer");
        Assert.Equal(HttpStatusCode.OK, updatedResponse.StatusCode); var updated = await updatedResponse.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(2, updated.GetProperty("version").GetInt64()); Assert.Equal("Draft", updated.GetProperty("status").GetString());
        var listResponse = await Send(fixture.Client, HttpMethod.Get, path, fixture.Owner, role: "procurement_buyer");
        Assert.Equal(HttpStatusCode.OK, listResponse.StatusCode);
        var reloaded = (await listResponse.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("items")[0];
        Assert.Equal("99999999999999.9999", reloaded.GetProperty("exactAmounts").GetProperty("unitPrice").GetString());
        Assert.Equal("1.2345", reloaded.GetProperty("exactAmounts").GetProperty("minimumQuantity").GetString());
        Assert.Equal("Exclusive", reloaded.GetProperty("taxBasis").GetString()); Assert.Equal("STAFF", reloaded.GetProperty("audienceCode").GetString());
        Assert.Equal(created.GetProperty("effectiveFrom").GetString(), reloaded.GetProperty("effectiveFrom").GetString());
        Assert.Equal(HttpStatusCode.Conflict, (await SendJson(fixture.Client, HttpMethod.Put, entryPath, fixture.Owner, new { version = 1, entry = draft }, "procurement_buyer")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await SendJson(fixture.Client, HttpMethod.Put, entryPath, fixture.Owner, new { version = 2, entry = draft }, "procurement_buyer", fixture.OtherCompany)).StatusCode);
        Assert.Equal(1, await fixture.VendorDetailAuditCount(created.GetProperty("id").GetGuid(), "update"));
    }

    [Theory]
    [InlineData("100.1234")]
    [InlineData("99999999999999.9999")]
    [InlineData("0.0001")]
    public void Published_catalog_keeps_legacy_numeric_and_lossless_price_companion(string amount)
    {
        var value = decimal.Parse(amount, System.Globalization.CultureInfo.InvariantCulture);
        var row = new CatalogView(Guid.NewGuid(), Guid.NewGuid(), "EXACT", "Exact catalog price", "Goods",
            null, "OFFICE", "each", value, "ZMW", Guid.NewGuid(), "Synthetic supplier", null, 1);
        var body = JsonSerializer.SerializeToElement(row, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        Assert.Equal(JsonValueKind.Number, body.GetProperty("estimatedUnitPrice").ValueKind);
        Assert.Equal(value, body.GetProperty("estimatedUnitPrice").GetDecimal());
        Assert.Equal(amount, body.GetProperty("exactEstimatedUnitPrice").GetString());
    }

    [Theory]
    [InlineData("1.00001")]
    [InlineData("12345678901234.567899999999999999999999")]
    [InlineData("1E-1000")]
    public void Catalog_entry_converter_rejects_precision_before_decimal_binding(string literal)
    {
        var raw = "{\"currencyCode\":\"ZMW\",\"unitCode\":\"each\",\"unitPrice\":" + literal + ",\"effectiveFrom\":\"2026-10-22T00:00:00Z\"}";
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<EntryInput>(raw, new JsonSerializerOptions(JsonSerializerDefaults.Web)));
    }

    [Fact]
    public void Catalog_entry_converter_keeps_legacy_numeric_and_exact_string_input_compatible()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        foreach (var value in new[] { "12345678901234.5678", "\"12345678901234.5678\"" })
        {
            var row = JsonSerializer.Deserialize<EntryInput>("{\"currencyCode\":\"ZMW\",\"unitCode\":\"each\",\"unitPrice\":" + value + ",\"minimumQuantity\":null,\"maximumQuantity\":\"1.2345\",\"effectiveFrom\":\"2026-10-22T00:00:00Z\"}", options)!;
            Assert.Equal(12345678901234.5678m, row.UnitPrice); Assert.Null(row.MinimumQuantity); Assert.Equal(1.2345m, row.MaximumQuantity);
        }
    }
}
