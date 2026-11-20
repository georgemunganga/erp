# Protected Kuubu company setup

### Setup to company-team navigation (2026-11-08, isolated qualification passed)

Create and exact-request retry responses add the boolean
`team_management_available`, derived from the existing ProductIdentity,
ProductLicensing and CompanyTeam switches. It describes deployed capability,
not the person's membership, current license or operational admission. The
setup client defaults a missing field to false for older APIs and rejects
non-boolean values. Only true offers the fixed `/team` link after confirmed
creation/retry; no identity, payer or company selectors travel in that URL.

Core still checks a fresh team-purpose sign-in, explicit membership, license
and command permissions. Setup authorization cannot substitute. No new records,
events, scopes, cookies, migration, or feature activation are introduced. Flags
default off; standalone/dedicated setup behavior remains available independently
of this optional product flow. Rollback removes the navigation/optional metadata
without changing stored commands or companies. Acceptance covers true, false,
legacy missing metadata, malformed metadata, retry, and fresh team authorization.

Fresh Windows build passed (83 inherited warnings, zero errors). Targeted tests
passed143 with10 native PostgreSQL opt-ins skipped;59 Node contracts passed.
The production SSR build, generated route registration, TypeScript and final lint
passed.18 synthetic-API setup browser scenarios passed, including true/false/
missing availability and the fixed link leading to fresh team authorization.
The generator's21-line `/team` registration is now tracked so a clean checkout
can type-check without depending on a preceding build.

Actual Laravel/Passport, published website relay, Core and the new SSR build
passed the updated combined browser case in41.3947592s. The case clicks the
visible setup-to-team link rather than navigating there directly, verifies setup
proof cannot authorize team operations, then exercises fresh sign-in, licensed
reads, role change with committed-response loss/exact-key retry, revocation,
last-owner denial, suspension/read/replay denial and default ERP probe401.
Independent read-only audit verified one binding, two exact memberships/identity
links, two command receipts/two matching team audit/outbox changes and zero Core
credentials, sessions or module grants. Staff has no payer membership. The member
remains an explicit preaccepted/enrolled fixture; invitation delivery/acceptance
is still unfinished. All487 output files retained hashes/sizes/mtime after both
browser consumers. Owned local services stopped; ports8101/8121/3186 closed.

Evidence: inspection/qualifications/company-team-entry-01/source-verification.json
and team-entry-browser-01/verification.json. Browser log SHA256
8201e9348a93f9ad54d22b8cc69ff3eb494d0619d3ffa282e7aec481e075690b;
runner7deb991d0a386a3754f0a0c386c4ee26252dc8a83f9c795f63efdfb293df2cd1.
Initial clean-checkout typecheck failed on the previously missing generated route;
regeneration plus the tracked update fixed it. Initial lint found only test
formatting; final source lint passed and the complete parsed test syntax tree
matches the executed fixture copy. A first emission-based evidence check retained
trailing-comma formatting and was unsuitable for equality; the full syntax-tree
comparison passed. All failed logs/scripts remain. No production activation,
deployed TLS/DNS verification, native refresh, payment or Perfex change occurred.

