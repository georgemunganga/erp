#!/usr/bin/env python3
"""Pure negative checks; these do not claim HTTP, cookie, or database acceptance."""
import hashlib
import json
import unittest

from verify_http import (ITEM_HEADER, VENDOR_HEADER, assert_formula_protected,
                         audit_denied_items_query, parse_csv)
from verify_owner import browser_query, reconcile


class CsvSourceChecks(unittest.TestCase):
    def test_audit_denial_reaches_insert_after_bounded_item_filter(self):
        config = {"datasets": {"filterQuery": {"items": "?search=BE01FIELD"},
                               "capSearch": "BE01CAP", "exact10000Search": "BE01CAPINCLUDED"}}
        self.assertEqual(audit_denied_items_query(config), "?search=BE01FIELD")
        for unbounded in ("", "?search=BE01CAP", "?search=BE01CAPINCLUDED"):
            config["datasets"]["filterQuery"]["items"] = unbounded
            with self.assertRaises(RuntimeError):
                audit_denied_items_query(config)

    def test_browser_item_query_must_match_bounded_fixture(self):
        config = {"datasets": {"filterQuery": {"items": "?search=BE01FIELD"}}}
        self.assertEqual(browser_query("vendors", {"query": ""}, config), "")
        self.assertEqual(browser_query("items", {"query": "?search=BE01FIELD"}, config),
                         "?search=BE01FIELD")
        for kind, query in (("items", ""), ("items", "?search=BE01CAPINCLUDED"),
                            ("vendors", "?search=BE01FIELD")):
            with self.assertRaises(RuntimeError):
                browser_query(kind, {"query": query}, config)

    def test_exact_utf8_bom_crlf_and_fixed_width(self):
        source = b"\xef\xbb\xbf" + (",".join('"'+x+'"' for x in VENDOR_HEADER)
                 + '\r\n' + ','.join('"x"' for _ in VENDOR_HEADER) + '\r\n').encode()
        self.assertEqual(len(parse_csv(source, VENDOR_HEADER)), 1)
        for malformed in (source[3:], source.replace(b'\r\n', b'\n'),
                          source.replace(b'"x","x"', b'"x"', 1)):
            with self.assertRaises(RuntimeError):
                parse_csv(malformed, VENDOR_HEADER)
        with self.assertRaises(RuntimeError):
            parse_csv(source, ITEM_HEADER)

    def test_spreadsheet_formula_needs_tab_inside_cell(self):
        assert_formula_protected([["\t=1+1"]], (0,))
        for value in ("=1+1", "  @cmd", "\ufeff＋SUM(1)"):
            with self.assertRaises(RuntimeError):
                assert_formula_protected([[value]], (0,))

    def test_owner_rejects_missing_or_substituted_audit(self):
        export_id = '11111111-1111-4111-8111-111111111111'
        actor = '22222222-2222-4222-8222-222222222222'
        company = '33333333-3333-4333-8333-333333333333'
        branch = '44444444-4444-4444-8444-444444444444'
        payload = hashlib.sha256(b'private csv').hexdigest()
        empty_filter = hashlib.sha256(json.dumps({'search': '', 'status': ''},
            separators=(',', ':')).encode()).hexdigest()
        original = {'database': 'proc_csv_export_20261025_pure', 'systemIdentifier': '17',
                    'supplierSha256': 'a', 'supplierCount': 1,
                    'itemSha256': 'b', 'itemCount': 1, 'auditRows': [], 'coreRoles': []}
        audit = {'id': '55555555-5555-4555-8555-555555555555', 'recordId': export_id,
                 'recordType': 'SupplierCsvExport', 'action': 'ExportGenerated',
                 'actor': actor, 'tenant': 'private', 'company': company,
                 'changed': {'schemaVersion': 1, 'rowCount': 1,
                             'payloadSha256': payload, 'filterSha256': empty_filter,
                             'branchId': branch, 'allPermittedBranches': False}}
        after = {**original, 'auditRows': [audit]}
        http = {'database': original['database'], 'exports': [
            {'exportId': export_id, 'kind': 'vendors', 'role': 'reader',
             'rowCount': 1, 'payloadSha256': payload, 'scope': 'branch', 'query': ''}]}
        config = {'actors': {'reader': {'principalId': actor}},
                  'core': {'tenantId': 'private', 'companyAId': company, 'branchAId': branch}}
        self.assertEqual(reconcile(original, after, http, config)['exportAuditCount'], 1)
        for tamper in ({**audit, 'actor': export_id},
                       {**audit, 'changed': {**audit['changed'], 'rowCount': 2}},
                       {**audit, 'recordId': actor}):
            with self.assertRaises(RuntimeError):
                reconcile(original, {**after, 'auditRows': [tamper]}, http, config)
        with self.assertRaises(RuntimeError):
            reconcile(original, {**after, 'supplierSha256': 'changed'}, http, config)
        with self.assertRaises(RuntimeError):
            reconcile(original, original, http, config)
        with self.assertRaises(RuntimeError):
            reconcile(original, {**after, 'auditRows': [audit, audit]}, http, config)


if __name__ == '__main__':
    unittest.main()
