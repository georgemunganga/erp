# GREENWEBB TECHNOLOGIES

## Universal Systems Engineering & Development Standard

**Document ID:** GW-ENG-STD-001
**Version:** 1.0 — Proposed for corporate adoption; adopted for this ERP programme
**Issued:** 18 October 2026
**Corporate effective date:** Upon management approval
**Document owner:** Engineering & Technical Leadership
**Approver:** Managing Director / delegated technical authority
**Classification:** Internal — controlled document
**Review cycle:** Every six months and after a material architectural, regulatory, or security change

> Every system must have a clear purpose, a defined domain model, explicit access and ownership rules, controlled behaviour, testable outcomes, and an operable production environment. Complexity is introduced only when the problem requires it.

This is the repository's working copy of the standard supplied by the product owner on 18 October 2026. It governs new ERP and Procurement design and implementation now. Corporate-wide approval and its effective date remain to be recorded by management.

## 1. Purpose and language

This standard defines minimum engineering rules, reusable architectural building blocks, development workflow, and release controls for GreenWebb software. It applies to websites, mobile applications, business systems, ERP/HRMS/CRM, marketplaces, portals, integrations, SaaS, financial applications, internal tools, and AI-enabled products. It governs design and verification rather than a particular programming language.

- **MUST:** mandatory when relevant; an exception needs recorded approval.
- **SHOULD:** expected practice; justify deviations in the design record.
- **MAY:** optional when requirements and risk justify it.
- **N/A:** explicitly not applicable, with a reason where assessed.

Do not implement authentication, tenancy, workflow engines, or other substantial capabilities where the product genuinely does not need them. The Engineering Owner remains accountable for employee, contractor, partner, and AI-assisted contributions. Stronger laws, contracts, and security obligations take precedence.

## 2. Project classification

Controls are cumulative. Tier 2 includes Tier 1; Tier 3 includes both.

| Tier | Typical system | Minimum controls |
|---|---|---|
| 1 — Simple / low risk | Public content site or small non-sensitive tool | Purpose, scope, ownership, source control, responsive/accessibility review, secure configuration, relevant tests, deployment/rollback and basic monitoring. Authentication, workflow and tenancy apply only when used. |
| 2 — Business / transactional | HRMS, CRM, procurement, booking, marketplace | Tier 1 plus entity model, access design, classification, API contracts, transaction rules, consequential audit, backup/recovery and integration/acceptance tests. Add workflows, approvals and notifications when needed. |
| 3 — High risk / regulated / multi-tenant | Finance, payroll, healthcare, sensitive SaaS | Tier 1 and 2 plus threat model, elevated authorization tests, segregation, encryption/key procedures, recovery objectives, stronger logging, independent review, incident and change controls. |

Sensitive data, material financial impact, or multiple customer organisations can raise a small project to Tier 3. The Engineering Owner MUST record the tier and rationale. **This ERP programme is Tier 3:** it handles employee and financial procurement data and has tenant/entity boundaries. The system definition must record the approving owner and any more precise risk assessment.

## 3. Design principles

1. Describe actors, journeys, entities and decisions before screens and tables.
2. Default deny, least privilege, explicit data boundaries and secret/personal-data protection.
3. Separate domain responsibilities and shared infrastructure; do not create services for their own sake.
4. Centralize proven cross-cutting capabilities rather than speculative abstractions.
5. Define record owner and lifecycle explicitly.
6. Test material calculations, permissions, validations and transitions.
7. Domain/database transactions are authoritative; notifications and downstream integrations are secondary effects.
8. Make production observable, supportable, migratable, backed up and recoverable.
9. Trace requirements, decisions, changes, tests, releases and incidents.
10. Keep solution cost proportional to risk.

## 4. Reference architecture

