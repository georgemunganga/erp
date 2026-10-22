SET LOCAL lock_timeout = '5s';
LOCK TABLE procurement.approval_command_receipts,
    procurement.assignment_authority_evidence,
    procurement.audit_events,
    procurement.budget_check_snapshots,
    procurement.commercial_command_receipts,
    procurement.goods_receipt_lines,
    procurement.goods_receipts,
    procurement.invoice_exceptions,
    procurement.invoice_match_allocations,
    procurement.invoice_match_exposure_states,
    procurement.invoice_match_mapping_versions,
    procurement.invoice_match_results,
    procurement.operational_policy_identity_evidence,
    procurement.outbox_events,
    procurement.policy_versions,
    procurement.procurement_methods,
    procurement.purchase_order_assignment_authority,
    procurement.purchase_order_commitment_ownership,
    procurement.purchase_order_delivery_evidence,
    procurement.purchase_order_finance_evidence,
    procurement.purchase_order_finance_resolutions,
    procurement.purchase_order_issued_versions,
    procurement.purchase_order_lines,
    procurement.purchase_order_submissions,
    procurement.purchase_orders,
    procurement.purchase_request_allocations,
    procurement.purchase_request_approval_outcomes,
    procurement.purchase_request_approval_submissions,
    procurement.purchase_request_finance_release_confirmations,
    procurement.purchase_request_finance_release_requests,
    procurement.purchase_request_lines,
    procurement.purchase_requests,
    procurement.receipt_inspections,
    procurement.receiving_acceptance_effects,
    procurement.receiving_source_states,
    procurement.service_acceptances,
    procurement.sourcing_command_receipts,
    procurement.sourcing_demand_bindings,
    procurement.sourcing_draft_evidence,
    procurement.sourcing_events,
    procurement.sourcing_policy_identity_evidence,
    procurement.spending_authority_categories,
    procurement.spending_authority_grants,
    procurement.spending_authority_identity_evidence,
    procurement.supplier_invoices IN ACCESS EXCLUSIVE MODE;
