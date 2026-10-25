namespace Mightyfin.Erp.Procurement.Infrastructure.Migrations;

/// <summary>
/// Authored physical owner overlay proposal for the future genuine P37 migration.
/// It is deliberately uninstalled until the genuine EF scaffold, raw catalog capture,
/// finite ACL and owner negative probes are reviewed. Run AfterUpSql only after EF
/// creates all three P37 tables and the mapped exact receipt-to-revision command
/// and receipt-to-head branch FKs. This overlay complements those modeled FKs;
/// it must not replace or duplicate them. Run BeforeDropSql only after the first-Down veto.
/// </summary>
internal static class P37PrincipalGoodsReturnOwnershipSql
{
    internal const string AfterUpSql = """
        ALTER TABLE procurement.principal_receiving_sources
          ADD CONSTRAINT ak_pgret_source_owner UNIQUE
          (tenant_id, legal_entity_id, id, branch_id, purchase_order_id, purchase_order_line_id, issued_version_id);
        ALTER TABLE procurement.principal_receiving_facts
          ADD CONSTRAINT ak_pgret_fact_owner UNIQUE
          (tenant_id, legal_entity_id, id, source_state_id, draft_id, branch_id);
        ALTER TABLE procurement.principal_receiving_drafts
          ADD CONSTRAINT ak_pgret_receipt_owner UNIQUE
          (tenant_id, legal_entity_id, id, branch_id, purchase_order_id, issued_version_id);
        ALTER TABLE procurement.principal_goods_return_drafts
          ADD CONSTRAINT fk_pgret_exact_source FOREIGN KEY
          (tenant_id, legal_entity_id, source_state_id, branch_id, purchase_order_id, purchase_order_line_id, issued_version_id)
          REFERENCES procurement.principal_receiving_sources
          (tenant_id, legal_entity_id, id, branch_id, purchase_order_id, purchase_order_line_id, issued_version_id);
        ALTER TABLE procurement.principal_goods_return_drafts
          ADD CONSTRAINT fk_pgret_exact_fact FOREIGN KEY
          (tenant_id, legal_entity_id, acceptance_fact_id, source_state_id, receipt_id, branch_id)
          REFERENCES procurement.principal_receiving_facts
          (tenant_id, legal_entity_id, id, source_state_id, draft_id, branch_id);
        ALTER TABLE procurement.principal_goods_return_drafts
          ADD CONSTRAINT fk_pgret_exact_receipt FOREIGN KEY
          (tenant_id, legal_entity_id, receipt_id, branch_id, purchase_order_id, issued_version_id)
          REFERENCES procurement.principal_receiving_drafts
          (tenant_id, legal_entity_id, id, branch_id, purchase_order_id, issued_version_id);
        ALTER TABLE procurement.principal_goods_return_revisions
          ADD CONSTRAINT ak_pgret_current_revision UNIQUE
          (tenant_id, legal_entity_id, draft_id, id, revision, state);
        ALTER TABLE procurement.principal_goods_return_drafts
          ADD CONSTRAINT fk_pgret_current_revision FOREIGN KEY
          (tenant_id, legal_entity_id, id, current_revision_id, revision, state)
          REFERENCES procurement.principal_goods_return_revisions
          (tenant_id, legal_entity_id, draft_id, id, revision, state)
          DEFERRABLE INITIALLY DEFERRED;
        """;

    internal const string BeforeDropSql = """
        ALTER TABLE procurement.principal_goods_return_drafts DROP CONSTRAINT fk_pgret_current_revision;
        ALTER TABLE procurement.principal_goods_return_drafts DROP CONSTRAINT fk_pgret_exact_receipt;
        ALTER TABLE procurement.principal_goods_return_drafts DROP CONSTRAINT fk_pgret_exact_fact;
        ALTER TABLE procurement.principal_goods_return_drafts DROP CONSTRAINT fk_pgret_exact_source;
        ALTER TABLE procurement.principal_goods_return_revisions DROP CONSTRAINT ak_pgret_current_revision;
        ALTER TABLE procurement.principal_receiving_drafts DROP CONSTRAINT ak_pgret_receipt_owner;
        ALTER TABLE procurement.principal_receiving_facts DROP CONSTRAINT ak_pgret_fact_owner;
        ALTER TABLE procurement.principal_receiving_sources DROP CONSTRAINT ak_pgret_source_owner;
        """;
}
