# Core company-team HTTP boundary

## 1. Identification
Tier 3 Core child feature, 8 November 2026. Implementation increment; not deployed.
## 2. Purpose
Expose company governance through an explicitly requested product sign-in, while
preserving the separation between identity, commercial entitlement and ERP access.
## 3. Scope
This increment implements team sign-in, session/CSRF discovery, cancellation,
company discovery, licensed member reads and the role/revoke HTTP adapter.
The existing internal commands remain the authoritative mutation path. Invitations
and the team screen remain unfinished; this changed source is under qualification.
## 4. Parent
CORE-COMPANY-TEAM-20261108.md and protected product identity/setup. One ERP release
must continue supporting shared SaaS and dedicated installations.
## 5. Actors
Verified product identity. A team-session proof alone establishes no company role.
## 6. Entry
GET /api/core/product-identity/team/start creates signed company-team-v1 OAuth
intent and a fixed /team return path. It requests product:identity only.
The /team screen is a subsequent UI deliverable, not supplied by this API change.
## 7. Preconditions
ProductIdentity, ProductLicensing and CompanyTeam flags must all be enabled.
Flags are off by default. Existing reviewed HTTPS origin/issuer/client/key checks
apply. This increment does not enable any production flag.
## 8. Workflow
OAuth correlation/PKCE and verified provider response establish a short-lived
pending cookie. A team intent adds a cryptographically random per-sign-in nonce.
GET /team/session returns expiry and an ASP.NET Core antiforgery request token.
POST /team/cancel validates origin, JSON content type and antiforgery before
clearing the pending cookie. No browser-supplied return destination is accepted.
Company discovery lists only explicit current owner/administrator memberships.
It provides navigation metadata with license_check_required=true, not a license
grant. Member reads and every command resolve the enrollment from Core's immutable
binding, obtain a fresh commercial license and recheck current Core authority.
## 9. States
Absent, valid team proof or expired/invalid proof. Setup and ordinary product
proofs cannot be promoted into a team proof by a browser request.
## 10. Permissions
No team-session response grants company membership, branch/module permissions or
an ERP session. Member reads/commands check fresh company authority and the company
binding's current commercial license. Owners alone assign roles; administrators
can revoke only ordinary members. No command can reactivate inactive membership.
## 11. Rules
Only exact signed purpose, issuer, canonical subject, nonce and expiry pass.
Mixed setup/team intents fail OAuth callback. A setup handle or setup-purpose
property invalidates a team proof. Never infer organization access from email.
## 12. Data
Signed cookie properties retain purpose; claims retain issuer/subject/nonce. No
provider bearer is saved for team management. No database/model migration.
## 13. UI
Existing Kuubu branding/login retained. Future /team UI must fetch this session
and keep the request token in memory; no token in URL or browser persistent storage.
## 14. UX
Explicit team-management entry. Invalid/expired proof requires a fresh sign-in.
Company/role confirmation must precede the future mutation endpoint.
## 15. Notifications
No email, invitations or external notifications in this increment.
## 16. Documents
No company personnel, payroll, accounting or document data returned.
## 17. Errors
Disabled endpoints return 503; invalid proof 401; invalid origin/content type or
antiforgery 403. Never include cookie, state, nonce or token values in diagnostics.
## 18. API
Prefix /api/core/product-identity/team: GET start, GET session, POST cancel,
GET companies, GET members and POST commands.
Session includes verified, product_code, purpose, expires_at, csrf_token and
operational_access_granted=false. Responses are no-store under the origin guard.
Companies accepts only optional after; members requires company_id/link_id and
optional after. Cursors and selectors are nonempty canonical lowercase UUIDs;
duplicates/unknown query fields fail400. Pages scan at most21 rows and return20
with next_cursor; company cursors advance past unavailable identities, so a page
may be empty while still providing a next cursor. Neither directory returns a
tenant, payer, enrollment, principal ID, global subject or email. Member state
separates membership_active from identity_available and includes its revision.

Commands accepts at most16KiB JSON, with exact fields company_id, link_id,
membership_id, command_key, expected_revision, operation and confirmed=true.
role-change also requires role (owner/administrator/member); revoke forbids role.
Unknown/duplicate/malformed fields and browser actor/tenant/payer authority fail400;
oversize fails413. JSON depth is bounded. All writes require the team proof,
same-origin JSON and the per-sign-in antiforgery token. The HTTP adapter derives
issuer/subject from the signed proof. UUID selectors never confer access.

