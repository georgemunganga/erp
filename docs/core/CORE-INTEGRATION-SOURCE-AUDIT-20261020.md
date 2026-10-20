# Core integration source checkpoint — 20 October 2026

Source audit: backend `36c5d38`; Procurement frontend `0420489`. This is source evidence, not a runtime, deployment or acceptance claim. Existing ownership: [Core module](../erp-core-module.md), [ERP system definition](../engineering/ERP-SYSTEM-DEFINITION.md). Existing child-feature specifications remain authoritative; this map records implementation gaps against their permissions, ownership, interfaces and acceptance sections.

| Capability / owner | Existing implementation | Concrete gap / next acceptance gate |
|---|---|---|
| Organization/Profile/default currency — Core | Existing legal_entities, ConfigRepository, `/api/hrm/admin/legal-entities` GET/POST/PATCH; tenant query filters and shared audit interceptor | Core gate admits erp_admin, but OrganisationConfig.canAct and ConfigAdminServiceImpl omit it. First fix only these global organization methods. Company edit/create UI is disabled/unwired in live mode; subsequently reuse existing forms/API with validated currency/default-company semantics. Verify tenant-wide admin success and foreign-tenant no-effects. |
| Locations/Departments — Core | Existing work_locations/org_units, real lists, NewLocation and department Close; create/update APIs already persist | Same admin omission. Existing location edits disabled in live UI; department create/edit forms missing. Reuse existing records, no competing masters. Preserve employee read access; deny employee mutations. |
| Branding — Core | CompanyBrandingService permits erp_admin/hr_admin; persisted tenant branding, Poppins/Montserrat/Inter, shared frontend cache/CSS | Source implemented; cross-module production font/cache behavior needs deployed-source/browser proof. First GET currently initializes a default row; do not claim strictly read-only GET. |
| Users — Core identity | Local/IDP adapters, persisted users and admin endpoints, separate identities and worker records | UI lacks optional existing worker link. New login alone cannot perform worker-bound Procurement actions. Design owned active-worker reference validation; never auto-create employees. IDP role mapping remains narrower than local custom roles. |
| Roles — Core access | Persisted TenantRoleAssignment; backend validates Procurement grants against current ProcurementActionCatalog | UI has static old permission subset. Expose safe server catalog projection and preserve unknown retained grants; do not add another hardcoded list. Verify role mutation and effective backend permissions. |
| Module admission — Core shell/identity | Existing fixed-role checks in auth.tsx and platform/modules.ts | Custom Procurement role grants can work server-side yet fail shell admission. Plan server effective module-entitlement projection consumed by login/switcher; preserve point-of-use action/scope checks. Do not grant HRM authority just to expose a module. |
| Currencies — Core reference / Finance accounting | Company Currency is persisted, consumed by ProcurementCurrencyDefaults and context | Separate currency settings editor is a truthful placeholder. First reuse company-default API; define normalization and default-company uniqueness before new editing UX. Existing transaction currency must not change retroactively. |
| Taxes/Payment terms — Finance-owned shared references | Procurement references / original tax amounts; imported settings paths honestly unavailable | No real shared tax/terms CRUD found. Agree Finance owner and contracts before mounting editor. Payroll tax slabs are not a procurement tax master. |
| Other Core settings | Canonical `/core/workspace/settings/$slug`; organization/branding/users/roles mount existing live editors | Remaining General/tags/templates/web tabs show editor-coming-soon; do not count them as saved settings. Procurement redirects shared masters to Core. |

## Narrow first correction and acceptance

The existing Core admin role grants tenant-wide organization administration, not selected-company transaction authority. Add erp_admin only to global company/profile/currency/location/department service methods preceding the Work calendars section; preserve HRM-specific rules and repository tenant isolation. UI may enable only already connected actions. Existing roles hr_ops/hr_admin retain compatibility. No schema or new permissions are required.

Author restricted-role service tests: erp_admin-only CRUD/read/tree/close; employee mutation denial with unchanged records; foreign-tenant IDs hidden and unmodifiable; ERP admin alone cannot configure HRM calendars. Frontend helper tests must preserve existing HR operations access without expanding Core gate admission. These tests are authored separately and require serialized execution plus mapped HTTP/UAT evidence before release.

## Remaining contract sequence

1. Validate company currency/default semantics, then wire existing company/location/department editors using shared controls and real APIs.
2. Design worker-link lookup/validation and account readiness UX using canonical Core/HRM references.
3. Publish current server action catalog to Roles; server remains final authority.
4. Define effective module entitlement response instead of fixed-name shell admission.
5. Define Finance-owned tax/terms/reference contracts; keep unavailable paths honest until implemented.

No new organization, employee, role or branding master is justified. Existing issue `module-connect#20` concerns Procurement approval cases, not completion of all Core CRUD. Old Core/dependency documents are historical architecture snapshots and must not be read as current runtime completion evidence.

## First authority source increment

`codex/core-organisation-admin-authority` adds ERP-only admin to existing global company/location/department service role guards and enables existing organization UI actions through a shared helper. It does not grant HRM calendar/payroll authority, change persistence or remove tenant filtering. Restricted-role SQLite service regressions and a frontend authorization-helper regression are authored but not executed. Additional UI prerequisite discovered: organization loading currently requests HRM employee details for counts even for ERP-only admins; that dependency must be removed or made permission-aware with explicitly unavailable counts before claiming usable ERP-only organization UX. Existing live company/location editing remains disabled and is a separate integration gap.

Deferred gates: `dotnet test backend/hrm-api/src/Mightyfin.Erp.Hrm.Tests/Mightyfin.Erp.Hrm.Tests.csproj --filter FullyQualifiedName~CoreOrganisationAuthorityTests`; existing ConfigAdminServiceTests/EntityTreeTests; frontend TypeScript and `core-organisation-authority.spec.ts`; then actual mapped HTTP and role-specific organization browser flow. No execution or deployment evidence is asserted by this source increment.
