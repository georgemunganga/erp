#!/usr/bin/env python3
"""Reviewed, private-only P24 backup restore and P30 owner/ACL rehearsal.

This source is inert without a complete hash-pinned reviewed plan and an explicit
execution flag. It never connects to or writes to the production database.
"""
import argparse
import hashlib
import importlib.util
import json
import os
import pathlib
import re
import shutil
import subprocess

os.umask(0o077)
PRIVATE_CONTAINER = 'procurement-approval-rehearsal-postgres'
PRIVATE_DB_PREFIX = r'proc_p30_full_acl_20261024_[a-z0-9_]{1,24}'
MIN_FREE_BYTES = 1_073_741_824 + 200_000_000
EXPECTED_MIGRATIONS = [
    ('hrm', '20261023130726_CoreCompanyTimeZone'),
    ('procurement', '20261022161353_P25CorePrincipalRequestApproval'),
    ('procurement', '20261023125243_P27ManualPaymentRecords'),
    ('procurement', '20261023153226_P28ManualPaymentGovernance'),
    ('procurement', '20261023204131_P29CorePrincipalSourcingDrafts'),
    ('procurement', '20261023213118_P29CanonicalPrincipalSourceGuids'),
    ('procurement', '20261023222748_P30PrincipalCommercialWorkflows'),
    ('procurement', '20261023234712_P30PrincipalCommercialAuthorityActions'),
]
PINNED_KEYS = (
    'sourceArchive', 'sourceBackupMetadata', 'sourceBackupResult', 'cutoverPlan',
    'sourceBaselinePrivileges', 'baselineDatabaseProperties', 'baselineSchemaSql', 'baselinePrivilegeSql',
    'baselineDataSql', 'finalSchemaSql', 'finalHistorySql', 'newTableAclSql',
    'finiteAclSql', 'finiteNegativeSql', 'fullFingerprintSql', 'sequenceStateSql',
    'restoreEquivalenceHelper',
)


def require(ok, message):
    if not ok:
        raise RuntimeError(message)


def digest(path):
    h = hashlib.sha256()
    with path.open('rb') as stream:
        for block in iter(lambda: stream.read(1024 * 1024), b''):
            h.update(block)
    return h.hexdigest()


def pinned(spec):
    require(isinstance(spec, dict), 'Missing reviewed file pin.')
    raw_path, expected = spec.get('path'), spec.get('sha256')
    require(isinstance(raw_path, str) and isinstance(expected, str)
            and re.fullmatch(r'[0-9a-f]{64}', expected), 'Incomplete reviewed file pin.')
    path = pathlib.Path(raw_path)
    require(path.is_absolute() and path.is_file() and not path.is_symlink(),
            'Pinned input must be a regular absolute file.')
    require(digest(path) == expected, 'Reviewed input hash mismatch.')
    return path


def run(args, *, input_bytes=None, timeout=600):
    return subprocess.run(args, input=input_bytes, stdout=subprocess.PIPE,
                          stderr=subprocess.PIPE, timeout=timeout, check=False)


def checked_run(args, *, input_bytes=None, timeout=600):
    result = run(args, input_bytes=input_bytes, timeout=timeout)
    # Neither stderr nor stdout may contain safe-to-print business or secret data.
    require(result.returncode == 0, 'Reviewed private operation failed; inspect protected evidence.')
    return result.stdout, result.stderr


def private_sql(sql, db, *, role=None, timeout=600):
    require(role in (None, 'erp', 'procurement_api'), 'Unknown private role.')
    require(db == 'postgres' or re.fullmatch(PRIVATE_DB_PREFIX, db), 'Private database only.')
    options = '-c lock_timeout=15s -c statement_timeout=600s'
    if role:
        options += ' -c role=' + role
    args = ['docker', 'exec', '-i', '-e', 'PGOPTIONS=' + options,
            PRIVATE_CONTAINER, 'psql', '-X', '-qAt', '-U', 'postgres', '-d', db,
            '-v', 'ON_ERROR_STOP=1', '-v', 'VERBOSITY=terse', '-f', '-']
    return checked_run(args, input_bytes=sql.encode() if isinstance(sql, str) else sql,
                       timeout=timeout)[0]


def read_only_sql(path, db, *, role=None):
    sql = path.read_bytes()
    require(b'BEGIN READ ONLY' in sql.upper() and (b'COMMIT' in sql.upper() or b'ROLLBACK' in sql.upper()),
            'Read-only verification SQL must explicitly bound a read-only transaction.')
    return private_sql(sql, db, role=role)


