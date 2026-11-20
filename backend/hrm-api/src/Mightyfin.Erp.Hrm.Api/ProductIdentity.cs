using System.Net.Http.Headers;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Mightyfin.Erp.Hrm.Infrastructure.Data;

namespace Mightyfin.Erp.Hrm.Api;

/// <summary>Product identity is deliberately separate from the default ERP scheme.</summary>
internal static class ProductIdentity
{
    internal const string PendingScheme = "kuubu-product-identity";
    internal const string OAuthScheme = "kuubu-product-oauth";
    internal const string PendingCookie = "__Host-kuubu_product_identity";
    internal const string Prefix = "/api/core/product-identity";
    internal const string SubjectClaim = "kuubu:product:subject";
    internal const string IssuerClaim = "kuubu:product:issuer";
    internal const string SetupHandleClaim = "kuubu:setup:handle";
    internal const string SetupPurpose = "kuubu:setup:purpose";
    private const string DefaultIssuer = "https://commercial-api.greenwebb.tech";
    private const string DefaultOrigin = "https://app.kuubuerp.com";

    internal static bool Enabled(IConfiguration config) => config.GetValue<bool>("ERP:ProductIdentity:Enabled");
    internal static IServiceCollection AddProductIdentity(this IServiceCollection services, IConfiguration config)
    {
        if (!Enabled(config)) return services;
        var issuer = ExactHttpsOrigin(config["ERP:ProductIdentity:Issuer"] ?? DefaultIssuer);
        var origin = ExactHttpsOrigin(config["ERP:ProductIdentity:PublicOrigin"] ?? DefaultOrigin);
        if (issuer != DefaultIssuer || origin != DefaultOrigin)
            throw new InvalidOperationException("The product issuer and callback origin must match the reviewed Kuubu deployment.");
        var authorize = config["ERP:ProductIdentity:AuthorizationEndpoint"] ?? "https://www.kuubuerp.com/account/authorize/";
        if (!Uri.TryCreate(authorize, UriKind.Absolute, out var authUri) || authUri.Scheme != "https"
            || authUri.Host != "www.kuubuerp.com" || !authUri.IsDefaultPort
            || authUri.UserInfo.Length != 0 || authUri.Query.Length != 0 || authUri.Fragment.Length != 0)
            throw new InvalidOperationException("A reviewed Kuubu HTTPS authorization endpoint is required.");
        var clientId = config["ERP:ProductIdentity:ClientId"];
        var secret = config["ERP:ProductIdentity:ClientSecret"];
        if (!Guid.TryParse(clientId, out _) || string.IsNullOrWhiteSpace(secret))
            throw new InvalidOperationException("A confidential product identity client is required.");
        var keyPath = config["ERP:ProductIdentity:DataProtectionPath"];
        if (string.IsNullOrWhiteSpace(keyPath) || !Path.IsPathFullyQualified(keyPath)
            || !Directory.Exists(keyPath) || new DirectoryInfo(keyPath).LinkTarget is not null)
            throw new InvalidOperationException("An existing private Data Protection directory is required.");
        if (!OperatingSystem.IsWindows() && (File.GetUnixFileMode(keyPath) &
            (UnixFileMode.GroupRead | UnixFileMode.GroupWrite | UnixFileMode.GroupExecute |
             UnixFileMode.OtherRead | UnixFileMode.OtherWrite | UnixFileMode.OtherExecute)) != 0)
            throw new InvalidOperationException("Product Data Protection keys must be private to the runtime user.");
        services.AddDataProtection().SetApplicationName("Kuubu.ProductIdentity.v1")
            .PersistKeysToFileSystem(new DirectoryInfo(keyPath));
        // The framework's OAuth debug/correlation diagnostics can contain state
        // or cookie identifiers. Emit our generic failure event instead.
        services.AddLogging(logging => logging.AddFilter("Microsoft.AspNetCore.Authentication.OAuth", LogLevel.None));
        ProductOperationalFlow.Add(services,config,keyPath);
        services.AddProductCompanyTeam(config);
        if (ProductInvitationAcceptanceFlow.Enabled(config))
        {
            var intentPath = config["ERP:CompanyInvitationAcceptance:IntentPath"];
            ProductSetupGrantStore.ValidateDirectory(intentPath, keyPath);
            if (string.Equals(Path.TrimEndingDirectorySeparator(Path.GetFullPath(intentPath!)),
                Path.TrimEndingDirectorySeparator(Path.GetFullPath(config["ERP:WorkspaceSetup:GrantPath"] ?? keyPath)),
                OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal))
                throw new InvalidOperationException("Invitation intents require a separate private directory.");
            services.AddSingleton(provider => {
                ProductSetupGrantStore.ValidateDirectory(intentPath,keyPath,provider.GetRequiredService<IWebHostEnvironment>().ContentRootPath);
                return new ProductInvitationIntentStore(intentPath!,clientId!,provider.GetRequiredService<IDataProtectionProvider>(),TimeProvider.System);
            });
            services.AddHostedService<ProductInvitationIntentCleanup>();
            services.AddSingleton(new ProductInvitationGuestBudget(TimeProvider.System));
            services.AddSingleton(new ProductInvitationRecipient(new HttpClient(new HttpClientHandler { AllowAutoRedirect=false,UseCookies=false })
                {Timeout=TimeSpan.FromSeconds(10)},TimeProvider.System,clientId!));
        }
        if (ProductSetupFlow.Enabled(config))
        {
            var grantPath = config["ERP:WorkspaceSetup:GrantPath"];
            ProductSetupGrantStore.ValidateDirectory(grantPath, keyPath);
            services.AddSingleton(provider =>
            {
                ProductSetupGrantStore.ValidateDirectory(grantPath, keyPath,
                    provider.GetRequiredService<IWebHostEnvironment>().ContentRootPath);
                return new ProductSetupGrantStore(grantPath!, clientId!, provider.GetRequiredService<IDataProtectionProvider>(), TimeProvider.System);
            });
            services.AddHostedService<ProductSetupGrantCleanup>();
        }
        services.AddAuthentication()
            .AddCookie(PendingScheme, options =>
            {
                options.Cookie.Name = PendingCookie;
                options.Cookie.Path = "/";
                options.Cookie.HttpOnly = true;
                options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
                options.Cookie.SameSite = SameSiteMode.Lax;
                options.ExpireTimeSpan = TimeSpan.FromMinutes(5);
                options.SlidingExpiration = false;
            })
            .AddOAuth(OAuthScheme, options =>
            {
                options.SignInScheme = PendingScheme;
                options.ClientId = clientId!;
                options.ClientSecret = secret;
                options.AuthorizationEndpoint = authorize;
                options.TokenEndpoint = issuer+"/api/v1/product-identity/token";
                options.UserInformationEndpoint = issuer+"/api/v1/product-identity/subject";
                options.CallbackPath = Prefix+"/callback";
                options.UsePkce = true;
                options.SaveTokens = false;
                options.Scope.Add("product:identity");
                options.RemoteAuthenticationTimeout = TimeSpan.FromMinutes(5);
                options.CorrelationCookie.Name = "__Host-kuubu_product_correlation.";
                options.CorrelationCookie.Path = "/";
                options.CorrelationCookie.HttpOnly = true;
                options.CorrelationCookie.SecurePolicy = CookieSecurePolicy.Always;
                options.CorrelationCookie.SameSite = SameSiteMode.Lax;
                options.Backchannel = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false, UseCookies = false })
                { Timeout = TimeSpan.FromSeconds(10), MaxResponseContentBufferSize = 65536 };
                options.Events.OnCreatingTicket = async context =>
                {
                    var lifetime = context.ExpiresIn;
                    if (lifetime is null || lifetime.Value.TotalSeconds is < 1 or > 300
                        || !string.Equals(context.TokenType, "Bearer", StringComparison.OrdinalIgnoreCase))
                        throw new AuthenticationFailureException("Invalid product identity proof.");
                    // Starting at challenge time is conservative: the pending proof
                    // cannot outlast the token even when the backchannel is slow.
                    var now = options.TimeProvider!.GetUtcNow();
                    var expires = context.Properties.IssuedUtc?.Add(lifetime.Value);
                    if (expires is null || expires <= now)
                        throw new AuthenticationFailureException("Product sign-in expired.");
                    using var request = new HttpRequestMessage(HttpMethod.Get, options.UserInformationEndpoint);
                    request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", context.AccessToken);
                    request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                    using var response = await context.Backchannel.SendAsync(request, context.HttpContext.RequestAborted);
                    if (!response.IsSuccessStatusCode) throw new AuthenticationFailureException("Product identity was not verified.");
                    using var payload = JsonDocument.Parse(await response.Content.ReadAsStringAsync(context.HttpContext.RequestAborted));
                    var data = payload.RootElement.GetProperty("data");
                    var subject = data.GetProperty("subject").GetString();
                    if (data.GetProperty("schema_version").GetInt32() != 1
                        || data.GetProperty("issuer").GetString() != issuer
                        || data.GetProperty("product_code").GetString() != "kuubu"
                        || !data.GetProperty("verified").GetBoolean()
                        || data.GetProperty("operational_access_granted").GetBoolean()
                        || subject is null || !Regex.IsMatch(subject, "^[1-9][0-9]{0,18}$", RegexOptions.CultureInvariant))
                        throw new AuthenticationFailureException("Invalid product identity proof.");
                    context.Identity!.AddClaim(new Claim(SubjectClaim, subject));
                    await ProductOperationalFlow.RevokeAsync(context.HttpContext);
                    context.Identity.AddClaim(new Claim(IssuerClaim, issuer));
                    context.Properties.ExpiresUtc = expires;
                    context.Properties.IsPersistent = false;
                    context.Properties.AllowRefresh = false;
                    var store = context.HttpContext.RequestServices.GetService<ProductSetupGrantStore>();
                    var prior = await context.HttpContext.AuthenticateAsync(PendingScheme);
                    if (store is not null && prior.Succeeded && prior.Principal?.FindFirstValue(SetupHandleClaim) is { } oldHandle)
                        store.Revoke(oldHandle);
                    var invitationStore=context.HttpContext.RequestServices.GetService<ProductInvitationIntentStore>();
                    if(prior.Succeeded && prior.Principal?.FindFirstValue(ProductInvitationAcceptanceFlow.HandleClaim) is { } oldInvitation)
                        invitationStore?.Revoke(oldInvitation);
                    if(context.Properties.Items.TryGetValue(ProductInvitationAcceptanceFlow.Purpose,out var invitationPurpose))
                    {
                        if(invitationPurpose!=ProductInvitationAcceptanceFlow.PurposeValue || !ProductInvitationAcceptanceFlow.Enabled(config)
                            || context.Properties.Items.ContainsKey(SetupPurpose) || context.Properties.Items.ContainsKey(ProductCompanyTeamFlow.Purpose))
                            throw new AuthenticationFailureException("Invitation authorization is unavailable.");
                        var handle=await ProductInvitationAcceptanceFlow.CompleteAsync(context.HttpContext,context.Properties,subject,context.AccessToken!,expires.Value);
                        if(handle is null)throw new AuthenticationFailureException("Invitation sign-in expired.");
                        context.Identity.AddClaim(new Claim(ProductInvitationAcceptanceFlow.HandleClaim,handle));
                    }
                    else ProductInvitationAcceptanceFlow.Revoke(context.HttpContext);
                    if (context.Properties.Items.TryGetValue(ProductCompanyTeamFlow.Purpose, out var teamPurpose))
                    {
                        if (teamPurpose != ProductCompanyTeamFlow.PurposeValue || !ProductCompanyTeamFlow.Enabled(config)
                            || context.Properties.Items.ContainsKey(SetupPurpose))
                            throw new AuthenticationFailureException("Company team authorization is unavailable.");
                        context.Identity.AddClaim(new Claim(ProductCompanyTeamFlow.NonceClaim,
                            Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(32))));
                    }
                    if (context.Properties.Items.TryGetValue(SetupPurpose, out var purpose))
                    {
                        if (purpose != "company-create-v1" || !ProductSetupFlow.Enabled(config) || store is null)
                            throw new AuthenticationFailureException("Setup authorization is unavailable.");
                        var handle = await store.IssueAsync(subject, context.AccessToken!, expires.Value, context.HttpContext.RequestAborted);
                        context.Identity.AddClaim(new Claim(SetupHandleClaim, handle));
                    }
                };
                options.Events.OnRemoteFailure = async context =>
                {
                    context.HandleResponse();
                    context.Response.StatusCode = 403;
                    context.Response.Headers.CacheControl = "no-store";
                    context.HttpContext.RequestServices.GetRequiredService<ILoggerFactory>()
                        .CreateLogger("Kuubu.ProductIdentity").LogWarning("Product sign-in failed.");
                    await context.Response.WriteAsJsonAsync(new { error = "product_sign_in_failed" });
                };
            });
        return services;
    }

    internal static void UseProductIdentityOriginGuard(this WebApplication app)
    {
        app.Use(async (context, next) =>
        {
            if (!context.Request.Path.StartsWithSegments(Prefix)) { await next(); return; }
            context.Response.Headers.CacheControl = "no-store";
            context.Response.Headers.Pragma = "no-cache";
            if (!Enabled(app.Configuration)) { context.Response.StatusCode = 503; return; }
            var origin = app.Configuration["ERP:ProductIdentity:PublicOrigin"] ?? DefaultOrigin;
            if (context.Request.Scheme+"://"+context.Request.Host != origin)
            { context.Response.StatusCode = 400; return; }
            if (HttpMethods.IsPost(context.Request.Method)
                && (!ProductAccountHub.Allows(app.Configuration, context.Request.Headers.Origin.ToString()) || !context.Request.HasJsonContentType()))
            { context.Response.StatusCode = 403; return; }
            if(context.Request.Path.StartsWithSegments(ProductOperationalFlow.Prefix)) {
                if(!ProductOperationalFlow.Enabled(app.Configuration)){context.Response.StatusCode=503;return;}
                if(context.Request.Path==ProductOperationalFlow.Callback && !ProductOperationalFlow.CallbackAllowed(context))
                {context.Response.StatusCode=403;return;}
            }
            await next();
        });
    }

    internal static void MapProductIdentity(this WebApplication app)
    {
        var group = app.MapGroup(Prefix).WithMetadata(new ProductIdentityRequestBoundary());
        group.MapGet("/start", () => Results.Challenge(new AuthenticationProperties
        { RedirectUri = Prefix+"/session", IssuedUtc = DateTimeOffset.UtcNow }, [OAuthScheme])).AllowAnonymous();
        group.MapGet("/session", async (HttpContext context, HrmDbContext db) =>
        {
            var proof = await context.AuthenticateAsync(PendingScheme);
            if (!proof.Succeeded) return Results.Unauthorized();
            var subject = proof.Principal!.FindFirstValue(SubjectClaim);
            var issuer = proof.Principal.FindFirstValue(IssuerClaim);
            if (subject is null || issuer != (app.Configuration["ERP:ProductIdentity:Issuer"] ?? DefaultIssuer))
                return Results.Unauthorized();
            var now = DateTimeOffset.UtcNow;
            // Only this trusted issuer/subject may bypass tenant filters to find
            // explicit same-tenant link/user pairs. No email/organization search.
            var links = await (from link in db.ExternalIdentityLinks.IgnoreQueryFilters().AsNoTracking()
                join user in db.LocalUsers.IgnoreQueryFilters().AsNoTracking()
                    on new { link.TenantId, Id = link.LocalUserId } equals new { user.TenantId, user.Id }
                where link.Issuer == issuer && link.Subject == subject && link.IsActive && !link.IsArchived
                    && user.IsActive && !user.IsArchived && (user.LockedUntil == null || user.LockedUntil <= now)
                orderby link.Id
                select new { link_id = link.Id, display_name = user.DisplayName }).ToListAsync(context.RequestAborted);
            return Results.Ok(new { verified = true, product_code = "kuubu", expires_at = proof.Properties!.ExpiresUtc,
                operational_access_granted = false, links });
        }).AllowAnonymous();
        group.MapPost("/logout", async (HttpContext context) =>
        {
            var proof = await context.AuthenticateAsync(PendingScheme);
            if (proof.Succeeded && proof.Principal?.FindFirstValue(SetupHandleClaim) is { } handle)
                context.RequestServices.GetService<ProductSetupGrantStore>()?.Revoke(handle);
            ProductInvitationAcceptanceFlow.Revoke(context);
            await ProductOperationalFlow.RevokeAsync(context);
            await context.SignOutAsync(PendingScheme);
            return Results.NoContent();
        }).AllowAnonymous();
        app.MapProductInvitationAcceptance();
        ProductOperationalFlow.Map(app);
    }

    private static string ExactHttpsOrigin(string origin)
    {
        if (!Uri.TryCreate(origin, UriKind.Absolute, out var uri) || uri.Scheme != "https"
            || uri.UserInfo.Length != 0 || uri.AbsolutePath != "/" || uri.Query.Length != 0
            || uri.Fragment.Length != 0 || origin != uri.GetLeftPart(UriPartial.Authority))
            throw new InvalidOperationException("Product identity requires an exact reviewed HTTPS origin.");
        return origin;
    }
}
