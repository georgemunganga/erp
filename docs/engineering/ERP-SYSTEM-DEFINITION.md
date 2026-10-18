# ERP system definition and engineering-standard applicability

**Status:** Working engineering record, 18 October 2026. Business and management approvals remain open.
**Standard:** [GW-ENG-STD-001](GW-ENG-STD-001.md)
**System:** GreenWebb ERP, including Core, HRMS and Procurement

## Purpose and boundary

Provide one organisation-aware ERP identity, navigation and shared platform for business modules. Core owns organisation structure, user identity, role grants, data-scope primitives, branding and common configuration. HRMS owns employee and workforce records. Procurement owns supplier procurement profiles and the request-to-invoice-validation lifecycle. Finance owns budgets, accounting, AP and payment; Inventory and Assets own their respective ledgers.

## Tier and rationale

**Tier 3** under GW-ENG-STD-001: employee information, supplier and financial references, high-impact purchase decisions, and tenant/legal-entity boundaries. This classification triggers cross-tenant and privilege tests, threat review, migration/recovery evidence and independent review for high-impact controls. The Engineering Owner and business owner must be named and sign this record before production release of new Procurement workflows.

## Main users and workflows

Employees request purchases; managers and budget owners approve within scope; Procurement sources, selects and orders; receivers accept goods or services; AP validates invoices and Finance posts/pays; suppliers participate through a separately scoped portal. Core administrators manage organisation, access, shared branding and configuration. Existing HRMS users keep HRMS-specific permissions; ERP sign-in alone grants no Procurement or HRMS transaction access.

## Capability assessment

`Required` means design and controls are required for this ERP, not that implementation is complete. `Conditional` means it applies only to enabled features. See module definitions and feature specifications for completion evidence.

| ID | Capability | Applicability | Current evidence / remaining work |
|---|---|---|---|
| C01 | Organisation & tenancy | Required | Core company/branch context exists; verify all new Procurement APIs and child links across tenant/entity boundaries. |
| C02 | Identity & authentication | Required | Shared ERP sign-in exists; document privileged MFA/session policy and supplier identity. |
| C03 | Roles & permissions | Required | Core/HRMS grants exist; Procurement action matrix and server guards remain incomplete beyond draft routes. |
| C04 | Data scope & ownership | Required | Procurement request scope and scoped schema keys exist; test all read/write/export paths. |
| C05 | Domain entities & data | Required | P0–P7 source migrations define records; data dictionary/classification and retention need business review. |
| C06 | Workflow orchestration | Required | 28-section feature drafts exist; authoritative Procurement transitions remain to implement. |
| C07 | States & transitions | Required | Lifecycle states are modelled; APIs must enforce guards and concurrency. |
| C08 | Business rules | Required | Procurement policy decisions and effective versions need confirmed business rules and tests. |
| C09 | Approvals & delegation | Required | Shared workflow integration, limits and maker/checker checks remain a release gate. |
| C10 | Audit & history | Required | Shared audit is available; consequential Procurement transitions need integration and replay proof. |
| C11 | Events & notifications | Conditional | Required for enabled asynchronous workflows; define outbox, consumer idempotency and templates. |
| C12 | Documents & media | Conditional | Required for supplier, sourcing and invoice evidence; scanning, access and retention need shared contract. |
| C13 | API & integrations | Required | Draft APIs exist in source; Finance, Inventory, Assets and supplier callbacks need owned contracts. |
| C14 | Configuration & extension | Required | Core/Procurement settings boundary is documented; effective policies and UI editors need authoritative storage. |
| C15 | Reporting & analytics | Conditional | Required for enabled dashboards; metric definitions, scope, freshness and Finance reconciliation are pending. |
| C16 | User experience | Required | Shared UI system and demo screens exist; live states, accessibility and adapter acceptance remain pending. |
| C17 | Security & privacy | Required | Default-deny and scope design exist; Tier 3 threat model and independent review are pending. |
| C18 | Reliability & operations | Required | Deployment infrastructure exists; Procurement backup, restore, alerts and support runbook are pending. |
| C19 | Testing & quality | Required | Schema, isolated DB and draft tests exist; full permission, negative and UAT coverage remain pending. |
| C20 | Lifecycle & governance | Required | Standard and module specifications are versioned; name owners, review and retain release evidence. |

## Current release position

The Procurement source schema includes P0–P7 migrations and has been tested on disposable PostgreSQL. Production currently has no Procurement tables or configured Procurement database connection. Procurement UI records remain demonstration data, and the live flag is off. The live readiness gate fails closed if ERP identity, company context or schema readiness is missing. New workflow APIs, migrations and UI adapters require the Tier 3 release gates in the standard and [PROC-BE-05](../procurement/PROC-BE-05-LIFECYCLE-SCHEMA-AND-LIVE-INTEGRATION.md).

## Open decisions and approvals

- Name the business/product owner, Engineering Owner, independent reviewer and operations support owner.
- Approve launch companies, budget/approval rules, supplier diligence, retention and data classification.
- Confirm Finance/AP, Inventory, Assets and shared document/workflow contracts.
- Record a threat model, backup/restore targets, migration review and UAT evidence before production activation.
- Record management approval and corporate effective date for GW-ENG-STD-001 separately from this ERP programme's adoption.
