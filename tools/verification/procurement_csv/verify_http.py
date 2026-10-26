#!/usr/bin/env python3
"""Private, normal-cookie BE-01 HTTP probe. Source-only until fixture admission.

The reviewed launcher must provide a fresh, synthetic fixture. This verifier
does not create accounts, grants, records, containers, or databases.
"""
from __future__ import annotations

import csv
import hashlib
import http.cookiejar
import io
import json
import os
from pathlib import Path
import re
import ssl
import urllib.error
import urllib.parse
import urllib.request


VENDOR_HEADER = ["Vendor Number", "Display Name", "Legal Name", "Country Code",
                 "Status", "Orderable", "Created At UTC"]
ITEM_HEADER = ["Item Code", "Name", "Kind", "Category Code", "Unit Code", "Status",
               "Purchasable", "Stock Item", "Asset"]
GUID = re.compile(r"^[0-9a-f]{8}-(?:[0-9a-f]{4}-){3}[0-9a-f]{12}$")
DB = re.compile(r"^proc_csv_export_20261025_[a-z0-9_]{1,24}$")
HEX = re.compile(r"^[a-f0-9]{64}$")


def need(value: bool, code: str) -> None:
    if not value:
        raise RuntimeError(code)


def protected(path: str, *, maximum: int = 65536) -> bytes:
    p = Path(path)
    need(p.is_absolute() and not p.is_symlink() and p.is_file() and p.resolve() == p,
         "private-file-boundary")
    need(p.stat().st_mode & 0o777 == 0o600 and p.stat().st_size <= maximum,
         "private-file-mode-or-size")
    return p.read_bytes()


def parse_csv(raw: bytes, expected_header: list[str]) -> list[list[str]]:
    need(raw.startswith(b"\xef\xbb\xbf"), "csv-bom")
    text = raw[3:].decode("utf-8", "strict")
    need(text.endswith("\r\n"), "csv-crlf")
    # Independent lexical check: every cell is quoted, including empty cells.
    state = "start"
    index = 0
    while index < len(text):
        char = text[index]
        if state == "start":
            need(char == '"', "csv-unquoted-cell")
            state = "quoted"
        elif state == "quoted":
            if char == '"':
                if index + 1 < len(text) and text[index + 1] == '"':
                    index += 1
                else:
                    state = "after"
        else:
            if char == ',':
                state = "start"
            elif char == '\r' and index + 1 < len(text) and text[index + 1] == '\n':
                state = "start"
                index += 1
            else:
                raise RuntimeError("csv-separator")
        index += 1
    need(state == "start", "csv-unterminated")
    reader = csv.reader(io.StringIO(text, newline=""), strict=True)
    rows = list(reader)
    need(bool(rows) and rows[0] == expected_header and
         all(len(row) == len(expected_header) for row in rows), "csv-shape")
    return rows[1:]


def assert_formula_protected(rows: list[list[str]], indices: tuple[int, ...]) -> None:
    for row in rows:
        for column in indices:
            text = row[column]
            leading = text.lstrip("\ufeff\t \r\n\v\f")
            if leading and leading[0] in "=+-@＝＋－＠":
                need(text.startswith("\t"), "csv-formula-unprotected")


def neutralized_formula(value: str) -> bool:
    leading = value.lstrip("\ufeff\t \r\n\v\f")
    return value.startswith("\t") and bool(leading) and leading[0] in "=+-@＝＋－＠"


