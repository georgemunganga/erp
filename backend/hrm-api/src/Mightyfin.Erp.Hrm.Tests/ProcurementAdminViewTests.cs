using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using System.Security.Claims;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.FileProviders;
using Mightyfin.Erp.Hrm.Api;
using Mightyfin.Erp.Hrm.Domain.Entities;

namespace Mightyfin.Erp.Hrm.Tests;

public sealed class ProcurementAdminViewTests
{
    [Theory]
    [InlineData("/api/procurement/v1/context")]
    [InlineData("/api/procurement/v1/meta")]
    [InlineData("/api/procurement/v1/vendors")]
    public async Task Actual_unmapped_admin_can_read_without_creating_an_employee(string path)
    {
        await using var db = TestDbContextFactory.Create("admin-tenant");
        var company = new LegalEntity { Code="A", RegisteredName="Company", IsDefault=true };
        db.LegalEntities.Add(company);
        var account = Account(); db.LocalUsers.Add(account); await db.SaveChangesAsync();
        var (http, scope) = Request(account.Id, path);
        ProcurementContextResponse? context = null;
        await Middleware(async _ => context = await ProcurementContextRoutes.BuildAsync(db, scope, http.User.Claims))
            .InvokeAsync(http, scope, db);
        Assert.NotNull(context); Assert.True(context.AdminViewOnly); Assert.Null(context.WorkerId);
        Assert.Null(context.Branch); Assert.Null(context.OrgUnit); Assert.Equal(company.Id,context.LegalEntityId);
        Assert.True(scope.AdminViewOnly); Assert.Null(scope.WorkerId);
        Assert.Contains("procurement-vendor-read",context.Permissions);
        Assert.All(context.Permissions,x=>Assert.True(x=="procurement-access" || x.EndsWith("-read") || x=="procurement-receipt-view"));
        Assert.DoesNotContain("procurement-configuration-edit",context.Permissions);
        Assert.DoesNotContain("procurement-authority-configure",context.Permissions);
        Assert.DoesNotContain("procurement-approval-decide",context.Permissions);
        Assert.Empty(db.Workers); Assert.Null(account.WorkerId);
        Assert.False(db.ChangeTracker.HasChanges());
    }

    [Theory]
    [InlineData("POST")]
    [InlineData("PUT")]
    [InlineData("PATCH")]
    [InlineData("DELETE")]
    public async Task Unmapped_admin_never_reaches_a_business_command(string method)
    {
        await using var db = TestDbContextFactory.Create("admin-tenant");
        db.LegalEntities.Add(new LegalEntity {Code="A",RegisteredName="Company",IsDefault=true});
        var account=Account(); db.LocalUsers.Add(account);await db.SaveChangesAsync();
        var (http,scope)=Request(account.Id,"/api/procurement/v1/vendors");http.Request.Method=method;
        var reached=false;await Middleware(_=>{reached=true;return Task.CompletedTask;}).InvokeAsync(http,scope,db);
        Assert.False(reached);Assert.Equal(403,http.Response.StatusCode);Assert.False(scope.AdminViewOnly);
        Assert.Empty(db.Workers);Assert.False(db.ChangeTracker.HasChanges());
    }

    [Theory]
    [InlineData("stored-role")]
    [InlineData("claim-role")]
    [InlineData("inactive")]
    [InlineData("archived")]
    [InlineData("locked")]
    [InlineData("other-tenant")]
    public async Task Read_requires_current_same_tenant_active_account_and_both_admin_grants(string failure)
    {
        await using var db=TestDbContextFactory.Create("admin-tenant");
        db.LegalEntities.Add(new LegalEntity {Code="A",RegisteredName="Company",IsDefault=true});
        var account=Account();
        if(failure=="stored-role")account.RolesCsv="hr_admin";
        if(failure=="inactive")account.IsActive=false;
        if(failure=="archived")account.IsArchived=true;
        if(failure=="locked")account.LockedUntil=DateTimeOffset.UtcNow.AddHours(1);
        db.LocalUsers.Add(account);await db.SaveChangesAsync();
        var (http,scope)=Request(account.Id,"/api/procurement/v1/context",failure=="claim-role"?"hr_admin":"erp_admin");
        if(failure=="other-tenant")http.User=new ClaimsPrincipal(new ClaimsIdentity(new[]{new Claim("tenant","foreign"),
            new Claim("sub",account.Id.ToString()),new Claim(ClaimTypes.Role,"erp_admin")},"test"));
        var reached=false;await Middleware(_=>{reached=true;return Task.CompletedTask;}).InvokeAsync(http,scope,db);
        Assert.False(reached);Assert.Equal(403,http.Response.StatusCode);Assert.False(scope.AdminViewOnly);
        Assert.Empty(db.Workers);Assert.False(db.ChangeTracker.HasChanges());
    }

