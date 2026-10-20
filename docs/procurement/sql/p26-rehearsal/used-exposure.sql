-- Private clone only. This records UNKNOWN coverage to test migration retention.
-- It is NOT authenticated Finance history, a successful match, or an allocated claim.
BEGIN;
DO $fixture$
DECLARE inserted integer;
BEGIN
    INSERT INTO procurement.invoice_match_exposure_states
        (id,tenant_id,legal_entity_id,created_at,created_by,version,
         purchase_order_id,issued_version_id,supplier_id,currency_code,sequence,coverage_state)
    SELECT gen_random_uuid(),i.tenant_id,i.legal_entity_id,now(),'p26-guard-rehearsal',1,
           i.purchase_order_id,i.id,p.supplier_id,p.currency_code,0,'Unknown'
      FROM procurement.purchase_order_issued_versions i
      JOIN procurement.purchase_orders p ON (p.tenant_id,p.legal_entity_id,p.id)=(i.tenant_id,i.legal_entity_id,i.purchase_order_id)
     ORDER BY i.id LIMIT 1;
    GET DIAGNOSTICS inserted = ROW_COUNT;
    IF inserted<>1 THEN RAISE EXCEPTION 'P26RehearsalNeedsActualIssuedSource'; END IF;
END $fixture$;
COMMIT;
