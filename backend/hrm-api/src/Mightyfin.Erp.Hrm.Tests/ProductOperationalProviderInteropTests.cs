using System.Net;
using System.Net.Http.Json;
using System.Runtime.Versioning;
using System.Security.Claims;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Mightyfin.Erp.Hrm.Api;
using Mightyfin.Erp.Hrm.Infrastructure.Data;

namespace Mightyfin.Erp.Hrm.Tests;

public sealed class OperationalProviderWireFactAttribute:FactAttribute
{
    public OperationalProviderWireFactAttribute()
    {
        if(!OperatingSystem.IsLinux() || Environment.GetEnvironmentVariable("KUUBU_OPERATIONAL_PROVIDER_FIXTURE")!="/evidence/provider/operational-interop.json")
            Skip="Requires an explicitly owned isolated Laravel/Passport/private-TLS operational fixture.";
    }
}

/// <summary>Actual fixed-origin HTTP/TLS clients and Passport; no provider-response replacement.</summary>
[SupportedOSPlatform("linux")]
public sealed class ProductOperationalProviderInteropTests
{
    [OperationalProviderWireFact]
    public async Task Real_Passport_callback_refresh_and_revocation_preserve_person_only_admission()
    {
        using var metadata=JsonDocument.Parse(await File.ReadAllTextAsync("/evidence/provider/operational-interop.json"));
        string Field(string name)=>metadata.RootElement.GetProperty(name).GetString()!;
        Assert.Equal("kuubu-operational-provider-wire-v1",Field("fixture_scope"));
        var root="/evidence/core";var keys=root+"/keys";var state=root+"/credentials";
        Assert.False(Directory.Exists(root));
        foreach(var path in new[]{root,keys,state}){Directory.CreateDirectory(path);File.SetUnixFileMode(path,UnixFileMode.UserRead|UnixFileMode.UserWrite|UnixFileMode.UserExecute);}

        using var certificate=X509CertificateLoader.LoadPkcs12FromFile("/evidence/tls/commercial.pfx","",X509KeyStorageFlags.EphemeralKeySet);
        var transport=WebApplication.CreateBuilder();transport.Logging.ClearProviders();
        transport.WebHost.ConfigureKestrel(server=>server.Listen(IPAddress.Loopback,443,listen=>listen.UseHttps(certificate)));
        await using var gateway=transport.Build();
        using var upstream=new HttpClient(new HttpClientHandler{AllowAutoRedirect=false,UseCookies=false,UseProxy=false}){Timeout=TimeSpan.FromSeconds(15)};
        var tokens=0;var observations=0;var rotations=0;var duplicateRefresh=false;var seenRefresh=new HashSet<string>();
        var allowed=new HashSet<string>{"/api/v1/identity/csrf","/api/v1/identity/login","/api/v1/identity/products/kuubu/enroll",
            "/api/v1/product-identity/authorize","/api/v1/product-identity/token","/api/v1/product-identity/operational-subject"};
        gateway.Run(async http=> {
            var path=http.Request.Path.Value!;if(!allowed.Contains(path)){http.Response.StatusCode=404;return;}
            if(path.EndsWith("/operational-subject",StringComparison.Ordinal))observations++;
            if(path.EndsWith("/token",StringComparison.Ordinal))tokens++;
            using var request=new HttpRequestMessage(new HttpMethod(http.Request.Method),"http://kuubu-operational-provider01:8101"+path+http.Request.QueryString);
            // JsonContent may use chunked transfer with no Content-Length.
            // Forward every POST body instead of silently dropping streamed JSON.
            if(HttpMethods.IsPost(http.Request.Method)) {
                using var buffer=new MemoryStream();await http.Request.Body.CopyToAsync(buffer,http.RequestAborted);var bytes=buffer.ToArray();
                if(path.EndsWith("/token",StringComparison.Ordinal)) {
                    var form=QueryHelpers.ParseQuery(System.Text.Encoding.UTF8.GetString(bytes));
                    if(form["grant_type"]=="refresh_token"){rotations++;if(!seenRefresh.Add(form["refresh_token"].ToString()))duplicateRefresh=true;}
                }
                request.Content=new ByteArrayContent(bytes);request.Content.Headers.ContentType=System.Net.Http.Headers.MediaTypeHeaderValue.Parse(http.Request.ContentType!);
            }
            foreach(var header in new[]{"Authorization","Accept","Cookie","X-CSRF-TOKEN"})
                if(http.Request.Headers.TryGetValue(header,out var value))request.Headers.TryAddWithoutValidation(header,value.ToArray());
            using var response=await upstream.SendAsync(request,http.RequestAborted);
            http.Response.StatusCode=(int)response.StatusCode;
            foreach(var header in response.Headers.Where(x=>x.Key is "Set-Cookie" or "Location" or "Cache-Control"))http.Response.Headers[header.Key]=header.Value.ToArray();
            http.Response.ContentType=response.Content.Headers.ContentType?.ToString();await response.Content.CopyToAsync(http.Response.Body,http.RequestAborted);
        });
        await gateway.StartAsync();

        var builder=WebApplication.CreateBuilder();builder.Logging.ClearProviders();builder.WebHost.UseTestServer().UseContentRoot(AppContext.BaseDirectory);
        builder.Configuration.AddInMemoryCollection(new Dictionary<string,string?>{
            ["ERP:ProductIdentity:Enabled"]="true",["ERP:ProductIdentity:ClientId"]=Field("setup_client_id"),["ERP:ProductIdentity:ClientSecret"]=Field("setup_client_secret"),
            ["ERP:ProductIdentity:DataProtectionPath"]=keys,["ERP:OperationalIdentity:Enabled"]="true",["ERP:OperationalIdentity:ClientId"]=Field("client_id"),
            ["ERP:OperationalIdentity:ClientSecret"]=Field("client_secret"),["ERP:OperationalIdentity:StatePath"]=state});
        builder.Services.AddHttpContextAccessor();builder.Services.AddScoped<ITenantAccessor>(_=>new FixedTenantAccessor("untrusted-default"));
        builder.Services.AddDbContext<HrmDbContext>(options=>options.UseInMemoryDatabase("operational-wire-only"));
        builder.Services.AddAuthentication(LocalAuthenticationHandler.Scheme).AddScheme<LocalAuthOptions,LocalAuthenticationHandler>(LocalAuthenticationHandler.Scheme,_=>{});
        builder.Services.AddAuthorization();builder.Services.AddProductIdentity(builder.Configuration);
        await using var app=builder.Build();app.UseProductIdentityOriginGuard();app.UseAuthentication();app.UseAuthorization();
        ProductOperationalFlow.Map(app);app.MapGet("/erp-protected",()=>"ERP").RequireAuthorization();await app.StartAsync();
        using var core=app.GetTestClient();core.BaseAddress=new Uri("https://app.kuubuerp.com");
        var cookies=new Dictionary<string,string>();
        async Task<HttpResponseMessage> Send(HttpMethod method,string path,string? csrf=null) {
            using var request=new HttpRequestMessage(method,path);
            if(cookies.Count>0)request.Headers.Add("Cookie",string.Join("; ",cookies.Select(x=>x.Key+"="+x.Value)));
            if(method==HttpMethod.Post){request.Content=JsonContent.Create(new{});request.Headers.Add("Origin","https://app.kuubuerp.com");if(csrf is not null)request.Headers.Add(ProductOperationalBrowser.Header,csrf);}
            var response=await core.SendAsync(request);
            if(response.Headers.TryGetValues("Set-Cookie",out var values))foreach(var value in values){var pair=value.Split(';')[0].Split('=',2);if(pair[1]=="")cookies.Remove(pair[0]);else cookies[pair[0]]=pair[1];}
            return response;
        }
        using var bootstrap=await Send(HttpMethod.Get,ProductOperationalFlow.Prefix+"/bootstrap");
        var csrf=(await bootstrap.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("csrf_token").GetString()!;
        Assert.Empty(Directory.GetFiles(state));
        using var start=await Send(HttpMethod.Post,ProductOperationalFlow.Prefix+"/start",csrf);Assert.Equal(HttpStatusCode.OK,start.StatusCode);Assert.Null(start.Headers.Location);
        var authorizationUrl=new Uri((await start.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("authorization_url").GetString()!);
        var intent=QueryHelpers.ParseQuery(authorizationUrl.Query);
        Assert.Equal("https://www.kuubuerp.com",authorizationUrl.GetLeftPart(UriPartial.Authority));
        Assert.Equal("product:identity product:erp-session",intent["scope"]);

        // This HTTP actor explicitly logs in and opts in. It is not a browser UI qualification.
        using var person=new HttpClient(new HttpClientHandler{AllowAutoRedirect=false,UseCookies=true,UseProxy=false}){BaseAddress=new Uri(ProductCommercialLicense.Issuer)};
        async Task<string> Csrf(){using var response=await person.GetAsync("/api/v1/identity/csrf");Assert.Equal(HttpStatusCode.OK,response.StatusCode);return (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("csrf_token").GetString()!;}
        async Task<HttpResponseMessage> Post(string path,object body,string token){var request=new HttpRequestMessage(HttpMethod.Post,path){Content=JsonContent.Create(body)};request.Headers.Add("X-CSRF-TOKEN",token);return await person.SendAsync(request);}
        using(var login=await Post("/api/v1/identity/login",new{email="owner@example.test",password="AcceptancePassword123!"},await Csrf()))Assert.Equal(HttpStatusCode.OK,login.StatusCode);
        using(var denied=await person.GetAsync("/api/v1/product-identity/authorize"+authorizationUrl.Query))Assert.Equal(HttpStatusCode.Forbidden,denied.StatusCode);
        using(var enrolled=await Post("/api/v1/identity/products/kuubu/enroll",new{consent=true},await Csrf()))Assert.Equal(HttpStatusCode.Created,enrolled.StatusCode);
        using var authorization=await person.GetAsync("/api/v1/product-identity/authorize"+authorizationUrl.Query);Assert.Equal(HttpStatusCode.Redirect,authorization.StatusCode);
        var callback=authorization.Headers.Location!;Assert.Equal("https://app.kuubuerp.com"+ProductOperationalFlow.Callback,callback.GetLeftPart(UriPartial.Path));
        Assert.Equal(intent["state"],QueryHelpers.ParseQuery(callback.Query)["state"]);
        using var accepted=await Send(HttpMethod.Get,callback.PathAndQuery);Assert.Equal(HttpStatusCode.Redirect,accepted.StatusCode);
        Assert.Equal(1,tokens);Assert.Equal(1,observations);Assert.Equal(0,rotations);
        var identity=app.Services.GetRequiredService<IOptionsMonitor<CookieAuthenticationOptions>>().Get(ProductOperationalFlow.Scheme).TicketDataFormat.Unprotect(cookies[ProductOperationalFlow.Cookie])!;
        Assert.Empty(identity.Properties.GetTokens());Assert.Equal(Field("subject"),identity.Principal.FindFirstValue(ProductIdentity.SubjectClaim));
        Assert.DoesNotContain(identity.Principal.Claims,x=>x.Type is "tenant" or ClaimTypes.Role or ClaimTypes.Email);
        using(var current=await Send(HttpMethod.Get,ProductOperationalFlow.Prefix+"/session")) {
            Assert.Equal(HttpStatusCode.OK,current.StatusCode);var json=await current.Content.ReadFromJsonAsync<JsonElement>();
            Assert.False(json.GetProperty("operational_access_granted").GetBoolean());Assert.Equal("company_selection",json.GetProperty("next_step").GetString());
        }
        Assert.Equal(2,tokens);Assert.Equal(1,rotations);Assert.Equal(2,observations);Assert.False(duplicateRefresh);
        using(var denied=await Send(HttpMethod.Get,"/erp-protected"))Assert.Equal(HttpStatusCode.Unauthorized,denied.StatusCode);
        using(var revoke=new HttpRequestMessage(HttpMethod.Post,"http://kuubu-operational-provider01:8101/__fixture/revoke")){
            revoke.Headers.Add("X-Kuubu-Test-Fixture",Field("fixture_marker"));using var response=await upstream.SendAsync(revoke);Assert.Equal(HttpStatusCode.NoContent,response.StatusCode);
        }
        using(var denied=await Send(HttpMethod.Get,ProductOperationalFlow.Prefix+"/session"))Assert.Equal(HttpStatusCode.Unauthorized,denied.StatusCode);
        Assert.Equal(3,tokens);Assert.Equal(2,rotations);Assert.Equal(3,observations);Assert.False(duplicateRefresh);
        using(var denied=await Send(HttpMethod.Get,ProductOperationalFlow.Prefix+"/session"))Assert.Equal(HttpStatusCode.Unauthorized,denied.StatusCode);
        Assert.Equal(3,tokens);Assert.Equal(3,observations);
        await using(var scope=app.Services.CreateAsyncScope()) {
            var db=scope.ServiceProvider.GetRequiredService<HrmDbContext>();Assert.Empty(await db.LocalSessions.IgnoreQueryFilters().ToArrayAsync());Assert.Empty(await db.LocalUsers.IgnoreQueryFilters().ToArrayAsync());
        }
        Assert.Single(Directory.GetFiles(state,"*.credentials"));Assert.Single(Directory.GetFiles(state,"*.revoked"));
        await File.WriteAllTextAsync("/evidence/wire-result.json",JsonSerializer.Serialize(new{actual_Passport_HTTP_TLS=true,actual_browser_tested=false,
            access_token_lifetime_fixture_seconds=20,token_requests=tokens,refresh_requests=rotations,current_person_requests=observations,
            refresh_token_reuse=duplicateRefresh,ERP_sessions_created=0,company_access_granted=false,production_changes=false}));
    }
}
