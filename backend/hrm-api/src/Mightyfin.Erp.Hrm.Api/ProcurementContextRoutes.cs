using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Mightyfin.Erp.Hrm.Infrastructure.Data;

namespace Mightyfin.Erp.Hrm.Api;

/// <summary>Minimum authenticated ERP context needed to initialize Procurement UI.</summary>
public static class ProcurementContextRoutes
{
    public static void MapRoutes(WebApplication app)
    {
        app.MapGet("/api/procurement/v1/context", GetAsync)
            .RequireAuthorization("procurement-access");
    }

    private static async Task<IResult> GetAsync(HttpContext http, HrmDbContext db,
        ProcurementRequestScope scope, CancellationToken ct)
    {
        var context = await BuildAsync(db, scope, http.User.Claims, ct);
        return context is null
            ? Results.Problem(statusCode: StatusCodes.Status403Forbidden,
                title: "The Procurement company or active worker is unavailable.")
            : Results.Ok(context);
    }

    /// <summary>
    /// The middleware resolves the selected company and active worker. Recheck
    /// both here before exposing display information. Multi-company choices
    /// require an explicit workforce authorization model, so this endpoint
    /// offers only the verified current company.
    /// </summary>
    public static async Task<ProcurementContextResponse?> BuildAsync(HrmDbContext db,
        ProcurementRequestScope scope, IEnumerable<Claim> claims, CancellationToken ct = default)
    {
        if (scope.WorkerId is not Guid workerId || workerId == Guid.Empty)
            return null;

        var company = await db.LegalEntities.AsNoTracking()
            .Where(x => x.TenantId == scope.TenantId && x.Id == scope.LegalEntityId && !x.IsArchived)
            .Select(x => new ProcurementCompany(x.Id, x.Code, x.RegisteredName))
            .SingleOrDefaultAsync(ct);
        if (company is null) return null;

        var worker = await db.Workers.AsNoTracking()
            .Where(x => x.TenantId == scope.TenantId && x.Id == workerId && !x.IsArchived && x.Status == "active")
            .Select(x => new { x.Id, x.LocationId, x.OrgUnitId })
            .SingleOrDefaultAsync(ct);
        if (worker is null) return null;

        var branchId = scope.WorkLocationId ?? worker.LocationId;
        ProcurementLocation? branch = null;
        if (branchId is Guid selectedBranchId)
        {
            branch = await db.WorkLocations.AsNoTracking()
                .Where(x => x.TenantId == scope.TenantId && x.Id == selectedBranchId
                    && x.LegalEntityId == company.Id && !x.IsArchived)
                .Select(x => new ProcurementLocation(x.Id, x.Code, x.Name))
                .SingleOrDefaultAsync(ct);
            if (branch is null) return null;
        }

        var orgUnitId = scope.OrgUnitId ?? worker.OrgUnitId;
        ProcurementLocation? orgUnit = null;
        if (orgUnitId is Guid selectedOrgUnitId)
        {
            orgUnit = await db.OrgUnits.AsNoTracking()
                .Where(x => x.TenantId == scope.TenantId && x.Id == selectedOrgUnitId
                    && x.LegalEntityId == company.Id && !x.IsArchived && x.Status == "active")
                .Select(x => new ProcurementLocation(x.Id, x.Code, x.Name))
                .SingleOrDefaultAsync(ct);
            if (orgUnit is null) return null;
        }

        if (branch is null && orgUnit is null) return null;

        var granted = ProcurementPermissions.GrantedPolicies(claims);
        if (!granted.Contains("procurement-access", StringComparer.Ordinal)) return null;

        return new ProcurementContextResponse(scope.TenantId, company.Id, worker.Id,
            company, branch, orgUnit, [company],
            ProcurementPermissions.ProcurementRoles(claims), granted, scope.IsConfined);
    }
}

public sealed record ProcurementCompany(Guid Id, string Code, string Name);
public sealed record ProcurementLocation(Guid Id, string Code, string Name);
public sealed record ProcurementContextResponse(
    string TenantId,
    Guid LegalEntityId,
    Guid WorkerId,
    ProcurementCompany Company,
    ProcurementLocation? Branch,
    ProcurementLocation? OrgUnit,
    ProcurementCompany[] AllowedCompanies,
    string[] Roles,
    string[] Permissions,
    bool Confined);
