using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
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
using Microsoft.Extensions.Options;
using Mightyfin.Erp.Hrm.Api;
using Mightyfin.Erp.Hrm.Domain.Entities;
using Mightyfin.Erp.Hrm.Infrastructure.Data;

namespace Mightyfin.Erp.Hrm.Tests;

public sealed class RealProductProviderFactAttribute : FactAttribute
{
    public RealProductProviderFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("KUUBU_REAL_PROVIDER_FIXTURE")))
            Skip = "Requires an explicitly prepared synthetic loopback Laravel/Kuubu fixture.";
    }
}

public sealed class ProductProviderInteropTests
{
    [RealProductProviderFact]
    public async Task Actual_Kuubu_relay_Laravel_PKCE_and_machine_license_interoperate_without_ERP_admission()
    {
        var path = Environment.GetEnvironmentVariable("KUUBU_REAL_PROVIDER_FIXTURE")!;
        Assert.True(Path.IsPathFullyQualified(path));
        using var manifest = JsonDocument.Parse(await File.ReadAllTextAsync(path));
        string Field(string name) => manifest.RootElement.GetProperty(name).GetString()!;
        Assert.Equal("greenwebb-loopback-interop-v1", Field("fixture_scope"));
        Assert.True(Guid.TryParse(Field("fixture_marker"), out _));
        Assert.NotEqual(Field("client_id"), Field("licensing_client_id"));
        using var bridge = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false, UseCookies = false, UseProxy = false })
        { BaseAddress = new Uri("http://127.0.0.1:8120"), Timeout = TimeSpan.FromSeconds(40) };
        bridge.DefaultRequestHeaders.Add("X-Kuubu-Test-Fixture", Field("fixture_marker"));
        using (var health = await bridge.GetAsync("/health"))
        {
            Assert.Equal(HttpStatusCode.OK, health.StatusCode);
            using var json = JsonDocument.Parse(await health.Content.ReadAsStringAsync());
            Assert.Equal(Field("fixture_marker"), json.RootElement.GetProperty("fixture_marker").GetString());
        }
        await Audit("active", 0);

        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer(); builder.Logging.ClearProviders();
        var keys = Path.Combine(Path.GetTempPath(), "kuubu-real-provider-"+Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(keys);
        if (!OperatingSystem.IsWindows()) File.SetUnixFileMode(keys, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?> {
            ["ERP:ProductIdentity:Enabled"] = "true", ["ERP:ProductIdentity:ClientId"] = Field("client_id"),
            ["ERP:ProductIdentity:ClientSecret"] = Field("client_secret"), ["ERP:ProductIdentity:DataProtectionPath"] = keys });
        builder.Services.AddScoped<ITenantAccessor>(_ => new FixedTenantAccessor("no-operational-tenant"));
        builder.Services.AddDbContext<HrmDbContext>(o => o.UseInMemoryDatabase(keys));
        builder.Services.AddAuthentication(LocalAuthenticationHandler.Scheme)
            .AddScheme<LocalAuthOptions, LocalAuthenticationHandler>(LocalAuthenticationHandler.Scheme, _ => { });
        builder.Services.AddProductIdentity(builder.Configuration);
        var identityChannel = new FixtureBackchannel();
        var clock = new Clock();
        builder.Services.Configure<OAuthOptions>(ProductIdentity.OAuthScheme, o =>
        { o.Backchannel = new HttpClient(identityChannel) { Timeout = TimeSpan.FromSeconds(20), MaxResponseContentBufferSize = 65536 }; o.TimeProvider = clock; });
        builder.Services.Configure<CookieAuthenticationOptions>(ProductIdentity.PendingScheme, o => o.TimeProvider = clock);
        builder.Services.AddAuthorization();
        await using var app = builder.Build();
        app.UseProductIdentityOriginGuard(); app.UseAuthentication(); app.UseAuthorization();
        app.MapProductIdentity(); app.MapGet("/erp-protected", () => "records").RequireAuthorization();
        await app.StartAsync();
        using var core = app.GetTestClient(); core.BaseAddress = new Uri("https://app.kuubuerp.com");
        var start = await core.GetAsync(ProductIdentity.Prefix+"/start");
        Assert.Equal(HttpStatusCode.Redirect, start.StatusCode);
        Assert.Equal("www.kuubuerp.com", start.Headers.Location!.Host);
        Assert.Equal("/account/authorize/", start.Headers.Location.AbsolutePath);
        var intent = QueryHelpers.ParseQuery(start.Headers.Location.Query).ToDictionary(x => x.Key, x => x.Value.ToString());
        Assert.Equal("S256", intent["code_challenge_method"]);
        Assert.Equal("product:identity", intent["scope"]);
        var correlation = Assert.Single(start.Headers.GetValues("Set-Cookie")).Split(';')[0];
        var authorized = await bridge.PostAsJsonAsync("/authorize", intent);
        Assert.Equal(HttpStatusCode.OK, authorized.StatusCode);
        using var authorizedBody = JsonDocument.Parse(await authorized.Content.ReadAsStringAsync());
        Assert.True(authorizedBody.RootElement.GetProperty("pre_enrollment_denied").GetBoolean());
        var destination = new Uri(authorizedBody.RootElement.GetProperty("redirect_uri").GetString()!);
        Assert.Equal("https://app.kuubuerp.com"+ProductIdentity.Prefix+"/callback", destination.GetLeftPart(UriPartial.Path));
        Assert.Equal(intent["state"], QueryHelpers.ParseQuery(destination.Query)["state"].ToString());
        var callback = await Send(destination.PathAndQuery, correlation);
        Assert.Equal(HttpStatusCode.Redirect, callback.StatusCode);
        var issued = callback.Headers.GetValues("Set-Cookie").ToArray();
        Assert.DoesNotContain(issued, x => x.StartsWith("hrm_session="));
        var pending = Assert.Single(issued, x => x.StartsWith(ProductIdentity.PendingCookie+"=")).Split(';')[0];
        var proof = await Send(ProductIdentity.Prefix+"/session", pending);
        Assert.Equal(HttpStatusCode.OK, proof.StatusCode);
        using (var proofBody = JsonDocument.Parse(await proof.Content.ReadAsStringAsync()))
        {
            Assert.True(proofBody.RootElement.GetProperty("verified").GetBoolean());
            Assert.False(proofBody.RootElement.GetProperty("operational_access_granted").GetBoolean());
            Assert.Empty(proofBody.RootElement.GetProperty("links").EnumerateArray());
        }
        var options = app.Services.GetRequiredService<IOptionsMonitor<CookieAuthenticationOptions>>().Get(ProductIdentity.PendingScheme);
        var ticket = options.TicketDataFormat.Unprotect(pending[(pending.IndexOf('=')+1)..]);
        Assert.NotNull(ticket);
        Assert.Equal(Field("staff_subject"), ticket.Principal.FindFirstValue(ProductIdentity.SubjectClaim));
        Assert.Empty(ticket.Properties.GetTokens());
        Assert.DoesNotContain(ticket.Principal.Claims, claim => claim.Type is "tenant" or ClaimTypes.Role or ClaimTypes.Email);
        Assert.Equal(HttpStatusCode.Unauthorized, (await Send("/erp-protected", pending)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await Send(destination.PathAndQuery, correlation)).StatusCode);
        Assert.Equal(2, identityChannel.TokenCalls); Assert.Equal(1, identityChannel.SubjectCalls);

        var machineChannel = new FixtureBackchannel();
        using var machineHttp = new HttpClient(machineChannel);
        var machine = new ProductCommercialLicense(machineHttp, clock, Field("licensing_client_id"), Field("licensing_secret"));
        var licensed = await machine.ReadAsync(Field("enrollment_id"));
        Assert.Equal(CommercialLicenseStatus.Authorized, licensed.Status);
        Assert.NotNull(licensed.Lease); Assert.True(licensed.Lease.AllowsModule("hrm"));
        Assert.False(licensed.Lease.AllowsModule("payroll"));
        Assert.Equal(CommercialLicenseStatus.Denied, (await machine.ReadAsync(Field("foreign_enrollment_id"))).Status);
        var suspended = await bridge.PostAsJsonAsync("/suspend", new { });
        Assert.Equal(HttpStatusCode.OK, suspended.StatusCode);
        var denied = await machine.ReadAsync(Field("enrollment_id"));
        Assert.Equal(CommercialLicenseStatus.Denied, denied.Status); Assert.Null(denied.Lease);
        Assert.Equal(1, machineChannel.TokenCalls); Assert.Equal(3, machineChannel.LicenseCalls);
        await Audit("suspended", 1);
        using (var scope = app.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<HrmDbContext>();
            Assert.Empty(await db.LocalUsers.ToArrayAsync()); Assert.Empty(await db.LocalSessions.ToArrayAsync());
            Assert.Empty(await db.ExternalIdentityLinks.ToArrayAsync());
        }
        clock.Advance(TimeSpan.FromMinutes(6));
        Assert.False(licensed.Lease.IsCurrent);
        Assert.Equal(HttpStatusCode.Unauthorized, (await Send(ProductIdentity.Prefix+"/session", pending)).StatusCode);

        Task<HttpResponseMessage> Send(string uri, string cookie)
        {
            var request = new HttpRequestMessage(HttpMethod.Get, uri); request.Headers.Add("Cookie", cookie);
            return core.SendAsync(request);
        }
        async Task Audit(string status, int personalEnrollments)
        {
            using var response = await bridge.GetAsync("/audit"); Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            foreach (var (name, count) in new[] { ("users", 2), ("customers", 1), ("customer_memberships", 0),
                ("identity_product_enrollments", personalEnrollments), ("product_enrollments", 2), ("subscriptions", 1) })
                Assert.Equal(count, json.RootElement.GetProperty(name).GetInt32());
            Assert.Equal(status, json.RootElement.GetProperty("subscription_status").GetString());
        }
    }

    private sealed class Clock : TimeProvider
    {
        private TimeSpan offset;
        public override DateTimeOffset GetUtcNow() => DateTimeOffset.UtcNow+offset;
        public override long TimestampFrequency => TimeSpan.TicksPerSecond;
        public override long GetTimestamp() => Stopwatch.GetElapsedTime(0, Stopwatch.GetTimestamp()).Ticks+offset.Ticks;
        internal void Advance(TimeSpan value) => offset += value;
    }
    private sealed class FixtureBackchannel : DelegatingHandler
    {
        internal int TokenCalls, SubjectCalls, LicenseCalls;
        internal FixtureBackchannel() : base(new HttpClientHandler { AllowAutoRedirect = false, UseCookies = false, UseProxy = false }) { }
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellation)
        {
            // Qualification adapter only. Runtime issuer/configuration stays pinned;
            // no TLS validation callback, generic URL override or fabricated response.
            var target = request.RequestUri!;
            Assert.Equal("https", target.Scheme); Assert.Equal("commercial-api.greenwebb.tech", target.Host);
            Assert.Empty(target.Query); Assert.Empty(target.Fragment); Assert.False(request.Headers.Contains("Cookie"));
            switch (target.AbsolutePath)
            {
                case "/api/v1/product-identity/token": TokenCalls++; Assert.Equal(HttpMethod.Post, request.Method); break;
                case "/api/v1/product-identity/subject": SubjectCalls++; Assert.Equal(HttpMethod.Get, request.Method); break;
                default:
                    Assert.StartsWith("/api/v1/product-identity/licensing/enrollments/", target.AbsolutePath);
                    LicenseCalls++; Assert.Equal(HttpMethod.Get, request.Method); break;
            }
            request.RequestUri = new Uri("http://127.0.0.1:8101"+target.AbsolutePath);
            return base.SendAsync(request, cancellation);
        }
    }
}
