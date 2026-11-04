namespace Mightyfin.Erp.Procurement.Infrastructure.Data;

/// <summary>Successor migration SQL only. Attach after genuine EF CreateTable and
/// CHECK replacements; never amend the already applied B1 migration.</summary>
public static class CommercialRoundReviewSql
{
    public const string Install = """
CREATE OR REPLACE FUNCTION procurement.commercial_round_plan_update() RETURNS trigger LANGUAGE plpgsql AS $$
BEGIN
 IF NOT ((OLD.status='Draft' AND NEW.status IN ('Draft','Cancelled','InReview'))
      OR (OLD.status='InReview' AND NEW.status IN ('InReview','Draft','Rejected','Published'))
      OR (OLD.status='Rejected' AND NEW.status='Cancelled'))
    OR NEW.version<>OLD.version+1
    OR (to_jsonb(NEW)-ARRAY['status','updated_at','updated_by','version']) IS DISTINCT FROM
       (to_jsonb(OLD)-ARRAY['status','updated_at','updated_by','version'])
 THEN RAISE EXCEPTION 'Round plan identity/state transition invalid' USING ERRCODE='55000'; END IF;
 RETURN NEW;
END $$;
CREATE FUNCTION procurement.commercial_round_review_complete() RETURNS trigger LANGUAGE plpgsql AS $$
DECLARE plan_id_value uuid; current_plan procurement.commercial_round_plans%ROWTYPE;
 current_revision procurement.commercial_round_plan_revisions%ROWTYPE;
BEGIN
 IF TG_TABLE_NAME='commercial_round_plans' THEN plan_id_value=NEW.id;
 ELSE plan_id_value=NEW.plan_id; END IF;
 SELECT * INTO STRICT current_plan FROM procurement.commercial_round_plans
  WHERE tenant_id=NEW.tenant_id AND legal_entity_id=NEW.legal_entity_id AND id=plan_id_value;
 SELECT * INTO STRICT current_revision FROM procurement.commercial_round_plan_revisions
  WHERE tenant_id=current_plan.tenant_id AND legal_entity_id=current_plan.legal_entity_id
   AND plan_id=current_plan.id AND result_version=current_plan.version;
 IF current_revision.result_status<>current_plan.status THEN
  RAISE EXCEPTION 'Round review result mismatch' USING ERRCODE='23514'; END IF;
 IF current_revision.operation='Submit' THEN
  IF current_plan.status<>'InReview' OR NOT EXISTS(
   SELECT 1 FROM procurement.commercial_round_review_submissions s
   JOIN core_approval.cases c ON c."TenantId"=s.tenant_id AND c."LegalEntityId"=s.legal_entity_id
      AND c."Id"=s.core_case_id AND c."SourceModule"='procurement'
      AND c."SourceType"='commercial_round_plan' AND c."SourceId"=s.plan_id
      AND c."SourceVersion"=s.submitted_plan_version AND c."MakerSubjectId"=s.maker_principal_id
      AND c."PolicyVersionId"=s.policy_version_id AND c."PolicyVersionNumber"=s.policy_version_number
   JOIN procurement.policy_versions p ON p.tenant_id=s.tenant_id AND p.legal_entity_id=s.legal_entity_id
      AND p.id=s.policy_version_id AND p.policy_key='COMMERCIAL_ROUND_PLAN_REVIEW'
      AND p.policy_version_number=s.policy_version_number AND p.state='Published'
   JOIN procurement.operational_policy_identity_evidence pe ON pe.tenant_id=s.tenant_id
      AND pe.legal_entity_id=s.legal_entity_id AND pe.policy_key=p.policy_key
      AND pe.policy_version_id=p.id AND pe.policy_version_number=p.policy_version_number
      AND pe.stage='Published' AND pe.rules_sha256=s.policy_rules_sha256
   JOIN core_approval.stages st ON st."TenantId"=c."TenantId"
      AND st."LegalEntityId"=c."LegalEntityId" AND st."CaseId"=c."Id"
      AND st."Sequence"=1 AND st."Code"='RoundPlanReview' AND st."State"='Active'
      AND st."Mode"=p.rules_json::jsonb->>'mode'
      AND st."RequiredApprovals"=(p.rules_json::jsonb->>'requiredApprovals')::integer
   JOIN procurement.commercial_round_plan_receipts receipt
     ON receipt.tenant_id=s.tenant_id AND receipt.legal_entity_id=s.legal_entity_id
      AND receipt.plan_id=s.plan_id AND receipt.revision_id=s.submitted_revision_id
      AND receipt.operation='Submit'
      AND (receipt.response_json::jsonb->>'submissionId')::uuid=s.id
      AND (receipt.response_json::jsonb->>'caseId')::uuid=s.core_case_id
   WHERE s.tenant_id=current_plan.tenant_id AND s.legal_entity_id=current_plan.legal_entity_id
     AND s.plan_id=current_plan.id AND s.branch_id=current_plan.branch_id
     AND s.sourcing_event_id=current_plan.sourcing_event_id
     AND s.submitted_revision_id=current_revision.id
     AND s.submitted_plan_version=current_revision.result_version
     AND s.submitted_content_sha256=current_revision.content_sha256
     AND s.submitted_at=current_revision.recorded_at AND c."State"='Pending'
     AND (SELECT count(*) FROM core_approval.stages other_stage
          WHERE other_stage."TenantId"=c."TenantId" AND other_stage."LegalEntityId"=c."LegalEntityId"
            AND other_stage."CaseId"=c."Id")=1
     AND (SELECT count(*) FROM core_approval.assignments a
          WHERE a."TenantId"=c."TenantId" AND a."LegalEntityId"=c."LegalEntityId"
            AND a."CaseId"=c."Id" AND a."StageId"=st."Id"
            AND a."PermissionKey"='procurement-round-plan-review' AND a."State"='Pending'
            AND a."DelegatedFromSubjectId" IS NULL
            AND a."AssigneeSubjectId"<>s.maker_principal_id
            AND a."AssigneeSubjectId"<>current_plan.maker_principal_id
            AND a."AssigneeSubjectId"<>(SELECT material.actor_principal_id
                FROM procurement.commercial_round_plan_revisions material
                WHERE material.tenant_id=s.tenant_id AND material.legal_entity_id=s.legal_entity_id
                  AND material.plan_id=s.plan_id AND material.result_version<s.submitted_plan_version
                  AND material.operation IN ('Create','Revise')
                ORDER BY material.result_version DESC LIMIT 1)
            AND a."AssigneeSubjectId" IN (SELECT value FROM jsonb_array_elements_text(
                p.rules_json::jsonb->'assigneePrincipalIds') AS wanted(value)))
           =jsonb_array_length(p.rules_json::jsonb->'assigneePrincipalIds')
     AND (SELECT count(*) FROM core_approval.assignments all_assignment
          WHERE all_assignment."TenantId"=c."TenantId" AND all_assignment."LegalEntityId"=c."LegalEntityId"
            AND all_assignment."CaseId"=c."Id")
           =jsonb_array_length(p.rules_json::jsonb->'assigneePrincipalIds')
     AND NOT EXISTS (SELECT 1 FROM jsonb_array_elements_text(
         p.rules_json::jsonb->'assigneePrincipalIds') AS wanted(value)
         WHERE NOT EXISTS (SELECT 1 FROM core_approval.assignments a
           WHERE a."TenantId"=c."TenantId" AND a."LegalEntityId"=c."LegalEntityId"
             AND a."CaseId"=c."Id" AND a."StageId"=st."Id"
             AND a."AssigneeSubjectId"=wanted.value)))
  THEN RAISE EXCEPTION 'Round submission or Core case incomplete' USING ERRCODE='23514'; END IF;
 ELSIF current_revision.operation IN ('Approve','Return','Reject') THEN
  IF NOT EXISTS(
   SELECT 1 FROM procurement.commercial_round_review_outcomes o
   JOIN procurement.commercial_round_review_submissions s
      ON s.tenant_id=o.tenant_id AND s.legal_entity_id=o.legal_entity_id
      AND s.plan_id=o.plan_id AND s.id=o.submission_id AND s.core_case_id=o.core_case_id
   JOIN core_approval.decisions d ON d."TenantId"=o.tenant_id AND d."LegalEntityId"=o.legal_entity_id
      AND d."Id"=o.core_decision_id AND d."CaseId"=o.core_case_id
      AND d."StageId"=o.core_stage_id AND d."AssignmentId"=o.core_assignment_id
      AND d."ActorSubjectId"=o.actor_principal_id AND d."Action"=o.action
      AND d."Reason" IS NOT DISTINCT FROM o.reason AND d."DecidedAt"=o.recorded_at
   JOIN core_approval.cases c ON c."TenantId"=o.tenant_id AND c."LegalEntityId"=o.legal_entity_id
      AND c."Id"=o.core_case_id AND c."SourceModule"='procurement'
      AND c."SourceType"='commercial_round_plan'
      AND c."SourceId"=o.plan_id AND c."SourceVersion"=s.submitted_plan_version
      AND c."MakerSubjectId"=s.maker_principal_id
      AND c."PolicyVersionId"=s.policy_version_id AND c."PolicyVersionNumber"=s.policy_version_number
   JOIN core_approval.stages st ON st."TenantId"=c."TenantId"
      AND st."LegalEntityId"=c."LegalEntityId" AND st."CaseId"=c."Id"
      AND st."Id"=d."StageId" AND st."Code"='RoundPlanReview' AND st."Sequence"=1
   JOIN core_approval.assignments a ON a."TenantId"=c."TenantId"
      AND a."LegalEntityId"=c."LegalEntityId" AND a."CaseId"=c."Id"
      AND a."StageId"=st."Id" AND a."Id"=d."AssignmentId"
      AND a."PermissionKey"='procurement-round-plan-review'
      AND a."DelegatedFromSubjectId" IS NULL
      AND a."AssigneeSubjectId"=d."ActorSubjectId"
      AND a."AssigneeSubjectId"<>current_plan.maker_principal_id
      AND a."AssigneeSubjectId"<>(SELECT material.actor_principal_id
          FROM procurement.commercial_round_plan_revisions material
          WHERE material.tenant_id=s.tenant_id AND material.legal_entity_id=s.legal_entity_id
            AND material.plan_id=s.plan_id AND material.result_version<s.submitted_plan_version
            AND material.operation IN ('Create','Revise')
          ORDER BY material.result_version DESC LIMIT 1)
   JOIN procurement.commercial_round_plan_receipts receipt
     ON receipt.tenant_id=o.tenant_id AND receipt.legal_entity_id=o.legal_entity_id
      AND receipt.plan_id=o.plan_id AND receipt.revision_id=o.result_revision_id
      AND receipt.operation=o.action
      AND (receipt.response_json::jsonb->>'submissionId')::uuid=o.submission_id
      AND (receipt.response_json::jsonb->>'caseId')::uuid=o.core_case_id
      AND (receipt.response_json::jsonb->>'assignmentId')::uuid=o.core_assignment_id
      AND (receipt.response_json::jsonb->>'decisionId')::uuid=o.core_decision_id
   WHERE o.tenant_id=current_plan.tenant_id AND o.legal_entity_id=current_plan.legal_entity_id
     AND o.plan_id=current_plan.id AND o.branch_id=current_plan.branch_id
     AND o.sourcing_event_id=current_plan.sourcing_event_id
     AND o.result_revision_id=current_revision.id AND o.result_plan_version=current_revision.result_version
     AND o.result_status=current_plan.status AND o.action=current_revision.operation
     AND o.actor_principal_id=current_revision.actor_principal_id
     AND current_revision.content_sha256=s.submitted_content_sha256
     AND ((o.action='Approve' AND ((o.result_status='InReview' AND c."State"='Pending')
          OR (o.result_status='Published' AND c."State"='Approved')))
       OR (o.action='Return' AND o.result_status='Draft' AND c."State"='Returned')
       OR (o.action='Reject' AND o.result_status='Rejected' AND c."State"='Rejected')))
  THEN RAISE EXCEPTION 'Round review decision or result incomplete' USING ERRCODE='23514'; END IF;
 END IF;
 RETURN NEW;
END $$;
CREATE CONSTRAINT TRIGGER commercial_round_review_plan_complete AFTER INSERT OR UPDATE
 ON procurement.commercial_round_plans DEFERRABLE INITIALLY DEFERRED FOR EACH ROW
 EXECUTE FUNCTION procurement.commercial_round_review_complete();
CREATE CONSTRAINT TRIGGER commercial_round_review_revision_complete AFTER INSERT
 ON procurement.commercial_round_plan_revisions DEFERRABLE INITIALLY DEFERRED FOR EACH ROW
 EXECUTE FUNCTION procurement.commercial_round_review_complete();
CREATE CONSTRAINT TRIGGER commercial_round_review_submission_complete AFTER INSERT
 ON procurement.commercial_round_review_submissions DEFERRABLE INITIALLY DEFERRED FOR EACH ROW
 EXECUTE FUNCTION procurement.commercial_round_review_complete();
CREATE CONSTRAINT TRIGGER commercial_round_review_outcome_complete AFTER INSERT
 ON procurement.commercial_round_review_outcomes DEFERRABLE INITIALLY DEFERRED FOR EACH ROW
 EXECUTE FUNCTION procurement.commercial_round_review_complete();
CREATE TRIGGER commercial_round_review_submission_immutable BEFORE UPDATE OR DELETE
 ON procurement.commercial_round_review_submissions FOR EACH ROW
 EXECUTE FUNCTION procurement.commercial_round_immutable();
CREATE TRIGGER commercial_round_review_outcome_immutable BEFORE UPDATE OR DELETE
 ON procurement.commercial_round_review_outcomes FOR EACH ROW
 EXECUTE FUNCTION procurement.commercial_round_immutable();
""";

