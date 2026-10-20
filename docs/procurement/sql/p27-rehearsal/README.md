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

## Authored actual restricted HTTP checker scenarios (unrun)

`ApprovalHttpPostgresProbe/FinanceCheckerIdentityVerification.cs` runs after the genuine schema-readiness preflight in every non-serve mode. Use a NEW guarded private database with the committed P27 migration and separate finite runtime / bootstrap identities through existing `PROCUREMENT_APPROVAL_HTTP_PROBE_CONNECTION` and `PROCUREMENT_APPROVAL_HTTP_PROBE_BOOTSTRAP_CONNECTION`; invoke the compiled `ApprovalHttpPostgresProbe` with `--orders` (or its original default approval mode). Parent owns compilation, private role provisioning and runtime execution. Do not reuse a previously consumed fixture database or print connection strings. No new flags, service bindings, auth mechanism, readiness override, public listener, Finance provider or production behavior is added.

The probe uses actual request Create/Submit and Budget HTTP capture, reloads the persisted v1 tuple/digest and PostgreSQL microsecond timestamp, and compares repeat original-reference response bytes with zero row/audit/outbox changes. Bootstrap-only fixture edits rebind the existing Finance subject to a separate real active worker and remove its specific Finance action grant; exact original replay must respectively fail409/403 without effects, and grant revocation must also deny a fresh reference403. `finally` restores only synthetic account/role configuration; historical evidence and the additional worker remain retained. A separately inserted owner-only synthetic unbound legacy0 reference must fail409 without metadata backfill. Its original financial tuple is copied solely for a compatibility negative; it is not a new validated Finance capture or external reservation proof. The original genuine v1 Finance encoding must remain byte-identical after every negative.

### Authored raw CHECK companion (unrun)

Immediately after the genuine new bound1 capture/reload, eight bootstrap-only transactions attempt NULL/zero worker, NULL/uppercase/short/nonhex digest, schema0 with retained nonempty metadata, and unsupported schema2 on that actual row. Each must fail PostgreSQL `23514` with **exactly** `ck_budget_checker_identity`, then roll back; the full Finance/request/submission/audit/outbox footprint must be identical. This catches required-NULL SQL UNKNOWN holes independently of application factories and schema manifest matching. No CHECK is disabled and no fake positive bound evidence is inserted.

After the selected non-serve mode finishes its real workflows, an existing genuine schema2 submission (if any) receives a rollback-only unsupported Finance discriminator2 attempt, requiring `23514` / `ck_pr_finance_identity_version`. Modes that genuinely create no submission report **NOT EXERCISED**, never PASS or invented approval evidence. Parent should include default approval/`--orders` execution to cover this gate. Raw SQL negatives use only bootstrap; actual preceding capture/replay/denial requests still use the finite runtime connection.
