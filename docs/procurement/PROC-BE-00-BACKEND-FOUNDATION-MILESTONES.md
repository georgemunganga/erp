# PROC-BE-00 — Procurement Backend Foundation and CRUD Milestones

**Document type:** Technical child feature and implementation milestone record

**Status:** P0 foundation implemented in source and validated against an isolated PostgreSQL database; production integration decisions open

**Prepared:** 4 October 2026

**Hierarchy:** ERP → Procurement → Platform and Configuration → Backend Foundation → P0 operations
**Framework:** ERP 28-section feature standard

This technical foundation follows the HRMS backend pattern: ASP.NET Core 10, Domain/Application/Infrastructure/API projects, EF Core migrations, PostgreSQL, tenant-scoped records, append-only audit metadata, and health probes. It has its own `procurement` schema and migration history in the ERP database. The API project is an isolated development/migration host; the adopted [ERP architecture](../00-architecture-position.md) calls for one production deployment with bounded modules. It has **no business CRUD API routes yet** and has not been applied to the live ERP database.

The initial P0 tables do **not** contain every business field. An additive P1–P3 migration now supplies the first Vendor, Item and Purchase Request field model without enabling CRUD. [PROC-BE-01](PROC-BE-01-FIELD-COVERAGE-AND-SCHEMA-GATES.md) records what was added and the remaining gates for each CRUD milestone.

## 1. Feature Identification

`PROC-BE-00` is the backend foundation for `PROC-CFG-01`, `PROC-01`, `PROC-02`, and later child features. Operations: `P0-O1` establish module projects, `P0-O2` create isolated schema/migration, `P0-O3` enforce scoped persistence, `P0-O4` expose health/authenticated metadata, `P0-O5` validate an isolated deployment. The implementation is in [backend/procurement-api](../../backend/procurement-api/README.md).

## 2. Purpose and Business Outcome

Give each Procurement CRUD milestone a consistent database and service boundary before records can be created or changed. An engineer can add a repository and route to an established context rather than choosing table names, tenancy rules, audit behavior, or migration conventions independently.

## 3. Scope and Exclusions

P0 includes the project layout, initial Supplier/Contact, Catalog Item, Purchase Request/Line, Policy Version, Import Batch, Audit Event and Outbox Event tables, scoped EF context, migration, API host, and tests. P0 excludes production deployment, live UI integration, payment/bank records, Finance/Inventory/HRMS masters, supplier approval decisions, import apply endpoints, workflow execution, and business CRUD routes. Later migrations add PO, sourcing, contracts, receiving, invoice, and portal tables with their child feature milestones.

## 4. Parent and Related Features

The parent is Procurement Platform and Configuration in [PROC-00](PROC-00-PROCUREMENT-MODULE-ARCHITECTURE-AND-CAPABILITY-MAP.md). First dependent children are [PROC-01 Supplier Management](PROC-01-SUPPLIER-ONBOARDING-AND-MANAGEMENT.md), [PROC-04 Items](PROC-04-PROCUREMENT-ITEMS-AND-CATALOG.md), and [PROC-02 Purchase Requests](PROC-02-PURCHASE-REQUISITION-AND-LINE-ROUTING.md). Shared identity and organization access must be resolved before their API routes serve real users.

## 5. Actors and Participants

Database migrator applies a reviewed migration; API operator manages configuration and probes; authenticated Procurement user is the future CRUD actor; auditor reads permitted audit events; Finance/HRMS/Inventory remain owners of their records. A public client is never an authority for tenant or legal-entity scope.

## 6. Entry Points and Triggers

The migration command is `--apply-migrations-only`. Runtime probes are `/health/live` and `/health/ready`; authenticated metadata is `/api/procurement/v1/meta`. A future CRUD operation enters only after authentication, entity resolution, permission check, input validation and policy evaluation.

## 7. Preconditions

An ERP PostgreSQL database and approved connection credential exist; a backup/change procedure exists for production; an identity service can issue an authenticated subject and tenant; a legal-entity reference is resolved and authorized; required business policies are approved for the target CRUD child. Development auth is explicitly restricted to the Development environment.

## 8. Workflow

`P0-O1`: create bounded project layers → build. `P0-O2`: generate EF migration → inspect DDL → apply to disposable PostgreSQL → check tables/constraints. `P0-O3`: stamp scope before tracking new records → filter reads by tenant/entity → reject cross-scope update and physical delete → add audit metadata on save. `P0-O4`: start API against migrated DB → check probes and authenticated metadata. `P0-O5`: run tests and no-pending-model-changes check → record evidence. For production, backup → review migration → run migration job → verify → start API → monitor; production execution is a separate release step.

## 9. Status / State Transitions

Foundation gate: `Proposed → Built → Migration validated → Shared host/auth/organization bridge validated → CRUD-ready → Production approved`. Current state is **shared host and scope implemented in source; access gate partially validated**. [PROC-BE-02](PROC-BE-02-SHARED-HOST-ACCESS.md) records the ERP host integration and remaining permission and rollout checks. No business record is activated by P0. Imported vendors later start Proposed; imported items later start Inactive.

