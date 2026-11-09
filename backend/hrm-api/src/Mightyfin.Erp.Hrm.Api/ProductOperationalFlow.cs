using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.OAuth;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Options;

namespace Mightyfin.Erp.Hrm.Api;

internal sealed record OperationalBrowser(string Binding,string Csrf,string Challenge,DateTimeOffset Issued,DateTimeOffset Expires)
{ public override string ToString()=>nameof(OperationalBrowser); }

internal sealed class ProductOperationalBrowser(IDataProtectionProvider protection)
{
    private readonly IDataProtector protector=protection.CreateProtector("Kuubu.OperationalBrowser.v1");
    internal const string Cookie="__Host-kuubu_operational_browser",Header="X-Kuubu-Operational-CSRF";
    internal string Protect(OperationalBrowser value)=>protector.Protect(JsonSerializer.Serialize(value));
    internal OperationalBrowser? Read(HttpContext context)
    {
        if(context.Request.Headers.Cookie.ToString().Split(';').Count(x=>x.Trim().StartsWith(Cookie+"=",StringComparison.Ordinal))!=1)return null;
        var text=context.Request.Cookies[Cookie];if(text is null || text.Length>8192)return null;
        try {
            var value=JsonSerializer.Deserialize<OperationalBrowser>(protector.Unprotect(text));var now=DateTimeOffset.UtcNow;
            return value is not null && ProductInvitationIntentStore.Opaque(value.Binding) && ProductInvitationIntentStore.Opaque(value.Csrf)
                && (value.Challenge=="" || ProductInvitationIntentStore.Opaque(value.Challenge))
                && value.Issued<=now && value.Expires>now && value.Expires-value.Issued==TimeSpan.FromHours(8)?value:null;
        }catch(Exception error)when(error is CryptographicException or JsonException or ArgumentException){return null;}
    }
    internal void Write(HttpContext context,OperationalBrowser value)=>context.Response.Cookies.Append(Cookie,Protect(value),new CookieOptions {
        Path="/",Secure=true,HttpOnly=true,SameSite=SameSiteMode.Lax,Expires=value.Expires,IsEssential=true });
    internal bool Csrf(HttpContext context,OperationalBrowser value)=>Equal(value.Csrf,context.Request.Headers[Header].ToString());
    internal static bool Equal(string value,string candidate)=>ProductInvitationIntentStore.Opaque(candidate)
        && CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(value),Encoding.ASCII.GetBytes(candidate));
}

/// <summary>Protected person continuity only; never the default ERP authentication or a company/module grant.</summary>
internal static class ProductOperationalFlow
{
    internal const string Prefix=ProductIdentity.Prefix+"/operational",Callback=Prefix+"/callback";
    internal const string Scheme="kuubu-operational-identity",OAuthScheme="kuubu-operational-oauth";
    internal const string Cookie="__Host-kuubu_operational_identity",Purpose="erp-session-v1";
    internal const string HandleClaim="kuubu:operational:handle",BindingClaim="kuubu:operational:binding";
    private const string ChallengeKey="kuubu:operational:challenge",BindingKey="kuubu:operational:browser",PurposeKey="kuubu:operational:purpose";
    private const string Origin="https://app.kuubuerp.com";
    internal static bool Enabled(IConfiguration config)=>ProductIdentity.Enabled(config) && config.GetValue<bool>("ERP:OperationalIdentity:Enabled");

