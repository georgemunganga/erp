# Explicit Kuubu company invitations

## 1. Identification
Tier3 Core child feature, 8 November2026. Implementation in progress; not live.
## 2. Purpose
Let an authorized company actor invite a person who explicitly accepts access.
## 3. Scope
Separate confidential-client recipient identity observation and strict Core
reader, followed by internal invitation issue/revoke/accept commands, persistence,
immutable receipts/audit and durable delivery intent. Sender issue/list/revoke
HTTP endpoints and company-team UI are implemented behind a default-off flag.
Kuubu account recipient consent/relay is implemented and tested separately.
Core protected recipient handoff, review and acceptance HTTP are implemented
behind a separate default-off flag. ERP recipient UI is implemented and tested
with synthetic API responses and now qualified against actual Laravel/Core
handlers in an isolated browser fixture. Guest controls and delivery remain.
No production activation.
## 4. Parent
CORE-COMPANY-TEAM-20261108.md; one ERP release for SaaS and dedicated installations.
## 5. Actors
Current company owner/admin, invited verified and explicitly enrolled identity.
## 6. Entry
Invitation creation must explicitly name recipient, company and bounded role.
Acceptance must review the company/role and require an unchecked consent control.
## 7. Preconditions
Inviter's current Core authority and company license; recipient's verified current
email and explicit personal Kuubu enrollment. No payer membership is required.
## 8. Workflow
Issue commits invitation/audit/durable delivery intent before attempting delivery.
Acceptance checks a protected invitation token, current recipient observation,
current company/license/inviter authority, explicit confirmation and then commits
exact principal/link/membership/audit/outbox together. No automatic acceptance.

Recipient HTTP/browser contract (Core HTTP, account consent and ERP UI implemented; joint real-provider gate passed locally):

- The fixed `/accept-invitation#token=` landing consumes and removes the fragment
  immediately. Never put the secret in a query, OAuth state, redirect, browser
  storage, logs or referrer. Before redirecting for authentication, hold it in
  short-lived encrypted server state bound to an opaque secure HttpOnly cookie;
  purpose, browser binding and expiry must survive only the reviewed callback.
- Use a distinct invitation-purpose authorization, requesting exactly
  `product:identity product:invitation-recipient`. Kuubu account consent must
  explain recipient verification and require explicit unchecked confirmation;
  ordinary identity, setup and team proofs cannot stand in for it. Account consent
  is not company acceptance. No provider access token is saved in a browser cookie.
- The callback binds the authenticated subject/client to the protected intent.
  Review reads fresh recipient observation and exact targeted invitation, then
  current company/license/inviter authority before revealing company name/role.
  Missing/expired/wrong-recipient invitations reveal no company metadata.
- Acceptance is a separate same-origin JSON/CSRF-protected command with unchecked
  company/role confirmation, stable request key and expected reviewed revision.
  Recipient email/issuer/subject, inviter, tenant, payer and role come from trusted
  server records, never browser overrides. Refresh recipient observation and
  license immediately before the atomic existing domain command.
- Cancel, expiry, account switching and new sign-in revoke prior protected intent
  and grants. Ambiguous retry retains the exact reviewed request but still checks
  current identity/inviter/license; an old accepted receipt cannot restore revoked
  access. Successful acceptance creates no default ERP operational session.
