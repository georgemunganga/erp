using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Mightyfin.Erp.Hrm.Api;
using Mightyfin.Erp.Hrm.Application;
using Mightyfin.Erp.Hrm.Infrastructure.Data;
using Mightyfin.Erp.Procurement.Domain;
using Mightyfin.Erp.Procurement.Infrastructure.Data;
using Npgsql;

sealed partial class Probe
{
    private async Task VerifyCatalogPublicationFence(Guid supplier)
    {
        // Separate synthetic owner host: runtime role keeps its finite catalog SELECT grants.
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer(); builder.Logging.ClearProviders();
        builder.Services.AddScoped<ITenantAccessor>(_ => new Tenant());
        builder.Services.AddDbContext<HrmDbContext>(o => o.UseNpgsql(databaseConnection));
        ProcurementHostIntegration.AddServices(builder.Services, databaseConnection, builder.Configuration);
        builder.Services.AddAuthentication("catalog-probe").AddScheme<AuthenticationSchemeOptions, ProbeAuthentication>("catalog-probe", _ => { });
        builder.Services.AddAuthorization(ProcurementPermissions.AddPolicies);
        await using var writerApp = builder.Build();
        writerApp.UseRouting(); writerApp.UseAuthentication(); writerApp.UseAuthorization();
        writerApp.UseMiddleware<ProcurementScopeMiddleware>(); ProcurementItemEndpoints.MapRoutes(writerApp);
        await writerApp.StartAsync(); using var writerClient = writerApp.GetTestClient();

        Task<HttpResponseMessage> Withdraw(Guid item, Guid entry, long version)
        {
            var message = new HttpRequestMessage(HttpMethod.Post, $"{Root}/items/{item}/catalog-entries/{entry}/withdraw")
            { Content = JsonContent.Create(new { version, reason = "Synthetic catalog publication fence proof" }, options: Json) };
            message.Headers.Add("X-Probe-User", OrderBuyer.ToString("D"));
            message.Headers.Add("X-Shell-Entity", company.ToString("D"));
            message.Headers.Add("X-Shell-Location", branch.ToString("D"));
            return writerClient.SendAsync(message);
        }
        async Task<(Guid Item, Guid Entry, long Version, JsonElement Source)> Seed(string suffix, decimal? minimum = null)
        {
            Guid itemId, entryId; long version;
            using (var services = Scope(OrderBuyer))
            {
                var db = services.ServiceProvider.GetRequiredService<ProcurementDbContext>();
                var item = new CatalogItem { Code = "FENCE-" + suffix, Name = "Synthetic catalog fence " + suffix,
                    Kind = "Goods", CategoryCode = "IT", UnitCode = "each", Status = "Active", IsPurchasable = true };
                db.AddScoped(item);
                var entry = new CatalogEntry { CatalogItemId = item.Id, SupplierId = supplier,
                    CurrencyCode = "ZMW", UnitCode = "each", UnitPrice = 100, MinimumQuantity = minimum, MaximumQuantity = 2, Status = "Published", PublishedVersion = 1,
                    EffectiveFrom = DateTimeOffset.UtcNow.AddDays(-1), EffectiveTo = DateTimeOffset.UtcNow.AddDays(1) };
                db.AddScoped(entry); await db.SaveChangesAsync(); itemId = item.Id; entryId = entry.Id; version = entry.Version;
            }
            var source = await NewApprovedOrderSource("Catalog publication fence " + suffix, quantity: 2, checkedAmount: 200,
                purchaseType: "Goods", catalogItemId: itemId, catalogEntryId: entryId, preferredSupplierId: supplier);
            return (itemId, entryId, version, source);
        }

        // Reader-first owns the shared catalog lease before waiting on its source row.
        var first = await Seed("READER-FIRST"); var key = Guid.NewGuid();
        var conversion = OrderConversion(first.Source, supplier); JsonElement firstOrder;
        await using (var blocker = new NpgsqlConnection(databaseConnection))
        {
            await blocker.OpenAsync(); await using var hold = await blocker.BeginTransactionAsync();
            await using (var row = new NpgsqlCommand("SELECT id FROM procurement.purchase_requests WHERE id=@id FOR UPDATE", blocker, hold))
            { row.Parameters.AddWithValue("id", first.Source.GetProperty("id").GetGuid()); await row.ExecuteScalarAsync(); }
            var reader = Send(HttpMethod.Post, Root + "/orders/from-approved-sources", OrderBuyer, conversion, key);
            await WaitForLock("%procurement.purchase_requests%FOR UPDATE%");
            var writer = Withdraw(first.Item, first.Entry, first.Version);
            await WaitForLock("%pg_advisory_lock(%");
            Require(!writer.IsCompleted, "Catalog withdrawal crossed a reader's pre-snapshot shared lease.");
            await hold.CommitAsync(); var converted = await reader.WaitAsync(TimeSpan.FromSeconds(15));
            await Check(converted, HttpStatusCode.Created); firstOrder = await converted.Content.ReadFromJsonAsync<JsonElement>();
            await Check(await writer.WaitAsync(TimeSpan.FromSeconds(15)), HttpStatusCode.OK);
        }
        // Historical exact receipt is acknowledgement, not permission for another commitment.
        await Check(await Send(HttpMethod.Post, Root + "/orders/from-approved-sources", OrderBuyer, conversion, key), HttpStatusCode.Created);
        var before = await ExactInputFootprint();
        var withdrawn = await Send(HttpMethod.Post, Root + "/orders/from-approved-sources", OrderBuyer, conversion, Guid.NewGuid());
        await Check(withdrawn, HttpStatusCode.Conflict);
        Require((await withdrawn.Content.ReadAsStringAsync()).Contains("OrderSourceCatalogOfferUnavailable", StringComparison.Ordinal),
            "Withdrawn offer failed for an unrelated source reason.");
        Require(before == await ExactInputFootprint(), "Withdrawn offer created a new commercial effect.");
        await Check(await Send(HttpMethod.Post, $"{Root}/orders/{firstOrder.GetProperty("id").GetGuid()}/source-cancel", OrderBuyer,
            new { expectedOrderVersion = firstOrder.GetProperty("version").GetInt64(), reason = "Withdrawn offer cleanup remains possible" }, Guid.NewGuid()), HttpStatusCode.OK);

        // Writer-first owns exclusive catalog lease while blocked on the entry row. Reader
        // must wait before BEGIN; after writer commit it must observe the withdrawal.
        var second = await Seed("WRITER-FIRST");
        await using (var blocker = new NpgsqlConnection(databaseConnection))
        {
            await blocker.OpenAsync(); await using var hold = await blocker.BeginTransactionAsync();
            await using (var row = new NpgsqlCommand("SELECT id FROM procurement.catalog_entries WHERE id=@id FOR UPDATE", blocker, hold))
            { row.Parameters.AddWithValue("id", second.Entry); await row.ExecuteScalarAsync(); }
            var writer = Withdraw(second.Item, second.Entry, second.Version);
            // Withdraw reads before implicit Save transaction: block its UPDATE while it owns the lease.
            await WaitForLock("%catalog_entries%");
            var reader = Send(HttpMethod.Post, Root + "/orders/from-approved-sources", OrderBuyer,
                OrderConversion(second.Source, supplier), Guid.NewGuid());
            await WaitForLock("%pg_advisory_lock_shared(%");
            Require(!reader.IsCompleted, "New commitment crossed an already-owned catalog writer lease.");
            await hold.CommitAsync(); await Check(await writer.WaitAsync(TimeSpan.FromSeconds(15)), HttpStatusCode.OK);
            before = await ExactInputFootprint();
            var denied = await reader.WaitAsync(TimeSpan.FromSeconds(15)); await Check(denied, HttpStatusCode.Conflict);
            Require((await denied.Content.ReadAsStringAsync()).Contains("OrderSourceCatalogOfferUnavailable", StringComparison.Ordinal),
                "Writer-first commitment failed for an unrelated reason.");
            Require(before == await ExactInputFootprint(), "Writer-first withdrawn offer persisted a commitment.");
        }
        // Offer MOQ belongs to actual commercial intent; a qualifying PR cannot justify
        // splitting below the selected price tier or substituting a supplier/price/unit.
        var tier = await Seed("COMMERCIAL-TIER", minimum: 2);
        Guid alternateSupplier;
        using (var services = Scope(OrderBuyer))
        {
            var db = services.ServiceProvider.GetRequiredService<ProcurementDbContext>();
            var other = new Supplier { Number = "CATALOG-ALTERNATE", LegalName = "Synthetic alternate supplier",
                DisplayName = "Synthetic alternate", CountryCode = "ZM", Status = "Active", IsOrderable = true };
            db.AddScoped(other); await db.SaveChangesAsync(); alternateSupplier = other.Id;
        }
        var validTier = JsonSerializer.SerializeToNode(OrderConversion(tier.Source, supplier), Json)!;
        validTier["draft"]!["lines"]![0]!["quantity"] = 2;
        validTier["draft"]!["lines"]![0]!["unitCode"] = "EACH"; // PO-owned case-equivalent unit is valid.
        foreach (var (field, value) in new (string, JsonNode?)[] {
            ("quantity", JsonValue.Create(1)), ("quantity", JsonValue.Create(3)),
            ("unitPrice", JsonValue.Create(99)), ("unitCode", JsonValue.Create("BOX")),
            ("supplierId", JsonValue.Create(alternateSupplier)), ("currencyCode", JsonValue.Create("USD")),
        })
        {
            var invalid = validTier.DeepClone();
            if (field is "supplierId" or "currencyCode") invalid["draft"]![field] = value?.DeepClone();
            else invalid["draft"]!["lines"]![0]![field] = value?.DeepClone();
            before = await ExactInputFootprint();
            var denied = await SendRawOrder(HttpMethod.Post, Root + "/orders/from-approved-sources", OrderBuyer, invalid, Guid.NewGuid());
            await Check(denied, HttpStatusCode.Conflict);
            Require((await denied.Content.ReadAsStringAsync()).Contains("OrderCatalogCommercialTermsMismatch", StringComparison.Ordinal),
                "Actual tier/substitution failed for an unrelated source reason: " + field);
            Require(before == await ExactInputFootprint(), "Ineligible actual catalog intent persisted effects: " + field);
        }
        var tierKey = Guid.NewGuid();
        var tierConverted = await SendRawOrder(HttpMethod.Post, Root + "/orders/from-approved-sources", OrderBuyer, validTier, tierKey);
        await Check(tierConverted, HttpStatusCode.Created);
        var tierText = await tierConverted.Content.ReadAsStringAsync();
        var tierOrder = JsonDocument.Parse(tierText).RootElement.Clone(); var tierOrderId = tierOrder.GetProperty("id").GetGuid();
        var tierDetail = await Get($"{Root}/orders/{tierOrderId}", OrderBuyer);
        foreach (var (field, value) in new (string, JsonNode?)[] {
            ("quantity", JsonValue.Create(1)), ("unitPrice", JsonValue.Create(99)),
            ("unitCode", JsonValue.Create("BOX")), ("supplierId", JsonValue.Create(alternateSupplier)),
        })
        {
            var draft = validTier["draft"]!.DeepClone();
            draft["lines"]![0]!["id"] = tierDetail.GetProperty("lines")[0].GetProperty("id").GetGuid();
            if (field == "supplierId") draft[field] = value?.DeepClone(); else draft["lines"]![0]![field] = value?.DeepClone();
            var edit = new JsonObject { ["expectedOrderVersion"] = tierOrder.GetProperty("version").GetInt64(),
                ["reason"] = "Invalid proposed catalog terms cannot mutate stored intent", ["draft"] = draft };
            before = await ExactInputFootprint();
            var denied = await SendRawOrder(HttpMethod.Put, $"{Root}/orders/{tierOrderId}/source-draft", OrderBuyer, edit, Guid.NewGuid());
            await Check(denied, HttpStatusCode.Conflict);
            Require((await denied.Content.ReadAsStringAsync()).Contains("OrderCatalogCommercialTermsMismatch", StringComparison.Ordinal),
                "Actual source edit tier/substitution failed for an unrelated reason: " + field);
            Require(before == await ExactInputFootprint(), "Denied proposed catalog edit persisted changes: " + field);
        }
        await Check(await Withdraw(tier.Item, tier.Entry, tier.Version), HttpStatusCode.OK);
        var historicalTier = await SendRawOrder(HttpMethod.Post, Root + "/orders/from-approved-sources", OrderBuyer, validTier, tierKey);
        await Check(historicalTier, HttpStatusCode.Created);
        Require(await historicalTier.Content.ReadAsStringAsync() == tierText, "Exact historical tier conversion receipt was reinterpreted.");
        await Check(await Send(HttpMethod.Post, $"{Root}/orders/{tierOrderId}/source-cancel", OrderBuyer,
            new { expectedOrderVersion = tierOrder.GetProperty("version").GetInt64(), reason = "Catalog tier withdrawal cleanup" }, Guid.NewGuid()), HttpStatusCode.OK);
        Console.WriteLine("PASS catalog commercial tiers: partial MOQ, quantity ceiling, actual price/vendor/unit/currency substitutions denied; exact case-equivalent positive/replay/cancel retained.");
        Console.WriteLine("PASS catalog fence: mapped reader-first withdrawal waits, writer-first commitment sees withdrawal; exact receipt and cancel remain usable.");
    }
}
