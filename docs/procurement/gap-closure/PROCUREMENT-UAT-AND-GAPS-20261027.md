# Procurement UAT and remaining gaps — 27 October 2026

**Overall decision: NOT READY for whole-module acceptance.** This report supersedes earlier readiness summaries, not retained test and deployment records. Existing screens do not prove connected business workflows. This is an engineering UAT assessment; no business-owner sign-off is recorded.

The audit preserves the imported Procurement UI and shared ERP components. Core owns organisation/company, identity, role grants, record scope, branding and shared platform configuration. A permitted Core user need not be an employee. The organisation is the legal entity and its selected currency is the default; test policy amounts are not approved Amizo purchasing thresholds. Finance owns the ledger and payment execution; its absence does not prevent separately governed manual payment recording.

## Latest bounded invoice-replacement evidence — 30 October

The existing correction form saved a replacement and recovered a deliberately lost acknowledgement. Subsequent flags-off original-UI checks passed for both invoices' Overview/History and original-key recovery: seven real-cookie reads, six screenshots, no page errors or business writes, and confirmed logout. Independent owner reconciliation retained all original data and the saved correction, allowing only its exact authentication cohort. Backend `f1e5102e` passed 1,752 Procurement and 167 focused API tests; UI `9c56b492` retains 228 focused checks and eight mocked browser cases. See the [M6E implementation record](M6E-CONTROLLED-INVOICE-REPLACEMENT-20261030.md) for the separate failed attempts and actual private evidence boundaries. Temporary-grant cleanup and production release remain open; whole-module acceptance is still **NOT READY**. The M7 sourcing-to-award roadmap remains the next functional workstream, beginning with isolated quotation validation and draft snapshots.

## Current development update — 30 October

Core currency publication, invoice capture/recovery, manual review and one qualified comparison now have bounded private original-UI and native-owner evidence. ERP `d54d01ec` / UI `8b91313b` created exactly one quantity-1 allocation for ZMW 5.12 while preserving the older Blocked result. Backend checks passed 1,678 Procurement tests and 80 focused Host tests; UI type checking, 85 focused tests and build passed. See the [dated checkpoint](M6D-PRIVATE-CURRENCY-CAPTURE-CHECKPOINT-20261030.md) for exact scope and retained failures.

The initial feature-off harness stopped on a wrong expected error code; its three denied requests are retained separately. Continuation completed the remaining two denials, 19 history reads and original-UI history checks with zero business writes. Native reconciliation passed; all owned processes stopped and temporary grants were restored with business history preserved. Controlled invoice replacement has tested source and a genuine generated migration in draft ERP PR138, with typed adapter/recovery work in UI PR96; native migration/readiness and complete Bill-form integration remain open. M6D production release and whole-module acceptance remain open. Published main baselines remain ERP `1c6a81d7` and UI `47cac6a1`.

The production database was separately restarted after a disk-full shutdown using its existing image and volume; the API is healthy. This recovery changed no application deployment, schema or permission policy. Whole-module acceptance remains **NOT READY**.

## Current development update — 29 October (retained history)

Published main baselines are ERP `1c6a81d7` and UI `47cac6a1`. Draft M6C backend `0f656d63` passed 1,592 Procurement tests, 671 host tests and one focused regression. The [private workflow checkpoint](M6C-PRIVATE-WORKFLOW-CHECKPOINT-20261029.md) records original-UI manual unit review, independent Core decision, original-key recovery, native history reconciliation and a combined retained-data downgrade veto. These are private acceptance results, not a production release.

The actual comparison saved a **Blocked** result for a four-decimal invoice header under a two-decimal matching policy. No qualified allocation or Finance posting was created. Currency-aware new invoice calculation and controlled correction after review history are explicit remaining gaps; neither is solved by changing tolerances or replacing the original UI. Whole-module acceptance remains **NOT READY**. The sections below retain their dated historical scope.

## Release update — 28 October (retained history)

The [M6A private invoice-history checkpoint](M6A-INVOICE-HISTORY-PRIVATE-ACCEPTANCE-20261028.md) passed original-key Open recovery and independent Core approval on backend source `6287bd22`, with native owner and fixture-restoration proof. Its first 503 and committed Open201/readback409 remain separate failed evidence. This source is pending PR #133; published ERP main remains `536f4c77`. The baseline records history only: qualified invoice matching, Finance posting, original invoice-history UI, production activation and whole-module UAT remain open.

