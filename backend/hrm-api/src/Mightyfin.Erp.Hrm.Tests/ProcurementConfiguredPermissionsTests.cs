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
    public void Return_proposal_actions_are_registered_without_bootstrap_grants(string action)
    {
        var catalog=Mightyfin.Erp.Hrm.Application.ProcurementActionCatalog.AdminReadCatalog();
        Assert.Contains(catalog,x=>x.Key==action && x.Group=="Receiving and returns");
        Assert.True(Mightyfin.Erp.Hrm.Application.ProcurementActionCatalog.RequiresExplicitConfiguredGrant(action));
        Assert.Empty(Mightyfin.Erp.Hrm.Application.ProcurementActionCatalog.BootstrapGrants[action]);
        Assert.DoesNotContain(action,Mightyfin.Erp.Hrm.Application.ProcurementActionCatalog
            .ResolveConfiguredActionsForRole("erp_admin","erp_admin"));
        Assert.False(ProcurementPermissions.Allows(action,[new Claim(ClaimTypes.Role,"erp_admin")]));
        Assert.True(ProcurementPermissions.Allows(action,[
            new Claim(ProcurementPermissions.ConfiguredClaim,"true"),
            new Claim(ProcurementPermissions.ActionClaim,"procurement-access"),
            new Claim(ProcurementPermissions.ActionClaim,action)]));
        Assert.Contains(action,JobsAdminServiceImpl.PermissionKeys);
    }

    [Fact]
    public async Task Master_import_actions_require_exact_current_explicit_Core_grants()
    {
        const string vendor = "procurement-vendor-import", item = "procurement-item-import";
        foreach (var action in new[] { vendor, item })
        {
            Assert.Empty(Mightyfin.Erp.Hrm.Application.ProcurementActionCatalog.BootstrapGrants[action]);
            Assert.True(Mightyfin.Erp.Hrm.Application.ProcurementActionCatalog.RequiresExplicitConfiguredGrant(action));
            Assert.DoesNotContain(action, Mightyfin.Erp.Hrm.Application.ProcurementActionCatalog
                .ResolveConfiguredActionsForRole("erp_admin", "erp_admin"));
            Assert.False(ProcurementPermissions.Allows(action, [new Claim(ClaimTypes.Role, "erp_admin")]));
            Assert.Contains(action, JobsAdminServiceImpl.PermissionKeys);
        }
        await using var db = TestDbContextFactory.Create("tenant-a");
        var row = new TenantRoleAssignment { RoleKey = "importer", RoleName = "Importer", Category = "procurement",
            PermissionsCsv = "procurement-access,procurement-vendor-create," + vendor, Active = true };
        db.TenantRoleAssignments.Add(row); await db.SaveChangesAsync();
        var actions = await ProcurementPermissions.ResolveConfiguredActionsAsync(db, row.RoleKey, default);
        var claims = new[] { new Claim(ClaimTypes.Role, row.RoleKey),
            new Claim(ProcurementPermissions.ConfiguredClaim, "true") }
            .Concat(actions.Select(x => new Claim(ProcurementPermissions.ActionClaim, x))).ToArray();
        Assert.True(ProcurementPermissions.Allows(vendor, claims));
        Assert.True(ProcurementPermissions.Allows("procurement-vendor-create", claims));
        Assert.False(ProcurementPermissions.Allows(item, claims));
        row.PermissionsCsv = "procurement-access,procurement-vendor-create";
        await db.SaveChangesAsync();
        Assert.DoesNotContain(vendor, await ProcurementPermissions.ResolveConfiguredActionsAsync(db, row.RoleKey, default));
    }

    [Fact]
    public async Task Csv_export_actions_are_explicit_independent_grants_even_for_legacy_ERP_admin()
    {
        const string vendor = "procurement-vendor-export", item = "procurement-item-export";
        foreach (var action in new[] { vendor, item })
        {
            Assert.Empty(Mightyfin.Erp.Hrm.Application.ProcurementActionCatalog.BootstrapGrants[action]);
            Assert.DoesNotContain(action, Mightyfin.Erp.Hrm.Application.ProcurementActionCatalog.ForRole("procurement_manager"));
            Assert.DoesNotContain(action, Mightyfin.Erp.Hrm.Application.ProcurementActionCatalog
                .ResolveConfiguredActionsForRole("erp_admin", "erp_admin"));
            Assert.False(ProcurementPermissions.Allows(action, [new Claim(ClaimTypes.Role, "erp_admin")]));
            Assert.Contains(action, JobsAdminServiceImpl.PermissionKeys);
        }
        await using var db = TestDbContextFactory.Create("tenant-a");
        var row = new TenantRoleAssignment { RoleKey = "exporter", RoleName = "Exporter", Category = "procurement",
            PermissionsCsv = "procurement-access,procurement-vendor-read," + vendor, Active = true };
        db.TenantRoleAssignments.Add(row); await db.SaveChangesAsync();
        var actions = await ProcurementPermissions.ResolveConfiguredActionsAsync(db, "exporter", default);
        var claims = new[] { new Claim(ClaimTypes.Role, "exporter"), new Claim(ProcurementPermissions.ConfiguredClaim, "true") }
            .Concat(actions.Select(x => new Claim(ProcurementPermissions.ActionClaim, x))).ToArray();
        Assert.True(ProcurementPermissions.Allows(vendor, claims));
        Assert.False(ProcurementPermissions.Allows(item, claims));
        Assert.True(ProcurementPermissions.Allows("procurement-vendor-read", claims));
        Assert.False(ProcurementPermissions.Allows("procurement-item-manage", claims));
        row.PermissionsCsv = "procurement-access,procurement-vendor-read"; await db.SaveChangesAsync();
        Assert.DoesNotContain(vendor, await ProcurementPermissions.ResolveConfiguredActionsAsync(db, "exporter", default));
    }
    [Theory]
    [InlineData("employee")][InlineData("manager")][InlineData("procurement_buyer")]
    [InlineData("procurement_manager")][InlineData("hr_admin")]
    public void Governed_sourcing_actions_have_no_ordinary_bootstrap_or_legacy_permission_alias(string role)
    {
        foreach (var action in new[] { "procurement-sourcing-convert", "procurement-sourcing-cancel",
            "procurement-sourcing-submit", "procurement-sourcing-publish" })
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

    [Fact]
    public void Legacy_admin_does_not_inherit_new_publication_actions()
    {
        foreach (var action in new[] { "procurement-sourcing-submit", "procurement-sourcing-publish" })
        {
            Assert.True(Mightyfin.Erp.Hrm.Application.ProcurementActionCatalog.RequiresExplicitConfiguredGrant(action));
            Assert.False(ProcurementPermissions.Allows(action, [new Claim(ClaimTypes.Role, "erp_admin")]));
            Assert.DoesNotContain(action, Mightyfin.Erp.Hrm.Application.ProcurementActionCatalog
                .ResolveConfiguredActionsForRole("erp_admin", "erp_admin"));
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
        Assert.DoesNotContain("procurement-sourcing-publish", actions);
        Assert.DoesNotContain("procurement-sourcing-submit", actions);
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
