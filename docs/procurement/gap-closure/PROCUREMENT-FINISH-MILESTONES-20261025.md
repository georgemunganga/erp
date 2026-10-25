# Procurement finish milestones

Updated 25 October 2026. This is the execution order for the existing **55 grouped audit findings**, which mix implementation, integration, verification, release, documentation and scope decisions. They are not 55 confirmed production defects or 55 features to rebuild. Preserve the supplied UI and shared ERP components. This plan does not approve spending policy or mark business UAT complete.

## Current verified baseline

**Current deployed M2 selected-export checkpoint — 25 October 2026:** [Release](../release/P24-M2-SELECTED-CSV-RELEASE-20261025.md) records exact product `cb18353d94af016ca65cd939ced76f6f97222c27`, UI `092121409c3d032706b1ba0ca305a24c2d84ede7`, API/publisher image `89375b1514f5a9cd5d2b255b94f09f2bba0a43020025e43ad6e30531084dd998`. Vendor/Item All/search/selected CSV and bounded M0 scope remain released. Local 116 focused/1,081 Host, 93 UI units/23 mocks and private normal-cookie HTTP/two browsers/revocation with owner audit deltas 6/2/1 passed. Cutover: healthy API/running publisher, zero restarts, Core validate-only, 150 image files/five served UI hashes exact, four pages 200/four anonymous APIs 401; old M0 pair retained stopped. No schema/grant changes or production test writes. M2 remains partial; imports/batch/saved views, broader operational All, production-user UAT and later-model R0 gates stay open. Main remains ERP `cafcffd2` / UI `403bfdac`; no new collaborator assignment. Earlier checkpoints below are historical.


## Mandatory schema gate — R0

Before a capability needs a model beyond live P24: identify exact live/target migrations; rehearse genuine forward migrations over a representative restored dataset; verify retained history, scope, quantities, constraints and restricted runtime roles; test backup restore and a compatible rollback/fix-forward path. Deploy the matching API and publisher together. Keep each capability off until its own acceptance passes. Never deploy the later model graph onto P24 and assume it is compatible, or edit a historical migration to bypass a failure.

## Ordered milestones

| Milestone | What finishes | Required exit evidence | Current status |
| --- | --- | --- | --- |
| **M0 — Core access, branch scope and release baseline** | PAY-18; CORE-02; CORE-03; CORE-04; CORE-13; AUD-01; AUD-02; AUD-03; AUD-04 | Current baseline matrix; real user/role/scope read/write and revocation tests. | partially deployed |
| **M1 — Shared documents and usable attachments** | BE-04; BE-13; PAY-02; PAY-13 | Real scanner + authorized original-document upload/use/download; infected/stale/denied cases. | not closed |
| **M2 — Vendor/item completeness and shared data operations** | BE-01; BE-02; BE-17; BE-18 | Existing forms persist supported fields; import dry-run/row recovery and scoped data operations work. | partially deployed |
| **M3 — Purchase requests, approvals, funding and line routing** | BE-03; BE-09; PAY-10; CORE-05; CORE-11 | Real request-to-approval and line-routing journeys; no self-approval, double ordering or invented funding. | not closed |
| **M4 — RFQs, bids, evaluation and awards** | BE-11; BE-12 | Request-to-orderable award; sealed bids, original evidence, evaluation and residual-safe split award proof. | not closed |
| **M5 — Purchase orders, delivery, inspection and returns** | BE-06; BE-07; BE-10 | PO issue/revision through partial receiving/service/inspection/return; conserved quantity/value. | not closed |
| **M6 — Bills, matching, credits and manual payments** | BE-08; PAY-01; PAY-03; PAY-04; PAY-05; PAY-06; PAY-07; PAY-08; PAY-12 | Bills/matching/credits/manual settlement reconcile; Finance statuses remain truthful. | not closed |
| **M7 — Contracts, recurrence, communications and reports** | BE-05; BE-15; BE-19; PAY-09; PAY-11; PAY-14; PAY-15; PAY-16; PAY-17; CORE-01; CORE-08; CORE-09 | Contract ceilings, recurrence, delivery receipts and complete-query reports reconcile. | not closed |
| **M8 — Supplier portal and external participation** | BE-14; BE-16; CORE-12 | Real external supplier sessions and cross-supplier denial; or explicit launch deferral. | scope decision required |
| **M9 — Integrated UAT, safe rollout and handover** | AUD-05 | One coherent normal-session UAT cohort, restore/rollout evidence and recorded business acceptance. | not complete |
| **M10 — Optional ERP platform extensions** | CORE-06; CORE-07; CORE-10 | Separate scope/ownership and security acceptance; never silently count absent controls as complete. | future scope decision |

## Work packages and completion checks

