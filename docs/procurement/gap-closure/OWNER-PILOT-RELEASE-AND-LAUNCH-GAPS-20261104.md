# Procurement owner pilot — deployed UI and minimum launch gaps

**4 November 2026: updated frontend deployed; operational purchasing handover NOT yet accepted.** This is a minimum launch assessment, not whole-module sign-off. It preserves the existing imported UI and shared ERP branding/components.

## Deployed and verified

UI PR106 merged to UI main `5b8814e9d58c8a2a5c3b7ac0fbd22cc922cd4523`. Deployed UI product is `0cf2f881`, reviewed head `05d3cfb26528910fb4f842f9c9c5e6dd6c174539`, at `round-review-0cf2f881-20261104`. TypeScript,40 focused tests, live build and one mocked-transport Chromium review-only/workerless queue smoke passed before release.

Production verification passed all45 primary/compatibility file hashes and sizes,11 HTTP page routes and13 real anonymous Playwright renders with zero page errors and an empty owned browser scope. These anonymous checks do not prove signed-in purchasing CRUD. The first switch failed because the new release root was0700; it rolled back. Correcting only that new root to0755 preserved all file hashes and the second switch passed. Original failure/rollback evidence remains retained.

The existing API/image/config/HostConfig are unchanged: backend executable `c93e8df4`, API4b3d2136, image8fa2f598. No migration, permission grant or business-processing flag was changed. Old UI `plan-review-3e2783b1-20261104` and the original nginx config are retained for rollback. Current nginx SHA256 `d5dc9b8bb8c7f9e2ff3fb715c024cfb9f435fd798fcfe878b50b1935f4faed21`.

Private release receipt SHA256 `571967d8b33ff661667b6aecd964b0e7c0a48d70682612ac92484675c37dc0e5`; public browser receipt `e3bdb176f325566609b01d00f41dd8f5325236fca05311bb84d1a0459feb8cf1`. Operator evidence is under `procurement-ui-owner-pilot-release-20261104_run01`. No credentials or raw business records are included here.

## What can be offered at the owner meeting

Offer the deployed system for an owner walkthrough and a supervised setup/draft-data pilot. Existing live adapters cover supplier/item management and saved request drafts; fresh owner-account create/edit/read/cancel checks remain a gate before treating those operations as accepted. Do not describe the company as ready to issue real purchasing commitments from the current release evidence alone.

Use Amizo Power's existing Core company, default currency and branch setup. Core owns users, roles and branch scope; a permitted system user does not require an employee link. Avoid duplicating company or identity masters in Procurement.

## Minimum operational handover gates

| Gate | Actual position | Required result |
| --- | --- | --- |
| Owner/buyer/requester access | Shared Core identity and permissions exist; previous branch and workerless fixes retained | Named real users open the correct company/branch and can perform only their assigned actions |
| Basic saved records | Supplier/item/request draft adapters exist; current frontend build/smoke is verified | Signed-in create, edit, reload and cancel/retire tests against real APIs, including an assigned-branch denial |
| Purchasing authority | Production `Approvals:Enabled=false`; direct-order processing is also false | Business owner supplies real approvers, spending limits/sourcing rules and independent funding-check authority; publish the actual scoped policy, validate readiness and activate the tested flow |
| Complete basic purchase | Source/private evidence exists across requisition, PO and goods/service receipt; current owner-account production journey is not accepted | Request → independent approval → linked PO → approval/issue → partial/full receipt → invoice entry, with native persistence, audit, wrong-user denial and duplicate/retry checks |
| Funding/payment boundary | Finance module is not built; governed manual evidence/payment reporting has source/private evidence but production manual payments are false | For a pilot, approve the external/manual funding process and responsibilities. Keep accounting/bank execution external; activate manual reporting only after its real scoped policy and workflow pass. Do not invent Finance balances or paid status |
| Documents and operations | Verified document/scanner workflow remains incomplete; server had zero free root bytes during preflight | Do not rely on unverified attachments. Resolve durable storage headroom, confirm recoverable backup/support ownership and finish the agreed attachment-required transitions before operational handover |

The current owner meeting can settle the named approvers, spending limits, who verifies funding, permitted starting branches and the exact first workflow. No thresholds, approvers or policy references have been invented or seeded by this deployment.

## Explicitly outside the first controlled pilot

Formal tender/bid opening/evaluation/award, supplier self-service, OCR/email invoice intake, automatic recurring purchasing, blanket/agreement drawdowns, full contract lifecycle, automated Finance/AP/bank execution and Inventory/Asset ledger integrations need not block a deliberately limited pilot. They remain open capabilities; do not label them complete or silently replace their screens.

The first operational milestone should close the basic signed-in purchasing journey above before continuing advanced round-plan review. B1c source PR183 has separate verified source/native evidence; new readiness-capture PR187 remains a draft with unexecuted regression/capture work and is not deployed. Parent issue145 stays NOT READY for whole-module acceptance.

## Storage recovery performed

The root filesystem had effectively zero free bytes. Removed unused regenerable APT temporary/index caches, archived system journals, one unreferenced obsolete private test image and three explicitly inactive reproducible build-cache archives. Raw test logs/manifests, source, production/private data volumes, uploads and retained immutable publications were preserved. The private rehearsal PostgreSQL container was stopped with its data retained. After staging this UI, root free space was approximately347MiB: the immediate failure was mitigated, but durable capacity/headroom remains an operational launch gap.
