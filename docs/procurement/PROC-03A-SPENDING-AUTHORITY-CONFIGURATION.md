# PROC-03A — Spending authority configuration

**Parent:** PROC-03 Approval and Budget Control  
**Status:** Implementation candidate; verification and business policy approval pending  
**Updated:** 20 October 2026  
**Standard:** GW-ENG-STD-001, Tier3; Module → Parent Feature → Child Feature → Operation

## 1. Feature Identification

Procurement → Approval and Budget Control → Spending Authority Configuration → Create/edit draft, submit, return, activate, revoke, supersede, list and inspect. Stable feature ID `PROC-03A`. This specification defines configuration behaviour; it does not approve financial limits.

## 2. Purpose & Business Outcome

An organisation can explicitly authorise a named person to approve purchases within a currency, amount, period and record scope. A manager role alone never establishes a monetary limit. The system preserves which authority version supported each purchasing decision.

## 3. Scope & Exclusions

Includes named purchase-request approval authority, independent publication, finite limits, effective dates, branch/department/category scope and replacement/revocation. Excludes foreign-exchange conversion, combining grants to cover an amount, payment release, automatic approval, delegated authority and unsupported purchasing transaction types. Those require their own contracts.

## 4. Parent & Related Features

Parent PROC-03; related Core users/roles/organisation, procurement categories, buying policy versions, request submission and Core approval execution. Core owns identity and action permissions; Procurement owns financial purchasing authority.

## 5. Actors & Participants

- Configuration owner: prepares and submits their draft.
- Independent reviewer: reviews and publishes or returns it.
- Revocation operator: stops an active authority with a reason.
- Authority subject: the named approver; cannot approve their own authority grant.
- Auditor: reads permitted configuration/history and decision references.

The creator, owner, submitter and subject cannot perform independent activation. ERP administration permits governed configuration access; purchasing approval still requires a matching finite grant.

## 6. Entry Points & Triggers

Module settings and its controlled configuration API; a new employee responsibility, revised financial policy, departure, suspension or correction can start a change. Existing purchasing screens consume published authority; they do not create it implicitly.

## 7. Preconditions

Authenticated active canonical Core account, active employee mapping in the selected company, action permission and applicable data scope. The authority subject must resolve to an active company account. No record can activate without complete valid terms and independent review. A missing configuration never supplies an unlimited fallback.

## 8. Workflow

1. Create a draft for a canonical subject and named authority reference. Allocate the next version from the complete logical family inside a serialised transaction.
2. Enter financial terms and explicit scopes. Incomplete drafts remain saveable; values must fit supported storage precision without rounding.
3. Submit: validate subject, organisation references, company currency, amount/basis, dates and categories; freeze a submitted business fingerprint.
4. A distinct reviewer returns the draft with a reason or activates the latest family version.
5. If another version is Active, use explicit supersession: retire its authority and publish the replacement within one transaction.
6. Revoke an active version with a reason. Revoked/superseded versions remain historical and cannot revive.

## 9. Status / State Transitions

| From | Action | To | Guards |
|---|---|---|---|
| None | Create | Draft | Configuration permission, canonical subject, family version allocation |
| Draft | Edit | Draft | Owner, expected revision, valid draft representation |
| Draft | Submit | PendingReview | Complete terms and current subject |
| PendingReview | Return | Draft | Independent reviewer, reason |
| PendingReview | Activate | Active | Latest version, unchanged submission, independent reviewer, no other Active version |
| PendingReview + old Active | Supersede | Active + old Superseded | Both expected revisions, same logical family, atomic saves, replacement effective now |
| Active | Revoke | Revoked | Revocation permission, reason, expected revision |

Effective eligibility is evaluated separately from lifecycle state. An Active grant outside its effective period grants no authority. Future-dated supersession is denied until scheduled handover behaviour is implemented.

## 10. Permissions & Data Scope

Actions: `procurement-authority-read`, `procurement-authority-configure`, `procurement-authority-review`, `procurement-authority-revoke`. These do not automatically grant `procurement-approval-decide`. Default bootstrap grants for these financial configuration actions are empty; governed Core configuration assigns access.

Every query/write is tenant/company scoped. A selected or confined branch/department restricts visible/manageable grants; a branch-confined operator cannot create an all-branches grant. Direct IDs and exports must enforce the same boundary. Owners cannot edit other owners' drafts merely because they can list them.

## 11. Business Rules

- A logical family consists of tenant, company, subject, permission, action and authority reference. Changing currency, scope or limit creates a new version within that family.
- One Active row per logical family; version numbers are unique and increase.
- Current eligibility requires one grant covering the entire active request amount and every active line category. Never sum or combine limits from different grants.
- Supported amount basis: `EstimatedLineBase`, quantity × estimated unit price before tax, discounts and delivery. Display this basis.
- Draft zero amount is allowed; publication requires a positive finite amount. Reject unsupported scale/range before database storage; never silently round authority.
- The company's configured currency is the default and the supported currency for this increment. No implied FX conversion.
- Branch, department and category scopes require explicit single references or explicit all-scope flags. All flags default false.
- Published business values and review evidence are immutable. Supersession/revocation adds lifecycle evidence.
- Current identity, permission, scope, dates and authority must be rechecked at purchasing decision time; historical evidence alone is insufficient.

## 12. Data Fields & Ownership

Procurement owns grant ID, authority reference/version, revision, state, canonical principal reference, permission/action, configuration owner, currency, amount basis, maximum amount, effective interval, all-scope flags, branch/department references, category rows, submitted/published fingerprints and submission/activation/return/revocation/supersession evidence.