def admission(raw: bytes, result_path: str, *, result_must_be_fresh: bool = True) -> dict:
    data = json.loads(raw)
    need(data.get("schemaVersion") == 1 and data.get("status") == "READY_PRIVATE_PROCUREMENT_CSV_EXPORT_FIXTURE"
         and data.get("productSourceCommit") == "86ed727774853140fee93a35aa6ce05362e71edf"
         and DB.fullmatch(data.get("databaseName", "")) is not None,
         "wrong-private-fixture")
    need(data.get("transport") == "NORMAL_LOCAL_PASSWORD_AND_SESSION_COOKIE"
         and data.get("productionTouched") is False
         and data.get("employeeFixtureCount") == 0, "wrong-auth-fixture")
    url = urllib.parse.urlparse(data.get("proxyOrigin", ""))
    need(url.scheme == "https" and url.hostname == "127.0.0.1" and url.port is not None
         and not url.username and not url.password and url.path in ("", "/")
         and not url.query and not url.fragment, "nonprivate-url")
    denied_url = urllib.parse.urlparse(data.get("auditDeniedProxyOrigin", ""))
    need(denied_url.scheme == "https" and denied_url.hostname == "127.0.0.1"
         and denied_url.port is not None and denied_url.port != url.port
         and not denied_url.username and not denied_url.password
         and denied_url.path in ("", "/") and not denied_url.query
         and not denied_url.fragment, "audit-denied-origin")
    core = data.get("core", {})
    need(all(GUID.fullmatch(core.get(k, "")) for k in
             ("companyAId", "companyBId", "branchAId", "branchBId", "deniedBranchId")),
         "invalid-scope")
    need(isinstance(data.get("actors"), dict) and
         all(k in data["actors"] for k in ("reader", "manager", "approver",
                                           "invoiceCapture", "noExport", "legacyAdmin")),
         "missing-account-matrix")
    credentials = data.get("credentialsFile", {})
    credential_bytes = protected(credentials.get("path", ""))
    need(hashlib.sha256(credential_bytes).hexdigest() == credentials.get("sha256"),
         "credentials-file-hash")
    data["privateCredentials"] = json.loads(credential_bytes)
    need(isinstance(data.get("datasets"), dict) and
         isinstance(data["privateCredentials"], dict), "missing-owner-fixture")
    datasets = data["datasets"]
    need(isinstance(datasets.get("vendorVisibilityCaseIds"), dict)
         and all(isinstance(datasets["vendorVisibilityCaseIds"].get(role), list)
                 and all(GUID.fullmatch(value) for value in datasets["vendorVisibilityCaseIds"][role])
                 for role in ("reader", "approver", "invoiceCapture"))
         and all(isinstance(datasets.get(name), list)
                 and all(GUID.fullmatch(value) for value in datasets[name])
                 for name in ("itemFieldCaseIds", "itemForbiddenCaseIds"))
         and isinstance(datasets.get("itemVisibleCount"), int)
         and datasets["itemVisibleCount"] == 10_001
         and isinstance(datasets.get("filterQuery"), dict)
         and all(isinstance(datasets["filterQuery"].get(kind), str)
                 for kind in ("vendors", "items"))
         and all(isinstance(datasets.get(name), str)
                 for name in ("emptySearch", "capSearch", "exact10000Search")),
         "owner-fixture-dataset-contract")
    need(Path(result_path).is_absolute() and
         (not result_must_be_fresh or not Path(result_path).exists()) and
         Path(result_path).parent == Path(data["credentialsFile"]["path"]).parent,
         "result-path")
    need(Path(result_path).parent.stat().st_mode & 0o777 == 0o700,
         "evidence-directory-mode")
    protected(data["caCertificatePath"], maximum=16384)
    return data


class Client:
    def __init__(self, config: dict, account: dict, *, denied_audit_host: bool = False):
        self.config = config
        self.account = account
        self.origin = config["auditDeniedProxyOrigin"] if denied_audit_host else config["proxyOrigin"]
        jar = http.cookiejar.CookieJar()
        tls = ssl.create_default_context(cafile=config["caCertificatePath"])
        self.opener = urllib.request.build_opener(
            urllib.request.HTTPSHandler(context=tls), urllib.request.HTTPCookieProcessor(jar))
        self.jar = jar

    def request(self, path: str, method: str = "GET", body: dict | None = None,
                *, scope: str = "branch") -> tuple[int, dict[str, str], bytes]:
        need(path.startswith("/api/") and not path.startswith("//") and "#" not in path,
             "invalid-request-path")
        headers = {"Accept": "application/json, text/csv"}
        core = self.config["core"]
        if scope == "branch":
            headers.update({"X-Shell-Entity": core["companyAId"],
                            "X-Shell-Location": core["branchAId"]})
        elif scope == "all":
            headers.update({"X-Shell-Entity": core["companyAId"],
                            "X-Shell-Branch-Scope": "all"})
        elif scope == "denied_branch":
            headers.update({"X-Shell-Entity": core["companyAId"],
                            "X-Shell-Location": core["deniedBranchId"]})
        elif scope == "other_company":
            headers.update({"X-Shell-Entity": core["companyBId"]})
        else:
            raise RuntimeError("unknown-scope")
        payload = None if body is None else json.dumps(body, separators=(",", ":")).encode()
        if payload is not None:
            headers["Content-Type"] = "application/json"
        req = urllib.request.Request(self.origin + path, payload, headers,
                                     method=method)
        try:
            response = self.opener.open(req, timeout=35)
        except urllib.error.HTTPError as error:
            response = error
        with response:
            raw = response.read(10_000_001)
            need(len(raw) <= 10_000_000, "response-too-large")
            return response.status, dict(response.headers.items()), raw

    def login(self) -> None:
        account = self.account
        credential = self.config["privateCredentials"].get(account.get("credentialKey"), {})
        need(GUID.fullmatch(account.get("principalId", "")) is not None
             and isinstance(credential.get("email"), str)
             and isinstance(credential.get("password"), str), "invalid-private-account")
        code, _, _ = self.request("/api/hrm/auth/login", "POST",
                                  {"email": credential["email"], "password": credential["password"]})
        need(code == 200 and any(cookie.secure for cookie in self.jar), "cookie-login")
        code, _, raw = self.request("/api/hrm/auth/me")
        need(code == 200 and json.loads(raw)["user"]["id"] == account["principalId"],
             "cookie-principal")

    def logout(self) -> None:
        code, _, _ = self.request("/api/hrm/auth/logout", "POST")
        need(code == 200, "cookie-logout")
        code, _, raw = self.request("/api/hrm/auth/me")
        need(code == 200 and json.loads(raw) == {"authenticated": False, "user": None},
             "cookie-postlogout-revocation")


