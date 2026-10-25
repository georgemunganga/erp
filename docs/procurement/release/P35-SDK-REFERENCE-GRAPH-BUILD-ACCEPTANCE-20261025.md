# P35 SDK reference graph build acceptance

**Status:** runbook correction; SDK06 full rebuild pending. This note records build evidence only. It does not activate P35 or replace private PostgreSQL and HTTP acceptance.

## Failure record

- Unified SDK04 failed compilation with CS1061 in `ProcurementRfqCommercialStoreReadiness` and CS0136 in `ProcurementPrincipalCommercialSchemaInspection`. Its log and partial cache remain failed evidence. Source `d81ad414` corrected those source errors before the next run.
- Unified SDK05 compiled, then its 69 focused Host cases yielded 63 passed and six failed. The failures reported missing `procurement-contract-submit` authorization policy. The current source catalog contains that action, but the reused `Hrm.Application.dll` did not contain its UTF-16 string. This was a stale compiled reference graph, not a passing authorization test. The SDK05 log, TRX and cache lineage remain failed evidence.
- SDK06 at source `f5ed339d91b24f7c374551552ec84fa87a8dad85` is pending. No SDK06 test count or publication is accepted by this note.

## Acceptance procedure for cross-worktree .NET builds

1. Freeze the physical source tree and record the exact source commit, tree hash, SDK image, package cache, and build artifact lineage. A reused artifact directory is a **cache continuation**; record its before/after inventory and prior failed or accepted runs. Never label it a fresh build.
2. When source may have changed outside the current worktree, force a full reference-graph `Rebuild` of the relevant application, infrastructure, host and test projects, or clean only the owned disposable build cache and compile the full graph. File modification times and an incremental Host-only build do not establish which source produced referenced DLLs.
3. Before tests, verify the current built assemblies against critical source contracts. For this candidate, both `Hrm.Application.dll` and the Host test output copy must contain the three P35 action keys: `procurement-contract-submit`, `procurement-contract-review-read`, and `procurement-contract-review`. Pin the assembly SHA-256 values. This check supplements, rather than replaces, compilation and authorization tests.
4. Accept only an exit-zero run with complete source-before/source-after equality, current assembly inventory, test TRXs and exact pass/fail/skip counts. Keep failed attempts immutable. Publish from the accepted output graph only, then hash the entire publication; a tool-only overlay must separately prove that every product file is unchanged.

Private SDK04 and SDK05 evidence is retained under `/home/amizo/erp-backups/procurement-p35-unified-source-validation-20261024_run04` and `..._run05`. The reviewed pending SDK06 preparation is `/home/amizo/erp-backups/procurement-p35-unified-source-validation-20261025_run06`. These local paths are evidence locations, not deployment targets.
