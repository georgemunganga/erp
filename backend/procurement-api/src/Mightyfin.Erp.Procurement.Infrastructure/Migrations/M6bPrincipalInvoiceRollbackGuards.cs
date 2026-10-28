namespace Mightyfin.Erp.Procurement.Infrastructure.Migrations;

/// <summary>Must be the FIRST Down operation of the genuine
/// 20261028184527_M6bPrincipalInvoiceReceivingBindings migration. Protects retained
/// principal invoice bindings; it never deletes evidence, resets counters or
/// admits an owner. An M6A baseline/header/policy alone is not M6B evidence.</summary>
internal static class M6bPrincipalInvoiceRollbackGuards
{
    internal const string BeforeDownSql = """
        LOCK TABLE procurement.principal_receiving_sources,
          procurement.principal_receiving_facts,
          procurement.invoice_match_allocations,
          procurement.invoice_match_mapping_versions,
          procurement.invoice_match_results,
          procurement.invoice_unit_review_sources,
          procurement.invoice_unit_review_assignments,
          procurement.invoice_unit_review_outcomes,
          procurement.invoice_unit_review_receipts,
          procurement.commercial_command_receipts,
          procurement.audit_events,
          procurement.outbox_events,
          core_approval.cases IN ACCESS EXCLUSIVE MODE;
        DO $m6b_principal_invoice_down$
        BEGIN
          IF EXISTS (SELECT 1 FROM procurement.invoice_match_allocations
                     WHERE principal_receiving_source_state_id IS NOT NULL
                        OR principal_receiving_fact_id IS NOT NULL)
            OR EXISTS (SELECT 1 FROM procurement.invoice_match_mapping_versions
                       WHERE source_schema_version=3)
            OR EXISTS (SELECT 1 FROM procurement.invoice_match_results r
                       JOIN procurement.invoice_match_mapping_versions m
                         ON m.tenant_id=r.tenant_id AND m.legal_entity_id=r.legal_entity_id
                           AND m.id=r.mapping_version_id
                       WHERE m.source_schema_version=3)
            OR EXISTS (SELECT 1 FROM procurement.invoice_unit_review_sources s
                       JOIN procurement.invoice_match_mapping_versions m
                         ON m.tenant_id=s.tenant_id AND m.legal_entity_id=s.legal_entity_id
                           AND m.id=s.mapping_version_id
                       WHERE m.source_schema_version=3)
            OR EXISTS (SELECT 1 FROM procurement.invoice_unit_review_assignments a
                       JOIN procurement.invoice_unit_review_sources s
                         ON s.tenant_id=a.tenant_id AND s.legal_entity_id=a.legal_entity_id
                           AND s.id=a.review_source_id
                       JOIN procurement.invoice_match_mapping_versions m
                         ON m.tenant_id=s.tenant_id AND m.legal_entity_id=s.legal_entity_id
                           AND m.id=s.mapping_version_id
                       WHERE m.source_schema_version=3)
            OR EXISTS (SELECT 1 FROM procurement.invoice_unit_review_outcomes o
                       JOIN procurement.invoice_unit_review_sources s
                         ON s.tenant_id=o.tenant_id AND s.legal_entity_id=o.legal_entity_id
                           AND s.id=o.review_source_id
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
            OR EXISTS (SELECT 1 FROM core_approval.cases c
                       JOIN procurement.invoice_unit_review_sources s
                         ON s.tenant_id=c."TenantId" AND s.legal_entity_id=c."LegalEntityId"
                           AND s.id=c."SourceId" AND s.core_case_id=c."Id"
                       JOIN procurement.invoice_match_mapping_versions m
                         ON m.tenant_id=s.tenant_id AND m.legal_entity_id=s.legal_entity_id
                           AND m.id=s.mapping_version_id
                       WHERE c."SourceModule"='procurement'
                         AND c."SourceType"='invoice_unit_review'
                         AND m.source_schema_version=3)
            OR EXISTS (SELECT 1 FROM procurement.audit_events
                       WHERE action IN ('PrincipalInvoiceMappingProposed','PrincipalInvoiceCompared'))
            OR EXISTS (SELECT 1 FROM procurement.audit_events a
                       JOIN procurement.invoice_match_mapping_versions m
                         ON m.tenant_id=a.tenant_id AND m.legal_entity_id=a.legal_entity_id
                           AND ((a.record_type='InvoiceMatchMappingVersion' AND a.record_id=m.id)
                             OR (a.record_type='SupplierInvoice' AND a.record_id=m.supplier_invoice_id))
                       WHERE m.source_schema_version=3)
            OR EXISTS (SELECT 1 FROM procurement.audit_events a
                       JOIN procurement.invoice_match_results r
                         ON r.tenant_id=a.tenant_id AND r.legal_entity_id=a.legal_entity_id
                           AND a.record_type='InvoiceMatchResult' AND a.record_id=r.id
                       JOIN procurement.invoice_match_mapping_versions m
                         ON m.tenant_id=r.tenant_id AND m.legal_entity_id=r.legal_entity_id
                           AND m.id=r.mapping_version_id
                       WHERE m.source_schema_version=3)
            OR EXISTS (SELECT 1 FROM procurement.outbox_events
                       WHERE event_type IN ('supplier_invoice.mapping_proposed.v3',
                         'supplier_invoice.compared.v3'))
            OR EXISTS (SELECT 1 FROM procurement.outbox_events e
                       JOIN procurement.invoice_match_mapping_versions m
                         ON m.tenant_id=e.tenant_id AND m.legal_entity_id=e.legal_entity_id
                           AND e.aggregate_type='SupplierInvoice'
                           AND e.aggregate_id=m.supplier_invoice_id
                       WHERE m.source_schema_version=3)
          THEN
            RAISE EXCEPTION USING ERRCODE='55000',
              MESSAGE='Retained principal invoice receiving bindings or source evidence prevent downgrade; use compatible code or fix forward.';
          END IF;
        END
        $m6b_principal_invoice_down$;
        """;
}
