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
Because legacy callers omit the tenant until SaveChanges, a stateless client-side
value generator now supplies a null new LocalUser tenant from the current trusted
context before EF tracks its composite key. It captures no tenant across
contexts. SaveChanges retains its existing server tenant authority.

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

The next admission step can preserve this token-free pending cookie: after the
person selects an explicit link, start a fresh PKCE challenge carrying that link
in protected state. Its callback must match the newly verified subject to that
exact link/current local principal and obtain a current commercial lease from
Core's explicit workspace/payer binding. Only then may Core issue a tenant-scoped
local operational session and retain encrypted provider credentials server-side.
This avoids putting refresh tokens in a browser cookie or inventing a default
tenant for a global identity. Session refresh/revocation, current Core roles,
company/branch scope and per-module authorization remain mandatory.

Workspace creation is a separate explicit owner setup command, not a callback
side effect. Team admission belongs to Core invitations/permissions and must not
add staff to Laravel billing memberships. Existing local-user email fields must
not be filled with invented addresses; any requested profile/contact sharing
needs an explicit provider contract and consent.

Licensing client checkpoint: source11879016 adds the default-off machine
commercial channel and validates exact enrollment/module/lease responses.105
focused checks and the complete2,076-case HRM regression passed. This is not
company binding or operational session enforcement. See
`CORE-COMMERCIAL-LICENSE-20261107.md` for exact evidence and
`CORE-FEDERATED-PRINCIPALS-20261107.md` for the next principal prerequisite;
neither a commercial license nor global identity may fabricate local credentials.

Provider/relay follow-through: Kuubu's fixed account relay currently rejects
upstream redirects and lacks personal-enrollment/authorization actions. Add
only fixed Kuubu actions and validate an authorization redirect against the
registered Core callback and original state; never turn this into an open proxy.
Laravel's signed relay-IP middleware also needs the exact authorization path.
The current provider group has a30/minute source-IP quota shared across token,
subject and licensing requests; replace that shared-backend bottleneck with
bounded operation/actor-aware limits before multi-customer activation.

Follow-through source checkpoint: foundationPR3/source62f8971 now implements
the exact signed authorization path and bounded post-protocol actor/enrollment
quotas;57 tests/482 assertions passed on native MySQL. Kuubu websitePR3/source
d3452e6 implements its own enrollment/authorization page and fixed redirect relay;
35 normal checks and its separate real Laravel protocol test passed, plus API
build, browser QA and hosted CI. These changes are not deployed. They qualify
the website/provider slice; real ASP.NET consumer interoperability, workspace
admission and runtime/tenant/module checks remain. See MANUS_HANDOFF.md and the
cross-repository acceptance documents rather than repeating the old source gap.

## 28. Delivery Evidence & Handover
Stacked draft [PR201](https://github.com/georgemunganga/erp/pull/201) targets the
session repair branch/PR200, itself dependent on packagingPR199. Exact compiled
and tested source is `7d67f6d16a414aacf3a8a6242dc94aa9576ff3b6`; source archive
SHA256 `a0086be4a142bfe0e71039eb4cb3137a8c318c50da2813113680fb6d077638a1`.

All59 focused HTTP/session/workspace/relational checks passed, followed by15
genuine PostgreSQL cases (five link constraint scenarios plus ten existing
non-default-session scenarios). The complete HRM suite then passed2,030 cases,
zero failures/zero skips, in7m25s. No original acceptance test was removed or
weakened. The PostgreSQL cases validate the real migration, cross-tenant FK,
uniqueness including revoked links, raw-SQL update/delete rejection, populated
rollback protection, retained migration history and unchanged model snapshot.

Retained gate: `/opt/kuubu-builds/product-consumer-7d67f6d1-04`; successful launcher
`kuubu-product-consumer05.service`, SDK container `kuubu-product-consumer-gate04`.
SDK10.0.403 image digest
`sha256:212fe4cb74627d5adf9817a4088906ae3edff1bd853bb27565d4b46731efe61e`.
The dedicated PostgreSQL test container has no published port and was returned
to its prior stopped state. The SDK test container also exited. Normal focused/
full tests do not inherit the native connection; only the explicit native job
receives the root-private synthetic control connection.

Log SHA256:
- focused: `6afac110033d34ef458a953350847b441bab716ac0217cf7abe0908a0b8bc899`
- native: `58de04d2405762cf5cd17cea76936e5df1a2c7cc3317d018f219599efbd245d6`
- full: `17b33bd81747ba1e40700ca845e4477a81a189dba320ae0da7c8ee6b46da9caa`

A separate read of the five retained `product_identity_*` databases confirmed
the new migration, two-column foreign key and binding trigger in each, with link
row counts0/1/1/1/2 as expected. Receipt:
`/opt/kuubu-builds/product-consumer-native-metadata-01/receipt.json`. This audit
briefly started only that synthetic cluster and returned it to stopped. These
are test database owner checks, not runtime ACL qualification.

Earlier attempts and logs remain retained. Source5232aa22 had missing imports in
the new tests. Source7ce2c4e3 exposed the legacy tenant-stamping/alternate-key
tracking conflict and a new fixture scope lifetime error. Both were repaired in
application/test harness code without changing the established tests; source
a11c55d7 then passed59 focused cases and generated the owning EF scaffold. One
later upload timed out before creating remote files; its launcher stopped before
the helper or any database startup. No retained publication was patched/reused
as mutable output. Subsequent SDK caches are independent plain copies from
stopped mutable workspaces, with unchanged input bytes verified before preserving
source timestamps; no hardlinks connect outputs to historical publications.

GitHub run42395211640/check128470995663 did not start its backend job: its actual
annotation says the account is locked due to a billing issue. Hosted CI success
is not claimed. The independent review and full integration/operational gates
above remain required.

No production deployment, signing/client credential registration, public ingress
or successful browser-to-ERP admission is claimed. Public main remains7b7e3fc7,
qualified release branch6b47bbeb and deployed qualified image1f54ff04 unchanged.
Kuubu's branded login and GreenWebb's white portal login remain live separately;
this source step does not enable Google, Lenco or operational workspace access.
