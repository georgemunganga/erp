using Mightyfin.Erp.Procurement.Domain;

namespace Mightyfin.Erp.Procurement.Application;

/// <summary>Presence of each explicitly named evaluator's original observation.
/// This is a purpose read, not a score comparison or award recommendation.</summary>
public static class PrincipalRfqAssessmentCoverage
{
    public sealed record Slot(string EvaluatorPrincipalId, bool Observed,
        Guid? AssessmentId, int? Revision, string? AssessmentSha256);

    public sealed record Projection(Guid OfferId, string CriterionCode, string Kind,
        int RequiredObservations, int ObservedObservations, IReadOnlyList<Slot> Slots);

    public static Projection Build(Guid offerId,
        PrincipalRfqEvaluationPlanDefinition.Criterion criterion,
        IReadOnlyList<PrincipalRfqCriterionAssessmentRecord> retained)
    {
        if (offerId == Guid.Empty || retained.Count > criterion.EvaluatorPrincipalIds.Count * 100
            || criterion.EvaluatorPrincipalIds.Count is < 1 or > 64)
            throw new InvalidOperationException("RfqAssessmentCoverageBound");
        var named = criterion.EvaluatorPrincipalIds.ToHashSet(StringComparer.Ordinal);
        if (named.Count != criterion.EvaluatorPrincipalIds.Count || retained.Any(x =>
            x.OfferId != offerId || x.CriterionCode != criterion.Code
            || !named.Contains(x.EvaluatorPrincipalId)))
            throw new InvalidOperationException("RfqAssessmentCoverageSourceChanged");
        var latest = new Dictionary<string, PrincipalRfqCriterionAssessmentRecord>(StringComparer.Ordinal);
        foreach (var group in retained.GroupBy(x => x.EvaluatorPrincipalId, StringComparer.Ordinal))
        {
            PrincipalRfqCriterionAssessmentRecord? previous = null;
            foreach (var row in group.OrderBy(x => x.Revision))
            {
                if (row.Revision != (previous?.Revision ?? 0) + 1
                    || row.PreviousRevision != previous?.Revision
                    || row.PreviousAssessmentId != previous?.Id
                    || row.Revision > 100)
                    throw new InvalidOperationException("RfqAssessmentCoverageChainChanged");
                previous = row;
            }
            if (previous is not null) latest.Add(group.Key, previous);
        }
        var slots = criterion.EvaluatorPrincipalIds.Select(person =>
            latest.TryGetValue(person, out var row)
                ? new Slot(person, true, row.Id, row.Revision, row.SnapshotSha256)
                : new Slot(person, false, null, null, null)).ToArray();
        return new(offerId, criterion.Code, criterion.Kind, slots.Length,
            slots.Count(x => x.Observed), Array.AsReadOnly(slots));
    }
}
