using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.OAuth;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Mightyfin.Erp.Hrm.Api;
using Mightyfin.Erp.Hrm.Infrastructure.Data;

namespace Mightyfin.Erp.Hrm.Tests;

public sealed class RealBrowserSetupFactAttribute : FactAttribute
{
    public RealBrowserSetupFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("KUUBU_REAL_BROWSER_SETUP_FIXTURE")))
            Skip = "Requires an explicitly prepared private browser/provider/Core fixture.";
    }
}

public sealed class ProductSetupBrowserInteropTests
{
    [RealBrowserSetupFact]
    public async Task Branded_browser_login_consent_setup_and_retry_use_the_real_provider_and_Core()
    {
        string Required(string name) { var value=Environment.GetEnvironmentVariable(name); Assert.True(value is not null && Path.IsPathFullyQualified(value)); return value!; }
        var manifestPath=Required("KUUBU_REAL_BROWSER_SETUP_FIXTURE");
        var runner=Required("KUUBU_REAL_BROWSER_SETUP_RUNNER");
        var website=Required("KUUBU_REAL_BROWSER_SETUP_WEBSITE");
        var output=Required("KUUBU_REAL_BROWSER_SETUP_OUTPUT");
        var playwright=Required("KUUBU_REAL_BROWSER_SETUP_PLAYWRIGHT");
        Assert.False(Directory.Exists(output)); Assert.True(File.Exists(runner));
        using var manifest=JsonDocument.Parse(await File.ReadAllTextAsync(manifestPath));
        string Field(string name)=>manifest.RootElement.GetProperty(name).GetString()!;
        Assert.Equal("greenwebb-loopback-interop-v1",Field("fixture_scope"));
        Assert.True(manifest.RootElement.GetProperty("setup_owner_enabled").GetBoolean());
        var root=Path.Combine(Path.GetDirectoryName(manifestPath)!,"browser-core-"+Guid.NewGuid().ToString("N"));
        var keys=Path.Combine(root,"keys");var grants=Path.Combine(root,"grants");
        foreach(var p in new[]{root,keys,grants}) {Directory.CreateDirectory(p);if(!OperatingSystem.IsWindows())File.SetUnixFileMode(p,UnixFileMode.UserRead|UnixFileMode.UserWrite|UnixFileMode.UserExecute);}
        await using var connection=new SqliteConnection("Data Source="+Path.Combine(root,"core.sqlite"));await connection.OpenAsync();
        var options=new DbContextOptionsBuilder<HrmDbContext>().UseSqlite(connection).Options;
        HrmDbContext Context(string tenant)=>new(options,new FixedTenantAccessor(tenant));
        await using(var db=Context("schema-only"))await db.Database.EnsureCreatedAsync();
        var builder=WebApplication.CreateBuilder();builder.WebHost.UseTestServer().UseContentRoot(AppContext.BaseDirectory);builder.Logging.ClearProviders();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string,string?>{
            ["ERP:ProductIdentity:Enabled"]="true",["ERP:WorkspaceSetup:Enabled"]="true",["ERP:WorkspaceProvisioning:Enabled"]="true",["ERP:ProductLicensing:Enabled"]="true",
            ["ERP:ProductIdentity:ClientId"]=Field("client_id"),["ERP:ProductIdentity:ClientSecret"]=Field("client_secret"),
            ["ERP:ProductIdentity:DataProtectionPath"]=keys,["ERP:WorkspaceSetup:GrantPath"]=grants});
        builder.Services.AddHttpContextAccessor();builder.Services.AddScoped<ITenantAccessor>(_=>new FixedTenantAccessor("untrusted-default"));
        builder.Services.AddDbContext<HrmDbContext>(o=>o.UseSqlite(connection));
        builder.Services.AddAuthentication(LocalAuthenticationHandler.Scheme).AddScheme<LocalAuthOptions,LocalAuthenticationHandler>(LocalAuthenticationHandler.Scheme,_=>{});
        builder.Services.AddAuthorization();builder.Services.AddProductIdentity(builder.Configuration);
        HttpClient Channel()=>new(new ProductSetupProviderInteropTests.RealProviderChannel()){Timeout=TimeSpan.FromSeconds(20)};
        builder.Services.Configure<OAuthOptions>(ProductIdentity.OAuthScheme,o=>o.Backchannel=Channel());
        builder.Services.AddSingleton(new ProductSetupPayers(Channel(),Field("client_id")));
        builder.Services.AddSingleton(new ProductWorkspaceSetupAuthority(Channel(),TimeProvider.System,Field("client_id")));
        builder.Services.AddSingleton(new ProductCommercialLicense(Channel(),TimeProvider.System,Field("licensing_client_id"),Field("licensing_secret")));
        builder.Services.AddSingleton(new CoreWorkspaceProvisioner(Context,TimeProvider.System));
        await using var app=builder.Build();app.UseProductIdentityOriginGuard();app.UseAuthentication();app.UseAuthorization();
        app.MapProductIdentity();app.MapProductSetup();app.MapGet("/erp-protected",()=>"records").RequireAuthorization();
        await app.StartAsync();using var core=app.GetTestClient();core.BaseAddress=new Uri("https://app.kuubuerp.com");

