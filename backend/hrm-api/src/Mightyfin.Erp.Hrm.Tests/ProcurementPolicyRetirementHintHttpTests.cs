using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Mightyfin.Erp.Hrm.Infrastructure.Data;
using Mightyfin.Erp.Procurement.Domain;
using Mightyfin.Erp.Procurement.Infrastructure.Data;
namespace Mightyfin.Erp.Hrm.Tests;
public sealed partial class ProcurementRequestHttpTests
{
    const string RetirementHintPath = "/api/procurement/v1/configuration/policies";
    static async Task AssertRetirementHints(Fixture f, Guid id, Guid actor, bool expected, string role = "procurement_manager")
    {
        var detail = await Send(f.Client, HttpMethod.Get, RetirementHintPath + "/" + id, actor, role: role);
        Assert.Equal(HttpStatusCode.OK, detail.StatusCode);
        Assert.Equal(expected, (await detail.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("canRetireDraft").GetBoolean());
        var list = await Send(f.Client, HttpMethod.Get, RetirementHintPath + "?key=SOURCING", actor, role: role);
        Assert.Equal(HttpStatusCode.OK, list.StatusCode);
        Assert.Equal(expected, (await list.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("items").EnumerateArray().Single(x => x.GetProperty("id").GetGuid() == id).GetProperty("canRetireDraft").GetBoolean());
    }
    static async Task<Guid> NewHintSourcingPolicy(Fixture f)
    {
        var response = await SendJson(f.Client, HttpMethod.Post, RetirementHintPath, f.Other, SourcingPolicy(f.Owner), "procurement_manager");
        Assert.Equal(HttpStatusCode.Created, response.StatusCode); return (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
    }
    [Fact]
    public async Task Retire_hints_require_same_original_worker_and_principal_not_another_authorized_editor()
    {
        // Synthetic mapped readiness is not actual PostgreSQL readiness/role proof.
        await using var f = await Fixture.Start(); await f.ConfigureSourcingPolicyActors(); var id = await NewHintSourcingPolicy(f);
        var before = await f.SourcingConfigurationFootprint();
        await AssertRetirementHints(f, id, f.Other, true); await AssertRetirementHints(f, id, f.Reviewer, false);
        Assert.Equal(before, await f.SourcingConfigurationFootprint());
        await f.SwapSourcingWorkers(f.Other, f.Reviewer); before = await f.SourcingConfigurationFootprint();
        await AssertRetirementHints(f, id, f.Other, false); await AssertRetirementHints(f, id, f.Reviewer, false);
        Assert.Equal(before, await f.SourcingConfigurationFootprint());
    }
    [Theory] [InlineData("schema-unavailable")] [InlineData("unknown-origin")] [InlineData("no-worker-admin")]
    public async Task Unavailable_identity_hides_only_retirement_while_policy_read_remains_usable(string fault)
    {
        await using var f = await Fixture.Start(); await f.ConfigureSourcingPolicyActors(); var id = await NewHintSourcingPolicy(f);
        if (fault == "schema-unavailable") f.SetSourcingIdentityReadiness(false);
        if (fault == "unknown-origin") await f.RemoveRetirementHintOriginForDenial(id);
        if (fault == "no-worker-admin") await f.SetRetirementHintAdminWithoutWorker(f.Other);
        var before = await f.SourcingConfigurationFootprint();
        await AssertRetirementHints(f, id, f.Other, false, fault == "no-worker-admin" ? "erp_admin" : "procurement_manager");
        Assert.Equal(before, await f.SourcingConfigurationFootprint());
    }
    private sealed partial class Fixture
    {
        internal async Task SetRetirementHintAdminWithoutWorker(Guid id)
        {
            using var services = app.Services.CreateScope(); var core = services.ServiceProvider.GetRequiredService<HrmDbContext>();
            var actor = await core.LocalUsers.SingleAsync(x => x.Id == id); actor.WorkerId = null; actor.RolesCsv = "erp_admin"; await core.SaveChangesAsync();
        }
        internal async Task RemoveRetirementHintOriginForDenial(Guid id)
        {
            // Deliberate corrupt owner-only InMemory fixture; never an adoption/authority path.
            using var services = app.Services.CreateScope(); var owner = services.ServiceProvider.GetRequiredService<ProcurementDbContext>();
            await using var raw = new DbContext(new DbContextOptionsBuilder().UseInMemoryDatabase(procurementStoreName, procurementStoreRoot).UseModel(owner.Model).Options);
            // The raw corruption fixture has no resolved request scope. Select the
            // exact synthetic owner explicitly instead of inheriting its unset filter.
            var origin = await raw.Set<SourcingPolicyIdentityEvidence>().IgnoreQueryFilters()
                .SingleAsync(x => x.PolicyVersionId == id && x.TenantId == "tenant-a" && x.LegalEntityId == company);
            raw.Remove(origin); await raw.SaveChangesAsync();
        }
    }
}
