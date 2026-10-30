# M6D private currency and invoice capture checkpoint — 30 October 2026

**Bounded result: Core publication, invoice capture/recovery, one qualified comparison, feature-off history and private cleanup verified. Whole-module UAT and M6D production release remain open.**

Product source was ERP `8013252d` and original Procurement UI `fe425ed`. The subsequent source-picker correction has separate checks and must not inherit a completed matching claim from this checkpoint.

## What the real workflow proved

An explicitly permitted Core user without an employee link published a ZMW calculation reference for the private test company. The original UI returned 201 and its original-key GET returned exactly the recorded response. This is a test convention, not approval of Amizo monetary policies or a production configuration change.

A separately permitted user entered an invoice through the supplied Bill form: quantity 1, source unit price 5.1234, tax 0. The server calculated net/gross 5.12 and adjustment -0.0034 against the published Core reference. A changed input with an old preview was rejected with 409.

The invoice POST committed while its browser request context was closing. Native read-only reconciliation found exactly one invoice, line, calculation and capture receipt. No second capture was sent. A normal-cookie GET returned the original 201 response byte for byte. A continuation restored the exact original pending client command from the captured request, reloaded the page, used the real UI recovery action, and cleared the journal. Another permitted invoice reader received 404 for that actor-owned command key. This proves a split continuation; it is not an uninterrupted passing browser run or a claim that the original incognito browser profile was retained.

The persisted original rate remains 5.1234. Core owns currency precision; Procurement owns calculation evidence and the invoice. Neither matching tolerances nor the older blocked invoice were rewritten. No employee record, Finance posting, bank operation or supplier communication was created.

## Evidence boundaries

Protected evidence is under `procurement-m6d-native-20261030_run01` in the host evidence directory:

- `real-ui-core-invoice-result-run01.private.json`: Core UI publication/original-key success, followed by a harness failure that incorrectly used native select selection on the shared floating control.
- `real-ui-core-invoice-result-run02.private.json`: a needless detailed-vendor lookup failed under this purpose-limited private SQL role, which lacks supplier-contact access. The actual invoice form uses the permitted supplier summary lookup. This is not Vendor Detail acceptance or evidence about the production SQL profile.
- `real-ui-core-invoice-result-run03.private.json`: the harness checked a pending banner before its response observer completed and closed the context. The retained failure remains a failure; its capture key actually committed.
- `real-ui-original-capture-recovery-result-run01.private.json`, SHA `0024ba28307dfd84180e93ce00f0b5aae499b8fd9ceabe0d82403128eba345e8`: real GET/UI recovery, page reload, actor isolation, no invoice POST in the continuation, and logout.
- `uat-owner-run01/post-uat-native-owner.private.json`, SHA `4a62393ad7dd47e43bdc9e94520ff7a0a44aac945f034ebc808040b9b0f514d6`: accepted domain codecs replay the actual Core/calculation/receipt graph; prior business rows and security are reconciled, with typed authentication changes accounted for. All eight capture-cohort sessions were revoked at that checkpoint.
- `uat-owner-run01/post-uat-offline-domain-execution.private.json`, SHA `4451c8be6ea6995abcf421eb88a514181e9ff2c37e54cc96e89c7d9e734573b9`: exact compiled verifier, raw database snapshot, failed-attempt and recovery lineage. Offline verification did not call the API or database.

Later read-only matching preflights have their own failed receipts and session cleanup. They must not be counted as successful mapping, review or comparison commands.

## Remaining matching defect and correction

A new invoice with no PO header legitimately lists several issued orders for its supplier and branch. The private matching policy requires Goods receipts. The intended Goods source matches that policy, but an unrelated Service source in the same category caused the selector to reject the entire page with `InvoiceMatchingAcceptanceKindChanged`.

Native diagnosis preserved all five candidate parents, the correct Goods source and the unrelated Service source. Three other parents have no receiving state; their receipt kinds were not guessed from mutable request records. No policy or retained source was changed.

The correction keeps the verified incompatible candidate visible as a blocked row, retains pagination and totals, and leaves compatible Goods selectable. The existing table shows a plain receipt-type explanation. Direct acceptance selection and comparison retain their authority checks; malformed witness evidence still fails closed.

