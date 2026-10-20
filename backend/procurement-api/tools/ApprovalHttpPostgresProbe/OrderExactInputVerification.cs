using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Mightyfin.Erp.Procurement.Infrastructure.Data;

sealed partial class Probe
{
    private async Task VerifyOrderExactInput(Guid supplier)
    {
        // Real legacy draft route, actual PostgreSQL. Decimal input annotations must
        // preserve the existing numeric response, not change it to authority strings.
        var draft = JsonSerializer.SerializeToNode(new { supplierId = supplier, currencyCode = "ZMW",
            lines = new[] { new { description = "Exact transport draft", unitCode = "each",
                quantity = "0.0001", unitPrice = "0.0001", taxAmount = "0.0001" } } }, Json)!;
        var created = await SendRawOrder(HttpMethod.Post, Root + "/orders", OrderBuyer, draft);
        await Check(created, HttpStatusCode.Created);
        var legacy = await created.Content.ReadFromJsonAsync<JsonElement>(); var legacyId = legacy.GetProperty("id").GetGuid();
        Require(legacy.GetProperty("lines")[0].GetProperty("taxAmount").ValueKind == JsonValueKind.Number
            && legacy.GetProperty("lines")[0].GetProperty("taxAmount").GetDecimal() == .0001m,
            "Exact input changed legacy numeric response or rounded stored tax.");
        var loaded = await Get($"{Root}/orders/{legacyId}", OrderBuyer);
        Require(loaded.GetProperty("lines")[0].GetProperty("taxAmount").GetDecimal() == .0001m,
            "Legacy draft reload lost exact numeric tax.");
        draft["lines"]![0]!["id"] = loaded.GetProperty("lines")[0].GetProperty("id").GetGuid();
        var source = await NewApprovedOrderSource("Raw lexeme rejection before PO persistence");
        var conversion = JsonSerializer.SerializeToNode(OrderConversion(source, supplier), Json)!;
        var sourceOrder = await Send(HttpMethod.Post, Root + "/orders/from-approved-sources", OrderBuyer,
            OrderConversion(source, supplier), Guid.NewGuid());
        await Check(sourceOrder, HttpStatusCode.Created);
        var order = await sourceOrder.Content.ReadFromJsonAsync<JsonElement>(); var id = order.GetProperty("id").GetGuid();
        var sourceDraft = (JsonObject)conversion["draft"]!.DeepClone();
        sourceDraft["lines"]![0]!["id"] = (await Get($"{Root}/orders/{id}", OrderBuyer))
            .GetProperty("lines")[0].GetProperty("id").GetGuid();
        var before = await ExactInputFootprint();
        foreach (var (field, raw) in new[]
        {
            ("taxAmount", "12.34567"), ("taxAmount", "\"12.34567\""),
            ("quantity", "1.00000000000000000000000000001"),
            ("unitPrice", "100.000000000000000000000000001"),
            ("quantity", "1e-1000"), ("taxAmount", "1e-8"),
        })
        {
            var invalidDraft = draft.DeepClone(); invalidDraft["lines"]![0]![field] = JsonNode.Parse(raw);
            await Check(await SendRawOrder(HttpMethod.Post, Root + "/orders", OrderBuyer, invalidDraft), HttpStatusCode.BadRequest);
            await Check(await SendRawOrder(HttpMethod.Put, $"{Root}/orders/{legacyId}", OrderBuyer,
                new JsonObject { ["version"] = legacy.GetProperty("version").GetInt64(), ["draft"] = invalidDraft.DeepClone() }), HttpStatusCode.BadRequest);
            var invalidConvert = conversion.DeepClone(); invalidConvert["draft"]!["lines"]![0]![field] = JsonNode.Parse(raw);
            await Check(await SendRawOrder(HttpMethod.Post, Root + "/orders/from-approved-sources", OrderBuyer,
                invalidConvert, Guid.NewGuid()), HttpStatusCode.BadRequest);
            var invalidEdit = sourceDraft.DeepClone(); invalidEdit["lines"]![0]![field] = JsonNode.Parse(raw);
            await Check(await SendRawOrder(HttpMethod.Put, $"{Root}/orders/{id}/source-draft", OrderBuyer,
                new JsonObject { ["expectedOrderVersion"] = order.GetProperty("version").GetInt64(),
                    ["reason"] = "Invalid transport must not write", ["draft"] = invalidEdit }, Guid.NewGuid()), HttpStatusCode.BadRequest);
        }
        foreach (var raw in new[] { "0.000000001", "1.00000000000000000000000000001", "1e-1000", "79228162514264337593543950336" })
        {
            var invalidFinance = JsonSerializer.SerializeToNode(OrderFunding(order, "INVALID-LEXEME", await SourceReservation(
                source.GetProperty("id").GetGuid()), "INVALID-LEXEME-EVIDENCE"), Json)!;
            invalidFinance["confirmedGrossAmount"] = JsonNode.Parse(raw);
            await Check(await SendRawOrder(HttpMethod.Post, $"{Root}/orders/{id}/finance-confirmations", OrderFinance,
                invalidFinance, Guid.NewGuid()), HttpStatusCode.BadRequest);
            var release = new JsonObject { ["expectedOrderVersion"] = order.GetProperty("version").GetInt64(),
                ["releasedGrossAmount"] = JsonNode.Parse(raw), ["currencyCode"] = "ZMW", ["evidenceReference"] = "INVALID-RELEASE",
                ["restoredReservationReferences"] = new JsonArray(), ["reason"] = "Invalid transport must not write",
                ["confirmedAt"] = DateTimeOffset.UtcNow };
            foreach (var operation in new[] { "finance-release-confirmations", "draft-finance-release-confirmations" })
                await Check(await SendRawOrder(HttpMethod.Post, $"{Root}/orders/{id}/{operation}", OrderFinance,
                    release.DeepClone(), Guid.NewGuid()), HttpStatusCode.BadRequest);
        }
        Require(await ExactInputFootprint() == before,
            "Rejected raw PO/Finance decimals changed PostgreSQL orders, versions, allocations, evidence, receipts, audit or outbox.");
        await VerifyExponentGrossRoundtrip(supplier);
        Console.WriteLine("PASS mapped PO exact input: raw numeric/string precision rejection across legacy/convert/edit/Finance, zero PostgreSQL effects, numeric compatibility, exponent8dp confirmation/release");
    }

