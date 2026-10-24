"""Pure fail-closed identity/role guard cases; no Docker or database access."""
import copy
import unittest

from rehearse import validate_system_identifiers, verify_finite_role_state


SOURCE = '7683513820537307167'


def identifiers():
    return {'sourceBackupSystemIdentifier': SOURCE,
            'productionSystemIdentifier': SOURCE,
            'privateSystemIdentifier': '7698678713374847009'}


def roles():
    return [{'name': name, 'login': False, 'super': False,
             'createdb': False, 'createrole': False, 'replication': False,
             'bypassrls': False, 'inherit': True, 'connlimit': -1,
             'validUntil': None}
            for name in ('erp', 'procurement_api')]


class PrivateSystemIdentifierGuard(unittest.TestCase):
    def test_exact_pinned_source_and_distinct_private_pass(self):
        validate_system_identifiers(identifiers())

    def test_plan_cannot_relabel_production_system(self):
        plan = identifiers(); plan['productionSystemIdentifier'] = '9999999999999999999'
        with self.assertRaises(RuntimeError): validate_system_identifiers(plan)

    def test_private_system_cannot_equal_source_or_be_missing(self):
        for value in (SOURCE, None, 'invalid'):
            with self.subTest(value=value):
                plan = identifiers(); plan['privateSystemIdentifier'] = value
                with self.assertRaises(RuntimeError): validate_system_identifiers(plan)


class PrivateRoleGuard(unittest.TestCase):
    def test_existing_finite_roles_without_membership_pass(self):
        self.assertEqual(verify_finite_role_state(roles(), [])['memberships'], [])

    def test_role_elevation_or_login_denied(self):
        for field in ('login', 'super', 'createdb', 'createrole', 'replication', 'bypassrls'):
            with self.subTest(field=field):
                changed = copy.deepcopy(roles()); changed[0][field] = True
                with self.assertRaises(RuntimeError): verify_finite_role_state(changed, [])

    def test_missing_role_field_denied(self):
        changed = roles(); changed[0].pop('bypassrls')
        with self.assertRaises(RuntimeError): verify_finite_role_state(changed, [])

    def test_member_or_grantee_relation_denied(self):
        for membership in ({'role': 'erp', 'member': 'other'},
                           {'role': 'other', 'member': 'procurement_api'}):
            with self.subTest(membership=membership):
                with self.assertRaises(RuntimeError): verify_finite_role_state(roles(), [membership])


if __name__ == '__main__':
    unittest.main()
