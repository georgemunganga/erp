#!/usr/bin/env python3
"""Private disposable P26 migration rehearsal; run only after source/SQL review.
No services, SDK, migrations generation, production connection or source-db writes.
"""
import argparse
import datetime
import decimal
import hashlib
import json
import os
from pathlib import Path
import re
import subprocess


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--container", default="procurement-approval-rehearsal-postgres")
    parser.add_argument("--source-db", default="proc_approval_http_receiving_review_20261020_152211")
    parser.add_argument("--prefix", required=True, help="New proc_approval_http_* database prefix; never reused")
    parser.add_argument("--migration-id", required=True)
    parser.add_argument("--up-sql", type=Path, required=True, help="Reviewed idempotent zero-to-P26 SQL")
    parser.add_argument("--down-sql", type=Path, required=True, help="Reviewed transactional P26-to-P25-correction SQL")
    parser.add_argument("--output", type=Path, required=True, help="New private evidence directory")
    parser.add_argument("--repo", type=Path, default=Path(__file__).resolve().parents[4])
    args = parser.parse_args()
    if args.container != "procurement-approval-rehearsal-postgres":
        parser.error("Only the explicitly named private rehearsal container is supported")
    for name, maximum in ((args.source_db, 63), (args.prefix, 50)):
        if not re.fullmatch(r"proc_approval_http_[a-z0-9_]+", name) or len(name) > maximum:
            parser.error("Use bounded disposable proc_approval_http_* database names")
    if not re.fullmatch(r"[0-9]{14}_[A-Za-z0-9_]+", args.migration_id):
        parser.error("Supply the actual reviewed generated migration ID")
    if args.source_db.startswith(args.prefix):
        parser.error("Output prefix must differ from the retained source database")
    up, down = args.up_sql.read_text(), args.down_sql.read_text()
    if args.migration_id not in up or args.migration_id not in down:
        parser.error("Both generated scripts must reference the exact reviewed P26 migration ID")
    if "P26RollbackRequiresEvidencePreservingRecovery" not in down or "P26RollbackWouldLoseTaxPrecision" not in down:
        parser.error("Down SQL lacks the reviewed used-evidence/precision stops")
    if args.output.exists():
        parser.error("Evidence output must be new; previous failures must remain intact")
    args.output.mkdir(parents=True, mode=0o700)
    os.umask(0o077)
    sequence = 0

    def run(program, *options, data=None, allowed_failure=False):
        nonlocal sequence
        sequence += 1
        result = subprocess.run(["docker", "exec", "-i", args.container, program, *options],
                                input=data, text=True, capture_output=True)
        (args.output / f"{sequence:03}-{program}.log").write_text(result.stdout + result.stderr)
        if result.returncode and not allowed_failure:
            raise RuntimeError(f"{program} failed; retained private log {sequence:03}")
        return result

    def sql(db, statement, allowed_failure=False):
        # All SQL executes inside this fixed private container, never a supplied network connection.
        return run("psql", "-X", "-U", "postgres", "-d", db, "-qAt", "-v", "ON_ERROR_STOP=1",
                   data=statement, allowed_failure=allowed_failure)

    def quote(name):
        return '"' + name.replace('"', '""') + '"'

    def columns(db):
        rows = sql(db, "SELECT table_name,column_name FROM information_schema.columns "
                   "WHERE table_schema='procurement' AND table_name<>'__procurement_migrations' "
                   "ORDER BY table_name,ordinal_position").stdout.splitlines()
        result = {}
        for row in rows:
            table, column = row.split('|')
            result.setdefault(table, []).append(column)
        return result

    def data_snapshot(db, shape=None):
        result = {}
        for table, fields in (shape or columns(db)).items():
            projection = ','.join(map(quote, fields))
            value = sql(db, f"SELECT coalesce(jsonb_agg(to_jsonb(r) ORDER BY r.id),'[]'::jsonb) "
                        f"FROM (SELECT {projection} FROM procurement.{quote(table)}) r").stdout
            result[table] = json.loads(value, parse_float=decimal.Decimal)
        return result

    def history(db):
        return sql(db, "SELECT migration_id FROM procurement.__procurement_migrations ORDER BY migration_id").stdout.splitlines()

    def schema(db):
        dump = run("pg_dump", "-U", "postgres", "-d", db, "--schema-only", "--schema=procurement",
                   "--no-owner", "--no-privileges").stdout
        # pg_dump18 emits a fresh unrelated client restriction nonce each time.
        return '\n'.join(line for line in dump.splitlines() if not line.startswith(('\\restrict ', '\\unrestrict ')))

    def manifest(db):
        base = args.repo / "backend/procurement-api/src/Mightyfin.Erp.Procurement.Infrastructure/Data"
        for filename in ("ReceivingOwnershipConstraintSql.cs", "InvoiceMatchingOwnershipConstraintSql.cs"):
            content = (base / filename).read_text()
            marker = 'public const string ManifestQuery = """'
            query = content.split(marker, 1)[1].split('""";', 1)[0]
            if sql(db, query).stdout.strip() != 't':
                raise RuntimeError(f"Exact ownership manifest failed: {filename}")

    # Read only the retained actual P25 source. Any P26 history/rows means wrong starting point.
    old_shape = columns(args.source_db)
    old_values, old_history = data_snapshot(args.source_db, old_shape), history(args.source_db)
    if args.migration_id in old_history or "invoice_match_mapping_versions" in old_shape:
        raise RuntimeError("Source must be the retained used-P25, pre-P26 database")
    if not old_values.get('receiving_acceptance_effects'):
        raise RuntimeError("Source lacks actual used P25 acceptance evidence")
    names = {name: args.prefix + '_' + name for name in ('fresh', 'populated', 'used', 'precision')}
    for name, db in names.items():
        # createdb fails on any existing database; never overwrite, drop or terminate sessions.
        run("createdb", "-U", "postgres", "--template=" + ("template0" if name == 'fresh' else args.source_db), db)
        sql(db, up); sql(db, up); manifest(db)
        if history(db).count(args.migration_id) != 1:
            raise RuntimeError("Idempotent Up did not retain exactly one P26 history entry")
        if name != 'fresh':
            if data_snapshot(db, old_shape) != old_values or not set(old_history).issubset(history(db)):
                raise RuntimeError("Populated Up changed retained P25 values/history")

    # Used P25 receiving must not itself block an otherwise UNUSED P26 rollback.
    populated = names['populated']
    sql(populated, down)
    if history(populated) != old_history or data_snapshot(populated, old_shape) != old_values:
        raise RuntimeError("Unused Down lost retained P25 values/history")
    sql(populated, up); sql(populated, up); manifest(populated)
    if data_snapshot(populated, old_shape) != old_values:
        raise RuntimeError("Unused Down/Up changed retained P25 values")

    for case, filename, expected in (
        ('used', 'used-exposure.sql', 'P26RollbackRequiresEvidencePreservingRecovery'),
        ('precision', 'legacy-tax-precision.sql', 'P26RollbackWouldLoseTaxPrecision'),
    ):
        db = names[case]
        sql(db, (Path(__file__).parent / filename).read_text())
        before = (data_snapshot(db), history(db), schema(db))
        denied = sql(db, down, allowed_failure=True)
        if denied.returncode == 0 or expected not in denied.stderr:
            raise RuntimeError("Expected exact protected Down stop: " + case)
        if (data_snapshot(db), history(db), schema(db)) != before:
            raise RuntimeError("Denied Down changed data/schema/history: " + case)
        manifest(db)
    if data_snapshot(args.source_db, old_shape) != old_values or history(args.source_db) != old_history:
        raise RuntimeError("Retained original P25 source changed")
    (args.output / 'result.json').write_text(json.dumps({
        'status': 'PASS', 'utc': datetime.datetime.now(datetime.timezone.utc).isoformat(), 'databases': names,
        'sourceDatabase': args.source_db, 'migration': args.migration_id,
        'upSha256': hashlib.sha256(up.encode()).hexdigest(), 'downSha256': hashlib.sha256(down.encode()).hexdigest(),
        'scope': 'fresh/populated Up twice; exact P25 values; unused DownUp; two protected Down denials; exact manifests',
        'limits': 'Not HTTP authorization, row-level FK tamper, runtime least privilege or business-owner verification',
    }, indent=2) + '\n')
    print("PASS private migration rehearsal; retained evidence:", args.output)


if __name__ == '__main__':
    main()
