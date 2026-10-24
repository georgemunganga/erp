#!/usr/bin/env python3
"""Reviewed private clone + two real finite LOGIN sessions. Incomplete plan cannot launch."""
import argparse
import hashlib
import importlib.util
import json
import os
import pathlib
import re
import secrets
import shutil
import subprocess
from contracts import need, names, validate_output, login_proof, alias_targets, verify_acl, grant_sql, structural, first_local_scram, validate_headroom, provision_sql, verify_container

HERE = pathlib.Path(__file__).resolve().parent
CONTAINER = 'procurement-approval-rehearsal-postgres'
SOURCE = 'proc_p30_stageb_20261024_01'
FILES = {'stageBResult','sourceCatalog','sourceData','sourceAcl','sourceRoles','catalogSql','dataSql','expandedAclSql','sequenceSql','sqlHelper','aclDeltaHelper','effectiveMatrix','hbaRules','runtimeManifest','stageAResult'}
HEX = re.compile(r'[0-9a-f]{64}')

def digest(path): return hashlib.sha256(path.read_bytes()).hexdigest()

def real_path(path):
    need(path.is_absolute() and '..' not in path.parts and path.resolve() == path, 'Absolute non-symlink input required')
    current = pathlib.Path('/')
    for part in path.parts[1:]:
        current /= part
        need(not current.is_symlink(), 'Symlink input ancestor forbidden')
    return path

def pinned(spec):
    need(isinstance(spec, dict) and set(spec) == {'path','sha256'} and isinstance(spec['path'],str)
         and isinstance(spec['sha256'],str) and HEX.fullmatch(spec['sha256']), 'Missing reviewed file pin')
    path = real_path(pathlib.Path(spec['path']))
    need(path.is_file() and digest(path) == spec['sha256'], 'Pinned input differs')
    return path

def load_module(path, name):
    spec = importlib.util.spec_from_file_location(name, path)
    need(spec is not None and spec.loader is not None, 'Pinned helper unavailable')
    module = importlib.util.module_from_spec(spec)
    # No pycache mutations in the accepted private helper directories.
    import sys
    previous = sys.dont_write_bytecode
    sys.dont_write_bytecode = True
    try: spec.loader.exec_module(module)
    finally: sys.dont_write_bytecode = previous
    return module

def admit(plan, path):
    need(plan.get('status') == 'REVIEWED_PRIVATE_FINITE_LOGIN_READY', 'Incomplete/unreviewed plan cannot launch')
    need(plan.get('runnerSha256') == digest(path) and plan.get('contractsSha256') == digest(HERE/'contracts.py'), 'Runner/contract pin differs')
    need(plan.get('sourceDatabase') == SOURCE and plan.get('privateContainer') == CONTAINER, 'Fixed accepted source/container required')
    target = plan.get('targetDatabase')
    roles = names(target)
    need(plan.get('authMethod') == 'scram-sha-256' and plan.get('loginRoles') == list(roles), 'Explicit SCRAM/alias contract required')
    need(plan.get('privateSystemIdentifier') == '7698678713374847009'
         and plan.get('productionSystemIdentifier') == '7683513820537307167', 'Fixed distinct cluster identities required')
    need(set(plan.get('files', {})) == FILES, 'Exact mandatory input set required')
    files = {key: pinned(spec) for key, spec in plan['files'].items()}
    need(json.loads(files['stageBResult'].read_text())['status'] == 'PASS_PRIVATE_CLONE_FINITE_SESSION_ACL_ONLY'
         and json.loads(files['stageBResult'].read_text())['targetDatabase'] == SOURCE, 'Accepted StageB result required')
    need(json.loads(files['stageAResult'].read_text())['status'] == 'PASS_READONLY_SUPPLEMENT_OF_FAILED_STAGE_A02', 'Accepted production-snapshot StageA supplement required')
    need(isinstance(plan.get('runtimeImage'),str) and re.fullmatch(r'sha256:[0-9a-f]{64}', plan['runtimeImage']), 'Immutable compiled runtime image required')
    publication = real_path(pathlib.Path(plan['publicationDirectory']))
    socket = real_path(pathlib.Path(plan['socketHostDirectory']))
    data = real_path(pathlib.Path(plan['privateDataHostDirectory']))
    need(data.is_dir() and plan['privateDataContainerDestination'] == '/var/lib/postgresql/data', 'Reviewed actual PostgreSQL data mount required')
    need(publication.is_dir() and socket.is_dir() and str(publication).startswith('/home/amizo/erp-backups/')
         and str(socket).startswith('/home/amizo/erp-backups/'), 'Protected publication/socket paths required')
    manifest = json.loads(files['runtimeManifest'].read_text())
    need(isinstance(manifest,dict) and manifest and 'FullHostCompatibilityProbe.dll' in manifest, 'Whole compiled publication manifest required')
    actual = {}
    for item in publication.rglob('*'):
        need(not item.is_symlink(), 'No publication symlinks')
        if item.is_file(): actual[str(item.relative_to(publication))] = digest(item)
    need(actual == manifest, 'Whole publication differs; no stale/product overlay permitted')
    need(not any(k.startswith('/') or '..' in pathlib.PurePosixPath(k).parts for k in manifest), 'Bounded publication members required')
    return files, target, roles, publication, socket, data, manifest

