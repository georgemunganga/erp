# CORE-ORG-02 — Existing organization master editors

Status: source contract; not runtime acceptance. Follows GW-ENG-STD-001 and the ERP 28-section feature standard. Extends existing organization page/services, not another Core master or navigation.

## 1. Feature Identification

Core → Organization → Company/profile/default currency and Departments → Create, read, update, close. Engineering boundary: existing ConfigAdminServiceImpl / ConfigRepository / OrganisationConfig. Existing ownership record: CORE-INTEGRATION-SOURCE-AUDIT-20261020.md.

## 2. Purpose & Business Outcome

A tenant administrator maintains the company and department references already consumed by ERP modules. New Procurement drafts use the selected company's saved currency. No separate employee or accounting master is created.

## 3. Scope & Exclusions

Wire existing live company create/update and department create/update in the current page, preserving existing lists/location creation/closure. No deletion, default-company reassignment, payroll setup, tax calculation, currency conversion, worker creation, manager reassignment, organizational hierarchy movement or historical financial recoding. Existing location edit is a later bounded adapter using its existing API.

## 4. Parent & Related Features

Core organization, branding and shell company selection; HRM employee references; Procurement context and ProcurementCurrencyDefaults. Shared identity and tenant filters remain authoritative.

## 5. Actors & Participants

ERP admin, HR admin and existing HR operations editor; other roles read only according to existing service grants. Core-only admins do not gain worker visibility. Assigning a manager remains existing HR-ops/admin authority and is not part of this editor.

## 6. Entry Points & Triggers

Existing `/core/workspace/settings/organization` and compatibility organization page. Company section Add/Edit, department section Add/Edit; shared Dialog/Input/Select/Button controls. Existing rows remain in place, no new sidebar.

## 7. Preconditions

Authenticated current tenant with server organization administration role. Backend authority/privacy fixes must be integrated and tested before release. Real API mode; required references loaded. Currency validation must normalize exactly three ASCII letters server-side before persistence. Do not expose unsupported default-company mutation.

## 8. Workflow

Load owned references → open new or selected-row editor → enter supported fields → validate → send exactly one existing POST/PATCH → on confirmed response refresh real list and close. Error preserves input. No demo mutation in real mode. Cancel discards unsaved input. Create ambiguity requires refresh and finding the stable code before any deliberate repeat; never automatically retry a mutation.

## 9. Status / State Transitions

Company metadata has no new approval/status lifecycle. Department creates existing active unit; metadata updates are immediate. Existing close operation sets closed/effective-to and preserves record. Do not describe name/currency edits as versioned future-effective operations: current service updates in place.

## 10. Permissions & Data Scope

Use server erp_admin/hr_admin/hr_ops organization guards; frontend helper is usability only. Tenant-wide Core organization authority does not come from selected-company headers. Foreign-tenant IDs remain hidden/unmodifiable by repository filters. Core-only response omits worker-manager identity and does not query workers. Existing HR scope is not expanded.

## 11. Business Rules

Company: code/name required; code immutable after create; existing tenant duplicate-code validation. Currency trimmed/uppercase, exactly three ASCII letters; existing invalid retained currency is readable, but cannot be resaved as a new invalid value. Country create normalized two ASCII letters, edit read-only because update contract has no country field. Blank update name rejected before mutation. No new default-company checkbox; create IsDefault=false, update omits IsDefault. Department: code/name/company required at create; code/company immutable in this editor after create; update supports name/cost-centre reference only. Parent/type/manager/history are preserved and not silently reconstructed from display rows. Cost-centre text is a reference, not a Finance budget/account creation.

## 12. Data Fields & Ownership

Company editor: Code(create only), RegisteredName, TradingName, Currency, CountryCode(create only). Existing PACRA/TPIN/statutory references remain retained/unchanged when omitted; this slice does not add a second payroll editor. Department editor: Code(create only), Name, LegalEntityId(create only), CostCentreRef. Existing UnitType/ParentId/ManagerId/effective dates/status omitted on update, preserved by server. Existing Entity/Tenant/Audit records and IDs are reused.

## 13. Screens & Data Views

Existing organization company and department sections, shared row Edit button and Add action. Modal shows current values from retained raw owned row, not inferred display fallbacks. IDs are not free text. Country/code/company become read-only where update does not support them. On absent/denied data no actionable stale editor.

## 14. User Experience / Human Behaviour

Plain labels: Company name, Default currency, Department, Cost centre. Explain currency applies to future defaulted transactions; existing purchasing amounts/currencies are unchanged. Required fields and actionable API errors, busy save disables duplicate clicks, cancellation does not report success. Counts appear only if complete, otherwise unavailable. Preserve existing imported layout and ERP branding.

## 15. Notifications & Communications

Use existing local success/error feedback after actual response. No email, approval or accounting notification is implied.

## 16. Documents & Attachments

N/A to this metadata slice. Existing branded company documents and employee evidence remain separate owned capabilities.

