# M6D private currency and invoice capture checkpoint — 30 October 2026

**Bounded result: Core publication and invoice capture/recovery verified. Matching and whole-module UAT remain open. No M6D production deployment is claimed.**

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

The correction keeps the verified incompatible candidate visible as a blocked row, retains pagination and totals, and leaves compatible Goods selectable. The existing table shows a plain receipt-type explanation. Direct acceptance selection and comparison retain their authority checks; malformed witness evidence still fails closed. Fresh API and real UI matching acceptance are required after the corrected build.

## Operational incident and active obligations

The production PostgreSQL service had stopped during an earlier disk-full event. It was restarted using the same container, image and volume after cache reclamation. Read-only checks confirmed its existing system identifier, 220 tables and healthy API. No deployment, migration, role grant or logical database restore occurred. The separate approved recovery receipt is `production-db-diskfull-recovery-20261030.private.json`, SHA `e0a30081f279d71ff4912a7640acc7fa0e2bfac663edcd0e4ff9b4f1c5b43f64`; historical pre-recovery guards were preserved rather than silently refreshed. Whole-data integrity or business UAT is not inferred from a health check.

Private test grants remain temporary during continuation. After tests, stop the exact private processes and revoke all cohort sessions, restore the supplier-read addon first, then restore the original 60 permission edges and three role rows, and verify exact security restoration while retaining the tested business history. Posted-history correction/replacement, populated rollback/history checks, reviewed release and the wider Procurement gap register remain open.
