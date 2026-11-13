# Explicit ERP session HTTP lifecycle

## 1. Identification
Tier3 Core feature,10 November2026. Implementation/qualification in progress;
not live. Parent: operational admission, scope authority and durable registry.
## 2. Purpose
Connect a verified Kuubu person and explicit company/module/scope selection to a
durable opaque browser session without reusing legacy role/bootstrap authority.
## 3. Scope
Default-off select/current-state/logout API and parent logout/start revocation.
Product request authentication and the protected selected Core context are now
separately qualified; see [request authentication](CORE-PRODUCT-REQUEST-AUTH-20261110.md).
Business resource adapters remain required; this HTTP lifecycle alone does not
admit protected HRM/Procurement operations.
## 4. Ownership
Core owns session lifecycle, identity binding, current scope and audit. Laravel
owns enrollment, payer/subscription and licensing. Modules own business actions
and resource scope. Perfex and commercial migration ownership remain unchanged.
## 5. Actors
Explicitly enrolled verified person, active company member with explicit module
permission, current licensing provider and deployment operator.
## 6. Preconditions
ERP:ProductSessions:Enabled defaults false. Enabling requires operational identity,
company discovery, licensing, confidential client, private key ring/credential
state and configured Core database with qualified owning migrations. Do not
activate production before authentication/resource/ingress acceptance completes.
## 7. Entry
GET /erp-session/review accepts exact company/link/module selectors and shows only
the current person's grant version, scope, roles and actions after fresh licensing
and final scope validation. Ordinary explicit members need no owner/editor role.
Review never creates a session or substitutes for scope confirmation.
POST /api/core/product-identity/operational/erp-session/select. Exact JSON company,
link, module, reviewed grant version/revision, scope, location IDs, confirmations
and command key. No tenant/principal/credential/browser/deadline supplied by UI.
## 8. User journey
Existing branded sign-in and separate operational consent, explicit workspace
and module review, confirm scope, issue durable session/cookie, revalidate current
state, revoke on logout or explicit replacement. UI entry remains gated until
protected ERP authentication/resource integration is qualified.
## 9. States
Unselected, selected, replaced, revoked, expired, denied or unavailable. A current
session-state response is not default ERP authentication or business authority.
## 10. Identity
ProductOperationalFlow verifies confidential-client person continuity. Trusted
material comes only from protected store/authenticated browser and ticket.
Customer/contact/invoice creation never creates an identity, enrollment or access.
## 11. Authorization
FindCandidate is discovery only. Fresh commercial licensing and the registry's
current Core/grant/branch checks are required before issue or state disclosure.
Governance role alone does not grant module access. No email/domain joins.
## 12. Scope
Company/link/module/version/revision plus copied nonempty location set, or
separate explicit company-wide confirmation. No empty-means-all conversion.
Unknown modules, duplicate IDs, selectors and fields fail before provider/DB work.
## 13. Data model
Reuse CoreProductSession/Location/Revocation and existing admission/identity/
license models. No new schema, legacy sessions, tenant roles or local passwords.
CoreProductSessionState carries no token and routes expose selected metadata only.
## 14. Commands
Selection requires canonical GUIDs, exact fields and explicit confirmation.
8192-byte bounded body, depth3, duplicate-property rejection and max128 locations.
An exact command retry recovers the same token; changed intent conflicts.
## 15. Persistence
Registry owns atomic issue/replacement, immutable receipts and revocations.
Admission snapshots inputs before asynchronous reads. No response/cookie on
failure, stale proof or expired final result. A lost committed response retains
safe exact-key retry; it must not create another command automatically.
## 16. Cookie
__Host-kuubu_erp_session; Path=/, Secure, HttpOnly, SameSite=Lax, no Domain.
Only the opaque token is stored; no provider credential, tenant, roles or scopes.
Duplicate/malformed cookies fail closed. Expiry cannot exceed parent absolute
credential, authenticated ticket or protected browser expiry. No sliding extension.
## 17. CSRF and origin
Reuse exact HTTPS origin guard, JSON-only same-origin POST, protected browser
CSRF, cross-site rejection, query allowlist and endpoint/request budgets. GET
never mutates session state. Every response is no-store/no-cache.
## 18. Current state
GET /erp-session/session re-observes person and current licensing, verifies token/
credential/browser/subject/client, exact stored grant and current owned branches.
No cached success on provider/DB failure, revocation, replacement or expiry.
operational_access_granted remains false pending protected module integration.
## 19. Logout
POST /erp-session/logout needs CSRF and bound authenticated continuity but no
provider/license request. Append registry revocation idempotently. DB outage
returns503 and retains token for retry. Missing/foreign token reveals no existence.
Parent logout/start revokes parent credentials first, attempts ERP revocation and
clears the ERP cookie. Parent credential denial remains authoritative if registry
revocation is unavailable. Request cancellation must not preserve parent access.
## 20. Errors
400 invalid contract,401 missing verified identity/session cookie,403 denied
scope/license/CSRF,409 changed retry intent,413 oversized body,429 issue/request
budget,503 disabled/dependency unavailable. No redirect or raw provider errors.
## 21. Audit and events
Reuse registry ID-only issuance/replacement/revocation audit/outbox records.
No token/handle/browser binding in JSON, logs or audit. No duplicate retry events.
## 22. Reliability
Existing key-ring recovery, immutable scope and owning migration rules remain.
Expired/stale/retargeted state cannot become current by replay. Preserve all
qualification attempts in exclusive roots; do not rewrite retained publications.
## 23. Performance
Bounded requests/locations and indexed token/actor lookups. Candidate metadata is
internal only. Current person/license checks are required; outage fails closed.
Capacity/load, production key retention and recovery remain release checks.
## 24. Tests
Actual HTTP OAuth/cookie middleware, encrypted store and Core registry/relational
DB tests with synthetic provider responses. Cover explicit issue/state, secure
cookie/no default ERP auth, lost-response retry, conflicting intent, logout and
parent logout, company replacement, outages/denials, unknown fields, invalid scope,
CSRF/origin/cookies and default-off. Verify ordinary members can review/select their own explicit
grant while the enabled owner editor still denies them; foreign selectors reveal
no grant/license. Run Windows and isolated native PostgreSQL.
## 25. Deployment
Source only. Do not enable production flags, apply production migration, publish
new DNS or change Perfex/payment/mail. Preserve dedicated local/OIDC operation.
## 26. Rollback
Disable session feature. Existing registry evidence remains immutable; schema
rollback with populated data requires a separate retention decision. Do not
reintroduce default ERP admission from ordinary identity or legacy role fallback.
## 27. Acceptance
Qualified in isolation:674 Windows passes/44 explicit environment skips and186
native PostgreSQL passes/zero skips. All23 new HTTP lifecycle cases pass in each;
focused Windows23/zero skips also passes. Provider person responses and current
license delegates are synthetic; actual OAuth/cookie middleware, encrypted
credentials, Core commands and relational databases execute. This is not an
actual-provider browser ERP launch or protected module authentication.

Evidence: inspection/qualifications/product-session-http-core-03,
product-session-http-windows-focused-03, product-session-http-native-02 and
product-session-http-verification-01/verification.json. Seven compiled C# inputs,
35 independent dependency DLLs and586 read-only native runtime files retain verified
hashes. Both native containers are stopped with no published host ports. API/tests
retain27/21 existing warnings. Initial focused01 correctly rejected sessions
created with Core's fixed8-November fixture clock as expired by actual HTTP time;
focused02 uses matching runtime session/license clocks and passes19 cases.
Core03/focused03 add self-review, ordinary-member versus enabled owner-editor
checks and foreign-selector denial,23 cases. No production guard or assertion
was weakened. All build/focused attempts are retained.
Full acceptance still requires product authentication, route/action/resource
mapping, real protected module read/write isolation and actual browser ERP launch.
## 28. Programme
One maintained ASP.NET/Docker codebase for shared SaaS, dedicated and on-premise.
Separate GreenWebb/Kuubu branded sign-in remains. Lenco, hosting/domain/email,
controlled deployment/monitoring/recovery and offline licensing/installation/
upgrades/backup restore remain required. Keep both programme PRs draft.
