"""Synthetic profile/SQL contracts and no-effect source guards. No C#/SQL executed."""
import copy
import hashlib
import importlib.util
import json
import os
from pathlib import Path
import runpy
import tempfile
from types import SimpleNamespace
import unittest
from unittest.mock import patch
import core_profile

HERE=Path(__file__).resolve().parent
ROOT=HERE.parents[2]
SHARED=SimpleNamespace(**runpy.run_path(str(HERE.parent/'restored_finite_login/contracts.py')))

# Synthetic mapped-profile fixture, not an actual C# model capture or readiness proof.
def example(catalog=None):
    base=['id','tenant_id','created_at','created_by','updated_at','updated_by','is_archived']
    full={'local_users':base+['email','normalized_email','display_name','password_hash','roles_csv','worker_id','is_active','must_change_password','failed_login_count','locked_until','last_login_at','password_changed_at'],
          'local_sessions':base+['local_user_id','token_hash','expires_at','revoked_at','last_seen_at','user_agent'],
          'tenant_role_assignments':base+['role_key','role_name','permissions_csv','active'],
          'audit_entries':base+['entity_type','entity_id','action','before_json','after_json','actor_subject_id','correlation_id'],
          'privileged_action_events':base+['actor_subject_id','actor_roles','method','path','outcome','status_code','request_id','source_address_hash'],
          'legal_entities':['id','tenant_id','is_archived','code','registered_name','currency','is_default'],
          'work_locations':['id','tenant_id','is_archived','legal_entity_id','code','name'],
          'org_units':['id','tenant_id','is_archived','legal_entity_id','code','name','status','effective_from','effective_to'],
          'hr_user_branch_assignments':['tenant_id','user_id','location_id','is_archived'],
          'workers':sorted(core_profile.WORKER_READS), '__hrm_migrations':['MigrationId','ProductVersion']}
    rows=[]
    for table,columns in sorted(full.items()):
        rows.append({'Schema':'hrm','Table':table,'Select':[] if table in ['audit_entries','privileged_action_events'] else columns,
            'Insert':columns if table in core_profile.INSERTS else [],
            'Update':sorted(core_profile.USER_UPDATES if table=='local_users' else core_profile.SESSION_UPDATES if table=='local_sessions' else [])})
    if catalog is None:
        tables=[['hrm',t,'erp',None] for t in full]+[['procurement','synthetic_'+str(i),'erp',None] for i in range(207)]
        cols=[['hrm',t,n+1,c,'text',False,'','',None] for t,columns in full.items() for n,c in enumerate(columns)]
        cols += [['procurement','synthetic_'+str(i),1,'id','uuid',False,'','',None] for i in range(207)]
        catalog={'tables':tables,'columns':cols,'schemas':[['hrm','erp',None],['procurement','erp',None],['core_approval','erp',None]]}
    manifest={'SchemaVersion':1,'Owner':'Core','Scope':'PRIVATE_LOCAL_SESSION_AND_CURRENT_CORE_READS_ONLY','Relations':rows,'PhysicalSchemaVerified':False,'RuntimeReady':False}
    return manifest,catalog

