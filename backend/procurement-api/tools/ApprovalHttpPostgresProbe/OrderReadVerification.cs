using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Mightyfin.Erp.Hrm.Infrastructure.Data;
using Mightyfin.Erp.Hrm.Application;
using Mightyfin.Erp.Hrm.Application.ConfigAndExtras;
using Mightyfin.Erp.Procurement.Infrastructure.Data;
using Mightyfin.Erp.Procurement.Application;

sealed partial class Probe
{
    private async Task VerifyOrderDraftReadContract(Guid orderId, Guid conversionKey)
    {
        // Remove the fixture's formerly redundant broad read grant: Finance must work using
        // only its actual purpose-specific action, as with the default production role.
        using (var services = Scope(OrderFinance))
        {
            var core = services.ServiceProvider.GetRequiredService<HrmDbContext>();
            var role = await core.TenantRoleAssignments.SingleAsync(x => x.RoleKey == "finance_approver");
            role.PermissionsCsv = string.Join(',', role.PermissionsCsv.Split(',').Where(x => x.Trim() != "procurement-po-read"));
            await core.SaveChangesAsync();
        }
        await Expect(HttpStatusCode.Forbidden, HttpMethod.Get, $"{Root}/orders/{orderId}", OrderFinance);
        await Expect(HttpStatusCode.Forbidden, HttpMethod.Get, $"{Root}/orders/{orderId}/commercial-history", OrderFinance);
        var finance = await Get($"{Root}/orders/execution-worklist?view=finance", OrderFinance);
        Require(finance.GetProperty("total").GetInt32() == 1
            && finance.GetProperty("items")[0].GetProperty("id").GetGuid() == orderId, "Finance discovery lost the scoped draft.");
        var detail = await Get($"{Root}/orders/{orderId}/execution-detail", OrderFinance);
        Require(detail.GetProperty("order").GetProperty("lines")[0].GetProperty("quantity").ValueKind == JsonValueKind.String
            && detail.GetProperty("totals").GetProperty("grossAmount").GetString() == "100"
            && detail.GetProperty("sourceReservationReferences").GetArrayLength() == 1,
            "Finance form lacks exact amounts and original source reservation references.");
        Require(ActionCandidate(detail, "Finance"), "Independent Finance cannot open its confirmation form.");
        var dispatch = await Get($"{Root}/orders/execution-worklist?view=dispatch", OrderManager1);
        Require(dispatch.GetProperty("total").GetInt32() == 0, "Dispatch queue exposed an unissued draft.");
        await Expect(HttpStatusCode.NotFound, HttpMethod.Get, $"{Root}/orders/{orderId}/execution-detail", OrderManager1);
        await Expect(HttpStatusCode.Forbidden, HttpMethod.Get, $"{Root}/orders/execution-worklist?view=finance", OrderRequester);
        var recovery = await Get($"{Root}/orders/command-receipts/Convert/{conversionKey:D}", OrderBuyer);
        Require(recovery.GetProperty("state").GetString() == "RecordedHistoricalOutcome"
            && !recovery.GetProperty("replayAuthorized").GetBoolean()
            && recovery.GetProperty("purchaseOrderId").GetGuid() == orderId, "Conversion response recovery changed authority or target.");
        await Expect(HttpStatusCode.Forbidden, HttpMethod.Get, $"{Root}/orders/command-receipts/Convert/{conversionKey:D}", OrderFinance);
        await Expect(HttpStatusCode.NotFound, HttpMethod.Get, $"{Root}/orders/command-receipts/Finance/{conversionKey:D}", OrderFinance);
        await VerifyOrderReadSelectedBranch(orderId, conversionKey);
        Console.WriteLine("PASS order reads: default Finance purpose-specific discovery/detail + unissued dispatch denial + actor/operation-isolated historical receipt");
    }

