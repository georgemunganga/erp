# PROC-BE-05 — Lifecycle schema and live integration gates

**Status:** Schema implemented in source and applied to disposable PostgreSQL; business transitions, UI adapters and production migration are not enabled
**Prepared:** 18 October 2026
**Hierarchy:** ERP → Procurement → Backend and integration → Lifecycle persistence → P4–P6
**Standard:** [GW-ENG-STD-001](../engineering/GW-ENG-STD-001.md), Tier 3; ERP 28-section feature format

## 1. Feature Identification

`PROC-BE-05` extends the existing P0–P3 draft foundation with Procurement-owned sourcing, contracts, orders, receipts, invoice-validation, planning, portal membership and operational-control records. It is a **schema/integration milestone**, not authorization to transact in production.

## 2. Purpose & Business Outcome

Every visible Procurement transaction must have an authoritative record, owner, lifecycle, scoped API, and durable evidence before the browser demo can become a live ERP workflow. This milestone supplies the persistence spine and the ordered release gates.

## 3. Scope & Exclusions

The additive `P4P6CommercialLifecycle` migration creates 27 tables; `P7PlanningPortalAndControls` adds nine planning, portal membership, performance, collaboration, category/method and setup-assessment tables. They do not create Finance AP, payments or budget balances, Inventory stock, Asset register, HRMS employees, shared document binaries, shared identities, or a new approval engine. They do not activate any business transition. The new P7 records still need reviewed child APIs and rules before their live screens can be enabled.

The disposable PostgreSQL run contains **56 Procurement tables including migration history, 69 foreign keys and four applied migrations**. A cross-tenant sourcing child insert was rejected by its composite foreign key. These counts are evidence for the schema, not for business API readiness.

## 4. Parent & Related Features

The parent is [PROC-00](PROC-00-PROCUREMENT-MODULE-ARCHITECTURE-AND-CAPABILITY-MAP.md). Child definitions are PROC-01 through PROC-14. Earlier backend gates are [PROC-BE-00](PROC-BE-00-BACKEND-FOUNDATION-MILESTONES.md) through [PROC-BE-04](PROC-BE-04-LIVE-IDENTITY-AND-COMPANY-CONTEXT.md). Core organization, identity, permissions, workflow, audit, documents, numbering and Finance contracts are dependencies.

## 5. Actors & Participants

Requester, procurement buyer, sourcing owner/evaluator, supplier, contract owner, PO approver, receiver/inspector, service owner, AP processor, Finance integration and auditor. Core resolves login, company/branch and roles; the Procurement API enforces each action and record scope.

## 6. Entry Points & Triggers

UI routes: Vendors, Items, My Requests, Approvals, Get Quotes, Contracts, Purchase Orders, Purchase Receives, Bills, Credits, Settings and Analytics. API entry remains `/api/procurement/v1`. Transition endpoints are **not yet implemented** beyond draft Vendor/Item/PR CRUD.

## 7. Preconditions

Approved company/tenant mapping, active worker where an employee acts, Core role/permission decision, reviewed policy version, migrated Procurement database, and a reconciled Finance/Inventory/Document contract where the workflow depends on one. A visible button does not satisfy these conditions.

## 8. Workflow

Core company/session → draft supplier/item/request → policy, budget and approval → source/award or contract → PO issue/revision → goods/service acceptance → invoice intake/match → Finance AP hand-off → Finance payment outcome → closure. A request line may route to different downstream records. Every transition validates the current state and version, persists business record and audit atomically, and uses outbox/idempotency for external effects.

## 9. Status / State Transitions

The new tables default to draft/pending/received states. They do **not** imply automatic progression. The relevant child feature owns the allowed transition matrix. In particular: RFx bids are sealed until authorized opening; signed contract versions remain immutable; issued POs change by revision; accepted receipts reverse through adjustments; invoices cannot become Paid without a Finance callback.

## 10. Permissions & Data Scope

Every new row inherits `tenant_id`, `legal_entity_id`, actor metadata and version, with EF query filters. Child foreign keys include tenant/entity to prevent cross-company references. Live APIs must additionally check action permission, branch/department/assigned/own scope, field classification, delegated authority and separation of duties. No new business routes are exposed by this migration.

## 11. Business Rules

Orderable supplier, approved source demand, valid contract/award, budget authority, PO tolerance, receipt limits, invoice duplicate key, match tolerance and self-approval restrictions are enforced by later domain/application transitions. Unique indexes already constrain document numbers, version/line keys, supplier invitation, bid submission version, invoice duplicate key and hand-off idempotency. Policy amounts and tolerances remain unset until the business approves them.

## 12. Data Fields & Ownership