ROLE_SQL = """BEGIN READ ONLY;
SELECT jsonb_build_object('roles',(SELECT coalesce(jsonb_agg(jsonb_build_array(rolname,rolcanlogin,rolsuper,rolcreatedb,rolcreaterole,rolreplication,rolbypassrls,rolinherit,rolconnlimit,rolvaliduntil::text,rolconfig) ORDER BY rolname),'[]'::jsonb) FROM pg_roles),
'memberships',(SELECT coalesce(jsonb_agg(jsonb_build_array(roleid,member,grantor,admin_option,inherit_option,set_option) ORDER BY roleid,member,grantor),'[]'::jsonb) FROM pg_auth_members))::text; ROLLBACK;"""
HBA_SQL = """BEGIN READ ONLY; SELECT coalesce(jsonb_agg(jsonb_build_object('ruleNumber',rule_number,'type',type,'database',database,'users',user_name,'authMethod',auth_method,'error',error) ORDER BY rule_number),'[]'::jsonb)::text FROM pg_hba_file_rules; ROLLBACK;"""

def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--plan', type=pathlib.Path, required=True)
    parser.add_argument('--expected-plan-sha256', required=True)
    parser.add_argument('--output', type=pathlib.Path, required=True)
    parser.add_argument('--execute-reviewed-private-login', action='store_true')
    args = parser.parse_args()
    need(args.execute_reviewed_private_login and os.environ.get('P30_FINITE_LOGIN_EXECUTE') == 'reviewed-private-only', 'Separate reviewed execution admission required')
    plan_path = pinned({'path':str(args.plan),'sha256':args.expected_plan_sha256})
    plan = json.loads(plan_path.read_text())
    files, target, roles, publication, socket, data, manifest = admit(plan, pathlib.Path(__file__))
    inputs = [plan_path, HERE, publication, socket, data, *files.values()]
    validate_output(args.output, inputs)
    helper = load_module(files['sqlHelper'], 'accepted_stageb_sql')
    acl = load_module(files['aclDeltaHelper'], 'accepted_stageb_acl')
    matrix = json.loads(files['effectiveMatrix'].read_text())['actual218MutationMatrix']
    expected_catalog = json.loads(files['sourceCatalog'].read_text())
    need(len(matrix) == len(expected_catalog['tables']) == 218 and len(expected_catalog['columns']) == 4097, 'Exact accepted218/4097 required')
    expected = alias_targets(acl, matrix, expected_catalog, target)
    proofs = [login_proof(helper, matrix, expected_catalog, which, alias) for which, alias in zip(('record','execution'), roles)]
    first_local_scram(json.loads(files['hbaRules'].read_text()), target, roles)
    headroom = validate_headroom([args.output.parent,data],plan['minimumFreeBytes'],shutil.disk_usage)
    # All file/plan/proof/output/auth/disk admission precedes the first Docker call.
    inspect = json.loads(subprocess.run(['docker','inspect',CONTAINER], capture_output=True, check=True, timeout=20).stdout)[0]
    need(inspect['Id'] == plan['privateContainerId'] and inspect['Image'] == plan['privateImageSha256']
         and inspect['State']['Running'] and inspect['HostConfig']['NetworkMode'] == 'none'
         and not inspect['HostConfig'].get('PortBindings'), 'Private cluster container differs')
    need(any(m.get('Type') == 'bind' and m.get('Source') == str(socket) and m.get('Destination') == '/var/run/postgresql'
             for m in inspect['Mounts']), 'Socket mount must belong to the exact private PostgreSQL container')
    need(any(m.get('Source') == str(data) and m.get('Destination') == plan['privateDataContainerDestination']
             for m in inspect['Mounts']), 'Headroom must cover the actual private PostgreSQL data mount')
    image = json.loads(subprocess.run(['docker','image','inspect',plan['runtimeImage']],capture_output=True,check=True,timeout=20).stdout)[0]
    need(image['Id'] == plan['runtimeImage'], 'Exact local runtime image required')
    validate_output(args.output, inputs)
    args.output.mkdir(mode=0o700)
    private_input = args.output/'finite-input'
    private_input.mkdir(mode=0o700)
    runtime_output = args.output/'finite-result'
    runtime_output.mkdir(mode=0o700)
    step = 0
    passwords = []
    def save(label, data):
        nonlocal step
        step += 1
        raw = data if isinstance(data,bytes) else json.dumps(data,sort_keys=True).encode()
        for password in passwords: raw = raw.replace(password.encode(),b'[REDACTED]')
        fd = os.open(args.output/f'{step:03d}-{label}.private',os.O_CREAT|os.O_EXCL|os.O_WRONLY|os.O_NOFOLLOW,0o600)
        with os.fdopen(fd,'wb') as stream: stream.write(raw)
    def sql(database, text, label):
        need(database in ('postgres', SOURCE, target), 'Private database allowlist')
        run = subprocess.run(['docker','exec','-i','-e','PGOPTIONS=-c lock_timeout=2s -c statement_timeout=90s -c idle_in_transaction_session_timeout=10s -c max_parallel_workers_per_gather=0',
            CONTAINER,'psql','-X','-qAt','-U','postgres','-d',database,'-v','ON_ERROR_STOP=1','-v','VERBOSITY=sqlstate','-f','-'],
            input=text if isinstance(text,bytes) else text.encode(),capture_output=True,timeout=180)
        save(label+'-stdout',run.stdout); save(label+'-stderr',run.stderr)
        need(run.returncode == 0, 'Private SQL failed; redacted protected evidence retained')
        return run.stdout
    def capture(database):
        return {key:json.loads(sql(database,files[file].read_bytes(),database+'-'+key)) for key,file in
                [('catalog','catalogSql'),('data','dataSql'),('acl','expandedAclSql')]}
    need(sql('postgres','BEGIN READ ONLY; SELECT system_identifier::text FROM pg_control_system(); ROLLBACK;','system').decode().strip() == plan['privateSystemIdentifier'], 'Private cluster identity differs')
    absent = "BEGIN READ ONLY; SELECT NOT EXISTS(SELECT 1 FROM pg_database WHERE datname='"+target+"') AND NOT EXISTS(SELECT 1 FROM pg_roles WHERE rolname IN ('"+"','".join(roles)+"')) AND NOT EXISTS(SELECT 1 FROM pg_stat_activity WHERE datname='"+SOURCE+"'); ROLLBACK;"
    need(sql('postgres',absent,'fresh-target-roles-idle-source').decode().strip() == 't', 'Fresh target/roles and idle source required; no repair')
    hba = json.loads(sql('postgres', HBA_SQL, 'current-hba-metadata'))
    need(hba == json.loads(files['hbaRules'].read_text()), 'Reviewed HBA metadata differs')
    first_local_scram(hba,target,roles)
    before_roles = json.loads(sql('postgres',ROLE_SQL,'roles-before'))
    # Plan pins all existing roles, not just the old aliases; no active-session drift masked.
    need(before_roles == json.loads(files['sourceRoles'].read_text()), 'Existing role/member baseline changed')
    for name in ('erp','procurement_api','procurement_execution_api_p30_01'):
        found = [r for r in before_roles['roles'] if r[0] == name]
        need(len(found) == 1 and found[0][1] is False, 'Original StageA/B roles must remain NOLOGIN')
    source = capture(SOURCE)
    need(source == {'catalog':expected_catalog,'data':json.loads(files['sourceData'].read_text()),'acl':json.loads(files['sourceAcl'].read_text())}, 'Accepted final StageB source changed')
    save('headroom-before-clone',validate_headroom([args.output.parent,data],plan['minimumFreeBytes'],shutil.disk_usage))
    clone = subprocess.run(['docker','exec','-e','PGOPTIONS=-c lock_timeout=2s -c statement_timeout=90s',CONTAINER,'createdb','-U','postgres','--template='+SOURCE,'--owner=erp',target],capture_output=True,timeout=90)
    save('createdb-stdout',clone.stdout);save('createdb-stderr',clone.stderr)
    need(clone.returncode == 0, 'Fresh private clone failed')
    need(capture(target) == source, 'New clone differs from accepted source')
    # Passwords never enter argv/environment or retained provisioning SQL.
    passwords.extend(secrets.token_hex(32) for _ in roles)
    sql(target,provision_sql(target,passwords),'provision-fresh-login-aliases')
    sql(target,grant_sql(expected),'direct-alias-grants')
    credential_check = "BEGIN READ ONLY; SELECT count(*)=2 AND bool_and(rolpassword LIKE 'SCRAM-SHA-256$%') FROM pg_authid WHERE rolname IN ('"+"','".join(roles)+"'); ROLLBACK;"
    need(sql('postgres',credential_check,'scram-verifier-kind-only').decode().strip() == 't', 'Actual password verifier must be SCRAM; no verifier bytes captured')
    login_inputs = []
    for which,role,password,(proof,transport) in zip(('record','execution'),roles,passwords,proofs):
        filename = which+'-proof.private.sql'
        destination = private_input/filename
        fd = os.open(destination,os.O_WRONLY|os.O_CREAT|os.O_EXCL|os.O_NOFOLLOW,0o600)
        with os.fdopen(fd,'w') as stream: stream.write(proof)
        save(which+'-proof-transport',transport)
        login_inputs.append({'Name':role,'Password':password,'ProofFile':filename,'ProofSha256':digest(destination)})
    fd = os.open(private_input/'login-input.private.json',os.O_WRONLY|os.O_CREAT|os.O_EXCL|os.O_NOFOLLOW,0o600)
    with os.fdopen(fd,'w') as stream: json.dump({'Database':target,'AuthMethod':'scram-sha-256','Logins':login_inputs},stream)
    # Verify publication and input pins again immediately before starting a non-host verifier.
    need({str(p.relative_to(publication)):digest(p) for p in publication.rglob('*') if p.is_file()} == manifest, 'Compiled publication changed before verifier')
    container_name = 'p30-finite-login-'+target[-2:]
    mounts = [(str(publication),'/app',False),(str(socket),'/private-pg-socket',False),
              (str(private_input),'/evidence',False),(str(runtime_output),'/result',True)]
    command = ['docker','create','--name',container_name,'--label','erp.private-proof=p30-restored-finite-login',
        '--label','erp.reviewed-plan-sha256='+args.expected_plan_sha256,'--network','none','--read-only','--cpus','1','--memory','512m','--memory-swap','512m',
        '--pids-limit','128','--tmpfs','/tmp:rw,nosuid,nodev,size=32m','--log-driver','none',
        '--mount','type=bind,src='+str(publication)+',dst=/app,readonly',
        '--mount','type=bind,src='+str(socket)+',dst=/private-pg-socket,readonly',
        '--mount','type=bind,src='+str(private_input)+',dst=/evidence,readonly',
        '--mount','type=bind,src='+str(runtime_output)+',dst=/result',
        '-e','P30_RESTORED_FINITE_LOGIN=reviewed-private-only','--entrypoint','dotnet',plan['runtimeImage'],
        '/app/FullHostCompatibilityProbe.dll','--restored-finite-login']
    created = subprocess.run(command,capture_output=True,timeout=30)
    save('verifier-create-stdout',created.stdout);save('verifier-create-stderr',created.stderr)
    need(created.returncode == 0, 'Fresh verifier container required; no replacement')
    identifier = created.stdout.decode().strip()
    need(re.fullmatch(r'[0-9a-f]{64}',identifier), 'Exact created verifier ID required')
    def verifier_state():
        state = json.loads(subprocess.run(['docker','inspect',identifier],capture_output=True,check=True,timeout=20).stdout)[0]
        verify_container(state,identifier,plan['runtimeImage'],container_name,mounts,args.expected_plan_sha256)
        return state
    need(not verifier_state()['State']['Running'],'New verifier must initially be stopped')
    try:
        run = subprocess.run(['docker','start','-a',identifier],capture_output=True,timeout=330)
    except subprocess.TimeoutExpired:
        # Retain the failed verifier container and evidence; bound its lifetime.
        verifier_state()  # Revalidate exact ID/image/labels/mounts before stopping only this container.
        stopped = subprocess.run(['docker','stop','--time','5',identifier],capture_output=True,timeout=15)
        save('verifier-timeout-stop',{'returnCode':stopped.returncode})
        raise RuntimeError('Finite verifier deadline exceeded')
    save('verifier-stdout',run.stdout);save('verifier-stderr',run.stderr)
    state = verifier_state()
    need(run.returncode == 0 and not state['State']['Running'] and state['State']['ExitCode'] == 0, 'Actual finite LOGIN verifier failed')
    result_path = runtime_output/'login-result.private.json'
    result = json.loads(result_path.read_text())
    need(result['status'] == 'PASS_PRIVATE_REAL_NPGSQL_LOGIN_ACL_ONLY' and result['database'] == target
         and {r['role'] for r in result['logins']} == set(roles) and len(result['logins']) == 2
         and all(r['actualLogin'] and r['wrongSecretSqlState'] == '28P01' and r['exactMatrixPassed']
                 and r['proofSha256'] == dict(zip(roles,[p[1]['adaptedSha256'] for p in proofs]))[r['role']] for r in result['logins']), 'Both authenticated proofs required')
    final = capture(target)
    need(final['data'] == source['data'] and structural(final['catalog']) == structural(source['catalog']), 'Clone business data or structural/default schema changed')
    verify_acl(acl,source['acl'],final['acl'],expected)
    after_roles = json.loads(sql('postgres',ROLE_SQL,'roles-after'))
    need(after_roles['memberships'] == before_roles['memberships'] and [r for r in after_roles['roles'] if r[0] not in roles] == before_roles['roles'], 'Existing role/member changed')
    need([r for r in after_roles['roles'] if r[0] in roles] == sorted([[r,True,False,False,False,False,False,False,-1,None,None] for r in roles]), 'Fresh LOGIN attributes differ')
    need(capture(SOURCE) == source, 'Original accepted StageB database changed')
    sql(target,files['sequenceSql'].read_bytes(),'sequence-inventory-still-exact')
    need(json.loads(sql('postgres',HBA_SQL,'hba-after')) == hba, 'HBA metadata changed')
    for spec in plan['files'].values(): pinned(spec)
    save('result',{'status':'PASS_PRIVATE_RESTORED_FINITE_LOGIN_ACL_ONLY','sourceDatabase':SOURCE,'targetDatabase':target,'aliases':list(roles),
        'realNpgsqlLogin':True,'wrongSecret28P01Both':True,'all218Tables4097Columns':True,'oldRolesRowsSchemaAclPreserved':True,
        'apiStarted':False,'publisherStarted':False,'businessOperationsProven':False,'productionTouched':False,
        'planSha256':args.expected_plan_sha256,'runtimeResultSha256':digest(result_path)})

if __name__ == '__main__':
    try: main()
    except Exception:
        # Never echo exceptions which may carry a password-bearing process/SQL context.
        raise SystemExit('Private finite LOGIN admission/verification failed; protected stage evidence retained.')
