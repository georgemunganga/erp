using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Mightyfin.Erp.Hrm.Application;
using Mightyfin.Erp.Hrm.Infrastructure.Data;

namespace Mightyfin.Erp.Hrm.Api;

/// <summary>
/// Every route also checks the resolved entity and its own record/state scope.
/// Every Procurement request revalidates its actual Core account and configured
/// grants; authentication claims alone never establish module authority.
/// </summary>
public static class ProcurementPermissions
{
    private static readonly IReadOnlyDictionary<string, string[]> RoleGrants = ProcurementActionCatalog.BootstrapGrants;
    private static bool ExplicitReturnDraftGrant(string action) => action is
        "procurement-return-read" or "procurement-return-create" or "procurement-return-edit";
    public const string ConfiguredClaim = "procurement_permissions_configured";
    public const string ActionClaim = "procurement_permission";
    // Legacy wire claim is stripped on current Core-principal requests; never emitted.
    public const string AdminViewOnlyClaim = "procurement_admin_view_only";
    public static bool IsReadOnlyPolicy(string policy) => policy == "procurement-access"
        || policy.EndsWith("-read", StringComparison.Ordinal) || policy == "procurement-receipt-view";

    public static async Task<string[]> ResolveConfiguredActionsAsync(HrmDbContext db, string csv, CancellationToken ct, string? tenantId = null)
    {
        var assigned = LocalIdentityRoutes.ParseRoles(csv);
        if (assigned.Length == 0) return [];
        var configured = await db.TenantRoleAssignments.AsNoTracking()
            .Where(r => r.Active && !r.IsArchived && assigned.Contains(r.RoleKey)
                && (tenantId == null || r.TenantId == tenantId))
            .Select(r => new { r.RoleKey, r.PermissionsCsv }).ToListAsync(ct);
        var grants = new HashSet<string>(StringComparer.Ordinal);
        foreach (var role in assigned)
        {
            var row = configured.FirstOrDefault(r => r.RoleKey.Equals(role, StringComparison.OrdinalIgnoreCase));
            if (row is null) continue;
            foreach (var action in ProcurementActionCatalog.ResolveConfiguredActionsForRole(role, row.PermissionsCsv))
                grants.Add(action);
        }
        return grants.Order(StringComparer.Ordinal).ToArray();
    }

    public static void AddPolicies(AuthorizationOptions options)
    {
        foreach (var (name, _) in RoleGrants)
        {
            var policyName = name;
            options.AddPolicy(policyName, policy => policy.RequireAuthenticatedUser()
                .RequireClaim("tenant")
                .RequireAssertion(context => Allows(policyName, context.User.Claims)));
        }
    }

    public static bool Allows(string policy, IEnumerable<Claim> claims)
    {
        if (!RoleGrants.TryGetValue(policy, out var roles)) return false;
        var principal = WorkerPrincipal.FromClaims(claims);
        if (claims.Any(x => x.Type == ConfiguredClaim))
        {
            var hasAccess = claims.Any(x => x.Type == ActionClaim && x.Value == "procurement-access");
            return hasAccess && (policy == "procurement-access"
                || claims.Any(x => x.Type == ActionClaim && x.Value == policy));
        }
        if (ExplicitReturnDraftGrant(policy)) return false;
        return principal.IsRole("erp_admin") || principal.IsRole(roles);
    }

    public static string[] GrantedPolicies(IEnumerable<Claim> claims)
    {
        return RoleGrants.Where(x => Allows(x.Key, claims))
            .Select(x => x.Key).Order(StringComparer.Ordinal).ToArray();
    }

    public static string[] ProcurementRoles(IEnumerable<Claim> claims)
    {
        var principal = WorkerPrincipal.FromClaims(claims);
        var knownRoles = RoleGrants.Values.SelectMany(x => x).ToHashSet(StringComparer.OrdinalIgnoreCase);
        knownRoles.Add("erp_admin");
        return principal.Roles.Where(knownRoles.Contains)
            .Order(StringComparer.OrdinalIgnoreCase).ToArray();
    }
}
