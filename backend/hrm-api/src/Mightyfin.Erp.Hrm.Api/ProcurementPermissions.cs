using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Mightyfin.Erp.Hrm.Application;

namespace Mightyfin.Erp.Hrm.Api;

/// <summary>
/// Initial role-to-action grants for Procurement. Every route also checks the
/// resolved entity and its own record/state scope. Tenant-configured grants
/// replace this bootstrap map in the next policy milestone.
/// </summary>
public static class ProcurementPermissions
{
    private static readonly Dictionary<string, string[]> RoleGrants = new(StringComparer.Ordinal)
    {
        ["procurement-access"] = ["employee", "manager", "procurement_requester", "procurement_buyer", "procurement_manager", "supplier_admin", "ap_processor"],
        ["procurement-vendor-read"] = ["employee", "manager", "procurement_requester", "procurement_buyer", "procurement_manager", "supplier_admin"],
        ["procurement-vendor-create"] = ["employee", "manager", "procurement_requester", "procurement_buyer", "procurement_manager", "supplier_admin"],
        ["procurement-vendor-edit"] = ["employee", "manager", "procurement_requester", "procurement_buyer", "procurement_manager", "supplier_admin"],
        ["procurement-vendor-manage"] = ["procurement_buyer", "procurement_manager", "supplier_admin"],
        ["procurement-item-read"] = ["employee", "manager", "procurement_requester", "procurement_buyer", "procurement_manager", "supplier_admin"],
        ["procurement-item-manage"] = ["procurement_buyer", "procurement_manager"],
        ["procurement-request-read"] = ["employee", "manager", "procurement_requester", "procurement_buyer", "procurement_manager"],
        ["procurement-request-create"] = ["employee", "manager", "procurement_requester", "procurement_buyer", "procurement_manager"],
        ["procurement-request-edit"] = ["employee", "manager", "procurement_requester", "procurement_buyer", "procurement_manager"],
    };

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
        return WorkerPrincipal.FromClaims(claims).IsRole(roles);
    }

    public static string[] GrantedPolicies(IEnumerable<Claim> claims)
    {
        var principal = WorkerPrincipal.FromClaims(claims);
        return RoleGrants.Where(x => principal.IsRole(x.Value))
            .Select(x => x.Key).Order(StringComparer.Ordinal).ToArray();
    }

    public static string[] ProcurementRoles(IEnumerable<Claim> claims)
    {
        var principal = WorkerPrincipal.FromClaims(claims);
        var knownRoles = RoleGrants.Values.SelectMany(x => x).ToHashSet(StringComparer.OrdinalIgnoreCase);
        return principal.Roles.Where(knownRoles.Contains)
            .Order(StringComparer.OrdinalIgnoreCase).ToArray();
    }
}
