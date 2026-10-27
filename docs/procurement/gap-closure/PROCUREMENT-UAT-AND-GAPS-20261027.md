# Procurement UAT and remaining gaps — 27 October 2026

**Overall decision: NOT READY for whole-module acceptance.** This report supersedes earlier readiness summaries, not retained test and deployment records. Existing screens do not prove connected business workflows. This is an engineering UAT assessment; no business-owner sign-off is recorded.

The audit preserves the imported Procurement UI and shared ERP components. Core owns organisation/company, identity, role grants, record scope, branding and shared platform configuration. A permitted Core user need not be an employee. The organisation is the legal entity and its selected currency is the default; test policy amounts are not approved Amizo purchasing thresholds. Finance owns the ledger and payment execution; its absence does not prevent separately governed manual payment recording.

## Baselines and evidence boundaries

| Surface | Baseline | Meaning |
|---|---|---|
| ERP source | `fcdafac34351b3e0d312b4e49ff949dc2d1c0ede` | M5a merged, including private acceptance and Manus handoff |
| API publication tested for M5a | `20985344252b0b394939cfceb87152d49f39e2e2` | Same backend tree through the ERP baseline; not deployed |
| UI audited initially | `c33b435efe041bb97c7b831be93cd9d8aca05d28` | Includes original UI adapters and M5a acceptance |
| UI UAT corrections | `77f582e` | Test fixtures aligned to current Core identity/branch contract; one view-only message corrected |
| Production backend | `cca4772473b2a87411b3ca59a0a766fbd84187de` | M4 image `sha256:b6dc4e822fd7383e13c186e2bac4d0974dde4fb6922b53e20bf1e5be525f6fff` |
| Production UI | `b51216eaf4af07c3985faef9e8b51145c4bbf92f` | M4; the current M5a source is not a production release |

No production configuration, user grant, business record, supplier message, bank operation or deployment was changed during this audit. Production manual payments, operational approvals and principal direct-order drafts were observed disabled; principal approval execution was enabled. A source capability and a production-enabled capability are different claims.

## Verification completed

- Fresh TypeScript and actor-contract checks, plus **587/587 unit tests**, passed on the current UI work. These are not real-database workflow acceptance.
- Fresh anonymous production Playwright passed **25 route bootstraps**, **five exact deployed asset hashes** and **eight protected API 401 responses**, with no page errors or production writes. These checks cover authentication boundaries and navigation, not signed-in CRUD.
- Retained M5a real-cookie private tests cover request-to-direct-order governance, approval, issue, acknowledgment, cancellation, original-key recovery after a lost response, and concurrent same-key Submit/Issue. Owner checks reconcile receipts, Core decisions, audit/outbox and quantities; historical rows were preserved. All-queue acceptance used one assigned private branch and does not establish multi-branch isolation. See [exact M5a verification](M5A-PRIVATE-UI-VERIFICATION-20261027.json).
- Retained M2a normal-cookie import acceptance covers Vendor creation/quota and Item lost-commit/expiry, with owner reconciliation and all eighteen sessions revoked. The current audit does not rerun that native cohort. See [M2a current-mainline acceptance](M2A-CURRENT-MAINLINE-ACCEPTANCE-20261026.json).
- Retained M4 private tests cover approved-request sourcing conversion and internal RFQ publication/independent review. Supplier invitation delivery, quotations, evaluation and award are not implied by that result. See [M4 acceptance](M4-INTERNAL-RFQ-PUBLICATION-ACCEPTANCE-20261026.json).
- Retained M5a populated migration rehearsal covers upgrade, empty downgrade/upgrade and fourteen evidence-retention downgrade vetoes. Current production-row compatibility and a reviewed release are still required before M5a deployment.

The complete mocked browser suite contains 444 cases. Its first two attempts stopped after eight failures: run01 passed six, run02 passed 85. Failures included obsolete Core workspace/identity fixtures, a selector for an RFQ line hidden behind the existing Items tab, and an incorrect token-refresh precondition. These attempts are retained; their unrun cases are not passes. Corrections change fixture setup and selectors, not server permissions or imported page layout. The final retry is running; its result will be recorded in the accompanying machine-readable checkpoint.

## Capability assessment

“Implemented” below means source exists, not blanket UAT acceptance or production activation. “Gap” means a current-main contract or workflow is missing; held branches are not released capabilities.

