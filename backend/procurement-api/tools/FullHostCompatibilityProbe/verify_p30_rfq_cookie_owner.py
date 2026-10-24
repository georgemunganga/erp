#!/usr/bin/env python3
"""Independent read-only P30 RFQ cookie baseline/final proof. No acceptance by completion file.

Capture BEFORE the browser. Verify AFTER browser logout. Both modes bind the exact
0600 fixture and HTTPS proxy receipt to the same fresh private Unix-socket database.
This deliberately rejects any unrecognised changed owning row; a new application
write shape needs a reviewed verifier change, never a broadened acceptance claim.
"""
import argparse
import decimal
from datetime import datetime, timezone
import hashlib
import json
from pathlib import Path
import re
from verify_p30_po_cookie_owner import (SYSTEM, TENANT, IDENT, Failed, require, protected,
    output_path, write, container_guard, query, digest, guid, canonical_json)

DB = re.compile(r"proc_p30_http_rfq_[a-z0-9_]{1,40}\Z")
HEX = re.compile(r"[0-9a-f]{64}\Z")
TABLES = {"procurement.sourcing_events", "procurement.sourcing_lines",
    "procurement.sourcing_invitations", "procurement.sourcing_publication_openings",
    "procurement.sourcing_publication_decisions", "procurement.sourcing_publication_receipts",
    "procurement.sourcing_manual_contacts", "procurement.sourcing_manual_quote_versions",
    "procurement.sourcing_command_receipts", "procurement.sourcing_draft_evidence",
    "procurement.sourcing_demand_bindings", "procurement.supplier_bids", "procurement.supplier_bid_lines",
    "procurement.purchase_request_allocations", "procurement.purchase_request_lines",
    "procurement.purchase_requests",
    "procurement.audit_events", "procurement.outbox_events", "hrm.local_sessions", "hrm.local_users",
    "core_approval.cases", "core_approval.stages", "core_approval.assignments", "core_approval.decisions"}
# Login may update these operational fields only. All assignment, action, branch,
# password, active and role data remain part of the identity hash.
LOGIN = {"last_login_at", "failed_login_count", "locked_until", "updated_at", "version"}


def stable_user_row(row, fixture):
    require(isinstance(row, dict) and guid(row.get("id")), "LocalUserIdentityInvalid")
    interactive = {fixture["buyerPrincipalId"], fixture["reviewerPrincipalId"]}
    return {key:value for key,value in row.items() if key not in LOGIN} if row["id"] in interactive else row


def core_row(row):
    """Preserve every EF Core approval column while giving the verifier one key shape."""
    require(isinstance(row, dict), "CoreApprovalRowInvalid")
    result = {}
    for key, value in row.items():
        require(isinstance(key, str) and key, "CoreApprovalColumnInvalid")
        converted = re.sub(r'(?<=[a-z0-9])(?=[A-Z])|(?<=[A-Z])(?=[A-Z][a-z])', '_', key).lower()
        require(converted not in result, "CoreApprovalColumnCollision")
        result[converted] = value
    require(guid(result.get("id")) and len(result) == len(row), "CoreApprovalIdentityInvalid")
    return result


def utc_stamp(value):
    require(isinstance(value, str), "SourceAuditTimeInvalid")
    try:
        parsed = datetime.fromisoformat(value.replace('Z', '+00:00'))
    except ValueError:
        raise Failed("SourceAuditTimeInvalid") from None
    require(parsed.tzinfo is not None and parsed.utcoffset().total_seconds() == 0,
        "SourceAuditTimeInvalid")
    return parsed


