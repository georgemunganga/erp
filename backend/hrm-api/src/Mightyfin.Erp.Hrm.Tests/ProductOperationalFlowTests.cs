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
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Mightyfin.Erp.Hrm.Api;
using Mightyfin.Erp.Hrm.Application;
using Mightyfin.Erp.Hrm.Infrastructure.Data;

namespace Mightyfin.Erp.Hrm.Tests;

public sealed class ProductOperationalFlowTests
{
    [Fact]
    public async Task Company_discovery_requires_the_distinct_bound_identity_and_rejects_unreviewed_selectors()
    {
        await using var f=await Fixture.Start(companies:true);
        Assert.Equal(HttpStatusCode.Unauthorized,(await f.Get(ProductOperationalFlow.Prefix+"/companies")).StatusCode);
        var signed=await f.Callback(await f.Challenge());var cookie=SetCookie(signed,ProductOperationalFlow.Cookie)+"; "+SetCookie(signed,ProductOperationalBrowser.Cookie);
        var reads=f.Channel.Reads;
        foreach(var query in new[]{"?tenant_id=foreign","?after=bad","?after=11111111-1111-1111-1111-111111111111&after=22222222-2222-2222-2222-222222222222"})
            Assert.Equal(HttpStatusCode.BadRequest,(await f.Get(ProductOperationalFlow.Prefix+"/companies"+query,cookie)).StatusCode);
        Assert.Equal(reads,f.Channel.Reads);
        var page=await f.Get(ProductOperationalFlow.Prefix+"/companies",cookie);Assert.Equal(HttpStatusCode.OK,page.StatusCode);
        var data=await page.Content.ReadFromJsonAsync<JsonElement>();Assert.Empty(data.GetProperty("companies").EnumerateArray());Assert.False(data.GetProperty("operational_access_granted").GetBoolean());
        Assert.Equal(HttpStatusCode.Forbidden,(await f.Get(ProductOperationalFlow.Prefix+"/companies/11111111-1111-1111-1111-111111111111?link_id=22222222-2222-2222-2222-222222222222",cookie)).StatusCode);
        Assert.Empty(await f.Db.LocalSessions.IgnoreQueryFilters().ToArrayAsync());
    }
    [Fact]
    public async Task Company_discovery_defaults_off_independently_of_person_sign_in()
    {
        await using var f=await Fixture.Start();Assert.Equal(HttpStatusCode.ServiceUnavailable,(await f.Get(ProductOperationalFlow.Prefix+"/companies")).StatusCode);
        Assert.Equal(0,f.Channel.Reads);
    }
    [Fact]
    public async Task Correlated_PKCE_callback_holds_credentials_server_side_and_rechecks_person_without_ERP_admission()
    {
        await using var f=await Fixture.Start();var challenge=await f.Challenge();
        Assert.Equal("product:identity product:erp-session",challenge.Parameters["scope"]);
        Assert.Equal(Fixture.Origin+ProductOperationalFlow.Callback,challenge.Parameters["redirect_uri"]);
        Assert.Equal("S256",challenge.Parameters["code_challenge_method"]);Assert.False(challenge.Parameters.ContainsKey("client_secret"));
        var response=await f.Callback(challenge);Assert.Equal(HttpStatusCode.Redirect,response.StatusCode);
        Assert.Equal(ProductOperationalFlow.Landing,response.Headers.Location!.ToString());
        var identity=SetCookie(response,ProductOperationalFlow.Cookie);var browser=SetCookie(response,ProductOperationalBrowser.Cookie);
        foreach(var header in response.Headers.GetValues("Set-Cookie").Where(x=>x.StartsWith(ProductOperationalFlow.Cookie+"=") || x.StartsWith(ProductOperationalBrowser.Cookie+"="))) {
            Assert.Contains("secure",header.ToLowerInvariant());Assert.Contains("httponly",header.ToLowerInvariant());Assert.Contains("samesite=lax",header.ToLowerInvariant());Assert.DoesNotContain("domain=",header.ToLowerInvariant());
        }
        var cookie=identity+"; "+browser;
        var ticket=f.App.Services.GetRequiredService<IOptionsMonitor<CookieAuthenticationOptions>>().Get(ProductOperationalFlow.Scheme).TicketDataFormat.Unprotect(identity[(identity.IndexOf('=')+1)..])!;
        Assert.Empty(ticket.Properties.GetTokens());Assert.Equal("42",ticket.Principal.FindFirstValue(ProductIdentity.SubjectClaim));
        Assert.DoesNotContain(ticket.Principal.Claims,x=>x.Type is "tenant" or ClaimTypes.Role or ClaimTypes.Email);
        Assert.DoesNotContain("operational.access",JsonSerializer.Serialize(ticket.Properties.Items));
        Assert.Equal(1,f.Channel.Reads);
        var session=await f.Get(ProductOperationalFlow.Prefix+"/session",cookie);Assert.Equal(HttpStatusCode.OK,session.StatusCode);
        var body=await session.Content.ReadFromJsonAsync<JsonElement>();Assert.False(body.GetProperty("operational_access_granted").GetBoolean());
        Assert.Equal("company_selection",body.GetProperty("next_step").GetString());Assert.Equal(2,f.Channel.Reads);
        Assert.Equal(HttpStatusCode.Unauthorized,(await f.Get("/erp-protected",cookie)).StatusCode);
        Assert.Empty(await f.Db.LocalSessions.IgnoreQueryFilters().ToArrayAsync());Assert.Empty(await f.Db.LocalUsers.IgnoreQueryFilters().ToArrayAsync());
        Assert.Equal(HttpStatusCode.Unauthorized,(await f.Get(ProductOperationalFlow.Prefix+"/session",identity)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,(await f.Post("/logout",cookie,"forged")).StatusCode);
        var csrf=body.GetProperty("csrf_token").GetString()!;
        Assert.Equal(HttpStatusCode.NoContent,(await f.Post("/logout",cookie,csrf)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized,(await f.Get(ProductOperationalFlow.Prefix+"/session",cookie)).StatusCode);
        Assert.Equal(2,f.Channel.Reads);
    }
    [Theory]
    [InlineData("browser")][InlineData("state")][InlineData("correlation")][InlineData("host")][InlineData("duplicate")]
    public async Task Invalid_browser_state_is_denied_before_token_exchange(string mode)
    {
        await using var f=await Fixture.Start();var challenge=await f.Challenge();
        var response=await f.Callback(challenge,mode);Assert.Contains(response.StatusCode,new[]{HttpStatusCode.Forbidden,HttpStatusCode.BadRequest});
        Assert.Equal(0,f.Channel.Exchanges);Assert.Equal(0,f.Channel.Reads);
    }
    [Theory]
    [InlineData("refresh")][InlineData("lifetime")][InlineData("type")][InlineData("scope")][InlineData("issuer")][InlineData("purpose")][InlineData("denied")][InlineData("outage")]
    public async Task Invalid_provider_state_creates_no_operational_identity(string mode)
    {
        await using var f=await Fixture.Start(mode:mode);var response=await f.Callback(await f.Challenge());
        Assert.Equal(HttpStatusCode.Forbidden,response.StatusCode);
        Assert.Equal("{\"error\":\"operational_sign_in_failed\"}",await response.Content.ReadAsStringAsync());
        Assert.DoesNotContain(response.Headers.GetValues("Set-Cookie"),x=>x.StartsWith(ProductOperationalFlow.Cookie+"=") && !x.StartsWith(ProductOperationalFlow.Cookie+"=;"));
        Assert.Empty(Directory.GetFiles(f.State,"*.credentials"));
    }
    [Fact]
    public async Task Consent_start_requires_origin_JSON_csrf_and_no_browser_selectors()
    {
        await using var f=await Fixture.Start();var bootstrap=await f.Get(ProductOperationalFlow.Prefix+"/bootstrap");
        var browser=SetCookie(bootstrap,ProductOperationalBrowser.Cookie);var csrf=(await bootstrap.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("csrf_token").GetString()!;
        Assert.Empty(Directory.GetFiles(f.State));
        Assert.Equal(HttpStatusCode.Forbidden,(await f.Post("/start",browser,null)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,(await f.Post("/start",browser,csrf,origin:"https://foreign.invalid")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,(await f.Post("/start",browser,csrf,json:false)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest,(await f.Post("/start",browser,csrf,body:"{\"tenant\":\"invented\"}")).StatusCode);
        var request=new HttpRequestMessage(HttpMethod.Get,ProductOperationalFlow.Prefix+"/bootstrap");request.Headers.Add("Sec-Fetch-Site","cross-site");
        Assert.Equal(HttpStatusCode.Forbidden,(await f.Client.SendAsync(request)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest,(await f.Get(ProductOperationalFlow.Prefix+"/bootstrap?subject=42")).StatusCode);
        Assert.Equal(0,f.Channel.Exchanges);Assert.Equal(0,f.Channel.Reads);
    }
    [Fact]
    public async Task Provider_outage_is_unavailable_and_later_denial_durably_revokes_even_restored_cookies()
    {
        await using var f=await Fixture.Start();var signed=await f.Callback(await f.Challenge());
        var cookie=SetCookie(signed,ProductOperationalFlow.Cookie)+"; "+SetCookie(signed,ProductOperationalBrowser.Cookie);
        f.Channel.Mode="outage";Assert.Equal(HttpStatusCode.ServiceUnavailable,(await f.Get(ProductOperationalFlow.Prefix+"/session",cookie)).StatusCode);
        f.Channel.Mode="denied";Assert.Equal(HttpStatusCode.Unauthorized,(await f.Get(ProductOperationalFlow.Prefix+"/session",cookie)).StatusCode);
        var reads=f.Channel.Reads;f.Channel.Mode="";
        Assert.Equal(HttpStatusCode.Unauthorized,(await f.Get(ProductOperationalFlow.Prefix+"/session",cookie)).StatusCode);Assert.Equal(reads,f.Channel.Reads);
    }
    [Fact]
    public async Task Callback_replay_cannot_restore_consumed_provider_code()
    {
        await using var f=await Fixture.Start();var challenge=await f.Challenge();
        Assert.Equal(HttpStatusCode.Redirect,(await f.Callback(challenge)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,(await f.Callback(challenge)).StatusCode);
        Assert.Single(Directory.GetFiles(f.State,"*.credentials"));
    }
    [Fact]
    public async Task Operational_endpoints_default_off_without_cookies_or_provider_calls()
    {
        await using var f=await Fixture.Start(enabled:false);
        foreach(var path in new[]{"/bootstrap","/session","/callback?code=x&state=y"}) {
            var response=await f.Get(ProductOperationalFlow.Prefix+path);Assert.Equal(HttpStatusCode.ServiceUnavailable,response.StatusCode);Assert.False(response.Headers.Contains("Set-Cookie"));
        }
        Assert.Equal(HttpStatusCode.ServiceUnavailable,(await f.Post("/start","","x")).StatusCode);
        Assert.Equal(0,f.Channel.Exchanges);
    }
    private static string SetCookie(HttpResponseMessage response,string name)=>response.Headers.GetValues("Set-Cookie").Single(x=>x.StartsWith(name+"=",StringComparison.Ordinal)).Split(';')[0];
    private sealed record Challenge(Dictionary<string,string> Parameters,string Cookie);
    private sealed class Fixture: IAsyncDisposable
    {
        internal const string Origin="https://app.kuubuerp.com",ClientId="391bdeaa-87b3-44b3-8e64-2110dcc1a564",Secret="synthetic-operational-secret";
        internal required WebApplication App;internal required HttpClient Client;internal required Channel Channel;internal required string State;internal required HrmDbContext Db;internal AsyncServiceScope Scope;
        internal static async Task<Fixture> Start(bool enabled=true,string mode="",bool companies=false)
        {
            var root=Path.Combine(Path.GetTempPath(),"kuubu-operational-flow-"+Guid.NewGuid().ToString("N"));
            var keys=Path.Combine(root,"keys");var state=Path.Combine(root,"credentials");
            foreach(var path in new[]{root,keys,state}){Directory.CreateDirectory(path);if(!OperatingSystem.IsWindows())File.SetUnixFileMode(path,UnixFileMode.UserRead|UnixFileMode.UserWrite|UnixFileMode.UserExecute);}
            var builder=WebApplication.CreateBuilder();builder.WebHost.UseTestServer();builder.Logging.ClearProviders();
            builder.Configuration.AddInMemoryCollection(new Dictionary<string,string?>{
                ["ERP:ProductIdentity:Enabled"]="true",["ERP:ProductIdentity:ClientId"]="4798a03c-5ed7-4438-9ec9-d46f63a2f4f1",
                ["ERP:ProductIdentity:ClientSecret"]="synthetic-setup-secret",["ERP:ProductIdentity:DataProtectionPath"]=keys,
                ["ERP:OperationalIdentity:Enabled"]=enabled.ToString(),["ERP:OperationalIdentity:ClientId"]=ClientId,["ERP:OperationalIdentity:ClientSecret"]=Secret,["ERP:OperationalIdentity:StatePath"]=state,
                ["ERP:ProductLicensing:Enabled"]=companies.ToString(),["ERP:OperationalCompanies:Enabled"]=companies.ToString()});
            builder.Services.AddHttpContextAccessor();builder.Services.AddScoped<ITenantAccessor>(_=>new FixedTenantAccessor("tenant-a"));
            builder.Services.AddDbContext<HrmDbContext>(options=>options.UseInMemoryDatabase(root));
            builder.Services.AddAuthentication(LocalAuthenticationHandler.Scheme).AddScheme<LocalAuthOptions,LocalAuthenticationHandler>(LocalAuthenticationHandler.Scheme,_=>{});
            builder.Services.AddProductIdentity(builder.Configuration);var channel=new Channel{Mode=mode};
            if(companies)builder.Services.AddScoped(provider=>new CoreOperationalCompanies(provider.GetRequiredService<HrmDbContext>(),
                (_,_)=>throw new InvalidOperationException("Unbound company must not reach licensing."),TimeProvider.System));
            if(enabled){builder.Services.Configure<OAuthOptions>(ProductOperationalFlow.OAuthScheme,o=>o.Backchannel=new HttpClient(channel){MaxResponseContentBufferSize=65536});
                builder.Services.AddSingleton(new ProductOperationalSubject(new HttpClient(channel),TimeProvider.System,ClientId));}
            builder.Services.AddAuthorization();var app=builder.Build();app.UseProductIdentityOriginGuard();app.UseAuthentication();app.UseAuthorization();
            ProductOperationalFlow.Map(app);app.MapGet("/erp-protected",()=>"ERP").RequireAuthorization();await app.StartAsync();
            var scope=app.Services.CreateAsyncScope();var client=app.GetTestClient();client.BaseAddress=new Uri(Origin);
            return new(){App=app,Client=client,Channel=channel,State=state,Scope=scope,Db=scope.ServiceProvider.GetRequiredService<HrmDbContext>()};
        }
        internal async Task<Challenge> Challenge()
        {
            var bootstrap=await Get(ProductOperationalFlow.Prefix+"/bootstrap");var browser=SetCookie(bootstrap,ProductOperationalBrowser.Cookie);
            var csrf=(await bootstrap.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("csrf_token").GetString()!;
            var response=await Post("/start",browser,csrf);Assert.Equal(HttpStatusCode.OK,response.StatusCode);Assert.Null(response.Headers.Location);
            var authorization=new Uri((await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("authorization_url").GetString()!);
            Assert.True(response.Headers.CacheControl!.NoStore);Assert.Equal("www.kuubuerp.com",authorization.Host);
            var parameters=QueryHelpers.ParseQuery(authorization.Query).ToDictionary(x=>x.Key,x=>x.Value.ToString());Channel.Hash=parameters["code_challenge"];
            var correlation=response.Headers.GetValues("Set-Cookie").Single(x=>x.StartsWith("__Host-kuubu_operational_correlation.")).Split(';')[0];
            return new(parameters,SetCookie(response,ProductOperationalBrowser.Cookie)+"; "+correlation);
        }
        internal Task<HttpResponseMessage> Callback(Challenge challenge,string mode="")
        {
            var url=ProductOperationalFlow.Callback+"?"+new FormUrlEncodedContent(new Dictionary<string,string>{["code"]="synthetic-once-code",["state"]=mode=="state"?"bad-state":challenge.Parameters["state"]}).ReadAsStringAsync().GetAwaiter().GetResult();
            if(mode=="duplicate")url+="&state=again";
            var cookie=mode=="browser"?challenge.Cookie.Split(';')[1]:mode=="correlation"?challenge.Cookie.Split(';')[0]:challenge.Cookie;
            return Get(mode=="host"?"https://foreign.invalid"+url:url,cookie);
        }
        internal Task<HttpResponseMessage> Get(string url,string? cookie=null){var request=new HttpRequestMessage(HttpMethod.Get,url);if(cookie is not null)request.Headers.Add("Cookie",cookie);return Client.SendAsync(request);}
        internal Task<HttpResponseMessage> Post(string path,string cookie,string? csrf,string origin=Origin,bool json=true,string body="{}")
        {
            var request=new HttpRequestMessage(HttpMethod.Post,ProductOperationalFlow.Prefix+path){Content=new StringContent(body,Encoding.UTF8,json?"application/json":"text/plain")};
            request.Headers.Add("Origin",origin);if(cookie!="")request.Headers.Add("Cookie",cookie);if(csrf is not null)request.Headers.Add(ProductOperationalBrowser.Header,csrf);return Client.SendAsync(request);
        }
        public async ValueTask DisposeAsync(){Client.Dispose();await Scope.DisposeAsync();await App.DisposeAsync();}
    }
    private sealed class Channel:HttpMessageHandler
    {
        internal string Hash="",Mode="";internal int Exchanges,Reads;
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken cancellation)
        {
            if(request.RequestUri!.AbsolutePath.EndsWith("/token",StringComparison.Ordinal)) {
                Exchanges++;var form=QueryHelpers.ParseQuery(await request.Content!.ReadAsStringAsync(cancellation));
                Assert.Equal(Fixture.ClientId,form["client_id"]);Assert.Equal(Fixture.Secret,form["client_secret"]);Assert.Equal("authorization_code",form["grant_type"]);
                Assert.Equal(Hash,WebEncoders.Base64UrlEncode(SHA256.HashData(Encoding.ASCII.GetBytes(form["code_verifier"].ToString()))));
                Assert.Equal(Fixture.Origin+ProductOperationalFlow.Callback,form["redirect_uri"]);
                if(Exchanges>1)return new(HttpStatusCode.BadRequest){Content=JsonContent.Create(new{error="invalid_grant"})};
                return new(HttpStatusCode.OK){Content=JsonContent.Create(new{access_token="operational.access",refresh_token=Mode=="refresh"?null:"operational.refresh",token_type=Mode=="type"?"other":"Bearer",expires_in=Mode=="lifetime"?900:300,
                    scope=Mode=="scope"?"product:identity product:workspace-setup":"product:identity product:erp-session"})};
            }
            Reads++;Assert.Equal(ProductOperationalSubject.Endpoint,request.RequestUri.AbsoluteUri);Assert.Equal("operational.access",request.Headers.Authorization!.Parameter);
            if(Mode is "denied" or "outage")return new(Mode=="denied"?HttpStatusCode.Forbidden:HttpStatusCode.ServiceUnavailable){Content=JsonContent.Create(new{error="unavailable"})};
            var now=DateTimeOffset.UtcNow;
            return new(HttpStatusCode.OK){Content=JsonContent.Create(new{data=new{schema_version=1,issuer=Mode=="issuer"?"https://foreign.invalid":ProductCommercialLicense.Issuer,
                subject="42",client_id=Fixture.ClientId,product_code="kuubu",purpose=Mode=="purpose"?"company-create-v1":"erp-session-v1",verified=true,
                observed_at=now.ToString("O"),proof_until=now.AddSeconds(30).ToString("O"),operational_access_granted=false}})};
        }
    }
}
