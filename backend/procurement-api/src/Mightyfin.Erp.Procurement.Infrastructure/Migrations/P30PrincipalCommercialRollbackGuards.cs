namespace Mightyfin.Erp.Procurement.Infrastructure.Migrations;

// First statement of the genuine P30 downgrade, before dropping overlays or columns.
// Preserves new records and principal policy history; never backfills worker identities.
internal static class P30PrincipalCommercialRollbackGuards
{
    internal const string BeforeDownSql = """
        LOCK TABLE procurement.purchase_orders,
          procurement.sourcing_events,
          procurement.supplier_bids,
          procurement.policy_versions,
          procurement.sourcing_policy_identity_evidence,
          procurement.principal_order_acknowledgments,
          procurement.principal_order_assignments,
          procurement.principal_order_decisions,
          procurement.principal_order_draft_receipts,
          procurement.principal_order_funding_evidence,
          procurement.principal_order_issued_versions,
          procurement.principal_order_release_confirmations,
          procurement.principal_order_release_requests,
          procurement.principal_order_source_bindings,
          procurement.principal_order_submissions,
          procurement.principal_receiving_drafts,
          procurement.principal_receiving_facts,
          procurement.principal_receiving_line_bindings,
          procurement.principal_receiving_receipts,
          procurement.principal_receiving_revisions,
          procurement.principal_receiving_sources,
          procurement.sourcing_manual_contacts,
          procurement.sourcing_manual_quote_versions,
          procurement.sourcing_publication_decisions,
          procurement.sourcing_publication_openings,
          procurement.sourcing_publication_receipts IN ACCESS EXCLUSIVE MODE;
        DO $$
        BEGIN
          IF EXISTS (SELECT 1 FROM procurement.principal_order_acknowledgments)
            OR EXISTS (SELECT 1 FROM procurement.principal_order_assignments)
            OR EXISTS (SELECT 1 FROM procurement.principal_order_decisions)
            OR EXISTS (SELECT 1 FROM procurement.principal_order_draft_receipts)
            OR EXISTS (SELECT 1 FROM procurement.principal_order_funding_evidence)
            OR EXISTS (SELECT 1 FROM procurement.principal_order_issued_versions)
            OR EXISTS (SELECT 1 FROM procurement.principal_order_release_confirmations)
            OR EXISTS (SELECT 1 FROM procurement.principal_order_release_requests)
            OR EXISTS (SELECT 1 FROM procurement.principal_order_source_bindings)
            OR EXISTS (SELECT 1 FROM procurement.principal_order_submissions)
            OR EXISTS (SELECT 1 FROM procurement.principal_receiving_drafts)
            OR EXISTS (SELECT 1 FROM procurement.principal_receiving_facts)
            OR EXISTS (SELECT 1 FROM procurement.principal_receiving_line_bindings)
            OR EXISTS (SELECT 1 FROM procurement.principal_receiving_receipts)
            OR EXISTS (SELECT 1 FROM procurement.principal_receiving_revisions)
            OR EXISTS (SELECT 1 FROM procurement.principal_receiving_sources)
            OR EXISTS (SELECT 1 FROM procurement.sourcing_manual_contacts)
            OR EXISTS (SELECT 1 FROM procurement.sourcing_manual_quote_versions)
            OR EXISTS (SELECT 1 FROM procurement.sourcing_publication_decisions)
            OR EXISTS (SELECT 1 FROM procurement.sourcing_publication_openings)
            OR EXISTS (SELECT 1 FROM procurement.sourcing_publication_receipts)
            OR EXISTS (SELECT 1 FROM procurement.purchase_orders WHERE principal_draft_schema_version <> 0 OR original_direct_source_sha256 IS NOT NULL)
            OR EXISTS (SELECT 1 FROM procurement.sourcing_events WHERE execution_version = 2 AND status NOT IN ('Draft','Cancelled'))
            OR EXISTS (SELECT 1 FROM procurement.supplier_bids WHERE draft_revision <> 0)
            OR EXISTS (SELECT 1 FROM procurement.sourcing_policy_identity_evidence WHERE schema_version <> 1 OR actor_worker_id IS NULL)
            OR EXISTS (SELECT 1 FROM procurement.policy_versions WHERE policy_key IN ('RECEIVING','SOURCING') AND rules_json->>'schemaVersion' = '2') THEN
            RAISE EXCEPTION 'P30 principal commercial evidence prevents downgrade' USING ERRCODE = '55000';
          END IF;
        END $$;
        """;
}
