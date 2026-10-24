"""Pure admission and exact-role SQL checks; no Docker, SDK or database access."""
import importlib.util
from pathlib import Path
import unittest

HERE=Path(__file__).resolve().parent
spec=importlib.util.spec_from_file_location('owner_prep',HERE/'prepare_owner.py')
owner=importlib.util.module_from_spec(spec);spec.loader.exec_module(owner)

class OwnerPreparationSourceTests(unittest.TestCase):
    def test_only_three_noninheriting_finite_roles_and_no_member_or_owner_grants(self):
        roles=('p30_core_session_login_01','p30_record_login_01','p30_execute_login_01')
        sql=owner.role_sql(roles,['a'*64,'b'*64,'c'*64])
        self.assertEqual(sql.count('CREATE ROLE '),3)
        self.assertEqual(sql.count('LOGIN NOINHERIT NOSUPERUSER NOCREATEDB NOCREATEROLE NOREPLICATION NOBYPASSRLS'),3)
        self.assertEqual(sql.count('PASSWORD '),3)
        self.assertNotIn('CREATE USER',sql)
        self.assertNotIn('GRANT ',sql)
        self.assertNotIn('ALTER SYSTEM',sql)
        self.assertEqual(sql.count('COMMIT;'),1)

    def test_wrong_alias_or_weak_secret_fails_before_sql(self):
        for roles,pw in [(['p30_core_session_login_02','p30_record_login_01','p30_execute_login_01'],['a'*64]*3),
                         (['p30_core_session_login_01','p30_record_login_01','p30_execute_login_01'],['short']*3),
                         (['postgres','p30_record_login_01','p30_execute_login_01'],['a'*64]*3)]:
            with self.assertRaises(RuntimeError):owner.role_sql(roles,pw)

    def test_core_contract_keeps_exact_target_and_separate_source(self):
        self.assertEqual(owner.TARGET,'proc_p30_core_session_20261024_01')
        self.assertEqual(owner.SOURCE,'proc_p30_stageb_20261024_01')
        self.assertNotEqual(owner.TARGET,owner.SOURCE)
        self.assertEqual(owner.core_profile.aliases(owner.TARGET),
            ('p30_core_session_login_01','p30_record_login_01','p30_execute_login_01'))

if __name__=='__main__':unittest.main()
