using Microsoft.EntityFrameworkCore;
using Mightyfin.Erp.Hrm.Infrastructure.Data;

namespace Mightyfin.Erp.Hrm.Api;

/// <summary>Current Core branch projection shared by Procurement execution adapters.
/// The coordinator owns the paired tenant session fence before the transaction snapshot.</summary>
internal static class ProcurementExecutionRecordScope
{
    internal static async Task<Guid[]> Branches(HrmDbContext core, ProcurementRequestScope scope, Guid? recordBranch, bool target, CancellationToken ct)
    {
        if (!Guid.TryParse(scope.SubjectId, out var principal)) throw new UnauthorizedAccessException("ProcurementCurrentScopeRequired");
        var branches = await core.UserBranchAssignments.AsNoTracking().Where(x => x.UserId == principal && !x.IsArchived).Select(x => x.LocationId).Distinct().ToArrayAsync(ct);
        if (scope.IsConfined && branches.Length == 0) throw new UnauthorizedAccessException("ProcurementCurrentScopeRequired");
        if (scope.WorkLocationId is { } selected && (branches.Length != 0 && !branches.Contains(selected)
            || !await core.WorkLocations.AsNoTracking().AnyAsync(x => x.Id == selected && x.LegalEntityId == scope.LegalEntityId && !x.IsArchived, ct)))
            throw new UnauthorizedAccessException("ProcurementCurrentScopeRequired");
        if (target && (scope.WorkLocationId is { } context && recordBranch != context
            || branches.Length != 0 && (recordBranch is null || !branches.Contains(recordBranch.Value))))
            throw new KeyNotFoundException("ProcurementRecordNotFound");
        return branches;
    }
}
