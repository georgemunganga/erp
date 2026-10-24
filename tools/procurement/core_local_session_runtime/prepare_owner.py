#!/usr/bin/env python3
"""Reviewed private Core01 clone, finite role grants and synthetic owner fixture.

No application host, production connection, migration, source-data edit, or auto-retry.
"""
import argparse
import hashlib
import importlib.util
import json
import os
from pathlib import Path
import re
import secrets
import shutil
import subprocess
import sys
import uuid
import core_profile

HERE=Path(__file__).resolve().parent
BACKUPS=Path('/home/amizo/erp-backups')
SOURCE='proc_p30_stageb_20261024_01'
TARGET='proc_p30_core_session_20261024_01'
PRIVATE='procurement-approval-rehearsal-postgres'
PRIVATE_ID='10b7cb11af108c3a47ba557c0f903ac73439f88b060e7f7150c08c8fa99778fd'
SYSTEM_ID='7698678713374847009'
FOUNDATION=HERE.parent/'restored_finite_login/contracts.py'
HEX=re.compile(r'[0-9a-f]{64}')


def need(ok,code):
    if not ok:raise RuntimeError(code)
def sha(path):return hashlib.sha256(path.read_bytes()).hexdigest()
def pin(item):
    need(isinstance(item,dict) and set(item)=={'path','sha256'} and HEX.fullmatch(item['sha256']),
         'exact-private-input-pin-required')
    path=Path(item['path']);need(path.is_absolute() and path.resolve()==path and path.is_file()
        and not path.is_symlink() and sha(path)==item['sha256'],'private-input-changed')
    return path

def module(path,name):
    spec=importlib.util.spec_from_file_location(name,path);need(spec and spec.loader,'reviewed-helper-required')
    mod=importlib.util.module_from_spec(spec);sys.dont_write_bytecode=True
    try:spec.loader.exec_module(mod)
    finally:sys.dont_write_bytecode=False
    return mod

def save(path,data):
    raw=data if isinstance(data,bytes) else (json.dumps(data,sort_keys=True,indent=2)+'\n').encode()
    fd=os.open(path,os.O_WRONLY|os.O_CREAT|os.O_EXCL|os.O_NOFOLLOW,0o600)
    with os.fdopen(fd,'wb') as out:out.write(raw);out.flush();os.fsync(out.fileno())

def sql(database,statement,secret=False):
    need(database in {'postgres',SOURCE,TARGET},'fixed-private-database-allowlist')
    options='-c lock_timeout=2s -c statement_timeout=90s '
    options+='-c idle_in_transaction_session_timeout=10s -c max_parallel_workers_per_gather=0'
    if secret:
        # Session startup settings apply BEFORE the first password-bearing SQL
        # statement. The server's global logging configuration is untouched.
        options+=' -c log_statement=none -c log_min_error_statement=panic'
    cmd=['docker','exec','-i','-e','PGOPTIONS='+options,
         PRIVATE,'psql','-XqAt','-U','postgres','-d',database,'-v','ON_ERROR_STOP=1',
         '-v','VERBOSITY=sqlstate','-f','-']
    run=subprocess.run(cmd,input=statement if isinstance(statement,bytes) else statement.encode(),
                       capture_output=True,timeout=150)
    need(run.returncode==0,'private-owner-SQL-failed-retain-cohort')
    # Secret-bearing CREATE ROLE text is stdin only; never stored or echoed.
    return run.stdout

def role_sql(roles,passwords):
    need(len(roles)==len(passwords)==3 and all(re.fullmatch('p30_[a-z_]+_login_01',r) for r in roles)
        and all(HEX.fullmatch(p) for p in passwords),'exact-three-private-SCRAM-aliases-required')
    lines=["SET log_statement='none';","SET log_min_error_statement='panic';",
           'SET log_min_duration_statement=-1;','SET log_min_duration_sample=-1;',
           'SET log_transaction_sample_rate=0;','SET log_duration=off;','BEGIN;',
           "SET LOCAL password_encryption='scram-sha-256';",
           "SET LOCAL statement_timeout='30s';","SET LOCAL lock_timeout='2s';"]
    for name,password in zip(roles,passwords):
        lines.append(f'CREATE ROLE "{name}" LOGIN NOINHERIT NOSUPERUSER NOCREATEDB NOCREATEROLE '
                     f'NOREPLICATION NOBYPASSRLS CONNECTION LIMIT -1 PASSWORD \'{password}\';')
    return '\n'.join([*lines,'COMMIT;',''])

