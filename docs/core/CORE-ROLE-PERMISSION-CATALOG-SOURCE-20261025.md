# Core Procurement role permission catalog source checkpoint

The read-only `GET /api/core/v1/role-permissions` returns schema version 1
and the existing Procurement action keys with display group and label. It
requires the existing `hrm-admin` policy and grants no action. It does not
expose role assignments or add a Procurement workflow route.

The Application catalog now interprets one configured role row for both the
local-session authorization resolver and Procurement-category role DTO
readback. Explicit action names are case-sensitive and limited to the existing
catalog. Only a CSV consisting of the role's exact legacy seed token selects
bootstrap actions. Empty, unknown and HR-only CSV gives no Procurement
actions. The `erp_admin` seed still excludes Return Draft read/create/edit.
Role DTO `effectiveProcurementPermissions` reports the current role-row
Procurement grants across categories, and is empty for inactive/archived rows.
The editable `permissions` field retains its existing HRM, payroll and system
projection; for Procurement-category rows it uses the shared resolver. Thus a
legacy system `erp_admin` row can show its system edit token separately from
its effective Procurement actions. Current Core account/tenant checks still
belong to the authorizer; this display field is not a grant or PATCH input.

Authored focused tests cover catalog closure and admin-only HTTP access,
empty/unknown/HR-only/exact-seed/custom role readback, excluded Return Draft
actions, and unchanged stored actions on a metadata-only role edit. The final source checkpoint `e45b6638` passed 15 focused backend tests, all 1,364 Host regression tests, strict frontend TypeScript, the production frontend build, and eight focused built-app Playwright cases with mocked APIs. Earlier browser startup and selector failures were retained and corrected. No Domain rerun or database execution is claimed.

See [verification receipt](CORE-ROLE-PERMISSION-CATALOG-VERIFICATION-20261025.json). Production remains on the older 67-action catalog; its separately prepared Core-only backport preserves those exact keys. The 91-action development host must not be deployed over that release without its separate schema/runtime gates. No migration, production role, feature activation or deployed endpoint claim is made. Authenticated production UAT remains pending.

The [compatible release](CORE-ROLE-CATALOG-RELEASE-20261025.md) is now deployed with the existing 67-action production catalog. Authenticated business UAT remains pending; the 91-action development host was not deployed.
