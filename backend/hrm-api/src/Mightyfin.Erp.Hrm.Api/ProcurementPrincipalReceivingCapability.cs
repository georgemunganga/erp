using Microsoft.EntityFrameworkCore;
using Mightyfin.Erp.Hrm.Infrastructure.Data;
using Mightyfin.Erp.Procurement.Application;

namespace Mightyfin.Erp.Hrm.Api;

internal static class ProcurementPrincipalReceivingCapability
{
    // Stopping new work must not strand historical reads or original-command recovery.
    internal static bool Configured(ProcurementPrincipalReceivingOptions? options, bool write) =>
        options is { ExecutionDatabaseConfigured: true } && (!write || options.Enabled);

    internal static bool WriteContextAllowed(Guid? branch, bool explicitAll, bool rawAll) =>
        branch is not null && branch != Guid.Empty && !explicitAll && !rawAll;

    internal static async Task<bool> ReadReadyAsync(IServiceProvider services,
        ProcurementRequestScope scope, bool pending, CancellationToken ct)
    {
        if (pending || scope.OrgUnitId is not null || scope.PrincipalId?.ToString("D") != scope.SubjectId
            || scope.IsConfined && scope.WorkLocationId is null && !scope.IsExplicitAllBranches
            || !Configured(services.GetService<ProcurementPrincipalReceivingOptions>(), false)
            || services.GetService<IProcurementPrincipalReceivingReadiness>() is not { } readiness
            || !await readiness.IsReadyAsync(ct)) return false;
        var core = services.GetRequiredService<HrmDbContext>();
        var person = await ProcurementPrincipalPeople.Resolve(core, scope.TenantId,
            scope.LegalEntityId, scope.SubjectId, ct);
        if (person is null || person.CoreLinkedWorkerId != scope.CoreLinkedWorkerId
            || !person.Permissions.Any(x => x is "procurement-receipt-view"
                or "procurement-receipt-create" or "procurement-receipt-edit"
                || x == ControlledReceiving.RecordPermission || x == ControlledReceiving.InspectPermission
                || x == ControlledReceiving.AcceptPermission || x == ControlledReceiving.CertifyPermission)) return false;
        var allowed = person.AllowedBranches.ToArray();
        return await core.WorkLocations.AsNoTracking().AnyAsync(site => site.TenantId == scope.TenantId
            && site.LegalEntityId == scope.LegalEntityId && !site.IsArchived
            && (person.OrganisationWide || allowed.Contains(site.Id))
            && (scope.WorkLocationId == null || site.Id == scope.WorkLocationId), ct);
    }
}