DO $canonical_guard$
BEGIN
    IF EXISTS (SELECT 1 FROM procurement.approval_command_receipts)
      OR EXISTS (SELECT 1 FROM procurement.commercial_command_receipts)
      OR EXISTS (SELECT 1 FROM procurement.operational_policy_identity_evidence)
      OR EXISTS (SELECT 1 FROM procurement.purchase_order_commitment_ownership)
      OR EXISTS (SELECT 1 FROM procurement.purchase_request_approval_submissions)
      OR EXISTS (SELECT 1 FROM procurement.sourcing_command_receipts)
      OR EXISTS (SELECT 1 FROM procurement.sourcing_draft_evidence)
      OR EXISTS (SELECT 1 FROM procurement.sourcing_policy_identity_evidence)
      OR EXISTS (SELECT 1 FROM procurement.spending_authority_grants)
      OR EXISTS (SELECT 1 FROM procurement.purchase_order_finance_evidence)
      OR EXISTS (SELECT 1 FROM procurement.purchase_request_approval_outcomes)
      OR EXISTS (SELECT 1 FROM procurement.assignment_authority_evidence)
      OR EXISTS (SELECT 1 FROM procurement.spending_authority_categories)
      OR EXISTS (SELECT 1 FROM procurement.spending_authority_identity_evidence)
      OR EXISTS (SELECT 1 FROM procurement.purchase_order_submissions)
      OR EXISTS (SELECT 1 FROM procurement.purchase_request_finance_release_requests)
      OR EXISTS (SELECT 1 FROM procurement.sourcing_demand_bindings)
      OR EXISTS (SELECT 1 FROM procurement.purchase_order_assignment_authority)
      OR EXISTS (SELECT 1 FROM procurement.purchase_order_finance_resolutions)
      OR EXISTS (SELECT 1 FROM procurement.purchase_order_issued_versions)
      OR EXISTS (SELECT 1 FROM procurement.purchase_request_finance_release_confirmations)
      OR EXISTS (SELECT 1 FROM procurement.invoice_match_exposure_states)
      OR EXISTS (SELECT 1 FROM procurement.invoice_match_mapping_versions)
      OR EXISTS (SELECT 1 FROM procurement.purchase_order_delivery_evidence)
      OR EXISTS (SELECT 1 FROM procurement.receiving_source_states)
      OR EXISTS (SELECT 1 FROM procurement.invoice_match_allocations)
      OR EXISTS (SELECT 1 FROM procurement.receiving_acceptance_effects)
      OR EXISTS (SELECT 1 FROM procurement.audit_events WHERE record_type IN ('InvoiceMatchAllocation','InvoiceMatchExposureState','InvoiceMatchMappingVersion','OperationalPolicyIdentityRecord','ProcurementApprovalCommandReceipt','ProcurementCommercialCommandReceipt','ProcurementSpendingAuthorityCategory','ProcurementSpendingAuthorityGrant','PurchaseOrderAssignmentAuthorityEvidence','PurchaseOrderCommitmentOwnership','PurchaseOrderDeliveryEvidence','PurchaseOrderFinanceEvidence','PurchaseOrderFinanceResolution','PurchaseOrderIssuedVersion','PurchaseOrderSubmission','PurchaseRequestApprovalOutcome','PurchaseRequestApprovalSubmission','PurchaseRequestAssignmentAuthorityEvidence','PurchaseRequestFinanceReleaseConfirmation','PurchaseRequestFinanceReleaseRequest','ReceivingAcceptanceEffect','ReceivingSourceState','SourcingCommandReceipt','SourcingDemandBinding','SourcingDraftEvidence','SourcingPolicyIdentityEvidence','SpendingAuthorityIdentityRecord')
        OR (record_type='PurchaseRequest' AND action IN ('ApprovalCaseOpened','ApprovalDecisionRecorded','FinanceReleaseEvidenceRecorded')) OR (record_type='PurchaseOrder' AND action IN ('CommercialApprovalOpened','CommercialApprovalDecision','UnfundedDraftCancelled','UnsubmittedFinanceReleaseConfirmed','FinanceCommitmentConfirmed','FinanceCommitmentReleaseConfirmed','IssueAuthorized','SupplierDeliveryObserved','ApprovedSourcesConverted','SourceDraftUpdated')))
      OR EXISTS (SELECT 1 FROM procurement.outbox_events WHERE event_type IN
        ('purchase_request.approval_opened.v1','purchase_request.approval_decision_recorded.v1',
         'purchase_request.approved.v1','purchase_request.returned.v1','purchase_request.rejected.v1',
         'procurement.finance_release.requested.v1','procurement.finance_release.confirmed.v1','purchase_order.approval_opened.v1','purchase_order.approval_decided.v1','purchase_order.draft_cancelled.v1','purchase_order.unsubmitted_finance_released.v1','purchase_order.finance_release_confirmed.v1','purchase_order.dispatch_requested.v1','purchase_order.delivery_observed.v1','purchase_order.draft_created.v1'))
    THEN RAISE EXCEPTION 'CanonicalGovernedRollbackWouldLoseEvidence: retained governed rows/audit/outbox require a forward fix or protected restore.';
    END IF;
END $canonical_guard$;

