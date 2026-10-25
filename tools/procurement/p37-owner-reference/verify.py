#!/usr/bin/env python3
"""Independent, read-only owner reconciliation for the future private P37 cohort.

This tool intentionally has no write SQL and makes no independent HTTP-body
fingerprint claim: the native probe does not export the original request bodies.
"""
import argparse
import hashlib
import json
import os
import re
import subprocess
from decimal import Decimal
from pathlib import Path

HERE = Path(__file__).resolve().parent
PLAN = HERE / "plan.INCOMPLETE.json"
DATABASE = "proc_p37_http_return_20261025_05"
NATIVE = Path('/home/amizo/erp-backups/procurement-p37-return-native-http-prep-20261025_run05')
NATIVE_PLAN_SHA = '7ca266c3439f1ce9376b149231c629990873361632b915999d3db12c3642e67f'
NATIVE_RUNNER_SHA = '0fdff061812a27be6e63fbef43219cfde9b71ed5abba09916d7582eac2a2d24f'
PRODUCT_SOURCE = '98274f3b43675d33f649dbfbf884bcfcc1b3d85f'
TOOL_SOURCE = '98274f3b43675d33f649dbfbf884bcfcc1b3d85f'
PG_ID = "10b7cb11af108c3a47ba557c0f903ac73439f88b060e7f7150c08c8fa99778fd"
PG_IMAGE = "sha256:18cfe3ef5e6815560c98237d6216d1e5119702fb0f3894c8785dd58b8bbe5d73"
PG_SYSTEM = "7698678713374847009"
UUID = re.compile(r"[0-9a-f]{8}-(?:[0-9a-f]{4}-){3}[0-9a-f]{12}\Z")
HEX = re.compile(r"[0-9a-f]{64}\Z")
TAGS = ("head", "revisions", "receipts", "source", "fact", "receiving_draft",
        "issued", "po", "po_line", "creator", "audit", "outbox")

def need(condition, code):
    if not condition:
        raise RuntimeError(code)

def sha(data):
    return hashlib.sha256(data).hexdigest()

def read_pinned(path, digest):
    p = Path(path)
    need(p.is_absolute() and p.is_file() and not p.is_symlink() and HEX.fullmatch(digest or ""),
         "missing-or-unpinned-input")
    data = p.read_bytes()
    need(sha(data) == digest, "input-sha-mismatch")
    return json.loads(data)

def rows_sql(tag, table, where="true"):
    return ("SELECT jsonb_build_object('tag','" + tag + "','rows',"
            "COALESCE((SELECT jsonb_agg(to_jsonb(t) ORDER BY t.id) FROM " + table + " t WHERE "
            + where + "),'[]'::jsonb))::text;")

def capture_sql(draft):
    need(UUID.fullmatch(draft), "invalid-draft-id")
    h = "(SELECT id FROM procurement.principal_goods_return_drafts WHERE id='" + draft + "'::uuid)"
    column = lambda name: "(SELECT " + name + " FROM procurement.principal_goods_return_drafts WHERE id=" + h + ")"
    chunks = ["BEGIN ISOLATION LEVEL REPEATABLE READ READ ONLY;",
              "SET LOCAL statement_timeout='15s';", "SET LOCAL lock_timeout='3s';",
              "SET LOCAL TIME ZONE 'UTC';"]
    spec = [
        ("head", "procurement.principal_goods_return_drafts", "id=" + h),
        ("revisions", "procurement.principal_goods_return_revisions", "draft_id=" + h),
        ("receipts", "procurement.principal_goods_return_receipts", "draft_id=" + h),
        ("source", "procurement.principal_receiving_sources", "id=" + column("source_state_id")),
        ("fact", "procurement.principal_receiving_facts", "id=" + column("acceptance_fact_id")),
        ("receiving_draft", "procurement.principal_receiving_drafts", "id=" + column("receipt_id")),
        ("issued", "procurement.principal_order_issued_versions", "id=" + column("issued_version_id")),
        ("po", "procurement.purchase_orders", "id=" + column("purchase_order_id")),
        ("po_line", "procurement.purchase_order_lines", "id=" + column("purchase_order_line_id")),
        ("creator", "hrm.local_users", "id::text=" + column("creator_principal_id")),
        ("audit", "procurement.audit_events", "record_type IN ('PrincipalGoodsReturnDraft',"
         "'PrincipalGoodsReturnRevision','PrincipalGoodsReturnCommandReceipt')"),
        ("outbox", "procurement.outbox_events", "aggregate_type='PrincipalGoodsReturnDraft'"),
    ]
    chunks.extend(rows_sql(*item) for item in spec)
    chunks.append("ROLLBACK;")
    return "\n".join(chunks) + "\n"

