namespace Mightyfin.Erp.Procurement.Infrastructure.Migrations;

/// <summary>
/// Authored companion for the genuine P31 EF migration. It must be applied
/// after EF creates all thirteen owned tables and their mapped foreign keys.
/// These three nullable current-state pointers can name rows added by the
/// same command. The complete mapped owner chain is otherwise acyclic; this
/// companion does not relax EF's own dependency ordering or validate an
/// incomplete business evidence graph.
/// </summary>
internal static class P31PrincipalInvoiceOwnershipGuards
{
    internal const string InstallSql = """
        ALTER TABLE procurement.principal_invoice_states
          ALTER CONSTRAINT fk_pi_state_current_mapping DEFERRABLE INITIALLY DEFERRED;
        ALTER TABLE procurement.principal_invoice_states
          ALTER CONSTRAINT fk_pi_state_last_comparison DEFERRABLE INITIALLY DEFERRED;
        ALTER TABLE procurement.principal_invoice_states
          ALTER CONSTRAINT fk_pi_state_active_claim DEFERRABLE INITIALLY DEFERRED;
        """;

    // Include this exact result in the P31 physical capture. Presence alone
    // is insufficient: column order, owner tables, and deferred state matter.
    internal const string PointerManifestSql = """
        WITH expected(name, parent, childcols, parentcols) AS (VALUES
          ('fk_pi_state_current_mapping','procurement.principal_invoice_mappings',
           ARRAY['tenant_id','legal_entity_id','current_mapping_id','supplier_invoice_id'],
           ARRAY['tenant_id','legal_entity_id','id','supplier_invoice_id']),
          ('fk_pi_state_last_comparison','procurement.principal_invoice_comparisons',
           ARRAY['tenant_id','legal_entity_id','last_comparison_id','supplier_invoice_id'],
           ARRAY['tenant_id','legal_entity_id','id','supplier_invoice_id']),
          ('fk_pi_state_active_claim','procurement.principal_invoice_comparisons',
           ARRAY['tenant_id','legal_entity_id','active_claim_comparison_id','supplier_invoice_id'],
           ARRAY['tenant_id','legal_entity_id','id','supplier_invoice_id']))
        SELECT count(*)=3 AND coalesce(bool_and(
          c.oid IS NOT NULL AND c.contype='f' AND c.convalidated
          AND c.condeferrable AND c.condeferred AND c.confmatchtype='s'
          AND c.confdeltype='r' AND c.confupdtype='a'
          AND ARRAY(SELECT a.attname::text FROM unnest(c.conkey) WITH ORDINALITY k(n,pos)
            JOIN pg_attribute a ON a.attrelid=c.conrelid AND a.attnum=k.n ORDER BY k.pos)=e.childcols
          AND ARRAY(SELECT a.attname::text FROM unnest(c.confkey) WITH ORDINALITY k(n,pos)
            JOIN pg_attribute a ON a.attrelid=c.confrelid AND a.attnum=k.n ORDER BY k.pos)=e.parentcols
        ),false)
        FROM expected e
        LEFT JOIN pg_constraint c ON c.conname=e.name AND c.connamespace='procurement'::regnamespace
          AND c.conrelid=to_regclass('procurement.principal_invoice_states')
          AND c.confrelid=to_regclass(e.parent);
        """;

    // First statement in Down, before any pointer, table, FK, or grant is
    // removed. A retained row in ANY P31 family blocks downgrade atomically.
    internal const string BeforeDownSql = """
        LOCK TABLE procurement.principal_invoice_states,
          procurement.principal_invoice_mappings,
          procurement.principal_invoice_mapping_lines,
          procurement.principal_invoice_unit_reviews,
          procurement.principal_invoice_review_assignments,
          procurement.principal_invoice_review_attestations,
          procurement.principal_invoice_review_outcomes,
          procurement.principal_invoice_comparisons,
          procurement.principal_invoice_exposure_states,
          procurement.principal_invoice_claims,
          procurement.principal_invoice_receipts,
          procurement.principal_invoice_discrepancies,
          procurement.principal_invoice_discrepancy_actions IN ACCESS EXCLUSIVE MODE;
        DO $$
        BEGIN
          IF EXISTS (SELECT 1 FROM procurement.principal_invoice_states)
            OR EXISTS (SELECT 1 FROM procurement.principal_invoice_mappings)
            OR EXISTS (SELECT 1 FROM procurement.principal_invoice_mapping_lines)
            OR EXISTS (SELECT 1 FROM procurement.principal_invoice_unit_reviews)
            OR EXISTS (SELECT 1 FROM procurement.principal_invoice_review_assignments)
            OR EXISTS (SELECT 1 FROM procurement.principal_invoice_review_attestations)
            OR EXISTS (SELECT 1 FROM procurement.principal_invoice_review_outcomes)
            OR EXISTS (SELECT 1 FROM procurement.principal_invoice_comparisons)
            OR EXISTS (SELECT 1 FROM procurement.principal_invoice_exposure_states)
            OR EXISTS (SELECT 1 FROM procurement.principal_invoice_claims)
            OR EXISTS (SELECT 1 FROM procurement.principal_invoice_receipts)
            OR EXISTS (SELECT 1 FROM procurement.principal_invoice_discrepancies)
            OR EXISTS (SELECT 1 FROM procurement.principal_invoice_discrepancy_actions) THEN
            RAISE EXCEPTION 'P31 principal invoice evidence prevents downgrade' USING ERRCODE = '55000';
          END IF;
        END $$;
        """;
}
