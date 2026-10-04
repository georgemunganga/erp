using Microsoft.EntityFrameworkCore;
using Mightyfin.Erp.Hrm.Infrastructure.Data;

namespace Mightyfin.Erp.Hrm.Api;

public sealed record ProcurementWorkerHome(Guid WorkerId, Guid? LegalEntityId,
    Guid? LocationId, Guid? OrgUnitId);

/// <summary>Checks the HRMS-owned employee mapping for Procurement requests.</summary>
public static class ProcurementWorkforce
{
    public static async Task<ProcurementWorkerHome?> ResolveActiveWorkerHome(HrmDbContext hrm,
        string tenantId, string subjectId, CancellationToken ct)
    {
        Guid? workerId = null;
        if (Guid.TryParse(subjectId, out var accountId))
        {
            var localAccount = await hrm.LocalUsers.AsNoTracking()
                .Where(x => x.TenantId == tenantId && x.Id == accountId && !x.IsArchived)
                .Select(x => new { x.WorkerId, x.IsActive }).SingleOrDefaultAsync(ct);
            if (localAccount is not null)
            {
                if (!localAccount.IsActive || localAccount.WorkerId is null) return null;
                workerId = localAccount.WorkerId;
            }
        }
        if (workerId is null)
            workerId = await hrm.Workers.AsNoTracking()
                .Where(x => x.TenantId == tenantId && x.SubjectId == subjectId && !x.IsArchived)
                .Select(x => (Guid?)x.Id).SingleOrDefaultAsync(ct);
        if (workerId is null) return null;

        var worker = await hrm.Workers.AsNoTracking()
            .Where(x => x.TenantId == tenantId && x.Id == workerId && !x.IsArchived && x.Status == "active")
            .Select(x => new { x.Id, x.LocationId, x.OrgUnitId }).SingleOrDefaultAsync(ct);
        if (worker is null) return null;

        Guid? locationEntity = null;
        if (worker.LocationId is Guid locationId)
        {
            locationEntity = await hrm.WorkLocations.AsNoTracking()
                .Where(x => x.TenantId == tenantId && x.Id == locationId && !x.IsArchived)
                .Select(x => (Guid?)x.LegalEntityId).SingleOrDefaultAsync(ct);
            if (locationEntity is null) return null;
        }

        Guid? unitEntity = null;
        if (worker.OrgUnitId is Guid orgUnitId)
        {
            unitEntity = await hrm.OrgUnits.AsNoTracking()
                .Where(x => x.TenantId == tenantId && x.Id == orgUnitId && !x.IsArchived && x.Status == "active")
                .Select(x => (Guid?)x.LegalEntityId).SingleOrDefaultAsync(ct);
            if (unitEntity is null) return null;
        }
        if (locationEntity.HasValue && unitEntity.HasValue && locationEntity != unitEntity)
            return null;
        return new ProcurementWorkerHome(worker.Id, locationEntity ?? unitEntity,
            worker.LocationId, worker.OrgUnitId);
    }

    public static async Task<Guid?> ResolveActiveWorker(HrmDbContext hrm, ProcurementRequestScope scope,
        CancellationToken ct)
    {
        var home = await ResolveActiveWorkerHome(hrm, scope.TenantId, scope.SubjectId, ct);
        if (home is null) return null;
        if (home.LegalEntityId.HasValue)
            return home.LegalEntityId == scope.LegalEntityId ? home.WorkerId : null;
        return scope.IsConfined ? home.WorkerId : null;
    }
}
