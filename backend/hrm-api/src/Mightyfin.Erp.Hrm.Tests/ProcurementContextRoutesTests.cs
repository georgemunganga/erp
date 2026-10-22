using Mightyfin.Erp.Hrm.Application;
using Mightyfin.Erp.Hrm.Application.ConfigAndExtras;
using System.Security.Claims;
using System.Text.Json;
using Mightyfin.Erp.Hrm.Api;
using Mightyfin.Erp.Hrm.Domain.Entities;
using Mightyfin.Erp.Hrm.Infrastructure.Data;

namespace Mightyfin.Erp.Hrm.Tests;
public sealed class ProcurementContextRoutesTests
{
    [Fact]
    public async Task Context_returns_real_Core_principal_current_grants_and_no_private_HR_fields()
    {
        await using var db = TestDbContextFactory.Create("tenant-a");
        var (user, company, branch) = await Seed(db);
        var worker = new Worker { EmployeeNo = "OPTIONAL", FirstName = "Optional", LastName = "Employee", LocationId = branch.Id,
            Nrc = "private-nrc", PersonalEmail = "private@example.test", Status = "active" };
        db.Workers.Add(worker); user.WorkerId = worker.Id; await db.SaveChangesAsync();
        var scope = Scope(user.Id, company.Id, branch.Id); scope.AssignWorker(worker.Id);
        var context = await ProcurementContextRoutes.BuildAsync(db, scope, [new Claim(ClaimTypes.Role, "erp_admin")]);
        Assert.NotNull(context); Assert.Equal(user.Id, context.PrincipalId); Assert.Equal(worker.Id, context.WorkerId);
        Assert.Equal(branch.Id, context.Branch?.Id); Assert.False(context.AdminViewOnly); Assert.Single(context.AllowedCompanies);
        Assert.Contains("procurement-vendor-read", context.Permissions); Assert.DoesNotContain("procurement-vendor-approve", context.Permissions);
        var json = JsonSerializer.Serialize(context); Assert.DoesNotContain("private-nrc", json); Assert.DoesNotContain("private@example.test", json);
    }

    [Fact]
    public async Task Context_requires_verified_actual_principal_not_claims_or_employee_identity()
    {
        await using var db = TestDbContextFactory.Create("tenant-a"); var (user, company, branch) = await Seed(db);
        var scope = new ProcurementRequestScope(); scope.Set("tenant-a", company.Id, user.Id.ToString(), branch.Id, null, false, "test");
        Assert.Null(await ProcurementContextRoutes.BuildAsync(db, scope, [new Claim(ClaimTypes.Role, "erp_admin")]));
        scope.AssignPrincipal(user.Id); Assert.NotNull(await ProcurementContextRoutes.BuildAsync(db, scope, []));
        user.IsActive = false; await db.SaveChangesAsync(); Assert.Null(await ProcurementContextRoutes.BuildAsync(db, scope, []));
    }

    [Fact]
    public async Task Context_rechecks_current_role_revocation_and_new_branch_confinement()
    {
        await using var db = TestDbContextFactory.Create("tenant-a"); var (user, company, branch) = await Seed(db);
        var scope = Scope(user.Id, company.Id, branch.Id);
        Assert.NotNull(await ProcurementContextRoutes.BuildAsync(db, scope, []));
        var other = new WorkLocation { Code = "OTHER", Name = "Other", LegalEntityId = company.Id };
        db.WorkLocations.Add(other); db.UserBranchAssignments.Add(new HrUserBranchAssignment { UserId = user.Id, LocationId = other.Id });
        await db.SaveChangesAsync(); Assert.Null(await ProcurementContextRoutes.BuildAsync(db, scope, []));
    }
    private static async Task<(LocalUser, LegalEntity, WorkLocation)> Seed(HrmDbContext db)
    {
        var company = new LegalEntity { Code = "A", RegisteredName = "Company", Currency = "ZMW", IsDefault = true };
        var branch = new WorkLocation { Code = "A", Name = "Branch", LegalEntityId = company.Id };
        var user = new LocalUser { Email = "context@example.test", NormalizedEmail = "CONTEXT@EXAMPLE.TEST", DisplayName = "System user", PasswordHash = "unused", RolesCsv = "system-reader" };
        db.LegalEntities.Add(company); db.WorkLocations.Add(branch); db.LocalUsers.Add(user);
        db.TenantRoleAssignments.Add(new TenantRoleAssignment { RoleKey = "system-reader", RoleName = "Existing Core role", Active = true, PermissionsCsv = "procurement-access,procurement-vendor-read" });
        await db.SaveChangesAsync(); return (user, company, branch);
    }
    private static ProcurementRequestScope Scope(Guid user, Guid company, Guid branch)
    {
        var scope = new ProcurementRequestScope(); scope.Set("tenant-a", company, user.ToString("D"), branch, null, false, "test"); scope.AssignPrincipal(user); return scope;
    }
}
