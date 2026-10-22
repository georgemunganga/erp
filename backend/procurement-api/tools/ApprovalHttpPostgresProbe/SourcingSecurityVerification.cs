using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Mightyfin.Erp.Hrm.Api;
using Mightyfin.Erp.Hrm.Domain.Entities;
using Mightyfin.Erp.Hrm.Infrastructure.Data;
using Mightyfin.Erp.Procurement.Domain;
using Mightyfin.Erp.Procurement.Infrastructure.Data;
using Npgsql;

sealed partial class Probe
{
    private async Task VerifyRfqSecurityIdentitiesAndScope(Guid eventId, Guid key, JsonElement source)
    {
        var original = await Get($"{Root}/sourcing/{eventId}/source-draft", buyer);
        var originalTitle = original.GetProperty("title").GetString()!;
        var originalNumber = original.GetProperty("number").GetString()!;
        async Task Denied(HttpStatusCode status, Guid actor)
        {
            var before = await RfqFootprint();
            await Expect(status, HttpMethod.Get, $"{Root}/sourcing/{eventId}/source-draft", actor);
            await Expect(status, HttpMethod.Get, $"{Root}/sourcing/command-receipts/Create/{key}", actor);
            Require(before == await RfqFootprint(), "RFQ identity denial changed retained source or evidence.");
        }
        Guid originalWorker; Guid alias; Guid replacementWorker;
        using (var fixture = Scope(buyer))
        {
            var core = fixture.ServiceProvider.GetRequiredService<HrmDbContext>();
            originalWorker = (await core.LocalUsers.SingleAsync(x => x.Id == buyer)).WorkerId!.Value;
            var other = new Worker { EmployeeNo = "RFQ-REBOUND", FirstName = "Rebound", LastName = "Fixture", Status = "active", LocationId = branch };
            core.Workers.Add(other); replacementWorker = other.Id;
            var account = new LocalUser { Email = "rfq-alias@example.test", NormalizedEmail = "RFQ-ALIAS@EXAMPLE.TEST",
                DisplayName = "Synthetic same-worker alias", PasswordHash = "synthetic-no-login-secret", RolesCsv = "rfq_buyer", WorkerId = originalWorker };
            core.LocalUsers.Add(account); alias = account.Id; await core.SaveChangesAsync();
        }
        try { await Denied(HttpStatusCode.Forbidden, buyer); await Denied(HttpStatusCode.Forbidden, alias); }
        finally { using var fixture = Scope(buyer); var core = fixture.ServiceProvider.GetRequiredService<HrmDbContext>();
            (await core.LocalUsers.SingleAsync(x => x.Id == alias)).IsActive = false; await core.SaveChangesAsync(); }
        using (var fixture = Scope(buyer))
        { var core = fixture.ServiceProvider.GetRequiredService<HrmDbContext>(); (await core.LocalUsers.SingleAsync(x => x.Id == buyer)).WorkerId = replacementWorker; await core.SaveChangesAsync(); }
        try { await Denied(HttpStatusCode.NotFound, buyer); }
        finally { using var fixture = Scope(buyer); var core = fixture.ServiceProvider.GetRequiredService<HrmDbContext>();
            (await core.LocalUsers.SingleAsync(x => x.Id == buyer)).WorkerId = originalWorker; await core.SaveChangesAsync(); }
        using (var fixture = Scope(buyer))
        { var core = fixture.ServiceProvider.GetRequiredService<HrmDbContext>(); (await core.LocalUsers.SingleAsync(x => x.Id == buyer)).IsActive = false; await core.SaveChangesAsync(); }
        try { await Denied(HttpStatusCode.Unauthorized, buyer); }
        finally { using var fixture = Scope(buyer); var core = fixture.ServiceProvider.GetRequiredService<HrmDbContext>();
            (await core.LocalUsers.SingleAsync(x => x.Id == buyer)).IsActive = true; await core.SaveChangesAsync(); }
        Require((await Get($"{Root}/sourcing/command-receipts/Create/{key}", buyer)).GetProperty("recordedHistoricalResult").GetBoolean(),
            "Restored original canonical person could not recover its retained history.");

        var foreignCompany = new LegalEntity { Code = "RFQ-FOREIGN", RegisteredName = "Other synthetic company", Currency = "ZMW" };
        var foreignSite = new WorkLocation { Code = "RFQ-FOREIGN-SITE", Name = "Other company site", LegalEntityId = foreignCompany.Id };
        var foreignWorker = new Worker { EmployeeNo = "RFQ-FOREIGN", FirstName = "Foreign", LastName = "Company", Status = "active", LocationId = foreignSite.Id };
        var foreignPerson = new LocalUser { Email = "rfq-company@example.test", NormalizedEmail = "RFQ-COMPANY@EXAMPLE.TEST", DisplayName = "Other company buyer",
            PasswordHash = "synthetic-no-login-secret", RolesCsv = "rfq_buyer", WorkerId = foreignWorker.Id };
        var tenantCompany = new LegalEntity { TenantId = "rfq-other-tenant", Code = "RFQ-TENANT", RegisteredName = "Other synthetic tenant", Currency = "ZMW" };
        var tenantSite = new WorkLocation { TenantId = tenantCompany.TenantId, Code = "RFQ-TENANT-SITE", Name = "Other tenant site", LegalEntityId = tenantCompany.Id };
        var tenantWorker = new Worker { TenantId = tenantCompany.TenantId, EmployeeNo = "RFQ-TENANT", FirstName = "Foreign", LastName = "Tenant", Status = "active", LocationId = tenantSite.Id };
        var tenantPerson = new LocalUser { TenantId = tenantCompany.TenantId, Email = "rfq-tenant@example.test", NormalizedEmail = "RFQ-TENANT@EXAMPLE.TEST",
            DisplayName = "Other tenant person", PasswordHash = "synthetic-no-login-secret", RolesCsv = "rfq_buyer", WorkerId = tenantWorker.Id };
        using (var fixture = Scope(buyer))
        {
            var core = fixture.ServiceProvider.GetRequiredService<HrmDbContext>();
            core.LegalEntities.Add(foreignCompany); core.WorkLocations.Add(foreignSite);
            core.Workers.Add(foreignWorker); core.LocalUsers.Add(foreignPerson); await core.SaveChangesAsync();
        }
        // The existing HrmDbContext correctly stamps its own tenant on every new row.
        // Seed declared other-tenant identities through a distinct owner context, never
        // bypass that invariant or rewrite a stored tenant after insertion.
        await using (var tenantOwner = new HrmDbContext(new DbContextOptionsBuilder<HrmDbContext>()
            .UseNpgsql(databaseConnection).Options, new Tenant("rfq-other-tenant")))
        {
            tenantOwner.LegalEntities.Add(tenantCompany); tenantOwner.WorkLocations.Add(tenantSite);
            tenantOwner.Workers.Add(tenantWorker); tenantOwner.LocalUsers.Add(tenantPerson); await tenantOwner.SaveChangesAsync();
            Require(await tenantOwner.LocalUsers.AnyAsync(x => x.Id == tenantPerson.Id && x.TenantId == "rfq-other-tenant"), "Foreign tenant fixture not retained as explicit separate tenant.");
        }
        var footprint = await RfqFootprint();
        async Task<HttpResponseMessage> Foreign(string path, HttpMethod? method = null, object? body = null)
        {
            var request = new HttpRequestMessage(method ?? HttpMethod.Get, path);
            request.Headers.Add("X-Probe-User", foreignPerson.Id.ToString("D")); request.Headers.Add("X-Shell-Entity", foreignCompany.Id.ToString("D"));
            request.Headers.Add("X-Shell-Location", foreignSite.Id.ToString("D"));
            if (body is not null) { request.Content = JsonContent.Create(body, options: Json); request.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString("D")); }
            return await client.SendAsync(request);
        }
        foreach (var path in new[] { "/sourcing/approved-sources?page=1&pageSize=25", "/sourcing/source-drafts?page=1&pageSize=25" })
        {
            using var response = await Foreign(Root + path); await Check(response, HttpStatusCode.OK);
            var body = await response.Content.ReadFromJsonAsync<JsonElement>();
            Require(body.GetProperty("items").GetArrayLength() == 0 && (body.TryGetProperty("candidateTotal", out var count) ? count.GetInt32() : body.GetProperty("total").GetInt32()) == 0,
                "Other actual company person received original company rows/counts.");
        }
        await Check(await Foreign($"{Root}/sourcing/{eventId}/source-draft"), HttpStatusCode.NotFound);
        await Check(await Foreign($"{Root}/sourcing/command-receipts/Create/{key}"), HttpStatusCode.NotFound);
        await Check(await Foreign(Root + "/sourcing/from-approved-sources", HttpMethod.Post, RfqCreate("Cross-company IDs denied", (source, "1"))), HttpStatusCode.NotFound);
        // Tenant is server-owned/fixed 'probe': a genuine other-tenant account is NOT authenticated here.
        // This is rejection at the tenant-A identity boundary, not a claimed tenant-B logged-in session.
        await Denied(HttpStatusCode.Unauthorized, tenantPerson.Id);
        Require(footprint == await RfqFootprint(), "Foreign company/tenant attempts persisted effects.");

