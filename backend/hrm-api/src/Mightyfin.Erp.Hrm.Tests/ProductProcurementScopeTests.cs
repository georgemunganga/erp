using System.Security.Claims;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Mightyfin.Erp.Hrm.Api;
using Mightyfin.Erp.Hrm.Application.ConfigAndExtras;
using Mightyfin.Erp.Hrm.Domain.Entities;

namespace Mightyfin.Erp.Hrm.Tests;

public sealed class ProductProcurementScopeTests
{
    private sealed class Environment : IWebHostEnvironment
    {
        public string ApplicationName { get; set; } = "tests";
        public string EnvironmentName { get; set; } = "Testing";
        public string WebRootPath { get; set; } = "";
        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
        public string ContentRootPath { get; set; } = "";
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
    private static CoreErpRequestScope Proof(Guid person, Guid company, Guid[] branches, bool all = false,
        string module = "procurement", string[]? actions = null)
    {
        var clock = TimeProvider.System; var now = clock.GetUtcNow(); var stamp = clock.GetTimestamp();
        var identity = new OperationalSubjectObservation("42", "391bdeaa-87b3-44b3-8e64-2110dcc1a564", now.AddMinutes(1), clock, stamp, TimeSpan.FromMinutes(1));
        var license = new CommercialLicenseLease("01arz3ndektsv4rrffq69g5fav", [module], 1, now.AddMinutes(1), clock, stamp, TimeSpan.FromMinutes(1));
        return new(new(company, person, "tenant-a", Guid.NewGuid(), 1, module, ["procurement_manager"],
            actions ?? ["procurement-access"], all, branches, identity, license), all, branches);
    }
    private static DefaultHttpContext Request(CoreErpRequestScope proof, IConfiguration configuration)
    {
        var http = new DefaultHttpContext { RequestServices = new ServiceCollection().AddLogging().AddOptions()
            .AddSingleton(configuration).BuildServiceProvider() };
        http.Request.Method = "GET"; http.Request.Path = "/api/procurement/v1/context";
        http.Response.Body = new MemoryStream();
        http.User = new(new ClaimsIdentity([new("tenant", proof.TenantId), new("sub", proof.PrincipalId.ToString("D")),
            new(ClaimTypes.NameIdentifier, proof.PrincipalId.ToString("D"))], "kuubu-erp"));
        ProductRequestContext.Set(http, proof, (_, _) => Task.FromResult(new CoreProductSessionState(ModuleAccessStatus.Authorized, proof)));
        return http;
    }
    private static IConfiguration Configuration(bool enabled = true) => new ConfigurationBuilder()
        .AddInMemoryCollection(new Dictionary<string, string?> { ["ERP:AuthMode"] = "product", ["ERP:ProductProcurement:Enabled"] = enabled.ToString() }).Build();