The following table maps the prepared UI and the source schema. The new normalized records are in `CommercialRecords.cs`; P0–P3 records are in `Records.cs`.

| UI/workflow | Procurement-owned record(s) | External owner/reference | Current status |
|---|---|---|---|
| Vendors and catalog | Supplier, contacts/sites/qualification/decision; CatalogItem/Entry | Core org, Finance supplier/AP ref, documents, Inventory item | P1–P3 schema + draft API |
| My Requests | PurchaseRequest/Line/Allocation, PolicyEvaluation, BudgetCheckSnapshot | HRMS worker, Core org, Finance budget, shared workflow | P1–P3 schema + own draft API |
| Approvals | Approval instance reference and policy snapshots | Shared workflow owns tasks, delegation, decision history | Contract/decision API pending |
| RFx and awards | SourcingEvent/Line/Invitation, SupplierBid/Line, BidEvaluation, SourcingAward/Line | Supplier portal identity, shared documents/notifications | New schema; API pending |
| Contracts | SupplierContract/Version, ContractRate | Signed document/e-sign reference; Finance recurring payable | New schema; API pending |
| Orders | PurchaseOrder/Line/Revision, SupplierOrderAcknowledgement | Finance commitment, supplier delivery channel | New schema; API pending |
| Receiving | GoodsReceipt/Line, ReceiptInspection, ServiceAcceptance, SupplierReturn | Inventory stock/return, Asset register | New schema; API pending |
| Invoice checks | SupplierInvoice/Line, InvoiceMatchResult, InvoiceException, FinanceInvoiceHandoff, FinancePaymentStatusRef, SupplierCreditNoteRef | Finance AP, payment and credit accounting | New schema; API pending |
| Budgets/payments/recurring bills/batch payments | Procurement references or read models only | Finance owns balances, liabilities, schedules and execution | Finance contract pending; no Procurement ledger |
| Planning | ProcurementPlan/Line | Core period/department, Finance budget | New schema; API pending |
| Supplier portal and performance | SupplierPortalMembership, SupplierPerformanceReview/Criterion | Core identity, shared document/notification | New schema; portal auth and score rules pending |
| Collaboration and first use | ProcurementComment, ProcurementSetupAssessment, ProcurementCategory/Method | Core audit/identity; business policy versions | New schema; audience and readiness APIs pending |
| Core settings | Organization, branding, users, roles, branch access are existing Core/HRMS surfaces; shared defaults include placeholders | Core owns common settings and number series | Partial; verify each API before use |

Each commercial row has ID, tenant/entity, creator/updater, timestamps and optimistic version. Amounts use decimal precision and currency; dates are stored separately from display locale. New child records have scoped foreign keys; external references are stable IDs/strings without cross-schema ownership.

## 13. Screens & Data Views

The reference UI currently renders records from browser-local `AppRecord` fixtures and local storage. Live adapters must map typed API DTOs rather than submit the generic `meta` bag as a business record. Keep feature-specific list, detail and editor views, but show only server-authorized actions and authoritative status. Use direct URL checks and permissioned search/export.

## 14. User Experience / Human Behaviour

Retain the simple labels and layout already prepared. Replace demo actions incrementally with real loading, empty, validation, stale edit, forbidden and unavailable states. Preserve unsaved form input on network or version failure. Do not claim save, approval, receipt or payment until the owner API confirms it.

## 15. Notifications & Communications

The shared notification service owns delivery and retry. Procurement emits facts through its outbox after a committed transition. RFx invitations, PO dispatch and supplier-facing messages need delivery status; a failed email does not erase the business decision.

## 16. Documents & Attachments

`attachment_refs` links to the shared document service. A filename in local UI state is not an uploaded or scanned document. RFx bid evidence, signed contracts, delivery notes, inspections and invoices need upload, scanning, version, access scope and retention contracts before live use.

## 17. Exceptions & Edge Cases

Handle duplicate invoice keys, late/surrogate bids, rejected supplier, expiring contract, material PO revision, over-receipt, disputed service, failed inspection, partial invoice, AP rejection, unknown payment outcome, repeated callbacks and cross-company IDs. Existing obligations remain visible when a supplier is suspended.

## 18. Integrations / APIs / Events / Sandbox

The shared HRMS host already maps Procurement context and draft Vendor/Item/PR routes. Its `/meta` source now reports schema readiness and individual capability flags rather than implying that all Procurement workflows are available. Production currently has **no `procurement` tables** in its PostgreSQL database and the running API has **no Procurement connection configured** (read-only check, 18 October 2026). The UI must therefore remain demo/holding mode. Later APIs follow the order in the release plan below and use correlation IDs, idempotency keys, bounded retries, outbox delivery and reconciliation. Isolated migration testing uses a disposable PostgreSQL database, never live credentials.

