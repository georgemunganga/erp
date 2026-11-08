using System.Security.Claims;
using System.Text.Json;
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
                    invitation_management_available = ProductCompanyInvitationsFlow.Enabled(app.Configuration),
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
        group.MapGet("/companies", async (HttpContext context) =>
        {
            var proof = await ReadProof(context);
            if (proof is null) return Results.Unauthorized();
            if (!ValidQuery(context.Request.Query, ["after"]) || !Cursor(context.Request.Query, out var after))
                return Results.BadRequest();
            var page = await context.RequestServices.GetRequiredService<CoreCompanyTeamApi>()
                .CompaniesAsync(proof.Issuer, proof.Subject, after, context.RequestAborted);
            if (page.Status != CompanyTeamReadStatus.Ok) return ReadFailure(page.Status);
            return Results.Ok(new { companies = page.Items!.Select(x => new { company_id = x.CompanyId,
                link_id = x.LinkId, company_name = x.CompanyName, role = x.Role }), next_cursor = page.Next,
                license_check_required = true, operational_access_granted = false });
        }).AllowAnonymous();
        group.MapGet("/members", async (HttpContext context) =>
        {
            var proof = await ReadProof(context);
            if (proof is null) return Results.Unauthorized();
            var query = context.Request.Query;
            if (!ValidQuery(query, ["company_id", "link_id", "after"]) || !Cursor(query, out var after)
                || !CanonicalGuid(query["company_id"].ToString(), out var company)
                || !CanonicalGuid(query["link_id"].ToString(), out var link)) return Results.BadRequest();
            var page = await context.RequestServices.GetRequiredService<CoreCompanyTeamApi>().MembersAsync(
                new(proof.Issuer, proof.Subject, link), company, after, context.RequestAborted);
            if (page.Status != CompanyTeamReadStatus.Ok) return ReadFailure(page.Status);
            return Results.Ok(new { members = page.Items!.Select(x => new { membership_id = x.MembershipId,
                display_name = x.DisplayName, role = x.Role, membership_active = x.MembershipActive,
                identity_available = x.IdentityAvailable, revision = x.Revision }), next_cursor = page.Next,
                actor_role = page.ActorRole, operational_access_granted = false });
        }).AllowAnonymous();
        group.MapPost("/commands", (Func<HttpContext, Task<IResult>>)ExecuteCommand).AllowAnonymous();
        app.MapProductCompanyInvitations();
    }

    internal static IResult ReadFailure(CompanyTeamReadStatus status) => Results.StatusCode(
        status == CompanyTeamReadStatus.Denied ? StatusCodes.Status403Forbidden : StatusCodes.Status503ServiceUnavailable);

    internal static bool ValidQuery(IQueryCollection query, string[] allowed) => query.All(
        item => allowed.Contains(item.Key, StringComparer.Ordinal) && item.Value.Count == 1);
    internal static bool Cursor(IQueryCollection query, out Guid? cursor)
    {
        cursor = null;
        if (!query.ContainsKey("after")) return true;
        if (!CanonicalGuid(query["after"].ToString(), out var value)) return false;
        cursor = value;
        return true;
    }
    internal static bool CanonicalGuid(string? text, out Guid value) => Guid.TryParseExact(text, "D", out value)
        && value != Guid.Empty && text == value.ToString("D");

    private static async Task<IResult> ExecuteCommand(HttpContext context)
    {
        var proof = await ReadProof(context);
        if (proof is null) return Results.Unauthorized();
        if (!await ValidateCsrf(context, proof)) return Results.StatusCode(StatusCodes.Status403Forbidden);
        if (context.Request.Query.Count != 0) return Results.BadRequest();
        const int limit = 16384;
        if (context.Request.ContentLength > limit) return Results.StatusCode(StatusCodes.Status413PayloadTooLarge);
        var buffer = new byte[limit+1];
        var used = 0;
        while (used < buffer.Length)
        {
            var count = await context.Request.Body.ReadAsync(buffer.AsMemory(used), context.RequestAborted);
            if (count == 0) break;
            used += count;
        }
        if (used > limit) return Results.StatusCode(StatusCodes.Status413PayloadTooLarge);
        Guid company, link, membership, key;
        int revision;
        CompanyTeamOperation operation;
        string? role = null;
        try
        {
            using var json = JsonDocument.Parse(buffer.AsMemory(0, used), new JsonDocumentOptions { MaxDepth = 4 });
            var root = json.RootElement;
            ProductWorkspaceSetupAuthority.RejectDuplicates(root);
            string[] allowed = ["company_id", "link_id", "membership_id", "command_key", "expected_revision", "operation", "role", "confirmed"];
            if (root.ValueKind != JsonValueKind.Object || root.EnumerateObject().Any(x => !allowed.Contains(x.Name))
                || root.GetProperty("confirmed").ValueKind != JsonValueKind.True
                || !CanonicalGuid(root.GetProperty("company_id").GetString(), out company)
                || !CanonicalGuid(root.GetProperty("link_id").GetString(), out link)
                || !CanonicalGuid(root.GetProperty("membership_id").GetString(), out membership)
                || !CanonicalGuid(root.GetProperty("command_key").GetString(), out key)
                || !root.GetProperty("expected_revision").TryGetInt32(out revision) || revision < 1) return Results.BadRequest();
            switch (root.GetProperty("operation").GetString())
            {
                case "role-change":
                    role = root.GetProperty("role").GetString();
                    if (role is not ("owner" or "administrator" or "member")) return Results.BadRequest();
                    operation = CompanyTeamOperation.ChangeRole;
                    break;
                case "revoke":
                    if (root.TryGetProperty("role", out _)) return Results.BadRequest();
                    operation = CompanyTeamOperation.Revoke;
                    break;
                default: return Results.BadRequest();
            }
        }
        catch (Exception error) when (error is JsonException or InvalidOperationException or KeyNotFoundException)
        { return Results.BadRequest(); }
        var result = await context.RequestServices.GetRequiredService<CoreCompanyTeamApi>().ExecuteAsync(
            new(proof.Issuer, proof.Subject, link), new(company, membership, key, revision, operation, role), context.RequestAborted);
        var status = result.Status switch { CompanyTeamStatus.Changed or CompanyTeamStatus.Unchanged or CompanyTeamStatus.Existing => 200,
            CompanyTeamStatus.Invalid => 400, CompanyTeamStatus.Denied => 403,
            CompanyTeamStatus.Conflict or CompanyTeamStatus.LastOwner => 409, _ => 503 };
        return Results.Json(new { status = result.Status.ToString(), membership_id = result.MembershipId,
            role = result.Role, membership_active = result.Active, revision = result.Revision,
            operational_access_granted = false }, statusCode: status);
    }

    internal sealed record TeamProof(string Issuer, string Subject, DateTimeOffset ExpiresAt, ClaimsPrincipal CsrfPrincipal);

    internal static async Task<TeamProof?> ReadProof(HttpContext context)
    {
        var result = await context.AuthenticateAsync(ProductIdentity.PendingScheme);
        if (!result.Succeeded || result.Principal is null || result.Properties is null
            || !result.Properties.Items.TryGetValue(Purpose, out var purpose) || purpose != PurposeValue
            || result.Properties.Items.ContainsKey(ProductIdentity.SetupPurpose)
            || result.Properties.Items.ContainsKey(ProductInvitationAcceptanceFlow.Purpose)
            || result.Principal.HasClaim(x => x.Type == ProductInvitationAcceptanceFlow.HandleClaim)
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