A subsequent real-cookie run against ERP `f3ea1b5a` and Procurement UI `72c3882e` verified the selector: five parents remained visible, the intended Goods source was selectable, and the incompatible Service source was blocked. The original UI committed the line mapping and opened a manual-unit review. A separate permitted user then approved that review; the original-key recovery returned its recorded decision. These commands were reconciled before continuation and were not replayed.

The next comparison returned 409 with `PrincipalInvoiceTargetResultUnclassified`. An older genuine Blocked comparison on the same order had no allocation, but the owner check treated all results without an active claim as unknown effects. Read-only native evidence `verified-review-compare409-owner-run01/verified-review-compare409-owner.private.json`, SHA `8ad701e0bdee47ac4b6703ae189200146e903ea3531a214bbcb484a9cd6071a4`, confirms the new review is Verified/Core Approved, the failed comparison created no result or receipt, and no new claim or allocation exists. The older result, exception, original receipt, audit and outbox remain linked and unchanged, with no target Finance, credit or payment effect. One unrelated retained manual payment is outside this target; it was not erased or treated as a successful payment for this invoice.

The narrow classifier correction passed independent source review, all 1,677 Procurement tests, 19 focused Host tests and the analyzer Host build. A first build failed on a new test fixture variable typo; that failure is retained separately, and the successor changed only that fixture before the successful suite. The classifier verifies the complete recorded no-claim footprint before admitting the exact historical header-invalid result, preserves unknown-effect denial, and does not revalue the historical invalid header. Its supported scope is explicit; this is not a waiver for arbitrary Blocked results. A further audit found that new-key matching writes can alter an already-compared invoice and invalidate that retained footprint. An after-recovery history lock is required before continuation/release. Comparison acceptance remains open. The exact R5 private stack has stopped cleanly and all its owned processes are absent. No matching success, capacity claim or production release is inferred from the successful review.

## Qualified comparison and interrupted feature-off continuation — 30 October

ERP `d54d01ec` and Procurement UI `8b91313b` now have bounded private qualified-comparison evidence. The original UI submitted one comparison, received 200, displayed its saved result and locked new mapping/comparison actions. Original-key comparison recovery returned 200; capture recovery retained its exact original 201 bytes. Native reconciliation found exactly one allocation for quantity 1 and net ZMW 5.12, no new Finance, credit or payment effects, and the older Blocked invoice and its evidence unchanged. This does not accept every discrepancy or release the module.

Protected UI result `procurement-m6d-compare-only-original-ui-prep-20261030_run02/compare-only-result.private.json` has SHA `a47cf2428a79cb2a9eef3f61198b9cac53377a2ccca73c7d3c0f8ec4c8e2ffe7`. Corrected native result `r7-qualified-owner-run01/r7-qualified-owner.run02.private.json` has SHA `050546fa96e52040005df1d6862b54a3533627695c26d0b4bd0c60f14a8942f9`. Its first envelope had stale summary fields and remains retained separately. The native check did not independently replay the claim codec offline; the actual comparison executed it.

The combined backend passed 1,678 Procurement tests, 80 focused Host tests and the analyzer build. UI type checking, 85 focused tests and its production build passed. These are local checks, not hosted CI. Earlier comparison failures remain failed evidence: a physical-versus-projected selector mismatch and the retained policy evidence serializer mismatch were corrected without modifying historical business rows.

A subsequent normal-cookie test with new writes disabled recovered saved invoice, calculation, comparison and manual-review history. Invoice capture and preview returned their expected 503; comparison returned the generic `InvoiceMatchingUnavailable` 503 because disabled principal sources select the legacy readiness gate first. The test expected a different error code and stopped after three denied requests. Its result is **FAIL**, not a completed feature-off pass. The remaining reviewer/publisher history, denials and original-UI history checks require continuation. The restart stopped the owned private stack; native reconciliation and temporary-grant cleanup remain tracked obligations.

Read-only native reconciliation after that failed test verified all 227 business tables unchanged apart from the explicitly attributed authentication records, with the new test session revoked and no other private clients. The exact owner receipt is `r8-interrupted-owner-run01/interrupted-r8-owner.private.json`, SHA `556a479a78f4232f24df72d603e5f351e4a8e2777e325ceef958321001f93581`. This proves no business effects, not completion of the remaining feature-off tests.

