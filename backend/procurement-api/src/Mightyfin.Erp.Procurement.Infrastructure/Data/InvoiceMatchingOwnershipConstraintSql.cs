namespace Mightyfin.Erp.Procurement.Infrastructure.Data;

/// <summary>P26 migration-owned exact receiving-source overlay. Stable EF identities permit
/// draft receiving completion; deferred database FKs independently bind retained claim ownership.
/// Root must install this literal overlay after generated parent unique indexes exist.</summary>
public static class InvoiceMatchingOwnershipConstraintSql
{
    public const string Goods = "fk_invoice_allocation_exact_goods";
    public const string Service = "fk_invoice_allocation_exact_service";
    public const string Install = """
        ALTER TABLE procurement.invoice_match_allocations
          ADD CONSTRAINT fk_invoice_allocation_exact_goods
          FOREIGN KEY (tenant_id, legal_entity_id, goods_receipt_line_id, purchase_order_line_id, receiving_source_state_id)
          REFERENCES procurement.goods_receipt_lines (tenant_id, legal_entity_id, id, purchase_order_line_id, source_state_binding_id)
          ON DELETE NO ACTION DEFERRABLE INITIALLY DEFERRED;
        ALTER TABLE procurement.invoice_match_allocations
          ADD CONSTRAINT fk_invoice_allocation_exact_service
          FOREIGN KEY (tenant_id, legal_entity_id, service_acceptance_id, purchase_order_line_id, receiving_source_state_id)
          REFERENCES procurement.service_acceptances (tenant_id, legal_entity_id, id, purchase_order_line_id, source_state_binding_id)
          ON DELETE NO ACTION DEFERRABLE INITIALLY DEFERRED;
        """;
    public const string ManifestQuery = """
        WITH expected(name, child, parent, childcols, parentcols) AS (VALUES
          ('fk_invoice_allocation_exact_goods', 'procurement.invoice_match_allocations', 'procurement.goods_receipt_lines',
           ARRAY['tenant_id','legal_entity_id','goods_receipt_line_id','purchase_order_line_id','receiving_source_state_id'],
           ARRAY['tenant_id','legal_entity_id','id','purchase_order_line_id','source_state_binding_id']),
          ('fk_invoice_allocation_exact_service', 'procurement.invoice_match_allocations', 'procurement.service_acceptances',
           ARRAY['tenant_id','legal_entity_id','service_acceptance_id','purchase_order_line_id','receiving_source_state_id'],
           ARRAY['tenant_id','legal_entity_id','id','purchase_order_line_id','source_state_binding_id']))
        SELECT count(*) = 2 AND coalesce(bool_and(
          c.oid IS NOT NULL AND c.contype='f' AND c.convalidated AND c.condeferrable AND c.condeferred
          AND c.confdeltype='a' AND c.confupdtype='a' AND c.confmatchtype='s'
          AND i.indisunique AND i.indisvalid AND i.indimmediate
          AND ARRAY(SELECT a.attname::text FROM unnest(c.conkey) WITH ORDINALITY k(n,pos)
                    JOIN pg_attribute a ON a.attrelid=c.conrelid AND a.attnum=k.n ORDER BY k.pos) = e.childcols
          AND ARRAY(SELECT a.attname::text FROM unnest(c.confkey) WITH ORDINALITY k(n,pos)
                    JOIN pg_attribute a ON a.attrelid=c.confrelid AND a.attnum=k.n ORDER BY k.pos) = e.parentcols
        ),false)
        FROM expected e
        LEFT JOIN pg_constraint c ON c.conname=e.name AND c.connamespace='procurement'::regnamespace
          AND c.conrelid=to_regclass(e.child) AND c.confrelid=to_regclass(e.parent)
        LEFT JOIN pg_index i ON i.indexrelid=c.conindid;
        """;

}
