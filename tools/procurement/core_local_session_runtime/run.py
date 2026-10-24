#!/usr/bin/env python3
"""Guarded private Core HTTP phase, using pre-provisioned finite roles/store only.
Role creation, actor preparation and independent post-audit are owning Core gates.
"""
import argparse
import ast
import hashlib
import importlib.util
import json
import os
from pathlib import Path
import re
import runpy
import shutil
import subprocess
from types import SimpleNamespace
import core_profile

HERE=Path(__file__).resolve().parent
BACKUPS=Path('/home/amizo/erp-backups')
PRIVATE='procurement-approval-rehearsal-postgres'
FOUNDATION=HERE.parent/'restored_finite_login/contracts.py'
FOUNDATION_RUNNER=FOUNDATION.with_name('run.py')

def need(ok,code):
    if not ok:raise RuntimeError(code)
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def pinned(item):
    need(isinstance(item,dict) and set(item)=={'path','sha256'},'reviewed-file-pin-required')
    p=Path(item['path']);need(p.is_absolute() and '..' not in p.parts and p.resolve()==p and p.is_file() and not p.is_symlink()
        and sha(p)==item['sha256'],'reviewed-input-changed');return p

def private_input(path,database):
    need(path.stat().st_mode&0o777==0o600,'private0600-credential-input-required')
    value=json.loads(path.read_text())
    need(set(value)=={'Database','Tenant','CompanyId','BranchId','ForeignCompanyId','ForeignBranchId','Core','Record','Execution','Granted','Ungranted','OwnerBaselineSha256'}
        and value['Database']==database and value['Tenant']=='p30-core-session-'+database[-2:],'closed-private-input-no-owner-fields-required')
    for key,name in zip(['Core','Record','Execution'],core_profile.aliases(database)):
        row=value[key];need(set(row)=={'Name','Password'} and row['Name']==name
            and isinstance(row['Password'],str) and re.fullmatch(r'[0-9a-f]{64}',row['Password']),'structured-finite-credential-only')
    for key in ['Granted','Ungranted']:
        row=value[key];need(set(row)=={'Id','Email','Password','NewPasswords'} and isinstance(row['Password'],str)
            and re.fullmatch(r'[0-9a-f]{64}',row['Password']) and isinstance(row['NewPasswords'],list)
            and len(row['NewPasswords'])==2 and all(re.fullmatch(r'[0-9a-f]{64}',p) for p in row['NewPasswords']),'private-actor-input-invalid')
    return value

def validate_output(path,inputs,render=False):
    need(path.parent==BACKUPS and path.is_absolute() and path.resolve()==path and '..' not in path.parts
        and re.fullmatch((r'procurement-p30-core-session-render-20261024_run[0-9]{2}' if render else r'procurement-p30-core-session-20261024_run[0-9]{2}'),path.name)
        and not path.exists() and not path.is_symlink(),'fresh-private-Core-evidence-direct-child-required')
    current=Path('/')
    for part in path.parent.parts[1:]:current/=part;need(current.is_dir() and not current.is_symlink(),'real-private-parents-required')
    for p in inputs:
        p=p.resolve();need(p!=path and p not in path.parents and path not in p.parents,'evidence-input-overlap')