-- Retained 20261020114243_P24PurchaseOrderCommercialExecution downgrade invariant.
DO $guard$
BEGIN
    IF EXISTS (SELECT 1 FROM procurement.commercial_command_receipts)
        OR EXISTS (SELECT 1 FROM procurement.purchase_order_assignment_authority)
        OR EXISTS (SELECT 1 FROM procurement.purchase_order_commitment_ownership)
        OR EXISTS (SELECT 1 FROM procurement.purchase_order_delivery_evidence)
        OR EXISTS (SELECT 1 FROM procurement.purchase_order_finance_evidence)
        OR EXISTS (SELECT 1 FROM procurement.purchase_order_finance_resolutions)
        OR EXISTS (SELECT 1 FROM procurement.purchase_order_issued_versions)
        OR EXISTS (SELECT 1 FROM procurement.purchase_order_submissions)
        OR EXISTS (SELECT 1 FROM procurement.purchase_orders
            WHERE return_reason IS NOT NULL OR submitted_at IS NOT NULL
                OR submitted_business_version IS NOT NULL)
        OR EXISTS (SELECT 1 FROM procurement.procurement_methods
            WHERE execution_route <> 'Unconfigured') THEN
        RAISE EXCEPTION 'P24RollbackRequiresEvidencePreservingRecovery: order execution has been used or configured; use a reviewed forward fix or protected restore plan.';
    END IF;
    IF EXISTS (SELECT 1 FROM procurement.purchase_request_allocations
        WHERE estimated_value <> trunc(estimated_value, 4)
            OR abs(estimated_value) >= 100000000000000) THEN
        RAISE EXCEPTION 'P24RollbackAmountWouldLosePrecision: allocation amounts cannot fit numeric(18,4) exactly.';
    END IF;
END $guard$;

-- Retained 20261020133820_P25PositiveReceivingExecution downgrade invariant.
DO $p25_guard$
BEGIN
    IF EXISTS (SELECT 1 FROM procurement.receiving_source_states)
        OR EXISTS (SELECT 1 FROM procurement.receiving_acceptance_effects)
        OR EXISTS (SELECT 1 FROM procurement.goods_receipts WHERE execution_version <> 0
            OR issued_version_id IS NOT NULL OR submission_id IS NOT NULL
            OR issued_binding_id <> '00000000-0000-0000-0000-000000000000'::uuid
            OR branch_ref IS NOT NULL OR receiver_worker_id IS NOT NULL OR recorded_at IS NOT NULL OR notes IS NOT NULL)
        OR EXISTS (SELECT 1 FROM procurement.goods_receipt_lines WHERE execution_version <> 0
            OR receiving_source_state_id IS NOT NULL OR source_purchase_order_id IS NOT NULL
            OR source_state_binding_id <> '00000000-0000-0000-0000-000000000000'::uuid
            OR issued_binding_id <> '00000000-0000-0000-0000-000000000000'::uuid
            OR initially_rejected_quantity <> 0 OR physical_observation_id IS NOT NULL
            OR physical_binding_id <> '00000000-0000-0000-0000-000000000000'::uuid
            OR physical_policy_version_id IS NOT NULL OR physical_evidence_ref IS NOT NULL
            OR physical_sequence IS NOT NULL OR physical_effect_key IS NOT NULL OR physical_recorded_at IS NOT NULL)
        OR EXISTS (SELECT 1 FROM procurement.receipt_inspections WHERE execution_version <> 0
            OR physical_binding_id <> '00000000-0000-0000-0000-000000000000'::uuid
            OR physical_observation_id IS NOT NULL OR receiving_source_state_id IS NOT NULL
            OR source_state_binding_id <> '00000000-0000-0000-0000-000000000000'::uuid
            OR qualified_quantity <> 0 OR inspector_worker_id IS NOT NULL OR policy_version_id IS NOT NULL
            OR evidence_ref IS NOT NULL OR ledger_sequence IS NOT NULL OR effect_key IS NOT NULL)
        OR EXISTS (SELECT 1 FROM procurement.service_acceptances WHERE execution_version <> 0
            OR receiving_source_state_id IS NOT NULL OR source_purchase_order_id IS NOT NULL
            OR source_state_binding_id <> '00000000-0000-0000-0000-000000000000'::uuid
            OR issued_binding_id <> '00000000-0000-0000-0000-000000000000'::uuid
            OR owner_worker_id IS NOT NULL OR policy_version_id IS NOT NULL OR evidence_ref IS NOT NULL
            OR certification_effect_id IS NOT NULL OR current_accepted_quantity <> 0 OR current_accepted_net_amount <> 0 OR notes IS NOT NULL)
        OR EXISTS (SELECT 1 FROM procurement.policy_versions WHERE upper(btrim(policy_key)) = 'RECEIVING')
        OR EXISTS (SELECT 1 FROM procurement.commercial_command_receipts WHERE operation IN
            ('ReceivingDraft','ReceivingRecord','ReceivingInspect','ReceivingAccept','ReceivingCancel','ServiceDraft','ServiceCertify','ServiceCancel'))
        OR EXISTS (SELECT 1 FROM procurement.audit_events WHERE action IN
            ('ReceivingDraft','ReceivingRecord','ReceivingInspect','ReceivingAccept','ReceivingCancel','ServiceDraft','ServiceCertify','ServiceCancel'))
        OR EXISTS (SELECT 1 FROM procurement.audit_events
            WHERE record_type IN ('ReceivingSourceState','ReceivingAcceptanceEffect'))
        OR EXISTS (SELECT 1 FROM procurement.outbox_events WHERE aggregate_type = 'Receiving'
            OR event_type IN ('procurement.goods.recorded.v1','procurement.goods.inspected.v1','procurement.fulfilment.accepted.v1')) THEN
        RAISE EXCEPTION 'P25RollbackRequiresEvidencePreservingRecovery: receiving has been configured or used; use a reviewed forward fix or protected restore, preserving records and evidence.';
    END IF;
    -- P25 widens an EXISTING service amount. Even a v0 owner-written value must not
    -- silently round or overflow on restoration of the prior numeric(18,4) column.
    IF EXISTS (SELECT 1 FROM procurement.service_acceptances
        WHERE accepted_amount <> trunc(accepted_amount, 4) OR abs(accepted_amount) >= 100000000000000) THEN
        RAISE EXCEPTION 'P25RollbackAmountWouldLosePrecision: retained service amount does not fit numeric(18,4) exactly.';
    END IF;
