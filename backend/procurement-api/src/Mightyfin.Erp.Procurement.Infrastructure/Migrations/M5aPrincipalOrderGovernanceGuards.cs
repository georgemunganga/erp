namespace Mightyfin.Erp.Procurement.Infrastructure.Migrations;

// Lock the evidence and its secondary markers before checking or changing any
// schema. An issued commitment cannot be downgraded into an ordinary draft.
internal static class M5aPrincipalOrderGovernanceGuards
{
    internal const string BeforeDownSql = """
        LOCK TABLE procurement.principal_order_funding_evidence,
          procurement.principal_order_submissions,
          procurement.principal_order_assignments,
          procurement.principal_order_decisions,
          procurement.principal_order_issued_versions,
          procurement.principal_order_acknowledgments,
          procurement.principal_order_release_requests,
          procurement.principal_order_release_confirmations,
          procurement.principal_order_draft_receipts,
          procurement.purchase_orders,
          procurement.spending_authority_identity_evidence,
          procurement.audit_events,
          procurement.outbox_events,
          core_approval.cases IN ACCESS EXCLUSIVE MODE;
        DO $m5a_principal_order_down$
        BEGIN
          IF EXISTS (SELECT 1 FROM procurement.principal_order_funding_evidence)
            OR EXISTS (SELECT 1 FROM procurement.principal_order_submissions)
            OR EXISTS (SELECT 1 FROM procurement.principal_order_assignments)
            OR EXISTS (SELECT 1 FROM procurement.principal_order_decisions)
            OR EXISTS (SELECT 1 FROM procurement.principal_order_issued_versions)
            OR EXISTS (SELECT 1 FROM procurement.principal_order_acknowledgments)
            OR EXISTS (SELECT 1 FROM procurement.principal_order_release_requests)
            OR EXISTS (SELECT 1 FROM procurement.principal_order_release_confirmations)
            OR EXISTS (SELECT 1 FROM procurement.principal_order_draft_receipts
                       WHERE operation NOT IN ('Create','Cancel'))
            OR EXISTS (SELECT 1 FROM procurement.purchase_orders
                       WHERE principal_draft_schema_version=2 AND
                         (current_revision<>0 OR status NOT IN ('Draft','Cancelled')
                          OR approval_instance_ref IS NOT NULL OR submitted_at IS NOT NULL
                          OR issued_at IS NOT NULL))
            OR EXISTS (SELECT 1 FROM procurement.spending_authority_identity_evidence
                       WHERE schema_version=2 AND
                         (action='PurchaseOrder.Approve' OR permission='procurement-po-approve'))
            OR EXISTS (SELECT 1 FROM core_approval.cases c
                       JOIN procurement.purchase_orders o
                         ON o.tenant_id=c."TenantId" AND o.legal_entity_id=c."LegalEntityId"
                           AND o.id=c."SourceId"
                       WHERE c."SourceModule"='procurement' AND c."SourceType"='purchase_order'
                         AND o.principal_draft_schema_version=2)
            OR EXISTS (SELECT 1 FROM procurement.audit_events
                       WHERE record_type='PurchaseOrder' AND action IN
                         ('PrincipalOrderFunding','PrincipalOrderSubmit','PrincipalOrderDecide',
                          'PrincipalOrderIssue','PrincipalOrderAck','PrincipalOrderReleaseRequest',
                          'PrincipalOrderReleaseConfirm'))
            OR EXISTS (SELECT 1 FROM procurement.outbox_events
                       WHERE aggregate_type='PurchaseOrder' AND event_type IN
                         ('purchase_order.manual_commitment_recorded.v2',
                          'purchase_order.approval_opened.v2',
                          'purchase_order.approval_decided.v2',
                          'purchase_order.internal_version_authorized.v2',
                          'purchase_order.supplier_response_observed.v2',
                          'purchase_order.external_release_requested.v2',
                          'purchase_order.external_release_attested.v2'))
          THEN
            RAISE EXCEPTION USING ERRCODE='55000',
              MESSAGE='Retained principal PO governance evidence prevents downgrade; use compatible code or fix forward.';
          END IF;
        END
        $m5a_principal_order_down$;
        """;
}
