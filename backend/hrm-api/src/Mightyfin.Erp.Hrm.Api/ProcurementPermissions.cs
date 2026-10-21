using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Mightyfin.Erp.Hrm.Application;
using Mightyfin.Erp.Hrm.Infrastructure.Data;

namespace Mightyfin.Erp.Hrm.Api;

/// <summary>
/// Every route also checks the resolved entity and its own record/state scope.
/// Local sessions use Core's configured action grants; OIDC uses bootstrap
/// roles until the IdP-to-Core permission contract is established.
/// </summary>
public static class ProcurementPermissions
{
    private static readonly IReadOnlyDictionary<string, string[]> RoleGrants = ProcurementActionCatalog.BootstrapGrants;
    public const string ConfiguredClaim = "procurement_permissions_configured";
    public const string ActionClaim = "procurement_permission";
    // Added only after the middleware verifies a worker-less Core admin GET.
    // A narrowing claim: it cannot grant an action or replace existing authorization.
    public const string AdminViewOnlyClaim = "procurement_admin_view_only";
    public static bool IsReadOnlyPolicy(string policy) => policy == "procurement-access"
        || policy.EndsWith("-read", StringComparison.Ordinal) || policy == "procurement-receipt-view";

    public static async Task<string[]> ResolveConfiguredActionsAsync(HrmDbContext db, string csv, CancellationToken ct)
    {
        var assigned = LocalIdentityRoutes.ParseRoles(csv);
        if (assigned.Length == 0) return [];
        var configured = await db.TenantRoleAssignments.AsNoTracking()
            .Where(r => r.Active && assigned.Contains(r.RoleKey)).ToListAsync(ct);
        var known = RoleGrants.Keys.ToHashSet(StringComparer.Ordinal);
        var grants = new HashSet<string>(StringComparer.Ordinal);
        foreach (var role in assigned)
        {
            var row = configured.FirstOrDefault(r => r.RoleKey.Equals(role, StringComparison.OrdinalIgnoreCase));
            if (row is null) continue;
            var explicitActions = row.PermissionsCsv.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Where(known.Contains).ToArray();
            foreach (var action in explicitActions.Length > 0 ? explicitActions : ProcurementActionCatalog.ForRole(role))
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
        if (claims.Any(x => x.Type == AdminViewOnlyClaim && x.Value == "true")
            && !IsReadOnlyPolicy(policy)) return false;
        var principal = WorkerPrincipal.FromClaims(claims);
        if (claims.Any(x => x.Type == ConfiguredClaim))
        {
            if (principal.IsRole("erp_admin")) return true;
            var hasAccess = claims.Any(x => x.Type == ActionClaim && x.Value == "procurement-access");
            return hasAccess && (policy == "procurement-access"
                || claims.Any(x => x.Type == ActionClaim && x.Value == policy));
        }
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
