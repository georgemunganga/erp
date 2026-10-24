#!/usr/bin/env python3
"""Independent read-only P30 RFQ cookie baseline/final proof. No acceptance by completion file.

Capture BEFORE the browser. Verify AFTER browser logout. Both modes bind the exact
0600 fixture and HTTPS proxy receipt to the same fresh private Unix-socket database.
This deliberately rejects any unrecognised changed owning row; a new application
write shape needs a reviewed verifier change, never a broadened acceptance claim.
"""
import argparse
import hashlib
import json
from pathlib import Path
import re
from verify_p30_po_cookie_owner import (SYSTEM, TENANT, IDENT, Failed, require, protected,
    output_path, write, container_guard, query, digest, guid)

DB = re.compile(r"proc_p30_http_rfq_[a-z0-9_]{1,40}\Z")
HEX = re.compile(r"[0-9a-f]{64}\Z")
TABLES = {"procurement.sourcing_events", "procurement.sourcing_lines",
    "procurement.sourcing_invitations", "procurement.sourcing_publication_openings",
    "procurement.sourcing_publication_decisions", "procurement.sourcing_publication_receipts",
    "procurement.sourcing_manual_contacts", "procurement.sourcing_manual_quote_versions",
    "procurement.sourcing_command_receipts", "procurement.sourcing_draft_evidence",
    "procurement.sourcing_demand_bindings", "procurement.supplier_bids", "procurement.supplier_bid_lines",
    "procurement.purchase_request_allocations", "procurement.purchase_request_lines",
    "procurement.audit_events", "procurement.outbox_events", "hrm.local_sessions", "hrm.local_users"}
# Login may update these operational fields only. All assignment, action, branch,
# password, active and role data remain part of the identity hash.
LOGIN = {"last_login_at", "failed_login_count", "locked_until", "updated_at", "version"}


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
        and HEX.fullmatch(fixture.get("challenge", "")), "FixtureBoundary")
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
        rows[name] = item["rows"]
    hashes = {}
    for name, group in rows.items():
        if name == "hrm.local_sessions": continue
        hashes[name] = {}
        for row in group:
            identity = row.get("id")
            require(guid(identity) and identity not in hashes[name], "OwningDuplicateId")
            stable = {k:v for k,v in row.items() if k not in LOGIN} if name == "hrm.local_users" else row
            hashes[name][identity] = digest(stable)
    def one(table, id):
        found = [x for x in rows[table] if x.get("id") == id]
        require(len(found) == 1, "OriginalOwningRowMissing")
        return found[0]
    source = one("procurement.purchase_request_lines", fixture["approvedRequestLineId"])
    native = one("procurement.sourcing_events", fixture["nativeEventId"])
    require(source.get("purchase_request_id") == fixture["approvedRequestId"]
        and str(source.get("approved_quantity")) in ("2", "2.0000")
        and str(source.get("allocated_quantity")) in ("1", "1.0000")
        and str(source.get("ordered_quantity")) in ("0", "0.0000")
        and native.get("status") == "Published" and native.get("execution_version") == 2,
        "OriginalApprovedRfqSourceInvalid")
    sessions = {x["id"]: x for x in rows["hrm.local_sessions"]
        if x.get("local_user_id") in (fixture["buyerPrincipalId"],fixture["reviewerPrincipalId"])}
    return {"schemaVersion":3,"database":database,"system":SYSTEM,"hashes":hashes,
        "source":source,"nativeEvent":native,"sessions":sessions,
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
    original_line = before["source"]; final_line = after["source"]
    for field in original_line:
        if field not in {"allocated_quantity", "version", "updated_at"}:
            require(final_line.get(field) == original_line[field], "ApprovedSourceFieldsChanged")
    require(str(final_line.get("allocated_quantity")) in ("2", "2.0000"), "ApprovedSourceConservation")
    def added(table):
        return [x for x in after["selected"][table] if x["id"] not in old[table]]
    ev = added("procurement.sourcing_events")
    require(len(ev) == 1 and ev[0]["id"] == event and ev[0].get("status") == "Published"
        and ev[0].get("execution_version") == 2, "ExactlyOnePublishedBrowserRfq")
    allocations = added("procurement.purchase_request_allocations")
    require(len(allocations) == 1 and allocations[0].get("purchase_request_line_id") == fixture["approvedRequestLineId"]
        and allocations[0].get("target_id") == event, "ExactlyOneBrowserReservation")
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
    except (Failed,KeyError,TypeError,ValueError) as error:
        print("P30_RFQ_PRIVATE_OWNER_VERIFICATION_FAILED:"+(str(error) if isinstance(error,Failed) else "MalformedEvidence"),file=__import__('sys').stderr)
        raise SystemExit(1) from None
if __name__=="__main__":main()