| Layer | Owns | Must not own |
|---|---|---|
| Experience | Pages, forms, navigation, accessibility, client state and API presentation | Final authorization and authoritative business decisions |
| Application / use cases | Commands, queries, orchestration, transactions and applied policies | HTTP rendering or database schema decisions |
| Domain / business modules | Entities, invariants, state machines, events and calculations | Email vendors, hosting or UI formatting |
| Platform / shared capabilities | Identity, permissions, audit, messaging, documents, integration adapters and configuration | Module-specific business decisions |
| Infrastructure & delivery | Database, queue, storage, deployment, monitoring, secrets and backup | Business rules that only infrastructure operators can change |

A modular monolith is acceptable and often preferred until scale, team ownership, isolation or availability justify services. Code boundaries matter more than deployable count. Identity differs from an employee/customer/vendor record; authentication differs from authorization; a role differs from data scope; a workflow differs from a status column; a notification differs from an audit event.

## 5. Reusable capability assessment

Mark each capability **Required, Conditional, or N/A** in the System Definition.

| ID | Capability | Minimum design contract |
|---|---|---|
| C01 | Organisation & tenancy | Organisation, branch, department, membership and segregation; mandatory for multi-tenant systems |
| C02 | Identity & authentication | Identity types, login, recovery, sessions, service accounts and MFA decision |
| C03 | Roles & permissions | Resources, actions, role grants, privileged roles and policy evaluation |
| C04 | Data scope & ownership | Own/team/branch/tenant scope, accountable owner and reassignment |
| C05 | Domain entities & data | Dictionary, identifiers, relationships, constraints and retention |
| C06 | Workflow orchestration | Steps, assignments, triggers, escalation, exceptions and re-entry |
| C07 | States & transitions | States, guards, side effects and terminal outcomes |
| C08 | Business rules | Validation, calculations, effective dates, versions and overrides |
| C09 | Approvals & delegation | Levels, limits, segregation, delegation and timeout |
| C10 | Audit & history | Actor, action, target, time, result, changed fields and reason |
| C11 | Events & notifications | Schemas, recipients, channels, retry, deduplication and templates |
| C12 | Documents & media | Upload, type/size, scanning, access, versioning and retention |
| C13 | API & integrations | Contracts, authentication, errors, idempotency and ownership |
| C14 | Configuration & extension | Hierarchy, change authority, flags and custom fields |
| C15 | Reporting & analytics | Metric definitions, scope, freshness and reconciliation |
| C16 | User experience | Navigation, feedback, validation, accessibility and responsive layout |
| C17 | Security & privacy | Threats, classification, minimisation, encryption, secrets and retention |
| C18 | Reliability & operations | Logging, alerts, backup, recovery, deployment and support owner |
| C19 | Testing & quality | Risk-based automated checks, UAT and release evidence |
| C20 | Lifecycle & governance | Repo ownership, ADRs, versioning, review, change control and handover |

## 6. Foundational implementation rules

### Identity, authorization and scope

Where accounts exist, define human and service principals, account state, recovery, sessions/tokens and links to domain records. Use established credential storage, not reversible passwords. Evaluate MFA for Tier 3; privileged access SHOULD require it. Protected actions MUST be checked server-side at the point of use. Frontend hiding is only usability. Decisions consider **principal, action, resource, and context/scope**. Use default deny, least privilege, direct-URL/API checks, explicit grants, record scope and bounded delegation. Test cross-tenant and unauthorized-object access for Tier 3.

### Domain, workflows and approvals

Every persisted entity MUST define ID, required attributes, relations, owning module, creator, accountable owner, classification, retention and lifecycle. Enforce important consistency in domain/application code and database constraints where appropriate. Do not substitute a generic JSON field for ownership or validation.

For workflows, define trigger, entry data, actors/assignments, states, transitions, guards, side effects, exception/retry/escalation paths, terminal outcome and reconciliation. A material transition MUST validate current state and authority, persist atomically and produce durable audit. Retries MUST not duplicate financial effects. A generic engine is optional; explicit transitions are acceptable. Name, own and test material rules. Version and date rules whose behaviour changes over time. Define approval authority, sequence/quorum, delegation, absence coverage, self-approval restrictions, overrides and escalation; enforce segregation in software where required.