| Capability | Current position | Remaining acceptance or implementation |
|---|---|---|
| Core users, roles and branch scope | Shared Core contracts used; current workspace fixture now matches them | Full signed-in branch/All, direct-object denial and revocation matrix across remaining workflows |
| Company currency and settings | Core owns company and currency defaults | Verify each live editor uses defaults; do not duplicate Core settings |
| Vendor drafts and contacts/sites | CRUD and review/activation routes exist | Current-baseline end-to-end CRUD/activation, portal invitation and verification evidence coverage |
| Supplier qualifications | Review routes and validity checks exist | Real verification-owner callbacks, risk scoring and performance assessments |
| Items, categories and methods | Stewardship, CRUD and catalog publication/withdrawal exist | Fresh current-baseline full CRUD and catalog restriction/expiry tests |
| Catalog and guided buying | Existing UI and published catalog references | End-to-end discovery, pricing, non-catalog fallback and scoped access UAT |
| Vendor/Item CSV imports and exports | Create-only imports and scoped exports exist; bounded native import evidence retained | Fresh current-baseline full data/denial/recovery matrix; updates, cloning, saved views and arbitrary bulk actions are not implied |
| Buying policy | Draft, validate, preview, review, publish and return exist | Approved business values; negative/effective-version tests and release configuration |
| Request drafts | Persisted create/read/revise/cancel adapters exist | Full current-baseline CRUD matrix, partial lines and denied/stale cases |
| Budgets | Live queue/history records independently checked Finance references; no duplicate budget ledger | Actual Finance reservation/release reconciliation and budget balances remain external; a recorded check is not a Finance posting |
| Request approval and funding evidence | Core-principal private acceptance retained | Production rollout; real Finance reservation adapter remains separate |
| Request-line conversion | M3b/M4 governed direct-order and RFQ source allocations exist | Full split/consolidation/cancellation and contention coverage |
| RFQ internal publication | M4 private Open/Approve/Return acceptance | Enable only after approved policy; prove actual invitation delivery separately |
| Supplier quotation intake | No current-main complete mapped workflow established | Integrate/review held quotation work; native/API/UI capture, submit, correction, confidentiality and deadline tests |
| Evaluation and award | No current-main complete approved-award path | Evaluator conflict rules, sealed opening, independent scores, award approval, split lines and M5 PO source adapter |
| Standard PO governance | M5a private real-cookie acceptance; not deployed | Reviewed M5a release, branch/revocation/load coverage and business policy activation |
| PO amendments | Initial M5a governance exists | Issued commercial revisions, delta approval, immutable sent versions and supplier acknowledgment |
| Blanket PO and agreement releases | Not established by standard PO support | Limits, validity, releases, consumption, cancellation and concurrency controls |
| Goods and service receipts | Draft and positive command code exists | M5b: Core-user identity and M5 issued-evidence adapter; private complete receipt/inspection/service tests |
| Receipt corrections and returns | Domain models/rules exist; operational correction route not mapped | Reversal/return API/UI, consumption checks, downstream compensation and replacement/credit references |
| Inventory and Assets | Integration boundaries defined | Idempotent accepted-receipt/return hand-off and reconciliation; their ledgers stay outside Procurement |
| Invoice intake | Structured invoice CRUD exists | Native/UI validation, duplicate keys, line edits, cancellation and document evidence UAT |
| Invoice inbox/OCR | Scoped received-invoice list is live; file chooser/drop area currently explains that file intake is unavailable | Controlled intake channels, extraction review, duplicate handling and access-controlled document service |
| Two-/three-way matching | Matching routes exist, capability gated | Current receiving evidence integration, price/quantity/tax tolerances, partials, exception resolution and qualified consumption authority |
| Invoice approval/AP hand-off | Readiness projection deliberately remains Blocked | Procurement validation authority, durable AP receiver/accept/reject contract and reconciliation |
| Manual payments | Complete independently gated command routes exist | Fresh current-baseline native API/UI acceptance, independent actor policy and reviewed activation; not dependent on a built Finance module |
| Payment confirmation | Manual Recorded/ConfirmedManually workflow exists | Test create/edit/submit/approve/record/confirm, return/reject/dispute/cancel, capacity/race/retry and denied actors |
| Bank payment execution | Finance responsibility | Future Finance adapter; a manual external reference is not proof of bank settlement |
| Batch payments | Screen presence is not execution | Define and implement governed batch selection/approval, per-item outcomes and Finance/manual boundary |
| Supplier credits | Invoice-linked draft/read/edit/cancel exists | Approve/apply, governed balance reduction, returns/documents and Finance reconciliation |
| Plans | Draft CRUD exists | Reviewed approval, effective financial period and conversion to actual demand |
| Contracts | Draft CRUD exists | Review/signature/active versions, obligations, renewal, amendments and ceiling/PO drawdown |
| Recurring purchases | Schedule CRUD exists | Occurrence generation, approval, deduplication, renewal and downstream document creation |
| Supplier portal | Capability not mapped/enabled | Supplier identity, own-supplier access, bids/PO/invoice/documents/messages and isolation tests |
| Shared documents | Shared upload/download and retention integration exists | Verified scan/access/retention lifecycle and attachment-required transition tests; do not invent proof files |
| Collaboration | Audit/activity is not record discussion | Comments, mentions, internal versus supplier audience and permissioned sharing |
| Notifications | Durable audit/outbox foundations exist | Actual transport delivery/retry, SLA reminders, recipient scope and failure recovery |
| Analytics | Scoped vendor/item exports exist | Reconciled live spend, savings, performance, aging and budget metrics; loaded rows alone are not enterprise reports |
| First-use setup | Live Home and capability/readiness checks exist | Verify an explicit first-use checklist against persisted organisation, supplier, policy and workflow readiness; Home counts alone do not prove onboarding completion |
| Module preferences | Existing settings shell/adapters | Trace each editable preference to a persisted validated contract; keep global settings in Core |

