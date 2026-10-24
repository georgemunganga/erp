import copy
import unittest
from restore_equivalence import verify_database, verify_restored_privileges


def database():
    return {'database': {'datname': 'source', 'oid': '1', 'datdba': '10', 'datfrozenxid': '50',
        'encoding': 6, 'datcollate': 'en_US.utf8', 'datctype': 'en_US.utf8', 'datlocprovider': 'c',
        'datlocale': None, 'daticurules': None, 'datcollversion': None, 'datallowconn': True,
        'datconnlimit': -1, 'datistemplate': False, 'dathasloginevt': False,
        'datacl': ['=Tc/erp', 'erp=CTc/erp', 'procurement_api=c/erp']}, 'settings': [], 'owner': 'erp'}


def privileges():
    return {'schemas': [{'name': 'procurement', 'owner': 'erp', 'acl': '{procurement_api=U/erp}'}],
        'originalObjects': [{'schema': 'procurement', 'name': f't{i}', 'owner': 'erp', 'acl': None} for i in range(191)],
        'originalColumnAcls': [{'schema': 'procurement', 'table': 't0', 'column': f'c{i}', 'acl': None} for i in range(3464)],
        'defaultAcls': [{'role': 'erp', 'schema': 'procurement', 'kind': 'r', 'acl': '{procurement_api=arwd/erp}'}],
        'memberships': [], 'roles': [{'name': 'erp', 'login': True, 'superuser': True, 'createDb': True,
            'createRole': True, 'replication': True, 'bypassRls': True, 'inherit': True, 'config': None},
            {'name': 'procurement_api', 'login': True, 'superuser': False, 'inherit': True, 'config': None}]}


class DatabaseEquivalence(unittest.TestCase):
    def test_renamed_private_database_and_physical_oid_changes_are_not_owner_changes(self):
        source = database(); target = copy.deepcopy(source)
        target['database'].update(datname='private', oid='900', datdba='800', datfrozenxid='1000')
        verify_database(source, 'erp', target)

    def test_acl_order_and_permission_order_are_semantic(self):
        source = database(); target = copy.deepcopy(source)
        target['database']['datacl'] = ['procurement_api=c/erp', 'erp=cTC/erp', '=cT/erp']
        verify_database(source, 'erp', target)

    def test_wrong_database_owner_is_denied(self):
        source = database(); target = copy.deepcopy(source); target['owner'] = 'postgres'
        with self.assertRaises(RuntimeError): verify_database(source, 'erp', target)

    def test_missing_explicit_runtime_database_acl_is_denied(self):
        source = database(); target = copy.deepcopy(source); target['database']['datacl'].pop()
        with self.assertRaises(RuntimeError): verify_database(source, 'erp', target)

    def test_extra_database_grant_option_is_denied(self):
        source = database(); target = copy.deepcopy(source); target['database']['datacl'][-1] = 'procurement_api=c*/erp'
        with self.assertRaises(RuntimeError): verify_database(source, 'erp', target)

    def test_locale_encoding_provider_and_connection_controls_cannot_differ(self):
        for field, value in [('encoding', 8), ('datcollate', 'C'), ('datctype', 'C'), ('datlocprovider', 'i'),
                             ('datcollversion', 'different'), ('datconnlimit', 10), ('datallowconn', False)]:
            with self.subTest(field=field):
                source = database(); target = copy.deepcopy(source); target['database'][field] = value
                with self.assertRaises(RuntimeError): verify_database(source, 'erp', target)

    def test_database_role_setting_drift_is_denied(self):
        source = database(); target = copy.deepcopy(source); target['settings'] = [{'role': 'erp', 'config': ['search_path=other']}]
        with self.assertRaises(RuntimeError): verify_database(source, 'erp', target)

    def test_incomplete_or_malformed_database_acl_fails_closed(self):
        for acl in [None, ['procurement_api=**/erp'], ['unparseable']]:
            with self.subTest(acl=acl):
                source = database(); target = copy.deepcopy(source); target['database']['datacl'] = acl
                with self.assertRaises(RuntimeError): verify_database(source, 'erp', target)


class OriginalPrivilegeEquivalence(unittest.TestCase):
    def setUp(self):
        # Every denied mutation starts from an accepted complete baseline.
        baseline = privileges()
        verify_restored_privileges(baseline, copy.deepcopy(baseline), 191, 3464)

    def test_private_owner_runtime_login_and_elevation_flags_may_differ_only_as_reviewed(self):
        source = privileges(); target = copy.deepcopy(source)
        for role in target['roles']:
            for flag in ('login', 'superuser', 'createDb', 'createRole', 'replication', 'bypassRls'):
                if flag in role: role[flag] = False
        target['originalObjects'].reverse(); target['originalColumnAcls'].reverse()
        verify_restored_privileges(source, target, 191, 3464)

    def test_original_table_owner_change_is_denied(self):
        source = privileges(); target = copy.deepcopy(source); target['originalObjects'][0]['owner'] = 'postgres'
        with self.assertRaises(RuntimeError): verify_restored_privileges(source, target, 191, 3464)

    def test_original_schema_or_table_acl_change_is_denied(self):
        for key in ('schemas', 'originalObjects'):
            with self.subTest(key=key):
                source = privileges(); target = copy.deepcopy(source); target[key][0]['acl'] = '{procurement_api=arwdD/erp}'
                with self.assertRaises(RuntimeError): verify_restored_privileges(source, target, 191, 3464)

    def test_original_column_acl_change_is_denied(self):
        source = privileges(); target = copy.deepcopy(source); target['originalColumnAcls'][0]['acl'] = '{procurement_api=w/erp}'
        with self.assertRaises(RuntimeError): verify_restored_privileges(source, target, 191, 3464)

    def test_missing_original_object_is_denied(self):
        source = privileges(); target = copy.deepcopy(source); target['originalObjects'].pop()
        with self.assertRaises(RuntimeError): verify_restored_privileges(source, target, 191, 3464)

    def test_default_acl_change_is_denied(self):
        source = privileges(); target = copy.deepcopy(source); target['defaultAcls'][0]['acl'] = '{procurement_api=arwdD/erp}'
        with self.assertRaises(RuntimeError): verify_restored_privileges(source, target, 191, 3464)

    def test_new_role_membership_is_denied(self):
        source = privileges(); target = copy.deepcopy(source); target['memberships'] = [{'role': 'erp', 'member': 'procurement_api'}]
        with self.assertRaises(RuntimeError): verify_restored_privileges(source, target, 191, 3464)

    def test_nonflag_role_configuration_change_is_denied(self):
        source = privileges(); target = copy.deepcopy(source); target['roles'][0]['config'] = ['search_path=other']
        with self.assertRaises(RuntimeError): verify_restored_privileges(source, target, 191, 3464)


if __name__ == '__main__': unittest.main()