## 9. States
Pending at revision1; accepted/revoked at revision2. Expiry is computed from
ExpiresAt without rewriting history. Terminal outcomes cannot be repurposed.
## 10. Permissions
Owner may invite administrators/members; administrator may invite ordinary members only.
Owner promotion remains an explicit owner-authorized action. Server enforces all.
## 11. Rules
Email match alone grants nothing. Require an exact targeted invitation, token,
verified identity, enrollment, explicit acceptance and current Core guards.
Do not canonicalize provider aliases/dots or infer access from email domains.
## 12. Data
Invitation: immutable company/target hash/role/issuer/inviter binding, SHA256 secret
hash,48-hour expiry, revision, actor and accepted exact subject/member references.
Token is32 random bytes. Recipient hash is SHA256 of the provider-compatible
trimmed/lowercase address; hashing does not anonymize it. No alias/domain inference.
No plain token/email in receipts/audit/outbox. ProtectedDelivery encrypts recipient
and fixed /accept-invitation#token= link through Data Protection. Delivery intent
contains IDs only; worker must recheck expiry/state/license/inviter before sending.
Key-ring backup/rotation, retention/erasure and actual mail transport remain release
gates. Scoped company/principal/link/member FKs and guarded terminal transitions
apply in ORM and PostgreSQL; receipts are append-only. Populated rollback refuses
to discard invitation history or delivery obligations.
## 13. UI
Preserve Kuubu login. Separate company invitations from GreenWebb billing-team UI.
The `/accept-invitation` page uses the shared Kuubu semantic theme and controls,
with generated route registration and no default ERP auth/branding requests.
Blocking self-hosted bootstrap removes the fragment/query before hydration and
provides one-use memory-only access to a correctly shaped fragment secret. The
page and error reporter do not send invitation context to telemetry. No secret or
CSRF data enters browser storage; router scroll-position metadata is permitted.
Fresh links always require explicit handoff; a previous grant cannot silently
select its old company. Company/role review requires unchecked confirmation;
an uncertain acceptance locks the reviewed request/key for exact retry. Module
permissions and operational access remain separate from the receipt acknowledgement.
Company-team UI shows invitations only when the server advertises availability.
Review company/email/role, require unchecked confirmation and preserve exact
request/key after an uncertain response. Lock surrounding actions during review.
Refresh current state after a receipt; never treat historical receipt as current
status or claim email was sent. Owners can invite administrators/members;
administrators can invite members only. Mobile controls wrap inside the viewport.
## 14. UX
Review recipient/company/role before issue; generic invalid/expired acceptance
states without company disclosure; no fake sent/accepted acknowledgements.
## 15. Notifications
Delivery cannot create membership. Queued retry/deduplication and real mail
provider qualification remain required; local acceptance uses mail=array only.
## 16. Documents
No employee, payroll or financial records exposed by recipient proof.
## 17. Errors
Default deny on missing scope, wrong client/product, unavailable provider,
unverified/un-enrolled identity, stale proof, expiry, revoked invitation or license.
## 18. Integration
Laravel adds a separately scoped confidential-Kuubu-only resource:
GET /api/v1/product-identity/invitation-recipient,
scope product:invitation-recipient together with product:identity. It returns
schema1, reviewed issuer, current subject/client/product, verified_email,
email_verified=true, purpose=company-invitation-v1, observed_at/proof_until,
and operational_access_granted=false. Proof is short-lived and server-consumed;
not a browser-supplied email, signed admission token or membership grant.
Normal subject responses continue to exclude email. Kuubu account authorization
UI/relay now supports this exact separate scope pair and requires unchecked
recipient-verification consent. It explains verification is not company acceptance;
mixed scopes and substituted setup consent fail closed. Core now requests and
consumes the distinct purpose in isolated HTTP tests. Actual provider-to-Core
recipient interoperability passed the local browser gate below. Deployed TLS
and ingress remain unqualified. Backend flag remains default-off.
## 19. Audit/privacy
Return only the current person's email to their scoped confidential product
client. Reject selectors; no user lookup, email directory, payer or company list.
Never log raw proof bodies, provider tokens, invitation tokens or OAuth state.
## 20. Configuration
ERP:CompanyInvitationAcceptance:Enabled=false requires existing identity, team and
licensing flags. ERP:CompanyInvitationAcceptance:IntentPath must be an existing
private absolute directory outside application assets and separate from the key
ring/setup state. Linux directories are0700 and files0600. Protected bootstrap
and intent cookies use distinct Data Protection purposes, secure HttpOnly host
cookies, Lax callback semantics and five-minute expiry. JSON writes require exact
same-origin and X-Kuubu-Invitation-CSRF; cookie-only or another workflow's proof
cannot accept. Routes: /api/core/product-identity/invitation/{session,prepare,start,
review,accept,cancel}. Tokens stay encrypted server-side; OAuth state carries only
opaque intent binding. New purpose sign-in revokes prior workflow handles.
ERP:CompanyInvitations:Enabled=false additionally requires the existing team,
identity, provisioning and licensing flags. Sender routes reuse team-purpose
proof, exact actor/link/company authority, live license checks and nonce-bound
antiforgery. Same-origin JSON writes reject unknown/duplicate fields, unconfirmed
requests and oversized bodies. Bounded list returns current recipient metadata
only to authorized owners/admins; protected envelopes and tokens never leave Core.
PRODUCT_INVITATION_RECIPIENT_ENABLED=false plus existing identity flag. No schema
or production configuration change for the recipient contract. Later Core
invitations need their own default-off activation gate; classic local auth stays.
## 21. Security
Test real PKCE/tokens, scope escalation, public/foreign/licensing clients, revoked
tokens/client/binding/enrollment/verification, cookie-only calls, selectors and
default-off behavior. Core validates exact issuer/subject/client/product/purpose,
bounded observation time, email syntax and operational_access_granted=false.
## 22. Operations
No real mail, customer access, production deployment or DNS changes in this stage.
Unusable intents, retained exclusive callback reservations and revocation markers
are purged after ten minutes; grants expire within five minutes from preparation.
Claim reservation uses persistent exclusive CreateNew. Files held by readers may
remain until cleanup; markers already make them unusable. New claims cannot undo
cancellation. Qualify shared filesystem/permissions, guest prepare rate/volume
controls and path-only ingress logging before public activation.
## 23. Postconditions
Recipient observation creates no customer, enrollment, subscription or company
membership. Later accepted invitation creates only consented Core governance.
## 24. Recovery
Lost responses retry the same operation key. No replay may resurrect revoked
membership. Migrations must preserve populated invitations and immutable receipts.
Issue retries return the historical receipt after current role/license checks;
new keys conflict while the same recipient has an unexpired pending invitation.
Acceptance retries require a still-usable explicit member/link/principal and
current inviter/recipient/license. Active members cannot use acceptance as a role
change. Inactive members need a new invitation and consent, which restores only
the requested bounded role and advances membership revision. All membership,
identity-link, receipt, audit and outbox writes roll back together on post-save
expiry, cancellation or failure. Receipt acknowledgements never establish access.
## 25. Acceptance
Explicit targeted acceptance and fresh provider/Core guards; zero operational
sessions, credentials or module-role grants. Complete flow still unqualified.
## 26. Tests
Recipient protocol and strict Core reader first; native transactional/concurrency,
delivery retry, browser acceptance, cross-tenant and recovery gates afterward.
## 27. Release gates
Do not enable this contract before account consent/relay, Core invitations,
delivery, migrations/runtime grants, audit and complete browser flow qualify.
## 28. Evidence
### Actual-provider recipient browser checkpoint

