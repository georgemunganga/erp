-- Private clone only. Deliberately unverified legacy0 result exercises ONLY numeric Down loss.
-- No mapping, lease, claim, review, invoice approval or payable authority is fabricated.
BEGIN;
DO $fixture$
DECLARE original uuid := gen_random_uuid(); supplier record;
BEGIN
    SELECT tenant_id,legal_entity_id,id INTO STRICT supplier FROM procurement.suppliers ORDER BY id LIMIT 1;
    INSERT INTO procurement.supplier_invoices
        (id,tenant_id,legal_entity_id,created_at,created_by,version,supplier_id,
         invoice_number,normalized_invoice_number,status,intake_channel,currency_code,
         net_amount,tax_amount,gross_amount,invoice_date,mapping_revision,match_revision,matching_state)
    VALUES(original,supplier.tenant_id,supplier.legal_entity_id,now(),'p26-guard-rehearsal',1,supplier.id,
           'P26-LEGACY-TAX-GUARD','P26-LEGACY-TAX-GUARD','Received','Manual','ZMW',0,0,0,current_date,0,0,'NotCompared');
    INSERT INTO procurement.invoice_match_results
        (id,tenant_id,legal_entity_id,created_at,created_by,version,supplier_invoice_id,
         match_type,outcome,matched_at,quantity_variance,price_variance,tax_variance,execution_version,invoice_version)
    VALUES(gen_random_uuid(),supplier.tenant_id,supplier.legal_entity_id,now(),'p26-guard-rehearsal',1,original,
           'UnverifiedRehearsal','UnverifiedRehearsal',now(),0,0,0.00000001,0,0);
END $fixture$;
COMMIT;