Milestone IDs retain the original audit mapping. The [machine-readable execution plan](PROCUREMENT-IMPLEMENTATION-AND-UAT-PLAN-20261025.json) includes every finding exactly once, dependencies, scope decisions and exit criteria. Its original per-finding UI traces and historical audit source pins remain intact.

### M0 — Core access, branch scope and release baseline

Owner: Core and release engineering. Depends on: no prerequisite milestone. Dependencies apply to the affected operation; a pending decision must not stop unrelated defined CRUD.

- Reconcile published ERP/UI main, reviewed branches, deployed product and physical schema; merge only accepted prerequisites before assigning collaborators.
- Complete company/all-branch versus selected-branch behavior across lists, creates, approvals, pickers and exports. Company-wide viewing never invents a posting branch; branch-bound commands require an authorized explicit or configured default branch.
- Use Core organisation currency and permitted headquarters defaults. Verify roles CRUD/readback, current grants, branding/settings ownership and HRM settings-route alignment without requiring an employee link.

Exit checks:

- A current operation/source/schema/UI/live matrix distinguishes implemented, privately verified, deployed and business-accepted states.
- Real-session tests cover assigned/unassigned, organisation-wide, cross-company/tenant, revoked scope, system-user-only and employee-linked accounts.
- Core settings/role edits round-trip without manufacturing or dropping grants; unsupported settings are explicit.

### M1 — Shared documents and usable attachments

Owner: Core Documents and Procurement. Depends on: M0. Dependencies apply to the affected operation; a pending decision must not stop unrelated defined CRUD.

- Complete genuine scanner acceptance and reuse Core upload/storage/download/version/retention services.
- Bind request, supplier bid, invoice, payment and credit evidence to its current authorized business purpose and original document.
- Keep pending, infected, stale, unavailable and unauthorized files unusable; optional evidence remains optional when the applicable policy permits it.

Exit checks:

- Actual upload → scan → authorized attach/retrieve/retire works through existing screens.
- Real scanner and independent owner evidence prove Clean/current-original semantics; UI filenames or mocked scanner results do not qualify.

### M2 — Vendor/item completeness and shared data operations

**Selected CSV private checkpoint (25 October):** [M2 selected-export verification](../release/P24-M2-SELECTED-CSV-PRIVATE-VERIFICATION-20261025.md) passed 116 focused/1,081 Host tests, 93 UI units/23 mocked browser cases and actual normal-cookie PostgreSQL HTTP/two real browser files with independent owner deltas 6/2/1. Full fixed CSV bytes/cells bind typed owner-before rows; same-cookie explicit grant revocation denied 403. That private receipt is historical source-phase evidence; the separate selected-export release above now records deployment. Whole M2, imports, batch/clone, saved views and business UAT remain open.

Owner: Procurement master data and Core data-transfer services. Depends on: M0. Dependencies apply to the affected operation; a pending decision must not stop unrelated defined CRUD.

- Exercise existing vendor/item/contact/site/qualification CRUD and lifecycle in the supplied forms; fix individual gaps before adding alternative forms.
- Complete scoped template download, import mapping, dry-run validation, row errors, duplicate handling, retry-safe commit and recovery using existing shared import infrastructure where it fits.
- Selected-row and All/search CSV exports are deployed; complete approved cloning/batch operations and saved views with server scope.
- Persist selected numbering, precision, terms and supplier approval preferences under their owning policies. Decide duplicate-vendor merge/alias semantics before exposing merge; retain approved transaction history.

Exit checks:

- Create/edit/refresh/state-change workflows persist all supported fields and enforce current permissions, scope and versions.
- Malformed imports and partial failures have recoverable per-row outcomes; replay cannot duplicate records.
- Every enabled import/export/batch/clone control has a real contract and actual browser/API/database proof.

### M3 — Purchase requests, approvals, funding and line routing

Owner: Procurement buying, Core approvals and Finance interface. Depends on: M0, M2; M1 for document-required submission paths. Dependencies apply to the affected operation; a pending decision must not stop unrelated defined CRUD.

- Complete draft → submit → approve/reject/return → revise/resubmit and permitted withdrawal using the shared Core engine and current users.
- Implement active-case recall only after its named permission, assignment and independent reservation-release contracts are settled; use the existing 28-section recall draft. No edit/resubmit while a required release remains unconfirmed.
- Accept line-level direct PO/RFQ routing, partial/cross-request consolidation and remaining-quantity conservation.
- Consume Finance-owned budget contracts; an explicit policy may permit controlled manual funding evidence. Missing Finance must be visible and must never manufacture available balance, reservation or release.

Exit checks:

- A workerless permitted Core user completes the eligible request path in the existing UI.
- Maker/checker, delegation, limits, policy versions, lost responses and decision/cancellation races are enforced and audited.
- Approved quantities cannot be sourced or ordered twice; hard budget controls cannot be bypassed.

