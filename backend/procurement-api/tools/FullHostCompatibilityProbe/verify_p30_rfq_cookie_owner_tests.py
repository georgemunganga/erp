"""Pure exact-command vectors. No PostgreSQL, Docker, network or fixture reads."""
import decimal
import hashlib
import unittest
from verify_p30_po_cookie_owner import Failed, canonical_json
from verify_p30_rfq_cookie_owner import (CONTACT_PRODUCER_VECTOR, captured_body,
    contact_utc_offset, core_row, exact_decimal_text, fingerprint, rfq_canonical_json,
    verify_source_progress)


class RfqOwnerFingerprints(unittest.TestCase):
    def test_core_pascal_case_preserves_all_fields_and_rejects_collision(self):
        value = {'Id':'11111111-1111-4111-8111-111111111111',
            'TenantId':'22222222-2222-4222-8222-222222222222',
            'ActorSubjectId':'33333333-3333-4333-8333-333333333333',
            'State':'Approved','SHA256':'abc'}
        normalized = core_row(value)
        self.assertEqual(normalized['actor_subject_id'],value['ActorSubjectId'])
        self.assertEqual(normalized['state'],'Approved')
        self.assertEqual(len(normalized),len(value))
        with self.assertRaises(Failed): core_row({**value,'id':value['Id']})
        with self.assertRaises(Failed): core_row({'Id':None})

    def test_exact_source_reservation_and_audit_progress(self):
        buyer='11111111-1111-4111-8111-111111111111'
        stamp='2026-10-24T10:00:00+00:00'
        line={'id':'a','allocated_quantity':'1.0000','approved_quantity':'2.0000',
            'ordered_quantity':'0.0000','accepted_quantity':'0.0000','route':'Approved',
            'version':7,'created_at':stamp,'updated_at':None,'updated_by':None}
        request={'id':'r','version':4,'created_at':stamp,'updated_at':None,'updated_by':None,
            'status':'Approved'}
        before={'source':line,'request':request}
        next_stamp='2026-10-24T10:01:00+00:00'
        after={'source':{**line,'allocated_quantity':'2.0000','route':'PendingSourcing',
            'version':8,'updated_at':next_stamp,'updated_by':buyer},
            'request':{**request,'version':5,'updated_at':next_stamp,'updated_by':buyer}}
        verify_source_progress(before,after,buyer)
        for mutation in (
            {'source':{**after['source'],'updated_by':'other'}},
            {'source':{**after['source'],'version':9}},
            {'source':{**after['source'],'ordered_quantity':'1.0000'}},
            {'source':{**after['source'],'allocated_quantity':'1.0000'}},
            {'request':{**after['request'],'updated_at':stamp}},
            {'request':{**after['request'],'status':'Cancelled'}}):
            with self.subTest(mutation=mutation), self.assertRaises(Failed):
                verify_source_progress(before,{**after,**mutation},buyer)

    def test_canonical_property_order_and_numeric_spellings(self):
        expected = hashlib.sha256(b'{"operation":"Create","quantity":"1","schemaVersion":2e0}').hexdigest()
        self.assertEqual(fingerprint({"schemaVersion":2,"operation":"Create","quantity":"1"}), expected)
        self.assertEqual(fingerprint({"quantity":"1","operation":"Create","schemaVersion":decimal.Decimal('2.000')}), expected)
        self.assertNotEqual(fingerprint({"schemaVersion":2,"operation":"Cancel","quantity":"1"}), expected)

    def test_exact_quantity_text(self):
        self.assertEqual(exact_decimal_text('1.0000'), '1')
        self.assertEqual(exact_decimal_text('0.0001'), '0.0001')
        with self.assertRaises(Failed): exact_decimal_text('0.00001')
        with self.assertRaises(Failed): exact_decimal_text('0')

    def test_duplicate_or_extra_command_key_rejected(self):
        with self.assertRaises(Failed):
            captured_body({'create': {'body':'{"title":"A","title":"B"}'}}, 'create', {'title'})
        with self.assertRaises(Failed):
            captured_body({'create': {'body':'{"title":"A","extra":1}'}}, 'create', {'title'})
        self.assertEqual(captured_body({'create': {'body':'{"title":"A"}'}}, 'create', {'title'}), {'title':'A'})

    def test_exact_decimal_quote_value_not_float(self):
        value = {'schemaVersion':2,'operation':'CreateManualQuote','unitPrice':decimal.Decimal('0.0001')}
        actual = fingerprint(value)
        expected = hashlib.sha256(b'{"operation":"CreateManualQuote","schemaVersion":2e0,"unitPrice":1e-4}').hexdigest()
        self.assertEqual(actual, expected)
        self.assertNotEqual(actual, fingerprint({**value,'unitPrice':decimal.Decimal('0.0002')}))

    def test_contact_datetimeoffset_canonicalization_is_bounded(self):
        self.assertEqual(contact_utc_offset('2026-10-23T12:00:00.000Z'), '2026-10-23T12:00:00+00:00')
        expected = (b'{"channel":"EmailOutsideSystem","eventId":"11111111-1111-4111-8111-111111111111",'
            b'"invitationId":"22222222-2222-4222-8222-222222222222",'
            b'"operation":"RecordManualContact","reference":"outside-ref",'
            b'"reportedContactAt":"2026-10-23T12:00:00\\u002B00:00","schemaVersion":2e0}')
        self.assertEqual(rfq_canonical_json(CONTACT_PRODUCER_VECTOR).encode(), expected)
        self.assertEqual(fingerprint(CONTACT_PRODUCER_VECTOR), hashlib.sha256(expected).hexdigest())
        self.assertNotEqual(fingerprint(CONTACT_PRODUCER_VECTOR),
            hashlib.sha256(canonical_json(CONTACT_PRODUCER_VECTOR).encode()).hexdigest())
        with self.assertRaises(Failed): contact_utc_offset('2026-10-23T12:00:00')
        with self.assertRaises(Failed): contact_utc_offset('2026-10-23T12:00:00.001Z')

if __name__=='__main__': unittest.main()
