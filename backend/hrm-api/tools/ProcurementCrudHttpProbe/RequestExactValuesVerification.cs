using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;

namespace ProcurementCrudHttpProbe;
internal static partial class Program
{
    // Real mapped HTTP/PG, synthetic outer auth + existing synthetic supplier owner prerequisite.
    // No request is submitted/approved/funded and no source offer is owner-SQL fabricated.
    static async Task ExactRequests(HttpClient client)
    {
        stage = "exact-request-real-catalog-owner-setup";
        var category = await Send(client, HttpMethod.Post, "/configuration/categories", new { code = "REQ_EXACT", name = "Exact request prerequisite" }, HttpStatusCode.Created);
        await ExactRequestSend(client, HttpMethod.Post, "/configuration/categories/" + Id(category) + "/activate", new { version = Version(category), reason = "Synthetic independent prerequisite review" }, HttpStatusCode.OK, Other);
        var item = await Send(client, HttpMethod.Post, "/items", new { code = "REQ-EXACT", name = "Exact request source", kind = "Goods", categoryCode = "REQ_EXACT", unitCode = "each" }, HttpStatusCode.Created);
        var itemId = Id(item);
        await ExactRequestSend(client, HttpMethod.Post, "/items/" + itemId + "/activate", new { version = Version(item), reason = "Synthetic independent prerequisite review" }, HttpStatusCode.OK, Other);
        var entry = await Send(client, HttpMethod.Post, "/items/" + itemId + "/catalog-entries", new { supplierId = approvedSupplier, currencyCode = "ZMW", unitCode = "each", unitPrice = "12345678901234.5678", effectiveFrom = DateTimeOffset.UtcNow.AddDays(-1), effectiveTo = DateTimeOffset.UtcNow.AddDays(1) }, HttpStatusCode.Created);
        var entryId = Id(entry);
        await ExactRequestSend(client, HttpMethod.Post, $"/items/{itemId}/catalog-entries/{entryId}/publish", new { version = Version(entry), reason = "Synthetic independent catalog review" }, HttpStatusCode.OK, Other);
        var catalogPage = await Get(client, "/catalog");
        var published = catalogPage.GetProperty("items").EnumerateArray().Single(x => x.GetProperty("entryId").GetGuid() == entryId);
        Check(published.GetProperty("exactEstimatedUnitPrice").GetString() == "12345678901234.5678", "Published catalog original companion missing.");
        var protectedRows = await RequestStoreState(true);

        stage = "request-max18-4-string-capture-and-complete-owned-edit";
        var created = await Send(client, HttpMethod.Post, "/requests", new {
            purpose = "Exact maximum request", currencyCode = "ZMW", neededBy = "2030-01-01", costCenterRef = "HEADER-COST", projectRef = "HEADER-PROJECT",
            deliveryLocationRef = "Retained delivery", deliveryAddressText = "Retained address", fundingSourceRef = "Retained funding", notesToApprover = "Retained note",
            lines = new object[] {
                new { description = "Maximum exact original", unitCode = "each", requestedQuantity = "1", estimatedUnitPrice = "99999999999999.9999", discountPercent = "12.3456", taxCodeRef = "TAX-RETAINED", costCenterRef = "LINE-COST", projectRef = "LINE-PROJECT", neededBy = "2030-01-02", specificationDocumentRefId = (Guid?)null },
                new { description = "Retain removal history", unitCode = "each", requestedQuantity = "1", estimatedUnitPrice = "0" } } }, HttpStatusCode.Created);
        var id = Id(created); var path = "/requests/" + id;
        var original = created.GetProperty("lines")[0]; var removedId = Id(created.GetProperty("lines")[1]);
        Check(created.GetProperty("requestEditVersion").GetInt32() == 1 && original.GetProperty("estimatedUnitPrice").ValueKind == JsonValueKind.Number
            && original.GetProperty("exactAmounts").GetProperty("estimatedUnitPrice").GetString() == "99999999999999.9999", "Request maximum numeric compatibility/exact companion lost.");
        var update = ExactRequestEdit(created, 0); update["purpose"] = "Corrected exact request header";
        var saved = await Send(client, HttpMethod.Put, path, update);
        var reloaded = await Get(client, path);
        Check(Version(saved) == Version(created) + 1 && Version(reloaded) == Version(saved), "Request current CAS not preserved.");
        foreach (var key in new[] { "neededBy", "costCenterRef", "projectRef", "deliveryLocationRef", "deliveryAddressText", "fundingSourceRef", "notesToApprover" })
            Check(created.GetProperty(key).GetRawText() == reloaded.GetProperty(key).GetRawText(), "Request hidden header changed: " + key);
        var first = reloaded.GetProperty("lines").EnumerateArray().Single(x => Id(x) == Id(original));
        foreach (var key in new[] { "exactAmounts", "neededBy", "discountPercent", "taxCodeRef", "costCenterRef", "projectRef", "specificationDocumentRefId", "catalogItemId", "catalogEntryId", "catalogPublishedVersion", "preferredSupplierId" })
            Check(original.GetProperty(key).GetRawText() == first.GetProperty(key).GetRawText(), "Request hidden line changed: " + key);
        await using (var owner = Db())
        {
            var rows = await owner.PurchaseRequestLines.AsNoTracking().Where(x => x.PurchaseRequestId == id).ToArrayAsync();
            Check(rows.Length == 2 && rows.Single(x => x.Id == Id(original)).EstimatedUnitPrice == 99999999999999.9999m
                && rows.Single(x => x.Id == Id(original)).DiscountPercent == 12.3456m, "PostgreSQL request exact original was rounded.");
            var removed = rows.Single(x => x.Id == removedId);
            Check(removed.ApprovalStatus == "Cancelled" && removed.RequestedQuantity == 1 && removed.CancelledQuantity == 1, "Removed request line was deleted or original lost.");
        }

        var allRows = await RequestStoreState(false);
        await Send(client, HttpMethod.Put, path, ExactRequestEdit(created, 0), HttpStatusCode.Conflict);
        await Send(client, HttpMethod.Get, path, null, HttpStatusCode.Forbidden, foreign: true);
        await Send(client, HttpMethod.Put, path, ExactRequestEdit(saved, 0), HttpStatusCode.Forbidden, foreign: true);
        await ExactRequestSend(client, HttpMethod.Get, path, null, HttpStatusCode.NotFound, Other);
        Check(allRows == await RequestStoreState(false), "Stale/foreign/nonowner request denial changed owning rows.");
        foreach (var token in new[] { "1.00001", "1.00000000000000000000000000001", "1e-1000", "\"100000000000000\"" })
        {
            var line = "{\"description\":\"Invalid exact\",\"unitCode\":\"each\",\"requestedQuantity\":1,\"estimatedUnitPrice\":" + token + "}";
            await ExactRequestRaw(client, HttpMethod.Post, "/requests", "{\"purpose\":\"Must not create\",\"lines\":[" + line + "]}", HttpStatusCode.BadRequest);
            await ExactRequestRaw(client, HttpMethod.Put, path, "{\"version\":" + Version(saved) + ",\"purpose\":\"Must not partially edit\",\"lines\":[" + line + "]}", HttpStatusCode.BadRequest);
            Check(allRows == await RequestStoreState(false), "Invalid request raw lexeme changed original/data/audit/outbox.");
        }

        var numericWide = await Send(client, HttpMethod.Post, "/requests", new { purpose = "Legacy numeric maximum original", lines = new[] {
            new { description = "Maximum numeric lexeme", unitCode = "each", requestedQuantity = 1m, estimatedUnitPrice = 99999999999999.9999m } } }, HttpStatusCode.Created);
        var numericReload = await Get(client, "/requests/" + Id(numericWide));
        Check(numericReload.GetProperty("lines")[0].GetProperty("exactAmounts").GetProperty("estimatedUnitPrice").GetString() == "99999999999999.9999", "Legacy maximum numeric lexeme changed exact original.");
        var tiny = await Send(client, HttpMethod.Post, "/requests", new { purpose = "Legacy numeric exact products", lines = new[] {
            new { description = "Tiny1", unitCode = "each", requestedQuantity = 0.0001m, estimatedUnitPrice = 0.0001m },
            new { description = "Tiny2", unitCode = "each", requestedQuantity = 0.0001m, estimatedUnitPrice = 0.0001m } } }, HttpStatusCode.Created);
        await using (var owner = Db())
        {
            var tinyLines = await owner.PurchaseRequestLines.AsNoTracking().Where(x => x.PurchaseRequestId == Id(tiny)).ToArrayAsync();
            Check(tinyLines.Sum(x => x.RequestedQuantity * x.EstimatedUnitPrice) == 0.00000002m, "Legacy numeric8dp product was rounded before persistence.");
        }
        object CatalogLine(Guid? lineId = null) => new { id = lineId, description = "Bound exact catalog original", unitCode = "each", requestedQuantity = "1", estimatedUnitPrice = "12345678901234.5678", catalogItemId = itemId, catalogEntryId = entryId, preferredSupplierId = approvedSupplier, categoryCode = "REQ_EXACT", purchaseType = "Goods" };
        var catalog = await Send(client, HttpMethod.Post, "/requests", new { purpose = "Exact catalog request", currencyCode = "ZMW", lines = new[] { CatalogLine() } }, HttpStatusCode.Created);
        var corrected = ExactRequestEdit(catalog, 0); corrected["purpose"] = "Catalog header corrected";
        var catalogSaved = await Send(client, HttpMethod.Put, "/requests/" + Id(catalog), corrected);
        var catalogReload = await Get(client, "/requests/" + Id(catalog));
        foreach (var key in new[] { "catalogItemId", "catalogEntryId", "catalogPublishedVersion", "preferredSupplierId", "exactAmounts" })
            Check(catalog.GetProperty("lines")[0].GetProperty(key).GetRawText() == catalogReload.GetProperty("lines")[0].GetProperty(key).GetRawText(), "Request catalog binding changed: " + key);
        Check(catalogReload.GetProperty("lines")[0].GetProperty("catalogPublishedVersion").GetInt32() == published.GetProperty("publishedVersion").GetInt32(), "Selected published catalog version not retained.");
        var ids = new[] { id, Id(numericWide), Id(tiny), Id(catalog) };
        await using (var owner = Db())
        {
            Check(await owner.PurchaseRequests.CountAsync(x => ids.Contains(x.Id) && x.Status == "Draft" && x.SubmittedVersion == null && x.ApprovalInstanceRef == null) == 4, "Exact draft proof created lifecycle authority.");
            Check(!await owner.PolicyEvaluations.AnyAsync(x => ids.Contains(x.PurchaseRequestId)) && !await owner.BudgetCheckSnapshots.AnyAsync(x => ids.Contains(x.PurchaseRequestId)), "Exact draft proof created policy/Finance effects.");
            Check((await owner.PurchaseRequestLines.AsNoTracking().SingleAsync(x => x.PurchaseRequestId == Id(numericWide))).EstimatedUnitPrice == 99999999999999.9999m, "Legacy maximum numeric PostgreSQL original changed.");
            Check((await owner.PurchaseRequestLines.AsNoTracking().SingleAsync(x => x.PurchaseRequestId == Id(catalog))).EstimatedUnitPrice == 12345678901234.5678m, "Catalog original PostgreSQL amount changed.");
        }
        Check(protectedRows == await RequestStoreState(true), "Exact request operations changed unrelated Procurement rows/audits/outbox/history.");
    }

