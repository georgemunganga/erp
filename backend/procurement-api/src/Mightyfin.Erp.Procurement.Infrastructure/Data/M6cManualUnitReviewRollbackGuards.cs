namespace Mightyfin.Erp.Procurement.Infrastructure.Data;

/// <summary>First Down operation before restoring the document-only outcome CHECK.
/// Retains manual review contracts, including pending reviews and historical
/// publication/acknowledgement markers. No Core/global access or data repair.</summary>
internal static class M6cManualUnitReviewRollbackGuards
{
    internal const string BeforeDownSql = """
        LOCK TABLE procurement.invoice_unit_review_sources,
          procurement.invoice_unit_review_assignments,
          procurement.invoice_unit_review_outcomes,
          procurement.invoice_unit_review_receipts,
          procurement.policy_versions,
          procurement.operational_policy_identity_evidence,
          procurement.audit_events,
          procurement.outbox_events IN ACCESS EXCLUSIVE MODE;
        DO $m6c_manual_unit_review_down$
        BEGIN
          IF EXISTS (SELECT 1 FROM procurement.invoice_unit_review_sources
                     WHERE source_json::jsonb->>'SourceSchemaVersion'='4')
            OR EXISTS (SELECT 1 FROM procurement.invoice_unit_review_receipts
                       WHERE request_json::jsonb->>'schemaVersion'='2'
                          OR response_json::jsonb->>'evidenceSchemaVersion'='4')
            OR EXISTS (SELECT 1 FROM procurement.invoice_unit_review_outcomes
                       WHERE evidence_schema_version=4
                          OR evidence_json::jsonb->>'schemaVersion'='4')
            OR EXISTS (SELECT 1 FROM procurement.policy_versions
                       WHERE policy_key='INVOICE_UNIT_REVIEW'
                         AND rules_json::jsonb->>'schemaVersion'='2'
                         AND rules_json::jsonb->>'evidenceMode'='ManualAttestation')
            OR EXISTS (SELECT 1 FROM procurement.operational_policy_identity_evidence identity
                       JOIN procurement.policy_versions policy
                         ON policy.tenant_id=identity.tenant_id
                           AND policy.legal_entity_id=identity.legal_entity_id
                           AND policy.id=identity.policy_version_id
                           AND policy.policy_key=identity.policy_key
                           AND policy.policy_version_number=identity.policy_version_number
                       WHERE identity.policy_key='INVOICE_UNIT_REVIEW'
                         AND identity.stage='Published'
                         AND policy.rules_json::jsonb->>'schemaVersion'='2'
                         AND policy.rules_json::jsonb->>'evidenceMode'='ManualAttestation')
            OR EXISTS (SELECT 1 FROM procurement.audit_events
                       WHERE action IN ('ManualUnitReviewOpened','ManualUnitReviewDecision'))
            OR EXISTS (SELECT 1 FROM procurement.outbox_events
                       WHERE event_type='invoice.manual_units_reviewed.v1')
          THEN
            RAISE EXCEPTION USING ERRCODE='55000',
              MESSAGE='Retained manual invoice unit review evidence prevents downgrade; use compatible code or fix forward.';
          END IF;
        END
        $m6c_manual_unit_review_down$;
        """;
}