    [Fact]
    public async Task Current_product_principal_and_branch_ignore_broad_legacy_roles_and_empty_assignments()
    {
        await using var db = TestDbContextFactory.Create("tenant-a");
        var company = new LegalEntity { Code = "CO", RegisteredName = "Selected company", Currency = "ZMW" };
        var branch = new WorkLocation { Code = "SITE", Name = "Assigned site", LegalEntityId = company.Id };
        var other = new WorkLocation { Code = "OTHER", Name = "Unassigned site", LegalEntityId = company.Id };
        var person = new LocalUser { Email = "product@example.test", NormalizedEmail = "PRODUCT@EXAMPLE.TEST", DisplayName = "Product member", PasswordHash = "unused", RolesCsv = "erp_admin" };
        db.LegalEntities.Add(company); db.WorkLocations.AddRange(branch, other); db.LocalUsers.Add(person);
        db.TenantRoleAssignments.Add(new TenantRoleAssignment { RoleKey = "erp_admin", RoleName = "Legacy administrator", PermissionsCsv = "procurement-access,procurement-vendor-create", Active = true });
        await db.SaveChangesAsync();
        var configuration = Configuration(); var proof = Proof(person.Id, company.Id, [branch.Id]);
        var http = Request(proof, configuration); http.Request.Headers["X-Shell-Location"] = branch.Id.ToString("D");
        var scope = new ProcurementRequestScope(); var reached = false;
        await new ProcurementCorePrincipalRefreshMiddleware(async context =>
        {
            await new ProcurementScopeMiddleware(_ => { reached = true; return Task.CompletedTask; }, new Environment(), configuration).InvokeAsync(context, scope, db);
        }, configuration).InvokeAsync(http, db);
        Assert.True(reached); Assert.True(scope.IsProduct); Assert.True(scope.IsConfined);
        Assert.Equal(company.Id, scope.LegalEntityId); Assert.Equal(branch.Id, scope.WorkLocationId);
        Assert.Equal(branch.Id, Assert.Single(scope.PermittedWorkLocationIds));
        Assert.Null(scope.WorkerId);
        var context = await ProcurementContextRoutes.BuildAsync(db, scope, http.User.Claims);
        Assert.NotNull(context); Assert.Equal(person.Id, context.PrincipalId);
        Assert.Equal(["procurement-access"], context.Permissions);
        Assert.Equal(company.Id, Assert.Single(context.AllowedCompanies).Id);
        Assert.False(ProcurementPermissions.Allows("procurement-vendor-create", http.User.Claims));
        Assert.Equal("erp_admin", person.RolesCsv); Assert.Empty(db.UserBranchAssignments);
    }

    [Theory]
    [InlineData("company")]
    [InlineData("branch")]
    [InlineData("duplicate")]
    [InlineData("mixed")]
    public async Task Forged_or_conflicting_selectors_cannot_reach_a_business_handler(string selector)
    {
        await using var db = TestDbContextFactory.Create("tenant-a");
        var company = new LegalEntity { Code = "CO", RegisteredName = "Selected" };
        var branch = new WorkLocation { Code = "A", Name = "A", LegalEntityId = company.Id };
        var foreign = new WorkLocation { Code = "B", Name = "B", LegalEntityId = company.Id };
        var person = new LocalUser { Email = "product@example.test", NormalizedEmail = "PRODUCT@EXAMPLE.TEST", DisplayName = "Product", PasswordHash = "unused" };
        db.LegalEntities.Add(company); db.WorkLocations.AddRange(branch, foreign); db.LocalUsers.Add(person); await db.SaveChangesAsync();
        var config = Configuration(); var proof = Proof(person.Id, company.Id, [branch.Id]); var http = Request(proof, config);
        http.Request.Headers["X-Shell-Location"] = selector == "branch" ? foreign.Id.ToString("D") : branch.Id.ToString("D");
        if (selector == "company") http.Request.Headers["X-Shell-Entity"] = Guid.NewGuid().ToString("D");
        if (selector == "duplicate") http.Request.Headers["X-Shell-Location"] = new[] { branch.Id.ToString("D"), branch.Id.ToString("D") };
        if (selector == "mixed") http.Request.Headers["X-Shell-Branch-Scope"] = "all";
        var reached = false;
        await new ProcurementCorePrincipalRefreshMiddleware(context => new ProcurementScopeMiddleware(_ =>
            { reached = true; return Task.CompletedTask; }, new Environment(), config).InvokeAsync(context, new(), db), config).InvokeAsync(http, db);
        Assert.False(reached); Assert.Contains(http.Response.StatusCode, new[] { 400, 403 });
    }

    [Fact]
    public async Task Disabled_product_adapter_never_falls_back_to_the_legacy_administrator()
    {
        await using var db = TestDbContextFactory.Create("tenant-a"); var config = Configuration(false);
        var proof = Proof(Guid.NewGuid(), Guid.NewGuid(), [Guid.NewGuid()]); var http = Request(proof, config);
        var reached = false;
        await new ProcurementCorePrincipalRefreshMiddleware(_ => { reached = true; return Task.CompletedTask; }, config).InvokeAsync(http, db);
        Assert.False(reached); Assert.Equal(403, http.Response.StatusCode);
    }
}
