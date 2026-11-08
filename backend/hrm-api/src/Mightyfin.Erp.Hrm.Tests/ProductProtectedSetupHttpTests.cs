using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
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
using Mightyfin.Erp.Hrm.Infrastructure.Data;

namespace Mightyfin.Erp.Hrm.Tests;

public sealed class ProductProtectedSetupHttpTests
{
    private const string Customer = "01arz3ndektsv4rrffq69g5fav";
    private const string Enrollment = "01arz3ndektsv4rrffq69g5faw";
    private const string ClientId = "4798a03c-5ed7-4438-9ec9-d46f63a2f4f1";
    private const string MachineId = "391bdeaa-87b3-44b3-8e64-2110dcc1a564";
    private const string Key = "synthetic_setup_request_0000000001";
    private const string Origin = "https://app.kuubuerp.com";

    [Fact]
    public async Task Setup_callback_keeps_tokens_server_side_and_does_not_create_a_company_or_ERP_session()
    {
        await using var f = await Fixture.Start();
        var challenge = await f.Challenge(true);
        Assert.Equal("product:identity product:workspace-setup", challenge.Scope);
        var cookie = await f.Callback(challenge);
        var options = f.App.Services.GetRequiredService<IOptionsMonitor<CookieAuthenticationOptions>>().Get(ProductIdentity.PendingScheme);
        var ticket = options.TicketDataFormat.Unprotect(cookie[(cookie.IndexOf('=')+1)..])!;
        Assert.Empty(ticket.Properties.GetTokens());
        Assert.Equal("company-create-v1", ticket.Properties.Items[ProductIdentity.SetupPurpose]);
        Assert.NotNull(ticket.Principal.FindFirstValue(ProductIdentity.SetupHandleClaim));
        Assert.DoesNotContain(ticket.Principal.Claims, x => x.Type is "tenant" or ClaimTypes.Role or ClaimTypes.Email);
        var file = Assert.Single(Directory.GetFiles(f.Grants));
        Assert.DoesNotContain("synthetic.setup.token", await File.ReadAllTextAsync(file));
        var session = await f.Get("/session", cookie);
        Assert.Equal(HttpStatusCode.OK, session.StatusCode);
        var text = await session.Content.ReadAsStringAsync();
        using var sessionData = JsonDocument.Parse(text);
        Assert.Equal("17", sessionData.RootElement.GetProperty("subject").GetString());
        Assert.DoesNotContain("access_token", text); Assert.DoesNotContain("refresh_token", text);
        Assert.Equal(HttpStatusCode.Unauthorized, (await f.GetAbsolute("/erp-protected", cookie)).StatusCode);
        await f.AssertCompanies(0);
    }

