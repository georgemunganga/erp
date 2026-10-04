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
