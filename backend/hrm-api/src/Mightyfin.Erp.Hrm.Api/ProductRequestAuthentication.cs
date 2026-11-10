using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;
using Mightyfin.Erp.Hrm.Infrastructure.Data;

namespace Mightyfin.Erp.Hrm.Api;

internal sealed record ProductIdentityRequestBoundary;
internal sealed record ProductWorkspaceRequestPolicy;
internal sealed class ProductAuthOptions:AuthenticationSchemeOptions {}

/// <summary>Private request proof; never accept a scope or tenant from claims,
/// headers, JSON or the legacy empty-branch assignment convention.</summary>
internal static class ProductRequestContext
{
    private static readonly object Key=new();
    private sealed record Proof(CoreErpRequestScope Scope,Func<HrmDbContext?,CancellationToken,Task<CoreProductSessionState>> Recheck);
    internal static void Set(HttpContext http,CoreErpRequestScope scope,Func<HrmDbContext?,CancellationToken,Task<CoreProductSessionState>> recheck)=>http.Items[Key]=new Proof(scope,recheck);
    internal static CoreErpRequestScope? Current(HttpContext http)
    {
        if(http.User.Identity?.AuthenticationType!=ProductRequestAuthentication.Scheme || http.User.Identity.IsAuthenticated!=true
            || !http.Items.TryGetValue(Key,out var value) || value is not Proof proof || !proof.Scope.IsCurrent
            || http.User.FindFirstValue("tenant")!=proof.Scope.TenantId
            || http.User.FindFirstValue(ClaimTypes.NameIdentifier)!=proof.Scope.PrincipalId.ToString("D"))return null;
        return proof.Scope;
    }
    internal static async Task<ModuleAccessStatus> RecheckAsync(HttpContext http,CancellationToken ct,HrmDbContext? db=null)
    {
        var current=Current(http);if(current is null || !http.Items.TryGetValue(Key,out var value) || value is not Proof proof)return ModuleAccessStatus.Denied;
        var final=await proof.Recheck(db,ct);
        if(final.Status!=ModuleAccessStatus.Authorized || final.Scope is not {IsCurrent:true} next)return final.Status==ModuleAccessStatus.Authorized?ModuleAccessStatus.Denied:final.Status;
        return current.IsCurrent && next.TenantId==current.TenantId && next.CompanyId==current.CompanyId && next.PrincipalId==current.PrincipalId
            && next.Module==current.Module && next.AllCompanyLocations==current.AllCompanyLocations && next.LocationIds.Order().SequenceEqual(current.LocationIds.Order())
            ?ModuleAccessStatus.Authorized:ModuleAccessStatus.Denied;
    }
}
internal static class ProductRequestAuthentication
{
    internal const string Scheme="kuubu-erp";
    internal const string SessionContextHeader = "X-Kuubu-Session-Context";

