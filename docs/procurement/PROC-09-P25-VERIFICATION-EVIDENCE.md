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

Final rebuild/model-drift checks, generated SQL, real PostgreSQL constraint and
upgrade/recovery tests, receiving HTTP probes and browser tests remain required.