def verify_source_progress(before, after, buyer):
    """Only the real demand reservation and its stamp may advance."""
    old_line, new_line = before["source"], after["source"]
    old_request, new_request = before["request"], after["request"]
    require(decimal.Decimal(str(old_line.get("allocated_quantity"))) == 1
        and decimal.Decimal(str(new_line.get("allocated_quantity"))) == 2
        and new_line.get("route") == "PendingSourcing"
        and new_line.get("updated_by") == new_request.get("updated_by") == buyer
        and new_line.get("version") == old_line.get("version") + 1
        and new_request.get("version") == old_request.get("version") + 1,
        "ApprovedSourceReservationProgressInvalid")
    stamp = utc_stamp(new_line.get("updated_at"))
    require(stamp == utc_stamp(new_request.get("updated_at"))
        and stamp >= utc_stamp(old_line.get("updated_at") or old_line.get("created_at"))
        and stamp >= utc_stamp(old_request.get("updated_at") or old_request.get("created_at")),
        "ApprovedSourceAuditTimeInvalid")
    for prior, current, allowed in ((old_line, new_line,
            {"allocated_quantity", "route", "updated_by", "updated_at", "version"}),
            (old_request, new_request, {"updated_by", "updated_at", "version"})):
        require(prior.keys() == current.keys()
            and all(current[key] == value for key, value in prior.items() if key not in allowed),
            "ApprovedSourceBusinessFieldsChanged")


def captured_body(browser, operation, expected):
    raw = browser[operation]["body"]
    def unique(pairs):
        value = {}
        for key, item in pairs:
            require(key not in value, "DuplicateCapturedCommandKey")
            value[key] = item
        return value
    try:
        body = json.loads(raw, parse_float=decimal.Decimal, parse_int=decimal.Decimal,
            object_pairs_hook=unique)
    except (ValueError, TypeError, decimal.InvalidOperation):
        raise Failed("CapturedCommandBodyInvalid") from None
    require(isinstance(body, dict) and set(body) == expected,
        "CapturedCommandBodyUnexpected")
    return body


def exact_decimal_text(value):
    try:
        number = decimal.Decimal(value)
    except (decimal.InvalidOperation, TypeError, ValueError):
        raise Failed("CapturedQuantityInvalid") from None
    require(number.is_finite() and number > 0 and number.as_tuple().exponent >= -4,
        "CapturedQuantityInvalid")
    return format(number.normalize(), 'f')


def fingerprint(value):
    return hashlib.sha256(rfq_canonical_json(value).encode()).hexdigest()


def rfq_canonical_json(value):
    """The reviewed PO command subset with the .NET default '+' escape.

    PolicyJsonDigest writes string values with System.Text.Json's default
    encoder, which emits the literal plus sign as ``\\u002B``. Keep this
    correction local to RFQ evidence; the accepted PO owner is unchanged.
    """
    if isinstance(value, str):
        return canonical_json(value).replace('+', r'\u002B')
    if isinstance(value, list):
        return '[' + ','.join(rfq_canonical_json(item) for item in value) + ']'
    if isinstance(value, dict):
        require(all(isinstance(key, str) for key in value), "CommandShapeInvalid")
        return '{' + ','.join(rfq_canonical_json(key) + ':' + rfq_canonical_json(value[key])
            for key in sorted(value)) + '}'
    return canonical_json(value)


CONTACT_PRODUCER_VECTOR = {"schemaVersion":2,"operation":"RecordManualContact",
    "eventId":"11111111-1111-4111-8111-111111111111",
    "invitationId":"22222222-2222-4222-8222-222222222222",
    "channel":"EmailOutsideSystem","reference":"outside-ref",
    "reportedContactAt":"2026-10-23T12:00:00+00:00"}


def contact_utc_offset(value):
    # The browser uses Date.toISOString(): exactly three millisecond digits and Z.
    # Do not adopt general ISO8601, offsets, or sub-millisecond evidence here.
    require(isinstance(value, str) and re.fullmatch(
        r"[0-9]{4}-[0-9]{2}-[0-9]{2}T[0-9]{2}:[0-9]{2}:[0-9]{2}\.[0-9]{3}Z", value),
        "CapturedContactTimeMustBeReviewedJsUtcMilliseconds")
    try:
        observed = datetime.fromisoformat(value[:-1] + '+00:00')
    except ValueError:
        raise Failed("CapturedContactTimeInvalid") from None
    require(observed.tzinfo is not None and observed.utcoffset().total_seconds() == 0
        and observed.microsecond % 1000 == 0, "CapturedContactTimePrecisionUnreviewed")
    # System.Text.Json DateTimeOffset omits zero fractional ticks and trims only
    # trailing fractional zeros, retaining the explicit UTC offset.
    fraction = value[20:23].rstrip('0')
    return value[:19] + ('.' + fraction if fraction else '') + '+00:00'