## 1. Identification
Tier3 Core child feature; authenticated setup without operational ERP admission.
## 2. Purpose
Connect separately consented product identity to explicit capacity-bound creation.
## 3. Scope
Setup-only OAuth challenge, short server-held encrypted grant, payer selection,
CSRF-protected explicit create and cancel. No employee/session/module grant.
## 4. Parent
Own Kuubu authorization UI, verified subject, payer authority and allocation.
## 5. Actors
Verified enrolled identity; current payer owner/admin with purchased capacity.
## 6. Entry
GET product-identity/setup/start requests identity plus workspace-setup scope.
Ordinary start remains identity-only. Both use the registered fixed callback.
## 7. Preconditions
Default-off setup/provisioning, reviewed confidential client, private DP key and
grant directories, licensing credentials and qualified Core schema/runtime ACLs.
## 8. Workflow
Signed OAuth state marks setup purpose. Genuine S256 callback verifies subject;
only setup callback writes encrypted short-lived token/CSRF data server-side.
Protected HttpOnly cookie carries an opaque reference, never bearer/refresh.
Read eligible payer choices; explicit confirmed POST rechecks current payer and
license before the atomic create command. No company is created by callback.
## 9. States
Pending purpose, active short setup grant, expired/revoked/canceled; explicit
create returns created/existing/conflict/exhausted/denied/unavailable.
## 10. Permissions
Normal pending cookie cannot substitute for setup grant. Exact subject/client/
issuer binding, fresh resource checks, same-origin JSON and per-grant CSRF.
## 11. Rules
Max five minutes and provider token expiry. No automatic refresh, scope upgrade,
email join, tenant/role/quota input or company creation from enrollment.
## 12. Data
Random opaque handle hashes name encrypted grant files; DP purpose binds schema
and client. Payload binds handle, subject/client, token, CSRF and lifetime.
Private directory/file permissions, bounded reads, no links or public static path.
Retained browser commands are untrusted drafts, not an access boundary or proof
of persistence. Core validates all input/current authority independently; the UI
withholds a draft from another signed-in subject but does not encrypt tab storage.
## 13. UI
Own Kuubu login/explicit setup consent; select owned eligible payer, enter company
name, review and explicitly create. Handle loading/empty/denied/expiry/retry.
The typed browser client uses fixed same-origin, no-store, redirect-rejecting
requests, keeps CSRF out of URLs, and accepts only the expected setup response
contracts. Create retries reuse the caller's reviewed command/key; conflict and
capacity-exhaustion remain distinct recovery states. It stores no credentials or
session data. The public setup route bypasses tenant branding and operational
auth providers, reusing the shared controls in a Kuubu product shell. Account,
details and review are separate steps with an unchecked confirmation. A reviewed
request/key is written to versioned sessionStorage before sending, bound to the
server-verified setup subject. No bearer/CSRF is persisted. Once submitted, its
values cannot be edited until the outcome is known. Reload or fresh consent can
resume the exact request only after the same subject is verified; changing
identity cannot reuse it. Storage failure prevents sending. Ambiguous failures
retain the request; explicit retry may create or retrieve the same company.
No cached/local success is treated as server-confirmed creation. Session expiry
requires new consent, with the reviewed request retained for safe recovery.
An explicit correction after a CapacityExhausted reply retains the original
subject/idempotency key and requires another review/confirmation. It never clears
the retained request or creates a new key; Core's global actor/key uniqueness
protects against an older delayed command. Unknown, denied or conflicting outcomes
remain locked. Reload during correction resumes the last retained command.
Isolated real browser/provider acceptance passed; deployed acceptance remains a
separate release gate.
## 14. UX
Setup is separate from ordinary sign-in. Successful creation grants no operational
session; display the resulting workspace and subsequent onboarding honestly.
## 15. Notifications
Only allocation's committed outbox; setup grant/selection sends no email/payment.
## 16. Documents
No HR, payroll, financial or customer contact documents in grant/list response.
## 17. Errors
Invalid cookie/reference/ciphertext/binding/expiry/CSRF denies without token logs.
Provider denial/outage prevents creation; no stale grant substitutes for authority.
## 18. Integration
Fixed Laravel resources only. Payer choices are UX, never create authority. Fresh
setup authority and machine licensing are fetched for the submitted payer.
The setup session includes the verified numeric subject solely to bind reviewed
browser recovery state. It conveys no membership or operational permission. The
new UI requires this additive field; deploy its qualified backend prerequisite
before enabling the screen. Disabled/unavailable setup offers only a recheck.
## 19. Audit/privacy
No token, OAuth state, cookie, secret or body diagnostic logging. Committed
creation retains existing durable allocation evidence without raw idempotency.
## 20. Reporting
Login, consent and eligible choices are not provisioned workspaces or payments.
## 21. Configuration
Existing setup/provisioning flags remain off. Grant path must be private, mounted
outside web assets and distinct from DP keys. No production credential change.
## 22. Reliability
Short setup grants use an encrypted private shared-volume repository on the
existing workload host, not per-customer instances. Same-host replicas require
shared grant volume and DP keys; a repository boundary permits later distributed
storage without changing browser contracts. Bounded expiry/purge and cancellation
invalidate references. Provider freshness remains authoritative across restarts.
## 23. Postconditions
Callback/list creates no company or ERP grant. Only confirmed create invokes
allocation; retries preserve its receipt and the grant remains usable until TTL.
## 24. Recovery
Cancellation deletes only its grant; expiry purges old short-lived grant files.
Source rollback needs no schema change. Retain qualification source/evidence.
## 25. Acceptance
Normal proof, tampered state/cookie/reference, wrong subject/client, expired
token/grant, wrong origin/CSRF and unconfirmed/extra input cannot create.
## 26. Tests
Actual OAuth handlers/PKCE/correlation, private encrypted storage, CSRF/host scope,
current payer/licensing denial, explicit create/idempotent retry and UI states.
The real-provider opt-in cohort uses a fresh loopback Laravel Passport/RSA
fixture, the pinned actual Kuubu account client/signed relay, ASP.NET OAuth and
encrypted pending grants, and actual Core SQLite allocation. Only an explicit
fixture flag seeds one owner membership; staff retain none. Separate environment
variables keep ordinary identity qualification independent. Confirm scoped
consent/enrollment, callback/list with zero companies, explicit create/retry,
purchased capacity, revoked owner/suspended purchase/canceled grant denial and
read-only provider/Core audits. No fabricated upstream response or production
TLS/issuer override. This protocol gate does not prove deployed browser ingress,
production PostgreSQL integration, operational admission or a public SaaS release.
The separate browser opt-in uses the exact published website build, built ERP
setup screen, actual relay/provider and Core API/SQLite allocator. An isolated
Chrome context routes only the reviewed public HTTPS origins to marker-protected
loopback transports, preserving real cookie/origin/CSRF/state behavior. No browser
profile, certificate exception or production flag is used. Verify own login,
disabled Google, explicit Join/setup consent, zero callback provisioning,
confirmed creation, reload retry, separate HttpOnly cookie hosts and denied
operational access. This does not certify deployed TLS, DNS or PostgreSQL ingress.
## 27. Release gates
Isolated browser→provider→protected grant→create and synthetic definitive-denial
form recovery passed. Remaining gates: private runtime ACLs/shared-volume
operations, owner/invite policy, licensed sessions/refresh/revocation, branch/
module scope, public ingress/log redaction and deployed recovery acceptance.
## 28. Evidence
### Actual browser acceptance —8 November2026