## 10. Permissions and Data Scope

Every table carries `tenant_id` and `legal_entity_id`; EF reads use both filters. New records are stamped through `AddScoped` before EF tracks composite keys, and save rejects mismatched scope. Child foreign keys include tenant/entity, preventing a contact or request line from referencing a parent in another scope. Only health probes are anonymous. The metadata route requires an authenticated tenant and legal-entity claim. CRUD milestones must add action-specific permissions and record/field scope. `IgnoreQueryFilters` is prohibited in request handlers unless a separately reviewed administrative path performs its own scope checks.

## 11. Business Rules

P0 rules: no physical delete of Procurement records; audit events cannot be updated; policy values are not seeded or assumed approved; no supplier bank credentials in Procurement foundation; no automatic supplier activation or item orderability; request lines carry distinct requested, approved, allocated, ordered, accepted, and cancelled quantities; requests and line records belong to the same tenant/entity. Later child milestones enforce approval, budget, sourcing, and matching rules.

## 12. Data Fields and Ownership

| Table | Key fields | Owner |
|---|---|---|
| `suppliers`, `supplier_contacts` | Number, legal/display name, country, registration key, status, contacts, Finance supplier reference | Procurement profile; Finance owns AP account and remittance |
| `catalog_items` | Code, name, kind, category, unit, status, tax/inventory/asset references | Procurement catalog reference; Inventory/Assets own their ledgers |
| `purchase_requests`, `purchase_request_lines` | Requester HRMS ID, organization/coding references, currency, status, quantity and value progress | Procurement; HRMS/Organization/Finance own referenced masters |
| `policy_versions` | Key, version, effective dates, state, JSON rules | Procurement policy configuration |
| `import_batches` | Type, file hash, preview expiry, apply result | Procurement data movement |
| `audit_events`, `outbox_events` | Actor/action/correlation and versioned integration payload/idempotency | Procurement business audit and event delivery; shared platform may later centralize storage |

External IDs are stable references, not cross-schema foreign keys. `version` is an optimistic concurrency token. Monetary/quantity columns use decimal precision. The initial schema is not a claim that every field in the child specs has been finalized.

## 13. Screens and Data Views

P0 has no business screen. The existing Procurement frontend remains a browser-data demonstration. Future list/detail/edit screens will call scoped CRUD routes. Operational data movement should show mapping, server preview, per-row result, and export, as described in [PROC-UX-09](PROC-UX-09-SHARED-DATA-TRANSFER.md).

## 14. User Experience / Human Behaviour

Migration is an operator task with a visible success/failure report. Users should see plain-language validation errors from future CRUD services and never a raw database exception. The first business CRUD screen must preserve the frontend's simple Add, Import, Review and Export entry points.

## 15. Notifications and Communications

P0 does not send notifications. The outbox table establishes durable event metadata; a later publisher uses the ERP notification/event service with retry and reconciliation. Migration failure alerts operators through deployment monitoring, not end users.

## 16. Documents and Attachments

P0 stores no binary attachment content. Supplier and purchase-request document references belong in a later document-service integration with scanning, access control and retention. Import batch stores file hash/name and result metadata, not uploaded file bytes.

## 17. Exceptions and Edge Cases

Unknown entity/tenant scope blocks writes; cross-scope update and physical delete throw; duplicate supplier registration, document number, item code, policy version and outbox idempotency key fail database uniqueness checks; negative/zero invalid PR quantities fail a check constraint. A missing migration or database makes readiness fail. A stale concurrency version causes an EF conflict for the future API to map to a user-facing conflict response.

## 18. Integrations / APIs / Events / Sandbox

Database connection uses `ConnectionStrings:Procurement`; migration history is `procurement.__procurement_migrations`. API base is `/api/procurement/v1`. OIDC authority and audience are configurable in the isolated host. The ERP host now composes Procurement routes in source and reuses HRMS login and validated shell selection when configured; action-specific permissions and production rollout remain P0.5 tasks. Events will be inserted transactionally into `outbox_events`; no publisher or external event is enabled yet. Sandbox validation uses disposable PostgreSQL, not the live ERP database.

## 19. Audit / Security / Privacy

The context adds append-only audit metadata on business record saves: record type/ID, action, actor, changed field names and correlation. It does not store sensitive field values in the audit payload. No ordinary role may update audit records. Production needs credential rotation, least-privilege database role, reviewed auth bridge, field masking, export logging, backup/restore verification, and a decision on PostgreSQL row-level security as defense in depth.

## 20. Reports / Dashboards / Metrics

P0 has no business KPI. Operations can track migration version, readiness, import batches, outbox pending/failed age and audit volume once routes are added. Business metrics wait for their child records and exact numerator/denominator definitions.

## 21. Configuration and Localization

