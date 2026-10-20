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


## Stable binding correction and subsequent verification — 20 October 2026

The retained `20261020_141959` failure exposed mutable receiving binding fields
being mapped as EF principal alternate keys. The stable-key correction preserves
exact physical/service ownership with migration-owned composite constraints while
using stable entity IDs for tracked relationships. Correction migration
`20261020144755_P25ReceivingStableEvidenceBindings` is additive to the original
P25 migration; it does not rewrite receiving records. Its distinct review is in
`PROC-09-P25-STABLE-BINDINGS-DISTINCT-REVIEW.md`.

`artifacts/order-execution/p25-stable-regression.log` records the newer complete
checkpoint: **480 domain tests passed; 111 shared-host Procurement API tests
passed; the PostgreSQL verification tool built with zero warnings/errors; EF
reported no pending model changes**. These replace the earlier checkpoint for
this source baseline and are not additional test totals. Existing shared-host
build warnings remain outside the verification tool's warning-free result.

Actual PostgreSQL migration rehearsals under protected evidence
`20261020_143802/migration-rehearsal` passed fresh idempotent application,
unused correction/full receiving reversal and reapplication, restored legacy
field preservation, and denial of reversal after receiving evidence exists.
The restored fixture preserved 73 tables / 11 records; the retained used fixture
preserved 73 tables / 138 records. The original P24 baseline was also checked at
the full receiving rollback boundary. Production was not migrated.

The subsequent actual HTTP/PostgreSQL workflow under `20261020_143802` passed
physical arrival, inspection, goods acceptance, two partial service certifications,
conservation/replay checks, purpose-specific access checks, and exact binding
constraint negatives. It then stopped at multi-site policy fixture setup: the
fixture requested a two-site policy while selecting one site. That rejected
request is correct server behaviour, not a completed receiving gate.

Tool-only commit `ebe07f6` grants both sites explicitly to the synthetic policy
maker and independent reviewer, then configures the policy in company context.
It retains a single-site rejection with no saved policy and grants no extra site
to receiving workers. A distinct source reviewer found no business-guard
relaxation. Its initial rebuild caught a test-only incorrect branch-assignment namespace;
`ec52410` corrects that reference to the existing shared application type. The
first failed build log is retained. The corrected tool rebuild passed with zero
warnings/errors (`p25-cross-site-probe-build-corrected.log`); a fresh full
receiving retry is running under protected `20261020_151428` evidence.

Receiving UI now has a separate executed adapter checkpoint documented in
`PROC-RECEIVING-UI-PARITY-AUDIT.md`: 12 focused unit tests passed; TypeScript and
staged build passed after retained corrections; all 16 distinct mocked browser
scenarios passed across recorded checkpoints, including a test-only accessible
name locator correction. This is not one uninterrupted 16-case run and is not
actual backend browser evidence. The actual API receiving browser gate remains
pending. No deployment or full-module completion is claimed.


### Correction to the cross-site fixture checkpoint

The `20261020_151428` run again passed physical/inspection/service evidence
checks but correctly returned 201 for policy **draft capture**. The fixture's
422 assertion was at the wrong boundary: actual receiving site validation runs
on submit-for-review and publication. For confined users, omitting a shell
location also defaults to an assigned branch; it does not establish company-wide
scope. The earlier fixture interpretation above is superseded by this finding.

`9554b4e` changes only the verification tool: it captures a service-only policy
for site B, proves submission from site A fails without changing draft/version,
then submits and independently publishes in explicitly assigned site B. Workers
performing receiving remain confined to A. After the retained-scope negative,
the normal site-A policy is restored before subsequent goods checks. A separate
fresh full probe is required. Product configuration guards are unchanged.