def admit(plan,plan_path,expected):
    need(sha(plan_path)==expected and plan['status']=='REVIEWED_PRIVATE_CORE_SESSION_HTTP_PHASE_READY','incomplete-or-unreviewed-Core-profile')
    need(plan['sourceCommit']==subprocess.check_output(['git','rev-parse','HEAD'],cwd=HERE,text=True).strip()
        and not subprocess.check_output(['git','status','--porcelain=v1','--untracked-files=all'],cwd=HERE,text=True).strip(),'exact-clean-Core-source-required')
    need(plan['runnerSha256']==sha(Path(__file__)) and plan['profileSha256']==sha(HERE/'core_profile.py')
        and plan['foundationSha256']==sha(FOUNDATION) and plan['foundationRunnerSha256']==sha(FOUNDATION_RUNNER)
        and plan['rendererSha256']==sha(HERE/'render.py') and plan['pureTestsSha256']==sha(HERE/'test_profile.py'),'reviewed-source-boundary-changed')
    need(plan['privateContainer']==PRIVATE and plan['privateSystemIdentifier']=='7698678713374847009'
        and plan['productionSystemIdentifier']=='7683513820537307167','exact-private-cluster-required')
    need(isinstance(plan['runtimeImageId'],str) and re.fullmatch(r'sha256:[0-9a-f]{64}',plan['runtimeImageId']),'immutable-local-runtime-image-required')
    roles=core_profile.aliases(plan['database'])
    files={key:pinned(item) for key,item in plan['files'].items()}
    need(set(files)=={'privateInput','mappedCoreManifest','catalog','baseAcl','effectiveMatrix','stagebSqlHelper','stagebAclHelper',
        'catalogSql','expandedAclSql','runtimeManifest','preparedStoreReceipt','ownerBaseline','hbaRules'},'exact-Core-role-input-set-required')
    value=private_input(files['privateInput'],plan['database'])
    need(value['OwnerBaselineSha256']==sha(files['ownerBaseline']),'independent-owner-baseline-pin-required')
    prepared=json.loads(files['preparedStoreReceipt'].read_text())
    need(prepared['status']=='REVIEWED_CORE_OWNED_PRIVATE_ACTORS_AND_FINITE_PROFILE_PREPARED'
        and prepared['database']==plan['database'] and prepared['actorIds']==[value['Granted']['Id'],value['Ungranted']['Id']]
        and prepared['ownerBaselineSha256']==sha(files['ownerBaseline']),'independent-Core-preparation-witness-required')
    # This witness never grants authority: C# re-reads actual Core actors/actions,
    # real roles/SCRAM and effective app/service connections before normal HTTP.
    manifest=json.loads(files['mappedCoreManifest'].read_text());catalog=json.loads(files['catalog'].read_text())
    core_profile.validate(manifest,catalog)
    need(plan['authMethod']=='scram-sha-256','explicit-reviewed-SCRAM-required')
    need(len(catalog['tables'])==218 and len(catalog['columns'])==4097,'accepted-physical218-4097-shape-required')
    pub=Path(plan['publicationDirectory']);need(pub.resolve()==pub and pub.is_dir() and not pub.is_symlink(),'exact-compiled-publication-required')
    expected_manifest=json.loads(files['runtimeManifest'].read_text())
    actual={str(p.relative_to(pub)):sha(p) for p in pub.rglob('*') if p.is_file()}
    need(all(not p.is_symlink() for p in pub.rglob('*')) and actual==expected_manifest
        and 'FullHostCompatibilityProbe.dll' in actual,'complete-current-Core-tool-publication-required')
    need(plan['minimumFreeBytes']>=1273741824 and shutil.disk_usage(BACKUPS).free>=plan['minimumFreeBytes'],'private-evidence-headroom-required')
    return files,value,pub,manifest,catalog