Published runner `aee65eebf468275997912e60b906c6c6e2a3a662`, compiled test
source `ba98d8399a138dbc900dffc1c20b3e928f0ed8d7`, unchanged production Core
runtime `a8134a9a`, Laravel helper `b71b29fe` and website `b01985ee` passed
the actual installed-Chrome browser case in48s, zero failures/skips. All92
tracked website blobs were verified against the published Git tree before its
API-mode build. Original compiled runtime assemblies were consumed read-only;
only the test-only Node runner changed afterward. SDK10.0.403, PHP8.4.12 and
Node24.18.0 were used.

The browser performs Kuubu's own branded login, disabled Google, explicit Join
Kuubu and separate unchecked setup consent, then actual OAuth callback/payer
selection and an unchecked company review. There are zero companies before
confirmation. Confirm creates201; reload requires a new confirmation and exact
same-command retry returns200 for the existing company. Secure HttpOnly account
and Core cookies have separate exact hosts. Operational access returns401.
Independent read-only SQL verified one company binding, credential-free/role-free
owner, exact subject/tenant-linked identity, audit and outbox; zero sessions,
credential links and module-role grants; slot1 within purchased limit; clean FKs.
Provider retains one explicitly seeded payer membership and one personal join.

Evidence root `inspection/qualifications/real-browser-setup-20261108-05` holds
the immutable runner, login/consent/review/result screenshots and SQL acceptance
receipt. Log SHA256 `f3df65050fd481473b57fee9c03051342ab1c2a07cc39cb4715d5882c812d246`;
runner SHA256 `58ac29e09d7946a9dc154fb141e6ac8205152d097c9608c8ba4a7a1032e9c06c`;
result SHA256 `cbabafeb099d81d2c9073128bfb59d7e598dc48db60074c1f4a68b8cb1c93339`.

Attempts01/02 exposed Playwright routing skipping subsequent redirect URLs,
loading a deployed document with different asset hashes. Test-only CDP Fetch
interception now handles every redirect. Attempt03 completed the journey but
failed the strict transport assertion because missing favicons threw instead
of returning404. Attempt04 passed with normal static404 handling; attempt05
also disabled browser DNS (missed interception fails closed) and captured own
login/consent screenshots. All attempts remain retained; no website source was
changed to hide the mismatch. Service workers are blocked, off-fixture requests
are aborted, no certificate exception/user browser profile is used. Logs exclude
query parameters, bodies, cookies and secrets. Fixture/SSR processes stopped and
ports8101/8120/8121/3186/3187 were verified closed.

Setup UI035869f0 separately passed15 synthetic-API browser scenarios,35 contract
tests, TypeScript, targeted lint and production build, including capacity-denial
correction retaining the original actor/request key. Production C# did not
change after the already qualified runtime; the older regression evidence below
is not a claim that the new opt-in browser case ran in that earlier full suite.
No production deployment, migration, DNS, credentials, Perfex or payment change.
Deployed TLS/DNS, production PostgreSQL browser integration, licensed operational
admission, owner/invitation policy and customer release remain unqualified.

