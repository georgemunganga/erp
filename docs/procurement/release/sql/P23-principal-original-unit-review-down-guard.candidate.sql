-- SOURCE REVIEW CANDIDATE ONLY. Not an emitted/applied migration.
-- Must be the FIRST Down operation, in the ordinary EF transaction (no suppressTransaction).
-- P23 candidate: embed reviewed literal SQL only into the genuinely generated new migration.
-- Released P01-P22 migration bytes are immutable; this file is not an emitted migration.
LOCK TABLE procurement.invoice_unit_review_sources,
    procurement.invoice_unit_review_assignments,
    procurement.invoice_unit_review_outcomes,
    procurement.invoice_unit_review_receipts,
    procurement.invoice_match_mapping_versions,
    procurement.invoice_match_results,
    procurement.policy_versions,
    procurement.operational_policy_identity_evidence,
    procurement.audit_events,
    procurement.outbox_events IN ACCESS EXCLUSIVE MODE;
DO $p23_principal_original_unit_review$
DECLARE retained_core_case boolean := false;
BEGIN
    -- Core owns this schema. Standalone Procurement stores may not have it. Never create
    -- a Core schema/table or delete/alter a case merely to make a downgrade pass.
    IF to_regclass('core_approval.cases') IS NOT NULL THEN
        EXECUTE 'LOCK TABLE core_approval.cases IN ACCESS EXCLUSIVE MODE';
        EXECUTE 'SELECT EXISTS(SELECT 1 FROM core_approval.cases
            WHERE "SourceModule"=''procurement'' AND "SourceType"=''invoice_unit_review'')'
            INTO retained_core_case;
    END IF;
    IF retained_core_case
        OR EXISTS(SELECT 1 FROM procurement.invoice_unit_review_sources)
        OR EXISTS(SELECT 1 FROM procurement.invoice_unit_review_assignments)
        OR EXISTS(SELECT 1 FROM procurement.invoice_unit_review_outcomes)
        OR EXISTS(SELECT 1 FROM procurement.invoice_unit_review_receipts)
        OR EXISTS(SELECT 1 FROM procurement.policy_versions WHERE policy_key='INVOICE_UNIT_REVIEW')
        OR EXISTS(SELECT 1 FROM procurement.operational_policy_identity_evidence
            WHERE policy_key='INVOICE_UNIT_REVIEW' OR schema_version<>1 OR actor_worker_id IS NULL)
        OR EXISTS(SELECT 1 FROM procurement.invoice_match_mapping_versions
            WHERE identity_schema_version<>1 OR actor_worker_id IS NULL)
        OR EXISTS(SELECT 1 FROM procurement.invoice_match_results WHERE execution_version NOT IN (0,1))
        OR EXISTS(SELECT 1 FROM procurement.audit_events
            WHERE record_type IN ('InvoiceUnitReviewSource','InvoiceUnitReviewAssignmentEvidence',
                'InvoiceUnitReviewOutcome','InvoiceUnitReviewCommandReceipt')
                OR (record_type='PolicyVersion' AND changed_fields_json->>'PolicyKey'='INVOICE_UNIT_REVIEW')
                OR (record_type IN ('OperationalPolicyIdentityRecord','InvoiceMatchMappingVersion','InvoiceMatchResult')
                    AND changed_fields_json->'principalEvidenceVersion'='2'::jsonb
                    AND changed_fields_json->>'principalEvidenceKind' IN
                        ('OperationalPolicyIdentity','InvoiceMappingIdentity','InvoiceMatchExecution')))
        OR EXISTS(SELECT 1 FROM procurement.outbox_events
            WHERE aggregate_type IN ('InvoiceUnitReviewSource','InvoiceUnitReviewAssignmentEvidence',
                'InvoiceUnitReviewOutcome','InvoiceUnitReviewCommandReceipt')
                OR event_type IN ('invoice.original_units_reviewed.v1','supplier_invoice.compared.v2')
                OR (aggregate_type IN ('OperationalPolicyIdentityRecord','InvoiceMatchMappingVersion','InvoiceMatchResult','SupplierInvoice')
                    AND payload_json->'principalEvidenceVersion'='2'::jsonb))
    THEN
        RAISE EXCEPTION USING ERRCODE='55000',
            MESSAGE='Retained principal invoice or policy evidence prevents destructive downgrade.';
    END IF;
END;
$p23_principal_original_unit_review$;
-- Only after this guard succeeds may generated Down remove the new tables/discriminator,
-- restore the exact old operational/mapping/execution CHECKs and require legacy workers.
-- No default/fabricated employee UUID, JSON/hash rewrite, old-row delete or granted exception.
-- Genuine isolated PG Up/unusedDown/usedDown/audit-only/outbox-only negative proofs are required.
