#!/usr/bin/env python3
"""Read-only, private PostgreSQL owner proof for the P30 PO cookie journey.

Run capture after the protected fixture is written and before the browser starts.
Run verify after the browser result is written and all synthetic sessions are revoked.
The browser completion file, proxy receipt, and UI assertions are controls, never
substitutes for the original owning rows queried here.
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

SYSTEM = "7698678713374847009"
TENANT = "p25-private-http"
GUID = re.compile(r"[0-9a-f]{8}(?:-[0-9a-f]{4}){3}-[0-9a-f]{12}\Z")
HEX = re.compile(r"[0-9a-f]{64}\Z")
DB = re.compile(r"proc_p30_http_po_[a-z0-9_]{1,40}\Z")
IDENT = re.compile(r"[A-Za-z_][A-Za-z_0-9]*\Z")


class Failed(Exception):
    pass


def require(value, code):
    if not value:
        raise Failed(code)


def guid(value):
    return isinstance(value, str) and bool(GUID.fullmatch(value)) and value != "00000000-0000-0000-0000-000000000000"


def digest(value):
    return hashlib.sha256(json.dumps(value, sort_keys=True, ensure_ascii=False,
        separators=(",", ":"), default=str).encode()).hexdigest()


def protected(path, limit=65536):
    require(path.is_absolute() and path.is_file() and not path.is_symlink()
            and path.resolve(strict=True) == path, "ProtectedInputMissing")
    require(stat.S_IMODE(path.stat().st_mode) == 0o600 and 0 < path.stat().st_size <= limit,
            "ProtectedInputModeOrSize")
    raw = path.read_bytes()
    try:
        value = json.loads(raw, parse_float=decimal.Decimal)
    except (ValueError, UnicodeError):
        raise Failed("ProtectedInputMalformed") from None
    require(isinstance(value, dict), "ProtectedInputShape")
    return value, hashlib.sha256(raw).hexdigest()


def output_path(path, fixture):
    require(path.is_absolute() and path.parent == fixture.parent and path != fixture
            and path.parent.is_dir() and not path.parent.is_symlink()
            and path.parent.resolve(strict=True) == path.parent
            and stat.S_IMODE(path.parent.stat().st_mode) == 0o700
            and not path.exists() and not path.is_symlink(), "NewPrivateOutputRequired")


def exact_evidence_json(value):
    """Keep PostgreSQL decimal values exact in protected owner snapshots."""
    if isinstance(value, decimal.Decimal):
        require(value.is_finite(), "OwnerEvidenceNumberInvalid")
        return str(value)
    if isinstance(value, dict):
        require(all(isinstance(k, str) for k in value), "OwnerEvidenceKeyInvalid")
        return "{" + ",".join(json.dumps(k, ensure_ascii=True) + ":"
            + exact_evidence_json(value[k]) for k in sorted(value)) + "}"
    if isinstance(value, (list, tuple)):
        return "[" + ",".join(exact_evidence_json(x) for x in value) + "]"
    require(value is None or isinstance(value, (str, bool, int)), "OwnerEvidenceShapeInvalid")
    return json.dumps(value, ensure_ascii=True, allow_nan=False)


def write(path, value):
    # Render before exclusive creation: unsupported values must not leave a
    # partial baseline that could be mistaken for a complete owner snapshot.
    encoded = exact_evidence_json(value) + "\n"
    fd = os.open(path, os.O_WRONLY | os.O_CREAT | os.O_EXCL | os.O_NOFOLLOW, 0o600)
    with os.fdopen(fd, "w", encoding="utf-8") as stream:
        stream.write(encoded)
    require(stat.S_IMODE(path.stat().st_mode) == 0o600, "OutputMode")


def container_guard(container):
    require(isinstance(container, str) and HEX.fullmatch(container), "PrivateContainerIdRequired")
    try:
        value = json.loads(subprocess.run(["docker", "inspect", container], check=True,
            capture_output=True, text=True, timeout=15).stdout)[0]
    except (OSError, ValueError, subprocess.SubprocessError, IndexError):
        raise Failed("PrivateContainerInspectionFailed") from None
    require(value.get("Id") == container and value.get("State", {}).get("Running") is True
            and value.get("HostConfig", {}).get("NetworkMode") == "none"
            and not value.get("HostConfig", {}).get("PortBindings")
            and any(m.get("Destination") == "/var/run/postgresql" for m in value.get("Mounts", [])),
            "PrivateSocketContainerChanged")


def query(container, database, sql):
    try:
        run = subprocess.run(["docker", "exec", "-i", "-e",
            "PGOPTIONS=-c default_transaction_read_only=on -c statement_timeout=30000 -c lock_timeout=3000",
            container, "psql", "-X", "-qAt", "-U", "postgres", "-d", database,
            "-v", "ON_ERROR_STOP=1", "-v", "VERBOSITY=terse"], input=sql,
            capture_output=True, text=True, timeout=45, check=True)
        return [json.loads(line, parse_float=decimal.Decimal) for line in run.stdout.splitlines()]
    except (OSError, ValueError, subprocess.SubprocessError):
        raise Failed("OwnerReadOnlyQueryFailed") from None


def native_orders_for_phase(orders, fixture, original_ids=None, successor=None):
    if original_ids is None:
        require(successor is None and len(orders) == 3, "ThreeNativeOrdersRequired")
        native = orders
    else:
        ids = set(original_ids)
        require(len(original_ids) == len(ids) == 3 and all(guid(x) for x in ids)
                and guid(successor) and successor not in ids
                and len(orders) == 4 and {x.get("id") for x in orders} == ids | {successor},
                "ExactBrowserSuccessorRequired")
        native = [x for x in orders if x.get("id") in ids]
    require(any(x.get("id") == fixture["issuedOrderId"] and x.get("status") == "Issued"
                for x in native), "OriginalIssuedOrderRequired")
    return native


def owner_snapshot(container, database, fixture, original_ids=None, successor=None):
    container_guard(container)
    head = query(container, database, """BEGIN TRANSACTION ISOLATION LEVEL REPEATABLE READ READ ONLY;
