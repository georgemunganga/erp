using System.Security.Claims;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;

namespace Mightyfin.Erp.Hrm.Api;

/// <summary>
/// A short-lived, explicitly requested team-management proof. This is never the
/// default ERP principal and cannot itself authorize a company or module action.
/// </summary>
internal static class ProductCompanyTeamFlow
{
    internal const string Prefix = ProductIdentity.Prefix+"/team";
    internal const string Purpose = "kuubu:team:purpose";
    internal const string PurposeValue = "company-team-v1";
    internal const string NonceClaim = "kuubu:team:nonce";
    internal const string CsrfCookie = "__Host-kuubu_company_team_csrf";
    internal const string CsrfHeader = "X-Kuubu-Team-CSRF";

    internal static bool Enabled(IConfiguration config) => ProductIdentity.Enabled(config)
        && config.GetValue<bool>("ERP:ProductLicensing:Enabled")
        && config.GetValue<bool>("ERP:CompanyTeam:Enabled");

    internal static IServiceCollection AddProductCompanyTeam(this IServiceCollection services, IConfiguration config)
    {
        if (!Enabled(config)) return services;
        services.AddAntiforgery(options =>
        {
            options.Cookie.Name = CsrfCookie;
            options.Cookie.Path = "/";
            options.Cookie.HttpOnly = true;
            options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
            options.Cookie.SameSite = SameSiteMode.Strict;
            options.HeaderName = CsrfHeader;
        });
        return services;
    }

    internal static void MapProductCompanyTeam(this WebApplication app)
    {
        var group = app.MapGroup(Prefix);
        group.AddEndpointFilter(async (context, next) => Enabled(app.Configuration)
            ? await next(context) : Results.StatusCode(StatusCodes.Status503ServiceUnavailable));
        group.MapGet("/start", () =>
        {
            var properties = new AuthenticationProperties
            { RedirectUri = "/team", IssuedUtc = DateTimeOffset.UtcNow };
            properties.Items[Purpose] = PurposeValue;
            return Results.Challenge(properties, [ProductIdentity.OAuthScheme]);
        }).AllowAnonymous();
        group.MapGet("/session", async (HttpContext context) =>
        {
            var proof = await ReadProof(context);
            if (proof is null) return Results.Unauthorized();
            var original = context.User;
            try
            {
                // Bind the framework token to this verified person AND this
                // particular signed sign-in. Never replace the default ERP user
                // outside the antiforgery call.
                context.User = proof.CsrfPrincipal;
                var tokens = context.RequestServices.GetRequiredService<IAntiforgery>().GetAndStoreTokens(context);
                return Results.Ok(new { verified = true, product_code = "kuubu", purpose = PurposeValue,
                    expires_at = proof.ExpiresAt, csrf_token = tokens.RequestToken, operational_access_granted = false });
            }
            finally { context.User = original; }
        }).AllowAnonymous();
        group.MapPost("/cancel", async (HttpContext context) =>
        {
            var proof = await ReadProof(context);
            if (proof is null) return Results.Unauthorized();
            if (!await ValidateCsrf(context, proof)) return Results.StatusCode(StatusCodes.Status403Forbidden);
            await context.SignOutAsync(ProductIdentity.PendingScheme);
            return Results.NoContent();
        }).AllowAnonymous();
    }

    internal sealed record TeamProof(string Issuer, string Subject, DateTimeOffset ExpiresAt, ClaimsPrincipal CsrfPrincipal);

    internal static async Task<TeamProof?> ReadProof(HttpContext context)
    {
        var result = await context.AuthenticateAsync(ProductIdentity.PendingScheme);
        if (!result.Succeeded || result.Principal is null || result.Properties is null
            || !result.Properties.Items.TryGetValue(Purpose, out var purpose) || purpose != PurposeValue
            || result.Properties.Items.ContainsKey(ProductIdentity.SetupPurpose)
            || result.Principal.HasClaim(x => x.Type == ProductIdentity.SetupHandleClaim)
            || result.Properties.ExpiresUtc is not { } expires) return null;
        var issuer = result.Principal.FindFirstValue(ProductIdentity.IssuerClaim);
        var subject = result.Principal.FindFirstValue(ProductIdentity.SubjectClaim);
        var nonce = result.Principal.FindFirstValue(NonceClaim);
        if (issuer != ProductCommercialLicense.Issuer || subject is null
            || !Regex.IsMatch(subject, "^[1-9][0-9]{0,18}\\z", RegexOptions.CultureInvariant)
            || nonce is null || !Regex.IsMatch(nonce, "^[0-9a-f]{64}\\z", RegexOptions.CultureInvariant)) return null;
        var identity = new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, issuer+"#"+subject+"#"+nonce)], "kuubu-team-csrf");
        return new(issuer, subject, expires, new ClaimsPrincipal(identity));
    }

    internal static async Task<bool> ValidateCsrf(HttpContext context, TeamProof proof)
    {
        var original = context.User;
        try
        {
            context.User = proof.CsrfPrincipal;
            await context.RequestServices.GetRequiredService<IAntiforgery>().ValidateRequestAsync(context);
            return true;
        }
        catch (AntiforgeryValidationException) { return false; }
        finally { context.User = original; }
    }
}
