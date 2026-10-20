-- PRE-P26 disposable populated clone only: original storage preservation, NOT matching authority.
-- Uses only P25 columns. P26 must leave this legacy invoice/result/exception unverified.
BEGIN;
DO $fixture$
DECLARE original uuid := gen_random_uuid(); supplier record;
BEGIN
    SELECT tenant_id,legal_entity_id,id INTO STRICT supplier FROM procurement.suppliers ORDER BY id LIMIT 1;
    INSERT INTO procurement.supplier_invoices
        (id,tenant_id,legal_entity_id,created_at,created_by,version,supplier_id,
         invoice_number,normalized_invoice_number,status,intake_channel,currency_code,
         net_amount,tax_amount,gross_amount,invoice_date,due_date,credit_for_invoice_ref)
    VALUES(original,supplier.tenant_id,supplier.legal_entity_id,'2026-10-19 10:00:00+00','p26-legacy-original',7,supplier.id,
           'P26-LEGACY-ORIGINAL','P26-LEGACY-ORIGINAL','Received','Manual','ZMW',
           24.6912,1.2345,25.9257,'2026-10-18','2026-11-18','RETAIN-LEGACY-REFERENCE');
    INSERT INTO procurement.supplier_invoice_lines
        (id,tenant_id,legal_entity_id,created_at,created_by,version,supplier_invoice_id,
         line_number,description,quantity,unit_price,tax_amount,tax_code_ref,cost_center_ref)
    VALUES(gen_random_uuid(),supplier.tenant_id,supplier.legal_entity_id,'2026-10-19 10:00:00+00','p26-legacy-original',3,original,
           1,'Retain original legacy line',2,12.3456,1.2345,'LEGACY-TAX','LEGACY-COST-CENTRE');
    INSERT INTO procurement.invoice_match_results
        (id,tenant_id,legal_entity_id,created_at,created_by,version,supplier_invoice_id,
         match_type,outcome,matched_at,quantity_variance,price_variance,tax_variance)
    VALUES(gen_random_uuid(),supplier.tenant_id,supplier.legal_entity_id,'2026-10-19 10:01:00+00','p26-legacy-original',5,original,
           'UnverifiedRehearsal','UnverifiedRehearsal','2026-10-19 10:01:00+00',0.1234,-0.2345,0.3456);
    INSERT INTO procurement.invoice_exceptions
        (id,tenant_id,legal_entity_id,created_at,created_by,version,supplier_invoice_id,
         type,status,owner_subject_id,reason,resolution,due_at,resolved_at)
    VALUES(gen_random_uuid(),supplier.tenant_id,supplier.legal_entity_id,'2026-10-19 10:02:00+00','p26-legacy-original',4,original,
           'LegacyReview','Open','retained-legacy-owner','Retain this original legacy reason',
           'Retain original historical note','2026-11-01 10:00:00+00',NULL);
END $fixture$;
COMMIT;
