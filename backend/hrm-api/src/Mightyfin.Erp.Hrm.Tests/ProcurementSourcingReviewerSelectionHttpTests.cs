using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Mightyfin.Erp.Hrm.Tests;

public sealed partial class ProcurementRequestHttpTests
{
    private const string SourcingReviewerLookupPath = "/api/procurement/v1/configuration/policies/lookups/sourcing-reviewers";

    [Fact]
    public async Task Schema_two_reviewer_lookup_includes_only_current_granted_core_principals_without_requiring_a_worker()
    {
        await using var fixture = await Fixture.Start();
        await fixture.ConfigureSourcingPolicyActors();
        var workerless = await fixture.SeedSourcingLookupPerson("Core reviewer without employee", "no-worker");
        await fixture.ConfigureCoreActions(workerless, "procurement-access,procurement-sourcing-publish");
        var path = SourcingReviewerLookupPath + "?principalIds=" + workerless.ToString("D");
        var before = await fixture.SourcingConfigurationFootprint();
        using (var schemaOne = await Send(fixture.Client, HttpMethod.Get, path,
            fixture.Other, role: "procurement_manager"))
        {
            Assert.Equal(HttpStatusCode.OK, schemaOne.StatusCode);
            Assert.Equal(0, (await schemaOne.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("total").GetInt32());
        }
        using (var schemaTwo = await Send(fixture.Client, HttpMethod.Get, path + "&schemaVersion=2",
            fixture.Other, role: "procurement_manager"))
        {
            Assert.Equal(HttpStatusCode.OK, schemaTwo.StatusCode);
            var body = await schemaTwo.Content.ReadFromJsonAsync<JsonElement>();
            Assert.Equal(1, body.GetProperty("total").GetInt32());
            var item = Assert.Single(body.GetProperty("items").EnumerateArray());
            Assert.Equal(workerless, item.GetProperty("principalId").GetGuid());
            Assert.Equal(JsonValueKind.Null, item.GetProperty("workerId").ValueKind);
        }
        await fixture.ConfigureCoreActions(workerless, "procurement-access");
        using (var revoked = await Send(fixture.Client, HttpMethod.Get, path + "&schemaVersion=2",
            fixture.Other, role: "procurement_manager"))
        {
            Assert.Equal(HttpStatusCode.OK, revoked.StatusCode);
            Assert.Equal(0, (await revoked.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("total").GetInt32());
        }
        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await Send(fixture.Client, HttpMethod.Get,
            path + "&schemaVersion=3", fixture.Other, role: "procurement_manager")).StatusCode);
        Assert.Equal(before, await fixture.SourcingConfigurationFootprint());
    }

    [Fact]
    public async Task Sourcing_saved_reviewer_names_resolve_exact_targets_without_exposing_unavailable_people()
    {
        await using var fixture = await Fixture.Start();
        await fixture.ConfigureSourcingPolicyActors();
        await fixture.SeedSourcingLookupPerson("Earlier unselected name", "visible");
        var selected = await fixture.SeedSourcingLookupPerson("Saved reviewer off the search page", "visible");
        var hidden = new[] {
            await fixture.SeedSourcingLookupPerson("Private foreign name", "foreign-company"),
            await fixture.SeedSourcingLookupPerson("Private inactive name", "inactive"),
            await fixture.SeedSourcingLookupPerson("Private alias name", "alias"), Guid.NewGuid() };
        var before = await fixture.SourcingConfigurationFootprint();
        var ids = string.Join(',', hidden.Append(selected).Select(x => x.ToString("D")));
        var response = await Send(fixture.Client, HttpMethod.Get,
            SourcingReviewerLookupPath + "?principalIds=" + Uri.EscapeDataString(ids), fixture.Other, role: "procurement_manager");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("no-store", response.Headers.CacheControl?.ToString());
        var body = await response.Content.ReadAsStringAsync();
        using var document = JsonDocument.Parse(body);
        var page = document.RootElement;
        Assert.Equal(1, page.GetProperty("total").GetInt32());
        Assert.Equal(selected, Assert.Single(page.GetProperty("items").EnumerateArray()).GetProperty("principalId").GetGuid());
        Assert.Equal(JsonValueKind.Null, page.GetProperty("nextOffset").ValueKind);
        Assert.False(page.GetProperty("isAuthority").GetBoolean());
        Assert.DoesNotContain("Private", body, StringComparison.Ordinal);
        foreach (var id in hidden) Assert.DoesNotContain(id.ToString("D"), body, StringComparison.Ordinal);
        Assert.Equal(before, await fixture.SourcingConfigurationFootprint());
    }

    [Fact]
    public async Task Sourcing_saved_reviewer_targeting_cannot_bypass_current_branch_scope_or_configuration_access()
    {
        await using var fixture = await Fixture.Start();
        await fixture.ConfigureSourcingPolicyActors();
        var home = await fixture.SeedSourcingLookupPerson("Visible selected person", "visible");
        var remote = await fixture.SeedSourcingLookupPerson("Other branch selected person", "remote-branch");
        await fixture.SourcingIdentity("confined-reviewer", fixture.Other);
        var before = await fixture.SourcingConfigurationFootprint();
        var path = SourcingReviewerLookupPath + "?principalIds=" + home.ToString("D") + "," + remote.ToString("D");
        var response = await Send(fixture.Client, HttpMethod.Get, path, fixture.Other, role: "procurement_manager");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var page = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(1, page.GetProperty("total").GetInt32());
        Assert.Equal(home, Assert.Single(page.GetProperty("items").EnumerateArray()).GetProperty("principalId").GetGuid());
        Assert.Equal(HttpStatusCode.Forbidden, (await Send(fixture.Client, HttpMethod.Get, path,
            fixture.Owner, role: "procurement_manager")).StatusCode);
        Assert.Equal(before, await fixture.SourcingConfigurationFootprint());
    }

    [Fact]
    public async Task Sourcing_saved_reviewer_targeting_rejects_invalid_sets_without_falling_back_to_all_people()
    {
        await using var fixture = await Fixture.Start();
        await fixture.ConfigureSourcingPolicyActors();
        var id = Guid.NewGuid().ToString("D");
        var invalid = new[] { "", "not-a-person", Guid.Empty.ToString("D"), id + "," + id, id + ",",
            "AAAAAAAA-AAAA-4AAA-8AAA-AAAAAAAAAAAA",
            string.Join(',', Enumerable.Range(0, 26).Select(_ => Guid.NewGuid().ToString("D"))) };
        var before = await fixture.SourcingConfigurationFootprint();
        foreach (var value in invalid)
            Assert.Equal(HttpStatusCode.UnprocessableEntity, (await Send(fixture.Client, HttpMethod.Get,
                SourcingReviewerLookupPath + "?principalIds=" + Uri.EscapeDataString(value), fixture.Other, role: "procurement_manager")).StatusCode);
        foreach (var suffix in new[] { "&search=Somebody", "&offset=1", "&limit=1", "&offset=-1", "&limit=1000" })
            Assert.Equal(HttpStatusCode.UnprocessableEntity, (await Send(fixture.Client, HttpMethod.Get,
                SourcingReviewerLookupPath + "?principalIds=" + id + suffix, fixture.Other, role: "procurement_manager")).StatusCode);
        Assert.Equal(before, await fixture.SourcingConfigurationFootprint());
    }
}