The opt-in `ProductInvitationBrowserInteropTests` and
`tools/ProductInvitationBrowser/browser.mjs` passed one real-provider browser
case with zero skips in133.189s. Baselines: Core APIacc3ae39, ERP UI067362a0,
Foundation15707bd9 and Kuubu website02f42d39. Test assembly compiled with zero
errors/20 existing warnings, referencing independently copied qualified runtime.
Evidence: inspection/qualifications/real-recipient-browser-01/verification.json;
test build: inspection/qualifications/real-recipient-api-01.

Both synthetic verified people began without personal enrollment; no Core
preaccepted staff membership was seeded. Actual login, explicit product joining,
owner setup/creation/exact retry, sender invitation issue, account switching,
recipient login/joining, unchecked recipient-verification consent and unchecked
company/role confirmation all use the real handlers. Before company acceptance,
only the owner existed in Core. A deliberately dropped response after committed
acceptance was retried with the same body/key, returning the existing receipt.
Actual subscription suspension then denied the same acceptance replay403.

Independent read-only SQLite audit found one company, two exact federated
principal/link/memberships, one accepted invitation at revision2, two immutable
invitation receipts, four matching audits and five outbox records. Recipient
request hash and command/audit/event correlations match. No local passwords,
credential links, operational sessions, team-change receipts or module grants.
Foundation retains two users/two explicit personal enrollments and only the
owner's payer membership; joining/accepting creates no recipient billing access.
490 ERP output files and25 website dist files retained hash/size/nsmtime;
136 Foundation source files and49 copied dependency DLLs retained hashes.
Consent/review screenshots inspected. All owned services stopped/ports closed.

