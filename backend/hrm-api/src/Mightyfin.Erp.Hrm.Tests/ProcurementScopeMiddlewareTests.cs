using Mightyfin.Erp.Hrm.Application;
using Mightyfin.Erp.Hrm.Application.ConfigAndExtras;
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

    [Theory]
    [InlineData("/api/procurement/v1/context", true)]
    [InlineData("/api/procurement/v1/meta", true)]
    [InlineData("/api/procurement/v1/vendors", true)]
    [InlineData("/api/procurement/v1/vendors/export", true)]
    [InlineData("/api/procurement/v1/items", true)]
    [InlineData("/api/procurement/v1/items/export", true)]
    [InlineData("/api/procurement/v1/orders", false)]
    [InlineData("/api/procurement/v1/vendors/random-child", false)]
    public void All_scope_has_an_exact_company_master_read_allowlist(string path, bool allowed)
    {
        Assert.Equal(allowed, ProcurementScopeMiddleware.AllCompanyMasterReadPath(path, "GET"));
        Assert.False(ProcurementScopeMiddleware.AllCompanyMasterReadPath(path, "POST"));
    }

    [Fact]
    public async Task Explicit_All_uses_current_Core_branch_intersection_without_defaulting_to_first_branch()
    {
        await using var db = TestDbContextFactory.Create("tenant-a");
        var (user, company, other, first) = await Seed(db);
        var second = new WorkLocation { Code = "A2", Name = "Other permitted", LegalEntityId = company.Id };
        var foreign = new WorkLocation { Code = "B2", Name = "Foreign", LegalEntityId = other.Id };
        db.WorkLocations.AddRange(second, foreign);
        db.UserBranchAssignments.AddRange(new HrUserBranchAssignment { UserId = user.Id, LocationId = first.Id },
            new HrUserBranchAssignment { UserId = user.Id, LocationId = second.Id });
        await db.SaveChangesAsync();
        var (http, scope) = Request(user.Id);
        http.Request.Path = "/api/procurement/v1/vendors/export";
        http.Request.Headers["X-Shell-Entity"] = company.Id.ToString();
        http.Request.Headers["X-Shell-Branch-Scope"] = "all";
        var reached = false;
        await Middleware(_ => { reached = true; return Task.CompletedTask; }).InvokeAsync(http, scope, db);
        Assert.True(reached);
        Assert.True(scope.IsExplicitAllBranches);
        Assert.Null(scope.WorkLocationId);
        Assert.Equal(new[] { first.Id, second.Id }.OrderBy(x => x), scope.PermittedWorkLocationIds.OrderBy(x => x));
        Assert.DoesNotContain(foreign.Id, scope.PermittedWorkLocationIds);
        Assert.NotNull(await ProcurementContextRoutes.BuildAsync(db, scope, http.User.Claims));

        var (badCompany, badScope) = Request(user.Id);
        badCompany.Request.Headers["X-Shell-Entity"] = other.Id.ToString();
        badCompany.Request.Headers["X-Shell-Branch-Scope"] = "all";
        await Middleware(_ => throw new Exception("Foreign company reached route")).InvokeAsync(badCompany, badScope, db);
        Assert.Equal(403, badCompany.Response.StatusCode);
    }

    [Theory]
    [InlineData("/api/procurement/v1/vendors", "POST")]
    [InlineData("/api/procurement/v1/items", "POST")]
    public async Task Valid_All_on_existing_operational_routes_keeps_the_original_concrete_branch(
        string path, string method)
    {
        await using var db = TestDbContextFactory.Create("tenant-a");
        var (user, company, _, branch) = await Seed(db);
        db.UserBranchAssignments.Add(new HrUserBranchAssignment { UserId = user.Id, LocationId = branch.Id });
        await db.SaveChangesAsync();
        var (http, scope) = Request(user.Id);
        http.Request.Path = path;
        http.Request.Method = method;
        http.Request.Headers["X-Shell-Entity"] = company.Id.ToString();
        http.Request.Headers["X-Shell-Branch-Scope"] = "all";
        var reached = false;
        await Middleware(_ => { reached = true; return Task.CompletedTask; }).InvokeAsync(http, scope, db);
        Assert.True(reached);
        Assert.False(scope.IsExplicitAllBranches);
        Assert.Equal(branch.Id, scope.WorkLocationId);
        Assert.Equal(company.Id, scope.LegalEntityId);
        Assert.Empty(scope.PermittedWorkLocationIds);
    }

    [Theory]
    [InlineData("wrong", false)]
    [InlineData("all", true)]
    public async Task Invalid_or_coexisting_All_headers_are_rejected_before_route_execution(string header, bool addLocation)
    {
        await using var db = TestDbContextFactory.Create("tenant-a");
        var (user, company, _, branch) = await Seed(db);
        var (http, scope) = Request(user.Id);
        http.Request.Headers["X-Shell-Entity"] = company.Id.ToString();
        http.Request.Headers["X-Shell-Branch-Scope"] = header;
        if (addLocation) http.Request.Headers["X-Shell-Location"] = branch.Id.ToString();
        await Middleware(_ => throw new Exception("Invalid header reached route")).InvokeAsync(http, scope, db);
        Assert.Equal(400, http.Response.StatusCode);
    }

    [Theory]
    [InlineData("/api/procurement/v1/requests", true)]
    [InlineData("/api/procurement/v1/requests/", true)]
    [InlineData("/api/procurement/v1/plans", true)]
    [InlineData("/api/procurement/v1/contracts", true)]
    [InlineData("/api/procurement/v1/orders/principal-direct-drafts", true)]
    [InlineData("/api/procurement/v1/orders/principal-direct-drafts/", true)]
    [InlineData("/api/procurement/v1/orders", false)]
    [InlineData("/api/procurement/v1/orders/approved-principal-direct-sources", false)]
    [InlineData("/api/procurement/v1/orders/00000000-0000-0000-0000-000000000001/principal-direct-draft", false)]
    [InlineData("/api/procurement/v1/requests/random-child", false)]
    [InlineData("/api/procurement/v1/requestss", false)]
    public void All_operational_reads_are_exact_collections_not_commands_or_children(string path, bool allowed)
    {
        Assert.Equal(allowed, ProcurementScopeMiddleware.AllOperationalListReadPath(path,"GET"));
        Assert.False(ProcurementScopeMiddleware.AllOperationalListReadPath(path,"POST"));
    }

    [Theory]
    [InlineData("/api/procurement/v1/requests")]
    [InlineData("/api/procurement/v1/plans")]
    [InlineData("/api/procurement/v1/contracts")]
    [InlineData("/api/procurement/v1/orders/principal-direct-drafts")]
    public async Task Reviewed_All_collections_do_not_default_to_an_assigned_branch(string path)
    {
        await using var db=TestDbContextFactory.Create("tenant-a");var (user,company,_,branch)=await Seed(db);
        db.UserBranchAssignments.Add(new HrUserBranchAssignment { UserId=user.Id, LocationId=branch.Id });await db.SaveChangesAsync();
        var (http,scope)=Request(user.Id);http.Request.Path=path;
        http.Request.Headers["X-Shell-Entity"]=company.Id.ToString();http.Request.Headers["X-Shell-Branch-Scope"]="all";
        var reached=false;await Middleware(_=>{reached=true;return Task.CompletedTask;}).InvokeAsync(http,scope,db);
        Assert.True(reached);Assert.True(scope.IsExplicitAllBranches);Assert.Null(scope.WorkLocationId);
        Assert.Equal(new[]{branch.Id},scope.PermittedWorkLocationIds);
    }

    [Fact]
    public async Task Principal_direct_Create_rejects_All_before_route_but_selected_branch_reaches_route()
    {
        await using var db=TestDbContextFactory.Create("tenant-a");
        var (user,company,_,branch)=await Seed(db);
        db.UserBranchAssignments.Add(new HrUserBranchAssignment { UserId=user.Id, LocationId=branch.Id });
        await db.SaveChangesAsync();
        const string path="/api/procurement/v1/orders/from-approved-principal-direct-sources";
        Assert.True(ProcurementScopeMiddleware.RequestWriteRequiresBranch(path,"POST"));
        Assert.False(ProcurementScopeMiddleware.RequestWriteRequiresBranch(path,"GET"));
        Assert.False(ProcurementScopeMiddleware.RequestWriteRequiresBranch("/api/procurement/v1/orders/from-approved-principal-direct-sourcess","POST"));
        var (all,allScope)=Request(user.Id);all.Request.Path=path;all.Request.Method="POST";
        all.Request.Headers["X-Shell-Entity"]=company.Id.ToString();all.Request.Headers["X-Shell-Branch-Scope"]="all";
        await Middleware(_=>throw new Exception("All Create reached route")).InvokeAsync(all,allScope,db);
        Assert.Equal(400,all.Response.StatusCode);

        var (selected,selectedScope)=Request(user.Id);selected.Request.Path=path;selected.Request.Method="POST";
        selected.Request.Headers["X-Shell-Entity"]=company.Id.ToString();
        selected.Request.Headers["X-Shell-Location"]=branch.Id.ToString();
        var reached=false;
        await Middleware(_=>{reached=true;return Task.CompletedTask;}).InvokeAsync(selected,selectedScope,db);
        Assert.True(reached);Assert.False(selectedScope.IsExplicitAllBranches);
        Assert.Equal(branch.Id,selectedScope.WorkLocationId);
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
    [Theory]
    [InlineData("GET")]
    [InlineData("POST")]
    public async Task Existing_manual_payment_company_scope_keeps_current_permitted_branches(string method)
    {
        // Scope-only fixture. Real payment routes additionally enforce their
        // named permissions, current policy, authority and business-state guards.
        await using var db = TestDbContextFactory.Create("tenant-a");
        var (user, company, _, branch) = await Seed(db);
        db.UserBranchAssignments.Add(new HrUserBranchAssignment { UserId = user.Id, LocationId = branch.Id });
        await db.SaveChangesAsync();
        var (http, scope) = Request(user.Id);
        http.Request.Path = "/api/procurement/v1/manual-payments";
        http.Request.Method = method;
        http.Request.Headers["X-Shell-Entity"] = company.Id.ToString();
        http.Request.Headers["X-Shell-Branch-Scope"] = "all";
        var reached = false;
        await Middleware(_ => { reached = true; return Task.CompletedTask; }).InvokeAsync(http, scope, db);
        Assert.True(reached);
        Assert.True(scope.IsExplicitAllBranches);
        Assert.Null(scope.WorkLocationId);
        Assert.Equal(branch.Id, Assert.Single(scope.PermittedWorkLocationIds));
    }

    private static (DefaultHttpContext, ProcurementRequestScope) Request(Guid user)
    {
        var http = new DefaultHttpContext(); http.Request.Path = "/api/procurement/v1/context"; http.Response.Body = new MemoryStream();
        http.Request.Method = HttpMethods.Get;
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
