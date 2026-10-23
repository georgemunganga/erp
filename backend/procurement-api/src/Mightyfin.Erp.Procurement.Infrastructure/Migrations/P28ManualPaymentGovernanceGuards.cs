using Microsoft.EntityFrameworkCore.Migrations;

namespace Mightyfin.Erp.Procurement.Infrastructure.Migrations;

// Separate reviewed integrity SQL from EF's generated owning schema operations.
public partial class P28ManualPaymentGovernance
{
    private static void InstallGovernanceGuards(MigrationBuilder migrationBuilder) => migrationBuilder.Sql("""
        -- Source-module integrity checks. Core owns the referenced workflow records.
        CREATE TRIGGER manual_payment_case_append_only BEFORE UPDATE OR DELETE ON procurement.manual_payment_workflow_cases
         FOR EACH ROW EXECUTE FUNCTION procurement.reject_manual_payment_evidence_mutation();
        CREATE TRIGGER manual_payment_core_decision_append_only BEFORE UPDATE OR DELETE ON procurement.manual_payment_core_decisions
         FOR EACH ROW EXECUTE FUNCTION procurement.reject_manual_payment_evidence_mutation();
        CREATE TRIGGER manual_payment_date_append_only BEFORE UPDATE OR DELETE ON procurement.manual_payment_date_observations
         FOR EACH ROW EXECUTE FUNCTION procurement.reject_manual_payment_evidence_mutation();
        CREATE FUNCTION procurement.guard_manual_payment_title() RETURNS trigger LANGUAGE plpgsql AS $$
        BEGIN
         IF OLD.status <> 'Draft' AND NEW.title IS DISTINCT FROM OLD.title THEN
          RAISE EXCEPTION 'Submitted manual payment title is immutable' USING ERRCODE='23514';
         END IF;
         RETURN NEW;
        END; $$;
        CREATE TRIGGER manual_payment_title_immutable BEFORE UPDATE ON procurement.manual_payment_requests
         FOR EACH ROW EXECUTE FUNCTION procurement.guard_manual_payment_title();

        CREATE FUNCTION procurement.require_manual_payment_governance_companions() RETURNS trigger LANGUAGE plpgsql AS $$
        BEGIN
         IF NEW.action IN ('Submit','Record') AND NOT EXISTS (
          SELECT 1 FROM procurement.manual_payment_workflow_cases c
          WHERE c.tenant_id=NEW.tenant_id AND c.legal_entity_id=NEW.legal_entity_id
           AND c.opening_manual_decision_id=NEW.id AND c.manual_payment_request_id=NEW.manual_payment_request_id
           AND c.branch_id=NEW.branch_id AND c.opened_request_version=NEW.to_version
           AND c.purpose=CASE NEW.action WHEN 'Submit' THEN 'Approval' ELSE 'Confirmation' END) THEN
          RAISE EXCEPTION 'Manual payment transition requires its Core case link' USING ERRCODE='23514';
         END IF;
         IF (NEW.action IN ('Approve','Return','Reject','Confirm','Dispute') OR NEW.action='Cancel' AND NEW.from_status='PendingApproval')
         AND NOT EXISTS (
          SELECT 1 FROM procurement.manual_payment_core_decisions d
          WHERE d.tenant_id=NEW.tenant_id AND d.legal_entity_id=NEW.legal_entity_id
           AND d.manual_payment_decision_id=NEW.id AND d.manual_payment_request_id=NEW.manual_payment_request_id
           AND d.branch_id=NEW.branch_id AND d.manual_action=NEW.action
           AND d.from_request_version=NEW.from_version AND d.to_request_version=NEW.to_version) THEN
          RAISE EXCEPTION 'Manual payment transition requires its Core decision link' USING ERRCODE='23514';
         END IF;
         IF NEW.action IN ('Record','Confirm') AND NOT EXISTS (
          SELECT 1 FROM procurement.manual_payment_date_observations d
          WHERE d.tenant_id=NEW.tenant_id AND d.legal_entity_id=NEW.legal_entity_id
           AND d.manual_payment_decision_id=NEW.id AND d.manual_payment_request_id=NEW.manual_payment_request_id
           AND d.branch_id=NEW.branch_id AND d.request_version=NEW.to_version AND d.action=NEW.action) THEN
          RAISE EXCEPTION 'Manual payment transition requires a company date observation' USING ERRCODE='23514';
         END IF;
         RETURN NULL;
        END; $$;
        CREATE CONSTRAINT TRIGGER manual_payment_governance_bundle AFTER INSERT ON procurement.manual_payment_decisions
         DEFERRABLE INITIALLY DEFERRED FOR EACH ROW EXECUTE FUNCTION procurement.require_manual_payment_governance_companions();

        -- This encoder covers only the fixed ASCII v1 payment policy schema. The application
        -- owns generic canonical JSON; this database check cannot canonicalize other policies.
        CREATE FUNCTION procurement.manual_payment_policy_digest(doc jsonb) RETURNS text LANGUAGE plpgsql IMMUTABLE AS $$
        DECLARE item record; entries text; canonical text;
        BEGIN
         IF jsonb_typeof(doc) IS DISTINCT FROM 'object' OR (SELECT count(*) FROM jsonb_object_keys(doc))<>8
          OR NOT doc @> '{"schemaVersion":1,"identitySchemaVersion":2,"policyKey":"MANUAL_PAYMENT_REVIEW","transactionType":"ManualPaymentReview","approvalType":"SingleApprover","independentCreatorApprover":true,"independentRecorderConfirmer":true}'::jsonb
          OR jsonb_typeof(doc->'maximumByCurrency') IS DISTINCT FROM 'object' THEN RETURN NULL; END IF;
         IF (SELECT count(*) FROM jsonb_object_keys(doc->'maximumByCurrency')) NOT BETWEEN 1 AND 32 THEN RETURN NULL; END IF;
         FOR item IN SELECT key,value FROM jsonb_each(doc->'maximumByCurrency') LOOP
          IF item.key !~ '\A[A-Z]{3}\Z' OR jsonb_typeof(item.value) IS DISTINCT FROM 'string'
           OR (item.value#>>'{}') !~ '\A(0|[1-9][0-9]{0,13})(\.[0-9]{1,4})?\Z' THEN RETURN NULL; END IF;
          IF (item.value#>>'{}')::numeric<=0 THEN RETURN NULL; END IF;
         END LOOP;
         SELECT string_agg(to_jsonb(key)::text||':'||value::text,',' ORDER BY key COLLATE "C") INTO entries FROM jsonb_each(doc->'maximumByCurrency');
         canonical:='{"approvalType":"SingleApprover","identitySchemaVersion":2,"independentCreatorApprover":true,"independentRecorderConfirmer":true,"maximumByCurrency":{'||entries||'},"policyKey":"MANUAL_PAYMENT_REVIEW","schemaVersion":1,"transactionType":"ManualPaymentReview"}';
         RETURN encode(sha256(convert_to(canonical,'UTF8')),'hex');
        END; $$;

        CREATE FUNCTION procurement.require_manual_payment_core_case() RETURNS trigger LANGUAGE plpgsql AS $$
        BEGIN
         IF NOT EXISTS (
          SELECT 1 FROM procurement.manual_payment_requests p
          JOIN procurement.manual_payment_decisions d ON d.tenant_id=p.tenant_id AND d.legal_entity_id=p.legal_entity_id
           AND d.manual_payment_request_id=p.id AND d.branch_id=p.branch_id
          JOIN procurement.supplier_invoices i ON i.tenant_id=p.tenant_id AND i.legal_entity_id=p.legal_entity_id AND i.id=p.supplier_invoice_id
          JOIN procurement.policy_versions v ON v.tenant_id=p.tenant_id AND v.legal_entity_id=p.legal_entity_id AND v.id=NEW.policy_version_id
          JOIN core_approval.cases c ON c."TenantId"=p.tenant_id AND c."LegalEntityId"=p.legal_entity_id AND c."Id"=NEW.core_approval_case_id
          JOIN core_approval.stages s ON s."TenantId"=c."TenantId" AND s."LegalEntityId"=c."LegalEntityId" AND s."CaseId"=c."Id" AND s."Id"=NEW.core_approval_stage_id
          JOIN core_approval.assignments a ON a."TenantId"=s."TenantId" AND a."LegalEntityId"=s."LegalEntityId" AND a."StageId"=s."Id" AND a."Id"=NEW.core_approval_assignment_id
          JOIN procurement.operational_policy_identity_evidence pub ON pub.tenant_id=v.tenant_id AND pub.legal_entity_id=v.legal_entity_id
           AND pub.policy_version_id=v.id AND pub.stage='Published'
          JOIN procurement.operational_policy_identity_evidence sub ON sub.tenant_id=v.tenant_id AND sub.legal_entity_id=v.legal_entity_id
           AND sub.id=pub.submitted_evidence_id AND sub.policy_version_id=v.id AND sub.stage='Submitted'
          JOIN procurement.operational_policy_identity_evidence origin ON origin.tenant_id=v.tenant_id AND origin.legal_entity_id=v.legal_entity_id
           AND origin.id=pub.created_evidence_id AND origin.policy_version_id=v.id AND origin.stage='Created'
          WHERE p.tenant_id=NEW.tenant_id AND p.legal_entity_id=NEW.legal_entity_id AND p.id=NEW.manual_payment_request_id AND p.branch_id=NEW.branch_id
           AND d.id=NEW.opening_manual_decision_id AND d.to_version=NEW.opened_request_version
           AND d.action=CASE NEW.purpose WHEN 'Approval' THEN 'Submit' ELSE 'Record' END
           AND d.actor_principal_id=NEW.maker_principal_id AND d.actor_core_linked_worker_id IS NOT DISTINCT FROM NEW.maker_core_linked_worker_id
           AND d.occurred_at=NEW.opened_at AND NEW.created_at=NEW.opened_at AND NEW.created_by=d.actor_principal_id
           AND i.version=NEW.invoice_version_at_opening AND i.gross_amount=NEW.invoice_gross_amount_at_opening
           AND c."SourceModule"='procurement' AND c."SourceType"=CASE NEW.purpose WHEN 'Approval' THEN 'manual_payment_request' ELSE 'manual_payment_confirmation' END
           AND c."SourceId"=p.id AND c."SourceVersion"=NEW.opened_request_version AND c."MakerSubjectId"=NEW.maker_principal_id
           AND c."State"='Pending' AND s."State"='Active' AND a."State"='Pending'
           AND c."PolicyVersionId"=NEW.policy_version_id AND c."PolicyVersionNumber"=NEW.policy_version_number AND c."OpenedAt"=NEW.opened_at
           AND s."Sequence"=1 AND s."Code"=NEW.purpose AND s."Mode"='All' AND s."RequiredApprovals"=1
           AND (SELECT count(*) FROM core_approval.stages x WHERE x."TenantId"=c."TenantId" AND x."LegalEntityId"=c."LegalEntityId" AND x."CaseId"=c."Id")=1
           AND (SELECT count(*) FROM core_approval.assignments x WHERE x."TenantId"=c."TenantId" AND x."LegalEntityId"=c."LegalEntityId" AND x."CaseId"=c."Id")=1
           AND a."CaseId"=c."Id" AND a."AssigneeSubjectId"=NEW.assignee_principal_id AND a."DelegatedFromSubjectId" IS NULL
           AND a."PermissionKey"=CASE NEW.purpose WHEN 'Approval' THEN 'procurement-payment-approve' ELSE 'procurement-payment-confirm' END
           AND ((NEW.purpose='Approval' AND NEW.maker_principal_id=p.creator_principal_id AND NEW.assignee_principal_id=p.approver_principal_id
              AND NEW.maker_core_linked_worker_id IS NOT DISTINCT FROM p.creator_core_linked_worker_id
              AND NEW.assignee_core_linked_worker_id IS NOT DISTINCT FROM p.approver_core_linked_worker_id
              AND v.state='Published' AND v.effective_from<=NEW.opened_at AND (v.effective_to IS NULL OR v.effective_to>NEW.opened_at))
            OR (NEW.purpose='Confirmation' AND NEW.maker_principal_id=p.recorded_by_principal_id AND NEW.assignee_principal_id=p.confirmer_principal_id
              AND NEW.maker_core_linked_worker_id IS NOT DISTINCT FROM p.recorded_by_core_linked_worker_id
              AND NEW.assignee_core_linked_worker_id IS NOT DISTINCT FROM p.confirmer_core_linked_worker_id AND v.state IN ('Published','Superseded')
              AND EXISTS (SELECT 1 FROM procurement.manual_payment_workflow_cases oldcase
               JOIN procurement.manual_payment_core_decisions olddecision ON olddecision.tenant_id=oldcase.tenant_id AND olddecision.legal_entity_id=oldcase.legal_entity_id
                AND olddecision.workflow_case_link_id=oldcase.id AND olddecision.manual_action='Approve'
               WHERE oldcase.tenant_id=p.tenant_id AND oldcase.legal_entity_id=p.legal_entity_id AND oldcase.manual_payment_request_id=p.id
                AND oldcase.purpose='Approval' AND oldcase.policy_version_id=v.id AND oldcase.opened_request_version=d.from_version-1
                AND olddecision.to_request_version=d.from_version AND olddecision.actor_principal_id=p.approved_by_principal_id)))
           AND procurement.manual_payment_policy_digest(v.rules_json::jsonb)=NEW.policy_rules_sha256
           AND v.policy_key='MANUAL_PAYMENT_REVIEW' AND v.policy_version_number=NEW.policy_version_number AND v.published_at<=NEW.opened_at
           AND v.rules_json::jsonb->>'schemaVersion'='1' AND v.rules_json::jsonb->>'identitySchemaVersion'='2'
           AND v.rules_json::jsonb->>'policyKey'='MANUAL_PAYMENT_REVIEW' AND v.rules_json::jsonb->>'transactionType'='ManualPaymentReview'
           AND v.rules_json::jsonb->>'approvalType'='SingleApprover'
           AND v.rules_json::jsonb->>'independentCreatorApprover'='true' AND v.rules_json::jsonb->>'independentRecorderConfirmer'='true'
           AND (v.rules_json::jsonb->'maximumByCurrency'->>p.currency_code)::numeric >= NEW.invoice_gross_amount_at_opening
           AND pub.schema_version=2 AND sub.schema_version=2 AND origin.schema_version=2
           AND pub.rules_sha256=NEW.policy_rules_sha256 AND sub.rules_sha256=NEW.policy_rules_sha256
           AND pub.actor_principal_id=v.published_by AND pub.action_at=v.published_at
           AND origin.actor_principal_id=v.created_by AND origin.action_at=v.created_at
           AND sub.actor_principal_id=origin.actor_principal_id AND sub.action_at=v.review_requested_at AND sub.actor_principal_id=v.review_requested_by
           AND sub.created_evidence_id=origin.id AND pub.actor_principal_id<>origin.actor_principal_id
           AND (pub.actor_worker_id IS NULL OR origin.actor_worker_id IS NULL OR pub.actor_worker_id<>origin.actor_worker_id)
           AND (pub.actor_worker_id IS NULL OR sub.actor_worker_id IS NULL OR pub.actor_worker_id<>sub.actor_worker_id)
           AND sub.actor_worker_id IS NOT DISTINCT FROM origin.actor_worker_id
           AND pub.policy_record_version_at_action=sub.policy_record_version_at_action+1
           AND sub.policy_record_version_at_action>origin.policy_record_version_at_action
           AND v.version>=pub.policy_record_version_at_action AND pub.action_at>=sub.action_at AND sub.action_at>=origin.action_at
           AND (SELECT count(*)=count(DISTINCT key) FROM json_each(NEW.snapshot_json::json))
           AND NEW.snapshot_sha256=encode(sha256(convert_to(NEW.snapshot_json,'UTF8')),'hex')
           AND NEW.snapshot_json::jsonb = jsonb_build_object('schemaVersion',1,'Id',p.id,'TenantId',p.tenant_id,'LegalEntityId',p.legal_entity_id,
            'BranchId',p.branch_id,'SupplierInvoiceId',p.supplier_invoice_id,'SupplierId',p.supplier_id,'CurrencyCode',p.currency_code,
            'amount',trim_scale(p.amount)::text,'requestPurpose',p.purpose,'Title',p.title,
            'CreatorPrincipalId',p.creator_principal_id,'CreatorCoreLinkedWorkerId',p.creator_core_linked_worker_id,
            'ApproverPrincipalId',p.approver_principal_id,'ApproverCoreLinkedWorkerId',p.approver_core_linked_worker_id,
            'RecorderPrincipalId',p.recorder_principal_id,'RecorderCoreLinkedWorkerId',p.recorder_core_linked_worker_id,
            'ConfirmerPrincipalId',p.confirmer_principal_id,'ConfirmerCoreLinkedWorkerId',p.confirmer_core_linked_worker_id,
            'casePurpose',NEW.purpose,'OpenedRequestVersion',NEW.opened_request_version,'InvoiceVersionAtOpening',NEW.invoice_version_at_opening,
            'invoiceGross',trim_scale(NEW.invoice_gross_amount_at_opening)::text,'PolicyVersionId',NEW.policy_version_id,
            'PolicyVersionNumber',NEW.policy_version_number,'PolicyRulesSha256',NEW.policy_rules_sha256,
            'MakerPrincipalId',NEW.maker_principal_id,'MakerCoreLinkedWorkerId',NEW.maker_core_linked_worker_id,
            'AssigneePrincipalId',NEW.assignee_principal_id,'AssigneeCoreLinkedWorkerId',NEW.assignee_core_linked_worker_id)
         ) THEN
          RAISE EXCEPTION 'Manual payment requires exact Core case, source and published policy evidence' USING ERRCODE='23514';
         END IF;
         RETURN NULL;
        END; $$;
        CREATE CONSTRAINT TRIGGER manual_payment_core_case_bundle AFTER INSERT ON procurement.manual_payment_workflow_cases
         DEFERRABLE INITIALLY DEFERRED FOR EACH ROW EXECUTE FUNCTION procurement.require_manual_payment_core_case();

        CREATE FUNCTION procurement.require_manual_payment_core_decision() RETURNS trigger LANGUAGE plpgsql AS $$
        DECLARE expected_action text; expected_state text;
        BEGIN
         expected_action:=CASE NEW.manual_action WHEN 'Approve' THEN 'Approve' WHEN 'Confirm' THEN 'Approve' WHEN 'Return' THEN 'Return'
          WHEN 'Cancel' THEN 'Cancel' ELSE 'Reject' END;
         expected_state:=CASE expected_action WHEN 'Approve' THEN 'Approved' WHEN 'Return' THEN 'Returned' WHEN 'Cancel' THEN 'Cancelled' ELSE 'Rejected' END;
         IF NOT EXISTS (
          SELECT 1 FROM procurement.manual_payment_workflow_cases link
          JOIN procurement.manual_payment_decisions d ON d.tenant_id=link.tenant_id AND d.legal_entity_id=link.legal_entity_id
           AND d.manual_payment_request_id=link.manual_payment_request_id AND d.id=NEW.manual_payment_decision_id
          JOIN procurement.manual_payment_receipts r ON r.tenant_id=d.tenant_id AND r.legal_entity_id=d.legal_entity_id
           AND r.manual_payment_request_id=d.manual_payment_request_id AND r.result_version=d.to_version AND r.actor_principal_id=d.actor_principal_id AND r.action=d.action
          JOIN core_approval.decisions c ON c."TenantId"=link.tenant_id AND c."LegalEntityId"=link.legal_entity_id AND c."Id"=NEW.core_approval_decision_id
          JOIN core_approval.cases parent ON parent."TenantId"=c."TenantId" AND parent."LegalEntityId"=c."LegalEntityId" AND parent."Id"=c."CaseId"
          JOIN core_approval.stages stage ON stage."TenantId"=c."TenantId" AND stage."LegalEntityId"=c."LegalEntityId" AND stage."Id"=c."StageId"
          JOIN core_approval.assignments assignment ON assignment."TenantId"=c."TenantId" AND assignment."LegalEntityId"=c."LegalEntityId" AND assignment."Id"=c."AssignmentId"
          WHERE link.tenant_id=NEW.tenant_id AND link.legal_entity_id=NEW.legal_entity_id AND link.id=NEW.workflow_case_link_id
           AND link.manual_payment_request_id=NEW.manual_payment_request_id AND link.branch_id=NEW.branch_id AND d.branch_id=NEW.branch_id
           AND d.action=NEW.manual_action AND d.actor_principal_id=NEW.actor_principal_id AND d.actor_core_linked_worker_id IS NOT DISTINCT FROM NEW.actor_core_linked_worker_id
           AND d.from_version=NEW.from_request_version AND d.to_version=NEW.to_request_version AND d.occurred_at=NEW.decided_at
           AND NEW.created_at=NEW.decided_at AND NEW.created_by=NEW.actor_principal_id
           AND link.opened_request_version=d.from_version AND d.to_version=d.from_version+1
           AND ((link.purpose='Approval' AND d.action IN ('Approve','Return','Reject','Cancel')) OR (link.purpose='Confirmation' AND d.action IN ('Confirm','Dispute')))
           AND c."CaseId"=link.core_approval_case_id AND c."StageId"=link.core_approval_stage_id AND c."AssignmentId"=link.core_approval_assignment_id
           AND c."Action"=expected_action AND c."ActorSubjectId"=NEW.actor_principal_id AND c."DecidedAt"=NEW.decided_at
           AND c."IdempotencyKey"=r.command_key::text
           AND (d.action IN ('Approve','Confirm') OR c."Reason" IS NOT DISTINCT FROM d.reason)
           AND (NEW.actor_principal_id=link.assignee_principal_id OR d.action='Cancel' AND NEW.actor_principal_id=link.maker_principal_id)
           AND parent."State"=expected_state AND stage."State"=expected_state AND assignment."State"=expected_state
           AND parent."CompletedAt"=NEW.decided_at
           AND stage."CaseId"=parent."Id" AND assignment."CaseId"=parent."Id" AND assignment."StageId"=stage."Id"
         ) THEN
          RAISE EXCEPTION 'Manual payment requires the exact same Core decision' USING ERRCODE='23514';
         END IF;
         RETURN NULL;
        END; $$;
        CREATE CONSTRAINT TRIGGER manual_payment_core_decision_bundle AFTER INSERT ON procurement.manual_payment_core_decisions
         DEFERRABLE INITIALLY DEFERRED FOR EACH ROW EXECUTE FUNCTION procurement.require_manual_payment_core_decision();

        CREATE FUNCTION procurement.require_manual_payment_company_date() RETURNS trigger LANGUAGE plpgsql AS $$
        BEGIN
         IF NOT EXISTS (
          SELECT 1 FROM procurement.manual_payment_decisions d
          JOIN procurement.manual_payment_requests p ON p.tenant_id=d.tenant_id AND p.legal_entity_id=d.legal_entity_id AND p.id=d.manual_payment_request_id AND p.branch_id=d.branch_id
          WHERE d.tenant_id=NEW.tenant_id AND d.legal_entity_id=NEW.legal_entity_id AND d.id=NEW.manual_payment_decision_id
           AND d.manual_payment_request_id=NEW.manual_payment_request_id AND d.branch_id=NEW.branch_id AND d.to_version=NEW.request_version
           AND d.action=NEW.action AND d.occurred_at=NEW.observed_at_utc AND NEW.created_at=NEW.observed_at_utc AND NEW.created_by=d.actor_principal_id
           AND p.paid_on<=NEW.company_business_date
           AND EXISTS (SELECT 1 FROM pg_timezone_names z WHERE z.name=NEW.company_time_zone_id)
           AND (NEW.observed_at_utc AT TIME ZONE NEW.company_time_zone_id)::date=NEW.company_business_date
         ) THEN
          RAISE EXCEPTION 'Manual payment date must match its server-observed company date' USING ERRCODE='23514';
         END IF;
         RETURN NULL;
        END; $$;
        CREATE CONSTRAINT TRIGGER manual_payment_company_date_bundle AFTER INSERT ON procurement.manual_payment_date_observations
         DEFERRABLE INITIALLY DEFERRED FOR EACH ROW EXECUTE FUNCTION procurement.require_manual_payment_company_date();
        """);

