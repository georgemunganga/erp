using System.Security.Claims;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.FileProviders;
using Mightyfin.Erp.Hrm.Api;
using Mightyfin.Erp.Hrm.Application;
using Mightyfin.Erp.Hrm.Domain.Entities;

namespace Mightyfin.Erp.Hrm.Tests;

public sealed class ProcurementScopeMiddlewareTests
{
    [Fact]
    public void Procurement_actions_require_an_explicit_allowed_role()
    {
        Assert.False(ProcurementPermissions.Allows("procurement-item-manage",
            [new Claim(ClaimTypes.Role, "employee")]));
        Assert.False(ProcurementPermissions.Allows("procurement-vendor-read",
            [new Claim(ClaimTypes.Role, "tenant_owner")]));
        Assert.False(ProcurementPermissions.Allows("procurement-vendor-manage",
            [new Claim(ClaimTypes.Role, "employee")]));
        Assert.True(ProcurementPermissions.Allows("procurement-item-manage",
            [new Claim(ClaimTypes.Role, "procurement_buyer")]));
        Assert.True(ProcurementPermissions.Allows("procurement-vendor-manage",
            [new Claim(ClaimTypes.Role, "supplier_admin")]));
    }

    [Fact]
    public async Task Confined_user_cannot_select_another_branch_or_company()
    {
        await using var db = TestDbContextFactory.Create("tenant-a");
        var user = Guid.NewGuid();
        var entityA = new LegalEntity { Code = "A", RegisteredName = "Company A", IsDefault = true };
        var entityB = new LegalEntity { Code = "B", RegisteredName = "Company B" };
        db.LegalEntities.AddRange(entityA, entityB);
        var branchA = new WorkLocation { Code = "A1", Name = "Branch A", LegalEntityId = entityA.Id };
        var branchB = new WorkLocation { Code = "B1", Name = "Branch B", LegalEntityId = entityB.Id };
        db.WorkLocations.AddRange(branchA, branchB);
        db.UserBranchAssignments.Add(new HrUserBranchAssignment { UserId = user, LocationId = branchA.Id });
        await db.SaveChangesAsync();

        var (http, scope) = Request(user);
        http.Request.Headers["X-Shell-Location"] = branchB.Id.ToString();
        var reached = false;
        await Middleware(_ => { reached = true; return Task.CompletedTask; })
            .InvokeAsync(http, scope, db);
        Assert.False(reached);
        Assert.Equal(StatusCodes.Status403Forbidden, http.Response.StatusCode);

        var (entityHttp, entityScope) = Request(user);
        entityHttp.Request.Headers["X-Shell-Entity"] = entityB.Id.ToString();
        await Middleware(_ => Task.CompletedTask).InvokeAsync(entityHttp, entityScope, db);
        Assert.Equal(StatusCodes.Status403Forbidden, entityHttp.Response.StatusCode);
    }

    [Fact]
    public async Task Confined_user_defaults_to_assigned_branch_and_rejects_mismatched_headers()
    {
        await using var db = TestDbContextFactory.Create("tenant-a");
        var user = Guid.NewGuid();
        var entityA = new LegalEntity { Code = "A", RegisteredName = "Company A", IsDefault = true };
        var entityB = new LegalEntity { Code = "B", RegisteredName = "Company B" };
        db.LegalEntities.AddRange(entityA, entityB);
        var branchA = new WorkLocation { Code = "A1", Name = "Branch A", LegalEntityId = entityA.Id };
        db.WorkLocations.Add(branchA);
        db.UserBranchAssignments.Add(new HrUserBranchAssignment { UserId = user, LocationId = branchA.Id });
        await db.SaveChangesAsync();

        var (http, scope) = Request(user);
        await Middleware(_ => Task.CompletedTask).InvokeAsync(http, scope, db);
        Assert.Equal(entityA.Id, scope.LegalEntityId);
        Assert.Equal(branchA.Id, scope.WorkLocationId);
        Assert.True(scope.IsConfined);

        var (mismatchHttp, mismatchScope) = Request(user);
        mismatchHttp.Request.Headers["X-Shell-Location"] = branchA.Id.ToString();
        mismatchHttp.Request.Headers["X-Shell-Entity"] = entityB.Id.ToString();
        await Middleware(_ => Task.CompletedTask).InvokeAsync(mismatchHttp, mismatchScope, db);
        Assert.Equal(StatusCodes.Status403Forbidden, mismatchHttp.Response.StatusCode);
    }