class CoreProfileTests(unittest.TestCase):
    def test_exact_mapped_profile_bounds_auth_writes_and_no_role_or_employee_writes(self):
        manifest,catalog=example();target=core_profile.core_targets(manifest,catalog,'proc_p30_core_session_20261024_01')
        self.assertFalse(target['table']);self.assertEqual({x[0] for x in target['schema']},{'hrm'})
        updates={x[2] for x in target['column'] if x[1]=='local_users' and x[5]=='UPDATE'}
        self.assertEqual(updates,core_profile.USER_UPDATES)
        self.assertFalse(any(x[1] in ['workers','tenant_role_assignments'] and x[5]!='SELECT' for x in target['column']))
        self.assertFalse(any(x[1] in ['audit_entries','privileged_action_events'] and x[5]!='INSERT' for x in target['column']))

    def test_malformed_unsafe_profile_or_invented_readiness_denied(self):
        manifest,catalog=example()
        for mutate in [lambda x:x.update(RuntimeReady=True),lambda x:x.update(PhysicalSchemaVerified=True),
                       lambda x:next(r for r in x['Relations'] if r['Table']=='local_users')['Update'].append('roles_csv'),
                       lambda x:next(r for r in x['Relations'] if r['Table']=='workers')['Insert'].append('id'),
                       lambda x:next(r for r in x['Relations'] if r['Table']=='audit_entries')['Select'].append('id')]:
            bad=copy.deepcopy(manifest);mutate(bad)
            with self.assertRaises(RuntimeError):core_profile.validate(bad,catalog)

    def test_same_suffix_fresh_core_record_execute_aliases_only(self):
        self.assertEqual(core_profile.aliases('proc_p30_core_session_20261024_02'),('p30_core_session_login_02','p30_record_login_02','p30_execute_login_02'))
        with self.assertRaises(RuntimeError):core_profile.aliases('erp')
        with self.assertRaises(RuntimeError):core_profile.aliases('proc_p30_login_20261024_02')

    def test_selected_host_has_no_owner_fallback_or_unused_connection_key(self):
        source=(ROOT/'backend/procurement-api/tools/FullHostCompatibilityProbe/PrincipalApprovalFixture.cs').read_text()
        guard=(ROOT/'backend/procurement-api/tools/FullHostCompatibilityProbe/RestoredCoreSessionConnections.cs').read_text()
        self.assertIn('ExplicitFiniteCoreConnectionMissingNoOwnerFallback',source)
        self.assertIn('OfflineHost.Settings(restoredCore is null?owner:restoredCore.Core',source)
        self.assertIn('restoredCore is null&&Environment.GetEnvironmentVariable',source)
        self.assertIn('restoredCore.RequireConfiguration(config.Build(),record,execution)',source)
        self.assertIn('AsEnumerable(makePathsRelative:true)',guard)
        self.assertIn('owner.Length!=0',guard);self.assertIn('!normalAuth',guard)

    def test_real_normal_auth_source_preserves_wrong_current400_and_requires_both_audits(self):
        source=(ROOT/'backend/procurement-api/tools/FullHostCompatibilityProbe/RestoredCoreLocalSessionVerification.cs').read_text()
        self.assertIn('normalLocalAuthentication:true',source);self.assertIn('new PrincipalApprovalFixture("",false)',source)
        self.assertIn('cookie,400,actor',source);self.assertIn('cookie,401,actor',source)
        self.assertIn('hrm.audit_entries',source);self.assertIn('hrm.privileged_action_events',source)
        self.assertIn('ownerAuditVerified=false',source);self.assertNotIn('BootstrapAsync()',source)
        self.assertNotIn('X-Private-P25-Subject',source);self.assertIn('workerId").ValueKind==JsonValueKind.Null',source)

    def test_reviewed_operational_keys_have_actual_guard_preflight_and_legacy_mode_control(self):
        guard=(ROOT/'backend/procurement-api/tools/FullHostCompatibilityProbe/RestoredCoreSessionConnections.cs').read_text()
        proof=(ROOT/'backend/procurement-api/tools/FullHostCompatibilityProbe/CoreLocalSessionConfigurationVerification.cs').read_text()
        for key in ['Procurement:InvoiceUnitReview:Enabled','Procurement:ManualPayments:Enabled']:
            self.assertIn('"'+key+'"',guard.split('internal const string OptIn=')[0])
            self.assertIn('"'+key+'"',proof)
        self.assertIn('DisabledFeatures.Any(key=>configuration.GetValue<bool>(key))',guard)
        self.assertIn('.AddInMemoryCollection(new Dictionary<string,string?> {[key]="true"})',proof)
        self.assertIn('core.RequireConfiguration(configuration,record,execution)',proof)
        self.assertIn('CoreSessionPrivateNonExecutingHostRequired',proof)
        self.assertIn('UnselectedPrivateModeChanged',proof)
        self.assertNotIn('.CreateClient(',proof);self.assertNotIn('.OpenAsync(',proof)
        main=(ROOT/'backend/procurement-api/tools/FullHostCompatibilityProbe/RestoredCoreLocalSessionVerification.cs').read_text()
        self.assertLess(main.index('CoreLocalSessionConfigurationVerification.Verify()'),main.index('host.CreateClient('))

    def test_core_runtime_container_identity_mount_and_swap_denials(self):
        values=['/pub','/socket','/input','/result'];row={'Id':'a'*64,'Image':'sha256:fixed','Name':'/core-run',
            'Config':{'Labels':{'p30.core-session.source':'source'}},
            'HostConfig':{'NetworkMode':'none','ReadonlyRootfs':True,'Privileged':False,'PortBindings':{},'CapAdd':None,'NanoCpus':1000000000,
                'Memory':1100*1024*1024,'MemorySwap':1100*1024*1024,'PidsLimit':128,'Tmpfs':{'/tmp':'rw,nosuid,nodev,size=64m'}},
            'Mounts':[{'Type':'bind','Source':s,'Destination':d,'RW':w} for s,d,w in zip(values,['/app','/private-pg-socket','/evidence','/result'],[False,False,False,True])]}
        check=lambda r:core_profile.verify_container(r,'a'*64,'core-run','sha256:fixed','source',*values)
        check(row)
        for mutate in [lambda r:r.update(Id='b'*64),lambda r:r['Mounts'][0].update(RW=True),lambda r:r['HostConfig'].update(MemorySwap=-1)]:
            bad=copy.deepcopy(row);mutate(bad)
            with self.assertRaises(RuntimeError):check(bad)

    def test_owner_or_unknown_credentials_are_not_delivered_to_app(self):
        import run as runner
        database='proc_p30_core_session_20261024_01'
        actor=lambda n:{'Id':'11111111-1111-4111-8111-11111111111'+str(n),'Email':'private'+str(n)+'@core-session.example.test','Password':'a'*64,'NewPasswords':['b'*64,'c'*64]}
        value={'Database':database,'Tenant':'p30-core-session-01','CompanyId':'11111111-1111-4111-8111-111111111110',
            'BranchId':'22222222-2222-4222-8222-222222222222','ForeignCompanyId':'33333333-3333-4333-8333-333333333333',
            'ForeignBranchId':'44444444-4444-4444-8444-444444444444',
            **{key:{'Name':name,'Password':'d'*64} for key,name in zip(['Core','Record','Execution'],core_profile.aliases(database))},
            'Granted':actor(1),'Ungranted':actor(2),'OwnerBaselineSha256':'e'*64}
        with tempfile.TemporaryDirectory() as temp:
            p=Path(temp)/'input.json';p.write_text(json.dumps(value));p.chmod(0o600)
            runner.private_input(p,database)
            bad=copy.deepcopy(value);bad['OwnerConnection']='forbidden-owner-value';p.write_text(json.dumps(bad))
            with self.assertRaises(RuntimeError):runner.private_input(p,database)
            p.write_text(json.dumps(value));p.chmod(0o644)
            with self.assertRaises(RuntimeError):runner.private_input(p,database)

    def test_incomplete_runner_plan_fails_before_git_docker_or_database(self):
        import run as runner
        value={'status':'INCOMPLETE'}
        with tempfile.TemporaryDirectory() as temp:
            p=Path(temp)/'plan.json';p.write_text(json.dumps(value))
            with patch.object(runner.subprocess,'check_output') as command:
                with self.assertRaisesRegex(RuntimeError,'incomplete-or-unreviewed-Core-profile'):
                    runner.admit(value,p,runner.sha(p))
                command.assert_not_called()

    @unittest.skipUnless(os.environ.get('P30_REVIEWED_STAGE_B_PACKAGE'),'Optional actual retained StageB source-only generation')
    def test_shared_sql_adapter_keeps218_matrix_without_extra_schema_usage_and_column_inserts(self):
        package=Path(os.environ['P30_REVIEWED_STAGE_B_PACKAGE']);plan=json.loads((package/'finite-session-plan.REVIEWED-root01.json').read_text())
        spec=importlib.util.spec_from_file_location('retained_stageb_sql',plan['files']['sqlHelper']['path']);module=importlib.util.module_from_spec(spec);spec.loader.exec_module(module)
        self.assertEqual(hashlib.sha256(Path(spec.origin).read_bytes()).hexdigest(),plan['files']['sqlHelper']['sha256'])
        manifest,catalog=example()
        proof,transport=core_profile.core_proof(SHARED,module,manifest,catalog,'proc_p30_core_session_20261024_01')
        self.assertEqual(transport['columnInsertReplacements'],3);self.assertEqual(transport['boundedPositiveColumnUpdates'],8)
        self.assertIn('END IS NOT DISTINCT FROM allowed',proof);self.assertIn('SELECT c.oid FROM pg_class',proof)
        self.assertFalse(SHARED.SWITCH.search(proof));self.assertTrue(proof.endswith('ROLLBACK;\n'))
        self.assertIn("session_user<>'p30_core_session_login_01'",proof)
        bad=copy.deepcopy(catalog);next(c for c in bad['columns'] if c[1]=='audit_entries')[4]='text); DROP TABLE forbidden;'
        with self.assertRaises(RuntimeError):core_profile.core_proof(SHARED,module,manifest,bad,'proc_p30_core_session_20261024_01')

if __name__=='__main__':unittest.main()