    [Theory]
    [InlineData("foreign-company")]
    [InlineData("unassigned-branch")]
    [InlineData("company-branch-mismatch")]
    public async Task Admin_view_preserves_company_and_branch_boundaries(string failure)
    {
        await using var db=TestDbContextFactory.Create("admin-tenant");
        var a=new LegalEntity {Code="A",RegisteredName="A",IsDefault=true};
        var b=new LegalEntity {Code="B",RegisteredName="B"};db.LegalEntities.AddRange(a,b);
        var ba=new WorkLocation {Code="A1",Name="A1",LegalEntityId=a.Id};
        var bb=new WorkLocation {Code="B1",Name="B1",LegalEntityId=b.Id};db.WorkLocations.AddRange(ba,bb);
        var account=Account();db.LocalUsers.Add(account);
        if(failure=="unassigned-branch")db.UserBranchAssignments.Add(new HrUserBranchAssignment {UserId=account.Id,LocationId=ba.Id});
        await db.SaveChangesAsync();
        var (http,scope)=Request(account.Id,"/api/procurement/v1/context");
        http.Request.Headers["X-Shell-Entity"]=(failure=="foreign-company"?Guid.NewGuid():a.Id).ToString();
        if(failure!="foreign-company")http.Request.Headers["X-Shell-Location"]=bb.Id.ToString();
        var reached=false;await Middleware(_=>{reached=true;return Task.CompletedTask;}).InvokeAsync(http,scope,db);
        Assert.False(reached);Assert.Equal(403,http.Response.StatusCode);Assert.False(db.ChangeTracker.HasChanges());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Inactive_or_wrong_company_mapped_admin_cannot_fall_back_to_view_mode(bool inactive)
    {
        await using var db=TestDbContextFactory.Create("admin-tenant");
        var a=new LegalEntity {Code="A",RegisteredName="A",IsDefault=true};
        var b=new LegalEntity {Code="B",RegisteredName="B"};db.LegalEntities.AddRange(a,b);
        var branch=new WorkLocation {Code="B1",Name="B1",LegalEntityId=b.Id};db.WorkLocations.Add(branch);
        var worker=new Worker {EmployeeNo="W",FirstName="Mapped",LastName="Admin",LocationId=branch.Id,Status=inactive?"terminated":"active"};db.Workers.Add(worker);
        var account=Account();account.WorkerId=worker.Id;db.LocalUsers.Add(account);await db.SaveChangesAsync();
        var (http,scope)=Request(account.Id,"/api/procurement/v1/context");http.Request.Headers["X-Shell-Entity"]=a.Id.ToString();
        var reached=false;await Middleware(_=>{reached=true;return Task.CompletedTask;}).InvokeAsync(http,scope,db);
        Assert.False(reached);Assert.Equal(403,http.Response.StatusCode);Assert.False(scope.AdminViewOnly);
    }

    [Fact]
    public async Task Context_rechecks_admin_account_after_middleware_resolution()
    {
        await using var db=TestDbContextFactory.Create("admin-tenant");
        var company=new LegalEntity {Code="A",RegisteredName="A",IsDefault=true};db.LegalEntities.Add(company);
        var account=Account();db.LocalUsers.Add(account);await db.SaveChangesAsync();
        var (http,scope)=Request(account.Id,"/api/procurement/v1/context");
        await Middleware(_=>Task.CompletedTask).InvokeAsync(http,scope,db);Assert.True(scope.AdminViewOnly);
        account.IsActive=false;await db.SaveChangesAsync();
        Assert.Null(await ProcurementContextRoutes.BuildAsync(db,scope,http.User.Claims));
    }

    [Fact]
    public async Task Admin_context_rechecks_new_branch_confinement_without_broadening_scope()
    {
        await using var db=TestDbContextFactory.Create("admin-tenant");
        var company=new LegalEntity {Code="A",RegisteredName="A",IsDefault=true};db.LegalEntities.Add(company);
        var a=new WorkLocation {Code="A1",Name="A1",LegalEntityId=company.Id};
        var b=new WorkLocation {Code="A2",Name="A2",LegalEntityId=company.Id};db.WorkLocations.AddRange(a,b);
        var account=Account();db.LocalUsers.Add(account);await db.SaveChangesAsync();
        var (http,scope)=Request(account.Id,"/api/procurement/v1/context");http.Request.Headers["X-Shell-Location"]=a.Id.ToString();
        await Middleware(_=>Task.CompletedTask).InvokeAsync(http,scope,db);Assert.True(scope.AdminViewOnly);
        Assert.Equal(a.Id,(await ProcurementContextRoutes.BuildAsync(db,scope,http.User.Claims))!.Branch!.Id);
        db.UserBranchAssignments.Add(new HrUserBranchAssignment {UserId=account.Id,LocationId=b.Id});await db.SaveChangesAsync();
        Assert.Null(await ProcurementContextRoutes.BuildAsync(db,scope,http.User.Claims));
        Assert.False(await ProcurementWorkforce.IsUnmappedErpAdministrator(db,"different-tenant",account.Id.ToString(),http.User.Claims,default));
    }

    [Fact]
    public async Task Mapped_context_returns_nullable_worker_and_mapped_command_stays_denied()
    {
        await using var db=TestDbContextFactory.Create("admin-tenant");
        db.LegalEntities.Add(new LegalEntity {Code="A",RegisteredName="A",IsDefault=true});
        var account=Account();db.LocalUsers.Add(account);await db.SaveChangesAsync();
        var builder=WebApplication.CreateBuilder(new WebApplicationOptions {EnvironmentName="Production"});
        builder.WebHost.UseTestServer();builder.Services.AddSingleton(db);
        builder.Services.AddScoped<ProcurementRequestScope>();builder.Services.AddAuthorization(ProcurementPermissions.AddPolicies);
        await using var app=builder.Build();app.UseRouting();
        app.Use(async (http,next)=>{
            http.User=new ClaimsPrincipal(new ClaimsIdentity(new[]{new Claim("tenant","admin-tenant"),
                new Claim("sub",account.Id.ToString()),new Claim(ClaimTypes.Role,"erp_admin")},"synthetic-test"));
            await next(http);
        });
        app.UseAuthorization();app.UseMiddleware<ProcurementScopeMiddleware>();ProcurementContextRoutes.MapRoutes(app);
        var writes=0;app.MapPost("/api/procurement/v1/admin-test-command",()=>{writes++;return Results.Ok();})
            .RequireAuthorization("procurement-vendor-manage");
        await app.StartAsync();var client=app.GetTestClient();
        var response=await client.GetAsync("/api/procurement/v1/context");Assert.Equal(HttpStatusCode.OK,response.StatusCode);
        var body=await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(JsonValueKind.Null,body.GetProperty("workerId").ValueKind);
        Assert.True(body.GetProperty("adminViewOnly").GetBoolean());
        Assert.DoesNotContain(body.GetProperty("permissions").EnumerateArray(),x=>x.GetString()=="procurement-vendor-manage");
        Assert.Equal(HttpStatusCode.Forbidden,(await client.PostAsJsonAsync("/api/procurement/v1/admin-test-command",new {})).StatusCode);
        Assert.Equal(0,writes);Assert.Empty(db.Workers);Assert.False(db.ChangeTracker.HasChanges());
    }

    private static LocalUser Account()=>new() {Email="admin@example.test",NormalizedEmail="ADMIN@EXAMPLE.TEST",
        DisplayName="ERP administrator",PasswordHash="synthetic",IsActive=true,RolesCsv="erp_admin,hr_admin"};
    private static (DefaultHttpContext,ProcurementRequestScope) Request(Guid user,string path,string role="erp_admin")
    {
        var http=new DefaultHttpContext();http.Request.Path=path;http.Request.Method="GET";http.Response.Body=new MemoryStream();
        http.User=new ClaimsPrincipal(new ClaimsIdentity(new[]{new Claim("tenant","admin-tenant"),new Claim("sub",user.ToString()),new Claim(ClaimTypes.Role,role)},"test"));
        return(http,new ProcurementRequestScope());
    }
    private static ProcurementScopeMiddleware Middleware(RequestDelegate next)=>new(next,new Environment());
    private sealed class Environment:IWebHostEnvironment
    {
        public string ApplicationName {get;set;}="tests";public string EnvironmentName {get;set;}="Production";
        public string WebRootPath {get;set;}="";public IFileProvider WebRootFileProvider {get;set;}=new NullFileProvider();
        public string ContentRootPath {get;set;}="";public IFileProvider ContentRootFileProvider {get;set;}=new NullFileProvider();
    }
}