    // A browser precondition, never a credential. Domain separation keeps the
    // returned marker distinct from both the opaque cookie and its stored hash.
    internal static string SessionContext(string token)
        => ProductInvitationIntentStore.Hash("kuubu-browser-context-v1\n" + token);
    internal static bool Enabled(IConfiguration config)=>(config["ERP:AuthMode"]??config["HRM:AuthMode"]??"local").Equals("product",StringComparison.OrdinalIgnoreCase);
    internal static void Add(IServiceCollection services,IConfiguration config)
    {
        if(!ProductErpSessionFlow.Enabled(config))throw new InvalidOperationException("Product authentication requires the explicit product session lifecycle and its prerequisites.");
        services.AddAuthentication(Scheme).AddScheme<ProductAuthOptions,ProductRequestAuthenticationHandler>(Scheme,_=>{});
    }
    internal static void UseProductRequestBoundary(this WebApplication app)
    {
        if(!Enabled(app.Configuration))return;
        app.Use(async(http,next)=> {
            // Only the selected product handler may establish request authority.
            http.User=new ClaimsPrincipal(new ClaimsIdentity());
            var endpoint=http.GetEndpoint();
            var callback=HttpMethods.IsGet(http.Request.Method) && (http.Request.Path==ProductIdentity.Prefix+"/callback" || http.Request.Path==ProductOperationalFlow.Callback);
            var health=endpoint is not null && HttpMethods.IsGet(http.Request.Method) && (http.Request.Path=="/health/live" || http.Request.Path=="/health/ready");
            if(callback || health || endpoint?.Metadata.GetMetadata<ProductIdentityRequestBoundary>() is not null){await next(http);return;}
            http.Response.Headers.CacheControl="no-store";http.Response.Headers.Pragma="no-cache";
            if(endpoint?.Metadata.GetMetadata<ProductWorkspaceRequestPolicy>() is null && endpoint?.Metadata.GetMetadata<ProductWorkerRequestPolicy>() is null){http.Response.StatusCode=endpoint is null?404:403;return;}
            var origin=app.Configuration["ERP:ProductIdentity:PublicOrigin"]??"https://app.kuubuerp.com";
            if(http.Request.Scheme+"://"+http.Request.Host!=origin || http.Request.Headers["Sec-Fetch-Site"]=="cross-site") {http.Response.StatusCode=403;return;}
            string[] selectors=["Authorization","X-Tenant","X-Tenant-Id","X-Shell-Entity","X-Shell-Location","X-Branch-Id","X-All-Branches"];
            if(selectors.Any(x=>http.Request.Headers.ContainsKey(x))){http.Response.StatusCode=400;return;}
            var worker=endpoint.Metadata.GetMetadata<ProductWorkerRequestPolicy>();
            if(worker is not null){await ProductWorkerFlow.CheckBoundary(http,worker,next);return;}
            if(!HttpMethods.IsGet(http.Request.Method) || http.Request.Query.Count!=0){http.Response.StatusCode=400;return;}
            await next(http);
        });
    }
}
internal sealed class ProductRequestAuthenticationHandler(IOptionsMonitor<ProductAuthOptions> options,ILoggerFactory logger,UrlEncoder encoder)
    :AuthenticationHandler<ProductAuthOptions>(options,logger,encoder)
{
    private int failureStatus=401;
    private AuthenticateResult Deny(int status){failureStatus=status;return AuthenticateResult.Fail("Product request authority unavailable.");}
    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if(Context.GetEndpoint()?.Metadata.GetMetadata<ProductWorkspaceRequestPolicy>() is null && Context.GetEndpoint()?.Metadata.GetMetadata<ProductWorkerRequestPolicy>() is null)return AuthenticateResult.NoResult();
        var token=ProductErpSessionFlow.ReadCookie(Context);if(token is null)return Deny(401);
        if (Request.Headers.TryGetValue(ProductRequestAuthentication.SessionContextHeader, out var expected)
            && (expected.Count != 1 || expected[0] != ProductRequestAuthentication.SessionContext(token)))
            return Deny(409);
        if(!Context.RequestServices.GetRequiredService<OperationalRequestBudget>().Budget.TryAcquire(ProductInvitationGuestBudget.Operation.Session,Context.Connection.RemoteIpAddress))return Deny(429);
        var identity=await ProductOperationalFlow.ObserveAsync(Context);
        if(identity.Status!=OperationalCredentialStatus.Verified)return Deny(identity.Status is OperationalCredentialStatus.Unavailable or OperationalCredentialStatus.Busy?503:401);
        var admission=Context.RequestServices.GetRequiredService<CoreProductSessionAdmission>();
        var state=await admission.ReadAsync(identity,token,Context.RequestAborted);
        if(state.Status!=ModuleAccessStatus.Authorized || state.Scope is not {IsCurrent:true} scope)return Deny(state.Status==ModuleAccessStatus.Unavailable?503:403);
        ProductRequestContext.Set(Context,scope,(db,ct)=>db is null?admission.ReadAsync(identity,token,ct):admission.ReadWithinAsync(db,identity,token,ct));
        var claims=new List<Claim>{new("tenant",scope.TenantId),new("sub",scope.PrincipalId.ToString("D")),
            new(ClaimTypes.NameIdentifier,scope.PrincipalId.ToString("D")),new("kuubu:company",scope.CompanyId.ToString("D")),new("kuubu:module",scope.Module)};
        claims.AddRange(scope.Roles.Select(x=>new Claim(ClaimTypes.Role,x)));
        return AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(new ClaimsIdentity(claims,ProductRequestAuthentication.Scheme,ClaimTypes.Name,ClaimTypes.Role)),Scheme.Name));
    }
    protected override Task HandleChallengeAsync(AuthenticationProperties properties){Response.StatusCode=failureStatus;return Task.CompletedTask;}
    protected override Task HandleForbiddenAsync(AuthenticationProperties properties){Response.StatusCode=403;return Task.CompletedTask;}
}
