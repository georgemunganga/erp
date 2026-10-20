# P25 migration review and evidence-preserving recovery

**Status:** Source review/checklist and candidate guard SQL only; no EF generation, build,
SQL/database execution, migration or recovery rehearsal performed.
**Source contract:** receiving checkpoint2d8b35f, 28-section P02 adapter and P25 implementation plan.
**Owner:** Engineering migration/release owner; root coordinates the actual generated migration and tests.

## 1. Allowed change inventory

The centrally generated migration must have exactly two new receiving tables:
`receiving_source_states` and `receiving_acceptance_effects`. Extend the existing
`goods_receipts`, `goods_receipt_lines`, `receipt_inspections` and `service_acceptances`;
add the scoped alternate keys/foreign keys required on existing issued/submission/delivery,
PO-line/header and policy records. Do not map the pure physical/inspection projections
as duplicate tables, alter Inventory/Assets/Finance ledgers, or introduce Core identity FKs.
The P26 invoice comparison migration is a separate dependency, not silently included here.

Review the final generated migration/snapshot against `ProcurementReceivingMapping.cs`
and `ReceivingPersistenceRecords.cs`; this document is not a replacement for that diff.

### Legacy compatibility

- Preserve every existing row, ID, quantity, value, date, source link, status, reason, audit
  and document reference. No delete, zeroing, relabelling or fabricated evidence backfill.
- New execution-version defaults are0; legacy missing binding sentinels are Guid.Empty;
  nullable `IssuedVersionId`, `SubmissionId`, state/worker/policy/effect pins remain nullable.
  Do not let an EF alternate key retrofit a legacy nullable pin to NOT NULL.
- Guid.Empty means missing/unverified, never a valid issued/state/physical identity. New
  version-one final paths require exact nonempty binding and the full retained source proof.
- Existing legacy records with nonzero accepted/delivered quantities remain intact. They
  block new acceptance completeness where relevant; they are not converted to new effects.
- Unknown legacy historical branch is not guessed from mutable current PO ownership:
  its read path is organisation-wide with no selected branch only. This is code/read policy,
  not a database backfill. Unknown execution versions fail closed.
- Add nullable `ServiceAcceptance.Notes` with max2000 separately from the existing text
  Reason field (cancellation input remains bounded to1000). Never copy or guess legacy Reason into Notes. Cancellation changes Reason
  and preserves Notes. Any nonnull new service Notes blocks destructive Down, even on v0.
- `ServiceAcceptance.AcceptedAmount` widens from numeric(18,4) to numeric(38,8). Existing
  values must survive exactly. This is storage range, not a promise that CLR decimal can
  represent arbitrary38-digit values; exact arithmetic/transport remain required.

### Relational and evidence checks

- Verify tenant/entity/PO/issued/submission/line identities are joined in the same composite
  FK, not separately valid GUIDs. Source state is unique per issued version and exact PO line.
- Verify goods header/line, physical observation/inspection, typed goods/service effects and
  policy/source-state relations use the reviewed matching scoped keys and Restrict deletes.
- Confirm positive-only check, exactly-one typed effect target, source/effect key uniqueness,
  final inspection uniqueness and nonnull/nonempty v1 evidence requirements.
- Quantity columns use18,4; net money uses38,8. Physical received quantity must not be
  constrained to PO quantity. Acceptance controls are separate and cumulative.
- Per-table sequence uniqueness does not replace cross-kind source locking/combined-stream
  verification. Migration review must not claim those application guards are database triggers.
- No cascade deleting completed receipt/effect evidence or arbitrary default worker/policy/hash.

## 2. Down contract: only an entirely unused extension may be removed

An application code rollback normally leaves the additive schema in place with receiving
flags disabled. Destructive database Down is permitted only when the P25 extension has
never been configured or used and every prior value can be restored without precision loss.
New drafts and cancelled drafts count as use. An unused extension may coexist with arbitrary
unchanged legacy0 data; those legacy rows must survive Down/Up byte/value-equivalent.

