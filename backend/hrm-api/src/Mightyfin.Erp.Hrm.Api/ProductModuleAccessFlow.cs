using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Mightyfin.Erp.Hrm.Infrastructure.Data;
using Npgsql;

namespace Mightyfin.Erp.Hrm.Api;

internal sealed record OperationalJsonCommand;
internal static class ProductModuleAccessFlow
{
    private const string Path="/module-access";
    internal static bool Enabled(IConfiguration config)=>CoreOperationalCompanies.Enabled(config) && config.GetValue<bool>("ERP:ModuleAccess:Enabled");
    internal static void Register(IServiceCollection services,IConfiguration config,string? connection)
    {
        if(!config.GetValue<bool>("ERP:ModuleAccess:Enabled"))return;
        if(!Enabled(config))throw new InvalidOperationException("Module access requires operational identity, company discovery and licensing.");
        try{if(string.IsNullOrWhiteSpace(connection) || string.IsNullOrWhiteSpace(new NpgsqlConnectionStringBuilder(connection).Database))throw new ArgumentException();}
        catch(ArgumentException){throw new InvalidOperationException("Module access requires a configured Core database.");}
        var options=new DbContextOptionsBuilder<HrmDbContext>().UseNpgsql(connection,pg=>pg.MigrationsHistoryTable("__hrm_migrations","hrm")).Options;
        services.AddSingleton(provider=>new CoreModuleAccessApi(tenant=>new HrmDbContext(options,new ModuleTenant(tenant)),
            provider.GetRequiredService<ProductCommercialLicense>().ReadAsync,TimeProvider.System));
    }
    private sealed class ModuleTenant(string tenant):ITenantAccessor{public string GetTenantId()=>tenant;}
    internal static void Map(RouteGroupBuilder parent,IConfiguration config)
    {
        var group=parent.MapGroup(Path);
        group.AddEndpointFilter(async(invocation,next)=>Enabled(config)?await next(invocation):Results.StatusCode(503));
        group.MapGet("/members",async(HttpContext http)=> {
            if(!Selectors(http,out var company,out var link) || !ProductCompanyTeamFlow.Cursor(http.Request.Query,out var after))return Results.BadRequest();
            var proof=await ProductOperationalFlow.ObserveAsync(http);
            if(proof.Observation is not {} person || proof.Status!=OperationalCredentialStatus.Verified)return ProductOperationalFlow.Failure(proof.Status);
            var result=await http.RequestServices.GetRequiredService<CoreModuleAccessApi>().MembersAsync(person,new(ProductCommercialLicense.Issuer,person.Subject,link),company,after,http.RequestAborted);
            return result.Status==ModuleAccessStatus.Authorized?Results.Ok(new{members=result.Items!.Select(Member),next_cursor=result.Next,operational_access_granted=false}):Failure(result.Status);
        }).WithMetadata(new OperationalQueryPolicy(["company_id","link_id","after"])).AllowAnonymous();
        group.MapGet("/locations",async(HttpContext http)=> {
            if(!Selectors(http,out var company,out var link) || !ProductCompanyTeamFlow.Cursor(http.Request.Query,out var after))return Results.BadRequest();
            var proof=await ProductOperationalFlow.ObserveAsync(http);
            if(proof.Observation is not {} person || proof.Status!=OperationalCredentialStatus.Verified)return ProductOperationalFlow.Failure(proof.Status);
            var result=await http.RequestServices.GetRequiredService<CoreModuleAccessApi>().LocationsAsync(person,new(ProductCommercialLicense.Issuer,person.Subject,link),company,after,http.RequestAborted);
            return result.Status==ModuleAccessStatus.Authorized?Results.Ok(new{locations=result.Items!.Select(Location),next_cursor=result.Next,operational_access_granted=false}):Failure(result.Status);
        }).WithMetadata(new OperationalQueryPolicy(["company_id","link_id","after"])).AllowAnonymous();
        group.MapGet("/review",async(HttpContext http)=> {
            if(!Selectors(http,out var company,out var link) || !ProductCompanyTeamFlow.CanonicalGuid(http.Request.Query["membership_id"].ToString(),out var member)
                || http.Request.Query["module"].ToString() is not ("hrm" or "procurement"))return Results.BadRequest();
            var proof=await ProductOperationalFlow.ObserveAsync(http);
            if(proof.Observation is not {} person || proof.Status!=OperationalCredentialStatus.Verified)return ProductOperationalFlow.Failure(proof.Status);
            var module=http.Request.Query["module"].ToString();
            var result=await http.RequestServices.GetRequiredService<CoreModuleAccessApi>().ReviewAsync(person,new(ProductCommercialLicense.Issuer,person.Subject,link),company,member,module,http.RequestAborted);
            if(result.Status!=ModuleAccessStatus.Authorized)return Failure(result.Status);
            var grant=result.Grant;
            return Results.Ok(new{member=Member(result.Member!),module,roles=result.Roles,actions=result.Actions,
                grant=grant is null?null:new{version_id=grant.VersionId,revision=grant.Revision,membership_revision=grant.MembershipRevision,state=grant.State,
                    roles=grant.Roles,actions=grant.Actions,scope=grant.Scope,locations=grant.Locations.Select(Location),requires_reconfirmation=grant.RequiresReconfirmation},operational_access_granted=false});
        }).WithMetadata(new OperationalQueryPolicy(["company_id","link_id","membership_id","module"])).AllowAnonymous();
        group.MapPost("/commands",(Func<HttpContext,Task<IResult>>)Execute).WithMetadata(new OperationalJsonCommand()).AllowAnonymous();
    }
    private static bool Selectors(HttpContext http,out Guid company,out Guid link)
    {
        var companyValid=ProductCompanyTeamFlow.CanonicalGuid(http.Request.Query["company_id"].ToString(),out company);
        var linkValid=ProductCompanyTeamFlow.CanonicalGuid(http.Request.Query["link_id"].ToString(),out link);return companyValid && linkValid;
    }
    private static object Member(CompanyTeamMember x)=>new{membership_id=x.MembershipId,display_name=x.DisplayName,role=x.Role,
        membership_active=x.MembershipActive,identity_available=x.IdentityAvailable,revision=x.Revision};
    private static object Location(ModuleAccessLocation x)=>new{location_id=x.Id,name=x.Name,available=x.Available};
    private static IResult Failure(ModuleAccessStatus status)=>Results.StatusCode(status switch{ModuleAccessStatus.Invalid=>400,ModuleAccessStatus.Denied=>403,ModuleAccessStatus.Conflict=>409,_=>503});
    private static async Task<IResult> Execute(HttpContext http)
    {
        var browserService=http.RequestServices.GetRequiredService<ProductOperationalBrowser>();var browser=browserService.Read(http);
        if(browser is null || !browserService.Csrf(http,browser))return Results.StatusCode(403);
        const int limit=32768;
        if(http.Request.ContentLength>limit)return Results.StatusCode(413);
        var buffer=new byte[limit+1];var used=0;
        while(used<buffer.Length){var count=await http.Request.Body.ReadAsync(buffer.AsMemory(used),http.RequestAborted);if(count==0)break;used+=count;}
        if(used>limit)return Results.StatusCode(413);
        ModuleAccessCommand command;Guid link;
        try {
            using var json=JsonDocument.Parse(buffer.AsMemory(0,used),new JsonDocumentOptions{MaxDepth=4});var root=json.RootElement;
            ProductWorkspaceSetupAuthority.RejectDuplicates(root);
            string[] allowed=["company_id","link_id","membership_id","membership_revision","module","expected_revision","command_key","operation","roles","actions","scope","location_ids","all_company_locations_confirmed","confirmed"];
            if(root.ValueKind!=JsonValueKind.Object || root.EnumerateObject().Any(x=>!allowed.Contains(x.Name)) || root.EnumerateObject().Count()!=allowed.Length
                || !GuidValue(root,"company_id",out var company) || !GuidValue(root,"link_id",out link) || !GuidValue(root,"membership_id",out var member)
                || !GuidValue(root,"command_key",out var key) || !root.GetProperty("membership_revision").TryGetInt32(out var revision)
                || !root.GetProperty("expected_revision").TryGetInt32(out var expected)
                || root.GetProperty("confirmed").ValueKind!=JsonValueKind.True || root.GetProperty("all_company_locations_confirmed").ValueKind is not (JsonValueKind.True or JsonValueKind.False))return Results.BadRequest();
            var operation=root.GetProperty("operation").GetString();if(operation is not ("grant" or "revoke"))return Results.BadRequest();
            var roles=Strings(root.GetProperty("roles"),16);var actions=Strings(root.GetProperty("actions"),256);var locations=Strings(root.GetProperty("location_ids"),128);
            if(roles is null || actions is null || locations is null)return Results.BadRequest();
            var ids=new Guid[locations.Length];for(var i=0;i<ids.Length;i++)if(!ProductCompanyTeamFlow.CanonicalGuid(locations[i],out ids[i]))return Results.BadRequest();
            command=new(company,member,revision,root.GetProperty("module").GetString()!,expected,key,operation=="revoke",roles,actions,
                root.GetProperty("scope").GetString()!,ids,root.GetProperty("all_company_locations_confirmed").GetBoolean(),true);
            if(NormalizedModuleAccess.Read(command) is null)return Results.BadRequest();
        }catch(Exception error)when(error is JsonException or InvalidOperationException or KeyNotFoundException){return Results.BadRequest();}
        var proof=await ProductOperationalFlow.ObserveAsync(http);
        if(proof.Observation is not {} person || proof.Status!=OperationalCredentialStatus.Verified)return ProductOperationalFlow.Failure(proof.Status);
        var result=await http.RequestServices.GetRequiredService<CoreModuleAccessApi>().ExecuteAsync(person,new(ProductCommercialLicense.Issuer,person.Subject,link),command,http.RequestAborted);
        var status=result.Status switch{ModuleAccessStatus.Changed or ModuleAccessStatus.Existing or ModuleAccessStatus.Unchanged=>200,ModuleAccessStatus.Invalid=>400,ModuleAccessStatus.Denied=>403,ModuleAccessStatus.Conflict=>409,_=>503};
        return Results.Json(new{status=result.Status.ToString(),version_id=result.VersionId,revision=result.Revision,state=result.State,operational_access_granted=false},statusCode:status);
    }
    private static bool GuidValue(JsonElement root,string name,out Guid value)=>ProductCompanyTeamFlow.CanonicalGuid(root.GetProperty(name).GetString(),out value);
    private static string[]? Strings(JsonElement element,int limit)
    {
        if(element.ValueKind!=JsonValueKind.Array || element.GetArrayLength()>limit)return null;
        var result=new List<string>();foreach(var item in element.EnumerateArray()){
            if(item.ValueKind!=JsonValueKind.String || item.GetString() is not {} value || value.Length>128)return null;result.Add(value);
        }return result.ToArray();
    }
}