        Guid otherBranch;
        using (var fixture = Scope(buyer)) otherBranch = await fixture.ServiceProvider.GetRequiredService<HrmDbContext>().WorkLocations
            .Where(x => x.Code == "RFQ-OTHER").Select(x => x.Id).SingleAsync();
        async Task Header(string table, Guid id, Guid value)
        {
            await using var owner = new NpgsqlConnection(databaseConnection); await owner.OpenAsync();
            await using var update = new NpgsqlCommand($"UPDATE procurement.{table} SET branch_ref=@branch WHERE id=@id", owner);
            update.Parameters.AddWithValue("branch", value); update.Parameters.AddWithValue("id", id);
            Require(await update.ExecuteNonQueryAsync() == 1, "Synthetic retained-header tamper target missing.");
        }
        async Task NoPrivateBody(HttpResponseMessage response, HttpStatusCode status)
        {
            await Check(response, status); var body = await response.Content.ReadAsStringAsync();
            Require(!body.Contains(originalTitle, StringComparison.Ordinal) && !body.Contains(originalNumber, StringComparison.Ordinal), "Denied tampered header disclosed original RFQ text.");
        }
        footprint = await RfqFootprint();
        await Header("sourcing_events", eventId, otherBranch);
        try
        {
            var tampered = await RfqFootprint();
            using var list = await Send(HttpMethod.Get, Root + "/sourcing/source-drafts?page=1&pageSize=25", buyer, selectedLocation: otherBranch);
            await Check(list, HttpStatusCode.OK); var page = await list.Content.ReadFromJsonAsync<JsonElement>();
            Require(page.GetProperty("total").GetInt32() == 0 && page.GetProperty("items").GetArrayLength() == 0, "Tampered current RFQ branch disclosed retained branch-A rows/count.");
            await NoPrivateBody(await Send(HttpMethod.Get, $"{Root}/sourcing/{eventId}/source-draft", buyer, selectedLocation: otherBranch), HttpStatusCode.NotFound);
            await NoPrivateBody(await Send(HttpMethod.Get, $"{Root}/sourcing/command-receipts/Create/{key}", buyer, selectedLocation: otherBranch), HttpStatusCode.NotFound);
            Require(tampered == await RfqFootprint(), "Tampered-header reads repaired or mutated owner history.");
        }
        finally { await Header("sourcing_events", eventId, branch); }
        Require(footprint == await RfqFootprint(), "Original RFQ header restoration changed evidence bytes.");
        var requestId = source.GetProperty("id").GetGuid();
        await Header("purchase_requests", requestId, otherBranch);
        try
        {
            var tampered = await RfqFootprint();
            using var list = await Send(HttpMethod.Get, Root + "/sourcing/approved-sources?page=1&pageSize=25", buyer, selectedLocation: otherBranch);
            await Check(list, HttpStatusCode.OK); var page = await list.Content.ReadFromJsonAsync<JsonElement>();
            Require(page.GetProperty("candidateTotal").GetInt32() == 0 && page.GetProperty("items").GetArrayLength() == 0,
                "Retained PR branch mismatch entered candidate counts or rows before paging.");
            Require(tampered == await RfqFootprint(), "Tampered source picker changed evidence.");
        }
        finally { await Header("purchase_requests", requestId, branch); }
        Require(footprint == await RfqFootprint(), "Original source branch restoration changed immutable evidence.");
        Console.WriteLine("PASS RFQ canonical alias/rebind/offboarding and foreign-company scope; other-tenant account rejected by fixed server tenant; retained header tamper hidden before counts. No identity/evidence hashes rewritten.");
    }

    private sealed record RfqCatalogFixture(Guid ItemId, Guid EntryId, long EntryVersion, JsonElement Source);
    private async Task<RfqCatalogFixture> PrepareRfqCatalogSecuritySource()
    {
        Guid itemId, entryId; long version;
        using (var fixture = Scope(buyer))
        {
            var db = fixture.ServiceProvider.GetRequiredService<ProcurementDbContext>();
            var supplier = new Supplier { Number = "RFQ-CATALOG", LegalName = "Synthetic RFQ catalog supplier", DisplayName = "Synthetic RFQ catalog supplier", CountryCode = "ZM", Status = "Active", IsOrderable = true };
            var item = new CatalogItem { Code = "RFQ-CATALOG", Name = "Synthetic RFQ catalog item", Kind = "Goods", CategoryCode = "IT", UnitCode = "each", Status = "Active", IsPurchasable = true };
            db.AddScoped(supplier); db.AddScoped(item);
            var entry = new CatalogEntry { CatalogItemId = item.Id, SupplierId = supplier.Id, CurrencyCode = "ZMW", UnitCode = "each", UnitPrice = 100,
                MinimumQuantity = 1, MaximumQuantity = 2, Status = "Published", PublishedVersion = 1,
                EffectiveFrom = DateTimeOffset.UtcNow.AddDays(-1), EffectiveTo = DateTimeOffset.UtcNow.AddDays(1) };
            db.AddScoped(entry); await db.SaveChangesAsync(); itemId = item.Id; entryId = entry.Id; version = entry.Version;
            // Synthetic declared catalog configuration only, not invented PR/Core approval.
            var source = await NewApprovedOrderSource("RFQ catalog withdrawal security", 2, 100, 200, "Goods", itemId, entryId, supplier.Id);
            return new(itemId, entryId, version, source);
        }
    }
    private async Task VerifyRfqCatalogWithdrawal(RfqCatalogFixture fixture)
    {
        var key = Guid.NewGuid(); var body = RfqCreate("Catalog original hold", (fixture.Source, "1"));
        var created = await Send(HttpMethod.Post, Root + "/sourcing/from-approved-sources", buyer, body, key); await Check(created, HttpStatusCode.Created);
        var original = await created.Content.ReadAsStringAsync(); var id = JsonSerializer.Deserialize<JsonElement>(original).GetProperty("eventId").GetGuid();
        // Existing pattern: owner-only catalog writer host; runtime retains SELECT-only catalog grants.
        var builder = WebApplication.CreateBuilder(); builder.WebHost.UseTestServer(); builder.Logging.ClearProviders();
        builder.Services.AddScoped<ITenantAccessor>(_ => new Tenant()); builder.Services.AddDbContext<HrmDbContext>(o => o.UseNpgsql(databaseConnection));
        ProcurementHostIntegration.AddServices(builder.Services, databaseConnection, builder.Configuration);
        builder.Services.AddAuthentication("rfq-catalog").AddScheme<AuthenticationSchemeOptions, ProbeAuthentication>("rfq-catalog", _ => { });
        builder.Services.AddAuthorization(ProcurementPermissions.AddPolicies);
        await using var app = builder.Build(); app.UseRouting(); app.UseAuthentication(); app.UseAuthorization(); app.UseMiddleware<ProcurementScopeMiddleware>();
        ProcurementItemEndpoints.MapRoutes(app); await app.StartAsync(); using var writer = app.GetTestClient();
        await RestoreRfqSetupActor();
        try
        {
            var request = new HttpRequestMessage(HttpMethod.Post, $"{Root}/items/{fixture.ItemId}/catalog-entries/{fixture.EntryId}/withdraw")
                { Content = JsonContent.Create(new { version = fixture.EntryVersion, reason = "Synthetic withdrawal prevents new RFQ reservation only" }, options: Json) };
            request.Headers.Add("X-Probe-User", buyer.ToString("D")); request.Headers.Add("X-Shell-Entity", company.ToString("D")); request.Headers.Add("X-Shell-Location", branch.ToString("D"));
            await Check(await writer.SendAsync(request), HttpStatusCode.OK);
        }
        finally { await SetFiniteRfqActor(); }
        var fresh = await Get($"{Root}/requests/{fixture.Source.GetProperty("id").GetGuid()}", requester); var before = await RfqFootprint();
        using var blocked = await Send(HttpMethod.Post, Root + "/sourcing/from-approved-sources", buyer, RfqCreate("Withdrawn offer denied", (fresh, "1")), Guid.NewGuid());
        await Check(blocked, HttpStatusCode.Conflict);
        Require((await blocked.Content.ReadAsStringAsync()).Contains("SourcingCatalogSourceUnavailable", StringComparison.Ordinal), "Withdrawn offer failed for an unrelated RFQ prerequisite.");
        var replay = await Send(HttpMethod.Post, Root + "/sourcing/from-approved-sources", buyer, body, key); await Check(replay, HttpStatusCode.Created);
        Require(original == await replay.Content.ReadAsStringAsync(), "Catalog withdrawal erased or reinterpreted original owned RFQ receipt.");
        Require(before == await RfqFootprint(), "Withdrawn offer denial/replay persisted operational or financial effects.");
        await CancelRfq(id); await AssertRfqBalance(fixture.Source, 0);
        Console.WriteLine("PASS actual catalog withdrawal blocks new RFQ holds but preserves authorized original-key history/cancellation; runtime catalog grants unchanged.");
    }
}