    [Fact]
    public async Task Procurement_write_requires_active_worker_in_selected_company()
    {
        await using var db = TestDbContextFactory.Create("tenant-a");
        var entity = new LegalEntity { Code = "A", RegisteredName = "Company A", IsDefault = true };
        db.LegalEntities.Add(entity);
        var branch = new WorkLocation { Code = "A1", Name = "Branch A", LegalEntityId = entity.Id };
        db.WorkLocations.Add(branch);
        var worker = new Worker { EmployeeNo = "EMP-1", FirstName = "Pat", LastName = "Test",
            Status = "active", LocationId = branch.Id };
        db.Workers.Add(worker);
        var user = new LocalUser { Email = "pat@example.test", NormalizedEmail = "PAT@EXAMPLE.TEST",
            DisplayName = "Pat Test", PasswordHash = "test", WorkerId = worker.Id, IsActive = true };
        db.LocalUsers.Add(user);
        await db.SaveChangesAsync();

        var (activeHttp, activeScope) = Request(user.Id);
        activeHttp.Request.Path = "/api/procurement/v1/vendors";
        activeHttp.Request.Method = "POST";
        var reached = false;
        await Middleware(_ => { reached = true; return Task.CompletedTask; })
            .InvokeAsync(activeHttp, activeScope, db);
        Assert.True(reached);

        var otherEntity = new LegalEntity { Code = "B", RegisteredName = "Company B" };
        db.LegalEntities.Add(otherEntity);
        await db.SaveChangesAsync();
        var (otherHttp, otherScope) = Request(user.Id);
        otherHttp.Request.Path = "/api/procurement/v1/vendors";
        otherHttp.Request.Headers["X-Shell-Entity"] = otherEntity.Id.ToString();
        reached = false;
        await Middleware(_ => { reached = true; return Task.CompletedTask; })
            .InvokeAsync(otherHttp, otherScope, db);
        Assert.False(reached);
        Assert.Equal(StatusCodes.Status403Forbidden, otherHttp.Response.StatusCode);

        worker.Status = "terminated";
        await db.SaveChangesAsync();
        var (inactiveHttp, inactiveScope) = Request(user.Id);
        inactiveHttp.Request.Path = "/api/procurement/v1/vendors";
        inactiveHttp.Request.Method = "POST";
        reached = false;
        await Middleware(_ => { reached = true; return Task.CompletedTask; })
            .InvokeAsync(inactiveHttp, inactiveScope, db);
        Assert.False(reached);
        Assert.Equal(StatusCodes.Status403Forbidden, inactiveHttp.Response.StatusCode);
    }

    private static (DefaultHttpContext Http, ProcurementRequestScope Scope) Request(Guid user)
    {
        var http = new DefaultHttpContext();
        http.Request.Path = "/api/procurement/v1/meta";
        http.Response.Body = new MemoryStream();
        http.User = new ClaimsPrincipal(new ClaimsIdentity(
        [
            new Claim("tenant", "tenant-a"),
            new Claim("sub", user.ToString()),
            new Claim(ClaimTypes.Role, "employee"),
        ], "test"));
        return (http, new ProcurementRequestScope());
    }

    private static ProcurementScopeMiddleware Middleware(RequestDelegate next) =>
        new(next, new TestEnvironment());

    private sealed class TestEnvironment : IWebHostEnvironment
    {
        public string ApplicationName { get; set; } = "tests";
        public string EnvironmentName { get; set; } = "Production";
        public string WebRootPath { get; set; } = "";
        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
        public string ContentRootPath { get; set; } = "";
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