The [observed restore-readiness API patch](M5-RESTORE-READINESS-PRODUCTION-20261028.md) is also deployed: source6087, 174 focused Host checks, private Service API/owner acceptance and a new read-only canary/public smoke passed. It changed no schema, flags, permissions, publisher or UI. A later [bounded private original Service UI journey](M5-SERVICE-ORIGINAL-UI-PRIVATE-ACCEPTANCE-20261028.md) passed real-cookie Create recovery, Revise, independent Certify and owner reconciliation; its first two harness failures remain separate. Wider UAT remains open.

The initial [M5 technical rollout](M5-PRODUCTION-TECHNICAL-RELEASE-20261028.md) deployed API product414 (subsequently updated above), original UI47cac6a, 220 tables, fresh backup/restore/full migration rehearsal and independent production data/permission reconciliation passed. Ten public route checks, five exact UI file hashes and three unauthenticated Playwright renders passed. Existing business flags are preserved; whole-module acceptance and signed-in production CRUD remain open. The 27 October baselines and results below are retained history.

## Original 27 October baselines and evidence boundaries

| Surface | Baseline | Meaning |
|---|---|---|
| ERP source | `fcdafac34351b3e0d312b4e49ff949dc2d1c0ede` | M5a merged, including private acceptance and Manus handoff |
| API publication tested for M5a | `20985344252b0b394939cfceb87152d49f39e2e2` | Same backend tree through the ERP baseline; not deployed |
| UI audited initially | `c33b435efe041bb97c7b831be93cd9d8aca05d28` | Includes original UI adapters and M5a acceptance |
| UI UAT corrections | `720b108` product / `ba25a24` final tests | Current Core fixtures, permission-based guidance and two navigation race fixes; final checks completed |
| Production backend | `cca4772473b2a87411b3ca59a0a766fbd84187de` | M4 image `sha256:b6dc4e822fd7383e13c186e2bac4d0974dde4fb6922b53e20bf1e5be525f6fff` |
| Production UI | `b51216eaf4af07c3985faef9e8b51145c4bbf92f` | M4; the current M5a source is not a production release |

No production configuration, user grant, business record, supplier message, bank operation or deployment was changed during this audit. Production manual payments, operational approvals and principal direct-order drafts were observed disabled; principal approval execution was enabled. A source capability and a production-enabled capability are different claims.

## Verification completed

- Fresh Node 22.23.3 TypeScript, four actor-contract checks, production build and **587/587 unit tests** passed on UI source `720b108`. These are not real-database workflow acceptance.
- Fresh anonymous production Playwright passed **25 route bootstraps**, **five exact deployed asset hashes** and **eight protected API 401 responses**, with no page errors or production writes. These checks cover authentication boundaries and navigation, not signed-in CRUD.
- Retained M5a real-cookie private tests cover request-to-direct-order governance, approval, issue, acknowledgment, cancellation, original-key recovery after a lost response, and concurrent same-key Submit/Issue. Owner checks reconcile receipts, Core decisions, audit/outbox and quantities; historical rows were preserved. All-queue acceptance used one assigned private branch and does not establish multi-branch isolation. See [exact M5a verification](M5A-PRIVATE-UI-VERIFICATION-20261027.json).
- Retained M2a normal-cookie import acceptance covers Vendor creation/quota and Item lost-commit/expiry, with owner reconciliation and all eighteen sessions revoked. The current audit does not rerun that native cohort. See [M2a current-mainline acceptance](M2A-CURRENT-MAINLINE-ACCEPTANCE-20261026.json).
- Retained M4 private tests cover approved-request sourcing conversion and internal RFQ publication/independent review. Supplier invitation delivery, quotations, evaluation and award are not implied by that result. See [M4 acceptance](M4-INTERNAL-RFQ-PUBLICATION-ACCEPTANCE-20261026.json).
- Retained M5a populated migration rehearsal covers upgrade, empty downgrade/upgrade and fourteen evidence-retention downgrade vetoes. Current production-row compatibility and a reviewed release are still required before M5a deployment.

