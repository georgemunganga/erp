# P25 receiving verification evidence

## Integrated source gate — 20 October 2026

Candidate source: `f9c15b9`, including receiving merge `ee4d229` and the
`IReadOnlySet<Guid>` construction correction `bfc12e1`.

- Procurement domain tests: **451 passed, 0 failed, 0 skipped**.
- Shared-host Procurement tests: **99 passed, 0 failed, 0 skipped**.
- PostgreSQL verification tool: build passed with 0 warnings and 0 errors.
- Retained local log: `artifacts/order-execution/p25-source-gate.log`.
- First compile failure is retained separately as
  `p25-source-gate-first-set-compile-failure.log`.
- Existing nullable and xUnit analyzer warnings remain; these results do not
  establish a warning-free full ERP build.

These counts precede the separate service notes and pending source-revision
corrections (`6b574d7`, `b43e9b0`). Their new regressions require verification
after integration. The actual PostgreSQL receiving probe, migration generation,
upgrade/recovery rehearsals and receiving browser checks are still pending.

No P25 migration or receiving activation has been applied to production. A source
test pass is not evidence that the full Procurement module is live or complete.

## Notes regressions and generated migration

- Integrated fixes: `d071d1c` and `a983391` (original reviewed commits above).
- Focused persistence tests after those fixes: **7 passed, 0 failed, 0 skipped**.
  Log: `artifacts/order-execution/p25-generation.log`.
- Generated migration: `20261020133820_P25PositiveReceivingExecution`.
- Post-generation correction `35e3780` adds explicit required non-null sequence
  checks. The migration, designer and model snapshot carry the same predicates.
- An ordinary transaction-bound SQL guard is the first Down operation. It rejects
  configured or used receiving extensions and precision-losing amount narrowing.

### Distinct generated-source review

A separate agent reviewed the actual generated Up, Down, designer and snapshot:
exactly two new tables, 44 additive existing fields, nullable legacy pins and
sentinel defaults, tenant/entity composite references, Restrict deletes, nullable
2000-character service notes and accepted-amount widening were checked. Up drops
only two redundant prefix indexes before rebuilding suitable composite indexes;
it does not drop existing records, columns or tables or backfill evidence.

The reviewer found no blocking source defect. Designer and snapshot model bodies
agree; no P26 invoice matching records are included. Reviewed migration SHA-256:
`0a361aa9db2ee0f8f1c9e1554411b1c9e05c11ee543b0036e5eb839424a75ad1`.

At that source-review checkpoint, final rebuild/model-drift checks, generated SQL,
PostgreSQL upgrade/recovery tests, HTTP probes and browser tests remained required.

## Executed migration/recovery gates

- Final persistence regressions: **8 passed**, including required non-null sequence
  metadata. EF reports no model changes after the last migration.
- Full idempotent Up and unused-only Down SQL generated successfully. Final shared
  host/probe build passed with 0 errors and 13 existing nullable warnings.
- Fresh private PostgreSQL installation applied twice: **22 migration rows**, two
  new receiving tables, no duplicate migration.
- Entirely unused extension Down/Up passed (history 21 then 22).
- Restored P24 copy populated with eleven synthetic legacy records: Up twice,
  protected unused Down and re-Up twice passed. All original fields across the
  **71 original tables / 11 rows** were preserved after each step. Separate SQL
  assertions verified all eleven old row shapes and unverified legacy defaults;
  no new authority, state, acceptance effects or notes backfill was fabricated.
- Configured receiving policy and each generic receiving audit record type
  correctly denied Down through the evidence-preservation guard. Each rejected
  transaction retained all 22 migration rows/two new tables and no fixture rows.
- Legacy service amount `1.00000001` and old-range overflow correctly denied
  narrowing. Exact old maximum and zero permitted an otherwise unused Down inside
  rollback-only transactions. Original amount 277.7775 and P25 schema survived all
  four probes unchanged.

Protected logs/checkpoints are under
`/home/amizo/erp-backups/procurement-receiving-postgres-20261020/20261020_133953/`.
The first actual receiving HTTP run refused an incorrectly named private runtime
role during preflight, before application bootstrap. That failure is retained;
the unused role was renamed to the required guarded prefix and the probe restarted.
No product guard was relaxed. Actual receiving workflow/constraint HTTP results
and receiving browser integration are still pending at this checkpoint.


## Canonical input and retained source-unit corrections

The next actual workflow attempt reached PO submission but correctly refused the
fixture's display label `Goods` where the configured policy requires canonical
`GOODS`. This exposed the same request-input boundary used by editable catalog
and non-catalog request lines. Commit `168627b` normalizes new editable ASCII
policy codes before mutation; it does not rewrite historical evidence or relax
policy matching. Fixture `884a740` keeps explicit receiving-kind versus policy-code
contracts. Issue: [32](https://github.com/georgemunganga/module-connect/issues/32).

A fresh canonical-code attempt reached a delivered PO and goods draft, then
refused arrival because the retained PR source unit `each` differed in casing
from its PO owner's normalized `EACH`. Commit `fd1c7d0` applies the PO owner's
existing case-insensitive source-unit contract to that one lineage comparison.
The sealed PO-to-pin comparison remains exact; different units and whitespace
are not aliases. The rejected transaction left no receiving state, acceptance
effect or successful arrival command receipt.

After these business corrections, `p25-code-regression.log` records **465 domain
tests and 111 shared-host Procurement API tests passed**. The verification tool
also built with zero warnings/errors. Commit `ff6d929` subsequently changes only
the real receiving probe to submit display input through the request API and
assert canonical codes before PO conversion; its tool-only rebuild also passed.
These are separate evidence checkpoints, not additive test totals.

The first role-prefix, purchase-type and unit-case failures remain retained under
the protected receiving evidence directories `20261020_133953` and
`20261020_135521`. The subsequent complete receiving probe uses a fresh guarded
database under `20261020_141959`; it reached delivery recording but returned `409 ReceivingEvidenceNeedsReview`.
The first failure is retained for diagnosis; no actual receiving pass is claimed.
Receiving UI source
`2546465` restores existing presentation controls but has not yet passed its
unit, TypeScript, staged build or browser gates. Production remains unchanged.
