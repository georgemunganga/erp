using Mightyfin.Erp.Hrm.Application;
using Mightyfin.Erp.Hrm.Application.ConfigAndExtras;
using System.Security.Claims;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
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

    [Fact]
    public async Task Explicit_All_read_uses_only_current_Core_assignments_in_selected_company_and_never_authorizes_create()
    {
        await using var db = TestDbContextFactory.Create("tenant-a");
        var (user, company, foreignCompany, first) = await Seed(db);
        var second = new WorkLocation { Code = "A2", Name = "Second", LegalEntityId = company.Id };
        var foreign = new WorkLocation { Code = "B1", Name = "Foreign", LegalEntityId = foreignCompany.Id };
        db.WorkLocations.AddRange(second, foreign);
        db.UserBranchAssignments.AddRange(
            new HrUserBranchAssignment { UserId = user.Id, LocationId = first.Id },
            new HrUserBranchAssignment { UserId = user.Id, LocationId = second.Id });
        await db.SaveChangesAsync();
        var (read, scope) = Request(user.Id);
        read.Request.Path = "/api/procurement/v1/requests";
        read.Request.Headers["X-Shell-Entity"] = company.Id.ToString("D");
        read.Request.Headers["X-Shell-Branch-Scope"] = "all";
        var reached = false;
        await Middleware(_ => { reached = true; return Task.CompletedTask; }).InvokeAsync(read, scope, db);
        Assert.True(reached);
        Assert.True(scope.IsExplicitAllBranches);
        Assert.Null(scope.WorkLocationId);
        Assert.Equal(new[] { first.Id, second.Id }.Order(), scope.PermittedWorkLocationIds.Order());
        Assert.DoesNotContain(foreign.Id, scope.PermittedWorkLocationIds);

        var secondAssignment = await db.UserBranchAssignments.SingleAsync(x => x.UserId == user.Id && x.LocationId == second.Id);
        secondAssignment.IsArchived = true;
        await db.SaveChangesAsync();
        var (afterRevocation, restrictedScope) = Request(user.Id);
        afterRevocation.Request.Path = "/api/procurement/v1/requests";
        afterRevocation.Request.Headers["X-Shell-Entity"] = company.Id.ToString("D");
        afterRevocation.Request.Headers["X-Shell-Branch-Scope"] = "all";
        await Middleware(_ => Task.CompletedTask).InvokeAsync(afterRevocation, restrictedScope, db);
        Assert.Equal(new[] { first.Id }, restrictedScope.PermittedWorkLocationIds);

        var (create, deniedScope) = Request(user.Id);
        create.Request.Path = "/api/procurement/v1/requests";
        create.Request.Method = "POST";
        create.Request.Headers["X-Shell-Entity"] = company.Id.ToString("D");
        create.Request.Headers["X-Shell-Branch-Scope"] = "all";
        reached = false;
        await Middleware(_ => { reached = true; return Task.CompletedTask; }).InvokeAsync(create, deniedScope, db);
        Assert.False(reached);
        Assert.Equal(400, create.Response.StatusCode);

        var (master, masterScope) = Request(user.Id);
        master.Request.Path = $"/api/procurement/v1/vendors/{Guid.NewGuid():D}/contacts";
        master.Request.Method = "POST";
        master.Request.Headers["X-Shell-Entity"] = company.Id.ToString("D");
        master.Request.Headers["X-Shell-Branch-Scope"] = "all";
        reached = false;
        await Middleware(_ => { reached = true; return Task.CompletedTask; }).InvokeAsync(master, masterScope, db);
        Assert.True(reached);

        var (unknown, unknownScope) = Request(user.Id);
        unknown.Request.Path = $"/api/procurement/v1/vendors/{Guid.NewGuid():D}/bank-data";
        unknown.Request.Headers["X-Shell-Entity"] = company.Id.ToString("D");
        unknown.Request.Headers["X-Shell-Branch-Scope"] = "all";
        reached = false;
        await Middleware(_ => { reached = true; return Task.CompletedTask; }).InvokeAsync(unknown, unknownScope, db);
        Assert.False(reached);
        Assert.Equal(400, unknown.Response.StatusCode);
    }

    [Fact]
    public async Task Omitted_branch_selects_unique_authorized_headquarters_or_requires_explicit_choice()
    {
        await using var db = TestDbContextFactory.Create("tenant-a");
        var (user, company, _, first) = await Seed(db);
        var headquarters = new WorkLocation { Code = "HQ", Name = "Configured primary", Type = "headquarters",
            LegalEntityId = company.Id };
        db.WorkLocations.Add(headquarters);
        db.UserBranchAssignments.AddRange(
            new HrUserBranchAssignment { UserId = user.Id, LocationId = first.Id },
            new HrUserBranchAssignment { UserId = user.Id, LocationId = headquarters.Id });
        await db.SaveChangesAsync();
        var (chosen, scope) = Request(user.Id);
        chosen.Request.Headers["X-Shell-Entity"] = company.Id.ToString("D");
        await Middleware(_ => Task.CompletedTask).InvokeAsync(chosen, scope, db);
        Assert.Equal(headquarters.Id, scope.WorkLocationId);

        headquarters.Type = "branch";
        await db.SaveChangesAsync();
        var (ambiguous, unused) = Request(user.Id);
        ambiguous.Request.Headers["X-Shell-Entity"] = company.Id.ToString("D");
        var reached = false;
        await Middleware(_ => { reached = true; return Task.CompletedTask; }).InvokeAsync(ambiguous, unused, db);
        Assert.False(reached);
        Assert.Equal(400, ambiguous.Response.StatusCode);
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
        var http = new DefaultHttpContext(); http.Request.Method = HttpMethods.Get;
        http.Request.Path = "/api/procurement/v1/context"; http.Response.Body = new MemoryStream();
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