### Events, APIs and integrations

Events describe completed facts and have stable versioned schemas. Consequential integrations use at-least-once delivery, idempotent consumers, bounded retries, recovery/dead-letter handling and monitoring. Prefer a transactional outbox where record/event consistency matters. Notifications do not determine the underlying transaction outcome unless delivery is an explicit business condition. Document API owner, version, request/response, auth, scope, validation, pagination, errors, timeouts, retry, deprecation and export protection. Mutating endpoints SHOULD accept idempotency when retries can cause adverse effects. Direct cross-module database access requires explicit ownership and change approval. Plan reversible migrations where feasible and compatibility where rollback is unsafe.

### UX, security and operations

Every user-facing feature SHOULD cover happy, loading, empty, validation, failure, success and denied states. Keep terminology and interaction consistent; make required fields clear and destructive actions confirmable/recoverable. Target WCAG 2.2 AA unless an alternative is documented. Classify data, minimise collection, limit access, set retention, protect secrets outside source, use encrypted transport and sensitive storage as needed. Exclude personal data and credentials from logs, analytics and AI prompts unless explicitly authorised. Threat review covers object access, privilege escalation, injection, CSRF, upload, exports and webhooks. Tier 3 requires a threat model, privileged audit, recovery testing and independent security review. Before production, name owner, environments, DNS, configuration, secrets, dependencies, dashboards, alerts, escalation, backup, restore and rollback. Financial processes need reconciliation and exception queues.

## 7. Standard module contract

Every substantial module MUST have a Module Definition recording: module ID and owners; purpose and exclusions; entities/relationships; actions/permissions/data scope; state machines and workflows; business rules; events/integrations; screens/API operations; audit/privacy/retention; tests and operating requirements. This is a design record, not a mandated runtime plugin framework. HR leave approval and Procurement purchase approval can share authorization/workflow/audit patterns without sharing domain rules or data.

## 8. Standard feature specification and gates

Before active implementation, define feature reference and owner, outcome, actors/entry points, data and ownership, authorization/scope, workflow and rules, interfaces, side effects, non-functional needs, acceptance tests and release considerations. Small work may fit an issue; high-risk work needs a separate specification. The ERP's existing 28-section feature template expands this minimum and remains in force.

**Definition of Ready:** objective, acceptance and dependencies are known; high-risk controls identified; discovery ambiguity documented. **Definition of Done:** acceptance met, relevant tests and security/scope checks pass, review complete, schema/API docs updated, migrations and monitoring addressed, user-facing states verified, deployment and owner acceptance recorded where required.

## 9. Delivery lifecycle

| Stage | Exit evidence |
|---|---|
| 0 Intake & classification | System Definition, tier/rationale, accountable owners |
| 1 Discovery & product design | Journeys, backlog, entity/role/flow maps |
| 2 Architecture & planning | Module boundaries, contracts, threat review, ADRs, environment plan |
| 3 Implementation | Traceable code, tests, review, integration and docs |
| 4 Verification & UAT | Functional, authorization, failure, accessibility and risk evidence |
| 5 Release & handover | Migration, backup, deployment, smoke, rollback and support record |
| 6 Operate & improve | Monitoring, incidents, changes and maintenance backlog |

One person may fill multiple lean-team roles, but high-impact financial/security changes require a distinct reviewer or documented independent verification.

## 10. Execution rules

- Each project MUST have a repository owner, README, setup, environment inventory and protected secrets. Link work to a ticket or documented task; review material changes for rules, auth, migration and exposure. Never commit production credentials or private customer data. Avoid copying rules across frontend/backend/jobs.
- Pipelines SHOULD run formatting/lint, type/static checks, risk-based unit and integration/contract tests, dependency checks and builds. New features MUST test business rules and authorization, not just appearance. Tier 3 includes negative, isolation, migration and recovery checks plus independent review.
- Separate development, test/staging and production appropriately. Version configuration, document migration and secret management, use repeatable deployment, verify backup and rollback for risky changes, and smoke-test critical paths after deployment.
- AI output is untrusted until verified. The human owner MUST understand the change, run relevant checks, review access/data/security and dependencies. Do not send confidential data or credentials to unapproved AI services. AI does not waive peer review or acceptance.

