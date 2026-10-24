using System.Security.Claims;
using Mightyfin.Erp.Hrm.Api;
using Mightyfin.Erp.Hrm.Application.ConfigAndExtras;

namespace Mightyfin.Erp.Hrm.Tests;

public sealed class ProcurementConfiguredPermissionsTests
{
    [Theory]
    [InlineData("procurement-return-read")]
    [InlineData("procurement-return-create")]
    [InlineData("procurement-return-edit")]
    public void P37_draft_return_actions_require_explicit_current_Core_grants_even_for_legacy_admin(string action)
    {
        var options = new Microsoft.AspNetCore.Authorization.AuthorizationOptions();
        ProcurementPermissions.AddPolicies(options);
        Assert.NotNull(options.GetPolicy(action));
        Assert.Empty(Mightyfin.Erp.Hrm.Application.ProcurementActionCatalog.BootstrapGrants[action]);
        Assert.Contains(action, JobsAdminServiceImpl.PermissionKeys);
        Assert.False(ProcurementPermissions.Allows(action, [new Claim(ClaimTypes.Role, "erp_admin")]));
        Assert.False(ProcurementPermissions.Allows(action, [new Claim(ClaimTypes.Role, "procurement_manager")]));
        Assert.True(ProcurementPermissions.Allows(action, [new Claim(ClaimTypes.Role, "erp_admin"),
            new Claim(ProcurementPermissions.ConfiguredClaim, "true"),
            new Claim(ProcurementPermissions.ActionClaim, "procurement-access"),
            new Claim(ProcurementPermissions.ActionClaim, action)]));
    }

    [Fact]
    public void Submission_and_publication_have_distinct_registered_authorization_policies()
    {
        var options = new Microsoft.AspNetCore.Authorization.AuthorizationOptions();
        ProcurementPermissions.AddPolicies(options);
        Assert.NotNull(options.GetPolicy("procurement-sourcing-submit"));
        Assert.NotNull(options.GetPolicy("procurement-sourcing-publish"));
        Assert.Null(options.GetPolicy("procurement-sourcing-unregistered"));
    }

    [Theory]
    [InlineData("procurement-sourcing-commercial-configure")]
    [InlineData("procurement-sourcing-commercial-approve")]
    [InlineData("procurement-sourcing-offer-submit")]
    [InlineData("procurement-sourcing-round-close")]
    [InlineData("procurement-sourcing-round-open")]
    public void P32_commercial_action_is_catalogued_without_any_bootstrap_grant(string action)
    {
        Assert.Empty(Mightyfin.Erp.Hrm.Application.ProcurementActionCatalog.BootstrapGrants[action]);
        Assert.Contains(action, JobsAdminServiceImpl.PermissionKeys);
        Assert.DoesNotContain(action, Mightyfin.Erp.Hrm.Application.ProcurementActionCatalog.ForRole("procurement_manager"));
        Assert.False(ProcurementPermissions.Allows(action,
            [new Claim(ClaimTypes.Role, "procurement_manager")]));
    }

    [Theory]
    [InlineData("employee")][InlineData("manager")][InlineData("procurement_buyer")]
    [InlineData("procurement_manager")][InlineData("hr_admin")]
    public void Governed_sourcing_actions_have_no_ordinary_bootstrap_or_legacy_permission_alias(string role)
    {
        foreach (var action in new[] { "procurement-sourcing-convert", "procurement-sourcing-cancel", "procurement-sourcing-submit", "procurement-sourcing-publish" })
        {
            Assert.Empty(Mightyfin.Erp.Hrm.Application.ProcurementActionCatalog.BootstrapGrants[action]);
            Assert.DoesNotContain(action, Mightyfin.Erp.Hrm.Application.ProcurementActionCatalog.ForRole(role));
            Assert.Contains(action, JobsAdminServiceImpl.PermissionKeys);
            Assert.False(ProcurementPermissions.Allows(action, [new Claim(ClaimTypes.Role, role)]));
            Assert.False(ProcurementPermissions.Allows(action, [new Claim(ClaimTypes.Role, role),
                new Claim(ProcurementPermissions.ConfiguredClaim, "true"),
                new Claim(ProcurementPermissions.ActionClaim, "procurement-access"),
                new Claim(ProcurementPermissions.ActionClaim, "procurement-sourcing-create"),
                new Claim(ProcurementPermissions.ActionClaim, "procurement-sourcing-edit")]));
        }
    }