### Concrete source anchors

- `backend/hrm-api/src/Mightyfin.Erp.Hrm.Api/ProcurementReceivingSourceResolver.cs`: actor requires a canonical worker; source reads legacy `OrderIssuedVersions`, `OrderSubmissions` and `OrderDeliveryEvidence`. This is incompatible with treating every permitted Core user and M5 principal-governance PO as a complete receiving source. Do not bypass the checks: add the owned adapter and preserve original evidence verification.
- `backend/hrm-api/src/Mightyfin.Erp.Hrm.Api/ProcurementReceivingRoutes.cs`: receipt/inspection/service command routes already exist; the gap is not an absent receiving design.
- `backend/hrm-api/src/Mightyfin.Erp.Hrm.Api/ProcurementPrincipalSourcingPublicationRoutes.cs`: internal publication does not establish supplier quotation, evaluated bid or approved award.
- `backend/procurement-api/src/Mightyfin.Erp.Procurement.Application/InvoiceApReadinessProjection.cs`: returns Blocked with missing validation and Finance connection requirements.
- `backend/procurement-api/src/Mightyfin.Erp.Procurement.Infrastructure/Data/ManualPaymentCapacityReader.cs`: structured Received invoices without a Finance posting can qualify for manual payment capacity. Finance/matching are not blanket prerequisites.
- `backend/hrm-api/src/Mightyfin.Erp.Hrm.Api/ProcurementManualPaymentCommandRoutes.cs`: Create/Revise/Submit/Approve/Return/Reject/Record/Confirm/Dispute/Cancel exist; recovery endpoints are in `ProcurementManualPaymentRoutes.cs`. These need independent acceptance, not a replacement payment screen.
- `backend/hrm-api/src/Mightyfin.Erp.Hrm.Api/ProcurementHostIntegration.cs`: distinct feature/readiness gates prevent UI availability from being interpreted as enabled operational authority.

## Ordered completion milestones

| Priority | Milestone | Completion evidence |
|---|---|---|
| P0 | M5a release readiness and rollout | Production compatibility preflight, recoverable backup, exact publication/schema/finite-role verification, independent review, authenticated smoke and rollback record |
| P0 | M5b principal receiving | Existing Manus [complete milestone](../../collaboration/manus/MANUS-M5B-PRINCIPAL-RECEIVING-08.md): current PO to partial goods/service acceptance, inspection, reversals and returns; no mandatory employee link |
| P0 | Manual payment acceptance | Four permitted UAT actors; creator/approver and recorder/confirmer must be independent under the current policy (including worker aliases); real API/UI create through confirmation and negative/capacity/replay cases; explicitly no bank or ledger execution |
| P0 | Matching and invoice authority | Match current accepted quantities, resolve every discrepancy, record validated state, preserve corrections and prove duplicate/retry behavior |
| P1 | Sourcing to award | Integrate reviewed current-baseline quotation/evaluation/award increments and approved-award-to-PO; prove confidentiality and segregation |
| P1 | AP hand-off contract | Durable idempotent queue plus visible pending/rejected outcomes; future Finance owns actual posting/payment. Do not mark Paid without authoritative evidence |
| P1 | Amendments, credits and recurring/contract execution | Connect existing screens to controlled actions and caps; test reversals, period validity and concurrent consumption |
| P2 | Portal, collaboration, advanced reports and automation | Scoped external identities, transport evidence, precisely defined KPIs and reproducible acceptance |

Each milestone must reuse current UI and shared ERP infrastructure, retain the 28-section feature contract and pass the relevant GW-ENG-STD-001 controls. No broad merge of old held branches: compare each with current main, preserve unique work, review migrations and test the resulting exact baseline.

## Final acceptance conditions

Whole-module UAT remains open until the required launch journeys have signed-in real-API/database evidence, denied-actor/branch/tenant tests, conflict/retry recovery, operational ownership and business acceptance. A mocked UI pass, a disabled capability, a draft schema or an anonymous route load is not that evidence. Optional capabilities may be deferred only with an explicit launch scope decision; they must not be labelled complete.

Protected raw evidence is under `/home/amizo/erp-backups/procurement-uat-audit-20261027_run01`, `run02` and `run03`. Do not commit cookies, personal identities, credentials, private environment values or raw business payloads. The prepared manual-payment private cohort package remains inert and unexecuted in this audit; it is not a test pass.
