import copy
import hashlib
import importlib.util
import json
import os
import pathlib
import tempfile
import unittest
from contracts import names, validate_output, login_proof, verify_acl, grant_sql, first_local_scram, structural, SWITCH
from run import admit

class SourceHelper:
    @staticmethod
    def ident(v): return '"'+v+'"'
    @staticmethod
    def literal(v): return "'"+v+"'"
    @classmethod
    def proof_sql(cls,role,*_):
        return ('BEGIN;\nSET SESSION AUTHORIZATION '+cls.ident(role)+';\nDO $finite$ BEGIN\n'
                'IF session_user<>'+cls.literal(role)+' OR current_user<>'+cls.literal(role)+" THEN RAISE EXCEPTION 'FiniteSessionIdentityMismatch'; END IF;\n"
                "BEGIN EXECUTE 'SET ROLE erp'; RAISE EXCEPTION 'UnexpectedFinitePrivilegeSuccess' USING ERRCODE='55000'; EXCEPTION WHEN insufficient_privilege THEN NULL; END;\n"
                "EXECUTE 'SELECT exact_source_column FROM hrm.example WHERE false';\nEND $finite$;\nROLLBACK;\n")

class TupleHelper:
    GRANTEE={'table':2,'column':3,'schema':1}
    @staticmethod
    def tuples(value): return {k:{tuple(row) for row in rows} for k,rows in value.items()}

class ContractsTests(unittest.TestCase):
    def test_names_do_not_adopt_source_or_nonbounded_names(self):
        self.assertEqual(names('proc_p30_login_20261024_02'),('p30_record_login_02','p30_execute_login_02'))
        for value in ['proc_p30_stageb_20261024_01','erp','proc_p30_login_20261024_2','proc_p30_login_20261024_02;DROP DATABASE erp']:
            with self.assertRaises(RuntimeError): names(value)

    def test_adaptation_preserves_exact_body_without_identity_switch(self):
        proof,metadata=login_proof(SourceHelper,{}, {},'record','p30_record_login_02')
        self.assertIn("session_user<>'p30_record_login_02' OR current_user<>'p30_record_login_02'",proof)
        self.assertIn("EXECUTE 'SELECT exact_source_column FROM hrm.example WHERE false';",proof)
        self.assertIn("pg_has_role(current_user,'erp','SET')",proof)
        self.assertFalse(SWITCH.search(proof));self.assertTrue(proof.endswith('ROLLBACK;\n'))
        self.assertEqual(metadata['adaptedSha256'],hashlib.sha256(proof.encode()).hexdigest())

    def test_changed_adaptation_boundary_fails(self):
        class Changed(SourceHelper):
            @classmethod
            def proof_sql(cls,role,*args): return super().proof_sql(role,*args).replace('SET ROLE erp','SET ROLE other')
        with self.assertRaises(RuntimeError): login_proof(Changed,{}, {},'execution','p30_execute_login_02')
        with self.assertRaises(RuntimeError): login_proof(SourceHelper,{}, {},'execution','p30_record_login_02')

    def test_unexpected_additional_switch_denied(self):
        class Changed(SourceHelper):
            @classmethod
            def proof_sql(cls,role,*args): return super().proof_sql(role,*args).replace('DO $finite$', 'SET LOCAL ROLE erp;\nDO $finite$')
        with self.assertRaises(RuntimeError): login_proof(Changed,{}, {},'record','p30_record_login_02')

    def test_exact_acl_union_no_other_grantee_grantor_option_default_change(self):
        before={'table':[['procurement','old','erp','erp','SELECT',False]],'column':[],
                'schema':[['procurement','erp','erp','USAGE',False]],'default':[['erp','procurement','r','procurement_api','erp','SELECT',False]]}
        target={'table':{('procurement','old','p30_record_login_02','erp','SELECT',False)},'column':set(),'schema':set()}
        after=copy.deepcopy(before);after['table']+=list(map(list,target['table']))
        verify_acl(TupleHelper,before,after,target)
        for kind,index,new in [('table',0,['procurement','old','erp','wrong','SELECT',False]),
                               ('table',1,['procurement','old','p30_record_login_02','erp','SELECT',True]),
                               ('default',0,['erp','procurement','r','procurement_api','erp','UPDATE',False])]:
            changed=copy.deepcopy(after);changed[kind][index]=new
            with self.assertRaises(RuntimeError): verify_acl(TupleHelper,before,changed,target)
        changed=copy.deepcopy(after);changed['column'].append(['procurement','old','secret','p30_record_login_02','erp','UPDATE',False])
        with self.assertRaises(RuntimeError): verify_acl(TupleHelper,before,changed,target)
        with self.assertRaises(RuntimeError): verify_acl(TupleHelper,after,after,target)

    def test_grants_only_exact_alias_tuple_and_no_default_or_switch(self):
        target={'table':{('procurement','old','p30_record_login_02','erp','SELECT',False)},
                'column':{('hrm','local_users','id','p30_execute_login_02','erp','SELECT',False)},
                'schema':{('hrm','p30_execute_login_02','erp','USAGE',False)}}
        sql=grant_sql(target)
        self.assertIn('GRANT SELECT ("id") ON TABLE "hrm"."local_users" TO "p30_execute_login_02";',sql)
        self.assertNotIn('PASSWORD',sql);self.assertNotIn('DEFAULT PRIVILEGES',sql);self.assertFalse(SWITCH.search(sql))
        target['table']={('procurement','old','p30_record_login_02','erp','SELECT',True)}
        with self.assertRaises(RuntimeError): grant_sql(target)

    def test_hba_first_matching_rule_not_later_scram(self):
        rows=[{'ruleNumber':1,'type':'local','database':['all'],'users':['postgres'],'authMethod':'peer','error':None},
              {'ruleNumber':2,'type':'local','database':['all'],'users':['all'],'authMethod':'scram-sha-256','error':None}]
        self.assertEqual(first_local_scram(rows,'proc_p30_login_20261024_02',names('proc_p30_login_20261024_02')),[2,2])
        changed=copy.deepcopy(rows);changed[1]['authMethod']='trust'
        with self.assertRaises(RuntimeError): first_local_scram(changed,'proc_p30_login_20261024_02',names('proc_p30_login_20261024_02'))
        changed=copy.deepcopy(rows);changed[0]['users']=['+erp']
        with self.assertRaises(RuntimeError): first_local_scram(changed,'proc_p30_login_20261024_02',names('proc_p30_login_20261024_02'))
        changed=copy.deepcopy(rows);changed[0]['error']='invalid rule'
        with self.assertRaises(RuntimeError): first_local_scram(changed,'proc_p30_login_20261024_02',names('proc_p30_login_20261024_02'))

    def test_output_fences_wrong_parent_traversal_symlink_overlap_reuse(self):
        with tempfile.TemporaryDirectory() as temp:
            root=pathlib.Path(temp);out=root/'procurement-p30-finite-login-20261024_run02'
            validate_output(out,[],root)
            for invalid in [root/'wrong',root/'outside'/'procurement-p30-finite-login-20261024_run02',root/'..'/'procurement-p30-finite-login-20261024_run02']:
                with self.assertRaises(RuntimeError): validate_output(invalid,[],root)
            with self.assertRaises(RuntimeError): validate_output(out,[out/'input'],root)
            out.mkdir()
            with self.assertRaises(RuntimeError): validate_output(out,[],root)
            out.rmdir();out.symlink_to(root/'missing')
            with self.assertRaises(RuntimeError): validate_output(out,[],root)
            out.unlink();link=root/'link';link.symlink_to(root,target_is_directory=True)
            with self.assertRaises(RuntimeError): validate_output(link/out.name,[],link)

    def test_structural_reader_preserves_default_acl_and_metadata(self):
        value={'tables':[['procurement','x','erp','acl']], 'columns':[['procurement','x',1,'id','uuid',True,'','',None]],'schemas':[['procurement','erp','acl']], 'defaultAcl':[['erp','SELECT']], 'constraints':[['exact']]}
        altered=copy.deepcopy(value);altered['tables'][0][3]='new alias ACL'
        self.assertEqual(structural(value),structural(altered))
        altered['defaultAcl'][0][1]='UPDATE'
        self.assertNotEqual(structural(value),structural(altered))
        self.assertEqual(value['tables'][0][3],'acl')

    def test_incomplete_plan_denied_before_any_external_operation(self):
        with self.assertRaisesRegex(RuntimeError,'Incomplete'):
            admit({'status':'INCOMPLETE'},pathlib.Path(__file__))

