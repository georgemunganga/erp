using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Mightyfin.Erp.Procurement.Application;
using Mightyfin.Erp.Procurement.Domain;
using Mightyfin.Erp.Procurement.Infrastructure.Data;
namespace Mightyfin.Erp.Hrm.Tests;
// Mapped handlers + existing synthetic worker/scope/InMemory fixture, not PG runtime proof.
public sealed partial class ProcurementRequestHttpTests
{
    private const string ExactRequests = "/api/procurement/v1/requests";
    private static async Task<JsonElement> RequestExactBody(HttpResponseMessage response, HttpStatusCode expected)
    { Assert.Equal(expected, response.StatusCode); return await response.Content.ReadFromJsonAsync<JsonElement>(); }
    [Fact]
    public async Task Exact_request_create_edit_reload_preserves_wide_original_and_hidden_mutable_fields()
    {
        await using var fixture = await Fixture.Start();
        var before = await RequestExactBody(await SendJson(fixture.Client, HttpMethod.Post, ExactRequests, fixture.Owner,
            new { purpose = "Exact request", currencyCode = "ZMW", neededBy = "2030-01-01", costCenterRef = "HEADER-CC", projectRef = "HEADER-PROJECT",
                deliveryLocationRef = "Retained delivery reference", deliveryAddressText = "Retained address", fundingSourceRef = "Retained source", notesToApprover = "Retained note",
                lines = new object[] {
                    new { description = "Wide original", unitCode = "each", requestedQuantity = "1", estimatedUnitPrice = "12345678901234.5678", discountPercent = "12.3456", taxCodeRef = "RETAINED-TAX", costCenterRef = "LINE-CC", projectRef = "LINE-PROJECT", neededBy = "2030-01-02" },
                    new { description = "Eight-place product", unitCode = "each", requestedQuantity = "0.0001", estimatedUnitPrice = "0.0001" } } }), HttpStatusCode.Created);
        var id = before.GetProperty("id").GetGuid(); Assert.Equal(1, before.GetProperty("requestEditVersion").GetInt32());
        var first = before.GetProperty("lines")[0];
        Assert.Equal("12345678901234.5678", first.GetProperty("exactAmounts").GetProperty("estimatedUnitPrice").GetString());
        Assert.Equal("12.3456", first.GetProperty("exactAmounts").GetProperty("discountPercent").GetString());
        var header = JsonSerializer.Deserialize<Dictionary<string, object?>>(before.GetRawText())!;
        header["purpose"] = "Corrected purpose only";
        var lines = before.GetProperty("lines").EnumerateArray().Select(line => {
            var fields = JsonSerializer.Deserialize<Dictionary<string, object?>>(line.GetRawText())!;
            fields["requestedQuantity"] = line.GetProperty("exactAmounts").GetProperty("requestedQuantity").GetString();
            fields["estimatedUnitPrice"] = line.GetProperty("exactAmounts").GetProperty("estimatedUnitPrice").GetString();
            fields["discountPercent"] = line.GetProperty("exactAmounts").GetProperty("discountPercent").ValueKind == JsonValueKind.Null ? null : line.GetProperty("exactAmounts").GetProperty("discountPercent").GetString();
            return fields;
        }).ToArray(); header["lines"] = lines;
        var saved = await RequestExactBody(await SendJson(fixture.Client, HttpMethod.Put, $"{ExactRequests}/{id}", fixture.Owner, header), HttpStatusCode.OK);
        var reloaded = await RequestExactBody(await Send(fixture.Client, HttpMethod.Get, $"{ExactRequests}/{id}", fixture.Owner), HttpStatusCode.OK);
        Assert.Equal(before.GetProperty("version").GetInt64() + 1, saved.GetProperty("version").GetInt64());
        Assert.Equal(first.GetProperty("id").GetGuid(), reloaded.GetProperty("lines")[0].GetProperty("id").GetGuid());
        Assert.Equal(first.GetProperty("exactAmounts").GetRawText(), reloaded.GetProperty("lines")[0].GetProperty("exactAmounts").GetRawText());
        foreach (var key in new[] { "taxCodeRef", "costCenterRef", "projectRef", "neededBy", "catalogItemId", "catalogEntryId", "catalogPublishedVersion", "preferredSupplierId", "specificationDocumentRefId" })
            Assert.Equal(first.GetProperty(key).GetRawText(), reloaded.GetProperty("lines")[0].GetProperty(key).GetRawText());
        await fixture.AssertExactRequestStored(id, 12345678901234.5678m, 12.3456m);
    }
    [Theory]
    [InlineData("requestedQuantity", "1.00000000000000000000000000001")]
    [InlineData("estimatedUnitPrice", "1.00000000000000000000000000001")]
    [InlineData("estimatedUnitPrice", "1e-1000")]
    [InlineData("discountPercent", "0.00001")]
    [InlineData("estimatedUnitPrice", "\"999999999999999.9999\"")]
    [InlineData("requestedQuantity", "\"0.00000001\"")]
    public async Task Exact_request_raw_lexeme_precision_denials_create_no_partial_records(string field, string token)
    {
        await using var fixture = await Fixture.Start();
        var seed = await RequestExactBody(await SendJson(fixture.Client, HttpMethod.Post, ExactRequests, fixture.Owner, Draft("Existing exact draft")), HttpStatusCode.Created);
        var footprint = await fixture.ExactRequestFootprint();
        var fields = new Dictionary<string, object?> { ["description"] = "Invalid raw precision", ["unitCode"] = "each", ["requestedQuantity"] = 1, ["estimatedUnitPrice"] = 1, ["discountPercent"] = null };
        var line = JsonSerializer.Serialize(fields); var needle = field == "discountPercent" ? "null" : "1";
        line = line.Replace($"\"{field}\":{needle}", $"\"{field}\":{token}");
        using var request = new HttpRequestMessage(HttpMethod.Post, ExactRequests) { Content = new StringContent("{\"purpose\":\"Raw boundary\",\"currencyCode\":\"ZMW\",\"lines\":[" + line + "]}", Encoding.UTF8, "application/json") };
        request.Headers.Add("X-Test-User", fixture.Owner.ToString());
        var response = await fixture.Client.SendAsync(request);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(footprint, await fixture.ExactRequestFootprint());
        using var update = new HttpRequestMessage(HttpMethod.Put, $"{ExactRequests}/{seed.GetProperty("id").GetGuid()}") {
            Content = new StringContent("{\"version\":" + seed.GetProperty("version").GetInt64() + ",\"purpose\":\"Must not partially change\",\"currencyCode\":\"ZMW\",\"lines\":[" + line + "]}", Encoding.UTF8, "application/json") };
        update.Headers.Add("X-Test-User", fixture.Owner.ToString());
        Assert.Equal(HttpStatusCode.BadRequest, (await fixture.Client.SendAsync(update)).StatusCode);
        Assert.Equal(footprint, await fixture.ExactRequestFootprint());
    }
    [Fact]
    public async Task Request_transport_retains_legacy_numeric_clients_and_exact_eight_place_products()
    {
        await using var fixture = await Fixture.Start();
        var response = await RequestExactBody(await SendJson(fixture.Client, HttpMethod.Post, ExactRequests, fixture.Owner,
            new { purpose = "Legacy numeric compatibility", lines = new[] {
                new { description = "Tiny1", unitCode = "each", requestedQuantity = 0.0001m, estimatedUnitPrice = 0.0001m },
                new { description = "Tiny2", unitCode = "each", requestedQuantity = 0.0001m, estimatedUnitPrice = 0.0001m } } }), HttpStatusCode.Created);
        Assert.All(response.GetProperty("lines").EnumerateArray(), line => {
            Assert.Equal(JsonValueKind.Number, line.GetProperty("requestedQuantity").ValueKind);
            Assert.Equal("0.0001", line.GetProperty("exactAmounts").GetProperty("requestedQuantity").GetString());
        });
        var id = response.GetProperty("id").GetGuid();
        await fixture.AssertExactRequestHasNoAuthority(id);
    }
    [Fact]
    public async Task Exact_catalog_linked_request_preserves_owned_source_bindings_on_header_only_edit()
    {
        await using var fixture = await Fixture.Start(); var source = await fixture.ExactCatalogOwnerPrerequisite();
        object Line(Guid? id = null) => new { id, description = "Retained catalog line", unitCode = "each", requestedQuantity = "1", estimatedUnitPrice = "12345678901234.5678",
            catalogItemId = source.Item, catalogEntryId = source.Entry, preferredSupplierId = source.Supplier, purchaseType = "Goods", categoryCode = "OFFICE" };
        var before = await RequestExactBody(await SendJson(fixture.Client, HttpMethod.Post, ExactRequests, fixture.Owner,
            new { purpose = "Catalog exact", currencyCode = "ZMW", lines = new[] { Line() } }), HttpStatusCode.Created);
        var id = before.GetProperty("id").GetGuid();
        var after = await RequestExactBody(await SendJson(fixture.Client, HttpMethod.Put, $"{ExactRequests}/{id}", fixture.Owner,
            new { purpose = "Header corrected", version = before.GetProperty("version").GetInt64(), currencyCode = "ZMW", lines = new[] { Line(before.GetProperty("lines")[0].GetProperty("id").GetGuid()) } }), HttpStatusCode.OK);
        foreach (var key in new[] { "catalogItemId", "catalogEntryId", "preferredSupplierId", "catalogPublishedVersion", "exactAmounts" })
            Assert.Equal(before.GetProperty("lines")[0].GetProperty(key).GetRawText(), after.GetProperty("lines")[0].GetProperty(key).GetRawText());
    }
    private sealed partial class Fixture
    {
        private ProcurementDbContext RequestExactDb(IServiceScope serviceScope)
        {
            serviceScope.ServiceProvider.GetRequiredService<ProcurementRequestScope>().Set("tenant-a", company, Owner.ToString(), null, null, false, "test");
            return serviceScope.ServiceProvider.GetRequiredService<ProcurementDbContext>();
        }
        public async Task<string> ExactRequestFootprint()
        {
            using var scope = app.Services.CreateScope(); var db = RequestExactDb(scope);
            return JsonSerializer.Serialize(new { requests = await db.PurchaseRequests.AsNoTracking().OrderBy(x => x.Id).ToArrayAsync(), lines = await db.PurchaseRequestLines.AsNoTracking().OrderBy(x => x.Id).ToArrayAsync(), audit = await db.AuditEvents.CountAsync(), outbox = await db.OutboxEvents.CountAsync() });
        }
        public async Task AssertExactRequestStored(Guid id, decimal price, decimal discount)
        {
            using var scope = app.Services.CreateScope(); var db = RequestExactDb(scope);
            var lines = await db.PurchaseRequestLines.AsNoTracking().Where(x => x.PurchaseRequestId == id).OrderBy(x => x.LineNumber).ToArrayAsync();
            Assert.Equal(price, lines[0].EstimatedUnitPrice); Assert.Equal(discount, lines[0].DiscountPercent);
            Assert.Equal(0.00000001m, lines[1].RequestedQuantity * lines[1].EstimatedUnitPrice);
            Assert.Equal("LINE-CC", lines[0].CostCenterRef); Assert.Equal("LINE-PROJECT", lines[0].ProjectRef);
        }
        public async Task AssertExactRequestHasNoAuthority(Guid id)
        {
            using var scope = app.Services.CreateScope(); var db = RequestExactDb(scope); var row = await db.PurchaseRequests.SingleAsync(x => x.Id == id);
            Assert.Equal("Draft", row.Status); Assert.Null(row.ApprovalInstanceRef); Assert.Null(row.SubmittedVersion);
            Assert.False(await db.PolicyEvaluations.AnyAsync(x => x.PurchaseRequestId == id)); Assert.False(await db.BudgetCheckSnapshots.AnyAsync(x => x.PurchaseRequestId == id));
        }
        public async Task<(Guid Item, Guid Entry, Guid Supplier)> ExactCatalogOwnerPrerequisite()
        {
            // Synthetic owner facts test exact binding only; not supplier/catalog approval proof.
            var supplier = await AddInvoiceSupplier(); using var scope = app.Services.CreateScope(); var db = RequestExactDb(scope);
            var item = new CatalogItem { Code = "EXACT-OWNER", Name = "Exact owner prerequisite", Kind = "Goods", CategoryCode = "OFFICE", UnitCode = "each", Status = "Active", IsPurchasable = true };
            db.AddScoped(item); var entry = new CatalogEntry { CatalogItemId = item.Id, SupplierId = supplier, CurrencyCode = "ZMW", UnitCode = "each", UnitPrice = 12345678901234.5678m,
                EffectiveFrom = DateTimeOffset.UtcNow.AddDays(-1), Status = "Published", PublishedVersion = 1 };
            db.AddScoped(entry); await db.SaveChangesAsync(); return (item.Id, entry.Id, supplier);
        }
    }
}