END $p25_guard$;

-- Retained 20261020164745_P26InvoiceMatchingExecution downgrade invariant.
DO $p26_guard$
BEGIN
    IF EXISTS (SELECT 1 FROM procurement.invoice_match_allocations)
       OR EXISTS (SELECT 1 FROM procurement.invoice_match_exposure_states)
       OR EXISTS (SELECT 1 FROM procurement.invoice_match_mapping_versions)
       OR EXISTS (SELECT 1 FROM procurement.supplier_invoices
          WHERE mapping_revision IS DISTINCT FROM 0 OR match_revision IS DISTINCT FROM 0
             OR current_mapping_version_id IS NOT NULL OR last_match_result_id IS NOT NULL
             OR active_match_claim_result_id IS NOT NULL OR matching_state IS DISTINCT FROM 'NotCompared')
       OR EXISTS (SELECT 1 FROM procurement.invoice_match_results
          WHERE execution_version IS DISTINCT FROM 0 OR invoice_version IS DISTINCT FROM 0
             OR mapping_version_id IS NOT NULL OR purchase_order_id IS NOT NULL OR issued_version_id IS NOT NULL
             OR invoice_original_sha256 IS NOT NULL OR actor_principal_id IS NOT NULL OR actor_worker_id IS NOT NULL
             OR command_key IS NOT NULL OR input_fingerprint IS NOT NULL OR evidence_json IS NOT NULL
             OR evidence_sha256 IS NOT NULL OR policy_rules_sha256 IS NOT NULL OR source_watermarks_json IS NOT NULL)
       OR EXISTS (SELECT 1 FROM procurement.invoice_exceptions
          WHERE match_result_id IS NOT NULL OR supplier_invoice_line_id IS NOT NULL OR discrepancy_key IS NOT NULL
             OR owner_worker_id IS NOT NULL OR resolving_match_result_id IS NOT NULL)
       OR EXISTS (SELECT 1 FROM procurement.commercial_command_receipts WHERE operation IN ('InvoiceMapping','InvoiceCompare'))
       OR EXISTS (SELECT 1 FROM procurement.audit_events
          WHERE action IN ('InvoiceMapping','InvoiceCompare')
             OR record_type IN ('InvoiceMatchMappingVersion','InvoiceMatchExposureState','InvoiceMatchAllocation'))
       OR EXISTS (SELECT 1 FROM procurement.outbox_events WHERE event_type = 'supplier_invoice.compared.v1') THEN
        RAISE EXCEPTION 'P26RollbackRequiresEvidencePreservingRecovery: invoice matching has retained evidence; use a reviewed forward fix or protected restore without deleting evidence.';
    END IF;
    IF EXISTS (SELECT 1 FROM procurement.invoice_match_results
       WHERE quantity_variance IS NULL OR price_variance IS NULL OR tax_variance IS NULL) THEN
        RAISE EXCEPTION 'P26RollbackWouldLoseUnknownVariance: an unknown variance cannot be replaced with zero; preserve the nullable schema.';
    END IF;
    IF EXISTS (SELECT 1 FROM procurement.invoice_match_results
       WHERE tax_variance IS NOT NULL AND
          (abs(tax_variance) > 99999999999999.9999 OR tax_variance <> round(tax_variance, 4))) THEN
        RAISE EXCEPTION 'P26RollbackWouldLoseTaxPrecision: retained tax variance cannot fit numeric(18,4) exactly; preserve the expanded schema.';
    END IF;