Both official populated downgrade scripts were then attempted against the stopped private stack. Procurement and Core each stopped at their first retained-evidence guard with SQLSTATE `55000`; disconnect rolled each transaction back. Full 227-table rows, schema, owners, ACLs, RLS, columns, indexes and constraints matched before and after each attempt; the accepted source database and production guards stayed unchanged. Receipt `populated-down-and-fixture-closeout-plan-run02/populated-down-result.private.json`, SHA `0c4ad37f98c5c8bd92023f55b259701afd98b5b2ef32202f820f67e4823e1ec6`, records this bounded result. The first harness attempt omitted the six Core Approval relations from its metadata query and stopped before sending any downgrade SQL; its failed preflight is retained separately. No migration was reversed and no temporary fixture permission was restored during these checks.

Controlled invoice replacement is under implementation on a separate M6E branch. Its source is not yet a migration, operational feature, completed browser acceptance or production release. Same-number correction and wider invoice exceptions remain separate gaps.

## Feature-off history and private cleanup completed

The continuation completed 19 normal-cookie history reads and the two previously unsent disabled-feature commands. Manual Open and Core Publish returned their expected 503 responses; all three actors signed out. The original failed harness and its three consumed keys were preserved without replay. API receipt SHA: `3b689bf17ef9933a5fab2f3dd74a67228d83bdac5fc0e63cb18931cbc6e627d4`.

The original UI then showed invoice, comparison, manual-review, amount-calculation and Core currency history while hiding disabled new-work controls. It made zero Procurement or Core currency writes; both users signed out. UI receipt SHA: `cbc9ce9660cc46295c65dbca1b85b643735b71c7918585cd8110d2bcc53f11b1`. All owned API, Core SSR, static-proxy and watchdog processes stopped.

Final native verification confirmed the exact five new revoked sessions and their typed authentication records, with all other business rows, 227 table shapes, security, SourceC and production unchanged. SHA: `aba9d103c36ec0341828748d6d3ea11614d8c67c639edf22e1983aeeefd4892f`. The initial verifier stopped on a redundant MD5 inventory that also contained the known authentication changes. A separately reviewed offline successor applied the identical exact authentication cohort to both SHA-256 and MD5 inventories over the pinned repeatable-read snapshots; it did not recapture the database or waive unrelated changes. The failed verifier remains retained.

Temporary permissions were restored in order, each SQL stage once with fresh native before/after checks: vendor addon first (`25d12bc64ea4cb91264721632ab3a944ec07dd13fd9cf0a22911b32ece17b226`), then the original 46 table and 14 column privilege edges and three role rows (`ee820d1d3668742e25ea037ffaa047be355c94a151678c67482dab3d0cd92632`). Original security is restored; accepted synthetic business and authentication history remains intact. Final closeout SHA: `505e3e2127832eca3177fd3e1847e8bcb600e4ea3dafffa746e636c6d04177e0`. This is private acceptance and cleanup, not a production deployment.

Capture denial reused an old preview digest with a changed number, so it proves the early feature gate rather than a fully validated fresh capture. Compare/Open denials refer to an already-compared invoice. Actual Core retirement, source-policy supersession and complete correction/Finance workflows remain separate acceptance work.

## Operational incident and active obligations

The production PostgreSQL service had stopped during an earlier disk-full event. It was restarted using the same container, image and volume after cache reclamation. Read-only checks confirmed its existing system identifier, 220 tables and healthy API. No deployment, migration, role grant or logical database restore occurred. The separate approved recovery receipt is `production-db-diskfull-recovery-20261030.private.json`, SHA `e0a30081f279d71ff4912a7640acc7fa0e2bfac663edcd0e4ff9b4f1c5b43f64`; historical pre-recovery guards were preserved rather than silently refreshed. Whole-data integrity or business UAT is not inferred from a health check.

Private test grants are restored and the owned runtime is stopped, as recorded above. Controlled correction/replacement, actual Core retirement and policy-supersession acceptance, current production-row migration/backup/finite-role preflight, reviewed paired release and the wider Procurement gap register remain open.
