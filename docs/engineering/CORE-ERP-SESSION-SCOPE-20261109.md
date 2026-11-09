# ERP session scope authority

## 1. Identification
Core Tier3 child feature,9 November2026, parent licensed product admission.
Implementation in progress; no runtime activation or live session issuance.
## 2. Purpose
Authorize an ERP session's explicitly selected company/module/branch scope without
letting headers, old grants, company roles or subscription entitlement widen it.
## 3. Scope
This increment implements server-side selection binding and point-of-use scope
resolution. The [durable registry](CORE-PRODUCT-SESSION-REGISTRY-20261109.md) is now separately
implemented/qualified. HTTP cookie handling, verified operational-flow material,
authentication handler, route classification and connected launch UI remain next.
The existing operational identity cookie still cannot authenticate ERP requests.
## 4. Parent architecture
Foundation owns identity/enrollment/subscriptions; ERP Core owns company/member/
module grants and session scope. Reuse one ASP.NET/Docker codebase across shared
SaaS, dedicated and on-premise deployments. Preserve local authentication.
## 5. Actors
Verified enrolled person with explicit active company membership and module grant;
current identity/license providers; Core session registry and module endpoints.
## 6. Entry
A future same-origin/CSRF-protected launch command supplies company/link/module,
reviewed grant version/revision and explicit branch selection. The private binding
object is never accepted from browser JSON, headers or unsigned cookie payload.
## 7. Preconditions
Fresh separately scoped person observation, matching commercial lease, exact Core
link/company/member and valid latest module grant. No identity/domain inference.
## 8. Workflow
Read current grant, compare reviewed version/revision, check selected branches,
recheck grant and ownership, construct immutable copied selection. Each request
checks stored identity/client/company/module/version/tenant/principal, current
grant/member/license and both stored and requested branch ownership before
returning narrowed request scope. Request handling must not reuse old scope on
any denial/unavailability. Selection/request arrays are copied before asynchronous
reads; a final branch-ownership check covers branch moves under company-wide
parent grants. Critical module transactions still require their own
point-of-use business authorization.
## 9. States
Selection invalid/conflict/denied/unavailable/authorized. Request denied/
unavailable/authorized. These are authorization outcomes, not persisted session
states. Registry active/revoked/expired states remain unimplemented.
## 10. Permissions
Only registered HRM/Procurement actions in the exact current grant qualify.
Company governance does not imply module permissions. No legacy/global-admin
bootstrap merge. Module roles are copied only from the validated current grant.
## 11. Rules
An unchanged binding cannot follow a newly expanded or regranted version.
Finite selection remains finite even under a company-wide parent grant. Empty
branch lists do not mean all. Company-wide selection requires separate explicit
confirmation and a company-wide parent grant. Individual branches must currently
belong to the bound tenant/company and remain unarchived. Unknown modules/actions,
foreign companies/links, duplicate/zero/over128 branch selections deny. No tenant,
principal, membership, role or grant snapshot may come from the browser.
## 12. Data
Server binding: policy1, subject/client, exact tenant/principal/link/company/module,
grant version/revision, company-wide flag or nonempty copied location IDs. No
provider credentials, emails or browser tokens. The future trusted registry must
own persistence, integrity, subject/credential/browser binding and absolute expiry;
this value object alone is not a session or authentication proof.
## 13. UI
Reuse Kuubu's separate login/company picker. Ordinary members must see only their
current authorized modules and deliberate launch choices. No launch button is
added until real persistence, authentication and route/data enforcement qualify.
## 14. UX
Changed reviewed grant requires fresh review. Changed bound grant requires new
selection/launch; never silently expand branches or adopt a replacement grant.
Provider outage differs from revocation, with both denying protected operations.
## 15. Notifications
Read-only scope checks send no mail/events. Registry issue/revoke commands must
later commit audit and ID-only events atomically with session evidence.
## 16. Documents
No document access arises from selection alone; module document permissions remain.
## 17. Errors
Malformed selection invalid; outdated review conflict; failed current authority
or mismatched binding denied; DB failure unavailable. Cancellation propagates.
No stale-success cache or old-grant fallback. No raw proof/credential logging.
## 18. Integration
The authority uses the actual CoreModuleAccessReader and owned WorkLocation reads.
A future request adapter must resolve headers/paths to typed selectors, classify
allowed module/action routes and reject unknown routes. The classic all-branches
header must not widen finite scope. Scope results are short-lived observations.
## 19. Audit/privacy
Scope resolution is read-only and creates no customer, role, session or employee.
The future registry needs consequential issue/revoke evidence without token values.
String representations of bindings and resolved request scopes omit identifiers/proof data.
## 20. Configuration
No DI registration, default authentication change, route map or feature activation
is introduced in this increment. Existing production flags remain default-off.
## 21. Security
Threats: stale/replayed grant, retargeted person/client/tenant/company/principal,
branch-header widening, empty/all ambiguity, moved/archived branches, expired
proof, missing license and DB outage. Resolve current authority on every request;
never trust a browser-supplied binding or use the result as a cached credential.
## 22. Reliability
No writes/migration in this layer. Future registry/handler must add bounded
sessions, revocation, idempotent issue/retry, key/backup/recovery and capacity checks.
## 23. Performance
Bound branch arrays to128, use exact owned queries and no directory search.
Current-grant/member/ownership rechecks deliberately precede request admission.
Measure native query/load behavior before enabling production.
## 24. Tests
CoreErpSessionScopeTests exercises actual Core grant commands and relational
reader with narrowed and wide scopes, stale/retargeted binding, live changes,
proof expiry, unavailability, cancellation, immutable selection and zero side
effects. Run SQLite regression and separate native PostgreSQL qualification.
## 25. Deployment
Source only. Do not configure an ERP session cookie, change auth mode, modify
production schema/flags, or deploy this layer as a claim of completed admission.
## 26. Rollback
No schema/runtime registration change. Revert source if required; retain immutable
qualification attempts. Never overwrite a previous build or failed evidence.
## 27. Acceptance
This component passes616 Windows checks/40 explicit environment skips and105
native PostgreSQL checks/zero skips, with37 new scope cases passing in each.
Current-person/license observations are synthetic fixtures; Core reader/commands
and relational databases are actual. These checks do not qualify a new combined
provider/browser ERP launch or any protected module route.

