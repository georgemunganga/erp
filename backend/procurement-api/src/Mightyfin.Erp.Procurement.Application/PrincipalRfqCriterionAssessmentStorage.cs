using System.Text.Json;
using Mightyfin.Erp.Procurement.Domain;

namespace Mightyfin.Erp.Procurement.Application;

public static class PrincipalRfqCriterionAssessmentStorage
{
    public const string Action="procurement-sourcing-evaluate";
    public static string Snapshot(PrincipalRfqCriterionAssessmentRecord row) => JsonSerializer.Serialize(new
    {
        schemaVersion=1, roundId=row.RoundId, unsealingId=row.UnsealingId,
        offerId=row.OfferId, eventId=row.EventId, branchId=row.BranchId,
        preparedPlanId=row.PreparedPlanId, evaluationPlanRevisionId=row.EvaluationPlanRevisionId,
        evaluationDefinitionSha256=row.EvaluationDefinitionSha256,
        approvedRoundSha256=row.ApprovedRoundSha256,
        openingSha256=row.OpeningSha256, offerSha256=row.OfferSha256,
        row.CriterionCode, row.Kind, row.EvaluatorPrincipalId, row.EvaluatorWorkerAliasId,
        row.Revision, row.PreviousAssessmentId, row.PreviousRevision,
        row.Score, row.Passed, row.Rationale, row.OriginalDocumentVersionId,
        row.OriginalDocumentSha256, row.CommandKey, row.RequestSha256, row.RecordedAt,
    });
    public static string Response(PrincipalRfqCriterionAssessmentRecord row) => JsonSerializer.Serialize(new
    {
        schemaVersion=1, operation="RecordCriterionAssessment", assessmentId=row.Id,
        roundId=row.RoundId, offerId=row.OfferId, criterionCode=row.CriterionCode,
        revision=row.Revision, commandKey=row.CommandKey, snapshotSha256=row.SnapshotSha256,
        recordedAt=row.RecordedAt, evaluationComplete=false, awardMade=false,
    });
    public static void Verify(PrincipalRfqCriterionAssessmentRecord row,
        PrincipalRfqCommercialRoundOpeningRecord round,
        PrincipalRfqOfferOpeningRecord opening,
        PrincipalRfqCommercialOfferSubmissionRecord offer,
        PrincipalRfqEvaluationPlanDefinition definition)
    {
        var criterion=PrincipalRfqCriterionAssessmentRules.Validate(definition,
            row.CriterionCode,row.EvaluatorPrincipalId,row.Revision,
            row.Score?.ToString("0.0000",System.Globalization.CultureInfo.InvariantCulture),
            row.Passed,row.Rationale,row.OriginalDocumentVersionId,row.OriginalDocumentSha256);
        if(row.SchemaVersion!=1 || row.TenantId!=round.TenantId || row.LegalEntityId!=round.LegalEntityId
            || row.RoundId!=round.Id || row.EventId!=round.SourcingEventId
            || row.BranchId!=round.BranchId || row.PreparedPlanId!=round.PreparedPlanId
            || row.ApprovedRoundSha256!=round.SnapshotSha256
            || opening.ApprovedRoundSha256!=round.SnapshotSha256
            || row.TenantId!=opening.TenantId || row.LegalEntityId!=opening.LegalEntityId
            || row.RoundId!=opening.OpeningId || row.UnsealingId!=opening.Id
            || row.EventId!=opening.SourcingEventId || row.BranchId!=opening.BranchId
            || row.OpeningSha256!=opening.SnapshotSha256
            || row.OfferId!=offer.Id || offer.OpeningId!=row.RoundId
            || offer.TenantId!=row.TenantId || offer.LegalEntityId!=row.LegalEntityId
            || offer.SourcingEventId!=row.EventId || offer.BranchId!=row.BranchId
            || row.OfferSha256!=offer.SnapshotSha256
            || row.OriginalDocumentVersionId!=offer.OriginalDocumentVersionId
            || row.OriginalDocumentSha256!=offer.OriginalDocumentSha256
            || row.EvaluationDefinitionSha256!=definition.RulesSha256
            || row.Kind!=criterion.Kind || row.Score!=criterion.Score || row.Passed!=criterion.Passed
            || row.CommandKey==Guid.Empty || row.RequestSha256 is not {Length:64}
            || !row.RequestSha256.All(x=>x is >= '0' and <= '9' or >= 'a' and <= 'f')
            || row.RecordedAt<opening.OpenedAt || row.RecordedAt.Offset!=TimeSpan.Zero
            || row.Revision==1 && (row.PreviousAssessmentId is not null || row.PreviousRevision is not null)
            || row.Revision>1 && (row.PreviousAssessmentId is null || row.PreviousRevision!=row.Revision-1)
            || row.SnapshotJson!=Snapshot(row)
            || row.SnapshotSha256!=ProcurementEvidenceEncoding.Digest(row.SnapshotJson))
            throw new InvalidOperationException("RfqCriterionAssessmentEvidenceChanged");
    }
    public static void VerifyReceipt(PrincipalRfqCriterionAssessmentRecord row,
        PrincipalRfqCriterionAssessmentReceipt receipt)
    {
        if(receipt.SchemaVersion!=1 || receipt.AssessmentId!=row.Id || receipt.RoundId!=row.RoundId
            || receipt.UnsealingId!=row.UnsealingId || receipt.OfferId!=row.OfferId
            || receipt.EventId!=row.EventId || receipt.BranchId!=row.BranchId
            || receipt.TenantId!=row.TenantId || receipt.LegalEntityId!=row.LegalEntityId
            || receipt.ActorPrincipalId!=row.EvaluatorPrincipalId
            || receipt.CommandKey!=row.CommandKey || receipt.RequestSha256!=row.RequestSha256
            || receipt.ResponseJson!=Response(row)
            || receipt.ResponseSha256!=ProcurementEvidenceEncoding.Digest(receipt.ResponseJson))
            throw new InvalidOperationException("RfqCriterionAssessmentReceiptChanged");
    }
}