    [Fact]
    public async Task Ordinary_sign_in_cannot_substitute_and_account_switch_revokes_old_setup_reference()
    {
        await using var f = await Fixture.Start();
        var ordinary = await f.Callback(await f.Challenge(false));
        Assert.Equal(HttpStatusCode.Unauthorized, (await f.Get("/session", ordinary)).StatusCode);
        Assert.Empty(Directory.GetFiles(f.Grants));
        var setup = await f.Callback(await f.Challenge(true));
        var switched = await f.Callback(await f.Challenge(false), setup);
        Assert.Equal(HttpStatusCode.Unauthorized, (await f.Get("/session", setup)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await f.Get("/session", switched)).StatusCode);
        Assert.Empty(Directory.GetFiles(f.Grants));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Confirmed_create_rechecks_payer_and_license_and_retries_the_same_company(bool teamEnabled)
    {
        await using var f = await Fixture.Start(teamEnabled);
        var cookie = await f.Callback(await f.Challenge(true));
        var csrf = await f.Csrf(cookie);
        var page = await f.Get("/payers", cookie);
        Assert.Equal(HttpStatusCode.OK, page.StatusCode);
        Assert.DoesNotContain("product_enrollment_id", await page.Content.ReadAsStringAsync());
        Assert.Equal(0, f.Provider.AuthorityCalls);
        var created = await f.Post(cookie, csrf);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        using var first = JsonDocument.Parse(await created.Content.ReadAsStringAsync());
        var retry = await f.Post(cookie, csrf);
        Assert.Equal(HttpStatusCode.OK, retry.StatusCode);
        using var second = JsonDocument.Parse(await retry.Content.ReadAsStringAsync());
        Assert.Equal(teamEnabled, first.RootElement.GetProperty("team_management_available").GetBoolean());
        Assert.Equal(teamEnabled, second.RootElement.GetProperty("team_management_available").GetBoolean());
        Assert.Equal(first.RootElement.GetProperty("company_id").GetString(), second.RootElement.GetProperty("company_id").GetString());
        Assert.False(second.RootElement.GetProperty("operational_access_granted").GetBoolean());
        Assert.Equal(2, f.Provider.AuthorityCalls); Assert.Equal(2, f.Provider.LicenseCalls);
        await f.AssertCompanies(1);
        f.Provider.AllowSetup = false;
        Assert.Equal(HttpStatusCode.Forbidden, (await f.Post(cookie, csrf)).StatusCode);
        await f.AssertCompanies(1);
    }

    [Theory]
    [InlineData("origin")][InlineData("csrf")][InlineData("unconfirmed")][InlineData("extra")]
    [InlineData("duplicate")][InlineData("foreign-payer")][InlineData("oversized")]
    public async Task Invalid_or_forged_create_request_cannot_provision(string fault)
    {
        await using var f = await Fixture.Start();
        var cookie = await f.Callback(await f.Challenge(true));
        var csrf = await f.Csrf(cookie);
        var response = await f.Post(cookie, csrf, fault);
        Assert.True(response.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.Forbidden or HttpStatusCode.RequestEntityTooLarge);
        await f.AssertCompanies(0);
    }

    [Fact]
    public async Task Suspended_license_and_cancelled_setup_cookie_cannot_create()
    {
        await using var f = await Fixture.Start();
        var cookie = await f.Callback(await f.Challenge(true)); var csrf = await f.Csrf(cookie);
        f.Provider.Suspended = true;
        Assert.Equal(HttpStatusCode.Forbidden, (await f.Post(cookie, csrf)).StatusCode);
        using var cancel = new HttpRequestMessage(HttpMethod.Post, ProductSetupFlow.Prefix+"/cancel") { Content = JsonContent.Create(new { }) };
        cancel.Headers.Add("Cookie", cookie); cancel.Headers.Add("Origin", Origin); cancel.Headers.Add("X-Kuubu-Setup-CSRF", csrf);
        Assert.Equal(HttpStatusCode.NoContent, (await f.Http.SendAsync(cancel)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await f.Post(cookie, csrf)).StatusCode);
        Assert.Empty(Directory.GetFiles(f.Grants)); await f.AssertCompanies(0);
    }

    private sealed record Challenge(string State, string CorrelationCookie, string Code, string Scope);
    private sealed class Fixture(WebApplication app, HttpClient http, SqliteConnection connection,
        DbContextOptions<HrmDbContext> options, Provider provider, string grants) : IAsyncDisposable
    {
        internal WebApplication App => app;
        internal HttpClient Http => http;
        internal Provider Provider => provider;
        internal string Grants => grants;
        internal static async Task<Fixture> Start(bool teamEnabled = false)
        {
            var builder = WebApplication.CreateBuilder(); builder.WebHost.UseTestServer(); builder.Logging.ClearProviders();
            var root = Path.Combine(Path.GetTempPath(), "kuubu-protected-setup-"+Guid.NewGuid().ToString("N"));
            var keys = Path.Combine(root, "keys"); var grants = Path.Combine(root, "grants");
            foreach (var path in new[] { root, keys, grants })
            { Directory.CreateDirectory(path); if (!OperatingSystem.IsWindows()) File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute); }
            builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?> {
                ["ERP:ProductIdentity:Enabled"]="true", ["ERP:WorkspaceSetup:Enabled"]="true", ["ERP:WorkspaceProvisioning:Enabled"]="true",
                ["ERP:ProductLicensing:Enabled"]="true", ["ERP:ProductIdentity:ClientId"]=ClientId,
                ["ERP:CompanyTeam:Enabled"]=teamEnabled.ToString(),
                ["ERP:ProductIdentity:ClientSecret"]="synthetic-identity-secret", ["ERP:ProductIdentity:DataProtectionPath"]=keys,
                ["ERP:WorkspaceSetup:GrantPath"]=grants });
            var connection = new SqliteConnection("Data Source=:memory:"); await connection.OpenAsync();
            var options = new DbContextOptionsBuilder<HrmDbContext>().UseSqlite(connection).Options;
            HrmDbContext Context(string tenant) => new(options, new FixedTenantAccessor(tenant));
            await using (var db = Context("migration-only")) await db.Database.EnsureCreatedAsync();
            builder.Services.AddHttpContextAccessor();
            builder.Services.AddScoped<ITenantAccessor>(_ => new FixedTenantAccessor("untrusted-default"));
            builder.Services.AddDbContext<HrmDbContext>(o => o.UseSqlite(connection));
            builder.Services.AddAuthentication(LocalAuthenticationHandler.Scheme)
                .AddScheme<LocalAuthOptions, LocalAuthenticationHandler>(LocalAuthenticationHandler.Scheme, _ => { });
            builder.Services.AddAuthorization(); builder.Services.AddProductIdentity(builder.Configuration);
            var provider = new Provider();
            builder.Services.Configure<OAuthOptions>(ProductIdentity.OAuthScheme, o => o.Backchannel = new HttpClient(provider));
            builder.Services.AddSingleton(new ProductWorkspaceSetupAuthority(new HttpClient(provider), TimeProvider.System, ClientId));
            builder.Services.AddSingleton(new ProductSetupPayers(new HttpClient(provider), ClientId));
            builder.Services.AddSingleton(new ProductCommercialLicense(new HttpClient(provider), TimeProvider.System, MachineId, "synthetic-machine-secret"));
            builder.Services.AddSingleton(new CoreWorkspaceProvisioner(Context, TimeProvider.System));
            var app = builder.Build(); app.UseProductIdentityOriginGuard(); app.UseAuthentication(); app.UseAuthorization();
            app.MapProductIdentity(); app.MapProductSetup(); app.MapGet("/erp-protected", () => "records").RequireAuthorization();
            await app.StartAsync(); var http = app.GetTestClient(); http.BaseAddress = new Uri(Origin);
            return new Fixture(app, http, connection, options, provider, grants);
        }
        internal async Task<Challenge> Challenge(bool setup)
        {
            var response = await http.GetAsync(setup ? ProductSetupFlow.Prefix+"/start" : ProductIdentity.Prefix+"/start?setup=true");
            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
            var parameters = QueryHelpers.ParseQuery(response.Headers.Location!.Query);
            var code = Guid.NewGuid().ToString("N"); var scope = parameters["scope"].ToString();
            provider.Codes[code] = (parameters["code_challenge"].ToString(), scope.Contains("product:workspace-setup"));
            Assert.Equal("S256", parameters["code_challenge_method"]);
            Assert.Equal(Origin+ProductIdentity.Prefix+"/callback", parameters["redirect_uri"]);
            return new(parameters["state"]!, response.Headers.GetValues("Set-Cookie").Single().Split(';')[0], code, scope);
        }
        internal async Task<string> Callback(Challenge challenge, string? prior = null)
        {
            var path = QueryHelpers.AddQueryString(ProductIdentity.Prefix+"/callback", new Dictionary<string,string?> { ["state"]=challenge.State,["code"]=challenge.Code });
            using var request = new HttpRequestMessage(HttpMethod.Get,path);
            request.Headers.Add("Cookie", challenge.CorrelationCookie+(prior is null ? "" : "; "+prior));
            var response = await http.SendAsync(request); Assert.Equal(HttpStatusCode.Redirect,response.StatusCode);
            return response.Headers.GetValues("Set-Cookie").Single(x=>x.StartsWith(ProductIdentity.PendingCookie+"=")).Split(';')[0];
        }
        internal Task<HttpResponseMessage> Get(string suffix,string cookie) => GetAbsolute(ProductSetupFlow.Prefix+suffix,cookie);
        internal Task<HttpResponseMessage> GetAbsolute(string path,string cookie)
        { var r=new HttpRequestMessage(HttpMethod.Get,path);r.Headers.Add("Cookie",cookie);return http.SendAsync(r); }
        internal async Task<string> Csrf(string cookie)
        { using var r=await Get("/session",cookie);r.EnsureSuccessStatusCode();using var j=JsonDocument.Parse(await r.Content.ReadAsStringAsync());return j.RootElement.GetProperty("csrf_token").GetString()!; }
        internal Task<HttpResponseMessage> Post(string cookie,string csrf,string fault="")
        {
            var body=JsonSerializer.Serialize(new { customer_id=fault=="foreign-payer" ? Enrollment : Customer, company_name="Explicit company",idempotency_key=Key,confirm=fault!="unconfirmed" });
            if(fault=="extra") body=body[..^1]+",\"tenant\":\"foreign\"}";
            if(fault=="duplicate") body=body[..^1]+",\"customer_id\":\""+Customer+"\"}";
            if(fault=="oversized") body=new string('x',4097);
            var r=new HttpRequestMessage(HttpMethod.Post,ProductSetupFlow.Prefix+"/companies") {Content=new StringContent(body,Encoding.UTF8,"application/json")};
            r.Headers.Add("Cookie",cookie);r.Headers.Add("Origin",fault=="origin" ? "https://foreign.invalid" : Origin);
            r.Headers.Add("X-Kuubu-Setup-CSRF",fault=="csrf" ? new string('x',43) : csrf);return http.SendAsync(r);
        }
        internal async Task AssertCompanies(int count)
        { await using var db=new HrmDbContext(options,new FixedTenantAccessor("inspect"));Assert.Equal(count,await db.LegalEntities.IgnoreQueryFilters().CountAsync());Assert.Equal(count,await db.WorkspaceBindings.IgnoreQueryFilters().CountAsync());Assert.Empty(await db.LocalSessions.IgnoreQueryFilters().ToArrayAsync()); }
        public async ValueTask DisposeAsync() { http.Dispose();await app.DisposeAsync();await connection.DisposeAsync(); }
    }

    private sealed class Provider : HttpMessageHandler
    {
        internal readonly Dictionary<string,(string Hash,bool Setup)> Codes=new();
        private readonly HashSet<string> setupTokens=new();
        internal int AuthorityCalls,LicenseCalls;
        internal bool AllowSetup=true,Suspended;
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken ct)
        {
            Assert.Equal("commercial-api.greenwebb.tech",request.RequestUri!.Host);
            var path=request.RequestUri.AbsolutePath;
            if(path.EndsWith("/token"))
            {
                var body=QueryHelpers.ParseQuery(await request.Content!.ReadAsStringAsync(ct));
                if(body["grant_type"]=="client_credentials")
                { Assert.Equal(MachineId,body["client_id"]);Assert.Equal("synthetic-machine-secret",body["client_secret"]);return Json(new {access_token="synthetic.machine.token",token_type="Bearer",expires_in=120}); }
                Assert.Equal(ClientId,body["client_id"]);Assert.Equal("synthetic-identity-secret",body["client_secret"]);
                Assert.True(Codes.Remove(body["code"]!,out var expected));
                Assert.Equal(expected.Hash,WebEncoders.Base64UrlEncode(SHA256.HashData(Encoding.ASCII.GetBytes(body["code_verifier"]!))));
                var token="synthetic."+(expected.Setup ? "setup" : "identity")+".token."+Guid.NewGuid().ToString("N");
                if(expected.Setup) setupTokens.Add(token);
                return Json(new {access_token=token,refresh_token="synthetic.unused.refresh",token_type="Bearer",expires_in=300});
            }
            var bearer=request.Headers.Authorization?.Parameter;
            if(path.EndsWith("/subject")) return Json(new {data=new {schema_version=1,issuer=ProductCommercialLicense.Issuer,subject="17",product_code="kuubu",verified=true,operational_access_granted=false}});
            if(path.EndsWith("/workspace-setup-payers"))
            { if(!AllowSetup || !setupTokens.Contains(bearer!))return new(HttpStatusCode.Forbidden);return Json(new {data=new {schema_version=1,issuer=ProductCommercialLicense.Issuer,subject="17",client_id=ClientId,product_code="kuubu",customers=new[]{new {customer_id=Customer,name="Owned billing account",product_enrollment_id=Enrollment,workspace_limit=2}},next_cursor=(string?)null,operational_access_granted=false}}); }
            if(path.EndsWith("/workspace-setup-authority"))
            { AuthorityCalls++;if(!AllowSetup || !setupTokens.Contains(bearer!) || !path.Contains("/customers/"+Customer+"/"))return new(HttpStatusCode.Forbidden);var observed=DateTimeOffset.UtcNow;return Json(new {data=new {schema_version=1,issuer=ProductCommercialLicense.Issuer,subject="17",client_id=ClientId,customer_id=Customer,product_code="kuubu",product_enrollment_id=Enrollment,workspace_limit=2,action="request_workspace_creation",payer_control_verified=true,observed_at=observed,authority_until=observed.AddSeconds(60),operational_access_granted=false}}); }
            Assert.EndsWith("/licensing/enrollments/"+Enrollment,path);Assert.Equal("synthetic.machine.token",bearer);LicenseCalls++;
            var now=DateTimeOffset.UtcNow;
            return Json(new {data=new {schema_version=1,issuer=ProductCommercialLicense.Issuer,product_code="kuubu",product_enrollment_id=Enrollment,workspace_limit=Suspended ? 0 : 2,commercial_access_authorized=!Suspended,module_codes=Suspended ? Array.Empty<string>() : new[]{"hrm"},observed_at=now,lease_until=Suspended ? now : now.AddSeconds(60),access_until=Suspended ? (DateTimeOffset?)null : now.AddHours(1),operational_access_granted=false}});
        }
        private static HttpResponseMessage Json(object body)=>new(HttpStatusCode.OK){Content=JsonContent.Create(body)};
    }
}
