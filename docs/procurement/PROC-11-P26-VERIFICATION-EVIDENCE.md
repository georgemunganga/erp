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
