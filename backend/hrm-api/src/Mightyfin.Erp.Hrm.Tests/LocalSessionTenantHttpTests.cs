using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Mightyfin.Erp.Hrm.Api;
using Mightyfin.Erp.Hrm.Application.ConfigAndExtras;
using Mightyfin.Erp.Hrm.Domain.Entities;
using Mightyfin.Erp.Hrm.Infrastructure.Data;

namespace Mightyfin.Erp.Hrm.Tests;

public sealed class LocalSessionTenantHttpTests
{
    [Fact]
    public async Task Nondefault_session_uses_its_own_roles_records_and_activity_audit_despite_matching_email()
    {
        await using var fixture = await Fixture.Start();
        var response = await fixture.Send(fixture.TokenB);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = (await response.Content.ReadFromJsonAsync<ScopeResponse>())!;
        Assert.Equal("tenant-b", body.Tenant);
        Assert.Equal(fixture.UserB.ToString("D"), body.Subject);
        Assert.Equal(["B"], body.Companies);
        Assert.Contains("employee", body.Roles);
        Assert.DoesNotContain("erp_admin", body.Roles);
        await using var check = fixture.Context("tenant-b");
        var session = await check.LocalSessions.SingleAsync();
        Assert.True(session.LastSeenAt > DateTimeOffset.UtcNow.AddMinutes(-2));
        Assert.Contains(await check.AuditEntries.ToArrayAsync(), x => x.EntityType == nameof(LocalSession) && x.TenantId == "tenant-b");
        var anonymous = await fixture.Client.GetFromJsonAsync<ScopeResponse>("/scope-without-policy");
        Assert.Equal("tenant-a", anonymous!.Tenant); // no scope leaks to the next request
    }

    [Theory]
    [InlineData("session-archived")]
    [InlineData("session-revoked")]
    [InlineData("session-expired")]
    [InlineData("user-inactive")]
    [InlineData("user-archived")]
    [InlineData("user-locked")]
    [InlineData("tenant-user-mismatch")]
    public async Task Invalid_current_session_or_principal_is_denied_without_activity_writes(string condition)
    {
        await using var fixture = await Fixture.Start(condition);
        var response = await fixture.Send(fixture.TokenB);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        await using var check = fixture.Context("tenant-b");
        Assert.Empty(await check.AuditEntries.ToArrayAsync());
        Assert.True((await check.LocalSessions.SingleAsync()).LastSeenAt < DateTimeOffset.UtcNow.AddMinutes(-5));
    }

    [Fact]
    public async Task Forged_cookie_and_tenant_headers_never_choose_or_authorize_a_customer()
    {
        await using var fixture = await Fixture.Start();
        Assert.Equal(HttpStatusCode.Unauthorized, (await fixture.Send(LocalSessionTokens.Create())).StatusCode);
        using var request = new HttpRequestMessage(HttpMethod.Get, "/session-scope");
        request.Headers.Add("X-Tenant", "tenant-b");
        request.Headers.Add("X-Shell-Entity", Guid.NewGuid().ToString());
        Assert.Equal(HttpStatusCode.Unauthorized, (await fixture.Client.SendAsync(request)).StatusCode);
        var response = await fixture.Send(fixture.TokenA);
        var body = (await response.Content.ReadFromJsonAsync<ScopeResponse>())!;
        Assert.Equal("tenant-a", body.Tenant);
        Assert.Equal(["A"], body.Companies);
        Assert.Contains("erp_admin", body.Roles);
    }

    [Fact]
    public async Task Trusted_authentication_scope_restores_previous_value_even_on_failure()
    {
        var http = new Microsoft.AspNetCore.Http.DefaultHttpContext();
        var accessor = new PrincipalTenantAccessor(new Microsoft.AspNetCore.Http.HttpContextAccessor { HttpContext = http },
            new Microsoft.Extensions.Configuration.ConfigurationBuilder().AddInMemoryCollection(
                new Dictionary<string, string?> { ["HRM:DefaultTenantId"] = "tenant-a" }).Build());
        using (PrincipalTenantAccessor.BeginSessionAuthentication(http, "tenant-b"))
        {
            Assert.Equal("tenant-b", accessor.GetTenantId());
            Assert.Throws<InvalidOperationException>(() =>
            {
                using var nested = PrincipalTenantAccessor.BeginSessionAuthentication(http, "tenant-c");
                Assert.Equal("tenant-c", accessor.GetTenantId());
                throw new InvalidOperationException("synthetic failure");
            });
            Assert.Equal("tenant-b", accessor.GetTenantId());
        }
        Assert.Equal("tenant-a", accessor.GetTenantId());
    }

    private sealed record ScopeResponse(string Tenant, string? Subject, string[] Companies, string[] Roles);

