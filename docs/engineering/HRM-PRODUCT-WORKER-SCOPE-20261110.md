# Product-scoped HRM worker records

## 1. Identification
Tier3 HRM/Core integration,10 November2026. Implemented and independently qualified; not live. Parent: product request authentication and session scope.
## 2. Purpose
Reuse existing worker services with company/branch authority enforced before
pagination, detail/relation disclosure and persistence, not supplied by UI filters.
## 3. Scope
Existing worker list/detail/create/update/archive routes and their existing
profile contact/bank/history graph. Import, account linking, assignment/movement
and other HRM modules remain denied in product mode pending resource review.
## 4. Ownership
Worker records belong to the allocated company workspace tenant. Core enforces
one company per allocated workspace; inconsistent multi-company ownership denies.
HRM owns employee/profile rules. An employee is not a login/customer/contact.
## 5. Actors
Current verified member with an explicit HRM grant and existing HRM operational
roles. Read/write role checks remain in WorkerService; no admin bootstrap.
## 6. Preconditions
Explicit product auth, valid parent/session, current enrollment/licensing/Core
permissions and selected scope. The endpoint requires the scoped owning context;
a missing scope registration fails closed instead of using tenant-only queries.
## 7. Entry
Review and select the company/HRM module through existing explicit consent/session
flow. Reuse /api/hrm/workers and versioned aliases with reviewed operation metadata.
No tenant/principal/browser/authority may be supplied by worker request fields.
## 8. Read policy
Worker tenant must match the verified tenant, with an owning workspace binding
and no other legal entity in that tenant. Optional location/department links must
belong to that company. Finite scope requires an allowed active work location;
company-wide scope can include unassigned workers and historical owned sites.
## 9. Department semantics
OrgUnit is a department, not a work-location grant. Its company ownership is
checked independently; it never widens site authority. Historical owned department
links do not create new permissions. New assignments to archived masters deny.
## 10. SQL filtering
Context-global worker/profile-child filters are configured in every model and
parameterized per context. Legacy mode disables only the additional product
predicate, preserving tenant filters. Missing/expired product proof returns no
rows; an empty finite list never becomes unrestricted. Counts precede pagination.
## 11. Related data
Contact, bank, education and internal/external profile-history reads require an
accessible parent worker. Manager navigation is filtered as a worker too. Writes
check the persisted original child parent and proposed parent; reparenting cannot
hide a foreign source. Unreviewed lifecycle/resource routes remain denied.
## 12. Creation
Reuse WorkerService creation, validation and employee numbering. Require an allowed
location for finite scope; company-wide scope may create an unassigned profile.
Company/department/location/manager references are verified before saving. Creating
an employee never creates a login, product enrollment or workspace.
## 13. Employee numbers
Existing company-owned availability checks deliberately see only number occupancy
across this tenant, not hidden employee records. The company transaction lock
serializes worker mutations and prevents scoped numbering from choosing an
already-used number in another branch. This is not a worker-directory read.
## 14. Update
Both persisted source and proposed target must be authorized. Branch movement
cannot escape the selected scope. Newly selected masters must be active; unchanged
historical owned masters may remain under company-wide authority. Preserve the
existing application's ordinary field/date/status validation.
## 15. Identity linking
Basic product worker create/update cannot add/change/remove SubjectId. Account
linking is a separate unreviewed endpoint and remains blocked. No email/domain
matching or employee creation provisions company/product login access.
## 16. Archive
Reuse soft archive and existing worker status rules. Physical worker deletion is
not admitted by this feature. Reads retain existing includeArchived behavior
within authorized scope. Other lifecycle transitions require their own review.
## 17. Write enforcement
The context validates source/target ownership before both sync and async saves.
Tracked child updates/deletes check the owning persisted worker, not only the
client's proposed FK. Added graphs validate the added parent. Direct tenant/FK
retargeting cannot bypass the owning checks. Private proof must remain current.
## 18. Transactions
Endpoint execution uses the owning context transaction. PostgreSQL mutations
share the company/team advisory lock with Core grant/session changes. Recheck
current registry/license/Core before the service and after its saves, in that
same context/transaction; deny/exception/cancellation rolls back changes. Commit
only while authority remains current. No automatic retry of uncertain mutations.
## 19. Request boundary
Reviewed operation metadata distinguishes list/detail/create/update/archive.
Only known query filters are accepted as additional narrowing. Mutations require
exact HTTPS origin, JSON, protected browser CSRF and a bounded64KiB body. Legacy
shell/tenant/all-branches/authorization headers cannot replace cookie authority.
## 20. Shell context
Product requests populate selected company and finite locations from private
verified scope, with no legacy assignment lookup or implicit first branch. Work
location and department remain distinct. Other modes keep existing behavior.
## 21. Results and failures
Reuse worker DTOs/service behavior. Inaccessible details/sources return not-found
or forbidden without revealing hidden records. Validation uses existing status
mapping. Final authority denial returns no successful result and rolls back saved
rows. Unknown imports/linking/lifecycle endpoints remain denied before handlers.
## 22. Audit
Existing owning-context mutation audit participates in the transaction. Existing
privileged-request evidence may separately record a denied attempt. No credential/
session token is returned or added to worker audit. Production retention/PII
handling remain governed by the existing HRM/Tier3 release requirements.
## 23. Reliability
No new schema/worker-table fork or customer database copy. Preserve the two-argument
context constructor for binary compatibility and use optional scoped authority
for reviewed product requests. Model caching must not capture one tenant/scope.
Exact session selection/recovery remains distinct from legacy worker CRUD semantics.
## 24. Tests
Actual authenticated HRM routes, services, repository and relational persistence
with synthetic person/license providers. Verify list counts/detail/includes,
create/update/archive, source/target/identity denials, final-proof rollback, CSRF,
company-wide/unassigned profiles, no login/workspace provisioning and blocked
unreviewed routes. Include child source/reparenting and classic-mode regressions.
## 25. Deployment
Source only; do not activate production auth/flags, schema, DNS, Perfex, payments
or mail. Require model consistency and Windows/native PostgreSQL acceptance.
## 26. Rollback
Disable/revert reviewed product configuration/source; retain session/grant and
business evidence. Never remove scope enforcement to make a path work. Keep
all partial/failed build/test publications in exclusive immutable roots.
## 27. Acceptance
743 Windows passes/44 explicit environment skips; 255 native PostgreSQL-run
passes/zero skips. All 19 new cases pass in both: 16 authenticated HTTP cases
and three direct synthetic-scope storage cases. Focused Windows: 19 passes.
Person/license providers are synthetic; services, repository, cookies and relational
persistence execute. No other HRM/payroll/Procurement or real-provider browser ERP
operation is qualified.

Evidence: inspection/qualifications/product-worker-scope-verification-01/verification.json.
Independent Application build: core-01; Infrastructure/API/model: core-04; final
tests: core-05. All builds have zero errors; warnings respectively 22/3/27/21.
15 compiled C# source hashes, 49 dependency DLLs and 585 read-only native runtime
files verified unchanged. Model drift is zero. Both owned containers are stopped
with no host ports. Focused-04 and native-01 retain their original test evidence.
Earlier failed attempts remain retained, including the new-child tracking defect
fixed by explicit insert tracking; no tenant or authority guard was weakened.
## 28. Programme
Continue remaining HRM/Procurement resource/transaction scope and frontend/browser
integration, then controlled SaaS rollout. Preserve one ASP.NET/Docker codebase,
separate branded auth and Laravel commercial ownership. Lenco, hosting/domain/
email and dedicated/on-premise offline licensing/upgrades/restore remain required.
