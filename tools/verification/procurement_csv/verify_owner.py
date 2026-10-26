#!/usr/bin/env python3
"""Independent private PG read-only CSV export audit and master-row proof.

Usage: verify_owner.py before|after|reconcile. The runner creates the DB and
host; this script never creates or changes SQL objects or business rows.
"""
from __future__ import annotations

import hashlib
import json
import os
from pathlib import Path
import re
import subprocess
import sys
import urllib.parse

from verify_http import GUID, admission, need, protected


CONTAINER = re.compile(r"^[a-f0-9]{64}$")
PG_SQL = r"""
BEGIN ISOLATION LEVEL SERIALIZABLE READ ONLY;
SET LOCAL statement_timeout = '60s';
SELECT jsonb_build_object(
  'database', current_database(),
  'serverVersion', current_setting('server_version_num'),
  'systemIdentifier', (SELECT system_identifier::text FROM pg_control_system()),
  'supplierSha256', (SELECT encode(sha256(convert_to(coalesce(string_agg(to_jsonb(s)::text, E'\n' ORDER BY s.id), ''), 'UTF8')), 'hex') FROM procurement.suppliers s),
  'supplierCount', (SELECT count(*) FROM procurement.suppliers),
  'itemSha256', (SELECT encode(sha256(convert_to(coalesce(string_agg(to_jsonb(i)::text, E'\n' ORDER BY i.id), ''), 'UTF8')), 'hex') FROM procurement.catalog_items i),
  'itemCount', (SELECT count(*) FROM procurement.catalog_items),
  'coreRoles', (SELECT coalesce(jsonb_agg(jsonb_build_object('id', r.id,
      'tenant', r.tenant_id, 'roleKey', r.role_key,
      'permissionsCsv', r.permissions_csv, 'active', r.active,
      'isArchived', r.is_archived, 'updatedAt', r.updated_at,
      'updatedBy', r.updated_by) ORDER BY r.id), '[]'::jsonb)
    FROM hrm.tenant_role_assignments r),
  'auditRows', (SELECT coalesce(jsonb_agg(jsonb_build_object(
      'id', a.id, 'recordId', a.record_id, 'recordType', a.record_type,
      'action', a.action, 'actor', a.actor_subject_id,
      'tenant', a.tenant_id, 'company', a.legal_entity_id,
      'changed', a.changed_fields_json) ORDER BY a.id), '[]'::jsonb)
    FROM procurement.audit_events a
    WHERE a.record_type IN ('SupplierCsvExport', 'CatalogItemCsvExport'))
)::text;
COMMIT;
"""


def write_new(path: str, data: dict) -> None:
    target = Path(path)
    need(target.is_absolute() and not target.exists() and
         target.parent.stat().st_mode & 0o777 == 0o700, "owner-output-boundary")
    with target.open("x") as out:
        os.chmod(target, 0o600)
        json.dump(data, out, indent=2, sort_keys=True)
        out.write("\n")


def snapshot(config: dict) -> dict:
    container = config["privatePgContainerId"]
    need(CONTAINER.fullmatch(container) is not None, "private-container-id")
    inspect = subprocess.run(["docker", "inspect", "--format", "{{json .}}", container],
                             capture_output=True, text=True, timeout=10, check=True)
    info = json.loads(inspect.stdout)
    need(info.get("Id") == container and info.get("State", {}).get("Running") is True
         and info.get("Image") == config["privatePgImageId"]
         and info.get("HostConfig", {}).get("NetworkMode") == "none",
         "private-container-identity")
    database = config["databaseName"]
    query = subprocess.run(["docker", "exec", "-i", container, "psql", "-X", "-qAt",
                            "-v", "ON_ERROR_STOP=1", "-U", "postgres", "-d", database],
                           input=PG_SQL, capture_output=True, text=True, timeout=120)
    need(query.returncode == 0, "private-readonly-query-failed")
    lines = [line for line in query.stdout.splitlines() if line.strip()]
    need(len(lines) == 1, "owner-snapshot-shape")
    result = json.loads(lines[0])
    need(result["database"] == database and
         result["systemIdentifier"] == config["privatePgSystemIdentifier"] and
         170000 <= int(result["serverVersion"]) < 180000,
         "private-pg-identity")
    return result


def browser_query(kind: str, browser: dict, config: dict) -> str:
    expected = "" if kind == "vendors" else config["datasets"]["filterQuery"]["items"]
    need(browser.get("query") == expected, "browser-export-query-boundary")
    return expected


