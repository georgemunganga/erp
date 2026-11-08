using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OAuth;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Mightyfin.Erp.Hrm.Api;
using Mightyfin.Erp.Hrm.Application;
using Mightyfin.Erp.Hrm.Domain.Entities;
using Mightyfin.Erp.Hrm.Infrastructure.Data;

namespace Mightyfin.Erp.Hrm.Tests;

public sealed class ProductIdentityHttpTests
{
    [Fact]
    public async Task Real_OAuth_handler_requires_secret_PKCE_and_correlation_but_never_opens_an_ERP_session()
    {
        await using var fixture = await Fixture.Start();
        var challenge = await fixture.Challenge();
        Assert.Equal("S256", challenge.Parameters["code_challenge_method"]);
        Assert.Equal("product:identity", challenge.Parameters["scope"]);
        Assert.Equal("https://app.kuubuerp.com"+ProductIdentity.Prefix+"/callback", challenge.Parameters["redirect_uri"]);
        Assert.False(challenge.Parameters.ContainsKey("client_secret"));
        AssertCookie(challenge.CookieHeader, "__Host-kuubu_product_correlation.");
        var callback = await fixture.Callback(challenge);
        Assert.Equal(HttpStatusCode.Redirect, callback.StatusCode);
        var pending = callback.Headers.GetValues("Set-Cookie").Single(x => x.StartsWith(ProductIdentity.PendingCookie+"="));
        AssertCookie(pending, ProductIdentity.PendingCookie);
        Assert.DoesNotContain(callback.Headers.GetValues("Set-Cookie"), x => x.StartsWith("hrm_session="));
        var cookie = pending.Split(';')[0];
        var proof = await fixture.Read(cookie);
        Assert.Equal(HttpStatusCode.OK, proof.StatusCode);
        using var json = JsonDocument.Parse(await proof.Content.ReadAsStringAsync());
        Assert.True(json.RootElement.GetProperty("verified").GetBoolean());
        Assert.False(json.RootElement.GetProperty("operational_access_granted").GetBoolean());
        Assert.Empty(json.RootElement.GetProperty("links").EnumerateArray());
        Assert.Equal(1, fixture.Backchannel.Exchanges);
        Assert.True(fixture.Backchannel.CheckedSecretAndPkce);
        Assert.Equal(HttpStatusCode.Unauthorized, (await fixture.Get("/erp-protected", cookie)).StatusCode);
        Assert.Empty(await fixture.Db.LocalSessions.IgnoreQueryFilters().ToArrayAsync());
        Assert.Empty(await fixture.Db.LocalUsers.IgnoreQueryFilters().ToArrayAsync());
        var options = fixture.App.Services.GetRequiredService<Microsoft.Extensions.Options.IOptionsMonitor<CookieAuthenticationOptions>>().Get(ProductIdentity.PendingScheme);
        var ticket = options.TicketDataFormat.Unprotect(cookie[(cookie.IndexOf('=')+1)..]);
        Assert.NotNull(ticket);
        Assert.Empty(ticket.Properties.GetTokens());
        Assert.DoesNotContain(ticket.Principal.Claims, x => x.Type is "tenant" or System.Security.Claims.ClaimTypes.Role or System.Security.Claims.ClaimTypes.Email);
        Assert.Equal("17", ticket.Principal.FindFirstValue(ProductIdentity.SubjectClaim));
    }

    [Theory]
    [InlineData("missing-state")]
    [InlineData("tampered-state")]
    [InlineData("missing-correlation")]
    [InlineData("wrong-correlation")]
    [InlineData("wrong-host")]
    public async Task Invalid_browser_proof_is_rejected_before_the_backchannel(string mode)
    {
        await using var fixture = await Fixture.Start();
        var challenge = await fixture.Challenge();
        var response = await fixture.Callback(challenge, mode);
        Assert.Contains(response.StatusCode, new[] { HttpStatusCode.BadRequest, HttpStatusCode.Forbidden });
        Assert.Equal(0, fixture.Backchannel.Exchanges);
        Assert.False(response.Headers.TryGetValues("Set-Cookie", out var cookies)
            && cookies.Any(x => x.StartsWith(ProductIdentity.PendingCookie+"=")));
    }