def verify_captured_invitation(quote_body, contact, bid, version):
    invitation = quote_body.get("invitationId")
    require(guid(invitation) and contact.get("invitation_id") == invitation
        and bid.get("invitation_id") == invitation
        and version.get("supplier_bid_id") == bid.get("id"), "CapturedQuoteInvitationChanged")
    raw = version.get("snapshot_json")
    require(isinstance(raw, str) and len(raw) <= 500_000
        and hashlib.sha256(raw.encode()).hexdigest() == version.get("snapshot_sha256"),
        "CapturedQuoteSnapshotChanged")
    def unique(pairs):
        require(len({key for key, _ in pairs}) == len(pairs), "CapturedQuoteSnapshotAmbiguous")
        return dict(pairs)
    try:
        snapshot = json.loads(raw, object_pairs_hook=unique)
    except (ValueError, TypeError):
        raise Failed("CapturedQuoteSnapshotInvalid") from None
    # PrincipalManualQuotationSnapshot.Build uses default CLR property casing.
    # The version table has no invitation_id column; its immutable snapshot does.
    frozen = {"Id": "id", "SourcingEventId": "sourcing_event_id", "SupplierId": "supplier_id",
        "InvitationId": "invitation_id", "Status": "status", "SourceChannel": "source_channel",
        "CurrencyCode": "currency_code", "SurrogateEnteredBy": "surrogate_entered_by"}
    require(isinstance(snapshot, dict) and snapshot.get("schemaVersion") == 2
        and all(isinstance(bid.get(column), str) and bid[column]
            and snapshot.get(property) == bid[column] for property, column in frozen.items())
        and snapshot.get("InvitationId") == invitation, "CapturedQuoteInvitationChanged")


def boundary(fixture, proxy, fixture_hash, database):
    require(fixture.get("schemaVersion") == 3 and fixture.get("controlPlaneOnly") is True
        and fixture.get("database") == database and DB.fullmatch(database)
        and fixture.get("tenantId") == TENANT
        and fixture.get("browserAuthenticationRequired") == "NORMAL_LOCAL_PASSWORD_AND_SESSION_COOKIE"
        and all(guid(fixture.get(k)) for k in ("companyId", "branchId", "buyerPrincipalId",
            "reviewerPrincipalId", "approvedRequestId", "approvedRequestLineId", "nativeEventId",
            "nativePolicyId", "nativeCaseId"))
        and isinstance(fixture.get("nativeSupplierIds"), list)
        and len(set(fixture["nativeSupplierIds"])) == 2
        and all(guid(x) for x in fixture["nativeSupplierIds"])
        and HEX.fullmatch(fixture.get("challenge", ""))
        and fixture.get("contactCanonicalProbeSha256") == fingerprint(CONTACT_PRODUCER_VECTOR),
        "FixtureOrContactEncoderBoundary")
    require(proxy.get("schemaVersion") == 3 and proxy.get("transport") == "normal-local-cookie-https-v1"
        and proxy.get("database") == database and proxy.get("apiUrl") == fixture.get("apiUrl")
        and proxy.get("fixtureSha256") == fixture_hash
        and proxy.get("injectsIdentityHeaders") is False
        and proxy.get("mocksBusinessResponses") is False
        and proxy.get("browserAcceptanceVerified") is False, "ProxyBoundary")