    private sealed class Fixture(WebApplication app, HttpClient client, DbContextOptions<HrmDbContext> options,
        string tokenA, string tokenB, Guid userB) : IAsyncDisposable
    {
        internal HttpClient Client => client;
        internal string TokenA => tokenA;
        internal string TokenB => tokenB;
        internal Guid UserB => userB;
        internal HrmDbContext Context(string tenant) => new(options, new FixedTenantAccessor(tenant));
        internal Task<HttpResponseMessage> Send(string token)
        {
            var request = new HttpRequestMessage(HttpMethod.Get, "/session-scope");
            request.Headers.Add("Cookie", "hrm_session="+token);
            return client.SendAsync(request);
        }
        internal static async Task<Fixture> Start(string? condition = null)
        {
            var name = "session-tenant-"+Guid.NewGuid().ToString("N");
            var root = new InMemoryDatabaseRoot();
            var options = new DbContextOptionsBuilder<HrmDbContext>().UseInMemoryDatabase(name, root).Options;
            var tokens = new[] { LocalSessionTokens.Create(), LocalSessionTokens.Create() };
            var users = new Guid[2];
            for (var index=0; index<2; index++)
            {
                var tenant = index == 0 ? "tenant-a" : "tenant-b";
                await using var db = new HrmDbContext(options, new FixedTenantAccessor(tenant));
                var user = new LocalUser { Email="same@example.invalid", NormalizedEmail="SAME@EXAMPLE.INVALID",
                    DisplayName="Synthetic "+tenant, PasswordHash="not-used", RolesCsv="shared_role", IsActive=true };
                users[index] = user.Id;
                var session = new LocalSession { LocalUserId=user.Id, TokenHash=LocalSessionTokens.Hash(tokens[index]),
                    LastSeenAt=DateTimeOffset.UtcNow.AddHours(-1), ExpiresAt=DateTimeOffset.UtcNow.AddHours(1) };
                if (index == 1)
                {
                    if (condition == "session-archived") session.IsArchived=true;
                    if (condition == "session-revoked") session.RevokedAt=DateTimeOffset.UtcNow;
                    if (condition == "session-expired") session.ExpiresAt=DateTimeOffset.UtcNow.AddSeconds(-1);
                    if (condition == "user-inactive") user.IsActive=false;
                    if (condition == "user-archived") user.IsArchived=true;
                    if (condition == "user-locked") user.LockedUntil=DateTimeOffset.UtcNow.AddHours(1);
                    if (condition == "tenant-user-mismatch") session.LocalUserId=users[0];
                }
                db.LocalUsers.Add(user); db.LocalSessions.Add(session);
                db.TenantRoleAssignments.Add(new TenantRoleAssignment { RoleKey="shared_role", RoleName="Different authority",
                    PermissionsCsv=index==0 ? "erp_admin" : "employee", Active=true });
                db.LegalEntities.Add(new LegalEntity { Code=index==0 ? "A" : "B", RegisteredName=tenant, Currency="ZMW", CountryCode="ZM" });
                await db.SaveChangesAsync();
            }
            var builder = WebApplication.CreateBuilder();
            builder.Logging.ClearProviders(); builder.WebHost.UseTestServer();
            builder.Configuration["HRM:DefaultTenantId"]="tenant-a";
            builder.Services.AddHttpContextAccessor();
            builder.Services.AddScoped<ITenantAccessor, PrincipalTenantAccessor>();
            builder.Services.AddScoped<AuditInterceptor>();
            builder.Services.AddDbContext<HrmDbContext>((services, db) => db.UseInMemoryDatabase(name, root)
                .AddInterceptors(services.GetRequiredService<AuditInterceptor>()));
            builder.Services.AddAuthentication(LocalAuthenticationHandler.Scheme)
                .AddScheme<LocalAuthOptions, LocalAuthenticationHandler>(LocalAuthenticationHandler.Scheme, _ => { });
            builder.Services.AddAuthorization();
            var app = builder.Build(); app.UseAuthentication(); app.UseAuthorization();
            static async Task<ScopeResponse> Read(Microsoft.AspNetCore.Http.HttpContext http, ITenantAccessor tenant, HrmDbContext db)
                => new(tenant.GetTenantId(), http.User.FindFirstValue(ClaimTypes.NameIdentifier),
                    await db.LegalEntities.OrderBy(x=>x.Code).Select(x=>x.Code).ToArrayAsync(),
                    http.User.FindAll(ClaimTypes.Role).Select(x=>x.Value).ToArray());
            app.MapGet("/session-scope", Read).RequireAuthorization();
            app.MapGet("/scope-without-policy", Read);
            await app.StartAsync();
            return new(app, app.GetTestClient(), options, tokens[0], tokens[1], users[1]);
        }
        public async ValueTask DisposeAsync() { client.Dispose(); await app.DisposeAsync(); }
    }
}