[Candidate guard SQL](sql/P25-down-guard.candidate.sql) documents the exact source-level
predicate. It is **not executable migration approval**. Reconcile it with the actual generated
column/table names and diff, then place it first in the generated Down transaction, before
any DROP or type narrowing. Lock affected source/evidence/config/command/audit/outbox tables
with a bounded timeout. Never suppress the transaction, catch the guard and continue, or
clear fields/rows merely to make Down pass.

The guard rejects:

- Any row in either new receiving table, including otherwise empty source-state records.
- Any nondefault/non-null P25 extension field on existing tables, regardless of declared
  execution version. A row claiming legacy0 does not authorize dropping its newer data.
- Any RECEIVING policy configuration, including drafts, returned/rejected/superseded versions;
  existing governed JSON is retained in `policy_versions` and must not be erased to downgrade.
- Any receiving/service command receipt, controlled audit action or receiving outbox event,
  even if an unauthorized purge made newer business tables appear empty.
- Existing service amounts with more than four nonzero fractional digits or out-of-range
  numeric(18,4) integer part. Narrowing must never silently round an owner-written legacy value.

Future P26/other dependent migrations must be reverted only through their own protected
recovery contract before P25 can be considered. Do not remove unrelated or subsequently
owned alternate keys. Shared Core role configuration, document bytes/metadata and existing
Procurement records are not P25-owned cleanup targets and must never be erased by Down.

## 3. Required actual rehearsal matrix (not yet run)

| Scenario | Expected result / evidence |
|---|---|
| Fresh P24 → P25 Up twice via idempotent script | Exactly two tables, intended extensions/keys/checks; second application changes no data |
| Populated legacy0 goods/service/inspection/acceptance before Up | IDs/values/statuses unchanged; all new pins missing; no synthetic state/effects |
| Unused P25 Down → Up with legacy records | Succeeds; old rows and amount precision preserved; missing-proof defaults unchanged |
| One new goods draft or cancelled goods draft | Down fails atomically before any table/column/history entry removed |
| One service draft/certificate/inspection/acceptance | Down fails atomically; all referenced evidence remains |
| RECEIVING draft policy only; then published/superseded variants | Each independently blocks Down; no config deletion |
| Only receiving command/audit/outbox retained | Each independently blocks Down; no evidence cleanup workaround |
| Extension data on an executionVersion0 or unknown-version row | Down blocks; the version discriminator cannot conceal data loss |
| Legacy accepted amount0.00001 or boundary100000000000000 | Down fails precision/range check; within-range exact4dp succeeds if otherwise unused |
| Scoped FK corruption/raw owner attempts | Cross-tenant/entity/header/line/issued/state/typed-effect references rejected; no fake authority inferred |
| Concurrent writer during rollback/lock timeout | Bounded abort and complete rollback, no partial DDL or silent lost writes |
| Evidence-preserving dump/restore of used database | Restore into private isolated environment; verify original counters/effects/audit/config, no production overwrite |

A test harness may insert deliberate corruption only with the isolated bootstrap owner;
the actual runtime role remains least privilege. Check migration history and exact retained
values after each failed Down, not only the expected exception text. Preserve first failures.

## 4. Recovery once configured or used

1. Disable receiving execution/UI capabilities and pause writers/consumers using the normal
   controlled deployment procedure. Do not delete records or issue invented compensations.
2. Capture a recoverable database backup and protected document/evidence references; record
   the failing migration/release, affected scope and current source/receipt/effect watermarks.
3. Prefer a reviewed forward fix retaining all evidence and compatible code. A code rollback
   may leave additive schema/configuration intact; old code must not mutate v1 final records.
4. If a restore is required, restore a proven backup to a protected separate environment,
   reconcile all post-backup commands, receipts, policies and effects with business owners,
   and approve the recovery plan. A pre-P25 backup alone is not permission to discard later use.
5. Record the incident, decision, validation and release evidence. Re-enable only after the
   domain, API, restricted Postgres, browser and recovery gates pass.

No production credentials, backups or evidence payloads belong in Git. This plan creates
no production migration or operational recovery result.
