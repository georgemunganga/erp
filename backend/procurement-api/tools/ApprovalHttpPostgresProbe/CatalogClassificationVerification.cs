using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Mightyfin.Erp.Procurement.Domain;
using Mightyfin.Erp.Procurement.Infrastructure.Data;

sealed partial class Probe
{
    private async Task VerifyCatalogClassificationCommitments(Guid supplier)
    {
        Guid itemId;
        using (var services = Scope(OrderBuyer))
        {
            var db = services.ServiceProvider.GetRequiredService<ProcurementDbContext>();
            var item = new CatalogItem { Code = "CATALOG-CLASSIFICATION", Name = "Synthetic classified item",
                Kind = "Goods", CategoryCode = "IT", UnitCode = "each", Status = "Active", IsPurchasable = true };
            db.AddScoped(item); await db.SaveChangesAsync(); itemId = item.Id;
        }
        var source = await NewApprovedOrderSource("Catalog classification new commitment fence",
            purchaseType: "Goods", catalogItemId: itemId);
        var requestId = source.GetProperty("id").GetGuid();
        Require(source.GetProperty("lines")[0].GetProperty("purchaseType").GetString() == "GOODS",
            "Catalog source was not canonically classified before approval.");
        await SetSyntheticCatalogKind(itemId, "Service");
        var before = await ExactInputFootprint();
        var conversion = OrderConversion(source, supplier);
        var denied = await Send(HttpMethod.Post, Root + "/orders/from-approved-sources", OrderBuyer, conversion, Guid.NewGuid());
        await Check(denied, HttpStatusCode.Conflict);
        Require((await denied.Content.ReadAsStringAsync()).Contains("OrderSourceCatalogClassificationUnverified", StringComparison.Ordinal),
            "Conversion failed for an unrelated fixture reason.");
        Require(before == await ExactInputFootprint(), "Unproved catalog source conversion persisted effects.");
        var retained = await Get($"{Root}/requests/{requestId}", OrderRequester);
        Require(retained.GetProperty("lines")[0].GetProperty("purchaseType").GetString() == "GOODS",
            "Catalog drift rewrote approved source evidence.");
        await SetSyntheticCatalogKind(itemId, "Goods");
        var converted = await Send(HttpMethod.Post, Root + "/orders/from-approved-sources", OrderBuyer, conversion, Guid.NewGuid());
        await Check(converted, HttpStatusCode.Created);
        var order = await converted.Content.ReadFromJsonAsync<JsonElement>(); var id = order.GetProperty("id").GetGuid();
        await SetSyntheticCatalogKind(itemId, "Service");
        before = await ExactInputFootprint();
        var funding = await Send(HttpMethod.Post, $"{Root}/orders/{id}/finance-confirmations", OrderFinance,
            OrderFunding(order, "CATALOG-CLASSIFICATION-COMMITMENT", await SourceReservation(requestId), "CATALOG-CLASSIFICATION-EVIDENCE"), Guid.NewGuid());
        await Check(funding, HttpStatusCode.Conflict);
        Require((await funding.Content.ReadAsStringAsync()).Contains("OrderSourceCatalogClassificationUnverified", StringComparison.Ordinal),
            "Funding failed for an unrelated fixture reason.");
        Require(before == await ExactInputFootprint(), "Unproved catalog funding persisted effects.");
        await SetSyntheticCatalogKind(itemId, "Goods");
        var funded = await Send(HttpMethod.Post, $"{Root}/orders/{id}/finance-confirmations", OrderFinance,
            OrderFunding(order, "CATALOG-CLASSIFICATION-COMMITMENT", await SourceReservation(requestId), "CATALOG-CLASSIFICATION-EVIDENCE"), Guid.NewGuid());
        await Check(funded, HttpStatusCode.OK); order = await funded.Content.ReadFromJsonAsync<JsonElement>();
        await SetSyntheticCatalogKind(itemId, "Service");
        before = await ExactInputFootprint();
        var submit = new { expectedOrderVersion = order.GetProperty("version").GetInt64() };
        var deniedSubmit = await Send(HttpMethod.Post, $"{Root}/orders/{id}/submit", OrderBuyer, submit, Guid.NewGuid());
        await Check(deniedSubmit, HttpStatusCode.Conflict);
        Require((await deniedSubmit.Content.ReadAsStringAsync()).Contains("OrderSourceCatalogClassificationUnverified", StringComparison.Ordinal),
            "Submit failed for an unrelated fixture reason.");
        Require(before == await ExactInputFootprint(), "Unproved catalog submit persisted effects.");
        await SetSyntheticCatalogKind(itemId, "Goods");
        await Check(await Send(HttpMethod.Post, $"{Root}/orders/{id}/submit", OrderBuyer, submit, Guid.NewGuid()), HttpStatusCode.Created);
        var history = await Get($"{Root}/orders/{id}/commercial-history", OrderManager2);
        var caseView = history.GetProperty("cases")[0];
        var assignment = caseView.GetProperty("stages")[0].GetProperty("assignments")[0];
        var approved = await Send(HttpMethod.Post, $"{Root}/orders/{id}/assignments/{assignment.GetProperty("id").GetGuid()}/decisions", OrderManager2,
            new { action = "Approve", expectedOrderVersion = history.GetProperty("order").GetProperty("version").GetInt64(),
                expectedCaseVersion = caseView.GetProperty("version").GetInt64(),
                expectedAssignmentVersion = assignment.GetProperty("version").GetInt64() }, Guid.NewGuid());
        await Check(approved, HttpStatusCode.OK); order = await approved.Content.ReadFromJsonAsync<JsonElement>();
        await SetSyntheticCatalogKind(itemId, "Service");
        before = await ExactInputFootprint();
        var issue = new { expectedOrderVersion = order.GetProperty("version").GetInt64() };
        var deniedIssue = await Send(HttpMethod.Post, $"{Root}/orders/{id}/issue", OrderBuyer, issue, Guid.NewGuid());
        await Check(deniedIssue, HttpStatusCode.Conflict);
        Require((await deniedIssue.Content.ReadAsStringAsync()).Contains("OrderSourceCatalogClassificationUnverified", StringComparison.Ordinal),
            "Issue failed for an unrelated fixture reason.");
        Require(before == await ExactInputFootprint(), "Unproved catalog issue persisted effects.");
        await SetSyntheticCatalogKind(itemId, "Goods");
        await Check(await Send(HttpMethod.Post, $"{Root}/orders/{id}/issue", OrderBuyer, issue, Guid.NewGuid()), HttpStatusCode.OK);
        Console.WriteLine("PASS order: owned catalog drift denies conversion/funding/submit/issue without rewriting approved evidence; restored exact owner permits each action.");
    }

    private async Task SetSyntheticCatalogKind(Guid id, string kind)
    {
        // Disposable fixture owner only: model authoritative catalog drift, never rewrite PR pins.
        using var services = Scope(OrderBuyer);
        var db = services.ServiceProvider.GetRequiredService<ProcurementDbContext>();
        var item = await db.CatalogItems.SingleAsync(x => x.Id == id); item.Kind = kind; await db.SaveChangesAsync();
    }
}