Transport intercepts every browser redirect under the reviewed HTTPS origins
and forwards only to isolated loopback handlers, with DNS disabled and no service
workers. This proves handler/browser interoperability, not deployed TLS/ingress.
The marker-protected test-only delivery preview reads the committed protected
intent; it does not prove mail delivery. No production flag, schema, payment,
mail, Perfex, DNS, main/UI-main/B1 or PR201 draft-state change. Anonymous prepare
capacity controls, real delivery and licensed operational admission remain gates.

### ERP recipient page checkpoint

Published UI067362a0e4cf1f42b4fb427c5fc06446e6023662 adds entry/handoff, targeted
company/role review, unchecked confirmation, stable-key acceptance retry, denial,
expiry and end-sign-in states. Shared Kuubu green theme/controls reused. Source
matches the tested build, including generated route tree; early fragment bootstrap
and route telemetry exclusion prevent secret-bearing URL/error state persistence.

82 frontend contracts passed, ESLint, production build and TypeScript passed.
Eight recipient browser scenarios passed: early removal/explicit preparation,
review/confirmation, response-loss exact retry, fresh link with previous grant,
unavailable/missing link, denied recipient, cancel and390px mobile layout. Storage
asserts empty localStorage and only finite router scroll positions in sessionStorage,
with no invitation/CSRF secret. Existing14 team cases passed on the same application
before the wrapper-only visual fix; no team code changed afterward. Desktop/mobile
render inspected; final490 output files retained hash/size/nsmtime and services
stopped. React checklist covers shared controls, abort/stale response handling,
per-instance transient refs, primitive effect dependencies and labelled controls.

UI01 retains the no-control-regex lint correction. UI02 retains five recipient/
14 team passes and three overstrict storage assertions that rejected router
metadata; corrected assertions explicitly check permitted metadata and secret
absence. UI03 passes eight cases, with missing Kuubu theme wrapper found in visual
QA; UI04 fixes the wrapper and passes. No application privacy assertion was removed.
Evidence: inspection/qualifications/recipient-ui-04/verification.json, with the
earlier team gate in recipient-ui-02. These browser cases use synthetic API responses;
native API/store qualification below is separate. New real provider→Core→ERP-page
recipient acceptance is still unqualified. No production deployment or activation.

### Native recipient API and filesystem checkpoint

Published runtimeacc3ae39e7f4e42973f69f5d5bc05eb3cd0d7929 passed37 recipient
HTTP/store cases,20 sender HTTP cases and97 invitation/team regressions on Linux
with genuine PostgreSQL, zero failures/skips. The focused Linux SQLite cohort
passed300/14 native skips. Callback reservations/marker revocation,160 competing
callbacks, reader contention, Unix file privacy and new recipient queries are
exercised by the native gate. Independent copied unchanged dependencies retain
hash/size/nsmtime; fresh API/tests restore/build do not use old restore metadata.

Read-only SQL audit checked141 new databases (140 current invitation schemas,
one exact legacy rollback fixture);69 invitations/76 receipts/69 delivery intents/
81 matching audits/150 matching events and five accepted memberships. Exact actor,
tenant/company, recipient, immutable history and correlation assertions passed;
zero credentials/sessions/module grants. Prior databases excluded by pre-run
inventory. Isolated SDK/test database stopped; no public ports or production
mutations. Evidence: inspection/qualifications/recipient-native-acc3ae39-01/
verification.json. Actual recipient provider/browser wire and ERP page still need
joint qualification; native results are not live customer acceptance.

### Core recipient handoff and acceptance HTTP checkpoint

