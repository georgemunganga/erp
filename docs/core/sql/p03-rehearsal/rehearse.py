#!/usr/bin/env python3
"""Clone-only private PostgreSQL17 Core P03 DDL preservation rehearsal, not runtime authority."""
import argparse
import datetime
import hashlib
import json
import os
from pathlib import Path
import re
import subprocess

P01 = '20261020060744_P01CoreDocumentFoundation'
P02 = '20261020063543_P02CoreDocumentUploadRecovery'
P03 = '20261021080548_P03DocumentUseRetention'
STOP = 'CoreDocumentUseRollbackWouldLoseEvidence'


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--container', default='procurement-approval-rehearsal-postgres')
    parser.add_argument('--source-db', required=True, help='Closed genuine workflow P02 database without use tables')
    parser.add_argument('--used-db', required=True, help='Closed genuine P03 opt-in use-pin probe database')
    parser.add_argument('--prefix', required=True, help='New core_doc_rehearsal_* clone prefix')
    parser.add_argument('--baseline-sql', type=Path, required=True, help='Reviewed zero-to-P02 SQL')
    parser.add_argument('--up-sql', type=Path, required=True, help='Reviewed idempotent Up containing P03')
    parser.add_argument('--down-sql', type=Path, required=True, help='Reviewed transactional P03-to-P02 SQL')
    parser.add_argument('--output', type=Path, required=True)
    args = parser.parse_args()
    if args.container != 'procurement-approval-rehearsal-postgres':
        parser.error('Only the fixed private rehearsal container is supported')
    for value, limit in ((args.source_db, 63), (args.used_db, 63), (args.prefix, 43)):
        if not re.fullmatch(r'core_doc_rehearsal_[a-z0-9_]+', value) or len(value) > limit:
            parser.error('Use bounded private core_doc_rehearsal_* database names')
    if args.source_db == args.used_db or any(name.startswith(args.prefix) for name in (args.source_db, args.used_db)):
        parser.error('Original sources and new clone prefix must be distinct')
    files = {'baseline': args.baseline_sql, 'up': args.up_sql, 'down': args.down_sql}
    # Consume EF UTF8 BOM only for execution; provenance always hashes original file bytes.
    scripts = {name: file.read_text(encoding='utf-8-sig') for name, file in files.items()}
    if P01 not in scripts['baseline'] or P02 not in scripts['baseline'] or P03 in scripts['baseline']:
        parser.error('Baseline must be reviewed zero-to-P02 SQL without P03')
    if P03 not in scripts['up'] or P03 not in scripts['down'] or STOP not in scripts['down']:
        parser.error('Require actual P03 migration and exact evidence stop in reviewed scripts')
    if not re.search(r'\b(?:START\s+TRANSACTION|BEGIN)\s*;', scripts['down'], re.I):
        parser.error('Down must retain its owning migration transaction')
    forbidden = r'DROP\s+DATABASE|pg_terminate_backend|pg_cancel_backend|session_replication_role|DISABLE\s+TRIGGER'
    if any(re.search(forbidden, script, re.I) for script in scripts.values()):
        parser.error('Database drops, forced clients and constraint/trigger bypasses are forbidden')
    if args.output.exists():
        parser.error('Never replace prior evidence')
    os.umask(0o077)
    args.output.mkdir(parents=True, mode=0o700)
    sequence = 0

    def run(program, *options, data=None, allow_failure=False):
        nonlocal sequence
        sequence += 1
        result = subprocess.run(['docker', 'exec', '-i', args.container, program, *options],
                                input=data, text=True, capture_output=True)
        (args.output / f'{sequence:03}-{program}.log').write_text(result.stdout + result.stderr)
        if result.returncode and not allow_failure:
            raise RuntimeError(f'Private operation failed; retained log {sequence:03}')
        return result

    def sql(db, statement, allow_failure=False):
        return run('psql', '-X', '-U', 'postgres', '-d', db, '-qAt', '-v', 'ON_ERROR_STOP=1',
                   '-v', 'VERBOSITY=verbose', data="SET statement_timeout='60s'; SET lock_timeout='10s';\n" + statement,
                   allow_failure=allow_failure)

    def literal(value):
        return "'" + value.replace("'", "''") + "'"

    def identifier(value):
        return '"' + value.replace('"', '""') + '"'

    def json_sql(db, query):
        return json.loads(sql(db, query).stdout)

    def closed(db):
        # Queries run on postgres, not the source. No terminate/cancel, reconnect pool or DB replacement.
        if sql('postgres', 'SELECT count(*) FROM pg_stat_activity WHERE datname=' + literal(db) + ';').stdout.strip() != '0':
            raise RuntimeError('Source must be normally closed before copying: ' + db)

    def shape(db):
        return json_sql(db, """SELECT coalesce(jsonb_object_agg(table_name,cols),'{}'::jsonb) FROM (
            SELECT table_name,jsonb_agg(jsonb_build_object('name',column_name,'type',udt_name,
              'nullable',is_nullable,'default',column_default,'length',character_maximum_length,
              'precision',numeric_precision,'scale',numeric_scale,'datetimePrecision',datetime_precision,
              'identity',is_identity,'generated',is_generated,'collation',collation_name)
              ORDER BY ordinal_position) cols
            FROM information_schema.columns WHERE table_schema='core_document'
              AND table_name<>'__EFMigrationsHistory' GROUP BY table_name) s;""")

    def values(db, columns):
        parts = []
        for table, fields in sorted(columns.items()):
            projection = ','.join(identifier(field['name']) for field in fields)
            parts.append(f"SELECT {literal(table)} name,coalesce(jsonb_agg(to_jsonb(r) ORDER BY to_jsonb(r)::text),'[]'::jsonb) rows FROM (SELECT {projection} FROM core_document.{identifier(table)}) r")
        return sql(db, "SELECT coalesce(jsonb_object_agg(name,rows),'{}'::jsonb) FROM (" + ' UNION ALL '.join(parts) + ') r;').stdout.strip()

    def history(db):
        return json_sql(db, 'SELECT coalesce(jsonb_agg(to_jsonb(h) ORDER BY "MigrationId"),\'[]\'::jsonb) FROM core_document."__EFMigrationsHistory" h;')

    def schema(db):
        result = run('pg_dump', '-U', 'postgres', '-d', db, '--schema-only', '--schema=core_document', '--no-owner', '--no-privileges').stdout
        return '\n'.join(line for line in result.splitlines() if not line.startswith(('\\restrict ', '\\unrestrict ')))

    def original_constraints(db):
        return json_sql(db, """SELECT coalesce(jsonb_agg(to_jsonb(c) ORDER BY c.table_name,c.name),'[]'::jsonb) FROM (
            SELECT r.relname table_name,c.conname name,pg_get_constraintdef(c.oid,false) definition,c.convalidated validated
            FROM pg_constraint c JOIN pg_class r ON r.oid=c.conrelid JOIN pg_namespace n ON n.oid=r.relnamespace
            WHERE n.nspname='core_document') c;""")

    def original_indexes(db):
        return json_sql(db, "SELECT coalesce(jsonb_agg(to_jsonb(i) ORDER BY i.tablename,i.indexname),'[]'::jsonb) FROM (SELECT tablename,indexname,indexdef FROM pg_indexes WHERE schemaname='core_document') i;")

    def snapshot(db):
        columns = shape(db)
        return {'columns': columns, 'values': values(db, columns), 'history': history(db), 'schema': schema(db),
                'constraints': original_constraints(db), 'indexes': original_indexes(db)}

    def save(name, value):
        (args.output / name).write_text(json.dumps(value, indent=2, sort_keys=True) + '\n')

    def workflow(db):
        # Corroborates retained owned rows, not cryptographic evidence of the fixture producer/API.
        counts = json_sql(db, """SELECT jsonb_build_object(
          'documents',(SELECT count(*) FROM core_document.documents),
          'versions',(SELECT count(*) FROM core_document.versions),
          'scans',(SELECT count(*) FROM core_document.scan_evidence),
          'events',(SELECT count(*) FROM core_document.record_events),
          'cleanJoined',(SELECT count(*) FROM core_document.versions v
            JOIN core_document.documents d ON d."Id"=v."DocumentId" AND d."TenantId"=v."TenantId" AND d."LegalEntityId"=v."LegalEntityId"
            JOIN core_document.scan_evidence s ON s."VersionId"=v."Id" AND s."TenantId"=v."TenantId" AND s."LegalEntityId"=v."LegalEntityId"
            WHERE v."ScanState"=2 AND s."Verdict"=1 AND s."Attempt"=v."ScanAttempts" AND s."Sha256"=v."Sha256"),
          'workflowEvents',(SELECT count(DISTINCT "Action") FROM core_document.record_events
            WHERE "Action" IN ('UploadRegistered','ScanStarted','ScanFinished','DownloadAuthorized')));""")
        if min(counts['documents'], counts['versions'], counts['scans'], counts['cleanJoined']) < 1 or counts['events'] < 4 or counts['workflowEvents'] != 4:
            raise RuntimeError('Require genuine retained document workflow/scan/download evidence: ' + db)
        return counts

    server = int(sql('postgres', 'SHOW server_version_num;').stdout.strip())
    if server < 170000 or server >= 180000:
        raise RuntimeError('Only actual PostgreSQL17 is supported for this manifest')
    (args.output / 'postgresql-version.txt').write_text(sql('postgres', 'SELECT version();').stdout)
    closed(args.source_db)
    closed(args.used_db)
    source = snapshot(args.source_db)
    used = snapshot(args.used_db)
    if [row['MigrationId'] for row in source['history']] != [P01, P02] or set(source['columns']) != {'documents','versions','scan_evidence','record_events'}:
        raise RuntimeError('Source must have exact used P02 history/schema without P03 tables')
    if [row['MigrationId'] for row in used['history']] != [P01, P02, P03] or set(used['columns']) != set(source['columns']) | {'use_sets','use_pins'}:
        raise RuntimeError('Used source must have exact P03 history/schema')
    source_workflow, used_workflow = workflow(args.source_db), workflow(args.used_db)
    retained = json_sql(args.used_db, """SELECT jsonb_build_object(
        'sets',(SELECT count(*) FROM core_document.use_sets),
        'pins',(SELECT count(*) FROM core_document.use_pins),
        'completeSets',(SELECT count(*) FROM core_document.use_sets s WHERE s."SelectionCount">0
          AND s."SelectionCount"=(SELECT count(*) FROM core_document.use_pins p WHERE p."UseSetId"=s."Id"
            AND p."TenantId"=s."TenantId" AND p."LegalEntityId"=s."LegalEntityId"
            AND p."OwnerModule"=s."OwnerModule" AND p."OwnerType"=s."OwnerType" AND p."OwnerId"=s."OwnerId")),
        'scannedPins',(SELECT count(*) FROM core_document.use_pins p
          JOIN core_document.versions v ON v."Id"=p."VersionId" AND v."DocumentId"=p."DocumentId" AND v."TenantId"=p."TenantId" AND v."LegalEntityId"=p."LegalEntityId"
          JOIN core_document.scan_evidence s ON s."Id"=p."ScanEvidenceId" AND s."VersionId"=v."Id" AND s."TenantId"=p."TenantId" AND s."LegalEntityId"=p."LegalEntityId"
          WHERE s."Verdict"=1 AND s."Sha256"=p."Sha256" AND v."Sha256"=p."Sha256"));""")
    if retained['sets'] < 1 or retained['pins'] < 1 or retained['completeSets'] != retained['sets'] or retained['scannedPins'] != retained['pins']:
        raise RuntimeError('Used P03 must retain complete real scanned pin sets from the opt-in owner probe')
    save('original-p02-snapshot.json', source)
    save('original-used-p03-snapshot.json', used)
    names = {case: args.prefix + '_' + case for case in ('fresh','used','unused','retained')}
    if any(len(name) > 63 or name in (args.source_db,args.used_db) for name in names.values()):
        raise RuntimeError('Clone names must be bounded and distinct from original sources')
    existing = sql('postgres', 'SELECT count(*) FROM pg_database WHERE datname IN (' + ','.join(map(literal,names.values())) + ');').stdout.strip()
    if existing != '0':
        raise RuntimeError('All clone names must be new; never reuse previous evidence')
    upgraded = {}
    for case in ('fresh','used','unused'):
        db = names[case]
        if case != 'fresh': closed(args.source_db)
        run('createdb', '-U', 'postgres', '--template=' + ('template0' if case == 'fresh' else args.source_db), db)
        if case == 'fresh':
            sql(db, scripts['baseline'])
            baseline = snapshot(db)
            if baseline['values'] != values(db, source['columns']) or baseline['history'] != source['history'] or baseline['schema'] != source['schema']:
                raise RuntimeError('Fresh P02 SQL did not restore the original schema/history shape')
            if any(json.loads(baseline['values']).values()):
                raise RuntimeError('Fresh reviewed baseline must not fabricate workflow evidence')
        sql(db, scripts['up'])
        upgraded[case] = snapshot(db)
        sql(db, scripts['up'])
        if snapshot(db) != upgraded[case]:
            raise RuntimeError('Idempotent Up twice changed schema/data/history: ' + case)
        if [row['MigrationId'] for row in upgraded[case]['history']] != [P01,P02,P03]:
            raise RuntimeError('Unexpected upgraded migration history: ' + case)
        if set(upgraded[case]['columns']) != set(source['columns']) | {'use_sets','use_pins'}:
            raise RuntimeError('Up introduced unexpected tables: ' + case)
        if any(upgraded[case]['columns'][table] != columns for table,columns in source['columns'].items()):
            raise RuntimeError('Up altered an original column/type/default/nullability: ' + case)
        if any(c not in upgraded[case]['constraints'] for c in source['constraints']) or any(i not in upgraded[case]['indexes'] for i in source['indexes']):
            raise RuntimeError('Up changed an original constraint/index: ' + case)
        if case != 'fresh' and (values(db, source['columns']) != source['values']
                or [row for row in upgraded[case]['history'] if row['MigrationId'] != P03] != source['history']):
            raise RuntimeError('Up changed original used columns/content/hash or prior history: ' + case)
        save(case + '-upgraded-snapshot.json', upgraded[case])
    if any(used[key] != upgraded['used'][key] for key in ('columns','schema','constraints','indexes')):
        raise RuntimeError('Genuine used P03 schema must exactly match reviewed upgraded P03; no relaxed/missing constraints')
    checks_query = """SELECT r.relname table_name,c.conname constraint_name,
        pg_get_constraintdef(c.oid,false) definition,pg_get_expr(c.conbin,c.conrelid,false) expression,c.convalidated validated
        FROM pg_constraint c JOIN pg_class r ON r.oid=c.conrelid JOIN pg_namespace n ON n.oid=r.relnamespace
        WHERE n.nspname='core_document' AND c.contype='c' AND c.conname IN ('ck_doc_use_set_shape','ck_doc_use_pin_shape')
        ORDER BY r.relname,c.conname"""
    checks = json_sql(names['used'], "SELECT coalesce(jsonb_agg(to_jsonb(c) ORDER BY c.table_name,c.constraint_name),'[]'::jsonb) FROM (" + checks_query + ') c;')
    if len(checks) != 2 or {row['constraint_name'] for row in checks} != {'ck_doc_use_set_shape','ck_doc_use_pin_shape'} or not all(row['validated'] for row in checks):
        raise RuntimeError('Actual PostgreSQL17 P03 CHECK manifest incomplete/unvalidated')
    save('p03-checks.json',checks)
    (args.output / 'p03-checks.tsv').write_text(run('psql','-X','-U','postgres','-d',names['used'],'-qAt','-F','\t',
        '-v','ON_ERROR_STOP=1',data=checks_query+';').stdout)
    unused = names['unused']
    sql(unused, scripts['down'])
    if snapshot(unused) != source:
        raise RuntimeError('Unused Down did not exactly preserve/restore used P02 schema/data/history')
    sql(unused, scripts['up'])
    sql(unused, scripts['up'])
    if snapshot(unused) != upgraded['unused']:
        raise RuntimeError('Unused Down/Up did not restore original upgraded snapshot')
    closed(args.used_db)
    run('createdb','-U','postgres','--template='+args.used_db,names['retained'])
    before = snapshot(names['retained'])
    if before != used:
        raise RuntimeError('Used P03 clone differs from closed original source')
    denial = sql(names['retained'],scripts['down'],allow_failure=True)
    if denial.returncode == 0 or not re.search(r'ERROR:\s+55000:\s+' + STOP + r'(?:\n|$)', denial.stderr):
        raise RuntimeError('Used P03 Down did not produce exact SQLSTATE55000 evidence veto')
    if snapshot(names['retained']) != before:
        raise RuntimeError('Denied Down changed retained schema/data/history')
    if snapshot(args.source_db) != source or snapshot(args.used_db) != used:
        raise RuntimeError('An original source changed during this rehearsal')
    save('result.json', {'status':'PASS','utc':datetime.datetime.now(datetime.timezone.utc).isoformat(),
        'sourceDatabase':args.source_db,'usedDatabase':args.used_db,'databases':names,'migration':P03,
        'sqlSha256':{name:hashlib.sha256(file.read_bytes()).hexdigest() for name,file in files.items()},
        'sourceWorkflowCounts':source_workflow,'usedWorkflowCounts':used_workflow,'retainedUses':retained,
        'limits':'Logical Core schema/data/history DDL preservation only; owner fixture provenance/logs required. Not blob backup, current-purpose authorization, lifecycle race fence, runtime grants, HTTP publication or production enablement.'})
    print('PASS private Core P03 migration rehearsal:',args.output)


if __name__ == '__main__':
    main()