        // Test-only transport adapter. Browser uses the actual reviewed HTTPS
        // origins/cookie rules; production origin/TLS checks are never changed.
        var transport=WebApplication.CreateBuilder();transport.Logging.ClearProviders();
        transport.WebHost.UseUrls("http://127.0.0.1:8121").ConfigureKestrel(o=>o.Limits.MaxRequestBodySize=65536);
        await using var bridge=transport.Build();
        bool Authorized(HttpContext http)=>IPAddress.IsLoopback(http.Connection.RemoteIpAddress!) && http.Request.Headers["X-Kuubu-Test-Fixture"].ToString()==Field("fixture_marker");
        async Task<IResult> Dispatch(HttpContext http)
        {
            if(!Authorized(http))return Results.StatusCode(403);
            var input=await http.Request.ReadFromJsonAsync<BridgeInput>();
            if(input is null || input.Path.Length>8192 || (!input.Path.StartsWith(ProductIdentity.Prefix+"/",StringComparison.Ordinal) && input.Path!="/erp-protected")
                || input.Method is not ("GET" or "POST"))return Results.BadRequest();
            using var request=new HttpRequestMessage(new HttpMethod(input.Method),"https://app.kuubuerp.com"+input.Path);
            if(input.Body is not null)request.Content=new ByteArrayContent(Convert.FromBase64String(input.Body));
            var allowed=new HashSet<string>(["Cookie","Origin","Accept","Content-Type","X-Kuubu-Setup-CSRF"],StringComparer.OrdinalIgnoreCase);
            foreach(var header in input.Headers.Where(h=>allowed.Contains(h.Key)))
                if(!request.Headers.TryAddWithoutValidation(header.Key,header.Value))request.Content?.Headers.TryAddWithoutValidation(header.Key,header.Value);
            using var response=await core.SendAsync(request);
            var headers=response.Headers.Concat(response.Content.Headers).ToDictionary(h=>h.Key,h=>h.Value.ToArray(),StringComparer.OrdinalIgnoreCase);
            return Results.Json(new {status=(int)response.StatusCode,headers,body=Convert.ToBase64String(await response.Content.ReadAsByteArrayAsync())});
        }
        async Task<IResult> Audit(HttpContext http)
        {
            if(!Authorized(http))return Results.StatusCode(403);
            await using var db=Context("audit-only");
            return Results.Json(new{companies=await db.WorkspaceBindings.IgnoreQueryFilters().CountAsync(),sessions=await db.LocalSessions.IgnoreQueryFilters().CountAsync(),roles=await db.TenantRoleAssignments.IgnoreQueryFilters().CountAsync()});
        }
        bridge.MapPost("/request",(Delegate)(Func<HttpContext,Task<IResult>>)Dispatch);
        bridge.MapGet("/audit",(Delegate)(Func<HttpContext,Task<IResult>>)Audit);
        await bridge.StartAsync();
        var start=new ProcessStartInfo("node"){UseShellExecute=false,RedirectStandardOutput=true,RedirectStandardError=true};
        foreach(var value in new[]{runner,"--manifest",manifestPath,"--website",website,"--output",output,"--playwright",playwright})start.ArgumentList.Add(value);
        using var child=Process.Start(start)!;var stdout=child.StandardOutput.ReadToEndAsync();var stderr=child.StandardError.ReadToEndAsync();
        try{await child.WaitForExitAsync(new CancellationTokenSource(TimeSpan.FromMinutes(3)).Token);}
        finally{if(!child.HasExited){child.Kill(true);await child.WaitForExitAsync();}}
        await File.WriteAllTextAsync(Path.Combine(root,"browser-child.log"),(await stdout)+(await stderr));
        Assert.True(child.ExitCode==0,"Browser acceptance failed; inspect private fixture diagnostics.");
        await using(var db=Context("final-audit")){
            Assert.Single(await db.WorkspaceBindings.IgnoreQueryFilters().ToArrayAsync());
            Assert.Single(await db.LocalUsers.IgnoreQueryFilters().ToArrayAsync());
            Assert.Single(await db.ExternalIdentityLinks.IgnoreQueryFilters().ToArrayAsync());
            Assert.Empty(await db.LocalSessions.IgnoreQueryFilters().ToArrayAsync());
            Assert.Empty(await db.TenantRoleAssignments.IgnoreQueryFilters().ToArrayAsync());
            Assert.Single(await db.AuditEntries.IgnoreQueryFilters().ToArrayAsync());
            Assert.Single(await db.OutboxMessages.IgnoreQueryFilters().ToArrayAsync());
        }
    }
    private sealed record BridgeInput(string Method,string Path,Dictionary<string,string[]> Headers,string? Body);
}