def validate_system_identifiers(plan):
    source = plan.get('sourceBackupSystemIdentifier')
    production = plan.get('productionSystemIdentifier')
    private = plan.get('privateSystemIdentifier')
    require(source == '7683513820537307167' and production == source,
            'Production system identifier must equal the pinned backup source.')
    require(isinstance(private, str) and re.fullmatch(r'[0-9]+', private)
            and private != source, 'A separate pinned private cluster is required.')


def catalog_snapshot(db):
    sql = """BEGIN READ ONLY;
SELECT jsonb_build_object('database',(SELECT to_jsonb(d) FROM pg_database d WHERE datname=current_database()),
 'owner',(SELECT pg_get_userbyid(datdba) FROM pg_database WHERE datname=current_database()),
 'settings',(SELECT coalesce(jsonb_agg(jsonb_build_object('role',CASE WHEN s.setrole=0 THEN 'ALL' ELSE pg_get_userbyid(s.setrole) END,'config',s.setconfig) ORDER BY s.setrole),'[]'::jsonb)
 FROM pg_db_role_setting s WHERE s.setdatabase=(SELECT oid FROM pg_database WHERE datname=current_database())))::text;
ROLLBACK;"""
    return json.loads(private_sql(sql, db))


def ensure_private_container(plan):
    validate_system_identifiers(plan)
    raw, _ = checked_run(['docker', 'inspect', PRIVATE_CONTAINER], timeout=20)
    items = json.loads(raw)
    require(len(items) == 1, 'Exact private container required.')
    item = items[0]
    require(item['Name'] == '/' + PRIVATE_CONTAINER and item['State']['Running']
            and item['Id'] == plan['privateContainerId']
            and item['Image'] == plan['privateImageSha256']
            and item['HostConfig']['NetworkMode'] == 'none',
            'Pinned isolated private container mismatch.')
    system = private_sql('BEGIN READ ONLY; SELECT system_identifier::text FROM pg_control_system(); ROLLBACK;', 'postgres').decode().strip()
    require(system == plan['privateSystemIdentifier'] and system != plan['productionSystemIdentifier'],
            'Separate pinned private PostgreSQL cluster required.')
    return {'containerId': item['Id'], 'image': item['Image'], 'systemIdentifier': system}


def verify_finite_role_state(roles, memberships):
    require(isinstance(roles, list) and len(roles) == 2
            and {r.get('name') for r in roles} == {'erp', 'procurement_api'}
            and all(not any(r.get(k) is not False for k in
                            ('login', 'super', 'createdb', 'createrole', 'replication', 'bypassrls'))
                    for r in roles), 'Existing private owner/runtime roles must be finite NOLOGIN.')
    require(isinstance(memberships, list) and not memberships,
            'Unexpected private role membership.')
    return {'roles': roles, 'memberships': memberships}


def ensure_finite_private_roles():
    sql = """BEGIN READ ONLY;
SELECT coalesce(jsonb_agg(jsonb_build_object('name',rolname,'login',rolcanlogin,'super',rolsuper,
 'createdb',rolcreatedb,'createrole',rolcreaterole,'replication',rolreplication,'bypassrls',rolbypassrls,
 'inherit',rolinherit,'connlimit',rolconnlimit,'validUntil',rolvaliduntil)
 ORDER BY rolname),'[]'::jsonb)::text
FROM pg_roles WHERE rolname IN ('erp','procurement_api');
ROLLBACK;"""
    roles = json.loads(private_sql(sql, 'postgres'))
    memberships = json.loads(private_sql("""BEGIN READ ONLY;
SELECT coalesce(jsonb_agg(jsonb_build_object('role', pg_get_userbyid(roleid),
 'member', pg_get_userbyid(member), 'grantor', pg_get_userbyid(grantor),
 'admin', admin_option, 'inherit', inherit_option, 'set', set_option)
 ORDER BY roleid, member, grantor),'[]'::jsonb)::text
FROM pg_auth_members WHERE roleid IN (SELECT oid FROM pg_roles WHERE rolname IN ('erp','procurement_api'))
 OR member IN (SELECT oid FROM pg_roles WHERE rolname IN ('erp','procurement_api'));
ROLLBACK;""", 'postgres'))
    return verify_finite_role_state(roles, memberships)


