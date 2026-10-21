using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Mightyfin.Erp.Hrm.Api;
using Mightyfin.Erp.Hrm.Application;
using Mightyfin.Erp.Hrm.Domain.Entities;
using Mightyfin.Erp.Hrm.Infrastructure.Data;
using Mightyfin.Erp.Procurement.Application;
using Mightyfin.Erp.Procurement.Domain;
using Mightyfin.Erp.Procurement.Infrastructure.Data;
using Npgsql;

sealed partial class Probe
{
    public async Task VerifyGovernedRfq()
    {
        Require(OrderDistinctRuntime, "RFQ proof requires bootstrap separated from finite runtime.");
        await RuntimePrivileges.AssertDeniedAsync(runtimeConnection, fixtureServices, rfq: true);
        await VerifySourcingOriginalRequestUpdatePrivilege(); // genuine P28 + frozen CHECKs, never fake-ready
        await Authority(manager1, "RFQ-PR-M1"); await Authority(manager2, "RFQ-PR-M2");
        await PublishRfqRequestPolicy(true);
        var a = await NewApprovedOrderSource("RFQ source A", 5, 10, 50);
        var b = await NewApprovedOrderSource("RFQ source B", 3, 10, 30);
        var raceSource = await NewApprovedOrderSource("RFQ concurrent source", 1, 10, 10);
        var untouched = await NewApprovedOrderSource("Untouched competitive source for direct exclusion", 1, 10, 10);
        var tiny = await NewApprovedOrderSource("Exact eight-place hold value", 0.0001m, 0.0001m, 0.0001m);
        await SetFiniteRfqActor();
        var picker = await Get(Root + "/sourcing/approved-sources?page=1&pageSize=25", buyer);
        Require(picker.GetProperty("items").EnumerateArray().Any(x => x.GetProperty("purchaseRequestLineId").GetGuid() == SourceLine(a)
            && x.GetProperty("minimumQuoteCount").GetInt32() == 0 && x.GetProperty("availableQuantity").GetString() == "5"),
            "Real approved competitive source/explicit voluntary zero-quote rule absent from scoped picker.");
        var e1body = RfqCreate("Consolidated first hold", (a, "3"), (b, "2")); var e1key = Guid.NewGuid();
        var before = await RfqFootprint();
        await Expect(HttpStatusCode.UnprocessableEntity, HttpMethod.Post, Root + "/sourcing/from-approved-sources", buyer,
            RfqCreate("Duplicate line", (a, "1"), (a, "1")), Guid.NewGuid());
        await Expect(HttpStatusCode.Conflict, HttpMethod.Post, Root + "/sourcing/from-approved-sources", buyer,
            RfqCreate("Over approved", (a, "6")), Guid.NewGuid());
        await Expect(HttpStatusCode.Forbidden, HttpMethod.Post, Root + "/sourcing/from-approved-sources", requester, e1body, Guid.NewGuid());
        Require(before == await RfqFootprint(), "RFQ malformed/overallocated/action-denied command persisted effects.");
        var created = await Send(HttpMethod.Post, Root + "/sourcing/from-approved-sources", buyer, e1body, e1key);
        await Check(created, HttpStatusCode.Created); var originalResponse = await created.Content.ReadAsStringAsync();
        var e1 = JsonSerializer.Deserialize<JsonElement>(originalResponse).GetProperty("eventId").GetGuid();
        var e2source = await Get($"{Root}/requests/{a.GetProperty("id").GetGuid()}", requester);
        var e2key = Guid.NewGuid();
        var e2 = await Send(HttpMethod.Post, Root + "/sourcing/from-approved-sources", buyer, RfqCreate("Remaining A", (e2source, "2")), e2key);
        await Check(e2, HttpStatusCode.Created); var e2id = (await e2.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("eventId").GetGuid();
        await AssertRfqBalance(a, 5); await AssertRfqBalance(b, 2);
        before = await RfqFootprint();
        var replay = await Send(HttpMethod.Post, Root + "/sourcing/from-approved-sources", buyer, e1body, e1key);
        await Check(replay, HttpStatusCode.Created); Require(originalResponse == await replay.Content.ReadAsStringAsync(), "Original Create replay bytes changed.");
        var recovered = await Get($"{Root}/sourcing/command-receipts/Create/{e1key}", buyer);
        Require(recovered.GetProperty("responseJson").GetString() == originalResponse, "GET recovery did not preserve original response bytes.");
        await Expect(HttpStatusCode.Conflict, HttpMethod.Post, Root + "/sourcing/from-approved-sources", buyer,
            RfqCreate("Changed original command", (a, "3"), (b, "2")), e1key);
        await Expect(HttpStatusCode.NotFound, HttpMethod.Get, $"{Root}/sourcing/command-receipts/Create/{Guid.NewGuid()}", buyer);
        Require(before == await RfqFootprint(), "RFQ replay/read/recovery changed evidence or source revisions.");
        await VerifyRfqBranchAndOwnerDenials(e1, e1key);
        await VerifyRfqCurrentActionDenial(e1, e1key);
        var detail = await Get($"{Root}/sourcing/{e1}/source-draft", buyer);
        Require(detail.GetProperty("canPublish").ValueKind == JsonValueKind.False
            && detail.GetProperty("financeReservationChanged").ValueKind == JsonValueKind.False, "Internal hold falsely claimed publication/Finance effect.");
        var cancelBody = new { expectedEventVersion = detail.GetProperty("version").GetInt64(),
            originalDraftFingerprint = detail.GetProperty("originalDraftFingerprint").GetString(), reason = "Synthetic demand no longer needs this consolidated draft" };
        var cancelKey = Guid.NewGuid();
        var cancel = await Send(HttpMethod.Post, $"{Root}/sourcing/{e1}/source-cancel", buyer, cancelBody, cancelKey);
        await Check(cancel, HttpStatusCode.OK); var originalCancel = await cancel.Content.ReadAsStringAsync();
        await AssertRfqBalance(a, 2); await AssertRfqBalance(b, 0);
        before = await RfqFootprint();
        var cancelledReplay = await Send(HttpMethod.Post, $"{Root}/sourcing/{e1}/source-cancel", buyer, cancelBody, cancelKey);
        await Check(cancelledReplay, HttpStatusCode.OK); Require(originalCancel == await cancelledReplay.Content.ReadAsStringAsync(), "Cancel replay changed bytes.");
        Require((await Get($"{Root}/sourcing/command-receipts/Cancel/{cancelKey}", buyer)).GetProperty("responseJson").GetString() == originalCancel,
            "Cancel GET recovery changed bytes.");
        var oldCreate = await Send(HttpMethod.Post, Root + "/sourcing/from-approved-sources", buyer, e1body, e1key);
        await Check(oldCreate, HttpStatusCode.Created); Require(originalResponse == await oldCreate.Content.ReadAsStringAsync(), "Historic Create after cancellation was reinterpreted.");
        Require(before == await RfqFootprint(), "Historic cancellation/Create replay caused new transitions.");
        await CancelRfq(e2id); await AssertRfqBalance(a, 0);
        await VerifyRfqConcurrency(raceSource);
        await VerifyRfqReceiptRollback(a);
        await VerifyRfqTinyValue(tiny);
        await VerifyCompetitiveSourceCannotConvertToDirectOrder(untouched);
        // Publish a distinct direct rule only for a NEW source; never rewrite approved sourcing intent.
        await RestoreRfqSetupActor(); await PublishRfqRequestPolicy(false);
        var direct = await NewApprovedOrderSource("Genuine direct-only source cannot RFQ", 1, 10, 10);
        await SetFiniteRfqActor(); before = await RfqFootprint();
        await Expect(HttpStatusCode.Conflict, HttpMethod.Post, Root + "/sourcing/from-approved-sources", buyer,
            RfqCreate("Direct intent excluded", (direct, "1")), Guid.NewGuid());
        Require(before == await RfqFootprint(), "Direct source was silently rerouted or reserved by RFQ.");
        Console.WriteLine("PASS RFQ actual approved competitive sources, partial/consolidated holds, complete balance, owned reads, current-action/branch/owner denials, history replay/recovery, cancellation and concurrent conservation. No invitation/bid/award/Finance/stock effect.");
    }

    private static Guid SourceLine(JsonElement request) => request.GetProperty("lines")[0].GetProperty("id").GetGuid();
    private static object RfqCreate(string title, params (JsonElement Source, string Quantity)[] selected) => new
    { title, sources = selected.Select(x => new { purchaseRequestId = x.Source.GetProperty("id").GetGuid(), purchaseRequestLineId = SourceLine(x.Source),
        expectedRequestVersion = x.Source.GetProperty("version").GetInt64(), quantity = x.Quantity }).ToArray() };
    private async Task CancelRfq(Guid id)
    {
        var detail = await Get($"{Root}/sourcing/{id}/source-draft", buyer);
        await Expect(HttpStatusCode.OK, HttpMethod.Post, $"{Root}/sourcing/{id}/source-cancel", buyer, new {
            expectedEventVersion = detail.GetProperty("version").GetInt64(), originalDraftFingerprint = detail.GetProperty("originalDraftFingerprint").GetString(),
            reason = "Synthetic bounded fixture cancellation" }, Guid.NewGuid());
    }
    private async Task AssertRfqBalance(JsonElement source, decimal expected)
    {
        using var services = Scope(buyer); var db = services.ServiceProvider.GetRequiredService<ProcurementDbContext>();
        var lineId = SourceLine(source); var line = await db.PurchaseRequestLines.AsNoTracking().SingleAsync(x => x.Id == lineId);
        var held = await db.PurchaseRequestAllocations.Where(x => x.PurchaseRequestLineId == lineId && x.Status == "Reserved").SumAsync(x => x.Quantity);
        Require(line.AllocatedQuantity == expected && held == expected && line.OrderedQuantity == 0,
            "Complete RFQ retained allocations and original request counters diverged.");
    }
    private async Task VerifyRfqConcurrency(JsonElement source)
    {
        var body = RfqCreate("Competing fresh transactions", (source, "1"));
        var results = await Task.WhenAll(Send(HttpMethod.Post, Root + "/sourcing/from-approved-sources", buyer, body, Guid.NewGuid()),
            Send(HttpMethod.Post, Root + "/sourcing/from-approved-sources", buyer, body, Guid.NewGuid()));
        Require(results.Count(x => x.StatusCode == HttpStatusCode.Created) == 1 && results.Count(x => x.StatusCode == HttpStatusCode.Conflict) == 1,
            "RFQ race did not produce exactly one reserve and one stale/exhausted loser.");
        await AssertRfqBalance(source, 1);
        var winner = await results.Single(x => x.StatusCode == HttpStatusCode.Created).Content.ReadFromJsonAsync<JsonElement>();
        await CancelRfq(winner.GetProperty("eventId").GetGuid()); await AssertRfqBalance(source, 0);
        // Refresh current CAS, then simultaneous exact original command must converge on one event/receipt.
        var fresh = await Get($"{Root}/requests/{source.GetProperty("id").GetGuid()}", requester);
        var same = RfqCreate("Same UUID race", (fresh, "1")); var key = Guid.NewGuid();
        var exact = await Task.WhenAll(Send(HttpMethod.Post, Root + "/sourcing/from-approved-sources", buyer, same, key),
            Send(HttpMethod.Post, Root + "/sourcing/from-approved-sources", buyer, same, key));
        foreach (var response in exact) await Check(response, HttpStatusCode.Created);
        Require(await exact[0].Content.ReadAsStringAsync() == await exact[1].Content.ReadAsStringAsync(), "Same RFQ UUID produced different responses.");
        using (var services = Scope(buyer)) Require(await services.ServiceProvider.GetRequiredService<ProcurementDbContext>().SourcingCommandReceipts
            .CountAsync(x => x.Operation == "Create" && x.CommandKey == key) == 1, "Same UUID produced duplicate RFQ receipts.");
        await AssertRfqBalance(source, 1);
    }

    private async Task VerifyRfqTinyValue(JsonElement source)
    {
        var response = await Send(HttpMethod.Post, Root + "/sourcing/from-approved-sources", buyer,
            RfqCreate("Exact small original", (source, "0.0001")), Guid.NewGuid()); await Check(response, HttpStatusCode.Created);
        var id = (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("eventId").GetGuid();
        var detail = await Get($"{Root}/sourcing/{id}/source-draft", buyer);
        Require(detail.GetProperty("lines")[0].GetProperty("estimatedValue").GetString() == "0.00000001", "RFQ hold silently rounded exact product.");
        await AssertRfqBalance(source, 0.0001m); await CancelRfq(id); await AssertRfqBalance(source, 0);
    }

    private async Task VerifyRfqReceiptRollback(JsonElement source)
    {
        var fresh = await Get($"{Root}/requests/{source.GetProperty("id").GetGuid()}", requester);
        var key = Guid.NewGuid(); var body = RfqCreate("Rollback after original/effect save", (fresh, "1"));
        await using var owner = new NpgsqlConnection(databaseConnection); await owner.OpenAsync();
        await using (var install = new NpgsqlCommand($"""
            CREATE FUNCTION procurement.probe_rfq_receipt_abort() RETURNS trigger LANGUAGE plpgsql AS $probe$
            BEGIN IF NEW.command_key='{key:D}'::uuid THEN RAISE EXCEPTION 'SyntheticRfqReceiptAbort'; END IF; RETURN NEW; END $probe$;
            CREATE TRIGGER probe_rfq_receipt_abort BEFORE INSERT ON procurement.sourcing_command_receipts
            FOR EACH ROW EXECUTE FUNCTION procurement.probe_rfq_receipt_abort();
            """, owner)) await install.ExecuteNonQueryAsync();
        var before = await RfqFootprint();
        try
        {
            await Expect(HttpStatusCode.ServiceUnavailable, HttpMethod.Post, Root + "/sourcing/from-approved-sources", buyer, body, key);
            Require(before == await RfqFootprint(), "RFQ failed final receipt left earlier source/evidence/audit/outbox writes committed.");
            await Expect(HttpStatusCode.NotFound, HttpMethod.Get, $"{Root}/sourcing/command-receipts/Create/{key}", buyer);
            Require(before == await RfqFootprint(), "Unknown failed-command recovery mutated evidence.");
        }
        finally
        {
            await using var remove = new NpgsqlCommand("DROP TRIGGER probe_rfq_receipt_abort ON procurement.sourcing_command_receipts; DROP FUNCTION procurement.probe_rfq_receipt_abort()", owner);
            await remove.ExecuteNonQueryAsync();
        }
        var retry = await Send(HttpMethod.Post, Root + "/sourcing/from-approved-sources", buyer, body, key);
        await Check(retry, HttpStatusCode.Created); var result = await retry.Content.ReadFromJsonAsync<JsonElement>();
        await CancelRfq(result.GetProperty("eventId").GetGuid()); await AssertRfqBalance(source, 0);
    }

    private async Task PublishRfqRequestPolicy(bool competitive)
    {
        var rules = competitive ? Rules(true).Replace("DIRECT", "COMPETITIVE", StringComparison.Ordinal) : Rules(true);
        Require(PolicyRuleValidator.Validate(rules).Count == 0, "Synthetic RFQ policy invalid before submission.");
        var starts = DateTimeOffset.UtcNow.AddSeconds(10);
        var path = Root + "/configuration/policies";
        var created = await Send(HttpMethod.Post, path, buyer, new { policyKey = "PURCHASE_REQUEST", effectiveFrom = starts,
            effectiveTo = (DateTimeOffset?)null, rulesJson = rules }); await Check(created, HttpStatusCode.Created);
        var policy = await created.Content.ReadFromJsonAsync<JsonElement>(); var id = policy.GetProperty("id").GetGuid();
        var submitted = await Send(HttpMethod.Post, $"{path}/{id}/submit-review", buyer,
            new { version = policy.GetProperty("version").GetInt64(), reason = "Synthetic configured route and explicit zero quote minimum" }); await Check(submitted, HttpStatusCode.OK);
        policy = await submitted.Content.ReadFromJsonAsync<JsonElement>();
        var decision = new { version = policy.GetProperty("version").GetInt64(), reason = "Independent review of named synthetic sourcing method and approval stages" };
        await Expect(HttpStatusCode.Forbidden, HttpMethod.Post, $"{path}/{id}/publish", buyer, decision);
        await Expect(HttpStatusCode.OK, HttpMethod.Post, $"{path}/{id}/publish", finance, decision);
        var delay = starts - DateTimeOffset.UtcNow; if (delay > TimeSpan.Zero) await Task.Delay(delay + TimeSpan.FromMilliseconds(50));
    }

    private async Task SetFiniteRfqActor()
    {
        using var services = Scope(buyer); var core = services.ServiceProvider.GetRequiredService<HrmDbContext>();
        if (!await core.TenantRoleAssignments.AnyAsync(x => x.RoleKey == "rfq_buyer"))
            core.TenantRoleAssignments.Add(new TenantRoleAssignment { RoleKey = "rfq_buyer", RoleName = "Synthetic finite RFQ buyer", Active = true,
                PermissionsCsv = "procurement-access,procurement-request-read,procurement-approval-open,procurement-sourcing-convert,procurement-sourcing-cancel,procurement-po-convert,procurement-po-read" });
        (await core.LocalUsers.SingleAsync(x => x.Id == buyer)).RolesCsv = "rfq_buyer"; await core.SaveChangesAsync();
    }
    private async Task RestoreRfqSetupActor()
    { using var services = Scope(buyer); var core = services.ServiceProvider.GetRequiredService<HrmDbContext>();
        (await core.LocalUsers.SingleAsync(x => x.Id == buyer)).RolesCsv = "erp_admin"; await core.SaveChangesAsync(); }

    private async Task VerifyRfqCurrentActionDenial(Guid id, Guid key)
    {
        using (var services = Scope(buyer))
        { var core = services.ServiceProvider.GetRequiredService<HrmDbContext>(); var role = await core.TenantRoleAssignments.SingleAsync(x => x.RoleKey == "rfq_buyer");
            role.PermissionsCsv = "procurement-access,procurement-request-read"; await core.SaveChangesAsync(); }
        try
        {
            var before = await RfqFootprint();
            await Expect(HttpStatusCode.Forbidden, HttpMethod.Get, $"{Root}/sourcing/{id}/source-draft", buyer);
            await Expect(HttpStatusCode.Forbidden, HttpMethod.Get, $"{Root}/sourcing/command-receipts/Create/{key}", buyer);
            Require(before == await RfqFootprint(), "Revoked RFQ action changed source/evidence.");
        }
        finally
        {
            using var services = Scope(buyer); var core = services.ServiceProvider.GetRequiredService<HrmDbContext>();
            var role = await core.TenantRoleAssignments.SingleAsync(x => x.RoleKey == "rfq_buyer");
            role.PermissionsCsv = "procurement-access,procurement-request-read,procurement-approval-open,procurement-sourcing-convert,procurement-sourcing-cancel,procurement-po-convert,procurement-po-read";
            await core.SaveChangesAsync();
        }
    }
    private async Task VerifyRfqBranchAndOwnerDenials(Guid id, Guid key)
    {
        var other = new WorkLocation { Code = "RFQ-OTHER", Name = "Synthetic other RFQ branch", LegalEntityId = company };
        using (var services = Scope(buyer))
        { var core = services.ServiceProvider.GetRequiredService<HrmDbContext>(); core.WorkLocations.Add(other);
            core.UserBranchAssignments.Add(new HrUserBranchAssignment { UserId = buyer, LocationId = branch }); await core.SaveChangesAsync(); }
        var before = await RfqFootprint();
        await Check(await Send(HttpMethod.Get, $"{Root}/sourcing/{id}/source-draft", buyer, selectedLocation: other.Id), HttpStatusCode.Forbidden);
        using (var services = Scope(buyer))
        { var core = services.ServiceProvider.GetRequiredService<HrmDbContext>();
            core.UserBranchAssignments.Add(new HrUserBranchAssignment { UserId = buyer, LocationId = other.Id }); await core.SaveChangesAsync(); }
        await Check(await Send(HttpMethod.Get, $"{Root}/sourcing/{id}/source-draft", buyer, selectedLocation: other.Id), HttpStatusCode.NotFound);
        await Check(await Send(HttpMethod.Get, $"{Root}/sourcing/command-receipts/Create/{key}", buyer, selectedLocation: other.Id), HttpStatusCode.NotFound);
        // The distinct current actor has the same finite actions; it must still not own this draft.
        string oldRole;
        using (var services = Scope(buyer))
        { var core = services.ServiceProvider.GetRequiredService<HrmDbContext>(); var account = await core.LocalUsers.SingleAsync(x => x.Id == manager2);
            oldRole = account.RolesCsv; account.RolesCsv = "rfq_buyer"; await core.SaveChangesAsync(); }
        try { await Expect(HttpStatusCode.NotFound, HttpMethod.Get, $"{Root}/sourcing/{id}/source-draft", manager2); }
        finally { using var services = Scope(buyer); var core = services.ServiceProvider.GetRequiredService<HrmDbContext>();
            (await core.LocalUsers.SingleAsync(x => x.Id == manager2)).RolesCsv = oldRole; await core.SaveChangesAsync(); }
        Require(before == await RfqFootprint(), "Branch/owner denial changed RFQ data.");
    }
    private async Task VerifyCompetitiveSourceCannotConvertToDirectOrder(JsonElement source)
    {
        Guid supplier;
        using (var services = Scope(buyer))
        { var db = services.ServiceProvider.GetRequiredService<ProcurementDbContext>(); var row = new Supplier { Number = "RFQ-DIRECT-NEGATIVE", LegalName = "Synthetic supplier", DisplayName = "Synthetic supplier", CountryCode = "ZM", Status = "Active", IsOrderable = true };
            db.AddScoped(row); await db.SaveChangesAsync(); supplier = row.Id; }
        var fresh = await Get($"{Root}/requests/{source.GetProperty("id").GetGuid()}", requester); var before = await RfqFootprint();
        await Expect(HttpStatusCode.Conflict, HttpMethod.Post, Root + "/orders/from-approved-sources", buyer, OrderConversion(fresh, supplier), Guid.NewGuid());
        Require(before == await RfqFootprint(), "Competitive source was silently converted to direct PO.");
    }
    private async Task<string> RfqFootprint()
    {
        await using var owner = new NpgsqlConnection(databaseConnection); await owner.OpenAsync();
        string[] tables = ["purchase_requests", "purchase_request_lines", "purchase_request_allocations", "purchase_orders", "purchase_order_lines", "sourcing_events", "sourcing_lines", "sourcing_draft_evidence", "sourcing_demand_bindings", "sourcing_command_receipts", "budget_check_snapshots", "purchase_request_approval_submissions", "purchase_request_approval_outcomes", "audit_events", "outbox_events"];
        var parts = tables.Select(t => $"'{t}',(SELECT coalesce(jsonb_agg(to_jsonb(r) ORDER BY r.id),'[]'::jsonb) FROM procurement.{t} r)");
        await using var query = new NpgsqlCommand("SELECT jsonb_build_object(" + string.Join(',', parts) + ")::text", owner);
        return (string)(await query.ExecuteScalarAsync())!;
    }
}
