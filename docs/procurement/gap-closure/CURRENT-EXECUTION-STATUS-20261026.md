# Procurement execution checkpoint — 26 October 2026

This checkpoint supersedes earlier status paragraphs, not historical acceptance records. Procurement is not complete. The coordinator continues M0–M10, preserving the imported UI and shared ERP components. The technical release below is deployed; whole-module business UAT remains open. Earlier no-deployment statements are historical.

## Latest backend correction deployed

ERP main `de449645a5cf4b46da1024c821252ac46b625103` includes PR117 and its deployment record PR118. Backend product `237a936b25690f780cd09ca6ab3f90b1a05cc009` / image `c233ffd9e76f7579f52bfb03259985a7abc6691c94d05b9633773b2389f3aa0a` is deployed to API and publisher. The SMTP fallback now rejects expired or invalid account-link expiry before transport, retaining visible failed-retry semantics. Eight focused and all 1,327 Host tests passed, and 150 actual image files matched the genuine publication. Read-only canary and independent live release verification passed; all Env/Host settings, import+maintenance flags, schema200/histories56/28/1 and database role grants were preserved. Old containers remain stopped for rollback. [Deployment evidence](../release/CORE-ACCOUNT-LINK-EXPIRY-DEPLOYMENT-20261026.json) retains the failed healthcheck-metadata verifier attempt. This supersedes D35/C9 as the current backend, while the UI remains 9b6. A read-only production aggregate also observed one failed account-link retry with the fixed expiry-guard reason, without exposing payload or recipient. SMTP provider delivery remains open; whole-module UAT is not complete.

M4 source `e49b0631` passed 1,327 Host and 30 genuinely discovered Core Approval tests; Procurement1,434 and no-pending-model evidence are carried only by exact unchanged source/model parity. A populated 200-table clone passed Up/empty Down/Up; ten independent transactional marker tests proved each physical Down veto, with FK-bypass limits explicit. Real-cookie acceptance is in progress, including an actual branch-scope denial and normal-cookie independently activated PR authority. None of this activates M4 in production.

## Latest in-progress workflow test and cleanup

The original RFQ UI opened publication review and recovered its committed Open
result after a deliberately lost response. A reviewer-only metadata availability
bug was fixed in private source `9009562134f86f1c74ce5200034a330ea00d49df`,
which passed 1,341 Host tests and a genuine 150-file publication. The subsequent
real reviewer Decide still returned 409. Independent read-only reconciliation
confirmed no decision, Decide receipt or invitation was committed; the case
remains pending. A single original-key diagnostic retry also returned 409.
Its original Finance evidence expired naturally and the private proxy/API were
closed. Safe stage/code logging successor `d295e08850d24b317d76aa434aabe5da0c224ca0`
is being built before a fresh normal-cookie authority/funding/source cohort.
The underlying refusal is not yet isolated. This is a real unresolved UAT
blocker, not a passed publication workflow or a production release.

