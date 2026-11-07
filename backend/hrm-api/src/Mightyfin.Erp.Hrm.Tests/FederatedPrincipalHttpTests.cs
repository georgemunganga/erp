using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Mightyfin.Erp.Hrm.Api;
using Mightyfin.Erp.Hrm.Application;
using Mightyfin.Erp.Hrm.Application.Integration;
using Mightyfin.Erp.Hrm.Domain.Entities;
using Mightyfin.Erp.Hrm.Infrastructure.Data;

namespace Mightyfin.Erp.Hrm.Tests;

public sealed class FederatedPrincipalHttpTests
{
    [Theory]
    [InlineData("/api/hrm/auth")]
    [InlineData("/api/v1/hrm/auth")]
    public async Task Federated_accounts_have_no_local_password_recovery_setup_or_administrator_override(string prefix)
    {
        await using var fixture = await Fixture.Start();
        foreach (var (path, body, actor, status) in new (string, object, string?, HttpStatusCode)[] {
            ("/login", new { email = "", password = "AttemptedPassword123!" }, null, HttpStatusCode.Unauthorized),
            ("/change-password", new { currentPassword = "", newPassword = "AttemptedPassword123!" }, "federated", HttpStatusCode.Conflict),
            ($"/users/{fixture.FederatedId}/reset-password", new { newPassword = "AttemptedPassword123!" }, "admin", HttpStatusCode.Conflict),
            ($"/users/{fixture.FederatedId}/send-password-link", new { }, "admin", HttpStatusCode.Conflict),
            ("/set-password", new { token = fixture.OldLinkToken, newPassword = "AttemptedPassword123!" }, null, HttpStatusCode.BadRequest),
        })
            Assert.Equal(status, (await fixture.Send(HttpMethod.Post, prefix+path, body, actor)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await fixture.Send(HttpMethod.Patch, prefix+$"/users/{fixture.FederatedId}",
            new { email = "invented@example.test" }, "admin")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await fixture.Send(HttpMethod.Post, prefix+"/request-password-reset",
            new { email = "not-a-local-login@example.test" })).StatusCode);
        var response = await fixture.Send(HttpMethod.Get, prefix+$"/users/{fixture.FederatedId}", actor: "admin");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var user = json.RootElement.GetProperty("user");
        Assert.Equal("federated", user.GetProperty("credentialMode").GetString());
        Assert.Equal(JsonValueKind.Null, user.GetProperty("email").ValueKind);
        Assert.False(user.GetProperty("passwordLoginAvailable").GetBoolean());
        Assert.False(user.GetProperty("passwordManagementAvailable").GetBoolean());
        Assert.False(user.GetProperty("mustChangePassword").GetBoolean());
        using var scope = fixture.App.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<HrmDbContext>();
        var stored = await db.LocalUsers.AsNoTracking().SingleAsync(x => x.Id == fixture.FederatedId);
        Assert.Null(stored.Email); Assert.Null(stored.NormalizedEmail); Assert.Null(stored.PasswordHash);
        Assert.Equal(0, stored.FailedLoginCount);
        Assert.Equal("", stored.RolesCsv);
        Assert.Null((await db.LocalCredentialLinks.SingleAsync()).UsedAt);
        Assert.Empty(await db.LocalSessions.ToArrayAsync());
        Assert.Equal(0, fixture.Outbox.Calls);
    }

    [Fact]
    public async Task A_local_session_row_cannot_admit_a_federated_principal_or_write_its_activity()
    {
        await using var fixture = await Fixture.Start();
        var raw = LocalSessionTokens.Create();
        var previous = DateTimeOffset.UtcNow.AddMinutes(-20);
        using (var scope = fixture.App.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<HrmDbContext>();
            db.LocalSessions.Add(new LocalSession { LocalUserId = fixture.FederatedId,
                TokenHash = LocalSessionTokens.Hash(raw), ExpiresAt = DateTimeOffset.UtcNow.AddHours(1), LastSeenAt = previous });
            await db.SaveChangesAsync();
        }
        var request = new HttpRequestMessage(HttpMethod.Get, "/protected");
        request.Headers.Add("Cookie", "hrm_session="+raw);
        Assert.Equal(HttpStatusCode.Unauthorized, (await fixture.Client.SendAsync(request)).StatusCode);
        using var check = fixture.App.Services.CreateScope();
        Assert.Equal(previous, (await check.ServiceProvider.GetRequiredService<HrmDbContext>().LocalSessions.SingleAsync()).LastSeenAt);
    }

