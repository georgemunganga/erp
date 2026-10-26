namespace Mightyfin.Erp.Procurement.Infrastructure.Migrations;

// Run before M4 Down changes any table or constraint. The migration transaction
// holds these locks until the old schema is restored. Retained internal review
// evidence needs compatible code or a reviewed forward repair.
internal static class M4PrincipalRfqPublicationGuards
{
    internal const string BeforeDownSql = """
        LOCK TABLE procurement.sourcing_publication_openings,
          procurement.sourcing_publication_decisions,
          procurement.sourcing_publication_receipts,
          procurement.sourcing_policy_identity_evidence,
          procurement.spending_authority_identity_evidence,
          procurement.sourcing_events,
          procurement.sourcing_invitations,
          procurement.audit_events,
          procurement.outbox_events,
          core_approval.cases IN ACCESS EXCLUSIVE MODE;
        DO $m4_rfq_publication_down$
        BEGIN
          IF EXISTS (SELECT 1 FROM procurement.sourcing_publication_openings)
            OR EXISTS (SELECT 1 FROM procurement.sourcing_publication_decisions)
            OR EXISTS (SELECT 1 FROM procurement.sourcing_publication_receipts)
            OR EXISTS (SELECT 1 FROM procurement.sourcing_policy_identity_evidence
                       WHERE schema_version = 2)
            OR EXISTS (SELECT 1 FROM procurement.spending_authority_identity_evidence
                       WHERE schema_version = 2 AND action = 'SourcingEvent.Publish')
            OR EXISTS (SELECT 1 FROM procurement.sourcing_events
                       WHERE execution_version = 2 AND status IN ('PublicationReview','Published'))
            OR EXISTS (SELECT 1 FROM procurement.sourcing_invitations i
                       JOIN procurement.sourcing_events e ON e.tenant_id=i.tenant_id
                         AND e.legal_entity_id=i.legal_entity_id AND e.id=i.sourcing_event_id
                       WHERE e.execution_version=2 AND i.status='ReadyForManualDelivery')
            OR EXISTS (SELECT 1 FROM core_approval.cases
                       WHERE "SourceModule"='procurement' AND "SourceType"='sourcing_publication')
            OR EXISTS (SELECT 1 FROM procurement.audit_events
                       WHERE record_type='SourcingEvent' AND action IN
                         ('PublicationReviewOpened','PublicationReviewDecisionRecorded'))
            OR EXISTS (SELECT 1 FROM procurement.outbox_events
                       WHERE aggregate_type='SourcingEvent' AND event_type IN
                         ('sourcing.publication_review_opened.v2',
                          'sourcing.internally_published.v2',
                          'sourcing.publication_review_decision.v2'))
          THEN
            RAISE EXCEPTION USING ERRCODE='55000',
              MESSAGE='Retained principal RFQ publication evidence prevents downgrade; use compatible code or fix forward.';
          END IF;
        END
        $m4_rfq_publication_down$;
        """;
}
