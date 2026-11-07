# CORE-PRODUCT-IDENTITY — Kuubu product authentication and explicit ERP links

## 1. Feature Identification
Tier3 Core identity integration under GW-ENG-STD-001. Engineering owns the
implementation; production enablement requires separate admission acceptance.

## 2. Purpose & Business Outcome
Keep Kuubu's own sign-in experience while reusing a verified platform identity.
Recognizing a person does not confer company, billing or ERP access.

## 3. Scope & Exclusions
Confidential OAuth code/S256 PKCE consumer, short-lived separate identity cookie
and Core-owned external identity link persistence. ERP session admission,
commercial lease enforcement and workspace/invitation commands remain subsequent
integration steps. No automatic user, company or role creation.

## 4. Parent & Related Features
Laravel personal enrollment/provider PR3; session tenant repair PR200; existing
Core local users, roles, organization context and audit.

## 5. Actors & Participants
Verified Kuubu-enrolled person, confidential Kuubu backend, Laravel provider and
Core organization authority. Billing permission is not a prerequisite for staff.

## 6. Entry Points & Triggers
GET /api/core/product-identity/start challenges through the owned Kuubu experience;
the fixed callback is handled by ASP.NET OAuth. GET session shows only the caller's
verified identity and explicit links. POST logout ends only this identity cookie.

## 7. Preconditions
Default-off flag; reviewed HTTPS endpoints; private confidential credential;
persistent, private Data Protection keys; exact configured public origin. Laravel
must already have explicit personal enrollment. No client secret in public UI.

## 8. Workflow
Create protected state, browser correlation and S256 challenge using ASP.NET's
handler. Exchange code with secret/verifier over a nonredirecting backchannel.
Read the bearer-protected subject resource; validate exact issuer/product,
immutable subject, verification and no operational grant. Store only those
claims in a separate protected cookie expiring no later than the five-minute
proof. Read explicit active same-tenant link/user pairs. Never issue hrm_session.

## 9. Status / State Transitions
Anonymous → correlated challenge → verified product identity → expiry/logout.
No organization admission transition is implemented by this identity step.
Inactive/revoked/archived links and users are omitted, never reactivated.

## 10. Permissions & Data Scope
Pending cookie is not a default authentication scheme and carries no roles or
tenant. Exact issuer/subject may bypass tenant filters only for bootstrap link
lookup and exact matching tenant/user lookup. Other subjects and archived state
are excluded. Operational policies continue using the existing local scheme.

## 11. Business Rules
No email/domain lookup. No billing membership or payment creates a link. A
person may have explicit links to several independent security tenants. Each
tenant permits at most one active local principal for a given issuer/subject.
Database relationship must bind the local user's tenant and ID together.

## 12. Data Fields & Ownership
Core owns external_identity_links: tenant, immutable issuer/subject/local user,
active/archived state and existing actor/timestamp evidence. The provider owns
global identity and personal enrollment. No provider tokens enter browser cookie,
localStorage, query parameters or database through this initial proof step.

## 13. Screens & Data Views
Kuubu retains its own login. Consumer JSON is a backend integration surface;
connected workspace selection remains a later UI step, not a fabricated launch.

## 14. User Experience / Human Behaviour
Missing enrollment needs explicit personal opt-in. Recognized but unlinked users
must receive an empty workspace state and an invitation/setup route once built.
Failure must not look like successful ERP sign-in.

## 15. Notifications & Communications
None in this step. Workspace invitations require a separate recipient acceptance
workflow; identity authentication itself does not send invitations.

## 16. Documents & Attachments
No document access; existing module permissions remain authoritative.

## 17. Exceptions & Edge Cases
Missing/tampered state, missing correlation, callback replay, wrong origin,
PKCE/token error, wrong issuer/product/subject, unverified response, timeout,
redirect, invalid JSON, expired cookie and stale/revoked/foreign-tenant links.

## 18. Integrations / APIs / Events / Sandbox
Use actual ASP.NET OAuth/cookie handlers. Contract is Laravel schema_version1
subject and product:identity scope. Tests use synthetic HTTP backchannel while
exercising real state, correlation, code exchange and cookie protection. Real
Laravel→consumer acceptance remains required, not implied by fixtures.
Framework reference: [ASP.NET Core10 OAuth handler source](https://github.com/dotnet/aspnetcore/blob/v10.0.0/src/Security/Authentication/OAuth/src/OAuthHandler.cs).

## 19. Audit / Security / Privacy
No tokens, code, state, correlation, confidential secret or sensitive provider
error in app logs. Use generic remote-failure response. Existing Core audit covers
link changes once authorized link commands are added. No email stored in links.
Before public ingress, proxy access logging must also omit callback and
authorization query strings; application log redaction alone is insufficient.

## 20. Reports / Dashboards / Metrics
Observe generic auth failure/latency and operational401 rates. Never label
successful personal authentication as provisioned/paid/organization access.

## 21. Configuration & Localization
ERP:ProductIdentity:Enabled=false by default. Expected provider issuer, owned
Kuubu authorization URL, fixed callback/public origin, confidential ID/secret and
DataProtectionPath are private deployment configuration. No generic OIDC mode.

## 22. Non-Functional Requirements
HTTPS; bounded10-second,64KiB backchannel; no redirects/cookie forwarding; secure
HttpOnly host-only SameSite=Lax identity/correlation cookies; no-store responses;
five-minute maximum pending identity proof, no sliding expiration.

## 23. Postconditions
Identity is verified and isolated from operational principal. Default tenant,
ERP users, roles, local sessions, customer records and subscriptions unchanged.

## 24. Failure & Recovery
Fail closed on provider/config/database errors. Disabled feature creates no new
scheme or cookie. Preserve prior image and retained artifacts. New migration is
additive; rollback code while retaining populated link/audit evidence.

## 25. Acceptance Criteria
Actual handler validates correlation/state and sends secret/PKCE, returns only
trusted subject, omits tokens and ERP cookie, and cannot satisfy an ERP policy.
Explicit links cannot match another subject/tenant or inactive local user.
Native migration must enforce the cross-tenant FK and unique link boundary.

## 26. Test Scenarios
HTTP happy/denied/corrupt/replay/timeout/foreign-origin paths; secure cookie flags,
proof expiry, no local-session creation, exact explicit mappings and non-default
tenant isolation. Full HRM regression and real PostgreSQL migration tests needed.

## 27. Dependencies & Release Gates
Own Kuubu authorization relay/opt-in UI, confidential provider deployment,
credential/key delivery, external link invitation/administration commands,
Core workspace admission, runtime ACLs and company/module licensing acceptance
must pass before public SaaS access. Existing financial release holds remain.
The existing anonymous `/experience/speak-up` submission/status routes also need
an explicit product-owned organization context before shared public ingress:
anonymous callers currently have no authenticated tenant. Do not turn a request
header, email/domain or the process-default tenant into customer authorization.

## 28. Delivery Evidence & Handover
Implementation/acceptance evidence will be recorded against exact source commits.
No production enablement or successful end-to-end ERP admission is claimed.
