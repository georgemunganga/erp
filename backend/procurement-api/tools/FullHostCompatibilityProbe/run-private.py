#!/usr/bin/env python3
"""Clone the approved private canonical backup, then run the compiled actual-Program smoke offline."""
import argparse
import hashlib
import json
import os
from pathlib import Path
import re
import subprocess
import shutil

CONTAINER = 'procurement-approval-rehearsal-postgres'
SOURCE = 'proc_release_backup_20261022_canonical_0619'
PAYMENT_UPGRADE_SOURCE = 'proc_release_backup_20261022_smoke_p24_native01'


def main():
    p = argparse.ArgumentParser(description=__doc__)
    p.add_argument('--publish-dir', type=Path, required=True)
    p.add_argument('--runtime-image', required=True, help='Existing local immutable sha256 image; never pulled')
    p.add_argument('--target-db', required=True)
    p.add_argument('--source-db', default=SOURCE, help='Verified private canonical source or prepared Core-principal upgrade clone; never migrated by this runner')
    p.add_argument('--output', type=Path, required=True)
    p.add_argument('--default-tenant-file', type=Path, help='Protected exact deployment tenant already verified against retained company/account scope; required for actual Program')
    modes = p.add_mutually_exclusive_group()
    modes.add_argument('--diagnose-hrm-model', action='store_true', help='Read-only source model operations; never start Program or migrate')
    modes.add_argument('--rehearse-fresh-hrm', action='store_true', help='New empty template0 private database, actual owning HRM MigrateAsync twice; never start Program')
    modes.add_argument('--rehearse-payment-owner-upgrade', action='store_true', help='New canonical clone only; exact reviewed Core timezone and P27 owning migrations, never start Program')
    a = p.parse_args()
    if len(a.source_db) > 63 or not (a.source_db in (SOURCE, PAYMENT_UPGRADE_SOURCE) or re.fullmatch(r'proc_release_backup_20261022_core_principal_[a-z0-9_]+', a.source_db)):
        p.error('Require the exact canonical source or a bounded verified private Core-principal clone')
    if a.source_db == a.target_db:
        p.error('Source and target must be distinct; never run against the retained source')
    if a.rehearse_payment_owner_upgrade and a.source_db != PAYMENT_UPGRADE_SOURCE:
        p.error('Payment owner upgrade requires the exact retained P24 actual-Program clone')
    if a.source_db == PAYMENT_UPGRADE_SOURCE and not a.rehearse_payment_owner_upgrade:
        p.error('The retained P24 source is admitted only for the explicit owner-upgrade rehearsal')
    target_pattern = (r'hrm_model_reconcile_20261022_[a-z0-9_]{1,24}' if a.rehearse_fresh_hrm else
        r'proc_release_backup_20261022_core_principal_[a-z0-9_]{1,16}' if a.rehearse_payment_owner_upgrade else
        r'proc_release_backup_20261022_smoke_[a-z0-9_]{1,28}')
    if not re.fullmatch(target_pattern, a.target_db) or len(a.target_db) > 63:
        p.error('Require new bounded smoke clone name')
    if not re.fullmatch(r'sha256:[0-9a-f]{64}', a.runtime_image):
        p.error('Require existing immutable local runtime image ID')
    if a.publish_dir.is_symlink() or not a.publish_dir.is_dir():
        p.error('Require an ordinary compiled publication directory')
    if a.output.resolve().is_relative_to(a.publish_dir.resolve()):
        p.error('Evidence/output must be outside the immutable publication directory')
    required = ['FullHostCompatibilityProbe.dll', 'FullHostCompatibilityProbe.runtimeconfig.json',
                'FullHostCompatibilityProbe.deps.json', 'Mightyfin.Erp.Hrm.Api.dll']
    if not all((a.publish_dir / name).is_file() for name in required) or a.output.exists():
        p.error('Require full compiled publication and unused protected evidence path')
    default_tenant = None
    if not a.rehearse_fresh_hrm and not a.diagnose_hrm_model and not a.rehearse_payment_owner_upgrade:
        if a.default_tenant_file is None or a.default_tenant_file.is_symlink() or not a.default_tenant_file.is_file():
            p.error('Actual owner composition requires a protected verified default-tenant file')
        if a.default_tenant_file.stat().st_mode & 0o077:
            p.error('Verified tenant file must not be group/world accessible')
        default_tenant = a.default_tenant_file.read_text()
        if not default_tenant or default_tenant != default_tenant.strip() or len(default_tenant) > 200 or any(ord(c) < 32 for c in default_tenant):
            p.error('Verified default tenant is invalid')
    os.umask(0o077)
    a.output.mkdir(parents=True, mode=0o700)

    def docker(*args, data=None, label=None):
        r = subprocess.run(['docker', *args], input=data, text=True, capture_output=True)
        if label:
            (a.output / (label + '.log')).write_text(r.stdout + r.stderr)
        if r.returncode:
            raise RuntimeError('Private operation failed; retain clone/evidence, do not print restricted diagnostics')
        return r.stdout

    info = json.loads(docker('inspect', CONTAINER))
    if len(info) != 1 or info[0]['Name'] != '/' + CONTAINER or not info[0]['State']['Running'] or info[0]['HostConfig']['NetworkMode'] != 'none':
        raise RuntimeError('Require exact running network-none private PostgreSQL before clone DDL')
    socket = [m['Source'] for m in info[0]['Mounts'] if m['Destination'] == '/var/run/postgresql' and m['Type'] == 'bind']
    if len(socket) != 1 or not Path(socket[0]).is_dir():
        raise RuntimeError('Require the existing private bound Unix socket; never enable TCP networking')
    docker('image', 'inspect', a.runtime_image)  # --pull never below; no network dependency.
    exists = docker('exec', '-i', CONTAINER, 'psql', '-X', '-U', 'postgres', '-d', 'postgres',
                    '-qAt', '-v', 'ON_ERROR_STOP=1', '-v', 'VERBOSITY=terse',
                    data="SELECT count(*) FROM pg_database WHERE datname='" + a.source_db + "';")
    if exists.strip() != '1':
        raise RuntimeError('Approved retained canonical source is missing')
    def source_footprint(label):
        # Same table-data/history + column/constraint/index scope as the compiled
        # smoke footprint. Only server-produced digests/counts leave PostgreSQL;
        # source rows, default expressions and credentials are never printed.
        sql = r"""BEGIN ISOLATION LEVEL REPEATABLE READ READ ONLY;
SET LOCAL TIME ZONE 'UTC';
SET LOCAL DateStyle='ISO,YMD';
SELECT format($finger$
SELECT %L||count(*)::text||':'||encode(sha256(convert_to(coalesce(string_agg(h,'' ORDER BY h),''),'UTF8')),'hex')
FROM (SELECT encode(sha256(convert_to(to_jsonb(r)::text,'UTF8')),'hex') h FROM %I.%I r) q;
$finger$,n.nspname||'.'||c.relname||':',n.nspname,c.relname)
FROM pg_class c JOIN pg_namespace n ON n.oid=c.relnamespace
WHERE c.relkind IN ('r','p') AND n.nspname NOT LIKE 'pg_%' AND n.nspname<>'information_schema'
ORDER BY n.nspname,c.relname
\gexec
SELECT 'schema:'||encode(sha256(convert_to(coalesce(string_agg(s,E'\n' ORDER BY s),''),'UTF8')),'hex') FROM (
 SELECT n.nspname||'.'||c.relname||':'||a.attnum||':'||a.attname||':'||format_type(a.atttypid,a.atttypmod)||':'||a.attnotnull::text||':'||coalesce(pg_get_expr(d.adbin,d.adrelid),'') s
 FROM pg_class c JOIN pg_namespace n ON n.oid=c.relnamespace
 JOIN pg_attribute a ON a.attrelid=c.oid AND a.attnum>0 AND NOT a.attisdropped
 LEFT JOIN pg_attrdef d ON d.adrelid=c.oid AND d.adnum=a.attnum
 WHERE c.relkind IN ('r','p') AND n.nspname NOT LIKE 'pg_%' AND n.nspname<>'information_schema'
 UNION ALL SELECT n.nspname||'.'||c.relname||':'||k.conname||':'||pg_get_constraintdef(k.oid)
 FROM pg_constraint k JOIN pg_class c ON c.oid=k.conrelid JOIN pg_namespace n ON n.oid=c.relnamespace
 WHERE n.nspname NOT LIKE 'pg_%' AND n.nspname<>'information_schema'
 UNION ALL SELECT schemaname||'.'||tablename||':'||indexname||':'||indexdef FROM pg_indexes
 WHERE schemaname NOT LIKE 'pg_%' AND schemaname<>'information_schema'
) q;
ROLLBACK;"""
        value = docker('exec', '-i', CONTAINER, 'psql', '-X', '-qAt', '-U', 'postgres', '-d', a.source_db,
                       '-v', 'ON_ERROR_STOP=1', '-v', 'VERBOSITY=terse', data=sql, label=label)
        if not value.strip():
            raise RuntimeError('Unavailable retained source footprint; no compatibility acceptance')
        return hashlib.sha256(value.encode('utf-8')).hexdigest()

    source_before = source_footprint('source-before')
    try:
        # createdb refuses an existing target. Never DROP, terminate sessions, alter source or roles.
        docker('exec', '-i', CONTAINER, 'createdb', '-U', 'postgres', '--template=' + ('template0' if a.rehearse_fresh_hrm else a.source_db), a.target_db,
               label='clone')
        # Freeze every loaded artifact before launch; later builds cannot replace running DLLs.
        if any(path.is_symlink() for path in a.publish_dir.rglob('*')):
            raise RuntimeError('Publication must contain regular files, not external configuration links')
        runtime = a.output / 'runtime'
        shutil.copytree(a.publish_dir, runtime)
        def runtime_hashes():
            return {str(path.relative_to(runtime)): hashlib.sha256(path.read_bytes()).hexdigest()
                    for path in runtime.rglob('*') if path.is_file()}
        manifest = runtime_hashes()
        (a.output / 'run-manifest.json').write_text(json.dumps({'source': a.source_db, 'target': a.target_db,
            'runtimeImage': a.runtime_image, 'networkMode': 'none', 'databaseTemplate': 'template0' if a.rehearse_fresh_hrm else a.source_db, 'freshHrmRehearsal': a.rehearse_fresh_hrm, 'publishedArtifacts': manifest,
            'limit': 'Owner credential on new private clone only; no finite-role or production claim'}, indent=2) + '\n')
        # Only the Unix socket is shared. No Docker socket, host network, source data directory,
        # actual configuration directory or unrestricted source database connection is mounted.
        connection = 'Host=/private-pg;Database=' + a.target_db + ';Username=postgres;Pooling=false;Include Error Detail=false'
        r = subprocess.run(['docker', 'run', '--rm', '--pull=never', '--network=none', '--memory=512m', '--cpus=1',
            '--read-only', '--tmpfs', '/tmp:rw,nosuid,nodev,size=64m',
            '--mount', 'type=bind,src=' + str(runtime.resolve()) + ',dst=/app,readonly',
            '--mount', 'type=bind,src=' + socket[0] + ',dst=/private-pg,readonly',
            '--mount', 'type=bind,src=' + str(a.output.resolve()) + ',dst=/evidence',
            '--env', 'ERP_HOST_SMOKE_CONNECTION=' + connection,
            '--env', 'ERP_HOST_SMOKE_DEFAULT_TENANT=' + (default_tenant or ''),
            '--env', 'ERP_HOST_SMOKE_CATALOG_SOURCE=' + a.source_db,
            '--env', 'ERP_HOST_SMOKE_RESULT=/evidence/result.json',
            '--env', 'DOTNET_EnableDiagnostics=0', '--workdir', '/app', a.runtime_image,
            'dotnet', '/app/FullHostCompatibilityProbe.dll'] + (['--diagnose-hrm-model'] if a.diagnose_hrm_model else ['--rehearse-fresh-hrm'] if a.rehearse_fresh_hrm else ['--rehearse-payment-owner-upgrade'] if a.rehearse_payment_owner_upgrade else []), text=True, capture_output=True)
        (a.output / 'host-smoke.log').write_text(r.stdout + r.stderr)
        if runtime_hashes() != manifest:
            raise RuntimeError('Frozen smoke runtime changed; no compatibility result can be accepted')
        if r.returncode and not (a.diagnose_hrm_model and r.returncode == 2):
            raise RuntimeError('Actual Program smoke blocked/failed; restricted summary/log and clone retained')
        result = json.loads((a.output / 'result.json').read_text())
        expected = 'OWNER_MODEL_DIAGNOSTIC_ONLY_PROGRAM_NOT_STARTED' if a.diagnose_hrm_model else 'PASS'
        unchanged = (result.get('secondMigrationSchemaHistoryAndDataUnchanged') if a.rehearse_fresh_hrm else
            result.get('existingRowsAndUnrelatedSchemaUnchanged') if a.rehearse_payment_owner_upgrade else result.get('allTableValuesAndSchemasUnchanged'))
        if result.get('status') != expected or unchanged is not True:
            raise RuntimeError('Actual Program smoke did not prove unchanged clone')
    finally:
        source_after = source_footprint('source-after')
        source_unchanged = source_before == source_after
        (a.output / 'source-preservation.json').write_text(json.dumps({
            'source': a.source_db, 'target': a.target_db,
            'sourceBeforeSha256': source_before, 'sourceAfterSha256': source_after,
            'allSourceTableDataHistoryAndSchemasUnchanged': source_unchanged,
            'limits': 'Read-only source; table data, columns, constraints and indexes only, not sequence state or roles'
        }, indent=2) + '\n')
        if not source_unchanged:
            raise RuntimeError('Retained source changed; preserve all evidence and refuse compatibility acceptance')
    print('PASS private owner upgrade; Program not started; protected summary retained.' if a.rehearse_payment_owner_upgrade else 'PASS private fresh HRM actual EF migration rehearsal; Program not started; protected summary retained.' if a.rehearse_fresh_hrm else 'Private owner model diagnostic complete; Program not started; protected summary retained.' if a.diagnose_hrm_model else 'PASS private offline actual Program smoke; protected summary retained, no ERP rows output.')


if __name__ == '__main__':
    main()