    [Theory]
    [InlineData("issuer")]
    [InlineData("product")]
    [InlineData("subject")]
    [InlineData("verified")]
    [InlineData("operational")]
    [InlineData("schema")]
    [InlineData("json")]
    [InlineData("redirect")]
    [InlineData("unavailable")]
    [InlineData("expired-token")]
    [InlineData("oversized-lifetime")]
    [InlineData("token-type")]
    [InlineData("timeout")]
    [InlineData("oversized-response")]
    public async Task Untrusted_or_failed_provider_response_never_becomes_a_pending_identity(string condition)
    {
        await using var fixture = await Fixture.Start(condition);
        var challenge = await fixture.Challenge();
        var callback = await fixture.Callback(challenge);
        Assert.Equal(HttpStatusCode.Forbidden, callback.StatusCode);
        Assert.Equal("{\"error\":\"product_sign_in_failed\"}", await callback.Content.ReadAsStringAsync());
        Assert.False(callback.Headers.TryGetValues("Set-Cookie", out var cookies)
            && cookies.Any(x => x.StartsWith(ProductIdentity.PendingCookie+"=")));
        Assert.Empty(await fixture.Db.LocalSessions.IgnoreQueryFilters().ToArrayAsync());
    }

    [Fact]
    public async Task Callback_code_replay_is_rejected_even_with_a_restored_old_correlation_cookie()
    {
        await using var fixture = await Fixture.Start();
        var challenge = await fixture.Challenge();
        Assert.Equal(HttpStatusCode.Redirect, (await fixture.Callback(challenge)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await fixture.Callback(challenge)).StatusCode);
    }

    [Fact]
    public async Task Pending_proof_expires_without_sliding_and_logout_is_origin_protected_and_separate()
    {
        await using var fixture = await Fixture.Start();
        var cookie = await fixture.SignIn();
        Assert.Equal(HttpStatusCode.OK, (await fixture.Read(cookie)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await fixture.Logout(cookie, "https://foreign.invalid")).StatusCode);
        var logout = await fixture.Logout(cookie, "https://app.kuubuerp.com");
        Assert.Equal(HttpStatusCode.NoContent, logout.StatusCode);
        Assert.DoesNotContain(logout.Headers.GetValues("Set-Cookie"), x => x.StartsWith("hrm_session="));
        fixture.Clock.Now += TimeSpan.FromMinutes(6);
        Assert.Equal(HttpStatusCode.Unauthorized, (await fixture.Read(cookie)).StatusCode);
    }

    [Fact]
    public async Task Explicit_links_match_only_verified_subject_and_same_tenant_active_users_without_email_join()
    {
        await using var fixture = await Fixture.Start();
        var userA = await fixture.Seed("tenant-a", "17", "active-A");
        await fixture.Seed("tenant-b", "17", "active-B");
        await fixture.Seed("tenant-c", "99", "other-person");
        await fixture.Seed("tenant-d", "17", "inactive", userActive: false);
        await fixture.Seed("tenant-e", "17", "archived-user", userArchived: true);
        await fixture.Seed("tenant-f", "17", "revoked", linkActive: false);
        await fixture.Seed("tenant-g", "17", "archived-link", linkArchived: true);
        await fixture.Seed("tenant-h", "17", "locked", locked: true);
        // Deliberately malformed InMemory data: a genuine PostgreSQL FK must
        // reject this. Query must still refuse the mismatched tenant/user pair.
        await fixture.Seed("tenant-i", "17", "mismatch", localUserOverride: userA);
        var response = await fixture.Read(await fixture.SignIn());
        using var payload = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var names = payload.RootElement.GetProperty("links").EnumerateArray()
            .Select(x => x.GetProperty("display_name").GetString()).Order().ToArray();
        Assert.Equal(["active-A", "active-B"], names);
        Assert.False(payload.RootElement.GetProperty("operational_access_granted").GetBoolean());
    }

