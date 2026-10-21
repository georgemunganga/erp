# Procurement access for an ERP administrator without an employee

## Source checkpoint — not deployed or executed

An identity account is not an employee. Core ERP administrators may inspect
Procurement without creating a fictional HR record or borrowing another worker's
identity. This patch is schema-free and deliberately grants no business command
without the existing active, correctly scoped employee mapping.

## Contract and ownership

- Core remains owner of LocalUser, roles, tenant, company and branch assignments.
- The middleware retains its existing company selection and branch confinement
  checks. Company/branch selections are context, never an additional permission.
- A read-only exception requires an authenticated `erp_admin` claim **and** a
  current same-tenant, unarchived, active, unlocked LocalUser whose stored roles
  contain `erp_admin` and whose WorkerId is genuinely null. No fallback for a
  terminated, broken or wrong-company mapped employee, or for an HR-only admin.
- Only GET may use this exception. POST, PUT, PATCH, DELETE and other methods
  retain the workforce requirement before endpoint execution, including direct
  API calls. No non-human actor is silently assigned approval or spending power.
- Scope exposes `AdminViewOnly`; context rechecks the actual account and current
  branch assignments, then returns `workerId:null, adminViewOnly:true`. An
  unconfined admin may inspect the verified company without an arbitrary branch
  or department. A confined admin must retain its actual selected branch.
- The context permission projection contains access, named `*-read` actions and
  receipt-view only. No create/edit/review/approve/convert/match/finance permissions.
  This is UX guidance; the middleware method denial is the server write boundary.
  A server-added narrowing claim also runs before the ERP-admin shortcut in existing
  authorization policies, so GET action projections cannot advertise approve,
  suspend, activate, edit or other commands. It cannot grant any new read privilege,
  and global auth/me roles remain unchanged.
- Existing endpoint permissions and record-specific scope checks still apply.
  Actor-owned queues or use cases that require an employee may remain unavailable;
  this change does not manufacture ownership or bypass their guards.
- Normal employee contexts retain their existing worker identity and permissions,
  with additive `adminViewOnly:false`. Metadata and reads do not create records.

## Frontend adoption

The context client must accept a null worker **only** when the response explicitly
says `adminViewOnly:true`; an absent/null worker for an ordinary employee remains
invalid. Render existing screens with read-only permissions, clear any previous
actor/context data, and do not treat the ERP role alone as a command permission.
No new page, menu, preview dataset, employee, or business provider is required.

## Acceptance evidence to execute

`ProcurementAdminViewTests` authors actual middleware/context checks for GET
context/meta/vendor read, no fake worker/no tracked writes, each non-GET denial,
stored/claim role mismatch, inactive/archived/locked/foreign-tenant account,
foreign company, unassigned/mismatched branch, wrong-company/inactive employee
mapping, and account/branch revocation between scope and context construction.
Existing middleware, context and mapped request suites remain required regression
gates. Source-only tests are not runtime or production evidence. A private mapped
HTTP and browser verification must precede any production access claim.

## Retained production compatibility and release hold

This requires no database migration and does not enable P20–P29 execution flags.
Production is retained v36/P19 under the existing deployment hold. An immediate
production correction must be applied to and fully built from the explicitly
chosen retained source with its matching frontend nullable-worker contract,
reviewed host image/source provenance and scoped smoke tests. Do not deploy the
whole current development branch merely to ship this access fix, bypass the
release hold, or advertise undeployed Procurement workflows as available.

### P19 hotfix reconstruction compatibility

On the isolated v36-compatible branch the same production access delta is applied
without later Procurement migrations or action catalog entries. Source tests use
existing P19 configuration edit/approve/read and PO-read policies where current tests
used future authority policies; future unknown actions remain denied. The full source
recipe/hash inventory and no-DDL startup gate are recorded in
`PROC-OPS-V36-ACCESS-HOTFIX-SOURCE-AUDIT.md`. This is unbuilt source, not deployment.

## Retained P19 actual-handler test supplement (authored, not executed)

`ProcurementAdminVendorHttpTests` maps the actual retained context, vendor and
item handlers behind authentication, real authorization and scope middleware.
Only outer authentication and existing persisted fixture records are synthetic;
Core/Procurement use isolated InMemory stores. This is not PostgreSQL, production,
new supplier activation, or employee-identity evidence.

The test verifies the current company/branch and null-worker read-only context;
actual vendor list/detail; all five detail action flags false; other-company and
private other-creator draft invisibility; and all 19 mapped vendor write commands
returning 403 with an unchanged independently re-read owner-store footprint
(parent/children/decisions/audit/outbox/accounts and no employee). It also checks
that retained GET open-orders (vendor-suspend) and item maintenance (item-manage)
remain denied: authorization ran before scope narrowing, so middleware must
recheck required endpoint policy metadata after narrowing. This is not a new
item-read grant or an expansion of existing record visibility.

This supplement is based on retained hotfix candidate `0333559`. The single
middleware correction and test must be rebuilt into the chosen retained source
before release; an earlier frozen candidate or successful synthetic metadata
endpoint test cannot prove this actual-handler behavior. Root owns serial gates,
full host publish/image provenance and any production decision under release hold.