def field(headers: dict[str, str], key: str) -> str:
    return next((v for k, v in headers.items() if k.lower() == key.lower()), "")


def export(client: Client, kind: str, expected: list[str], *, query: str = "",
           scope: str = "branch") -> tuple[str, list[list[str]], str]:
    code, headers, raw = client.request(f"/api/procurement/v1/{kind}/export{query}", scope=scope)
    need(code == 200 and field(headers, "Cache-Control").lower() == "no-store",
         f"{kind}-export-response")
    need(field(headers, "Content-Type").lower().startswith("text/csv") and
         f"{kind}-" in field(headers, "Content-Disposition") and
         ".csv" in field(headers, "Content-Disposition"), f"{kind}-csv-attachment")
    export_id = field(headers, "X-Export-Id")
    need(GUID.fullmatch(export_id) is not None, f"{kind}-export-id")
    rows = parse_csv(raw, expected)
    assert_formula_protected(rows, (0, 1, 2) if kind == "vendors" else (0, 1))
    return export_id, rows, hashlib.sha256(raw).hexdigest()


def list_rows(client: Client, kind: str, *, query: str = "", scope: str = "branch") -> list[dict]:
    rows: list[dict] = []
    for page in range(1, 102):
        suffix = "&" if query else "?"
        code, _, raw = client.request(f"/api/procurement/v1/{kind}{query}{suffix}page={page}&pageSize=100",
                                      scope=scope)
        need(code == 200, f"{kind}-list")
        data = json.loads(raw)
        need(data["page"] == page and len(data["items"]) <= 100, f"{kind}-pagination")
        rows.extend(data["items"])
        if len(rows) >= data["total"]:
            need(len(rows) == data["total"], f"{kind}-total")
            return rows
    raise RuntimeError("bounded-list-page-limit")


def check_json_code(raw: bytes, expected: str) -> None:
    need(json.loads(raw).get("code") == expected, "wrong-error-code")


def audit_denied_items_query(config: dict) -> str:
    query = config["datasets"]["filterQuery"]["items"]
    need(query == "?search=BE01FIELD"
         and query != "?search=" + config["datasets"]["capSearch"]
         and query != "?search=" + config["datasets"]["exact10000Search"],
         "audit-denied-probe-needs-bounded-item-filter")
    return query