    [Fact]
    public async Task Binding_cannot_be_reassigned_or_deleted_through_the_context()
    {
        await using var fixture = await Fixture.Start();
        await fixture.Seed("tenant-a", "17", "original");
        var row = await fixture.Db.ExternalIdentityLinks.SingleAsync();
        row.Subject = "19";
        await Assert.ThrowsAsync<DomainException>(() => fixture.Db.SaveChangesAsync());
        fixture.Db.ChangeTracker.Clear();
        row = await fixture.Db.ExternalIdentityLinks.SingleAsync();
        fixture.Db.Remove(row);
        await Assert.ThrowsAsync<DomainException>(() => fixture.Db.SaveChangesAsync());
    }

    [Fact]
    public async Task Disabled_consumer_exposes_no_challenge_or_session_cookie()
    {
        await using var fixture = await Fixture.Start(enabled: false);
        foreach (var path in new[] { "/start", "/session", "/callback" })
        {
            var response = await fixture.Get(ProductIdentity.Prefix+path);
            Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
            Assert.False(response.Headers.Contains("Set-Cookie"));
        }
    }

    [Fact]
    public async Task Team_sign_in_has_signed_intent_identity_scope_and_session_bound_antiforgery_without_ERP_access()
    {
        await using var fixture = await Fixture.Start(teamEnabled: true);
        var challenge = await fixture.Challenge(ProductCompanyTeamFlow.Prefix+"/start");
        Assert.Equal("product:identity", challenge.Parameters["scope"]);
        var callback = await fixture.Callback(challenge);
        Assert.Equal("/team", callback.Headers.Location!.OriginalString);
        var pending = callback.Headers.GetValues("Set-Cookie").Single(x => x.StartsWith(ProductIdentity.PendingCookie+"=")).Split(';')[0];
        var session = await fixture.Get(ProductCompanyTeamFlow.Prefix+"/session", pending);
        Assert.Equal(HttpStatusCode.OK, session.StatusCode);
        Assert.True(session.Headers.CacheControl!.NoStore);
        using var json = JsonDocument.Parse(await session.Content.ReadAsStringAsync());
        Assert.False(json.RootElement.GetProperty("operational_access_granted").GetBoolean());
        Assert.Equal(ProductCompanyTeamFlow.PurposeValue, json.RootElement.GetProperty("purpose").GetString());
        var token = json.RootElement.GetProperty("csrf_token").GetString()!;
        var csrfSet = session.Headers.GetValues("Set-Cookie").Single(x => x.StartsWith(ProductCompanyTeamFlow.CsrfCookie+"="));
        Assert.Contains("httponly", csrfSet.ToLowerInvariant());
        Assert.Contains("secure", csrfSet.ToLowerInvariant());
        Assert.Contains("samesite=strict", csrfSet.ToLowerInvariant());
        Assert.Contains("path=/", csrfSet.ToLowerInvariant());
        Assert.DoesNotContain("domain=", csrfSet.ToLowerInvariant());
        var cookie = pending+"; "+csrfSet.Split(';')[0];
        Assert.Equal(HttpStatusCode.Unauthorized, (await fixture.Get("/erp-protected", cookie)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await fixture.TeamCancel(cookie, null)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await fixture.TeamCancel(cookie, "forged")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await fixture.TeamCancel(pending, token)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await fixture.TeamCancel(cookie, token, "https://foreign.invalid")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await fixture.TeamCancel(cookie, token, json: false)).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await fixture.TeamCancel(cookie, token)).StatusCode);
        Assert.Empty(await fixture.Db.LocalSessions.IgnoreQueryFilters().ToArrayAsync());
        Assert.Empty(await fixture.Db.LocalUsers.IgnoreQueryFilters().ToArrayAsync());
    }

    [Fact]
    public async Task Team_token_from_an_earlier_sign_in_is_rejected_for_a_fresh_sign_in_by_the_same_person()
    {
        await using var fixture = await Fixture.Start(teamEnabled: true);
        var first = await fixture.SignIn(ProductCompanyTeamFlow.Prefix+"/start");
        var session = await fixture.Get(ProductCompanyTeamFlow.Prefix+"/session", first);
        using var json = JsonDocument.Parse(await session.Content.ReadAsStringAsync());
        var token = json.RootElement.GetProperty("csrf_token").GetString()!;
        var csrfCookie = session.Headers.GetValues("Set-Cookie").Single(x => x.StartsWith(ProductCompanyTeamFlow.CsrfCookie+"=")).Split(';')[0];
        // The synthetic channel uses one fixed code per fixture; open a fresh
        // exchange window to model a distinct, newly correlated authorization.
        fixture.Backchannel.Exchanges = 0;
        var second = await fixture.SignIn(ProductCompanyTeamFlow.Prefix+"/start");
        Assert.NotEqual(first, second);
        Assert.Equal(HttpStatusCode.Forbidden, (await fixture.TeamCancel(second+"; "+csrfCookie, token)).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await fixture.TeamCancel(first+"; "+csrfCookie, token)).StatusCode);
    }

    [Theory]
    [InlineData("ordinary")]
    [InlineData("missing-purpose")]
    [InlineData("wrong-purpose")]
    [InlineData("setup-purpose")]
    [InlineData("setup-handle")]
    [InlineData("missing-nonce")]
    [InlineData("bad-nonce")]
    [InlineData("wrong-issuer")]
    [InlineData("wrong-subject")]
    [InlineData("expired")]
    [InlineData("tampered")]
    [InlineData("absent")]
    public async Task Team_session_rejects_unrelated_or_invalid_product_proofs(string condition)
    {
        await using var fixture = await Fixture.Start(teamEnabled: true);
        var cookie = await fixture.SignIn(condition == "ordinary" ? null : ProductCompanyTeamFlow.Prefix+"/start");
        var options = fixture.App.Services.GetRequiredService<Microsoft.Extensions.Options.IOptionsMonitor<CookieAuthenticationOptions>>().Get(ProductIdentity.PendingScheme);
        var ticket = options.TicketDataFormat.Unprotect(cookie[(cookie.IndexOf('=')+1)..])!;
        var identity = (ClaimsIdentity)ticket.Principal.Identity!;
        switch (condition)
        {
            case "missing-purpose": ticket.Properties.Items.Remove(ProductCompanyTeamFlow.Purpose); break;
            case "wrong-purpose": ticket.Properties.Items[ProductCompanyTeamFlow.Purpose] = "unreviewed"; break;
            case "setup-purpose": ticket.Properties.Items[ProductIdentity.SetupPurpose] = "company-create-v1"; break;
            case "setup-handle": identity.AddClaim(new Claim(ProductIdentity.SetupHandleClaim, "unrelated")); break;
            case "missing-nonce": identity.RemoveClaim(identity.FindFirst(ProductCompanyTeamFlow.NonceClaim)); break;
            case "bad-nonce": identity.RemoveClaim(identity.FindFirst(ProductCompanyTeamFlow.NonceClaim)); identity.AddClaim(new Claim(ProductCompanyTeamFlow.NonceClaim, "invalid")); break;
            case "wrong-issuer": identity.RemoveClaim(identity.FindFirst(ProductIdentity.IssuerClaim)); identity.AddClaim(new Claim(ProductIdentity.IssuerClaim, "https://foreign.invalid")); break;
            case "wrong-subject": identity.RemoveClaim(identity.FindFirst(ProductIdentity.SubjectClaim)); identity.AddClaim(new Claim(ProductIdentity.SubjectClaim, "email@example.test")); break;
        }
        cookie = ProductIdentity.PendingCookie+"="+options.TicketDataFormat.Protect(ticket);
        if (condition == "expired") fixture.Clock.Now += TimeSpan.FromMinutes(6);
        if (condition == "tampered") cookie += "invalid";
        if (condition == "absent") cookie = "hrm_session=unrelated";
        var response = await fixture.Get(ProductCompanyTeamFlow.Prefix+"/session", cookie);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.False(response.Headers.Contains("Set-Cookie"));
        Assert.Equal(HttpStatusCode.Unauthorized, (await fixture.TeamCancel(cookie, "forged")).StatusCode);
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public async Task Team_endpoints_are_unavailable_without_both_identity_and_explicit_team_enablement(bool identityEnabled, bool teamEnabled)
    {
        await using var fixture = await Fixture.Start(enabled: identityEnabled, teamEnabled: teamEnabled);
        foreach (var path in new[] { "/start", "/session" })
        {
            var response = await fixture.Get(ProductCompanyTeamFlow.Prefix+path);
            Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
            Assert.False(response.Headers.Contains("Set-Cookie"));
        }
        Assert.Equal(HttpStatusCode.ServiceUnavailable, (await fixture.TeamCancel("", "forged")).StatusCode);
    }

    private static void AssertCookie(string value, string name)
    {
        Assert.StartsWith(name, value);
        Assert.Contains("httponly", value.ToLowerInvariant());
        Assert.Contains("secure", value.ToLowerInvariant());
        Assert.Contains("samesite=lax", value.ToLowerInvariant());
        Assert.Contains("path=/", value.ToLowerInvariant());
        Assert.DoesNotContain("domain=", value.ToLowerInvariant());
    }

    private sealed class Clock : TimeProvider
    {
        internal DateTimeOffset Now = DateTimeOffset.UtcNow;
        public override DateTimeOffset GetUtcNow() => Now;
    }
    private sealed record Challenge(Dictionary<string, string> Parameters, string Cookie, string CookieHeader);

    private sealed class Fixture(WebApplication app, HttpClient client, HrmDbContext db, Backchannel backchannel, Clock clock, AsyncServiceScope rootScope) : IAsyncDisposable
    {
        internal WebApplication App => app;
        internal HrmDbContext Db => db;
        internal Backchannel Backchannel => backchannel;
        internal Clock Clock => clock;
        internal static async Task<Fixture> Start(string? condition = null, bool enabled = true, bool teamEnabled = false)
        {
            var builder = WebApplication.CreateBuilder();
            builder.WebHost.UseTestServer();
            builder.Logging.ClearProviders();
            var keys = Path.Combine(Path.GetTempPath(), "kuubu-product-keys-"+Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(keys);
            if (!OperatingSystem.IsWindows()) File.SetUnixFileMode(keys, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
            builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ERP:ProductIdentity:Enabled"] = enabled.ToString(),
                ["ERP:ProductLicensing:Enabled"] = teamEnabled.ToString(),
                ["ERP:CompanyTeam:Enabled"] = teamEnabled.ToString(),
                ["ERP:ProductIdentity:ClientId"] = ProductIdentityHttpTests.Backchannel.ClientId,
                ["ERP:ProductIdentity:ClientSecret"] = ProductIdentityHttpTests.Backchannel.Secret,
                ["ERP:ProductIdentity:DataProtectionPath"] = keys,
            });
            builder.Services.AddHttpContextAccessor();
            builder.Services.AddScoped<ITenantAccessor>(_ => new FixedTenantAccessor("tenant-a"));
            builder.Services.AddDbContext<HrmDbContext>(options => options.UseInMemoryDatabase("product-consumer-"+keys));
            builder.Services.AddAuthentication(LocalAuthenticationHandler.Scheme)
                .AddScheme<LocalAuthOptions, LocalAuthenticationHandler>(LocalAuthenticationHandler.Scheme, _ => { });
            builder.Services.AddProductIdentity(builder.Configuration);
            var channel = new Backchannel(condition);
            var clock = new Clock();
            builder.Services.Configure<OAuthOptions>(ProductIdentity.OAuthScheme, options =>
            { options.Backchannel = new HttpClient(channel) { MaxResponseContentBufferSize = 65536 }; options.TimeProvider = clock; });
            builder.Services.Configure<CookieAuthenticationOptions>(ProductIdentity.PendingScheme, options => options.TimeProvider = clock);
            builder.Services.AddAuthorization();
            var app = builder.Build();
            app.UseProductIdentityOriginGuard();
            app.UseAuthentication();
            app.UseAuthorization();
            app.MapProductIdentity();
            app.MapProductCompanyTeam();
            app.MapGet("/erp-protected", () => "ERP records").RequireAuthorization();
            await app.StartAsync();
            var client = app.GetTestClient();
            client.BaseAddress = new Uri("https://app.kuubuerp.com");
            var scope = app.Services.CreateAsyncScope();
            return new Fixture(app, client, new HrmDbContext(
                scope.ServiceProvider.GetRequiredService<DbContextOptions<HrmDbContext>>(),
                new FixedTenantAccessor("tenant-a")), channel, clock, scope);
        }
        internal async Task<Challenge> Challenge(string? path = null)
        {
            var response = await client.GetAsync(path ?? ProductIdentity.Prefix+"/start");
            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
            var uri = response.Headers.Location!;
            Assert.Equal("www.kuubuerp.com", uri.Host);
            var parameters = QueryHelpers.ParseQuery(uri.Query).ToDictionary(x => x.Key, x => x.Value.ToString());
            backchannel.ChallengeHash = parameters["code_challenge"];
            var header = response.Headers.GetValues("Set-Cookie").Single();
            return new Challenge(parameters, header.Split(';')[0], header);
        }
        internal Task<HttpResponseMessage> Callback(Challenge challenge, string? condition = null)
        {
            var state = condition == "missing-state" ? "" : condition == "tampered-state" ? "tampered" : challenge.Parameters["state"];
            var uri = QueryHelpers.AddQueryString(ProductIdentity.Prefix+"/callback", new Dictionary<string, string?> { ["code"] = "fixture-once-code", ["state"] = state });
            var request = new HttpRequestMessage(HttpMethod.Get, condition == "wrong-host" ? "https://foreign.invalid"+uri : uri);
            if (condition != "missing-correlation") request.Headers.Add("Cookie", condition == "wrong-correlation" ? "unrelated-cookie=1" : challenge.Cookie);
            return client.SendAsync(request);
        }
        internal async Task<string> SignIn(string? path = null)
        {
            var response = await Callback(await Challenge(path));
            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
            return response.Headers.GetValues("Set-Cookie").Single(x => x.StartsWith(ProductIdentity.PendingCookie+"=")).Split(';')[0];
        }
        internal Task<HttpResponseMessage> Read(string cookie) => Get(ProductIdentity.Prefix+"/session", cookie);
        internal Task<HttpResponseMessage> TeamCancel(string cookie, string? token, string origin = "https://app.kuubuerp.com", bool json = true)
        {
            var request = new HttpRequestMessage(HttpMethod.Post, ProductCompanyTeamFlow.Prefix+"/cancel")
            { Content = json ? JsonContent.Create(new { }) : new StringContent("{}") };
            if (cookie.Length > 0) request.Headers.Add("Cookie", cookie);
            request.Headers.Add("Origin", origin);
            if (token is not null) request.Headers.Add(ProductCompanyTeamFlow.CsrfHeader, token);
            return client.SendAsync(request);
        }
        internal Task<HttpResponseMessage> Get(string path, string? cookie = null)
        {
            var request = new HttpRequestMessage(HttpMethod.Get, path);
            if (cookie is not null) request.Headers.Add("Cookie", cookie);
            return client.SendAsync(request);
        }
        internal Task<HttpResponseMessage> Logout(string cookie, string origin)
        {
            var request = new HttpRequestMessage(HttpMethod.Post, ProductIdentity.Prefix+"/logout") { Content = JsonContent.Create(new { }) };
            request.Headers.Add("Origin", origin);
            request.Headers.Add("Cookie", cookie+"; hrm_session=unrelated");
            return client.SendAsync(request);
        }
        internal async Task<Guid> Seed(string tenant, string subject, string name, bool userActive = true,
            bool userArchived = false, bool linkActive = true, bool linkArchived = false, bool locked = false, Guid? localUserOverride = null)
        {
            await using var scope = app.Services.CreateAsyncScope();
            // Explicit synthetic tenant write authority; never a production handler.
            var dbOptions = scope.ServiceProvider.GetRequiredService<DbContextOptions<HrmDbContext>>();
            await using var seed = new HrmDbContext(dbOptions, new FixedTenantAccessor(tenant));
            var user = new LocalUser { Email = "same@example.test", NormalizedEmail = "SAME@EXAMPLE.TEST", DisplayName = name,
                PasswordHash = "not-a-password", IsActive = userActive, IsArchived = userArchived,
                LockedUntil = locked ? DateTimeOffset.UtcNow.AddHours(1) : null };
            seed.LocalUsers.Add(user);
            seed.ExternalIdentityLinks.Add(new ExternalIdentityLink { Issuer = "https://commercial-api.greenwebb.tech",
                Subject = subject, LocalUserId = localUserOverride ?? user.Id, IsActive = linkActive, IsArchived = linkArchived });
            await seed.SaveChangesAsync();
            return user.Id;
        }
        public async ValueTask DisposeAsync() { client.Dispose(); await db.DisposeAsync(); await rootScope.DisposeAsync(); await app.DisposeAsync(); }
    }

    private sealed class Backchannel(string? condition) : HttpMessageHandler
    {
        internal const string ClientId = "4798a03c-5ed7-4438-9ec9-d46f63a2f4f1";
        internal const string Secret = "synthetic-test-confidential-secret";
        internal string ChallengeHash = "";
        internal int Exchanges;
        internal bool CheckedSecretAndPkce;
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Assert.Equal("commercial-api.greenwebb.tech", request.RequestUri!.Host);
            if (request.RequestUri.AbsolutePath.EndsWith("/token"))
            {
                Exchanges++;
                var body = QueryHelpers.ParseQuery(await request.Content!.ReadAsStringAsync(cancellationToken));
                Assert.Equal(ClientId, body["client_id"].ToString());
                Assert.Equal(Secret, body["client_secret"].ToString());
                Assert.Equal(ChallengeHash, WebEncoders.Base64UrlEncode(SHA256.HashData(Encoding.ASCII.GetBytes(body["code_verifier"].ToString()))));
                CheckedSecretAndPkce = true;
                if (Exchanges > 1) return new HttpResponseMessage(HttpStatusCode.BadRequest) { Content = JsonContent.Create(new { error = "invalid_grant" }) };
                return new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(new {
                    access_token = "synthetic-access-token", refresh_token = "synthetic-refresh-token",
                    token_type = condition == "token-type" ? "Other" : "Bearer",
                    expires_in = condition == "expired-token" ? 0 : condition == "oversized-lifetime" ? 3600 : 300 }) };
            }
            Assert.Equal("Bearer", request.Headers.Authorization!.Scheme);
            Assert.Equal("synthetic-access-token", request.Headers.Authorization.Parameter);
            if (condition == "timeout") throw new TaskCanceledException("synthetic timeout");
            if (condition == "redirect") return new HttpResponseMessage(HttpStatusCode.Redirect) { Headers = { Location = new Uri("https://foreign.invalid/steal") } };
            if (condition == "unavailable") return new HttpResponseMessage(HttpStatusCode.ServiceUnavailable);
            if (condition == "json") return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("invalid-json") };
            if (condition == "oversized-response") return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(new string('x', 70000)) };
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(new { data = new {
                schema_version = condition == "schema" ? 2 : 1,
                issuer = condition == "issuer" ? "https://foreign.invalid" : "https://commercial-api.greenwebb.tech",
                subject = condition == "subject" ? "email@example.test" : "17",
                product_code = condition == "product" ? "hosting" : "kuubu",
                verified = condition != "verified", operational_access_granted = condition == "operational" } }) };
        }
    }
}
