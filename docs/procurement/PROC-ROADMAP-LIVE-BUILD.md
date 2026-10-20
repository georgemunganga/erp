# Procurement live implementation roadmap

**Status:** Active engineering roadmap, updated 20 October 2026. Business policies and release approvals remain open.
**Standard:** [GW-ENG-STD-001](../engineering/GW-ENG-STD-001.md), Tier 3; every child uses the 28-section feature specification.
**UI contract:** Existing Procurement workspace and `docs/procurement-ui-contract-register.md` in the Procurement UI repository; do not create a second UI.

The [Core dependency audit](PROC-CORE-DEPENDENCY-AUDIT.md) records which shared services can already be reused and which HRMS implementations are still module-specific.
The [workflow reference trace](PROC-WORKFLOW-REFERENCE-TRACE.md) ties Zoho's help journeys to the child features and ERP ownership boundaries. Use ERPNext as a secondary public implementation example for complex data/control decisions.

## Current baseline

The shared ERP host has authenticated, tenant/entity-scoped draft CRUD for suppliers, items, purchase requests, plans, categories, methods, contracts, sourcing events and buyer-owned purchase orders. Production runs the isolated Procurement schema through P19 and a Procurement-enabled API/UI; see [the live enablement record](PROC-OPS-02-LIVE-ENABLEMENT-20261019.md). The live workspace uses API-backed draft screens and empty workflow states for unsupported routes. Core supplies sign-in and organisation context; local sessions resolve configurable Procurement action grants from Core tenant roles. Supplier, item, catalog price and buying-method activation and buying-rule publication have independent review controls. A vendor approver with read and approve grants can inspect submitted or suspended suppliers in the selected legal entity without a manage grant; individual task assignments are not yet implemented. Submitted requests pin non-authoritative policy evidence to their current version; Finance can record an external reservation reference, but no ERP Finance reservation, delegated approval, purchase commitment, AP posting or payment is enabled. Vendor draft contacts, addresses and pending qualifications support reasoned, retained removal. Plan and sourcing revisions reject duplicate saved line IDs in their shared validators; plan, sourcing and contract draft changes record before/after business audit evidence. P18 adds retained line deactivation for buyer-owned purchase-order drafts; approval and issue remain gated. P19 persists PO branch scope and confines buyer access to the selected authorized branch. P17 adds a normalized supplier-invoice duplicate key; the first invoice slice only records structured manual intake as `Received`, with no production source-document upload, matching, AP posting or payment. Source now includes a Core-owned invoice document adapter behind an off-by-default capability; 58 Core tests, PostgreSQL replay/concurrency checks and 36 mapped Procurement HTTP tests passed. Frontend release verification and production activation remain pending. Read-only HRMS approver candidates are visible, but Core approval cases remain disabled. OIDC role mapping, binding workflow assignments, document upload, Finance and Inventory/Asset contracts require completion or verification.

**Company and currency decision:** New Procurement records use the company selected in Core. Its configured currency is shown in the Procurement context and fills a new supplier, request or plan draft when no currency is supplied. An existing draft keeps its stored currency during an edit that omits currency, even if the company default changes. The first company in the user's setup is Amizo Power; no company ID or currency is hard-coded into Procurement. This follows the distinction in [ERPNext Global Defaults](https://docs.frappe.io/erpnext/global-defaults) between a suggested company/default currency and a document's transaction currency. Spending thresholds, budget actions and supplier activation rules remain company-owned configuration decisions; draft CRUD does not imply approval.

## Order of implementation

