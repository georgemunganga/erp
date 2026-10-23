using Mightyfin.Erp.Hrm.Infrastructure.Data;

namespace Mightyfin.Erp.Hrm.Api;

/// <summary>Availability preview for workerless approved-demand RFQ drafts. The command
/// still rechecks actor, record branch, Finance and source evidence under the owning lock.</summary>
internal static class ProcurementPrincipalSourcingCapability
{
    internal static bool Prerequisites(bool pending, ProcurementSourcingDraftExecutionOptions? sourcing,
        ProcurementApprovalExecutionOptions? approvals, bool p25Available)
        => !pending && p25Available
            && sourcing is { Enabled: true, PrincipalSourceEnabled: true, ExecutionDatabaseConfigured: true }
            && approvals is { Enabled: true, PrincipalExecutionEnabled: true, ExecutionDatabaseConfigured: true };

    internal static async Task<bool> ReadyAsync(IServiceProvider services, ProcurementRequestScope scope,
        bool pending, bool p25Available, CancellationToken ct)
    {
        if (!Prerequisites(pending, services.GetService<ProcurementSourcingDraftExecutionOptions>(),
            services.GetService<ProcurementApprovalExecutionOptions>(), p25Available)
            || scope.OrgUnitId is not null || scope.PrincipalId?.ToString("D") != scope.SubjectId)
            return false;
        var sourcingStore = services.GetService<IProcurementSourcingDraftSchemaReadiness>();
        var core = services.GetService<HrmDbContext>();
        if (sourcingStore is null || core is null || !await sourcingStore.IsReadyAsync(ct)) return false;
        var person = await ProcurementPrincipalPeople.Resolve(core, scope.TenantId, scope.LegalEntityId,
            scope.SubjectId, ct);
        if (person is null || !person.Permissions.Contains("procurement-sourcing-convert")
            && !person.Permissions.Contains("procurement-sourcing-cancel")) return false;
        try
        {
            _ = await ProcurementExecutionRecordScope.Branches(core, scope, null, false, ct);
            return true;
        }
        catch (UnauthorizedAccessException) { return false; }
    }
}
