using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Mightyfin.Erp.Hrm.Api;
using Mightyfin.Erp.Hrm.Application.ConfigAndExtras;
using Mightyfin.Erp.Hrm.Domain.Entities;
using Mightyfin.Erp.Hrm.Infrastructure.Data;

namespace Mightyfin.Erp.Hrm.Tests;

public sealed class LocalIdentityRoleFallbackTests
{
    [Theory]
    [InlineData(false, false)]
    [InlineData(true, true)]
    public async Task Configured_inactive_or_archived_admin_never_revives_CSV_admin(bool active, bool archived)
    {
        await using var db = TestDbContextFactory.Create("role-current");
        var account = AdminAccount();
        db.LocalUsers.Add(account);
        db.TenantRoleAssignments.Add(new TenantRoleAssignment
        {
            RoleKey = "erp_admin", RoleName = "Configured admin", PermissionsCsv = "erp_admin",
            Active = active, IsArchived = archived,
        });
        await db.SaveChangesAsync();

        Assert.Empty(await LocalIdentityRoutes.ResolveGrantedRolesAsync(db, account.RolesCsv, default));
        Assert.Empty(await LocalIdentityRoutes.RequireAssignableRolesAsync(db, ["erp_admin"], default));
        Assert.False(await LocalIdentityRoutes.ActiveAdminUserRemainsAsync(db, default));
        Assert.False(LocalIdentityRoutes.UserGrantsAdmin(account,
            await db.TenantRoleAssignments.AsNoTracking().ToArrayAsync()));
    }

    [Fact]
    public async Task Truly_unconfigured_tenant_keeps_legacy_HRM_CSV_compatibility()
    {
        await using var db = TestDbContextFactory.Create("role-current");
        var account = AdminAccount();
        db.LocalUsers.Add(account);
        await db.SaveChangesAsync();

        Assert.Equal(["erp_admin"], await LocalIdentityRoutes.ResolveGrantedRolesAsync(db, account.RolesCsv, default));
        Assert.Equal(["erp_admin"], await LocalIdentityRoutes.RequireAssignableRolesAsync(db, ["erp_admin"], default));
        Assert.True(await LocalIdentityRoutes.ActiveAdminUserRemainsAsync(db, default));
        Assert.True(LocalIdentityRoutes.UserGrantsAdmin(account, []));
    }

    [Fact]
    public async Task Current_active_custom_role_uses_its_configured_grants()
    {
        await using var db = TestDbContextFactory.Create("role-current");
        var account = AdminAccount("custom_admin");
        db.LocalUsers.Add(account);
        db.TenantRoleAssignments.Add(new TenantRoleAssignment
        {
            RoleKey = "custom_admin", RoleName = "Custom admin", PermissionsCsv = "hr_admin", Active = true,
        });
        await db.SaveChangesAsync();

        Assert.Equal(["custom_admin", "hr_admin"],
            await LocalIdentityRoutes.ResolveGrantedRolesAsync(db, account.RolesCsv, default));
        Assert.Equal(["custom_admin"],
            await LocalIdentityRoutes.RequireAssignableRolesAsync(db, ["custom_admin"], default));
        Assert.True(await LocalIdentityRoutes.ActiveAdminUserRemainsAsync(db, default));
    }

    [Fact]
    public async Task Another_tenants_role_master_does_not_disable_current_tenants_legacy_fallback()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var tenant = new MutableTenantAccessor("role-other");
        var options = new DbContextOptionsBuilder<HrmDbContext>().UseSqlite(connection).Options;
        await using var db = new HrmDbContext(options, tenant);
        await db.Database.EnsureCreatedAsync();
        db.TenantRoleAssignments.Add(new TenantRoleAssignment
        {
            RoleKey = "hr_admin", RoleName = "Other tenant only", PermissionsCsv = "hr_admin", Active = true,
        });
        await db.SaveChangesAsync();

        tenant.TenantId = "role-current";
        Assert.Equal(["erp_admin"], await LocalIdentityRoutes.ResolveGrantedRolesAsync(db, "erp_admin", default));
        Assert.Equal(["erp_admin"],
            await LocalIdentityRoutes.RequireAssignableRolesAsync(db, ["erp_admin"], default));
        Assert.Empty(await db.TenantRoleAssignments.AsNoTracking().ToArrayAsync());
    }

    private static LocalUser AdminAccount(string role = "erp_admin") => new()
    {
        Email = "role-fixture@example.invalid", NormalizedEmail = "ROLE-FIXTURE@EXAMPLE.INVALID",
        DisplayName = "Role fixture", PasswordHash = "test-only", RolesCsv = role, IsActive = true,
    };

    private sealed class MutableTenantAccessor(string tenantId) : ITenantAccessor
    {
        public string TenantId { get; set; } = tenantId;
        public string GetTenantId() => TenantId;
    }
}