### Actual protected provider protocol —8 November2026

Runtime `a8134a9a90bc604ff392daf06c49daf697887fec`, Laravel fixture helper
`b71b29fe4bfbc81e34610983ef61461e02e14845`, and published Kuubu website
`b01985ee536978e11944b3d170595c83fc68b3f3` passed the genuine protected setup
case in45 seconds on Windows SDK10.0.403/PHP8.4.12/Node24.18.0. The separate
ordinary identity/licensing case passed in12 seconds with a fresh default fixture
and zero billing memberships. Windows213 focused checks passed (four native
skips exercised separately); Linux213 focused and63 PostgreSQL checks passed.
Full Linux regression passed2,184 tests, zero failures, with six opt-in skips.
All six were exercised separately: four in the63-case PostgreSQL cohort and the
two actual provider cases above. The SDK runner and test PostgreSQL stopped0,
OOMKilled=false. Exact Linux log hashes are recorded below.

The protected case uses actual published account-client consent enforcement,
signed relay, Passport confidential S256/RSA exchange, ASP.NET OAuth/cookie/
encrypted grant handling, real payer/authority/licensing resources and the Core
SQLite allocator. Callback/list creates no company; confirmed commands create
two separately tenanted companies within a two-place purchase. Exact retry
returns the same company; excess capacity, revoked owner and suspended purchase
deny. Cancel invalidates the grant; no ERP session/module role is created.
This is actual protocol acceptance, not deployed browser/TLS/production-PostgreSQL
acceptance. The later isolated browser gate above passed; deployed browser
acceptance remains required before rollout.

Independent read-only SQL audit verified two company/owner/identity-link pairs in
distinct tenants, slots1/2, clean foreign keys, one audit/outbox per company and
zero credentials, sessions, credential links or tenant-role assignments. Provider
counts remained two users, one customer, two payer enrollments, one subscription;
two explicit personal joins and one explicitly seeded/restored owner membership.
Staff retained zero billing memberships. The ordinary fixture retained zero
memberships and one explicit personal join. Both loopback services stopped.

The first source preparation could not verify the private website through the
local GitHub CLI; a new preparation used the connected GitHub app. All three
runtime imports matched exact published Git blobs: session423f0cd3, clientee787361,
authorizationfa7ad566. The first actual setup attempt then exposed a Windows-only
FileStreamOptions bug: assigning even null to UnixCreateMode throws. Three store
tests reproduced it; read-only SQL confirmed zero partial Core records. The
fixed source sets that property only on non-Windows, preserving Unix0600.
All failed inputs, logs, fixtures and Core databases remain retained.

Exact ERP archive SHA256:
`bac5386abae3f9131354b7bd23bf3ee6056b2f088690211cd356a55bf8fcf6a3`;
foundation archive `a9ba0b7e268c9f0932768fd23caaef121c9396dc9bcb6606ee081696f91db51b`;
actual setup log `61d61dbb926c1c266c05f126d602793b680b0bffd6a84ed5a3cf374cf4b196a8`;
ordinary log `ca822468c8f972789e1e8052dde2611fa9f9d14b5620a698fb768f6622578d29`;
Windows focused log `66756e706801136dc21034a215af5d50cf19f7031b330193dac03c930c01b583`;
Core/provider audit `86ecf8692dc09a824ad7eca2ab151535531e5ba56e9b21492f2f0e15d3ba0b7b`;
Core SQLite snapshot `5688b96e5aef96617eea6768f10a908282c8b8522265dc72eb3471f19bd0c5e8`.
Linux focused log `8ae50e2533996c9fc7f98610cc01d4a601e5c05f93a14d725e96d937b5fcb763`;
native log `fbec4292b4a694f5050bcf616fc68964192392dfbb0518124e6b1ca632182f10`;
full log `4a7b9ce0f340b36165746c4432962e916ced0c7f5fc04b57c94245d4dbbac97f`.
Local acceptance/cleanup receipts are in
inspection/qualifications/protected-setup-provider-a8134a9a-03.

### Earlier UI qualification and backend checkpoints

