using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Reflection;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Mightyfin.Erp.Hrm.Api;
using Mightyfin.Erp.Hrm.Application;
using Mightyfin.Erp.Hrm.Application.Time;
using Mightyfin.Erp.Hrm.Domain.Entities;
using Mightyfin.Erp.Hrm.Infrastructure.Data;

namespace Mightyfin.Erp.Hrm.Tests;

public sealed class OnPremMobileHttpTests
{
    private const string Origin="https://erp.example.test";
    private const string Password="Isolated-test-password-2026";
    [Fact]
    public async Task Disabled_installations_expose_no_mobile_routes()
    {
        await using var f=await Fixture.Start(false);
        Assert.Equal(HttpStatusCode.NotFound,(await f.Client.GetAsync("/mobile/v1/profile")).StatusCode);
    }
    [Fact]
    public async Task Directory_is_explicit_minimal_and_private_companies_require_exact_code()
    {
        await using var f=await Fixture.Start();
        Assert.Equal(HttpStatusCode.BadRequest,(await f.Client.GetAsync("/mobile/v1/companies?query=A")).StatusCode);
        var a=await f.Client.GetFromJsonAsync<JsonElement>("/mobile/v1/companies?query=Public");
        var c=Assert.Single(a.GetProperty("items").EnumerateArray());Assert.Equal(4,c.EnumerateObject().Count());
        Assert.Equal(f.A,c.GetProperty("id").GetGuid());
        var b=await f.Client.GetFromJsonAsync<JsonElement>("/mobile/v1/companies?query=Private");Assert.Empty(b.GetProperty("items").EnumerateArray());
        b=await f.Client.GetFromJsonAsync<JsonElement>("/mobile/v1/companies?query=PRIVATE-B");Assert.Single(b.GetProperty("items").EnumerateArray());
        var manifest=await f.Client.GetFromJsonAsync<JsonElement>("/mobile/v1/profile");
        Assert.Equal(Origin,manifest.GetProperty("api_origin").GetString());Assert.False(manifest.GetProperty("central_registration").GetBoolean());
    }
    [Fact]
    public async Task Real_login_punches_retries_and_audit_preserve_existing_browser_sessions()
    {
        await using var f=await Fixture.Start();
        var browser=await f.Client.PostAsJsonAsync("/api/hrm/auth/login",new{email="qa-a@example.invalid",password=Password});
        Assert.Equal(HttpStatusCode.OK,browser.StatusCode);
        var cookie=browser.Headers.GetValues("Set-Cookie").First().Split(';')[0];
        var token=await f.Login();Assert.False(token.TryGetProperty("user",out _));
        var view=await f.Send("GET","/me",token);Assert.Equal(f.WorkerA,view.GetProperty("employee").GetProperty("id").GetGuid());
        Assert.Empty(view.GetProperty("attendance").GetProperty("records").EnumerateArray());
        var date=view.GetProperty("attendance").GetProperty("work_date").GetString();
        view=await f.Send("POST","/attendance/clock-in",token,new{expected_work_date=date});
        var row=Assert.Single(view.GetProperty("attendance").GetProperty("records").EnumerateArray());
        var first=row.GetProperty("check_in").GetString();var id=row.GetProperty("id").GetGuid();
        f.Clock.Value=f.Clock.Value.AddMinutes(4);
        view=await f.Send("POST","/attendance/clock-in",token,new{expected_work_date=date});
        Assert.Equal(first,Assert.Single(view.GetProperty("attendance").GetProperty("records").EnumerateArray()).GetProperty("check_in").GetString());
        view=await f.Send("POST","/attendance/clock-out",token,new{expected_record_id=id});
        var last=Assert.Single(view.GetProperty("attendance").GetProperty("records").EnumerateArray()).GetProperty("check_out").GetString();
        f.Clock.Value=f.Clock.Value.AddMinutes(1);
        view=await f.Send("POST","/attendance/clock-out",token,new{expected_record_id=id});
        Assert.Equal(last,Assert.Single(view.GetProperty("attendance").GetProperty("records").EnumerateArray()).GetProperty("check_out").GetString());
        await using var db=f.Db("tenant-a");
        Assert.Single(await db.AttendanceRecords.ToArrayAsync());Assert.Equal(2,await db.AuditEntries.CountAsync(x=>x.Action=="mobile-clock-in"||x.Action=="mobile-clock-out"));
        Assert.Single(await db.LocalSessions.ToArrayAsync());
        using var logout=await f.Client.PostAsJsonAsync("/mobile/v1/auth/sign-out",new{companyId=f.A,refreshToken=token.GetProperty("refreshToken").GetString()});Assert.Equal(HttpStatusCode.NoContent,logout.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized,(await f.Raw("GET","/me",token)).StatusCode);
        // SQLite cannot translate the pre-existing browser handler's DateTimeOffset
        // query. Check its persisted opaque credential unchanged here; run that
        // handler itself in the separate PostgreSQL deployment qualification.
        var webSession=Assert.Single(await db.LocalSessions.AsNoTracking().ToArrayAsync());
        Assert.Null(webSession.RevokedAt);Assert.Equal(LocalSessionTokens.Hash(cookie.Split('=',2)[1]),webSession.TokenHash);
    }
    [Fact]
    public async Task No_cookie_federated_identity_cross_company_or_caller_selected_worker_authority()
    {
        await using var f=await Fixture.Start();var token=await f.Login();
        Assert.Equal(HttpStatusCode.Unauthorized,(await f.Raw("GET","/me",token,company:f.B)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest,(await f.Raw("POST","/attendance/clock-in",token,new{expected_work_date="2026-11-17",workerId=f.WorkerB})).StatusCode);
        using var request=new HttpRequestMessage(HttpMethod.Get,"/mobile/v1/me");request.Headers.Add("Cookie","hrm_session="+token.GetProperty("accessToken").GetString());
        Assert.Equal(HttpStatusCode.BadRequest,(await f.Client.SendAsync(request)).StatusCode);
        using var altered=new HttpRequestMessage(HttpMethod.Get,"/mobile/v1/me");altered.Headers.Add("X-Tenant","tenant-b");
        Assert.Equal(HttpStatusCode.BadRequest,(await f.Client.SendAsync(altered)).StatusCode);
        var denied=await f.Client.PostAsJsonAsync("/mobile/v1/auth/sign-in",new{companyId=f.B,identifier="qa-a@example.invalid",password=Password});Assert.Equal(HttpStatusCode.Unauthorized,denied.StatusCode);
        await using var db=f.Db("tenant-a");var account=await db.LocalUsers.SingleAsync();account.WorkerId=null;await db.SaveChangesAsync();
        Assert.Equal(HttpStatusCode.Unauthorized,(await f.Raw("GET","/me",token)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized,(await f.Client.PostAsJsonAsync("/mobile/v1/auth/sign-in",new{companyId=f.A,identifier="qa-a@example.invalid",password=Password})).StatusCode);
    }
    [Fact]
    public async Task Refresh_rotates_and_password_changes_revoke_mobile_access_without_changing_web_sessions()
    {
        await using var f=await Fixture.Start();var old=await f.Login();
        var r=new{companyId=f.A,refreshToken=old.GetProperty("refreshToken").GetString()};
        using var response=await f.Client.PostAsJsonAsync("/mobile/v1/auth/refresh",r);Assert.Equal(HttpStatusCode.OK,response.StatusCode);
        var fresh=await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(HttpStatusCode.Unauthorized,(await f.Client.PostAsJsonAsync("/mobile/v1/auth/refresh",r)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized,(await f.Raw("GET","/me",old)).StatusCode);
        Assert.Equal(HttpStatusCode.OK,(await f.Raw("GET","/me",fresh)).StatusCode);
        await using var db=f.Db("tenant-a");var user=await db.LocalUsers.SingleAsync();user.PasswordHash=LocalPasswordHash.Hash("Changed-isolated-password-2026");await db.SaveChangesAsync();
        Assert.Equal(HttpStatusCode.Unauthorized,(await f.Raw("GET","/me",fresh)).StatusCode);
    }
    [Fact]
    public async Task Active_account_and_branch_worker_eligibility_are_rechecked()
    {
        await using var f=await Fixture.Start();var token=await f.Login();
        await using var db=f.Db("tenant-a");var user=await db.LocalUsers.SingleAsync();user.MustChangePassword=true;await db.SaveChangesAsync();
        Assert.Equal(HttpStatusCode.Unauthorized,(await f.Raw("GET","/me",token)).StatusCode);
        user.MustChangePassword=false;db.UserBranchAssignments.Add(new HrUserBranchAssignment{UserId=user.Id,LocationId=Guid.NewGuid()});await db.SaveChangesAsync();
        Assert.Equal(HttpStatusCode.Unauthorized,(await f.Raw("GET","/me",token)).StatusCode);
    }
    [Theory]
    [InlineData("inactive-account")][InlineData("terminated-worker")][InlineData("archived-worker")]
    [InlineData("future-employment")][InlineData("expired-employment")][InlineData("different-worker-link")][InlineData("missing-employee-role")]
    public async Task Eligibility_changes_deny_existing_mobile_sessions_and_new_password_signin(string change)
    {
        await using var f=await Fixture.Start();var token=await f.Login();
        await using var db=f.Db("tenant-a");var user=await db.LocalUsers.SingleAsync();var worker=await db.Workers.SingleAsync();
        switch(change){
            case "inactive-account":user.IsActive=false;break;
            case "terminated-worker":worker.Status="terminated";break;
            case "archived-worker":worker.IsArchived=true;break;
            case "future-employment":worker.StartDate=new(2027,1,1);break;
            case "expired-employment":worker.EndDate=new(2026,1,1);break;
            case "different-worker-link":worker.SubjectId=Guid.NewGuid().ToString("D");break;
            case "missing-employee-role":user.RolesCsv="hr_admin";break;
        }
        await db.SaveChangesAsync();
        Assert.Equal(HttpStatusCode.Unauthorized,(await f.Raw("GET","/me",token)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized,(await f.Client.PostAsJsonAsync("/mobile/v1/auth/sign-in",new{companyId=f.A,identifier="qa-a@example.invalid",password=Password})).StatusCode);
    }
    [Fact]
    public async Task Wrong_mobile_password_cannot_lock_out_or_change_an_existing_ERP_account()
    {
        await using var f=await Fixture.Start();
        for(var n=0;n<5;n++)Assert.Equal(HttpStatusCode.Unauthorized,(await f.Client.PostAsJsonAsync("/mobile/v1/auth/sign-in",new{companyId=f.A,identifier="qa-a@example.invalid",password="wrong-isolated-password"})).StatusCode);
        Assert.Equal(HttpStatusCode.TooManyRequests,(await f.Client.PostAsJsonAsync("/mobile/v1/auth/sign-in",new{companyId=f.A,identifier="qa-a@example.invalid",password=Password})).StatusCode);
        await using var db=f.Db("tenant-a");var user=await db.LocalUsers.SingleAsync();Assert.Equal(0,user.FailedLoginCount);Assert.Null(user.LockedUntil);
        Assert.True(LocalPasswordHash.Verify(Password,user.PasswordHash));
        Assert.Equal(HttpStatusCode.OK,(await f.Client.PostAsJsonAsync("/api/hrm/auth/login",new{email="qa-a@example.invalid",password=Password})).StatusCode);
    }
    [Theory]
    [InlineData("product")][InlineData("hybrid")][InlineData("oidc")]
    public void Standalone_feature_cannot_replace_other_authentication_profiles(string mode)
    {
        var c=new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?>{["ERP:AuthMode"]=mode,["ERP:OnPremMobile:PublicOrigin"]=Origin}).Build();
        Assert.Throws<InvalidOperationException>(()=>OnPremMobileOptions.Read(c));
    }
    [Fact]
    public void Spoofed_forwarded_IP_does_not_change_rate_limit_authority()
    {
        var services=new ServiceCollection().AddSingleton(new OnPremMobileOptions(Origin,[],"127.0.0.1")).BuildServiceProvider();
        var h=new Microsoft.AspNetCore.Http.DefaultHttpContext{RequestServices=services};
        h.Connection.RemoteIpAddress=IPAddress.Parse("192.0.2.10");h.Request.Headers["X-Real-IP"]="192.0.2.99";
        Assert.Equal("192.0.2.10",OnPremMobileApi.RateKey(h));
        h.Connection.RemoteIpAddress=IPAddress.Loopback;Assert.Equal("192.0.2.99",OnPremMobileApi.RateKey(h));
        h.Request.Headers["X-Real-IP"]="not-an-ip";Assert.Equal("127.0.0.1",OnPremMobileApi.RateKey(h));
    }
    public class UnusedWorkflow : DispatchProxy { protected override object? Invoke(MethodInfo? targetMethod,object?[]? args)=>throw new InvalidOperationException("Attendance must not invoke workflow commands."); }
    private sealed record Tenant(string Value):ITenantAccessor {public string GetTenantId()=>Value;}
    private sealed class Clock : TimeProvider { public DateTimeOffset Value=DateTimeOffset.Parse("2026-11-17T08:00:00Z");public override DateTimeOffset GetUtcNow()=>Value; }
    private sealed class Fixture : IAsyncDisposable
    {
        internal Guid A=Guid.NewGuid(),B=Guid.NewGuid(),WorkerA=Guid.NewGuid(),WorkerB=Guid.NewGuid();
        internal Clock Clock=new(); internal HttpClient Client=null!;private WebApplication App=null!;
        private SqliteConnection Connection=new("Data Source=:memory:");private DbContextOptions<HrmDbContext> DbOptions=null!;
        internal HrmDbContext Db(string tenant)=>new(DbOptions,new Tenant(tenant));
        internal static async Task<Fixture> Start(bool enabled=true)
        {
            var f=new Fixture();await f.Connection.OpenAsync();f.DbOptions=new DbContextOptionsBuilder<HrmDbContext>().UseSqlite(f.Connection).Options;
            await using(var db=f.Db("tenant-a")){await db.Database.EnsureCreatedAsync();}
            foreach(var (id,tenant,name,code,worker,email) in new[]{(f.A,"tenant-a","Public Test Company","PUBLIC-A",f.WorkerA,"qa-a@example.invalid"),(f.B,"tenant-b","Private Test Company","PRIVATE-B",f.WorkerB,"qa-b@example.invalid")}) {
                await using var db=f.Db(tenant);var location=Guid.NewGuid();var user=Guid.NewGuid();
                db.LegalEntities.Add(new(){Id=id,Code=code,RegisteredName=name,TimeZoneId="Africa/Lusaka"});
                db.WorkLocations.Add(new(){Id=location,LegalEntityId=id,Name="Test office",Code="HQ"});
                db.Workers.Add(new(){Id=worker,EmployeeNo=code+"-001",FirstName="Isolated",LastName="Employee",SubjectId=user.ToString("D"),LocationId=location,Status="active",StartDate=new(2026,1,1)});
                db.LocalUsers.Add(new(){Id=user,Email=email,NormalizedEmail=email.ToUpperInvariant(),DisplayName="Isolated mobile employee",PasswordHash=LocalPasswordHash.Hash(Password),WorkerId=worker,MustChangePassword=false});
                await db.SaveChangesAsync();
            }
            var b=WebApplication.CreateBuilder();b.WebHost.UseTestServer();
            b.Configuration.AddInMemoryCollection(new Dictionary<string,string?> {
                ["ERP:AuthMode"]="local",["HRM:DefaultTenantId"]="tenant-a",["ERP:OnPremMobile:Enabled"]=enabled.ToString(),
                ["ERP:OnPremMobile:PublicOrigin"]=Origin,["ERP:OnPremMobile:Companies:0:Id"]=f.A.ToString("D"),["ERP:OnPremMobile:Companies:0:Tenant"]="tenant-a",["ERP:OnPremMobile:Companies:0:Searchable"]="true",
                ["ERP:OnPremMobile:Companies:1:Id"]=f.B.ToString("D"),["ERP:OnPremMobile:Companies:1:Tenant"]="tenant-b",["ERP:OnPremMobile:Companies:1:Searchable"]="false",
            });
            b.Services.AddHttpContextAccessor();b.Services.AddSingleton<TimeProvider>(f.Clock);
            b.Services.AddSingleton<Func<string,HrmDbContext>>(f.Db);b.Services.AddSingleton(OnPremMobileOptions.Read(b.Configuration));
            b.Services.AddSingleton<OnPremMobileLoginBudget>();
            b.Services.AddScoped<ITenantAccessor,PrincipalTenantAccessor>();b.Services.AddScoped(sp=>new HrmDbContext(f.DbOptions,sp.GetRequiredService<ITenantAccessor>()));
            b.Services.AddAuthentication(LocalAuthenticationHandler.Scheme).AddScheme<LocalAuthOptions,LocalAuthenticationHandler>(LocalAuthenticationHandler.Scheme,_=>{});b.Services.AddAuthorization();
            b.Services.AddSingleton(DispatchProxy.Create<IWorkflowService,UnusedWorkflow>());
            b.Services.AddSingleton(DispatchProxy.Create<IOutboxWriter,UnusedWorkflow>());
            b.Services.AddSingleton(DispatchProxy.Create<Mightyfin.Erp.Hrm.Application.Integration.IIdentityProvisioningService,UnusedWorkflow>());
            b.Services.AddRateLimiter(o=>{o.AddFixedWindowLimiter("onprem-mobile-read",x=>{x.PermitLimit=1000;x.Window=TimeSpan.FromMinutes(1);});o.AddFixedWindowLimiter("onprem-mobile-login",x=>{x.PermitLimit=1000;x.Window=TimeSpan.FromMinutes(1);});o.AddFixedWindowLimiter("password-reset-request",x=>{x.PermitLimit=1000;x.Window=TimeSpan.FromMinutes(1);});});
            f.App=b.Build();f.App.UseRouting();OnPremMobileApi.UseBoundary(f.App);f.App.UseRateLimiter();f.App.UseAuthentication();f.App.UseAuthorization();OnPremMobileApi.Map(f.App);LocalIdentityRoutes.Map(f.App);
            await f.App.StartAsync();f.Client=f.App.GetTestClient();f.Client.BaseAddress=new Uri(Origin);return f;
        }
        internal async Task<JsonElement> Login(){using var r=await Client.PostAsJsonAsync("/mobile/v1/auth/sign-in",new{companyId=A,identifier="qa-a@example.invalid",password=Password});Assert.Equal(HttpStatusCode.OK,r.StatusCode);return await r.Content.ReadFromJsonAsync<JsonElement>();}
        internal Task<HttpResponseMessage> Raw(string method,string path,JsonElement token,object? body=null,Guid? company=null){var r=new HttpRequestMessage(new HttpMethod(method),"/mobile/v1"+path);r.Headers.Authorization=new AuthenticationHeaderValue("Bearer",token.GetProperty("accessToken").GetString());r.Headers.Add("X-Workplace-Company",(company??A).ToString("D"));if(body is not null)r.Content=JsonContent.Create(body);return Client.SendAsync(r);}
        internal async Task<JsonElement> Send(string method,string path,JsonElement token,object? body=null){using var r=await Raw(method,path,token,body);Assert.Equal(HttpStatusCode.OK,r.StatusCode);return await r.Content.ReadFromJsonAsync<JsonElement>();}
        public async ValueTask DisposeAsync(){Client.Dispose();await App.DisposeAsync();await Connection.DisposeAsync();}
    }
}
