# CORE-WF-01 — Shared workflow configuration directory

Status: implemented in source; M1 of issue192. Not deployed or activated. The supervisor/executive routing and advance-payment milestones remain open.

## 1. Feature Identification

CORE-WF-01; Core → Workflow Configuration → Registered workflow settings → Discover/open. Owner: ERP platform engineering; parent issue192. This is M1, not completion of configurable request routing.

## 2. Purpose & Business Outcome

Give administrators one Core entry to choose module, business record and trigger and open the owning module’s real approval settings.

## 3. Scope & Exclusions

Read-only registry and shared Core page. Excludes saving workflow definitions, approvals, role/authority grants, case execution, bank/AP actions and completion of the missing supervisor/executive bindings.

## 4. Parent & Related Features

Shared Core approval engine; Procurement buying rules, approval limits, payment and plan review editors. Existing module settings remain the authoritative adapters.

## 5. Actors & Participants

Authenticated current ERP/HR administrators with tenant context; module providers register metadata. Ordinary staff cannot use this admin endpoint.

## 6. Entry Points & Triggers

Core settings → Approval Workflows; GET /api/core/v1/workflow-configuration. Select Module → Record type → When → owning editor.

## 7. Preconditions

Registered authentication/tenant policy and installed module provider. A settings link never satisfies the target module permission or workflow readiness checks.

## 8. Workflow

Authorised GET → validate all registrations → choose module/record/trigger → display precise purpose/limits → navigate to existing live settings. Clear selections and discard stale results after identity/company changes.

## 9. Status / State Transitions

Page: Loading → Ready/Empty/Error/Denied. Registry metadata has no business lifecycle or published-policy state.

## 10. Permissions & Data Scope

Reuse server hrm-admin AND Core tenant-context policies, plus CoreAdminGate. Metadata contains no customer records, company rules or people; target settings enforce current module/action/scope again.

## 11. Business Rules

Unique module.record.trigger IDs; bounded100 registrations; local same-module settings paths only; no external/traversal URLs. Never present a directory registration as workflow activation.

## 12. Data Fields & Ownership

Core owns descriptor protocol; modules own IDs, record/trigger labels, availability notes and settings links. No database table/migration or copied policies.

## 13. Screens & Data Views

One minimalist title, shared ERP floating selects and buttons, grouped form/details; retain global typography/branding.

## 14. User Experience / Human Behaviour

Plain purpose and current limitation text; no fake save/activate button, demo data or employee prerequisite. Payment approval and confirmation are separate selectable triggers.

## 15. Notifications & Communications

N/A: reading/opening settings does not send a message or assign a task.

## 16. Documents & Attachments

N/A: record type means business transaction, not PDF or file upload.

## 17. Exceptions & Edge Cases

No providers → empty; duplicates/invalid links → controlled unavailable; rejected HTTP response clears old catalog; rapid context changes discard old results.

## 18. Integrations / APIs / Events / Sandbox

Read-only version1 API; IWorkflowConfigurationProvider module extension. No network calls or events inside providers. Tests use isolated providers/auth fixtures.

## 19. Audit / Security / Privacy

No-store metadata. No mutation or business audit event from this GET. No secrets, personal data or approval policy values in descriptors.

## 20. Reports / Dashboards / Metrics

N/A for M1; registry count is not a procurement KPI or readiness measure.

## 21. Configuration & Localization

Installed providers decide discoverability; module-owned links target /{module}/workspace/settings/. Human labels are centralised in providers; no tenant-specific names or thresholds.

## 22. Non-Functional Requirements

Bounded small response, deterministic order; existing accessible ERP selects, keyboard navigation and error/empty/loading components; fresh auth on each API request.

## 23. Postconditions

Only a validated descriptor is displayed or its local editor opened. All module business state and authority remain unchanged.

## 24. Failure & Recovery

Retry GET safely; no write recovery key required. Corrupt metadata returns no partial successful catalog. Preserve test/build failures and use exclusive build outputs.

## 25. Acceptance Criteria

Core entry exists; installed Procurement registers PR/PO/payment/plan records; payment triggers distinct; role/tenant negatives; duplicate/unsafe path rejection; no providers empty; stale UI responses ignored; no workflow activated.

## 26. Test Scenarios

Registry validation, admin/ordinary/anonymous/tenantless HTTP, exact local-link parsing, empty and corrupt responses, selector interaction, company/account switch and unchanged existing approval tests.

## 27. Ownership / Dependencies

Core Workflows application-layer protocol and directory (navigation metadata stays outside the approval domain); Procurement provider/adapters; existing Core identity and shared ERP UI. No duplicated approval engine or ledger.

## 28. Open Decisions / Assumptions

M2 must add named person/role/requester-condition routing; M3 must govern PO advances if required. Actual company approvers/limits are pending business input before activation. M1 links do not claim either gap is fixed.

The existing department `OrgUnit.ManagerId` is a worker reference. It is not a Core user/role grant. M2 must define the explicit Core governance projection/binding for department heads rather than assuming that this directory has implemented it. No HR record is required to discover these administrative settings.
