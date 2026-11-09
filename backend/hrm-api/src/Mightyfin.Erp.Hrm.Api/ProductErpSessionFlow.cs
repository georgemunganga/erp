using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Mightyfin.Erp.Hrm.Infrastructure.Data;
using Npgsql;

namespace Mightyfin.Erp.Hrm.Api;

/// <summary>Explicit selected ERP session lifecycle, independent of default ERP auth.
/// Protected module routes must still be integrated before production activation.</summary>
internal static class ProductErpSessionFlow
{
    internal const string Cookie="__Host-kuubu_erp_session",Path="/erp-session";
    internal static bool Enabled(IConfiguration config)=>CoreOperationalCompanies.Enabled(config) && config.GetValue<bool>("ERP:ProductSessions:Enabled");
    internal static void Register(IServiceCollection services,IConfiguration config,string? connection)
    {
        if(!config.GetValue<bool>("ERP:ProductSessions:Enabled"))return;
        if(!Enabled(config))throw new InvalidOperationException("Product sessions require operational identity, company discovery and licensing.");
        try{if(string.IsNullOrWhiteSpace(connection) || string.IsNullOrWhiteSpace(new NpgsqlConnectionStringBuilder(connection).Database))throw new ArgumentException();}
        catch(ArgumentException){throw new InvalidOperationException("Product sessions require a configured Core database.");}
        var options=new DbContextOptionsBuilder<HrmDbContext>().UseNpgsql(connection,pg=>pg.MigrationsHistoryTable("__hrm_migrations","hrm")).Options;
        services.AddSingleton(provider=>new CoreProductSessionAdmission(t=>new HrmDbContext(options,new SessionTenant(t)),
            provider.GetRequiredService<IDataProtectionProvider>(),provider.GetRequiredService<ProductCommercialLicense>().ReadAsync,TimeProvider.System));
    }
    private sealed class SessionTenant(string tenant):ITenantAccessor{public string GetTenantId()=>tenant;}
    internal static string? ReadCookie(HttpContext http)
    {
        if(http.Request.Headers.Cookie.ToString().Split(';').Count(x=>x.Trim().StartsWith(Cookie+"=",StringComparison.Ordinal))!=1)return null;
        var token=http.Request.Cookies[Cookie];return ProductInvitationIntentStore.Opaque(token)?token:null;
    }
    private static CookieOptions Options(DateTimeOffset? expires=null)=>new(){Path="/",Secure=true,HttpOnly=true,SameSite=SameSiteMode.Lax,IsEssential=true,Expires=expires};
    private static void Clear(HttpContext http)=>http.Response.Cookies.Delete(Cookie,Options());
    private static bool Csrf(HttpContext http)
    {
        var service=http.RequestServices.GetRequiredService<ProductOperationalBrowser>();var browser=service.Read(http);
        return browser is not null && service.Csrf(http,browser);
    }
    private static async Task<ProductSessionStatus> RevokeCookie(HttpContext http)
    {
        var token=ReadCookie(http);if(token is null)return ProductSessionStatus.Denied;
        var ticket=await http.AuthenticateAsync(ProductOperationalFlow.Scheme);
        var browser=http.RequestServices.GetRequiredService<ProductOperationalBrowser>().Read(http);
        if(!ticket.Succeeded || browser is null || ticket.Properties is not {} properties || !properties.Items.TryGetValue(ProductOperationalFlow.PurposeKey,out var purpose) || purpose!=ProductOperationalFlow.Purpose
            || ticket.Principal?.FindFirstValue(ProductIdentity.IssuerClaim)!=ProductCommercialLicense.Issuer
            || ticket.Principal.FindFirstValue(ProductOperationalFlow.BindingClaim)!=browser.Binding
            || ticket.Principal.FindFirstValue(ProductOperationalFlow.HandleClaim) is not {} credential
            || ticket.Principal.FindFirstValue(ProductIdentity.SubjectClaim) is not {} subject)return ProductSessionStatus.Denied;
        return await http.RequestServices.GetRequiredService<CoreProductSessionAdmission>().RevokeAsync(token,credential,browser.Binding,subject,
            http.RequestServices.GetRequiredService<IConfiguration>()["ERP:OperationalIdentity:ClientId"]!,http.RequestAborted);
    }
    internal static async Task RevokeForParentLogoutAsync(HttpContext http)
    {
        if(!Enabled(http.RequestServices.GetRequiredService<IConfiguration>()))return;
        // Parent credential revocation remains authoritative if the DB is unavailable.
        // Never retain the browser token after the parent logout/start transition.
        try{await RevokeCookie(http);}finally{Clear(http);}
    }
    internal static void Map(RouteGroupBuilder parent,IConfiguration config)
    {
        var group=parent.MapGroup(Path);
        group.AddEndpointFilter(async(invocation,next)=>Enabled(config)?await next(invocation):Results.StatusCode(503));
        group.MapGet("/review",async(HttpContext http)=> {
            if(!ProductCompanyTeamFlow.CanonicalGuid(http.Request.Query["company_id"].ToString(),out var company)
                || !ProductCompanyTeamFlow.CanonicalGuid(http.Request.Query["link_id"].ToString(),out var link)
                || http.Request.Query["module"].ToString() is not ("hrm" or "procurement"))return Results.BadRequest();
            var identity=await ProductOperationalFlow.ObserveAsync(http);
            if(identity.Status!=OperationalCredentialStatus.Verified)return ProductOperationalFlow.Failure(identity.Status);
            var result=await http.RequestServices.GetRequiredService<CoreProductSessionAdmission>().ReviewAsync(identity,company,link,http.Request.Query["module"].ToString(),http.RequestAborted);
            if(result.Status!=ModuleAccessStatus.Authorized || result.Decision is not {} grant)return Failure(result.Status);
            return Results.Ok(new{company_id=company,module=grant.Module,grant_version_id=grant.VersionId,grant_revision=grant.Revision,
                scope=grant.AllCompanyLocations?"company":"locations",location_ids=grant.LocationIds,roles=grant.Roles,actions=grant.Actions,
                session_established=false,operational_access_granted=false});
        }).WithMetadata(new OperationalQueryPolicy(["company_id","link_id","module"])).AllowAnonymous();
        group.MapPost("/select",(Func<HttpContext,Task<IResult>>)Select).WithMetadata(new OperationalJsonCommand()).AllowAnonymous();
        group.MapGet("/session",async(HttpContext http)=> {
            var token=ReadCookie(http);if(token is null)return Results.Unauthorized();
            var identity=await ProductOperationalFlow.ObserveAsync(http);
            if(identity.Status!=OperationalCredentialStatus.Verified)return ProductOperationalFlow.Failure(identity.Status);
            var result=await http.RequestServices.GetRequiredService<CoreProductSessionAdmission>().ReadAsync(identity,token,http.RequestAborted);
            if(result.Status!=ModuleAccessStatus.Authorized || result.Scope is not {} scope)return Failure(result.Status);
            return Results.Ok(new{session_established=true,company_id=scope.CompanyId,module=scope.Module,scope=scope.AllCompanyLocations?"company":"locations",
                location_ids=scope.LocationIds,expires_at=result.ExpiresAt,operational_access_granted=false,next_step="erp_access_pending"});
        }).AllowAnonymous();
        group.MapPost("/logout",async(HttpContext http)=> {
            if(!Csrf(http))return Results.StatusCode(403);
            var result=await RevokeCookie(http);
            if(result==ProductSessionStatus.Unavailable)return Results.StatusCode(503);
            Clear(http);return Results.NoContent();
        }).AllowAnonymous();
    }
    private static IResult Failure(ModuleAccessStatus status)=>Results.StatusCode(status switch{ModuleAccessStatus.Invalid=>400,ModuleAccessStatus.Denied=>403,ModuleAccessStatus.Conflict=>409,_=>503});
    private static async Task<IResult> Select(HttpContext http)
    {
        if(!Csrf(http))return Results.StatusCode(403);
        const int limit=8192;if(http.Request.ContentLength>limit)return Results.StatusCode(413);
        var buffer=new byte[limit+1];var used=0;
        while(used<buffer.Length){var read=await http.Request.Body.ReadAsync(buffer.AsMemory(used),http.RequestAborted);if(read==0)break;used+=read;}
        if(used>limit)return Results.StatusCode(413);
        CoreErpSessionSelection selection;Guid key;
        try {
            using var json=JsonDocument.Parse(buffer.AsMemory(0,used),new JsonDocumentOptions{MaxDepth=3});var root=json.RootElement;
            ProductWorkspaceSetupAuthority.RejectDuplicates(root);
            string[] allowed=["company_id","link_id","module","grant_version_id","grant_revision","scope","location_ids","all_company_locations_confirmed","confirmed","command_key"];
            if(root.ValueKind!=JsonValueKind.Object || root.EnumerateObject().Count()!=allowed.Length || root.EnumerateObject().Any(x=>!allowed.Contains(x.Name))
                || !GuidValue(root,"company_id",out var company) || !GuidValue(root,"link_id",out var link) || !GuidValue(root,"grant_version_id",out var version)
                || !GuidValue(root,"command_key",out key) || !root.GetProperty("grant_revision").TryGetInt32(out var revision) || revision<1
                || root.GetProperty("module").GetString() is not ("hrm" or "procurement")
                || root.GetProperty("scope").GetString() is not ("company" or "locations") || root.GetProperty("confirmed").ValueKind!=JsonValueKind.True
                || root.GetProperty("all_company_locations_confirmed").ValueKind is not (JsonValueKind.True or JsonValueKind.False))return Results.BadRequest();
            var list=root.GetProperty("location_ids");if(list.ValueKind!=JsonValueKind.Array || list.GetArrayLength()>128)return Results.BadRequest();
            var ids=new Guid[list.GetArrayLength()];var i=0;
            foreach(var value in list.EnumerateArray())if(value.ValueKind!=JsonValueKind.String || !ProductCompanyTeamFlow.CanonicalGuid(value.GetString(),out ids[i++]))return Results.BadRequest();
            var all=root.GetProperty("scope").GetString()=="company";
            if(all!=(ids.Length==0) || ids.Distinct().Count()!=ids.Length || root.GetProperty("all_company_locations_confirmed").GetBoolean()!=all)return Results.BadRequest();
            selection=new(company,link,root.GetProperty("module").GetString()!,version,revision,all,ids,all);
        }catch(Exception error)when(error is JsonException or InvalidOperationException or KeyNotFoundException){return Results.BadRequest();}
        var identity=await ProductOperationalFlow.ObserveAsync(http);
        if(identity.Status!=OperationalCredentialStatus.Verified)return ProductOperationalFlow.Failure(identity.Status);
        var result=await http.RequestServices.GetRequiredService<CoreProductSessionAdmission>().IssueAsync(identity,selection,key,http.RequestAborted);
        if(result.Status is ProductSessionStatus.Issued or ProductSessionStatus.Existing && result.Session is {} session) {
            if(identity.Observation is not {IsCurrent:true} || identity.Material is null || session.ExpiresAt>identity.Material.ExpiresAt || session.ExpiresAt<=DateTimeOffset.UtcNow)return Results.StatusCode(503);
            http.Response.Cookies.Append(Cookie,session.Token,Options(session.ExpiresAt));
            return Results.Ok(new{session_established=true,company_id=selection.CompanyId,module=selection.Module,expires_at=session.ExpiresAt,
                operational_access_granted=false,next_step="erp_access_pending"});
        }
        return Results.StatusCode(result.Status switch{ProductSessionStatus.Invalid=>400,ProductSessionStatus.Denied or ProductSessionStatus.Revoked=>403,
            ProductSessionStatus.Conflict=>409,ProductSessionStatus.Limited=>429,_=>503});
    }
    private static bool GuidValue(JsonElement root,string name,out Guid value)=>ProductCompanyTeamFlow.CanonicalGuid(root.GetProperty(name).GetString(),out value);
}