    public const string RetainedDownGuard = """
DO $$ BEGIN IF EXISTS(SELECT 1 FROM procurement.commercial_round_review_submissions)
 OR EXISTS(SELECT 1 FROM procurement.commercial_round_review_outcomes)
 OR EXISTS(SELECT 1 FROM procurement.policy_versions WHERE policy_key='COMMERCIAL_ROUND_PLAN_REVIEW')
 OR EXISTS(SELECT 1 FROM procurement.operational_policy_identity_evidence
     WHERE policy_key='COMMERCIAL_ROUND_PLAN_REVIEW')
 OR EXISTS(SELECT 1 FROM core_approval.cases WHERE "SourceModule"='procurement'
     AND "SourceType"='commercial_round_plan')
 OR EXISTS(SELECT 1 FROM procurement.commercial_round_plans WHERE status IN ('InReview','Rejected','Published'))
 OR EXISTS(SELECT 1 FROM procurement.commercial_round_plan_revisions
     WHERE operation IN ('Submit','Approve','Return','Reject'))
 OR EXISTS(SELECT 1 FROM procurement.commercial_round_plan_receipts
     WHERE operation IN ('Submit','Approve','Return','Reject'))
 OR EXISTS(SELECT 1 FROM procurement.audit_events WHERE record_type IN
     ('CommercialRoundReviewSubmission','CommercialRoundReviewOutcome'))
 OR EXISTS(SELECT 1 FROM procurement.outbox_events WHERE event_type LIKE 'commercial_round.plan_submit%'
     OR event_type LIKE 'commercial_round.plan_approve%'
     OR event_type LIKE 'commercial_round.plan_return%'
     OR event_type LIKE 'commercial_round.plan_reject%')
 THEN RAISE EXCEPTION 'Retain commercial round review and publication evidence; destructive Down denied'
 USING ERRCODE='55000'; END IF; END $$;
""";
    public const string UninstallEmpty = """
DROP TRIGGER commercial_round_review_plan_complete ON procurement.commercial_round_plans;
DROP TRIGGER commercial_round_review_revision_complete ON procurement.commercial_round_plan_revisions;
DROP TRIGGER commercial_round_review_submission_complete ON procurement.commercial_round_review_submissions;
DROP TRIGGER commercial_round_review_outcome_complete ON procurement.commercial_round_review_outcomes;
DROP TRIGGER commercial_round_review_submission_immutable ON procurement.commercial_round_review_submissions;
DROP TRIGGER commercial_round_review_outcome_immutable ON procurement.commercial_round_review_outcomes;
DROP FUNCTION procurement.commercial_round_review_complete();
CREATE OR REPLACE FUNCTION procurement.commercial_round_plan_update() RETURNS trigger LANGUAGE plpgsql AS $$
BEGIN
 IF OLD.status<>'Draft' OR NEW.status NOT IN ('Draft','Cancelled') OR NEW.version<>OLD.version+1
  OR (to_jsonb(NEW)-ARRAY['status','updated_at','updated_by','version']) IS DISTINCT FROM
     (to_jsonb(OLD)-ARRAY['status','updated_at','updated_by','version'])
 THEN RAISE EXCEPTION 'Round plan identity/state transition invalid' USING ERRCODE='55000'; END IF;
 RETURN NEW;
END $$;
""";
}