### M4 — RFQs, bids, evaluation and awards

Owner: Procurement sourcing and Core approvals. Depends on: M1, M3. Dependencies apply to the affected operation; a pending decision must not stop unrelated defined CRUD.

- Accept invitation/publish/close/reopen/cancel and supplier or surrogate bid capture with genuine original-document evidence.
- Preserve sealed access, bid versions, technical/commercial evaluation and independent award authority.
- Finish split awards, round/source identity, retained reservations and residual capacity; adopt awards into POs without duplicate consumption.
- Verify existing later-model sourcing work before implementing missing code; keep held capabilities off until their exact schema/source/role/document gates pass.

Exit checks:

- An approved request can produce an authorized, orderable award and a linked PO source.
- Actual tests cover late/conflicting bids, sealed access, wrong supplier, stale originals, cross-round over-allocation and repeated commands.

### M5 — Purchase orders, delivery, inspection and returns

Owner: Procurement purchasing/receiving; Inventory/Assets interfaces. Depends on: M3; M4 for award-backed orders and early M7a activation for contract releases. Dependencies apply to the affected operation; a pending decision must not stop unrelated defined CRUD.

- Complete standard/blanket PO creation, approvals, issue/dispatch evidence, acknowledgement, controlled revisions, partial cancellation and closure.
- Complete partial goods receipts, service milestones, inspection, rejection, reversal, return and replacement/credit references.
- Keep receipt and invoice progress separate; deliver accepted quantities to Inventory/Assets through owned contracts. Contract-release purchasing additionally requires M7a contract activation, which can be accepted early after M0/M2 and any required M1 document evidence; it does not wait for all of M7.

Exit checks:

- Original order/receive screens support real partial fulfilment, revisions and recovery without overwriting history.
- Concurrent receipts, returns and invoices conserve authorized quantity/value; over-receipt is policy-controlled.
- Stock and asset ledgers stay with their owning modules; unavailable adapters show truthful pending/unavailable status.

### M6 — Bills, matching, credits and manual payments

Owner: Procurement payables and Finance interface. Depends on: M5; M1 for document-required intake/matching. Dependencies apply to the affected operation; a pending decision must not stop unrelated defined CRUD.

- M6a: Finish existing bill/Inbox intake, manual capture/edit, PO/receipt-to-bill conversion, duplicate detection, two/three-way matching and exception review. OCR/email connectors are separate from controlled manual/file intake.
- M6b: Complete standalone/itemized credits, partial application/unapplication, refunds and compensating corrections with retained evidence.
- M6c: Complete manual payment recording, supplier advances, multi-invoice allocations, excess balances and batch per-line outcomes with independent configured authority and retry recovery.
- M6d: Publish/verify the Finance/AP handoff and callback contract. Keep payment screens and manual records usable under policy while Finance is unbuilt; do not implement a substitute GL, bank execution or fabricated Finance Paid.

Exit checks:

- Bill → match/exception → approval → credit/payment record → remaining balance reconciles on the existing screens.
- Price/quantity/tax differences, duplicates, cross-supplier/currency scope, stale versions, concurrent claims and uncertain acknowledgements have tested outcomes.
- Manual payment evidence and Finance-confirmed posting/payment statuses remain distinguishable; absent adapters are explicit.

### M7 — Contracts, recurrence, communications and reports

Owner: Procurement and shared Core services. Depends on: M2, M5, M6. Dependencies apply to the affected operation; a pending decision must not stop unrelated defined CRUD.

- M7a: Complete signed/authorized contract activation, amendment lineage, framework/blanket drawdown ceilings, expiry and renewal alerts.
- M7b: Execute due recurring commitments exactly once using Core business dates, current policy, retained template versions and pause/resume recovery.
- M7c: Bind shared PDF/print/email templates, internal/supplier-visible comments, scoped sharing, notification delivery and workflow/schedule logs to real business events.
- M7d: Reconcile Home, approvals, spend, commitments, supplier performance, aging and savings reports against full scoped persisted queries; define time/currency/FX and metric owners.
- Keep global branding, organisation, identity, numbering infrastructure and reusable presentation in Core; Procurement settings expose only their owned behavior.

Exit checks:

- A contract release cannot exceed its effective ceiling; recurring children cannot duplicate under retry/catch-up.
- Documents/notifications have authorized recipients and observable outcomes; outbox existence alone is not delivery.
- Dashboard totals, drilldowns and exports reconcile with known source records across the full query.

### M8 — Supplier portal and external participation

Owner: Core external identity and Procurement. Depends on: M1, M4, M5, M6. Dependencies apply to the affected operation; a pending decision must not stop unrelated defined CRUD.

