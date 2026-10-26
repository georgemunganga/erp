namespace Mightyfin.Erp.Procurement.Infrastructure.Migrations;

// Called first by M3b Down. The migration transaction retains these locks until
// the old schema is restored. A populated direct draft needs compatible code or
// a reviewed forward repair; an image rollback cannot discard its source proof.
internal static class M3bPrincipalOrderDraftGuards
{
    internal const string BeforeDownSql = """
        LOCK TABLE procurement.purchase_orders,
          procurement.purchase_order_lines,
          procurement.purchase_request_allocations,
          procurement.principal_order_source_bindings,
          procurement.principal_order_draft_receipts,
          procurement.audit_events,
          procurement.outbox_events
          IN ACCESS EXCLUSIVE MODE;
        DO $m3b_principal_order_down$
        BEGIN
          IF EXISTS (SELECT 1 FROM procurement.purchase_orders
                     WHERE principal_draft_schema_version = 2
                        OR original_direct_source_sha256 IS NOT NULL)
            OR EXISTS (SELECT 1 FROM procurement.principal_order_source_bindings)
            OR EXISTS (SELECT 1 FROM procurement.principal_order_draft_receipts)
            OR EXISTS (
                SELECT 1 FROM procurement.purchase_request_allocations a
                JOIN procurement.purchase_orders o
                  ON o.tenant_id = a.tenant_id
                 AND o.legal_entity_id = a.legal_entity_id
                 AND o.id = a.target_id
                WHERE a.target_type = 'PurchaseOrder'
                  AND o.principal_draft_schema_version = 2)
            OR EXISTS (SELECT 1 FROM procurement.audit_events
                       WHERE record_type = 'PurchaseOrder'
                         AND action IN ('PrincipalDraftCreate', 'PrincipalDraftCancel'))
            OR EXISTS (SELECT 1 FROM procurement.outbox_events
                       WHERE aggregate_type = 'PurchaseOrder'
                         AND event_type IN (
                           'purchase_order.internal_draft_reserved.v2',
                           'purchase_order.internal_draft_cancelled.v2'))
          THEN
            RAISE EXCEPTION USING ERRCODE = '55000',
              MESSAGE = 'Principal direct purchase order draft evidence prevents downgrade; use compatible code or fix forward.';
          END IF;
        END
        $m3b_principal_order_down$;
        """;
}
