# Procurement execution checkpoint — 26 October 2026

This checkpoint supersedes earlier status paragraphs, not historical acceptance records. Procurement is not complete. The coordinator continues the ordered M0–M10 roadmap, preserving the imported UI and shared ERP components. No production migration, grant, activation or deployment is claimed here.

## Published baselines and ownership

- ERP code baseline: `8eb2e305e66dba2f38763a2b029957ddec108b69` (main, PR96/97/98/99 merged). The bounded CSV reader passed 14 focused tests; earlier “compilation pending” wording is historical.
- UI main: `403bfdac505d593921a5bab1ba3388896205325f`. Reviewed UI compatibility branch `codex/procurement-scoped-exports-ui-20261025` is `44a577e894490dec779f7d477910553fb12c5839`; do not merge its full advanced ancestry merely on M2 evidence.
- Live remains product `cb18353d94af016ca65cd939ced76f6f97222c27`, UI `092121409c3d032706b1ba0ca305a24c2d84ede7`, API/publisher image `sha256:89375b1514f5a9cd5d2b255b94f09f2bba0a43020025e43ad6e30531084dd998`.
- Coordinator owns M2a vendor/item import and its backend/UI/database acceptance. Do not duplicate it. Manus retains the independent [Core scanner assignment](../../collaboration/manus/MANUS-CORE-DOCUMENT-SCANNER-04.md); no new overlapping task is assigned.

## Active M2a work

[ERP draft PR100](https://github.com/georgemunganga/erp/pull/100) contains default-off native vendor/item imports and a genuine generated migration. Exact verified source: `9d445f90ceb940bcbb53265c63741dab6484cd58`. Fresh builds passed; 68 focused tests and full suites of 1,282 host, 1,414 Procurement and 30 approval tests passed without failures/skips. EF model parity and the genuine 150-file API publication passed. The migration subsequently passed the separate private rehearsal below; it has not been applied to production.

[UI draft PR82](https://github.com/georgemunganga/module-connect/pull/82) preserves the original wizard presentation. Product source `da8f4ddf308779a4e0369d035aa2320cf832e38e`; test head `f1716182b88d1181bf22d8367de45ed9e591a74d`. TypeScript, 13 focused units, live production build and all six mocked browser cases passed. Tests cover mapping/preview, denied/unavailable paths, mobile lost-response recovery across branch context, and stale company/file responses. Mocked API tests are not real backend or production acceptance.

The recovery successor `bf1821935662bdbf5d9f395c32b5682659e813d4` subsequently passed TypeScript, 15 focused units, a new live build and all eight mocked browser cases. It adds persisted expiry, staging-quota feedback, and mobile GET recovery when new previews are unavailable. [Current UI evidence](M2A-RECOVERY-UI-VERIFICATION-20261026.json) remains separate from real API acceptance. The expiry worker source is reviewed but its actual build and finite-role PostgreSQL behavior are still pending.

[Machine-readable local verification](M2A-LOCAL-VERIFICATION-20261026.json) records actual result hashes. Original failed build/browser attempts remain retained separately. Hosted CI currently cannot start because GitHub reports an account billing lock; local results do not represent hosted CI success.

Independent review verified the raw build/TRX logs, all 150 publication bytes, owning/copy parity and source/profile pins. The [private migration verification](M2A-PRIVATE-MIGRATION-VERIFICATION-20261026.json) subsequently passed genuine Up → empty Down → Up, all 15 retained-evidence downgrade vetoes, and independent reconciliation of 197 original-table projections, history and ACL/role metadata. It establishes private schema compatibility, not import runtime acceptance.

Next: compile/test the reviewed transient-preview maintenance successor, verify finite runtime and maintenance roles, then run real normal-session HTTP/Playwright/import/recovery tests. Keep new writes default off until accepted. None of these pending checks is a reason to replace the supplied UI.

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
