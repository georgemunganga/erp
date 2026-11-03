using Mightyfin.Erp.Procurement.Infrastructure.Approvals;

namespace Mightyfin.Erp.Hrm.Api;

internal static class ProcurementCommercialRoundCapability
{
    internal static bool HasRecordAction(IReadOnlySet<string> permissions) =>
        permissions.Contains("procurement-round-plan-manage") || permissions.Contains("procurement-round-plan-cancel");
    internal static async Task<(bool Read, bool Record)> ReadyAsync(IServiceProvider services,
        ProcurementRequestScope scope, bool pending, CancellationToken ct)
    {
        var options = services.GetRequiredService<ProcurementCommercialRoundOptions>();
        var readiness = services.GetRequiredService<IProcurementCommercialRoundReadiness>();
        if (pending || !options.ReadEnabled || !readiness.HasCapturedContract || scope.OrgUnitId is not null
            || !services.GetRequiredService<ProcurementSourcingDraftExecutionOptions>().ExecutionDatabaseConfigured
            || scope.PrincipalId?.ToString("D") != scope.SubjectId) return (false, false);
        return await services.GetRequiredService<ProcurementApprovalTransaction>().ExecuteAsync(async (db, _, token) => {
            if (!await readiness.ReadyAsync(db, false, token)) return (false, false);
            await using var core = await ProcurementApprovalExecutionLocks.CoreAsync(db, scope, token);
            var person = await ProcurementPrincipalPeople.Resolve(core, scope.TenantId, scope.LegalEntityId, scope.SubjectId, token);
            var read = person is not null && person.Permissions.Contains("procurement-round-plan-read")
                && (scope.WorkLocationId is { } branch ? person.OrganisationWide || person.AllowedBranches.Contains(branch)
                    : person.OrganisationWide || person.AllowedBranches.Count > 0);
            var record = read && options.RecordEnabled && !scope.IsExplicitAllBranches && scope.WorkLocationId is not null
                && HasRecordAction(person!.Permissions) && await readiness.ReadyAsync(db, true, token);
            return (read, record);
        }, ct);
    }
}