END $p26_guard$;


-- Retained 20261020190554_P27FinanceCheckerIdentity downgrade invariant.
DO $guard$
BEGIN
    IF EXISTS (SELECT 1 FROM procurement.budget_check_snapshots
        WHERE checker_identity_schema_version <> 0
           OR checked_by_worker_id IS NOT NULL OR checker_identity_sha256 IS NOT NULL)
      OR EXISTS (SELECT 1 FROM procurement.purchase_request_approval_submissions
        WHERE finance_identity_schema_version <> 0)
      OR EXISTS (SELECT 1 FROM procurement.audit_events
        WHERE COALESCE(changed_fields_json::text,'') ~
            '"(CheckerIdentitySchemaVersion|CheckedByWorkerId|CheckerIdentitySha256|FinanceIdentitySchemaVersion|checkerIdentitySchemaVersion|checkedByWorkerId|checkerIdentitySha256|financeIdentitySchemaVersion)"')
      OR EXISTS (SELECT 1 FROM procurement.outbox_events
        WHERE COALESCE(payload_json::text,'') ~
            '"(CheckerIdentitySchemaVersion|CheckedByWorkerId|CheckerIdentitySha256|FinanceIdentitySchemaVersion|checkerIdentitySchemaVersion|checkedByWorkerId|checkerIdentitySha256|financeIdentitySchemaVersion)"')
    THEN
        RAISE EXCEPTION 'FinanceCheckerIdentityRollbackWouldLoseEvidence';
    END IF;
END $guard$;

