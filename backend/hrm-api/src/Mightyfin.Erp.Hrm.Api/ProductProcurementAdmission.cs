using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Mightyfin.Erp.Hrm.Infrastructure.Data;

namespace Mightyfin.Erp.Hrm.Api;

/// <summary>Procurement projection of a freshly admitted selected product session.
/// Does not consult or change legacy RolesCsv or branch assignments.</summary>
internal static class ProductProcurementAdmission
{
    internal static bool Enabled(IConfiguration configuration) =>
        ProductRequestAuthentication.Enabled(configuration)
        && configuration.GetValue<bool>("ERP:ProductProcurement:Enabled");

    internal static bool IsEndpoint(HttpContext http, IConfiguration configuration)
    {
        if (!Enabled(configuration) || !http.Request.Path.StartsWithSegments("/api/procurement/v1")) return false;
        var endpoint = http.GetEndpoint();
        var required = endpoint?.Metadata.GetOrderedMetadata<IAuthorizeData>();
        return endpoint is not null && required is { Count: > 0 }
            && endpoint.Metadata.GetMetadata<ProductProcurementRequestPolicy>() is not null
            && endpoint.Metadata.GetMetadata<IAllowAnonymous>() is null
            && required.All(policy => !string.IsNullOrEmpty(policy.Policy)
                && CoreModuleAccessCatalog.Actions("procurement").Contains(policy.Policy, StringComparer.Ordinal));
    }

    internal static bool Allows(CoreErpRequestScope? scope) => scope is { IsCurrent: true, Module: "procurement" }
        && scope.AllowsAction("procurement-access");

    internal static ProcurementCorePrincipal? Principal(CoreErpRequestScope? scope, Guid? worker)
        => Allows(scope) ? new(scope!.PrincipalId, worker, scope.Roles.ToArray(), scope.Actions.ToArray()) : null;

    internal static async Task<ProcurementCorePrincipal?> PrincipalAsync(HttpContext http, HrmDbContext db)
    {
        var scope = ProductRequestContext.Current(http);
        if (!Allows(scope)) return null;
        var account = await db.LocalUsers.AsNoTracking().Where(x => x.TenantId == scope!.TenantId
                && x.Id == scope.PrincipalId && x.IsActive && !x.IsArchived)
            .Select(x => new { x.WorkerId, x.LockedUntil }).SingleOrDefaultAsync(http.RequestAborted);
        if (account is null || account.LockedUntil is { } until && until > DateTimeOffset.UtcNow) return null;
        return Principal(scope, account.WorkerId);
    }

    /// <summary>Selectors can only narrow the server-owned company and locations.</summary>
    internal static bool SelectionWithin(CoreErpRequestScope? scope, Guid company, Guid? branch,
        bool allBranches, IReadOnlyCollection<Guid> activeCompanyBranches)
    {
        if (!Allows(scope) || company != scope!.CompanyId || branch.HasValue && allBranches) return false;
        if (branch is { } id) return id != Guid.Empty && activeCompanyBranches.Contains(id)
            && (scope.AllCompanyLocations || scope.LocationIds.Contains(id));
        if (!allBranches) return false;
        // Empty legacy assignments never convert a restricted grant to company-wide.
        return scope.AllCompanyLocations || scope.LocationIds.Count > 0
            && scope.LocationIds.All(activeCompanyBranches.Contains);
    }
}

internal sealed record ProductProcurementRequestPolicy;
