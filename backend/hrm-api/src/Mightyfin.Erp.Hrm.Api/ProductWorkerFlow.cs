using System.Data;
using Microsoft.EntityFrameworkCore;
using Mightyfin.Erp.Hrm.Application;
using Mightyfin.Erp.Hrm.Application.Workers;
using Mightyfin.Erp.Hrm.Infrastructure.Data;

namespace Mightyfin.Erp.Hrm.Api;

internal enum ProductWorkerOperation { List,Detail,Create,Update,Archive }
internal sealed record ProductWorkerRequestPolicy(ProductWorkerOperation Operation)
{
    internal bool Writes=>Operation is ProductWorkerOperation.Create or ProductWorkerOperation.Update or ProductWorkerOperation.Archive;
}
internal sealed class ProductWorkerScope(IHttpContextAccessor http,IConfiguration config):IWorkerDataScope
{
    private readonly bool configured=ProductRequestAuthentication.Enabled(config);
    private CoreErpRequestScope? Scope=>http.HttpContext is {} context?ProductRequestContext.Current(context):null;
    public bool Enabled=>configured || http.HttpContext?.User.Identity?.AuthenticationType==ProductRequestAuthentication.Scheme;
    public bool IsCurrent=>Scope is {IsCurrent:true,Module:"hrm"} scope && scope.AllowsAction("hrm-access");
    public string TenantId=>Scope?.TenantId??"untrusted-worker-scope";
    public Guid CompanyId=>Scope?.CompanyId??Guid.Empty;
    public bool AllLocations=>Scope?.AllCompanyLocations==true;
    public Guid[] LocationIds=>Scope?.LocationIds.ToArray()??[];
}
internal static class ProductWorkerFlow
{
    internal static async Task CheckBoundary(HttpContext http,ProductWorkerRequestPolicy policy,RequestDelegate next)
    {
        var expected=policy.Operation switch{ProductWorkerOperation.List or ProductWorkerOperation.Detail=>"GET",ProductWorkerOperation.Update=>"PUT",_=>"POST"};
        if(http.Request.Method!=expected){http.Response.StatusCode=400;return;}
        string[] query=policy.Operation==ProductWorkerOperation.List?["search","status","legalEntityId","orgUnitId","locationId","workerType","grade","page","pageSize","includeArchived"]:[];
        if(http.Request.Query.Any(x=>!query.Contains(x.Key,StringComparer.OrdinalIgnoreCase) || x.Value.Count!=1)){http.Response.StatusCode=400;return;}
        if(!policy.Writes){await next(http);return;}
        var origin=http.RequestServices.GetRequiredService<IConfiguration>()["ERP:ProductIdentity:PublicOrigin"]??"https://app.kuubuerp.com";
        var browser=http.RequestServices.GetRequiredService<ProductOperationalBrowser>();var bound=browser.Read(http);
        if(http.Request.Headers.Origin.ToString()!=origin || !http.Request.HasJsonContentType() || bound is null || !browser.Csrf(http,bound)){http.Response.StatusCode=403;return;}
        const int limit=65536;if(http.Request.ContentLength>limit){http.Response.StatusCode=413;return;}
        var bytes=new byte[limit+1];var used=0;
        while(used<bytes.Length){var count=await http.Request.Body.ReadAsync(bytes.AsMemory(used),http.RequestAborted);if(count==0)break;used+=count;}
        if(used>limit){http.Response.StatusCode=413;return;}
        var original=http.Request.Body;using var body=new MemoryStream(bytes,0,used,false);http.Request.Body=body;
        try{await next(http);}finally{http.Request.Body=original;}
    }
    internal static RouteHandlerBuilder ProductWorker(this RouteHandlerBuilder endpoint,ProductWorkerOperation operation)
        =>endpoint.WithMetadata(new ProductWorkerRequestPolicy(operation)).AddEndpointFilter(Enforce);
    private static async ValueTask<object?> Enforce(EndpointFilterInvocationContext invocation,EndpointFilterDelegate next)
    {
        var http=invocation.HttpContext;var config=http.RequestServices.GetRequiredService<IConfiguration>();
        if(!ProductRequestAuthentication.Enabled(config) && http.User.Identity?.AuthenticationType!=ProductRequestAuthentication.Scheme)return await next(invocation);
        var scope=ProductRequestContext.Current(http);
        if(scope is null || scope.Module!="hrm" || !scope.AllowsAction("hrm-access"))return Results.StatusCode(403);
        var policy=http.GetEndpoint()!.Metadata.GetMetadata<ProductWorkerRequestPolicy>()!;
        var db=http.RequestServices.GetRequiredService<HrmDbContext>();if(!db.HasProductWorkerScope)return Results.StatusCode(503);
        await using var transaction=await db.Database.BeginTransactionAsync(db.Database.IsNpgsql()?IsolationLevel.ReadCommitted:IsolationLevel.Serializable,http.RequestAborted);
        try {
            if(policy.Writes && db.Database.IsNpgsql()) {
                var key="core.company.team.v1\n"+scope.TenantId+"\n"+scope.CompanyId.ToString("D");
                await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock(hashtextextended({key},0))",http.RequestAborted);
            }
            var before=await ProductRequestContext.RecheckAsync(http,http.RequestAborted,db);
            if(before!=ModuleAccessStatus.Authorized)return Results.StatusCode(before==ModuleAccessStatus.Unavailable?503:403);
            var result=await next(invocation);
            if(result is null && policy.Operation==ProductWorkerOperation.Detail)return Results.NotFound();
            if(result is IStatusCodeHttpResult status && status.StatusCode>=400)return result;
            var final=await ProductRequestContext.RecheckAsync(http,http.RequestAborted,db);
            if(final!=ModuleAccessStatus.Authorized || !scope.IsCurrent)return Results.StatusCode(final==ModuleAccessStatus.Unavailable?503:403);
            await transaction.CommitAsync(http.RequestAborted);return result;
        }catch(DomainException error){return Results.Json(new{error=error.Code},statusCode:DomainExceptionHttpStatus.For(error.Code));}
    }
}
