using Microsoft.EntityFrameworkCore;
using Mightyfin.Erp.Hrm.Application;
using Mightyfin.Erp.Hrm.Infrastructure.Data;

namespace Mightyfin.Erp.Hrm.Api;

/// <summary>M44: Reads X-Shell-Location / X-Shell-Entity headers and stashes the
/// resulting work scope on the per-request ShellContext. The frontend sends
/// these headers on operational requests from the branch-selector state.
/// Headers are validated against tenant data so a client cannot fabricate or
/// widen a scope; non-HRM paths (health checks, OpenAPI, auth) pass through
/// untouched and leave the scope empty (org-wide by default).</summary>
public sealed class ShellContextMiddleware(RequestDelegate next, ILogger<ShellContextMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext http, ShellContext scope, ITenantAccessor tenant, HrmDbContext db)
    {
        if (!http.Request.Path.StartsWithSegments("/api/hrm")
            && !http.Request.Path.StartsWithSegments("/api/v1/hrm"))
        {
            await next(http);
            return;
        }

        if(http.User.Identity?.AuthenticationType==ProductRequestAuthentication.Scheme) {
            var verified=ProductRequestContext.Current(http);
            if(verified is null || verified.Module!="hrm"){http.Response.StatusCode=403;return;}
            scope.EntityId=verified.CompanyId;scope.LocationId=null;scope.OrgUnitId=null;
            scope.AllowedLocationIds.Clear();
            if(!verified.AllCompanyLocations)scope.AllowedLocationIds.AddRange(verified.LocationIds);
            if (ProductHrmWorkspace.IsEndpoint(http, http.RequestServices.GetRequiredService<IConfiguration>())
                && await ProductHrmWorkspace.CoversCompanyAsync(http, db, http.RequestAborted))
            {
                // The compatibility gate proved this uniquely provisioned tenant
                // contains only this company and every historical/current location
                // is covered. Legacy company configuration may use its normal
                // company-wide rules; the request/commit gate prevents expansion.
                scope.AllowedLocationIds.Clear();
                if (Guid.TryParse(http.Request.Headers["X-Shell-Location"].ToString(), out var selected)
                    && await db.WorkLocations.AnyAsync(x => x.Id == selected && x.LegalEntityId == verified.CompanyId
                        && (verified.AllCompanyLocations || verified.LocationIds.Contains(selected)), http.RequestAborted))
                    scope.LocationId = selected;
            }
            await next(http);return;
        }
        var tenantId = tenant.GetTenantId();

        // M45: load the operator's branch confinement (empty = org-wide top HR).
        // JwtSecurityTokenHandler maps "sub" to NameIdentifier, so check both.
        var rawSubject = http.User.FindFirst("sub")?.Value
            ?? http.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        Guid operatorId = Guid.Empty;
        if (!string.IsNullOrEmpty(rawSubject) && Guid.TryParse(rawSubject, out operatorId))
        {
            var allowed = await db.UserBranchAssignments
                .Where(x => x.UserId == operatorId)
                .Select(x => x.LocationId)
                .ToListAsync(http.RequestAborted);
            scope.AllowedLocationIds.AddRange(allowed);
        }

        var rawLocation = http.Request.Headers["X-Shell-Location"].FirstOrDefault()?.Trim();
        var rawEntity = http.Request.Headers["X-Shell-Entity"].FirstOrDefault()?.Trim();

        // M54: the switcher's branches are organisational UNITS (departments,
        // divisions — the entity-tree), while confinement assignments and
        // payroll/attendance run scopes are WORK LOCATIONS. One header, two
        // possible meanings: resolve against both, never silently widen.
        if (Guid.TryParse(rawLocation, out var locationId) && locationId != Guid.Empty)
        {
            var isWorkLocation = await db.WorkLocations
                .AnyAsync(x => x.Id == locationId && x.TenantId == tenantId, http.RequestAborted);
            // EntityId is informational and the entity header (below) is
            // parsed after this block — match any active org unit under the
            // tenant's legal entities instead; the unit's entity then becomes
            // the request's effective entity.
            var isOrgUnit = await db.OrgUnits
                .Where(u => u.Id == locationId && (u.Status == "active" || u.Status == "suspended"))
                .Join(db.LegalEntities.Where(e => e.TenantId == tenantId),
                      u => u.LegalEntityId, e => e.Id, (u, e) => new { u.Id, u.LegalEntityId })
                .AnyAsync(http.RequestAborted);
            if (scope.IsConfined)
            {
                // M45 + M54: confined operators may NEVER widen their scope.
                // A work location header must be one of their assignments; an
                // org-unit header must sit under an assigned work location.
                bool allowed = scope.AllowedLocationIds.Contains(locationId);
                if (!allowed && isOrgUnit)
                {
                    var underAssigned = await db.OrgUnits
                        .Where(u => u.Id == locationId)
                        .Select(u => u.LegalEntityId)
                        .FirstOrDefaultAsync(http.RequestAborted);
                    var assignedEntities = await db.WorkLocations
                        .Where(l => scope.AllowedLocationIds.Contains(l.Id))
                        .Select(l => l.LegalEntityId)
                        .ToListAsync(http.RequestAborted);
                    allowed = underAssigned != Guid.Empty && assignedEntities.Contains(underAssigned);
                }
                if (!allowed)
                {
                    logger.LogWarning("Operator {Op} confined to {Allowed} tried header {Loc}",
                        operatorId, string.Join(',', scope.AllowedLocationIds), locationId);
                    http.Response.StatusCode = StatusCodes.Status403Forbidden;
                    await http.Response.WriteAsJsonAsync(new { error = "forbidden", message = "You are confined to your assigned branches." }, http.RequestAborted);
                    return;
                }
                scope.LocationId = isWorkLocation ? locationId : null;
                scope.OrgUnitId = isOrgUnit ? locationId : null;
                if (isOrgUnit && !scope.EntityId.HasValue)
                {
                    scope.EntityId = await db.OrgUnits
                        .Where(u => u.Id == locationId)
                        .Select(u => u.LegalEntityId)
                        .FirstOrDefaultAsync(http.RequestAborted);
                }
            }
            else if (isWorkLocation)
            {
                scope.LocationId = locationId;
            }
            else if (isOrgUnit)
            {
                scope.OrgUnitId = locationId;
                if (!scope.EntityId.HasValue)
                {
                    scope.EntityId = await db.OrgUnits
                        .Where(u => u.Id == locationId)
                        .Select(u => u.LegalEntityId)
                        .FirstOrDefaultAsync(http.RequestAborted);
                }
            }
            else
            {
                logger.LogWarning("X-Shell-Location {Loc} is neither a work location nor an org unit under tenant {Tenant}; ignoring scope header", locationId, tenantId);
            }
        }
        else if (scope.IsConfined)
        {
            // Confined operator supplied no scope header: default to their
            // first assigned branch so their work is never accidentally org-wide.
            scope.LocationId = scope.AllowedLocationIds.FirstOrDefault();
        }

        if (Guid.TryParse(rawEntity, out var entityId) && entityId != Guid.Empty)
        {
            if (await db.LegalEntities.AnyAsync(x => x.Id == entityId && x.TenantId == tenantId, http.RequestAborted))
                scope.EntityId = entityId;
            else
                logger.LogWarning("X-Shell-Entity {Ent} not found under tenant {Tenant}; ignoring scope header", entityId, tenantId);
        }
        else if (!scope.LocationId.HasValue)
        {
            // No scope header: default to the tenant's primary legal entity.
            scope.EntityId = await db.LegalEntities
                .Where(x => x.TenantId == tenantId)
                .OrderBy(x => x.IsDefault ? 0 : 1)
                .ThenBy(x => x.RegisteredName)
                .Select(x => x.Id)
                .FirstOrDefaultAsync(http.RequestAborted);
        }

        await next(http);
    }
}
