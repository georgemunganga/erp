#!/usr/bin/env python3
"""Read-only, source-bounded owner check after the P25 normal-cookie UI journey.

The browser's result remains unmodified. Run only against a disposable, exact
private PostgreSQL 17 container after the browser and API processes have exited.
This is a bounded approval proof, not Finance or native IdP certification.
"""

import argparse
import decimal
import hashlib
import json
import os
from pathlib import Path
import re
import stat
import subprocess
import sys


SYSTEM_ID = "7698678713374847009"
TENANT = "p25-private-http"
GUID = re.compile(r"[0-9a-f]{8}(?:-[0-9a-f]{4}){3}-[0-9a-f]{12}\Z")
DB_NAME = re.compile(r"proc_p25_http_[a-z0-9_]{1,48}\Z")
HEX64 = re.compile(r"[0-9a-f]{64}\Z")


class VerificationFailure(Exception):
    pass


def need(ok, code):
    if not ok:
        raise VerificationFailure(code)


def uuid(value):
    return isinstance(value, str) and bool(GUID.fullmatch(value)) and value != "00000000-0000-0000-0000-000000000000"


def sha(data):
    return hashlib.sha256(data).hexdigest()


def protected_json(path):
    need(path.is_absolute() and not path.is_symlink() and path.is_file()
         and path.resolve(strict=True) == path, "ProtectedInputMissing")
    need(stat.S_IMODE(path.stat().st_mode) == 0o600 and path.stat().st_size <= 65536,
         "ProtectedInputModeOrSize")
    data = path.read_bytes()
    try:
        value = json.loads(data, parse_float=decimal.Decimal)
    except (ValueError, UnicodeError):
        raise VerificationFailure("ProtectedInputMalformed") from None
    need(isinstance(value, dict), "ProtectedInputShape")
    return value, sha(data)