Published runtimeacc3ae39e7f4e42973f69f5d5bc05eb3cd0d7929 adds protected browser/
intent binding, exact invitation-purpose OAuth, opaque authenticated grant and
fresh recipient/license/inviter checks for review and explicit atomic acceptance.
No browser company/tenant/email/role overrides; no provider token in cookies or
responses. Ordinary sign-in cannot reuse the handoff. Callback replay, cancellation,
cookie-purpose substitution, malformed/forged requests and inviter revocation deny.

Fresh API/tests compile with27/20 existing warnings, zero errors. Final local gate
passed300 checks/14 native skips:13 intent-store and24 new HTTP cases alongside
the existing identity/setup/team/constraint cohort. Concurrency case runs20 rounds
of eight callback attempts (160 total), exactly one grant per intent. A held file
reader cannot restore consumed or cancelled state. Accept/replay checks preserve
one membership effect and no credentials/sessions/module grants.

Attempt05 retained298 passes/one claim-concurrency failure: rename-based claiming
could produce two grants. Final implementation retains an exclusive CreateNew
reservation until cleanup and treats revocation markers as authoritative even
when file deletion is blocked. No assertion was removed; the race was strengthened.
Attempt01's RegisteredName compile correction and all later build/test attempts
remain retained. Source hashes, copies, API hash and unchanged independent
dependency DLLs: inspection/qualifications/recipient-core-07/verification.json.

Normal actual Laravel/website/Core/SSR compatibility passed95.070s at the pre-claim-
fix API05 checkpoint, website02f42d39 and unchanged team UI: own login/Google off,
setup/create/retry, fresh team sign-in, sender issue/revoke, role/retry/license
denial. Acceptance flag was off; it does not exercise the modified claim path.
Independent SQL audit verifies the prior two-member/five-audit/six-event scope,
zero credentials/sessions/module grants and485 unchanged SSR files; owned services
stopped. Source snapshot/evidence: recipient-core-05/source-before-claim-fix.json
and recipient-core-normal-browser-01/verification.json. Current new acceptance
cases use a synthetic HTTP provider and SQLite; they do not qualify the new real
recipient wire, PostgreSQL/filesystem semantics, recipient ERP UI or public TLS.
Those, guest prepare controls and actual delivery remain release gates. No schema,
production configuration, application deployment, payment, DNS or Perfex change.

### Earlier account-side recipient consent checkpoint

Website source02f42d39880e587b4ccaa4b0f650f1e6a2984219 passed42 account tests,
two existing opt-in skips, API-mode client/SSR builds,14 rendered pages/252 links
and connected entry checks. Seven synthetic browser scenarios passed: unchecked/
checked gating,390px checkbox sizing/no overflow, same-person email change,
account change, own branded login/Google unavailable, ordinary identity and mixed
scope denial. All25 dist files retained hash/size/nsmtime; owned service stopped.
Seven changed blobs verified byte-exact and remote comparison confirms exactly
one fast-forward commit/seven changes fromb01985ee, with expected-head lease.
Retained attempts caught a test error-message mismatch, checkbox flex shrink and
missing email dependency; final gate preserves the relevant assertions.

Evidence: inspection/qualifications/recipient-consent-website-04. This is the
account-side scope/consent contract, using synthetic API responses. New real
provider recipient wire, Core encrypted intent/callback, review/acceptance HTTP/UI
and delivery remain unqualified. Sender runtime is unchanged. No production flag,
configuration, payment, DNS, Perfex or application deployment change.

### Earlier sender HTTP and UI checkpoint

Default-off sender API/UI adds licensed scoped issue/list/revoke, bounded paging,
team-purpose proof/CSRF and explicit reviewed confirmation. Local focused gate
passed263 tests/14 native skips, including20 sender HTTP cases. Frontend contracts
passed72 checks; ESLint, production build and TypeScript passed. Browser gate
passed14 scenarios, including response-loss exact retry, administrator limits,
capability-off behavior, existing member actions and mobile layout. Attempt01
retains one selector failure: modal hides the background company selector from
the accessibility tree; includeHidden preserves the disabled-control assertion.
Attempt02 passed with identical independently copied production output.

