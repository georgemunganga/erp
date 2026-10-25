using Microsoft.EntityFrameworkCore;
using Microsoft.Data.Sqlite;
using System.Security.Claims;
using Mightyfin.Erp.Hrm.Api;
using Mightyfin.Erp.Hrm.Application;
using Mightyfin.Erp.Hrm.Application.ConfigAndExtras;
using Mightyfin.Erp.Hrm.Domain.Entities;
using Mightyfin.Erp.Hrm.Infrastructure;
using Mightyfin.Erp.Hrm.Infrastructure.Data;

using Xunit;

namespace Mightyfin.Erp.Hrm.Tests;

/// <summary>M28: CRUD coverage for jobs catalogue, tenant role assignments and
/// retention rules on the new jobs/tenant_role_assignments/retention_rules tables.</summary>
public sealed class JobsAdminTests
{
    private static readonly string TenantId = Guid.CreateVersion7().ToString();

    private static HrmDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<HrmDbContext>()
            .UseSqlite($"Data Source=m28-{Guid.CreateVersion7()};Mode=Memory;Cache=Shared")
            .Options;
        var db = new HrmDbContext(options, new FixedTenantAccessor(TenantId));
        db.Database.OpenConnection();
        db.Database.EnsureCreated();
        return db;
    }

    private static JobsAdminServiceImpl CreateService(HrmDbContext db)
    {
        var repo = new ConfigRepository(db);
        var authz = new PermissiveAuthz { Roles = ["hr_ops", "hr_admin"] };
        return new JobsAdminServiceImpl(repo, authz);
    }

    [Fact]
    public async Task Job_crud_full_cycle()
    {
        using var db = CreateContext();
        var svc = CreateService(db);
        var legal = new LegalEntity { Code = "MF001", RegisteredName = "Mighty Finance Limited" };
        db.Set<LegalEntity>().AddRange(legal);
        var units = new List<OrgUnit> { new() { Code = "HR", Name = "Human Resources", LegalEntityId = legal.Id, Status = "active", EffectiveFrom = DateOnly.FromDateTime(new DateTime(2026, 1, 1)) } };
        db.Set<OrgUnit>().AddRange(units);
        await db.SaveChangesAsync();

        var created = await svc.CreateJobAsync(new JobCreateRequest("ACC-001", "Accountant I", units[0].Id, "G4"), CancellationToken.None);
        Assert.Equal("ACC-001", created.Code);
        Assert.Equal("active", created.Status);

        var updated = await svc.UpdateJobAsync(created.Id, new JobUpdateRequest("Accountant II"), CancellationToken.None);
        Assert.Equal("Accountant II", updated.Title);

        var closed = await svc.CloseJobAsync(created.Id, CancellationToken.None);
        Assert.Equal("inactive", closed.Status);
        var list = await svc.ListJobsAsync(false, CancellationToken.None);
        Assert.DoesNotContain(list, j => j.Id == created.Id);
        var all = await svc.ListJobsAsync(true, CancellationToken.None);
        Assert.Single(all);
    }

    [Fact]
    public async Task Duplicate_job_code_rejected()
    {
        using var db = CreateContext();
        var svc = CreateService(db);
        await svc.CreateJobAsync(new JobCreateRequest("PAY-01", "Payroll Clerk", Grade: "G2"), CancellationToken.None);
        var ex = await Assert.ThrowsAsync<DomainException>(() =>
            svc.CreateJobAsync(new JobCreateRequest("pay-01", "Payroll Clerk 2"), CancellationToken.None));
        Assert.Equal("job-code-taken", ex.Code);
    }

    [Fact]
    public async Task Role_assignments_seeded_and_toggled()
    {
        using var db = CreateContext();
        var svc = CreateService(db);
        var repo = new ConfigRepository(db);
        foreach (var key in new[] { "employee", "manager", "hr_ops", "payroll", "finance_approver", "hr_admin", "investigator" })
            await repo.CreateRoleAssignmentAsync(new TenantRoleAssignment { RoleKey = key, RoleName = key, Category = "hrm", Active = true }, CancellationToken.None);

        var roles = await svc.ListRolesAsync(CancellationToken.None);
        Assert.Equal(7, roles.Count);

        var created = await svc.CreateRoleAsync(new RoleCreateRequest("hr_supervisor", "HR Supervisor", "hrm", ["hr_ops", "manager"]), CancellationToken.None);
        Assert.Equal(["hr_ops", "manager"], created.Permissions);

        var toggled = await svc.UpdateRoleAsync("manager", new RoleUpdateRequest(false), CancellationToken.None);
        Assert.False(toggled.Active);

        var lockout = await Assert.ThrowsAsync<DomainException>(() =>
            svc.UpdateRoleAsync("hr_admin", new RoleUpdateRequest(false), CancellationToken.None));
        Assert.Equal("last-admin-role", lockout.Code);

        await Assert.ThrowsAsync<DomainException>(() =>
            svc.UpdateRoleAsync("nonexistent", new RoleUpdateRequest(true), CancellationToken.None));
    }

    [Fact]
    public async Task Missing_Core_role_setup_rejects_custom_role_without_changing_login_or_procurement_grants()
    {
        using var db = CreateContext();
        db.LocalUsers.Add(new LocalUser { Email = "legacy@example.invalid", NormalizedEmail = "LEGACY@EXAMPLE.INVALID",
            DisplayName = "Legacy admin", PasswordHash = "test-only", RolesCsv = "erp_admin", IsActive = true });
        await db.SaveChangesAsync();
        var before = await LocalIdentityRoutes.ResolveGrantedRolesAsync(db, "erp_admin", default);
        Assert.Equal(["erp_admin"], before);
        var procurementBefore = await ProcurementPermissions.ResolveConfiguredActionsAsync(db, "erp_admin", default, TenantId);
        Assert.Empty(procurementBefore);
        var configuredClaims = new[] { new Claim(ProcurementPermissions.ConfiguredClaim, "true"),
            new Claim(ClaimTypes.Role, "erp_admin"), new Claim("tenant", TenantId) };
        Assert.False(ProcurementPermissions.Allows("procurement-access", configuredClaims));
        Assert.Empty(await db.TenantRoleAssignments.ToArrayAsync());

        var error = await Assert.ThrowsAsync<DomainException>(() => CreateService(db).CreateRoleAsync(
            new RoleCreateRequest("source_operator", "Source operator", "procurement",
                ["procurement-access", "procurement-sourcing-convert"]), default));
        Assert.Equal("role-setup-required", error.Code);
        Assert.Contains("Core role setup", error.Message);
        Assert.Empty(await db.TenantRoleAssignments.ToArrayAsync());
        Assert.Equal(before, await LocalIdentityRoutes.ResolveGrantedRolesAsync(db, "erp_admin", default));
        Assert.True(await LocalIdentityRoutes.ActiveAdminUserRemainsAsync(db, default));
        Assert.Equal(procurementBefore,
            await ProcurementPermissions.ResolveConfiguredActionsAsync(db, "erp_admin", default, TenantId));
        Assert.False(ProcurementPermissions.Allows("procurement-access", configuredClaims));
        // Owner setup is a separate controlled operation. Once a real master
        // exists, ordinary CRUD may create a custom role without resurrecting it.
        db.TenantRoleAssignments.Add(new TenantRoleAssignment { RoleKey = "erp_admin", RoleName = "ERP admin",
            Category = "system", PermissionsCsv = "erp_admin", Active = false });
        await db.SaveChangesAsync();
        Assert.Empty(await LocalIdentityRoutes.ResolveGrantedRolesAsync(db, "erp_admin", default));
        Assert.Empty(await ProcurementPermissions.ResolveConfiguredActionsAsync(db, "erp_admin", default, TenantId));
        Assert.False(await LocalIdentityRoutes.ActiveAdminUserRemainsAsync(db, default));
        await CreateService(db).CreateRoleAsync(new RoleCreateRequest("second_custom", "Second custom",
            "hrm", ["manager"]), default);
        Assert.False((await db.TenantRoleAssignments.SingleAsync(x => x.RoleKey == "erp_admin")).Active);
        Assert.Equal(2, await db.TenantRoleAssignments.CountAsync());
    }

    [Fact]
    public async Task Missing_setup_rejects_attempt_to_replace_legacy_token_with_new_permissions()
    {
        using var db = CreateContext();
        var error = await Assert.ThrowsAsync<DomainException>(() => CreateService(db).CreateRoleAsync(
            new RoleCreateRequest("manager", "New manager", "hrm", ["hr_admin"]), default));
        Assert.Equal("role-setup-required", error.Code);
        Assert.Empty(await db.TenantRoleAssignments.ToArrayAsync());
    }

    [Fact]
    public async Task Existing_archived_configuration_never_triggers_legacy_role_seeding()
    {
        using var db = CreateContext();
        db.TenantRoleAssignments.Add(new TenantRoleAssignment { RoleKey = "erp_admin", RoleName = "Retired admin",
            Category = "system", PermissionsCsv = "erp_admin", Active = true, IsArchived = true });
        await db.SaveChangesAsync();
        await CreateService(db).CreateRoleAsync(new RoleCreateRequest("separate_role", "Separate role",
            "hrm", ["manager"]), default);
        Assert.Equal(2, await db.TenantRoleAssignments.CountAsync());
        Assert.Empty(await LocalIdentityRoutes.ResolveGrantedRolesAsync(db, "erp_admin", default));
        Assert.True((await db.TenantRoleAssignments.SingleAsync(x => x.RoleKey == "erp_admin")).IsArchived);
    }

    [Fact]
    public async Task Missing_setup_guard_is_tenant_scoped_and_does_not_reuse_other_tenant_roles()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var tenant = new MutableTenantAccessor("first-tenant");
        var options = new DbContextOptionsBuilder<HrmDbContext>().UseSqlite(connection).Options;
        using var db = new HrmDbContext(options, tenant);
        await db.Database.EnsureCreatedAsync();
        db.TenantRoleAssignments.Add(new TenantRoleAssignment { RoleKey = "hr_admin", RoleName = "First tenant admin",
            PermissionsCsv = "hr_admin", Active = true });
        await db.SaveChangesAsync();
        await CreateService(db).CreateRoleAsync(new RoleCreateRequest("one_custom", "One custom", "hrm", ["manager"]), default);
        var firstCount = await db.TenantRoleAssignments.CountAsync();
        Assert.Equal(2, firstCount);

        tenant.TenantId = "second-tenant";
        Assert.Empty(await db.TenantRoleAssignments.AsNoTracking().ToArrayAsync());
        Assert.Equal(["erp_admin"], await LocalIdentityRoutes.ResolveGrantedRolesAsync(db, "erp_admin", default));
        var denied = await Assert.ThrowsAsync<DomainException>(() => CreateService(db).CreateRoleAsync(
            new RoleCreateRequest("two_custom", "Two custom", "hrm", ["manager"]), default));
        Assert.Equal("role-setup-required", denied.Code);
        Assert.Empty(await db.TenantRoleAssignments.ToArrayAsync());
        Assert.DoesNotContain(await db.TenantRoleAssignments.ToArrayAsync(), x => x.RoleKey == "one_custom");

        tenant.TenantId = "first-tenant";
        Assert.Equal(firstCount, await db.TenantRoleAssignments.CountAsync());
        Assert.DoesNotContain(await db.TenantRoleAssignments.ToArrayAsync(), x => x.RoleKey == "two_custom");
    }

    [Fact]
    public async Task Archived_admin_role_cannot_satisfy_last_admin_guard()
    {
        using var db = CreateContext();
        db.TenantRoleAssignments.AddRange(
            new TenantRoleAssignment { RoleKey = "hr_admin", RoleName = "Archived admin",
                PermissionsCsv = "hr_admin", Active = true, IsArchived = true },
            new TenantRoleAssignment { RoleKey = "manager", RoleName = "Manager",
                PermissionsCsv = "manager", Active = true });
        await db.SaveChangesAsync();

        var failure = await Assert.ThrowsAsync<DomainException>(() =>
            CreateService(db).UpdateRoleAsync("manager", new RoleUpdateRequest(false), CancellationToken.None));
        Assert.Equal("last-admin-role", failure.Code);
    }

    [Fact]
    public async Task Existing_Core_role_CRUD_roundtrips_explicit_governed_sourcing_actions()
    {
        using var db = CreateContext(); var service = CreateService(db);
        db.TenantRoleAssignments.Add(new TenantRoleAssignment { RoleKey = "hr_admin", RoleName = "Admin",
            Category = "system", PermissionsCsv = "hr_admin", Active = true });
        await db.SaveChangesAsync();
        var created = await service.CreateRoleAsync(new RoleCreateRequest("source_operator", "Source operator", "procurement",
            ["procurement-access", "procurement-sourcing-convert"]), default);
        Assert.Contains("procurement-sourcing-convert", created.Permissions);
        Assert.DoesNotContain("procurement-sourcing-cancel", created.Permissions);
        var updated = await service.UpdateRoleAsync("source_operator", new RoleUpdateRequest(Permissions:
            ["procurement-access", "procurement-sourcing-cancel"]), default);
        Assert.Contains("procurement-sourcing-cancel", updated.Permissions);
        Assert.DoesNotContain("procurement-sourcing-convert", updated.Permissions);
        var read = Assert.Single((await service.ListRolesAsync(default)).Where(x => x.RoleKey == "source_operator"));
        Assert.Equal(updated.Permissions, read.Permissions);
        var publication = await service.UpdateRoleAsync("source_operator",
            new RoleUpdateRequest(Permissions: ["procurement-access", "procurement-sourcing-publish"]), default);
        Assert.Contains("procurement-sourcing-publish", publication.Permissions); // Existing catalog action; no endpoint activation implied.
        var submission = await service.UpdateRoleAsync("source_operator",
            new RoleUpdateRequest(Permissions: ["procurement-access", "procurement-sourcing-submit"]), default);
        Assert.Contains("procurement-sourcing-submit", submission.Permissions);
        Assert.DoesNotContain("procurement-sourcing-publish", submission.Permissions);
        var error = await Assert.ThrowsAsync<DomainException>(() => service.UpdateRoleAsync("source_operator",
            new RoleUpdateRequest(Permissions: ["procurement-access", "procurement-sourcing-unregistered"]), default));
        Assert.Equal("invalid-permissions", error.Code);
    }

    [Fact]
    public async Task Procurement_role_readback_matches_configured_session_actions_without_phantom_bootstrap()
    {
        using var db = CreateContext();
        db.TenantRoleAssignments.AddRange(
            new TenantRoleAssignment { RoleKey = "procurement_buyer", RoleName = "Empty buyer", Category = "procurement", PermissionsCsv = "", Active = true },
            new TenantRoleAssignment { RoleKey = "buyer_unknown", RoleName = "Unknown", Category = "procurement", PermissionsCsv = "procurement-unknown", Active = true },
            new TenantRoleAssignment { RoleKey = "buyer_hr_only", RoleName = "HR only", Category = "procurement", PermissionsCsv = "hr_admin", Active = true },
            new TenantRoleAssignment { RoleKey = "manager", RoleName = "Legacy manager", Category = "procurement", PermissionsCsv = "manager", Active = true },
            new TenantRoleAssignment { RoleKey = "custom_operator", RoleName = "Explicit", Category = "procurement",
                PermissionsCsv = "procurement-access,procurement-sourcing-publish,Procurement-po-issue,unknown-action", Active = true },
            new TenantRoleAssignment { RoleKey = "erp_admin", RoleName = "Legacy ERP admin", Category = "system", PermissionsCsv = "erp_admin", Active = true },
            new TenantRoleAssignment { RoleKey = "disabled_buyer", RoleName = "Disabled", Category = "procurement",
                PermissionsCsv = "procurement-access,procurement-vendor-read", Active = false },
            new TenantRoleAssignment { RoleKey = "hr_only", RoleName = "HR projection", Category = "hrm", PermissionsCsv = "manager", Active = true });
        await db.SaveChangesAsync();

        var roles = await CreateService(db).ListRolesAsync(default);
        foreach (var role in roles)
            Assert.Equal(await ProcurementPermissions.ResolveConfiguredActionsAsync(db, role.RoleKey, default, TenantId),
                role.EffectiveProcurementPermissions);
        foreach (var key in new[] { "procurement_buyer", "buyer_unknown", "buyer_hr_only" })
            Assert.Empty(Assert.Single(roles, x => x.RoleKey == key).Permissions);
        Assert.Contains("procurement-request-read", Assert.Single(roles, x => x.RoleKey == "manager").Permissions);
        Assert.Equal(["procurement-access", "procurement-sourcing-publish"],
            Assert.Single(roles, x => x.RoleKey == "custom_operator").Permissions);
        var admin = Assert.Single(roles, x => x.RoleKey == "erp_admin");
        Assert.Equal(["erp_admin"], admin.Permissions); // Keep the system-role edit projection.
        Assert.DoesNotContain("procurement-return-read", admin.EffectiveProcurementPermissions);
        Assert.DoesNotContain("procurement-return-create", admin.EffectiveProcurementPermissions);
        Assert.DoesNotContain("procurement-return-edit", admin.EffectiveProcurementPermissions);
        Assert.Equal(88, admin.EffectiveProcurementPermissions.Length);
        var disabled = Assert.Single(roles, x => x.RoleKey == "disabled_buyer");
        Assert.Equal(["procurement-access", "procurement-vendor-read"], disabled.Permissions);
        Assert.Empty(disabled.EffectiveProcurementPermissions);
        Assert.Equal(["manager"], Assert.Single(roles, x => x.RoleKey == "hr_only").Permissions);
    }

    [Fact]
    public async Task Updating_role_name_retains_explicit_procurement_actions_without_reintroducing_legacy_seed()
    {
        using var db = CreateContext();
        db.TenantRoleAssignments.AddRange(
            new TenantRoleAssignment { RoleKey = "hr_admin", RoleName = "Administrator", Category = "hrm", PermissionsCsv = "hr_admin", Active = true },
            new TenantRoleAssignment { RoleKey = "custom_operator", RoleName = "Old name", Category = "procurement",
                PermissionsCsv = "procurement-access,procurement-sourcing-publish", Active = true });
        await db.SaveChangesAsync();
        var before = await ProcurementPermissions.ResolveConfiguredActionsAsync(db, "custom_operator", default, TenantId);
        var updated = await CreateService(db).UpdateRoleAsync("custom_operator", new RoleUpdateRequest(RoleName: "New name"), default);
        Assert.Equal(before, updated.Permissions);
        Assert.Equal("procurement-access,procurement-sourcing-publish",
            (await db.TenantRoleAssignments.SingleAsync(x => x.RoleKey == "custom_operator")).PermissionsCsv);
    }

    [Fact]
    public async Task Core_role_admin_can_configure_procurement_actions()
    {
        using var db = CreateContext();
        var svc = CreateService(db);
        db.TenantRoleAssignments.Add(new TenantRoleAssignment
        {
            RoleKey = "hr_admin", RoleName = "HR admin", Category = "system",
            PermissionsCsv = "hr_admin", Active = true,
        });
        await db.SaveChangesAsync();

        var created = await svc.CreateRoleAsync(new RoleCreateRequest("procurement_reviewer",
            "Procurement reviewer", "procurement",
            ["procurement-access", "procurement-vendor-read"]), CancellationToken.None);
        Assert.Equal("procurement", created.Category);
        Assert.Equal(["procurement-access", "procurement-vendor-read"], created.Permissions);

        var updated = await svc.UpdateRoleAsync("procurement_reviewer",
            new RoleUpdateRequest(Permissions: ["procurement-access", "procurement-item-read"]), CancellationToken.None);
        Assert.Contains("procurement-item-read", updated.Permissions);
        Assert.DoesNotContain("procurement-vendor-read", updated.Permissions);
        var invalid = await Assert.ThrowsAsync<DomainException>(() => svc.UpdateRoleAsync(
            "procurement_reviewer", new RoleUpdateRequest(Permissions: ["procurement-access", "hr_admin"]),
            CancellationToken.None));
        Assert.Equal("invalid-permissions", invalid.Code);
    }

    [Fact]
    public async Task Retention_rules_crud()
    {
        using var db = CreateContext();
        var svc = CreateService(db);

        var created = await svc.CreateRetentionRuleAsync(new DataRetentionCreateRequest("payslip", 120), CancellationToken.None);
        Assert.Equal(120, created.RetentionMonths);

        await Assert.ThrowsAsync<DomainException>(() =>
            svc.CreateRetentionRuleAsync(new DataRetentionCreateRequest("PaySlip ", 60), CancellationToken.None));
        // A genuinely different record type must succeed.
        var second = await svc.CreateRetentionRuleAsync(new DataRetentionCreateRequest("timesheet", 60), CancellationToken.None);
        Assert.Equal("timesheet", second.RecordType);

        var updated = await svc.UpdateRetentionRuleAsync(created.Id, new DataRetentionUpdateRequest(RetentionMonths: 132), CancellationToken.None);
        Assert.Equal(132, updated.RetentionMonths);

        await svc.DeleteRetentionRuleAsync(created.Id, CancellationToken.None);
        var list = await svc.ListRetentionRulesAsync(CancellationToken.None);
        Assert.Single(list);
        Assert.Equal("timesheet", list[0].RecordType);
    }

    private sealed class FixedTenantAccessor(string tenantId) : ITenantAccessor
    {
        public string GetTenantId() => tenantId;
    }
    private sealed class MutableTenantAccessor(string tenantId) : ITenantAccessor
    {
        public string TenantId { get; set; } = tenantId;
        public string GetTenantId() => TenantId;
    }
}