def parse_capture(raw):
    lines = raw.decode().splitlines()
    need(len(lines) == len(TAGS), "capture-tag-count")
    parsed = [json.loads(line, parse_float=Decimal) for line in lines]
    need(tuple(x.get("tag") for x in parsed) == TAGS and
         all(isinstance(x.get("rows"), list) for x in parsed), "capture-tags-or-shape")
    return {x["tag"]: x["rows"] for x in parsed}

def one(data, tag):
    rows = data[tag]
    need(len(rows) == 1, "expected-one-" + tag)
    return rows[0]

def exact_decimal(value):
    return Decimal(str(value))

def canonical_number(value):
    """Mirror ProcurementEvidenceEncoding.NormalizeNumber for bounded exact decimals.

    The producer emits a coefficient and exponent even for integer JSON numbers.
    Decimal preserves the base-ten value without Python float conversion.
    """
    number = Decimal(str(value))
    need(number.is_finite(), "unsupported-proposal-number")
    if number.is_zero():
        return "0"
    digits = list(number.copy_abs().as_tuple().digits)
    exponent = number.as_tuple().exponent
    while digits[-1] == 0:
        digits.pop()
        exponent += 1
    coefficient = "".join(str(digit) for digit in digits)
    return ("-" if number.is_signed() else "") + coefficient + "e" + str(exponent)

def proposal_sha(draft, number, quantity, reason, reference, state):
    # The actual .NET producer hashes seven properties in ordinal sorted order.
    # Its canonical JSON encodes numeric schemaVersion/revision as 1e0/Ne0,
    # while quantity is a STRING. These fixture fields are bounded plain ASCII.
    value = {"schemaVersion": 1, "draftId": draft, "revision": number,
             "quantity": quantity, "reason": reason,
             "evidenceReference": reference, "state": state}
    need(all(value[k].isascii() and not any(ch in value[k] for ch in '<>&+\\')
             for k in ("reason", "evidenceReference") if value[k] is not None),
         "unsupported-proposal-text-encoding")
    parts = []
    for name in sorted(value):
        item = value[name]
        encoded = canonical_number(item) if name in ("schemaVersion", "revision") else json.dumps(
            item, separators=(",", ":"), ensure_ascii=True)
        parts.append(json.dumps(name) + ":" + encoded)
    return sha(("{" + ",".join(parts) + "}").encode())

