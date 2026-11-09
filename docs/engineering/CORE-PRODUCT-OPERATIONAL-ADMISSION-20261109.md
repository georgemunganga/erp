# Licensed product admission

## 1. Identification
Tier3 Core feature,9 November2026. Implementation in progress; not live.
## 2. Purpose
Let an explicitly enrolled Kuubu person enter an authorized company and licensed
module with current permissions and branch scope, using the existing ERP.
## 3. Scope
Current increment: separately scoped, email-free Foundation person observation
and strict bounded Core reader. These do not create a session or grant access.
Next: protected OAuth/refresh state, explicit company/module grants and selection,
revocable operational session, route/module enforcement and scoped work context.
## 4. Parent
Core company membership/team/invitations; purchased workspace/licensing contracts.
Keep one ASP.NET/Docker release for shared SaaS and dedicated/on-premise use.
## 5. Actors
Verified personally enrolled person, explicit company member, authorized company
access administrator, commercial license provider and deployment operator.
## 6. Entry
Kuubu keeps its own login. Operational sign-in requests only
`product:identity product:erp-session` from its confidential identity client.
Setup, team, invitation, billing and ordinary identity proofs cannot substitute.
## 7. Preconditions
Current verified personal enrollment; exact product/client/issuer/subject; active
Core identity link/principal/company membership; valid allocated company slot;
current matching subscription modules; explicit module/action/branch grants.
## 8. Workflow
Authenticate through Kuubu, hold provider credentials only in protected server
state, show explicitly authorized companies/modules, confirm selection, then
create the bound session atomically. Recheck current person, link, membership,
license and action/data scope when processing operational requests.
## 9. States
Person observation: verified/denied/unavailable, maximum30 seconds and actual
token expiry. Future operational session: pending selection/active/revoked/expired.
Refreshing a token is not a current identity or company authorization decision.
## 10. Permissions
Company governance roles do not imply HRM, payroll or Procurement permissions.
Module entitlement permits a product capability; it is not a person grant.
Branch scope must be explicit; absence of grants denies access.
## 11. Rules
No email/domain or billing-membership inference. No implicit employee, password,
module role, branch grant or organization-wide access. Federated principals remain
credential-free; the legacy local-session handler continues to reject them.
## 12. Data
Foundation owns verified identity and personal enrollment; Core owns exact links,
companies, memberships, module/action/branch grants and operational sessions.
Provider tokens stay encrypted server-side, absent from cookies/logs/URLs.
Session persistence and grant schema must bind exact tenant/company/principal/
membership/client keys and preserve revocation before activation.
## 13. UI
Reuse existing Kuubu login, company selector, shell and controls. Explicitly
select a company/module; show unavailable/denied/no-company/no-module states.
GreenWebb commercial portal remains separately branded and owns billing access.
## 14. UX
Return to the same workflow after reauthentication; never silently switch a
company or widen a branch. Handle provider failure separately from revocation.
## 15. Notifications
No new email for identity observations. Future consequential grant/revoke/session
commands commit audit and ID-only events with the domain transaction.
## 16. Documents
No new personnel/financial document access from identity proof. Existing module
document permissions remain necessary.
## 17. Errors
Provider401/403/404 means denied; timeout, malformed/ambiguous/foreign/stale proof
or transport failure means unavailable. Neither reuses old proof. Caller
cancellation propagates. No raw token, provider body or identity directory leaks.
## 18. Integration
Default-off `operational-subject` provider resource: schema1, fixed issuer,
subject/client/product/purpose, verified flag, observed/deadline timestamps and
`operational_access_granted=false`. No selectors, email, company, roles or payer.
Core validates the exact field set, purpose, size and wall/monotonic lifetime.
## 19. Audit/privacy
Read-only identity observation creates no commercial/company record. OAuth token
issuance/refresh is separately owned by Passport. Operational session/grant audit
must contain references/outcomes, never credentials or raw proof bodies.
## 20. Configuration
`PRODUCT_ERP_SESSION_ENABLED=false` plus existing product identity flag. Only a
confidential Kuubu identity client may request the exact two scopes. No ERP
operational registration/route or production activation in this increment.
## 21. Security
Test real PKCE/token/refresh, current verification/enrollment/client/token
revocation, public/foreign/machine/cookie denials, scope mixing and default-off.
Core rejects extra/duplicate/missing/incorrect fields, foreign purpose, oversized
and non-JSON proof, clock rollback, transit expiry and cached-proof reuse.
## 22. Operations
Preserve classic local authentication and its deployment behavior. Before enabling
SaaS operational sessions, qualify key storage/rotation, refresh concurrency,
revocation, ingress/CSRF, rate limits, monitoring, recovery and privilege mapping.
## 23. Postconditions
A verified observation proves only the current person/product context. No ERP
session, membership, license, module action or branch permission follows from it.
## 24. Recovery
Provider denial revokes admission; outage fails closed without deleting company
data. Refresh rotation must be atomic across processes, bound to original client/
subject and absolute session lifetime; ambiguous refresh needs explicit recovery.
## 25. Acceptance
Complete when real sign-in/selection/operations/revocation flows are verified
through browser, provider, Core, database and protected module endpoints, with
cross-company/branch/module denial and unchanged dedicated/local authentication.
## 26. Tests
Current proof contract has provider and reader tests; evidence is recorded after
execution. Full session/grants/route isolation remain separate required checks.
## 27. Release gates
No SaaS operational activation until all acceptance checks pass. Lenco/hosting
integrations and dedicated/offline licensing/installation/upgrades/backup restore
remain part of the full platform programme, with separate deployment evidence.
## 28. Evidence
Pending fresh provider and Core qualification. Existing company, invitation and
mail evidence retains its recorded sources and does not prove operational access.
