import copy
import unittest

from verify_p30_receiving_cookie_owner import Failed, command_canonical_json, stored_json_object, source_line_acceptance


class SourceLineAcceptanceTests(unittest.TestCase):
    def setUp(self):
        self.actor = '11111111-1111-4111-8111-111111111111'
        self.before = {'id': 'line', 'quantity': '1.0000', 'unit_price': '0.0001',
            'currency_code': 'ZMW', 'accepted_quantity': '0', 'version': 3,
            'updated_by': None, 'updated_at': None,
            'created_at': '2026-10-23T17:00:00+00:00'}
        self.after = {**self.before, 'accepted_quantity': '1', 'version': 4,
            'updated_by': self.actor, 'updated_at': '2026-10-24T17:00:00+00:00'}

    def test_acceptance_requires_the_acceptor_and_one_version_increment(self):
        self.assertEqual(source_line_acceptance(self.before, self.after, self.actor).isoformat(),
            self.after['updated_at'])

    def test_wrong_actor_or_extra_version_increment_is_denied(self):
        for key, value in [('updated_by', 'another-actor'), ('version', 5)]:
            with self.subTest(field=key), self.assertRaises(Failed):
                source_line_acceptance(self.before, {**self.after, key: value}, self.actor)

    def test_quantity_price_currency_and_identity_changes_remain_denied(self):
        for key, value in [('quantity', '2'), ('unit_price', '0.0002'),
                ('currency_code', 'USD'), ('id', 'other-line')]:
            with self.subTest(field=key), self.assertRaisesRegex(Failed,
                    'SourceLineCommercialFieldsChanged'):
                source_line_acceptance(self.before, {**self.after, key: value}, self.actor)

    def test_acceptance_counter_and_time_must_be_valid(self):
        for change in [{'accepted_quantity': '0'}, {'accepted_quantity': '2'},
                {'updated_at': None}, {'updated_at': '2026-10-24T17:00:00'},
                {'updated_at': '2026-10-24T17:00:00+02:00'}]:
            with self.subTest(change=change), self.assertRaises(Failed):
                source_line_acceptance(self.before, {**self.after, **change}, self.actor)

    def test_audit_time_cannot_move_backwards(self):
        before = copy.deepcopy(self.before)
        before['updated_at'] = '2026-10-24T17:00:01+00:00'
        with self.assertRaisesRegex(Failed, 'SourceLineAcceptanceTimeReversed'):
            source_line_acceptance(before, self.after, self.actor)

    def test_audit_time_cannot_precede_creation(self):
        with self.assertRaisesRegex(Failed, 'SourceLineAcceptanceBeforeCreation'):
            source_line_acceptance(self.before,
                {**self.after, 'updated_at': '2026-10-22T17:00:00+00:00'}, self.actor)

    def test_command_timestamp_uses_default_dotnet_plus_escape(self):
        self.assertEqual(command_canonical_json({'when': '2026-10-24T17:00:00+00:00'}),
            '{"when":"2026-10-24T17:00:00\\u002B00:00"}')
        self.assertEqual(command_canonical_json({'operation': 'Accept', 'version': 2}),
            '{"operation":"Accept","version":2e0}')

    def test_jsonb_policy_object_and_text_have_the_same_canonical_form(self):
        self.assertEqual(command_canonical_json(stored_json_object({'schemaVersion': 2}, 'Malformed')),
            command_canonical_json(stored_json_object('{"schemaVersion":2}', 'Malformed')))
        for value in [None, [], '[]', 'invalid']:
            with self.subTest(value=value), self.assertRaises(Failed):
                stored_json_object(value, 'Malformed')


if __name__ == '__main__':
    unittest.main()
