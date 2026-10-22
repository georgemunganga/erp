using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Mightyfin.Erp.Hrm.Tests;

public sealed partial class ProcurementRequestHttpTests
{
    private const string ReceivingLookupPath = "/api/procurement/v1/configuration/policies/lookups/receiving/";

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