-- Retained 20261021065049_P28GovernedSourcingDrafts downgrade invariant.
DO $guard$
BEGIN
    IF EXISTS (SELECT 1 FROM procurement.sourcing_events
        WHERE execution_version <> 0 OR branch_ref IS NOT NULL
           OR maker_worker_id IS NOT NULL OR method_id IS NOT NULL
           OR method_revision IS NOT NULL OR source_evidence_id IS NOT NULL
           OR original_draft_fingerprint IS NOT NULL)
      OR EXISTS (SELECT 1 FROM procurement.sourcing_draft_evidence)
      OR EXISTS (SELECT 1 FROM procurement.sourcing_demand_bindings)
      OR EXISTS (SELECT 1 FROM procurement.sourcing_command_receipts)
      OR EXISTS (SELECT 1 FROM procurement.audit_events
        WHERE record_type IN ('SourcingDraftEvidence','SourcingDemandBinding','SourcingCommandReceipt')
           OR (record_type='SourcingEvent' AND action='DraftReserved')
           OR COALESCE(changed_fields_json::text,'') ~
            '"(SourceEvidenceId|OriginalDraftFingerprint|sourceEvidenceId|originalDraftFingerprint)"')
      OR EXISTS (SELECT 1 FROM procurement.outbox_events
        WHERE event_type IN ('sourcing.draft_reserved.v1','sourcing.draft_cancelled.v1')
           OR COALESCE(payload_json::text,'') ~
            '"(SourceEvidenceId|OriginalDraftFingerprint|sourceEvidenceId|originalDraftFingerprint)"')
    THEN
        RAISE EXCEPTION 'GovernedSourcingRollbackWouldLoseEvidence';
    END IF;
END $guard$;


-- Retained 20261021084158_P29SourcingPolicyIdentity downgrade invariant.
DO $guard$
BEGIN
    IF EXISTS (SELECT 1 FROM procurement.sourcing_policy_identity_evidence)
    THEN
        RAISE EXCEPTION 'SourcingPolicyIdentityRollbackWouldLoseEvidence'
            USING ERRCODE = '55000';
    END IF;
END $guard$;

-- Retained 20261021114959_P30SpendingAuthorityWorkerIdentity downgrade invariant.
DO $authority_identity_down$
BEGIN
    IF EXISTS (SELECT 1 FROM procurement.spending_authority_identity_evidence)
        OR EXISTS (SELECT 1 FROM procurement.audit_events
            WHERE record_type = 'SpendingAuthorityIdentityRecord') THEN
        RAISE EXCEPTION USING ERRCODE = '55000',
            MESSAGE = 'Retained spending authority identity prevents destructive downgrade.';
    END IF;
END
$authority_identity_down$;

-- Retained 20261022041534_P31OperationalPolicyWorkerIdentity downgrade invariant.
DO $operational_policy_identity_down$
BEGIN
    IF EXISTS (SELECT 1 FROM procurement.operational_policy_identity_evidence)
        OR EXISTS (SELECT 1 FROM procurement.audit_events
            WHERE record_type = 'OperationalPolicyIdentityRecord')
    THEN
        RAISE EXCEPTION USING ERRCODE = '55000',
            MESSAGE = 'Retained operational policy identity prevents destructive downgrade.';
    END IF;
END
$operational_policy_identity_down$;

-- Drop DB-only overlays before EF removes their supporting indexes/tables.
ALTER TABLE procurement.receipt_inspections DROP CONSTRAINT fk_receiving_inspection_exact_physical;
ALTER TABLE procurement.receiving_acceptance_effects DROP CONSTRAINT fk_receiving_effect_exact_physical;
ALTER TABLE procurement.receiving_acceptance_effects DROP CONSTRAINT fk_receiving_effect_exact_service;
ALTER TABLE procurement.invoice_match_allocations DROP CONSTRAINT fk_invoice_allocation_exact_goods;
ALTER TABLE procurement.invoice_match_allocations DROP CONSTRAINT fk_invoice_allocation_exact_service;
ALTER TABLE procurement.sourcing_events DROP CONSTRAINT fk_rfq_event_original;
ALTER TABLE procurement.sourcing_demand_bindings DROP CONSTRAINT fk_rfq_binding_original;
ALTER TABLE procurement.sourcing_demand_bindings DROP CONSTRAINT fk_rfq_binding_exact_line;
ALTER TABLE procurement.sourcing_demand_bindings DROP CONSTRAINT fk_rfq_binding_exact_hold;
