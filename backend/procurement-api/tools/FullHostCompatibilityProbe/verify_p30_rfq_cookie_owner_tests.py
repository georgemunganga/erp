"""Pure exact-command vectors. No PostgreSQL, Docker, network or fixture reads."""
import decimal
import hashlib
import unittest
from verify_p30_po_cookie_owner import Failed
from verify_p30_rfq_cookie_owner import captured_body, contact_utc_offset, exact_decimal_text, fingerprint


class RfqOwnerFingerprints(unittest.TestCase):
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
        with self.assertRaises(Failed): contact_utc_offset('2026-10-23T12:00:00')
        with self.assertRaises(Failed): contact_utc_offset('2026-10-23T12:00:00.001Z')

if __name__=='__main__': unittest.main()