def owner_query(container, database, request_id, case_id):
    need(HEX64.fullmatch(container), "PrivateContainerIdRequired")
    try:
        meta = json.loads(subprocess.run(["docker", "inspect", container], check=True,
            capture_output=True, text=True, timeout=15).stdout)[0]
    except (OSError, ValueError, subprocess.SubprocessError, IndexError):
        raise VerificationFailure("PrivateContainerInspectionFailed") from None
    need(meta.get("Id") == container and meta.get("State", {}).get("Running") is True
         and meta.get("HostConfig", {}).get("NetworkMode") == "none"
         and not meta.get("HostConfig", {}).get("PortBindings")
         and any(m.get("Destination") == "/var/run/postgresql" for m in meta.get("Mounts", [])),
         "PrivateSocketContainerChanged")
    # Only validated UUID literals reach SQL. The server forces a read-only
    # session; the explicit transaction gives one consistent owner snapshot.
    query = """BEGIN TRANSACTION ISOLATION LEVEL REPEATABLE READ READ ONLY;
SELECT jsonb_build_object('database',current_database(),
 'system',(SELECT system_identifier::text FROM pg_control_system()),
 'tcp',inet_server_addr(),'readOnly',current_setting('default_transaction_read_only'),
 'request',(SELECT to_jsonb(r) FROM procurement.purchase_requests r WHERE r.id='{request}'::uuid),
 'lines',(SELECT coalesce(jsonb_agg(to_jsonb(l) ORDER BY l.id),'[]'::jsonb)
          FROM procurement.purchase_request_lines l WHERE l.purchase_request_id='{request}'::uuid),
 'evaluation',(SELECT to_jsonb(e) FROM procurement.policy_evaluations e
               WHERE e.purchase_request_id='{request}'::uuid AND e.id='{evaluation}'::uuid),
 'checks',(SELECT coalesce(jsonb_agg(to_jsonb(b) ORDER BY b.id),'[]'::jsonb)
           FROM procurement.budget_check_snapshots b WHERE b.purchase_request_id='{request}'::uuid),
 'submission',(SELECT to_jsonb(s) FROM procurement.purchase_request_approval_submissions s
               WHERE s.purchase_request_id='{request}'::uuid),
 'authority',(SELECT to_jsonb(a) FROM procurement.assignment_authority_evidence a
              JOIN procurement.purchase_request_approval_submissions s ON s.id=a.submission_id
              WHERE s.purchase_request_id='{request}'::uuid),
 'outcome',(SELECT to_jsonb(o) FROM procurement.purchase_request_approval_outcomes o
            WHERE o.purchase_request_id='{request}'::uuid),
 'receipts',(SELECT coalesce(jsonb_agg(to_jsonb(x) ORDER BY x.id),'[]'::jsonb)
             FROM procurement.approval_command_receipts x WHERE x.purchase_request_id='{request}'::uuid),
 'events',(SELECT coalesce(jsonb_agg(jsonb_build_object('type',x.event_type,'version',x.event_version,
             'payload',x.payload_json) ORDER BY x.id),'[]'::jsonb)
           FROM procurement.outbox_events x WHERE x.aggregate_id='{request}'::uuid
             AND x.event_type IN ('purchase_request.approval_opened.v2',
               'purchase_request.approval_decision_recorded.v2','purchase_request.approved.v2')),
 'policy',(SELECT to_jsonb(p) FROM procurement.policy_versions p WHERE p.id='{policy}'::uuid),
 'grant',(SELECT to_jsonb(g) FROM procurement.spending_authority_grants g
          JOIN procurement.assignment_authority_evidence a ON a.grant_id=g.id
          JOIN procurement.purchase_request_approval_submissions s ON s.id=a.submission_id
          WHERE s.purchase_request_id='{request}'::uuid),
 'case',(SELECT to_jsonb(c) FROM core_approval.cases c WHERE c."Id"='{case}'::uuid),
 'stages',(SELECT coalesce(jsonb_agg(to_jsonb(x) ORDER BY x."Id"),'[]'::jsonb)
           FROM core_approval.stages x WHERE x."CaseId"='{case}'::uuid),
 'assignments',(SELECT coalesce(jsonb_agg(to_jsonb(x) ORDER BY x."Id"),'[]'::jsonb)
                FROM core_approval.assignments x WHERE x."CaseId"='{case}'::uuid),
 'decisions',(SELECT coalesce(jsonb_agg(to_jsonb(x) ORDER BY x."Id"),'[]'::jsonb)
              FROM core_approval.decisions x WHERE x."CaseId"='{case}'::uuid),
 'people',(SELECT coalesce(jsonb_agg(jsonb_build_object('id',id,'worker',worker_id,
            'active',is_active,'archived',is_archived,'tenant',tenant_id) ORDER BY id),'[]'::jsonb)
           FROM hrm.local_users WHERE id IN ('{maker}'::uuid,'{checker}'::uuid,'{approver}'::uuid)),
 'noFinanceFacts',NOT EXISTS(SELECT 1 FROM procurement.finance_invoice_handoffs)
              AND NOT EXISTS(SELECT 1 FROM procurement.finance_payment_status_refs)
              AND NOT EXISTS(SELECT 1 FROM procurement.purchase_request_finance_release_requests)
              AND NOT EXISTS(SELECT 1 FROM procurement.purchase_request_finance_release_confirmations))::text;
ROLLBACK;""".format(**request_id)
    try:
        run = subprocess.run(["docker", "exec", "-i", "-e",
            "PGOPTIONS=-c default_transaction_read_only=on -c statement_timeout=15000 -c lock_timeout=3000",
            container, "psql", "-X", "-qAt", "-U", "postgres", "-d", database,
            "-v", "ON_ERROR_STOP=1", "-v", "VERBOSITY=terse"], input=query,
            capture_output=True, text=True, timeout=30, check=True)
        lines = run.stdout.splitlines()
        need(len(lines) == 1, "OwnerQueryResultCount")
        return json.loads(lines[0], parse_float=decimal.Decimal)
    except (OSError, ValueError, subprocess.SubprocessError):
        raise VerificationFailure("OwnerReadOnlyQueryFailed") from None


def digest(value):
    return sha(json.dumps(value, ensure_ascii=False, separators=(",", ":")).encode("utf-8"))


def exact(value):
    try:
        return decimal.Decimal(str(value))
    except decimal.InvalidOperation:
        raise VerificationFailure("InvalidExactAmount") from None