## 11. Required artefacts

Keep version-controlled artefacts proportionate to tier; record N/A rather than fabricate documents: System Definition; solution/deployment architecture; entity/data dictionary and classification; identity/permissions/scope matrix; workflow/state/rule maps; API/event/integration contracts; feature specs and acceptance; tests/UAT; release/rollback/operations; ADRs. Tier 3 adds threat model, retention/privacy review, adversarial tests, independent review and recovery exercise. Suggested layout: `docs/00-system-definition.md` through `docs/09-release-notes.md`, plus `docs/adr/`, `docs/modules/`, and `docs/features/`.

## 12. Decision matrix

| System | Identity | Access / workflow | Tenancy / audit | Security emphasis |
|---|---|---|---|---|
| Public brochure | Only if needed | Editorial where used | Publishing audit if CMS | Form abuse, updates and secrets |
| Internal HRMS | Required | Roles/scope; leave/payroll workflow | Organisation/branch; sensitive-action audit | Employee records and segregation |
| Marketplace | Account identities | Seller/buyer/admin; orders/refunds/disputes | Platform-party ownership | Payments, privacy and fraud |
| Multi-tenant finance | Required | Strong policy/strict scope; approval/repayment | Tenant isolation and high-integrity audit | Fraud, reconciliation and recovery |

## 13. Exceptions and governance

For each exception to a MUST, record the requirement, reason, affected system/data, risk, compensating control, approving authority, owner and review/expiry. Security/privacy exceptions need Engineering Owner review and business acceptance; Tier 3 high-impact exceptions need management approval. ADRs record context, decision, alternatives, consequences and date; retain old ADRs. Corporate management approval, effective date, first review owner and communication to active projects remain open for this proposed company-wide standard. Existing projects need not be rewritten immediately, but new work closes critical gaps and records inherited risks.

## 14. Production release gate

Release only when relevant items are satisfied or an approved exception exists: acceptance verified; server-side authentication/authorization/scope tested; transitions and failure paths pass; sensitive data/upload/integration protection checked; migration/backup/rollback understood; monitoring and support ownership set; docs/runbook/release notes updated; UAT/product acceptance recorded; smoke and incident response planned. Stop release for known unauthorized access or tenant leakage, unresolved critical corruption, unreviewed financial posting risk, missing recovery for destructive migration, exposed secrets, or critical flow failures without accepted mitigation.

## Appendix A — System Definition template

Record project, customer/business owner, engineering owner, tier/rationale, problem/outcome, identities, scope/exclusions, workflows, domain records and owners, roles/scopes, integrations, security/privacy, operational expectations, constraints, C01–C20 applicability, success measures, approval and date.

## Appendix B — Permission and transition templates

| Permission | Actor | Action | Resource | Scope | Guard |
|---|---|---|---|---|---|
| `procurement.purchase_request.view` | Requester | Read | Purchase request | Own | Active organisation |
| `procurement.purchase_request.approve` | Approver | Approve | Purchase request | Assigned department | Within limit; not creator |

| Entity | From → To | Initiator | Guards | Atomic effect | Recovery |
|---|---|---|---|---|---|
| Purchase request | Draft → Submitted | Creator | Required fields and own record | Persist, audit, outbox | Preserve draft on validation failure |
| Purchase request | Submitted → Approved | Approver | Permission, limit, distinct approver | Persist, audit, outbox | Reject/return; retry-safe event |

## Appendix C — Practice references

- [OWASP ASVS 5.0.0](https://owasp.org/www-project-application-security-verification-standard/)
- [NIST SP 800-218 SSDF 1.1](https://csrc.nist.gov/pubs/sp/800/218/final)
- [W3C WCAG 2.2](https://www.w3.org/TR/WCAG22/)

These references inform controls and do not claim certification or full conformance.
