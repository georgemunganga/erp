#!/usr/bin/env python3
"""Clone-only P29 Procurement identity DDL preservation rehearsal, not runtime authority."""
import argparse
import datetime
import hashlib
import json
import os
from pathlib import Path
import re
import subprocess

P28 = '20261021065049_P28GovernedSourcingDrafts'
P29 = '20261021084158_P29SourcingPolicyIdentity'
STOP = 'SourcingPolicyIdentityRollbackWouldLoseEvidence'
IDENTITY = 'sourcing_policy_identity_evidence'


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--container', default='procurement-approval-rehearsal-postgres')
    parser.add_argument('--source-db', required=True, help='Closed actual P28 governed RFQ browser/owner database')
    parser.add_argument('--source-producer-log', type=Path, required=True, help='Retained actual RFQ producer/owner terminal log')
    parser.add_argument('--used-db', help='Optional closed actual P29 API-produced policy identity database')
    parser.add_argument('--used-producer-log', type=Path, help='Required with used-db; actual P29 API/owner terminal log')
    parser.add_argument('--prefix', required=True, help='New distinct proc_approval_http_* clone prefix')
    parser.add_argument('--migration-id', required=True, help='Actual reviewed generated P29 identity migration ID')
    parser.add_argument('--baseline-sql', type=Path, required=True, help='Reviewed zero-to-P28 SQL')
    parser.add_argument('--up-sql', type=Path, required=True, help='Reviewed idempotent SQL containing P29')
    parser.add_argument('--down-sql', type=Path, required=True, help='Reviewed transactional P29-to-P28 SQL')
    parser.add_argument('--output', type=Path, required=True)
    args = parser.parse_args()
    if args.container != 'procurement-approval-rehearsal-postgres':
        parser.error('Only the fixed private rehearsal container is supported')
    for value, limit in ((args.source_db,63),(args.used_db,63),(args.prefix,43)):
        if value is not None and (not re.fullmatch(r'proc_approval_http_[a-z0-9_]+',value) or len(value)>limit):
            parser.error('Use bounded private proc_approval_http_* database names')
    if args.used_db == args.source_db or any(name and name.startswith(args.prefix) for name in (args.source_db,args.used_db)):
        parser.error('Original sources and new clone prefix must be distinct')
    if args.migration_id != P29:
        parser.error('Require the actual reviewed P29 migration identity')
    if bool(args.used_db) != bool(args.used_producer_log):
        parser.error('Actual used database and producer log must be supplied together')
    producers = {'p28':args.source_producer_log}
    if args.used_db: producers['p29'] = args.used_producer_log
    if any(not log.is_file() or log.stat().st_size == 0 for log in producers.values()):
        parser.error('Require nonempty retained real producer logs, not synthesized identity rows')
    files = {'baseline':args.baseline_sql,'up':args.up_sql,'down':args.down_sql}
    scripts = {name:file.read_text(encoding='utf-8-sig') for name,file in files.items()}
    if P28 not in scripts['baseline'] or args.migration_id in scripts['baseline']:
        parser.error('Baseline must contain P28 without P29')
    if any(args.migration_id not in scripts[name] for name in ('up','down')) or STOP not in scripts['down']:
        parser.error('Require actual reviewed P29 scripts and exact evidence rollback stop')
    if not re.search(r'\b(?:START\s+TRANSACTION|BEGIN)\s*;',scripts['down'],re.I):
        parser.error('Down must retain its owning transaction')
    forbidden = r'DROP\s+DATABASE|pg_terminate_backend|pg_cancel_backend|session_replication_role|DISABLE\s+TRIGGER'
    if any(re.search(forbidden,script,re.I) for script in scripts.values()):
        parser.error('Database drops, forced clients and enforcement bypasses are forbidden')
    if args.output.exists(): parser.error('Never replace prior evidence')
    os.umask(0o077)
    args.output.mkdir(parents=True,mode=0o700)
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
            FROM information_schema.columns WHERE table_schema='procurement'
              AND table_name<>'__procurement_migrations' GROUP BY table_name) s;""")

    def values(db, columns):
        parts = []
        for table, fields in sorted(columns.items()):
            projection = ','.join(identifier(field['name']) for field in fields)
            parts.append(f"SELECT {literal(table)} name,coalesce(jsonb_agg(to_jsonb(r) ORDER BY to_jsonb(r)::text),'[]'::jsonb) rows FROM (SELECT {projection} FROM procurement.{identifier(table)}) r")
        return sql(db, "SELECT coalesce(jsonb_object_agg(name,rows),'{}'::jsonb) FROM (" + ' UNION ALL '.join(parts) + ') r;').stdout.strip()

    def history(db):
        return json_sql(db, 'SELECT coalesce(jsonb_agg(to_jsonb(h) ORDER BY "MigrationId"),\'[]\'::jsonb) FROM procurement.__procurement_migrations h;')

    def schema(db):
        result = run('pg_dump', '-U', 'postgres', '-d', db, '--schema-only', '--schema=procurement', '--no-owner', '--no-privileges').stdout
        return '\n'.join(line for line in result.splitlines() if not line.startswith(('\\restrict ', '\\unrestrict ')))

    def original_constraints(db):
        return json_sql(db, """SELECT coalesce(jsonb_agg(to_jsonb(c) ORDER BY c.table_name,c.name),'[]'::jsonb) FROM (
            SELECT r.relname table_name,c.conname name,pg_get_constraintdef(c.oid,false) definition,c.convalidated validated
            FROM pg_constraint c JOIN pg_class r ON r.oid=c.conrelid JOIN pg_namespace n ON n.oid=r.relnamespace
            WHERE n.nspname='procurement') c;""")

    def original_indexes(db):
        return json_sql(db, "SELECT coalesce(jsonb_agg(to_jsonb(i) ORDER BY i.tablename,i.indexname),'[]'::jsonb) FROM (SELECT tablename,indexname,indexdef FROM pg_indexes WHERE schemaname='procurement') i;")

    def snapshot(db):
        columns = shape(db)
        return {'columns': columns, 'values': values(db, columns), 'history': history(db), 'schema': schema(db),
                'constraints': original_constraints(db), 'indexes': original_indexes(db)}

    def save(name, value):
        (args.output / name).write_text(json.dumps(value, indent=2, sort_keys=True) + '\n')

    def rfq_counts(db):
        counts = json_sql(db,"""SELECT jsonb_build_object(
          'cancelled',(SELECT count(*) FROM procurement.sourcing_events WHERE execution_version=1 AND status='Cancelled'),
          'originals',(SELECT count(*) FROM procurement.sourcing_draft_evidence),
          'releasedBindings',(SELECT count(*) FROM procurement.sourcing_events e
            JOIN procurement.sourcing_draft_evidence original ON original.id=e.source_evidence_id AND original.sourcing_event_id=e.id AND original.tenant_id=e.tenant_id AND original.legal_entity_id=e.legal_entity_id
            JOIN procurement.sourcing_demand_bindings b ON b.sourcing_event_id=e.id AND b.source_evidence_id=original.id AND b.tenant_id=e.tenant_id AND b.legal_entity_id=e.legal_entity_id
            JOIN procurement.purchase_request_allocations a ON a.id=b.allocation_id AND a.tenant_id=b.tenant_id AND a.legal_entity_id=b.legal_entity_id
            WHERE e.execution_version=1 AND e.status='Cancelled' AND a.target_type='SourcingEvent' AND a.target_id=e.id AND a.status='Released' AND a.quantity=b.quantity),
          'bindings',(SELECT count(*) FROM procurement.sourcing_demand_bindings),
          'createReceipts',(SELECT count(*) FROM procurement.sourcing_command_receipts WHERE operation='Create' AND response_status_code=201),
          'cancelReceipts',(SELECT count(*) FROM procurement.sourcing_command_receipts WHERE operation='Cancel' AND response_status_code=200),
          'reservedAudits',(SELECT count(*) FROM procurement.audit_events WHERE record_type='SourcingEvent' AND action='DraftReserved'),
          'cancelAudits',(SELECT count(*) FROM procurement.audit_events WHERE record_type='SourcingEvent' AND action='DraftCancelled'));
        """)
        if min(counts.values())<1: raise RuntimeError('Require retained genuine governed RFQ create/cancel and owner evidence: '+db)
        return counts

    def identity_counts(db):
        counts = json_sql(db,"""SELECT jsonb_build_object(
          'identities',(SELECT count(*) FROM procurement.sourcing_policy_identity_evidence),
          'created',(SELECT count(*) FROM procurement.sourcing_policy_identity_evidence WHERE stage='Created'),
          'submitted',(SELECT count(*) FROM procurement.sourcing_policy_identity_evidence WHERE stage='Submitted'),
          'published',(SELECT count(*) FROM procurement.sourcing_policy_identity_evidence WHERE stage='Published'),
          'completePublished',(SELECT count(*) FROM procurement.sourcing_policy_identity_evidence pub
            JOIN procurement.policy_versions p ON p.id=pub.policy_version_id AND p.tenant_id=pub.tenant_id AND p.legal_entity_id=pub.legal_entity_id AND p.policy_version_number=pub.policy_version_number
            JOIN procurement.sourcing_policy_identity_evidence origin ON origin.id=pub.created_evidence_id AND origin.policy_version_id=pub.policy_version_id AND origin.tenant_id=pub.tenant_id AND origin.legal_entity_id=pub.legal_entity_id AND origin.stage='Created'
            JOIN procurement.sourcing_policy_identity_evidence sub ON sub.id=pub.submitted_evidence_id AND sub.policy_version_id=pub.policy_version_id AND sub.tenant_id=pub.tenant_id AND sub.legal_entity_id=pub.legal_entity_id AND sub.stage='Submitted'
            WHERE p.policy_key='SOURCING' AND p.state='Published' AND pub.stage='Published'
              AND sub.created_evidence_id=origin.id AND origin.action_at<=sub.action_at AND sub.action_at<=pub.action_at
              AND pub.actor_worker_id<>origin.actor_worker_id AND pub.actor_worker_id<>sub.actor_worker_id
              AND pub.action_at=p.published_at AND sub.action_at=p.review_requested_at
              AND pub.rules_sha256=sub.rules_sha256));""")
        if min(counts.values())<1: raise RuntimeError('Require actual API-produced complete Created/Submitted/Published identity provenance')
        return counts

    server = int(sql('postgres','SHOW server_version_num;').stdout.strip())
    if not 170000<=server<180000: raise RuntimeError('Only actual PostgreSQL17 is supported')
    (args.output/'postgresql-version.txt').write_text(sql('postgres','SELECT version();').stdout)
    closed(args.source_db)
    source = snapshot(args.source_db)
    prior = [row['MigrationId'] for row in source['history']]
    if not prior or prior[-1]!=P28 or args.migration_id in prior or IDENTITY in source['columns']:
        raise RuntimeError('Source must be closed P28 without P29 table/history')
    source_counts = rfq_counts(args.source_db)
    save('original-p28-snapshot.json',source)
    used = None
    retained_counts = None
    if args.used_db:
        closed(args.used_db)
        used = snapshot(args.used_db)
        if [row['MigrationId'] for row in used['history']] != prior+[args.migration_id] or set(used['columns'])!=set(source['columns'])|{IDENTITY}:
            raise RuntimeError('Used P29 source must contain only the reviewed additive identity schema/history')
        retained_counts = identity_counts(args.used_db)
        save('original-used-p29-snapshot.json',used)
    cases = ['fresh','used','unused']+(['retained'] if used is not None else [])
    names = {case:args.prefix+'_'+case for case in cases}
    if any(len(name)>63 or name in (args.source_db,args.used_db) for name in names.values()): raise RuntimeError('Bounded distinct clone names required')
    if sql('postgres','SELECT count(*) FROM pg_database WHERE datname IN ('+','.join(map(literal,names.values()))+');').stdout.strip()!='0':
        raise RuntimeError('All clone names must be new')
    upgraded = {}
    for case in ('fresh','used','unused'):
        db = names[case]
        if case!='fresh': closed(args.source_db)
        run('createdb','-U','postgres','--template='+('template0' if case=='fresh' else args.source_db),db)
        if case=='fresh':
            sql(db,scripts['baseline'])
            baseline = snapshot(db)
            if any(baseline[key]!=source[key] for key in ('columns','schema','history','constraints','indexes')):
                raise RuntimeError('Fresh baseline SQL differs from retained P28 schema/history')
            if any(json.loads(baseline['values']).values()): raise RuntimeError('Fresh baseline cannot fabricate operational evidence')
        sql(db,scripts['up']); upgraded[case]=snapshot(db)
        sql(db,scripts['up'])
        if snapshot(db)!=upgraded[case]: raise RuntimeError('Second Up changed schema/data/history: '+case)
        if [row['MigrationId'] for row in upgraded[case]['history']]!=prior+[args.migration_id]: raise RuntimeError('Unexpected upgraded history')
        if set(upgraded[case]['columns'])!=set(source['columns'])|{IDENTITY}: raise RuntimeError('Unexpected additive tables')
        if any(upgraded[case]['columns'][table]!=cols for table,cols in source['columns'].items()): raise RuntimeError('Original column/type/default/nullability changed')
        if any(row not in upgraded[case]['constraints'] for row in source['constraints']) or any(row not in upgraded[case]['indexes'] for row in source['indexes']):
            raise RuntimeError('Original constraint/index changed, including retained ownership overlays')
        if case!='fresh' and (values(db,source['columns'])!=source['values'] or [row for row in upgraded[case]['history'] if row['MigrationId']!=args.migration_id]!=source['history']):
            raise RuntimeError('Used P28 original data/history changed')
        save(case+'-upgraded-snapshot.json',upgraded[case])
    checks_query = """SELECT r.relname table_name,c.conname constraint_name,pg_get_constraintdef(c.oid,false) definition,
        pg_get_expr(c.conbin,c.conrelid,false) expression,c.convalidated validated
        FROM pg_constraint c JOIN pg_class r ON r.oid=c.conrelid JOIN pg_namespace n ON n.oid=r.relnamespace
        WHERE n.nspname='procurement' AND c.conname='ck_source_policy_identity' AND c.contype='c' ORDER BY r.relname,c.conname"""
    checks=json_sql(names['used'],"SELECT coalesce(jsonb_agg(to_jsonb(c) ORDER BY c.table_name,c.constraint_name),'[]'::jsonb) FROM ("+checks_query+') c;')
    if len(checks)!=1 or not checks[0]['validated']: raise RuntimeError('Actual P29 CHECK manifest missing/unvalidated')
    save('p29-checks.json',checks)
    # Retain the engine-returned partial predicates; readiness must compare exact
    # PostgreSQL expressions rather than infer casts/grouping from model strings.
    indexes = json_sql(names['used'], """SELECT coalesce(jsonb_agg(to_jsonb(x) ORDER BY x.index_name),'[]'::jsonb) FROM (
        SELECT r.relname index_name, pg_get_indexdef(i.indexrelid) definition,
          pg_get_expr(i.indpred,i.indrelid,false) predicate,
          i.indisvalid valid, i.indisunique unique_index, i.indimmediate immediate
        FROM pg_index i JOIN pg_class r ON r.oid=i.indexrelid
        WHERE i.indrelid='procurement.sourcing_policy_identity_evidence'::regclass
          AND r.relname IN ('ux_source_policy_identity_created','ux_source_policy_identity_published')) x;""")
    if len(indexes)!=2 or any(not row['valid'] or not row['unique_index'] or not row['immediate'] or not row['predicate'] for row in indexes):
        raise RuntimeError('Actual P29 partial unique index manifest missing/invalid')
    save('p29-partial-indexes.json', indexes)
    (args.output/'p29-checks.tsv').write_text(run('psql','-X','-U','postgres','-d',names['used'],'-qAt','-F','\t','-v','ON_ERROR_STOP=1',data=checks_query+';').stdout)
    sql(names['unused'],scripts['down'])
    if snapshot(names['unused'])!=source: raise RuntimeError('Unused Down did not exactly restore original used P28')
    sql(names['unused'],scripts['up']); sql(names['unused'],scripts['up'])
    if snapshot(names['unused'])!=upgraded['unused']: raise RuntimeError('Unused Down/Up did not restore exact upgraded snapshot')
    if used is not None:
        if any(used[key]!=upgraded['used'][key] for key in ('columns','schema','constraints','indexes')): raise RuntimeError('Genuine P29 source schema differs from reviewed upgraded model')
        closed(args.used_db)
        run('createdb','-U','postgres','--template='+args.used_db,names['retained'])
        before = snapshot(names['retained'])
        if before!=used: raise RuntimeError('Retained clone differs from closed original source')
        denial=sql(names['retained'],scripts['down'],allow_failure=True)
        if denial.returncode==0 or not re.search(r'ERROR:\s+55000:\s+'+STOP+r'(?:\n|$)',denial.stderr): raise RuntimeError('Used Down missing exact SQLSTATE55000 evidence veto')
        if snapshot(names['retained'])!=before: raise RuntimeError('Denied Down changed retained schema/data/history')
    if snapshot(args.source_db)!=source or used is not None and snapshot(args.used_db)!=used: raise RuntimeError('Original source changed')
    complete = used is not None
    save('result.json',{'status':'PASS' if complete else 'PARTIAL','utc':datetime.datetime.now(datetime.timezone.utc).isoformat(),
        'sourceDatabase':args.source_db,'usedDatabase':args.used_db,'databases':names,'migration':args.migration_id,
        'retainedDownVerified':complete,'sqlSha256':{name:hashlib.sha256(file.read_bytes()).hexdigest() for name,file in files.items()},
        'producerLogSha256':{name:hashlib.sha256(file.read_bytes()).hexdigest() for name,file in producers.items()},
        'sourceRfqCounts':source_counts,'retainedIdentityCounts':retained_counts,
        'limits':'Logical Procurement schema/data/history DDL proof only. Actual producer logs required; SQL shape/hash does not authenticate provenance. No synthetic identity rows, runtime identity/permissions, finite grants, API publication or production acceptance.'})
    print(('PASS' if complete else 'PARTIAL (genuine P29 retained Down not yet verified)')+' private P29 rehearsal:',args.output)


if __name__=='__main__': main()
