# P26 invoice comparison — verification evidence

## Scope and release boundary

P26 adds explicit original-invoice mapping and comparison, scoped source selectors and read-only Finance readiness to the existing Procurement API. It reuses Core identity, branch scope and documents, and retained issued orders and receiving evidence. Missing independent original-unit review or complete Finance consumption evidence remains Blocked. This document does not claim AP posting, payment, full Procurement completion or production deployment.

## Combined source verification, 20 October 2026

Frozen source `d2ea852` ran in the network-isolated .NET 10 SDK container `procurement-p26-source-verification`, limited to one CPU and 1250 MB memory, with the existing package cache read-only.

- Offline restores passed for domain tests, shared API tests and the actual PostgreSQL probe.
- Full shared API Release build passed, with existing warnings.
- All **546 Procurement domain/model tests passed**. This includes receiving completion bindings remaining outside EF keys.
- Procurement API regression: **124 passed, 2 failed, 126 total**. Both failures were obsolete fixture setup attempting to modify a captured original invoice's branch, rejected by `OriginalInvoiceImmutable`.
- The serial script stopped at the failed API gate; it did not build the probe. No probe-build pass is inferred.

Logs and TRX remain under ignored `artifacts/p26/source-verification-first.log`, `artifacts/p26/domain` and `artifacts/p26/http` in the isolated P26 worktree. Failed evidence has been retained.

## Corrections and next verification

Reviewed original-intake exactness change `2c9f301` was integrated as `1da1925`. It validates raw JSON numeric precision, persisted 18,4 values and exact aggregate arithmetic before tracking any invoice. It distinguishes a genuine PostgreSQL invoice-number unique violation from an unconfirmed database save. No original invoice money schema was widened.

Fixture correction `226da98` creates the remote original through the real capture API with explicit selected branch, then checks the confined reader; document tests change Core reader membership rather than the original invoice. An independent source review confirmed the retained branch, duplicate, upload/download and permission assertions. Business immutability guards remain unchanged.

The corrected full Procurement API regression **passed: 140 passed, zero failed/skipped**, duration4m27s; container `procurement-p26-api-reverification` terminated0. Evidence: `artifacts/p26/api-reverification.log` and `artifacts/p26/http-corrected`. Its compiled business/test source is `226da98` (including integrated exact intake validation), not later uncompiled guards.

Subsequently source-reviewed changes were integrated: real browser fixture2765d11/9030e17; database required-evidence checksffa6887; lossless original catalog/request/plan/contract/budget input guardse570b82; malformed nested item correction792f512. These later changes do not inherit the prior140-API pass. The updated domain/model gate and central P26 migration generation are now running; updated API/probe builds follow.

Migration SQL review, private PostgreSQL rehearsal, actual HTTP comparison and actual browser verification remain pending until terminal evidence is recorded. Existing P25 actual receiving/catalog/order passes and mocked invoice UI passes are separate evidence, not substitutes. No production migration/deployment or GitHub CI success is claimed.

## Required-evidence model pass and unaccepted first migration

The next domain/model regression passed **548/548** in41s; terminal0 in `procurement-p26-reviewed-generation`. Log `artifacts/p26/reviewed-model-generation.log`; TRX under `artifacts/p26/domain-required-evidence`. This covers explicit required-evidence CHECK definitions; raw PostgreSQL denial proof remains pending.

EF generated draft `20261020163750_P26InvoiceMatchingExecution`. Independent review identified two blockers before any SQL execution:

1. A new four-column issued-order alternate key consumed a truncated existing constraint name and would cause EF to drop the retained five-column key with dependent P25 foreign keys. The new key now has explicit short name `ak_invoice_issued_order_binding`; a model regression freezes the retained and new names.
2. The actual preceding schema requires all three variance fields. P26 permits unknown NULL variances. The generated Down would replace NULL with0. The protected reverse now additionally refuses NULL variance via `P26RollbackWouldLoseUnknownVariance`; its generated default-zero reverse operations must be removed.

The unapplied first draft/designer/snapshot are preserved in ignored `artifacts/p26/unaccepted-first-generation`. Only those untracked new files were removed and the tracked snapshot restored to its preceding version before regeneration. Neither existing P25 migration was edited. Correction `b593730` was regenerated after the PostgreSQL key-name regression correction described below. The first draft was never applied.

The private rehearsal helper is versioned under `docs/procurement/sql/p26-rehearsal`; Python compilation and help checks passed. It adds original unverified legacy invoice/line/result/exception preservation and separate used-evidence, unknown-variance and precision-loss stops. No database rehearsal result is inferred from these source checks.


## Accepted generated migration and source gate

Accepted migration `20261020164745_P26InvoiceMatchingExecution` is committed in `3481d7b`. Migration source SHA256: `808a22945a5e02016b84ee712ced702b42ca511428a70c67953da96f3dcf82e5`. Its literal ownership constraints and first-operation rollback guard preserve the existing P25 key and refuse used evidence, unknown variance or tax precision loss. The three generated reverse default-zero operations were removed before acceptance.

The preceding revised domain run had548 passes and one failed provider-name assertion: the regression used the InMemory provider to inspect PostgreSQL truncation. Correction `9e60c2a` uses the actual Npgsql model without opening a connection. The targeted regression then passed; these split runs are not described as an uninterrupted549-test pass.

On source `ea7982b`, serial container `procurement-p26-generated-source-reverification` has completed:

- Actual PostgreSQL verification-tool Release build: passed, zero warnings/errors.
- EF pending-model check: no changes detected.
- Idempotent Up SQL and P26-to-P25 guarded reverse SQL generated.
- Full Procurement domain/model regression: **550 passed, zero failed/skipped**,44s.

The full Procurement API regression in this same serial run **passed:167 passed, zero failed/skipped**,5m26s. Container terminated0; TRX in `artifacts/p26/http-final-generated`. This gate covers the later input guards, required evidence and first-comparison persistence correction; the compiled source is `ea7982b` (later commits changed documentation/helper only).

Independent review of the actual generated scripts found no blocker: one atomic transaction; no retained issued-key drop; exact ownership constraints after their parent indexes; reverse locks and all three loss stops before destructive operations; no silent NULL-to-zero update. SQL SHA256: Up `1e70840e8b5c03ee07681d740ea13cc5ecb702c1311c7370d7970f3a6ff26849`; Down `15a8a6d2e47dcf72b7a44467b380074a613a1b2ff878191147414c04c80b3d77`.

The first tool build failed on a missing test-tool namespace and nullable command-key formatting; `ea7982b` corrected both. Evidence remains in `artifacts/p26/generated-source-verification-first.log`. The successful subsequent build and550-test result are in `artifacts/p26/generated-source-reverification.log`, with domain TRX in `domain-final-generated`.

Rehearsal helper correction `bedcb5f` reads quoted EF `"MigrationId"`; Python compilation passed. Private migration execution, actual restricted HTTP/PostgreSQL comparison and actual browser/independent-owner verification remain pending. No P26 production migration, qualified invoice approval, Finance posting or payment is claimed.
