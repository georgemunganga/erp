# Product request authentication and selected workspace context

## 1. Identification
Tier3 Core feature,10 November2026; implementation/qualification in progress,
not live. Parent: operational admission, scope authority, registry and HTTP session.
## 2. Purpose
Turn an explicitly selected ERP session into current request authority without
legacy role/bootstrap, default-tenant or empty-branch-means-all fallbacks.
## 3. Scope
Explicit ERP:AuthMode=product, reviewed endpoint boundary, per-request person/
license/registry verification and actual protected Core workspace-context read.
HRM/Procurement business resource adapters remain required and are denied until
reviewed. The subsequent [worker resource adapter](HRM-PRODUCT-WORKER-SCOPE-20261110.md)
adds reviewed HRM worker list/detail/create/update/archive scope; its acceptance
is recorded separately. This feature is not full ERP operational acceptance.
## 4. Ownership
Core owns authentication, selected company/principal/module/branch context and
request proof. Laravel owns enrollment/subscriptions/licensing. Business modules
own their actions and resource/data filters. Perfex responsibilities are unchanged.
## 5. Actors
Verified explicitly enrolled person with current company membership and module
grant; confidential identity/license providers; deployment operator.
## 6. Configuration
Product mode requires the enabled explicit session lifecycle and prerequisites.
Default remains local; existing local/OIDC/hybrid paths are preserved. Product
mode registers only its own default authentication, with no local/bearer fallback.
Do not activate production before full module/data/ingress qualification.
## 7. Boundaries
Server metadata marks reviewed identity/setup/team/invitation/operational routes
and the selected workspace-context endpoint. Only the two existing OAuth callback
paths and mapped health probes have explicit additional public exceptions.
Unknown routes, including anonymous legacy routes, are denied before handlers.
OpenAPI/root/local-login are not implicit public exceptions in product mode.
## 8. Entry
GET /api/core/v1/workspace-context with existing operational identity/browser and
ERP session cookies. No query selectors, authorization header, tenant or legacy
shell/branch/all-branches headers. Exact reviewed HTTPS host; no cross-site fetch.
## 9. Identity
Re-observe the person through existing encrypted credential continuity for every
protected request. Provider failure never reuses an earlier successful request.
Customer/contact/email/domain records create no authority. No local password path
is available for a federated principal in product mode.
## 10. Session
Strict single opaque ERP cookie plus verified parent credential/browser binding,
absolute expiry and exact issuer/subject/client. Resolve current licensing and
registry/Core/grant/branch state. Replaced/revoked/expired bindings fail closed.
## 11. Request proof
A private object-keyed HttpContext.Items proof is created by the authentication
handler after resolution. No client header/claim/JSON/cache can supply it. Its
principal/tenant pair must match the current authenticated principal and its
person/license observations must still be current. No cross-request reuse.
## 12. Principal
Use the resolved Core GUID, tenant, selected company/module and exact explicit
module roles. No email, worker inference, legacy RolesCsv or role-action bootstrap.
Module-specific actions/resources are not granted by this context endpoint.
## 13. Tenant
In product mode, PrincipalTenantAccessor accepts only authenticated product-scheme
tenant claims; otherwise it returns an untrusted marker, never the configured
standalone/default tenant. Classic authentication-only overrides/default behavior
remain unchanged outside product mode. Product startup skips legacy role seeding.
## 14. Scope
Only the selected company and its selected branches. Finite empty scope never
becomes company-wide. Explicit company-wide scope includes current branches of
that company only; it cannot include other companies under a tenant.
WorkLocation and OrgUnit authorization are distinct; business adapters remain held.
## 15. Protected read
The actual Core route uses its owning tenant-filtered database and explicit tenant/
company/branch predicates, not user branch assignments. Missing company/branches
or expired private proof deny. Recheck registry/licensing/Core after the data read
and require the same selected scope before returning metadata.
## 16. Response
Keep existing workspace-context fields; product responses add contextKind,
selectedCompanyId and selectedModule. organisationWide means explicit selected-
company-wide scope here. coreActions is empty: no legacy currency/admin grant is
inherited. UI integration must honor the selected company/module boundary.
## 17. State
Anonymous, current authenticated request, denied/unavailable, expired/revoked or
replaced. A selected cookie is not sufficient alone. No user, workspace, grant,
subscription, credential or registry row is created by protected context reads.
## 18. CSRF and transport
This increment supports only the reviewed GET context policy. Mutation/resource
policies need their own explicit server metadata, CSRF and commit-time scope.
Reuse host-only Secure/HttpOnly cookies. Mark protected responses no-store and
no-cache; expose no token/provider diagnostics. Preserve existing identity guards.
## 19. Rate limits
Apply the existing operational session/IP budget before upstream reads. Limit
exhaustion yields429 without a successful principal. Production proxy trust,
edge limits and load/capacity still require qualification.
## 20. Errors
401 missing/invalid parent or ERP cookie;403 denied membership/license/grant,
unreviewed endpoint, host/cross-site scope;400 unreviewed request selectors;
404 unknown route;429 budget;503 upstream/database unavailable. No redirects,
credential disclosure, cached authority or default-tenant retry.
## 21. Lifecycle
Existing ERP and parent logout/replacement/revocation invalidate the next protected
request. Provider outage preserves recoverable server state but returns no scope.
Key ring/registry restore and absolute expiry retain their existing contracts.
## 22. Audit
No new mutation endpoint or grant event. Existing registry events remain owning
evidence. Future business mutation audits must use the verified tenant/principal,
not an unrelated default tenant. No handles/tokens are logged or returned.
## 23. Compatibility
Legacy workspace context keeps declared assignments/role policies in nonproduct
modes. Standalone local/OIDC, existing ERP applications and one maintained release
remain required. Product mode deliberately denies unreviewed module routes until
resource adapters are implemented, rather than inheriting classic permissions.
## 24. Tests
Actual OAuth cookies/encrypted state, registry and protected Core route with actual
relational queries; synthetic provider responses/licenses. Test finite/company
scope, other company/tenant exclusion, replacement, revoke/archive, outage/denial,
unknown/anonymous routes, selectors, credential ambiguity, logout and no default
tenant/legacy fallback. Retain existing standalone Core workspace-context tests.
## 25. Deployment
Source only. No production authentication mode/flag activation, schema, DNS,
Perfex, mail or payment change. Native migrations/readiness and model consistency
must remain qualified. Worker/tenant scheduling and production ingress/recovery
are separate release gates; default mode remains unchanged.
## 26. Rollback
Restore the prior reviewed mode/configuration; retain session/grant evidence.
Do not weaken product policies or reintroduce legacy fallback to make a flow pass.
Every build/attempt uses a new retained root; no publication is overwritten.
## 27. Acceptance
Qualified:700 Windows passes/44 explicit environment skips and212 native
PostgreSQL-run passes/zero skips. All22 new request-auth cases and four classic
context cases pass in each; focused Windows26/zero skips passes. Classic context
cases use SQLite. New OAuth-cookie/registry/protected Core context uses SQLite/
native PostgreSQL. Person HTTP responses and current-license delegates are
synthetic; this is not real-provider ERP browser or business-operation acceptance.

Evidence: inspection/qualifications/product-request-auth-core-01,
product-request-auth-focused-01, product-request-auth-native-01 and
product-request-auth-verification-01/verification.json.13 compiled C# inputs,
32 independently copied dependencies and586 read-only native runtime files retain
verified hashes. Model drift is zero; both owned containers are stopped/no host
ports. Infrastructure/API/tests retain3/27/21 existing warnings. No production
mode, flag, schema, DNS, Perfex, mail or payment change occurred. Protected context alone does not prove HRM/payroll/Procurement
reads/writes, actual-provider ERP browser entry or full SaaS isolation.
## 28. Programme
Next: product-aware HRM/Procurement action/resource scope, private Core context and
frontend integration, then full real-provider browser/ERP operations and controlled
rollout. Preserve separate GreenWebb/Kuubu login and one SaaS/dedicated/on-premise
ERP codebase. Lenco, hosting/domain/email, monitoring/recovery and offline licensing/
installation/upgrades/backup restore remain required. Keep programme PRs draft.
