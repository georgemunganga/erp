using Microsoft.EntityFrameworkCore;
using Mightyfin.Erp.Hrm.Infrastructure.Data;
using Mightyfin.Erp.Procurement.Application;

namespace Mightyfin.Erp.Hrm.Api;

public static partial class ProcurementSourcingExecutionRoutes
{
    private static async Task RequireCommercialCurrentIndependence(
        HrmDbContext core, ProcurementRequestScope scope, RfqPrincipalDraft original,
        IReadOnlyList<RfqPrincipalActor> officers, CancellationToken ct)
    {
        var originals = original.Lines.SelectMany(x => new[]
            { (x.MakerPrincipalId, x.MakerWorkerAlias),
              (x.RequesterPrincipalId, x.RequesterWorkerAlias) })
            .Append((original.MakerPrincipalId, original.MakerWorkerAlias))
            .Distinct().ToArray();
        var ids = originals.Select(x => Guid.ParseExact(x.Item1, "D"))
            .Concat(officers.Select(x => Guid.ParseExact(x.PrincipalId, "D")))
            .Distinct().ToArray();
        if (ids.Length is < 2 or > 500 || officers.Count == 0)
            throw new Problem("RfqCommercialParticipantSetInvalid", 409);
        // Include currently archived historical makers for denial-only alias
        // checks; tenant confinement remains explicit despite IgnoreFilters.
        var aliases = await core.LocalUsers.IgnoreQueryFilters().AsNoTracking()
            .Where(x => x.TenantId == scope.TenantId && ids.Contains(x.Id))
            .Select(x => new { x.Id, x.WorkerId })
            .ToDictionaryAsync(x => x.Id, x => x.WorkerId, ct);
        if (!CommercialParticipantsIndependentNow(originals, officers, aliases))
            throw new Problem("RfqCommercialParticipantNotIndependent", 403);
    }

    internal static bool CommercialParticipantsIndependentNow(
        IReadOnlyList<(string PrincipalId, Guid? WorkerAlias)> originals,
        IReadOnlyList<RfqPrincipalActor> officers,
        IReadOnlyDictionary<Guid, Guid?> aliases)
    {
        if (originals.Count == 0 || officers.Count == 0)
            return false;
        for (var index = 0; index < officers.Count; index++)
        {
            var officer = officers[index];
            if (originals.Any(x => !ProcurementPrincipalPeople.IndependentWithCurrentAliases(
                    x.PrincipalId, x.WorkerAlias, officer.PrincipalId,
                    officer.LinkedWorkerId, aliases)))
                return false;
            for (var prior = 0; prior < index; prior++)
                if (!ProcurementPrincipalPeople.IndependentWithCurrentAliases(
                        officers[prior].PrincipalId, officers[prior].LinkedWorkerId,
                        officer.PrincipalId, officer.LinkedWorkerId, aliases))
                    return false;
        }
        return true;
    }
}
