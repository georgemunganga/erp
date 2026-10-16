using System.Security.Claims;

namespace Mightyfin.Erp.Hrm.Application;

/// <summary>Typed principal resolved from the OIDC token claims (mirrors the Go
/// skeleton's auth.Principal: subject, tenant, environment, scopes, roles). When
/// ERP_AUTH_MODE=disabled, the developer middleware synthesizes a local-developer
/// principal so local testing never needs Keycloak running.</summary>
public sealed class WorkerPrincipal
{
    public string SubjectId { get; init; } = "";
    public string TenantId { get; init; } = "";
    public string Environment { get; init; } = "local";
    public string[] Roles { get; init; } = [];
    public string[] Scopes { get; init; } = [];
    public bool IsDeveloperFallback { get; init; }

    public bool IsRole(params string[] roles) => roles.Any(r => Roles.Contains(r, StringComparer.OrdinalIgnoreCase));
    public bool CanPayroll => IsRole("payroll", "hr_admin");
    public bool CanHr => IsRole("hr_ops", "hr_admin", "payroll");
    public bool CanInvestigate => IsRole("investigator", "hr_admin");

    public static WorkerPrincipal FromClaims(IEnumerable<Claim> claims)
    {
        static string First(IEnumerable<Claim> c, string type) =>
            c.FirstOrDefault(x => x.Type == type)?.Value ?? "";
        // Microsoft's JWT handler keeps a multivalued JSON-array claim (e.g.
        // realm_access.roles = ["hr_admin","manager"]) as a SINGLE claim whose
        // value is the raw JSON array string, so parse it apart when needed.
        static string[] ParseRoleClaims(Claim claim)
        {
            var v = claim.Value?.Trim();
            if (v is null || v.Length == 0) return [];
            if (v.StartsWith('['))
            {
                return v.Trim('[', ']').Split(',')
                        .Select(p => p.Trim().Trim('"'))
                        .Where(s => s.Length > 0)
                        .ToArray();
            }
            return [v];
        }
        // Keycloak may emit the roles either as a claim "realm_access.roles"
        // (one claim per value) or as a single JSON claim "realm_access" with
        // {"roles":[...]}; handle both shapes.
        var roleClaims = claims.Where(c => c.Type is "realm_access.roles" or ClaimTypes.Role or "roles")
            .SelectMany(c => ParseRoleClaims(c));
        var realmAccess = claims.FirstOrDefault(c => c.Type == "realm_access");
        if (realmAccess is not null)
        {
            try
            {
                var json = System.Text.Json.JsonSerializer.Deserialize<System.Text.Json.JsonElement>(realmAccess.Value);
                if (json.TryGetProperty("roles", out var rolesProp) && rolesProp.ValueKind is System.Text.Json.JsonValueKind.Array)
                    roleClaims = roleClaims.Concat(rolesProp.EnumerateArray().Select(e => e.GetString() ?? ""));
                else if (json.ValueKind is System.Text.Json.JsonValueKind.Array)
                    roleClaims = roleClaims.Concat(json.EnumerateArray().Select(e => e.GetString() ?? ""));
            }
            catch { /* malformed json; skip */ }
        }
        var roles = roleClaims
            .Where(r => r.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var isDeveloperFallback = string.Equals(First(claims, ClaimTypes.NameIdentifier), "dev-user-001", StringComparison.Ordinal)
            && string.Equals(First(claims, "preferred_username"), "developer", StringComparison.Ordinal);
        return new WorkerPrincipal
        {
            SubjectId = First(claims, "sub"),
            IsDeveloperFallback = isDeveloperFallback,
            TenantId = First(claims, "tenant"),
            Environment = First(claims, "environment") switch { "" => "local", var v => v },
            Roles = roles,
            Scopes = claims.Where(c => c.Type == "scope").Select(c => c.Value).ToArray(),
        };
    }
}

/// <summary>
/// Product admission boundary for HRM. A valid platform identity is not, by
/// itself, an HRM user: the identity must also carry a workforce role issued
/// for this ERP module. Tenant/customer roles from other products (for
/// example EFaaS tenant_owner) deliberately do not grant entry.
/// </summary>
public static class HrmStaffAccess
{
    public static readonly string[] Roles =
        ["employee", "manager", "hr_ops", "payroll", "finance_approver", "hr_admin", "erp_admin", "investigator", "timesheet_operator"];

    public static bool IsStaff(IEnumerable<Claim> claims) =>
        WorkerPrincipal.FromClaims(claims).IsRole(Roles);
}

/// <summary>Standard error payload shape matching the ERP API conventions
/// ({code, message, details[]}).</summary>
public sealed record ApiError(string Code, string Message, string[] Details);

/// <summary>Typed pagination result used across list endpoints.</summary>
public sealed record Paged<T>(List<T> Items, int TotalCount, int Page, int PageSize);