## 17. Exceptions & Edge Cases

Invalid currency/name/country returns actionable validation before tracked mutation. Duplicate create code uses existing conflict. Foreign IDs not found. Expired session/403 does not mutate UI records. Closed departments remain readable; editor does not reopen. No arbitrary parent movement. Retained unknown/missing fields remain preserved.

## 18. Integrations / APIs / Events / Sandbox

Reuse `/api/hrm/admin/legal-entities` POST and `/{id}` PATCH, `/api/hrm/admin/org-units` POST and `/{id}` PATCH, existing GETs and typed shared HTTP client. Procurement defaults use selected LegalEntity.Currency; no integration ledger or invented event. Mocked browser scenarios must be labelled presentation/contract checks; actual mapped HTTP and tenant-scoped persistence checks remain required.

## 19. Audit / Security / Privacy

Existing HrmDbContext AuditInterceptor and tenant query/write guards remain authoritative. Do not log submitted identifiers/worker details. UI sends only supported changed fields and never round-trips hidden worker/payroll data. No granting hr_admin to ERP-only admins. No claim of CAS/versioned edits or independent audit verification until corresponding gates run.

## 20. Reports / Dashboards / Metrics

Existing tables refresh from real GETs. Capped workforce pages do not prove headcount totals. No new financial/reporting metric.

## 21. Configuration & Localization

Tenant branding/shared controls reused. Currency code is a storage/reference code, not a conversion policy. Date formatting follows existing locale; closure validates actual server date and must not use fixed demo date in real mode.

## 22. Non-Functional Requirements

Accessible labelled dialogs/forms, keyboard submission/focus, no extra workforce query for Core-only account, no unbounded lookup. Existing master lists are unpaginated; this slice does not invent pagination or claim scale acceptance. Protect failures and no effects before commit.

## 23. Postconditions

Confirmed save returns persisted own-tenant record; refreshed list and subsequent Procurement context reflect saved company currency. Existing records, prices, worker links and foreign tenants unchanged.

## 24. Failure & Recovery

Existing CRUD has no idempotency receipt or optimistic version contract. No automatic POST/PATCH retry after response loss; retain inputs, mark outcome unconfirmed and instruct refresh/check code. A new explicit attempt is a user decision after reconciliation. Do not claim durable exactly-once creation. Concurrent metadata writes retain existing server semantics; CAS enhancement is a separate owned contract.

## 25. Acceptance Criteria

ERP-only admin can create company and save valid currency/profile fields, create/edit department without workers, reload exact saved values and use company currency for a new draft. Employee and foreign-tenant mutations deny with unchanged tables. Invalid inputs deny before changes. Hidden manager/statutory/history fields remain intact. Existing HRM configuration/Procurement forms retain separate authority.

## 26. Test Scenarios

Restricted-role service and mapped HTTP positive/denied/foreign-scope/no-effects; currency normalization and malformed values; invalid update cannot partially mutate name; existing worker-linked department update preserves link without querying workers; browser Add/Edit/reset/error/uncertain response; no duplicate automatic POST; company currency default changes future draft only; current branding and Core navigation retained. Author first, schedule actual execution with root resource gates.

## 27. Ownership / Dependencies

Core owns organization and role authority; existing HRM adapter paths remain compatibility interfaces. Finance owns currency accounting and cost-centre budgets. Procurement consumes references. Root owns source integration, serialized tests and deployment; UI/backend changes remain source-only until evidence accepted.

## 28. Open Decisions / Assumptions

Default-company uniqueness/reassignment and historical concurrent metadata editing need separate contract; not exposed here. Full currency catalog/tax/terms remain Finance/shared-reference work. No manager picker or departmental data-scope grant is inferred. Existing closed-unit metadata edits need explicit product adoption before enabling; initially disable editing closed units. Source acceptance is not runtime/UAT approval.

## Source implementation checkpoint (not executed)

The bounded adapter reuses the existing company/department sections and shared ERP Dialog/Input/Select/Button controls. Companies gain create/edit profile and currency; departments gain create/edit name/cost-centre reference. Raw owned response fields initialize editors; update payloads explicitly omit hidden/statutory/worker/default/hierarchy/history properties. Closed/effective-to departments are not offered editing. Existing company/department IDs and repositories remain authoritative. Existing compatibility HTTP paths are reused, adding only the previously absent client wrapper for company PATCH.

The existing HTTP client does not automatically retry writes. This adapter locks an uncertain mutation after a transport/server failure and offers close-and-refresh before deliberate reconciliation. No durable receipt, CAS guarantee or browser-local saved record is claimed. Static demo date is replaced with the current UTC date in live mode; existing demo presentation keeps its original date. Three presentation/browser cases plus payload/authorization tests are authored, not executed; Node/SDK/browser jobs remain deferred. Company/department section arrangement and existing location/closure controls remain in place. Location edit remains the documented subsequent adapter gap.