- Keep portal work explicitly planned; confirm launch inclusion before activation rather than silently declaring it unnecessary.
- Implement restricted invitation/login, own-supplier profile corrections, bids, PO acknowledgement, invoice/credit submission, document access and payment visibility.
- Keep remittance verification with its Finance owner and never give suppliers internal ERP accounts as a shortcut.

Exit checks:

- Real external sessions complete selected supplier journeys in the retained portal UI.
- Cross-supplier/company/tenant/attachment access is denied; invitation/token expiry, revocation and recovery are tested.
- If deferred, the deferral and unavailable UI are recorded; it cannot be described as completed portal parity.

### M9 — Integrated UAT, safe rollout and handover

Owner: QA, engineering, operations and business owner. Depends on: M0, M1, M2, M3, M4, M5, M6, M7. Dependencies apply to the affected operation; a pending decision must not stop unrelated defined CRUD.

- Run the selected complete procure-to-pay journeys against one pinned UI/API/schema/configuration cohort with normal sessions and independent database/audit reconciliation.
- Include M8 acceptance if portal is selected. Test negative scope/SoD, partial/concurrent transactions, stale sources, retries, integration outages and recovery.
- Rehearse upgrade/restore/rollback from the live schema, then release accepted capabilities incrementally with backup, monitoring, support ownership and post-deploy smoke tests.
- Record business UAT acceptance and every deferred operation explicitly. Keep Finance/Inventory/Assets adapter readiness separate from Procurement internal completion.

Exit checks:

- Every selected visible action is traceable to behavior, permission/scope, state/rules, persistence, audit and an accepted operation-level test.
- Business UAT and release sign-off are recorded; technical test counts and anonymous page 200 responses do not substitute.
- Exact live capability matrix, schema/API documents, rollback/support runbook and Git release records are published.

### M10 — Optional ERP platform extensions

Owner: Core platform product owner. Depends on: separate scope decision. Dependencies apply to the affected operation; a pending decision must not stop unrelated defined CRUD.

- Separately scope arbitrary custom modules/field designers, executable custom functions/buttons, general developer tooling and expanded integration/backup administration.
- Reuse existing typed preferences now; do not make a generic low-code platform an automatic Procurement prerequisite.

Exit checks:

- Each selected extension has ownership, security/authorization and runtime acceptance before its designer/control is enabled.
- Deferred extensions remain listed explicitly and are excluded only from the agreed launch scope, not claimed as implemented.

## First execution sequence

1. **M0.1:** publish the exact source/deployment/schema matrix and reconcile accepted prerequisites into reviewed shared baselines before assigning collaborator tasks.
2. **M0.2:** finish operational company/all-branch behavior and explicit transaction-branch selection with real current-authority tests.
3. **M1.1:** finish real scanner/document-use acceptance with the existing Core Documents owner.
4. **M2.1:** verify vendor/item field-by-field CRUD in the supplied forms, then complete import preview/mapping/validation/recovery and remaining data operations. This can progress independently of document-required submission paths.
5. Proceed through M3–M7 and selected M8, releasing accepted compatible slices under R0; finish M9 against the integrated release. M10 is a separate optional platform programme.

## One definition of done for every slice

Applicable 28-section specification and operation trace → existing UI and owning domain/API → current permission/data scope and state rules → genuine schema change only if needed → meaningful positive/negative/retry/concurrency tests → actual Playwright/API/PostgreSQL plus independent owner/audit proof → reviewed Git commits/PR evidence → compatible deployment and smoke checks. Record business UAT separately. A heading check, mock, endpoint, outbox row or HTTP 200 alone is not completion.

## Module ownership and decisions

Core owns identity, roles, organisation/default currency, branding, shared workflow, documents, audit and reusable UI. Procurement owns purchasing decisions, commercial records and manual settlement evidence. An employee record is optional for permitted Procurement actions. Finance owns budget balances, AP/GL, bank execution and confirmed financial status; the imported Accounting UI is not its backend. Inventory and Assets own their ledgers.

Missing Finance must not remove manual payment screens or block unrelated draft CRUD. It also cannot authorize fabricated budget availability, reservation release, bank execution or Finance Paid. Required external integrations show explicit queued/unavailable states. Budget/approval policies, organisation-wide versus unassigned branch authority, signature authority, vendor merge semantics and portal launch scope block only their dependent operations; retain them in a named decision register rather than inventing policy.

A proposed scope deferral is not implementation completion. Confirm the launch scope before M9; include supplier portal acceptance if selected. Preserve earlier failed/historical evidence and current ownership reservations. Assign new collaborators only after reviewed prerequisite merges and fresh published ERP/UI baselines; this plan itself assigns no overlapping tasks.
