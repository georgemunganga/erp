using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Mightyfin.Erp.Procurement.Application;
using Microsoft.EntityFrameworkCore;
using Mightyfin.Erp.Hrm.Api;
using Mightyfin.Erp.Hrm.Infrastructure.Data;
using Mightyfin.Erp.Procurement.Infrastructure.Data;

namespace Mightyfin.Erp.Hrm.Tests;

public sealed partial class ProcurementRequestHttpTests
{
    private const string ReceivingLookupPath = "/api/procurement/v1/configuration/policies/lookups/receiving/";

    [Fact]
    public async Task Receiving_schema_two_validation_accepts_named_Core_accounts_without_worker_fields_and_preserves_schema_one()
    {
        await using var fixture = await Fixture.Start(); await fixture.ConfigureSourcingPolicyActors();
        var principal = Guid.NewGuid().ToString("D"); var site = Guid.NewGuid();
        var schemaTwo = JsonSerializer.Serialize(new { schemaVersion = 2, transactionType = "Receiving", currencyCode = "ZMW",
            rules = new[] { new { code = "NAMED", categoryCode = "IT", purchaseType = "Goods", kind = "Goods",
                siteIds = new[] { site }, inspectionRequired = false, checklistReference = (string?)null,
                inspectorPrincipalIds = Array.Empty<string>(), acceptorPrincipalIds = new[] { principal },
                serviceOwnerPrincipalIds = Array.Empty<string>(), allowRequesterCertification = false,
                allowBuyerCertification = false, allowReceiverCertification = false, requireIndependentInspector = true,
                evidenceMode = "ControlledManualReference", evidenceReferencePrefix = "REC:" } } });
        Assert.True(PrincipalReceivingPolicyRules.IsDocument(schemaTwo));
        var response = await SendJson(fixture.Client, HttpMethod.Post,
            "/api/procurement/v1/configuration/policies/validate", fixture.Other,
            new { rulesJson = schemaTwo }, role: "procurement_manager");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True((await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("isValid").GetBoolean());
        var schemaOne = JsonSerializer.Serialize(new { schemaVersion = 1, transactionType = "Receiving", currencyCode = "ZMW",
            rules = new[] { new { code = "LEGACY", categoryCode = "IT", purchaseType = "Goods", kind = "Goods",
                siteIds = new[] { site }, inspectionRequired = false, checklistReference = (string?)null,
                inspectorWorkerIds = Array.Empty<Guid>(), serviceOwnerWorkerIds = Array.Empty<Guid>(),
                allowRequesterCertification = false, allowBuyerCertification = false, allowReceiverCertification = false,
                requireIndependentInspector = true, evidenceMode = "ControlledManualReference", evidenceReferencePrefix = "REC:" } } });
        response = await SendJson(fixture.Client, HttpMethod.Post,
            "/api/procurement/v1/configuration/policies/validate", fixture.Other,
            new { rulesJson = schemaOne }, role: "procurement_manager");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True((await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("isValid").GetBoolean());
        var wrong = schemaTwo.Replace("\"acceptorPrincipalIds\"", "\"acceptorWorkerIds\"", StringComparison.Ordinal);
        response = await SendJson(fixture.Client, HttpMethod.Post,
            "/api/procurement/v1/configuration/policies/validate", fixture.Other,
            new { rulesJson = wrong }, role: "procurement_manager");
        Assert.False((await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("isValid").GetBoolean());
    }

    [Fact]
    public async Task Receiving_principal_lookup_selects_current_workerless_Core_account_without_changing_worker_lookup()
    {
        await using var fixture = await Fixture.Start(); await fixture.ConfigureSourcingPolicyActors();
        var workerless = await fixture.SeedSourcingLookupPerson("Receiving named Core account", "no-worker");
        await fixture.ConfigureCoreActions(workerless, "procurement-access");
        var selection = "?ids=" + workerless.ToString("D");
        var before = await fixture.SourcingConfigurationFootprint();
        var principalResponse = await Send(fixture.Client, HttpMethod.Get,
            ReceivingLookupPath + "principals" + selection, fixture.Other, role: "procurement_manager");
        Assert.Equal(HttpStatusCode.OK, principalResponse.StatusCode);
        var principalPage = await principalResponse.Content.ReadFromJsonAsync<JsonElement>();
        var row = Assert.Single(principalPage.GetProperty("items").EnumerateArray());
        Assert.Equal(workerless, row.GetProperty("id").GetGuid());
        Assert.Equal(workerless, row.GetProperty("principalId").GetGuid());
        Assert.False(principalPage.GetProperty("isAuthority").GetBoolean());
        var legacyResponse = await Send(fixture.Client, HttpMethod.Get,
            ReceivingLookupPath + "people" + selection, fixture.Other, role: "procurement_manager");
        Assert.Equal(HttpStatusCode.OK, legacyResponse.StatusCode);
        Assert.Equal(0, (await legacyResponse.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("total").GetInt32());
        await fixture.ConfigureCoreActions(workerless, "");
        var revoked = await Send(fixture.Client, HttpMethod.Get,
            ReceivingLookupPath + "principals" + selection, fixture.Other, role: "procurement_manager");
        Assert.Equal(0, (await revoked.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("total").GetInt32());
        Assert.Equal(before, await fixture.SourcingConfigurationFootprint());
    }

    [Theory]
    [InlineData("people")]
    [InlineData("sites")]
    [InlineData("categories")]
    public void Receiving_lookup_search_and_retained_selection_translate_with_the_actual_Npgsql_provider(string kind)
    {
        // Translation only: no connection is opened and no table/schema is created.
        const string connection = "Host=127.0.0.1;Database=receiving_translation_only;Username=unused";
        var scope = new ProcurementRequestScope();
        scope.Set("translation-tenant", Guid.NewGuid(), Guid.NewGuid().ToString("D"), Guid.NewGuid(), null, false, "translation-only");
        using var core = new HrmDbContext(new DbContextOptionsBuilder<HrmDbContext>().UseNpgsql(connection).Options,
            new FixedTenantAccessor(scope.TenantId));
        using var db = new ProcurementDbContext(new DbContextOptionsBuilder<ProcurementDbContext>().UseNpgsql(connection).Options, scope);
        var selected = new[] { Guid.NewGuid() }; var search = "named selection";
        var query = ProcurementPolicyDraftRoutes.ReceivingPolicyLookupQuery(kind, core, db, scope, DateTimeOffset.UtcNow)
            .Where(x => selected.Contains(x.Id) && (x.DisplayName.ToLower().Contains(search)
                || x.Code != null && x.Code.ToLower().Contains(search)))
            .OrderBy(x => x.DisplayName).ThenBy(x => x.Id).Skip(1).Take(25);
        var sql = query.ToQueryString();
        Assert.Contains("LIMIT", sql, StringComparison.Ordinal);
        Assert.Contains("OFFSET", sql, StringComparison.Ordinal);
        Assert.Contains("tenant_id", sql, StringComparison.Ordinal);
        Assert.Contains("legal_entity_id", sql, StringComparison.Ordinal);
        Assert.DoesNotContain("\"email\"", sql, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Receiving_people_lookup_filters_before_count_and_resolves_only_exact_saved_worker_ids()
    {
        await using var fixture = await Fixture.Start(); await fixture.ConfigureSourcingPolicyActors();
        var selected = await fixture.SeedSourcingLookupPerson("Receiving lookup selected", "visible");
        foreach (var kind in new[] { "foreign-company", "foreign-tenant", "inactive", "inactive-worker", "archived", "locked", "no-worker", "alias" })
            await fixture.SeedSourcingLookupPerson("Receiving lookup private " + kind, kind);
        var before = await fixture.SourcingConfigurationFootprint();
        var response = await Send(fixture.Client, HttpMethod.Get, ReceivingLookupPath + "people?search=Receiving%20lookup&limit=1",
            fixture.Other, role: "procurement_manager");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("no-store", response.Headers.CacheControl?.ToString());
        var body = await response.Content.ReadAsStringAsync();
        var page = JsonDocument.Parse(body).RootElement;
        Assert.Equal(1, page.GetProperty("total").GetInt32());
        var row = Assert.Single(page.GetProperty("items").EnumerateArray());
        Assert.Equal(selected, row.GetProperty("principalId").GetGuid());
        Assert.DoesNotContain("private", body, StringComparison.OrdinalIgnoreCase);
        Assert.False(page.GetProperty("isAuthority").GetBoolean());
        Assert.False(page.GetProperty("canAcceptDelivery").GetBoolean());
        var worker = row.GetProperty("id").GetGuid();
        response = await Send(fixture.Client, HttpMethod.Get, ReceivingLookupPath + "people?ids=" + worker.ToString("D") + "," + Guid.NewGuid().ToString("D"),
            fixture.Other, role: "procurement_manager");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        page = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(worker, Assert.Single(page.GetProperty("items").EnumerateArray()).GetProperty("id").GetGuid());
        Assert.Equal(1, page.GetProperty("total").GetInt32());
        Assert.Equal(before, await fixture.SourcingConfigurationFootprint());
    }

    [Fact]
    public async Task Receiving_locations_and_people_respect_current_assigned_branch_and_current_configuration_access()
    {
        await using var fixture = await Fixture.Start(); await fixture.ConfigureSourcingPolicyActors();
        var branch = await fixture.SourcingBranch();
        var local = await fixture.SeedSourcingLookupPerson("Receiving branch local", "visible");
        await fixture.SeedSourcingLookupPerson("Receiving branch hidden", "remote-branch");
        await fixture.SeedSourcingLookupPerson("Receiving foreign hidden", "foreign-company");
        await fixture.SourcingIdentity("confined-reviewer", fixture.Other);
        var before = await fixture.SourcingConfigurationFootprint();
        var response = await Send(fixture.Client, HttpMethod.Get, ReceivingLookupPath + "sites", fixture.Other, role: "procurement_manager");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var sites = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(1, sites.GetProperty("total").GetInt32());
        Assert.Equal(branch, Assert.Single(sites.GetProperty("items").EnumerateArray()).GetProperty("id").GetGuid());
        response = await Send(fixture.Client, HttpMethod.Get, ReceivingLookupPath + "people?search=Receiving%20branch", fixture.Other, role: "procurement_manager");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var people = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(local, Assert.Single(people.GetProperty("items").EnumerateArray()).GetProperty("principalId").GetGuid());
        Assert.Equal(HttpStatusCode.Forbidden, (await Send(fixture.Client, HttpMethod.Get,
            ReceivingLookupPath + "people", fixture.Owner, role: "procurement_manager")).StatusCode);
        Assert.Equal(before, await fixture.SourcingConfigurationFootprint());
    }

    [Theory]
    [InlineData("people")]
    [InlineData("sites")]
    [InlineData("categories")]
    public async Task Receiving_saved_selection_errors_do_not_fall_back_to_directory_reads(string kind)
    {
        await using var fixture = await Fixture.Start(); await fixture.ConfigureSourcingPolicyActors();
        var id = Guid.NewGuid().ToString("D");
        var bad = new[] { "", "not-an-id", Guid.Empty.ToString("D"), id + "," + id, id + ",",
            "AAAAAAAA-AAAA-4AAA-8AAA-AAAAAAAAAAAA",
            string.Join(',', Enumerable.Range(0, 26).Select(_ => Guid.NewGuid().ToString("D"))) };
        var before = await fixture.SourcingConfigurationFootprint();
        foreach (var value in bad)
            Assert.Equal(HttpStatusCode.UnprocessableEntity, (await Send(fixture.Client, HttpMethod.Get,
                ReceivingLookupPath + kind + "?ids=" + Uri.EscapeDataString(value), fixture.Other, role: "procurement_manager")).StatusCode);
        foreach (var suffix in new[] { "&search=someone", "&offset=1", "&limit=1", "&offset=-1", "&limit=1000" })
            Assert.Equal(HttpStatusCode.UnprocessableEntity, (await Send(fixture.Client, HttpMethod.Get,
                ReceivingLookupPath + kind + "?ids=" + id + suffix, fixture.Other, role: "procurement_manager")).StatusCode);
        Assert.Equal(before, await fixture.SourcingConfigurationFootprint());
    }

    [Fact]
    public async Task Receiving_category_lookup_returns_only_active_categories_from_the_selected_company()
    {
        await using var fixture = await Fixture.Start(); await fixture.ConfigureSourcingPolicyActors();
        var active = await fixture.SeedReceivingLookupCategory("LOOKUP-ACTIVE", "Active");
        var draft = await fixture.SeedReceivingLookupCategory("LOOKUP-DRAFT", "Draft");
        var retired = await fixture.SeedReceivingLookupCategory("LOOKUP-RETIRED", "Retired");
        var foreign = await fixture.SeedReceivingLookupCategory("LOOKUP-FOREIGN", "Active", foreignCompany: true);
        var before = await fixture.SourcingConfigurationFootprint();
        var targets = string.Join(',', new[] { active, draft, retired, foreign });
        var response = await Send(fixture.Client, HttpMethod.Get, ReceivingLookupPath + "categories?ids=" + targets,
            fixture.Other, role: "procurement_manager");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var page = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(1, page.GetProperty("total").GetInt32());
        var row = Assert.Single(page.GetProperty("items").EnumerateArray());
        Assert.Equal(active, row.GetProperty("id").GetGuid());
        Assert.Equal("LOOKUP-ACTIVE", row.GetProperty("code").GetString());
        Assert.Equal(before, await fixture.SourcingConfigurationFootprint());
    }
}
