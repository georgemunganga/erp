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