No monetary threshold, tax rate, approval route, or default procurement policy is hard-coded. Policy versions carry effective dates and JSON rules for a later validated editor. Store currency codes and normalized numeric values; presentation locale belongs to the UI and shared ERP settings.

## 22. Non-Functional Requirements

Schema migrations are repeatable; ordinary API startup does not alter the database. Tenant/entity filters, composite relationships, optimistic concurrency, idempotency keys, structured logs and probes are foundation controls. SLA, throughput, retention and recovery targets await volume and governance decisions. Production API should use a least-privilege DB role and dedicated migration credential.

## 23. Postconditions

After isolated migration, nine Procurement business tables plus `__procurement_migrations` exist in the `procurement` schema, with no `hrm` schema created by this migration. After API startup, liveness and database readiness respond; authenticated metadata reports `crudEnabled=false`.

## 24. Failure and Recovery

If a migration fails, stop the release, keep the API from serving new routes, restore or forward-fix under the database change procedure, and rerun validation. If the database is unavailable, readiness returns 503. Import apply and outbox retry/reconciliation behavior are later milestones. Do not use a destructive `Down` migration on production data without an approved recovery plan.

## 25. Acceptance Criteria

1. Projects build on .NET 10 without warnings or errors.
2. Migration applies to disposable PostgreSQL and leaves HRMS schema untouched.
3. EF reports no pending model changes.
4. New records receive authenticated tenant/entity/actor before tracking; a cross-scope update and physical delete are rejected.
5. Reads from a different tenant or entity return no rows.
6. Audit metadata is append-only and omits sensitive values.
7. Health probes and authenticated metadata return expected responses.
8. No business CRUD route or unapproved policy is enabled by P0.

## 26. Test Scenarios

Automated tests cover scope stamping, read isolation, cross-scope write rejection, physical-delete rejection and audit immutability. An isolated PostgreSQL migration run checks table ownership, composite foreign keys and quantity constraint. A Development API smoke run checks `/health/live`, `/health/ready` and scoped metadata. Before production CRUD, add integration tests for real authentication, entity switching, permission denial, concurrent versions, duplicate keys, import idempotency, and transactional outbox behavior.

## 27. Ownership / Dependencies

Backend implementation owner: Procurement engineering. HRMS/Identity owner must provide a supported subject/tenant and authorized entity-scope contract, including local-session compatibility or a move to OIDC. Organization/Core owns legal entity and department IDs. Finance owns budgets, AP and payment data. Database operations owns migration and backup gates. Product/Procurement owns policy decisions in each child spec.

## 28. Open Decisions / Assumptions

1. Which shared login mechanism will Procurement use while HRMS runs local sessions? OIDC-only in the isolated host is an implementation scaffold, not an approved production integration.
2. Which service validates legal-entity and branch selection for Procurement users, and how are HRMS branch restrictions reused?
3. Does a supplier have one global identity across legal entities or an entity-scoped profile linked by a group ID?
4. Which supplier and item fields are mandatory at Draft, Proposed, Active and orderable states?
5. Which permission names, data scopes, retention rules and export fields are approved?
6. When should PostgreSQL row-level security and a shared ERP audit service replace or complement module-level controls?
7. What are the production database backup, migration approval, and rollback/forward-fix procedures?
8. Which existing ERP process will compose the Procurement module for production, and how will its independently owned migration run before that host starts?

## Milestone sequence and exit gates

| Milestone | Deliverable | Exit gate |
|---|---|---|
| **P0 — Foundation** | Projects, initial schema/migration, scoped context, audit/outbox tables, API probes, isolated tests | **Implemented and validated in source.** No live DB change. |
| **P0.5 — Shared host and access contract** | Shared host, HRMS-compatible authentication and strict tenant/entity/branch resolver implemented; finish action-specific Procurement permissions and production integration | Architecture and cross-module owners approve contract; denied, confined-scope and real-session tests pass. |
| **P1 — Vendor CRUD** | P1 field tables are migrated; add `PROC-01` draft/propose/read/update/contact list/duplicate review, state transitions and audit/outbox | Section 28 decisions for `PROC-01` resolved; imported rows stay Proposed; no self-approval. |
| **P2 — Item CRUD** | P2 item/catalog fields are migrated; add `PROC-04` item/category/reference list/create/update/activate with eligibility checks | Ownership of inventory/asset/tax references approved; inactive items excluded from buying. |
| **P3 — Purchase Request CRUD** | P3 header/line/allocation fields are migrated; add `PROC-02` draft/submit/revise/cancel/read, line balances and concurrency | Budget/approval contracts agreed; line-level accounting tests pass. |
| **P4 onward** | Approval and budget, sourcing, PO, receiving, invoice/AP, contract and portal children | Each child receives a reviewed schema migration, routes, permissions, events, tests and release evidence before the next. |

The first CRUD milestone can now be designed against a real schema and shared-host scope. It cannot be connected to the live Procurement UI until P0.5 completes action-specific permissions, real-session verification and deployment review, and the relevant child specification's policy decisions are approved.
