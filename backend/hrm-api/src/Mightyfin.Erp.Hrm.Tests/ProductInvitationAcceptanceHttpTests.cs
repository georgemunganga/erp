using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication.OAuth;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Mightyfin.Erp.Hrm.Api;
using CoreFixture=Mightyfin.Erp.Hrm.Tests.CoreCompanyTeamCommandsTests.Fixture;

namespace Mightyfin.Erp.Hrm.Tests;

public sealed class ProductInvitationAcceptanceHttpTests
{
    [Theory]
    [InlineData("/API/CORE/PRODUCT-IDENTITY/INVITATION/SESSION")]
    [InlineData("/api/core/product-identity/invitation/session/")]
    public async Task Equivalent_route_spellings_share_the_same_endpoint_budget(string path)
    {
        await using var f=await Fixture.Start();
        for(var i=0;i<60;i++)Assert.Equal(HttpStatusCode.OK,(await f.Get("/session")).StatusCode);
        Assert.Equal(HttpStatusCode.TooManyRequests,(await f.Send("GET",path)).StatusCode);
    }
    [Fact]
    public async Task Anonymous_prepare_exhaustion_is_429_with_retry_and_cannot_be_reset_by_forwarding_headers()
    {
        await using var f=await Fixture.Start();
        for(var i=0;i<10;i++) {
            await f.ReadSession();
            Assert.Equal(HttpStatusCode.Created,(await f.Post("/prepare",new(){["token"]=ProductInvitationIntentStore.Random()})).StatusCode);
        }
        await f.ReadSession();
        var denied=await f.PostRaw("/prepare",JsonSerializer.Serialize(new{token=ProductInvitationIntentStore.Random()}),"spoof-ip");
        Assert.Equal(HttpStatusCode.TooManyRequests,denied.StatusCode);
        Assert.Equal(TimeSpan.FromSeconds(60),denied.Headers.RetryAfter?.Delta);
        await using var db=f.Core.Context();Assert.Single(await db.CompanyMemberships.ToArrayAsync());
        Assert.Empty(await db.CompanyInvitationReceipts.ToArrayAsync());
    }
    [Fact]
    public async Task Full_directory_returns_retryable_503_and_keeps_the_existing_intent_usable()
    {
        await using var f=await Fixture.Start(admissionUnits:2);
        await f.ReadSession();
        Assert.Equal(HttpStatusCode.Created,(await f.Post("/prepare",new(){["token"]=ProductInvitationIntentStore.Random()})).StatusCode);
        await f.ReadSession();
        var denied=await f.Post("/prepare",new(){["token"]=ProductInvitationIntentStore.Random()});
        Assert.Equal(HttpStatusCode.ServiceUnavailable,denied.StatusCode);
        Assert.Equal(TimeSpan.FromSeconds(5),denied.Headers.RetryAfter?.Delta);
        Assert.Equal(HttpStatusCode.Redirect,(await f.Get("/start")).StatusCode);
    }
    [Fact]
    public void Invitation_and_setup_private_directories_cannot_alias_via_a_trailing_separator()
    {
        var keys=Path.Combine(Path.GetTempPath(),"kuubu-invitation-config-keys-"+Guid.NewGuid().ToString("N"));
        var intents=Path.Combine(Path.GetTempPath(),"kuubu-invitation-config-state-"+Guid.NewGuid().ToString("N"));
        foreach(var path in new[]{keys,intents}){Directory.CreateDirectory(path);if(!OperatingSystem.IsWindows())File.SetUnixFileMode(path,UnixFileMode.UserRead|UnixFileMode.UserWrite|UnixFileMode.UserExecute);}
        var config=new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?>{
            ["ERP:ProductIdentity:Enabled"]="true",["ERP:ProductLicensing:Enabled"]="true",["ERP:CompanyTeam:Enabled"]="true",
            ["ERP:CompanyInvitationAcceptance:Enabled"]="true",["ERP:CompanyInvitationAcceptance:IntentPath"]=intents,
            ["ERP:WorkspaceSetup:GrantPath"]=intents+Path.DirectorySeparatorChar,
            ["ERP:ProductIdentity:DataProtectionPath"]=keys,["ERP:ProductIdentity:ClientId"]=Client,["ERP:ProductIdentity:ClientSecret"]="synthetic-invitation-secret"}).Build();
        Assert.Throws<InvalidOperationException>(()=>new ServiceCollection().AddProductIdentity(config));
    }
    [Fact]
    public async Task Prepared_secret_survives_purpose_bound_OAuth_and_explicit_acceptance_without_operational_access()
    {
        await using var f=await Fixture.Start();await f.Issue();await f.Prepare();
        var location=await f.SignIn();Assert.Equal("/accept-invitation",location);
        var session=await f.Json(await f.Get("/session"));Assert.True(session.GetProperty("verified").GetBoolean());
        f.Csrf=session.GetProperty("csrf_token").GetString();
        var review=await f.Get("/review");Assert.Equal(HttpStatusCode.OK,review.StatusCode);
        var data=await f.Json(review);Assert.Equal("member",data.GetProperty("role").GetString());
        Assert.Equal(f.Core.Company,data.GetProperty("company_id").GetGuid());
        Assert.DoesNotContain("token",data.GetRawText());Assert.DoesNotContain("email",data.GetRawText());
        await using(var db=f.Core.Context())Assert.Single(await db.CompanyMemberships.ToArrayAsync());
        var command=f.Command();var accepted=await f.Post("/accept",command);Assert.Equal(HttpStatusCode.OK,accepted.StatusCode);
        Assert.Equal("Accepted",(await f.Json(accepted)).GetProperty("status").GetString());
        Assert.Equal("Existing",(await f.Json(await f.Post("/accept",command))).GetProperty("status").GetString());
        f.State.Status=CommercialLicenseStatus.Denied;
        Assert.Equal(HttpStatusCode.Forbidden,(await f.Post("/accept",command)).StatusCode);
        await using(var db=f.Core.Context()){
            Assert.Equal(2,await db.CompanyMemberships.CountAsync());Assert.Equal(2,await db.CompanyInvitationReceipts.CountAsync());
            Assert.Empty(await db.LocalSessions.ToArrayAsync());Assert.Empty(await db.LocalCredentialLinks.ToArrayAsync());Assert.Empty(await db.TenantRoleAssignments.ToArrayAsync());
            Assert.All(await db.LocalUsers.ToArrayAsync(),x=>{Assert.Null(x.Email);Assert.Null(x.PasswordHash);Assert.Equal("",x.RolesCsv);});
        }
        Assert.Equal(HttpStatusCode.Unauthorized,(await f.Send("GET","/erp-protected")).StatusCode);
    }
    [Theory]
    [InlineData("missing-header",403)][InlineData("forged-header",403)][InlineData("missing-cookie",401)]
    [InlineData("foreign-origin",403)][InlineData("unconfirmed",400)][InlineData("extra",400)]
    [InlineData("duplicate",400)][InlineData("string-revision",400)][InlineData("number-key",400)][InlineData("oversized",400)]
    public async Task Unreviewed_or_forged_acceptance_never_adds_members(string fault,int status)
    {
        await using var f=await Fixture.Start();await f.Issue();await f.Prepare();await f.SignIn();await f.ReadSession();
        var command=f.Command();if(fault=="unconfirmed")command["confirmed"]=false;
        if(fault=="extra")command["company_id"]=Guid.NewGuid().ToString("D");
        if(fault=="string-revision")command["expected_revision"]="1";
        if(fault=="number-key")command["command_key"]=1;
        var raw=JsonSerializer.Serialize(command);
        if(fault=="duplicate")raw=raw[..^1]+",\"confirmed\":true}";
        if(fault=="oversized")raw=new string(' ',2049)+raw;
        var response=await f.PostRaw("/accept",raw,fault);Assert.Equal(status,(int)response.StatusCode);
        await using var db=f.Core.Context();Assert.Single(await db.CompanyMemberships.ToArrayAsync());Assert.Single(await db.CompanyInvitationReceipts.ToArrayAsync());
    }
    [Theory]
    [InlineData("email")][InlineData("client")][InlineData("unverified")][InlineData("expired")][InlineData("denied")][InlineData("unavailable")]
    public async Task Fresh_scoped_recipient_observation_is_required_for_every_review_and_acceptance(string mode)
    {
        await using var f=await Fixture.Start();await f.Issue();await f.Prepare();await f.SignIn();await f.ReadSession();
        f.Channel.Mode=mode;
        var expected=mode=="denied"?403:mode=="email"?403:503;
        // A well-formed but wrong client is rejected by the strict reader too.
        Assert.Equal(expected,(int)(await f.Get("/review")).StatusCode);
        Assert.Equal(expected,(int)(await f.Post("/accept",f.Command())).StatusCode);
        await using var db=f.Core.Context();Assert.Single(await db.CompanyMemberships.ToArrayAsync());
    }
    [Fact]
    public async Task Wrong_browser_or_replayed_callback_cannot_complete_an_intent()
    {
        await using var f=await Fixture.Start();await f.Issue();await f.Prepare();
        var start=await f.Get("/start");var state=QueryHelpers.ParseQuery(start.Headers.Location!.Query)["state"].ToString();
        var before=new Dictionary<string,string>(f.Cookies);
        f.Cookies.Remove(ProductInvitationAcceptanceFlow.IntentCookie);
        Assert.Equal(HttpStatusCode.Forbidden,(await f.Callback(state)).StatusCode);
        f.Cookies.Clear();foreach(var pair in before)f.Cookies.Add(pair.Key,pair.Value);
        Assert.Equal(HttpStatusCode.Redirect,(await f.Callback(state)).StatusCode);
        f.Cookies.Clear();foreach(var pair in before)f.Cookies.Add(pair.Key,pair.Value);
        Assert.Equal(HttpStatusCode.Forbidden,(await f.Callback(state)).StatusCode);
        await using var db=f.Core.Context();Assert.Single(await db.CompanyMemberships.ToArrayAsync());
    }
    [Fact]
    public async Task Prepare_requires_browser_nonce_and_cancel_revokes_the_authenticated_grant()
    {
        await using var f=await Fixture.Start();await f.Issue();
        Assert.Equal(HttpStatusCode.Forbidden,(await f.Post("/prepare",new(){["token"]=f.Token})).StatusCode);
        await f.Prepare();await f.SignIn();await f.ReadSession();
        Assert.Equal(HttpStatusCode.NoContent,(await f.Post("/cancel",new())).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized,(await f.Get("/review")).StatusCode);
        Assert.False((await f.Json(await f.Get("/session"))).GetProperty("verified").GetBoolean());
    }
    [Fact]
    public async Task Default_off_endpoints_never_create_browser_state()
    {
        await using var f=await Fixture.Start(enabled:false);
        foreach(var path in new[]{"/session","/start","/review"})Assert.Equal(HttpStatusCode.ServiceUnavailable,(await f.Get(path)).StatusCode);
        Assert.Equal(HttpStatusCode.ServiceUnavailable,(await f.Post("/prepare",new(){["token"]=new string('x',43)})).StatusCode);
        Assert.Empty(f.Cookies);
    }
    [Fact]
    public async Task Ordinary_signin_cannot_reuse_an_invitation_handoff_or_accept_company_access()
    {
        await using var f=await Fixture.Start();await f.Issue();await f.Prepare();
        var start=await f.Send("GET",ProductIdentity.Prefix+"/start");
        var query=QueryHelpers.ParseQuery(start.Headers.Location!.Query);Assert.Equal("product:identity",query["scope"].ToString());
        Assert.Equal(HttpStatusCode.Redirect,(await f.Callback(query["state"].ToString())).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized,(await f.Get("/review")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized,(await f.Post("/accept",f.Command())).StatusCode);
        await using var db=f.Core.Context();Assert.Single(await db.CompanyMemberships.ToArrayAsync());
    }
    [Fact]
    public async Task Browser_nonce_and_invitation_cookies_cannot_be_substituted_for_each_other()
    {
        await using var f=await Fixture.Start();await f.Issue();await f.Prepare();await f.ReadSession();
        var intent=f.Cookies[ProductInvitationAcceptanceFlow.IntentCookie];var bootstrap=f.Cookies[ProductInvitationAcceptanceFlow.BootstrapCookie];
        f.Cookies[ProductInvitationAcceptanceFlow.IntentCookie]=bootstrap;
        Assert.Equal(HttpStatusCode.BadRequest,(await f.Get("/start")).StatusCode);
        f.Cookies[ProductInvitationAcceptanceFlow.IntentCookie]=intent;
        f.Cookies[ProductInvitationAcceptanceFlow.BootstrapCookie]=intent;
        Assert.Equal(HttpStatusCode.Forbidden,(await f.Post("/prepare",new(){["token"]=f.Token})).StatusCode);
    }
    [Fact]
    public async Task Inviter_revocation_while_reading_a_license_denies_review_and_acceptance()
    {
        await using var f=await Fixture.Start();await f.Issue();await f.Prepare();await f.SignIn();await f.ReadSession();
        f.State.BeforeReturn=async()=>{
            await using var db=f.Core.Context();var owner=await db.CompanyMemberships.SingleAsync(x=>x.Id==f.Core.Owner.Member);
            owner.IsActive=false;owner.Revision++;await db.SaveChangesAsync();
        };
        Assert.Equal(HttpStatusCode.Forbidden,(await f.Get("/review")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,(await f.Post("/accept",f.Command())).StatusCode);
        await using var inspect=f.Core.Context();Assert.Single(await inspect.CompanyMemberships.ToArrayAsync());Assert.Single(await inspect.CompanyInvitationReceipts.ToArrayAsync());
    }
    private sealed class Provider:HttpMessageHandler
    {
        internal string Mode="";
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken cancellation)
        {
            object data;
            if(request.RequestUri!.AbsolutePath.EndsWith("/token"))data=new{access_token="synthetic.invitation.access",token_type="Bearer",expires_in=300,scope="product:identity product:invitation-recipient"};
            else if(request.RequestUri.AbsolutePath.EndsWith("/subject"))data=new{data=new{schema_version=1,issuer=ProductCommercialLicense.Issuer,subject="43",product_code="kuubu",verified=true,operational_access_granted=false}};
            else {
                if(Mode=="denied")return Task.FromResult(new HttpResponseMessage(HttpStatusCode.Forbidden));
                if(Mode=="unavailable")return Task.FromResult(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));
                var now=DateTimeOffset.UtcNow;
                data=new{data=new{schema_version=1,issuer=ProductCommercialLicense.Issuer,subject="43",client_id=Mode=="client"?Guid.NewGuid().ToString("D"):Client,
                    product_code="kuubu",purpose="company-invitation-v1",verified_email=Mode=="email"?"other@example.test":"invited+person@example.test",email_verified=Mode!="unverified",
                    observed_at=Mode=="expired"?now.AddMinutes(-1):now,proof_until=Mode=="expired"?now.AddSeconds(-30):now.AddSeconds(30),operational_access_granted=false}};
            }
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK){Content=new StringContent(JsonSerializer.Serialize(data),Encoding.UTF8,"application/json")});
        }
    }
    private const string Client="391bdeaa-87b3-44b3-8e64-2110dcc1a564";
    private sealed class Fixture(WebApplication app,HttpClient client,CoreFixture core,ProductCompanyTeamHttpTests.LicenseState state,Provider channel):IAsyncDisposable
    {
        internal CoreFixture Core=>core;internal ProductCompanyTeamHttpTests.LicenseState State=>state;internal Provider Channel=>channel;
        internal readonly Dictionary<string,string> Cookies=new();internal string? Csrf;internal string Token="";
        internal static async Task<Fixture> Start(bool enabled=true,int? admissionUnits=null)
        {
            var core=await CoreFixture.Start();var state=new ProductCompanyTeamHttpTests.LicenseState(core);var provider=new Provider();
            var keys=Path.Combine(Path.GetTempPath(),"kuubu-invitation-http-keys-"+Guid.NewGuid().ToString("N"));
            var intents=Path.Combine(Path.GetTempPath(),"kuubu-invitation-http-state-"+Guid.NewGuid().ToString("N"));
            foreach(var path in new[]{keys,intents}){Directory.CreateDirectory(path);if(!OperatingSystem.IsWindows())File.SetUnixFileMode(path,UnixFileMode.UserRead|UnixFileMode.UserWrite|UnixFileMode.UserExecute);}
            var builder=WebApplication.CreateBuilder();builder.WebHost.UseTestServer();builder.Logging.ClearProviders();
            builder.Configuration.AddInMemoryCollection(new Dictionary<string,string?>{
                ["ERP:ProductIdentity:Enabled"]="true",["ERP:ProductLicensing:Enabled"]="true",["ERP:CompanyTeam:Enabled"]="true",
                ["ERP:CompanyInvitationAcceptance:Enabled"]=enabled.ToString(),["ERP:CompanyInvitationAcceptance:IntentPath"]=intents,
                ["ERP:ProductIdentity:DataProtectionPath"]=keys,["ERP:ProductIdentity:ClientId"]=Client,["ERP:ProductIdentity:ClientSecret"]="synthetic-invitation-secret"});
            builder.Services.AddScoped(_=>core.Context("untrusted-default"));
            builder.Services.AddAuthentication(LocalAuthenticationHandler.Scheme).AddScheme<LocalAuthOptions,LocalAuthenticationHandler>(LocalAuthenticationHandler.Scheme,_=>{});
            builder.Services.AddProductIdentity(builder.Configuration);builder.Services.AddAuthorization();
            if(admissionUnits is { } units)builder.Services.AddSingleton(sp=>new ProductInvitationIntentStore(intents,Client,
                sp.GetRequiredService<IDataProtectionProvider>(),TimeProvider.System,units));
            builder.Services.Configure<OAuthOptions>(ProductIdentity.OAuthScheme,o=>o.Backchannel=new HttpClient(provider,false));
            builder.Services.AddSingleton(new ProductInvitationRecipient(new HttpClient(provider,false),TimeProvider.System,Client));
            builder.Services.AddSingleton(new CoreCompanyTeamApi(tenant=>core.Context(tenant),state.Read,core.Clock));
            builder.Services.AddSingleton(sp=>new CoreCompanyInvitationAcceptanceApi(tenant=>core.Context(tenant),sp.GetRequiredService<CoreCompanyTeamApi>(),core.Clock,
                sp.GetRequiredService<IDataProtectionProvider>().CreateProtector(CoreCompanyInvitationsApi.DeliveryPurpose)));
            var app=builder.Build();app.UseProductIdentityOriginGuard();app.UseAuthentication();app.UseAuthorization();app.MapProductIdentity();
            app.MapGet("/erp-protected",()=>"records").RequireAuthorization();await app.StartAsync();
            var client=app.GetTestClient();client.BaseAddress=new Uri("https://app.kuubuerp.com");return new(app,client,core,state,provider);
        }
        internal async Task Issue()
        {
            var protection=app.Services.GetRequiredService<IDataProtectionProvider>().CreateProtector(CoreCompanyInvitationsApi.DeliveryPurpose);
            var result=await new CoreCompanyInvitations(tenant=>core.Context(tenant),core.Clock,protection).IssueAsync(core.Owner.Proof,
                new(core.Company,Guid.NewGuid(),"invited+person@example.test","member",true),core.License());
            Assert.Equal(CompanyInvitationStatus.Issued,result.Status);
            await using var db=core.Context();var row=await db.CompanyInvitations.SingleAsync();
            using var json=JsonDocument.Parse(protection.Unprotect(row.ProtectedDelivery));Token=new Uri(json.RootElement.GetProperty("review_url").GetString()!).Fragment[7..];
        }
        internal Task<HttpResponseMessage> Get(string path)=>Send("GET",ProductInvitationAcceptanceFlow.Prefix+path);
        internal Task<HttpResponseMessage> Callback(string state)=>Send("GET",ProductIdentity.Prefix+"/callback?code=synthetic-code&state="+Uri.EscapeDataString(state));
        internal async Task Prepare(){await ReadSession();var response=await Post("/prepare",new(){["token"]=Token});Assert.Equal(HttpStatusCode.Created,response.StatusCode);
            var cookie=response.Headers.GetValues("Set-Cookie").Last(x=>x.StartsWith(ProductInvitationAcceptanceFlow.IntentCookie+"="));
            Assert.Contains("secure",cookie,StringComparison.OrdinalIgnoreCase);Assert.Contains("httponly",cookie,StringComparison.OrdinalIgnoreCase);
            Assert.Contains("samesite=lax",cookie,StringComparison.OrdinalIgnoreCase);Assert.DoesNotContain(Token,cookie);}
        internal async Task ReadSession(){var body=await Json(await Get("/session"));Csrf=body.GetProperty("csrf_token").GetString();}
        internal async Task<string> SignIn(){var start=await Get("/start");Assert.Equal(HttpStatusCode.Redirect,start.StatusCode);
            var query=QueryHelpers.ParseQuery(start.Headers.Location!.Query);Assert.Equal("product:identity product:invitation-recipient",query["scope"].ToString());
            Assert.DoesNotContain(Token,start.Headers.Location.ToString());var result=await Callback(query["state"].ToString());Assert.Equal(HttpStatusCode.Redirect,result.StatusCode);return result.Headers.Location!.ToString();}
        internal Dictionary<string,object?> Command()=>new(){["command_key"]=Guid.NewGuid().ToString("D"),["expected_revision"]=1,["confirmed"]=true};
        internal Task<HttpResponseMessage> Post(string path,Dictionary<string,object?> value)=>PostRaw(path,JsonSerializer.Serialize(value));
        internal Task<HttpResponseMessage> PostRaw(string path,string value,string fault="")=>Send("POST",ProductInvitationAcceptanceFlow.Prefix+path,value,fault);
        internal async Task<HttpResponseMessage> Send(string method,string path,string? value=null,string fault="")
        {
            var request=new HttpRequestMessage(new HttpMethod(method),path);
            if(fault=="spoof-ip"){
                request.Headers.Add("X-Real-IP","203.0.113.19");request.Headers.Add("X-Forwarded-For","203.0.113.20");
                request.Headers.Add("Forwarded","for=203.0.113.21");
            }
            if(fault!="missing-cookie" && Cookies.Count>0)request.Headers.Add("Cookie",string.Join("; ",Cookies.Select(x=>x.Key+"="+x.Value)));
            if(method=="POST"){
                request.Content=new StringContent(value??"{}",Encoding.UTF8,"application/json");request.Headers.Add("Origin",fault=="foreign-origin"?"https://foreign.invalid":"https://app.kuubuerp.com");
                if(fault!="missing-header" && Csrf is not null)request.Headers.Add(ProductInvitationAcceptanceFlow.CsrfHeader,fault=="forged-header"?"forged":Csrf);
            }
            var response=await client.SendAsync(request);
            if(response.Headers.TryGetValues("Set-Cookie",out var cookies))foreach(var raw in cookies){var pair=raw.Split(';')[0].Split('=',2);if(pair[1].Length==0)Cookies.Remove(pair[0]);else Cookies[pair[0]]=pair[1];}
            return response;
        }
        internal async Task<JsonElement> Json(HttpResponseMessage response){using var json=JsonDocument.Parse(await response.Content.ReadAsStringAsync());return json.RootElement.Clone();}
        public async ValueTask DisposeAsync(){client.Dispose();await app.DisposeAsync();await core.DisposeAsync();}
    }
}