def checked_migration_sequence(plan):
    items = plan['ownerSqlSequence']
    require(isinstance(items, list) and len(items) == len(EXPECTED_MIGRATIONS),
            'Exact HRM plus seven Procurement owner Up files required.')
    result = []
    for item, expected in zip(items, EXPECTED_MIGRATIONS):
        require((item.get('context'), item.get('migrationId')) == expected,
                'Migration order or identity differs from reviewed P30 cutover.')
        result.append((expected, pinned(item)))
    return result


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--plan', required=True, type=pathlib.Path)
    parser.add_argument('--expected-plan-sha256', required=True)
    parser.add_argument('--output', required=True, type=pathlib.Path)
    parser.add_argument('--execute-reviewed-private-rehearsal', action='store_true')
    args = parser.parse_args()
    require(args.execute_reviewed_private_rehearsal, 'Explicit private rehearsal flag required.')
    plan_file = pinned({'path': str(args.plan), 'sha256': args.expected_plan_sha256})
    plan = json.loads(plan_file.read_text())
    require(plan.get('status') == 'REVIEWED_PRIVATE_REHEARSAL_READY', 'Plan is incomplete or unreviewed.')
    require(plan.get('runnerSha256') == digest(pathlib.Path(__file__)), 'Reviewed runner changed.')
    validate_system_identifiers(plan)
    require(plan.get('sourceBackupSystemIdentifier') == '7683513820537307167'
            and plan.get('sourceBackupDatabase') == 'erp'
            and plan.get('sourceBackupOwner') == 'erp'
            and plan.get('expectedOriginalTables') == 191
            and plan.get('expectedOriginalColumns') == 3464,
            'Exact observed P24 backup/baseline required.')
    target = plan.get('targetDatabase')
    require(isinstance(target, str) and re.fullmatch(PRIVATE_DB_PREFIX, target)
            and target not in ('erp', 'postgres'), 'Fresh bounded private database required.')
    files = {key: pinned(plan.get(key)) for key in PINNED_KEYS}
    migration_files = checked_migration_sequence(plan)
    backup_metadata = json.loads(files['sourceBackupMetadata'].read_text())
    backup_result = json.loads(files['sourceBackupResult'].read_text())
    cutover = json.loads(files['cutoverPlan'].read_text())
    require(backup_metadata['source']['database'] == 'erp'
            and backup_metadata['source']['system'] == plan['sourceBackupSystemIdentifier']
            and backup_metadata['source']['owner'] == 'erp'
            and backup_metadata['source']['applicationTables'] == 191
            and backup_result['archiveSha256'] == plan['sourceArchive']['sha256']
            and backup_result['status'] == 'PROTECTED_CONSISTENT_READONLY_BACKUP_CREATED_RESTORE_UNVERIFIED'
            and cutover['productionBaseline']['lastProcurementMigration']
                == '20261022144102_P24CorePrincipalRecurringPurchasePlanning'
            and cutover['procurementMigrationsInOrder'] == [x[1] for x in EXPECTED_MIGRATIONS[1:]]
            and cutover['otherContextDependencies']['hrm'] == [EXPECTED_MIGRATIONS[0][1]],
            'Backup and cutover lineage mismatch.')
    with files['sourceArchive'].open('rb') as stream:
        require(stream.read(5) == b'PGDMP', 'Pinned PostgreSQL custom archive required.')
    expected_properties = json.loads(files['baselineDatabaseProperties'].read_text())
    helper_spec = importlib.util.spec_from_file_location('p30_restore_equivalence', files['restoreEquivalenceHelper'])
    helper = importlib.util.module_from_spec(helper_spec)
    helper_spec.loader.exec_module(helper)
    output = args.output
    require(output.is_absolute() and not output.exists() and not output.is_symlink(),
            'Fresh protected result directory required.')
    require(shutil.disk_usage(output.parent).free >= MIN_FREE_BYTES,
            'Insufficient free space for a retained private restore plus headroom.')
    private_identity = ensure_private_container(plan)
    initial_role_state = ensure_finite_private_roles()  # Never repair or elevate private roles.
    exists = private_sql("BEGIN READ ONLY; SELECT count(*) FROM pg_database WHERE datname='" + target + "'; ROLLBACK;", 'postgres').strip()
    require(exists == b'0', 'Private target database already exists.')
    output.mkdir(mode=0o700)
    step = 0

    def evidence(label, payload):
        nonlocal step
        step += 1
        path = output / f'{step:03d}-{label}.private'
        with path.open('xb') as stream:
            stream.write(payload)
        path.chmod(0o600)
        return payload

    def verify_read(label, key, *, role=None):
        return evidence(label, read_only_sql(files[key], target, role=role))

    try:
        evidence('private-identity', json.dumps(private_identity).encode())
        # Exact creation settings and database ACL require independent review in the plan.
        create_args = plan['createDbArgs']
        require(create_args == ['--template=template0', '--owner=erp', '--encoding=UTF8',
                                '--lc-collate=en_US.utf8', '--lc-ctype=en_US.utf8',
                                '--locale-provider=libc'], 'Unexpected private database creation options.')
        _, err = checked_run(['docker', 'exec', PRIVATE_CONTAINER, 'createdb', '-U', 'postgres',
                              *create_args, target], timeout=60)
        evidence('private-createdb-stderr', err)
        private_sql(f'REVOKE ALL PRIVILEGES ON DATABASE \"{target}\" FROM PUBLIC; '
                    f'GRANT TEMPORARY, CONNECT ON DATABASE \"{target}\" TO PUBLIC; '
                    f'GRANT CREATE,TEMPORARY,CONNECT ON DATABASE \"{target}\" TO erp; '
                    f'GRANT CONNECT ON DATABASE \"{target}\" TO procurement_api;',
                    target, role='erp')
        helper.verify_database(expected_properties, 'erp', catalog_snapshot(target))
        with files['sourceArchive'].open('rb') as archive:
            process = subprocess.run(['docker', 'exec', '-i', PRIVATE_CONTAINER, 'pg_restore',
                                      '-U', 'postgres', '--dbname', target, '--exit-on-error'],
                                     stdin=archive, stdout=subprocess.PIPE, stderr=subprocess.PIPE,
                                     timeout=1800, check=False)
        evidence('private-restore-stderr', process.stderr)
        require(process.returncode == 0, 'Private full-owner/ACL restore failed.')
        helper.verify_database(expected_properties, 'erp', catalog_snapshot(target))
        verify_read('baseline-p24-schema', 'baselineSchemaSql')
        before_acl = json.loads(verify_read('original-boundary-before', 'baselinePrivilegeSql'))
        source_acl = json.loads(files['sourceBaselinePrivileges'].read_text())
        helper.verify_restored_privileges(source_acl, before_acl, 191, 3464)
        require(len(before_acl['originalObjects']) == 191 and len(before_acl['originalColumnAcls']) == 3464,
                'Complete P24 original privilege inventory required.')
        original_data = verify_read('original-data-before', 'baselineDataSql')
        for (context, migration_id), source in migration_files:
            # Owner SQL has no P31/P32/P33 input path. Each file is independently pinned.
            sql = source.read_bytes()
            private_sql(sql, target, role='erp', timeout=1200)
            evidence('owner-up-' + context + '-' + migration_id, source.name.encode())
        require(ensure_finite_private_roles() == initial_role_state,
                'Owner Up changed private role attributes or memberships.')
        verify_read('final-p30-schema', 'finalSchemaSql')
        verify_read('final-p30-history', 'finalHistorySql')
        after_acl = json.loads(verify_read('original-boundary-after', 'baselinePrivilegeSql'))
        helper.verify_restored_privileges(before_acl, after_acl, 191, 3464)
        require(original_data == verify_read('original-data-after', 'baselineDataSql'),
                'Original P24 data fingerprint changed.')
        helper.verify_database(expected_properties, 'erp', catalog_snapshot(target))
        verify_read('new-table-acls', 'newTableAclSql')
        verify_read('finite-acl-readonly', 'finiteAclSql', role='procurement_api')
        fingerprint_before = verify_read('full-fingerprint-before-finite', 'fullFingerprintSql')
        sequences_before = verify_read('sequences-before-finite', 'sequenceStateSql')
        negative_sql = files['finiteNegativeSql'].read_bytes()
        require(b'BEGIN' in negative_sql.upper() and b'ROLLBACK' in negative_sql.upper(),
                'Finite negative probe must be explicitly rolled back.')
        evidence('finite-negative-matrix', private_sql(negative_sql, target, role='procurement_api'))
        require(ensure_finite_private_roles() == initial_role_state,
                'Finite role checks changed private role attributes or memberships.')
        require(fingerprint_before == verify_read('full-fingerprint-after-finite', 'fullFingerprintSql')
                and sequences_before == verify_read('sequences-after-finite', 'sequenceStateSql'),
                'Finite role matrix changed private business data or sequences.')
        require(original_data == verify_read('original-data-after-finite', 'baselineDataSql'),
                'Finite role matrix changed original data.')
        require(after_acl == json.loads(verify_read('original-boundary-after-finite', 'baselinePrivilegeSql')),
                'Finite role matrix changed original privilege boundary.')
        require(digest(files['sourceArchive']) == plan['sourceArchive']['sha256'],
                'Pinned source archive changed during rehearsal.')
        evidence('result', json.dumps({'status': 'PASS_PRIVATE_P30_FULL_ACL_REHEARSAL',
                                      'targetDatabase': target, 'sourceArchiveSha256': plan['sourceArchive']['sha256'],
                                      'productionMutated': False, 'privateSetRoleOnly': True,
                                      'realRuntimeLoginOrHttpProven': False,
                                      'sourceWritersStopped': False}, sort_keys=True).encode())
    except Exception:
        # Leave the fresh private database and protected evidence for diagnosis.
        raise


if __name__ == '__main__':
    main()
