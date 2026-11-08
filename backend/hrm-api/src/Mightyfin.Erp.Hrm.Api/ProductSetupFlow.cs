using System.Security.Claims;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.OAuth;

namespace Mightyfin.Erp.Hrm.Api;

internal static class ProductSetupFlow
{
    internal const string Prefix = ProductIdentity.Prefix+"/setup";
    internal static bool Enabled(IConfiguration config) => ProductIdentity.Enabled(config)
        && config.GetValue<bool>("ERP:WorkspaceSetup:Enabled") && config.GetValue<bool>("ERP:WorkspaceProvisioning:Enabled")
        && config.GetValue<bool>("ERP:ProductLicensing:Enabled");

    internal static void MapProductSetup(this WebApplication app)
    {
        var group = app.MapGroup(Prefix);
        group.AddEndpointFilter(async (invocation, next) =>
        {
            var http = invocation.HttpContext;
            http.Response.Headers.CacheControl = "no-store"; http.Response.Headers.Pragma = "no-cache";
            http.Response.Headers.XContentTypeOptions = "nosniff";
            if (!Enabled(app.Configuration)) return Results.StatusCode(503);
            var origin = app.Configuration["ERP:ProductIdentity:PublicOrigin"] ?? "https://app.kuubuerp.com";
            if (http.Request.Scheme+"://"+http.Request.Host != origin) return Results.StatusCode(400);
            var supplied = http.Request.Headers.Origin.ToString();
            if ((supplied.Length > 0 && supplied != origin) || (HttpMethods.IsPost(http.Request.Method)
                && (supplied != origin || !http.Request.HasJsonContentType()))) return Results.StatusCode(403);
            return await next(invocation);
        });
        group.MapGet("/start", () =>
        {
            var properties = new OAuthChallengeProperties { RedirectUri = "/setup", IssuedUtc = DateTimeOffset.UtcNow,
                Scope = new[] { "product:identity", "product:workspace-setup" } };
            properties.Items[ProductIdentity.SetupPurpose] = "company-create-v1";
            return Results.Challenge(properties, [ProductIdentity.OAuthScheme]);
        }).AllowAnonymous();
        group.MapGet("/session", async (HttpContext http) =>
        {
            var grant = await Grant(http);
            return grant is null ? Results.Unauthorized() : Results.Ok(new { verified = true, csrf_token = grant.CsrfToken,
                expires_at = grant.ExpiresAt, operational_access_granted = false });
        }).AllowAnonymous();
        group.MapGet("/payers", async (HttpContext http) =>
        {
            var grant = await Grant(http);
            if (grant is null) return Results.Unauthorized();
            if (http.Request.Query.Keys.Any(key => key != "after")) return Results.BadRequest();
            var after = http.Request.Query["after"].ToString();
            if (after.Length > 0 && !Regex.IsMatch(after, "^[0-7][0-9a-hjkmnp-tv-z]{25}\\z", RegexOptions.CultureInvariant)) return Results.BadRequest();
            var result = await http.RequestServices.GetRequiredService<ProductSetupPayers>().ReadAsync(grant, after.Length == 0 ? null : after, http.RequestAborted);
            return result.Status switch { WorkspaceAuthorityStatus.Denied => Results.StatusCode(403),
                WorkspaceAuthorityStatus.Verified => Results.Ok(new { customers = result.Page!.Customers.Select(x => new {
                    customer_id = x.CustomerId, name = x.Name, workspace_limit = x.WorkspaceLimit }), next_cursor = result.Page.NextCursor }),
                _ => Results.StatusCode(503) };
        }).AllowAnonymous();
        group.MapPost("/companies", Create).AllowAnonymous();
        group.MapPost("/cancel", async (HttpContext http) =>
        {
            var grant = await Grant(http);
            if (grant is null) return Results.Unauthorized();
            if (!ProductSetupGrantStore.CsrfMatches(grant, http.Request.Headers["X-Kuubu-Setup-CSRF"].ToString())) return Results.StatusCode(403);
            var proof = await http.AuthenticateAsync(ProductIdentity.PendingScheme);
            http.RequestServices.GetRequiredService<ProductSetupGrantStore>().Revoke(proof.Principal!.FindFirstValue(ProductIdentity.SetupHandleClaim)!);
            await http.SignOutAsync(ProductIdentity.PendingScheme);
            return Results.NoContent();
        }).AllowAnonymous();
    }