def main():
    p=argparse.ArgumentParser(description=__doc__);p.add_argument('--plan',type=Path,required=True);p.add_argument('--expected-plan-sha256',required=True)
    p.add_argument('--output',type=Path,required=True);p.add_argument('--execute-reviewed-core-http',action='store_true');args=p.parse_args()
    need(args.execute_reviewed_core_http and os.getenv('P30_CORE_SESSION_HTTP_EXECUTE')=='reviewed-private-only','explicit-Core-profile-execution-required')
    plan_path=pinned({'path':str(args.plan),'sha256':args.expected_plan_sha256});plan=json.loads(plan_path.read_text())
    files,value,pub,manifest,catalog=admit(plan,plan_path,args.expected_plan_sha256)
    need(args.output.name=='procurement-p30-core-session-20261024_run'+plan['database'][-2:],'one-output-per-private-target-no-resume')
    validate_output(args.output,[plan_path,HERE,pub,*files.values()])
    foundation=SimpleNamespace(**runpy.run_path(str(FOUNDATION)))
    # Existing reviewed finite ACL helper, hash checked before loading.
    spec=importlib.util.spec_from_file_location('reviewed_stageb_acl',files['stagebAclHelper']);acl=importlib.util.module_from_spec(spec);spec.loader.exec_module(acl)
    expected=foundation.alias_targets(acl,json.loads(files['effectiveMatrix'].read_text())['actual218MutationMatrix'],catalog,
        'proc_p30_login_20261024_'+plan['database'][-2:])
    foundation.first_local_scram(json.loads(files['hbaRules'].read_text()),plan['database'],core_profile.aliases(plan['database']))
    core=core_profile.core_targets(manifest,catalog,plan['database'])
    for kind in expected:expected[kind]|=core[kind]
    pg=json.loads(subprocess.check_output(['docker','inspect',PRIVATE],timeout=20))[0]
    need(pg['Id']==plan['privateContainerId'] and pg['Image']==plan['privateImageId'] and pg['State']['Running']
        and pg['HostConfig']['NetworkMode']=='none' and not pg['HostConfig'].get('PortBindings'),'exact-private-PostgreSQL-container-required')
    runtime=json.loads(subprocess.check_output(['docker','image','inspect',plan['runtimeImageId']],timeout=20))[0]
    need(runtime['Id']==plan['runtimeImageId'],'exact-existing-runtime-image-required')
    socket=Path(plan['socketHostDirectory'])
    need(socket.resolve()==socket and socket.is_dir() and any(m.get('Source')==str(socket) and m.get('Destination')=='/var/run/postgresql' for m in pg['Mounts']),'exact-private-socket-mount-required')
    def read(text):
        result=subprocess.run(['docker','exec','-i','-e','PGOPTIONS=-c default_transaction_read_only=on -c lock_timeout=2s -c statement_timeout=90s -c max_parallel_workers_per_gather=0',PRIVATE,
            'psql','-XqAt','-U','postgres','-d',plan['database'],'-v','ON_ERROR_STOP=1','-f','-'],input=text,capture_output=True,timeout=120)
        need(result.returncode==0,'private-readonly-owner-admission-failed');return result.stdout
    need(read(b"BEGIN READ ONLY; SELECT system_identifier::text='7698678713374847009' AND current_setting('listen_addresses')='' AND current_setting('server_version_num')::integer BETWEEN 170000 AND 179999 FROM pg_control_system(); ROLLBACK;").decode().strip()=='t','separate-private-cluster-required')
    tree=ast.parse(FOUNDATION_RUNNER.read_text())
    declarations=[node.value for node in tree.body if isinstance(node,ast.Assign) and any(isinstance(t,ast.Name) and t.id=='HBA_SQL' for t in node.targets)]
    need(len(declarations)==1,'reviewed-HBA-reader-boundary-changed')
    hba=ast.literal_eval(declarations[0])
    need(json.loads(read(hba.encode()))==json.loads(files['hbaRules'].read_text()),'reviewed-HBA-file-projection-changed')
    names=core_profile.aliases(plan['database'])
    check="BEGIN READ ONLY; SELECT count(*)=3 AND bool_and(rolpassword LIKE 'SCRAM-SHA-256$%') FROM pg_authid WHERE rolname IN ('"+"','".join(names)+"'); ROLLBACK;"
    need(read(check.encode()).decode().strip()=='t','actual-SCRAM-verifier-kind-required-no-verifier-output')
    observed=json.loads(read(files['catalogSql'].read_bytes()))
    need(foundation.structural(observed)==foundation.structural(catalog),'actual-prepared-Core-schema-changed')
    actual=json.loads(read(files['expandedAclSql'].read_bytes()))
    foundation.verify_acl(acl,json.loads(files['baseAcl'].read_text()),actual,expected)
    # Provisioning is intentionally not here. No role/HBA/grant/DDL or copied
    # password hashing implementation enters this runtime/app process.
    validate_output(args.output,[plan_path,HERE,pub,*files.values()]);args.output.mkdir(mode=0o700)
    inputs=args.output/'input';inputs.mkdir(mode=0o700);shutil.copyfile(files['privateInput'],inputs/'core-session-input.private.json');(inputs/'core-session-input.private.json').chmod(0o600)
    result=args.output/'result';result.mkdir(mode=0o700)
    name='p30-core-session-http-'+plan['database'][-2:]
    command=['docker','create','--name',name,'--network','none','--read-only','--cpus','1','--memory','1100m','--memory-swap','1100m','--pids-limit','128',
        '--tmpfs','/tmp:rw,nosuid,nodev,size=64m','--log-driver','none','--label','p30.core-session.source='+plan['sourceCommit'],
        '--mount','type=bind,src='+str(pub)+',dst=/app,readonly','--mount','type=bind,src='+str(socket)+',dst=/private-pg-socket,readonly',
        '--mount','type=bind,src='+str(inputs)+',dst=/evidence,readonly','--mount','type=bind,src='+str(result)+',dst=/result',
        '-e','P30_RESTORED_CORE_SESSION=reviewed-private-only','--entrypoint','dotnet',plan['runtimeImageId'],'/app/FullHostCompatibilityProbe.dll','--restored-core-local-session']
    created=subprocess.run(command,capture_output=True,timeout=30);need(created.returncode==0,'fresh-private-Core-container-required')
    identifier=created.stdout.decode().strip();need(re.fullmatch('[0-9a-f]{64}',identifier),'exact-created-Core-container-id-required')
    def state():
        row=json.loads(subprocess.check_output(['docker','inspect',identifier],timeout=20))[0]
        core_profile.verify_container(row,identifier,name,plan['runtimeImageId'],plan['sourceCommit'],pub,socket,inputs,result)
        return row
    state()
    try:run=subprocess.run(['docker','start','-a',identifier],capture_output=True,timeout=900)
    except subprocess.TimeoutExpired:
        state();subprocess.run(['docker','stop','--time','10',identifier],capture_output=True,timeout=30)
        raise RuntimeError('Core-HTTP-phase-timeout-no-resume')
    secrets=[value[k]['Password'] for k in ['Core','Record','Execution']]+[value[k]['Password'] for k in ['Granted','Ungranted']]+[p for k in ['Granted','Ungranted'] for p in value[k]['NewPasswords']]
    for label,data in [('runtime.stdout.private.log',run.stdout),('runtime.stderr.private.log',run.stderr)]:
        for secret in secrets:data=data.replace(secret.encode(),b'[REDACTED]')
        fd=os.open(args.output/label,os.O_WRONLY|os.O_CREAT|os.O_EXCL|os.O_NOFOLLOW,0o600)
        with os.fdopen(fd,'wb') as stream:stream.write(data)
    row=state();need(run.returncode==0 and row['State']['ExitCode']==0 and not row['State']['Running'] and not row['State']['OOMKilled'],'Core-HTTP-phase-failed')
    need(foundation.structural(json.loads(read(files['catalogSql'].read_bytes())))==foundation.structural(catalog),'Core-HTTP-altered-schema-or-defaults')
    foundation.verify_acl(acl,json.loads(files['baseAcl'].read_text()),json.loads(read(files['expandedAclSql'].read_bytes())),expected)
    for item in plan['files'].values():pinned(item)
    outcome=json.loads((result/'core-session-http.private.json').read_text())
    need(outcome['status']=='PASS_PRIVATE_CORE_SESSION_HTTP_AWAITING_OWNER_AUDIT' and outcome['ownerAuditVerified'] is False,'HTTP-never-substitutes-owner-audit')
    report={'status':'CORE_SESSION_HTTP_PHASE_COMPLETE_OWNER_RECONCILIATION_REQUIRED','httpResultSha256':sha(result/'core-session-http.private.json'),
        'ownerAuditVerified':False,'originalRowsPreservationVerifiedAfterHttp':False,'generalHrmCrudProven':False,'publisherStarted':False,'productionTouched':False}
    fd=os.open(args.output/'phase-result.private.json',os.O_WRONLY|os.O_CREAT|os.O_EXCL|os.O_NOFOLLOW,0o600)
    with os.fdopen(fd,'w') as stream:json.dump(report,stream,indent=2);stream.write('\n')
    print(report['status'])

if __name__=='__main__':
    try:main()
    except Exception:raise SystemExit('Private Core session phase failed; protected owner reconciliation required.')