## 19. Audit / Security / Privacy

The context writes a scoped audit event on record creation/update and prevents physical deletion. Evaluations, match results, payment status snapshots and acknowledgements are append-only to application roles. The next API milestones must enforce maker/checker, supplier isolation, cross-tenant denial, restricted tax/payment references and permissioned exports. The Tier 3 threat review and independent release review are still required.

## 20. Reports / Dashboards / Metrics

Analytics is a read model, not an accounting ledger. Define numerator, denominator, time window, currency conversion, source and freshness for every KPI. Spend and paid totals must reconcile to Finance posting/payment references; stock must reconcile to Inventory.

## 21. Configuration & Localization

Core owns organization, users, roles, branding, common currency display, numbering and communication defaults. Procurement owns effective-dated policy, categories, sourcing methods, approval conditions and tolerances. UI settings that still open placeholder editors must not appear as active rules. Localize money, dates, time zones and UOM at presentation boundaries.

## 22. Non-Functional Requirements

Bounded pagination, optimistic concurrency, server scope filters, indexed business keys, backup/recovery, health checks, request tracing, API timeouts, accessible keyboard/mobile states and retention are required. Peak volumes and recovery objectives await owner decisions.

## 23. Postconditions

This milestone leaves production unchanged. The source can represent the commercial lifecycle, but no new user action is enabled. A live transition will be complete only after authoritative persistence, audit, side effects and owner acknowledgement as required by its child spec.

## 24. Failure & Recovery

Validation preserves form data; stale versions return conflict; external failures remain pending/retryable with correlation; Finance rejection returns to an owned exception queue; migrations need backup/roll-forward planning. A client never falls back from failed live API calls to fixtures.

## 25. Acceptance Criteria

1. New migrations add only 36 reviewed lifecycle/operational tables and scoped keys; P0–P3 records survive an upgrade.
2. Empty disposable PostgreSQL applies all four Procurement migrations and reports no pending model changes.
3. Cross-tenant/entity reads, writes and child links are denied by API and database constraints.
4. Append-only decision/outcome snapshots cannot be rewritten by normal application writes.
5. Live UI surfaces are enabled by capability only after matching API, state, scope, audit, failure and acceptance paths pass.
6. Finance payment execution and Inventory stock balances remain outside Procurement.

## 26. Test Scenarios

Source build, Procurement tests, disposable migration and schema inspection are mandatory for this milestone. Later route tests include unauthorized direct URL, stale version, duplicate invoice, late bid, self-approval, cross-company child ID, partial receipt, repeated Finance callback, AP rejection and document failure. Run keyboard/mobile and full chain UAT before release.

## 27. Ownership / Dependencies

Procurement engineering owns schema and business APIs; Core owns identity/org/permissions/workflow/documents/numbering; Finance owns budget/AP/payment; Inventory/Assets own their ledgers; product and business owners approve policy and UAT. A distinct reviewer must inspect the Tier 3 migration and financial controls before production deployment.

## 28. Open Decisions / Assumptions

Confirm launch companies/currencies; supplier diligence and approval matrix; item canonical owner; sourcing thresholds and opening authority; contract signing and ceiling rules; budget reservation contract; receipt tolerance; invoice/tax/match rules; Finance/AP interface; Core role/scope grants; document retention; data volume, backup and recovery targets. These decisions gate transitions, not further isolated design work.

## Incremental live release sequence

1. Complete Core permission/scope and shared number/document contracts; review, back up and apply P0–P7 migrations; configure Procurement connection and health in a non-production ERP environment.
2. Wire Vendor, Item and own PR draft list/detail/editor to their existing APIs with capability gating and conflict/denial states.
3. Implement PR submit, policy/budget check, shared approval and line routing with tests; then enable those actions.
4. Implement sourcing and award, contract versions, PO and revisions, receiving/service/returns in dependency order.
5. Implement invoice validation/matching, AP hand-off, callback reconciliation and payment visibility. Supplier portal, planning, performance and analytics follow their own ownership and security gates.
6. Repeat the chain in staging with UAT, authorization and recovery evidence before production migration or switching off demo mode.

### Confirmed integration owner — 22 October 2026

The product owner confirmed Finance/AP and payments will be owned by the
ERP Finance module, which has not been built. Existing unavailable owner
contracts remain truthful until that module provides authenticated budget,
reservation/release, historical exposure and AP status interfaces. No
Procurement ledger or synthetic successful Finance response substitutes
for those contracts. Procurement's own permitted CRUD and non-financial
evidence workflows continue independently.
