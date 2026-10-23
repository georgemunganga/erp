#!/usr/bin/env python3
"""Private, read-only P29 browser owner proof with a prebrowser baseline.

Capture the baseline after the normal-cookie fixture is written but before
Playwright starts. Verify only after the browser result and completion exist.
Neither the challenged completion nor a browser result alone is acceptance.
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
from urllib.parse import urlparse


SYSTEM_ID = "7698678713374847009"
TENANT = "p25-private-http"
GUID = re.compile(r"[0-9a-f]{8}(?:-[0-9a-f]{4}){3}-[0-9a-f]{12}\Z")
DB_NAME = re.compile(r"proc_p25_http_p29_[a-z0-9_]{1,48}\Z")
HEX64 = re.compile(r"[0-9a-f]{64}\Z")


class VerificationFailure(Exception):
    pass


def need(value, code):
    if not value:
        raise VerificationFailure(code)


def uuid(value):
    return isinstance(value, str) and bool(GUID.fullmatch(value)) and value != "00000000-0000-0000-0000-000000000000"


def digest(value):
    return hashlib.sha256(json.dumps(value, ensure_ascii=False, sort_keys=True,
        separators=(",", ":"), default=str).encode("utf-8")).hexdigest()


def protected_json(path):
    need(path.is_absolute() and not path.is_symlink() and path.is_file()
         and path.resolve(strict=True) == path, "ProtectedInputMissing")
    need(stat.S_IMODE(path.stat().st_mode) == 0o600 and 0 < path.stat().st_size <= 65536,
         "ProtectedInputModeOrSize")
    data = path.read_bytes()
    try:
        value = json.loads(data, parse_float=decimal.Decimal)
    except (ValueError, UnicodeError):
        raise VerificationFailure("ProtectedInputMalformed") from None
    need(isinstance(value, dict), "ProtectedInputShape")
    return value, hashlib.sha256(data).hexdigest()


def private_output(path, beside):
    need(path.is_absolute() and path.parent == beside.parent and path != beside
         and path.parent.is_dir() and not path.parent.is_symlink()
         and path.parent.resolve(strict=True) == path.parent
         and stat.S_IMODE(path.parent.stat().st_mode) == 0o700
         and not path.exists() and not path.is_symlink(), "NewPrivateOutputRequired")


def write_private(path, value):
    fd = os.open(path, os.O_WRONLY | os.O_CREAT | os.O_EXCL | os.O_NOFOLLOW, 0o600)
    with os.fdopen(fd, "w", encoding="utf-8") as stream:
        json.dump(value, stream, indent=2, sort_keys=True)
        stream.write("\n")
    need(stat.S_IMODE(path.stat().st_mode) == 0o600, "OutputMode")


def owner_query(container, database, request, line, first_event, buyer):
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
    need(all(uuid(x) for x in (request, line, first_event, buyer)), "OwnerQueryIdentifiers")
    # Only validated canonical UUIDs enter these fixed SQL literals. No browser
    # text, credential, command body, or arbitrary table name reaches SQL.
    query = """BEGIN TRANSACTION ISOLATION LEVEL REPEATABLE READ READ ONLY;
