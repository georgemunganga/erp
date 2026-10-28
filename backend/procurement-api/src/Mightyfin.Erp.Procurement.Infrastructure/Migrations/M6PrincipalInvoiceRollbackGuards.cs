namespace Mightyfin.Erp.Procurement.Infrastructure.Migrations;

/// <summary>First-Down veto for the pending genuine M6 migration. This source
/// is not wired into a migration until EF scaffolding and native review.</summary>
internal static class M6PrincipalInvoiceRollbackGuards
{
    internal const string BeforeDownSql = """
        LOCK TABLE procurement.invoice_owner_baselines,
          procurement.invoice_owner_baseline_rows,
          procurement.invoice_match_mapping_versions,
          procurement.invoice_match_results,
          procurement.invoice_match_allocations,
          procurement.invoice_unit_review_sources,
          procurement.invoice_unit_review_receipts,
          procurement.audit_events,
          procurement.outbox_events,
          core_approval.cases IN ACCESS EXCLUSIVE MODE;
        DO $m6_principal_invoice_down$
        BEGIN
          IF EXISTS (SELECT 1 FROM procurement.invoice_owner_baselines)
            OR EXISTS (SELECT 1 FROM procurement.invoice_owner_baseline_rows)
            OR EXISTS (SELECT 1 FROM procurement.invoice_match_mapping_versions
                       WHERE source_schema_version=3)
            OR EXISTS (SELECT 1 FROM procurement.invoice_match_results r
                       JOIN procurement.invoice_match_mapping_versions m
                         ON m.tenant_id=r.tenant_id AND m.legal_entity_id=r.legal_entity_id
                           AND m.id=r.mapping_version_id
                       WHERE m.source_schema_version=3)
            OR EXISTS (SELECT 1 FROM procurement.invoice_match_allocations a
                       JOIN procurement.invoice_match_results r
                         ON r.tenant_id=a.tenant_id AND r.legal_entity_id=a.legal_entity_id
                           AND r.id=a.match_result_id
                       JOIN procurement.invoice_match_mapping_versions m
                         ON m.tenant_id=r.tenant_id AND m.legal_entity_id=r.legal_entity_id
                           AND m.id=r.mapping_version_id
                       WHERE m.source_schema_version=3)
            OR EXISTS (SELECT 1 FROM procurement.invoice_unit_review_sources s
                       JOIN procurement.invoice_match_mapping_versions m
                         ON m.tenant_id=s.tenant_id AND m.legal_entity_id=s.legal_entity_id
                           AND m.id=s.mapping_version_id
                       WHERE m.source_schema_version=3)
            OR EXISTS (SELECT 1 FROM procurement.invoice_unit_review_receipts receipt
                       JOIN procurement.invoice_unit_review_sources s
                         ON s.tenant_id=receipt.tenant_id
                           AND s.legal_entity_id=receipt.legal_entity_id
                           AND s.id=receipt.review_source_id
                       JOIN procurement.invoice_match_mapping_versions m
                         ON m.tenant_id=s.tenant_id AND m.legal_entity_id=s.legal_entity_id
                           AND m.id=s.mapping_version_id
                       WHERE m.source_schema_version=3)
            OR EXISTS (SELECT 1 FROM core_approval.cases
                       WHERE "SourceModule"='procurement'
                         AND "SourceType"='InvoiceProcurementOwnerBaseline')
            OR EXISTS (SELECT 1 FROM procurement.audit_events
                       WHERE record_type IN ('InvoiceProcurementOwnerBaseline',
                         'InvoiceProcurementOwnerBaselineRow'))
            OR EXISTS (SELECT 1 FROM procurement.outbox_events
                       WHERE aggregate_type='InvoiceProcurementOwnerBaseline')
          THEN
            RAISE EXCEPTION USING ERRCODE='55000',
              MESSAGE='Retained principal invoice owner or source evidence prevents downgrade; use compatible code or fix forward.';
          END IF;
        END
        $m6_principal_invoice_down$;
        """;
}