def verify_graph(data, native):
    result = native["principalGoodsReturn"]
    need(native.get("status") == "PASS_PRIVATE_P37_GOODS_RETURN_DRAFT_ONLY" and
         result.get("status") == native["status"] and result.get("revisions") == 3 and
         result.get("originalReceiptCount") == 3 and
         type(native.get('actualHttpRequests')) is int and native['actualHttpRequests'] > 150 and
         native.get('p37ScopedMetadataReadinessVerified') is True and
         native.get('positiveApprovalGraph') is True and
         native.get('currentCoreWorkerlessSourceVerified') is True and
         result.get('currentCoreGrantRevocationDenied') is True and
         result.get('p30AndOtherBusinessRowsUnchanged') is True and
         result.get("physicalReturnDispatched") is False and
         result.get("stockPostingPerformed") is False and
         result.get("financePostingPerformed") is False, "native-result-not-terminal-pass")
    head = one(data, "head")
    source, fact = one(data, "source"), one(data, "fact")
    receiving, issued, po, line = (one(data, x) for x in ("receiving_draft", "issued", "po", "po_line"))
    creator = one(data, "creator")
    revisions, receipts, audit, outbox = (data[x] for x in ("revisions", "receipts", "audit", "outbox"))
    draft = str(result["draftId"])
    need(UUID.fullmatch(draft) and head["id"] == draft and head["schema_version"] == 1 and
         head["state"] == "Cancelled" and head["revision"] == 3 and
         head["creator_worker_alias_id"] is None and
         creator["id"] == head["creator_principal_id"] and creator["worker_id"] is None and
         creator["tenant_id"] == head["tenant_id"] and creator["is_active"] is True,
         "head-or-workerless-creator")
    scope = (head["tenant_id"], head["legal_entity_id"])
    for row in [head, source, fact, receiving, issued, po, line, *revisions, *receipts, *audit, *outbox]:
        need((row["tenant_id"], row["legal_entity_id"]) == scope, "cross-scope-row")
    need(head["source_state_id"] == source["id"] and source["kind"] == "Goods" and
         head["acceptance_fact_id"] == fact["id"] == str(result["acceptedFactId"]) and
         fact["kind"] == "Acceptance" and fact["operation"] == "Accept" and
         fact["source_state_id"] == source["id"] and fact["draft_id"] == receiving["id"] == head["receipt_id"] and
         fact["branch_id"] == source["branch_id"] == head["branch_id"] and
         head["purchase_order_id"] == source["purchase_order_id"] == po["id"] and
         head["purchase_order_line_id"] == source["purchase_order_line_id"] == line["id"] and
         head["issued_version_id"] == source["issued_version_id"] == issued["id"] and
         issued["purchase_order_id"] == po["id"] and line["purchase_order_id"] == po["id"] and
         receiving["purchase_order_id"] == po["id"] and
         head["source_sha256"] == source["source_sha256"] and
         head["acceptance_sha256"] == fact["payload_sha256"] and
         sha(source["source_json"].encode()) == source["source_sha256"] and
         sha(fact["payload_json"].encode()) == fact["payload_sha256"] and
         exact_decimal(head["accepted_fact_quantity"]) == exact_decimal(fact["quantity"]) == Decimal("0.9999"),
         "original-accepted-goods-link")
    need(all(HEX.fullmatch(head[k]) for k in ("source_sha256", "acceptance_sha256")) and
         len(revisions) == len(receipts) == len(outbox) == 3 and len(audit) == 9,
         "evidence-cardinality-or-hash-shape")
    by_revision = {r["revision"]: r for r in revisions}
    by_result = {r["result_revision"]: r for r in receipts}
    need(set(by_revision) == set(by_result) == {1, 2, 3} and
         len(by_revision) == len(revisions) and len(by_result) == len(receipts) and
         head["current_revision_id"] == by_revision[3]["id"], "revision-cursor")
    keys = {1: ("Create", str(result["createKey"]), "Draft", "0.5", 201),
            2: ("Revise", str(result["reviseKey"]), "Draft", "0.75", 200),
            3: ("Cancel", str(result["cancelKey"]), "Cancelled", "0.75", 200)}
    expected_reasons = {1: ("Private proposed goods return", "PRIVATE-RETURN:original"),
                        2: ("Revised private proposed return", "PRIVATE-RETURN:revised"),
                        3: ("Private request abandoned", "PRIVATE-RETURN:revised")}
    actor = head["creator_principal_id"]
    for number, (operation, key, state, quantity, status) in keys.items():
        revision, receipt = by_revision[number], by_result[number]
        need(revision["draft_id"] == receipt["draft_id"] == draft and
             revision["command_key"] == receipt["command_key"] == key and
             revision["actor_principal_id"] == receipt["actor_principal_id"] == actor and
             revision["state"] == state and exact_decimal(revision["quantity"]) == Decimal(quantity) and
             receipt["operation"] == operation and receipt["response_status"] == status and
             receipt["branch_id"] == head["branch_id"] and receipt["schema_version"] == 1 and
             revision["schema_version"] == 1 and
             (revision["reason"], revision["evidence_reference"]) == expected_reasons[number] and
             revision["proposal_sha256"] == proposal_sha(draft, number, quantity,
                 revision["reason"], revision["evidence_reference"], state) and
             HEX.fullmatch(receipt["request_sha256"]),
             "revision-receipt-link")
        response = receipt["response_json"]
        need(sha(response.encode()) == receipt["response_sha256"], "response-raw-sha")
        body = json.loads(response, parse_float=Decimal)
        need(body["id"] == draft and body["version"] == number and body["status"] == state and
             body["receiptId"] == head["receipt_id"] and
             body["acceptanceFactId"] == head["acceptance_fact_id"] and
             body["purchaseOrderId"] == po["id"] and body["purchaseOrderLineId"] == line["id"] and
             exact_decimal(body["quantityExact"]) == Decimal(quantity) and
             exact_decimal(body["acceptedFactQuantityExact"]) == Decimal("0.9999") and
             body["reason"] == revision["reason"] and body["evidenceReference"] == revision["evidence_reference"] and
             body["draftOnly"] is True and all(body[x] is False for x in
                 ("stockChanged", "financeChanged", "supplierNotified")), "response-source-link")
        request = json.loads(receipt["request_json"], parse_float=Decimal)
        need(request["operation"] == operation and
             (operation == "Create" and request["input"]["acceptanceFactId"] == fact["id"] or
              operation != "Create" and request["id"] == draft and
              request["input"]["expectedRevision"] == number - 1) and
             request["input"]["reason"].strip() == revision["reason"], "stored-request-structure")
        if operation != "Cancel":
            need(exact_decimal(request["input"]["quantity"]) == Decimal(quantity) and
                 request["input"].get("evidenceReference") == revision["evidence_reference"],
                 "stored-request-values")
        else:
            need(revision["evidence_reference"] == by_revision[2]["evidence_reference"] and
                 revision["quantity"] == by_revision[2]["quantity"], "cancel-changed-proposal")
        event = [x for x in outbox if x["idempotency_key"] == "goods-return.draft." + key.replace("-", "") + ".v1"]
        need(len(event) == 1 and event[0]["aggregate_id"] == draft and
             event[0]["event_type"] == "procurement.goods_return.draft_" + operation.lower() + ".v1" and
             event[0]["state"] == "Pending" and event[0]["attempts"] == 0 and
             event[0]["published_at"] is None, "outbox-companion")
        payload = json.loads(event[0]["payload_json"]) if isinstance(event[0]["payload_json"], str) else event[0]["payload_json"]
        need(payload == {"id": draft, "operation": operation, "commandKey": key,
                         "revision": number, "actorPrincipalId": actor, "approved": False,
                         "dispatched": False, "stockChanged": False, "financeChanged": False},
             "outbox-payload")
    seen = {}
    for row in audit:
        need(row["actor_subject_id"] == actor and row["created_by"] == actor,
             "audit-actor")
        signature = (row["record_type"], row["record_id"], row["action"])
        seen[signature] = seen.get(signature, 0) + 1
    need(seen.get(("PrincipalGoodsReturnDraft", draft, "create")) == 1 and
         seen.get(("PrincipalGoodsReturnDraft", draft, "update")) == 2 and
         all(seen.get(("PrincipalGoodsReturnRevision", r["id"], "create")) == 1 for r in revisions) and
         all(seen.get(("PrincipalGoodsReturnCommandReceipt", r["id"], "create")) == 1 for r in receipts) and
         sum(seen.values()) == 9, "audit-companions")
    return {"draftRows": 1, "revisionRows": 3, "receiptRows": 3, "auditRows": 9,
            "outboxRows": 3, "responseRawShaVerified": True,
            "proposalSnapshotShaIndependentlyRecomputed": True,
            "requestCanonicalShaIndependentlyRecomputed": False,
            "nonP37RowsIndependentlyBaselined": False,
            "normalCookieOrNarrowP37RoleVerified": False}