    static JsonObject ExactRequestEdit(JsonElement current, params int[] lineIndexes)
    {
        var body = new JsonObject { ["version"] = Version(current) };
        foreach (var key in new[] { "purpose", "currencyCode", "neededBy", "costCenterRef", "projectRef", "deliveryLocationRef", "deliveryAddressText", "fundingSourceRef", "notesToApprover" })
            body[key] = JsonNode.Parse(current.GetProperty(key).GetRawText());
        var lines = new JsonArray();
        foreach (var index in lineIndexes)
        {
            var source = current.GetProperty("lines")[index]; var line = new JsonObject();
            foreach (var key in new[] { "id", "description", "unitCode", "catalogItemId", "catalogEntryId", "catalogPublishedVersion", "preferredSupplierId", "categoryCode", "purchaseType", "neededBy", "taxCodeRef", "costCenterRef", "projectRef", "specificationDocumentRefId" })
                line[key] = JsonNode.Parse(source.GetProperty(key).GetRawText());
            foreach (var key in new[] { "requestedQuantity", "estimatedUnitPrice", "discountPercent" })
                line[key] = JsonNode.Parse(source.GetProperty("exactAmounts").GetProperty(key).GetRawText());
            lines.Add(line);
        }
        body["lines"] = lines; return body;
    }
    static Task<JsonElement> ExactRequestSend(HttpClient client, HttpMethod method, string path, object? body, HttpStatusCode expected, Guid actor)
        => ExactRequestRaw(client, method, path, body is null ? null : JsonSerializer.Serialize(body, new JsonSerializerOptions(JsonSerializerDefaults.Web)), expected, actor);
    static async Task<JsonElement> ExactRequestRaw(HttpClient client, HttpMethod method, string path, string? body, HttpStatusCode expected, Guid? actor = null)
    {
        stage = "Exact request HTTP " + method + " " + path;
        using var request = new HttpRequestMessage(method, Root + path);
        request.Headers.Add("X-Crud-Actor", (actor ?? Actor).ToString()); request.Headers.Add("X-Shell-Entity", Company.ToString()); request.Headers.Add("X-Shell-Location", Branch.ToString());
        if (body is not null) request.Content = new StringContent(body, Encoding.UTF8, "application/json");
        using var response = await client.SendAsync(request); Interlocked.Increment(ref requests);
        var text = await response.Content.ReadAsStringAsync();
        Check(response.StatusCode == expected, $"{stage}: expected {(int)expected}, got {(int)response.StatusCode}; code={SafeCode(text)}");
        return string.IsNullOrWhiteSpace(text) ? default : JsonDocument.Parse(text).RootElement.Clone();
    }
    // Independent owner connection, every persisted Procurement table, including audit/outbox/history.
    // The protected variant permits only the intended request/header/line and request audit changes.
    static async Task<string> RequestStoreState(bool excludeRequests)
    {
        var rows = await PolicyStoreRows(); // Existing private owner full-table snapshot helper.
        if (excludeRequests)
        {
            rows.Remove("purchase_requests"); rows.Remove("purchase_request_lines");
            rows["audit_events"] = await Scalar("SELECT COALESCE(jsonb_agg(to_jsonb(t) ORDER BY to_jsonb(t)::text),'[]'::jsonb)::text FROM procurement.audit_events t WHERE record_type NOT IN ('PurchaseRequest','PurchaseRequestLine')");
        }
        return JsonSerializer.Serialize(rows);
    }
}
