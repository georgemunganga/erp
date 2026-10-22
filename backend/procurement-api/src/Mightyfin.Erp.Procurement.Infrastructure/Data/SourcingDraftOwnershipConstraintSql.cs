namespace Mightyfin.Erp.Procurement.Infrastructure.Data;

/// <summary>PROPOSAL only: central future migration must freeze/review these literals after
/// generated ordinary unique indexes exist. No migration installs this source yet.</summary>
public static class SourcingDraftOwnershipConstraintSql
{
    public const string Install = """
        ALTER TABLE procurement.sourcing_events ADD CONSTRAINT fk_rfq_event_original
          FOREIGN KEY (tenant_id,legal_entity_id,source_evidence_id,id)
          REFERENCES procurement.sourcing_draft_evidence (tenant_id,legal_entity_id,id,sourcing_event_id)
          ON DELETE NO ACTION DEFERRABLE INITIALLY DEFERRED;
        ALTER TABLE procurement.sourcing_demand_bindings ADD CONSTRAINT fk_rfq_binding_original
          FOREIGN KEY (tenant_id,legal_entity_id,source_evidence_id,sourcing_event_id)
          REFERENCES procurement.sourcing_draft_evidence (tenant_id,legal_entity_id,id,sourcing_event_id)
          ON DELETE NO ACTION DEFERRABLE INITIALLY DEFERRED;
        ALTER TABLE procurement.sourcing_demand_bindings ADD CONSTRAINT fk_rfq_binding_exact_line
          FOREIGN KEY (tenant_id,legal_entity_id,sourcing_line_id,sourcing_event_id,purchase_request_line_id)
          REFERENCES procurement.sourcing_lines (tenant_id,legal_entity_id,id,sourcing_event_id,purchase_request_line_id)
          ON DELETE NO ACTION DEFERRABLE INITIALLY DEFERRED;
        ALTER TABLE procurement.sourcing_demand_bindings ADD CONSTRAINT fk_rfq_binding_exact_hold
          FOREIGN KEY (tenant_id,legal_entity_id,allocation_id,purchase_request_id,purchase_request_line_id,target_type,sourcing_event_id)
          REFERENCES procurement.purchase_request_allocations (tenant_id,legal_entity_id,id,purchase_request_id,purchase_request_line_id,target_type,target_id)
          ON DELETE NO ACTION DEFERRABLE INITIALLY DEFERRED;
        """;
    public const string ManifestQuery = """
        WITH expected(name, child, parent, childcols, parentcols) AS (VALUES
          ('fk_rfq_event_original','procurement.sourcing_events','procurement.sourcing_draft_evidence',
           ARRAY['tenant_id','legal_entity_id','source_evidence_id','id'],ARRAY['tenant_id','legal_entity_id','id','sourcing_event_id']),
          ('fk_rfq_binding_original','procurement.sourcing_demand_bindings','procurement.sourcing_draft_evidence',
           ARRAY['tenant_id','legal_entity_id','source_evidence_id','sourcing_event_id'],ARRAY['tenant_id','legal_entity_id','id','sourcing_event_id']),
          ('fk_rfq_binding_exact_line','procurement.sourcing_demand_bindings','procurement.sourcing_lines',
           ARRAY['tenant_id','legal_entity_id','sourcing_line_id','sourcing_event_id','purchase_request_line_id'],ARRAY['tenant_id','legal_entity_id','id','sourcing_event_id','purchase_request_line_id']),
          ('fk_rfq_binding_exact_hold','procurement.sourcing_demand_bindings','procurement.purchase_request_allocations',
           ARRAY['tenant_id','legal_entity_id','allocation_id','purchase_request_id','purchase_request_line_id','target_type','sourcing_event_id'],
           ARRAY['tenant_id','legal_entity_id','id','purchase_request_id','purchase_request_line_id','target_type','target_id']))
        SELECT count(*)=4 AND coalesce(bool_and(
          c.oid IS NOT NULL AND c.contype='f' AND c.convalidated AND c.condeferrable AND c.condeferred
          AND c.confdeltype='a' AND c.confupdtype='a' AND c.confmatchtype='s'
          AND i.indisunique AND i.indisvalid AND i.indimmediate
          AND ARRAY(SELECT a.attname::text FROM unnest(c.conkey) WITH ORDINALITY k(n,pos)
            JOIN pg_attribute a ON a.attrelid=c.conrelid AND a.attnum=k.n ORDER BY k.pos)=e.childcols
          AND ARRAY(SELECT a.attname::text FROM unnest(c.confkey) WITH ORDINALITY k(n,pos)
            JOIN pg_attribute a ON a.attrelid=c.confrelid AND a.attnum=k.n ORDER BY k.pos)=e.parentcols
        ),false)
        FROM expected e
        LEFT JOIN pg_constraint c ON c.conname=e.name AND c.connamespace='procurement'::regnamespace
          AND c.conrelid=to_regclass(e.child) AND c.confrelid=to_regclass(e.parent)
        LEFT JOIN pg_index i ON i.indexrelid=c.conindid;
        """;
}