def admit(plan, plan_sha):
    need(sha(PLAN.read_bytes()) == plan_sha and plan["status"] == "REVIEWED_READY_FOR_NATIVE_RESULT",
         "unreviewed-or-incomplete-plan")
    need(plan["database"] == DATABASE and plan["postgresContainerId"] == PG_ID and
         plan["postgresImageId"] == PG_IMAGE and plan["postgresSystemId"] == PG_SYSTEM,
         "wrong-private-owner-target")
    need(plan['nativePlanSha256'] == NATIVE_PLAN_SHA and
         plan['nativeRunnerSha256'] == NATIVE_RUNNER_SHA and
         sha((NATIVE/'plan.INCOMPLETE.json').read_bytes()) == NATIVE_PLAN_SHA and
         sha((NATIVE/'run.py').read_bytes()) == NATIVE_RUNNER_SHA and
         plan['productSourceCommit'] == PRODUCT_SOURCE and
         plan['toolSourceCommit'] == TOOL_SOURCE,
         'reviewed-native-source-or-profile-changed')
    need(sha(Path(__file__).read_bytes()) == plan["verifierSha256"], "verifier-source-changed")
    need(plan["nativeResultSha256"] and plan["nativeRunResultSha256"], "future-native-pins-missing")
    need(plan['nativeResult'] == str(NATIVE/'result.private.json') and
         plan['nativeRunResult'] == str(NATIVE/'native-run-result.private.json'),
         'wrong-native-result-path')
    native = read_pinned(plan["nativeResult"], plan["nativeResultSha256"])
    run = read_pinned(plan["nativeRunResult"], plan["nativeRunResultSha256"])
    need(run.get("status") == "PASS_PRIVATE_P37_GOODS_RETURN_DRAFT_ONLY" and
         native.get("status") == run["status"] and
         native.get('p37ScopedMetadataReadinessVerified') is True and
         run.get("privateFreshDatabase") == plan["database"] and
         run.get("independentOwnerVerified") is False,
         "native-not-terminal-or-wrong-db")
    return native

