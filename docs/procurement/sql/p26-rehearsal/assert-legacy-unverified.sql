-- Read-only post-Up verification: additive fields must NOT promote retained legacy evidence.
DO $assert$
DECLARE original uuid;
BEGIN
    SELECT id INTO STRICT original FROM procurement.supplier_invoices WHERE invoice_number='P26-LEGACY-ORIGINAL';
    IF NOT EXISTS (SELECT 1 FROM procurement.supplier_invoices WHERE id=original
        AND mapping_revision=0 AND match_revision=0 AND matching_state='NotCompared'
        AND current_mapping_version_id IS NULL AND last_match_result_id IS NULL AND active_match_claim_result_id IS NULL)
       OR (SELECT count(*) FROM procurement.supplier_invoice_lines WHERE supplier_invoice_id=original)<>1
       OR (SELECT count(*) FROM procurement.invoice_match_results WHERE supplier_invoice_id=original
        AND execution_version=0 AND invoice_version=0 AND mapping_version_id IS NULL
        AND purchase_order_id IS NULL AND issued_version_id IS NULL AND invoice_original_sha256 IS NULL
        AND actor_principal_id IS NULL AND actor_worker_id IS NULL AND command_key IS NULL
        AND input_fingerprint IS NULL AND evidence_json IS NULL AND evidence_sha256 IS NULL
        AND policy_rules_sha256 IS NULL AND source_watermarks_json IS NULL)<>1
       OR (SELECT count(*) FROM procurement.invoice_exceptions WHERE supplier_invoice_id=original
        AND match_result_id IS NULL AND supplier_invoice_line_id IS NULL AND discrepancy_key IS NULL
        AND owner_worker_id IS NULL AND resolving_match_result_id IS NULL)<>1
       OR EXISTS (SELECT 1 FROM procurement.invoice_match_mapping_versions)
       OR EXISTS (SELECT 1 FROM procurement.invoice_match_allocations)
       OR EXISTS (SELECT 1 FROM procurement.invoice_match_exposure_states) THEN
        RAISE EXCEPTION 'P26LegacyOriginalWasPromotedOrChanged';
    END IF;
END $assert$;