    private async Task VerifyOrderReadSelectedBranch(Guid orderId, Guid conversionKey)
    {
        Guid otherBranch;
        using (var services = Scope(OrderBuyer))
        {
            var core = services.ServiceProvider.GetRequiredService<HrmDbContext>();
            var other = new Mightyfin.Erp.Hrm.Domain.Entities.WorkLocation
                { Code = "READ-OTHER", Name = "Other synthetic read context", LegalEntityId = OrderCompany };
            core.WorkLocations.Add(other); await core.SaveChangesAsync(); otherBranch = other.Id;
        }
        async Task<HttpResponseMessage> Read(string path, Guid actor)
        {
            var request = new HttpRequestMessage(HttpMethod.Get, path);
            request.Headers.Add("X-Probe-User", actor.ToString("D"));
            request.Headers.Add("X-Shell-Entity", OrderCompany.ToString("D"));
            request.Headers.Add("X-Shell-Location", otherBranch.ToString("D"));
            return await client.SendAsync(request);
        }
        var response = await Read($"{Root}/orders/execution-worklist?view=finance", OrderFinance);
        await Check(response, HttpStatusCode.OK);
        var queue = await response.Content.ReadFromJsonAsync<JsonElement>();
        Require(queue.GetProperty("total").GetInt32() == 0 && queue.GetProperty("items").GetArrayLength() == 0,
            "Organisation-wide Finance read leaked selected-branch counts or page content.");
        await Check(await Read($"{Root}/orders/{orderId}/execution-detail", OrderFinance), HttpStatusCode.NotFound);
        await Check(await Read($"{Root}/orders/command-receipts/Convert/{conversionKey:D}", OrderBuyer), HttpStatusCode.NotFound);
        Console.WriteLine("PASS order reads: explicit selected branch filters unconfined role counts/detail/recovery");
    }

    private async Task VerifyOrderDispatchReadContract(Guid orderId, Guid issuedVersionId, Guid issueKey)
    {
        var queue = await Get($"{Root}/orders/execution-worklist?view=dispatch&status=IssuePending&pageSize=1", OrderManager1);
        Require(queue.GetProperty("total").GetInt32() == 1 && queue.GetProperty("items").GetArrayLength() == 1
            && queue.GetProperty("items")[0].GetProperty("id").GetGuid() == orderId, "Dispatch queue lost an unassigned verifier's order.");
        var detail = await Get($"{Root}/orders/{orderId}/execution-detail", OrderManager1);
        Require(detail.GetProperty("issued")[0].GetProperty("id").GetGuid() == issuedVersionId
            && ActionCandidate(detail, "Delivery") && detail.GetProperty("configuredDeliveryChannels").GetArrayLength() > 0,
            "Dispatch detail lacks sealed version/allowed channel/form candidate.");
        var finance = await Get($"{Root}/orders/{orderId}/execution-detail", OrderFinance);
        Require(finance.GetProperty("financeEvidence").GetArrayLength() == 1
            && finance.GetProperty("financeStatus").GetString() == "CommitmentHeld",
            "Finance confirmer cannot read their historical confirmation after issue.");
        var recovery = await Get($"{Root}/orders/command-receipts/Issue/{issueKey:D}?orderId={orderId:D}", OrderBuyer);
        Require(recovery.GetProperty("recordedResult").GetProperty("status").GetString() == "IssuePending"
            && !recovery.GetProperty("replayAuthorized").GetBoolean(), "Issue history was treated as current replay approval.");
        await Expect(HttpStatusCode.NotFound, HttpMethod.Get, $"{Root}/orders/command-receipts/Issue/{issueKey:D}?orderId={Guid.NewGuid():D}", OrderBuyer);
        using (var services = Scope(OrderBuyer))
        {
            var core = services.ServiceProvider.GetRequiredService<HrmDbContext>();
            var role = await core.TenantRoleAssignments.SingleOrDefaultAsync(x => x.RoleKey == "procurement_buyer");
            var temporary = role is null;
            if (role is null)
            {
                role = new TenantRoleAssignment { RoleKey = "procurement_buyer", RoleName = "Synthetic buyer override", Active = true,
                    PermissionsCsv = string.Join(',', ProcurementActionCatalog.ForRole("procurement_buyer")) };
                core.TenantRoleAssignments.Add(role);
            }
            var previous = role.PermissionsCsv;
            role.PermissionsCsv = string.Join(',', previous.Split(',').Where(x => x.Trim() != "procurement-po-issue"));
            await core.SaveChangesAsync();
            try { await Expect(HttpStatusCode.Forbidden, HttpMethod.Get, $"{Root}/orders/command-receipts/Issue/{issueKey:D}", OrderBuyer); }
            finally { if (temporary) core.TenantRoleAssignments.Remove(role); else role.PermissionsCsv = previous; await core.SaveChangesAsync(); }
        }
        Console.WriteLine("PASS order reads: nonassigned dispatch discovery + sealed version/channels + Finance history + current-operation grant recovery denial");
    }