    [Theory]
    [InlineData("")][InlineData("unknown-action")]
    [InlineData("procurement-access,procurement-sourcing-create,procurement-sourcing-edit")]
    public async Task Existing_or_unknown_Core_role_permissions_do_not_gain_source_reservation_actions(string csv)
    {
        await using var db = TestDbContextFactory.Create("tenant-a");
        db.TenantRoleAssignments.Add(new TenantRoleAssignment { RoleKey = "procurement_buyer", RoleName = "Buyer",
            Category = "procurement", PermissionsCsv = csv, Active = true });
        await db.SaveChangesAsync();
        var actions = await ProcurementPermissions.ResolveConfiguredActionsAsync(db, "procurement_buyer", default);
        Assert.DoesNotContain("procurement-sourcing-convert", actions);
        Assert.DoesNotContain("procurement-sourcing-cancel", actions);
        Assert.DoesNotContain("procurement-sourcing-submit", actions);
        Assert.DoesNotContain("procurement-sourcing-publish", actions);
        if (csv == "procurement-access,procurement-sourcing-create,procurement-sourcing-edit")
            Assert.Equal(new[] { "procurement-access", "procurement-sourcing-create", "procurement-sourcing-edit" }, actions);
        else
            Assert.Empty(actions); // Missing/unknown configured permissions never restore legacy grants.
    }

    [Theory]
    [InlineData("procurement-sourcing-convert", "procurement-sourcing-cancel")]
    [InlineData("procurement-sourcing-cancel", "procurement-sourcing-convert")]
    [InlineData("procurement-sourcing-publish", "procurement-sourcing-convert")]
    [InlineData("procurement-sourcing-submit", "procurement-sourcing-publish")]
    [InlineData("procurement-sourcing-publish", "procurement-sourcing-submit")]
    public async Task Explicit_source_action_is_independent_and_current_Core_revocation_removes_it(string granted, string denied)
    {
        await using var db = TestDbContextFactory.Create("tenant-a");
        var row = new TenantRoleAssignment { RoleKey = "sourcing_operator", RoleName = "Sourcing operator",
            Category = "procurement", PermissionsCsv = "procurement-access," + granted, Active = true };
        db.TenantRoleAssignments.Add(row); await db.SaveChangesAsync();
        var actions = await ProcurementPermissions.ResolveConfiguredActionsAsync(db, row.RoleKey, default);
        Assert.Equal(new[] { "procurement-access", granted }, actions);
        var claims = new[] { new Claim(ClaimTypes.Role, row.RoleKey), new Claim(ProcurementPermissions.ConfiguredClaim, "true") }
            .Concat(actions.Select(x => new Claim(ProcurementPermissions.ActionClaim, x))).ToArray();
        Assert.True(ProcurementPermissions.Allows(granted, claims));
        Assert.False(ProcurementPermissions.Allows(denied, claims));
        Assert.False(ProcurementPermissions.Allows("procurement-po-convert", claims));
        Assert.False(ProcurementPermissions.Allows("procurement-approval-decide", claims));

        row.PermissionsCsv = "procurement-access"; await db.SaveChangesAsync();
        Assert.DoesNotContain(granted, await ProcurementPermissions.ResolveConfiguredActionsAsync(db, row.RoleKey, default));
        row.Active = false; await db.SaveChangesAsync();
        Assert.Empty(await ProcurementPermissions.ResolveConfiguredActionsAsync(db, row.RoleKey, default));
        row.Active = true; row.IsArchived = true; await db.SaveChangesAsync();
        Assert.Empty(await ProcurementPermissions.ResolveConfiguredActionsAsync(db, row.RoleKey, default));
        Assert.Empty(await ProcurementPermissions.ResolveConfiguredActionsAsync(db, "missing_role", default));
    }

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