SELECT jsonb_build_object(
 'database',current_database(),
 'system',(SELECT system_identifier::text FROM pg_control_system()),
 'tcp',inet_server_addr(),'readOnly',current_setting('default_transaction_read_only'),
 'request',(SELECT to_jsonb(r) FROM procurement.purchase_requests r
            WHERE r.id='{request}'::uuid AND r.tenant_id='{tenant}' AND r.legal_entity_id=
             (SELECT legal_entity_id FROM procurement.purchase_requests WHERE id='{request}'::uuid)),
 'requestStable',(SELECT to_jsonb(r)-'version'-'updated_at'-'updated_by'
                  FROM procurement.purchase_requests r WHERE r.id='{request}'::uuid AND r.tenant_id='{tenant}'),
 'line',(SELECT to_jsonb(l) FROM procurement.purchase_request_lines l
         WHERE l.id='{line}'::uuid AND l.purchase_request_id='{request}'::uuid AND l.tenant_id='{tenant}'),
 'lineStable',(SELECT to_jsonb(l)-'version'-'updated_at'-'updated_by'-'allocated_quantity'
               FROM procurement.purchase_request_lines l
               WHERE l.id='{line}'::uuid AND l.purchase_request_id='{request}'::uuid AND l.tenant_id='{tenant}'),
 'approval',jsonb_build_object(
    'evaluations',(SELECT coalesce(jsonb_agg(to_jsonb(x) ORDER BY x.id),'[]'::jsonb)
                   FROM procurement.policy_evaluations x WHERE x.purchase_request_id='{request}'::uuid),
    'checks',(SELECT coalesce(jsonb_agg(to_jsonb(x) ORDER BY x.id),'[]'::jsonb)
              FROM procurement.budget_check_snapshots x WHERE x.purchase_request_id='{request}'::uuid),
    'submissions',(SELECT coalesce(jsonb_agg(to_jsonb(x) ORDER BY x.id),'[]'::jsonb)
                   FROM procurement.purchase_request_approval_submissions x WHERE x.purchase_request_id='{request}'::uuid),
    'authority',(SELECT coalesce(jsonb_agg(to_jsonb(x) ORDER BY x.id),'[]'::jsonb)
                 FROM procurement.assignment_authority_evidence x JOIN procurement.purchase_request_approval_submissions s
                   ON s.id=x.submission_id WHERE s.purchase_request_id='{request}'::uuid),
    'outcomes',(SELECT coalesce(jsonb_agg(to_jsonb(x) ORDER BY x.id),'[]'::jsonb)
                FROM procurement.purchase_request_approval_outcomes x WHERE x.purchase_request_id='{request}'::uuid),
    'receipts',(SELECT coalesce(jsonb_agg(to_jsonb(x) ORDER BY x.id),'[]'::jsonb)
                FROM procurement.approval_command_receipts x WHERE x.purchase_request_id='{request}'::uuid),
    'approvalEvents',(SELECT coalesce(jsonb_agg(to_jsonb(x) ORDER BY x.id),'[]'::jsonb)
                      FROM procurement.outbox_events x WHERE x.aggregate_id='{request}'::uuid
                        AND x.event_type IN ('purchase_request.approval_opened.v2',
                          'purchase_request.approval_decision_recorded.v2','purchase_request.approved.v2')),
    'policies',(SELECT coalesce(jsonb_agg(to_jsonb(x) ORDER BY x.id),'[]'::jsonb)
                FROM procurement.policy_versions x WHERE x.tenant_id='{tenant}' AND x.legal_entity_id=
                 (SELECT legal_entity_id FROM procurement.purchase_requests WHERE id='{request}'::uuid)),
    'grants',(SELECT coalesce(jsonb_agg(to_jsonb(x) ORDER BY x.id),'[]'::jsonb)
              FROM procurement.spending_authority_grants x WHERE x.tenant_id='{tenant}' AND x.legal_entity_id=
               (SELECT legal_entity_id FROM procurement.purchase_requests WHERE id='{request}'::uuid)),
    'cases',(SELECT coalesce(jsonb_agg(to_jsonb(x) ORDER BY x."Id"),'[]'::jsonb)
             FROM core_approval.cases x WHERE x."SourceId"='{request}'::uuid),
    'stages',(SELECT coalesce(jsonb_agg(to_jsonb(x) ORDER BY x."Id"),'[]'::jsonb)
              FROM core_approval.stages x JOIN core_approval.cases c ON c."Id"=x."CaseId"
              WHERE c."SourceId"='{request}'::uuid),
    'assignments',(SELECT coalesce(jsonb_agg(to_jsonb(x) ORDER BY x."Id"),'[]'::jsonb)
                   FROM core_approval.assignments x JOIN core_approval.cases c ON c."Id"=x."CaseId"
                   WHERE c."SourceId"='{request}'::uuid),
    'decisions',(SELECT coalesce(jsonb_agg(to_jsonb(x) ORDER BY x."Id"),'[]'::jsonb)
                 FROM core_approval.decisions x JOIN core_approval.cases c ON c."Id"=x."CaseId"
                 WHERE c."SourceId"='{request}'::uuid),
    'financeReleaseRequests',(SELECT coalesce(jsonb_agg(to_jsonb(x) ORDER BY x.id),'[]'::jsonb)
       FROM procurement.purchase_request_finance_release_requests x WHERE x.purchase_request_id='{request}'::uuid),
    'financeReleaseConfirmations',(SELECT coalesce(jsonb_agg(to_jsonb(x) ORDER BY x.id),'[]'::jsonb)
       FROM procurement.purchase_request_finance_release_confirmations x
       JOIN procurement.purchase_request_finance_release_requests r
         ON r.id=x.release_request_id AND r.tenant_id=x.tenant_id AND r.legal_entity_id=x.legal_entity_id
       WHERE r.purchase_request_id='{request}'::uuid)),
 'sourceGraphs',(SELECT coalesce(jsonb_agg(jsonb_build_object(
    'event',to_jsonb(ev),
    'evidence',(SELECT to_jsonb(x) FROM procurement.sourcing_draft_evidence x WHERE x.sourcing_event_id=ev.id),
    'bindings',(SELECT coalesce(jsonb_agg(to_jsonb(x) ORDER BY x.id),'[]'::jsonb)
                FROM procurement.sourcing_demand_bindings x WHERE x.sourcing_event_id=ev.id),
    'lines',(SELECT coalesce(jsonb_agg(to_jsonb(x) ORDER BY x.id),'[]'::jsonb)
             FROM procurement.sourcing_lines x WHERE x.sourcing_event_id=ev.id),
    'receipts',(SELECT coalesce(jsonb_agg(to_jsonb(x) ORDER BY x.id),'[]'::jsonb)
                FROM procurement.sourcing_command_receipts x WHERE x.sourcing_event_id=ev.id),
    'allocations',(SELECT coalesce(jsonb_agg(to_jsonb(a) ORDER BY a.id),'[]'::jsonb)
                   FROM procurement.purchase_request_allocations a
                   JOIN procurement.sourcing_demand_bindings b ON b.allocation_id=a.id WHERE b.sourcing_event_id=ev.id)
    ) ORDER BY ev.id),'[]'::jsonb)
    FROM procurement.sourcing_events ev WHERE ev.tenant_id='{tenant}' AND ev.legal_entity_id=
      (SELECT legal_entity_id FROM procurement.purchase_requests WHERE id='{request}'::uuid)),
 'buyer',(SELECT jsonb_build_object('id',u.id,'workerId',u.worker_id,'active',u.is_active,
                                   'archived',u.is_archived,'tenant',u.tenant_id)
          FROM hrm.local_users u WHERE u.id='{buyer}'::uuid AND u.tenant_id='{tenant}'),
 'noDownstream',NOT EXISTS(SELECT 1 FROM procurement.sourcing_invitations)
    AND NOT EXISTS(SELECT 1 FROM procurement.supplier_bids)
    AND NOT EXISTS(SELECT 1 FROM procurement.sourcing_awards)
    AND NOT EXISTS(SELECT 1 FROM procurement.purchase_orders)
    AND NOT EXISTS(SELECT 1 FROM procurement.finance_invoice_handoffs)
    AND NOT EXISTS(SELECT 1 FROM procurement.finance_payment_status_refs))::text;