    private async Task VerifyExponentGrossRoundtrip(Guid supplier)
    {
        var source = await NewApprovedOrderSource("Exact exponent gross roundtrip");
        var input = JsonSerializer.SerializeToNode(OrderConversion(source, supplier), Json)!;
        input["draft"]!["lines"]![0]!["quantity"] = "0.0001";
        input["draft"]!["lines"]![0]!["unitPrice"] = "0.0001";
        input["draft"]!["lines"]![0]!["taxAmount"] = "0";
        var response = await SendRawOrder(HttpMethod.Post, Root + "/orders/from-approved-sources", OrderBuyer, input, Guid.NewGuid());
        await Check(response, HttpStatusCode.Created); var order = await response.Content.ReadFromJsonAsync<JsonElement>();
        var id = order.GetProperty("id").GetGuid(); var reservation = await SourceReservation(source.GetProperty("id").GetGuid());
        var finance = JsonSerializer.SerializeToNode(OrderFunding(order, "EXPONENT-8DP", reservation, "EXPONENT-8DP-EVIDENCE"), Json)!;
        finance["confirmedGrossAmount"] = "1E-08";
        var confirmed = await SendRawOrder(HttpMethod.Post, $"{Root}/orders/{id}/finance-confirmations", OrderFinance, finance, Guid.NewGuid());
        await Check(confirmed, HttpStatusCode.OK); order = await confirmed.Content.ReadFromJsonAsync<JsonElement>();
        var released = await SendRawOrder(HttpMethod.Post, $"{Root}/orders/{id}/draft-finance-release-confirmations", OrderFinance,
            new JsonObject { ["expectedOrderVersion"] = order.GetProperty("version").GetInt64(), ["releasedGrossAmount"] = "1E-08",
                ["currencyCode"] = "ZMW", ["evidenceReference"] = "EXPONENT-8DP-RELEASE",
                ["restoredReservationReferences"] = new JsonArray(JsonValue.Create(reservation)),
                ["reason"] = "Synthetic exact release proof", ["confirmedAt"] = DateTimeOffset.UtcNow }, Guid.NewGuid());
        await Check(released, HttpStatusCode.OK);
        using var services = Scope(OrderFinance); var db = services.ServiceProvider.GetRequiredService<ProcurementDbContext>();
        Require((await db.OrderFinanceEvidence.SingleAsync(x => x.PurchaseOrderId == id)).ConfirmedGrossAmount == .00000001m
            && (await db.OrderFinanceResolutions.SingleAsync(x => x.PurchaseOrderId == id)).ReleasedGrossAmount == .00000001m,
            "Canonical exponent string did not survive actual Finance confirmation/release persistence at8dp.");
    }

    private Task<HttpResponseMessage> SendRawOrder(HttpMethod method, string path, Guid actor, JsonNode body, Guid? key = null)
    {
        var request = new HttpRequestMessage(method, path);
        request.Headers.Add("X-Probe-User", actor.ToString("D")); request.Headers.Add("X-Shell-Entity", company.ToString("D"));
        request.Headers.Add("X-Shell-Location", branch.ToString("D"));
        if (key.HasValue) request.Headers.Add("Idempotency-Key", key.Value.ToString("D"));
        request.Content = new StringContent(body.ToJsonString(), Encoding.UTF8, "application/json");
        return client.SendAsync(request);
    }

    private async Task<string> ExactInputFootprint()
    {
        using var services = Scope(OrderBuyer); var db = services.ServiceProvider.GetRequiredService<ProcurementDbContext>();
        return JsonSerializer.Serialize(new
        {
            orders = await db.PurchaseOrders.OrderBy(x => x.Id).Select(x => new { x.Id, x.Version, x.Status, x.UpdatedAt }).ToArrayAsync(),
            lines = await db.PurchaseOrderLines.OrderBy(x => x.Id).Select(x => new { x.Id, x.Version, x.Quantity, x.UnitPrice, x.TaxAmount }).ToArrayAsync(),
            requests = await db.PurchaseRequests.OrderBy(x => x.Id).Select(x => new { x.Id, x.Version, x.UpdatedAt }).ToArrayAsync(),
            sourceLines = await db.PurchaseRequestLines.OrderBy(x => x.Id).Select(x => new { x.Id, x.Version, x.AllocatedQuantity, x.OrderedQuantity }).ToArrayAsync(),
            allocations = await db.PurchaseRequestAllocations.OrderBy(x => x.Id).Select(x => new { x.Id, x.Version, x.Quantity, x.EstimatedValue, x.Status }).ToArrayAsync(),
            receipts = await db.CommercialCommandReceipts.CountAsync(), finance = await db.OrderFinanceEvidence.CountAsync(),
            resolutions = await db.OrderFinanceResolutions.CountAsync(), ownership = await db.OrderCommitmentOwnership.CountAsync(),
            audit = await db.AuditEvents.CountAsync(), outbox = await db.OutboxEvents.CountAsync(),
        }, Json);
    }
}
