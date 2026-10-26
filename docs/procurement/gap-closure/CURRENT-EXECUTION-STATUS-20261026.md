# Procurement execution checkpoint — 26 October 2026

This checkpoint supersedes earlier status paragraphs, not historical acceptance records. Procurement is not complete. The coordinator continues the ordered M0–M10 roadmap, preserving the imported UI and shared ERP components. No production migration, grant, activation or deployment is claimed here.

## Published baselines and ownership

- ERP code baseline: `ff255065ab3240d7949315aafd62395db3d03838` (main, PR100 merged). The default-off M2a source and prior CSV reader are verified; historical pending-build statements are superseded below.
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

## Remaining finish sequence

The existing [full roadmap](https://github.com/georgemunganga/erp/blob/3fc26d93be9eaa144d0135a13c70116956c2137a/docs/procurement/gap-closure/PROCUREMENT-FINISH-MILESTONES-20261025.md) tracks 55 grouped findings, not 55 proven defects. That branch is a planning reference, not permission to merge all its code. Its earlier baseline paragraphs are superseded above.

| Milestone | Remaining exit |
| --- | --- |
| R0 / M0 | Production least privilege, coherent schema/release and current scope/role verification; publisher empty-cycle proof does not establish nonempty cross-tenant delivery. |
| M1 | Real scanner and authorized attachment lifecycle, including failure/denied paths. |
| M2 | Real imports and remaining master-data/batch/saved-view contracts; selected exports are already deployed. |
| M3 | Real request approval, line routing, retained quantities and truthful Finance budget interface. |
| M4 | RFQ publication, sealed/surrogate bids, evaluation and residual-safe awards. |
| M5 | Governed PO revisions, goods/service receipts, inspection, returns and owned Inventory/Assets handoff. |
| M6 | Bills, matching, credits and manual-payment reconciliation; future Finance/AP adapter remains explicit. |
| M7 | Contracts, recurrence, delivery/communications and reconciled reports. |
| M8 | Supplier portal acceptance or explicit launch scope decision. |
| M9 | Coherent end-to-end UAT, safe rollout and recorded business acceptance. |
| M10 | Optional platform extensions, separately scoped. |

Dependencies gate affected operations, not unrelated defined CRUD. Procurement uses Core users/permissions/scope without requiring an employee. Company currency comes from Core; company-wide viewing does not invent a transaction posting branch. Finance owns accounting/bank execution; unavailable Finance must not fabricate funding or payment confirmation.