SELECT jsonb_build_object('database',current_database(),
 'system',(SELECT system_identifier::text FROM pg_control_system()),
 'tcp',inet_server_addr(),'readOnly',current_setting('default_transaction_read_only'),
 'version',current_setting('server_version_num')::integer,
 'tables',(SELECT jsonb_agg(jsonb_build_object('schema',schemaname,'name',tablename)
   ORDER BY schemaname,tablename) FROM pg_tables
   WHERE (schemaname IN ('procurement','core_approval')
      OR (schemaname='hrm' AND tablename IN ('local_sessions','local_users',
        'hr_user_branch_assignments','tenant_role_assignments',
        'work_locations','legal_entities')))
      AND tablename !~ '^__'));
ROLLBACK;""")
    require(len(head) == 1, "OwnerHeaderCount")
    meta = head[0]
    require(meta.get("database") == database and meta.get("system") == SYSTEM
            and meta.get("tcp") is None and meta.get("readOnly") == "on"
            and 170000 <= meta.get("version", 0) < 180000, "PrivatePostgresBoundary")
    tables = meta.get("tables")
    require(isinstance(tables, list) and len(tables) >= 50, "OwningTableCatalogMissing")
    names = [(x.get("schema"), x.get("name")) for x in tables]
    require(all(s in ("procurement", "core_approval", "hrm") and IDENT.fullmatch(s)
                and isinstance(t, str) and IDENT.fullmatch(t) for s, t in names)
            and len(names) == len(set(names))
            and {("procurement", x) for x in ("purchase_orders", "purchase_order_lines",
                "principal_order_source_bindings", "principal_order_draft_receipts",
                "purchase_request_lines", "purchase_request_allocations", "audit_events", "outbox_events")}.issubset(names)
            and {("hrm", x) for x in ("local_sessions", "local_users",
                "hr_user_branch_assignments", "tenant_role_assignments",
                "work_locations", "legal_entities")}.issubset(names), "OwningTableCatalogChanged")
    # All source and successor business rows are read in one repeatable-read
    # transaction. The protected baseline retains row digests, not supplier or
    # authentication data. Selected business rows below retain only needed facts.
    statements = ["BEGIN TRANSACTION ISOLATION LEVEL REPEATABLE READ READ ONLY;"]
    for schema, table in names:
        statements.append(f"SELECT jsonb_build_object('table','{schema}.{table}',"
            f"'rows',(SELECT coalesce(jsonb_agg(to_jsonb(t) ORDER BY to_jsonb(t)::text),'[]'::jsonb)"
            f" FROM \"{schema}\".\"{table}\" t));")
    statements.append("ROLLBACK;")
    result = query(container, database, "\n".join(statements))
    require(len(result) == len(names), "OwningTableSnapshotCount")
    rows = {}
    for item, pair in zip(result, names):
        key = ".".join(pair)
        require(item.get("table") == key and isinstance(item.get("rows"), list), "OwningTableSnapshotShape")
        rows[key] = item["rows"]
    request = fixture["approvedRequestId"]
    line = fixture["approvedRequestLineId"]
    buyer = fixture["buyerPrincipalId"]

    def one(table, field, value):
        found = [x for x in rows["procurement." + table] if x.get(field) == value]
        require(len(found) == 1, "OwningRowMissingOrDuplicate")
        return found[0]

    request_row = one("purchase_requests", "id", request)
    line_row = one("purchase_request_lines", "id", line)
    require(request_row.get("tenant_id") == TENANT and request_row.get("legal_entity_id") == fixture["companyId"]
            and request_row.get("branch_ref") == fixture["branchId"]
            and request_row.get("status") == "Approved"
            and line_row.get("purchase_request_id") == request, "ApprovedOwnerSourceInvalid")
    originals = native_orders_for_phase(rows["procurement.purchase_orders"], fixture,
        original_ids, successor)
    buyer_sessions = [x for x in rows["hrm.local_sessions"] if x.get("local_user_id") == buyer]
    # Full row hashes for every Procurement and Core Approval table establish
    # unchanged legacy and pre-existing principal business history.
    login_fields = {"last_login_at", "failed_login_count", "locked_until", "updated_at", "version"}
    def stable(name, row):
        # Real login may update these operational fields. Its role, tenant,
        # branch, active flag, credential and other identity data remain pinned.
        return {k: v for k, v in row.items() if k not in login_fields} if name == "hrm.local_users" else row
    hashes = {name: {x.get("id", x.get("Id")): digest(stable(name, x)) for x in group} for name, group in rows.items()
        if name != "hrm.local_sessions"}
    require(all(len(value) == len(rows[name]) for name, value in hashes.items()), "DuplicateBusinessRowId")
    return {"schemaVersion": 3, "database": database, "system": SYSTEM,
        "tableDigestRows": hashes, "request": request_row, "sourceLine": line_row,
        "nativeOrderIds": sorted(x["id"] for x in originals),
        "buyerSessionIds": sorted(x["id"] for x in buyer_sessions),
        "buyerSessions": [{"id": x["id"], "revokedAt": x.get("revoked_at"),
            "expiresAt": x.get("expires_at")} for x in buyer_sessions],
        "selectedRows": {name: rows[name] for name in (
            "procurement.purchase_orders", "procurement.purchase_order_lines",
            "procurement.principal_order_source_bindings", "procurement.principal_order_draft_receipts",
            "procurement.purchase_request_allocations", "procurement.audit_events", "procurement.outbox_events")},
        "sessionRows": [{"id": x["id"], "revoked_at": x.get("revoked_at"),
            "expires_at": x.get("expires_at")} for x in buyer_sessions]}


def fixture_boundary(fixture, proxy, fixture_sha, expected):
    require(fixture.get("schemaVersion") == 3 and fixture.get("controlPlaneOnly") is True
            and fixture.get("browserAuthenticationRequired") == "NORMAL_LOCAL_PASSWORD_AND_SESSION_COOKIE"
            and fixture.get("database") == expected and fixture.get("tenantId") == TENANT
            and DB.fullmatch(expected), "FixtureBoundary")
    require(all(guid(fixture.get(x)) for x in ("companyId", "branchId", "buyerPrincipalId",
            "approvedRequestId", "approvedRequestLineId", "supplierId", "issuedOrderId"))
            and HEX.fullmatch(fixture.get("challenge", "")), "FixtureIdentifiers")
    require(proxy.get("schemaVersion") == 3 and proxy.get("transport") == "normal-local-cookie-https-v1"
            and proxy.get("fixtureSha256") == fixture_sha and proxy.get("database") == expected
            and proxy.get("apiUrl") == fixture.get("apiUrl")
            and proxy.get("injectsIdentityHeaders") is False
            and proxy.get("mocksBusinessResponses") is False
            and proxy.get("browserAcceptanceVerified") is False, "NormalCookieProxyBoundary")
    parsed = urlparse(proxy.get("origin", ""))
    require(parsed.scheme == "https" and parsed.hostname in ("127.0.0.1", "localhost")
            and parsed.port and not parsed.username and not parsed.password, "ProxyLoopbackBoundary")


def exact(value):
    try:
        return decimal.Decimal(str(value))
    except (decimal.InvalidOperation, TypeError, ValueError):
        raise Failed("ExactQuantityInvalid") from None


def canonical_json(value):
    """The bounded System.Text.Json/PolicyJsonDigest v2 command subset.

    Numeric spellings are normalized as exact decimal coefficients/exponents.
    The browser PO form uses simple ASCII labels. Other strings fail closed
    because its serializer escape rules must be reviewed before adding them.
    """
    if value is None:
        return "null"
    if value is True:
        return "true"
    if value is False:
        return "false"
    if isinstance(value, (decimal.Decimal, int)) and not isinstance(value, bool):
        number = decimal.Decimal(value)
        require(number.is_finite(), "CommandNumberInvalid")
        if number == 0:
            return "0"
        digits = "".join(str(d) for d in number.as_tuple().digits).lstrip("0")
        exponent = number.as_tuple().exponent
        while digits.endswith("0"):
            digits = digits[:-1]
            exponent += 1
        return ("-" if number.is_signed() else "") + digits + "e" + str(exponent)
    if isinstance(value, str):
        require(all(32 <= ord(c) <= 126 and c not in "<>&'\\\"" for c in value),
                "CommandStringCanonicalizationUnsupported")
        return json.dumps(value, ensure_ascii=True, separators=(",", ":"))
    if isinstance(value, list):
        return "[" + ",".join(canonical_json(x) for x in value) + "]"
    require(isinstance(value, dict) and all(isinstance(k, str) for k in value), "CommandShapeInvalid")
    return "{" + ",".join(canonical_json(k) + ":" + canonical_json(value[k])
        for k in sorted(value)) + "}"


def command_fingerprints(browser, fixture, order, pin):
    try:
        create = json.loads(browser["createRequestBody"], parse_float=decimal.Decimal,
                            parse_int=decimal.Decimal)
        cancel = json.loads(browser["cancelRequestBody"], parse_float=decimal.Decimal,
                            parse_int=decimal.Decimal)
    except (ValueError, TypeError, KeyError):
        raise Failed("CapturedCommandBodiesMissing") from None
    require(isinstance(create, dict) and set(create) == {"draft", "sources"}
            and isinstance(cancel, dict) and set(cancel) == {
                "expectedOrderVersion", "originalDirectSourceSha256", "reason"},
            "CapturedCommandBodyShape")
    draft = create["draft"]
    sources = create["sources"]
    require(isinstance(draft, dict) and isinstance(sources, list) and len(sources) == 1
            and isinstance(draft.get("lines"), list) and len(draft["lines"]) == 1
            and draft.get("supplierId") == fixture["supplierId"]
            and sources[0].get("purchaseRequestId") == fixture["approvedRequestId"]
            and sources[0].get("purchaseRequestLineId") == fixture["approvedRequestLineId"]
            and sources[0].get("draftLineIndex") == 0
            and exact(sources[0].get("expectedRequestVersion")) == exact(pin["request_version"]),
            "CapturedCreateSourceMismatch")
    draft_keys = ("supplierId", "currencyCode", "shipToRef", "billToRef", "paymentTermRef",
                  "expectedDeliveryOn", "lines")
    line_keys = ("id", "description", "unitCode", "quantity", "unitPrice", "taxAmount",
                 "costCenterRef", "projectRef", "expectedDeliveryOn")
    source_keys = ("draftLineIndex", "purchaseRequestId", "purchaseRequestLineId", "expectedRequestVersion")
    require(set(draft).issubset(draft_keys) and set(draft["lines"][0]).issubset(line_keys)
            and set(sources[0]) == set(source_keys), "CapturedCreateFieldsUnexpected")
    hydrated = {k: draft.get(k) for k in draft_keys if k != "lines"}
    hydrated["lines"] = [{k: item.get(k, decimal.Decimal(0) if k in (
        "quantity", "unitPrice", "taxAmount") else None) for k in line_keys}
        for item in draft["lines"]]
    create_value = {"schemaVersion": 2, "operation": "Create",
        "actor": fixture["buyerPrincipalId"], "draft": hydrated,
        "sources": [{k: sources[0][k] for k in source_keys}]}
    require(exact(cancel["expectedOrderVersion"]) == exact(browser["createdVersion"])
            and cancel["originalDirectSourceSha256"] == order["original_direct_source_sha256"]
            and isinstance(cancel["reason"], str) and cancel["reason"].strip()
            and cancel["reason"].strip() == cancel["reason"], "CapturedCancelCASMismatch")
    cancel_value = {"schemaVersion": 2, "operation": "Cancel", "id": browser["orderId"],
        "expectedOrderVersion": cancel["expectedOrderVersion"],
        "originalDirectSourceSha256": cancel["originalDirectSourceSha256"],
        "reason": cancel["reason"]}
    return {"Create": hashlib.sha256(canonical_json(create_value).encode()).hexdigest(),
            "Cancel": hashlib.sha256(canonical_json(cancel_value).encode()).hexdigest()}


def final_check(fixture, baseline, final, browser, fixture_sha):
    require(browser.get("schemaVersion") == 3
            and browser.get("status") == "PASS_NORMAL_COOKIE_P30_PO_INTERNAL_DRAFT_UI_ONLY"
            and browser.get("fixtureSha256") == fixture_sha
            and browser.get("database") == fixture["database"]
            and browser.get("challenge") == fixture["challenge"]
            and browser.get("buyerPrincipalId") == fixture["buyerPrincipalId"]
            and browser.get("approvedRequestLineId") == fixture["approvedRequestLineId"]
            and browser.get("supplierId") == fixture["supplierId"]
            and browser.get("principalPoInternalDraftVerified") is True
            and browser.get("supplierSendPerformed") is False
            and browser.get("financePostingPerformed") is False
            and browser.get("productionTouched") is False,
            "BrowserResultNotComplete")
    po = browser.get("orderId")
    create_key = browser.get("createCommandKey")
    cancel_key = browser.get("cancelCommandKey")
    require(all(guid(x) for x in (po, create_key, cancel_key))
            and create_key != cancel_key and po not in baseline["nativeOrderIds"], "BrowserCommandIdentity")
    before = baseline["tableDigestRows"]
    after = final["tableDigestRows"]
    require(set(before) == set(after), "OwningTableSetChanged")
    changed = {"procurement.purchase_requests", "procurement.purchase_request_lines",
        "procurement.purchase_request_allocations", "procurement.purchase_orders",
        "procurement.purchase_order_lines", "procurement.principal_order_source_bindings",
        "procurement.principal_order_draft_receipts", "procurement.audit_events",
        "procurement.outbox_events"}
    for table in before:
        if table not in changed:
            require(before[table] == after[table], "UnrelatedBusinessHistoryChanged")
    for table in changed:
        require(set(before[table]).issubset(after[table]), "ExistingBusinessRowRemoved")
        if table not in ("procurement.purchase_requests", "procurement.purchase_request_lines"):
            require(all(after[table][row_id] == row_hash for row_id, row_hash in before[table].items()),
                    "ExistingBusinessRowChanged")
    require(baseline["nativeOrderIds"] == sorted(x["id"] for x in final["selectedRows"]["procurement.purchase_orders"] if x["id"] != po)
            and all(before["procurement.purchase_orders"][oid] == after["procurement.purchase_orders"][oid]
                for oid in baseline["nativeOrderIds"]), "OriginalNativeOrdersChanged")
    require(set(after["procurement.purchase_orders"]) - set(before["procurement.purchase_orders"]) == {po},
            "ExactlyOneBrowserOrder")
    order = next(x for x in final["selectedRows"]["procurement.purchase_orders"] if x["id"] == po)
    require(order.get("principal_draft_schema_version") == 2 and order.get("status") == "Cancelled"
            and order.get("buyer_subject_id") == fixture["buyerPrincipalId"]
            and order.get("supplier_id") == fixture["supplierId"]
            and order.get("branch_ref") == fixture["branchId"]
            and order.get("legal_entity_id") == fixture["companyId"]
            and order.get("current_revision") == 0
            and HEX.fullmatch(order.get("original_direct_source_sha256", ""))
            and order.get("closed_at") is not None and order.get("issued_at") is None
            and order.get("finance_commitment_ref") is None
            and order.get("version") == browser.get("cancelledVersion"), "CancelledDraftOwnerFacts")
    require(browser.get("createdVersion") == order.get("version") - 1, "ExactOrderCASProgression")
    lines = [x for x in final["selectedRows"]["procurement.purchase_order_lines"] if x.get("purchase_order_id") == po]
    pins = [x for x in final["selectedRows"]["procurement.principal_order_source_bindings"] if x.get("purchase_order_id") == po]
    allocations = [x for x in final["selectedRows"]["procurement.purchase_request_allocations"] if x.get("target_id") == po]
    require(len(lines) == len(pins) == len(allocations) == 1, "OneOriginalSourceLine")
    line, pin, allocation = lines[0], pins[0], allocations[0]
    require(line.get("purchase_order_id") == po and line.get("purchase_request_line_id") == fixture["approvedRequestLineId"]
            and pin.get("purchase_order_line_id") == line.get("id")
            and pin.get("allocation_id") == allocation.get("id")
            and pin.get("purchase_request_id") == fixture["approvedRequestId"]
            and pin.get("purchase_request_line_id") == fixture["approvedRequestLineId"]
            and pin.get("buyer_principal_id") == fixture["buyerPrincipalId"]
            and pin.get("branch_id") == fixture["branchId"]
            and pin.get("original_direct_source_sha256") == order["original_direct_source_sha256"]
            and pin.get("schema_version") == 2
            and all(guid(pin.get(name)) for name in ("submission_id", "approval_outcome_id",
                "core_approval_case_id", "core_decision_id", "finance_snapshot_id", "method_id",
                "requester_principal_id"))
            and all(HEX.fullmatch(pin.get(name, "")) for name in (
                "request_snapshot_sha256", "policy_decision_sha256", "policy_rules_sha256",
                "finance_snapshot_sha256", "principal_identity_sha256"))
            and allocation.get("status") == "Released"
            and allocation.get("target_type") == "PurchaseOrder"
            and allocation.get("purchase_request_line_id") == fixture["approvedRequestLineId"]
            and exact(allocation.get("quantity")) == exact(pin.get("quantity")) == exact(line.get("quantity")) == 1,
            "OriginalSourceAllocationRelease")
    require(set(after["procurement.purchase_order_lines"]) - set(before["procurement.purchase_order_lines"]) == {line["id"]}
            and set(after["procurement.principal_order_source_bindings"]) - set(before["procurement.principal_order_source_bindings"]) == {pin["id"]}
            and set(after["procurement.purchase_request_allocations"]) - set(before["procurement.purchase_request_allocations"]) == {allocation["id"]},
            "UnexpectedCommercialSuccessors")
    require(exact(baseline["sourceLine"]["allocated_quantity"]) == exact(final["sourceLine"]["allocated_quantity"]) == 0
            and exact(baseline["sourceLine"]["ordered_quantity"]) == exact(final["sourceLine"]["ordered_quantity"]) == 1
            and exact(baseline["sourceLine"]["approved_quantity"]) == exact(final["sourceLine"]["approved_quantity"]) == 2
            and exact(final["sourceLine"]["cancelled_quantity"]) == 0
            and exact(final["sourceLine"]["approved_quantity"]) - exact(final["sourceLine"]["allocated_quantity"])
                - exact(final["sourceLine"]["ordered_quantity"]) - exact(final["sourceLine"]["cancelled_quantity"]) == 1
            and exact(final["sourceLine"]["requested_quantity"]) == 2
            and final["sourceLine"]["version"] == baseline["sourceLine"]["version"] + 2,
            "ApprovedDemandNotRestored")
    for field in ("status", "approval_state", "branch_ref", "legal_entity_id"):
        require(final["request"].get(field) == baseline["request"].get(field), "ApprovedRequestChanged")
    for field in baseline["request"]:
        if field not in ("version", "updated_at", "updated_by"):
            require(final["request"].get(field) == baseline["request"][field], "ApprovedRequestBusinessChanged")
    require(final["sourceLine"].get("route") == "DirectPurchase", "SourceRouteNotRecorded")
    for field in baseline["sourceLine"]:
        if field not in ("version", "updated_at", "updated_by", "allocated_quantity", "route"):
            require(final["sourceLine"].get(field) == baseline["sourceLine"][field], "SourceLineBusinessChanged")
    require(set(after["procurement.purchase_request_lines"]) == set(before["procurement.purchase_request_lines"])
            and set(after["procurement.purchase_requests"]) == set(before["procurement.purchase_requests"]),
            "OriginalDemandRowSetChanged")
    for row_id, row_hash in before["procurement.purchase_request_lines"].items():
        if row_id != fixture["approvedRequestLineId"]:
            require(after["procurement.purchase_request_lines"].get(row_id) == row_hash, "OtherDemandLineChanged")
    for row_id, row_hash in before["procurement.purchase_requests"].items():
        if row_id != fixture["approvedRequestId"]:
            require(after["procurement.purchase_requests"].get(row_id) == row_hash, "OtherRequestChanged")
    receipts = [x for x in final["selectedRows"]["procurement.principal_order_draft_receipts"] if x.get("purchase_order_id") == po]
    fingerprints = command_fingerprints(browser, fixture, order, pin)
    require(len(receipts) == 2 and {x.get("operation") for x in receipts} == {"Create", "Cancel"}
            and {x.get("command_key") for x in receipts} == {create_key, cancel_key}
            and set(after["procurement.principal_order_draft_receipts"]) - set(before["procurement.principal_order_draft_receipts"]) == {x["id"] for x in receipts},
            "ExactBrowserCommandReceipts")
    for receipt in receipts:
        expected_key = create_key if receipt["operation"] == "Create" else cancel_key
        expected_status = 201 if receipt["operation"] == "Create" else 200
        expected_version = browser["createdVersion"] if receipt["operation"] == "Create" else browser["cancelledVersion"]
        require(receipt.get("command_key") == expected_key
                and receipt.get("actor_principal_id") == fixture["buyerPrincipalId"]
                and receipt.get("tenant_id") == TENANT and receipt.get("legal_entity_id") == fixture["companyId"]
                and receipt.get("response_status") == expected_status
                and receipt.get("result_version") == expected_version
                and receipt.get("command_sha256") == fingerprints[receipt["operation"]]
                and receipt.get("response_sha256") == hashlib.sha256(receipt["response_json"].encode()).hexdigest(),
                "PersistedCommandReceiptMismatch")
        response = json.loads(receipt["response_json"])
        require(response.get("orderId") == po and response.get("status") == ("Draft" if expected_status == 201 else "Cancelled")
                and response.get("version") == expected_version and response.get("commandKey") == expected_key
                and response.get("supplierSendPerformed") is False
                and response.get("financeReservationChanged") is False,
                "StoredCommandResponseMismatch")
    audit = [x for x in final["selectedRows"]["procurement.audit_events"] if x.get("record_id") == po]
    outbox = [x for x in final["selectedRows"]["procurement.outbox_events"] if x.get("aggregate_id") == po]
    require(len(audit) == len(outbox) == 2
            and {x.get("action") for x in audit} == {"PrincipalOrderCreate", "PrincipalOrderCancel"}
            and {x.get("event_type") for x in outbox} == {
                "purchase_order.internal_draft_reserved.v2", "purchase_order.internal_draft_cancelled.v2"}
            and all(x.get("actor_subject_id") == fixture["buyerPrincipalId"] for x in audit)
            and set(after["procurement.audit_events"]) - set(before["procurement.audit_events"]) == {x["id"] for x in audit}
            and set(after["procurement.outbox_events"]) - set(before["procurement.outbox_events"]) == {x["id"] for x in outbox},
            "ExactAuditOutboxPair")
    for row in outbox:
        require(row.get("aggregate_type") == "PurchaseOrder" and row.get("event_version") == 1
                and row.get("idempotency_key") in {f"po.principal.Create.{create_key.replace('-', '')}.v2",
                    f"po.principal.Cancel.{cancel_key.replace('-', '')}.v2"}, "OutboxCommandBinding")
    # Browser logout is not inferred from context.close(). A separate owner
    # check requires each newly created real local session to be revoked.
    old_sessions = set(baseline["buyerSessionIds"])
    new_sessions = [x for x in final["sessionRows"] if x["id"] not in old_sessions]
    require(new_sessions and all(x.get("revoked_at") is not None for x in new_sessions),
            "BuyerLocalSessionsNotRevoked")
    return {"schemaVersion": 3, "status": "PASS_P30_PO_OWNER_FACTS",
        "database": fixture["database"], "system": SYSTEM,
        "fixtureSha256": fixture_sha, "orderId": po,
        "sourceAllocationRestored": True, "originalThreeOrdersUnchanged": True,
        "exactCreateCancelReceipts": True, "exactCommandFingerprintsVerified": True,
        "auditOutboxBound": True,
        "realLocalSessionsRevoked": True, "productionTouched": False}


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("mode", choices=("capture", "verify", "check-baseline"))
    parser.add_argument("--fixture", type=Path, required=True)
    parser.add_argument("--proxy-receipt", type=Path, required=True)
    parser.add_argument("--baseline", type=Path, required=True)
    parser.add_argument("--browser-result", type=Path)
    parser.add_argument("--output", type=Path)
    parser.add_argument("--container", required=True)
    parser.add_argument("--database", required=True)
    args = parser.parse_args()
    try:
        fixture, fixture_sha = protected(args.fixture)
        proxy, _ = protected(args.proxy_receipt)
        fixture_boundary(fixture, proxy, fixture_sha, args.database)
        if args.mode == "check-baseline":
            baseline, _ = protected(args.baseline, 4_000_000)
            require(baseline.get("schemaVersion") == 3
                    and baseline.get("fixtureSha256") == fixture_sha
                    and baseline.get("challenge") == fixture["challenge"], "OriginalBaselineBinding")
            facts = owner_snapshot(args.container, args.database, fixture)
            require(not facts["buyerSessionIds"] and facts == baseline.get("facts"),
                    "OriginalPreBrowserBaselineChanged")
            print("P30_PO_PRIVATE_PRE_BROWSER_BASELINE_UNCHANGED")
        elif args.mode == "capture":
            output_path(args.baseline, args.fixture)
            facts = owner_snapshot(args.container, args.database, fixture)
            require(len(facts["buyerSessionIds"]) == 0, "BuyerSessionExistsBeforeBrowser")
            write(args.baseline, {"schemaVersion": 3, "fixtureSha256": fixture_sha,
                "challenge": fixture["challenge"], "facts": facts})
            print("P30_PO_PRIVATE_BASELINE_CAPTURED")
        else:
            require(args.browser_result is not None and args.output is not None, "FinalInputsRequired")
            output_path(args.output, args.fixture)
            baseline, _ = protected(args.baseline, 4_000_000)
            browser, _ = protected(args.browser_result)
            require(baseline.get("schemaVersion") == 3
                    and baseline.get("fixtureSha256") == fixture_sha
                    and baseline.get("challenge") == fixture["challenge"], "OriginalBaselineBinding")
            completion, _ = protected(Path(fixture["completionPath"]), 4096)
            require(completion == {"schemaVersion": 3, "challenge": fixture["challenge"],
                                   "status": "BrowserFinished"}, "ControlPlaneCompletionMismatch")
            facts = owner_snapshot(args.container, args.database, fixture,
                baseline["facts"]["nativeOrderIds"], browser.get("orderId"))
            result = final_check(fixture, baseline["facts"], facts, browser, fixture_sha)
            write(args.output, result)
            print("P30_PO_PRIVATE_OWNER_VERIFIED")
    except (Failed, KeyError, TypeError, OverflowError) as error:
        print("P30_PO_PRIVATE_OWNER_DENIED:" + (str(error) if isinstance(error, Failed) else "InvalidEvidenceShape"), file=sys.stderr)
        return 1
    return 0


if __name__ == "__main__":
    sys.exit(main())
