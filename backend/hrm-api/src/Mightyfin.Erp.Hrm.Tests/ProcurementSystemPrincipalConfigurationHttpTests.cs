using Mightyfin.Erp.Hrm.Application.ConfigAndExtras;
using System.Net;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Mightyfin.Erp.Hrm.Domain.Entities;
using Mightyfin.Erp.Hrm.Infrastructure.Data;

namespace Mightyfin.Erp.Hrm.Tests;
public sealed partial class ProcurementRequestHttpTests
{
    [Theory]
    [InlineData("people")]
    [InlineData("sites")]
    [InlineData("categories")]
    public async Task Active_system_only_Core_user_can_read_receiving_configuration_selections_without_operational_authority(string kind)
    {
        await using var fixture = await Fixture.Start(); await fixture.ConfigureSystemOnlyConfigurationReader();
        var before = await fixture.SourcingConfigurationFootprint();
        var response = await Send(fixture.Client, HttpMethod.Get,
            "/api/procurement/v1/configuration/policies/lookups/receiving/" + kind, fixture.Other, role: "procurement_manager");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(before, await fixture.SourcingConfigurationFootprint());
        await fixture.RevokeSystemOnlyConfigurationRole();
        Assert.Equal(HttpStatusCode.Forbidden, (await Send(fixture.Client, HttpMethod.Get,
            "/api/procurement/v1/configuration/policies/lookups/receiving/" + kind, fixture.Other, role: "procurement_manager")).StatusCode);
    }
    private sealed partial class Fixture
    {
        public async Task ConfigureSystemOnlyConfigurationReader()
        {
            using var services = app.Services.CreateScope(); var core = services.ServiceProvider.GetRequiredService<HrmDbContext>();
            var account = await core.LocalUsers.SingleAsync(x => x.Id == Other);
            account.WorkerId = null; account.RolesCsv = "system-configuration-reader";
            core.TenantRoleAssignments.Add(new TenantRoleAssignment { RoleKey = account.RolesCsv, RoleName = "Synthetic Core configuration reader",
                Active = true, PermissionsCsv = "procurement-access,procurement-configuration-read" });
            await core.SaveChangesAsync();
        }
        public async Task RevokeSystemOnlyConfigurationRole()
        {
            using var services = app.Services.CreateScope(); var core = services.ServiceProvider.GetRequiredService<HrmDbContext>();
            var role = await core.TenantRoleAssignments.SingleAsync(x => x.RoleKey == "system-configuration-reader");
            role.PermissionsCsv = ""; await core.SaveChangesAsync();
        }
    }
}
