using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Mightyfin.Erp.Hrm.Application;
using Mightyfin.Erp.Hrm.Infrastructure.Data;
using Mightyfin.Erp.Procurement.Application;
using Mightyfin.Erp.Procurement.Infrastructure.Data;

namespace Mightyfin.Erp.Hrm.Api;

/// <summary>Composes Procurement into the existing ERP process and authentication pipeline.</summary>
public static class ProcurementHostIntegration
{
    public static void AddServices(IServiceCollection services, string connection)
    {
        services.AddScoped<ProcurementRequestScope>();
        services.AddScoped<IProcurementScope>(sp => sp.GetRequiredService<ProcurementRequestScope>());
        services.AddDbContext<ProcurementDbContext>(options =>
            options.UseNpgsql(connection, provider =>
                provider.MigrationsHistoryTable("__procurement_migrations", "procurement")));
        services.AddHealthChecks().AddNpgSql(connection, name: "procurement-db");
    }

    public static void MapRoutes(WebApplication app)
    {
        app.MapGet("/api/procurement/v1/meta", (ProcurementRequestScope scope) => Results.Ok(new
        {
            module = "procurement",
            version = 1,
            tenantId = scope.TenantId,
            legalEntityId = scope.LegalEntityId,
            workLocationId = scope.WorkLocationId,
            orgUnitId = scope.OrgUnitId,
            confined = scope.IsConfined,
            phase = "shared-host-access",
            crudEnabled = false,
        })).RequireAuthorization("procurement-access");
    }
}

public sealed class ProcurementRequestScope : IProcurementScope
{
    private bool resolved;
    private string tenantId = "";
    private Guid legalEntityId;
    private string subjectId = "";

    public string TenantId => resolved ? tenantId : throw new InvalidOperationException("Procurement scope has not been resolved.");
    public Guid LegalEntityId => resolved ? legalEntityId : throw new InvalidOperationException("Procurement scope has not been resolved.");
    public string SubjectId => resolved ? subjectId : throw new InvalidOperationException("Procurement scope has not been resolved.");
    public Guid? WorkLocationId { get; private set; }
    public Guid? OrgUnitId { get; private set; }
    public bool IsConfined { get; private set; }
    public string? CorrelationId { get; private set; }

    public void Set(string tenant, Guid entity, string subject, Guid? location, Guid? orgUnit, bool confined, string correlation)
    {
        if (resolved || string.IsNullOrWhiteSpace(tenant) || entity == Guid.Empty || string.IsNullOrWhiteSpace(subject))
            throw new InvalidOperationException("A valid Procurement scope may be set only once.");
        tenantId = tenant;
        legalEntityId = entity;
        subjectId = subject;
        WorkLocationId = location;
        OrgUnitId = orgUnit;
        IsConfined = confined;
        CorrelationId = correlation;
        resolved = true;
    }
}

/// <summary>Resolves entity and branch from authenticated ERP identity and Organization data.</summary>
public sealed class ProcurementScopeMiddleware(RequestDelegate next, IWebHostEnvironment environment)
{
    public async Task InvokeAsync(HttpContext http, ProcurementRequestScope scope, HrmDbContext db)
    {
        if (!http.Request.Path.StartsWithSegments("/api/procurement/v1"))
        {
            await next(http);
            return;
        }

        // Authorization runs before this middleware. Anonymous requests retain the normal 401.
        if (http.User.Identity?.IsAuthenticated != true)
        {
            await next(http);
            return;
        }

        var tenant = http.User.FindFirstValue("tenant");
        var subject = http.User.FindFirstValue("sub") ?? http.User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(tenant) || string.IsNullOrWhiteSpace(subject))
        {
            await Deny(http, StatusCodes.Status403Forbidden, "A workforce tenant and user are required.");
            return;
        }
        if (!Guid.TryParse(subject, out var userId)
            && !(environment.IsDevelopment() && http.User.Identity.AuthenticationType == "dev"))
        {
            await Deny(http, StatusCodes.Status403Forbidden, "This account needs a workforce user mapping.");
            return;
        }

        var requestedEntityText = http.Request.Headers["X-Shell-Entity"].FirstOrDefault()?.Trim();
        var requestedLocationText = http.Request.Headers["X-Shell-Location"].FirstOrDefault()?.Trim();
        Guid? requestedEntity = null;
        Guid? requestedLocation = null;
        if (!string.IsNullOrEmpty(requestedEntityText))
        {
            if (!Guid.TryParse(requestedEntityText, out var parsed) || parsed == Guid.Empty)
            {
                await Deny(http, StatusCodes.Status400BadRequest, "Choose a valid company.");
                return;
            }
            requestedEntity = parsed;
        }
        if (!string.IsNullOrEmpty(requestedLocationText))
        {
            if (!Guid.TryParse(requestedLocationText, out var parsed) || parsed == Guid.Empty)
            {
                await Deny(http, StatusCodes.Status400BadRequest, "Choose a valid branch.");
                return;
            }
            requestedLocation = parsed;
        }