def verify(fixture, browser, facts, expected_database):
    need(fixture.get("schemaVersion") == 1 and fixture.get("controlPlaneOnly") is True
         and fixture.get("browserAuthenticationRequired") == "NORMAL_LOCAL_PASSWORD_AND_SESSION_COOKIE"
         and fixture.get("tenantId") == TENANT and fixture.get("database") == expected_database,
         "FixtureBoundary")
    need(browser.get("schemaVersion") == 1 and browser.get("status") == "PASS_NORMAL_COOKIE_APPROVAL_UI_ONLY"
         and browser.get("browserApprovalJourneyVerified") is True
         and browser.get("authentication") == "NORMAL_LOCAL_PASSWORD_AND_SESSION_COOKIE"
         and browser.get("apiSetupAuthentication") == "PRIVATE_SUBJECT_TEST_AUTH_ONLY"
         and all(browser.get(flag) is False for flag in
                 ("externalFinanceAuthority", "nativeIdentityProviderVerified", "ownerCompletionVerified", "productionTouched"))
         and browser.get("database") == expected_database and browser.get("tenantId") == TENANT
         and browser.get("companyId") == fixture.get("companyId")
         and browser.get("branchId") == fixture.get("branchId"), "BrowserBoundary")
    for key in ("companyId", "branchId", "makerPrincipalId", "checkerPrincipalId",
                "approverPrincipalId", "configurationMakerPrincipalId"):
        need(uuid(fixture.get(key)), "FixtureIdentifier")
    for key in ("requestId", "policyEvaluationId", "financeCheckId", "caseId", "decisionId", "publishedPolicyId"):
        need(uuid(browser.get(key)), "BrowserIdentifier")
    need(browser.get("checkedAmountExact") == "10.2468"
         and browser.get("publishedPolicyVersion") == 1,
         "BrowserAmountOrPolicy")
    need(facts.get("database") == expected_database and facts.get("system") == SYSTEM_ID
         and facts.get("tcp") is None and facts.get("readOnly") == "on", "PrivatePostgresBoundary")
    request = facts.get("request"); lines = facts.get("lines")
    maker = fixture["makerPrincipalId"]; checker = fixture["checkerPrincipalId"]
    approver = fixture["approverPrincipalId"]; company = fixture["companyId"]
    rid = browser["requestId"]; case_id = browser["caseId"]
    need(isinstance(request, dict) and request.get("id") == rid
         and request.get("tenant_id") == TENANT and request.get("legal_entity_id") == company
         and request.get("branch_ref") == fixture["branchId"] and request.get("status") == "Approved"
         and request.get("version") == browser.get("approvedVersion")
         and request.get("submitted_version") == browser.get("submittedVersion")
         and request.get("requester_identity_schema_version") == 1
         and request.get("requester_principal_id") == maker == request.get("created_by")
         and request.get("requester_worker_id") is None and request.get("requester_core_worker_id") is None
         and request.get("source_channel") == "ERP" and not request.get("on_behalf_of_subject_id")
         and request.get("currency_code") == "ZMW" and request.get("approval_state") == "Approved",
         "ApprovedOriginalCoreRequest")
    original = {"schemaVersion": 1, "Id": rid, "TenantId": TENANT, "LegalEntityId": company,
                "RequesterPrincipalId": maker, "CreatedBy": maker,
                "RequesterCoreWorkerId": None, "SourceChannel": "ERP"}
    need(request.get("requester_identity_sha256") == digest(original), "OriginalPrincipalDigest")
    need(isinstance(lines, list) and len(lines) == 1, "ExactlyOneRequestLine")
    line = lines[0]
    need(line.get("purchase_request_id") == rid and line.get("category_code") == "PRIVATE_P25"
         and line.get("approval_status") == "Approved"
         and exact(line.get("requested_quantity")) == decimal.Decimal("2")
         and exact(line.get("estimated_unit_price")) == decimal.Decimal("5.1234")
         and exact(line.get("approved_quantity")) == decimal.Decimal("2")
         and exact(line["requested_quantity"]) * exact(line["estimated_unit_price"])
             == decimal.Decimal("10.2468"), "ExactApprovedLine")
    evaluation = facts.get("evaluation"); policy = facts.get("policy")
    need(isinstance(evaluation, dict) and evaluation.get("id") == browser["policyEvaluationId"]
         and evaluation.get("purchase_request_id") == rid
         and evaluation.get("request_version") == browser.get("submittedVersion")
         and evaluation.get("outcome") == "Matched"
         and isinstance(policy, dict) and policy.get("id") == browser["publishedPolicyId"]
         and policy.get("state") == "Published" and policy.get("policy_key") == "PURCHASE_REQUEST"
         and policy.get("policy_version_number") == browser["publishedPolicyVersion"]
         and evaluation.get("policy_version_id") == policy["id"], "IndependentBuyingRules")
    checks = facts.get("checks")
    need(isinstance(checks, list) and len(checks) == 1, "ExactlyOneFinanceCheck")
    check = checks[0]
    need(check.get("id") == browser["financeCheckId"] and check.get("purchase_request_id") == rid
         and check.get("request_version") == browser.get("submittedVersion")
         and check.get("status") == "Available" and check.get("currency_code") == "ZMW"
         and exact(check.get("checked_amount")) == decimal.Decimal("10.2468")
         and check.get("checker_identity_schema_version") == 2
         and check.get("checked_by_subject_id") == checker != maker
         and check.get("checked_by_worker_id") is None
         and check.get("reservation_status") == "Confirmed"
         and check.get("evidence_ref") == "private-fixture:human-recorded-manual-evidence-only"
         and HEX64.fullmatch(check.get("checker_identity_sha256") or ""), "IndependentChecker2")
    pin = facts.get("submission"); authority = facts.get("authority")
    need(isinstance(pin, dict) and pin.get("snapshot_schema_version") == 3
         and pin.get("finance_identity_schema_version") == 2
         and pin.get("purchase_request_id") == rid and pin.get("core_approval_case_id") == case_id
         and pin.get("submitted_business_version") == browser.get("submittedVersion")
         and pin.get("requester_principal_id") == maker == pin.get("maker_subject_id")
         and pin.get("maker_worker_id") is None and pin.get("requester_worker_id") is None
         and pin.get("requester_identity_sha256") == request["requester_identity_sha256"]
         and pin.get("finance_snapshot_id") == check["id"]
         and pin.get("policy_evaluation_id") == evaluation["id"]
         and pin.get("policy_version_id") == policy["id"]
         and pin.get("policy_version_number") == policy["policy_version_number"]
         and pin.get("finance_evidence_mode") == "ApprovedManualEvidence"
         and pin.get("amount_basis") == "EstimatedLineBase"
         and exact(pin.get("estimated_amount")) == decimal.Decimal("10.2468"),
         "Submission3Binding")
    principal_pin = {"schemaVersion": 3, "tenant": TENANT, "company": company, "request": rid,
                     "businessVersion": pin["submitted_business_version"], "caseId": case_id,
                     "maker": maker, "requester": maker, "makerAlias": None,
                     "requesterAlias": None, "originalIdentitySha": request["requester_identity_sha256"]}
    need(pin.get("principal_identity_sha256") == digest(principal_pin), "SubmissionPrincipalDigest")
    core_case = facts.get("case"); stages = facts.get("stages"); assignments = facts.get("assignments")
    decisions = facts.get("decisions")
    need(isinstance(core_case, dict) and core_case.get("Id") == case_id
         and core_case.get("TenantId") == TENANT and core_case.get("LegalEntityId") == company
         and core_case.get("SourceId") == rid and core_case.get("State") == "Approved"
         and core_case.get("MakerSubjectId") == maker
         and core_case.get("PolicyVersionId") == policy["id"]
         and core_case.get("SourceVersion") == browser.get("submittedVersion")
         and len(stages) == len(assignments) == len(decisions) == 1,
         "CoreCaseBinding")
    stage = stages[0]; assignment = assignments[0]; decision = decisions[0]
    need(stage.get("CaseId") == case_id and stage.get("State") == "Approved"
         and assignment.get("CaseId") == case_id and assignment.get("StageId") == stage.get("Id")
         and assignment.get("State") == "Approved"
         and assignment.get("AssigneeSubjectId") == approver
         and decision.get("Id") == browser["decisionId"] and decision.get("CaseId") == case_id
         and decision.get("StageId") == stage.get("Id") and decision.get("AssignmentId") == assignment.get("Id")
         and decision.get("Action") == "Approve" and decision.get("ActorSubjectId") == approver
         and approver not in (maker, checker), "IndependentCoreDecision")
    need(isinstance(authority, dict) and authority.get("submission_id") == pin["id"]
         and authority.get("identity_schema_version") == 2
         and authority.get("core_approval_case_id") == case_id
         and authority.get("core_stage_id") == stage["Id"]
         and authority.get("core_assignment_id") == assignment["Id"]
         and authority.get("principal_id") == approver
         and authority.get("assignee_worker_id") is None
         and uuid(authority.get("grant_id")) and HEX64.fullmatch(authority.get("evidence_sha256") or ""),
         "Assignment2Binding")
    grant = facts.get("grant")
    need(isinstance(grant, dict) and grant.get("id") == authority["grant_id"]
         and grant.get("state") == "Active" and grant.get("principal_id") == approver
         and grant.get("permission") == "procurement-approval-decide"
         and grant.get("action") == "PurchaseRequest.Approve", "CurrentGrantBinding")
    outcome = facts.get("outcome")
    need(isinstance(outcome, dict) and outcome.get("submission_id") == pin["id"]
         and outcome.get("purchase_request_id") == rid and outcome.get("core_approval_case_id") == case_id
         and outcome.get("core_decision_id") == decision["Id"]
         and outcome.get("actor_subject_id") == approver and outcome.get("outcome") == "Approved",
         "ApprovedOutcomeBinding")
    receipts = facts.get("receipts"); events = facts.get("events")
    need(isinstance(receipts, list) and len(receipts) == 2
         and {x.get("operation") for x in receipts} == {"Open", "Decide"}
         and all(x.get("core_approval_case_id") == case_id for x in receipts)
         and all(x.get("purchase_request_id") == rid and uuid(x.get("command_key"))
                 and HEX64.fullmatch(x.get("command_fingerprint") or "") for x in receipts)
         and len({x["command_key"] for x in receipts}) == 2
         and {x.get("operation"): x.get("actor_subject_id") for x in receipts}
             == {"Open": maker, "Decide": approver}
         and {x.get("operation"): x.get("core_assignment_id") for x in receipts}
             == {"Open": None, "Decide": assignment["Id"]}, "TwoActorOwnedReceipts")
    for receipt in receipts:
        try:
            reply = json.loads(receipt["response_json"])
        except (TypeError, ValueError):
            raise VerificationFailure("ReceiptResponseMalformed") from None
        need(reply.get("requestId") == rid and reply.get("caseId") == case_id
             and (receipt["operation"] != "Decide" or reply.get("decisionId") == decision["Id"]),
             "ReceiptResponseBinding")
    expected_events = {"purchase_request.approval_opened.v2",
                       "purchase_request.approval_decision_recorded.v2", "purchase_request.approved.v2"}
    need(isinstance(events, list) and len(events) == 3
         and {x.get("type") for x in events} == expected_events
         and all(x.get("version") == 2 for x in events), "ApprovedV2Events")
    for event in events:
        try:
            payload = json.loads(event["payload"])
        except (TypeError, ValueError):
            raise VerificationFailure("V2EventPayloadMalformed") from None
        need(payload.get("submissionId") == pin["id"]
             and payload.get("submissionSchemaVersion") == 3
             and payload.get("principalApprovalSchemaVersion") == 1, "V2EventBinding")
    people = facts.get("people")
    need(isinstance(people, list) and len(people) == 3
         and {x.get("id") for x in people} == {maker, checker, approver}
         and all(x.get("worker") is None and x.get("tenant") == TENANT
                 and x.get("active") is True and x.get("archived") is False for x in people)
         and facts.get("noFinanceFacts") is True, "CoreOnlyNoFinanceFacts")
    return {"requestId": rid, "caseId": case_id, "approvedLineExact": "10.2468",
            "sourceVersion": request["version"], "submittedBusinessVersion": pin["submitted_business_version"],
            "checkerIdentitySchemaVersion": 2, "submissionSchemaVersion": 3,
            "assignmentIdentitySchemaVersion": 2, "coreDecisionAction": "Approve",
            "receiptCount": 2, "v2EventCount": 3,
            "originalPrincipalDigestVerified": True, "submissionPrincipalDigestVerified": True,
            "externalFinanceAuthority": False, "nativeIdentityProviderVerified": False,
            "globalProcurementModuleComplete": False}


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--expected-database", required=True)
    parser.add_argument("--pg-container", required=True, help="Full private PostgreSQL container ID")
    parser.add_argument("--output", type=Path, required=True, help="New 0600 owner result beside browser result")
    args = parser.parse_args()
    need(bool(DB_NAME.fullmatch(args.expected_database)), "ExpectedPrivateDatabaseRequired")
    fixture_path = Path(os.environ.get("P25_COOKIE_FIXTURE", ""))
    browser_path = Path(os.environ.get("P25_COOKIE_BROWSER_RESULT", ""))
    need(str(fixture_path) and str(browser_path), "ProtectedPathsRequired")
    fixture, fixture_hash = protected_json(fixture_path)
    browser, browser_hash = protected_json(browser_path)
    need(browser.get("fixtureSha256") == fixture_hash, "BrowserFixtureHash")
    need(args.output.is_absolute() and args.output.parent == browser_path.parent
         and args.output != browser_path and args.output != fixture_path
         and args.output.parent.is_dir() and not args.output.parent.is_symlink()
         and args.output.parent.resolve(strict=True) == args.output.parent
         and stat.S_IMODE(args.output.parent.stat().st_mode) == 0o700
         and not args.output.exists() and not args.output.is_symlink(), "NewPrivateOutputRequired")
    identifiers = {"request": browser.get("requestId"), "evaluation": browser.get("policyEvaluationId"),
                   "policy": browser.get("publishedPolicyId"), "case": browser.get("caseId"),
                   "maker": fixture.get("makerPrincipalId"), "checker": fixture.get("checkerPrincipalId"),
                   "approver": fixture.get("approverPrincipalId")}
    need(all(uuid(value) for value in identifiers.values()), "QueryIdentifier")
    facts = owner_query(args.pg_container, args.expected_database, identifiers)
    bounded = verify(fixture, browser, facts, args.expected_database)
    summary = {"schemaVersion": 1, "status": "PASS_PRIVATE_P25_COOKIE_APPROVAL_OWNER_ONLY",
               "database": args.expected_database, "tenantId": TENANT,
               "privatePgSystemIdentifier": SYSTEM_ID, "privatePgContainerId": args.pg_container,
               "fixtureSha256": fixture_hash, "browserResultSha256": browser_hash,
               "verifierSourceSha256": sha(Path(__file__).read_bytes()),
               "readOnlyConsistentPostgresSnapshot": True, "browserResultUnmodified": True,
               "originalBaseGraphUnchanged": "NOT_PROVED_WITHOUT_BEFORE_BROWSER_HASH",
               "browserProvedByIndependentOwner": bounded}
    fd = os.open(args.output, os.O_WRONLY | os.O_CREAT | os.O_EXCL | os.O_NOFOLLOW, 0o600)
    with os.fdopen(fd, "w", encoding="utf-8") as stream:
        json.dump(summary, stream, indent=2, sort_keys=True)
        stream.write("\n")
    need(stat.S_IMODE(args.output.stat().st_mode) == 0o600, "OwnerResultMode")
    print("PASS_PRIVATE_P25_COOKIE_APPROVAL_OWNER_ONLY")


if __name__ == "__main__":
    try:
        main()
    except VerificationFailure as error:
        print("P25_OWNER_VERIFIER_FAILED:" + str(error), file=sys.stderr)
        sys.exit(2)
    except Exception:
        print("P25_OWNER_VERIFIER_FAILED:UnexpectedSourceOrDataShape", file=sys.stderr)
        sys.exit(2)
