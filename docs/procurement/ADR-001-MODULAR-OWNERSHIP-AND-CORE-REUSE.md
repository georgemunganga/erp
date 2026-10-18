# ADR-001 — Procurement ownership and ERP Core reuse

**Date:** 18 October 2026

**Status:** Proposed for project review; applied to draft implementation

**Scope:** ERP Core, HRMS, Procurement, Finance, Inventory and Assets

**Standard:** [GW-ENG-STD-001](../engineering/GW-ENG-STD-001.md), Tier 3

## Context

The Procurement workspace contains useful screens and forms, while the ERP already has sign-in, organisation settings, roles, branding and several shared services. Copying the UI's local records or creating parallel backend masters would make the system inconsistent. Some services with shared-sounding names are HRMS-specific: the current workflow applies leave rules, and the document service stores employee documents. Reusing those domain behaviors for purchasing would be incorrect.

## Decision

Procurement is a bounded module in the ERP modular monolith. It owns procurement records, rules and transitions in its own Domain, Application and Infrastructure projects. The existing ERP host may compose Procurement routes and adapters, but hosting does not transfer domain ownership to HRMS. The UI is a client of versioned server contracts; it never decides approval, budget, supplier eligibility, receipt, match or payment outcomes.

| Record or capability | Authoritative owner | Procurement contract |
|---|---|---|
| Account, session, tenant membership, selected company, branch, department, default currency, branding, role assignments | Core/Identity/Organisation | Resolve stable IDs and authorized context at request time; show Core settings in the shared shell. Do not maintain copies. |
| Employee status and reporting line | HRMS | Consume the minimum stable employee reference needed for requester and approver assignment. |
| Supplier procurement profile, item/catalog references, plan, request, RFx, contract commercial terms, PO, receipt, invoice validation | Procurement | Own lifecycle, version, audit and scoped APIs. Finance payee/bank records are references, not Procurement fields exposed to ordinary buyers. |
| Budget balance/reservation, tax accounting, AP ledger, payment and bank reconciliation | Finance | Request decisions using idempotent, correlated contracts; show Finance-confirmed status. Never infer payment from a Procurement invoice state. |
| Stock balance and asset register | Inventory and Assets | Publish accepted-receipt handoffs with stable IDs; retain delivery evidence in Procurement. |
| Authentication, notifications, document storage, audit transport, event dispatch | Shared platform | Reuse only via domain-neutral contracts with explicit tenant, entity, actor, audience and retention scope. Procurement owns the business meaning of events and approval evidence. |

The selected Core company is the default legal entity and its configured default currency is the draft currency. User-entered currency, when allowed, is validated against Core/Finance currency policy. No Procurement-specific organisation or currency master is created.

## Integration rules

1. A browser fixture is permitted for design review only. A live action requires an authenticated server API, record scope check and server-owned state transition. A UI success message follows the committed result.
2. Core roles grant named actions; Procurement checks the action **and** tenant, company, record ownership, amount authority and separation of duties at the point of use. A hidden button is not authorization.
3. Shared workflow infrastructure may schedule tasks, delegation and escalation, while Procurement defines approval stages, material changes, policy versions and maker/checker guards. HR leave approval logic is not called for purchases.
4. Supplier, bid, contract and invoice files use a record-document contract that supports supplier-scoped audiences, scanning, versioning and retention. Employee-document APIs do not hold them.
5. Consequential events use an outbox and versioned schemas. Downstream effects require retry, consumer idempotency, dead-letter handling and reconciliation. Notifications never substitute for audit or committed state.
6. Finance/AP, Inventory and Asset integrations reference their owners' records through adapters. Direct writes to another module's tables are prohibited unless a separately reviewed compatibility contract explicitly authorizes them.
7. Module configuration stays under `/procurement/workspace/settings`; Core organisation, branding, users and global settings remain in Core. Shared UI primitives and navigation are reused; domain-specific fields and journeys remain in Procurement.

## Implementation and verification gates

| Gate | Evidence before activation |
|---|---|
| Draft CRUD | Scoped server routes, validation, concurrency/version behavior, tenant isolation, empty/loading/error UI states. |
| Supplier activation, approvals, RFx publication, PO issue | Policy and authority version, maker/checker checks, audit, transition tests and independently reviewed role/scope behavior. |
| Receipt and invoice match | Quantity/value invariants, reversal/exception paths, document evidence, duplicate detection and authorization tests. |
| Finance, Inventory and Asset effects | Agreed owner contracts, idempotency, outbox delivery, failure queue, reconciliation and recovery tests. |
| Production release | GW-ENG-STD-001 Tier 3 threat review, migrations/backup/recovery evidence, UAT and independent review of high-impact controls. |

## Consequences and open work

The separate Procurement project boundary is retained even while ERP Core hosts its routes. Existing draft CRUD can progress with selected-company defaults. The UI reference workspace remains a design source until its adapters use live contracts.

The current Core role editor and auth middleware know Procurement action names. This is composition coupling, not a second authorization database. As more modules join, move action registration behind a Core-owned permission-provider contract so module catalogs can be registered without Core code importing module internals. The existing HRMS workflow, employee-document service, and notification transport need domain-neutral contracts before Procurement uses them. OIDC grants, record-document APIs, outbox dispatch and Finance/AP contracts remain explicit gaps; no financial transition is activated by this ADR.

## Alternatives considered

- Copy the Procurement UI's local data model into Core: rejected because it creates competing records and rules.
- Reuse HRMS leave workflow and employee-document APIs directly: rejected because their business behavior and data audience are HR-specific.
- Split Procurement into a separate deployed service immediately: deferred; independent domain ownership is achievable within the modular monolith and avoids deployment complexity before scale requires it.
