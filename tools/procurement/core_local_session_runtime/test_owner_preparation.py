"""Pure admission and exact-role SQL checks; no Docker, SDK or database access."""
import importlib.util
import hashlib
import tempfile
from pathlib import Path
import unittest
from unittest.mock import patch
from types import SimpleNamespace

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

    def test_secret_sql_sets_only_session_startup_logging_controls(self):
        calls=[]
        def record(command,**kwargs):
            calls.append((command,kwargs))
            return SimpleNamespace(returncode=0,stdout=b'',stderr=b'')
        with patch.object(owner.subprocess,'run',side_effect=record):
            owner.sql(owner.TARGET,'CREATE ROLE "fixture" PASSWORD \'private\';',secret=True)
            owner.sql(owner.TARGET,'SELECT 1',secret=False)
        secret_command,secret_input=calls[0]
        ordinary_command,_=calls[1]
        self.assertIn('log_statement=none',secret_command[4])
        self.assertIn('log_min_error_statement=panic',secret_command[4])
        self.assertNotIn('log_statement=none',ordinary_command[4])
        self.assertNotIn('private',str(secret_command))
        self.assertIn(b'private',secret_input['input'])

    def test_reviewed_source_commit_and_every_physical_blob_required(self):
        with tempfile.TemporaryDirectory() as tmp:
            root=Path(tmp);data=b'fixture source\n'
            oid=hashlib.sha1(b'blob '+str(len(data)).encode()+b'\0'+data).hexdigest()
            raw=f'100644 blob {oid}\ttracked.cs\0'.encode()
            commit='a'*40;digest=hashlib.sha256(raw).hexdigest()
            replies={('--show-toplevel',):str(root),('HEAD',):commit,('--show-object-format',):'sha1'}
            def git(command,**kwargs):
                if command[1:3]==['ls-tree','-rz']:return raw
                if command[1]=='status':return ''
                return replies[tuple(command[2:])]
            with patch.object(owner.subprocess,'check_output',side_effect=git):
                with self.assertRaisesRegex(RuntimeError,'tracked-source-file-missing'):
                    owner.verify_source(root,commit,digest,1)
                (root/'tracked.cs').write_bytes(data)
                owner.verify_source(root,commit,digest,1)
                with self.assertRaisesRegex(RuntimeError,'exact-clean-Core-source-required'):
                    owner.verify_source(root,'b'*40,digest,1)
                with self.assertRaisesRegex(RuntimeError,'exact-reviewed-source-tree-required'):
                    owner.verify_source(root,commit,'0'*64,1)
                with self.assertRaisesRegex(RuntimeError,'exact-reviewed-source-tree-required'):
                    owner.verify_source(root,commit,digest,2)
                (root/'tracked.cs').write_bytes(data+b'drift')
                with self.assertRaisesRegex(RuntimeError,'tracked-source-byte-drift'):
                    owner.verify_source(root,commit,digest,1)
    def test_malformed_source_pin_denies_without_subprocess(self):
        with patch.object(owner.subprocess,'check_output') as called:
            for commit in ('','A'*40,'a'*39,'a'*41):
                with self.assertRaises(RuntimeError):owner.verify_source(Path('/never-used'),commit,'a'*64,1)
            called.assert_not_called()

if __name__=='__main__':unittest.main()
