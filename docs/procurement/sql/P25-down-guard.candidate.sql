-- SOURCE REVIEW CANDIDATE ONLY. Never run as a standalone downgrade.
-- Copy/review against the centrally generated P25 migration, then execute FIRST in
-- its ordinary PostgreSQL migration transaction, before any DROP/ALTER. Do not use
-- suppressTransaction, delete records, reset fields or catch-and-ignore this guard.
SET LOCAL lock_timeout = '5s';
LOCK TABLE procurement.receiving_source_states, procurement.receiving_acceptance_effects,
    procurement.goods_receipts, procurement.goods_receipt_lines, procurement.receipt_inspections,
    procurement.service_acceptances, procurement.policy_versions,
    procurement.commercial_command_receipts, procurement.audit_events, procurement.outbox_events
    IN ACCESS EXCLUSIVE MODE;
DO $p25_guard$
BEGIN
    IF EXISTS (SELECT 1 FROM procurement.receiving_source_states)
        OR EXISTS (SELECT 1 FROM procurement.receiving_acceptance_effects)
        OR EXISTS (SELECT 1 FROM procurement.goods_receipts WHERE execution_version <> 0
            OR issued_version_id IS NOT NULL OR submission_id IS NOT NULL
            OR issued_binding_id <> '00000000-0000-0000-0000-000000000000'::uuid
            OR branch_ref IS NOT NULL OR receiver_worker_id IS NOT NULL OR recorded_at IS NOT NULL OR notes IS NOT NULL)
        OR EXISTS (SELECT 1 FROM procurement.goods_receipt_lines WHERE execution_version <> 0
            OR receiving_source_state_id IS NOT NULL OR source_purchase_order_id IS NOT NULL
            OR source_state_binding_id <> '00000000-0000-0000-0000-000000000000'::uuid
            OR issued_binding_id <> '00000000-0000-0000-0000-000000000000'::uuid
            OR initially_rejected_quantity <> 0 OR physical_observation_id IS NOT NULL
            OR physical_binding_id <> '00000000-0000-0000-0000-000000000000'::uuid
            OR physical_policy_version_id IS NOT NULL OR physical_evidence_ref IS NOT NULL
            OR physical_sequence IS NOT NULL OR physical_effect_key IS NOT NULL OR physical_recorded_at IS NOT NULL)
        OR EXISTS (SELECT 1 FROM procurement.receipt_inspections WHERE execution_version <> 0
            OR physical_binding_id <> '00000000-0000-0000-0000-000000000000'::uuid
            OR physical_observation_id IS NOT NULL OR receiving_source_state_id IS NOT NULL
            OR source_state_binding_id <> '00000000-0000-0000-0000-000000000000'::uuid
            OR qualified_quantity <> 0 OR inspector_worker_id IS NOT NULL OR policy_version_id IS NOT NULL
            OR evidence_ref IS NOT NULL OR ledger_sequence IS NOT NULL OR effect_key IS NOT NULL)
        OR EXISTS (SELECT 1 FROM procurement.service_acceptances WHERE execution_version <> 0
            OR receiving_source_state_id IS NOT NULL OR source_purchase_order_id IS NOT NULL
            OR source_state_binding_id <> '00000000-0000-0000-0000-000000000000'::uuid
            OR issued_binding_id <> '00000000-0000-0000-0000-000000000000'::uuid
            OR owner_worker_id IS NOT NULL OR policy_version_id IS NOT NULL OR evidence_ref IS NOT NULL
            OR certification_effect_id IS NOT NULL OR current_accepted_quantity <> 0 OR current_accepted_net_amount <> 0)
        OR EXISTS (SELECT 1 FROM procurement.policy_versions WHERE upper(btrim(policy_key)) = 'RECEIVING')
        OR EXISTS (SELECT 1 FROM procurement.commercial_command_receipts WHERE operation IN
            ('ReceivingDraft','ReceivingRecord','ReceivingInspect','ReceivingAccept','ReceivingCancel','ServiceDraft','ServiceCertify','ServiceCancel'))
        OR EXISTS (SELECT 1 FROM procurement.audit_events WHERE action IN
            ('ReceivingDraft','ReceivingRecord','ReceivingInspect','ReceivingAccept','ReceivingCancel','ServiceDraft','ServiceCertify','ServiceCancel'))
        OR EXISTS (SELECT 1 FROM procurement.audit_events
            WHERE record_type IN ('ReceivingSourceState','ReceivingAcceptanceEffect'))
        OR EXISTS (SELECT 1 FROM procurement.outbox_events WHERE aggregate_type = 'Receiving'
            OR event_type IN ('procurement.goods.recorded.v1','procurement.goods.inspected.v1','procurement.fulfilment.accepted.v1')) THEN
        RAISE EXCEPTION 'P25RollbackRequiresEvidencePreservingRecovery: receiving has been configured or used; use a reviewed forward fix or protected restore, preserving records and evidence.';
    END IF;
    -- P25 widens an EXISTING service amount. Even a v0 owner-written value must not
    -- silently round or overflow on restoration of the prior numeric(18,4) column.
    IF EXISTS (SELECT 1 FROM procurement.service_acceptances
        WHERE accepted_amount <> trunc(accepted_amount, 4) OR abs(accepted_amount) >= 100000000000000) THEN
        RAISE EXCEPTION 'P25RollbackAmountWouldLosePrecision: retained service amount does not fit numeric(18,4) exactly.';
    END IF;
END $p25_guard$;
