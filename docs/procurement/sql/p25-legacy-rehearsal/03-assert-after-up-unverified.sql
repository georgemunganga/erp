-- Run only AFTER P25 Up. Confirms missing new proof stays missing on populated legacy0.
\set ON_ERROR_STOP on
BEGIN READ ONLY;
DO $unverified$
BEGIN
    IF current_database() NOT LIKE 'procurement_p25_restore_%' THEN
        RAISE EXCEPTION 'P25FixturePrivateRestoredDatabaseRequired';
    END IF;
    IF (SELECT count(*) FROM procurement.goods_receipts WHERE tenant_id='p25-legacy-rehearsal')<>1
        OR (SELECT count(*) FROM procurement.goods_receipt_lines WHERE tenant_id='p25-legacy-rehearsal')<>1
        OR (SELECT count(*) FROM procurement.receipt_inspections WHERE tenant_id='p25-legacy-rehearsal')<>1
        OR (SELECT count(*) FROM procurement.service_acceptances WHERE tenant_id='p25-legacy-rehearsal')<>1 THEN
        RAISE EXCEPTION 'P25LegacyFixtureMissingOrExpanded';
    END IF;
    IF EXISTS (SELECT 1 FROM procurement.goods_receipts WHERE tenant_id='p25-legacy-rehearsal'
        AND (execution_version IS DISTINCT FROM 0 OR issued_version_id IS NOT NULL OR submission_id IS NOT NULL OR branch_ref IS NOT NULL
            OR issued_binding_id IS DISTINCT FROM '00000000-0000-0000-0000-000000000000'::uuid OR receiver_worker_id IS NOT NULL OR recorded_at IS NOT NULL OR notes IS NOT NULL))
        OR EXISTS (SELECT 1 FROM procurement.goods_receipt_lines WHERE tenant_id='p25-legacy-rehearsal'
        AND (execution_version IS DISTINCT FROM 0 OR receiving_source_state_id IS NOT NULL OR source_purchase_order_id IS NOT NULL OR initially_rejected_quantity IS DISTINCT FROM 0
            OR source_state_binding_id IS DISTINCT FROM '00000000-0000-0000-0000-000000000000'::uuid OR issued_binding_id IS DISTINCT FROM '00000000-0000-0000-0000-000000000000'::uuid
            OR physical_binding_id IS DISTINCT FROM '00000000-0000-0000-0000-000000000000'::uuid OR physical_observation_id IS NOT NULL OR physical_policy_version_id IS NOT NULL
            OR physical_evidence_ref IS NOT NULL OR physical_sequence IS NOT NULL OR physical_effect_key IS NOT NULL OR physical_recorded_at IS NOT NULL))
        OR EXISTS (SELECT 1 FROM procurement.receipt_inspections WHERE tenant_id='p25-legacy-rehearsal'
        AND (execution_version IS DISTINCT FROM 0 OR receiving_source_state_id IS NOT NULL OR physical_observation_id IS NOT NULL OR qualified_quantity IS DISTINCT FROM 0
            OR source_state_binding_id IS DISTINCT FROM '00000000-0000-0000-0000-000000000000'::uuid OR physical_binding_id IS DISTINCT FROM '00000000-0000-0000-0000-000000000000'::uuid
            OR inspector_worker_id IS NOT NULL OR policy_version_id IS NOT NULL OR evidence_ref IS NOT NULL OR ledger_sequence IS NOT NULL OR effect_key IS NOT NULL))
        OR EXISTS (SELECT 1 FROM procurement.service_acceptances WHERE tenant_id='p25-legacy-rehearsal'
        AND (execution_version IS DISTINCT FROM 0 OR receiving_source_state_id IS NOT NULL OR source_purchase_order_id IS NOT NULL OR owner_worker_id IS NOT NULL
            OR source_state_binding_id IS DISTINCT FROM '00000000-0000-0000-0000-000000000000'::uuid OR issued_binding_id IS DISTINCT FROM '00000000-0000-0000-0000-000000000000'::uuid
            OR policy_version_id IS NOT NULL OR evidence_ref IS NOT NULL OR certification_effect_id IS NOT NULL OR notes IS NOT NULL
            OR current_accepted_quantity IS DISTINCT FROM 0 OR current_accepted_net_amount IS DISTINCT FROM 0)) THEN
        RAISE EXCEPTION 'P25LegacyEvidenceWasGuessedOrBackfilled';
    END IF;
    IF EXISTS (SELECT 1 FROM procurement.receiving_source_states WHERE tenant_id='p25-legacy-rehearsal')
        OR EXISTS (SELECT 1 FROM procurement.receiving_acceptance_effects WHERE tenant_id='p25-legacy-rehearsal')
        OR EXISTS (SELECT 1 FROM procurement.policy_versions WHERE tenant_id='p25-legacy-rehearsal' AND policy_key='RECEIVING') THEN
        RAISE EXCEPTION 'P25LegacyFixtureAcquiredInventedAuthority';
    END IF;
    IF NOT EXISTS (SELECT 1 FROM procurement.goods_receipt_lines WHERE id='25000000-0000-0000-0000-000000000009'
        AND tenant_id='p25-legacy-rehearsal' AND received_quantity=10.5000 AND accepted_quantity=7.2500 AND rejected_quantity=1.2500)
        OR NOT EXISTS (SELECT 1 FROM procurement.receipt_inspections WHERE id='25000000-0000-0000-0000-000000000010'
        AND tenant_id='p25-legacy-rehearsal' AND inspected_quantity=10.5000 AND defect_quantity=1.2500 AND inspected_at='2026-09-04T10:00:00Z')
        OR NOT EXISTS (SELECT 1 FROM procurement.service_acceptances WHERE id='25000000-0000-0000-0000-000000000011'
        AND tenant_id='p25-legacy-rehearsal' AND accepted_quantity=3.1250 AND accepted_amount=277.7775 AND status='Accepted'
            AND reason=repeat('Retained legacy service reason. ',60) AND certified_at='2026-10-01T12:30:00Z') THEN
        RAISE EXCEPTION 'P25LegacyNonzeroEvidenceChanged';
    END IF;
END $unverified$;
COMMIT;