    private static void RemoveGovernanceGuards(MigrationBuilder migrationBuilder) => migrationBuilder.Sql("""
        LOCK TABLE procurement.manual_payment_requests, procurement.manual_payment_workflow_cases,
          procurement.manual_payment_core_decisions, procurement.manual_payment_date_observations,
          procurement.policy_versions, procurement.operational_policy_identity_evidence,
          procurement.audit_events IN ACCESS EXCLUSIVE MODE;
        DO $$ DECLARE core_evidence boolean := false;
        BEGIN
          IF to_regclass('core_approval.cases') IS NOT NULL THEN
            EXECUTE 'LOCK TABLE core_approval.cases IN ACCESS EXCLUSIVE MODE';
            EXECUTE 'SELECT EXISTS (SELECT 1 FROM core_approval.cases WHERE "SourceModule"=''procurement''
              AND "SourceType" IN (''manual_payment_request'',''manual_payment_confirmation''))' INTO core_evidence;
          END IF;
          IF core_evidence OR EXISTS (SELECT 1 FROM procurement.manual_payment_requests)
            OR EXISTS (SELECT 1 FROM procurement.manual_payment_workflow_cases)
            OR EXISTS (SELECT 1 FROM procurement.manual_payment_core_decisions)
            OR EXISTS (SELECT 1 FROM procurement.manual_payment_date_observations)
            OR EXISTS (SELECT 1 FROM procurement.policy_versions WHERE policy_key='MANUAL_PAYMENT_REVIEW')
            OR EXISTS (SELECT 1 FROM procurement.operational_policy_identity_evidence WHERE policy_key='MANUAL_PAYMENT_REVIEW')
            OR EXISTS (SELECT 1 FROM procurement.audit_events WHERE record_type IN
                ('ManualPaymentWorkflowCaseLink','ManualPaymentCoreDecisionLink','ManualPaymentBusinessDateObservation')
                OR changed_fields_json->>'principalEvidenceKind'='ManualPayment') THEN
            RAISE EXCEPTION 'Payment governance evidence is retained; downgrade is unsafe' USING ERRCODE='23514';
          END IF;
        END; $$;
        DROP TRIGGER manual_payment_company_date_bundle ON procurement.manual_payment_date_observations;
        DROP TRIGGER manual_payment_core_decision_bundle ON procurement.manual_payment_core_decisions;
        DROP TRIGGER manual_payment_core_case_bundle ON procurement.manual_payment_workflow_cases;
        DROP TRIGGER manual_payment_governance_bundle ON procurement.manual_payment_decisions;
        DROP TRIGGER manual_payment_title_immutable ON procurement.manual_payment_requests;
        DROP TRIGGER manual_payment_case_append_only ON procurement.manual_payment_workflow_cases;
        DROP TRIGGER manual_payment_core_decision_append_only ON procurement.manual_payment_core_decisions;
        DROP TRIGGER manual_payment_date_append_only ON procurement.manual_payment_date_observations;
        DROP FUNCTION procurement.require_manual_payment_company_date();
        DROP FUNCTION procurement.require_manual_payment_core_decision();
        DROP FUNCTION procurement.require_manual_payment_core_case();
        DROP FUNCTION procurement.manual_payment_policy_digest(jsonb);
        DROP FUNCTION procurement.require_manual_payment_governance_companions();
        DROP FUNCTION procurement.guard_manual_payment_title();
        """);
}