def snapshot(container, database, fixture):
    container_guard(container)
    meta = query(container, database, """BEGIN TRANSACTION ISOLATION LEVEL REPEATABLE READ READ ONLY;
SELECT jsonb_build_object('database',current_database(),
'system',(SELECT system_identifier::text FROM pg_control_system()),
'version',current_setting('server_version_num')::integer,
'tcp',inet_server_addr(),'readOnly',current_setting('default_transaction_read_only'),
'tables',(SELECT jsonb_agg(jsonb_build_object('schema',schemaname,'name',tablename)
ORDER BY schemaname,tablename) FROM pg_tables WHERE
(schemaname IN ('procurement','core_approval') OR (schemaname='hrm' AND tablename IN
('local_sessions','local_users','hr_user_branch_assignments','tenant_role_assignments',
'work_locations','legal_entities'))) AND tablename !~ '^__'));
ROLLBACK;""")
    require(len(meta) == 1 and meta[0].get("database") == database
        and meta[0].get("system") == SYSTEM and meta[0].get("tcp") is None
        and meta[0].get("readOnly") == "on" and 170000 <= meta[0].get("version", 0) < 180000,
        "PrivatePostgresBoundary")
    names = [(x.get("schema"), x.get("name")) for x in meta[0].get("tables", [])]
    require(len(names) >= 50 and len(names) == len(set(names))
        and all(s in ("procurement", "core_approval", "hrm") and IDENT.fullmatch(s)
            and isinstance(t, str) and IDENT.fullmatch(t) for s, t in names)
        and TABLES.issubset({s+"."+t for s, t in names}), "OwningCatalogBoundary")
    statements = ["BEGIN TRANSACTION ISOLATION LEVEL REPEATABLE READ READ ONLY;"]
    for schema, table in names:
        statements.append(f"SELECT jsonb_build_object('table','{schema}.{table}',"
            f"'rows',(SELECT coalesce(jsonb_agg(to_jsonb(t) ORDER BY to_jsonb(t)::text),'[]'::jsonb)"
            f" FROM \"{schema}\".\"{table}\" t));")
    statements.append("ROLLBACK;")
    answer = query(container, database, "\n".join(statements))
    require(len(answer) == len(names), "OwningSnapshotCount")
    rows = {}
    for item, pair in zip(answer, names):
        name = ".".join(pair)
        require(item.get("table") == name and isinstance(item.get("rows"), list), "OwningSnapshotShape")
        rows[name] = ([core_row(row) for row in item["rows"]]
            if name.startswith("core_approval.") else item["rows"])
    hashes = {}
    for name, group in rows.items():
        if name == "hrm.local_sessions": continue
        hashes[name] = {}
        for row in group:
            identity = row.get("id")
            require(guid(identity) and identity not in hashes[name], "OwningDuplicateId")
            stable = stable_user_row(row, fixture) if name == "hrm.local_users" else row
            hashes[name][identity] = digest(stable)
    def one(table, id):
        found = [x for x in rows[table] if x.get("id") == id]
        require(len(found) == 1, "OriginalOwningRowMissing")
        return found[0]
    source = one("procurement.purchase_request_lines", fixture["approvedRequestLineId"])
    request = one("procurement.purchase_requests", fixture["approvedRequestId"])
    native = one("procurement.sourcing_events", fixture["nativeEventId"])
    require(source.get("purchase_request_id") == fixture["approvedRequestId"]
        and str(source.get("approved_quantity")) in ("2", "2.0000")
        and str(source.get("ordered_quantity")) in ("0", "0.0000")
        and str(source.get("accepted_quantity")) in ("0", "0.0000")
        and native.get("status") == "Published" and native.get("execution_version") == 2,
        "OriginalApprovedRfqSourceInvalid")
    sessions = {x["id"]: x for x in rows["hrm.local_sessions"]
        if x.get("local_user_id") in (fixture["buyerPrincipalId"],fixture["reviewerPrincipalId"])}
    return {"schemaVersion":3,"database":database,"system":SYSTEM,"hashes":hashes,
        "source":source,"request":request,"nativeEvent":native,"sessions":sessions,
        "selected":{name: rows[name] for name in TABLES if name != "hrm.local_users"}}