class ReviewedActualStageBSourceTests(unittest.TestCase):
    @unittest.skipUnless(os.environ.get('P30_REVIEWED_STAGE_B_PACKAGE'),'Optional source-only review of retained private helpers')
    def test_actual_reviewed218_4097_proofs_and_direct_matrix(self):
        from run import load_module, pinned
        directory=pathlib.Path(os.environ['P30_REVIEWED_STAGE_B_PACKAGE'])
        plan=json.loads((directory/'finite-session-plan.REVIEWED-root01.json').read_text())
        helper=load_module(pinned(plan['files']['sqlHelper']),'reviewed_sql_for_test')
        acl=load_module(pinned(plan['files']['aclDeltaHelper']),'reviewed_acl_for_test')
        catalog=json.loads(pinned(plan['files']['sourceCatalog']).read_text())
        matrix=json.loads(pinned(plan['files']['effectiveMatrix']).read_text())['actual218MutationMatrix']
        from contracts import alias_targets
        expected=alias_targets(acl,matrix,catalog,'proc_p30_login_20261024_02')
        self.assertEqual(len(catalog['tables']),218);self.assertEqual(len(catalog['columns']),4097)
        for which,alias in zip(('record','execution'),names('proc_p30_login_20261024_02')):
            proof,_=login_proof(helper,matrix,catalog,which,alias)
            self.assertFalse(SWITCH.search(proof));self.assertIn('ExactEffectivePrivilegeMatrixMismatch',proof)
        self.assertTrue(expected['column']);self.assertFalse(SWITCH.search(grant_sql(expected)))

if __name__ == '__main__': unittest.main()
