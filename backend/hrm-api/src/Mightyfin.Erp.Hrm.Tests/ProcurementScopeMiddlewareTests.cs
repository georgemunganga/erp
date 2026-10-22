using System.Security.Claims;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.FileProviders;
using Mightyfin.Erp.Hrm.Api;
using Mightyfin.Erp.Hrm.Domain.Entities;
using Mightyfin.Erp.Hrm.Infrastructure.Data;

namespace Mightyfin.Erp.Hrm.Tests;
public sealed class ProcurementScopeMiddlewareTests
{
    [Fact]
    public void Procurement_actions_require_explicit_module_permissions_on_configured_principals()
    {
        var claims = new[] { new Claim(ClaimTypes.Role, "erp_admin"), new Claim(ProcurementPermissions.ConfiguredClaim, "true") };
        Assert.False(ProcurementPermissions.Allows("procurement-vendor-manage", claims));
        Assert.False(ProcurementPermissions.Allows("procurement-access", claims));
        Assert.True(ProcurementPermissions.Allows("procurement-vendor-read", claims.Concat(new[] {
            new Claim(ProcurementPermissions.ActionClaim, "procurement-access"), new Claim(ProcurementPermissions.ActionClaim, "procurement-vendor-read") })));
    }

    [Fact]
    public async Task Core_branch_assignment_defaults_and_foreign_company_denials_do_not_require_a_worker()
    {
        await using var db = TestDbContextFactory.Create("tenant-a");
        var (user, a, b, branch) = await Seed(db);
        db.UserBranchAssignments.Add(new HrUserBranchAssignment { UserId = user.Id, LocationId = branch.Id }); await db.SaveChangesAsync();
        var (http, scope) = Request(user.Id); var reached = false;
        await Middleware(_ => { reached = true; return Task.CompletedTask; }).InvokeAsync(http, scope, db);
        Assert.True(reached); Assert.Equal(user.Id, scope.PrincipalId); Assert.Null(scope.WorkerId);
        Assert.Equal(a.Id, scope.LegalEntityId); Assert.Equal(branch.Id, scope.WorkLocationId); Assert.True(scope.IsConfined);
        var (denied, deniedScope) = Request(user.Id); denied.Request.Headers["X-Shell-Entity"] = b.Id.ToString();
        reached = false; await Middleware(_ => { reached = true; return Task.CompletedTask; }).InvokeAsync(denied, deniedScope, db);
        Assert.False(reached); Assert.Equal(403, denied.Response.StatusCode);
    }

    [Fact]
    public async Task Optional_employee_home_does_not_grant_company_access_or_override_Core_default()
    {
        await using var db = TestDbContextFactory.Create("tenant-a"); var (user, a, b, _) = await Seed(db);
        var branch = new WorkLocation { Code = "B", Name = "Other home", LegalEntityId = b.Id };
        var worker = new Worker { EmployeeNo = "OPTIONAL", FirstName = "Optional", LastName = "Profile", LocationId = branch.Id, Status = "active" };
        db.WorkLocations.Add(branch); db.Workers.Add(worker); user.WorkerId = worker.Id; await db.SaveChangesAsync();
        var (http, scope) = Request(user.Id); await Middleware(_ => Task.CompletedTask).InvokeAsync(http, scope, db);
        Assert.Equal(a.Id, scope.LegalEntityId); Assert.Null(scope.WorkerId); Assert.Null(scope.WorkLocationId);
        Assert.Equal(user.Id, scope.PrincipalId);
    }

    [Fact]
    public async Task Inactive_HR_profile_does_not_deactivate_the_active_system_account()
    {
        await using var db = TestDbContextFactory.Create("tenant-a"); var (user, _, _, branch) = await Seed(db);
        var worker = new Worker { EmployeeNo = "OLD", FirstName = "Past", LastName = "Employee", LocationId = branch.Id, Status = "terminated" };
        db.Workers.Add(worker); user.WorkerId = worker.Id; await db.SaveChangesAsync();
        var (http, scope) = Request(user.Id); var reached = false;
        await Middleware(_ => { reached = true; return Task.CompletedTask; }).InvokeAsync(http, scope, db);
        Assert.True(reached); Assert.Equal(user.Id, scope.PrincipalId); Assert.Null(scope.WorkerId);
    }

    private static async Task<(LocalUser, LegalEntity, LegalEntity, WorkLocation)> Seed(HrmDbContext db)
    {
        var a = new LegalEntity { Code = "A", RegisteredName = "A", IsDefault = true };
        var b = new LegalEntity { Code = "B", RegisteredName = "B" };
        var branch = new WorkLocation { Code = "A", Name = "A", LegalEntityId = a.Id };
        var user = new LocalUser { Email = "scope@example.test", NormalizedEmail = "SCOPE@EXAMPLE.TEST", DisplayName = "System", PasswordHash = "unused", RolesCsv = "employee" };
        db.LegalEntities.AddRange(a, b); db.WorkLocations.Add(branch); db.LocalUsers.Add(user);
        db.TenantRoleAssignments.Add(new TenantRoleAssignment { RoleKey = "employee", RoleName = "Existing role", Active = true, PermissionsCsv = "procurement-access,procurement-vendor-read" });
        await db.SaveChangesAsync(); return (user, a, b, branch);
    }
    private static (DefaultHttpContext, ProcurementRequestScope) Request(Guid user)
    {
        var http = new DefaultHttpContext(); http.Request.Path = "/api/procurement/v1/context"; http.Response.Body = new MemoryStream();
        http.User = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim("tenant", "tenant-a"), new Claim("sub", user.ToString()), new Claim(ClaimTypes.Role, "employee") }, "test"));
        return (http, new ProcurementRequestScope());
    }
    private static ProcurementScopeMiddleware Middleware(RequestDelegate next) => new(next, new TestEnvironment());
    private sealed class TestEnvironment : IWebHostEnvironment
    {
        public string ApplicationName { get; set; } = "tests"; public string EnvironmentName { get; set; } = "Development";
        public string WebRootPath { get; set; } = ""; public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
        public string ContentRootPath { get; set; } = ""; public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