def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--reviewed-plan-sha256", required=True)
    args = parser.parse_args()
    os.umask(0o077)
    plan = json.loads(PLAN.read_text())
    native = admit(plan, args.reviewed_plan_sha256)  # All native pins before Docker/SQL.
    need(not any((HERE / name).exists() for name in
                 ("capture-1.private.jsonl", "capture-2.private.jsonl", "result.private.json")),
         "owner-output-already-exists")
    pg = json.loads(subprocess.check_output(["docker", "inspect", plan["postgresContainerId"]], timeout=20))[0]
    need(pg["Id"] == PG_ID and pg["Name"] == "/procurement-approval-rehearsal-postgres" and
         pg["Image"] == PG_IMAGE and
         pg["State"]["Running"] is True and pg["HostConfig"]["NetworkMode"] == "none" and
         not pg["HostConfig"].get("PortBindings"), "wrong-private-container")
    def query(sql):
        result = subprocess.run(["docker", "exec", "-i", plan["postgresContainerId"], "psql", "-X", "-qAt",
            "-v", "ON_ERROR_STOP=1", "-U", "postgres", "-d", plan["database"]],
            input=sql.encode(), capture_output=True, timeout=90)
        need(result.returncode == 0 and not result.stderr, "read-only-sql-failed")
        return result.stdout
    identity = query("BEGIN READ ONLY; SELECT system_identifier::text FROM pg_control_system(); ROLLBACK;\n")
    need(identity.decode().strip() == PG_SYSTEM, "wrong-private-system")
    sql = capture_sql(str(native["principalGoodsReturn"]["draftId"]))
    captures = []
    for n in (1, 2):
        raw = query(sql)
        data = parse_capture(raw)
        destination = HERE / f"capture-{n}.private.jsonl"
        fd = os.open(destination, os.O_WRONLY | os.O_CREAT | os.O_EXCL | os.O_NOFOLLOW, 0o600)
        with os.fdopen(fd, "wb") as output:
            output.write(raw)
        captures.append((sha(raw), data))
    need(captures[0][0] == captures[1][0], "independent-read-captures-disagree")
    proof = verify_graph(captures[0][1], native)
    result = {"status": "PASS_PRIVATE_P37_READ_ONLY_OWNER_GRAPH", "database": plan["database"],
              "nativeResultSha256": plan["nativeResultSha256"], "captureSha256": captures[0][0], **proof}
    fd = os.open(HERE / "result.private.json", os.O_WRONLY | os.O_CREAT | os.O_EXCL | os.O_NOFOLLOW, 0o600)
    with os.fdopen(fd, "w") as output:
        output.write(json.dumps(result, sort_keys=True, indent=2) + "\n")
    print("PASS_PRIVATE_P37_READ_ONLY_OWNER_GRAPH")

if __name__ == "__main__":
    main()