        var legalEntities = await db.LegalEntities.AsNoTracking()
            .Where(x => x.TenantId == tenant && !x.IsArchived)
            .Select(x => new { x.Id, x.IsDefault, x.RegisteredName })
            .ToListAsync(http.RequestAborted);
        if (legalEntities.Count == 0)
        {
            await Deny(http, StatusCodes.Status403Forbidden, "No company is available for this account.");
            return;
        }

        var assignedLocationIds =
            await db.UserBranchAssignments.AsNoTracking()
                .Where(x => x.TenantId == tenant && x.UserId == userId && !x.IsArchived)
                .Select(x => x.LocationId).Distinct().ToListAsync(http.RequestAborted);
        var confined = assignedLocationIds.Count > 0;
        var assignedLocations = confined
            ? await db.WorkLocations.AsNoTracking()
                .Where(x => x.TenantId == tenant && !x.IsArchived && assignedLocationIds.Contains(x.Id))
                .Select(x => new { x.Id, x.LegalEntityId })
                .ToListAsync(http.RequestAborted)
            : [];
        if (confined && assignedLocations.Count == 0)
        {
            await Deny(http, StatusCodes.Status403Forbidden, "Assigned branches are unavailable.");
            return;
        }

        Guid? effectiveLocation = null;
        Guid? effectiveOrgUnit = null;
        Guid? entityFromLocation = null;
        if (requestedLocation.HasValue)
        {
            var workLocation = await db.WorkLocations.AsNoTracking()
                .Where(x => x.TenantId == tenant && x.Id == requestedLocation.Value && !x.IsArchived)
                .Select(x => new { x.Id, x.LegalEntityId })
                .SingleOrDefaultAsync(http.RequestAborted);
            if (workLocation is not null)
            {
                if (confined && !assignedLocationIds.Contains(workLocation.Id))
                {
                    await Deny(http, StatusCodes.Status403Forbidden, "You cannot use that branch.");
                    return;
                }
                effectiveLocation = workLocation.Id;
                entityFromLocation = workLocation.LegalEntityId;
            }
            else if (!confined)
            {
                var orgUnit = await db.OrgUnits.AsNoTracking()
                    .Where(x => x.Id == requestedLocation.Value && !x.IsArchived && x.Status == "active")
                    .Join(db.LegalEntities.Where(e => e.TenantId == tenant && !e.IsArchived),
                        x => x.LegalEntityId, e => e.Id, (x, _) => new { x.Id, x.LegalEntityId })
                    .SingleOrDefaultAsync(http.RequestAborted);
                if (orgUnit is not null)
                {
                    effectiveOrgUnit = orgUnit.Id;
                    entityFromLocation = orgUnit.LegalEntityId;
                }
            }
            if (!entityFromLocation.HasValue)
            {
                await Deny(http, StatusCodes.Status403Forbidden, "That branch is unavailable for this account.");
                return;
            }
        }
        else if (confined)
        {
            var first = assignedLocations
                .Where(x => !requestedEntity.HasValue || x.LegalEntityId == requestedEntity.Value)
                .OrderBy(x => x.Id).FirstOrDefault();
            if (first is null)
            {
                await Deny(http, StatusCodes.Status403Forbidden, "You cannot use that company.");
                return;
            }
            effectiveLocation = first.Id;
            entityFromLocation = first.LegalEntityId;
        }

        if (requestedEntity.HasValue && !legalEntities.Any(x => x.Id == requestedEntity.Value))
        {
            await Deny(http, StatusCodes.Status403Forbidden, "You cannot use that company.");
            return;
        }
        if (requestedEntity.HasValue && entityFromLocation.HasValue && requestedEntity.Value != entityFromLocation.Value)
        {
            await Deny(http, StatusCodes.Status403Forbidden, "The company and branch do not match.");
            return;
        }
        if (confined && requestedEntity.HasValue && !assignedLocations.Any(x => x.LegalEntityId == requestedEntity.Value))
        {
            await Deny(http, StatusCodes.Status403Forbidden, "You cannot use that company.");
            return;
        }

        var selectedEntity = requestedEntity ?? entityFromLocation
            ?? legalEntities.OrderBy(x => x.IsDefault ? 0 : 1).ThenBy(x => x.RegisteredName).First().Id;
        if (!legalEntities.Any(x => x.Id == selectedEntity))
        {
            await Deny(http, StatusCodes.Status403Forbidden, "The selected company is unavailable.");
            return;
        }
        scope.Set(tenant, selectedEntity, subject, effectiveLocation, effectiveOrgUnit, confined,
            http.Response.Headers["X-Request-Id"].FirstOrDefault() ?? http.TraceIdentifier);
        await next(http);
    }

    private static async Task Deny(HttpContext http, int status, string message)
    {
        http.Response.StatusCode = status;
        await http.Response.WriteAsJsonAsync(new ApiError(status == 400 ? "invalid-scope" : "forbidden", message, []),
            http.RequestAborted);
    }
}
