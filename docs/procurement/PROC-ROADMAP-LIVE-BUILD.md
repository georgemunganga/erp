# Procurement live implementation roadmap

**Status:** Active engineering roadmap, 18 October 2026. Business policies and release approvals remain open.
**Standard:** [GW-ENG-STD-001](../engineering/GW-ENG-STD-001.md), Tier 3; every child uses the 28-section feature specification.
**UI contract:** Existing Procurement workspace and `docs/procurement-ui-contract-register.md` in the Procurement UI repository; do not create a second UI.

The [Core dependency audit](PROC-CORE-DEPENDENCY-AUDIT.md) records which shared services can already be reused and which HRMS implementations are still module-specific.
The [workflow reference trace](PROC-WORKFLOW-REFERENCE-TRACE.md) ties Zoho's help journeys to the child features and ERP ownership boundaries. Use ERPNext as a secondary public implementation example for complex data/control decisions.

## Current baseline

The shared ERP host has authenticated, tenant/entity-scoped draft CRUD for suppliers, items, purchase requests, plans, categories, methods, contracts and sourcing events. The Procurement database has P0–P9 source migrations; P9 adds cancellation history for sourcing and contract-rate lines. These migrations are validated on disposable PostgreSQL, not production. The UI still uses demonstration records, and the live gate holds transaction screens. Core supplies sign-in and organisation context; local sessions now resolve configurable Procurement action grants from Core tenant roles. OIDC role mapping, workflow assignments, document upload, Finance and Inventory/Asset contracts require completion or verification. No purchase commitment, AP posting, payment or supplier activation is enabled merely by a table or screen.

**Company and currency decision:** New Procurement records use the company selected in Core. Its configured currency is shown in the Procurement context and fills a new supplier, request or plan draft when no currency is supplied. An existing draft keeps its stored currency during an edit that omits currency, even if the company default changes. The first company in the user's setup is Amizo Power; no company ID or currency is hard-coded into Procurement. This follows the distinction in [ERPNext Global Defaults](https://docs.frappe.io/erpnext/global-defaults) between a suggested company/default currency and a document's transaction currency. Spending thresholds, budget actions and supplier activation rules remain company-owned configuration decisions; draft CRUD does not imply approval.

## Order of implementation

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

## Progress register

| Area | State on 18 October 2026 | Next acceptance evidence |
|---|---|---|
| Source schema P0–P9 | Implemented and isolated DB tested | Review additive migration against existing data; staging upgrade and restore |
| ERP identity/entity context | Implemented in shared host; local Core roles can grant or revoke named Procurement actions | Real-session, OIDC mapping and cross-company/API access tests |
| Vendor, item, PR draft API | Implemented in source. The cloned workspace has live vendor, item and own-PR draft adapters with mocked Playwright acceptance. Nine mapped HTTP tests now exercise real Procurement routes, Core company/worker scope middleware, action policies and scoped in-memory persistence for vendor, item, PR, plan and sourcing drafts. Vendor drafts can be submitted for review only with category, contact and business reason. A distinct authorized reviewer can return a submitted vendor with a reason; the API returns a scoped action flag for the reviewer detail screen. The proposer can revise, resubmit or withdraw it. Qualification details can be added to drafts with expiry validation and version control; they lock during review and do not constitute approved evidence. Decisions are recorded at each step and orderability stays false. PR revision preserves removed-line history without duplicating newly added lines in the response, and the list counts active lines. | Production-like local-session/OIDC and PostgreSQL HTTP tests, production migration/release review; assigned reviewer queue, document evidence and supplier diligence/approval/activation, PR submit and catalog publication |
| Planning draft API | Implemented in source with P8 additive migration; own-plan list/detail/create/revise/cancel. Empty database and P7 data upgrade tested on disposable PostgreSQL; an existing line became `Active`. Mapped HTTP tests cover create, currency default, action and own-record denial, plus revision with cancelled-line history, active-line count, stale version and cancellation using in-memory persistence. | PostgreSQL HTTP integration tests, Finance period reference, existing planning UI adapter |
| Category and method draft API | Implemented in source; list/detail/create/update with scoped codes and version checks | Scoped HTTP tests, policy publication authority, existing Settings adapter |
| UI live readiness | Implemented; off by default | Per-screen capability and API acceptance, then enable individually |
| Contract drafts | Own-record list/detail/create/revise/cancel API in source, with supplier eligibility and Core company currency defaults; no signature or activation | Scoped HTTP and concurrency tests; signed-document/approval contract; existing Contracts screen adapter |
| Sourcing drafts | Own-record list/detail/create/revise/cancel API in source with line cancellation history; no PR-line link, publish, invitations, bids or award. Mapped HTTP tests cover draft create, currency default, action and own-record denial using in-memory persistence. The cloned RFQ screen has a live draft adapter and mocked Playwright create/edit/cancel/denied/stale coverage. | PostgreSQL HTTP/concurrency tests; approved-demand conversion and publish policy; production migration/release review |
| Policy and supplier decisions | Schema only | Draft/decision routes and rule tests |
| Approval through payment | Schema only | Complete waves 2–6 in order |
| Portal, performance, analytics | Schema/read-model design only | Complete wave 7 and security/KPI tests |

This roadmap is a sequence of implementable increments, not a claim that any undecided policy is approved. Update the progress register with actual evidence as each increment lands.
