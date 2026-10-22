using Microsoft.EntityFrameworkCore;
using Mightyfin.Erp.Hrm.Infrastructure.Data;
using Mightyfin.Erp.Procurement.Application;

namespace Mightyfin.Erp.Hrm.Api;

/// <summary>Read-only current Core account/role/branch projection. HR metadata is optional
/// and never creates company membership, permissions or a replacement identity.</summary>
internal static class ProcurementPrincipalPeople
{
    internal sealed record Person(string PrincipalId, Guid? WorkerId, IReadOnlySet<string> Permissions,
        bool OrganisationWide, IReadOnlySet<Guid> AllowedBranches);
    // Core branch grants define company reach before search/count/paging; employee
    // home department is metadata, not a grant. No assignments means tenant-wide,
    // matching the existing current-account Core scope contract.
    internal static IQueryable<Mightyfin.Erp.Hrm.Domain.Entities.LocalUser> Candidates(
        HrmDbContext core, ProcurementRequestScope scope, DateTimeOffset now)
    {
        var query = core.LocalUsers.AsNoTracking().Where(u => u.TenantId == scope.TenantId
            && u.IsActive && !u.IsArchived && (u.LockedUntil == null || u.LockedUntil <= now)
            && (!core.UserBranchAssignments.Any(a => a.TenantId == scope.TenantId && a.UserId == u.Id && !a.IsArchived)
                || core.UserBranchAssignments.Any(a => a.TenantId == scope.TenantId && a.UserId == u.Id && !a.IsArchived
                    && core.WorkLocations.Any(b => b.TenantId == scope.TenantId && b.Id == a.LocationId
                        && b.LegalEntityId == scope.LegalEntityId && !b.IsArchived
                        && (scope.WorkLocationId == null || b.Id == scope.WorkLocationId)))));
        // No Core department-grant contract exists: do not infer one from HR.
        if (scope.OrgUnitId is not null || scope.IsConfined && scope.WorkLocationId is null)
            query = query.Where(_ => false);
        return query;
    }
    internal static async Task<Person?> Resolve(HrmDbContext core, string tenant, Guid company,
        string principal, CancellationToken ct)
    {
        if (!ProcurementPrincipalEvidence.Canonical(principal)) return null;
        var account = await ProcurementCorePrincipals.CurrentAsync(core, tenant, principal, ct);
        if (account is null || !account.Actions.Contains("procurement-access", StringComparer.Ordinal)
            || !await core.LegalEntities.AsNoTracking().AnyAsync(x => x.TenantId == tenant && x.Id == company && !x.IsArchived, ct)) return null;
        var declared = await core.UserBranchAssignments.AsNoTracking().Where(x => x.TenantId == tenant
            && x.UserId == account.Id && !x.IsArchived).Select(x => x.LocationId).Distinct().ToArrayAsync(ct);
        var branches = await core.WorkLocations.AsNoTracking().Where(x => x.TenantId == tenant
            && declared.Contains(x.Id) && x.LegalEntityId == company && !x.IsArchived).Select(x => x.Id).ToArrayAsync(ct);
        if (declared.Length > 0 && branches.Length == 0) return null;
        var projection = new ProcurementRequestScope();
        projection.Set(tenant, company, account.Id.ToString("D"), null, null, false, "principal-optional-employee-projection");
        projection.AssignPrincipal(account.Id);
        var worker = await ProcurementWorkforce.ResolveActiveWorker(core, projection, ct);
        return new(account.Id.ToString("D"), worker, account.Actions.ToHashSet(StringComparer.Ordinal), declared.Length == 0, branches.ToHashSet());
    }
}