The complete mocked browser suite contains 444 cases. Its first two attempts stopped after eight failures: run01 passed six, run02 passed 85. Failures included obsolete Core workspace/identity fixtures, a selector for an RFQ line hidden behind the existing Items tab, and an incorrect token-refresh precondition. These attempts are retained; their unrun cases are not passes. Corrections change fixture setup and selectors, not server permissions or imported page layout. Run03 was deliberately interrupted after detecting an unsupported Node 20 runtime; its partial results are not a supported-runtime full-suite pass. Run04 on Node 22 retained 261 passes, eleven failures, one interrupted case and 171 unrun cases. Run05 reran all affected/unrun files: 218/227 passed, nine failed. These retries exposed two real navigation races, not only fixture problems: late validation could create a policy after leaving Settings, and an RFQ response could force navigation after leaving its editor. The existing failing regression tests are preserved; route guards now check the active URL as well as lifecycle/context. Run06 ran all 444 cases on product `720b108`: **443 passed; one failed solely on an ambiguous stale-order status selector**. A one-line test-only correction (`ba25a24`) was followed by a full affected-file rerun: **20/20 passed**. All 444 scenarios have passing current evidence; this is explicitly a full run plus targeted rerun, not one uninterrupted 444-pass run. The application and all other test dependencies are byte-identical across that final correction. [Machine-readable checkpoint](PROCUREMENT-UAT-CHECKPOINT-20261027.json) retains exact sources, reports and failed attempts.

## Real payment acceptance and limits

The private test used four separately permissioned Core users with **no employee links**. It captured one structured invoice for ZMW 10, created a payment request for 4, revised it to 5, submitted, independently approved, recorded an external observation and independently confirmed it. The result is **ConfirmedManually**, while the invoice remains **Received** with a null Finance posting reference. This is not a bank payment or ledger posting.

Owner verification reconciled six decision/receipt pairs and event keys, two real approval cases and date observations, preserved original business records and the source database, and confirmed zero active test sessions. Original-key reads, identical Record replay, wrong-approver403 and an over-capacity409 passed. Native UI forms, alternate transitions, multi-branch isolation and concurrent capacity remain unaccepted by this bounded test.

A release preflight must verify **both** Record and Execution database-role contracts. Execution readiness alone did not prove policy authoring or invoice capture: initial attempts caught missing named Record privileges. Those were corrected only in the isolated test profile, with source/old-row/role reconciliation; no production role or flag was changed. Do not copy fixture role names or its ZMW 100 synthetic policy into business configuration.

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
| Request approval and funding evidence | Core-principal private acceptance retained | M5 technical rollout complete; business activation/native UI acceptance and real Finance reservation adapter remain separate |
| Request-line conversion | M3b/M4 governed direct-order and RFQ source allocations exist | Full split/consolidation/cancellation and contention coverage |
| RFQ internal publication | M4 private Open/Approve/Return acceptance | Enable only after approved policy; prove actual invitation delivery separately |
| Supplier quotation intake | No current-main complete mapped workflow established | Integrate/review held quotation work; native/API/UI capture, submit, correction, confidentiality and deadline tests |
| Evaluation and award | No current-main complete approved-award path | Evaluator conflict rules, sealed opening, independent scores, award approval, split lines and M5 PO source adapter |
| Standard PO governance | M5a private real-cookie acceptance; technical source deployed in M5 | Native UI/branch/revocation/load acceptance and approved business policy activation |
| PO amendments | Initial M5a governance exists | Issued commercial revisions, delta approval, immutable sent versions and supplier acknowledgment |
| Blanket PO and agreement releases | Not established by standard PO support | Limits, validity, releases, consumption, cancellation and concurrency controls |
| Goods and service receipts | M5b Core-principal adapter merged; private Goods API/native-owner accepted. M5c company-date fix merged with regression/provider evidence | Private Service API and original UI accepted for one UTC, one-branch real-cookie case; non-UTC native and multibranch/concurrency acceptance remain |
| Receipt corrections and returns | M5d original UI and proposal CRUD published with private normal-cookie/API-owner acceptance, including write-off history; proposal does not alter balances | Native UI/broader scope acceptance; independent execution/consumption/dispatch and negative-fact reconciliation; replacement/credit references |
| Inventory and Assets | Integration boundaries defined | Idempotent accepted-receipt/return hand-off and reconciliation; their ledgers stay outside Procurement |
| Invoice intake | Structured invoice CRUD exists | Native/UI validation, duplicate keys, line edits, cancellation and document evidence UAT |
| Invoice inbox/OCR | Scoped received-invoice list is live; file chooser/drop area currently explains that file intake is unavailable | Controlled intake channels, extraction review, duplicate handling and access-controlled document service |
| Two-/three-way matching | Matching routes exist, capability gated | Current receiving evidence integration, price/quantity/tax tolerances, partials, exception resolution and qualified consumption authority |
| Invoice approval/AP hand-off | Readiness projection deliberately remains Blocked | Procurement validation authority, durable AP receiver/accept/reject contract and reconciliation |
| Manual payments | Six-command normal-cookie native API happy path and database-owner reconciliation passed on source209 | Actual UI-form acceptance, alternate outcomes, concurrency/multiple branches and reviewed activation; Finance is not a prerequisite |
| Payment confirmation | Native API reached PaymentRecorded then ConfirmedManually; identical Record replay, original-key reads, wrong-approver403 and over-capacity409 passed | Owner verified exact graph, old data and zero active sessions; test Return/Reject/Dispute/Cancel, stale/changed-payload and concurrent claims; no bank settlement claim |
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
| P0 | M5 technical rollout complete; business acceptance open | Fresh backup/restore, full migration rehearsal, preserved203 data, schema/finite-role/canary and paired UI rollout verified; authenticated CRUD and remaining workflow acceptance still required |
| P0 | M5b principal receiving | Existing Manus [complete milestone](../../collaboration/manus/MANUS-M5B-PRINCIPAL-RECEIVING-08.md): current PO to partial goods/service acceptance, inspection, reversals and returns; no mandatory employee link |
| P0 | Manual payment acceptance | Four permitted Core users without employee links completed the native API happy path; finish real UI-form/alternate/concurrent cases. Native owner verification passed. Policy requires creator/approver and recorder/confirmer independence, including worker aliases; no bank or ledger execution |
| P0 | Matching and invoice authority | Match current accepted quantities, resolve every discrepancy, record validated state, preserve corrections and prove duplicate/retry behavior |
| P1 | Sourcing to award | Integrate reviewed current-baseline quotation/evaluation/award increments and approved-award-to-PO; prove confidentiality and segregation |
| P1 | AP hand-off contract | Durable idempotent queue plus visible pending/rejected outcomes; future Finance owns actual posting/payment. Do not mark Paid without authoritative evidence |
| P1 | Amendments, credits and recurring/contract execution | Connect existing screens to controlled actions and caps; test reversals, period validity and concurrent consumption |
| P2 | Portal, collaboration, advanced reports and automation | Scoped external identities, transport evidence, precisely defined KPIs and reproducible acceptance |