def run(config: dict) -> dict:
    proofs: list[dict] = []
    formula_seen = {"vendors": False, "items": False}
    for role, kind, header in (("reader", "vendors", VENDOR_HEADER),
                               ("manager", "items", ITEM_HEADER),
                               ("approver", "vendors", VENDOR_HEADER),
                               ("invoiceCapture", "vendors", VENDOR_HEADER)):
        actor = config["actors"][role]
        required = ("procurement-vendor-read", "procurement-vendor-export") if kind == "vendors" \
            else ("procurement-item-manage", "procurement-item-export")
        need(all(action in actor["currentActions"] for action in required), "fixture-positive-grants")
        client = Client(config, actor)
        client.login()
        context_code, _, context_raw = client.request("/api/procurement/v1/context")
        context = json.loads(context_raw)
        need(context_code == 200 and context["workerId"] is None
             and context["principalId"] == actor["principalId"],
             "workerless-core-principal")
        # The item fixture deliberately has >10k rows; only its bounded
        # filtered cases can return a file. Vendor unfiltered remains small.
        cases = (("branch", ""), ("all", ""),
                 ("branch", config["datasets"]["filterQuery"][kind])) if kind == "vendors" \
            else (("branch", config["datasets"]["filterQuery"][kind]),
                  ("all", config["datasets"]["filterQuery"][kind]))
        for scope, query in cases:
            listed = list_rows(client, kind, query=query, scope=scope)
            keys = [row["number" if kind == "vendors" else "code"] for row in listed]
            export_id, rows, digest = export(client, kind, header, query=query, scope=scope)
            need([row[0] for row in rows] == keys, "list-export-parity")
            formula_seen[kind] |= any(neutralized_formula(cell) for row in rows
                                      for cell in row[:3])
            if not query and kind == "vendors":
                actual_ids = {row["id"] for row in listed}
                expected_ids = config["datasets"]["vendorVisibilityCaseIds"][role]
                need(actual_ids == set(expected_ids) and len(listed) == len(expected_ids),
                     "owner-fixture-vendor-visibility")
            proofs.append({"role": role, "kind": kind, "scope": scope,
                           "query": query, "rowCount": len(rows),
                           "exportId": export_id, "payloadSha256": digest})
        for forbidden_scope in ("denied_branch", "other_company"):
            code, _, _ = client.request(f"/api/procurement/v1/{kind}/export",
                                        scope=forbidden_scope)
            need(code in (403, 404), "cross-scope-export-denial")
        client.logout()
    manager = Client(config, config["actors"]["manager"])
    manager.login()
    complete_items = list_rows(manager, "items")
    complete_ids = {row["id"] for row in complete_items}
    need(len(complete_items) == config["datasets"]["itemVisibleCount"]
         and set(config["datasets"]["itemFieldCaseIds"]) <= complete_ids
         and not (set(config["datasets"]["itemForbiddenCaseIds"]) & complete_ids),
         "owner-fixture-item-visibility")
    empty_query = "?search=" + urllib.parse.quote(config["datasets"]["emptySearch"], safe="")
    empty_id, empty_rows, empty_sha = export(manager, "items", ITEM_HEADER, query=empty_query)
    need(empty_rows == [], "empty-export-not-empty")
    proofs.append({"role": "manager", "kind": "items", "scope": "branch",
                   "query": empty_query, "rowCount": 0, "exportId": empty_id,
                   "payloadSha256": empty_sha})
    for bad_filter in ("?unknown=value", "?search=x&search=y", "?search=" + "x" * 501):
        code, _, raw = manager.request("/api/procurement/v1/items/export" + bad_filter)
        need(code == 400, "invalid-filter-denial")
        check_json_code(raw, "invalid-export-filter")
    cap_query = "?search=" + urllib.parse.quote(config["datasets"]["capSearch"], safe="")
    code, _, raw = manager.request("/api/procurement/v1/items/export" + cap_query)
    need(code == 422, "actual-10001-row-limit")
    check_json_code(raw, "export-limit-exceeded")
    exact_query = "?search=" + urllib.parse.quote(config["datasets"]["exact10000Search"], safe="")
    cap_id, cap_rows, cap_sha = export(manager, "items", ITEM_HEADER, query=exact_query)
    need(len(cap_rows) == 10_000, "exact-10000-row-control")
    proofs.append({"role": "manager", "kind": "items", "scope": "branch",
                   "query": exact_query, "rowCount": 10_000, "exportId": cap_id,
                   "payloadSha256": cap_sha})
    manager.logout()
    denied_writer = Client(config, config["actors"]["manager"], denied_audit_host=True)
    denied_writer.login()
    code, headers, raw = denied_writer.request(
        "/api/procurement/v1/items/export" + audit_denied_items_query(config))
    need(code == 503 and not field(headers, "X-Export-Id") and
         not field(headers, "Content-Type").lower().startswith("text/csv"),
         "audit-insert-denial-must-not-return-csv")
    check_json_code(raw, "export-storage-unavailable")
    denied_writer.logout()
    need(all(formula_seen.values()), "formula-fixture-not-exercised")
    for role in ("legacyAdmin", "noExport"):
        denied = Client(config, config["actors"][role])
        denied.login()
        for kind in ("vendors", "items"):
            code, _, _ = denied.request(f"/api/procurement/v1/{kind}/export")
            need(code == 403, "absent-explicit-export-grant")
        denied.logout()
    return {"status": "HTTP_COOKIE_CHECKS_ONLY_OWNER_PENDING", "schemaVersion": 1,
            "database": config["databaseName"], "productCommit": config["productSourceCommit"],
            "exports": proofs, "browserUiVerified": False, "auditRowsVerified": False,
            "masterRowsUnchangedVerified": False, "productionTouched": False}


def main() -> None:
    path = os.environ["BE01_PRIVATE_FIXTURE"]
    result_path = os.environ["BE01_PRIVATE_HTTP_RESULT"]
    raw = protected(path)
    config = admission(raw, result_path)
    result = run(config)
    out = Path(result_path)
    with out.open("x") as stream:
        os.chmod(out, 0o600)
        json.dump(result, stream, indent=2, sort_keys=True)
        stream.write("\n")


if __name__ == "__main__":
    main()
