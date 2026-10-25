#!/usr/bin/env python3
"""Pure boundary negatives; real owner rows still require private PostgreSQL proof."""
import importlib.util
import json
from pathlib import Path
import unittest

SOURCE = Path(__file__).with_name('verify_p33_po_cookie_owner.py')
spec = importlib.util.spec_from_file_location('p33_po_cookie_owner', SOURCE)
owner = importlib.util.module_from_spec(spec)
spec.loader.exec_module(owner)


def order(value, status):
    return {'id': value, 'status': status}


def key(number):
    return f'{number:08d}-1111-4111-8111-111111111111'


class BoundaryTests(unittest.TestCase):
    def setUp(self):
        self.fixture = {'issuedOrderId': key(1), 'cancelledNativeEditOrderId': key(2)}
        self.native = [order(key(1), 'Issued'), order(key(2), 'Cancelled'),
                       order(key(3), 'Cancelled'), order(key(4), 'Draft')]

    def test_requires_exact_four_native_and_one_browser_successor(self):
        self.assertEqual(len(owner.native_orders_for_phase(self.native, self.fixture)), 4)
        final = self.native + [order(key(5), 'Cancelled')]
        self.assertEqual(len(owner.native_orders_for_phase(final, self.fixture,
            [x['id'] for x in self.native], key(5))), 4)
        with self.assertRaises(owner.Failed):
            owner.native_orders_for_phase(self.native[:3], self.fixture)
        with self.assertRaises(owner.Failed):
            owner.native_orders_for_phase(final + [order(key(6), 'Draft')], self.fixture,
                [x['id'] for x in self.native], key(5))
        with self.assertRaises(owner.Failed):
            owner.native_orders_for_phase(final, self.fixture,
                [x['id'] for x in self.native], key(2))

    def test_missing_native_edit_is_not_an_old_po_cookie_cohort(self):
        changed = [dict(row) for row in self.native]
        changed[1]['status'] = 'Draft'
        with self.assertRaises(owner.Failed):
            owner.native_orders_for_phase(changed, self.fixture)

    def test_documented_edit_cas_and_exact_amounts_are_not_inferred_from_result(self):
        self.assertEqual(owner.exact('0.5000'), owner.exact('0.5'))
        with self.assertRaises(owner.Failed):
            owner.exact('not-a-number')

    def test_only_buyers_login_counters_are_excluded_from_core_identity_hash(self):
        buyer, unrelated = key(7), key(8)
        original = {'id': buyer, 'tenant_id': 'private', 'worker_id': None,
                    'password_hash': 'synthetic', 'roles_csv': 'buyer',
                    'last_login_at': None, 'failed_login_count': 0,
                    'locked_until': None, 'updated_at': 'before', 'version': 1}
        logged_in = dict(original, last_login_at='after', failed_login_count=1,
                         updated_at='after', version=2)
        self.assertEqual(owner.digest(owner.stable_local_user(original, buyer)),
                         owner.digest(owner.stable_local_user(logged_in, buyer)))
        self.assertNotEqual(owner.digest(owner.stable_local_user(original, unrelated)),
                            owner.digest(owner.stable_local_user(logged_in, unrelated)))
        for field, changed in [('roles_csv', 'admin'), ('worker_id', key(9)),
                               ('password_hash', 'different'), ('tenant_id', 'other')]:
            modified = dict(logged_in, **{field: changed})
            self.assertNotEqual(owner.digest(owner.stable_local_user(original, buyer)),
                                owner.digest(owner.stable_local_user(modified, buyer)))

    def test_exact_revoked_preflight_and_one_revoked_browser_session(self):
        preflight, browser = key(31), key(32)
        def session(identity, revoked='2026-10-25T01:01:00+00:00'):
            return {'id': identity, 'revokedAt': revoked,
                    'expiresAt': '2026-10-25T02:00:00+00:00'}
        def facts(items):
            return {'buyerSessionIds': [x['id'] for x in items],
                    'buyerSessions': items,
                    'sessionRows': [{'id': x['id'], 'revoked_at': x['revokedAt'],
                        'expires_at': x['expiresAt'], 'token_hash': 'original'} for x in items]}
        original = facts([session(preflight)])
        after = facts([session(preflight), session(browser,
            '2026-10-25T01:07:00+00:00')])
        self.assertEqual(owner.revoked_preflight_session(original)['id'], preflight)
        self.assertIsNone(owner.browser_session_transition(original, after))
        for bad in (facts([]), facts([session(preflight), session(browser)]),
                    facts([session(preflight, None)]),
                    facts([session(preflight, '2026-10-25T03:00:00+00:00')])):
            with self.assertRaises(owner.Failed):
                owner.revoked_preflight_session(bad)
        for bad in (facts([session(preflight)]),
                    facts([session(preflight), session(browser, None)]),
                    facts([session(preflight, '2026-10-25T01:05:00+00:00'), session(browser)]),
                    facts([session(preflight), session(browser), session(key(33))])):
            with self.assertRaises(owner.Failed):
                owner.browser_session_transition(original, bad)
        changed_old_row = facts([session(preflight), session(browser)])
        changed_old_row['sessionRows'][0]['token_hash'] = 'substituted'
        with self.assertRaisesRegex(owner.Failed, 'PreflightLocalSessionRowChanged'):
            owner.browser_session_transition(original, changed_old_row)

    def test_three_command_fingerprints_bind_edit_version_original_and_exact_quantity(self):
        buyer, request, source, supplier, po, line = [key(n) for n in range(11, 17)]
        fixture = {'buyerPrincipalId': buyer, 'approvedRequestId': request,
                   'approvedRequestLineId': source, 'supplierId': supplier}
        draft = {'supplierId': supplier, 'currencyCode': 'ZMW', 'shipToRef': '',
                 'billToRef': '', 'paymentTermRef': '', 'expectedDeliveryOn': None,
                 'lines': [{'id': None, 'description': 'Exact Core principal source',
                            'unitCode': 'each', 'quantity': 1, 'unitPrice': 5.1234,
                            'taxAmount': 0}]}
        edit_draft = json.loads(json.dumps(draft))
        edit_draft['lines'][0]['id'] = line
        edit_draft['lines'][0]['quantity'] = 0.5
        sha = 'a' * 64
        browser = {'orderId': po, 'createdVersion': 1, 'editedVersion': 2,
                   'createRequestBody': json.dumps({'draft': draft, 'sources': [{
                       'draftLineIndex': 0, 'purchaseRequestId': request,
                       'purchaseRequestLineId': source, 'expectedRequestVersion': 7}]}),
                   'editRequestBody': json.dumps({'expectedOrderVersion': 1,
                       'originalDirectSourceSha256': sha,
                       'reason': 'Reduce uncommitted quantity', 'draft': edit_draft}),
                   'cancelRequestBody': json.dumps({'expectedOrderVersion': 2,
                       'originalDirectSourceSha256': sha, 'reason': 'Private PO draft cancellation'})}
        pins = {'request_version': 7}
        values = owner.command_fingerprints(browser, fixture,
            {'original_direct_source_sha256': sha}, pins, {'id': line})
        self.assertEqual(set(values), {'Create', 'Edit', 'Cancel'})
        self.assertTrue(all(owner.HEX.fullmatch(value) for value in values.values()))
        changed = dict(browser)
        changed['editRequestBody'] = browser['editRequestBody'].replace('0.5', '0.6')
        with self.assertRaises(owner.Failed):
            owner.command_fingerprints(changed, fixture,
                {'original_direct_source_sha256': sha}, pins, {'id': line})


if __name__ == '__main__':
    unittest.main()