Each milestone must reuse current UI and shared ERP infrastructure, retain the 28-section feature contract and pass the relevant GW-ENG-STD-001 controls. No broad merge of old held branches: compare each with current main, preserve unique work, review migrations and test the resulting exact baseline.

## Coverage still needed before release acceptance

- Signed-in production/staging CRUD and workflow smoke on the exact release: anonymous page loading cannot replace it.
- At least two real assigned branches plus an unassigned branch, cross-company/tenant direct IDs, role revocation, delegation expiry and concurrent quantity/capacity consumption. The existing one-branch All test is deliberately bounded.
- Complete document scanner/access/retention and transport retry evidence, including the independently assigned scanner work; no upload or delivery success is inferred from a reference field.
- Keyboard/accessibility review and mobile validation across the affected forms, not only Home viewport checks; locale, currency and company-date boundaries where amounts or deadlines change behavior.
- Volume/latency targets, recovery/restore, monitoring and independent Tier 3 security review appropriate to the release. Earlier bounded migration and restore records retain their specific baselines; they are not a new release sign-off.

## Final acceptance conditions

Whole-module UAT remains open until the required launch journeys have signed-in real-API/database evidence, denied-actor/branch/tenant tests, conflict/retry recovery, operational ownership and business acceptance. A mocked UI pass, a disabled capability, a draft schema or an anonymous route load is not that evidence. Optional capabilities may be deferred only with an explicit launch scope decision; they must not be labelled complete.

Protected raw evidence is under `/home/amizo/erp-backups/procurement-uat-audit-20261027_run01`, `run02` and `run03`. Do not commit cookies, personal identities, credentials, private environment values or raw business payloads. The original manual-payment preparation package remains inert. Private successors supplied independently checked finite grants and four Core users, followed by real API testing. Earlier policy-authoring and invoice-capture attempts failed closed on missing test-profile grants. The invoice’s unconfirmed result was resolved as absent with quiescent owner checks before retry; its original number/body and the already published policy were reused. API03 passed without a browser launch and stopped with normal logout. Owner05 independently verified the exact invoice/payment/receipt/Core/date/event graph, old-ID rows and stable user fields, captured schema/table ACLs, source/global roles and zero active sessions. It preserves the scope limit that historical column/schema/function/default ACL and trigger-body inventories were not independently captured. Read-only owner03/04 failures were retained and corrected for JSONB typing and source-supported pre-cookie login-audit attribution; no financial command was replayed. Retain all failed attempts, including the post-commit ACL verifier failure and its separate successful owner reconciliation. The final checkpoint records exact outcomes.