ROLLBACK;""".format(request=request, line=line, buyer=buyer, tenant=TENANT)
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


def exact(value):
    try:
        return decimal.Decimal(str(value))
    except (decimal.InvalidOperation, TypeError, ValueError):
        raise VerificationFailure("ExactDecimalInvalid") from None


def private_boundary(fixture, facts, expected_database):
    need(fixture.get("schemaVersion") == 2 and fixture.get("controlPlaneOnly") is True
         and fixture.get("browserAuthenticationRequired") == "NORMAL_LOCAL_PASSWORD_AND_SESSION_COOKIE"
         and fixture.get("tenantId") == TENANT and fixture.get("database") == expected_database,
         "FixtureBoundary")
    for key in ("companyId", "branchId", "buyerPrincipalId", "approvedRequestId",
                "approvedRequestLineId", "cancelledDraftEventId"):
        need(uuid(fixture.get(key)), "FixtureIdentifier")
    need(facts.get("database") == expected_database and facts.get("system") == SYSTEM_ID
         and facts.get("tcp") is None and facts.get("readOnly") == "on",
         "PrivatePostgresBoundary")
    buyer = facts.get("buyer")
    need(isinstance(buyer, dict) and buyer.get("id") == fixture["buyerPrincipalId"]
         and buyer.get("workerId") is None and buyer.get("active") is True
         and buyer.get("archived") is False and buyer.get("tenant") == TENANT,
         "CurrentWorkerlessBuyer")
    request = facts.get("request"); line = facts.get("line")
    need(isinstance(request, dict) and isinstance(line, dict)
         and request.get("id") == fixture["approvedRequestId"]
         and line.get("id") == fixture["approvedRequestLineId"]
         and line.get("purchase_request_id") == request["id"]
         and request.get("tenant_id") == TENANT and line.get("tenant_id") == TENANT
         and request.get("legal_entity_id") == fixture["companyId"]
         and line.get("legal_entity_id") == fixture["companyId"]
         and request.get("branch_ref") == fixture["branchId"]
         and request.get("status") == "Approved" and request.get("approval_state") == "Approved"
         and request.get("requester_identity_schema_version") == 1
         and uuid(request.get("requester_principal_id")) and request.get("requester_worker_id") is None
         and exact(line.get("requested_quantity")) == decimal.Decimal("2")
         and exact(line.get("approved_quantity")) == decimal.Decimal("2")
         and exact(line.get("estimated_unit_price")) == decimal.Decimal("5.1234")
         and exact(line.get("allocated_quantity")) == 0,
         "ApprovedOriginalSourceRestored")
    need(facts.get("noDownstream") is True, "NoSupplierOrderOrFinanceEffects")
    graphs = facts.get("sourceGraphs")
    need(isinstance(graphs, list) and all(isinstance(x, dict) for x in graphs), "SourceGraphsShape")
    return graphs


def proxy_boundary(proxy, fixture, fixture_hash, expected_database):
    origin = urlparse(proxy.get("origin", ""))
    need(proxy.get("schemaVersion") == 2
         and proxy.get("transport") == "normal-local-cookie-https-v1"
         and origin.scheme == "https" and origin.hostname == "127.0.0.1"
         and origin.port is not None and origin.path in ("", "/")
         and not origin.username and not origin.password and not origin.query and not origin.fragment
         and proxy.get("apiUrl") == fixture.get("apiUrl")
         and proxy.get("database") == expected_database
         and proxy.get("fixtureSha256") == fixture_hash
         and proxy.get("injectsIdentityHeaders") is False
         and proxy.get("mocksBusinessResponses") is False
         and proxy.get("browserAcceptanceVerified") is False,
         "NormalCookieProxyBoundary")


def first_graph(graphs, fixture):
    selected = [x for x in graphs if x.get("event", {}).get("id") == fixture["cancelledDraftEventId"]]
    need(len(selected) == 1 and selected[0]["event"].get("status") == "Cancelled"
         and selected[0]["event"].get("execution_version") == 2,
         "FirstApiDraftMissing")
    return selected[0]


def capture(fixture, fixture_hash, facts, expected_database):
    graphs = private_boundary(fixture, facts, expected_database)
    need(len(graphs) == 1, "OnePrebrowserApiDraftRequired")
    first = first_graph(graphs, fixture)
    first_binding = first.get("bindings")
    first_allocation = first.get("allocations")
    first_receipts = first.get("receipts")
    need(isinstance(first.get("evidence"), dict)
         and first["evidence"].get("schema_version") == 2
         and isinstance(first_binding, list) and len(first_binding) == 1
         and first_binding[0].get("identity_schema_version") == 2
         and first_binding[0].get("purchase_request_id") == fixture["approvedRequestId"]
         and first_binding[0].get("purchase_request_line_id") == fixture["approvedRequestLineId"]
         and isinstance(first_allocation, list) and len(first_allocation) == 1
         and first_allocation[0].get("status") == "Released"
         and isinstance(first_receipts, list) and len(first_receipts) == 2
         and {x.get("operation") for x in first_receipts} == {"Create", "Cancel"},
         "FirstApiPrincipalDraftAlreadyReleased")
    need(isinstance(facts.get("approval"), dict)
         and len(facts["approval"].get("submissions", [])) == 1
         and len(facts["approval"].get("outcomes", [])) == 1
         and len(facts["approval"].get("decisions", [])) == 1,
         "OriginalP25ApprovalGraphRequired")
    request = facts["request"]; line = facts["line"]
    return {"schemaVersion": 1, "status": "CAPTURED_PRIVATE_P29_BEFORE_BROWSER_ONLY",
            "database": expected_database, "tenantId": TENANT, "fixtureSha256": fixture_hash,
            "requestId": request["id"], "requestLineId": line["id"],
            "firstEventId": fixture["cancelledDraftEventId"],
            "requestVersion": request["version"], "lineVersion": line["version"],
            "requestStableSha256": digest(facts["requestStable"]),
            "lineStableSha256": digest(facts["lineStable"]),
            "approvalGraphSha256": digest(facts["approval"]),
            "firstApiDraftSha256": digest(first),
            "prebrowserSourceCount": len(graphs), "noDownstream": True}


def verify(fixture, fixture_hash, baseline, browser, facts, expected_database, container):
    graphs = private_boundary(fixture, facts, expected_database)
    need(browser.get("schemaVersion") == 2
         and browser.get("status") == "PASS_NORMAL_COOKIE_P29_INTERNAL_DRAFT_UI_ONLY"
         and browser.get("fixtureSha256") == fixture_hash
         and browser.get("challenge") == fixture.get("challenge")
         and browser.get("transport") == "normal-local-cookie-https-v1"
         and browser.get("authentication") == "NORMAL_LOCAL_PASSWORD_AND_SESSION_COOKIE"
         and browser.get("principalRfqDraftVerified") is True
         and browser.get("supplierDispatchVerified") is False
         and browser.get("productionTouched") is False
         and browser.get("database") == expected_database
         and browser.get("tenantId") == TENANT
         and browser.get("companyId") == fixture["companyId"]
         and browser.get("branchId") == fixture["branchId"]
         and browser.get("buyerPrincipalId") == fixture["buyerPrincipalId"],
         "BrowserNormalCookieBoundary")
    for key in ("eventId", "createCommandKey", "cancelCommandKey"):
        need(uuid(browser.get(key)), "BrowserGuidEvidence")
    need(browser["eventId"] != fixture["cancelledDraftEventId"]
         and browser["createCommandKey"] != browser["cancelCommandKey"]
         and isinstance(browser.get("createdVersion"), int) and browser["createdVersion"] > 0
         and browser.get("cancelledVersion") == browser["createdVersion"] + 1,
         "BrowserVersionAndCommandEvidence")
    need(baseline.get("schemaVersion") == 1
         and baseline.get("status") == "CAPTURED_PRIVATE_P29_BEFORE_BROWSER_ONLY"
         and baseline.get("fixtureSha256") == fixture_hash
         and baseline.get("database") == expected_database
         and baseline.get("requestId") == fixture["approvedRequestId"]
         and baseline.get("requestLineId") == fixture["approvedRequestLineId"]
         and baseline.get("firstEventId") == fixture["cancelledDraftEventId"]
         and baseline.get("prebrowserSourceCount") == 1 and baseline.get("noDownstream") is True
         and baseline.get("privatePgSystemIdentifier") == SYSTEM_ID
         and baseline.get("privatePgContainerId") == container
         and baseline.get("verifierSourceSha256") == hashlib.sha256(Path(__file__).read_bytes()).hexdigest()
         and all(isinstance(baseline.get(key), int) and baseline[key] > 0
                 for key in ("requestVersion", "lineVersion"))
         and all(isinstance(baseline.get(key), str) and HEX64.fullmatch(baseline[key])
                 for key in ("requestStableSha256", "lineStableSha256", "approvalGraphSha256", "firstApiDraftSha256")),
         "PrebrowserBaselineBoundary")
    need(digest(facts["requestStable"]) == baseline["requestStableSha256"]
         and digest(facts["lineStable"]) == baseline["lineStableSha256"]
         and digest(facts["approval"]) == baseline["approvalGraphSha256"]
         and facts["request"].get("version") == baseline["requestVersion"] + 2
         and facts["line"].get("version") == baseline["lineVersion"] + 2,
         "OriginalApprovalAndBusinessFieldsUnchangedWithExpectedSourceCAS")
    need(len(graphs) == 2 and digest(first_graph(graphs, fixture)) == baseline["firstApiDraftSha256"],
         "FirstApiDraftUnchangedAndOneBrowserDraftAdded")
    browser_graphs = [x for x in graphs if x.get("event", {}).get("id") == browser["eventId"]]
    need(len(browser_graphs) == 1, "ExactlyOneBrowserDraft")
    graph = browser_graphs[0]
    event = graph.get("event"); evidence = graph.get("evidence")
    bindings = graph.get("bindings"); lines = graph.get("lines")
    receipts = graph.get("receipts"); allocations = graph.get("allocations")
    buyer = fixture["buyerPrincipalId"]
    request = facts["request"]; request_line = facts["line"]
    need(isinstance(event, dict) and event.get("tenant_id") == TENANT
         and event.get("legal_entity_id") == fixture["companyId"]
         and event.get("branch_ref") == fixture["branchId"]
         and event.get("owner_subject_id") == buyer
         and event.get("maker_worker_id") is None
         and event.get("execution_version") == 2
         and event.get("status") == "Cancelled"
         and event.get("version") == browser["cancelledVersion"]
         and event.get("method_revision", 0) > 0
         and uuid(event.get("method_id"))
         and HEX64.fullmatch(event.get("original_draft_fingerprint") or ""),
         "BrowserPrincipalEventCurrent")
    need(isinstance(evidence, dict) and evidence.get("sourcing_event_id") == event["id"]
         and evidence.get("id") == event.get("source_evidence_id")
         and evidence.get("schema_version") == 2
         and evidence.get("maker_subject_id") == buyer
         and evidence.get("maker_principal_id") == buyer
         and evidence.get("maker_worker_id") is None
         and evidence.get("branch_ref") == fixture["branchId"]
         and evidence.get("create_command_key") == browser["createCommandKey"]
         and evidence.get("original_draft_fingerprint") == event["original_draft_fingerprint"]
         and HEX64.fullmatch(evidence.get("payload_sha256") or "")
         and hashlib.sha256(evidence.get("payload_json", "").encode("utf-8")).hexdigest()
             == evidence.get("payload_sha256"),
         "BrowserOriginalPrincipalEvidence")
    need(all(isinstance(items, list) and len(items) == 1 for items in (bindings, lines, allocations))
         and isinstance(receipts, list) and len(receipts) == 2,
         "BrowserSingleSourceAndTwoReceipts")
    binding = bindings[0]; line = lines[0]; allocation = allocations[0]
    need(binding.get("sourcing_event_id") == event["id"]
         and binding.get("source_evidence_id") == evidence["id"]
         and binding.get("purchase_request_id") == request["id"]
         and binding.get("purchase_request_line_id") == request_line["id"]
         and binding.get("sourcing_line_id") == line.get("id")
         and binding.get("allocation_id") == allocation.get("id")
         and binding.get("identity_schema_version") == 2
         # The binding retains the original PR maker; the RFQ evidence/receipts
         # above/below retain the purchasing Buyer. These are distinct identities.
         and uuid(facts["approval"]["submissions"][0].get("maker_subject_id"))
         and binding.get("maker_principal_id") == facts["approval"]["submissions"][0]["maker_subject_id"]
         and binding.get("requester_principal_id") == request.get("requester_principal_id")
         and binding.get("maker_worker_id") is None
         and binding.get("requester_worker_id") is None
         and HEX64.fullmatch(binding.get("principal_identity_sha256") or "")
         and binding.get("request_version") == baseline["requestVersion"]
         and binding.get("submitted_version") == request.get("submitted_version")
         and binding.get("method_id") == event.get("method_id")
         and binding.get("method_revision") == event.get("method_revision")
         and binding.get("submission_id") == facts["approval"]["submissions"][0]["id"]
         and binding.get("approval_outcome_id") == facts["approval"]["outcomes"][0]["id"]
         and binding.get("core_decision_id") == facts["approval"]["decisions"][0]["Id"],
         "BrowserBindingToOriginalApprovedDemand")
    quantity = exact(binding.get("quantity"))
    need(decimal.Decimal("0") < quantity <= decimal.Decimal("2")
         and line.get("sourcing_event_id") == event["id"]
         and line.get("purchase_request_line_id") == request_line["id"]
         and line.get("status") == "Cancelled"
         and exact(line.get("quantity")) == quantity
         and allocation.get("target_type") == "SourcingEvent"
         and allocation.get("target_id") == event["id"]
         and allocation.get("purchase_request_id") == request["id"]
         and allocation.get("purchase_request_line_id") == request_line["id"]
         and allocation.get("status") == "Released"
         and exact(allocation.get("quantity")) == quantity
         and exact(binding.get("estimated_value")) == quantity * exact(binding.get("unit_estimate"))
         and exact(allocation.get("estimated_value")) == exact(binding.get("estimated_value"))
         and exact(request_line.get("approved_quantity")) - exact(request_line.get("allocated_quantity"))
             == decimal.Decimal("2"),
         "BrowserAllocationReleasedAndRemainingExact")
    receipt_by_action = {x.get("operation"): x for x in receipts if isinstance(x, dict)}
    need(len(receipt_by_action) == 2 and set(receipt_by_action) == {"Create", "Cancel"}
         and {receipt_by_action["Create"].get("command_key"), receipt_by_action["Cancel"].get("command_key")}
             == {browser["createCommandKey"], browser["cancelCommandKey"]}
         and receipt_by_action["Create"].get("command_key") == browser["createCommandKey"]
         and receipt_by_action["Cancel"].get("command_key") == browser["cancelCommandKey"],
         "TwoObservedBrowserCommandKeys")
    for operation, recorded in receipt_by_action.items():
        need(recorded.get("sourcing_event_id") == event["id"]
             and recorded.get("tenant_id") == TENANT
             and recorded.get("legal_entity_id") == fixture["companyId"]
             and recorded.get("actor_principal_id") == buyer
             and recorded.get("actor_worker_id") is None
             and recorded.get("response_schema_version") == 2
             and recorded.get("result_version") == (browser["createdVersion"] if operation == "Create" else browser["cancelledVersion"])
             and recorded.get("response_status_code") == (201 if operation == "Create" else 200)
             and HEX64.fullmatch(recorded.get("command_fingerprint") or "")
             and HEX64.fullmatch(recorded.get("response_sha256") or "")
             and hashlib.sha256(recorded.get("response_json", "").encode("utf-8")).hexdigest()
                 == recorded.get("response_sha256"),
             "CurrentActorOwnedBrowserReceipts")
        try:
            reply = json.loads(recorded.get("response_json"))
        except (TypeError, ValueError):
            raise VerificationFailure("BrowserReceiptResponseMalformed") from None
        need(reply.get("eventId") == event["id"]
             and reply.get("status") == ("Draft" if operation == "Create" else "Cancelled"),
             "BrowserReceiptResponseBinding")
    return {"sourceEventCount": len(graphs),
            "currentSchemaVersion": 2, "receiptCount": 2,
            "browserAllocationQuantityExact": str(quantity), "remainingApprovedQuantityExact": "2",
            "originalApprovalGraphSha256": baseline["approvalGraphSha256"],
            "firstApiDraftSha256": baseline["firstApiDraftSha256"],
            "originalSourceBusinessFieldsStable": True,
            "expectedRequestAndLineCASIncrement": 2,
            "supplierPublished": False, "externalFinanceAuthority": False}


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--capture-baseline", action="store_true")
    parser.add_argument("--expected-database", required=True)
    parser.add_argument("--pg-container", required=True, help="Full private PostgreSQL container ID")
    parser.add_argument("--output", type=Path, required=True, help="New protected 0600 result")
    args = parser.parse_args()
    need(bool(DB_NAME.fullmatch(args.expected_database)), "ExpectedPrivateDatabaseRequired")
    fixture_path = Path(os.environ.get("P29_COOKIE_FIXTURE", ""))
    need(str(fixture_path), "ProtectedFixtureRequired")
    fixture, fixture_hash = protected_json(fixture_path)
    private_output(args.output, fixture_path)
    facts = owner_query(args.pg_container, args.expected_database,
        fixture.get("approvedRequestId"), fixture.get("approvedRequestLineId"),
        fixture.get("cancelledDraftEventId"), fixture.get("buyerPrincipalId"))
    if args.capture_baseline:
        result = capture(fixture, fixture_hash, facts, args.expected_database)
        result["privatePgContainerId"] = args.pg_container
        result["privatePgSystemIdentifier"] = SYSTEM_ID
        result["verifierSourceSha256"] = hashlib.sha256(Path(__file__).read_bytes()).hexdigest()
        write_private(args.output, result)
        print("CAPTURED_PRIVATE_P29_BEFORE_BROWSER_ONLY")
        return
    baseline_path = Path(os.environ.get("P29_COOKIE_OWNER_BASELINE", ""))
    browser_path = Path(os.environ.get("P29_COOKIE_BROWSER_RESULT", ""))
    proxy_path = Path(os.environ.get("P29_COOKIE_PROXY_RECEIPT", ""))
    need(str(baseline_path) and str(browser_path) and str(proxy_path),
         "ProtectedBaselineBrowserAndProxyRequired")
    baseline, baseline_hash = protected_json(baseline_path)
    browser, browser_hash = protected_json(browser_path)
    proxy, proxy_hash = protected_json(proxy_path)
    completion_path = Path(fixture.get("completionPath", ""))
    need(str(completion_path) and all(args.output != path for path in
         (fixture_path, baseline_path, browser_path, proxy_path, completion_path))
         and all(path.parent == fixture_path.parent for path in
                 (baseline_path, browser_path, proxy_path, completion_path)),
         "ProtectedEvidencePathSeparation")
    proxy_boundary(proxy, fixture, fixture_hash, args.expected_database)
    completion, completion_hash = protected_json(completion_path)
    need(set(completion) == {"schemaVersion", "challenge", "status"}
         and completion.get("schemaVersion") == 2
         and completion.get("challenge") == fixture.get("challenge")
         and completion.get("status") == "BrowserFinished", "ChallengedCompletionBoundary")
    need(browser.get("fixtureSha256") == fixture_hash, "BrowserFixtureHash")
    result = verify(fixture, fixture_hash, baseline, browser, facts,
                    args.expected_database, args.pg_container)
    write_private(args.output, {"schemaVersion": 1, "status": "PASS_PRIVATE_P29_COOKIE_OWNER_ONLY",
        "database": args.expected_database, "tenantId": TENANT,
        "privatePgContainerId": args.pg_container, "privatePgSystemIdentifier": SYSTEM_ID,
        "fixtureSha256": fixture_hash, "baselineSha256": baseline_hash,
        "browserResultSha256": browser_hash, "proxyReceiptSha256": proxy_hash,
        "completionSha256": completion_hash,
        "verifierSourceSha256": hashlib.sha256(Path(__file__).read_bytes()).hexdigest(),
        "readOnlyConsistentPostgresSnapshot": True, "browserResultUnmodified": True,
        "browserProvedByIndependentOwner": result, "externalFinanceAuthority": False,
        "supplierPublished": False, "nativeIdentityProviderVerified": False})
    print("PASS_PRIVATE_P29_COOKIE_OWNER_ONLY")


if __name__ == "__main__":
    try:
        main()
    except VerificationFailure as error:
        print("P29_OWNER_VERIFIER_FAILED:" + str(error), file=sys.stderr)
        sys.exit(2)
    except Exception:
        print("P29_OWNER_VERIFIER_FAILED:UnexpectedSourceOrDataShape", file=sys.stderr)
        sys.exit(2)
