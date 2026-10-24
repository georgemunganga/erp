namespace Mightyfin.Erp.Procurement.Infrastructure.Migrations;

/// <summary>
/// Authored retention guard for the future genuine P32 migration. This is not
/// installed until the owning migration invokes it as its first Down statement.
/// It protects all fifteen P32 tables and shared policy/Core case history whose
/// P32-specific schema or semantics would be lost by a downgrade.
/// </summary>
internal static class P32CommercialRoundRollbackGuards
{
    internal const string BeforeDownSql = """
        LOCK TABLE core_approval.cases,
          procurement.audit_events,
          procurement.outbox_events,
          procurement.operational_policy_identity_evidence,
          procurement.policy_versions,
          procurement.principal_rfq_criterion_assessments,
          procurement.principal_rfq_criterion_assessment_receipts,
          procurement.principal_rfq_commercial_command_receipts,
          procurement.principal_rfq_commercial_offer_openings,
          procurement.principal_rfq_commercial_offer_opening_receipts,
          procurement.principal_rfq_commercial_offer_submissions,
          procurement.principal_rfq_commercial_round_decisions,
          procurement.principal_rfq_commercial_round_openings,
          procurement.principal_rfq_commercial_round_seals,
          procurement.principal_rfq_commercial_round_votes,
          procurement.rfq_commercial_drafts,
          procurement.rfq_commercial_receipts,
          procurement.rfq_commercial_revisions,
          procurement.rfq_preparation_receipts,
          procurement.rfq_prepared_plans IN ACCESS EXCLUSIVE MODE;
        DO $$
        BEGIN
          IF EXISTS (SELECT 1 FROM procurement.rfq_commercial_drafts)
            OR EXISTS (SELECT 1 FROM procurement.principal_rfq_criterion_assessments)
            OR EXISTS (SELECT 1 FROM procurement.principal_rfq_criterion_assessment_receipts)
            OR EXISTS (SELECT 1 FROM procurement.rfq_commercial_revisions)
            OR EXISTS (SELECT 1 FROM procurement.rfq_commercial_receipts)
            OR EXISTS (SELECT 1 FROM procurement.rfq_prepared_plans)
            OR EXISTS (SELECT 1 FROM procurement.rfq_preparation_receipts)
            OR EXISTS (SELECT 1 FROM procurement.principal_rfq_commercial_round_openings)
            OR EXISTS (SELECT 1 FROM procurement.principal_rfq_commercial_round_votes)
            OR EXISTS (SELECT 1 FROM procurement.principal_rfq_commercial_round_decisions)
            OR EXISTS (SELECT 1 FROM procurement.principal_rfq_commercial_offer_submissions)
            OR EXISTS (SELECT 1 FROM procurement.principal_rfq_commercial_round_seals)
            OR EXISTS (SELECT 1 FROM procurement.principal_rfq_commercial_command_receipts)
            OR EXISTS (SELECT 1 FROM procurement.principal_rfq_commercial_offer_openings)
            OR EXISTS (SELECT 1 FROM procurement.principal_rfq_commercial_offer_opening_receipts)
            OR EXISTS (SELECT 1 FROM procurement.audit_events
                       WHERE record_type IN (
                           'PrincipalRfqCommercialDraftHead','PrincipalRfqCommercialDraftRevision',
                           'PrincipalRfqCommercialDraftReceipt','PrincipalRfqPreparedPlan',
                           'PrincipalRfqPreparationReceipt','PrincipalRfqCommercialRoundOpeningRecord',
                           'PrincipalRfqCommercialRoundVoteRecord','PrincipalRfqCommercialRoundDecisionRecord',
                           'PrincipalRfqCommercialOfferSubmissionRecord','PrincipalRfqCommercialRoundSealRecord',
                           'PrincipalRfqCommercialCommandReceipt','PrincipalRfqOfferOpeningRecord',
                           'PrincipalRfqOfferOpeningReceipt','PrincipalRfqCriterionAssessmentRecord',
                           'PrincipalRfqCriterionAssessmentReceipt'))
            OR EXISTS (SELECT 1 FROM procurement.outbox_events
                       WHERE event_type IN ('sourcing.commercial_draft_changed.v1',
                           'sourcing.prepared_candidate_recorded.v1',
                           'sourcing.commercial_round_recorded.v1',
                           'sourcing.commercial_offers_opened.v1',
                           'sourcing.criterion_assessment_recorded.v1'))
            OR EXISTS (SELECT 1 FROM procurement.policy_versions
                       WHERE policy_key = 'SOURCING_COMMERCIAL_ROUND')
            OR EXISTS (SELECT 1 FROM procurement.operational_policy_identity_evidence
                       WHERE policy_key = 'SOURCING_COMMERCIAL_ROUND')
            OR EXISTS (SELECT 1 FROM core_approval.cases
                       WHERE "SourceModule" = 'procurement'
                         AND "SourceType" = 'sourcing_commercial_round') THEN
            RAISE EXCEPTION 'P32 commercial round evidence prevents downgrade'
              USING ERRCODE = '55000';
          END IF;
        END $$;
        """;
}