Core owns referenced accounts, organisation/branch/department definitions and action grants. Category codes remain Procurement-owned. No duplicate account, employee, organisation or Finance balance master is created. Maximum amount uses `numeric(18,4)`; request evidence can retain higher intermediate calculation precision.

## 13. Screens & Data Views

Settings list: reference, version, subject, status, currency/limit/basis, dates, scope and owner; searchable/filterable/paginated with an appropriate empty state. Detail separates terms, scope and history. Draft form and reason/review actions reuse ERP inputs, floating selects, toggles and tables. Screen integration remains pending until verified; API presence is not proof of a completed UI.

## 14. User Experience / Human Behaviour

Use “Spending limits”, “Who can approve”, “Maximum purchase amount”, “Send for review”, “Replace this version” and “Stop this authority”. Explain that amount excludes tax/delivery in this increment. Show who must review next and why self-review is unavailable. Preserve inputs on validation/network errors; stale revisions require refresh without silently discarding edits.

## 15. Notifications & Communications

Required events include submitted, returned, activated, superseded and revoked. Recipients are owner, reviewer/assigned operator and affected subject where policy permits. Record audit independently of notifications. Existing source code audit does not prove notification dispatch; dispatch/retry integration is a separate activation requirement.

## 16. Documents & Attachments

Link the approved delegation/financial-policy document through Core documents when required by business policy. Procurement stores a scoped immutable reference/version; Core stores and secures content. A typed reference alone does not establish approved authority. Attachment requirements and retention are open policy decisions.

## 17. Exceptions & Edge Cases

Inactive/unmapped/ambiguous subject, invalid company references, unsupported currency, overlapping or duplicate authority versions, self-review, stale revision, removed permission, changed submitted fields, future supersession and missing prior version all deny the relevant action with a plain-language recovery step. A newer revoked version never makes a superseded older higher limit active again.

## 18. Integrations / APIs / Events / Sandbox

Base API: `/api/procurement/v1/configuration/spending-authorities`. List/detail, POST draft, PUT draft, POST submit/return/activate/revoke/supersede use existing authenticated ERP scope. Mutation payloads carry expected revisions; supersede carries both IDs/revisions. Server resolves accounts and permissions. Use a disposable PostgreSQL database and synthetic accounts for publication/concurrency tests; no production financial authority is created by a test.

## 19. Audit / Security / Privacy

Capture actor, tenant/company, prior/new state, changed terms, reason, revision, timestamps and correlation. Restrict subject identifiers and limits to authorised readers. Block published category additions/changes/deletions and terminal revival through all context save overloads. Draft category replacement is the narrow technical-delete exception; published/business history remains retained. Runtime grants must not permit ordinary users to erase audit evidence.

## 20. Reports / Dashboards / Metrics

Report active/effective/expired authority, drafts awaiting review, revoked/superseded history and pending purchasing tasks affected by revoked authority. Define as-of time and scope; counts must not disclose another company. Decision audit links to the grant/version actually evaluated.

## 21. Configuration & Localization

Use Core organisation currency and localisation. Policy owners configure actual amounts, scope and dates; no illustrative currency amount becomes a default threshold. Store normalized currency codes, UTC timestamps and decimal amounts, displaying the user's locale/time zone. The initial supported action/basis is explicit and must not masquerade as configurable support for other financial actions.

## 22. Non-Functional Requirements

Tenant isolation, deterministic fingerprints after PostgreSQL timestamp/decimal normalization, optimistic revisions, bounded list pages and serialized family publication. No external calls while holding publication locks. Deadlock/serialization conflicts return recoverable errors; monitor unexpected storage errors and denied mutations. Apply shared accessibility/responsive standards.

## 23. Postconditions

Drafts remain non-authoritative. Activation produces one independently reviewed current version. Supersession commits both lifecycle changes together; revocation stops eligibility. History and prior purchasing evidence remain attributable after account changes.

## 24. Failure & Recovery

Rollback the entire family transaction, including audit, on publication/supersession failure. Refresh stale revisions and revalidate current subject/scope. Missing schema returns unavailable rather than permitting authority. A lost activation response can be reconciled by detail/history; repeated expected revision cannot activate a second time. Durable command receipts are required where a later use case promises exact response replay.

## 25. Acceptance Criteria

An authorised owner can save incomplete valid drafts, edit categories, submit complete terms, and receive independent review. Self-review, foreign scope, invalid/rounded financial values and stale writes fail. Replacement/revocation cannot revive old limits. Published field/category tampering fails. Concurrent allocation/publication cannot create duplicate active family versions. Actual configuration UI, HTTP, PostgreSQL/migration and production activation evidence must be recorded before claiming the complete live feature.

## 26. Test Scenarios

Cover draft CRUD/zero amount; category replacement; missing scope; invalid 5-decimal/range input; canonical identity/company checks; owner/reviewer separation; permission revocation; expected revision; semantic fingerprint roundtrip; added/changed/deleted published children; terminal revival; atomic supersession rollback; concurrent version allocation; one-full-grant request eligibility; cross-tenant/entity/branch/dept denial; empty/loading/error UI; migration reapplication and compatible code rollback.

## 27. Ownership / Dependencies

Business policy owner: designated Procurement/Finance authority, to be assigned. Engineering owner: ERP team. Dependencies: Core identity/organisation/access, Procurement schema, database transactions, audit and existing UI system. Production activation additionally depends on coordinated shared-host release and documented policy ownership.

## 28. Open Decisions / Assumptions

Confirm named policy/review owners, monetary limits, effective intervals, all-scope access, required policy evidence, retention, notifications and delegation rules. Confirm supported treatment of other currencies/bases and future-dated replacements. These decisions do not block draft CRUD; they block publication or purchasing authorization where required evidence is missing. Source verification and deployment status are recorded separately from business approval.
