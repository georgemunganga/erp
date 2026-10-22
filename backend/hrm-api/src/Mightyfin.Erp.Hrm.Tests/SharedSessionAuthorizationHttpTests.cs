using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Mightyfin.Erp.Hrm.Api;
using Mightyfin.Erp.Hrm.Application;
using Mightyfin.Erp.Hrm.Application.ConfigAndExtras;
using Mightyfin.Erp.Hrm.Domain.Entities;
using Mightyfin.Erp.Hrm.Infrastructure.Data;

namespace Mightyfin.Erp.Hrm.Tests;

/// <summary>Mapped real LocalIdentityRoutes, local password/token/session handler and
/// authorization middleware. InMemory/fixed synthetic tenant; not PostgreSQL or full Program hosting proof.</summary>
public sealed class SharedSessionAuthorizationHttpTests
{
    private const string Password="Synthetic-Original-12345";
    private const string NewPassword="Synthetic-Corrected-12345";

    [Theory]
    [InlineData("/api/hrm/auth")]
    [InlineData("/api/v1/hrm/auth")]
    public async Task NonHrm_workerless_account_logs_out_and_revokes_only_its_actual_cookie_session(string prefix)
    {
        await using var fixture=await Fixture.Start();
        var cookie=await fixture.Login(prefix);var other=await fixture.Login(prefix,staff:true);
        var me=await fixture.Send(HttpMethod.Get,prefix+"/me",cookie);
        Assert.True((await Json(me)).GetProperty("authenticated").GetBoolean());
        var result=await fixture.Send(HttpMethod.Post,prefix+"/logout",cookie,new{});
        Assert.Equal(HttpStatusCode.OK,result.StatusCode);
        Assert.False((await Json(result)).GetProperty("authenticated").GetBoolean());
        var deleted=Assert.Single(result.Headers.GetValues("Set-Cookie"));
        Assert.Contains(LocalAuthenticationHandler.CookieName+"=",deleted);
        Assert.Contains("path=/",deleted,StringComparison.OrdinalIgnoreCase);
        Assert.Contains("httponly",deleted,StringComparison.OrdinalIgnoreCase);
        Assert.Contains("secure",deleted,StringComparison.OrdinalIgnoreCase);
        Assert.Contains("samesite=lax",deleted,StringComparison.OrdinalIgnoreCase);
        var revoked=await fixture.Session(cookie);Assert.NotNull(revoked.RevokedAt);
        Assert.Null((await fixture.Session(other)).RevokedAt);
        // Replay an explicitly retained old cookie: the actual handler, not browser deletion, denies it.
        var replay=await fixture.Send(HttpMethod.Get,prefix+"/me",cookie);
        Assert.False((await Json(replay)).GetProperty("authenticated").GetBoolean());
        Assert.Equal(HttpStatusCode.Unauthorized,(await fixture.Send(HttpMethod.Post,prefix+"/logout",cookie,new{})).StatusCode);
        Assert.Equal(revoked.RevokedAt,(await fixture.Session(cookie)).RevokedAt);
        var user=await fixture.Account();Assert.Null(user.WorkerId);Assert.Equal("procurement_session_only",user.RolesCsv);
    }

