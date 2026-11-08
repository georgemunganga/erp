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
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Mightyfin.Erp.Hrm.Api;
using Mightyfin.Erp.Hrm.Domain.Entities;
using Mightyfin.Erp.Hrm.Infrastructure.Data;

namespace Mightyfin.Erp.Hrm.Tests;

public sealed class RealSetupProviderFactAttribute : FactAttribute
{
    public RealSetupProviderFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("KUUBU_REAL_SETUP_PROVIDER_FIXTURE")))
            Skip = "Requires a fresh explicitly opted-in loopback Laravel/Kuubu owner fixture.";
    }
}

public sealed class ProductSetupProviderInteropTests
{
    [RealSetupProviderFact]
    public async Task Real_provider_setup_consent_grant_payer_and_company_transaction_interoperate_without_operational_admission()
    {
        var manifestPath = Environment.GetEnvironmentVariable("KUUBU_REAL_SETUP_PROVIDER_FIXTURE")!;
        Assert.True(Path.IsPathFullyQualified(manifestPath));
        using var manifest = JsonDocument.Parse(await File.ReadAllTextAsync(manifestPath));
        string Field(string name) => manifest.RootElement.GetProperty(name).GetString()!;
        Assert.Equal("greenwebb-loopback-interop-v1", Field("fixture_scope"));
        Assert.True(manifest.RootElement.GetProperty("setup_owner_enabled").GetBoolean());
        Assert.True(Guid.TryParse(Field("fixture_marker"), out _));
        using var bridge = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false, UseCookies = false, UseProxy = false })
            { BaseAddress = new Uri("http://127.0.0.1:8120"), Timeout = TimeSpan.FromSeconds(40) };
        bridge.DefaultRequestHeaders.Add("X-Kuubu-Test-Fixture", Field("fixture_marker"));
        using (var health = await bridge.GetAsync("/health"))
        {
            Assert.Equal(HttpStatusCode.OK, health.StatusCode);
            using var payload = JsonDocument.Parse(await health.Content.ReadAsStringAsync());
            Assert.True(payload.RootElement.GetProperty("fixture_marker").GetString() == Field("fixture_marker"));
        }
        await Audit("active", 0, 1);

        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer(); builder.Logging.ClearProviders();
        var root = Path.Combine(Path.GetTempPath(), "kuubu-real-setup-"+Guid.NewGuid().ToString("N"));
        var keys = Path.Combine(root, "keys"); var grants = Path.Combine(root, "grants");
        foreach (var path in new[] { root, keys, grants })
        {
            Directory.CreateDirectory(path);
            if (!OperatingSystem.IsWindows()) File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        }
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?> {
            ["ERP:ProductIdentity:Enabled"]="true", ["ERP:WorkspaceSetup:Enabled"]="true",
            ["ERP:WorkspaceProvisioning:Enabled"]="true", ["ERP:ProductLicensing:Enabled"]="true",
            ["ERP:ProductIdentity:ClientId"]=Field("client_id"), ["ERP:ProductIdentity:ClientSecret"]=Field("client_secret"),
            ["ERP:ProductIdentity:DataProtectionPath"]=keys, ["ERP:WorkspaceSetup:GrantPath"]=grants });
        await using var connection = new SqliteConnection("Data Source="+Path.Combine(root, "core.sqlite"));
        await connection.OpenAsync();
        var dbOptions = new DbContextOptionsBuilder<HrmDbContext>().UseSqlite(connection).Options;
        HrmDbContext Context(string tenant) => new(dbOptions, new FixedTenantAccessor(tenant));
        await using (var db = Context("schema-only")) await db.Database.EnsureCreatedAsync();
        builder.Services.AddHttpContextAccessor();
        builder.Services.AddScoped<ITenantAccessor>(_ => new FixedTenantAccessor("untrusted-default"));
        builder.Services.AddDbContext<HrmDbContext>(options => options.UseSqlite(connection));
        builder.Services.AddAuthentication(LocalAuthenticationHandler.Scheme)
            .AddScheme<LocalAuthOptions, LocalAuthenticationHandler>(LocalAuthenticationHandler.Scheme, _ => { });
        builder.Services.AddAuthorization(); builder.Services.AddProductIdentity(builder.Configuration);
        var channel = new RealProviderChannel();
        builder.Services.Configure<OAuthOptions>(ProductIdentity.OAuthScheme,
            options => options.Backchannel = new HttpClient(channel) { Timeout = TimeSpan.FromSeconds(20) });
        HttpClient Channel() => new(new RealProviderChannel()) { Timeout = TimeSpan.FromSeconds(20) };
        builder.Services.AddSingleton(new ProductSetupPayers(Channel(), Field("client_id")));
        builder.Services.AddSingleton(new ProductWorkspaceSetupAuthority(Channel(), TimeProvider.System, Field("client_id")));
        var machine = new ProductCommercialLicense(Channel(), TimeProvider.System, Field("licensing_client_id"), Field("licensing_secret"));
        builder.Services.AddSingleton(machine);
        builder.Services.AddSingleton(new CoreWorkspaceProvisioner(Context, TimeProvider.System));
        await using var app = builder.Build();
        app.UseProductIdentityOriginGuard(); app.UseAuthentication(); app.UseAuthorization();
        app.MapProductIdentity(); app.MapProductSetup();
        app.MapGet("/erp-protected", () => "records").RequireAuthorization();
        await app.StartAsync();
        using var core = app.GetTestClient(); core.BaseAddress = new Uri("https://app.kuubuerp.com");

        var staffCookie = await SignIn(false);
        Assert.Equal(HttpStatusCode.Unauthorized, (await Send(HttpMethod.Get, ProductSetupFlow.Prefix+"/session", staffCookie)).StatusCode);
        Assert.Empty(Directory.GetFiles(grants)); await CoreAudit(0);
        var ownerCookie = await SignIn(true);
        var cookieOptions = app.Services.GetRequiredService<IOptionsMonitor<CookieAuthenticationOptions>>().Get(ProductIdentity.PendingScheme);
        var ticket = cookieOptions.TicketDataFormat.Unprotect(ownerCookie[(ownerCookie.IndexOf('=')+1)..]);
        Assert.NotNull(ticket); Assert.Empty(ticket.Properties.GetTokens());
        Assert.True(ticket.Principal.FindFirstValue(ProductIdentity.SubjectClaim) == Field("owner_subject"));
        Assert.DoesNotContain(ticket.Principal.Claims, claim => claim.Type is "tenant" or ClaimTypes.Role or ClaimTypes.Email);
        var handle = ticket.Principal.FindFirstValue(ProductIdentity.SetupHandleClaim);
        Assert.NotNull(handle);
        var savedGrant = await app.Services.GetRequiredService<ProductSetupGrantStore>().ReadAsync(handle, Field("owner_subject"), default);
        Assert.NotNull(savedGrant);
        var encrypted = await File.ReadAllTextAsync(Assert.Single(Directory.GetFiles(grants)));
        Assert.False(encrypted.Contains(savedGrant.AccessToken, StringComparison.Ordinal));
        using var sessionResponse = await Send(HttpMethod.Get, ProductSetupFlow.Prefix+"/session", ownerCookie);
        Assert.Equal(HttpStatusCode.OK, sessionResponse.StatusCode);
        using var session = JsonDocument.Parse(await sessionResponse.Content.ReadAsStringAsync());
        Assert.True(session.RootElement.GetProperty("subject").GetString() == Field("owner_subject"));
        var csrf = session.RootElement.GetProperty("csrf_token").GetString()!;
        Assert.False(session.RootElement.GetProperty("operational_access_granted").GetBoolean());
        await Payers(1); await CoreAudit(0); await Audit("active", 2, 1);

        Assert.Equal(HttpStatusCode.BadRequest, (await Create("1", false)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await Create("1", csrfOverride:new string('x',43))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await Create("1", customer:Field("foreign_enrollment_id"))).StatusCode);
        await CoreAudit(0);
        using var created = await Create("1"); Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        using var createdData = JsonDocument.Parse(await created.Content.ReadAsStringAsync());
        using var retry = await Create("1"); Assert.Equal(HttpStatusCode.OK, retry.StatusCode);
        using var retryData = JsonDocument.Parse(await retry.Content.ReadAsStringAsync());
        Assert.Equal(createdData.RootElement.GetProperty("company_id").GetString(), retryData.RootElement.GetProperty("company_id").GetString());
        Assert.False(retryData.RootElement.GetProperty("operational_access_granted").GetBoolean());
        using var second = await Create("2"); Assert.Equal(HttpStatusCode.Created, second.StatusCode);
        using var excess = await Create("3"); Assert.Equal(HttpStatusCode.Conflict, excess.StatusCode);
        using var excessData = JsonDocument.Parse(await excess.Content.ReadAsStringAsync());
        Assert.Equal("CapacityExhausted", excessData.RootElement.GetProperty("status").GetString());
        await CoreAudit(2);
        Assert.Equal(HttpStatusCode.Unauthorized, (await Send(HttpMethod.Get, "/erp-protected", ownerCookie)).StatusCode);

        Assert.Equal(HttpStatusCode.OK, (await bridge.PostAsJsonAsync("/revoke-setup-owner", new { })).StatusCode);
        await Payers(0); Assert.Equal(HttpStatusCode.Forbidden, (await Create("1")).StatusCode);
        await Audit("active", 2, 0); await CoreAudit(2);
        Assert.Equal(HttpStatusCode.OK, (await bridge.PostAsJsonAsync("/restore-setup-owner", new { })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await bridge.PostAsJsonAsync("/suspend", new { })).StatusCode);
        await Payers(0); Assert.Equal(HttpStatusCode.Forbidden, (await Create("1")).StatusCode);
        Assert.Equal(CommercialLicenseStatus.Denied, (await machine.ReadAsync(Field("enrollment_id"))).Status);
        await Audit("suspended", 2, 1); await CoreAudit(2);
        Assert.Equal(HttpStatusCode.NoContent, (await Send(HttpMethod.Post, ProductSetupFlow.Prefix+"/cancel", ownerCookie, new { }, csrf)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await Create("1")).StatusCode);
        Assert.Empty(Directory.GetFiles(grants));

        async Task<string> SignIn(bool setup)
        {
            using var start = await core.GetAsync((setup ? ProductSetupFlow.Prefix : ProductIdentity.Prefix)+"/start");
            Assert.Equal(HttpStatusCode.Redirect, start.StatusCode);
            Assert.Equal("www.kuubuerp.com", start.Headers.Location!.Host);
            var intent = QueryHelpers.ParseQuery(start.Headers.Location.Query).ToDictionary(x => x.Key, x => x.Value.ToString());
            Assert.Equal(setup ? "product:identity product:workspace-setup" : "product:identity", intent["scope"]);
            Assert.Equal("S256", intent["code_challenge_method"]);
            var correlation = Assert.Single(start.Headers.GetValues("Set-Cookie")).Split(';')[0];
            using var authorization = await bridge.PostAsJsonAsync(setup ? "/authorize-setup" : "/authorize", intent);
            Assert.Equal(HttpStatusCode.OK, authorization.StatusCode);
            using var payload = JsonDocument.Parse(await authorization.Content.ReadAsStringAsync());
            Assert.True(payload.RootElement.GetProperty("pre_enrollment_denied").GetBoolean());
            Assert.Equal(setup, payload.RootElement.GetProperty("setup_consent_checked").GetBoolean());
            var redirect = new Uri(payload.RootElement.GetProperty("redirect_uri").GetString()!);
            Assert.Equal("https://app.kuubuerp.com"+ProductIdentity.Prefix+"/callback", redirect.GetLeftPart(UriPartial.Path));
            Assert.True(QueryHelpers.ParseQuery(redirect.Query)["state"].ToString() == intent["state"]);
            using var callback = await Send(HttpMethod.Get, redirect.PathAndQuery, correlation);
            Assert.Equal(HttpStatusCode.Redirect, callback.StatusCode);
            Assert.Equal(setup ? "/setup" : ProductIdentity.Prefix+"/session", callback.Headers.Location!.OriginalString);
            var cookie = Assert.Single(callback.Headers.GetValues("Set-Cookie"), value => value.StartsWith(ProductIdentity.PendingCookie+"=")).Split(';')[0];
            Assert.Equal(HttpStatusCode.Forbidden, (await Send(HttpMethod.Get, redirect.PathAndQuery, correlation)).StatusCode);
            return cookie;
        }
        Task<HttpResponseMessage> Create(string suffix, bool confirm = true, string? csrfOverride = null, string? customer = null) =>
            Send(HttpMethod.Post, ProductSetupFlow.Prefix+"/companies", ownerCookie,
                new { customer_id=customer??Field("customer_id"), company_name="Real provider company "+suffix,
                    idempotency_key="actual_setup_request_"+suffix.PadLeft(12,'0'), confirm }, csrfOverride??csrf);
        Task<HttpResponseMessage> Send(HttpMethod method, string path, string cookie, object? body = null, string? csrfToken = null)
        {
            var request = new HttpRequestMessage(method, path); request.Headers.Add("Cookie",cookie);
            if (body is not null) { request.Content=JsonContent.Create(body); request.Headers.Add("Origin","https://app.kuubuerp.com"); }
            if (csrfToken is not null) request.Headers.Add("X-Kuubu-Setup-CSRF", csrfToken);
            return core.SendAsync(request);
        }
        async Task Payers(int count)
        {
            using var response = await Send(HttpMethod.Get, ProductSetupFlow.Prefix+"/payers", ownerCookie);
            Assert.Equal(HttpStatusCode.OK,response.StatusCode);
            using var payload = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            var rows=payload.RootElement.GetProperty("customers").EnumerateArray().ToArray();
            Assert.Equal(count,rows.Length);
            if(count>0) { Assert.True(rows[0].GetProperty("customer_id").GetString()==Field("customer_id")); Assert.Equal(2,rows[0].GetProperty("workspace_limit").GetInt32()); }
        }
        async Task CoreAudit(int count)
        {
            await using var db=Context("unrelated-default");
            Assert.Empty(await db.LegalEntities.ToArrayAsync());
            var rows=await db.WorkspaceBindings.IgnoreQueryFilters().ToArrayAsync(); Assert.Equal(count,rows.Length);
            Assert.Equal(count,rows.Select(row=>row.TenantId).Distinct().Count());
            Assert.Equal(count,await db.LegalEntities.IgnoreQueryFilters().CountAsync());
            Assert.Equal(count,await db.LocalUsers.IgnoreQueryFilters().CountAsync());
            Assert.Equal(count,await db.ExternalIdentityLinks.IgnoreQueryFilters().CountAsync());
            Assert.Equal(count,await db.AuditEntries.IgnoreQueryFilters().CountAsync());
            Assert.Equal(count,await db.OutboxMessages.IgnoreQueryFilters().CountAsync());
            Assert.Empty(await db.LocalSessions.IgnoreQueryFilters().ToArrayAsync());
            Assert.Empty(await db.LocalCredentialLinks.IgnoreQueryFilters().ToArrayAsync());
            Assert.Empty(await db.TenantRoleAssignments.IgnoreQueryFilters().ToArrayAsync());
            foreach(var row in rows) {
                Assert.True(row.Subject==Field("owner_subject")); Assert.True(row.CustomerId==Field("customer_id"));
                Assert.Equal(2,row.PurchasedLimit);
                await using var scoped=Context(row.TenantId);
                var owner=Assert.Single(await scoped.LocalUsers.ToArrayAsync());
                Assert.Equal(LocalCredentialModes.Federated,owner.CredentialMode); Assert.Null(owner.PasswordHash);
                Assert.Null(owner.Email); Assert.Equal("",owner.RolesCsv);
                Assert.Single(await scoped.LegalEntities.ToArrayAsync()); Assert.Single(await scoped.ExternalIdentityLinks.ToArrayAsync());
            }
        }
        async Task Audit(string status,int personal,int memberships)
        {
            using var response=await bridge.GetAsync("/audit"); Assert.Equal(HttpStatusCode.OK,response.StatusCode);
            using var payload=JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            foreach(var (name,count) in new[]{("users",2),("customers",1),("customer_memberships",memberships),
                ("identity_product_enrollments",personal),("product_enrollments",2),("subscriptions",1),("staff_memberships",0)})
                Assert.Equal(count,payload.RootElement.GetProperty(name).GetInt32());
            Assert.Equal(status,payload.RootElement.GetProperty("subscription_status").GetString());
        }
    }

    internal sealed class RealProviderChannel : DelegatingHandler
    {
        internal RealProviderChannel() : base(new HttpClientHandler { AllowAutoRedirect=false, UseCookies=false, UseProxy=false }) { }
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken cancellation)
        {
            var target=request.RequestUri!;
            Assert.Equal("https",target.Scheme); Assert.Equal("commercial-api.greenwebb.tech",target.Host);
            Assert.Empty(target.Query); Assert.Empty(target.Fragment); Assert.False(request.Headers.Contains("Cookie"));
            Assert.True(target.AbsolutePath is "/api/v1/product-identity/token" or "/api/v1/product-identity/subject"
                or "/api/v1/product-identity/workspace-setup-payers"
                || target.AbsolutePath.StartsWith("/api/v1/product-identity/customers/",StringComparison.Ordinal)
                || target.AbsolutePath.StartsWith("/api/v1/product-identity/licensing/enrollments/",StringComparison.Ordinal));
            request.RequestUri=new Uri("http://127.0.0.1:8101"+target.AbsolutePath);
            return base.SendAsync(request,cancellation);
        }
    }
}