    internal static void Add(IServiceCollection services,IConfiguration config,string keyPath)
    {
        if(!Enabled(config))return;
        var client=config["ERP:OperationalIdentity:ClientId"];var secret=config["ERP:OperationalIdentity:ClientSecret"];
        if(!Guid.TryParseExact(client,"D",out var id) || id==Guid.Empty || id.ToString("D")!=client
            || Guid.TryParse(config["ERP:ProductIdentity:ClientId"],out var setupId) && setupId==id
            || string.IsNullOrWhiteSpace(secret) || secret.Length>512)
            throw new InvalidOperationException("A separate confidential operational identity client is required.");
        var state=config["ERP:OperationalIdentity:StatePath"];
        ProductSetupGrantStore.ValidateDirectory(state,keyPath);
        services.AddSingleton<ProductOperationalBrowser>();
        services.AddSingleton(provider=> {
            ProductSetupGrantStore.ValidateDirectory(state,keyPath,provider.GetRequiredService<IWebHostEnvironment>().ContentRootPath);
            return new ProductOperationalCredentialStore(state!,keyPath,client,provider.GetRequiredService<IDataProtectionProvider>(),TimeProvider.System);
        });
        services.AddHostedService<ProductOperationalCleanup>();
        services.AddSingleton(new OperationalRequestBudget());
        services.AddSingleton(new ProductOperationalSubject(Backchannel(),TimeProvider.System,client));
        services.AddSingleton(new ProductOperationalRefresh(Backchannel(),TimeProvider.System,client,secret));
        services.AddAuthentication().AddCookie(Scheme,options=> {
            options.Cookie.Name=Cookie;options.Cookie.Path="/";options.Cookie.SecurePolicy=CookieSecurePolicy.Always;
            options.Cookie.HttpOnly=true;options.Cookie.SameSite=SameSiteMode.Lax;
            options.ExpireTimeSpan=TimeSpan.FromHours(8);options.SlidingExpiration=false;
        }).AddOAuth(OAuthScheme,options=> {
            options.SignInScheme=Scheme;options.ClientId=client;options.ClientSecret=secret;
            options.AuthorizationEndpoint="https://www.kuubuerp.com/account/authorize/";
            options.TokenEndpoint=ProductCommercialLicense.Issuer+"/api/v1/product-identity/token";
            options.UserInformationEndpoint=ProductOperationalSubject.Endpoint;options.CallbackPath=Callback;
            options.UsePkce=true;options.SaveTokens=false;options.Scope.Clear();options.Scope.Add("product:identity");options.Scope.Add("product:erp-session");
            options.RemoteAuthenticationTimeout=TimeSpan.FromMinutes(5);
            options.CorrelationCookie.Name="__Host-kuubu_operational_correlation.";options.CorrelationCookie.Path="/";
            options.CorrelationCookie.SecurePolicy=CookieSecurePolicy.Always;options.CorrelationCookie.HttpOnly=true;options.CorrelationCookie.SameSite=SameSiteMode.Lax;
            options.Backchannel=Backchannel();
            options.Events.OnCreatingTicket=async context=> {
                var http=context.HttpContext;var browser=http.RequestServices.GetRequiredService<ProductOperationalBrowser>();
                var binding=browser.Read(http);
                if(binding is null || !ValidChallenge(context.Properties,binding))throw new AuthenticationFailureException("Operational sign-in expired.");
                // Consume the browser challenge even if provider verification subsequently fails.
                browser.Write(http,binding with{Challenge=""});
                var now=options.TimeProvider!.GetUtcNow();var ttl=context.ExpiresIn;
                var expires=ttl is not null?context.Properties.IssuedUtc?.Add(ttl.Value):null;
                var tokenJson=context.TokenResponse.Response?.RootElement;
                if(tokenJson is null)throw new AuthenticationFailureException("Operational identity unavailable.");
                ProductWorkspaceSetupAuthority.RejectDuplicates(tokenJson.Value);
                if(tokenJson.Value.TryGetProperty("scope",out var scope) && scope.GetString() is not ("product:identity product:erp-session" or "product:erp-session product:identity"))
                    throw new AuthenticationFailureException("Operational identity unavailable.");
                if(ttl is null || ttl.Value.TotalSeconds is <1 or >300 || context.TokenType!="Bearer" || expires is null || expires<=now
                    || !OperationalTokenPair.ValidAccess(context.AccessToken) || !OperationalTokenPair.ValidRefresh(context.RefreshToken))
                    throw new AuthenticationFailureException("Operational sign-in expired.");
                var person=await new ProductOperationalSubject(context.Backchannel,options.TimeProvider!,client).DiscoverAsync(context.AccessToken!,http.RequestAborted);
                if(person.Status!=OperationalSubjectStatus.Verified || person.Observation is not {} proof)
                    throw new AuthenticationFailureException("Operational identity unavailable.");
                var store=http.RequestServices.GetRequiredService<ProductOperationalCredentialStore>();
                var handle=await store.IssueAsync(proof,new(context.AccessToken!,context.RefreshToken!,expires.Value),binding.Binding,http.RequestAborted);
                if(handle is null)throw new AuthenticationFailureException("Operational identity unavailable.");
                try {
                    await RevokeAsync(http,clearCookie:false);
                    var prior=await http.AuthenticateAsync(ProductIdentity.PendingScheme);
                    if(prior.Succeeded && prior.Principal?.FindFirstValue(ProductIdentity.SetupHandleClaim) is {} setup)
                        http.RequestServices.GetService<ProductSetupGrantStore>()?.Revoke(setup);
                    if(prior.Succeeded && prior.Principal?.FindFirstValue(ProductInvitationAcceptanceFlow.HandleClaim) is {} invitation)
                        http.RequestServices.GetService<ProductInvitationIntentStore>()?.Revoke(invitation);
                    ProductInvitationAcceptanceFlow.Revoke(http);
                    await http.SignOutAsync(ProductIdentity.PendingScheme);
                    context.Identity!.AddClaims([new(ProductIdentity.SubjectClaim,proof.Subject),new(ProductIdentity.IssuerClaim,ProductCommercialLicense.Issuer),
                        new(HandleClaim,handle),new(BindingClaim,binding.Binding)]);
                    context.Properties.Items.Clear();context.Properties.Items[PurposeKey]=Purpose;
                    context.Properties.RedirectUri=Prefix+"/session";context.Properties.IssuedUtc=now;
                    context.Properties.ExpiresUtc=now.AddHours(8);context.Properties.AllowRefresh=false;context.Properties.IsPersistent=false;
                }catch {await store.RevokeAsync(handle,binding.Binding);throw;}
            };
            options.Events.OnRemoteFailure=async context=> {
                context.HandleResponse();context.Response.StatusCode=403;context.Response.Headers.CacheControl="no-store";
                await context.Response.WriteAsJsonAsync(new{error="operational_sign_in_failed"});
            };
        });
    }
    private static HttpClient Backchannel()=>new(new HttpClientHandler{AllowAutoRedirect=false,UseCookies=false}){Timeout=TimeSpan.FromSeconds(10),MaxResponseContentBufferSize=65536};
    private static bool ValidChallenge(AuthenticationProperties properties,OperationalBrowser browser)=>
        Item(properties,PurposeKey)==Purpose && Item(properties,BindingKey)==ProductInvitationIntentStore.Hash(browser.Binding)
        && browser.Challenge!="" && Item(properties,ChallengeKey)==ProductInvitationIntentStore.Hash(browser.Challenge)
        && properties.RedirectUri==Prefix+"/session" && properties.IssuedUtc is {} issued
        && issued<=DateTimeOffset.UtcNow && issued>DateTimeOffset.UtcNow.AddMinutes(-5)
        && !properties.Items.ContainsKey(ProductIdentity.SetupPurpose) && !properties.Items.ContainsKey(ProductCompanyTeamFlow.Purpose)
        && !properties.Items.ContainsKey(ProductInvitationAcceptanceFlow.Purpose);
    private static string? Item(AuthenticationProperties properties,string key)=>properties.Items.TryGetValue(key,out var value)?value:null;
    private static async Task<bool> EmptyBody(HttpContext context)
    {
        var bytes=new byte[3];var count=0;
        while(count<bytes.Length){var read=await context.Request.Body.ReadAsync(bytes.AsMemory(count),context.RequestAborted);if(read==0)break;count+=read;}
        return count==0 || count==2 && bytes[0]=='{' && bytes[1]=='}';
    }

