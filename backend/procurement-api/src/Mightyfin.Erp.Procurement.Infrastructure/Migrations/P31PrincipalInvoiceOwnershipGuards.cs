namespace Mightyfin.Erp.Procurement.Infrastructure.Migrations;

/// <summary>
/// Authored retention guard for the genuine P31 EF migration. The thirteen
/// owned tables and their exact FKs are generated from the reviewed model.
/// The owner graph is acyclic: no extra deferred FK overlay is justified
/// without an actual mapped PostgreSQL save-order failure.
/// </summary>
internal static class P31PrincipalInvoiceOwnershipGuards
{
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
