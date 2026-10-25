namespace Mightyfin.Erp.Procurement.Infrastructure.Migrations;

/// <summary>First-Down veto for the five P38-owned families and their shared
/// audit/outbox companions. No Core award or purchase order authority is implied.</summary>
internal static class P38SupplierAwardReviewRollbackGuards
{
    internal const string BeforeDownSql = """
        LOCK TABLE procurement.principal_rfq_recommendation_submissions,
          procurement.principal_rfq_award_review_submissions,
          procurement.principal_rfq_award_review_lines,
          procurement.principal_rfq_award_review_outcomes,
          procurement.principal_rfq_award_review_receipts,
          procurement.audit_events,
          procurement.outbox_events IN ACCESS EXCLUSIVE MODE;
        DO $$
        BEGIN
          IF EXISTS (SELECT 1 FROM procurement.principal_rfq_recommendation_submissions)
            OR EXISTS (SELECT 1 FROM procurement.principal_rfq_award_review_submissions)
            OR EXISTS (SELECT 1 FROM procurement.principal_rfq_award_review_lines)
            OR EXISTS (SELECT 1 FROM procurement.principal_rfq_award_review_outcomes)
            OR EXISTS (SELECT 1 FROM procurement.principal_rfq_award_review_receipts)
            OR EXISTS (SELECT 1 FROM procurement.audit_events
                       WHERE record_type = 'PrincipalRfqAwardReviewSubmission'
                          OR action = 'SubmitAwardReview')
            OR EXISTS (SELECT 1 FROM procurement.outbox_events
                       WHERE aggregate_type = 'PrincipalRfqAwardReviewSubmission'
                          OR event_type = 'sourcing.award_review_submitted.v1') THEN
            RAISE EXCEPTION 'P38 supplier award review evidence prevents downgrade'
              USING ERRCODE = '55000';
          END IF;
        END $$;
        """;
}
