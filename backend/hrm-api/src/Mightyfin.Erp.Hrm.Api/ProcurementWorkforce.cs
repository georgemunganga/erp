using Microsoft.EntityFrameworkCore;
using Mightyfin.Erp.Hrm.Infrastructure.Data;

namespace Mightyfin.Erp.Hrm.Api;

/// <summary>Checks the HRMS-owned employee mapping for Procurement mutations.</summary>
public static class ProcurementWorkforce
{
    public static async Task<Guid?> ResolveActiveWorker(HrmDbContext hrm, ProcurementRequestScope scope,
        CancellationToken ct)
    {
        Guid? workerId = null;
        if (Guid.TryParse(scope.SubjectId, out var accountId))
        {
            var localAccount = await hrm.LocalUsers.AsNoTracking()
                .Where(x => x.TenantId == scope.TenantId && x.Id == accountId && !x.IsArchived)
                .Select(x => new { x.WorkerId, x.IsActive }).SingleOrDefaultAsync(ct);
            if (localAccount is not null)
            {
                if (!localAccount.IsActive || localAccount.WorkerId is null) return null;
                workerId = localAccount.WorkerId;
            }
        }
        if (workerId is null)
            workerId = await hrm.Workers.AsNoTracking()
                .Where(x => x.TenantId == scope.TenantId && x.SubjectId == scope.SubjectId && !x.IsArchived)
                .Select(x => (Guid?)x.Id).SingleOrDefaultAsync(ct);
        if (workerId is null) return null;
        var worker = await hrm.Workers.AsNoTracking()
            .Where(x => x.TenantId == scope.TenantId && x.Id == workerId && !x.IsArchived && x.Status == "active")
            .Select(x => new { x.Id, x.LocationId, x.OrgUnitId }).SingleOrDefaultAsync(ct);
        if (worker is null) return null;
        if (worker.LocationId is Guid locationId)
        {
            var entity = await hrm.WorkLocations.AsNoTracking()
                .Where(x => x.TenantId == scope.TenantId && x.Id == locationId && !x.IsArchived)
                .Select(x => (Guid?)x.LegalEntityId).SingleOrDefaultAsync(ct);
            if (entity != scope.LegalEntityId) return null;
        }
        if (worker.OrgUnitId is Guid orgUnitId)
        {
            var entity = await hrm.OrgUnits.AsNoTracking()
                .Where(x => x.TenantId == scope.TenantId && x.Id == orgUnitId && !x.IsArchived)
                .Select(x => (Guid?)x.LegalEntityId).SingleOrDefaultAsync(ct);
            if (entity != scope.LegalEntityId) return null;
        }
        if (worker.LocationId is null && worker.OrgUnitId is null && !scope.IsConfined)
            return null;
        return worker.Id;
    }
}