def verify(before, after, fixture, browser, fixture_hash):
    require(before.get("schemaVersion") == after.get("schemaVersion") == 3
        and before.get("database") == after.get("database") == fixture["database"]
        and before.get("system") == after.get("system") == SYSTEM,
        "BaselineOwnerBoundary")
    require(browser.get("schemaVersion") == 3
        and browser.get("status") == "PASS_NORMAL_COOKIE_P30_RFQ_INTERNAL_UI_ONLY"
        and browser.get("fixtureSha256") == fixture_hash
        and browser.get("challenge") == fixture["challenge"]
        and browser.get("buyerSessionRevoked") is True
        and browser.get("reviewerSessionRevoked") is True
        and browser.get("supplierSendPerformed") is False
        and browser.get("quoteSubmitted") is False
        and browser.get("awardCreated") is False and browser.get("orderCreated") is False
        and browser.get("financePostingPerformed") is False,
        "BrowserEvidenceBoundary")
    event = browser.get("browserEventId"); bid = browser.get("browserBidId")
    require(guid(event) and guid(bid) and event != fixture["nativeEventId"], "SuccessorIdentifiers")
    operations = {"create":"buyerPrincipalId", "open":"buyerPrincipalId",
        "decide":"reviewerPrincipalId", "contact":"buyerPrincipalId", "quote":"buyerPrincipalId"}
    keys = []
    for operation in operations:
        entry = browser.get(operation)
        require(isinstance(entry, dict) and guid(entry.get("key"))
            and isinstance(entry.get("body"), str) and 1 <= len(entry["body"]) <= 65536,
            "BrowserCommandBoundary")
        keys.append(entry["key"])
    require(len(set(keys)) == 5, "FiveDistinctCommandsRequired")
    old = before["hashes"]; new = after["hashes"]
    for name, original in old.items():
        require(name in new and set(original).issubset(new[name]), "OriginalOwningRowRemoved")
        if name not in {"procurement.purchase_request_lines", "procurement.purchase_requests"}:
            require(all(new[name][id] == value for id,value in original.items()), "OriginalOwningHistoryChanged")
    permitted_additions = {
        "procurement.sourcing_events", "procurement.sourcing_lines",
        "procurement.sourcing_draft_evidence", "procurement.sourcing_demand_bindings",
        "procurement.sourcing_command_receipts", "procurement.purchase_request_allocations",
        "procurement.sourcing_publication_openings", "procurement.sourcing_publication_decisions",
        "procurement.sourcing_publication_receipts", "procurement.sourcing_invitations",
        "procurement.sourcing_manual_contacts", "procurement.sourcing_manual_quote_versions",
        "procurement.supplier_bids", "procurement.supplier_bid_lines",
        "procurement.audit_events", "procurement.outbox_events",
        "core_approval.cases", "core_approval.stages", "core_approval.assignments",
        "core_approval.decisions"}
    require(all(name in permitted_additions or set(new[name]) == set(original)
        for name, original in old.items()), "UnexpectedOwningTableAddedRows")
    require(after["nativeEvent"] == before["nativeEvent"], "NativeRfqChanged")
    verify_source_progress(before, after, fixture["buyerPrincipalId"])
    require(set(new["procurement.purchase_request_lines"]) == set(old["procurement.purchase_request_lines"])
        and set(new["procurement.purchase_requests"]) == set(old["procurement.purchase_requests"])
        and all(new["procurement.purchase_request_lines"][key] == value for key, value in
            old["procurement.purchase_request_lines"].items() if key != fixture["approvedRequestLineId"])
        and all(new["procurement.purchase_requests"][key] == value for key, value in
            old["procurement.purchase_requests"].items() if key != fixture["approvedRequestId"]),
        "OtherApprovedDemandChanged")
    def added(table):
        return [x for x in after["selected"][table] if x["id"] not in old[table]]
    ev = added("procurement.sourcing_events")
    require(len(ev) == 1 and ev[0]["id"] == event and ev[0].get("status") == "Published"
        and ev[0].get("execution_version") == 2, "ExactlyOnePublishedBrowserRfq")
    allocations = added("procurement.purchase_request_allocations")
    require(len(allocations) == 1 and allocations[0].get("purchase_request_line_id") == fixture["approvedRequestLineId"]
        and allocations[0].get("purchase_request_id") == fixture["approvedRequestId"]
        and allocations[0].get("target_type") == "SourcingEvent"
        and allocations[0].get("target_id") == event
        and allocations[0].get("status") == "Reserved"
        and str(allocations[0].get("quantity")) in ("1", "1.0000"),
        "ExactlyOneBrowserReservation")
    require(len(added("procurement.sourcing_lines")) == 1
        and len(added("procurement.sourcing_draft_evidence")) == 1
        and len(added("procurement.sourcing_demand_bindings")) == 1
        and len(added("procurement.supplier_bid_lines")) == 1,
        "ExactBrowserSourceAndQuoteLineCounts")
    opening = added("procurement.sourcing_publication_openings")
    decisions = added("procurement.sourcing_publication_decisions")
    invitations = added("procurement.sourcing_invitations")
    contacts = added("procurement.sourcing_manual_contacts")
    quotes = added("procurement.sourcing_manual_quote_versions")
    bids = added("procurement.supplier_bids")
    require(len(opening) == len(decisions) == len(contacts) == len(quotes) == len(bids) == 1
        and len(invitations) == 2 and bids[0]["id"] == bid
        and all(x.get("sourcing_event_id") == event for x in opening+decisions+invitations+contacts+quotes+bids)
        and opening[0].get("policy_version_id") == fixture["nativePolicyId"]
        and opening[0].get("maker_principal_id") == fixture["buyerPrincipalId"]
        and decisions[0].get("core_approval_case_id") == opening[0].get("core_approval_case_id")
        and {x.get("supplier_id") for x in invitations} == set(fixture["nativeSupplierIds"])
        and contacts[0].get("actor_principal_id") == fixture["buyerPrincipalId"]
        and decisions[0].get("actor_principal_id") == fixture["reviewerPrincipalId"]
        and quotes[0].get("actor_principal_id") == fixture["buyerPrincipalId"]
        and bids[0].get("status") == "Draft" and bids[0].get("submitted_at") is None,
        "PublishedManualDraftOwnerGraph")
    receipts = added("procurement.sourcing_publication_receipts")
    draft_receipts = added("procurement.sourcing_command_receipts")
    require(len(receipts) == 2 and len(draft_receipts) == 1
        and {x.get("command_key") for x in receipts} == {browser["open"]["key"],browser["decide"]["key"]}
        and draft_receipts[0].get("command_key") == browser["create"]["key"],
        "OriginalCommandReceiptsRequired")
    create_body = captured_body(browser, "create", {"title", "sources"})
    sources = create_body["sources"]
    require(isinstance(create_body["title"], str) and create_body["title"].strip() == create_body["title"]
        and isinstance(sources, list) and len(sources) == 1
        and set(sources[0]) == {"purchaseRequestId", "purchaseRequestLineId",
            "expectedRequestVersion", "quantity"}
        and sources[0]["purchaseRequestId"] == fixture["approvedRequestId"]
        and sources[0]["purchaseRequestLineId"] == fixture["approvedRequestLineId"]
        and exact_decimal_text(sources[0]["quantity"]) == "1", "CapturedApprovedDemandChanged")
    create_value = {"schemaVersion":2,"operation":"Create","title":create_body["title"],
        "sources":[{"purchaseRequestId":sources[0]["purchaseRequestId"],
            "purchaseRequestLineId":sources[0]["purchaseRequestLineId"],
            "expectedRequestVersion":sources[0]["expectedRequestVersion"],
            "quantity":exact_decimal_text(sources[0]["quantity"])}]}
    require(draft_receipts[0].get("command_fingerprint") == fingerprint(create_value),
        "RFQCreateCommandFingerprintMismatch")
    open_body = captured_body(browser, "open", {"expectedEventVersion",
        "originalDraftFingerprint", "supplierIds"})
    require(open_body["originalDraftFingerprint"] == ev[0].get("original_draft_fingerprint")
        and isinstance(open_body["supplierIds"], list)
        and set(open_body["supplierIds"]) == set(fixture["nativeSupplierIds"]),
        "CapturedPublicationShortlistChanged")
    open_value = {"schemaVersion":2,"operation":"OpenPublicationReview","id":event,
        "expectedEventVersion":open_body["expectedEventVersion"],
        "originalDraftFingerprint":open_body["originalDraftFingerprint"],
        "supplierIds":sorted(open_body["supplierIds"])}
    open_receipt = next(x for x in receipts if x.get("operation") == "Open")
    require(open_receipt.get("command_sha256") == fingerprint(open_value),
        "RFQPublicationOpenFingerprintMismatch")
    decide_body = captured_body(browser, "decide", {"expectedEventVersion", "expectedCaseVersion",
        "expectedAssignmentVersion", "action", "reason"})
    require(decide_body["action"] == "Approve" and decide_body["reason"] == "",
        "CapturedIndependentReviewChanged")
    assignment = added("core_approval.assignments")
    require(len(assignment) == 1, "OneIndependentReviewerAssignmentRequired")
    decide_value = {"schemaVersion":2,"operation":"DecidePublicationReview", "id":event,
        "caseId":opening[0]["core_approval_case_id"],"assignmentId":assignment[0]["id"],
        "expectedEventVersion":decide_body["expectedEventVersion"],
        "expectedCaseVersion":decide_body["expectedCaseVersion"],
        "expectedAssignmentVersion":decide_body["expectedAssignmentVersion"],
        "action":"Approve","reason":""}
    decide_receipt = next(x for x in receipts if x.get("operation") == "Decide")
    require(decide_receipt.get("command_sha256") == fingerprint(decide_value),
        "RFQPublicationDecisionFingerprintMismatch")
    contact_body = captured_body(browser, "contact", {"channel","reference","reportedContactAt"})
    require(contact_body["channel"] == contacts[0].get("channel")
        and contact_body["reference"] == contacts[0].get("reference")
        and datetime.fromisoformat(contact_body["reportedContactAt"].replace("Z","+00:00")).astimezone(timezone.utc)
            == datetime.fromisoformat(contacts[0]["reported_contact_at"].replace("Z","+00:00")).astimezone(timezone.utc),
        "CapturedOutsideContactChanged")
    contact_value = {"schemaVersion":2,"operation":"RecordManualContact",
        "eventId":event,"invitationId":contacts[0]["invitation_id"],
        "channel":contact_body["channel"],"reference":contact_body["reference"],
        "reportedContactAt":contact_utc_offset(contact_body["reportedContactAt"])}
    require(contacts[0].get("command_sha256") == fingerprint(contact_value),
        "RFQManualContactFingerprintMismatch")
    quote_body = captured_body(browser, "quote", {"invitationId", "lines"})
    verify_captured_invitation(quote_body, contacts[0], bids[0], quotes[0])
    quote_lines = quote_body["lines"]
    require(isinstance(quote_lines, list) and len(quote_lines) == 1
        and set(quote_lines[0]) == {"sourcingLineId","quantity","unitPrice",
            "taxAmount","deliveryAmount","leadTimeText","notes"}
        and quote_lines[0]["sourcingLineId"] == added("procurement.sourcing_lines")[0]["id"],
        "CapturedQuotationLinesChanged")
    hydrated_quote = {**quote_lines[0], **{name:decimal.Decimal(quote_lines[0][name])
        for name in ("quantity","unitPrice","taxAmount","deliveryAmount")}}
    quote_value = {"schemaVersion":2,"operation":"CreateManualQuote","eventId":event,
        "invitationId":quote_body["invitationId"],"lines":[hydrated_quote]}
    require(quotes[0].get("command_sha256") == fingerprint(quote_value),
        "RFQManualQuoteFingerprintMismatch")
    for row in receipts + draft_receipts:
        response = row.get("response_json")
        require(isinstance(response, str) and hashlib.sha256(response.encode()).hexdigest()
            == row.get("response_sha256") and HEX.fullmatch(row.get("command_sha256",
                row.get("command_fingerprint", ""))), "DurableCommandDigestMismatch")
    require({x.get("operation") for x in receipts} == {"Open", "Decide"}
        and all(x.get("sourcing_event_id") == event for x in receipts+draft_receipts)
        and draft_receipts[0].get("actor_principal_id") == fixture["buyerPrincipalId"]
        and next(x for x in receipts if x.get("operation") == "Open").get("actor_principal_id")
            == fixture["buyerPrincipalId"]
        and next(x for x in receipts if x.get("operation") == "Decide").get("actor_principal_id")
            == fixture["reviewerPrincipalId"], "CommandActorPurposeMismatch")
    require(contacts[0].get("command_key") == browser["contact"]["key"]
        and quotes[0].get("command_key") == browser["quote"]["key"],
        "ManualCommandReceiptsRequired")
    for name in ("procurement.purchase_orders","procurement.sourcing_awards",
        "procurement.finance_invoice_handoffs","procurement.finance_payment_status_refs"):
        require(before["hashes"].get(name) == after["hashes"].get(name), "NoDownstreamAuthority")
    core_cases = added("core_approval.cases")
    core_stages = added("core_approval.stages")
    core_assignments = added("core_approval.assignments")
    core_decisions = added("core_approval.decisions")
    require(len(core_cases) == len(core_stages) == len(core_assignments) == len(core_decisions) == 1
        and core_cases[0].get("id") == opening[0].get("core_approval_case_id")
        and core_cases[0].get("state") == "Approved"
        and core_decisions[0].get("actor_subject_id") == fixture["reviewerPrincipalId"],
        "IndependentCorePublicationCase")
    audit = added("procurement.audit_events")
    outbox = added("procurement.outbox_events")
    require(audit and outbox
        and all(x.get("tenant_id") == TENANT and x.get("legal_entity_id") == fixture["companyId"]
            and x.get("actor_subject_id") in (fixture["buyerPrincipalId"],fixture["reviewerPrincipalId"])
            for x in audit)
        and all(x.get("aggregate_id") == event and x.get("aggregate_type") == "SourcingEvent"
            and x.get("event_type") not in ("sourcing.awarded", "sourcing.supplier_dispatched")
            for x in outbox), "ScopedAuditOutboxEffects")
    current = after["sessions"]; prior = before["sessions"]
    new_sessions = [row for id,row in current.items() if id not in prior]
    require(len(new_sessions) >= 2 and {x.get("local_user_id") for x in new_sessions}
        == {fixture["buyerPrincipalId"],fixture["reviewerPrincipalId"]}
        and all(x.get("revoked_at") is not None for x in new_sessions), "BothRealSessionsRevoked")
    return {"schemaVersion":3,"status":"PASS_P30_RFQ_OWNER_FACTS","database":fixture["database"],
        "system":SYSTEM,"fixtureSha256":fixture_hash,"browserEventId":event,
        "browserBidId":bid,"nativeRfqUnchanged":True,"approvedDemandConserved":True,
        "independentReviewAndManualDraft":True,"realSessionsRevoked":True,"productionTouched":False}


