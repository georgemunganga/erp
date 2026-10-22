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
                title: "The current Core account, permissions or company scope is unavailable.")
            : Results.Ok(context);
    }

    /// <summary>Current Core account, grants and organisation scope; HR profile is optional.</summary>
    public static async Task<ProcurementContextResponse?> BuildAsync(HrmDbContext db,
        ProcurementRequestScope scope, IEnumerable<Claim> claims, CancellationToken ct = default)
    {
        var principal = await ProcurementCorePrincipals.CurrentAsync(db, scope.TenantId, scope.SubjectId, ct);
        if (principal is null || scope.PrincipalId != principal.Id) return null;
        var currentClaims = principal.Claims(scope.TenantId);
        var granted = ProcurementPermissions.GrantedPolicies(currentClaims);
        if (!granted.Contains("procurement-access", StringComparer.Ordinal)) return null;
        var company = await db.LegalEntities.AsNoTracking()
            .Where(x => x.TenantId == scope.TenantId && x.Id == scope.LegalEntityId && !x.IsArchived)
            .Select(x => new ProcurementCompany(x.Id, x.Code, x.RegisteredName, x.Currency)).SingleOrDefaultAsync(ct);
        if (company is null) return null;
        var assignments = await db.UserBranchAssignments.AsNoTracking()
            .Where(x => x.TenantId == scope.TenantId && x.UserId == principal.Id && !x.IsArchived)
            .Select(x => x.LocationId).Distinct().ToArrayAsync(ct);
        if (assignments.Length > 0 && (scope.WorkLocationId is not Guid selected || !assignments.Contains(selected))) return null;
        ProcurementLocation? branch = null;
        if (scope.WorkLocationId is Guid branchId)
        {
            branch = await db.WorkLocations.AsNoTracking().Where(x => x.TenantId == scope.TenantId
                && x.Id == branchId && x.LegalEntityId == company.Id && !x.IsArchived)
                .Select(x => new ProcurementLocation(x.Id, x.Code, x.Name)).SingleOrDefaultAsync(ct);
            if (branch is null) return null;
        }
        ProcurementLocation? unit = null;
        if (scope.OrgUnitId is Guid unitId)
        {
            unit = await db.OrgUnits.AsNoTracking().Where(x => x.TenantId == scope.TenantId
                && x.Id == unitId && x.LegalEntityId == company.Id && !x.IsArchived && x.Status == "active")
                .Select(x => new ProcurementLocation(x.Id, x.Code, x.Name)).SingleOrDefaultAsync(ct);
            if (unit is null) return null;
        }
        var workerId = await ProcurementWorkforce.ResolveActiveWorker(db, scope, ct);
        if (workerId != scope.WorkerId) return null;
        return new ProcurementContextResponse(scope.TenantId, company.Id, workerId, company, branch, unit,
            [company], ProcurementPermissions.ProcurementRoles(currentClaims), granted, assignments.Length > 0,
            false, principal.Id);
    }

}

public sealed record ProcurementCompany(Guid Id, string Code, string Name, string CurrencyCode);
public sealed record ProcurementLocation(Guid Id, string Code, string Name);
public sealed record ProcurementContextResponse(
    string TenantId,
    Guid LegalEntityId,
    Guid? WorkerId,
    ProcurementCompany Company,
    ProcurementLocation? Branch,
    ProcurementLocation? OrgUnit,
    ProcurementCompany[] AllowedCompanies,
    string[] Roles,
    string[] Permissions,
    bool Confined,
    bool AdminViewOnly = false,
    Guid? PrincipalId = null);
