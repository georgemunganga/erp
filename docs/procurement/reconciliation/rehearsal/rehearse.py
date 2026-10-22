#!/usr/bin/env python3
"""Private canonical P20 -> P21 DDL rehearsal. Synthetic negatives are NOT authority/API proof."""
import argparse
import datetime
from decimal import Decimal
import hashlib
import json
import os
from pathlib import Path
import re
import subprocess

CONTAINER = 'procurement-approval-rehearsal-postgres'
SOURCE = 'procurement_crud_http_20261022_050209'
MIGRATION = '20261022052824_P21CanonicalGovernedProcurementFoundation'
P20 = '20261022043718_P20InvoiceDraftMaintenance'
# Frozen from released 844190e P20 snapshot, not newly created by the P21 delta.
BASELINE_CHECKS = {'ck_catalog_entry_values', 'ck_catalog_item_buying_values',
                   'ck_pr_allocation_nonnegative', 'ck_pr_line_catalog_ref', 'ck_pr_line_nonnegative'}


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--container', default=CONTAINER)
    parser.add_argument('--source-db', default=SOURCE)
    parser.add_argument('--prefix', required=True)
    parser.add_argument('--up-sql', type=Path, required=True)
    parser.add_argument('--p19-sql', type=Path, required=True)
    parser.add_argument('--p20-sql', type=Path, required=True)
    parser.add_argument('--down-sql', type=Path, required=True)
    parser.add_argument('--source-root', type=Path, required=True)
    parser.add_argument('--output', type=Path, required=True)
    args = parser.parse_args()
    if args.container != CONTAINER or args.source_db != SOURCE:
        parser.error('Only the reviewed private container and exact retained P20 source are permitted')
    if not re.fullmatch(r'proc_approval_http_[a-z0-9_]{1,21}', args.prefix):
        parser.error('Use a new bounded proc_approval_http_* prefix')
    scripts = {key: getattr(args, key + '_sql').read_text() for key in ('up', 'p19', 'p20', 'down')}
    if MIGRATION not in scripts['up'] or MIGRATION not in scripts['down'] or P20 not in scripts['p20']:
        parser.error('Require exact reviewed canonical lineage scripts')
    if MIGRATION in scripts['p19'] or P20 in scripts['p19']:
        parser.error('P19 script must end before released P20')
    if 'CanonicalGovernedRollbackWouldLoseEvidence' not in scripts['down'] or 'COALESCE(tax_variance' in scripts['down']:
        parser.error('Require reviewed protected Down, never unknown-to-zero conversion')
    if args.output.exists():
        parser.error('Never overwrite evidence from an earlier attempt')
    os.umask(0o077)
    args.output.mkdir(mode=0o700, parents=True)
    inspection = subprocess.run(['docker', 'inspect', args.container], text=True, capture_output=True)
    (args.output / '000-docker-inspect.json').write_text(inspection.stdout + inspection.stderr)
    if inspection.returncode:
        raise RuntimeError('Private container inspection failed before any DDL')
    details = json.loads(inspection.stdout)
    if len(details) != 1 or not details[0]['State']['Running'] or details[0]['HostConfig']['NetworkMode'] != 'none':
        raise RuntimeError('Require the running reviewed private container with NetworkMode none before any DDL')
    sequence = 0

    def command(program, *options, data=None, fail=False):
        nonlocal sequence
        sequence += 1
        result = subprocess.run(['docker', 'exec', '-i', args.container, program, *options],
                                input=data, text=True, capture_output=True)
        (args.output / f'{sequence:03}-{program}.log').write_text(result.stdout + result.stderr)
        if result.returncode and not fail:
            raise RuntimeError(f'Private operation failed; retained log {sequence:03}')
        return result

    def sql(db, text, fail=False):
        return command('psql', '-X', '-U', 'postgres', '-d', db, '-qAt', '-v', 'ON_ERROR_STOP=1', data=text, fail=fail)

    def literal(value):
        return "'" + value.replace("'", "''") + "'"

    def ident(value):
        return '"' + value.replace('"', '""') + '"'

    def decode(value):
        return json.loads(value, parse_float=Decimal)

    def shape(db):
        return decode(sql(db, "SELECT jsonb_object_agg(table_name,cols) FROM (SELECT table_name,jsonb_agg(column_name ORDER BY ordinal_position) cols FROM information_schema.columns WHERE table_schema='procurement' AND table_name<>'__procurement_migrations' GROUP BY table_name)s;").stdout)

    def values(db, columns):
        # One process per database, not one per table. Decimal comparisons are exact;
        # widening 18,4 -> 38,8 may change display scale, never the numeric value.
        parts = []
        for table, fields in sorted(columns.items()):
            order = 'r.id' if 'id' in fields else 'to_jsonb(r)::text'
            parts.append(f"SELECT {literal(table)} name,coalesce(jsonb_agg(to_jsonb(r) ORDER BY {order}),'[]'::jsonb) rows FROM (SELECT {','.join(map(ident, fields))} FROM procurement.{ident(table)})r")
        return decode(sql(db, 'SELECT jsonb_object_agg(name,rows) FROM (' + ' UNION ALL '.join(parts) + ')s;').stdout)

    def history(db):
        return sql(db, 'SELECT "MigrationId" FROM procurement.__procurement_migrations ORDER BY "MigrationId";').stdout.splitlines()

    def schema(db):
        dump = command('pg_dump', '-U', 'postgres', '-d', db, '--schema-only', '--schema=procurement', '--no-owner', '--no-privileges').stdout
        return '\n'.join(line for line in dump.splitlines() if not line.startswith(('\\restrict ', '\\unrestrict ')))

    def snapshot(db):
        return values(db, shape(db)), history(db), schema(db)

    def clone(name, source='template0'):
        db = args.prefix + '_' + name
        if len(db) > 63 or db == args.source_db:
            raise RuntimeError('Unsafe disposable clone name')
        command('createdb', '-U', 'postgres', '--template=' + source, db)
        return db

    source_shape, source_before = shape(args.source_db), snapshot(args.source_db)
    if source_before[1][-1] != P20 or 'mapping_revision' in source_shape['supplier_invoices']:
        raise RuntimeError('Retained source is not actual canonical P20; never adopt a held history')
    for table in ('suppliers', 'supplier_invoices', 'supplier_invoice_lines', 'audit_events', 'purchase_orders', 'purchase_request_lines'):
        if not source_before[0].get(table):
            raise RuntimeError('Required actual CRUD baseline missing: ' + table)

    names = {}
    fresh = names['fresh'] = clone('fresh')
    sql(fresh, scripts['up'])
    fresh_after = snapshot(fresh)
    sql(fresh, scripts['up'])
    if snapshot(fresh) != fresh_after or history(fresh).count(MIGRATION) != 1:
        raise RuntimeError('Fresh idempotent Up did not preserve schema/data/history')
    sql(fresh, scripts['down'])
    if history(fresh)[-1] != P20:
        raise RuntimeError('Unused Down did not stop at actual released P20')
    sql(fresh, scripts['up'])
    if snapshot(fresh) != fresh_after:
        raise RuntimeError('Fresh unused Down/Up changed canonical schema/data/history')

    # Generated P19 with explicitly synthetic, UNVERIFIED original invoice evidence.
    # A real source invoice/supplier supplies only an old-schema template, not authority.
    templates = sql(args.source_db, "SELECT jsonb_build_object('invoice',to_jsonb(i),'supplier',(SELECT to_jsonb(s) FROM procurement.suppliers s WHERE s.id=i.supplier_id),'line',(SELECT to_jsonb(l) FROM procurement.supplier_invoice_lines l WHERE l.supplier_invoice_id=i.id ORDER BY l.id LIMIT 1)) FROM procurement.supplier_invoices i WHERE EXISTS(SELECT 1 FROM procurement.supplier_invoice_lines l WHERE l.supplier_invoice_id=i.id) ORDER BY i.id LIMIT 1;").stdout.strip()
    legacy = names['p19_synthetic'] = clone('p19_synthetic')
    sql(legacy, scripts['p19'])
    seed = (Path(__file__).parent / 'synthetic-p19.sql').read_text().replace('__SOURCE_TEMPLATE_JSON__', literal(templates))
    sql(legacy, seed)
    p19_shape, p19_before = shape(legacy), snapshot(legacy)
    sql(legacy, scripts['p20'])
    p20_legacy_before = snapshot(legacy)
    sql(legacy, scripts['up'])
    after_legacy = snapshot(legacy)
    sql(legacy, scripts['up'])
    if values(legacy, p19_shape) != p19_before[0] or snapshot(legacy) != after_legacy:
        raise RuntimeError('Synthetic P19 -> P20 -> P21 changed original values or idempotency')
    sql(legacy, scripts['down'])
    if snapshot(legacy) != p20_legacy_before:
        raise RuntimeError('Unused Down did not exactly restore synthetic populated P20 schema/data/history')
    sql(legacy, scripts['up'])
    if snapshot(legacy) != after_legacy:
        raise RuntimeError('Synthetic P19 Down/Up changed canonical rows/schema/history')

    actual = names['actual_p20'] = clone('actual_p20', args.source_db)
    sql(actual, scripts['up'])
    after_actual = snapshot(actual)
    sql(actual, scripts['up'])
    if values(actual, source_shape) != source_before[0] or snapshot(actual) != after_actual:
        raise RuntimeError('Actual populated P20 rows/JSON strings/history changed on Up twice')
    if history(actual) != source_before[1] + [MIGRATION]:
        raise RuntimeError('Actual P20 history was replaced or held migration IDs adopted')
    # Existing receiving configuration intentionally makes Down unavailable. Never delete it.
    baseline_receiving = sql(actual, "SELECT EXISTS(SELECT 1 FROM procurement.policy_versions WHERE upper(btrim(policy_key))='RECEIVING');").stdout.strip() == 't'
    if baseline_receiving:
        denial = sql(actual, scripts['down'], fail=True)
        if denial.returncode == 0 or 'P25RollbackRequiresEvidencePreservingRecovery' not in denial.stderr or snapshot(actual) != after_actual:
            raise RuntimeError('Expected protected RECEIVING baseline was not preserved')
    else:
        sql(actual, scripts['down'])
        if snapshot(actual) != source_before:
            raise RuntimeError('Unused Down did not exactly restore actual released P20 schema/data/history')
        sql(actual, scripts['up'])
        if snapshot(actual) != after_actual:
            raise RuntimeError('Actual P20 Down/Up changed original or canonical values')

    # Structural proof, NOT a finite runtime-role or operational workflow proof.
    infra = args.source_root / 'backend/procurement-api/src/Mightyfin.Erp.Procurement.Infrastructure'
    manifests = []
    for name in ('ReceivingOwnershipConstraintSql', 'InvoiceMatchingOwnershipConstraintSql', 'SourcingDraftOwnershipConstraintSql'):
        text = (infra / 'Data' / (name + '.cs')).read_text()
        query = re.search(r'public const string ManifestQuery = """(.*?)""";', text, re.S)[1]
        if sql(actual, query).stdout.strip() != 't':
            raise RuntimeError('Exact ownership overlay manifest failed: ' + name)
        manifests.append(query)
    checks = sql(actual, "SELECT jsonb_agg(jsonb_build_object('name',c.conname,'table',c.conrelid::regclass::text,'validated',c.convalidated,'expression',pg_get_expr(c.conbin,c.conrelid)) ORDER BY c.conname) FROM pg_constraint c WHERE c.connamespace='procurement'::regnamespace AND c.contype='c';").stdout
    expected_checks = set(re.findall(r'(?:table.CheckConstraint|migrationBuilder.AddCheckConstraint)\(\s*(?:name: )?"([^"]+)"', (infra / 'Migrations' / (MIGRATION + '.cs')).read_text()))
    if len(expected_checks) != 34 or expected_checks & BASELINE_CHECKS:
        raise RuntimeError('New canonical CHECK inventory must be34 disjoint from the5 preserved baseline checks')
    expected_checks |= BASELINE_CHECKS
    actual_checks = decode(checks)
    if len(expected_checks) != 39 or {c['name'] for c in actual_checks} != expected_checks or not all(c['validated'] for c in actual_checks):
        raise RuntimeError('Exact named/validated 39-CHECK inventory mismatch')
    (args.output / 'actual-check-manifest.json').write_text(checks)
    # Rollback-only missing-overlay tamper: every real manifest must detect its missing FK.
    install = (args.source_root / 'docs/procurement/reconciliation/canonical-forward-ownership-overlay.sql').read_text()
    for table, fk in re.findall(r'ALTER TABLE procurement\.([a-z_]+)\s+ADD CONSTRAINT ([a-z_]+)', install):
        query = next(q for q in manifests if "'" + fk + "'" in q)
        before = snapshot(actual)
        answer = sql(actual, f'BEGIN; ALTER TABLE procurement.{ident(table)} DROP CONSTRAINT {ident(fk)};\n{query}\nROLLBACK;').stdout.splitlines()
        if answer != ['f'] or snapshot(actual) != before:
            raise RuntimeError('Missing exact overlay not detected or rollback changed rows: ' + fk)

    # Owner-only disposable synthetic negatives. Never insert valid approval, acceptance,
    # checker/identity or Finance evidence, and never delete history to make Down succeed.
    fixture_text = (Path(__file__).parent / 'synthetic-negatives.json').read_text()
    for case, fixture in json.loads(fixture_text).items():
        base = actual if fixture['base'] == 'actual' else legacy
        db = names[case] = clone(case, base)
        sql(db, fixture['sql'])
        before = snapshot(db)
        denial = sql(db, scripts['down'], fail=True)
        if denial.returncode == 0 or fixture['expected'] not in denial.stderr or snapshot(db) != before:
            raise RuntimeError('Expected exact Down veto without side effects: ' + case)
    if snapshot(args.source_db) != source_before:
        raise RuntimeError('Original retained source changed')
    result = {'status': 'PASS', 'utc': datetime.datetime.now(datetime.timezone.utc).isoformat(), 'databases': names,
              'sourceDatabase': args.source_db, 'sourceUntouched': True, 'migration': MIGRATION,
              'baselineReceivingProtected': baseline_receiving, 'operations': sequence,
              'scripts': {k: hashlib.sha256(v.encode()).hexdigest() for k, v in scripts.items()},
              'limits': 'DDL and exact preserved-value/manifest proof only. P19 rows and Down negatives explicitly synthetic. No HTTP, finite runtime grants, active policy, identity, receipt or Finance authority inferred.'}
    (args.output / 'result.json').write_text(json.dumps(result, indent=2) + '\n')
    print('PASS private canonical migration rehearsal:', args.output)


if __name__ == '__main__':
    main()
