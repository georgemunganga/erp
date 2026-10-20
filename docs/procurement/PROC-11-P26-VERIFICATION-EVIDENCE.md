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


## Actual private PostgreSQL migration rehearsal — PASS

Session17430 terminated0 at17:21:52UTC. Protected evidence: `/home/amizo/erp-backups/procurement-invoice-postgres-20261020/20261020_1711_migration/result.json` and1415 retained operation logs. The helper used the actual accepted generated Up/Down SQL and private PostgreSQL17; no source or production database was migrated.

- Fresh database and four separately cloned used-P25 databases upgraded twice, retaining exactly one P26 history entry and both exact P25/P26 ownership manifests.
- Synthetic legacy original invoice/line/result/exception values remained exactly preserved and unverified. Original used P25 receiving evidence remained unchanged.
- Populated but unused P26 rollback/re-upgrade passed with exact retained values, history and full P25 schema equality, including defaults and constraint names.
- Three independent rollback denials passed with unchanged data/schema/history: used P26 evidence; NULL variance that old NOT NULL schema cannot retain; tax variance whose precision cannot fit18,4.
- Original retained P25 source remained unchanged. Fresh/clone names and SQL hashes are recorded in the protected result.

This proves the migration lifecycle and representative loss stops, not API authorization, every guard branch, runtime least privilege, row-level FK tamper or external owner authenticity. The next actual HTTP/PostgreSQL matching run uses a new restricted-runtime database under protected `20261020_172220_matching`; its terminal result remains pending.


## First actual restricted HTTP/PostgreSQL matching run — retained failure

Fresh private `20261020_172220_matching`, session49719, terminated139 after the tool's unhandled failed expectation. Receiving lifecycle, named ownership/sequence denials and exact original invoice intake passed. First mapping expected200 but returned409 `InvoiceMatchingEvidenceNeedsReview`; comparison/browser did not run.

Source diagnosis: the existing commercial receipt operation allowlist omitted `InvoiceMapping` and `InvoiceCompare`. The mapping's first save occurred inside the shared transaction; final receipt save denied the new operation and the entire transaction rolled back. Independent owner SQL confirms captured invoice remains version1, mapping revision0, null current mapping and NotCompared, with zero mapping rows and zero invoice-operation receipts. The protected log/checkpoint and failed database are retained. Correction must register only the exact operations, preserve actor/key/scoped FK checks and add a full-save regression before a fresh actual rerun. The prior167 API/550 domain passes did not cover this missing receipt integration.


## Exact receipt-operation correction and rebuilt gate

Reviewed `043c28c` integrated as `f402462` adds only `InvoiceMapping` and `InvoiceCompare` to the existing receipt allowlist. All actor, UUID key, fingerprint, completion and scoped PO binding rules remain. New full-context tests save original capture→mapping/pointer→receipt and a clearly synthetic Blocked result→receipt; wrong actor/unknown operation denies without persistence. These are application save-guard regressions, not PostgreSQL FK/real Finance proof.

Unexpected command failures now log only safe diagnostic metadata in `bda8e5e`: trace ID, exception type, reflected method name, SQLSTATE and constraint name. No exception object/message/detail, submitted body, SQL or headers go to the sink. The earlier full-exception logging approach `0b95ff1` was source-reviewed and hardened before the rebuilt API compiler ran. Tool logging is filtered to this warning category only; client responses are unchanged.

Session2127/container `procurement-p26-receipt-fix-verification` terminated0:

- **555 domain/model tests passed**, zero failed/skipped,48s.
- Full API/tool Release rebuild passed,14 existing warnings and zero errors.
- EF model drift check: no changes. Accepted migration/SQL remain unchanged; the actual migration rehearsal remains valid.

Evidence `artifacts/p26/receipt-fix-verification.log` and `domain-receipt-fix` TRX. Domain/application correction source is `f402462`; the API/tool includes the safe logging correction `bda8e5e`, committed before its compiler ran. The earlier167 API regression remains source-scoped evidence; no new full167-API rerun is inferred here. Fresh actual restricted HTTP/PostgreSQL rerun is now using private `20261020_174133_matching`; terminal outcome/browser remain pending.