Current UI source `43bc0aa1a5f9003399a052d2ae95236fb8d87107` passed14
installed-Chrome browser scenarios against the real production SSR build and an
explicit loopback synthetic API,34 Node contract/recovery tests, full strict
TypeScript checking, targeted ESLint and production build. Desktop/mobile review
screenshots were visually inspected. UI qualification includes the disabled
state, explicit confirmation, no operational auth/branding fetches, lost-response
reload recovery, fresh-consent retry, another identity blocked without draft
details, storage write failure, pagination/empty/capacity states, portaled product
branding, expiry, cancellation and acknowledgement. No genuine protected
Laravel→Core setup acceptance or production rollout is claimed.

UI archive SHA256:
`8e84a2c672d85962b7eb8b34a242ca08d65419076d00696345ae2fe586de29b8`;
final build log `164d1cd2bb1af5a50cf2d9aeceaf6b47e2ccb94124c6039501fd61af1e888791`;
browser log `bff82e7604f4a13b936408a3ad41790fa705004b467704513e0d3ff3910f2552`;
contract log `d9c808bbc1e550fdef6aca5b1026601bb725e105f419ba26d4f0cd5aa379a929`.
Local receipt: inspection/qualifications/kuubu-setup-ui-20261108-01/receipt01.json.
The first browser run failed before launch because the bundled browser was
absent; the dedicated suite uses installed Chrome. Earlier logs/runs are retained.
Both local loopback services stopped and their listening ports closed.

Verified-subject backend runtime `c138aef39f26b16e0f46de99b758a627be4469e0`
passed213 focused,63 genuine PostgreSQL and2,184 full Linux regressions, zero
failures. Four native skips passed separately; the fifth opt-in real provider
identity/licensing case was not rerun here. Runner and test PostgreSQL stopped0.
Backend archive `49e2051706feb57cce0f2c2c54655ab6612d66bf92186cf6c3238b45c4ca3c74`;
focused log `cbbc777a6aa33e6c139cdf0f6c23ea012a0e165bcc02f2fcb78a4fdcbd8e991d`;
native log `3cf8a8e6a28a2d44a7df9e3a69e8e0801a13d47f613aaa171ebcdd3069aae200`;
full log `98b845313a351dfb4d439dd019c6433ba16ce49d402058b37a5f2c4267dc0f4c`.
The UI adds no backend changes after that runtime. Production remains unchanged.

The earlier backend/client-only checkpoints below remain historical evidence.

Backend runtime `e8a1f38a269265c6a30c620e92b1b68e55780642` passed213
focused Linux checks (four native-only skips) and63 genuine PostgreSQL cohort
checks with zero skips. Full Linux regression passed2,184 tests with zero failures
and five opt-in skips. Four skipped native cases passed in the63-case cohort;
the existing real identity/licensing provider test was not rerun in this isolated
job. Its earlier qualification does not prove the new protected setup journey.
Exact archive SHA256:
`dcb5070362daf0ecce2e93e62bfa9fd2134c09e0f06df0d2147418bdc1d89051`.
Attempt01 stopped before compilation because of Windows shell line endings;
attempt02 compiled but exposed eight HTTP-result failures. The company handler
was bound to RequestDelegate, which discarded Task<IResult>; casting it to
Delegate restores result execution. The fixed exact source is in exclusive
`/opt/kuubu-builds/protected-setup-e8a1f38a-03`; both earlier attempts remain.
The runner and synthetic PostgreSQL cluster both stopped with exit0. Log SHA256:
focused `b918bee9b9150820b1e2fb486aba8b4c1efcc3b16437f2175e9eb8857e6cf356`;
native `cd187a8c1924af82039f95d41a24f73bae67fb7f4e7b91ef644c2d1008fb4981`;
full `2ee10a8fe3d61a92d33b3ec593e160a007d9574133b0bbb8aae936657116774d`.

Browser client source `d0de0c1a8838269bd76b96088917f7e918b0340d` adds only
the TypeScript client and its tests after that backend runtime.19 Node24.18.0
contract tests passed; TypeScript5.9.3 strict noEmit ES2022/DOM checking passed.
Tests include fixed request policy, repeated ambiguous command identity, denial
codes, bounded conflict recovery, malformed results/pagination, rejected HTML200,
and no operational grant. Test log SHA256:
`532292508d7c75d3ac931e87d21961a8b0cf1e4da49f0f827201b9be86547fe4`.
That earlier client-only checkpoint included no rendered setup form. Genuine
protected provider→create acceptance remains a separate gate.

Laravel payer resource15adde5 separately passed112 tests/874 assertions on native
MySQL8.0.46. Current read-only checks returned200 for Perfex sign-in and the
existing private ERP /health/ready. No production enablement, migration, image,
credential, Google, payment, DNS or Perfex change occurred.
