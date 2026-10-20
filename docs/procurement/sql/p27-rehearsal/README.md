# P27 retained Finance identity migration rehearsal

**Status:** source-only helper; not executed against PostgreSQL. Parent must first review and seal the actual P27 migration, generated transactional Up/Down scripts, exact CHECK/readiness manifest, and this helper. No runtime enablement follows from migration rehearsal.

Run `rehearse.py --help` for required arguments. `--up-sql` is reviewed idempotent zero-to-P27 SQL; `--down-sql` is reviewed P27-to-P26 SQL with the first locked `FinanceCheckerIdentityRollbackWouldLoseEvidence` veto. Supply the actual migration ID and a new private output directory/new `proc_approval_http_*` prefix. Container is fixed to `procurement-approval-rehearsal-postgres`; retained actual P26 source defaults to `proc_approval_http_p26_matching_20261020_184252`. No network DSN is accepted. No database is dropped, overwritten or disconnected; failures and all fixture clones are retained.

The original source is read-only throughout. Before execution, ensure PostgreSQL permits cloning it without terminating live sessions. A fresh database and seven template clones exercise Up twice, exact original values including text JSON/hash bytes and migration history, and an unused Down/Up that must reproduce the entire prior Procurement schema (including defaults, constraint/index names) despite used P26 receiving/invoice evidence. Each snapshot uses one batched JSON query across all business tables, rather than one `psql` per table. Full history and schema are recorded separately. This is not a byte-identical physical database dump or a proof of unrelated schemas.

Six owner-only synthetic **rollback-negative** clones exercise separately:

- nondefault snapshot metadata;
- partial metadata after deliberate owner-only removal of its CHECK (malformed legacy/tampering recovery scenario);
- nonzero submission discriminator;
- generic submission property-name-array audit without any new metadata rows;
- actual `PurchaseRequest` / `BudgetChecked` object-key audit format without any new metadata rows;
- hypothetical retained carrier fields in an existing outbox type, with an explicitly synthetic payload/idempotency key. There is **no new Finance checker event producer** and this fixture does not invent one.

These fixtures never create valid Finance authority: synthetic hashes are deliberately not canonical business digests; the submission discriminator fixture does not re-authorize or rewrite its old Finance JSON. They test the destructive-migration stop only. Existing source evidence is never deleted to make a rollback pass. Denials must preserve every clone's data, schema and complete history. The original database must match its initial snapshot after all work.

## Parent-owned additional gates

1. Independently verify generated SQL first-lock ordering, all four additive fields/two exact CHECKs, no backfill/coalescing/old evidence rewrites, and no invented event predicate.
2. Exercise the actual production readiness manifest, missing/wrong/unvalidated CHECKs, column/default/FK shape and finite runtime privileges using the existing actual HTTP probe. Owner migration rehearsal is not a least-privilege readiness test.
3. Run actual new capture/replay/current-checker rebind/alias/legacy-denial and historical old-source/receiving restoration regressions, then existing UI failure states.
4. Check deployed source/migration compatibility and the existing release hold. This helper neither changes configuration nor enables any workflow.