    [Theory]
    [InlineData("/api/hrm/auth")]
    [InlineData("/api/v1/hrm/auth")]
    public async Task Same_account_password_change_keeps_current_password_validation_without_granting_Hrm(string prefix)
    {
        await using var fixture=await Fixture.Start();var cookie=await fixture.Login(prefix);
        var original=(await fixture.Account()).PasswordHash;
        var denied=await fixture.Send(HttpMethod.Post,prefix+"/change-password",cookie,new{currentPassword="Wrong-current-password",newPassword=NewPassword});
        Assert.Equal(HttpStatusCode.BadRequest,denied.StatusCode);
        Assert.Equal("invalid-password",(await Json(denied)).GetProperty("code").GetString());
        Assert.Equal(original,(await fixture.Account()).PasswordHash);
        var changed=await fixture.Send(HttpMethod.Post,prefix+"/change-password",cookie,new{currentPassword=Password,newPassword=NewPassword});
        Assert.Equal(HttpStatusCode.OK,changed.StatusCode);
        var account=await fixture.Account();Assert.True(LocalPasswordHash.Verify(NewPassword,account.PasswordHash));Assert.False(LocalPasswordHash.Verify(Password,account.PasswordHash));
        Assert.False(account.MustChangePassword);Assert.NotNull(account.PasswordChangedAt);Assert.Null(account.WorkerId);Assert.Equal("procurement_session_only",account.RolesCsv);
        // Existing password-change semantics do not revoke the caller session or mint another one.
        Assert.Null((await fixture.Session(cookie)).RevokedAt);Assert.Equal(1,await fixture.SessionCount());
        Assert.Equal(HttpStatusCode.Forbidden,(await fixture.Send(HttpMethod.Get,"/test/hrm-staff",cookie)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,(await fixture.Send(HttpMethod.Get,prefix+"/users",cookie)).StatusCode);
    }

    [Theory]
    [InlineData("/api/hrm/auth/logout")]
    [InlineData("/api/v1/hrm/auth/logout")]
    [InlineData("/api/hrm/auth/change-password")]
    [InlineData("/api/v1/hrm/auth/change-password")]
    public async Task Anonymous_session_management_is_not_permitted_or_mutating(string path)
    {
        await using var fixture=await Fixture.Start();var original=(await fixture.Account()).PasswordHash;
        var response=await fixture.Send(HttpMethod.Post,path,null,new{currentPassword=Password,newPassword=NewPassword});
        Assert.Equal(HttpStatusCode.Unauthorized,response.StatusCode);Assert.Equal(original,(await fixture.Account()).PasswordHash);Assert.Equal(0,await fixture.SessionCount());
    }

    [Theory]
    [InlineData("/api/hrm/auth","expired")]
    [InlineData("/api/v1/hrm/auth","expired")]
    [InlineData("/api/hrm/auth","revoked")]
    [InlineData("/api/v1/hrm/auth","revoked")]
    [InlineData("/api/hrm/auth","inactive")]
    [InlineData("/api/v1/hrm/auth","inactive")]
    public async Task Invalid_actual_local_session_cannot_manage_credentials_or_logout(string prefix,string fault)
    {
        await using var fixture=await Fixture.Start();var cookie=await fixture.Login(prefix);
        using(var scope=fixture.App.Services.CreateScope())
        {
            var db=scope.ServiceProvider.GetRequiredService<HrmDbContext>();var session=await db.LocalSessions.SingleAsync();
            if(fault=="expired")session.ExpiresAt=DateTimeOffset.UtcNow.AddMinutes(-1);
            if(fault=="revoked")session.RevokedAt=DateTimeOffset.UtcNow.AddMinutes(-1);
            if(fault=="inactive")(await db.LocalUsers.SingleAsync(x=>x.Id==fixture.UserId)).IsActive=false;
            await db.SaveChangesAsync();
        }
        var before=await fixture.Session(cookie);var original=(await fixture.Account()).PasswordHash;
        Assert.Equal(HttpStatusCode.Unauthorized,(await fixture.Send(HttpMethod.Post,prefix+"/logout",cookie,new{})).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized,(await fixture.Send(HttpMethod.Post,prefix+"/change-password",cookie,new{currentPassword=Password,newPassword=NewPassword})).StatusCode);
        Assert.Equal(before.RevokedAt,(await fixture.Session(cookie)).RevokedAt);Assert.Equal(original,(await fixture.Account()).PasswordHash);
    }

    [Fact]
    public async Task Only_session_routes_use_shared_policy_Hrm_default_and_admin_admission_remain_separate()
    {
        await using var fixture=await Fixture.Start();var buyer=await fixture.Login("/api/hrm/auth");var staff=await fixture.Login("/api/hrm/auth",staff:true);
        Assert.Equal(HttpStatusCode.Forbidden,(await fixture.Send(HttpMethod.Get,"/test/hrm-staff",buyer)).StatusCode);
        Assert.Equal(HttpStatusCode.OK,(await fixture.Send(HttpMethod.Get,"/test/hrm-staff",staff)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,(await fixture.Send(HttpMethod.Get,"/api/hrm/auth/users",buyer)).StatusCode);
        var endpoints=((IEndpointRouteBuilder)fixture.App).DataSources.SelectMany(source=>source.Endpoints).OfType<RouteEndpoint>().ToArray();
        foreach(var prefix in new[]{"/api/hrm/auth","/api/v1/hrm/auth"})
        foreach(var suffix in new[]{"/logout","/change-password"})
        {
            var endpoint=Assert.Single(endpoints.Where(endpoint=>endpoint.RoutePattern.RawText==prefix+suffix));
            Assert.Null(endpoint.Metadata.GetMetadata<IAllowAnonymous>());
            Assert.Equal(SharedSessionAuthorization.PolicyName,Assert.Single(endpoint.Metadata.GetOrderedMetadata<IAuthorizeData>()).Policy);
        }
        Assert.Equal(4,endpoints.Count(endpoint=>endpoint.Metadata.GetOrderedMetadata<IAuthorizeData>().Any(data=>data.Policy==SharedSessionAuthorization.PolicyName)));
    }

    private static async Task<JsonElement> Json(HttpResponseMessage response){using var body=JsonDocument.Parse(await response.Content.ReadAsStringAsync());return body.RootElement.Clone();}
    private sealed class Fixture(WebApplication app,HttpClient client,Guid userId) : IAsyncDisposable
    {
        internal WebApplication App=>app;internal Guid UserId=>userId;
        internal async Task<string> Login(string prefix,bool staff=false)
        {
            var response=await client.PostAsJsonAsync(prefix+"/login",new{email=staff?"staff@example.test":"buyer@example.test",password=Password});Assert.Equal(HttpStatusCode.OK,response.StatusCode);
            return Assert.Single(response.Headers.GetValues("Set-Cookie")).Split(';')[0];
        }
        internal Task<HttpResponseMessage> Send(HttpMethod method,string path,string? cookie,object? body=null)
        {
            var request=new HttpRequestMessage(method,path);if(cookie is not null)request.Headers.Add("Cookie",cookie);if(body is not null)request.Content=JsonContent.Create(body);return client.SendAsync(request);
        }
        internal async Task<LocalUser> Account(){using var scope=app.Services.CreateScope();return await scope.ServiceProvider.GetRequiredService<HrmDbContext>().LocalUsers.AsNoTracking().SingleAsync(x=>x.Id==userId);}
        internal async Task<LocalSession> Session(string cookie){var hash=LocalSessionTokens.Hash(cookie[(cookie.IndexOf('=')+1)..]);using var scope=app.Services.CreateScope();return await scope.ServiceProvider.GetRequiredService<HrmDbContext>().LocalSessions.AsNoTracking().SingleAsync(x=>x.TokenHash==hash);}
        internal async Task<int> SessionCount(){using var scope=app.Services.CreateScope();return await scope.ServiceProvider.GetRequiredService<HrmDbContext>().LocalSessions.CountAsync();}
        public async ValueTask DisposeAsync(){client.Dispose();await app.DisposeAsync();}
        internal static async Task<Fixture> Start()
        {
            var builder=WebApplication.CreateBuilder();builder.Logging.ClearProviders();builder.WebHost.UseTestServer();
            builder.Services.AddScoped<ITenantAccessor>(_=>new FixedTenantAccessor("session-tests"));var store="shared-session-"+Guid.NewGuid().ToString("N");builder.Services.AddDbContext<HrmDbContext>(options=>options.UseInMemoryDatabase(store));
            builder.Services.AddAuthentication(LocalAuthenticationHandler.Scheme).AddScheme<LocalAuthOptions,LocalAuthenticationHandler>(LocalAuthenticationHandler.Scheme,_=>{});
            builder.Services.AddAuthorization(options=>{options.DefaultPolicy=new AuthorizationPolicyBuilder().RequireAuthenticatedUser().RequireAssertion(context=>HrmStaffAccess.IsStaff(context.User.Claims)).Build();options.AddPolicy("hrm-admin",policy=>policy.RequireAuthenticatedUser().RequireAssertion(context=>WorkerPrincipal.FromClaims(context.User.Claims).IsRole("hr_admin","erp_admin")));SharedSessionAuthorization.AddPolicy(options);});
            builder.Services.AddScoped<IOutboxWriter,UnusedOutbox>();builder.Services.AddRateLimiter(options=>options.AddFixedWindowLimiter("password-reset-request",limit=>{limit.PermitLimit=5;limit.Window=TimeSpan.FromMinutes(1);}));
            var app=builder.Build();app.UseRouting();app.UseAuthentication();app.UseAuthorization();app.UseMiddleware<TimesheetAccessMiddleware>();app.UseRateLimiter();LocalIdentityRoutes.Map(app);
            // Explicit default-policy probe, not an HRM feature/service integration stub.
            app.MapGet("/test/hrm-staff",()=>Results.Ok(new{staff=true})).RequireAuthorization();
            var buyer=new LocalUser{Email="buyer@example.test",NormalizedEmail="BUYER@EXAMPLE.TEST",DisplayName="Procurement only",PasswordHash=LocalPasswordHash.Hash(Password),RolesCsv="procurement_session_only",IsActive=true,WorkerId=null};
            using(var scope=app.Services.CreateScope())
            {
                var db=scope.ServiceProvider.GetRequiredService<HrmDbContext>();db.TenantRoleAssignments.AddRange(new TenantRoleAssignment{RoleKey="procurement_session_only",RoleName="Procurement only",Category="system",PermissionsCsv="procurement-access",Active=true},new TenantRoleAssignment{RoleKey="employee",RoleName="Employee",PermissionsCsv="employee",Active=true});
                db.LocalUsers.AddRange(buyer,new LocalUser{Email="staff@example.test",NormalizedEmail="STAFF@EXAMPLE.TEST",DisplayName="Staff admission control",PasswordHash=LocalPasswordHash.Hash(Password),RolesCsv="employee",IsActive=true});await db.SaveChangesAsync();
            }
            await app.StartAsync();return new Fixture(app,app.GetTestClient(),buyer.Id);
        }
    }
    private sealed class UnusedOutbox : IOutboxWriter
    {public UnusedOutbox() {} public Task<OutboxMessage> EnqueueAsync(string eventType,string subjectId,object privacySafePayload,CancellationToken ct)=>throw new InvalidOperationException("This session-only fixture must not enqueue messages.");}
}
