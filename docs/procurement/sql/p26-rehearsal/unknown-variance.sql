-- NEW disposable UNKNOWN clone only, after legacy-tax-precision.sql.
-- Changes only the synthetic unverified fixture, never original actual P25 source evidence.
-- Representable tax isolates NULL loss from tax-scale loss; all execution metadata stays absent.
BEGIN;
DO $fixture$
DECLARE changed integer;
BEGIN
    UPDATE procurement.invoice_match_results r
       SET quantity_variance=NULL, tax_variance=0.3456
      FROM procurement.supplier_invoices i
     WHERE r.supplier_invoice_id=i.id AND r.tenant_id=i.tenant_id AND r.legal_entity_id=i.legal_entity_id
       AND i.invoice_number='P26-LEGACY-TAX-GUARD' AND r.created_by='p26-guard-rehearsal'
       AND r.execution_version=0;
    GET DIAGNOSTICS changed=ROW_COUNT;
    IF changed<>1 THEN RAISE EXCEPTION 'P26UnknownVarianceFixtureMissingOrAmbiguous'; END IF;
END $fixture$;
COMMIT;
