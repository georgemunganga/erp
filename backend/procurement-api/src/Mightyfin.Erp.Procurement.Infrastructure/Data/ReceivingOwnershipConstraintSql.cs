namespace Mightyfin.Erp.Procurement.Infrastructure.Data;

/// <summary>
/// P25 migration-owned PostgreSQL overlay: exact final business bindings, deliberately
/// separate from EF's immutable identity keys. Future migrations touching these tuples
/// must preserve/recreate these constraints; an EF model-drift check alone cannot prove them.
/// </summary>
public static class ReceivingOwnershipConstraintSql
{
    public const string InspectionGoods = "fk_receiving_inspection_exact_physical";
    public const string EffectGoods = "fk_receiving_effect_exact_physical";
    public const string EffectService = "fk_receiving_effect_exact_service";
    // NO ACTION permits deferral; RESTRICT would check immediately regardless of DEFERRABLE.
    public const string Install = """
        ALTER TABLE procurement.receipt_inspections
          ADD CONSTRAINT fk_receiving_inspection_exact_physical
          FOREIGN KEY (tenant_id, legal_entity_id, goods_receipt_line_id, receiving_source_state_id, physical_observation_id)
          REFERENCES procurement.goods_receipt_lines (tenant_id, legal_entity_id, id, source_state_binding_id, physical_binding_id)
          ON DELETE NO ACTION DEFERRABLE INITIALLY DEFERRED;
        ALTER TABLE procurement.receiving_acceptance_effects
          ADD CONSTRAINT fk_receiving_effect_exact_physical
          FOREIGN KEY (tenant_id, legal_entity_id, goods_receipt_line_id, receiving_source_state_id, physical_observation_id)
          REFERENCES procurement.goods_receipt_lines (tenant_id, legal_entity_id, id, source_state_binding_id, physical_binding_id)
          ON DELETE NO ACTION DEFERRABLE INITIALLY DEFERRED;
        ALTER TABLE procurement.receiving_acceptance_effects
          ADD CONSTRAINT fk_receiving_effect_exact_service
          FOREIGN KEY (tenant_id, legal_entity_id, service_acceptance_id, receiving_source_state_id)
          REFERENCES procurement.service_acceptances (tenant_id, legal_entity_id, id, source_state_binding_id)
          ON DELETE NO ACTION DEFERRABLE INITIALLY DEFERRED;
        """;
    public const string ManifestQuery = """
        WITH expected(name, child, parent, childcols, parentcols) AS (VALUES
          ('fk_receiving_inspection_exact_physical', 'procurement.receipt_inspections', 'procurement.goods_receipt_lines',
           ARRAY['tenant_id','legal_entity_id','goods_receipt_line_id','receiving_source_state_id','physical_observation_id'],
           ARRAY['tenant_id','legal_entity_id','id','source_state_binding_id','physical_binding_id']),
          ('fk_receiving_effect_exact_physical', 'procurement.receiving_acceptance_effects', 'procurement.goods_receipt_lines',
           ARRAY['tenant_id','legal_entity_id','goods_receipt_line_id','receiving_source_state_id','physical_observation_id'],
           ARRAY['tenant_id','legal_entity_id','id','source_state_binding_id','physical_binding_id']),
          ('fk_receiving_effect_exact_service', 'procurement.receiving_acceptance_effects', 'procurement.service_acceptances',
           ARRAY['tenant_id','legal_entity_id','service_acceptance_id','receiving_source_state_id'],
           ARRAY['tenant_id','legal_entity_id','id','source_state_binding_id']))
        SELECT count(*) = 3 AND coalesce(bool_and(
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