    private static async Task<ProductSetupGrant?> Grant(HttpContext http)
    {
        var proof = await http.AuthenticateAsync(ProductIdentity.PendingScheme);
        if (!proof.Succeeded || proof.Principal?.FindFirstValue(ProductIdentity.IssuerClaim) != ProductCommercialLicense.Issuer
            || !proof.Properties!.Items.TryGetValue(ProductIdentity.SetupPurpose, out var purpose) || purpose != "company-create-v1") return null;
        var handle = proof.Principal.FindFirstValue(ProductIdentity.SetupHandleClaim);
        var subject = proof.Principal.FindFirstValue(ProductIdentity.SubjectClaim);
        return handle is null || subject is null ? null : await http.RequestServices.GetRequiredService<ProductSetupGrantStore>()
            .ReadAsync(handle, subject, http.RequestAborted);
    }

    private static async Task<IResult> Create(HttpContext http)
    {
        var grant = await Grant(http);
        if (grant is null) return Results.Unauthorized();
        if (!ProductSetupGrantStore.CsrfMatches(grant, http.Request.Headers["X-Kuubu-Setup-CSRF"].ToString())) return Results.StatusCode(403);
        var buffer = new byte[4097]; var used = 0;
        while (used < buffer.Length)
        {
            var read = await http.Request.Body.ReadAsync(buffer.AsMemory(used), http.RequestAborted);
            if (read == 0) break;
            used += read;
        }
        if (used > 4096) return Results.StatusCode(413);
        string customer, name, key;
        try
        {
            using var json = JsonDocument.Parse(buffer.AsMemory(0, used), new JsonDocumentOptions { MaxDepth = 4 });
            var root = json.RootElement;
            ProductWorkspaceSetupAuthority.RejectDuplicates(root);
            var allowed = new[] { "customer_id", "company_name", "idempotency_key", "confirm" };
            if (root.ValueKind != JsonValueKind.Object || root.EnumerateObject().Count() != 4
                || root.EnumerateObject().Any(x => !allowed.Contains(x.Name)) || !root.GetProperty("confirm").GetBoolean()) return Results.BadRequest();
            customer = root.GetProperty("customer_id").GetString()!; name = root.GetProperty("company_name").GetString()!;
            key = root.GetProperty("idempotency_key").GetString()!;
            if (customer is null || name is null || key is null || name.Length is < 1 or > 200
                || name.Any(char.IsControl) || string.IsNullOrWhiteSpace(name)
                || !Regex.IsMatch(customer, "^[0-7][0-9a-hjkmnp-tv-z]{25}\\z", RegexOptions.CultureInvariant)
                || !Regex.IsMatch(key, "^[A-Za-z0-9_-]{32,128}\\z", RegexOptions.CultureInvariant)) return Results.BadRequest();
        }
        catch (Exception error) when (error is JsonException or InvalidOperationException or KeyNotFoundException) { return Results.BadRequest(); }
        var authority = await http.RequestServices.GetRequiredService<ProductWorkspaceSetupAuthority>().ReadAsync(grant.Subject, customer, grant.AccessToken, http.RequestAborted);
        if (authority.Status != WorkspaceAuthorityStatus.Verified) return Results.StatusCode(authority.Status == WorkspaceAuthorityStatus.Denied ? 403 : 503);
        var license = await http.RequestServices.GetRequiredService<ProductCommercialLicense>().ReadAsync(authority.Observation!.EnrollmentId, http.RequestAborted);
        if (license.Status != CommercialLicenseStatus.Authorized) return Results.StatusCode(license.Status == CommercialLicenseStatus.Denied ? 403 : 503);
        var result = await http.RequestServices.GetRequiredService<CoreWorkspaceProvisioner>().CreateAsync(authority.Observation, license.Lease!, key, name, http.RequestAborted);
        var status = result.Status switch { WorkspaceCreateStatus.Created => 201, WorkspaceCreateStatus.Existing => 200,
            WorkspaceCreateStatus.Invalid => 400, WorkspaceCreateStatus.Denied => 403,
            WorkspaceCreateStatus.CapacityExhausted or WorkspaceCreateStatus.Conflict => 409, _ => 503 };
        return Results.Json(new { status = result.Status.ToString(), company_id = result.CompanyId, binding_id = result.BindingId,
            operational_access_granted = false }, statusCode: status);
    }
}