    private async Task VerifyDecimalStringOrderTransport(Guid supplier)
    {
        var source = await NewApprovedOrderSource("Exact decimal-string PO transport");
        var requestId = source.GetProperty("id").GetGuid(); var sourceLineId = source.GetProperty("lines")[0].GetProperty("id").GetGuid();
        var available = await Get($"{Root}/orders/approved-sources?pageSize=100", OrderBuyer);
        var availableLine = available.GetProperty("items").EnumerateArray().Single(x => x.GetProperty("lineId").GetGuid() == sourceLineId);
        Require(availableLine.GetProperty("availableQuantityExact").GetString() == "1"
            && availableLine.GetProperty("approvedUnitPriceExact").GetString() == "100", "Approved-source exact decimal companions are missing.");
        var converted = await Send(HttpMethod.Post, Root + "/orders/from-approved-sources", OrderBuyer,
            new { draft = new { supplierId = supplier, currencyCode = "ZMW", lines = new[] {
                new { description = "Exact fractional item", unitCode = "each", quantity = "0.0001", unitPrice = "0.0001", taxAmount = "0.0001" } } },
                sources = new[] { new { draftLineIndex = 0, purchaseRequestId = requestId, purchaseRequestLineId = sourceLineId,
                    expectedRequestVersion = source.GetProperty("version").GetInt64() } } }, Guid.NewGuid());
        await Check(converted, HttpStatusCode.Created); var order = await converted.Content.ReadFromJsonAsync<JsonElement>();
        var id = order.GetProperty("id").GetGuid(); var reservation = await SourceReservation(requestId); var financeKey = Guid.NewGuid();
        var confirmed = await Send(HttpMethod.Post, $"{Root}/orders/{id}/finance-confirmations", OrderFinance,
            new { expectedOrderVersion = order.GetProperty("version").GetInt64(), commercialRevision = 1,
                commitmentReference = "EXACT-STRING-COMMITMENT", evidenceReference = "EXACT-STRING-EVIDENCE", currencyCode = "ZMW",
                confirmedGrossAmount = "0.00010001", sourceReservationReferences = new[] { reservation }, confirmedAt = DateTimeOffset.UtcNow }, financeKey);
        await Check(confirmed, HttpStatusCode.OK); order = await confirmed.Content.ReadFromJsonAsync<JsonElement>();
        var detail = await Get($"{Root}/orders/{id}/execution-detail", OrderFinance);
        Require(detail.GetProperty("order").GetProperty("lines")[0].GetProperty("quantity").GetString() == "0.0001"
            && detail.GetProperty("totals").GetProperty("lineBaseAmount").GetString() == ProcurementEvidenceEncoding.Decimal(0.00000001m)
            && detail.GetProperty("totals").GetProperty("grossAmount").GetString() == "0.00010001"
            && detail.GetProperty("financeEvidence")[0].GetProperty("confirmedGrossAmount").GetString() == "0.00010001",
            "Actual API transport changed exact quantity/product/eight-decimal gross.");
        using (var services = Scope(OrderFinance))
        {
            var db = services.ServiceProvider.GetRequiredService<ProcurementDbContext>();
            Require((await db.PurchaseOrderLines.SingleAsync(x => x.PurchaseOrderId == id)).Quantity == 0.0001m
                && (await db.OrderFinanceEvidence.SingleAsync(x => x.PurchaseOrderId == id)).ConfirmedGrossAmount == 0.00010001m,
                "Decimal-string binding did not survive persistence.");
        }
        var recovered = await Get($"{Root}/orders/command-receipts/Finance/{financeKey:D}", OrderFinance);
        Require(recovered.GetProperty("purchaseOrderId").GetGuid() == id, "Finance receipt recovery lost the source.");
        Guid aliasId;
        using (var services = Scope(OrderFinance))
        {
            var core = services.ServiceProvider.GetRequiredService<HrmDbContext>();
            var worker = await core.LocalUsers.Where(x => x.Id == OrderFinance).Select(x => x.WorkerId).SingleAsync();
            var alias = new Mightyfin.Erp.Hrm.Domain.Entities.LocalUser { Email = "read-recovery-alias@example.test",
                NormalizedEmail = "READ-RECOVERY-ALIAS@EXAMPLE.TEST", DisplayName = "Synthetic Finance alias",
                PasswordHash = "synthetic-no-login-secret", RolesCsv = "finance_approver", WorkerId = worker };
            core.LocalUsers.Add(alias); await core.SaveChangesAsync(); aliasId = alias.Id;
        }
        await Expect(HttpStatusCode.NotFound, HttpMethod.Get, $"{Root}/orders/command-receipts/Finance/{financeKey:D}", aliasId);

        Console.WriteLine("PASS order reads: actual mapped decimal-string quantity/rate/tax/8dp Finance amount binding + PostgreSQL + exact DTO roundtrip");
    }

    private static bool ActionCandidate(JsonElement detail, string code) => detail.GetProperty("actionCandidates")
        .EnumerateArray().Single(x => x.GetProperty("code").GetString() == code).GetProperty("canStart").GetBoolean();
}