def verify_container(plan):
    row=json.loads(subprocess.check_output(['docker','inspect',PRIVATE],timeout=20))[0]
    need(row['Id']==PRIVATE_ID==plan['privateContainerId'] and row['Image']==plan['privateImageId']
         and row['State']['Running'] and row['HostConfig']['NetworkMode']=='none'
         and not row['HostConfig'].get('PortBindings'),'exact-private-PG-required')
    mounts={(m['Type'],m['Source'],m['Destination'],m['RW']) for m in row['Mounts']}
    need(('bind',plan['socketHostDirectory'],'/var/run/postgresql',True) in mounts
         and ('volume',plan['privateDataHostDirectory'],'/var/lib/postgresql/data',True) in mounts,
         'exact-private-PG-mounts-required')

def verify_source(source,commit,tree_manifest_sha256,tracked_file_count):
    """Admit every physical tracked blob from the reviewed clean commit, not a stale literal."""
    need(isinstance(commit,str) and re.fullmatch(r'[0-9a-f]{40}',commit)
         and isinstance(tree_manifest_sha256,str) and HEX.fullmatch(tree_manifest_sha256)
         and type(tracked_file_count) is int and tracked_file_count>0,
         'exact-reviewed-source-pins-required')
    need(source.is_absolute() and source.resolve()==source and source.is_dir() and not source.is_symlink(),
         'exact-source-directory-required')
    def git(*args):return subprocess.check_output(['git',*args],cwd=source,text=True,timeout=30).strip()
    need(git('rev-parse','--show-toplevel')==str(source) and git('rev-parse','HEAD')==commit
         and git('rev-parse','--show-object-format')=='sha1'
         and not git('status','--porcelain=v1','--untracked-files=all'),
         'exact-clean-Core-source-required')
    raw=subprocess.check_output(['git','ls-tree','-rz','HEAD'],cwd=source,timeout=30)
    rows=[row for row in raw.split(b'\0') if row]
    need(hashlib.sha256(raw).hexdigest()==tree_manifest_sha256 and len(rows)==tracked_file_count,
         'exact-reviewed-source-tree-required')
    for row in rows:
        header,name=row.split(b'\t',1);mode,kind,object_id=header.decode().split()
        path=source/name.decode()
        need(kind=='blob' and mode in {'100644','100755','120000'},'source-tree-entry-unsupported')
        if mode=='120000':
            need(path.is_symlink(),'tracked-source-link-missing')
            content=os.readlink(path).encode()
        else:
            need(path.is_file() and not path.is_symlink(),'tracked-source-file-missing')
            content=path.read_bytes()
        need(hashlib.sha1(b'blob '+str(len(content)).encode()+b'\0'+content).hexdigest()==object_id,
             'tracked-source-byte-drift')

