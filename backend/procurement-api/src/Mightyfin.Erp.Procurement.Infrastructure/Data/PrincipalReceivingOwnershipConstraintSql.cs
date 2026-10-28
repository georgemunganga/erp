namespace Mightyfin.Erp.Procurement.Infrastructure.Data;

/// <summary>Authored ownership overlay proposal, not installed or operationally ready.
/// The central genuine migration must freeze these literals after ordinary unique
/// indexes exist, preserve older P25/P26 overlays, then verify actual PostgreSQL.
/// No CAS/revision/balance column becomes an EF key.</summary>
public static class PrincipalReceivingOwnershipConstraintSql
{
    public const string Install = """
        ALTER TABLE procurement.principal_receiving_facts ADD CONSTRAINT fk_prcv_exact_fact_receipt
          FOREIGN KEY (tenant_id,legal_entity_id,command_key,draft_id,actor_principal_id,branch_id,operation)
          REFERENCES procurement.principal_receiving_receipts (tenant_id,legal_entity_id,command_key,draft_id,actor_principal_id,branch_id,operation)
          ON DELETE NO ACTION DEFERRABLE INITIALLY DEFERRED;
        ALTER TABLE procurement.principal_receiving_sources ADD CONSTRAINT fk_prcv_exact_issued
          FOREIGN KEY (tenant_id,legal_entity_id,issued_version_id,purchase_order_id,submission_id,funding_evidence_id,issued_snapshot_sha256,original_source_sha256)
          REFERENCES procurement.principal_order_issued_versions (tenant_id,legal_entity_id,id,purchase_order_id,submission_id,funding_evidence_id,commercial_snapshot_sha256,original_direct_source_sha256)
          ON DELETE NO ACTION DEFERRABLE INITIALLY DEFERRED;
        ALTER TABLE procurement.principal_receiving_sources ADD CONSTRAINT fk_prcv_exact_ack
          FOREIGN KEY (tenant_id,legal_entity_id,acknowledgment_id,issued_version_id,purchase_order_id)
          REFERENCES procurement.principal_order_acknowledgments (tenant_id,legal_entity_id,id,issued_version_id,purchase_order_id)
          ON DELETE NO ACTION DEFERRABLE INITIALLY DEFERRED;
        ALTER TABLE procurement.principal_receiving_sources ADD CONSTRAINT fk_prcv_exact_original
          FOREIGN KEY (tenant_id,legal_entity_id,source_binding_id,purchase_order_id,purchase_order_line_id,purchase_request_line_id,branch_id,original_source_sha256)
          REFERENCES procurement.principal_order_source_bindings (tenant_id,legal_entity_id,id,purchase_order_id,purchase_order_line_id,purchase_request_line_id,branch_id,original_direct_source_sha256)
          ON DELETE NO ACTION DEFERRABLE INITIALLY DEFERRED;
        ALTER TABLE procurement.principal_receiving_sources ADD CONSTRAINT fk_prcv_exact_po_line
          FOREIGN KEY (tenant_id,legal_entity_id,purchase_order_line_id,purchase_order_id,purchase_request_line_id)
          REFERENCES procurement.purchase_order_lines (tenant_id,legal_entity_id,id,purchase_order_id,purchase_request_line_id)
          ON DELETE NO ACTION DEFERRABLE INITIALLY DEFERRED;
        ALTER TABLE procurement.principal_receiving_drafts ADD CONSTRAINT fk_prcv_exact_draft_issued
          FOREIGN KEY (tenant_id,legal_entity_id,issued_version_id,purchase_order_id)
          REFERENCES procurement.principal_order_issued_versions (tenant_id,legal_entity_id,id,purchase_order_id)
          ON DELETE NO ACTION DEFERRABLE INITIALLY DEFERRED;
        ALTER TABLE procurement.principal_receiving_line_bindings ADD CONSTRAINT fk_prcv_exact_line_source
          FOREIGN KEY (tenant_id,legal_entity_id,source_state_id,branch_id,purchase_order_id,issued_version_id,purchase_order_line_id)
          REFERENCES procurement.principal_receiving_sources (tenant_id,legal_entity_id,id,branch_id,purchase_order_id,issued_version_id,purchase_order_line_id)
          ON DELETE NO ACTION DEFERRABLE INITIALLY DEFERRED;
        ALTER TABLE procurement.principal_receiving_line_bindings ADD CONSTRAINT fk_prcv_exact_line_revision
          FOREIGN KEY (tenant_id,legal_entity_id,draft_revision_id,draft_id)
          REFERENCES procurement.principal_receiving_revisions (tenant_id,legal_entity_id,id,draft_id)
          ON DELETE NO ACTION DEFERRABLE INITIALLY DEFERRED;
        ALTER TABLE procurement.principal_receiving_facts ADD CONSTRAINT fk_prcv_exact_fact_line
          FOREIGN KEY (tenant_id,legal_entity_id,draft_line_binding_id,source_state_id,draft_id,branch_id)
          REFERENCES procurement.principal_receiving_line_bindings (tenant_id,legal_entity_id,id,source_state_id,draft_id,branch_id)
          ON DELETE NO ACTION DEFERRABLE INITIALLY DEFERRED;
        ALTER TABLE procurement.principal_receiving_receipts ADD CONSTRAINT fk_prcv_exact_receipt_draft
          FOREIGN KEY (tenant_id,legal_entity_id,draft_id,branch_id)
          REFERENCES procurement.principal_receiving_drafts (tenant_id,legal_entity_id,id,branch_id)
          ON DELETE NO ACTION DEFERRABLE INITIALLY DEFERRED;
        ALTER TABLE procurement.principal_receiving_line_bindings ADD CONSTRAINT fk_prcv_exact_line_draft
          FOREIGN KEY (tenant_id,legal_entity_id,draft_id,branch_id,purchase_order_id,issued_version_id)
          REFERENCES procurement.principal_receiving_drafts (tenant_id,legal_entity_id,id,branch_id,purchase_order_id,issued_version_id)
          ON DELETE NO ACTION DEFERRABLE INITIALLY DEFERRED;
        """;
    // Central Down must first lock and veto retained usage; this constant does
    // not authorize evidence deletion or bypass a rollback guard.
    public const string Remove = """
        ALTER TABLE procurement.principal_receiving_facts DROP CONSTRAINT fk_prcv_exact_fact_receipt;
        ALTER TABLE procurement.principal_receiving_line_bindings DROP CONSTRAINT fk_prcv_exact_line_draft;
        ALTER TABLE procurement.principal_receiving_receipts DROP CONSTRAINT fk_prcv_exact_receipt_draft;
        ALTER TABLE procurement.principal_receiving_facts DROP CONSTRAINT fk_prcv_exact_fact_line;
        ALTER TABLE procurement.principal_receiving_line_bindings DROP CONSTRAINT fk_prcv_exact_line_revision;
        ALTER TABLE procurement.principal_receiving_line_bindings DROP CONSTRAINT fk_prcv_exact_line_source;
        ALTER TABLE procurement.principal_receiving_drafts DROP CONSTRAINT fk_prcv_exact_draft_issued;
        ALTER TABLE procurement.principal_receiving_sources DROP CONSTRAINT fk_prcv_exact_po_line;
        ALTER TABLE procurement.principal_receiving_sources DROP CONSTRAINT fk_prcv_exact_original;
        ALTER TABLE procurement.principal_receiving_sources DROP CONSTRAINT fk_prcv_exact_ack;
        ALTER TABLE procurement.principal_receiving_sources DROP CONSTRAINT fk_prcv_exact_issued;
        """;
    public const string ManifestQuery = """
        WITH expected(name, child, parent, childcols, parentcols) AS (VALUES
          ('fk_prcv_exact_fact_receipt','procurement.principal_receiving_facts','procurement.principal_receiving_receipts',
           ARRAY['tenant_id','legal_entity_id','command_key','draft_id','actor_principal_id','branch_id','operation'],ARRAY['tenant_id','legal_entity_id','command_key','draft_id','actor_principal_id','branch_id','operation']),
          ('fk_prcv_exact_issued','procurement.principal_receiving_sources','procurement.principal_order_issued_versions',
           ARRAY['tenant_id','legal_entity_id','issued_version_id','purchase_order_id','submission_id','funding_evidence_id','issued_snapshot_sha256','original_source_sha256'],ARRAY['tenant_id','legal_entity_id','id','purchase_order_id','submission_id','funding_evidence_id','commercial_snapshot_sha256','original_direct_source_sha256']),
          ('fk_prcv_exact_ack','procurement.principal_receiving_sources','procurement.principal_order_acknowledgments',
           ARRAY['tenant_id','legal_entity_id','acknowledgment_id','issued_version_id','purchase_order_id'],ARRAY['tenant_id','legal_entity_id','id','issued_version_id','purchase_order_id']),
          ('fk_prcv_exact_original','procurement.principal_receiving_sources','procurement.principal_order_source_bindings',
           ARRAY['tenant_id','legal_entity_id','source_binding_id','purchase_order_id','purchase_order_line_id','purchase_request_line_id','branch_id','original_source_sha256'],ARRAY['tenant_id','legal_entity_id','id','purchase_order_id','purchase_order_line_id','purchase_request_line_id','branch_id','original_direct_source_sha256']),
          ('fk_prcv_exact_po_line','procurement.principal_receiving_sources','procurement.purchase_order_lines',
           ARRAY['tenant_id','legal_entity_id','purchase_order_line_id','purchase_order_id','purchase_request_line_id'],ARRAY['tenant_id','legal_entity_id','id','purchase_order_id','purchase_request_line_id']),
          ('fk_prcv_exact_draft_issued','procurement.principal_receiving_drafts','procurement.principal_order_issued_versions',
           ARRAY['tenant_id','legal_entity_id','issued_version_id','purchase_order_id'],ARRAY['tenant_id','legal_entity_id','id','purchase_order_id']),
          ('fk_prcv_exact_line_source','procurement.principal_receiving_line_bindings','procurement.principal_receiving_sources',
           ARRAY['tenant_id','legal_entity_id','source_state_id','branch_id','purchase_order_id','issued_version_id','purchase_order_line_id'],ARRAY['tenant_id','legal_entity_id','id','branch_id','purchase_order_id','issued_version_id','purchase_order_line_id']),
          ('fk_prcv_exact_line_revision','procurement.principal_receiving_line_bindings','procurement.principal_receiving_revisions',
           ARRAY['tenant_id','legal_entity_id','draft_revision_id','draft_id'],ARRAY['tenant_id','legal_entity_id','id','draft_id']),
          ('fk_prcv_exact_fact_line','procurement.principal_receiving_facts','procurement.principal_receiving_line_bindings',
           ARRAY['tenant_id','legal_entity_id','draft_line_binding_id','source_state_id','draft_id','branch_id'],ARRAY['tenant_id','legal_entity_id','id','source_state_id','draft_id','branch_id']),
          ('fk_prcv_exact_receipt_draft','procurement.principal_receiving_receipts','procurement.principal_receiving_drafts',
           ARRAY['tenant_id','legal_entity_id','draft_id','branch_id'],ARRAY['tenant_id','legal_entity_id','id','branch_id']),
          ('fk_prcv_exact_line_draft','procurement.principal_receiving_line_bindings','procurement.principal_receiving_drafts',
           ARRAY['tenant_id','legal_entity_id','draft_id','branch_id','purchase_order_id','issued_version_id'],ARRAY['tenant_id','legal_entity_id','id','branch_id','purchase_order_id','issued_version_id']))
        SELECT count(*)=11 AND coalesce(bool_and(
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