Changed/Unchanged/Existing return200; invalid400; denied403; revision/key conflict
or LastOwner409; unavailable503. Retry obtains a fresh license and Core permission
before reading the historical receipt. An Existing result is a historical command
acknowledgment, not evidence of current member access.
## 19. Events
Read/session endpoints emit no business mutation. Role/revoke commands emit their
existing immutable receipts and transactional audit/outbox; no second HTTP-layer
event is emitted. No-op commands reserve a receipt without a false change event.
## 20. Configuration
ERP:CompanyTeam:Enabled is explicit opt-in alongside identity/licensing. The
antiforgery cookie is __Host-kuubu_company_team_csrf, Path=/, Secure, HttpOnly,
SameSite=Strict, without Domain; request header X-Kuubu-Team-CSRF.
CoreCompanyTeamApi registration uses the reviewed PostgreSQL connection and a
trusted-scope context factory; it never uses a browser tenant or process default
as authority. Existing company team management does not require setup/provisioning
to be enabled. Feature flags and credentials are deployment configuration, not a
browser toggle; enabling them requires the registered services and reviewed keys.
## 21. Security
Use IAntiforgery rather than a custom token algorithm. Temporarily bind token
generation/validation to a NameIdentifier containing verified issuer/subject and
signed nonce, then restore the original HttpContext.User in finally. This binds
tokens to one sign-in without authenticating the pending person in the ERP scheme.
Same-origin JSON checks supplement the cookie/token pair. Provider scopes remain
minimal and the identity cookie retains its five-minute, non-sliding lifetime.
## 22. Privacy
Return no payer, email/domain, tenant, local principal, membership or module data.
## 23. Reliability
No database writes or delivery jobs. Identity key persistence/rotation and public
TLS/proxy/log redaction still require deployment qualification.
## 24. Recovery
Cancellation clears this browser cookie. Like existing pending identity logout,
this is not server-side revocation of a copied cookie: it can remain valid until
its short expiry. Future mutation handlers must recheck current Core membership
and commercial license on every request, including idempotent retries.
## 25. Acceptance
Run real framework OAuth/cookie/antiforgery handlers in isolated TestServer. Check
intent/scope, secure cookie flags, no ERP admission, unrelated/mixed/expired proof,
forged/missing token/cookie, foreign origin, non-JSON and cross-sign-in token reuse.
## 26. Release
No production change. New source needs its own qualification; the prior 3cc77532
native/full/browser receipts qualify that earlier source only.

The company/member directory and mutation adapter are a newer increment than the
signed-proof gate below. Qualification is in progress. The initial adapter build
identified ASP0016 on the command method group: it could be selected as a
RequestDelegate and discard IResult. The mapping now explicitly uses the route
handler delegate type; a new independent build is required. No failed output was
patched or enabled. Do not present the older263-case pass as proof of this adapter.

The independent API-local-02 build and API-tests-01 test build passed with zero
errors; ASP0016 is gone. The current Windows focused gate passed331 tests, zero
failures, with12 environment-dependent opt-ins skipped. All43 new HTTP-to-Core
database cases passed: company/member paging, confirmations/strict fields,
origin/CSRF, exact company/link/subject scope, current Core state and fresh license
on retry, revocation during license retrieval, owner/admin limits, last-owner
denial, receipts/audit/outbox/no-op behavior and no operational grants.
Evidence: inspection/qualifications/company-team-api-tests-01/verification.json.
Native PostgreSQL and broader Linux/actual provider/browser refresh remain next.

Earlier signed-proof source dfe69f81531546ba547adfde42d516f7f3ec1eb3:
Windows SDK10.0.403 full dependency build passed (zero errors). A fresh test-only
build with independently copied unchanged dependency assemblies passed263 focused
tests, zero failures, with12 environment-dependent opt-ins skipped. All16 new
team-boundary cases passed using real OAuth/cookie/antiforgery framework handlers.
Current-source Linux/native/full and actual provider/browser qualification remain
pending; this is not public ingress or operational-session acceptance.

Evidence: inspection/qualifications/company-team-http-local-04/verification02.json,
build.log, focused.log and test-results/focused.trx. Its API DLL hash matches the
successful full dependency build01. First-run failure01 retained an overly strict
exact cache-header assertion; it now checks the required NoStore directive and
allows the framework's additional no-cache protection. Independent copy02 was not
built. Test-only attempt03 lacked the API apphost artifact; failed preparation is
retained. Attempt04 includes that unchanged artifact and fresh restore metadata.
No prior publication was rebuilt or patched and no hardlinks were introduced.
## 27. Ownership
Core owns company governance. Laravel owns verified product identity and purchased
entitlement. Neither customer billing records nor payer roles substitute for Core
company membership. Perfex remains unchanged.
## 28. Remaining work
Qualify the company/member directory and role/revoke HTTP adapter; implement
invitations/acceptance/delivery and UI, operational sessions,
tenant/module isolation, deployment/recovery and dedicated installation acceptance.