Actual frozen Laravel/website/Core/SSR browser integration passed40.374s:
own branded login/Google off, setup consent/create/retry, fresh team sign-in,
invitation review/issue/list/revoke, role/revoke/exact retry and suspension denial.
Read-only audit verifies one revoked invitation/two invitation receipts, unchanged
two memberships/two federated identities, five audits/six events, and zero
credentials/sessions/module grants. Invitation creation did not add a member.
Preaccepted staff remains a fixture, not an acceptance-flow qualification.
All485 production output files retained hash/size/nsmtime; owned services stopped.

Evidence: inspection/qualifications/invitation-sender-api-02,
invitation-sender-ui-02/verification.json and
invitation-sender-browser-01/verification.json. Source hashes are in the UI
verification record. New sender HTTP PostgreSQL qualification passed20 sender and
97 invitation/team regression cases, zero skips/failures, on published runtime
e14982865b94b3d147ea7a019c4947ee6c22d02e. Attempt01 stopped at compilation because
the helper omitted unchanged Procurement references; attempt02 includes all
unchanged backend dependencies as87 independent files, unchanged after consumers.
No previous project restore/clean metadata was copied. Fresh API/test builds
passed with27/20 existing warnings and no errors; the isolated database was
restored to stopped. Evidence: inspection/qualifications/
invitation-sender-native-e1498286-02. Independent read-only SQL audit checked118
new databases:117 current invitation schemas and one exact retained legacy team
rollback signature. It verified47 invitations/53 receipts/47 delivery intents/
57 matching audits/104 matching events and four accepted memberships, exact
tenant/company/inviter/recipient bindings, immutable history constraints and no
credentials/sessions/module roles. Prior databases were excluded using the
pre-run inventory. Audit restarted only the isolated test database, then stopped
it again. Read-only private ERP readiness and Perfex login both returned200.
Earlier native
lifecycle qualification below covers the unchanged domain/schema only. Recipient
wire/consent/acceptance HTTP/UI and actual delivery remain outstanding. No live
configuration, production application, payment, DNS or Perfex change.

### Qualified internal lifecycle; public/delivery integration pending

Published source a419439942a77db22a968ef5d94bb2c10f53921e passed97 genuine
PostgreSQL invitation/team checks and90 shared identity/session/schema checks,
zero skips. Focused Linux passed261/14 native skips; full Linux passed2,355/18
opt-in skips, zero failures. All14 native-only opt-ins ran in the separate
cohorts. The four new native cases exercised real concurrent issue/accept,
revoke/accept races, history triggers and empty/populated rollback.

Read-only SQL audit verified97 new company/team databases:96 have the current
invitation schema, six validated FKs and both enabled history guards; one legacy
team-command rollback fixture intentionally retains the previous schema, two
members and its guarded role-change receipt. EF can commit an earlier empty
Down migration before a later populated-history guard refuses another Down;
reapply the qualified latest schema before current-runtime admission. The audit
verifies that exact legacy signature rather than altering the fixture. There
are24 invitations,29 immutable invitation receipts,24 delivery intents,33 matching
audits and57 events, including four accepted memberships. Request hashes,
actor/company/tenant/subject links, before/after revisions and correlations match.
No credentials, operational sessions or module roles were created.

Native attempt01 passed97, then the original64MB /dev/shm test database exhausted
shared memory and entered recovery;23 shared cases failed and later phases did
not run. No application assertion changed. Attempt02 uses a fresh isolated
PostgreSQL container with512MB shared memory, the same pinned engine image and
586 independently copied byte-identical binary input files. All hashes/sizes/
mtime remained unchanged after consumers. Both test clusters are stopped. Source
archive909df1c6ba7a869b3258375527e410503ea9c6e88a1e517cf2c5608a86ccaf7c;
full log6c2c1683b04885c3b7f1ebecc6d22624d8f1ed6362e5be21efb7306d44e09b20;
auditb4a0a7f68da91196456a89974753048edf4ea4398cf0483114404827e700051f.
Audit02's initial missing-table assumption failed on the intentional older
rollback fixture; audit03 retains that result and classifies the exact older
schema read-only. No test database was upgraded to make the audit pass.

