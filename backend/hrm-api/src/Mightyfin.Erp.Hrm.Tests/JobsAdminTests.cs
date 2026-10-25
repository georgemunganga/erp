using Microsoft.EntityFrameworkCore;
using Mightyfin.Erp.Hrm.Application;
using Mightyfin.Erp.Hrm.Api;
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
        var error = await Assert.ThrowsAsync<DomainException>(() => service.UpdateRoleAsync("source_operator",
            new RoleUpdateRequest(Permissions: ["procurement-access", "procurement-sourcing-unregistered"]), default));
        Assert.Equal("invalid-permissions", error.Code);
    }

    [Fact]
    public async Task Role_readback_matches_session_grants_for_current_registry_row_shapes()
    {
        using var db = CreateContext();
        db.TenantRoleAssignments.AddRange(
            new TenantRoleAssignment { RoleKey = "procurement_buyer", RoleName = "Empty buyer", Category = "procurement", PermissionsCsv = "", Active = true },
            new TenantRoleAssignment { RoleKey = "buyer_unknown", RoleName = "Unknown", Category = "procurement", PermissionsCsv = "procurement-unknown", Active = true },
            new TenantRoleAssignment { RoleKey = "buyer_hr_only", RoleName = "HR only", Category = "procurement", PermissionsCsv = "hr_admin", Active = true },
            new TenantRoleAssignment { RoleKey = "manager", RoleName = "Legacy manager", Category = "procurement", PermissionsCsv = "manager", Active = true },
            new TenantRoleAssignment { RoleKey = "custom_operator", RoleName = "Explicit", Category = "procurement",
                PermissionsCsv = "procurement-access,procurement-sourcing-convert,Procurement-po-issue,unknown-action", Active = true },
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
        Assert.Equal(["procurement-access", "procurement-sourcing-convert"],
            Assert.Single(roles, x => x.RoleKey == "custom_operator").Permissions);
        var admin = Assert.Single(roles, x => x.RoleKey == "erp_admin");
        Assert.Equal(["erp_admin"], admin.Permissions); // HRM/system edit projection is unchanged.
        Assert.Equal(67, admin.EffectiveProcurementPermissions.Length); // Deployed P24 catalog, no new return grants.
        var disabled = Assert.Single(roles, x => x.RoleKey == "disabled_buyer");
        Assert.Equal(["procurement-access", "procurement-vendor-read"], disabled.Permissions);
        Assert.Empty(disabled.EffectiveProcurementPermissions);
        Assert.Equal(["manager"], Assert.Single(roles, x => x.RoleKey == "hr_only").Permissions);
    }

    [Fact]
    public async Task Renaming_role_preserves_explicit_actions_without_legacy_seed()
    {
        using var db = CreateContext();
        db.TenantRoleAssignments.AddRange(
            new TenantRoleAssignment { RoleKey = "hr_admin", RoleName = "Administrator", Category = "hrm", PermissionsCsv = "hr_admin", Active = true },
            new TenantRoleAssignment { RoleKey = "custom_operator", RoleName = "Old name", Category = "procurement",
                PermissionsCsv = "procurement-access,procurement-sourcing-convert", Active = true });
        await db.SaveChangesAsync();
        var before = await ProcurementPermissions.ResolveConfiguredActionsAsync(db, "custom_operator", default, TenantId);
        var updated = await CreateService(db).UpdateRoleAsync("custom_operator", new RoleUpdateRequest(RoleName: "New name"), default);
        Assert.Equal(before, updated.Permissions);
        Assert.Equal(before, updated.EffectiveProcurementPermissions);
        Assert.Equal("procurement-access,procurement-sourcing-convert",
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
}
