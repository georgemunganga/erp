#!/usr/bin/env python3
"""Disposable P27 DDL preservation/rollback rehearsal; never an authority or API proof."""
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
    parser.add_argument('--source-db', default='proc_approval_http_p26_matching_20261020_184252')
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
    if args.source_db.startswith(args.prefix) or not re.fullmatch(r'\d{14}_[A-Za-z0-9_]+', args.migration_id):
        parser.error('Require a distinct new prefix and actual reviewed migration ID')
    up, down = args.up_sql.read_text(), args.down_sql.read_text()
    if any(args.migration_id not in script for script in (up, down)) or 'FinanceCheckerIdentityRollbackWouldLoseEvidence' not in down:
        parser.error('Supply reviewed scripts containing migration ID and exact evidence stop')
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
        return run('psql', '-X', '-U', 'postgres', '-d', db, '-qAt', '-v', 'ON_ERROR_STOP=1', data=statement, fail=fail)

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
        return sql(db, 'SELECT "MigrationId" FROM procurement.__procurement_migrations ORDER BY "MigrationId"').stdout.splitlines()

    def schema(db):
        result = run('pg_dump', '-U', 'postgres', '-d', db, '--schema-only', '--schema=procurement', '--no-owner', '--no-privileges').stdout
        return '\n'.join(row for row in result.splitlines() if not row.startswith(('\\restrict ', '\\unrestrict ')))

    def snapshot(db):
        return values(db, shape(db)), history(db), schema(db)

    original_shape = shape(args.source_db)
    if 'checker_identity_schema_version' in original_shape.get('budget_check_snapshots', []):
        raise RuntimeError('Source must be actual used P26 before P27, never a consumed P27 fixture')
    original = snapshot(args.source_db)
    for table in ('budget_check_snapshots', 'purchase_request_approval_submissions', 'audit_events', 'outbox_events', 'invoice_match_results'):
        if sql(args.source_db, f'SELECT EXISTS(SELECT 1 FROM procurement.{identifier(table)});').stdout.strip() != 't':
            raise RuntimeError('Required actual P26 fixture history missing: ' + table)
    names = {name: args.prefix + '_' + name for name in ('fresh', 'legacy', 'used', 'partial', 'submission', 'audit', 'capture', 'outbox')}
    legacy_before = None
    for case, db in names.items():
        run('createdb', '-U', 'postgres', '--template=' + ('template0' if case == 'fresh' else args.source_db), db)
        if case == 'legacy':
            legacy_before = snapshot(db)
        sql(db, up)
        after_once = snapshot(db)
        sql(db, up)
        if snapshot(db) != after_once or history(db).count(args.migration_id) != 1:
            raise RuntimeError('Up twice changed rows/schema/history: ' + case)
        if case != 'fresh' and (values(db, original_shape) != original[0] or history(db) != sorted(original[1] + [args.migration_id])):
            raise RuntimeError('Upgrade changed old values, sealed JSON bytes or migration history: ' + case)
    legacy = names['legacy']
    sql(legacy, down)
    if snapshot(legacy) != legacy_before:
        raise RuntimeError('Unused Down did not exactly restore full used-P26 schema/data/history')
    sql(legacy, up)
    sql(legacy, up)
    if values(legacy, original_shape) != original[0]:
        raise RuntimeError('Unused Down/Up changed original P26 bytes')

    # Owner-only synthetic rollback NEGATIVES. These do not establish valid binding,
    # verified Finance, publication, delivery or a business-authorized producer.
    fixtures = {
        'used': "UPDATE procurement.budget_check_snapshots SET checker_identity_schema_version=1, checked_by_worker_id='11111111-1111-4111-8111-111111111111', checker_identity_sha256=repeat('a',64) WHERE id=(SELECT id FROM procurement.budget_check_snapshots ORDER BY id LIMIT 1);",
        'partial': "ALTER TABLE procurement.budget_check_snapshots DROP CONSTRAINT ck_budget_checker_identity; UPDATE procurement.budget_check_snapshots SET checker_identity_sha256=repeat('b',64) WHERE id=(SELECT id FROM procurement.budget_check_snapshots ORDER BY id LIMIT 1);",
        'submission': "UPDATE procurement.purchase_request_approval_submissions SET finance_identity_schema_version=1 WHERE id=(SELECT id FROM procurement.purchase_request_approval_submissions WHERE snapshot_schema_version=2 ORDER BY id LIMIT 1);",
        'audit': "INSERT INTO procurement.audit_events SELECT (jsonb_populate_record(NULL::procurement.audit_events,to_jsonb(a)||jsonb_build_object('id','11111111-1111-4111-8111-111111111112','record_type','PurchaseRequestApprovalSubmission','action','create','changed_fields_json',jsonb_build_array('FinanceIdentitySchemaVersion')))).* FROM procurement.audit_events a ORDER BY id LIMIT 1;",
        'capture': "INSERT INTO procurement.audit_events SELECT (jsonb_populate_record(NULL::procurement.audit_events,to_jsonb(a)||jsonb_build_object('id','11111111-1111-4111-8111-111111111113','record_type','PurchaseRequest','action','BudgetChecked','changed_fields_json',jsonb_build_object('CheckerIdentitySchemaVersion',1,'CheckedByWorkerId','11111111-1111-4111-8111-111111111111','CheckerIdentitySha256',repeat('a',64))))).* FROM procurement.audit_events a ORDER BY id LIMIT 1;",
        'outbox': "INSERT INTO procurement.outbox_events SELECT (jsonb_populate_record(NULL::procurement.outbox_events,to_jsonb(o)||jsonb_build_object('id','11111111-1111-4111-8111-111111111114','idempotency_key','SYNTHETIC-P27-ROLLBACK-NEGATIVE','payload_json',jsonb_build_object('FinanceIdentitySchemaVersion',1,'syntheticRollbackNegative',true)))).* FROM procurement.outbox_events o ORDER BY id LIMIT 1;",
    }
    for case, fixture in fixtures.items():
        db = names[case]
        if case == 'submission' and sql(db, 'SELECT EXISTS(SELECT 1 FROM procurement.purchase_request_approval_submissions WHERE snapshot_schema_version=2);').stdout.strip() != 't':
            raise RuntimeError('Need actual schema2 submission for isolated discriminator negative')
        sql(db, fixture)
        before = snapshot(db)
        denial = sql(db, down, fail=True)
        if denial.returncode == 0 or 'FinanceCheckerIdentityRollbackWouldLoseEvidence' not in denial.stderr or snapshot(db) != before:
            raise RuntimeError('Expected exact evidence stop without effects: ' + case)
    if snapshot(args.source_db) != original:
        raise RuntimeError('Retained original source changed')
    (args.output / 'result.json').write_text(json.dumps({'status': 'PASS', 'utc': datetime.datetime.now(datetime.timezone.utc).isoformat(),
        'sourceDatabase': args.source_db, 'databases': names, 'migration': args.migration_id,
        'upSha256': hashlib.sha256(up.encode()).hexdigest(), 'downSha256': hashlib.sha256(down.encode()).hexdigest(),
        'limits': 'DDL preservation only; synthetic negatives are not valid checker evidence or actual HTTP/runtime authorization proof'}, indent=2)+'\n')
    print('PASS private P27 migration rehearsal:', args.output)


if __name__ == '__main__':
    main()
