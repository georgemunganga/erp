-- SOURCE REVIEW CANDIDATE ONLY. Not an emitted/applied migration.
-- Must be the FIRST Down operation, in the ordinary EF transaction (no suppressTransaction).
-- Review against the generated P22 table names and owning audit producers before adoption.
LOCK TABLE procurement.invoice_unit_review_sources,
    procurement.invoice_unit_review_assignments,
    procurement.invoice_unit_review_outcomes,
    procurement.invoice_unit_review_receipts,
    procurement.policy_versions,
    procurement.operational_policy_identity_evidence,
    procurement.audit_events,
    procurement.outbox_events IN ACCESS EXCLUSIVE MODE;
DO $p22_original_unit_review$
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
        OR EXISTS(SELECT 1 FROM procurement.operational_policy_identity_evidence WHERE policy_key='INVOICE_UNIT_REVIEW')
        OR EXISTS(SELECT 1 FROM procurement.audit_events
            WHERE record_type IN ('InvoiceUnitReviewSource','InvoiceUnitReviewAssignmentEvidence',
                'InvoiceUnitReviewOutcome','InvoiceUnitReviewCommandReceipt')
                OR (record_type='PolicyVersion' AND changed_fields_json->>'PolicyKey'='INVOICE_UNIT_REVIEW'))
        OR EXISTS(SELECT 1 FROM procurement.outbox_events
            WHERE aggregate_type IN ('InvoiceUnitReviewSource','InvoiceUnitReviewAssignmentEvidence',
                'InvoiceUnitReviewOutcome','InvoiceUnitReviewCommandReceipt')
                OR event_type='invoice.original_units_reviewed.v1')
    THEN
        RAISE EXCEPTION USING ERRCODE='55000',
            MESSAGE='Retained original-invoice unit review prevents destructive downgrade.';
    END IF;
END;
$p22_original_unit_review$;
-- Only after this guard succeeds may generated Down remove the four tables and restore
-- the EXACT existing two-key operational identity CHECK. Do not reconstruct old SQL loosely.
-- No money/quantity column narrows in this model delta. No old policies/evidence are deleted.
