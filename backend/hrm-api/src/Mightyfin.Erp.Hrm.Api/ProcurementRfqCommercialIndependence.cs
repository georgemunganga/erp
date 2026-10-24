using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using Mightyfin.Erp.Hrm.Infrastructure.Data;
using Mightyfin.Erp.Procurement.Application;
using Mightyfin.Erp.Procurement.Domain;
using Mightyfin.Erp.Procurement.Infrastructure.Data;

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

    private static async Task RequireCommercialActingOfficerIndependence(
        ProcurementDbContext db, HrmDbContext core, ProcurementRequestScope scope,
        RfqPrincipalDraft original, PrincipalRfqCommercialRoundOpeningRecord opening,
        RfqPrincipalActor actor, CancellationToken ct)
    {
        await RequireCommercialCurrentIndependence(core, scope, original,
            [actor], ct);
        if (opening.SnapshotSha256 != ProcurementEvidenceEncoding.Digest(opening.SnapshotJson))
            throw new Problem("RfqCommercialOpeningChanged", 409);
        using var document = JsonDocument.Parse(opening.SnapshotJson);
        var root = document.RootElement;
        var pinned = root.GetProperty("stages").EnumerateArray()
            .SelectMany(stage => stage.GetProperty("assignments").EnumerateArray())
            .Select(assignment => (
                PrincipalId: assignment.GetProperty("AssigneeSubjectId").GetString()!,
                OpeningAlias: OptionalAlias(assignment.GetProperty("WorkerAliasId"))))
            .ToList();
        pinned.Add((root.GetProperty("closerPrincipalId").GetString()!,
            OptionalAlias(root.GetProperty("closerWorkerAliasId"))));
        if (pinned.Count is < 2 or > 101
            || pinned.Any(x => !ProcurementPrincipalEvidence.Canonical(x.PrincipalId))
            || pinned.Select(x => x.PrincipalId).Distinct(StringComparer.Ordinal).Count() != pinned.Count)
            throw new Problem("RfqCommercialOfficerPinsChanged", 409);
        var voteAliases = await db.PrincipalRfqCommercialRoundVotes.AsNoTracking()
            .Where(x => x.OpeningId == opening.Id)
            .Select(x => new { x.ActorPrincipalId, x.ActorWorkerAliasId })
            .ToArrayAsync(ct);
        if (voteAliases.GroupBy(x => x.ActorPrincipalId).Any(x => x.Count() != 1)
            || voteAliases.Any(x => !pinned.Any(p => p.PrincipalId == x.ActorPrincipalId)))
            throw new Problem("RfqCommercialVoteHistoryChanged", 409);
        var others = pinned.Where(x => x.PrincipalId != actor.PrincipalId)
            .Select(x => (x.PrincipalId, x.OpeningAlias,
                VoteAlias: voteAliases.SingleOrDefault(v => v.ActorPrincipalId == x.PrincipalId)
                    ?.ActorWorkerAliasId))
            .ToArray();
        var ids = pinned.Select(x => Guid.ParseExact(x.PrincipalId, "D"))
            .Append(Guid.ParseExact(actor.PrincipalId, "D"))
            .Distinct().ToArray();
        var aliases = await core.LocalUsers.IgnoreQueryFilters().AsNoTracking()
            .Where(x => x.TenantId == scope.TenantId && ids.Contains(x.Id))
            .Select(x => new { x.Id, x.WorkerId })
            .ToDictionaryAsync(x => x.Id, x => x.WorkerId, ct);
        if (!CommercialActingOfficerIndependentNow(actor, others, aliases))
            throw new Problem("RfqCommercialOfficerNotIndependent", 403);
    }

    private static Guid? OptionalAlias(JsonElement value) =>
        value.ValueKind == JsonValueKind.Null ? null : value.GetGuid();

    internal static bool CommercialActingOfficerIndependentNow(
        RfqPrincipalActor actor,
        IReadOnlyList<(string PrincipalId, Guid? OpeningAlias, Guid? VoteAlias)> others,
        IReadOnlyDictionary<Guid, Guid?> aliases)
        => others.All(x =>
            ProcurementPrincipalPeople.IndependentWithCurrentAliases(
                actor.PrincipalId, actor.LinkedWorkerId,
                x.PrincipalId, x.OpeningAlias, aliases)
            && (x.VoteAlias is null || ProcurementPrincipalPeople.IndependentWithCurrentAliases(
                actor.PrincipalId, actor.LinkedWorkerId,
                x.PrincipalId, x.VoteAlias, aliases)));
}
