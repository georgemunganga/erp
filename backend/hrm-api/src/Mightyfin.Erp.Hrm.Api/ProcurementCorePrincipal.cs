using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Mightyfin.Erp.Hrm.Application;
using Mightyfin.Erp.Hrm.Infrastructure.Data;

namespace Mightyfin.Erp.Hrm.Api;

/// <summary>Current Core account and its existing configured permissions. Employee data is optional.</summary>
public sealed record ProcurementCorePrincipal(Guid Id, Guid? WorkerId, string[] Roles, string[] Actions)
{
    public Claim[] Claims(string tenant) => [new("tenant", tenant), new("sub", Id.ToString("D")), new(ClaimTypes.NameIdentifier, Id.ToString("D")),
        .. Roles.Select(role => new Claim(ClaimTypes.Role, role)),
        new(ProcurementPermissions.ConfiguredClaim, "true"),
        .. Actions.Select(action => new Claim(ProcurementPermissions.ActionClaim, action))];
}

public static class ProcurementCorePrincipals
{
    private static readonly object CurrentKey = new();
    private sealed record RequestProof(string Tenant, ProcurementCorePrincipal Principal);

    internal static ProcurementCorePrincipal? Cached(HttpContext http, string tenant, string subject) =>
        http.Items.TryGetValue(CurrentKey, out var value) && value is RequestProof proof
        && proof.Tenant == tenant && Guid.TryParse(subject, out var id) && proof.Principal.Id == id ? proof.Principal : null;

    internal static void Apply(HttpContext http, string tenant, ProcurementCorePrincipal principal)
    {
        var remaining = http.User.Claims.Where(x => x.Type is not (ClaimTypes.Role or "role" or "roles")
            && x.Type != ProcurementPermissions.ConfiguredClaim && x.Type != ProcurementPermissions.ActionClaim
            && x.Type != ProcurementPermissions.AdminViewOnlyClaim && x.Type != "sub" && x.Type != ClaimTypes.NameIdentifier
            && x.Type != "tenant");
        http.User = new ClaimsPrincipal(new ClaimsIdentity(remaining.Concat(principal.Claims(tenant)),
            http.User.Identity?.AuthenticationType, ClaimTypes.Name, ClaimTypes.Role));
        // Private object key, populated only from owning Core reads in this request.
        // Neither an HTTP claim/header nor a cross-request/session cache.
        http.Items[CurrentKey] = new RequestProof(tenant, principal);
    }

    public static async Task<ProcurementCorePrincipal?> CurrentAsync(HrmDbContext db, string tenant,
        string subject, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(tenant) || !Guid.TryParse(subject, out var id) || id == Guid.Empty) return null;
        var now = DateTimeOffset.UtcNow;
        // Bound the owning-store read to this single active tenant account. Check
        // its optional lock after projection: SQLite cannot translate DateTimeOffset
        // ordering, and no directory-wide client evaluation is needed or permitted.
        var account = await db.LocalUsers.AsNoTracking().Where(x => x.TenantId == tenant && x.Id == id
            && x.IsActive && !x.IsArchived)
            .Select(x => new { x.Id, x.WorkerId, x.RolesCsv, x.LockedUntil }).SingleOrDefaultAsync(ct);
        if (account is null || account.LockedUntil is { } lockedUntil && lockedUntil > now) return null;
        var assigned = LocalIdentityRoutes.ParseRoles(account.RolesCsv);
        var activeRoles = await db.TenantRoleAssignments.AsNoTracking().Where(role => role.TenantId == tenant
            && role.Active && !role.IsArchived && assigned.Contains(role.RoleKey)).Select(role => role.RoleKey).ToArrayAsync(ct);
        var actions = await ProcurementPermissions.ResolveConfiguredActionsAsync(db, string.Join(',', activeRoles), ct, tenant);
        return new(account.Id, account.WorkerId, activeRoles, actions);
    }
}

/// <summary>Refresh module identity before named authorization, for additions as well as revocations.</summary>
public sealed class ProcurementCorePrincipalRefreshMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext http, HrmDbContext db)
    {
        if (!http.Request.Path.StartsWithSegments("/api/procurement/v1") || http.User.Identity?.IsAuthenticated != true)
        {
            await next(http); return;
        }
        var tenant = http.User.FindFirstValue("tenant");
        var subject = http.User.FindFirstValue("sub") ?? http.User.FindFirstValue(ClaimTypes.NameIdentifier);
        var principal = tenant is null || subject is null ? null
            : await ProcurementCorePrincipals.CurrentAsync(db, tenant, subject, http.RequestAborted);
        if (principal is null)
        {
            http.Response.StatusCode = StatusCodes.Status403Forbidden;
            await http.Response.WriteAsJsonAsync(new ApiError("forbidden", "An active current Core system account is required.", []), http.RequestAborted);
            return;
        }
        ProcurementCorePrincipals.Apply(http, tenant!, principal);
        await next(http);
    }
}
