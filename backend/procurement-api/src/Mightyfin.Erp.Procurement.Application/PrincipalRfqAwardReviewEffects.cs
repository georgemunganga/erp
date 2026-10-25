using System.Text.Json;
using Mightyfin.Erp.Procurement.Domain;

namespace Mightyfin.Erp.Procurement.Application;

/// <summary>Metadata only. No proposed value, quote body, supplier, document or
/// other reviewer details enter the broadly delivered outbox payload.</summary>
public static class PrincipalRfqAwardReviewEffects
{
    public const string EventType = "sourcing.award_review_submitted.v1";
    public const string AuditAction = "SubmitAwardReview";

    public static string Payload(PrincipalRfqAwardReviewSubmission row) => JsonSerializer.Serialize(new
    {
        schemaVersion = 1, submissionId = row.Id,
        recommendationSubmissionId = row.RecommendationSubmissionId,
        coreCaseId = row.CoreCaseId,
        roundId = row.RoundId,
        branchId = row.BranchId,
    });
}