The current safety increment is [invoice branch isolation, issue 27](https://github.com/georgemunganga/module-connect/issues/27), plus [shared shell headers, issue 28](https://github.com/georgemunganga/module-connect/issues/28). The frontend header change is deployed after TypeScript, 17 scope/readiness unit checks and the exact staged production build passing all 57 mocked Playwright scenarios. Issue 28 is closed. The API scope/default-currency change and P20 migration are committed on `codex/procurement-live-integration`; 35/35 mapped HTTP tests and the twice-applied restored-database migration rehearsal passed. The production API/database release remains pending shared-host compatibility coordination under issue 29. The next shared dependency is [Core document evidence](CORE-DOC-01-SHARED-DOCUMENT-EVIDENCE.md), with supplier invoice source files as the first adapter.

Before the next shared-host release, retain the live HRM loan model and migration. A two-library payroll overlay produced a verified missing-method failure; production was recovered to the consistent v36 image. Track source reconciliation in [issue 29](https://github.com/georgemunganga/module-connect/issues/29). The new GitHub verification workflow cannot run while the account billing lock remains; its presence is not passing test evidence.

| Wave | Features and operations | Backend deliverable | UI release gate |
|---|---|---|---|
| 0 — Shared control | Core entity/branch scope, tenant grants, numbering, document refs, audit/outbox dispatcher, health, test harness | Scoped contracts and denial tests; deployable migrations; operator runbook | No demo fallback in live mode; shared shell context verified |
| 1 — Masters and demand | PROC-CFG-01 categories/methods/policies; PROC-01 supplier draft/proposal/diligence/decision; PROC-04 items/catalog; PROC-05 plan drafts; PROC-02 PR drafts and line accounting | Draft APIs first, then governed submit/publish/approval; versioned decisions and audit | Vendor, Items, Plans, My Requests screen adapters, each enabled independently |
| 2 — Authority and budget | PROC-03 need/funding approvals, delegation, policy evaluation and Finance budget reservation | Workflow task and decision API, maker/checker, effective policy, idempotent Finance contract | Approvals and budget states only after Finance test double and real contract agree |
| 3 — Sourcing and contracts | PROC-06 RFx/invitations/bids/opening/evaluation/award; PROC-07 contracts, amendments and releases | Sealed bid access, surrogate evidence, award guard, signed version/ceiling accounting | RFx, compare, contract screens with supplier isolation and negative tests |
| 4 — Commercial commitment | PROC-08 PO draft/approve/issue/acknowledge/revise/close, blanket releases | PR line allocation, supplier eligibility, budget commitment, numbered revision and dispatch idempotency | Orders screen; no issue without approved source and commitment |
| 5 — Fulfilment | PROC-09 goods/service receipt, inspection, reversal, return | Remaining quantity/value control, append-only acceptance, Inventory/Asset events | Delivery and service screens with partial/over-receipt cases |
| 6 — Invoice to Finance | PROC-10 intake/duplicates/credit refs; PROC-11 matching/exceptions/AP handoff/payment status | Invoice version, two/three-way result, exception owner, idempotent AP event and callback reconciliation | Inbox/Bills/Payment Status; no payment execution in Procurement |
| 7 — External and intelligence | PROC-12 portal; PROC-13 risk/performance; PROC-14 comments, audit export, analytics | Supplier identity boundary, audience checks, score evidence and governed read models | Portal and dashboards after object/supplier isolation and KPI reconciliation |

## Implementation rule for every operation

1. Confirm the exact child operation in its 28-section spec: entry, state, actor, permission and data scope, guards, owner and failure path.
2. Add or verify schema constraints and a scoped API contract. Use explicit status transitions and optimistic version checks. Define external effects as durable outbox entries with stable idempotency keys.
3. Test allowed and denied actors, cross-tenant/entity object IDs, stale versions, duplicate requests, partial/exception flows and a disposable database migration. Do not let a browser record authorize a financial action.
4. Connect the existing UI screen to the API and verify loading, empty, error, conflict, success and permission-denied states. Keep each remaining demo screen visibly separate.
5. Record migration, review, backup/restore, staging UAT, monitoring and support evidence before production enablement.

## Contracts that block specific transitions

| Contract/decision | Blocks | Safe work that continues |
|---|---|---|
| Procurement thresholds, supplier diligence and delegated authority | Activation, submit/approve, RFx publish/award, PO issue | Draft validation, list/detail/edit, policy editor and test fixtures |
| Finance budget reservation and AP/payment callback | Budget approval, PO commitment, AP handoff and payment status | Internal estimates, invoice capture, exception drafting and contract tests |
| Shared document scanning/access and external supplier identity | Evidence-based approval and portal publication | Internal records and attachment-reference contracts |
| Inventory/Asset item mapping and receipt tolerances | Stock/asset handoff and over-receipt | Receipt draft/inspection evidence and integration outbox contracts |
| Production backup, migration review, threat model, independent security review and UAT | Production activation | Source implementation and disposable/staging verification |

## 19 October 2026 checkpoint

Supplier proposal, review and activation; reasoned category/item and buying-method activation; independently reviewed catalog price publication/withdrawal; request submission to `PendingPolicyAndBudget`; and an append-only Finance budget decision API have reached production through v18. P11 links a request line to the selected published catalog price/version and revalidates availability at submission. P12 records buying-rule review/publication evidence; P13 pins a line-by-line rule result to each submitted request version. The Finance budget result is evidence from a designated reviewer, not an implemented Finance reservation integration. A rule match is not approval. Shared workflow approvals, sourcing awards, purchase orders, receiving, invoice matching, AP hand-off and the supplier portal remain future waves. The following register preserves the earlier 18 October baseline for traceability.

## Progress register

### 20 October 2026 approval execution candidate

The production baseline above is unchanged: API v36, Procurement schema through P19,
with approval execution disabled. The backend integration branch
`codex/procurement-approval-transaction` now contains P21 immutable submission evidence,
P22 independently activated finite spending authority, and the shared Core approval
transaction coordinator. Its purchase-request execution candidate includes case opening,
assigned queues, decisions, retained history, source outcomes, and controlled manual
Finance reservation-release confirmation. These are source capabilities, not a live release.

Verification of the combined execution source at `416b320` passed all 196 Release
Procurement tests. The shared-host regression suite and real HTTP/PostgreSQL probe are
still running. Earlier P22 SQL was applied twice successfully to a disposable PostgreSQL
database; authority concurrency and whole-transaction rollback probes passed. Execution
and outcome model changes still need P23, migration rehearsal, least-privilege database
verification, and real API browser acceptance before activation.

Independent review is correcting employee-alias independence checks and preserving
immutable worker identity evidence so staff offboarding does not require reactivating an
old account to finish a reservation release. Assignment evidence validation and available
approval actions are also being tightened before P23 is generated. These corrections
have not yet passed the combined runtime gate.

The staged frontend at `ac67352` passed 76 mocked Playwright cases. Its successor
`fcdd53e` preserves Finance confirmation milliseconds and has passed TypeScript and
the staged production build; focused browser verification and the newly authored real
API browser flows are pending. Production assets have not been replaced with this
candidate. Track the bounded approval milestone in
[issue 9](https://github.com/georgemunganga/module-connect/issues/9).

Purchase-order execution is the next isolated source increment: approved direct-buy
request-line allocations, explicit method execution routes, finite PO authority, retained
commercial evidence and supplier eligibility. Sourcing awards, order issue/delivery,
receiving, invoice matching, AP handoff and portal workflows remain incomplete; draft
CRUD or a durable outbox row does not establish those operational capabilities.

| Area | State on 18 October 2026 | Next acceptance evidence |
|---|---|---|
| Source schema P0–P9 | Implemented and isolated DB tested | Review additive migration against existing data; staging upgrade and restore |
| ERP identity/entity context | Implemented in shared host; local Core roles can grant or revoke named Procurement actions | Real-session, OIDC mapping and cross-company/API access tests |
| Vendor, item, PR draft API | Implemented in source. The cloned workspace has live vendor, item and own-PR draft adapters with mocked Playwright acceptance. Ten mapped HTTP tests now exercise real Procurement routes, Core company/worker scope middleware, action policies and scoped in-memory persistence for vendor, item, PR, plan, sourcing and contract drafts. Vendor drafts can be submitted for review only with category, contact and business reason. A distinct authorized reviewer can return a submitted vendor with a reason; the API returns a scoped action flag for the reviewer detail screen. The proposer can revise, resubmit or withdraw it. Qualification details can be added to drafts with expiry validation and version control; they lock during review and do not constitute approved evidence. Decisions are recorded at each step and orderability stays false. PR revision preserves removed-line history without duplicating newly added lines in the response, and the list counts active lines. | Production-like local-session/OIDC and PostgreSQL HTTP tests, production migration/release review; assigned reviewer queue, document evidence and supplier diligence/approval/activation, PR submit and catalog publication |
| Planning draft API | Implemented in source with P8 additive migration; own-plan list/detail/create/revise/cancel. Empty database and P7 data upgrade tested on disposable PostgreSQL; an existing line became `Active`. Mapped HTTP tests cover create, currency default, action and own-record denial, plus revision with cancelled-line history, active-line count, stale version and cancellation using in-memory persistence. | PostgreSQL HTTP integration tests, Finance period reference, existing planning UI adapter |
| Category and method draft API | Implemented in source; list/detail/create/update with scoped codes and version checks. Mapped HTTP coverage verifies manager-only writes, buyer reads, selected-company isolation, duplicate codes, locked codes and stale revisions. The existing Settings shell now has a typed live adapter for these drafts, with three mocked Playwright cases. | Policy publication authority, effective rules and setup readiness |
| UI live readiness | Implemented; off by default | Per-screen capability and API acceptance, then enable individually |
| Contract drafts | Own-record list/detail/create/revise/cancel API in source, with supplier eligibility and Core company currency defaults; no signature or activation. Mapped HTTP tests cover unavailable supplier, own-record scope, revision conflict and cancellation while the draft version remains non-active. The live workspace now has a typed contract adapter using shared UI components; five mocked Playwright cases cover create/edit/cancel, denied list, stale edit, no eligible supplier and supplier search. | PostgreSQL HTTP/concurrency tests; signed-document/approval contract; production migration and live-session verification |
| Sourcing drafts | Own-record list/detail/create/revise/cancel API in source with line cancellation history; no PR-line link, publish, invitations, bids or award. Mapped HTTP tests cover draft create, currency default, action and own-record denial using in-memory persistence. The cloned RFQ screen has a live draft adapter and mocked Playwright create/edit/cancel/denied/stale coverage. | PostgreSQL HTTP/concurrency tests; approved-demand conversion and publish policy; production migration/release review |
| Policy and supplier decisions | Schema only | Draft/decision routes and rule tests |
| Approval through payment | Schema only | Complete waves 2–6 in order |
| Portal, performance, analytics | Schema/read-model design only | Complete wave 7 and security/KPI tests |

This roadmap is a sequence of implementable increments, not a claim that any undecided policy is approved. Update the progress register with actual evidence as each increment lands.