    // Runs before the remote OAuth handler exchanges a code.
    internal static bool CallbackAllowed(HttpContext context)
    {
        var query=context.Request.Query;
        if(!HttpMethods.IsGet(context.Request.Method) || query.Count!=2 || !query.ContainsKey("code") || !query.ContainsKey("state")
            || query.Any(x=>x.Value.Count!=1) || query["code"].ToString().Length is <1 or >8192 || query["state"].ToString().Length is <1 or >16384)return false;
        var browser=context.RequestServices.GetRequiredService<ProductOperationalBrowser>().Read(context);
        if(browser is null)return false;
        if(!context.RequestServices.GetRequiredService<OperationalRequestBudget>().Budget.TryAcquire(ProductInvitationGuestBudget.Operation.Prepare,context.Connection.RemoteIpAddress))return false;
        var options=context.RequestServices.GetRequiredService<IOptionsMonitor<OAuthOptions>>().Get(OAuthScheme);
        var properties=options.StateDataFormat.Unprotect(query["state"]);
        return properties is not null && ValidChallenge(properties,browser);
    }
    internal static async Task RevokeAsync(HttpContext context,bool clearCookie=true)
    {
        if(!Enabled(context.RequestServices.GetRequiredService<IConfiguration>()))return;
        var ticket=await context.AuthenticateAsync(Scheme);
        if(ticket.Succeeded && ticket.Principal?.FindFirstValue(HandleClaim) is {} handle && ticket.Principal.FindFirstValue(BindingClaim) is {} binding)
            await context.RequestServices.GetRequiredService<ProductOperationalCredentialStore>().RevokeAsync(handle,binding);
        if(clearCookie)await context.SignOutAsync(Scheme);
    }
    internal static void Map(WebApplication app)
    {
        var group=app.MapGroup(Prefix);
        group.AddEndpointFilter(async (invocation,next)=> {
            if(!Enabled(app.Configuration))return Results.StatusCode(503);
            var context=invocation.HttpContext;
            if(context.Request.Headers["Sec-Fetch-Site"]=="cross-site")return Results.StatusCode(403);
            if(context.Request.Query.Count!=0 || HttpMethods.IsPost(context.Request.Method) && !await EmptyBody(context))return Results.StatusCode(400);
            var operation=HttpMethods.IsGet(context.Request.Method)?ProductInvitationGuestBudget.Operation.Session:ProductInvitationGuestBudget.Operation.Start;
            if(!context.RequestServices.GetRequiredService<OperationalRequestBudget>().Budget.TryAcquire(operation,context.Connection.RemoteIpAddress))return Results.StatusCode(429);
            return await next(invocation);
        });
        group.MapGet("/bootstrap",(HttpContext context)=> {
            var service=context.RequestServices.GetRequiredService<ProductOperationalBrowser>();var value=service.Read(context);
            if(value is null){var now=DateTimeOffset.UtcNow;value=new(ProductInvitationIntentStore.Random(),ProductInvitationIntentStore.Random(),"",now,now.AddHours(8));service.Write(context,value);}
            return Results.Ok(new{csrf_token=value.Csrf,operational_access_granted=false});
        }).AllowAnonymous();
        group.MapPost("/start",async (HttpContext context)=> {
            var service=context.RequestServices.GetRequiredService<ProductOperationalBrowser>();var old=service.Read(context);
            if(old is null || !service.Csrf(context,old))return Results.StatusCode(403);
            await RevokeAsync(context);
            var now=DateTimeOffset.UtcNow;
            var value=new OperationalBrowser(ProductInvitationIntentStore.Random(),ProductInvitationIntentStore.Random(),ProductInvitationIntentStore.Random(),now,now.AddHours(8));
            service.Write(context,value);
            var properties=new AuthenticationProperties{IssuedUtc=now,RedirectUri=Prefix+"/session"};
            properties.Items[PurposeKey]=Purpose;properties.Items[BindingKey]=ProductInvitationIntentStore.Hash(value.Binding);
            properties.Items[ChallengeKey]=ProductInvitationIntentStore.Hash(value.Challenge);
            return Results.Challenge(properties,[OAuthScheme]);
        }).AllowAnonymous();
        group.MapGet("/session",async (HttpContext context)=> {
            var ticket=await context.AuthenticateAsync(Scheme);var browser=context.RequestServices.GetRequiredService<ProductOperationalBrowser>().Read(context);
            if(!ticket.Succeeded || browser is null || ticket.Properties is null || Item(ticket.Properties,PurposeKey)!=Purpose
                || ticket.Principal?.FindFirstValue(BindingClaim)!=browser.Binding || ticket.Principal.FindFirstValue(HandleClaim) is not {} handle
                || ticket.Principal.FindFirstValue(ProductIdentity.IssuerClaim)!=ProductCommercialLicense.Issuer)return Results.Unauthorized();
            var person=context.RequestServices.GetRequiredService<ProductOperationalSubject>();var refresh=context.RequestServices.GetRequiredService<ProductOperationalRefresh>();
            var proof=await context.RequestServices.GetRequiredService<ProductOperationalCredentialStore>().ObserveAsync(handle,browser.Binding,person.ReadAsync,refresh.RefreshAsync,context.RequestAborted);
            if(proof.Status is OperationalCredentialStatus.Unavailable or OperationalCredentialStatus.Busy)return Results.StatusCode(503);
            if(proof.Status!=OperationalCredentialStatus.Verified || proof.Observation is not {} observation
                || observation.Subject!=ticket.Principal.FindFirstValue(ProductIdentity.SubjectClaim))return Results.Unauthorized();
            return Results.Ok(new{verified=true,purpose=Purpose,product_code="kuubu",csrf_token=browser.Csrf,
                operational_access_granted=false,next_step="company_selection",proof_until=observation.ExpiresAt});
        }).AllowAnonymous();
        group.MapPost("/logout",async(HttpContext context)=> {
            var service=context.RequestServices.GetRequiredService<ProductOperationalBrowser>();var browser=service.Read(context);
            if(browser is null || !service.Csrf(context,browser))return Results.StatusCode(403);
            await RevokeAsync(context);context.Response.Cookies.Delete(ProductOperationalBrowser.Cookie,new CookieOptions{Path="/",Secure=true,HttpOnly=true,SameSite=SameSiteMode.Lax});
            return Results.NoContent();
        }).AllowAnonymous();
    }
}
internal sealed class OperationalRequestBudget
{ internal ProductInvitationGuestBudget Budget{get;}=new(TimeProvider.System); }
internal sealed class ProductOperationalCleanup(ProductOperationalCredentialStore store,ILogger<ProductOperationalCleanup> logger):BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer=new PeriodicTimer(TimeSpan.FromMinutes(5));
        while(await timer.WaitForNextTickAsync(stoppingToken)) {
            try{store.PurgeExpired();}
            catch(Exception error)when(error is IOException or UnauthorizedAccessException){logger.LogWarning("Operational credential cleanup unavailable.");}
        }
    }
}