    [Fact]
    public async Task Existing_local_account_can_still_sign_in_with_its_original_password()
    {
        await using var fixture = await Fixture.Start();
        var response = await fixture.Send(HttpMethod.Post, "/api/hrm/auth/login", new { email = "admin@example.test", password = Fixture.Password });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var user = json.RootElement.GetProperty("user");
        Assert.Equal("local", user.GetProperty("credentialMode").GetString());
        Assert.True(user.GetProperty("passwordLoginAvailable").GetBoolean());
        Assert.Single(response.Headers.GetValues("Set-Cookie"));
    }

    private sealed class Fixture(WebApplication app, HttpClient client, Guid federated, Outbox outbox) : IAsyncDisposable
    {
        internal const string Password = "OriginalSyntheticPassword123!";
        internal WebApplication App => app;
        internal HttpClient Client => client;
        internal Guid FederatedId => federated;
        internal Outbox Outbox => outbox;
        internal string OldLinkToken => "old-synthetic-credential-link";
        internal Task<HttpResponseMessage> Send(HttpMethod method, string path, object? body = null, string? actor = null)
        {
            var request = new HttpRequestMessage(method, path);
            if (body is not null) request.Content = JsonContent.Create(body);
            if (actor is not null) request.Headers.Add("X-Synthetic-Actor", actor);
            return client.SendAsync(request);
        }
        internal static async Task<Fixture> Start()
        {
            var builder = WebApplication.CreateBuilder();
            builder.Logging.ClearProviders(); builder.WebHost.UseTestServer();
            builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?> { ["ERP:AuthMode"] = "local" });
            builder.Services.AddScoped<ITenantAccessor>(_ => new FixedTenantAccessor("federated-tests"));
            var store = Guid.NewGuid().ToString();
            builder.Services.AddDbContext<HrmDbContext>(o => o.UseInMemoryDatabase(store));
            builder.Services.AddAuthentication(LocalAuthenticationHandler.Scheme)
                .AddScheme<LocalAuthOptions, LocalAuthenticationHandler>(LocalAuthenticationHandler.Scheme, _ => { });
            builder.Services.AddAuthorization(o => { o.AddPolicy("hrm-admin", p => p.RequireAuthenticatedUser().RequireRole("hr_admin")); SharedSessionAuthorization.AddPolicy(o); });
            builder.Services.AddScoped<IIdentityProvisioningService>(_ => throw new InvalidOperationException("Must not provision identity."));
            var outbox = new Outbox(); builder.Services.AddSingleton<IOutboxWriter>(outbox);
            builder.Services.AddRateLimiter(o => o.AddFixedWindowLimiter("password-reset-request", limit =>
            { limit.PermitLimit = 10; limit.Window = TimeSpan.FromMinutes(1); }));
            var app = builder.Build();
            var user = LocalUser.Federated("Explicit recipient");
            using (var scope = app.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<HrmDbContext>();
                db.LocalUsers.AddRange(user, new LocalUser { Email = "admin@example.test", NormalizedEmail = "ADMIN@EXAMPLE.TEST",
                    DisplayName = "Local administrator", PasswordHash = LocalPasswordHash.Hash(Password), RolesCsv = "hr_admin" });
                // A preexisting or malformed credential link must still fail at redemption.
                db.LocalCredentialLinks.Add(new LocalCredentialLink { LocalUserId = user.Id,
                    TokenHash = LocalSessionTokens.Hash("old-synthetic-credential-link"), ExpiresAt = DateTimeOffset.UtcNow.AddHours(1) });
                await db.SaveChangesAsync();
            }
            app.UseRouting();
            app.Use(async (http, next) => {
                if (http.Request.Headers.TryGetValue("X-Synthetic-Actor", out var actor))
                    http.User = new ClaimsPrincipal(new ClaimsIdentity(new[] {
                        new Claim(ClaimTypes.NameIdentifier, actor == "federated" ? user.Id.ToString() : Guid.Empty.ToString()),
                        new Claim(ClaimTypes.Role, actor == "admin" ? "hr_admin" : "employee"), new Claim("tenant", "federated-tests") }, "synthetic-route-authority"));
                await next();
            });
            app.UseAuthentication(); app.UseAuthorization(); app.UseRateLimiter();
            LocalIdentityRoutes.Map(app);
            app.MapGet("/protected", () => "records").RequireAuthorization();
            await app.StartAsync();
            return new(app, app.GetTestClient(), user.Id, outbox);
        }
        public async ValueTask DisposeAsync() { client.Dispose(); await app.DisposeAsync(); }
    }
    private sealed class Outbox : IOutboxWriter
    {
        internal int Calls;
        public Task<OutboxMessage> EnqueueAsync(string type, string subject, object payload, CancellationToken ct)
        { Calls++; throw new InvalidOperationException("A federated credential flow must not send local credential mail."); }
    }
}
