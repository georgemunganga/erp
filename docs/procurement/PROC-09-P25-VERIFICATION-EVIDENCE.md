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
