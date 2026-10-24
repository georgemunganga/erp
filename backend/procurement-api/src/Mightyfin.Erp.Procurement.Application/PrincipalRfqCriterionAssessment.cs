namespace Mightyfin.Erp.Procurement.Application;

/// <summary>An evaluator's own observation of one frozen criterion and opened offer.
/// This is neither an aggregate result nor award authority.</summary>
public sealed record PrincipalRfqCriterionAssessment(
    string CriterionCode, string Kind, string EvaluatorPrincipalId,
    int Revision, decimal? Score, bool? Passed, string Rationale,
    Guid OriginalDocumentVersionId, string OriginalDocumentSha256);

public static class PrincipalRfqCriterionAssessmentRules
{
    public static PrincipalRfqCriterionAssessment Validate(
        PrincipalRfqEvaluationPlanDefinition definition, string criterionCode,
        string evaluatorPrincipalId, int revision, string? scoreExact, bool? passed,
        string? rationale, Guid originalDocumentVersionId, string originalDocumentSha256)
    {
        var criterion = definition.Criteria.SingleOrDefault(x => x.Code == criterionCode)
            ?? throw new InvalidOperationException("RfqAssessmentCriterionUnknown");
        if (!ProcurementPrincipalEvidence.Canonical(evaluatorPrincipalId)
            || !criterion.EvaluatorPrincipalIds.Contains(evaluatorPrincipalId, StringComparer.Ordinal))
            throw new InvalidOperationException("RfqAssessmentEvaluatorNotNamed");
        if (revision is < 1 or > 100 || string.IsNullOrWhiteSpace(rationale)
            || rationale != rationale.Trim() || rationale.Length > 2000
            || originalDocumentVersionId == Guid.Empty
            || originalDocumentSha256 is not {Length:64}
            || !originalDocumentSha256.All(x => x is >= '0' and <= '9' or >= 'a' and <= 'f'))
            throw new InvalidOperationException("RfqAssessmentEvidenceInvalid");
        decimal? score = null;
        if (criterion.Kind == "Scored")
        {
            if (scoreExact is null || passed is not null)
                throw new InvalidOperationException("RfqAssessmentScoreRequired");
            score = ExactDecimalJsonCodec.Parse(scoreExact, 18, 4, "RfqAssessmentScore");
            if (score < 0 || score > criterion.MaximumScore)
                throw new InvalidOperationException("RfqAssessmentScoreOutOfRange");
        }
        else if (scoreExact is not null || passed is null)
            throw new InvalidOperationException("RfqAssessmentPassFailRequired");
        return new(criterion.Code, criterion.Kind, evaluatorPrincipalId, revision,
            score, passed, rationale, originalDocumentVersionId, originalDocumentSha256);
    }
}
