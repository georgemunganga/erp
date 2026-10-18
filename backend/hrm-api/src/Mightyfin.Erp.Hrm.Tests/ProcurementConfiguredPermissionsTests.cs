using System.Security.Claims;
using Mightyfin.Erp.Hrm.Api;
using Mightyfin.Erp.Hrm.Application.ConfigAndExtras;

namespace Mightyfin.Erp.Hrm.Tests;

public sealed class ProcurementConfiguredPermissionsTests
{
    [Fact]
    public async Task Explicit_core_role_actions_replace_bootstrap_actions_for_local_sessions()
    {
        await using var db = TestDbContextFactory.Create("tenant-a");
        db.TenantRoleAssignments.Add(new TenantRoleAssignment
        {
            RoleKey = "procurement_buyer", RoleName = "Buyer", Category = "procurement",
            PermissionsCsv = "procurement-access,procurement-vendor-read", Active = true,
        });
        await db.SaveChangesAsync();

        var actions = await ProcurementPermissions.ResolveConfiguredActionsAsync(db, "procurement_buyer", default);
        var claims = new[] { new Claim(ClaimTypes.Role, "procurement_buyer"),
            new Claim(ProcurementPermissions.ConfiguredClaim, "true") }
            .Concat(actions.Select(x => new Claim(ProcurementPermissions.ActionClaim, x))).ToArray();

        Assert.Equal(["procurement-access", "procurement-vendor-read"], actions);
        Assert.True(ProcurementPermissions.Allows("procurement-vendor-read", claims));
        Assert.False(ProcurementPermissions.Allows("procurement-vendor-manage", claims));
        Assert.False(ProcurementPermissions.Allows("procurement-item-manage", claims));
    }

    [Fact]
    public async Task Disabled_role_and_missing_access_action_deny_procurement()
    {
        await using var db = TestDbContextFactory.Create("tenant-a");
        db.TenantRoleAssignments.Add(new TenantRoleAssignment
        {
            RoleKey = "procurement_buyer", RoleName = "Buyer", Category = "procurement",
            PermissionsCsv = "procurement-vendor-read", Active = false,
        });
        await db.SaveChangesAsync();
        Assert.Empty(await ProcurementPermissions.ResolveConfiguredActionsAsync(db, "procurement_buyer", default));

        var claims = new[] { new Claim(ClaimTypes.Role, "procurement_buyer"),
            new Claim(ProcurementPermissions.ConfiguredClaim, "true"),
            new Claim(ProcurementPermissions.ActionClaim, "procurement-vendor-read") };
        Assert.False(ProcurementPermissions.Allows("procurement-access", claims));
        Assert.False(ProcurementPermissions.Allows("procurement-vendor-read", claims));
    }
}
