using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Cryptography.X509Certificates;
using System.Security.Authentication;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.OAuth;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Mightyfin.Erp.Hrm.Api;
using Mightyfin.Erp.Hrm.Infrastructure.Data;
using Npgsql;

namespace Mightyfin.Erp.Hrm.Tests;

public sealed class ModuleEditorBrowserFactAttribute:FactAttribute
{
    public ModuleEditorBrowserFactAttribute(){if(string.IsNullOrEmpty(Environment.GetEnvironmentVariable("KUUBU_MODULE_BROWSER_MANIFEST")))Skip="Requires an explicitly prepared private browser/Laravel/TLS/PostgreSQL fixture.";}
}
public sealed class ProductModuleAccessBrowserInteropTests
{
    [ModuleEditorBrowserFact]
    public async Task Branded_browser_setup_and_module_decisions_persist_through_real_Passport_and_PostgreSQL()
    {
        string Required(string name){var value=Environment.GetEnvironmentVariable("KUUBU_MODULE_BROWSER_"+name);Assert.True(value is not null && Path.IsPathFullyQualified(value));return value!;}
        var manifestPath=Required("MANIFEST");using var manifest=JsonDocument.Parse(await File.ReadAllTextAsync(manifestPath));
        string Field(string name)=>manifest.RootElement.GetProperty(name).GetString()!;
        Assert.Equal("greenwebb-loopback-interop-v1",Field("fixture_scope"));Assert.True(manifest.RootElement.GetProperty("setup_owner_enabled").GetBoolean());
        var root=Path.Combine(Path.GetDirectoryName(manifestPath)!,"module-core-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);
        var keys=Path.Combine(root,"keys");var grants=Path.Combine(root,"grants");var credentials=Path.Combine(root,"credentials");
        foreach(var path in new[]{keys,grants,credentials})Directory.CreateDirectory(path);
        var database=new NpgsqlConnectionStringBuilder(Environment.GetEnvironmentVariable("KUUBU_MODULE_BROWSER_POSTGRES"));
        Assert.Equal("127.0.0.1",database.Host);Assert.Equal(8135,database.Port);Assert.Equal("kuubu_session_test_control",database.Database);
        await using(var control=new NpgsqlConnection(database.ConnectionString)){
            await control.OpenAsync();database.Database="module_browser_"+Guid.NewGuid().ToString("N");
            await using var create=new NpgsqlCommand("CREATE DATABASE \""+database.Database+"\"",control);await create.ExecuteNonQueryAsync();
        }
        var options=new DbContextOptionsBuilder<HrmDbContext>().UseNpgsql(database.ConnectionString,pg=>pg.MigrationsHistoryTable("__hrm_migrations","hrm")).Options;
        HrmDbContext Context(string tenant)=>new(options,new FixedTenantAccessor(tenant));
        await using(var schema=Context("schema-only")){await CoreSchemaStartup.EnsureAsync(schema,"Owned module browser fixture",mayApply:true);Assert.False(schema.Database.HasPendingModelChanges());}
        await File.WriteAllTextAsync(Path.Combine(root,"native-database.json"),JsonSerializer.Serialize(new{database=database.Database,production_changes=false}));

        var tls=Required("TLS");using var rootCertificate=X509CertificateLoader.LoadCertificateFromFile(Path.Combine(tls,"root.pem"));
        using var certificate=X509CertificateLoader.LoadPkcs12FromFile(Path.Combine(tls,"commercial.pfx"),"",
            OperatingSystem.IsWindows()?X509KeyStorageFlags.DefaultKeySet:X509KeyStorageFlags.EphemeralKeySet);
        var transport=WebApplication.CreateBuilder();transport.Logging.ClearProviders();
        transport.WebHost.ConfigureKestrel(server=>server.Listen(IPAddress.Loopback,8143,listener=>listener.UseHttps(certificate)));
        await using var gateway=transport.Build();using var upstream=new HttpClient(new HttpClientHandler{AllowAutoRedirect=false,UseCookies=false,UseProxy=false}){Timeout=TimeSpan.FromSeconds(20)};
        var providerCalls=0;
        gateway.Run(async http=>{
            var path=http.Request.Path.Value!;
            if(!path.StartsWith("/api/v1/product-identity/",StringComparison.Ordinal)){http.Response.StatusCode=404;return;}
            Interlocked.Increment(ref providerCalls);
            using var request=new HttpRequestMessage(new HttpMethod(http.Request.Method),"http://127.0.0.1:8101"+path+http.Request.QueryString);
            if(HttpMethods.IsPost(http.Request.Method)){using var bytes=new MemoryStream();await http.Request.Body.CopyToAsync(bytes,http.RequestAborted);request.Content=new ByteArrayContent(bytes.ToArray());request.Content.Headers.ContentType=System.Net.Http.Headers.MediaTypeHeaderValue.Parse(http.Request.ContentType!);}
            foreach(var header in new[]{"Authorization","Accept"})if(http.Request.Headers.TryGetValue(header,out var value))request.Headers.TryAddWithoutValidation(header,value.ToArray());
            using var response=await upstream.SendAsync(request,http.RequestAborted);http.Response.StatusCode=(int)response.StatusCode;
            foreach(var header in response.Headers.Where(x=>x.Key is "Location" or "Cache-Control"))http.Response.Headers[header.Key]=header.Value.ToArray();
            http.Response.ContentType=response.Content.Headers.ContentType?.ToString();await response.Content.CopyToAsync(http.Response.Body,http.RequestAborted);
        });await gateway.StartAsync();
        HttpClient Channel(string expectedHost="commercial-api.greenwebb.tech"){
            var chain=new X509ChainPolicy{TrustMode=X509ChainTrustMode.CustomRootTrust,RevocationMode=X509RevocationMode.NoCheck};chain.CustomTrustStore.Add(rootCertificate);
            var handler=new SocketsHttpHandler{AllowAutoRedirect=false,UseCookies=false,UseProxy=false,SslOptions=new SslClientAuthenticationOptions{CertificateChainPolicy=chain},
                ConnectCallback=async(context,ct)=>{
                    Assert.Equal(expectedHost,context.DnsEndPoint.Host);Assert.Equal(443,context.DnsEndPoint.Port);
                    var socket=new Socket(AddressFamily.InterNetwork,SocketType.Stream,ProtocolType.Tcp);
                    try{await socket.ConnectAsync(IPAddress.Loopback,8143,ct);return new NetworkStream(socket,ownsSocket:true);}catch{socket.Dispose();throw;}
                }};
            return new(handler){Timeout=TimeSpan.FromSeconds(20)};
        }
        using(var preflight=Channel()){
            preflight.DefaultRequestHeaders.Accept.ParseAdd("application/json");
            using var response=await preflight.GetAsync(ProductOperationalSubject.Endpoint);
            Assert.Equal(HttpStatusCode.Unauthorized,response.StatusCode);
        }
        using(var negative=Channel("wrong-fixture.invalid")){
            var error=await Assert.ThrowsAsync<HttpRequestException>(()=>negative.GetAsync("https://wrong-fixture.invalid/api/v1/product-identity/operational-subject"));
            Assert.IsType<AuthenticationException>(error.InnerException);
        }
        var builder=WebApplication.CreateBuilder();builder.Logging.ClearProviders();builder.WebHost.UseTestServer().UseContentRoot(AppContext.BaseDirectory);
        builder.Configuration.AddInMemoryCollection(new Dictionary<string,string?>{
            ["ERP:ProductIdentity:Enabled"]="true",["ERP:ProductIdentity:ClientId"]=Field("client_id"),["ERP:ProductIdentity:ClientSecret"]=Field("client_secret"),["ERP:ProductIdentity:DataProtectionPath"]=keys,
            ["ERP:WorkspaceSetup:Enabled"]="true",["ERP:WorkspaceSetup:GrantPath"]=grants,["ERP:WorkspaceProvisioning:Enabled"]="true",["ERP:ProductLicensing:Enabled"]="true",
            ["ERP:OperationalIdentity:Enabled"]="true",["ERP:OperationalIdentity:ClientId"]=Field("operational_client_id"),["ERP:OperationalIdentity:ClientSecret"]=Field("operational_client_secret"),["ERP:OperationalIdentity:StatePath"]=credentials,
            ["ERP:OperationalCompanies:Enabled"]="true",["ERP:ModuleAccess:Enabled"]="true"});
        builder.Services.AddHttpContextAccessor();builder.Services.AddScoped<ITenantAccessor>(_=>new FixedTenantAccessor("untrusted-default"));
        builder.Services.AddDbContext<HrmDbContext>(o=>o.UseNpgsql(database.ConnectionString,pg=>pg.MigrationsHistoryTable("__hrm_migrations","hrm")));
        builder.Services.AddAuthentication(LocalAuthenticationHandler.Scheme).AddScheme<LocalAuthOptions,LocalAuthenticationHandler>(LocalAuthenticationHandler.Scheme,_=>{});
        builder.Services.AddAuthorization();builder.Services.AddProductIdentity(builder.Configuration);
        builder.Services.Configure<OAuthOptions>(ProductIdentity.OAuthScheme,o=>o.Backchannel=Channel());builder.Services.Configure<OAuthOptions>(ProductOperationalFlow.OAuthScheme,o=>o.Backchannel=Channel());
        foreach(var scheme in new[]{ProductIdentity.OAuthScheme,ProductOperationalFlow.OAuthScheme})builder.Services.PostConfigure<OAuthOptions>(scheme,o=>{
            var prior=o.Events.OnRemoteFailure;o.Events.OnRemoteFailure=async context=>{
                var errors=new List<object>();for(var error=context.Failure;error is not null;error=error.InnerException)errors.Add(new{kind=error.GetType().FullName,message=error.Message});
                await File.WriteAllTextAsync(Path.Combine(root,"private-callback-failure.json"),JsonSerializer.Serialize(new{scheme,errors}));await prior(context);
            };
        });
        builder.Services.AddSingleton(new ProductSetupPayers(Channel(),Field("client_id")));builder.Services.AddSingleton(new ProductWorkspaceSetupAuthority(Channel(),TimeProvider.System,Field("client_id")));
        builder.Services.AddSingleton(new ProductOperationalSubject(Channel(),TimeProvider.System,Field("operational_client_id")));builder.Services.AddSingleton(new ProductOperationalRefresh(Channel(),TimeProvider.System,Field("operational_client_id"),Field("operational_client_secret")));
        var machine=new ProductCommercialLicense(Channel(),TimeProvider.System,Field("licensing_client_id"),Field("licensing_secret"));builder.Services.AddSingleton(machine);
        builder.Services.AddSingleton(new CoreWorkspaceProvisioner(Context,TimeProvider.System));
        builder.Services.AddScoped(provider=>new CoreOperationalCompanies(provider.GetRequiredService<HrmDbContext>(),machine.ReadAsync,TimeProvider.System));
        builder.Services.AddSingleton(new CoreModuleAccessApi(Context,machine.ReadAsync,TimeProvider.System));
        await using var app=builder.Build();
        app.Use(async(http,next)=>{
            try{await next();}catch(Exception failure){
                var errors=new List<object>();for(var error=failure;error is not null;error=error.InnerException)errors.Add(new{kind=error.GetType().FullName,message=error.Message});
                await File.WriteAllTextAsync(Path.Combine(root,"private-core-failure.json"),JsonSerializer.Serialize(new{path=http.Request.Path.Value,errors}));throw;
            }
        });
        app.UseProductIdentityOriginGuard();app.UseAuthentication();app.UseAuthorization();
        app.MapProductIdentity();app.MapProductSetup();app.MapGet("/erp-protected",()=>"ERP").RequireAuthorization();
        app.MapGet("/fixture/grant-decision",async(HttpContext http)=>{
            http.Response.Headers.CacheControl="no-store";
            var proof=await ProductOperationalFlow.ObserveAsync(http);if(proof.Status!=OperationalCredentialStatus.Verified || proof.Observation is not {} person)return Results.Unauthorized();
            if(!ProductCompanyTeamFlow.CanonicalGuid(http.Request.Query["company_id"],out var company)||!ProductCompanyTeamFlow.CanonicalGuid(http.Request.Query["link_id"],out var link))return Results.BadRequest();
            await using var db=Context("untrusted-read");var candidate=await new CoreCompanyAccess(db,TimeProvider.System).FindCandidateAsync(ProductCommercialLicense.Issuer,person.Subject,link,company,http.RequestAborted);
            if(candidate is null)return Results.StatusCode(403);var license=await machine.ReadAsync(candidate.EnrollmentId,http.RequestAborted);
            if(license.Lease is null)return Results.StatusCode(403);
            var read=await new CoreModuleAccessReader(db,TimeProvider.System).ReadAsync(person,link,company,"hrm",license.Lease,http.RequestAborted);
            return read.Status==ModuleAccessStatus.Authorized?Results.Ok(new{authorized=true,branch_count=read.Decision!.LocationIds.Count,all_branches=read.Decision.AllCompanyLocations,entry=read.Decision.AllowsAction("hrm-access")}):Results.StatusCode(403);
        }).AllowAnonymous();
        await app.StartAsync();using var core=app.GetTestClient();core.BaseAddress=new Uri("https://app.kuubuerp.com");
        var bridgeBuilder=WebApplication.CreateBuilder();bridgeBuilder.Logging.ClearProviders();bridgeBuilder.WebHost.UseUrls("http://127.0.0.1:8121").ConfigureKestrel(o=>o.Limits.MaxRequestBodySize=65536);
        await using var bridge=bridgeBuilder.Build();
        bridge.Use(async(http,next)=>{
            try{await next();}catch(Exception failure){
                var errors=new List<object>();for(var error=failure;error is not null;error=error.InnerException)errors.Add(new{kind=error.GetType().FullName,message=error.Message});
                await File.WriteAllTextAsync(Path.Combine(root,"private-bridge-failure.json"),JsonSerializer.Serialize(new{path=http.Request.Path.Value,errors}));throw;
            }
        });
        bool Authorized(HttpContext http)=>IPAddress.IsLoopback(http.Connection.RemoteIpAddress!)&&http.Request.Headers["X-Kuubu-Test-Fixture"]==Field("fixture_marker");
        bridge.MapPost("/request",async(HttpContext http)=>{
            if(!Authorized(http))return Results.StatusCode(403);var input=await http.Request.ReadFromJsonAsync<BridgeInput>();
            if(input is null||input.Path.Length>8192||input.Method is not ("GET" or "POST")||!(input.Path.StartsWith(ProductIdentity.Prefix+"/",StringComparison.Ordinal)||input.Path.StartsWith("/fixture/grant-decision?",StringComparison.Ordinal)||input.Path=="/erp-protected"))return Results.BadRequest();
            using var request=new HttpRequestMessage(new HttpMethod(input.Method),"https://app.kuubuerp.com"+input.Path);if(input.Body is not null)request.Content=new ByteArrayContent(Convert.FromBase64String(input.Body));
            var allowed=new HashSet<string>(["Cookie","Origin","Accept","Content-Type","Sec-Fetch-Site",ProductOperationalBrowser.Header,"X-Kuubu-Setup-CSRF"],StringComparer.OrdinalIgnoreCase);
            foreach(var header in input.Headers.Where(x=>allowed.Contains(x.Key)))if(!request.Headers.TryAddWithoutValidation(header.Key,header.Value))request.Content?.Headers.TryAddWithoutValidation(header.Key,header.Value);
            using var response=await core.SendAsync(request,http.RequestAborted);
            return Results.Json(new{status=(int)response.StatusCode,headers=response.Headers.Concat(response.Content.Headers).ToDictionary(x=>x.Key,x=>x.Value.ToArray(),StringComparer.OrdinalIgnoreCase),body=Convert.ToBase64String(await response.Content.ReadAsByteArrayAsync())});
        });
        bridge.MapGet("/audit",async(HttpContext http)=>{
            if(!Authorized(http))return Results.StatusCode(403);await using var db=Context("audit-only");
            return Results.Json(new{companies=await db.WorkspaceBindings.IgnoreQueryFilters().CountAsync(),versions=await db.ModuleAccessVersions.IgnoreQueryFilters().CountAsync(),receipts=await db.ModuleAccessReceipts.IgnoreQueryFilters().CountAsync(),
                changes=await db.AuditEntries.IgnoreQueryFilters().CountAsync(x=>x.EntityType=="CoreModuleAccessVersion"),sessions=await db.LocalSessions.IgnoreQueryFilters().CountAsync(),roles=await db.TenantRoleAssignments.IgnoreQueryFilters().CountAsync()});
        });
        bridge.MapPost("/suspend-fixture",async(HttpContext http)=>{
            if(!Authorized(http))return Results.StatusCode(403);using var request=new HttpRequestMessage(HttpMethod.Post,"http://127.0.0.1:8101/__fixture/suspend-subscription");request.Headers.Add("X-Kuubu-Test-Fixture",Field("fixture_marker"));
            using var response=await upstream.SendAsync(request,http.RequestAborted);Assert.Equal(HttpStatusCode.NoContent,response.StatusCode);return Results.NoContent();
        });await bridge.StartAsync();
        var start=new ProcessStartInfo("node"){UseShellExecute=false,RedirectStandardOutput=true,RedirectStandardError=true};
        foreach(var item in new[]{Required("RUNNER"),"--manifest",manifestPath,"--website",Required("WEBSITE"),"--output",Required("OUTPUT"),"--playwright",Required("PLAYWRIGHT")})start.ArgumentList.Add(item);
        using var child=Process.Start(start)!;var stdout=child.StandardOutput.ReadToEndAsync();var stderr=child.StandardError.ReadToEndAsync();
        try{await child.WaitForExitAsync(new CancellationTokenSource(TimeSpan.FromMinutes(8)).Token);}finally{if(!child.HasExited){child.Kill(true);await child.WaitForExitAsync();}}
        await File.WriteAllTextAsync(Path.Combine(root,"browser-child.log"),(await stdout)+(await stderr));Assert.True(child.ExitCode==0,"Inspect retained private browser stage diagnostics.");
        await using(var db=Context("final-audit")){
            Assert.Equal(2,await db.WorkspaceBindings.IgnoreQueryFilters().CountAsync());Assert.Equal(3,await db.ModuleAccessVersions.IgnoreQueryFilters().CountAsync());Assert.Equal(3,await db.ModuleAccessReceipts.IgnoreQueryFilters().CountAsync());
            Assert.Equal(3,await db.AuditEntries.IgnoreQueryFilters().CountAsync(x=>x.EntityType=="CoreModuleAccessVersion"));Assert.Equal(3,await db.OutboxMessages.IgnoreQueryFilters().CountAsync(x=>x.EventType=="core.company.module.access.changed.v1"));
            Assert.Empty(await db.LocalSessions.IgnoreQueryFilters().ToArrayAsync());Assert.Empty(await db.TenantRoleAssignments.IgnoreQueryFilters().ToArrayAsync());Assert.Empty(await db.LocalCredentialLinks.IgnoreQueryFilters().ToArrayAsync());
            Assert.All(await db.LocalUsers.IgnoreQueryFilters().ToArrayAsync(),user=>{Assert.Null(user.PasswordHash);Assert.Null(user.Email);Assert.Equal("",user.RolesCsv);});
        }
        Assert.True(providerCalls>0);await File.WriteAllTextAsync(Path.Combine(root,"acceptance.json"),JsonSerializer.Serialize(new{actual_browser=true,actual_Laravel_Passport=true,actual_PostgreSQL=true,normal_fixed_origin_TLS=true,
            transport="CDP fixed-origin adapter to real loopback services; not deployed browser TLS/ingress",wrong_tls_hostname_rejected=true,provider_calls=providerCalls,ERP_sessions_created=0,production_changes=false}));
        NpgsqlConnection.ClearAllPools();
    }
    private sealed record BridgeInput(string Method,string Path,Dictionary<string,string[]> Headers,string? Body);
}