def main():
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument("mode",choices=("capture","verify","check-baseline"))
    for flag in ("fixture","proxy-receipt","baseline","container","database"):
        parser.add_argument("--"+flag,required=True)
    parser.add_argument("--browser-result");parser.add_argument("--output")
    args=parser.parse_args()
    fixture_path=Path(args.fixture);baseline_path=Path(args.baseline)
    try:
        fixture,fixture_hash=protected(fixture_path)
        proxy,_=protected(Path(args.proxy_receipt))
        boundary(fixture,proxy,fixture_hash,args.database)
        if args.mode=="capture":
            output_path(baseline_path,fixture_path)
            facts=snapshot(args.container,args.database,fixture)
            require(decimal.Decimal(str(facts["source"].get("allocated_quantity"))) == 1,
                "PreBrowserSourceAllocationInvalid")
            require(not facts["sessions"],"PreBrowserSessionsMustBeAbsent")
            write(baseline_path,{"schemaVersion":3,"fixtureSha256":fixture_hash,
                "challenge":fixture["challenge"],"facts":facts})
            print("P30_RFQ_PRIVATE_BASELINE_CAPTURED")
        else:
            baseline,_=protected(baseline_path,8_000_000)
            require(baseline.get("schemaVersion")==3 and baseline.get("fixtureSha256")==fixture_hash
                and baseline.get("challenge")==fixture["challenge"],"ProtectedBaselineBinding")
            if args.mode=="check-baseline":
                require(snapshot(args.container,args.database,fixture)==baseline["facts"],"PreBrowserBaselineChanged")
                print("P30_RFQ_PRIVATE_BASELINE_UNCHANGED")
            else:
                require(args.browser_result and args.output,"FinalBrowserEvidenceAndOutputRequired")
                browser,_=protected(Path(args.browser_result),500_000)
                output_path(Path(args.output),fixture_path)
                final=snapshot(args.container,args.database,fixture)
                result=verify(baseline["facts"],final,fixture,browser,fixture_hash)
                write(Path(args.output),result)
                print("P30_RFQ_PRIVATE_OWNER_FACTS_VERIFIED")
    except (Failed,KeyError,TypeError,ValueError,decimal.InvalidOperation) as error:
        print("P30_RFQ_PRIVATE_OWNER_VERIFICATION_FAILED:"+(str(error) if isinstance(error,Failed) else "MalformedEvidence"),file=__import__('sys').stderr)
        raise SystemExit(1) from None
if __name__=="__main__":main()