Evidence: inspection/qualifications/erp-session-scope-api-02 (fresh API build),
erp-session-scope-api-03 (independent corrected test build),
erp-session-scope-native-02 and erp-session-scope-verification-01/verification.json.
API binary matches the API02 build; source/API/test hashes,49 copied dependency
DLLs and585 read-only native runtime files are verified. Native containers are
stopped and published no host ports. API retains27 existing build warnings. Tests
have21 warnings:20 existing plus EF1003 on deliberate test schema-failure SQL;
its identifier is selected only from two hardcoded table names, with no external
input. The warning is reviewed as test-only; no production SQL is added.

API01 retains the initial completed build before defensive input copying.
Native01 was deliberately stopped after its fixture manually created another
company in a licensed tenant; PostgreSQL correctly rejected that bypass with
SQLSTATE23514. Its exit137, unchanged binaries and stopped database are retained.
The corrected fixture creates its second company through the actual purchased-slot
provisioner in both providers. No database/company guard or authority assertion
was weakened, and no failed publication was overwritten.

Read-only adapter inspection erp-session-adapter-inspection-01 records eight
hashed source inputs and seven integration requirements. Product sessions need
new context handling for legacy empty-branch-means-all behavior, distinct OrgUnit/
WorkLocation selectors, current Procurement legacy-role refresh and company-owned
masters/ledgers versus branch-owned records. Define resource/action policy before
enabling routes. Preserve standalone local/OIDC behavior and licensed creation.

Full session admission still requires durable registry/revocation/expiry, trusted
restoration of the exact stored binding without adopting a newer grant, secure
opaque cookie/browser/credential binding, real default authentication, request
adapters, protected HRM/Procurement reads/writes and company/branch data isolation,
browser launch and deployed ingress evidence. Component tests do not substitute
for those gates; no cookie, principal or session row is created here.
## 28. Ownership and handoff
Core/identity owns registry/authentication/request scope; modules retain business
authorization and transactions. Publish exact source/handoff/milestone baselines
before collaboration. Existing Perfex, payment/mail/DNS and production services
remain untouched. Lenco/hosting/domain/email and on-premise release work remain
part of the full platform goal.