def reconcile(before: dict, after: dict, http: dict, config: dict) -> dict:
    need(before["database"] == after["database"] == http["database"] and
         before["systemIdentifier"] == after["systemIdentifier"], "snapshot-owner")
    for field in ("supplierSha256", "supplierCount", "itemSha256", "itemCount"):
        need(before[field] == after[field], "master-rows-changed")
    if http.get("revokedAction"):
        old_roles = {row["id"]: row for row in before["coreRoles"]}
        new_roles = {row["id"]: row for row in after["coreRoles"]}
        need(old_roles.keys() == new_roles.keys(), "role-set-changed")
        changed = [(old, new_roles[key]) for key, old in old_roles.items()
                   if old != new_roles[key]]
        prior = changed[0][0]["permissionsCsv"].split(',') if len(changed) == 1 else []
        current = changed[0][1]["permissionsCsv"].split(',') if len(changed) == 1 else []
        need(len(changed) == 1 and http["revokedAction"] in changed[0][0]["permissionsCsv"].split(',')
             and current == [action for action in prior if action != http["revokedAction"]]
             and hashlib.sha256(changed[0][0]["permissionsCsv"].encode()).hexdigest()
                 == config["revocationExpectedPermissionsSha256"]
             and hashlib.sha256(changed[0][1]["permissionsCsv"].encode()).hexdigest()
                 == config["revocationNewPermissionsSha256"]
             and changed[0][0]["roleKey"] == config["revocationRoleKey"]
             and changed[0][0]["tenant"] == config["core"]["tenantId"]
             and all(changed[0][0][key] == changed[0][1][key] for key in
                     ("id", "tenant", "roleKey", "active", "isArchived")),
             "current-core-role-not-exactly-revoked")
    else:
        need(before["coreRoles"] == after["coreRoles"], "core-role-mutation")
    old = {row["id"]: row for row in before["auditRows"]}
    new = {row["id"]: row for row in after["auditRows"]}
    need(len(old) == len(before["auditRows"]) and len(new) == len(after["auditRows"])
         and all(new.get(key) == row for key, row in old.items()),
         "old-export-audit-changed")
    delta = [row for key, row in new.items() if key not in old]
    expected = {row["exportId"]: row for row in http["exports"]}
    need(len(expected) == len(http["exports"]) == len(delta), "export-audit-cardinality")
    for row in delta:
        exported = expected.get(row["recordId"])
        need(exported is not None and row["action"] == "ExportGenerated" and
             row["recordType"] == ("SupplierCsvExport" if exported["kind"] == "vendors"
                                    else "CatalogItemCsvExport") and
             row["actor"] == config["actors"][exported["role"]]["principalId"] and
             row["tenant"] == config["core"]["tenantId"] and
             row["company"] == config["core"]["companyAId"], "export-audit-link")
        changed = row["changed"] if isinstance(row["changed"], dict) else json.loads(row["changed"])
        query = exported["query"]
        need(query == "" or query.startswith("?"), "export-filter-query")
        pairs = urllib.parse.parse_qs(query[1:], keep_blank_values=True, strict_parsing=True)
        need(set(pairs) <= {"search", "status"} and all(len(v) == 1 for v in pairs.values()),
             "export-filter-shape")
        normalized = json.dumps({"search": pairs.get("search", [""])[0].strip(),
                                 "status": pairs.get("status", [""])[0].strip()},
                                separators=(',', ':'), ensure_ascii=False)
        filter_sha = hashlib.sha256(normalized.encode()).hexdigest()
        need(set(changed) == {"schemaVersion", "filterSha256", "rowCount",
                              "payloadSha256", "branchId", "allPermittedBranches"}
             and changed["schemaVersion"] == 1 and changed["rowCount"] == exported["rowCount"]
             and changed["payloadSha256"] == exported["payloadSha256"]
             and changed["allPermittedBranches"] == (exported["scope"] == "all")
             and changed["branchId"] == (None if exported["scope"] == "all"
                                          else config["core"]["branchAId"])
             and changed["filterSha256"] == filter_sha,
             "export-audit-payload")
    return {"status": "PASS_PRIVATE_CSV_OWNER_AUDIT_AND_MASTERS_ONLY", "schemaVersion": 1,
            "database": http["database"], "exportAuditCount": len(delta),
            "oldExportAuditsPreserved": len(old), "supplierCount": after["supplierCount"],
            "itemCount": after["itemCount"], "browserUiVerified": False,
            "productionTouched": False}


def main() -> None:
    mode = sys.argv[1] if len(sys.argv) == 2 else ""
    need(mode in ("before", "after", "reconcile", "reconcile-browser"), "owner-mode")
    fixture_bytes = protected(os.environ["BE01_PRIVATE_FIXTURE"])
    config = admission(fixture_bytes,
                       os.environ["BE01_PRIVATE_HTTP_RESULT"], result_must_be_fresh=mode == "before")
    if mode in ("before", "after"):
        data = snapshot(config)
        write_new(os.environ["BE01_PRIVATE_OWNER_" + mode.upper()], data)
        return
    before = json.loads(protected(os.environ["BE01_PRIVATE_OWNER_BEFORE"], maximum=2_000_000))
    after = json.loads(protected(os.environ["BE01_PRIVATE_OWNER_AFTER"], maximum=2_000_000))
    if mode == "reconcile-browser":
        prefix = os.environ["BE01_PRIVATE_BROWSER_RESULT"]
        exports = []
        for kind, role in (("vendors", "approver"), ("items", "manager")):
            browser = json.loads(protected(prefix + "." + kind, maximum=4096))
            need(browser["status"] == "BROWSER_UI_COOKIE_EXPORT_ONLY_OWNER_PENDING"
                 and browser["database"] == config["databaseName"]
                 and browser["sourceFixtureSha256"] == hashlib.sha256(fixture_bytes).hexdigest()
                 and browser["section"] == kind and browser["actor"] == role,
                 "browser-source-boundary")
            exports.append({"role": role, "kind": kind, "scope": "branch",
                            "query": browser_query(kind, browser, config),
                            "rowCount": browser["rowCount"], "exportId": browser["exportId"],
                            "payloadSha256": browser["payloadSha256"]})
        http = {"database": config["databaseName"], "exports": exports}
    else:
        http = json.loads(protected(os.environ["BE01_PRIVATE_HTTP_RESULT"], maximum=200_000))
    data = reconcile(before, after, http, config)
    write_new(os.environ["BE01_PRIVATE_OWNER_RESULT"], data)


if __name__ == "__main__":
    main()
