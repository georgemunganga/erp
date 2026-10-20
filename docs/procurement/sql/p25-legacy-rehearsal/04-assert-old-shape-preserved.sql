-- psql variable legacy_baseline contains the exact JSON from02, supplied by the private runner.
-- One read-only transaction; no helper table/function or schema mutation.
\set ON_ERROR_STOP on
BEGIN READ ONLY;
SELECT set_config('greenwebb.p25_legacy_baseline', :'legacy_baseline', true) AS baseline_applied \gset
DO $preservation$
DECLARE actual jsonb;
BEGIN
    IF current_database() NOT LIKE 'procurement_p25_restore_%' THEN
        RAISE EXCEPTION 'P25FixturePrivateRestoredDatabaseRequired';
    END IF;
    SELECT jsonb_build_object(
    'fixtureSchemaVersion',1,
    'fixtureTenant','p25-legacy-rehearsal',
    'suppliers', coalesce((SELECT jsonb_agg((to_jsonb(t)) ORDER BY t.id) FROM procurement.suppliers t WHERE t.tenant_id='p25-legacy-rehearsal'), '[]'::jsonb),
    'purchase_requests', coalesce((SELECT jsonb_agg((to_jsonb(t)) ORDER BY t.id) FROM procurement.purchase_requests t WHERE t.tenant_id='p25-legacy-rehearsal'), '[]'::jsonb),
    'purchase_request_lines', coalesce((SELECT jsonb_agg((to_jsonb(t)) ORDER BY t.id) FROM procurement.purchase_request_lines t WHERE t.tenant_id='p25-legacy-rehearsal'), '[]'::jsonb),
    'purchase_orders', coalesce((SELECT jsonb_agg((to_jsonb(t)) ORDER BY t.id) FROM procurement.purchase_orders t WHERE t.tenant_id='p25-legacy-rehearsal'), '[]'::jsonb),
    'purchase_order_lines', coalesce((SELECT jsonb_agg((to_jsonb(t)) ORDER BY t.id) FROM procurement.purchase_order_lines t WHERE t.tenant_id='p25-legacy-rehearsal'), '[]'::jsonb),
    'goods_receipts', coalesce((SELECT jsonb_agg((to_jsonb(t) - ARRAY['branch_ref','execution_version','issued_binding_id','issued_version_id','notes','receiver_worker_id','recorded_at','submission_id']::text[]) ORDER BY t.id) FROM procurement.goods_receipts t WHERE t.tenant_id='p25-legacy-rehearsal'), '[]'::jsonb),
    'goods_receipt_lines', coalesce((SELECT jsonb_agg((to_jsonb(t) - ARRAY['execution_version','initially_rejected_quantity','issued_binding_id','physical_binding_id','physical_effect_key','physical_evidence_ref','physical_observation_id','physical_policy_version_id','physical_recorded_at','physical_sequence','receiving_source_state_id','source_purchase_order_id','source_state_binding_id']::text[]) ORDER BY t.id) FROM procurement.goods_receipt_lines t WHERE t.tenant_id='p25-legacy-rehearsal'), '[]'::jsonb),
    'receipt_inspections', coalesce((SELECT jsonb_agg((to_jsonb(t) - ARRAY['effect_key','evidence_ref','execution_version','inspector_worker_id','ledger_sequence','physical_binding_id','physical_observation_id','policy_version_id','qualified_quantity','receiving_source_state_id','source_state_binding_id']::text[]) ORDER BY t.id) FROM procurement.receipt_inspections t WHERE t.tenant_id='p25-legacy-rehearsal'), '[]'::jsonb),
    'service_acceptances', coalesce((SELECT jsonb_agg((to_jsonb(t) - ARRAY['certification_effect_id','current_accepted_net_amount','current_accepted_quantity','evidence_ref','execution_version','issued_binding_id','notes','owner_worker_id','policy_version_id','receiving_source_state_id','source_purchase_order_id','source_state_binding_id']::text[]) ORDER BY t.id) FROM procurement.service_acceptances t WHERE t.tenant_id='p25-legacy-rehearsal'), '[]'::jsonb)
) INTO actual;
    IF actual IS DISTINCT FROM current_setting('greenwebb.p25_legacy_baseline')::jsonb THEN
        RAISE EXCEPTION 'P25LegacyOriginalFieldsChanged: compare private before/after snapshots; do not reset records to pass';
    END IF;
    IF jsonb_array_length(actual->'goods_receipts')<>1 OR jsonb_array_length(actual->'goods_receipt_lines')<>1
        OR jsonb_array_length(actual->'receipt_inspections')<>1 OR jsonb_array_length(actual->'service_acceptances')<>1 THEN
        RAISE EXCEPTION 'P25LegacyFixtureMissingOrExpanded';
    END IF;
END $preservation$;
COMMIT;
