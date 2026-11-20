using System.Security.Claims;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.OAuth;
using Microsoft.AspNetCore.DataProtection;

namespace Mightyfin.Erp.Hrm.Api;

internal static class ProductInvitationAcceptanceFlow
{
    internal const string Prefix = ProductIdentity.Prefix+"/invitation";
    internal const string Purpose = "kuubu:invitation:purpose";
    internal const string PurposeValue = "company-invitation-v1";
    internal const string HandleClaim = "kuubu:invitation:handle";
    internal const string BindingProperty = "kuubu:invitation:binding";
    internal const string IntentCookie = "__Host-kuubu_invitation_intent";
    internal const string BootstrapCookie = "__Host-kuubu_invitation_csrf";
    internal const string CsrfHeader = "X-Kuubu-Invitation-CSRF";
    private sealed record BrowserState(string Handle, string Binding, DateTimeOffset ExpiresAt)
    { public override string ToString() => nameof(BrowserState); }
    internal static bool Enabled(IConfiguration config) => ProductCompanyTeamFlow.Enabled(config)
        && config.GetValue<bool>("ERP:CompanyInvitationAcceptance:Enabled");
    private static IDataProtector Protector(HttpContext http,string name) => http.RequestServices.GetRequiredService<IDataProtectionProvider>()
        .CreateProtector("Kuubu.CompanyInvitation.Browser.v1",name);
    private static CookieOptions Cookie(DateTimeOffset expires) => new() { Path="/",HttpOnly=true,Secure=true,SameSite=SameSiteMode.Lax,Expires=expires,IsEssential=true };
    private static BrowserState? State(HttpContext http,string name)
    {
        try {
            var raw=http.Request.Cookies[name]; if(raw is null || raw.Length>4096)return null;
            var value=JsonSerializer.Deserialize<BrowserState>(Protector(http,name).Unprotect(raw));
            return value is not null && ProductInvitationIntentStore.Opaque(value.Handle) && ProductInvitationIntentStore.Opaque(value.Binding)
                && value.ExpiresAt>DateTimeOffset.UtcNow && value.ExpiresAt<=DateTimeOffset.UtcNow.AddMinutes(5) ? value : null;
        } catch(Exception error) when(error is CryptographicException or JsonException) {return null;}
    }
    private static void Set(HttpContext http,string name,BrowserState state) => http.Response.Cookies.Append(name,
        Protector(http,name).Protect(JsonSerializer.Serialize(state)),Cookie(state.ExpiresAt));
    internal static void Revoke(HttpContext http)
    {
        var store=http.RequestServices.GetService<ProductInvitationIntentStore>();
        if(store is null && !http.Request.Cookies.ContainsKey(IntentCookie) && !http.Request.Cookies.ContainsKey(BootstrapCookie))return;
        if(State(http,IntentCookie) is { } state)store?.Revoke(state.Handle);
        http.Response.Cookies.Delete(IntentCookie,Cookie(DateTimeOffset.UnixEpoch));
        http.Response.Cookies.Delete(BootstrapCookie,Cookie(DateTimeOffset.UnixEpoch));
    }
    internal static async Task<string?> CompleteAsync(HttpContext http,AuthenticationProperties properties,
        string subject,string accessToken,DateTimeOffset expires)
    {
        if(State(http,IntentCookie) is not { } state || !properties.Items.TryGetValue(HandleClaim,out var expected) || expected!=state.Handle
            || !properties.Items.TryGetValue(BindingProperty,out var binding) || binding!=ProductInvitationIntentStore.Hash(state.Binding))return null;
        var store=http.RequestServices.GetRequiredService<ProductInvitationIntentStore>();
        var handle=await store.CompleteAsync(state.Handle,state.Binding,subject,accessToken,expires,http.RequestAborted);
        if(handle is null)return null;
        var grant=await store.ReadAsync(handle,state.Binding,http.RequestAborted);
        if(grant is null)return null;
        properties.ExpiresUtc=grant.ExpiresAt;
        properties.Items.Remove(HandleClaim);properties.Items.Remove(BindingProperty);
        Set(http,IntentCookie,new(handle,state.Binding,grant.ExpiresAt));
        http.Response.Cookies.Delete(BootstrapCookie,Cookie(DateTimeOffset.UnixEpoch));
        return handle;
    }
    private static async Task<ProductInvitationIntent?> Grant(HttpContext http)
    {
        var proof=await http.AuthenticateAsync(ProductIdentity.PendingScheme);
        if(!proof.Succeeded || proof.Principal?.FindFirstValue(ProductIdentity.IssuerClaim)!=ProductCommercialLicense.Issuer
            || proof.Properties is null || !proof.Properties.Items.TryGetValue(Purpose,out var purpose) || purpose!=PurposeValue
            || proof.Properties.Items.ContainsKey(ProductIdentity.SetupPurpose) || proof.Properties.Items.ContainsKey(ProductCompanyTeamFlow.Purpose)
            || State(http,IntentCookie) is not { } state || proof.Principal.FindFirstValue(HandleClaim)!=state.Handle)return null;
        var grant=await http.RequestServices.GetRequiredService<ProductInvitationIntentStore>().ReadAsync(state.Handle,state.Binding,http.RequestAborted);
        return grant?.Subject is { } subject && subject==proof.Principal.FindFirstValue(ProductIdentity.SubjectClaim) && ProductAccountHub.MatchesSubject(http,subject) ? grant : null;
    }
    private static bool Csrf(string expected,string supplied) => ProductInvitationIntentStore.Opaque(supplied)
        && CryptographicOperations.FixedTimeEquals(System.Text.Encoding.ASCII.GetBytes(expected),System.Text.Encoding.ASCII.GetBytes(supplied));
    internal static void MapProductInvitationAcceptance(this WebApplication app)
    {
        var group=app.MapGroup(Prefix).WithMetadata(new ProductIdentityRequestBoundary());
        group.AddEndpointFilter(async(context,next)=>Enabled(app.Configuration)?await next(context):Results.StatusCode(503));
        group.AddEndpointFilter(async(context,next)=>{
            var http=context.HttpContext;
            var policy=http.GetEndpoint()?.Metadata.GetMetadata<ProductInvitationGuestBudget.Policy>();
            if(policy is not null && !http.RequestServices.GetRequiredService<ProductInvitationGuestBudget>()
                .TryAcquire(policy.Value,http.Connection.RemoteIpAddress)) {
                http.Response.Headers.RetryAfter="60";
                return Results.StatusCode(429);
            }
            return await next(context);
        });
        group.MapGet("/session",async(HttpContext http)=>{
            if(http.Request.Query.Count!=0)return Results.BadRequest();
            if(await Grant(http) is { } grant)return Results.Ok(new{verified=true,purpose=PurposeValue,csrf_token=grant.Csrf,expires_at=grant.ExpiresAt,operational_access_granted=false});
            var state=State(http,BootstrapCookie)??new(ProductInvitationIntentStore.Random(),ProductInvitationIntentStore.Random(),DateTimeOffset.UtcNow.AddMinutes(5));
            Set(http,BootstrapCookie,state);
            return Results.Ok(new{verified=false,purpose=PurposeValue,csrf_token=state.Binding,expires_at=state.ExpiresAt,operational_access_granted=false});
        }).AllowAnonymous().WithMetadata(new ProductInvitationGuestBudget.Policy(ProductInvitationGuestBudget.Operation.Session));
        group.MapPost("/prepare",(Func<HttpContext,Task<IResult>>)(async http=>{
            var csrf=State(http,BootstrapCookie);
            if(csrf is null || !Csrf(csrf.Binding,http.Request.Headers[CsrfHeader].ToString()))return Results.StatusCode(403);
            var body=await Body(http,["token"]);if(body is null)return Results.BadRequest();
            var token=body.Value.GetProperty("token");if(token.ValueKind!=JsonValueKind.String || !ProductInvitationIntentStore.Opaque(token.GetString()))return Results.BadRequest();
            try {
                var store=http.RequestServices.GetRequiredService<ProductInvitationIntentStore>();var binding=ProductInvitationIntentStore.Random();
                var handle=await store.PrepareAsync(token.GetString()!,binding,http.RequestAborted);
                Revoke(http);
                var prior=await http.AuthenticateAsync(ProductIdentity.PendingScheme);
                if(prior.Principal?.FindFirstValue(ProductIdentity.SetupHandleClaim) is { } oldSetup)
                    http.RequestServices.GetService<ProductSetupGrantStore>()?.Revoke(oldSetup);
                await http.SignOutAsync(ProductIdentity.PendingScheme);
                Set(http,IntentCookie,new(handle,binding,DateTimeOffset.UtcNow.AddMinutes(5)));
                return Results.Json(new{continue_url=Prefix+"/start",operational_access_granted=false},statusCode:201);
            } catch(Exception error) when(error is IOException or UnauthorizedAccessException) {
                http.Response.Headers.RetryAfter="5";return Results.StatusCode(503);
            }
        })).AllowAnonymous().WithMetadata(new ProductInvitationGuestBudget.Policy(ProductInvitationGuestBudget.Operation.Prepare));
        group.MapGet("/start",(Func<HttpContext,Task<IResult>>)(async http=>{
            if(http.Request.Query.Count!=0 || State(http,IntentCookie) is not { } state)return Results.BadRequest();
            var value=await http.RequestServices.GetRequiredService<ProductInvitationIntentStore>().ReadAsync(state.Handle,state.Binding,http.RequestAborted);
            if(value is null || value.Subject is not null)return Results.StatusCode(403);
            var properties=new OAuthChallengeProperties{RedirectUri=ProductAccountHub.Landing(app.Configuration,"company-invitation","/accept-invitation"),IssuedUtc=DateTimeOffset.UtcNow,Scope=["product:identity","product:invitation-recipient"]};
            properties.Items[Purpose]=PurposeValue;properties.Items[HandleClaim]=state.Handle;properties.Items[BindingProperty]=ProductInvitationIntentStore.Hash(state.Binding);
            return Results.Challenge(properties,[ProductIdentity.OAuthScheme]);
        })).AllowAnonymous().WithMetadata(new ProductInvitationGuestBudget.Policy(ProductInvitationGuestBudget.Operation.Start));
        group.MapGet("/review",(Func<HttpContext,Task<IResult>>)(http=>Execute(http,accept:false))).AllowAnonymous();
        group.MapPost("/accept",(Func<HttpContext,Task<IResult>>)(http=>Execute(http,accept:true))).AllowAnonymous();
        group.MapPost("/cancel",(Func<HttpContext,Task<IResult>>)(async http=>{
            var grant=await Grant(http);var csrf=grant?.Csrf??State(http,BootstrapCookie)?.Binding;
            if(csrf is null || !Csrf(csrf,http.Request.Headers[CsrfHeader].ToString()))return Results.StatusCode(403);
            if(http.Request.Query.Count!=0)return Results.BadRequest();
            Revoke(http);await http.SignOutAsync(ProductIdentity.PendingScheme);return Results.NoContent();
        })).AllowAnonymous();
    }
    private static async Task<JsonElement?> Body(HttpContext http,string[] allowed)
    {
        if(http.Request.Query.Count!=0 || http.Request.ContentLength>2048)return null;
        var buffer=new byte[2049];var used=0;
        while(used<buffer.Length){var read=await http.Request.Body.ReadAsync(buffer.AsMemory(used),http.RequestAborted);if(read==0)break;used+=read;}
        if(used>2048)return null;
        try {
            using var json=JsonDocument.Parse(buffer.AsMemory(0,used),new JsonDocumentOptions{MaxDepth=3});
            var value=json.RootElement;ProductWorkspaceSetupAuthority.RejectDuplicates(value);
            return value.ValueKind==JsonValueKind.Object && value.EnumerateObject().Count()==allowed.Length
                && value.EnumerateObject().All(x=>allowed.Contains(x.Name)) ? value.Clone() : null;
        }catch(Exception error)when(error is JsonException or InvalidOperationException){return null;}
    }
    private static async Task<IResult> Execute(HttpContext http,bool accept)
    {
        var grant=await Grant(http);if(grant is null)return Results.Unauthorized();
        if(http.Request.Query.Count!=0)return Results.BadRequest();
        Guid key=Guid.Empty;int revision=0;
        if(accept){
            if(!Csrf(grant.Csrf!,http.Request.Headers[CsrfHeader].ToString()))return Results.StatusCode(403);
            var body=await Body(http,["command_key","expected_revision","confirmed"]);
            if(body is null || body.Value.GetProperty("confirmed").ValueKind!=JsonValueKind.True
                || body.Value.GetProperty("command_key").ValueKind!=JsonValueKind.String
                || !ProductCompanyTeamFlow.CanonicalGuid(body.Value.GetProperty("command_key").GetString(),out key)
                || body.Value.GetProperty("expected_revision").ValueKind!=JsonValueKind.Number
                || !body.Value.GetProperty("expected_revision").TryGetInt32(out revision) || revision<1)return Results.BadRequest();
        }
        var recipient=await http.RequestServices.GetRequiredService<ProductInvitationRecipient>().ReadAsync(grant.Subject!,grant.AccessToken!,http.RequestAborted);
        if(recipient.Status!=InvitationRecipientStatus.Verified)return Results.StatusCode(recipient.Status==InvitationRecipientStatus.Denied?403:503);
        var api=http.RequestServices.GetRequiredService<CoreCompanyInvitationAcceptanceApi>();
        if(!accept){
            var result=await api.ReviewAsync(recipient.Observation!,grant.Token,http.RequestAborted);
            if(result.Status!=CompanyTeamReadStatus.Ok)return ProductCompanyTeamFlow.ReadFailure(result.Status);
            var row=result.Review!;return Results.Ok(new{invitation_id=row.InvitationId,company_id=row.CompanyId,company_name=row.CompanyName,
                role=row.Role,revision=row.Revision,expires_at=row.ExpiresAt,operational_access_granted=false});
        }
        var accepted=await api.AcceptAsync(recipient.Observation!,new(grant.Token,key,revision,true),http.RequestAborted);
        var status=accepted.Status switch{CompanyInvitationStatus.Accepted or CompanyInvitationStatus.Existing=>200,
            CompanyInvitationStatus.Invalid=>400,CompanyInvitationStatus.Conflict=>409,CompanyInvitationStatus.Unavailable=>503,_=>403};
        return Results.Json(new{status=accepted.Status.ToString(),invitation_id=accepted.InvitationId,membership_id=accepted.MembershipId,
            revision=accepted.Revision,operational_access_granted=false},statusCode:status);
    }
}
