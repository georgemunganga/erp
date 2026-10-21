#!/usr/bin/env python3
"""Disposable P28 DDL preservation/rollback rehearsal; never an authority or API proof."""
import argparse
import datetime
import hashlib
import json
import os
from pathlib import Path
import re
import subprocess


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--container', default='procurement-approval-rehearsal-postgres')
    parser.add_argument('--source-db', required=True)
    parser.add_argument('--prefix', required=True)
    parser.add_argument('--migration-id', required=True)
    parser.add_argument('--up-sql', type=Path, required=True)
    parser.add_argument('--down-sql', type=Path, required=True)
    parser.add_argument('--output', type=Path, required=True)
    args = parser.parse_args()
    if args.container != 'procurement-approval-rehearsal-postgres':
        parser.error('Only the fixed private rehearsal container is supported')
    for value, limit in ((args.source_db, 63), (args.prefix, 46)):
        if not re.fullmatch(r'proc_approval_http_[a-z0-9_]+', value) or len(value) > limit:
            parser.error('Use bounded disposable proc_approval_http_* database names')
    if args.source_db.startswith(args.prefix) or args.migration_id != '20261021065049_P28GovernedSourcingDrafts':
        parser.error('Require a distinct new prefix and actual reviewed migration ID')
    up, down = args.up_sql.read_text(), args.down_sql.read_text()
    if any(args.migration_id not in script for script in (up, down)) or 'GovernedSourcingRollbackWouldLoseEvidence' not in down:
        parser.error('Supply reviewed scripts containing migration ID and exact evidence stop')
    if any(re.search(r'DROP\s+DATABASE|pg_terminate_backend|pg_cancel_backend', script, re.I) for script in (up, down)):
        parser.error('Database drops and forced connection operations are forbidden')
    if args.output.exists():
        parser.error('Never replace prior evidence')
    os.umask(0o077)
    args.output.mkdir(parents=True, mode=0o700)
    sequence = 0

    def run(program, *options, data=None, fail=False):
        nonlocal sequence
        sequence += 1
        result = subprocess.run(['docker', 'exec', '-i', args.container, program, *options], input=data,
                                text=True, capture_output=True)
        (args.output / f'{sequence:03}-{program}.log').write_text(result.stdout + result.stderr)
        if result.returncode and not fail:
            raise RuntimeError(f'Private operation failed; retained log {sequence:03}')
        return result

    def sql(db, statement, fail=False):
        return run('psql', '-X', '-U', 'postgres', '-d', db, '-qAt', '-v', 'ON_ERROR_STOP=1',
                   data="SET statement_timeout='60s'; SET lock_timeout='10s';\n" + statement, fail=fail)

    def literal(value):
        return "'" + value.replace("'", "''") + "'"

    def identifier(value):
        return '"' + value.replace('"', '""') + '"'

    def shape(db):
        return json.loads(sql(db, "SELECT jsonb_object_agg(table_name,cols) FROM (SELECT table_name, jsonb_agg(column_name ORDER BY ordinal_position) cols FROM information_schema.columns WHERE table_schema='procurement' AND table_name<>'__procurement_migrations' GROUP BY table_name) s").stdout)

    def values(db, columns):
        # One psql call for ALL tables: no per-table subprocess amplification.
        parts = []
        for table, fields in sorted(columns.items()):
            projection = ','.join(map(identifier, fields))
            parts.append(f"SELECT {literal(table)} AS name, coalesce(jsonb_agg(to_jsonb(r) ORDER BY to_jsonb(r)::text),'[]'::jsonb) AS rows FROM (SELECT {projection} FROM procurement.{identifier(table)}) r")
        return sql(db, 'SELECT jsonb_object_agg(name,rows) FROM (' + ' UNION ALL '.join(parts) + ') all_rows;').stdout.strip()

    def history(db):
        return json.loads(sql(db, 'SELECT coalesce(jsonb_agg(to_jsonb(h) ORDER BY "MigrationId"),\'[]\'::jsonb) FROM procurement.__procurement_migrations h').stdout)

    def history_ids(db):
        return [row['MigrationId'] for row in history(db)]

    def schema(db):
        result = run('pg_dump', '-U', 'postgres', '-d', db, '--schema-only', '--schema=procurement', '--no-owner', '--no-privileges').stdout
        return '\n'.join(row for row in result.splitlines() if not row.startswith(('\\restrict ', '\\unrestrict ')))

    def snapshot(db):
        return values(db, shape(db)), history(db), schema(db)

    original_shape = shape(args.source_db)
    original = snapshot(args.source_db)
    original_ids = [row['MigrationId'] for row in original[1]]
    if ('20261020190554_P27FinanceCheckerIdentity' not in original_ids
            or args.migration_id in original_ids
            or 'checker_identity_schema_version' not in original_shape.get('budget_check_snapshots', [])
            or 'checked_by_worker_id' not in original_shape.get('budget_check_snapshots', [])
            or 'checker_identity_sha256' not in original_shape.get('budget_check_snapshots', [])
            or 'finance_identity_schema_version' not in original_shape.get('purchase_request_approval_submissions', [])
            or 'execution_version' in original_shape.get('sourcing_events', [])
            or any(name in original_shape for name in ('sourcing_draft_evidence', 'sourcing_demand_bindings', 'sourcing_command_receipts'))):
        raise RuntimeError('Require retained used P27 identity schema before any P28 additions')
    # The actual legacy producer serializes Detail with PascalCase inner properties;
    # ProcurementDraftAudit wraps its snapshots with lowercase before/after names.
    legacy_event = sql(args.source_db, """
        SELECT to_jsonb(e) FROM procurement.sourcing_events e
        WHERE e.status='Cancelled' AND coalesce(e.cancellation_reason,'')<>''
          AND EXISTS (SELECT 1 FROM procurement.audit_events a
            WHERE a.tenant_id=e.tenant_id AND a.legal_entity_id=e.legal_entity_id
              AND a.record_id=e.id AND a.record_type='SourcingEvent' AND a.action='DraftCancelled'
              AND jsonb_typeof(a.changed_fields_json::jsonb->'before')='object'
              AND jsonb_typeof(a.changed_fields_json::jsonb->'after')='object'
              AND a.changed_fields_json::jsonb->'before'->>'Id'=e.id::text
              AND a.changed_fields_json::jsonb->'after'->>'Id'=e.id::text
              AND a.changed_fields_json::jsonb->'before'->>'Status'='Draft'
              AND a.changed_fields_json::jsonb->'after'->>'Status'='Cancelled'
              AND a.changed_fields_json::text !~ '"(SourceEvidenceId|OriginalDraftFingerprint|sourceEvidenceId|originalDraftFingerprint)"')
        ORDER BY e.id LIMIT 1;
    """).stdout.strip()
    if not legacy_event:
        raise RuntimeError('Require actual pre-P28 legacy cancellation and matching before/after audit from parent HTTP fixture')
    event = json.loads(legacy_event)
    for table in ('budget_check_snapshots', 'purchase_request_approval_submissions', 'outbox_events'):
        if sql(args.source_db, f'SELECT EXISTS(SELECT 1 FROM procurement.{identifier(table)});').stdout.strip() != 't':
            raise RuntimeError('Required actual used-P27 evidence missing: ' + table)
    names = {name: args.prefix + '_' + name for name in
             ('fresh', 'used', 'unused', 'partial', 'evidence', 'binding', 'receipt', 'reserve_audit', 'cancel_audit', 'outbox')}
    if any(len(name) > 63 or name == args.source_db for name in names.values()):
        raise RuntimeError('Clone names must be bounded and distinct from retained source')
    existing = sql('postgres', 'SELECT count(*) FROM pg_database WHERE datname IN (' + ','.join(map(literal, names.values())) + ');').stdout.strip()
    if existing != '0':
        raise RuntimeError('All clone names must be new; never reuse or replace prior evidence')
    upgraded = {}
    for case, db in names.items():
        run('createdb', '-U', 'postgres', '--template=' + ('template0' if case == 'fresh' else args.source_db), db)
        sql(db, up)
        upgraded[case] = snapshot(db)
        sql(db, up)
        if snapshot(db) != upgraded[case] or history_ids(db).count(args.migration_id) != 1:
            raise RuntimeError('Up twice changed rows/schema/history: ' + case)
        if case != 'fresh' and (values(db, original_shape) != original[0] or history_ids(db) != sorted(original_ids + [args.migration_id])
                               or [row for row in history(db) if row['MigrationId'] != args.migration_id] != original[1]):
            raise RuntimeError('Upgrade changed original columns, sealed JSON/hash bytes or history: ' + case)
    # Freeze the actual PostgreSQL17 CHECK spelling returned by the owning upgraded database.
    # Both representations are retained verbatim for later readiness constants, never inferred from source SQL.
    checks_select = """SELECT r.relname AS table_name, c.conname AS constraint_name,
        pg_get_constraintdef(c.oid,false) AS definition,
        pg_get_expr(c.conbin,c.conrelid,false) AS expression, c.convalidated AS validated
        FROM pg_constraint c JOIN pg_class r ON r.oid=c.conrelid
        JOIN pg_namespace n ON n.oid=r.relnamespace
        WHERE n.nspname='procurement' AND c.contype='c'
          AND c.conname IN ('ck_sourcing_owned_draft','ck_rfq_original_envelope','ck_rfq_binding_value','ck_rfq_receipt_identity')
        ORDER BY r.relname,c.conname"""
    actual_checks = json.loads(sql(names['used'], 'SELECT coalesce(jsonb_agg(to_jsonb(c) ORDER BY c.table_name,c.constraint_name),\'[]\'::jsonb) FROM (' + checks_select + ') c;').stdout)
    if ({row['constraint_name'] for row in actual_checks} != {'ck_sourcing_owned_draft','ck_rfq_original_envelope','ck_rfq_binding_value','ck_rfq_receipt_identity'}
            or len(actual_checks) != 4 or not all(row['validated'] for row in actual_checks)):
        raise RuntimeError('Actual upgraded P28 CHECK manifest incomplete or unvalidated')
    (args.output / 'p28-checks.json').write_text(json.dumps(actual_checks, indent=2)+'\n')
    raw_checks = run('psql', '-X', '-U', 'postgres', '-d', names['used'], '-qAt', '-F', '\t',
                     '-v', 'ON_ERROR_STOP=1', data=checks_select+';').stdout
    (args.output / 'p28-checks.tsv').write_text(raw_checks)
    (args.output / 'postgresql-version.txt').write_text(sql(names['used'], 'SELECT version();').stdout)
    unused = names['unused']
    sql(unused, down)
    if snapshot(unused) != original:
        raise RuntimeError('Unused Down did not restore full P27 schema/data/history including genuine legacy cancellation')
    sql(unused, up)
    sql(unused, up)
    if snapshot(unused) != upgraded['unused']:
        raise RuntimeError('Unused Down/Up did not restore exact upgraded schema/data/history')

    # These are owner-only MALFORMED / recovery DDL negatives, not business evidence.
    # Only each isolated new table's child FKs/CHECKs may be removed for a fixture.
    # PK/unique/NOT NULL stay; no original table/history or source is deleted/rewritten.
    base = {'id': '11111111-1111-4111-8111-111111111111', 'tenant_id': event['tenant_id'],
            'legal_entity_id': event['legal_entity_id'], 'created_at': '2026-10-21T00:00:00+00:00',
            'created_by': 'SYNTHETIC-P28-ROLLBACK-NEGATIVE', 'version': 1, 'sourcing_event_id': event['id']}
    identity = '22222222-2222-4222-8222-222222222222'
    common_hash = 'a' * 64
    evidence = dict(base, schema_version=1, maker_subject_id='synthetic-only', maker_worker_id=identity,
                    create_command_key=identity, original_draft_fingerprint=common_hash,
                    payload_json='SYNTHETIC-NOT-VALID-EVIDENCE', payload_sha256=common_hash)
    receipt = dict(base, operation='Create', command_key=identity, actor_principal_id='synthetic-only',
                   actor_worker_id=identity, response_schema_version=1, response_status_code=201,
                   result_version=1, command_fingerprint=common_hash, response_json='SYNTHETIC-NOT-VALID-RECEIPT',
                   response_sha256=common_hash, completed_at=base['created_at'])
    binding = dict(base, source_evidence_id=identity, sourcing_line_id=identity, purchase_request_id=identity,
                   purchase_request_line_id=identity, request_version=1, submission_id=identity, submitted_version=1,
                   core_approval_case_id=identity, approval_outcome_id=identity, core_decision_id=identity,
                   request_sha=common_hash, policy_decision_sha=common_hash, policy_rules_sha=common_hash,
                   finance_snapshot_id=identity, finance_sha=common_hash, method_id=identity, method_revision=1,
                   method_code='SYNTHETIC', minimum_quote_count=0, maker_worker_id=identity, requester_worker_id=identity,
                   allocation_id=identity, target_type='SourcingEvent', quantity='0.0001', unit_estimate='0.0001',
                   estimated_value='0.00000001', unit_code='each')

    def seed_owned(table, row):
        # Names are fixed literals above, not caller-controlled identifiers.
        relation = 'procurement.' + identifier(table)
        relax = f"""DO $fixture$ DECLARE c record; BEGIN
            FOR c IN SELECT conname FROM pg_constraint WHERE conrelid={literal(relation)}::regclass AND contype IN ('f','c')
            LOOP EXECUTE format('ALTER TABLE %s DROP CONSTRAINT %I',{literal(relation)},c.conname); END LOOP;
            END $fixture$;"""
        insert = f'INSERT INTO {relation} SELECT (jsonb_populate_record(NULL::{relation},{literal(json.dumps(row))}::jsonb)).*;'
        return relax + '\n' + insert

    def audit(action, governed):
        changed = ({'executionVersion': 1, 'sourceEvidenceId': identity, 'originalDraftFingerprint': common_hash,
                    'commandKey': identity, 'changes': {'reason': 'Synthetic rollback negative'}} if governed else {'syntheticRollbackNegative': True})
        patch = {'id': identity, 'record_type': 'SourcingEvent', 'record_id': event['id'], 'action': action,
                 'changed_fields_json': changed, 'actor_subject_id': 'SYNTHETIC-P28-ROLLBACK-NEGATIVE'}
        return f"INSERT INTO procurement.audit_events SELECT (jsonb_populate_record(NULL::procurement.audit_events,to_jsonb(a)||{literal(json.dumps(patch))}::jsonb)).* FROM procurement.audit_events a ORDER BY id LIMIT 1;"

    fixtures = {
        'partial': 'ALTER TABLE procurement.sourcing_events DROP CONSTRAINT ck_sourcing_owned_draft; '
                   + f'UPDATE procurement.sourcing_events SET original_draft_fingerprint=repeat(\'b\',64) WHERE id={literal(event["id"])}::uuid;',
        'evidence': seed_owned('sourcing_draft_evidence', evidence),
        'binding': seed_owned('sourcing_demand_bindings', binding),
        'receipt': seed_owned('sourcing_command_receipts', receipt),
        'reserve_audit': audit('DraftReserved', False),
        'cancel_audit': audit('DraftCancelled', True),
        'outbox': f"INSERT INTO procurement.outbox_events SELECT (jsonb_populate_record(NULL::procurement.outbox_events,to_jsonb(o)||jsonb_build_object('id',{literal(identity)},'event_type','sourcing.draft_cancelled.v1','aggregate_type','SourcingEvent','aggregate_id',{literal(event['id'])},'idempotency_key','SYNTHETIC-P28-ROLLBACK-NEGATIVE','payload_json',jsonb_build_object('eventId',{literal(event['id'])},'commandKey',{literal(identity)},'internallyPublished',false,'supplierDeliveryConfirmed',false,'financeReservationChanged',false,'syntheticRollbackNegative',true)))).* FROM procurement.outbox_events o ORDER BY id LIMIT 1;",
    }
    for case, fixture in fixtures.items():
        db = names[case]
        sql(db, fixture)
        before = snapshot(db)
        if before == upgraded[case]:
            raise RuntimeError('Synthetic negative fixture produced no effects: ' + case)
        denial = sql(db, down, fail=True)
        if denial.returncode == 0 or 'GovernedSourcingRollbackWouldLoseEvidence' not in denial.stderr or snapshot(db) != before:
            raise RuntimeError('Expected exact locked evidence veto without any further effects: ' + case)
    if snapshot(args.source_db) != original:
        raise RuntimeError('Retained original source changed')
    (args.output / 'result.json').write_text(json.dumps({'status': 'PASS', 'utc': datetime.datetime.now(datetime.timezone.utc).isoformat(),
        'sourceDatabase': args.source_db, 'databases': names, 'migration': args.migration_id,
        'upSha256': hashlib.sha256(up.encode()).hexdigest(), 'downSha256': hashlib.sha256(down.encode()).hexdigest(),
        'limits': 'DDL preservation only; owner-only malformed/recovery negatives are not valid RFQ authority, immutable business evidence, Finance or HTTP/runtime proof'}, indent=2)+'\n')
    print('PASS private P28 migration rehearsal:', args.output)


if __name__ == '__main__':
    main()