[Draft M5a PR119](https://github.com/georgemunganga/erp/pull/119) preserves source
`e57de3c786e65e60da28285483ed9de9a8a942dd` for same-row PO governance,
manual commitment/release evidence, shared Core approval and strict historical
command recovery. Pending withdrawal uses the real Core cancellation engine;
manual supplier acknowledgment retains its approved procedure and observation
and recording times. Source review does not establish schema or runtime
acceptance. Its migration, physical readiness, original UI binding and actual
workflow tests remain required; the operational flag remains off.

Cleanup removed three verified compiled-cache archives totalling 299,087,290
bytes, preserving source, raw UAT results, DB backups and rollback products.
Merged remote branches for PR117/118 were retired after exact-head and main
ancestry checks. The [cleanup record](../release/UAT-CLEANUP-CHECKPOINT-20261026.json)
contains exact journal hashes. Neither cleanup nor draft source completes UAT.

## Current UAT and UI update

UI PR85 is merged through main `dfed2dd1954e8dc735a3bd8ff03449d4b00e6911`; deployed product is `9b6ab3442aa1583a339dcfed311adad393a29706` with test-only successor `544df3d698fa4bac3704860d8616413cae732d61`. The static release is `m0-signin-9b6-20261026`; backend image is the latest correction above and schema remains the technical release below. Anonymous Procurement now offers sign-in and retry, distinct from permission denial. TypeScript, 39 focused units, seven mocked browser cases, five exact served assets and live anonymous navigation to Core sign-in passed. [Evidence](../release/M0-ANONYMOUS-RECOVERY-DEPLOYMENT-20261026.json) retains the failed verifier attempt and rollback. This supersedes the anonymous-recovery issue paragraph below.

[Fresh M3b acceptance](M3B-FRESH-BROWSER-ACCEPTANCE-20261026.json) now proves real-cookie request submission, manual funding evidence, shared Core approval, internal order create, lost-acknowledgement original-key recovery, cancellation, released quantity, and All read/write boundaries. Typed owner checks reconcile the new order graph and 200 original table projections; all 57 cohort sessions are revoked. Failed grant-expiry, locator, stale-version and timing attempts remain retained. This full business flow used D35/e032; the newer 9b6 UI recovery has its separate evidence above. PO issue, dispatch and later milestones are not claimed.

[Current M2a acceptance](M2A-CURRENT-MAINLINE-ACCEPTANCE-20261026.json) proves D35/9b6 real-cookie import HTTP and four browser phases against a fresh clone using finite Core, Record and Maintenance roles. Preview/commit/replay, lost-response recovery across branch→All, unavailable-new-work recovery, persisted expiry and quota checks passed; 200 original tables are unchanged and 18/18 sessions revoked. Private Unix trust and co-resident fixture owner remain explicit limits. **Production imports and finite maintenance are now enabled after reviewed activation.** [Activation evidence](../release/M2A-PRODUCTION-IMPORT-ACTIVATION-20261026.json) records the healthy empty-sweep canary, unchanged histories/role permissions and retained rollback API. Only the two import flags and finite maintenance connector changed; no production import transaction was created. The current account has no explicit import permissions, so its Vendor/Item schema requests correctly return 403. Core role assignment remains separate from global enablement.

The live administrator account separately signed in and read Core company choices, Procurement context/schema metadata, Vendors, Items and Requests in company-All scope, then logged out. `workerLinked=false`; no employee link was required and no purchasing record was written.

M4 internal RFQ publication review is draft ERP PR114; independent source review closed policy-currentness, typed-receipt binding and policy-resubmission findings. The source suites, representative nonempty migration and physical veto evidence are now recorded above; final runtime/finite-role/browser owner acceptance remains pending. It remains disabled; no held mixed PO/receiving migration was merged.

## Deployed technical release — 26 October, 15:21 UTC (configuration and UI superseded above)

ERP main `c8eac4c54b009bbafe3cf916992abdbcf3562a61` includes PR111; UI main `7f75d327a2bb8eb399f48b370da0cc53a0a831b9` includes PR84. Deployed product is `d35ecc090b0de0abcab1e38be09fd25c52a1d0aa`, UI `e032805303e23e1658d5a66808d9b0d6f1ce991c`, paired API/publisher image `c9ea9f7bd48c112bf20cc5cbb6f20d48850075942dfa37f366af9c41f352464b`, and Procurement static release `m3b-d35-e032-20261026`. Use these published mains or descendants for new assignments. Core/HRM SSR and Accounting were preserved.

A fresh stopped-writer backup restored all **191 COPY table row multisets**. Genuine migration histories are now **HRM56 / Procurement28 / CoreApproval1**. Original 191 column projections were preserved and nine new tables were empty before cutover. Five finite LOGIN roles passed actual network SCRAM and wrong-password rejection. Application processes now use finite accounts; no superuser runtime session was observed. API is healthy and publisher is running, with zero restarts at verification. The previous release and backup remain retained.

Public Chromium checks verified **five exact served assets and eight public route navigations**, with no page/console errors or mutations. Protected pages settle at sign-in. These are anonymous checks, not production-user CRUD acceptance. A fresh full private request/PO browser rerun is separate and in progress; prior source evidence and its failed locator case remain historical.

At this initial cutover, approvals, imports, manual payments and principal direct-order execution remained off; imports and maintenance were subsequently activated as recorded above. Operational approvals, manual payments and principal direct-order execution remain off; principal identity capture is configured on. This release does not manufacture policies or enable held workflows. SMTP delivery remains open: a ten-day-old access-link event was already failed in the backup and retried with the same server rejection and unchanged settings. Its missing expiry guard was subsequently fixed and deployed as recorded above; provider delivery remains open. The anonymous sign-in recovery issue was subsequently fixed and deployed as recorded above.

[Sanitized deployment evidence](../release/M3B-TECHNICAL-DEPLOYMENT-20261026.json) records exact pins, failed attempts and limits. Initial storage cleanup reclaimed approximately **2.4 GB**, preserving production databases, source, current backup and rollback release. The cleanup inventory found 313 databases in the isolated cluster; older test copies need reviewed retention cleanup. A further 224,685,261 bytes of this coordinator’s completed untracked bin/obj cache were retired after source/publication/TRX/log preservation, bringing recorded cleanup to approximately 2.6 GB. **This technical deployment does not complete M1–M9 or business UAT.**

## Historical source checkpoint and ownership

- ERP code baseline: `35045b7e51efca2e756dd678b9de6a62477b03bc` (main, PR108 merged; includes PR100 and PR106). The default-off M2a source and prior CSV reader are verified; historical pending-build statements are superseded below.
- UI main: `704ac9c642d00e3672cd81e7c1df2d9cb50be160` (narrow PR83 merged). The 139-file held ancestry was excluded; the accepted native test product remains the earlier `bf182` build.
- Live remains product `cb18353d94af016ca65cd939ced76f6f97222c27`, UI `092121409c3d032706b1ba0ca305a24c2d84ede7`, API/publisher image `sha256:89375b1514f5a9cd5d2b255b94f09f2bba0a43020025e43ad6e30531084dd998`.
- Coordinator owns M2a vendor/item import and its backend/UI/database acceptance. Do not duplicate it. Manus retains the independent [Core scanner assignment](../../collaboration/manus/MANUS-CORE-DOCUMENT-SCANNER-04.md); no new overlapping task is assigned.

## M2a source acceptance

ERP [PR100](https://github.com/georgemunganga/erp/pull/100) is merged in main `ff255065ab3240d7949315aafd62395db3d03838`. Exact accepted product source is `88c561554b60f313881248e5cd581f6856e982d8`. Vendor/Item import remains default off; no production migration, role grant, configuration activation or deployment occurred.

Fresh API/Host builds, 25 focused tests, all **1,291 Host tests**, and a genuine 150-file API publication passed. Unchanged Procurement 1,414 and Approval 30 results are carried from their accepted build, not rerun. The prior genuine private migration rehearsal passed Up → empty Down → Up and 15 retained-evidence downgrade vetoes; its original-data/history/ACL verification remains separate from runtime fixtures.

The final private cohort passed **12 real-cookie API cases and four real browser phases** against UI product `bf1821935662bdbf5d9f395c32b5682659e813d4`. These cover Vendor import, Item save/replay, lost-response recovery across Headquarters → All while new work returns 503, persisted expiry recovery, and quota feedback. Independent reconciliation verified five committed batches, five receipts, four created masters, 198 unchanged original-table projections, 185 unchanged synthetic-other projections, and all 18 sessions revoked. Suppliers remain Draft with current Core company currency; Items remain Inactive. No employee link is required.

The [sanitized acceptance record](M2A-NATIVE-IMPORT-ACCEPTANCE-20261026.json) links exact source, artifacts and independent reviews. Six remaining synthetic previews were clock-advanced by exact reviewed owner CAS, then cleared by the genuine worker; all committed records and receipts remained unchanged. API and TLS proxy exited cleanly. This proves bounded private source acceptance, not production readiness, elapsed TTL, bulk/concurrent throughput or whole-module UAT. The private DB uses trust sockets and the fixture combines owner setup/control with finite API connectors; it does not prove production process isolation or password/TLS database authentication. Core login uses real password/cookie authentication; the sign-in form was not exercised here.

All failed attempts remain retained, including the original Item connection-state boundary failure and the browser forwarding-wait race. Those were corrected and followed by the fresh final passing cohort. Hosted CI remains blocked by GitHub's account billing lock; local acceptance is not a hosted CI pass.

UI [PR83](https://github.com/georgemunganga/module-connect/pull/83) is merged through main `704ac9c642d00e3672cd81e7c1df2d9cb50be160`. The narrow 35-file port excludes the 139-file held ancestry and preserves the original wizard/shared ERP controls. Clean committed source `cd4f51996d5c0bd5628675d327373fde49ec9b33` passed TypeScript, 99 unit tests, a fresh live build and 23 mocked browser tests. Its [mainline evidence](M2A-UI-MAINLINE-VERIFICATION-20261026.json) is separate from the earlier real-API `bf182` acceptance; real API/cookie acceptance for this port remains a release gate.

Next: close the production least-privilege and nonempty publisher gates, verify the mainline UI against the real API, then continue M1–M9. M2a acceptance does not complete the rest of M2 or authorize changes to Finance, Inventory or supplier eligibility.

## M0 cross-tenant publisher follow-up

Source `508f26bdfb0bfd90248a5b896c571bb7d61e6711` in [PR106](https://github.com/georgemunganga/erp/pull/106) binds publisher writes and audits to each locked database row’s database-row tenant. Ordinary API principal scope remains unchanged. Fresh Infrastructure/API/Host builds, **28 focused and 1,293 Host tests**, and a genuine 150-file API publication passed.

The finite-role private Store test completed two rows from different tenants and produced four correctly tenant-attributed audits. A genuine, network-isolated publisher then completed a second distinct pair: four total rows, eight correctly tenant-attributed audits, 198 original table fingerprints unchanged, and clean exit. The original retained database and its queued failed event were separately recaptured unchanged. Independent final review passed and PR106 is merged through main `27be07e0a97d61ea8fe57d4a15c3de527cbcfd43`; the [evidence record](M0-CROSS-TENANT-PUBLISHER-VERIFICATION-20261026.json) states that boundary.

This is acknowledgement and tenant-isolation evidence using deliberately unsupported no-email test events (`smtp-skipped`), not external delivery. Private local trust does not prove password/TLS database authentication. The isolated database copies genuine schema/history; this test is not a migration replay. No production role, migration, setting or deployment changed. GitHub run 40274531522 did not start because of the account billing lock; local passes are not a hosted CI pass.

## M3a request and approval source acceptance

[PR108](https://github.com/georgemunganga/erp/pull/108) is merged through `35045b7e51efca2e756dd678b9de6a62477b03bc`. Exact source `18585c14` passed fresh API/Host builds, **14 focused and 1,295 Host tests**, and a genuine 150-file publication. Unchanged Procurement 1,414 and Core Approval 30 results are carried, not rerun. The repair accepts only two independently captured PostgreSQL restored CHECK spellings; all original constraint/model/history/key/column/privilege controls remain enforced.

Three real-cookie Chromium phases passed against the existing UI main `704ac9c`: ordinary API approval of the same funded request; pre-case Return/correction/resubmission through the API; and actual UI lost-Open-response/reload recovery followed by API Return, independent manual release, correction and resubmission. Independent raw owner checks verified the approved graph, pre-case graph and the three legitimate Open/Decide/Release receipts, **198 unchanged original tables**, preserved existing role/ACL metadata, and **10/10 revoked sessions**. Three sessions left by the resource-aborted run were revoked by exact fixture-owner CAS; seven subsequent sessions logged out normally. The genuine resumed API exited zero without OOM. [Exact evidence, failures and limits](M3A-REQUEST-APPROVAL-ACCEPTANCE-20261026.json) remain explicit.

This is bounded private source acceptance. Core remains owner-backed; finite Procurement roles use private trust sockets. No external Finance authority or ledger reconciliation, foreign-tenant row-isolation proof, full sign-in UI, production activation or whole-module UAT is claimed. The old two-receipt owner verifier failed and remains retained; a separate independent check reconciled the legitimate third release receipt. Production is unchanged. Next: coherent principal-based approved-line conversion, its genuine migration and existing UI port, plus remaining production/release gates.

## Remaining finish sequence

The existing [full roadmap](https://github.com/georgemunganga/erp/blob/3fc26d93be9eaa144d0135a13c70116956c2137a/docs/procurement/gap-closure/PROCUREMENT-FINISH-MILESTONES-20261025.md) tracks 55 grouped findings, not 55 proven defects. That branch is a planning reference, not permission to merge all its code. Its earlier baseline paragraphs are superseded above.

| Milestone | Remaining exit |
| --- | --- |
| R0 / M0 | Production least privilege, coherent schema/release and current scope/role verification; publisher empty-cycle proof does not establish nonempty cross-tenant delivery. |
| M1 | Real scanner and authorized attachment lifecycle, including failure/denied paths. |
| M2 | Real imports and remaining master-data/batch/saved-view contracts; selected exports are already deployed. |
| M3 | Request/approval bounded private source accepted above; line routing/conversion, retained quantities and production activation remain open. |
| M4 | RFQ publication, sealed/surrogate bids, evaluation and residual-safe awards. |
| M5 | Governed PO revisions, goods/service receipts, inspection, returns and owned Inventory/Assets handoff. |
| M6 | Bills, matching, credits and manual-payment reconciliation; future Finance/AP adapter remains explicit. |
| M7 | Contracts, recurrence, delivery/communications and reconciled reports. |
| M8 | Supplier portal acceptance or explicit launch scope decision. |
| M9 | Coherent end-to-end UAT, safe rollout and recorded business acceptance. |
| M10 | Optional platform extensions, separately scoped. |

Dependencies gate affected operations, not unrelated defined CRUD. Procurement uses Core users/permissions/scope without requiring an employee. Company currency comes from Core; company-wide viewing does not invent a transaction posting branch. Finance owns accounting/bank execution; unavailable Finance must not fabricate funding or payment confirmation.