def main():
    p=argparse.ArgumentParser();p.add_argument('--plan',type=Path,required=True)
    p.add_argument('--expected-plan-sha256',required=True);p.add_argument('--output',type=Path,required=True)
    p.add_argument('--execute-reviewed-private-owner-preparation',action='store_true');args=p.parse_args()
    need(args.execute_reviewed_private_owner_preparation and
         os.getenv('P30_CORE_OWNER_PREPARE_EXECUTE')=='reviewed-private-only','explicit-private-review-required')
    plan_path=pin({'path':str(args.plan),'sha256':args.expected_plan_sha256});plan=json.loads(plan_path.read_text())
    need(plan['status']=='REVIEWED_PRIVATE_CORE_OWNER_PREPARATION_READY'
         and isinstance(plan['sourceCommit'],str) and re.fullmatch(r'[0-9a-f]{40}',plan['sourceCommit'])
         and plan['runnerSha256']==sha(Path(__file__)) and plan['profileSha256']==sha(HERE/'core_profile.py')
         and plan['foundationSha256']==sha(FOUNDATION)
         and plan['sourceDatabase']==SOURCE and plan['targetDatabase']==TARGET
         and plan['privateContainerId']==PRIVATE_ID and plan['privateSystemIdentifier']==SYSTEM_ID
         and plan['productionSystemIdentifier']=='7683513820537307167',
         'reviewed-private-Core-source-boundary-required')
    source=Path(plan['sourcePath'])
    need(source==Path(__file__).resolve().parents[3], 'runner-must-belong-to-reviewed-source')
    verify_source(source,plan['sourceCommit'],plan['trackedTreeManifestSha256'],plan['trackedFileCount'])
    need(args.output==Path('/home/amizo/erp-backups/procurement-p30-core-owner-preparation-20261024_run01')
         and args.output.parent==BACKUPS and not args.output.exists() and not args.output.is_symlink(),
         'fresh-private-owner-evidence-required')
    need(shutil.disk_usage(BACKUPS).free>=plan['minimumFreeBytes']>=1_273_741_824,
         'private-clone-headroom-required')
    required={'stageBResult','sourceCatalog','sourceData','sourceAcl','sourceRoles','catalogSql','dataSql',
              'expandedAclSql','sqlHelper','aclDeltaHelper','effectiveMatrix','hbaRules',
              'mappedCoreManifest','runtimeManifest','compiledOverlayResult'}
    need(set(plan['files'])==required,'closed-private-owner-input-set-required')
    files={name:pin(value) for name,value in plan['files'].items()}
    need(json.loads(files['stageBResult'].read_text())['status']=='PASS_PRIVATE_CLONE_FINITE_SESSION_ACL_ONLY'
         and json.loads(files['compiledOverlayResult'].read_text())['status']=='PASS_PRIVATE_TOOL_ONLY_COMPILE_OVERLAY_NOT_EXECUTED'
         and json.loads(files['compiledOverlayResult'].read_text())['sourceCommit']==plan['sourceCommit'],
         'accepted-private-stage-and-compiled-overlay-required')
    catalog=json.loads(files['sourceCatalog'].read_text());need(len(catalog['tables'])==218
        and len(catalog['columns'])==4097,'actual218-4097-Core-store-required')
    mapped=json.loads(files['mappedCoreManifest'].read_text());core_profile.validate(mapped,catalog)
    publication=Path(plan['publicationDirectory']);manifest=json.loads(files['runtimeManifest'].read_text())
    actual={str(x.relative_to(publication)):sha(x) for x in publication.rglob('*') if x.is_file()}
    need(publication.is_dir() and actual==manifest and not any(x.is_symlink() for x in publication.rglob('*')),
         'exact-compiled-private-overlay-required')
    foundation=module(FOUNDATION,'reviewed_core_foundation')
    acl=module(files['aclDeltaHelper'],'reviewed_core_acl')
    record_execution=foundation.alias_targets(acl,json.loads(files['effectiveMatrix'].read_text())['actual218MutationMatrix'],
                                               catalog,'proc_p30_login_20261024_01')
    core=core_profile.core_targets(mapped,catalog,TARGET)
    for kind in record_execution:record_execution[kind]|=core[kind]
    roles=core_profile.aliases(TARGET);need(roles==('p30_core_session_login_01','p30_record_login_01','p30_execute_login_01'),
                                    'exact-private-Core-aliases-required')
    hba=json.loads(files['hbaRules'].read_text());foundation.first_local_scram(hba,TARGET,roles)
    verify_container(plan)
    image=json.loads(subprocess.check_output(['docker','image','inspect',plan['runtimeImageId']],timeout=20))[0]
    need(image['Id']==plan['runtimeImageId'],'exact-local-runtime-image-required')
    identity=sql('postgres',"SELECT system_identifier::text||'|'||current_setting('listen_addresses') FROM pg_control_system()").decode().strip()
    need(identity==SYSTEM_ID+'|','separate-private-PG17-required')
    absent=sql('postgres',"SELECT NOT EXISTS(SELECT 1 FROM pg_database WHERE datname='"+TARGET+"') AND "
        "NOT EXISTS(SELECT 1 FROM pg_roles WHERE rolname IN ('"+"','".join(roles)+"'))").decode().strip()
    need(absent=='t','fresh-Core-DB-and-roles-required-no-resume')
    hba_sql="SELECT coalesce(jsonb_agg(jsonb_build_object('ruleNumber',rule_number,'type',type,'database',database,'users',user_name,'authMethod',auth_method,'error',error) ORDER BY rule_number),'[]'::jsonb)::text FROM pg_hba_file_rules"
    need(json.loads(sql('postgres',hba_sql))==hba,'current-private-HBA-drifted')
    role_sql_read="SELECT jsonb_build_object('roles',(SELECT coalesce(jsonb_agg(jsonb_build_array(rolname,rolcanlogin,rolsuper,rolcreatedb,rolcreaterole,rolreplication,rolbypassrls,rolinherit,rolconnlimit,rolvaliduntil::text,rolconfig) ORDER BY rolname),'[]'::jsonb) FROM pg_roles),'memberships',(SELECT coalesce(jsonb_agg(jsonb_build_array(roleid,member,grantor,admin_option,inherit_option,set_option) ORDER BY roleid,member,grantor),'[]'::jsonb) FROM pg_auth_members))::text"
    initial_roles=json.loads(sql('postgres',role_sql_read))
    need(initial_roles==json.loads(files['sourceRoles'].read_text()),'all-existing-private-roles-or-memberships-changed')
    need(sql('postgres',"SELECT NOT EXISTS(SELECT 1 FROM pg_stat_activity WHERE datname='"+SOURCE+"')").decode().strip()=='t',
         'accepted-StageB-source-must-be-idle-before-clone')
    # The reviewed source baseline is independently captured before the first mutation.
    need(json.loads(sql(SOURCE,files['catalogSql'].read_bytes()))==catalog
         and json.loads(sql(SOURCE,files['dataSql'].read_bytes()))==json.loads(files['sourceData'].read_text())
         and json.loads(sql(SOURCE,files['expandedAclSql'].read_bytes()))==json.loads(files['sourceAcl'].read_text()),
         'accepted-source-clone-baseline-changed')
    args.output.mkdir(mode=0o700)
    secrets_dir=args.output/'secrets';secrets_dir.mkdir(mode=0o700)
    save(args.output/'before.private.json',{'sourceDatabase':SOURCE,'targetDatabase':TARGET,
         'sourceDataSha256':sha(files['sourceData']),'sourceCatalogSha256':sha(files['sourceCatalog']),
         'sourceAclSha256':sha(files['sourceAcl']),'sourceRolesSha256':sha(files['sourceRoles']),
         'reviewedPlanSha256':args.expected_plan_sha256})
    created=subprocess.run(['docker','exec',PRIVATE,'createdb','-U','postgres','--template='+SOURCE,'--owner=erp',TARGET],
                           capture_output=True,timeout=90)
    need(created.returncode==0,'fresh-private-Core-clone-failed-retain')
    need(json.loads(sql(TARGET,files['catalogSql'].read_bytes()))==catalog
         and json.loads(sql(TARGET,files['dataSql'].read_bytes()))==json.loads(files['sourceData'].read_text()),
         'actual-private-Core-clone-not-identical')
    passwords=[secrets.token_hex(32) for _ in roles]
    sql(TARGET,role_sql(roles,passwords),secret=True)
    sql(TARGET,foundation.grant_sql(record_execution))
    actual_acl=json.loads(sql(TARGET,files['expandedAclSql'].read_bytes()))
    foundation.verify_acl(acl,json.loads(files['sourceAcl'].read_text()),actual_acl,record_execution)
    current=sql('postgres',"SELECT count(*)=3 AND bool_and(rolpassword LIKE 'SCRAM-SHA-256$%') "
        "FROM pg_authid WHERE rolname IN ('"+"','".join(roles)+"')").decode().strip()
    need(current=='t','actual-three-SCRAM-verifiers-required-no-bytes-output')
    after_roles=json.loads(sql('postgres',role_sql_read))
    need(after_roles['memberships']==initial_roles['memberships']
         and [r for r in after_roles['roles'] if r[0] not in roles]==initial_roles['roles']
         and [r for r in after_roles['roles'] if r[0] in roles]
         ==sorted([[r,True,False,False,False,False,False,False,-1,None,None] for r in roles]),
         'existing-private-role-or-finite-attributes-changed')
    baseline=sql(TARGET,files['dataSql'].read_bytes())
    save(args.output/'pre-actor-owner-data.private.json',baseline)
    baseline_sha=sha(args.output/'pre-actor-owner-data.private.json')
    IDs=[str(uuid.uuid4()) for _ in range(6)];actor_passwords=[secrets.token_hex(32) for _ in range(2)]
    actors=[{'Id':IDs[4+i],'Email':('granted' if i==0 else 'ungranted')+'-p30-core01@core-session.example.test',
             'Password':actor_passwords[i]} for i in range(2)]
    owner_input={'Database':TARGET,'Tenant':'p30-core-session-01','CompanyId':IDs[0],
        'BranchId':IDs[1],'ForeignCompanyId':IDs[2],'ForeignBranchId':IDs[3],
        'Granted':actors[0],'Ungranted':actors[1],'OwnerBaselineSha256':baseline_sha}
    save(secrets_dir/'core-owner-preparation-input.private.json',owner_input)
    new_passwords=[[secrets.token_hex(32),secrets.token_hex(32)] for _ in range(2)]
    runtime_input={'Database':TARGET,'Tenant':'p30-core-session-01','CompanyId':IDs[0],
        'BranchId':IDs[1],'ForeignCompanyId':IDs[2],'ForeignBranchId':IDs[3],
        'Core':{'Name':roles[0],'Password':passwords[0]},
        'Record':{'Name':roles[1],'Password':passwords[1]},
        'Execution':{'Name':roles[2],'Password':passwords[2]},
        'Granted':{**actors[0],'NewPasswords':new_passwords[0]},
        'Ungranted':{**actors[1],'NewPasswords':new_passwords[1]},
        'OwnerBaselineSha256':baseline_sha}
    save(secrets_dir/'core-session-input.private.json',runtime_input)
    results=args.output/'owner-result';results.mkdir(mode=0o700)
    name='p30-core-owner-prepare-01'
    command=['docker','create','--name',name,'--network','none','--read-only','--cpus','1',
        '--memory','1100m','--memory-swap','1100m','--pids-limit','128',
        '--tmpfs','/tmp:rw,nosuid,nodev,size=64m','--log-driver','none',
        '--label','p30.core-owner.source='+plan['sourceCommit'],
        '--mount','type=bind,src='+str(publication)+',dst=/app,readonly',
        '--mount','type=bind,src='+plan['socketHostDirectory']+',dst=/private-pg-socket,readonly',
        '--mount','type=bind,src='+str(secrets_dir)+',dst=/evidence,readonly',
        '--mount','type=bind,src='+str(results)+',dst=/result',
        '-e','P30_CORE_SESSION_OWNER_PREPARE=reviewed-private-only','--entrypoint','dotnet',
        plan['runtimeImageId'],'/app/FullHostCompatibilityProbe.dll','--prepare-core-session-owner-fixture']
    created=subprocess.run(command,capture_output=True,timeout=30)
    need(created.returncode==0,'fresh-private-Core-owner-container-required')
    identifier=created.stdout.decode().strip();need(HEX.fullmatch(identifier),'exact-owned-container-ID-required')
    def owner_state():
        row=json.loads(subprocess.check_output(['docker','inspect',identifier],timeout=20))[0]
        need(row['Id']==identifier and row['Image']==plan['runtimeImageId']
         and row['Name']=='/'+name
         and row['Config'].get('Labels',{}).get('p30.core-owner.source')==plan['sourceCommit']
         and row['HostConfig']['NetworkMode']=='none'
         and row['HostConfig']['ReadonlyRootfs'] and not row['HostConfig']['Privileged']
         and row['HostConfig']['Memory']==row['HostConfig']['MemorySwap']==1100*1024*1024
         and row['HostConfig']['NanoCpus']==1000000000
         and row['HostConfig']['PidsLimit']==128
         and row['HostConfig']['Tmpfs']=={'/tmp':'rw,nosuid,nodev,size=64m'}
         and {(m['Source'],m['Destination'],m['RW']) for m in row['Mounts'] if m['Type']=='bind'}=={
             (str(publication),'/app',False),(plan['socketHostDirectory'],'/private-pg-socket',False),
             (str(secrets_dir),'/evidence',False),(str(results),'/result',True)},
         'exact-Core-owner-container-required')
        return row
    need(not owner_state()['State']['Running'],'fresh-stopped-Core-owner-container-required')
    need({str(x.relative_to(publication)):sha(x) for x in publication.rglob('*') if x.is_file()}==manifest,
         'compiled-Core-owner-overlay-changed-before-start')
    try:running=subprocess.run(['docker','start','-a',identifier],capture_output=True,timeout=330)
    except subprocess.TimeoutExpired:
        owner_state()
        subprocess.run(['docker','stop','--time','5',identifier],capture_output=True,timeout=20)
        raise RuntimeError('Core-owner-prepare-timeout-retained')
    state=owner_state()
    need(running.returncode==0 and not state['State']['Running'] and state['State']['ExitCode']==0
         and not state['State']['OOMKilled'] and (results/'core-owned-preparation.private.json').is_file(),
         'Core-owner-row-preparation-failed-retained')
    receipt=json.loads((results/'core-owned-preparation.private.json').read_text())
    need(receipt['status']=='CORE_OWNER_PREPARATION_WRITTEN_INDEPENDENT_READBACK_REQUIRED'
         and receipt['database']==TARGET and receipt['actorIds']==[actors[0]['Id'],actors[1]['Id']]
         and receipt['OwnerBaselineSha256']==baseline_sha,
         'owner-write-receipt-not-readback-proof')
    # Owner SQL reads are independent of the app and assert the exact synthetic graph.
    q="""SELECT jsonb_build_object(
      'all_user_rows',(SELECT count(*) FROM hrm.local_users WHERE tenant_id='p30-core-session-01'),
      'all_role_rows',(SELECT count(*) FROM hrm.tenant_role_assignments WHERE tenant_id='p30-core-session-01'),
      'users',(SELECT count(*) FROM hrm.local_users WHERE tenant_id='p30-core-session-01'
        AND worker_id IS NULL AND is_active AND NOT is_archived AND NOT must_change_password
        AND ((id='%s' AND email='%s' AND roles_csv='p30_core_granted_01')
          OR (id='%s' AND email='%s' AND roles_csv='p30_core_ungranted_01'))
        AND password_hash LIKE 'pbkdf2-sha256$210000$%%'),
      'assignments',(SELECT count(*) FROM hrm.hr_user_branch_assignments WHERE tenant_id='p30-core-session-01'
        AND user_id IN ('%s','%s') AND location_id='%s' AND NOT is_archived),
      'distinct_assigned_users',(SELECT count(DISTINCT user_id) FROM hrm.hr_user_branch_assignments
        WHERE tenant_id='p30-core-session-01' AND user_id IN ('%s','%s') AND location_id='%s' AND NOT is_archived),
      'companies',(SELECT count(*) FROM hrm.legal_entities WHERE tenant_id='p30-core-session-01'
        AND id IN ('%s','%s') AND NOT is_archived),
      'branches',(SELECT count(*) FROM hrm.work_locations WHERE tenant_id='p30-core-session-01'
        AND NOT is_archived AND ((id='%s' AND legal_entity_id='%s')
          OR (id='%s' AND legal_entity_id='%s'))),
      'granted',(SELECT permissions_csv FROM hrm.tenant_role_assignments WHERE tenant_id='p30-core-session-01'
        AND role_key='p30_core_granted_01' AND active AND NOT is_archived),
      'ungranted',(SELECT permissions_csv FROM hrm.tenant_role_assignments WHERE tenant_id='p30-core-session-01'
        AND role_key='p30_core_ungranted_01' AND active AND NOT is_archived))::text""" % (
        actors[0]['Id'],actors[0]['Email'],actors[1]['Id'],actors[1]['Email'],
        actors[0]['Id'],actors[1]['Id'],IDs[1],actors[0]['Id'],actors[1]['Id'],IDs[1],
        IDs[0],IDs[2],IDs[1],IDs[0],IDs[3],IDs[2])
    readback=json.loads(sql(TARGET,q))
    need(readback=={'all_user_rows':2,'all_role_rows':2,
                    'users':2,'assignments':2,'distinct_assigned_users':2,
                    'companies':2,'branches':2,
                    'granted':'procurement-access','ungranted':''},
         'independent-synthetic-Core-graph-readback-failed')
    need(json.loads(sql(SOURCE,files['dataSql'].read_bytes()))==json.loads(files['sourceData'].read_text()),
         'original-StageB-business-rows-changed')
    # This is preparation, not HTTP or independent post-HTTP audit acceptance.
    save(args.output/'prepared-store-receipt.private.json',{'status':'REVIEWED_CORE_OWNED_PRIVATE_ACTORS_AND_FINITE_PROFILE_PREPARED',
         'database':TARGET,'actorIds':[actors[0]['Id'],actors[1]['Id']],
         'ownerBaselineSha256':baseline_sha,'sourceDataUnchanged':True,
         'currentCoreActionsPendingHttpRecheck':True,'postHttpAuditVerified':False,
         'producerReceiptSha256':sha(results/'core-owned-preparation.private.json'),
         'credentialsStoredOnlyIn0600Input':True,'productionTouched':False})
    print('PRIVATE_CORE_OWNER_PREPARATION_REQUIRES_SEPARATE_HTTP_AND_AUDIT')

if __name__=='__main__':
    try:main()
    except Exception:raise SystemExit('Private Core owner preparation failed; retained protected evidence requires review.')