Actual existing website/Laravel/Core/SSR browser compatibility passed47.167s
against this API and the unchanged UI661c30a0/websiteb01985ee. It covers own Kuubu
login/Google unavailable, explicit enrollment/setup consent, company create/retry,
fresh team sign-in, licensed reads, role/revoke, response loss/retry and subscription
suspension/replay denial. It uses a preaccepted-member fixture, not the new
invitation flow. Read-only SQLite audit verifies exact links/events, zero new
invitation rows/receipts and zero credentials/sessions/module roles. All487 SSR
output files unchanged; owned local services stopped. Three other standalone
provider/browser opt-ins remain unrefreshed for this runtime. New recipient wire,
mail delivery, public invitation HTTP/CSRF, consent relay and acceptance UI are
still unqualified. No deployed TLS/DNS or production activation.

Evidence: inspection/qualifications/company-invitations-native-a4194399-02,
company-invitations-audit-a4194399-03 and invitation-browser-compat-01, each with
verification.json. Read-only existing private ERP readiness and Perfex sign-in
returned200 after qualification. Production/Perfex/DNS/payment unchanged.

### Earlier local lifecycle gate

Fresh Infrastructure/API/test builds passed. Final local gate in
inspection/qualifications/company-invitations-04 passed243 tests, zero failures,
14 native opt-ins skipped. The complete identity/link/setup/team cohort includes
the existing identity constraints, not only new invitation cases. Design-only EF
10.0.11 generation and read-only model comparison passed without a database
connection. Four new native cases cover issue/accept concurrency, revoke/accept
race, physical binding/receipt immutability and safe empty/populated rollback.
They are not proven by this local-only result; the native qualification above
exercises them separately.

Attempt01 exposed missing link tenant persistence after introducing the scoped
inviter/link composite FK. Explicit trusted tenant construction restored setup/
team cases in02 (98 passed/four skips), but the broader identity cohort still
found five failing link insertions. Assigning the tenant through EF's property
entry fixes generated composite-key persistence;03 and04 pass the unchanged
identity constraints. Added constructors carry their trusted scope before
tracking.03 includes membership before/after audit and reactivation/expiry tests;
04 also verifies token/email command string representations are redacted.
All attempts and failures remain; no assertion or scope guard was removed.

No DI registration, public endpoint, delivery worker, UI, schema application,
production flag, payment, Perfex or DNS change. The following recipient-only
checkpoint is historical and does not qualify the newer lifecycle schema.

### Earlier recipient proof contract

Recipient contract Foundation source15707bd9eb04ee813aedc3797e425e5548548223
passed123 tests/980 assertions on isolated PHP8.4.12/SQLite and Pint. The same
source passed123 tests/981 assertions on genuine MySQL8.0.46 in a new network-none container
without host ports, host SQL/privilege changes or production mutations. Native
evidence: inspection/qualifications/invitation-recipient-mysql-15707bd9-01.
The container is stopped. Archive SHA256
dda64626e2fe71ccf0f38e396fe8bc6bacab35d8d2bb37a5a89d9478c11632b9;
native log24a72cb6b4522e57efbceab57510fa4ff8d64aaccc2c6faed544bcb61fdb77df.

Core reader build passed zero errors (27 existing API/20 test warnings).191
focused tests passed, zero skips;41 recipient cases cover exact identity/client/
purpose/email, bounded wall and monotonic expiry including transit, no stale
reuse, malformed/duplicate/oversized/non-JSON data, denial, transport failure and
caller cancellation. Other checks cover existing protected setup/team/identity.
Evidence: inspection/qualifications/invitation-recipient-core-01/verification.json
and invitation-recipient-foundation-03/verification.json. Independent dependency
copies retained hashes; no old restore or clean metadata copied. The proof class
has no email-bearing automatic ToString and never retains provider tokens.

No schema, feature activation, deployed ingress, UI, production, Perfex, DNS or
payment change. Recipient resource tests use actual Passport PKCE/tokens; the
Core reader tests use a synthetic HTTP provider. Actual provider-to-Core wire
interoperability and account consent/relay remain unqualified. Full Core
invitation lifecycle/migrations/authority/replay/concurrency, delivery and browser
acceptance still need implementation. Do not describe this proof as a membership.
