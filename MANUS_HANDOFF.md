# Internal invitation lifecycle qualified —8 November2026

Published runtime a419439942a77db22a968ef5d94bb2c10f53921e passed97 genuine
PostgreSQL invitation/team and90 shared identity/session/schema checks, zero skips;
focused Linux261/14 native skips and full Linux2,355/18 opt-in skips, zero failures.
All14 native-only opt-ins ran separately. Four new native cases cover issue/accept
and revoke/accept races, physical history guards and empty/populated rollback.

Read-only audit:97 new company/team databases,96 current schemas with six validated
FKs/two history triggers, one exact retained legacy team rollback signature.
24 invitations/29 immutable receipts/24 delivery intents/33 audits/57 events;
exact actor/tenant/company/subject, request hash, revisions and correlation checked.
Four accepted memberships; no credentials, sessions or module roles. The older
rollback fixture has no invitation history and retains its guarded team receipt;
the audit did not migrate it. Multi-migration Down can commit an earlier empty
rollback before a later history guard refuses another; reapply latest before
current-runtime admission.

Native01 retained:97 pass then shared-memory/recovery failures in23 shared cases.
Native02 uses a new512MB-shm isolated database and586 independent byte-identical
binary files from the exact published source; hashes/sizes/mtime unchanged.
Both test clusters stopped. Audit02's missing-table assumption retained; audit03
classifies the legacy rollback state read-only. No assertion or scope guard removed.

Existing actual website/Laravel/Core/SSR browser compatibility passed47.167s with
APIa4194399, unchanged UI661c30a0 and websiteb01985ee. Own Kuubu login/Google off,
explicit enrollment/setup consent, create/retry, fresh team sign-in, licensed
role/revoke, ambiguous retry and suspension/replay denial remain working. It uses
a preaccepted member: new invitation UI/recipient wire/delivery is not qualified.
Three older standalone actual provider/browser opt-ins remain unrefreshed.
SQLite audit: no invitation records and no credentials/sessions/module grants;
487 SSR output files unchanged; owned services stopped. Private ERP health and
Perfex login returned200. No production application/configuration/DNS/payment change.

Evidence: inspection/qualifications/company-invitations-native-a4194399-02,
company-invitations-audit-a4194399-03 and invitation-browser-compat-01, verification.json.
Archive909df1c6ba7a869b3258375527e410503ea9c6e88a1e517cf2c5608a86ccaf7c;
auditb4a0a7f68da91196456a89974753048edf4ea4398cf0483114404827e700051f.
PR201 remains draft; main/B1/release holds remain. Next: verified recipient wire,
account consent/relay, protected invitation HTTP and UI, durable mail delivery,
then licensed operational tenant admission. New commands remain unregistered.

## Earlier internal lifecycle checkpoint (before native qualification)

Published a419439942a77db22a968ef5d94bb2c10f53921e adds internal Core invitation
issue/revoke/explicit acceptance, schema and PostgreSQL history guards, immutable
receipts, protected48-hour delivery envelopes and ID-only durable delivery intent.
Owner may invite administrators/members; admin may invite only members. Acceptance
requires targeted secret plus fresh recipient proof, current inviter and license,
and explicit confirmation. Active membership cannot be changed through acceptance;
reactivation requires a new invitation and restores only its requested role.
Retry cannot revive a later-revoked member. No credentials, sessions or module roles.

Local exact-source gate:243 pass/14 native skips, including19 new lifecycle cases
and four new native cases pending. EF10.0.11 design generation/model comparison
passed without a DB connection. Evidence: inspection/qualifications/company-invitations-04/verification.json.
Retained01 exposed scoped-link insertions;02 restored setup/team but broader
identity tests still found five failures. Trusted tenant assignment through EF's
property entry fixes generated composite key persistence.03/04 preserve and pass
those identity constraints. Source also carries explicit scopes before tracking.

Native source archive909df1c6ba7a869b3258375527e410503ea9c6e88a1e517cf2c5608a86ccaf7c
is qualifying in exclusive /opt/kuubu-builds/company-invitations-a4194399-01,
container kuubu-company-invitations-gate01. Fresh source/restore, read-only package
cache, no reused project bin/obj; synthetic PostgreSQL only. Native results are
not yet established here. No DI/public HTTP, delivery worker or UI activation.
Existing main/B1/release holds and draft PR201 remain. Production applications,
configuration, DNS, Perfex and payments unchanged. Next: finish native gates and
read-only audit, then actual recipient wire/consent/HTTP/UI and delivery integration.

## Earlier recipient proof contract checkpoint

Published ERP57ee105877e963980ed593d737804a40bccbf3b6 adds a strict server-side
recipient reader and41 new proof cases;191 focused checks passed, zero skips,
API/test builds zero errors. The reader is not registered or exposed yet. It
accepts only exact issuer/subject/client/product/purpose, canonical verified email,
bounded wall/monotonic time and operational_access_granted=false. No token cache,
company grant, payer role, module permission, credential or ERP session.

Foundation15707bd9eb04ee813aedc3797e425e5548548223 adds the separate
product:invitation-recipient scope/resource for confidential Kuubu clients only.
Both existing identity and new default-off recipient switches are required.
Ordinary subject responses remain email-free. Current verified personal enrollment
is required; payer membership is not. Proof <=30 seconds and token record expiry.
Full SQLite123 tests/980 assertions and genuine MySQL8.0.46 (123/981) passed, Pint
passed. MySQL ran in an exclusive network-none/no-host-port test container with
no host SQL/privilege changes; container stopped. Read-only production Perfex login
and Laravel /up returned200. Production configuration/application/DNS/payment and
existing release/main/B1 holds remain unchanged. PR201 remains draft.

Evidence: inspection/qualifications/invitation-recipient-core-01/verification.json,
invitation-recipient-foundation-03/verification.json and
invitation-recipient-mysql-15707bd9-01/verification.json. See
docs/engineering/CORE-COMPANY-INVITATIONS-20261108.md for boundaries and retained
formatting/harness failures. Actual Laravel-to-Core wire proof, account consent/
relay, invitation schema/commands/delivery/acceptance UI and licensed operational
tenant admission remain unfinished. Do not enable staged proof as company access.

## Earlier setup-to-team entry checkpoint

Published implementation661c30a01220c431e808b5449e08df2db2f58f79 adds a
configuration-derived team_management_available boolean to company create/retry,
a fixed /team link only when true, fail-closed older API handling, and the
generator's previously untracked21-line team route registration. No new records,
permissions, feature activation or operational admission. Existing classic local
authentication and dedicated-instance architecture remain unchanged.

Fresh build zero errors;143 targeted backend tests passed/10 native opt-ins
skipped;59 contracts,18 synthetic setup browser cases, production SSR, TypeScript
and final lint passed. Actual website/Laravel/Core/SSR browser gate passed41.395s
through the visible link, then fresh team sign-in/current license/role change/
lost committed response/exact retry/revoke/last-owner/suspension/replay denial.
Independent SQL audit: one binding/two exact members/two receipts/two matching
team changes; zero credentials, operational sessions and module roles. Staff has
zero payer memberships. The member is still an explicit preaccepted/enrolled
fixture: invitations/acceptance/delivery remain unfinished.

Evidence: inspection/qualifications/company-team-entry-01/source-verification.json,
team-entry-browser-01/verification.json. All487 SSR output files unchanged after
consumers; all owned services stopped. Runtime source matches the tested copy;
final test-only formatting has an identical complete syntax tree. Failed initial
typecheck, formatting lint and unsuitable emission-comparison evidence checks
are retained. Native refresh and deployed TLS/DNS were not run for this metadata/
navigation increment. No production, Perfex, payment or DNS change. PR201 stays
draft; existing release/main/B1 holds remain. Next: website product entry links,
invitations/acceptance/delivery and licensed operational tenant isolation.

## Earlier actual Kuubu team sign-in and browser/Core checkpoint

Published harness/runner `bb047f489b01c86ce8685b7bd1dffd1bf19be7d4` (test fixture
first published86a75ee1) passed the actual branded website/Laravel/Core/SSR team
journey in36.589s. API2607cd69/UIaaafdf25 runtime source is unchanged. Websiteb01985ee
matched all92 published source blobs. Test-only bridge controls and explicit
preaccepted-member/personal-enrollment fixtures are not invitation implementation.

Verified: login/Google unavailable; explicit enrollment/setup consent; create and
same-request retry; setup proof cannot authorize /team; fresh identity-only team
sign-in; licensed member reads; role change; committed-response loss and same-key
retry; current-team refresh; revocation; last-owner denial; subscription suspension
and receipt replay denied403; default ERP protected probe401.

Independent read-only audit: one binding, two exact membership/principal/link
pairs, two receipts and two matching team audit/outbox changes (three total with
company creation), owner active/revision1, member administrator/inactive/revision3.
No Core credentials, sessions or module roles. Staff has no payer membership;
personal enrollments remain separate. Fourteen runtime libraries and487 UI output
files unchanged. Owned PHP/SSR/bridge stopped and8101/8121/3186 closed.

Evidence: inspection/qualifications/real-team-browser-acceptance-03/verification.json,
component-inputs.json, published-inputs.json; runner is now versioned under
backend/hrm-api/tools/ProductTeamBrowser with preparation instructions. Earlier
failure01 timed out before login;02 completed real role change but had a CP1252
test assertion. Both retained;03 writes UTF-8 and keeps the same assertions.

Next: capability-gated setup/website entry navigation and invitations/acceptance/
delivery, then licensed ERP sessions and tenant/module isolation. This new case
does not rerun all older standalone protocol opt-ins. No deployed TLS/DNS or
production activation/payment/Perfex change; the full ecosystem remains unfinished.

# Earlier company-team API native/full and isolated UI checkpoint —8 November2026

API source `2607cd697bb0c959b39e7e2091d3fbfe8d5a2c03` passed74 genuine PostgreSQL
team/API cases,89 shared native-cohort checks,326 Linux focused and2,294 full
regressions, zero failures. Ten native opt-ins were exercised in the native
cohorts; the three actual provider/browser refresh opt-ins remain pending.
Independent read-only audit of74 new databases (previous31 excluded) verified165
memberships,28 immutable receipts,23 audit/outbox changes and five no-op receipts.
Four actor-loss fixtures deliberately remove the last owner outside commands;
the audit records that scope instead of claiming an all-companies owner invariant.
No credentials, operational sessions or module roles were introduced.

Runner kuubu-company-team-api-gate02 exited0/OOMKilled=false. Controller/audit
restored the labeled test PostgreSQL to stopped. Read-only private ERP readiness
and Perfex sign-in both returned200. Evidence/hashes are recorded in
CORE-COMPANY-TEAM-HTTP-20261108.md and local qualification roots
company-team-api-linux-2607cd69-02 / company-team-api-audit-2607cd69-01.

Published UI source `aaafdf25649b6fa8feca59be1c97452407004462` adds /team with shared
ERP controls and its own Kuubu product shell, without operational admission.
Backend source is byte-for-byte unchanged from2607cd69. UI02 independently passed
18 contracts, TypeScript, ESLint, production SSR build and nine synthetic-API
Chrome scenarios, including visible mobile actions. All487 output files retained
hash/size/nanosecond mtime after consumers; SSR stopped and port3188 closed.
Desktop/mobile/review previews inspected under company-team-ui-02. UI01 remains
unchanged with its earlier offscreen mobile table actions. Frontend archive
48dddf823abdfbe0b4d754d381f0177a4567e2f9ebaa317b51e38bd2d757ca43.

Next: actual Laravel/Kuubu relay/Core team-sign-in/browser integration (the UI gate
above uses a synthetic API), invitations/acceptance/delivery and qualified entry
navigation. Licensed operational ERP sessions, tenant/module isolation, Lenco,
other product adapters, recovery and dedicated deployment remain unfinished.
No production activation or invitation workflow yet. See
CORE-COMPANY-TEAM-UI-20261108.md and company-team-ui-02/verification.json.

# Earlier company-team directory and mutation API checkpoint —8 November2026

Published source `2607cd697bb0c959b39e7e2091d3fbfe8d5a2c03` connects company discovery,
licensed member reads and strict confirmed role/revoke HTTP commands to the
existing audited Core commands. Every member read/command resolves the enrollment
from the immutable binding, fetches a current license and rechecks exact Core
identity/company membership. No browser tenant/payer/subject grants authority.
Owners alone assign roles; administrators may revoke ordinary members only.

Windows API and test builds passed;331 focused tests passed, zero failures,
including43 new HTTP-to-database cases. Twelve opt-ins skipped. Evidence:
inspection/qualifications/company-team-api-tests-01/verification.json; source
archive inputs under company-team-api-2607cd69-01. The initial ASP0016 route-mapping
warning was corrected in a new independent API build before running the tests.
Native/full qualification is running in systemd unit kuubu-company-team-api02,
isolated Docker container kuubu-company-team-api-gate02, root
/opt/kuubu-builds/company-team-api-2607cd69-02. Attempt01 exited125 before SDK
creation because a two-CPU quota exceeded the one-CPU host; its source/receipt are
retained and test PostgreSQL was restored to stopped. Attempt02 uses one CPU.
Poll those live handles, not this
note, before treating it as still running or terminal. The controller stops the
labeled test PostgreSQL container in finally. Never restart a timed-out observation.

Invitations/acceptance/delivery, /team UI and licensed operational admission remain
unfinished. Current source still needs native/full results and actual provider/
browser refresh; no production runtime/schema/feature flag/Perfex change.

# Earlier company-team HTTP proof increment —8 November2026

Published source `dfe69f81531546ba547adfde42d516f7f3ec1eb3` adds an explicit signed team-management OAuth purpose,
a per-sign-in nonce and framework antiforgery-protected session/cancel endpoints.
It does not yet expose role/revoke mutations, company/member reads, invitations,
the /team screen or operational ERP admission. All new endpoints are opt-in.
Windows full dependency build passed;263 focused tests passed with zero failures,
including16 new team cases. Twelve environment-dependent opt-ins were skipped.
Evidence is under inspection/qualifications/company-team-http-local-04,
verification02.json and published-source.json. The first exact cache-header test
assertion and a missing apphost in test-only preparation failed; all attempts are
retained. Do not borrow the earlier3cc77532 native/full/browser results for this
changed identity source. Those gates and real provider/browser refresh remain
pending for the new source. The runtime dependency DLLs were copied independently
from the full build and their API checksum matches; no old build was patched.
See docs/engineering/CORE-COMPANY-TEAM-HTTP-20261108.md for the contract and remaining
work. No production runtime, database, flag, ingress or Perfex change.

# Internal company role/revoke commands qualified —8 November2026

Source/runtime `3cc775323cbe89d434fde5dbdebf77b082b26516` passed31 genuine
PostgreSQL team-command checks,89 shared native-cohort checks,267 Linux focused
and2,235 full Linux regressions, zero failures. All13 full opt-ins ran separately:
10 native cases plus actual browser36s, protected setup83s and ordinary product
identity/licensing31s on Windows. Windows focused265 and EF no-model-drift passed.

Independent read-only audit verified31 synthetic native team databases with62
memberships,14 actor/key-bound immutable receipts,10 revisioned audit/outbox
changes and four no-op receipts. Exact tenant/company/member/actor identity,
validated FK/check/unique/trigger, historical snapshots and retained active-owner
memberships passed. Real PostgreSQL governance queries/concurrent owner demotions/
retries/rollback are now exercised, not just SQLite policy. Public command
authentication/origin/CSRF, invitation delivery/acceptance and team UI remain next.

Actual setup/browser SQL verified one/two company tenants with exact owner links,
revision1 and zero team-command receipts, credentials, sessions or module roles.
Staff still have zero billing memberships. This proves normal setup remains
unchanged; no public team HTTP/API or licensed ERP admission is qualified yet.

Exact hashes/bounds: CORE-COMPANY-TEAM-20261108.md. Evidence under
inspection/qualifications/company-team-linux/browser/protocol-3cc77532-01
(three separate directories); remote source/results
/opt/kuubu-builds/company-team-commands-3cc77532-01 and independent audit root
/opt/kuubu-builds/company-team-audit-3cc77532-01. Source archive
`96aa59719eaf077101e1b450424597e04fc0dee3ccf4faa1853fe8e32b9501c8`;
full log `ff0cf95ca20203a2476611f48037823a8af46df8020b909d86efd902cc930b91`.
SDK/test DB stopped0/OOMfalse; local ports closed. Existing private ERP readiness
and Perfex sign-in returned200. No production changes. Preserve on-premise/local
authentication compatibility and all deployed release/ACL/ingress/recovery holds.

Next API boundary must derive issuer/subject from verified product authentication,
resolve the commercial lease from the exact Core binding, and reject client
tenant/payer/subject grants. Require the reviewed app origin, JSON, anti-CSRF and
explicit command confirmation; a setup grant is not a team-management grant.
Keep billing-delegate UI separate from Core company governance. New membership
writers must set trusted tenant before tracking composite principal keys.

# Earlier company role/revoke local source checkpoint —8 November2026

Published source `3cc775323cbe89d434fde5dbdebf77b082b26516` adds internal
confirmed role-change/revoke commands, positive membership revisions, immutable
actor/key-bound retry receipts and atomic audit/outbox. Owners alone assign
roles; administrators can revoke ordinary members. Last-owner removal requires
another locally usable owner. Replays recheck current action authority and cannot
undo a newer change. No-op commands reserve their key. Reactivation, invitations,
public command authentication/CSRF and team UI are not implemented in this increment.

Local build/focused cohort265 passed, zero failures,13 opt-in skips; EF reports
no model drift. Initial58 failures/10 passes exposed a tenant_id NOT NULL failure
after adding the composite receipt key. Trusted tenant is now supplied before
tracking the initial membership; the repaired company cohort69 and broader265
passed. Failed logs retained. Company key retargeting is rejected before the
ordinary ORM guard and the stored binding is verified unchanged.

Native qualification was running at this checkpoint on kuubu-workloads under unit
kuubu-company-team-commands01, SDK container kuubu-company-team-commands-gate01,
root /opt/kuubu-builds/company-team-commands-3cc77532-01. It runs new native team
commands first, then the shared native cohort, focused and full Linux suites.
Check authoritative unit/container/log state; do not restart because a poll times
out or because the container has not yet been created during cache preparation.
The controller restores synthetic kuubu-session-native-db01 to stopped on exit.
The internal network has no published test database ports or production credentials.

Source archive SHA256 `96aa59719eaf077101e1b450424597e04fc0dee3ccf4faa1853fe8e32b9501c8`;
local evidence root inspection/qualifications/company-team-commands-3cc77532-01;
focused log `52b4a4c631617ed85ef1f68d980005c3d98349f081524da85243805a5ed5c028`.
New migration20261108113644 preserves predecessor memberships and refuses to
discard populated command history/changed revisions. Four new native cases cover
real governance queries, concurrent owner demotions/retries, receipt FK/immutability
and empty/populated rollback. Native/full and refreshed actual integration for
this source remain pending. The older qualified49780a39 below does not qualify
this new schema/workflow. No production change; do not enable public team actions.

# Earlier company membership allocation qualification —8 November2026

Runtime/source `49780a39a13c89cd496f92283c1f20ba6effb3c2` passed Linux
focused237, native qualification89 and full regression2,208, zero failures.
All nine full opt-ins ran separately: six native allocation/membership plus
actual browser39s, protected setup32s and ordinary identity/license14s against
real Laravel helperb71b29fe/published Kuubu websiteb01985ee. Owner-policy cases
in the mixed native cohort use SQLite; actual PostgreSQL governance-query/command
integration remains a gate before team endpoints are exposed.

Read-only native audit verified21 synthetic databases/16 memberships, exact
tenant/company/owner/link, validated FKs/check/unique/trigger, new-create audit/
outbox and two explicit legacy owner backfills (one inactive). Classic local
admin gains no membership and classic multi-company still works. Refreshed
browser/protocol SQL verified membership creation and safe retries without
credentials, sessions or module-role grants; staff retain no billing membership.

Exact sources/hashes/bounds: CORE-COMPANY-TEAM-20261108.md. Local evidence roots
company-membership-linux/browser/protocol-49780a39-01 under inspection/qualifications;
remote /opt/kuubu-builds/company-membership-49780a39-01 and separate audit root.
SDK runner and test PostgreSQL stopped0/OOMfalse; local ports closed. Perfex
sign-in and existing private ERP readiness returned200. No production changes.
Next: audited invitation/accept/revoke/role-change commands, last-owner/concurrency
guards and team UI, then licensed operational admission. Dedicated/on-premise
installer/licensing/upgrade/recovery and all deployed release holds remain.

# Earlier company membership source checkpoint —8 November2026

Draft PR201 source `49780a39a13c89cd496f92283c1f20ba6effb3c2` adds
Core company membership, atomic initial-owner creation, exact verified-identity/
licensed company-governance reader and migration20261108104749. Membership does
not create module roles, local credentials or operational sessions. Historical
allocation remains immutable; legacy backfill uses only explicit allocation
owners and exact links, not local admin/billing/email inference. No team endpoint
is exposed yet. Invitation/change/revoke commands and last-owner safeguards are
still required before enabling team administration.

Windows SDK10.0.403 build passed; focused identity/federation/workspace/company/
local-auth cohort238 passed, zero failures, nine opt-in skips (six native,
three actual-provider/browser). EF10.0.11 reports no pending model changes.
Retained source/local evidence: inspection/qualifications/company-membership-49780a39-01.
Archive SHA256 `0b336f22a1e5146a858f6b799556359e567160ff6fe303934efe5d0795d6b9b9`;
test log `42690b1df85bdfc308bb7369686e4d6ad5b504e3aa6014111ca55238d1595320`.
Next: genuine PostgreSQL migration/backfill/physical guards/concurrency, full
Linux regression and refreshed actual integration for this changed transaction.
The older full/browser qualification below does not qualify this new source.

User confirmed shared SaaS plus dedicated/on-premise installations from the same
maintained codebase. Preserve classic local login and multi-company behavior;
SaaS enrollment/provisioning is not mandatory on-premise. Dedicated installer,
licensing/renewal/offline policy, upgrades and backup/recovery remain acceptance
work. See deployment/kuubu/DEPLOYMENT-MODES.md and CORE-COMPANY-TEAM-20261108.md.
No production migration/image/flag/privilege, DNS, payment or Perfex change.

# Earlier actual browser setup acceptance —8 November2026

Draft PR201 published runner `aee65eebf468275997912e60b906c6c6e2a3a662`
and compiled test source `ba98d8399a138dbc900dffc1c20b3e928f0ed8d7` passed
the real browser→Kuubu login/enrollment/consent→Laravel→Core create/reload/retry
case in48s. Browser DNS disabled, every redirect intercepted, real signed relay
and API allocation, exact published website92 blobs verified. Independent SQL:
one company/owner/link/audit/outbox, zero credentials/sessions/module roles,
matching tenant/subject and clean FKs. Separate secure cookies and operational401
verified. Actual production Core runtime remainsa8134a9a; no production changes.
Setup UI035869f0 passed15 synthetic-API browser scenarios and35 contract checks,
TypeScript/lint/build, including explicit capacity-denial correction with the
same request key. Failed/partial attempts retained; all local fixture/SSR ports
closed. Exact sources/hashes/bounds: CORE-PROTECTED-SETUP-20261108.md, evidence
root inspection/qualifications/real-browser-setup-20261108-05. Next: explicit
owner/team policy, invitations and licensed operational admission. Deployed
TLS/DNS/PostgreSQL, runtime ACLs and release holds remain; do not enable live.

# Earlier actual protected setup protocol —8 November2026

Draft PR201 runtime `a8134a9a90bc604ff392daf06c49daf697887fec`, provider
helper `b71b29fe4bfbc81e34610983ef61461e02e14845` and published website
`b01985ee536978e11944b3d170595c83fc68b3f3` passed actual protected setup
interoperability in45s and ordinary identity/licensing in12s using separate fresh
fixtures. Real account-client consent/enrollment, signed relay, Laravel Passport
confidential S256/RSA, ASP.NET OAuth/private encrypted grant, payer/authority/
licensing and Core SQLite allocation all execute. Exact website runtime Git
blobs were checked through the connected GitHub app; local CLI lacked access.

Confirmed creates allocated two separate company tenants within purchased
capacity; retry returned the same company, excess capacity denied, owner revoke
and suspension denied fresh/retry requests, cancel invalidated the grant. No
operational session, credentials or module-role grant was created. Read-only SQL
audit verified exact owner/company/link tenant pairing, clean FKs, slots1/2 and
one audit/outbox per company. Ordinary staff retained zero billing memberships.

Windows213 focused, Linux213 focused,63 PostgreSQL and2,184 Linux regressions
passed, zero failures. All six full-suite skips ran separately (four native,
two actual provider cases). The real setup test exposed a Windows-only null
UnixCreateMode setter failure; the fixed source sets that property only on Unix
and preserves private0600 files. Failed inputs/tests/DBs remain retained. All
fixture processes stopped; ports8101/8120 closed; native runner/PG stopped0.
Exact sources, hashes and evidence are in CORE-PROTECTED-SETUP-20261108.md.

This is protocol acceptance, not the complete browser/deployed journey. Next:
actual browser→real provider/Core acceptance and definitive denial recovery,
then owner/team invitations and licensed operational sessions/refresh/revocation.
Runtime ACLs, shared-volume/key operations, branch/module scope, ingress/log
redaction, controlled migration and deployed recovery remain gates. Production
image/schema/flags/keys/DNS/payment/Google/Perfex remain unchanged.

The older checkpoints below remain historical evidence.

# Branded company setup UI checkpoint —8 November2026

Draft PR201 UI source `43bc0aa1a5f9003399a052d2ae95236fb8d87107` now provides
public `/setup`: own Kuubu brand, account selection, company details, review with
unchecked confirmation, create/retry and honest non-operational success. It
bypasses tenant branding and operational auth providers while reusing pinned
shared controls. Portaled account options retain product branding. Responses are
no-store/no-referrer; unavailable setup offers recheck rather than a broken login.

The reviewed request/key is retained before sending in versioned sessionStorage,
bound to the server-verified subject, without bearer/CSRF. Submitted values stay
fixed across lost response/reload/fresh consent; another identity cannot resume
them or see draft details. Storage failure prevents sending. Only acknowledged
server success clears the retained request. Expiry/cancellation grant no ERP access.

14 installed-Chrome synthetic-API browser cases and34 Node contract/recovery
tests passed, plus full strict TypeScript, targeted lint and production build.
Desktop/mobile screenshots inspected. Native backendc138aef39f26b16e0f46de99b758a627be4469e0
adds the recovery subject and passed213 focused,63 PostgreSQL and2,184 full
regressions. Four native skips exercised separately; existing real provider
identity/licensing test not rerun. All qualification services stopped. Exact
source/archive/log hashes are in CORE-PROTECTED-SETUP-20261108.md; prior attempts
are retained. Deploy the qualified subject-returning backend before this UI.

Next: real Laravel→Kuubu consent→Core create/browser acceptance, then owner/team
invitations and licensed operational sessions/refresh/revocation. Company/branch/
module scope, finite runtime ACLs, shared-volume/key operations, ingress/log
redaction and deployed recovery remain gates. No production schema/image/flag,
DNS, payment, Google or Perfex change. This is not a SaaS production release.

The older checkpoints below remain historical evidence.

# Protected company setup backend checkpoint —8 November2026

Draft PR201 adds runtime `e8a1f38a269265c6a30c620e92b1b68e55780642`:
separately consented setup OAuth, five-minute encrypted server-side grants,
token-free opaque pending-cookie references, eligible payer choices, explicit
CSRF-protected company creation and cancellation. Every create/retry obtains
fresh payer authority and machine licensing before the existing atomic allocator.
Normal sign-in remains identity-only; neither callback nor payer listing creates
companies or operational sessions. Laravel PR3 runtime15adde50a5a401f06f42a60f80888b261465333d
provides scoped paginated payer choices (112 native MySQL tests/874 assertions).

Linux213 focused,63 native PostgreSQL and2,184 full regressions passed, zero
failures. Four native opt-in skips passed separately; the existing real provider
identity/licensing test was not rerun in this job. Earlier provider evidence is
not genuine protected setup acceptance. Both failed earlier setup attempts are
retained; the fixed route now executes its IResult rather than discarding it.
Test runner and private synthetic PostgreSQL cluster stopped0. Source/archive/log
hashes are in CORE-PROTECTED-SETUP-20261108.md.

Source `d0de0c1a8838269bd76b96088917f7e918b0340d` adds only a typed browser
setup client and19 passing Node contract tests, with strict TypeScript checking.
No setup form is implemented yet. Next: public setup shell, account selection,
company details, review/confirmation, retained retry/reload handling and result;
then genuine Laravel→Kuubu consent→Core create/browser acceptance. Owner/team
invites, licensed operational sessions/refresh/revocation, module/company/branch
scope, runtime ACLs, shared-volume operations and public ingress remain gates.
No production migration/image/flag/key/DNS/payment or Perfex change. Read-only
Perfex sign-in and existing private ERP /health/ready both returned200.

The older checkpoints below remain historical evidence.

# Core capacity-controlled company transaction —8 November2026

Draft PR201 runtime `3e967e965217800c0996b3d9c0bd23cc79e810af` now
consumes bounded purchased capacity and provides an internal explicit company
create transaction. A fresh separately scoped payer observation and independent
machine lease must agree; Core serializes by enrollment, allocates a unique slot
and persists a company/HQ, role-free credential-free Core owner, explicit link,
immutable owner/payer/slot receipt and audit/outbox together. Exact retries return
the committed IDs; changed payload conflicts. No public setup route, automatic
signup provisioner or operational session is added. Module checks now require
the allocated slot as well as the licensed module. Missing/zero/malformed
capacity never becomes unlimited.

Migration `20261107231550_CoreWorkspaceAllocations` adds same-tenant owner/company
FKs, global slot/key constraints and immutable evidence/rollback guards. SaaS
tenants cannot add another legal entity through the legacy company-admin path;
classic multi-company installations remain supported. Native concurrency proves
two allocations within a two-workspace allowance and twelve retries yielding one
company/audit/outbox pair. Post-save expiry/failure/cancellation roll back all.

Final Linux197 focused,47 native PostgreSQL and2,168 full regressions passed,
zero failures. Five normal-suite skips were exercised separately: four in the
native cohort, one actual Laravel/Kuubu relay/ASP.NET identity/licensing case on
Windows. Windows146 focused cases also passed. A read-only audit of38 synthetic
DBs/26 allocations verified constraints, scope, role-free owners and audit/outbox.
Read CORE-WORKSPACE-PROVISIONING-20261108.md for exact source/log hashes and
the retained first native attempt's stale migration-order test fix. No production
code changed between Windows runtime dae2c340 and final3e967e96; only that test.

Provider fixture source2ebee45 uses explicit synthetic purchased capacity and
zero staff billing memberships; websiteb01985ee retains its own login/consent.
Actual protected setup OAuth→company-create/UI acceptance still remains, along
with invitations/owner policy, licensed operational sessions/refresh/revocation,
branch/module/local-auth enforcement, finite runtime DB ACLs and public ingress.
Do not deploy wholesale main or treat this internal transaction as a SaaS launch.
All old source/image/financial release holds remain. Private API image3ff07382
and /health/ready200 are unchanged; test containers and local fixture stopped.

# Purchased capacity provider checkpoint —8 November2026

Laravel foundation PR3 now publishes runtime
`2449fe9509454283d22da0bf29db54e06270c299`: bounded plan allowance,
immutable purchased subscription snapshot, audit/outbox evidence and
workspace_limit in current setup/licensing resources. Old subscriptions remain
unassigned; plan edits/retries do not expand a purchase.101 tests/740 assertions
passed on genuine MySQL8.0.46 plus upgrade/rollback and denied-host-DDL probes.
See foundation WORKSPACE-CAPACITY.md for exact source/logs and the privileged
schema migration requirement (no production global trust/SUPER grant changed).

Core73b4afbe has not yet consumed/enforced that allowance. Next: bounded capacity
parsing and atomic slot/company allocation, protected setup OAuth grants,
company/payer binding/idempotency/audit and invitations before operational
sessions. Missing/zero/unassigned capacity must never mean unlimited creation.
No ERP runtime source, production schema/image, public ingress or flag changed
by this documentation checkpoint. Keep previous consumer qualification separate.

# Core workspace setup authority consumer —7 November2026

Stacked draft PR201 now includes runtime
`73b4afbe620328410090b5e1ff89b559ba3807b4`: a separate default-off Core
reader for Laravel's current payer owner/admin workspace-setup authority.
It binds the exact verified subject, configured identity client and canonical
customer/enrollment references; validates the creation action, schema, issuer,
product and non-operational result; and caps observations using wall/monotonic
time. It stores no token, caches no authority and returns no old result on outage.
The fixed channel has bounded JSON/timeout, duplicate rejection, no redirects,
cookies or request loggers. Setup requires identity and licensing prerequisites.

Windows112 focused tests (including41 new cases), Linux163 focused tests and
Linux2,134 full regressions passed, zero failures. The existing real provider
opt-in case was skipped in the network-none full run; prior actual identity and
license interoperability remains separately qualified. This new setup resource
still needs actual Laravel→Core acceptance. Read
`docs/engineering/CORE-WORKSPACE-SETUP-20261107.md` for exact source/archive/log
hashes, runtime scope, architecture and remaining gates. Native launcher exited0.
Hosted run42417243189 did not start because GitHub explicitly reports an account
billing lock; no hosted pass is claimed.

Laravel authority source db6921d and website explicit consent source b01985ee are
published in their draft PR3s; neither new flow is production enabled. Next:
subscription-snapshotted purchased capacity, protected setup PKCE grants, Core
company/payer binding and idempotent create/invite/audit, then licensed sessions
and company/branch/module scope. Initial SaaS companies use separate Core tenants
within the shared app/database; one identity/payer can span explicit workspaces.
No per-customer source/container. Existing tenant data is not rewritten.

No production image, migration, flag, key/client, ingress, payment rail or Perfex
state changed. The older qualified private image remains distinct from this
source. Do not give employees billing memberships or infer grants from email.

# Actual Laravel/Kuubu/Core interoperability —7 November2026

The actual backend protocol now passes: published Kuubu signed relay/account
client → genuine Laravel Passport PKCE/RSA resources → real ASP.NET OAuth/cookie
handler, plus the Core machine licensing client. Exact implementation
`3f5cd44bb280dd3770a9c4091f9b6289e9a9cc1b`, provider source8e96bef7, website
source d3452e6c. The opt-in case passed in13s; Windows122 focused checks passed.
Linux122 focused and all2,093 existing HRM regressions passed (only the new opt-in
case skipped without a fixture). Read `docs/engineering/CORE-PROVIDER-INTEROP-20261107.md`
for exact hashes, source/SDK/fixture scope, reproduction and retained failures.

Real execution found a hidden contract mismatch: Laravel HasUlids emits lowercase
enrollment IDs; the prior Core consumer required uppercase. Core and valid test
fixtures now use the exact lowercase provider reference. Uppercase/transformed,
trailing-newline and injected references remain denied before network traffic;
no persisted ID was rewritten and no permission check was relaxed.

Acceptance verifies explicit personal join, confidential S256 exchange, immutable
verified subject, replay denial, a token-free/no-tenant/no-role pending cookie,
no ERP operational session, machine licensing without staff billing membership,
foreign-product denial, fresh denial after subscription suspension and bounded
expiry. Provider counts stayed at the explicit seed baseline except one personal
join; Core user/link/session tables stayed empty. All local fixture processes
stopped and ports8101/8120 closed. The SDK test container also exited0.

This does not enable licensed company access. Next: Core-owned company/payer
binding and setup authority, invitations, fresh link-bound PKCE admission,
encrypted grants/refresh/revocation and licensed operational sessions, plus
company/branch/module enforcement and public SaaS local-auth/anonymous-intake
policies. Public TLS/browser callback, UI, runtime ACL and existing financial/
operational gates remain. Do not give staff billing membership or grant company
access from matching email/domain or a commercial-access read alone.

No production migration, image, clients/keys, flags or ingress changed; private
ERP ready200 remains on image3ff07382. No SSH restriction was weakened. An early
forwarding attempt stopped safely; the successful gate used local portable SDK,
PHP and Node on loopback with fresh synthetic data and no mail/payment rail.

# Federated Core principal prerequisite — 7 November 2026

Stacked draft [PR201](https://github.com/georgemunganga/erp/pull/201) now adds
explicit local/federated credential mode, a role-free federated principal factory,
nullable credentials constrained by mode, immutable mode and populated rollback
guards. Existing local accounts keep their credentials. Federated principals
cannot use local sign-in/recovery/setup/change/admin reset or acquire a local
session; account DTOs expose per-principal credential capabilities. It creates
no workspace, identity link, billing membership or operational session by itself.

Exact tested source `2c6f9085e029138ebdb0eb311c49a98ae81b9741`:122 focused,
28 genuine PostgreSQL and2,093 full HRM regression cases passed, zero failures/
zero skips. An independent read of13 synthetic databases confirmed actual
migration/check/trigger/composite-FK state and null credentials for all five
federated fixtures. The dedicated PG and SDK containers are stopped. Read
`docs/engineering/CORE-FEDERATED-PRINCIPALS-20261107.md` for hashes, retained gates,
upgrade/rollback evidence, reproduction and scope. The older design-only checkpoint
below records the prior source state; this prerequisite is now implemented.

Still not ERP admission: actual provider interoperability, explicit company/payer
binding and setup authority, team invitations, fresh PKCE link admission, protected
server-side grants/refresh/revocation, licensed sessions and complete company/
branch/module enforcement remain. Public SaaS must disable anonymous local entry
and reject local-session licensing bypass regardless of principal mode. Federated
account UI must respect both deployment and per-principal capabilities. Existing
public-intake, runtime ACL, financial and operational release holds remain.

No production DB migration/image, public ingress, clients, flags, Google or Lenco
changed. Private ERP readiness200 on unchanged image3ff07382. Re-read public
main7b7e3fc7 and qualified release6b47bbeb are unchanged. No whole-main deployment.

# Core commercial licensing channel — 7 November 2026

Stacked draft [PR201](https://github.com/georgemunganga/erp/pull/201) now also
contains a separate default-off confidential licensing client. Exact tested
implementation `11879016777e077ec1adaa295aeaf7c149f753e9` passed105 focused
checks and the complete2,076-case HRM suite, zero failures/zero skips. It uses
only product:license machine credentials and exact Core-supplied enrollment
references; it does not require staff billing membership, create principals or
issue operational sessions. It validates bounded commercial leases, modules,
issuer/product/reference and expiry, with monotonic time caps, bounded JSON,
single-flight machine tokens, no redirects/cookies/logged credentials and no
cached lease returned on failure. Licensing requires both product flags and a
distinct machine client; no production configuration was changed.

Read `docs/engineering/CORE-COMMERCIAL-LICENSE-20261107.md` for exact source,
archive/log hashes, retained attempts and native qualification. The final gate
ran with no network and no production database connection. Actual Laravel↔C#
interoperability, company binding/admission and runtime permission enforcement
are still required; no SaaS launch is claimed. Existing deployed image1f54ff04
and public main/release baselines below are unchanged by this source increment.

Discovery confirmed that current LocalUser requires email/password fields and
LocalAuthenticationHandler emits email claims unconditionally. The next explicit
principal model must not invent addresses or give federated users an independent
local password recovery path. Design only:
`docs/engineering/CORE-FEDERATED-PRINCIPALS-20261107.md`. It preserves local-mode
deployments and requires explicit Core grants and exact external links. A billing
access read alone is not setup/rebinding authority. Coalesce commercial reads per
current company binding within the lease budget; always recheck Core grants.

Private ERP ready200/image unchanged; both branded public logins and API health
returned200. Perfex login rendered normally in browser after a scripted403.
No credential, billing, Google, ingress, database or deployment change was made.

# Kuubu owned authorization/provider follow-through — 7 November 2026

The website relay/opt-in step is now implemented in [Kuubu PR3](https://github.com/Greenwebb/kuubuerp-website/pull/3),
source `d3452e6c6ad652206169a7f298222197692e5edf`. All92 tracked blobs were
verified byte-exact against its published Git tree.35 normal tests, API build,
14-page/252-link checks and hosted CI run42400939282 passed. The separate actual
client→signed relay→Laravel confidential PKCE/RSA/replay/session test passed,
with a read-only audit confirming no payer/customer/subscription creation.
Browser checks preserved Kuubu's own login, explicit enrollment, account switch,
disabled Google and390px layout. No production frontend was deployed.

Foundation source `62f8971de47d489bc95b6d489ad7a794b7bc7c1f` in itsPR3 adds
the exact signed authorization path and replaces the shared30/minute backend-IP
bottleneck with bounded edge and post-protocol actor/enrollment quotas.57 tests/
482 assertions passed on Linux PHP8.4.27 and genuine isolated MySQL. Read
foundation `docs/PRODUCT-IDENTITY-ADMISSION.md` and website
`PRODUCT-AUTHORIZATION.md` for hashes, fixture boundaries and retained evidence.
Foundation hosted run42400388410 failed at startup before jobs; cause unproven.

These source gates do not qualify an actual browser→deployed ASP.NET callback or
operational workspace admission. Core still needs explicit invitation/owner setup,
workspace/payer binding, protected server-side operational sessions, runtime ACLs
and company/branch/module enforcement. No real client/key was registered and no
production product flag, database/image, ingress, Google or Lenco state changed.
The older checkpoint below records its own source state; do not redo the relay/
quota work as if it were still unimplemented.

# Kuubu product identity consumer — 7 November 2026

Stacked draft [PR201](https://github.com/georgemunganga/erp/pull/201), branch
`codex/kuubu-product-consumer-20261107`, depends onPR200/PR199. Exact compiled
source `7d67f6d16a414aacf3a8a6242dc94aa9576ff3b6` passed59 focused checks,
15 genuine-PostgreSQL scenarios and the full2,030-case HRM suite/0fail/0skip.
The default-off confidential OAuth/PKCE consumer uses a separate protected,
short-lived product identity cookie, containing no provider tokens, ERP roles or
tenant. It never creates an ERP operational session. Core reads only explicit
active issuer/subject links and same-tenant active principals; no email join.

The additive owning migration enforces tenant/user FK pairing and durable unique
bindings, with raw-SQL binding and populated-rollback guards. A trusted scoped
tenant key generator preserves existing local-user constructors while completing
the composite key before EF tracking. The native audit verified all five new
synthetic databases' actual migration/FK/trigger state. SDK and dedicated test
PostgreSQL containers are stopped; prior attempts/evidence remain retained.

Read `docs/engineering/CORE-PRODUCT-IDENTITY-20261107.md` for exact hashes,
receipts, source limits and release gates. Hosted GitHub CI remains account
billing-locked before its job starts; native results do not claim hosted success.
No public ingress or production database/image change accompanied this step.
Published main is still7b7e3fc7; qualified release6b47bbeb and deployed1f54ff04
remain unchanged. Existing financial/tenant/module holds remain.

Next: connect Kuubu's fixed own authorization/opt-in relay and allow only its
registered Core callback; remove the provider's shared30/minute backend-IP quota
bottleneck. Then implement explicit invitation/workspace admission, Core-owned
workspace/payer licensing bindings, protected server-side operational sessions
and company/branch/module enforcement. Never give ERP teammates billing
membership to make company licensing pass. Anonymous speak-up organization
selection and callback/query log redaction also gate shared public ingress.

# Core session tenant repair — 6 November 2026

Stacked draftPR200 (`codex/kuubu-session-tenant-20261106`, compiled source
`3f6c4b890a1b81827e31eba1e36145179e4dda04`) depends on packagingPR199.
Actual34 focused and10 genuine-PostgreSQL scenarios passed, followed by the
complete2,000-case HRM suite/0fail/0skip. The fix resolves a globally unique opaque
session hash and exact tenant/user pair before current role resolution and
activity/audit writes. It does not select organizations from email/domain,
headers or commercial membership, create identities or grant workspace access.

The reviewed application changes are backported to the maintained qualified
release as `1f54ff046cf69b7b4fb8604da9ad9bf6bc7b2f63`. Its independent Docker
test-and-publish gate passed1,982/0/0, followed by nonroot PDF smoke. After a fresh
verified backup/independent restore, the loopback-only qualification API switched
to1f54ff04. Actual health/session/admin checks passed;57 migrations and0 users/
sessions remain. No schema or public ingress changed; prior408 image/config/logs
are retained. Read
`docs/engineering/CORE-SESSION-TENANT-20261106.md` for hashes, native fixture
limits and retained failed-attempt evidence. Laravel admission and operational
tenant/module acceptance remain open; all prior release holds remain in force.

# Kuubu qualification checkpoint — 6 November 2026

Published ERP main remains `7b7e3fc70b2d4866bb24e165a9646d598b589a8e`;
the existing ERP UI pin remains `2b3357d2de3c383ea2926264abc701b1564a5895`
from the prior handoff, unchanged by this work. No new ERP UI publication is
claimed. PR199 repairs main-line Docker packaging and test fixtures;1,990 tests
passed locally, while hosted CI could not start due to the account billing lock.

On the separate authorized Kuubu host2.24.140.104, published release branch
`release/kuubu-baseline-20261106` at `86330ea6` retains documented application
baselinee340a6f4. Exact image source408da398 passed1,972 tests and nonroot PDF
rendering. Its API is loopback-only, with57 genuine Core/HRM migrations and no
login users/sessions. Native ACL denials and a real independent restore of all98
HRM tables passed. Read the branch's `deployment/kuubu/QUALIFICATION-20261106.md`
for image IDs, hashes, ACL limits and receipts. The existing ERP installation on
the other historical host was not accessed or changed. Root retains integration
and rollout ownership; no collaborator task or acknowledgment is inferred.

This is not a main rollout or SaaS launch. Held B1c/M3/financial controls remain
held. Tenant/session resolution, Laravel-to-Core identity/workspace admission,
unrelated-tenant denials, operational module ACLs and off-host recovery remain
required before customer routing. Separate GreenWebb and Kuubu login UIs now use
central Laravel identities; a login or billing record grants no ERP organization.

# Latest published checkpoint — Procurement, 4 November 2026

Fetch ERP main `ae32466e2959b79e62ae1d0a3b457c58110389ae` (PR197) and UI main `2b3357d2de3c383ea2926264abc701b1564a5895` (PR108/109), or reviewed descendants. These supersede earlier prerequisite pins below. Latest UI company-context/settings fixes are deployed; API stays on the separately qualified c93/M2 image with239 tables. Held B1c prevents wholesale main deployment.

**Manus issue196 pickup:** M3 pure source/capacity foundation and the28-section PROC-PAY-03 contract are now merged, freeing those files for the next reviewed implementation. Read the [remaining milestone register](docs/procurement/gap-closure/REMAINING-MODULE-ROADMAP-20261104.md), including before-allocation invoice capacity and complete-history/version requirements. Implement M3b/c/d/e with genuine migration, existing Core/payment controls and original UI; no fake invoice, ledger, invented authority or production activation. Root retains production/native credentials and rollout. No Manus acknowledgment/start is inferred.

[Actual source, deployed UI, owner draft-CRUD and evidence limitations](docs/procurement/gap-closure/OWNER-CRUD-AND-COMPANY-CONTEXT-20261104.md).2,065 foundation Procurement tests and actual bounded owner-cookie/native checks do not mean the full module or PO advance workflow is complete. Actual company policies and final UAT remain open in issues145/190/192/196. Historical checkpoints below do not override this entry.

---

# Procurement current handoff — 4 November 2026

**Latest deployed release — Core directory + M2 request routing:** source prerequisites ERP main `b291b64e790707ff259f0d65d6330a0f6a522186` (PR193/194) and UI main `0b1143a31fcdf3bc12d9e558f79614e08bc280b4` (PR107). Actual API is c93 backport runtime30bba4c4, verified descendant `e340a6f46ada8b2402b04eea8a7734c67004f348`, image `3da35edf636057662ef30918399397e4a82fc928878fe23ec471a7f08b20293f`, preserving production239 and prior business flags. Core workflow directory and original Procurement Buying rules with named person/role routing are live. Backport2,028 Procurement +49 Host tests, both frontend builds, exact266 public files and17 actual anonymous Chromium pages passed. [Production receipt, failed attempts and rollback](docs/procurement/gap-closure/CORE-REQUEST-ROUTING-PRODUCTION-20261104.md); [actual company setup/UAT checklist](docs/procurement/operations/COMPANY-APPROVAL-SETUP-AND-UAT.md). Earlier unchanged-deployment statements below are historical. No source-only feature is presented as live; no company UAT/financial activation is claimed.

**Next bounded Manus milestone for separate hardware:** review the published M2 contracts and preserved UI, then propose/implement M3 general PO-backed advance-payment support using the existing Core approval, payment idempotency/recording/independent-confirmation, finite policy and native-readiness mechanisms. First produce the28-section feature/design/contract and genuine migration plan against current published prerequisites; return a PR with actual backend authorization/business tests, preserved-screen browser cases and isolated native/recovery evidence. Keep unrelated held B1c source/schema out of any c93 production backport. No fake invoice, bank execution, Finance ledger, second workflow engine, invented company policy, production grants or deployment is assigned. Root retains release/runtime and native production ownership. This is a published task specification, not evidence Manus has started or acknowledged it. Owner-launch190 and full-module145 remain open.

**M2 source verification checkpoint:** explicit Core person/role and requester-dependent purchase-request routing is implemented in backend `f62a35a902a54fcefbbc67fcd2f3c3cdefe2194c` and preserved UI `12633f91733f5eae355d1377d6ba9194461910a8`. See [M2 evidence and remaining release gates](docs/procurement/gap-closure/CORE-REQUEST-ROUTING-M2-20261104.md). Actual final 2,033 Procurement +49 focused Host tests, 984 UI unit checks, TypeScript/build and21 unique mocked-browser cases passed across verification runs. A c93-schema backport is being verified separately; do not deploy latest main over production239 or activate financial flags. This supersedes M1's named-routing implementation gap only; native company acceptance, business setup, department governance and general PO advances remain open. Source branch pins are review checkpoints; published merge and deployment pins will be recorded separately before assigning dependent Manus implementation.

**Core workflow M1 source checkpoint:** `dd7e96b4cfe98051194e3c5709f87b10cd67aa77` adds a Core application-layer workflow configuration registry and `/core/workspace/settings/workflows`, selecting module/record/trigger and opening existing Procurement settings. Five registrations cover PR, PO, two invoice-backed payment stages and purchase plans. It grants no permissions and does not activate purchasing. 45 selected backend tests,7 frontend contract checks,6 Chromium UI cases and full TypeScript pass. See [M1 evidence and limits](docs/core/verification/CORE-WF-01-M1-20261104.md). ERP main at start is `3fbc66fc5137c2d4828b98ef56ac2da09b44930d`; UI main remains `5b8814e9d58c8a2a5c3b7ac0fbd22cc922cd4523`; deployed backend/UI are unchanged. This is M1 only, not completion of issue192 or owner launch190. Next: versioned explicit Core person/role/requester-dependent PR routing, department-governance binding, then payment timing/advance controls and real-cookie UAT. Do not claim department worker-manager pointers are Core user grants or implement another approval engine.

**Company workflow verification:** [the actual supervisor/executive/Accounts audit](docs/procurement/gap-closure/COMPANY-APPROVAL-WORKFLOW-VERIFICATION-20261104.md) found implementation gaps, not merely unpublished business values. Core ordered approval mechanics are reusable and tested, but Core has no module/document workflow configuration surface; Purchase Request editor/resolver lacks named executive/supervisor and requester-role routing, BudgetOwner is offered but unresolved, and manual payments are invoice-backed without a general PO advance path. 32 UI tests,71 selected existing backend cases and10 targeted in-memory checks passed, including confirmation of these limitations. Do not claim the whole chain configurable already or activate it to bypass missing bindings. Fix this as part of owner-launch190 before advanced B1c. No product deployment or business configuration changed in this audit.


**Owner-pilot priority:** updated UI PR106 is now merged/deployed (UI main5b8814e9, product0cf2f881), with45 public file hashes/11 HTTP routes/13 actual anonymous Playwright renders passing. Existing backend c93 and business flags are unchanged. [Minimum launch gaps and exact release evidence](docs/procurement/gap-closure/OWNER-PILOT-RELEASE-AND-LAUNCH-GAPS-20261104.md) supersede earlier pending-UI-deployment status. Prioritise real owner-account supplier/item/request CRUD and the basic request→approval→PO→receipt→invoice journey before advanced B1c work. Production approvals/direct orders/manual payments remain off; policy/authority and storage headroom require resolution. No whole-module acceptance. Private rehearsal PG is now stopped; data retained.


## Latest native and UI checkpoint — 4 November 2026

Backend source PR183 and evidence PR186 are merged; published ERP main `718ab07d275659ee0a6cea49bc9718852c8efa4f`. The deployed Plan release described below is unchanged. B1c is not enabled or deployed; whole-module issue145 remains NOT READY.

- Actual full2,009 Procurement and569 focused Host tests passed on backend9fecef, retaining all earlier551 Host identities. Earlier failures remain separate evidence.
- The genuine B1c migration passed on one isolated physical clone. Empty Down/Up inside a rollback and one audit-marker retained Down55000 passed. Root independently compared both rollback phases' before/after241-table native state with the original Up baseline; all three protected237/239/239 databases were exact. Combined private receipt SHA256 `ef635ad6e594ffd233344e8b5609b3cced9f0f016a3575b0b2c472e0dce70566`. This does not establish committed Down, true logical restore, all retention markers or AX races.
- Preserved-screen UI integration is pushed in [UI draft PR106](https://github.com/georgemunganga/module-connect/pull/106), product0cf2f881, documentation05d3cfb2. Full TypeScript,40 focused Vitest checks and live-mode production build passed. A real Chromium smoke passed the workerless review-only queue, empty-page cursor and absence of unrelated request-approval calls; its APIs were mocked. Receipt SHA256 `d59074ffba277884fad9d9539c34fa017441ed170babb487b01a88c873d2a6ff`. Initial sparse-build/missing-metadata fixture failures are preserved, not relabeled.
- [ERP draft PR187](https://github.com/georgemunganga/erp/pull/187), source14e1afbb, adds the missing review-completeness-function fingerprint/owner guard and an unexecuted regression test. It is not covered by the earlier569 pass and must not merge/deploy as completed readiness capture. Migration/fingerprint constants remain blank and flags defaultoff.

The parallel coding agents reported account usage limits during this slice. Root continued directly, verified and pushed the interrupted work; no coding agent is assumed to be running. All production services remain on the prior technical release. Private evidence lives under the dated `procurement-b1c-native-migration-prep` and `procurement-b1c-ui-root-verification` directories; no raw customer records, cookies or credentials belong in GitHub.

**Next bounded work / Manus review:** independently review UI PR106 and ERP PR187 against the published prerequisites; retain the imported layout and shared components. Finish actual migration-qualified direct and true-restored catalog captures for new review readiness and affected B1/Plan/shared-policy/invoice/payment selectors, compiled finite-role provider positives/negatives, AX races and all retained-evidence downgrade paths. Then verify real Core-cookie Return → edit/resubmit → Approve in the preserved UI, original-key recovery and exact native reconciliation. Review-only people must not require owner-read or employee permission. Do not invent expected hashes, policy/approvers, document pins or Finance effects; do not add a second workflow/document engine. Keep PR187/native ownership coordinated with Root before editing the same source or applying SQL. Independent review is available on separate hardware; no production mutation is assigned to Manus.


**Plan technical release is live.** Published code prerequisites are ERP main `3824fd1805f46a0d6177b7356500645c2bd74271` and UI main `ef42580fa53b5ef74c1b63d3f81d5302597f9bc1`, or reviewed descendants. ERP PR171 and UI PR103 are merged. Actual deployed executable source is backend `c93e8df4f82214b58273bf0c9be58d013aca3331` / image `8fa2f598…`, with preserved UI product `3e2783b1f373857ba11681e73407c13ab09e30a8`. Documentation merge SHAs and executable source pins are intentionally distinct. See the [production release and permanent asset publication rule](docs/procurement/gap-closure/PROC-05A-PAIRED-PRODUCTION-RELEASE-20261104.md).

Actual551 Host cases passed; the previous2004 Procurement cases were exactly verified/reused, not rerun. The original UI primary create→submit→review→final approval has private normal-cookie and native-owner evidence. Production genuine migration239 and exact13-right addition/eight new-object legacy revocations preserved all original237 tables' original-column business/authentication rows. Five production-purpose readiness checks passed through two real finite LOGIN roles; full B1 round READ remains held/observational. The same verified API candidate completed a reviewed UI handover continuation after the initial HTML mismatch; no migration/grants/container creation were replayed. Public44 asset hashes/sizes,22 route/auth checks and7 actual anonymous Playwright renders passed with zero page errors and owned-scope cleanup.

Only `Procurement:PlanReview:Enabled=true` was added; no policy, approver or business permission was seeded. Configure real review policy through the existing Core-backed administration before sending plans for approval. Anonymous smoke is not signed-in production business UAT. Alternate Plan flows and wider issue145 remain **NOT READY / open**.

**Next owned work:** B1c issue180 / merged PR183 is published in ERP main `7166e36232fb464a005dc5cbea9e1eef1ebd4848`, with verified backend source `9fecef1732b7629f5ac8dbd005c863d20898a250`. Its genuine EF migration/model/six SQL outputs and full 2,009 Procurement tests have actual evidence; the prior Host compile found two local-name collisions, now corrected. Actual 569 focused Host tests passed with all prior 551 test identities and all compiled runtime bytes retained; the earlier incomplete 394-test filter attempt remains a separate rejected acceptance result. Native capture, finite-role/runtime, preserved-UI integration and real-cookie acceptance remain open; new readiness capture pins are deliberately blank and workflow flags default off. See [the source verification record](docs/procurement/gap-closure/M7B1C-SOURCE-VERIFICATION-20261104.md). Root owns coordinated acceptance; do not overlap its schema/API work or call it deployed. Independent Manus scanner issue153 remains available on separate non-production hardware under its existing handoff; do not create a second document subsystem. Historical sections below retain earlier evidence and are superseded for current status by this section.

## Retained Plan source integration — 3 November 2026

Issue [169](https://github.com/georgemunganga/erp/issues/169), draft [ERP PR171](https://github.com/georgemunganga/erp/pull/171), now includes actual published ERP main `f064f1a343e9897f4234bed7b6fd9586aebcded3` (accepted PR176 and B1 release PR177) through merge `d2c24ae4ea8f156406ec8ab23d94a992fc2c3921`, retaining the earlier B1 baseline `dc6a082db74f9740104e2295988bc6f1f54027d8`. Published UI main is `9b5d02dcf971900289793f56e91895dd3747fd80`; Plan UI PR103 has separate ownership. The single B1 genuine migration `20261103163108_M7b1CommercialRoundDraftPlans`, designer and snapshot remain byte-identical to ERP main. The host All-reader merge retains both Plan review and B1 round-plan routes.

Source review additionally sealed each submitted line's version in the immutable snapshot, alongside its ID and exact four-place decimal values. A regression case covers a version change with unchanged values; it has not been executed. [The source readiness record](docs/procurement/gap-closure/PROC-05A-POST-B1-SOURCE-READINESS-20261103.md) distinguishes source checks from runtime evidence and records remaining schema and contract gates.

Prior integrated backend source candidate is `c0dc4198` (R8 security source `72601ac3813c68c24278847eb6f094039e20a9c1` cherry-picked onto genuine adoption `a4f31f7c669eb7f8040990d14007d6146b71a703`). A separate author-peer source review verified the extracted shared Core projection implementation body byte-identical to the former B1 guard, with unchanged minimum-read grants and existing B1 facade; actual SQL golden/runtime verification remains pending. Plan READ/WRITE now reject effective extra HRM SELECT and sealed submission UPDATE; WRITE admits exactly five terminal/concurrency columns. Four InMemory terminal-save cases are included in14 new Host cases, all unexecuted; two migration safety cases are also unexecuted. Actual2002 Procurement passes belong to the earlier raw generation run, followed by retained Host CS0833 failure. No post-fix Host/model/SQL/native success is claimed. Warm source and SDK remain Root-owned.

Root's later actual verification of `400441d1` passed2004 Procurement cases, then Host477 yielded437pass/40fail (noOOM). The retained TRX identifies38 endpoint-construction failures from implicit Plan GET body inference in the minimal legacy fixture, plus2 stale assertions confusing safe Core Approval ownership metadata with business data privileges. Source correction explicitly marks all mapped Plan-handler DI parameters FromServices, keeps real registration/readiness and transport DTO/query bindings, and narrows those assertions while preserving the exact ownership guard. The added minimal-host endpoint-construction regression and source corrections remain unexecuted; Root owns subsequent Host/SQL/native verification. No warm source or SDK operation occurred for these fixes.

Actual follow-up source `bd4ace99` now passed478 Host/0fail/0skip with exact2004 Procurement reuse, model parity and SQL emission; bindingSHA `5141705b79f050ccc2601617adc398c7cd3a1ce218c06c83aaff2b01ccbd3d7b` is retained under the HTTP verification packet. No database/publication/runtime acceptance follows. A later source-only Detail correction ties Withdraw/assignment hints to the requested currently pinned Pending round and live case/stage/assignment, suppressing actions on historical rounds and Company All detail; six authored regression cases are unexecuted. Actual native capture is still required before manifest adoption.

Actual native r4 direct/true-restore observations now populate the Plan migration/two-check manifest and one immutable shared CHECK capture. Both exact variants are accepted by applied-migration-qualified generic operational identity, unit-review, invoice-owner-baseline and payment selectors. Actual payment canonical query hash `2a1a42eccb1379a8039da14a59458440a286f8967db5901abe3f144e532fe759` is evidence-bound; old pre-Plan expectations and other catalog predicates stay exact. Twelve new selector/payment-query cases are unexecuted; six Detail action cases remain unexecuted too. Source adoption does not establish finite-role/API/real-cookie workflow or UI acceptance. [Exact capture/payment provenance and verification bounds](docs/procurement/gap-closure/PROC-05A-POST-B1-SOURCE-READINESS-20261103.md#actual-direct-and-logical-restore-check-adoption--native-r4).

Integrated source now includes actual capture adoption `190dac6038e0f249074832959cfe0228cc21c454` and independently authored B1 restore hotfix `e6d87c5ce3882c5240abfa3355969a64554896dd` (held PR179), cherry-picked as `81778f06d1d4ce32971ec0dc011870f3000213ca`. The B1 fix maps only five exact observed named restore CHECK definitions inside the original complete catalog fingerprint; its expected hash, schema/privileges/flags/migrations and unrelated predicates remain unchanged. No direct main merge occurred. Next Root SDK candidate must verify twelve new Plan capture/payment-query cases, six historical action cases and twenty B1 restore cases; none is covered by the prior478 result. Actual direct/restored provider SQL and finite-role/native compatibility still require integration evidence.

Before the next SDK overlay, the missing Plan branch in shared CheckCompanyRules was corrected: strict schema2/current Core company/assignee action/branch/alias checks now cover Create/Update/SubmitReview/Publish without inventing policy currency fields or employee requirements. New purpose-specific config picker `/configuration/policies/lookups/plan-reviewers` supports exact eligible-total pagination and same-scope bounded principalIds hydration; sourcing-publication permissions are not reused. Fourteen handler/lookup regressions are unexecuted, and the actual Npgsql shared policy API/native-cookie lifecycle remains a required acceptance gate. No direct database policy seed is evidence. UI ownership receives the exact contract through Root.

Peer R8 confirmed Plan policy schema2 is entity-global: current organization-wide Core configuration actors and named Review/Final people are required, independently of shell branch/department. New picker has the same global eligibility boundary and no extra Core grant. Raw policy All retains company context for organization-wide users; branch-confined All remains400 before fallback. Transaction All guards stay unchanged. Twelve further cases are unexecuted (five middleware, seven handler/lookup); Plan policy class totals21 cases. Branch-specific policy conditions are unavailable and must be explained by the existing UI.

R8's cleanup-path audit additionally closed Return/Retire and retirement-hint global-scope bypasses. A shared current Core global configuration-actor gate now covers all Plan policy mutations; cleanup calls it without currency/rules/assignee validation while preserving original identity/SoD/readiness/unused-draft checks. Six more source tests are unexecuted (policy class27): branch/action revocation with unchanged records/audit/outbox and hintfalse, plus correction with unavailable currency/assignee. R8's full source audit must clear before Root's final SDK freeze.

Pinned-policy source correction now separates current submission eligibility from retained round provenance: existing rounds retain their exact Published/Superseded policy after replacement/expiry through shared historical proof; current Core authority and explicit denial states remain enforced. Added regressions remain unexecuted. Attachment requirements, delivered notices and SLA/reminders are explicitly unavailable in the initial strict stage-only policy schema pending adopted shared-service contracts.

Root owns all SDK/scaffolding/native/finite-role/private runtime and production work. No SDK, migration generation/application, database, Docker or deployment action was run for this integration. Actual genuine migration `20261103211606_ProcurementPlanGovernedReview` is now adopted with raw-identical designer/snapshot and a reviewed Down retention prefix. It covers two review tables, nullable plan branch/index and shared operational-policy identity CHECK expansion. Root's actual2002 Procurement cases passed; Host compilation then failed CS0833. The explicit audit member-name fix and new migration safety cases remain unexecuted. [Exact raw/final provenance and remaining gates](docs/procurement/gap-closure/PROC-05A-GENUINE-MIGRATION-PROVENANCE-20261103.md). Actual direct/restore manifests are now adopted in source; integrated provider/finite-role/workflow verification remains required before runtime admission. Do not publish this branch as a schema-complete release or copy mappings into the snapshot by hand. No business assignees, quorum, policy publication or feature switch is activated. The actual post-Plan shared CHECK fingerprint and migration-specific selector must ship with it; the existing owner-baseline fingerprint would otherwise deny all operational-policy admission. [The inert generation/native plan](docs/procurement/gap-closure/PROC-05A-GENUINE-GENERATION-AND-NATIVE-PLAN-20261103.md) records verified accepted PR176 ancestry and meaningful compatibility/retention negatives. B1 technical release is complete; Root retains allocation of the next Plan SDK/resource slot. Concrete inert packet `/home/amizo/erp-backups/procurement-plan-review-generation-sdk-20261103_run01` binds this backend source to accepted warm6ec, an exact27-file overlay, archive-aware23-current/29-historical-publication guards, mandatory offline restore, bounded launch argv and genuine EF generation script. Only syntax parsing ran; no overlay/SDK/container/DB step ran. Root must claim169 and allocate/review its slot before execution.

---

# Current production and collaborator boundary — 3 November 2026, 20:56 UTC

The B1 paired technical release is now deployed and verified. Published ERP prerequisite is main `950856f7e01e3e2d7c628ddbf67dbac846644803` (merged PR176) or its reviewed descendant; UI main `9b5d02dcf971900289793f56e91895dd3747fd80` remains published. Deployed products are backend `6ec41f938409f9400bd627f727c0161af0043479` / pub29 and UI `26d4ef1924b508a4ca33a184ec21500691003b3f`, image `97bd9e85…`. See the [actual release record](docs/procurement/gap-closure/M7B1-PAIRED-PRODUCTION-RELEASE-20261103.md).

Actual429 Host passes, separately bound1970 baseline reuse, genuine restore/Up/Down/Up, exact production24-revoke/22+3 grants, compiled read-only provider acceptance,150 live runtime hashes,16 public route/auth checks,5 primary+38 compatibility UI assets and7 anonymous Playwright renders passed. The interrupted pre-record3 step and reconciled single-grant continuation remain explicit. No signed-in production CRUD or whole-module UAT is claimed; issue145 stays **NOT READY**. All processing flags remain off and current Env/HostConfig is preserved.

Next candidate: issue169 / draft ERP PR171 `codex/procurement-plan-approval-20261103`, published source `a2e39e595347fb328283756eab13264e330c4d0f` (includes accepted main950856f7), paired draft UI PR103 source `1cb1bf4d3cc357893387c5ed5066dca0d9193788`. Re-read issue169 ownership before scaffolding; only one genuine migration is allowed. Its current migration directory still equals main:2 review tables, nullable Plan branch/index and the changed existing operational-policy identity CHECK still need actual generation/capture/compatibility verification. The UI's173 focused source tests do not replace that backend/native evidence. Root owns runtime/SDK coordination; the independent Core scanner task153 remains separate. Do not reuse deleted PR165/104 remote branches or stale tips from historical entries below.

The idle private test database is stopped. Generated warm SDK obj caches were safely cleared after proof that all tracked source and10 runtime test assemblies remained exact; run offline restore before the next build. No source/bin/publication/database/release evidence was removed. Earlier entries below are retained as dated history and do not override this current status.

---

# Superseded prerequisite checkpoint — 3 November 2026

Fetch ERP main `dc6a082db74f9740104e2295988bc6f1f54027d8` (including merged PR165) and Procurement UI main `9b5d02dcf971900289793f56e91895dd3747fd80` (including merged PR104), or reviewed descendants. These are published source prerequisites; the deployed pair remains the separately recorded manual-payment release until the B1 technical rollout completes. Backend product `53ed4d5486173580660cb3ea571c3703f227c85a` and preserved UI product `567e318105687b655a886e79c07eebd2b6bf4563` are now deployed. The final backend 361/UI 172 checks and private run03 six-form/native/security/owner evidence remain distinct as recorded in the [current paired release](docs/procurement/gap-closure/MANUAL-PAYMENT-PAIRED-TECHNICAL-RELEASE-20261103.md).

Root completed cutover run02 and public/browser verification at 16:23 UTC: API `be45e03038a596a7e52ee5deab74a0300493eb34849fe33cc79d99aa687577d3`, image `sha256:eb2077c982a7e24722c1afeb1a3206b9c5dfea2e67af4f984089800bc24400aa`, UI alias `payment-write-567e3181-20261103/`. All 150 API files, five UI / 37 compatibility assets, 15 public route/auth checks and seven anonymous browser renders with zero page errors passed. Attempt01 caught UI 500 and fully rolled back; Root repaired only staged-root permissions 0700→0755 with all 42 content entries unchanged, then separately approved the successful retry. All containers/releases and first-attempt receipts remain retained.

PR167's exact 11 narrow Procurement rights passed full ACL-delta verification and the actual final production full-governance provider returned true in a forced-read-only canary. No Core identity/Finance write or broad HR grant was added; all application Env/HostConfig and PG/publisher configuration are preserved. **Production payment-policy counts are zero and `Procurement:ManualPayments:Enabled=false` remains unchanged.** Configure an actual independently reviewed payment policy and current scoped Core duties using the existing forms before a separate activation/signed-in production acceptance review; do not invent fixture policy values or implement another payment engine. All 17 synthetic cohort sessions are revoked/original 192 preserved; private policy API is stopped. No Finance execution, verified attachment or signed-in production CRUD/UAT claim is made.

**Issue145 remains NOT READY.** Sourcing PR165 / issue162 now has genuine EF migration `20261103163108_M7b1CommercialRoundDraftPlans`, the actual 1,970 Procurement test result, and latest source `0f9bbc0bc9a2c236b40613d46538751323b78eaf` / publication28 with **411 Host checks passing**. The initial compilation/OOM failures remain retained. Isolated native acceptance passed actual workerless Core-cookie Create/Revise/Cancel, three-version history, original actor/key recovery, cross-actor key privacy, same-cookie current READ withdrawal and branch reassignment denials, strict row/audit/outbox reconciliation, and exact Core fixture restoration. Credential/payroll SELECT additions denied READ/RECORD; missing referenced-table-owner schema USAGE denied RECORD while preserving READ. See [bounded B1 evidence](docs/procurement/gap-closure/M7B1-PRIVATE-SQL-ROLE-ACCEPTANCE-20261103.md).

The preserved imported UI source `8dca4943bbe5b5ea4993a7eecb0e7a40fecb53c1` passed its actual offline production build; no replacement UI was created. A separate fresh original231-source UI clone passed genuine upgrade to237 tables, all six new tables empty and expected ownership/schema access, while preserving the source and earlier Cancelled native graph. The first actual UI run saved version1 and recovered its original201 response, then found an All-branch rendering bug. Fixed UI product `26d4ef1924b508a4ca33a184ec21500691003b3f` passed37 focused checks/typecheck/build and a narrow actual Playwright continuation on that retained record: All reads, C revision/cancellation, immutable history and key privacy, zero page errors. Cumulative native owner proof `1785d682…` and exact Core role cleanup passed; the failed run remains retained. Populated Down guard and rollback-only minimal M4 publication-barrier tests passed with unchanged rows/ACLs. Paired release remains open, with production round flags off. Both private APIs/proxies are stopped; all six native and three UI cohort sessions are revoked, and their existing Core roles restored exactly. Scanner153 remains external/no verified attachments. Business-owner sign-off and wider UAT remain open; hosted CI stays billing-blocked. Do not merge held P32/P36/P38 or overlap Root's release/runtime/SDK ownership.

**Assigned independent Manus build/scaffold pickup:** [issue170](https://github.com/georgemunganga/erp/issues/170), [MANUS-PROC-M7B1-SCAFFOLD-05](docs/collaboration/manus/MANUS-PROC-M7B1-SCAFFOLD-05.md), on the user-authorized separate computer. Fetch PR165 branch `codex/m7b1-round-plan-drafts-20261103` current head into Manus's own branch, never push Root165 directly. Original source-only pin is `2188f1bff287c7f293e77cd7e711d0cc63284352` / backend tree `53b6de1d91ed4743bd82a01f2f7788043c7b4d1a`. Root run03 generated `20261103163108_M7b1CommercialRoundDraftPlans` and passed all 1970 Procurement tests; Host compilation failed before its tests. The reviewed genuine migration/snapshot and callback name fix are published on PR165 for independent verification; subsequent Root publication28 passed 411 Host checks and bounded native acceptance. Verify that existing migration; do not generate a duplicate. Browser and release gates remain open. See [portable provenance](docs/procurement/gap-closure/M7B1-GENUINE-MIGRATION-PROVENANCE-20261103.md). If the genuine migration is published before pickup, verify that existing migration/tests rather than adding a second; if still source-only 2188, independent genuine scaffolding is allowed. Reproduce SDK 10.0.400/EF 10.0.11, preserve raw/final provenance, run 1970 Procurement/377 exact focused Host cases with analyzers and return no-pending-model/SQL proof. No worker acknowledgment is inferred; only **one** genuine migration will be selected/reviewed to merge, never automatic parallel integration. Root retains SQL/native/finite-role/activation; no production/private fixtures/secrets/UI/held-branch changes assigned. Scanner153 remains independent.

Earlier dated checkpoints below are retained; this entry supersedes their current release status.

---

# B1a/B1b commercial-round draft implementation — genuine-generation checkpoint, 3 November 2026

Issue [162](https://github.com/georgemunganga/erp/issues/162) starts from **published ERP main `3a8436c5cf9639bcb0e5a99018f4386d8c03dbf6`**, with published UI main `5dd51dff2ff74087d3f72682025ded071edd4782` unchanged. Implementation ownership: Root coordinates release/SDK/native acceptance; m6d owns isolated source branch `codex/m7b1-round-plan-drafts-20261103`; R8 provides independent source review. The branch now includes published main `7b3be5414f74bb754e3721c59a1861efffd9061f`; collaborators must fetch the current PR165 branch head, retain its single genuine migration and avoid generating a second one.

The draft source adds typed incomplete-plan enrollment/revisions/criteria/team/actor-command receipts, scoped Core-principal draft/read/recovery routes, current EF mappings and guarded staging, default-off READ/RECORD options, concrete finite-schema admission, same-release M4 publication/source-cancel barriers, and proposed database trigger/retention SQL helpers. Submit/review/publication are intentionally unavailable. No held migration/snapshot was merged; no master, employee prerequisite or business-policy approval was invented. See the existing [28-section specification](docs/procurement/features/PROC-06-M7B1-COMMERCIAL-ROUND-PLAN.md#b1ab1b-implementation-checkpoint--3-november-2026-source-only).

**Evidence and release holds:** source/diff review only; 43 new Procurement cases have passed within actual 1970/0/0 suite after genuine EF generation. The 16 new Host cases are pending because Host compilation caught a callback parameter/discard name collision; its four-line fix is included but untested. No database/grant change, API, UI, production deployment or native/UAT acceptance occurred for B1. Genuine current-model migration/snapshot bytes are retained with [portable provenance](docs/procurement/gap-closure/M7B1-GENUINE-MIGRATION-PROVENANCE-20261103.md); exact captured migration/trigger/function/index/owner fingerprint, finite-role rollout and disabled-trigger/partial-install/rollback negatives remain required before enrollment can activate. Capture constants are unset and both feature options default off. SECURITY INVOKER barrier grants expose only scope/key/state/revision/operation columns to M4; preserve those grants on binary rollback. An old binary may fail exact schema readiness and must leave affected M4 writes disabled until a compatible forward release. Do not strip triggers to restore a stale fingerprint. Root retains unrelated payment/scanner work. Historical checkpoints below remain evidence, not current release claims.


---

# M7B1 adopted engineering baseline — 3 November 2026

Documentation-only draft against published ERP main `b42aa7355d196b5c3c678721890b6f1479851b63` and UI main `5dd51dff2ff74087d3f72682025ded071edd4782`: [PROC-06-M7B1 commercial round plan](docs/procurement/features/PROC-06-M7B1-COMMERCIAL-ROUND-PLAN.md), linked to [issue145](https://github.com/georgemunganga/erp/issues/145). Root and independent m6d review have adopted this 28-section engineering baseline for implementation under the user’s existing full-module authorization. Production business-policy settings remain unapproved; no implementation, runtime acceptance or activation is claimed. It uses the actual current principal capture2 / quote-evidence schema2 / commercial snapshot3 and existing `SupplierBid.Version` pin; no separate BidVersion entity or held branch merge is introduced.

The proposed first slice covers internal plan/enrollment/review/publication only for never-published, untouched principal RFQ Drafts. Existing published M7A quotations cannot be retrospectively sealed or enrolled. The existing RFQ publication paths must retain a barrier for enrolled events until formal submission and every confidentiality surface are accepted. All policy weights/thresholds, required evidence, disclosure and restart decisions remain explicit adoption inputs; no approved defaults or migration/route/UI implementation is claimed. See sections10/11/25/28 for the confidentiality matrix, publication guards, acceptance and blocking decisions.

Root retains payment issue158/PR159 SDK/native/write-policy acceptance and deployment ownership; independent scanner task153 is unchanged. After this reviewed PR merges, start the next B1 build from published main. The round handler must use round-specific current action/company/branch access that preserves the original RFQ/source maker; do not reuse or globally relax M4 PrincipalOwned’s maker==actor checks. Shared lock order is sorted PR requests → event → enrollment/plan → Core rows. The first enrollment write, including draft CRUD, must ship the existing RFQ publication barrier in the same release, never deferred from persistence to later HTTP work. Revision children have distinct physical row IDs and stable logical IDs, unique per plan/revision/logical ID. Every command pins its exact immutable result revision; publication references the unchanged submitted snapshot/hash. Explicitly Cancel the plan first, then use only the unchanged authorized M4 source-cancel path when no active review/Published plan remains; retain enrollment/history and let only M4 release the PR holds. Do not relabel events/sources or globally block legitimate other RFQ creation. Engineering may implement configuration/schema/adapters/draft CRUD under the existing authorization with these adopted constraints. Unresolved production business policy blocks Submit/Publish, not design or draft CRUD implementation. Do not merge held `98609167`/P32/P36/P38, touch active runtime/DB/UI, or treat this documentation branch as a production release. Historical checkpoints below remain retained.


## Next independent Manus milestone — MANUS-PLAN-06

**Repository:** `georgemunganga/erp`; track [issue169](https://github.com/georgemunganga/erp/issues/169) and [draft PR171](https://github.com/georgemunganga/erp/pull/171). This is a backend implementation/verification milestone. Procurement already has the UI; do not reimport or replace it. Root retains production, shared private databases, runtime resources, release and final integration ownership.

**Published prerequisites:** ERP main `dc6a082db74f9740104e2295988bc6f1f54027d8` and UI main `9b5d02dcf971900289793f56e91895dd3747fd80`. The work input is the published Plan feature branch `codex/procurement-plan-approval-20261103`, source `a7bc1371d06edb7b39d9b29415e0da81bfc847bb`, on top of that ERP main. Plan UI PR103 source `1cb1bf4d3cc357893387c5ed5066dca0d9193788` retains the existing screens; runtime tree `78b0b728` passed TypeScript and173 focused tests. A frontend checkout is unnecessary for the initial backend task; use the ERP feature/API documentation to preserve its contract.

**Claim and coordination:** comment on issue169 before generating a migration, with your branch and input SHA. Re-fetch issue169/PR171 immediately before scaffolding. If Root or another owner has already published a genuine Plan migration, verify that migration instead of generating another. Source review of the latest policy-pinning change is being recorded under issue169; do independent design/code verification while that review completes. Do not push directly to Root's branch, reuse server-private evidence or invent successful checks.

**Required work:**

1. Read AGENTS.md, GW-ENG-STD-001, the adopted Plan feature specification and `docs/procurement/gap-closure/PROC-05A-POST-B1-SOURCE-READINESS-20261103.md`. Preserve current Core identities, employee-optional access, company/branch scope, exact decimal values, immutable policy/line-version pins, shared Core approval engine and original-key receipts.
2. Compile the actual source using its pinned SDK/packages. Fix concrete compilation, authorization, state-machine and contract failures on your branch, with focused meaningful tests. Do not upgrade dependencies or replace shared services merely to make the build pass.
3. From the actual post-B1 model, generate the genuine EF migration/designer/snapshot for the two Plan review tables, nullable Plan branch/index changes, and the changed existing `ck_operational_policy_identity` CHECK adding `PROCUREMENT_PLAN_REVIEW`. Preserve all existing B1 migration bytes. Do not handwrite a snapshot, copy a held migration or create duplicate schema history. Record source, SDK/EF versions, command outputs and exact artifact hashes.
4. Run no-pending-model checks and actual generated Up/guarded Down SQL checks. Apply them only to an isolated non-production PostgreSQL fixture. Capture actual schema constraints, ownership, indexes/triggers and the canonical readiness fingerprint before filling the currently empty Plan readiness manifest. Update shared `ProcurementOperationalPolicyIdentitySchemaReadiness` only from that actual new CHECK capture; keeping its old owner-baseline capture would deny every affected policy workflow. Prove retained records/audit cannot be destroyed by downgrade; no trigger disabling or catalog edits.
5. Test current permission/branch/tenant denial, workerless Core accounts, All-scope read/no-write, source line-version consistency, maker/checker and current authority, sequential review/final decision, return/rejection/withdrawal, delegated/expired authority, pinned policy after ordinary replacement, original-key exact response recovery and retries/concurrency. A new Open/Submit still needs an eligible current policy; revoked/retired/unknown policies do not gain authority. Use current services, not test actor headers or fake approved outcomes.
6. Run the relevant complete Procurement and Host cohorts on your machine and identify stable baseline/new cases separately. Where feasible, complete normal-cookie API/native persistence acceptance using synthetic users on your own isolated fixture; preserve failures, owned audit/outbox effects and cleanup. Do not use or request production credentials or customer records.
7. Push a focused draft PR and progress entry on issue169 with exact source, migrations, tests run versus unrun, failure/recovery evidence and remaining limitations. Include operating/rollback and permission/schema contract updates. Do not mark whole-module UAT or production release complete.

**Boundary:** files, SLA delivery and reminder policies currently unsupported by this Plan policy schema remain explicitly documented; do not fabricate a scanner, attachments, notifications or Finance integration. Improve them only through an adopted shared-service contract. Root will review and integrate your PR with the existing UI, run paired browser acceptance, and deploy separately. Unclaimed work may advance under Root; always re-read current ownership and published migration state before adding schema.

---

# Current integration ownership — 3 November 2026

Published prerequisites: ERP main `9d10180cd09f8dcfd2be1ad4344219a70e54993a` and Procurement UI main `5dd51dff2ff74087d3f72682025ded071edd4782`, or reviewed current main descendants. ERP PR152 and UI PR99/100 are merged and their code/static releases are deployed. Fetch both main branches before starting; quotation write switches remain off. See [the current production record](docs/procurement/gap-closure/M7A-PRODUCTION-WORKFLOW-RELEASE-20261103.md).

**M7A issue148 is now owned by Root through ERP PR152 and UI PR99; do not start overlapping quotation UI edits.** The complete original-screen draft workflow has private API/UI/native-owner evidence, including current Core identity, exact terms, scoped reads, revision/cancellation and original-key recovery. Retained B/C Company All browser reads, test cleanup and paired code rollout passed. See [the current acceptance record](docs/procurement/gap-closure/M7A-NATIVE-DRAFT-WORKFLOW-ACCEPTANCE-20261103.md). Updated main baselines and the next collaborator milestone will be published after prerequisite merges; the independent Core scanner task remains available on separate non-production hardware as documented below.

Root retains positive database-backed quotation workflow verification, schema/finite-role operations, full UAT closeout and paired production deployment. New quotation record/capture switches default off independently from RFQ publishing. Existing independent document-scanner ownership is unchanged; quotation nonempty document transport is not available yet.

Evidence: typed persistence passed 1,927 Procurement tests and 167 focused Host cases; the final API integration passed 218 focused Host cases. Genuine isolated migration/rollback guards, exact ACL changes and real enlisted READ/RECORD admission passed; dedicated restore instances stopped with original data retained. The preserved RFQ UI passed ten adapter cases, TypeScript checking and 22 transport-mocked Playwright cases. These are bounded results, not complete quotation CRUD or whole-module UAT acceptance. See [the owning contract](docs/procurement/gap-closure/M7A-OWNING-HTTP-CONTRACT-DRAFT-20261030.md) and [whole-module gaps](docs/procurement/gap-closure/PROCUREMENT-UAT-AND-GAPS-20261027.md).

The fresh-backup paired API/schema/UI rollout is complete; see [the production release record](docs/procurement/gap-closure/M7-PRODUCTION-TECHNICAL-RELEASE-20261103.md). Signed-in production checks and the remaining bid/evaluation/award/returns/contracts/payment/portal acceptance remain open under [issue 145](https://github.com/georgemunganga/erp/issues/145). GitHub Actions is account-billing blocked; local/private test evidence is separate. Historical entries below are retained and superseded for current ownership by this section.

**Payment retained READ technical release — issue155 / ERP PR156+157 / UI PR101:** backend `b61ddf73` and preserved UI `ed044333` are deployed. Source passed 253 focused Host cases; UI passed 172 unit and 31 mocked Playwright cases plus actual normal-cookie history/client-key recovery. Three specifically reviewed Core column SELECT grants were necessary; the actual production read-only provider then passed. Public route/auth, five primary plus 36 compatibility assets, all 150 live API file hashes, and seven anonymous browser renders passed. Native session cleanup preserved all other sessions/business/source records. See [the current UAT release record](docs/procurement/gap-closure/PROCUREMENT-UAT-AND-GAPS-20261027.md#payment-retained-read-technical-release--3-november-2026). Processing stays off. This is not complete payment write or whole-module acceptance.

**Next coordinator milestone:** [issue158](https://github.com/georgemunganga/erp/issues/158), existing manual-payment write/policy/UI acceptance. Draft PR159 narrows full-governance Core reads without weakening write controls; it is unverified source, not an adopted main prerequisite. Root owns its SDK/finite-role/private multi-actor workflow and later activation decision. M7B formal rounds need their own adopted child specification and confidentiality/document controls. No competing workflow/UI or default business grants/thresholds are authorized.

**Independent Manus pickup:** [issue153](https://github.com/georgemunganga/erp/issues/153), [Core scanner C04](docs/collaboration/manus/MANUS-CORE-DOCUMENT-SCANNER-04.md), on separate adequate non-production hardware. Claim with its focused draft PR and progress file. No frontend checkout is needed. Root retains payment READ/schema/API/UI integration and document HTTP/workflow activation; do not overlap those files.

---

# Current integration ownership — 30 October 2026

Published code prerequisites: ERP main `a3381969581c613e2e583036d3d3e8bf176440f2` and Procurement UI main `06347d3029361533962b972556ab1d1952d68602` (`georgemunganga/module-connect`). These contain the complete merged M6 source cohort: ERP PRs #135–#139 and UI PRs #93–#96. Start from these published main commits or their descendants. Before starting collaborator work, fetch both current main branches and verify they include the complete cohort; do not start from an old deployment SHA or a held experiment. Tested backend `f1e5102e` and UI `9c56b492` identify the retained candidate publications. A source merge does not activate a production workflow.

The existing correction form completed a real save and original-key recovery after a deliberately lost response. Its later harness-origin failure remains retained. A separate flags-off original-UI continuation passed both invoices’ Overview/History and saved-result recovery: seven direct reads, six screenshots, no page errors/business writes, and logout. Independent native reconciliation preserved every business record and admitted only its exact authentication cohort. The single temporary vendor permission and 62 finite SQL grants are restored with current data/history retained; only two documented PostgreSQL owner-default ACL storage representations differ from their initial NULL representation. See the [M6E record](docs/procurement/gap-closure/M6E-CONTROLLED-INVOICE-REPLACEMENT-20261030.md).

Root owns fresh production backup/restore, the Core-first plus seven-Procurement migration rehearsal, paired rollout and signed-in verification. Production deployment and [whole-module UAT](docs/procurement/gap-closure/PROCUREMENT-UAT-AND-GAPS-20261027.md) remain open. M7 quotation validation/evidence contracts are isolated source groundwork, not deployed quotation CRUD; complete supplier-offer fields, schema, API and original-UI integration remain required. No new Manus assignment is issued here. Existing independent document-scanner ownership is unchanged. The dated sections below retain historical evidence and are superseded for current ownership by this section.

---

# M5 technical release and current prerequisites — 28 October 2026

Published ERP main: `1c6a81d772c654e7a58254845d116e2cc3b0d7bd` (through PR133).
Published Procurement UI main: `47cac6a13a24e9db113383f8a9cf5b319494d526`
(PR92, `georgemunganga/module-connect`). Use current main descendants.
These include M5b receiving and M5c company-local service-date source fixes.

M6A invoice-history Open and independent review passed a bounded private
real-cookie/native owner checkpoint on backend source `6287bd22`, now published through PR133:
[scope and exact evidence](docs/procurement/gap-closure/M6A-INVOICE-HISTORY-PRIVATE-ACCEPTANCE-20261028.md).
Published ERP main is `1c6a81d7`; PR #133 is merged, and this checkpoint
does not activate M6 or prove qualified matching, Finance posting or whole UAT.

M6B principal invoice mapping remains under Root integration in ERP draft PR135
and UI draft PR93; these are not published prerequisites for new Manus work.
The genuine receiving and dual-issued-parent migrations passed private Up,
guarded Down/Up and retention veto checks. Source `7f44d5f3` passed 1,559
Procurement and 304 focused Host tests. A real-cookie private journey created
an invoice, saved source3 links to a genuine principal receipt, recovered the
original command, denied another actor's key and Company All writes, and
required independent original-unit review before comparison. Native persistence
reconciliation passed. The original UI then passed a second mapping, lost-response
original-key recovery and review-required denial; the wording successor and
flag-off history reads also passed real Playwright. Exact temporary roles/grants
were restored and both private runtime stacks stopped, preserving the legitimate
test records. See [M6B bounded acceptance and receipt pins](docs/procurement/gap-closure/M6B-PRINCIPAL-INVOICE-LINKS-PRIVATE-ACCEPTANCE-20261028.md).
No M6B production activation, qualified matching, Finance posting or full-module
UAT is claimed. This is the bounded M6B record; later M6C manual-review and
Blocked-comparison acceptance is recorded in
[M6C's private checkpoint](docs/procurement/gap-closure/M6C-PRIVATE-WORKFLOW-CHECKPOINT-20261029.md),
and the separate M6D qualified comparison and feature-off closeout is recorded in
[M6D's checkpoint](docs/procurement/gap-closure/M6D-PRIVATE-CURRENCY-CAPTURE-CHECKPOINT-20261030.md).
Those draft integrations are not published Manus prerequisites. Preserve real
principal parent constraints; never insert surrogate legacy versions or fake
document evidence.
GitHub Actions currently cannot start because its account has a billing lock;
local pinned SDK/native evidence must remain explicit, never reported as CI.

M5d accepted-Goods return-request CRUD/history now has private normal-cookie
API and independently replayed native owner acceptance. It preserves the
original receiving UI and ERP components. Writes-off history/recovery passed;
all cohort sessions are revoked, temporary role rows restored and private
API/proxy services stopped. See [M5d scope and evidence](docs/procurement/gap-closure/M5D-GOODS-RETURN-PROPOSALS-20261028.md).
Do not duplicate M5b/M5c/M5d or resume held P37 migrations.

Production M5 technical rollout and observed restore-readiness fix are complete:
product `6087f211`, UI `47cac6a1`,
220 tables, independently preserved original203 rows/sequences, finite grants,
read-only canary, public route/asset checks and three Playwright renders passed.
Existing business flags are preserved; new return writes remain off. See the
[release record](docs/procurement/gap-closure/M5-PRODUCTION-TECHNICAL-RELEASE-20261028.md).
The [API-only recovery fix](docs/procurement/gap-closure/M5-RESTORE-READINESS-PRODUCTION-20261028.md)
preserves schema, settings, permissions, publisher and UI. Authenticated
production CRUD is not claimed.
Whole-module UAT is open: actual return approval/dispatch/reversal, qualified
invoice source/consumption ownership, and broader scope/concurrency tests remain.
The original Service UI completed one bounded private real-cookie journey with
owner reconciliation; see the
[Service UI acceptance](docs/procurement/gap-closure/M5-SERVICE-ORIGINAL-UI-PRIVATE-ACCEPTANCE-20261028.md).
The earlier private Service API journey has scoped owner and Core UTC-date
acceptance; see the
[Service checkpoint](docs/procurement/gap-closure/M5-SERVICE-RESTORE-READINESS-PRIVATE-CHECKPOINT-20261028.md).
Neither establishes production Service activation or non-UTC/multibranch UAT.
Return requests alone change no accepted,
Inventory, credit or Finance balance. Core retains shared identity/roles/scope,
organisation/calendar/branding ownership.

Root owns release follow-up. Active agent work covers the Service restore-readiness
release review and M6 principal invoice matching specification/implementation. No
new Manus implementation assignment is made by these agent scopes. Existing
independent C04 ownership is unchanged.

The checkpoints below are historical. Their invitations and baseline claims are
superseded by this active ownership section.

---

# Published Procurement UAT checkpoint — 27 October 2026

Audited ERP backend baseline: `fcdafac34351b3e0d312b4e49ff949dc2d1c0ede`
(product `20985344252b0b394939cfceb87152d49f39e2e2`).
UI PR89 is merged: accepted UI main
`f1e237713d5f803212ec2ae72773be842ab0e3b6`, product `720b108`, final tests
`ba25a24` and documentation-only successor `f03a532`. Start from current
published main descendants; no UI rebuild or duplicate import is assigned.

The [UAT report](docs/procurement/gap-closure/PROCUREMENT-UAT-AND-GAPS-20261027.md)
and [exact checkpoint](docs/procurement/gap-closure/PROCUREMENT-UAT-CHECKPOINT-20261027.json)
record 587 UI tests and a build, 444 mocked browser scenarios (full443/444 then20/20
affected-file rerun), two fixed navigation races, and real-cookie manual-payment
API/native-owner acceptance. Four Core users without employees completed the
payment flow; independent confirmation is not bank/Finance execution. Whole
Procurement UAT is NOT signed off; authenticated production CRUD was not tested
in this audit. Hosted CI is not represented as a pass.

Root owns M5a release readiness and invoice/payment follow-up. Manus may claim
[M5b receiving/inspection/returns](docs/collaboration/manus/MANUS-M5B-PRINCIPAL-RECEIVING-08.md)
from these merged prerequisites/current main. Connect Core users and M5 PO
issued evidence through the original receiving UI. Preserve permission/scope,
independent approvals, quantity conservation, reversal history and module
ownership. C04 scanner work remains independent. Do not resume old held branches
or infer supplier delivery, Finance posting or inventory ledger acceptance.

Production remains M4 with existing flags; this audit changed no production
role, policy, schema, flag, container or UI alias. Current-row migration preflight,
fresh recoverable backup/restore, finite Record+Execution grants and paired
rollout/smoke checks remain required for M5a. Test-profile grants and the synthetic
ZMW100 review policy are not production configuration.

---

# Published M5a prerequisites and receiving handoff — 27 October 2026

This checkpoint supersedes older assignment/baseline paragraphs below.
ERP M5a PR119 is merged in accepted main
`58dedebf9761d20c1dd0490b1b1dd6532c9d8454`; UI PR87 is merged in accepted main
`c33b435efe041bb97c7b831be93cd9d8aca05d28`. Start assigned work from these
published prerequisites or current main descendants. Application trees match
tested products `20985344` and `79a19df` respectively.

[Bounded M5a evidence](docs/procurement/gap-closure/M5A-PRIVATE-UI-VERIFICATION-20261027.json)
records 94 latest focused Host checks, 51 focused UI checks/build, ten genuine UI
form actions with reload/GET recovery, ten scoped/stale denials, five actual All
queues with writes off, and overlapping Submit/Issue with one Core case/issued
version. Native checks reconciled original rows, schema and normal logout.
This is source acceptance, not production deployment or whole Procurement UAT.
Hosted CI did not start because GitHub reports an account billing lock; local
results are recorded separately, never presented as a hosted pass.

Root owns M5a production preflight/release and M6 invoice/payment contract auditing.
Manus may claim the full
[M5b receiving/inspection/returns milestone 08](docs/collaboration/manus/MANUS-M5B-PRINCIPAL-RECEIVING-08.md)
from these published prerequisites. Preserve the imported UI and shared services;
Core users do not require employee links. Publish ERP coordination and linked UI
PRs. The independent C04 scanner scope remains; record its state/handoff if active
before claiming another implementation stream. Older queued P35/vendor tasks
below are historical instructions, not permission to resume stale branches.

Production remains the healthy M4 technical release with existing flags. M5a
release needs current-row migration preflight, fresh recoverable backup/restore,
finite grants and paired rollout/smoke checks. Do not enable production workflows,
claim external Finance/supplier delivery or merge held branch ancestry.

---

## Retained history

# Procurement collaboration checkpoint — 27 October 2026

Verified published ERP main: `9f2591cc3bd5b322e73cd44ea080795370b7352c`.
Verified published UI main: `46782ccdb502d8be34e5abb7e25ff659cd325582`.
The ERP main update after the M4 deployment is documentation only. These are
the shared starting points; the following draft branches are not merged release
baselines and must not be deployed as accepted milestones.

Root owns M5a backend and private verification in
[ERP draft PR #119](https://github.com/georgemunganga/erp/pull/119).
The existing UI integration is in
[UI draft PR #87](https://github.com/georgemunganga/module-connect/pull/87).
Preserve the imported Procurement layout and shared ERP components. Do not
rewrite the PO screens or introduce another master, ledger or approval engine.

Current verified backend product revision is
`20985344252b0b394939cfceb87152d49f39e2e2`; UI build revision is
`79a19df28b1d3e95d4ab8798c7f27cd1ff53f9d0`. Both are published draft branch
commits, not merged release baselines. The latest backend passed 94 focused checks,
no pending model changes and the genuine API publication. Earlier private
same-row PO and four release workflows are reconciled. The UI passed type
checking/build and 51 focused checks; the fresh uninterrupted ten-command
five-role form journey passed on backend product `2bebdf70` and that UI build.
Latest backend `20985344` additionally passed five All-queue UI reads with writes
off and original-key recovery, plus overlapping Submit/Issue requests with one
Core case and one issued version. Earlier ten denial checks passed on `2bebdf70`.
All results have separate native owner reconciliation. Production release and
broader Procurement UAT remain open.
Exact source-specific evidence, preserved failures and release limits are in
[the M5a implementation record](docs/procurement/M5A-PRINCIPAL-ORDER-GOVERNANCE-IMPLEMENTATION.md).

Do not overlap Root's active M5a files, change test credentials/grants, or enable
production operational flags. The existing independent Manus scanner assignment remains in place. No new collaborator milestone is assigned from
these drafts. Publish reviewed prerequisite merges and update this checkpoint
before assigning work that depends on them.

The bounded form-driven, return/reject/release, stale-command, scoped-queue and
overlapping-command results are recorded above. Release still needs current-row
canonical migration preflight, a fresh recoverable production backup/restore,
finite grants, paired rollout and post-deploy smoke checks. Broader security
matrices and whole Procurement business UAT are not declared complete.
Receiving/returns, amendments and later principal-identity procure-to-pay work
remain separate milestones. Finance owns its eventual accounting/payment
adapter; the private manual evidence here does not implement that adapter.

---

## Earlier retained checkpoints

# Current deployed and published baseline — M4 technical release

Use ERP main `63360f128911b0cd66eb4e44e0f79fabcd1a2345` and UI main
`46782ccdb502d8be34e5abb7e25ff659cd325582` or descendants for new assigned work.
The matched release is deployed: tested API product `cca4772473b2a87411b3ca59a0a766fbd84187de`,
UI `b51216eaf4af07c3985faef9e8b51145c4bbf92f`, image `b6dc4e822fd7383e13c186e2bac4d0974dde4fb6922b53e20bf1e5be525f6fff`,
schema203/histories56/29/1. [Deployment evidence](docs/procurement/release/M4-PRODUCTION-DEPLOYMENT-20261026.json)
records actual fresh backup/restore, atomic migration/finite grants, readonly
canary, live review and anonymous public Playwright. Core/HRM3300 and Accounting
are preserved. Current flags remain unchanged; no operational M4 activation or
whole Procurement UAT completion is claimed.

Coordinator owns M5a PR119, receiving, sourcing follow-ons and remaining UAT;
its eight-table migration and Down safety are under review, not deployed.
Do not duplicate these or merge held mixed migrations/UI ancestry. Manus retains
the independent Core document-scanner assignment; no overlapping new assignment.
The imported UI and shared ERP identity, settings, controls and ownership stay
in place. Nine merged remote branches were retired across the cleanup passes; their
commits/local worktrees and all unique/advanced work remain. Historical baseline
paragraphs below are superseded by this section.

---

# Latest UI and UAT checkpoint

ERP prerequisite main is `de449645a5cf4b46da1024c821252ac46b625103`; UI main `dfed2dd1954e8dc735a3bd8ff03449d4b00e6911` includes deployed anonymous-access recovery PR85. Current backend is product `237a936b25690f780cd09ca6ab3f90b1a05cc009` / image `c233ffd9e76f7579f52bfb03259985a7abc6691c94d05b9633773b2389f3aa0a` after the [verified expiry-guard image-only release](docs/procurement/release/CORE-ACCOUNT-LINK-EXPIRY-DEPLOYMENT-20261026.json); current UI product is `9b6ab3442aa1583a339dcfed311adad393a29706`. Fresh M3b and finite-Core current-mainline M2a private browser/owner/preservation checks passed; [current status](docs/procurement/gap-closure/CURRENT-EXECUTION-STATUS-20261026.md) distinguishes that from production activation.

Production imports and finite maintenance are now enabled; [activation evidence](docs/procurement/release/M2A-PRODUCTION-IMPORT-ACTIVATION-20261026.json) records healthy checks and unchanged role permissions/schema. No production import transaction was written. The tested account lacks explicit import permission and correctly receives 403; Core role assignment remains separate.

Coordinator owns M4 backend PR114 and original-layout UI PR86, their paired production release, plus [draft M5a PR119](https://github.com/georgemunganga/erp/pull/119) at published source `e57de3c786e65e60da28285483ed9de9a8a942dd`. M4 internal RFQ publication has [bounded private real-cookie acceptance](docs/procurement/gap-closure/M4-INTERNAL-RFQ-PUBLICATION-ACCEPTANCE-20261026.json), while supplier delivery, bidding, evaluation and awards remain open. M5a reuses existing PO records and shared Core approval; migration, readiness, UI and runtime acceptance remain open. Its source checkpoint is not an assignment or an accepted prerequisite. M4 remains disabled in production. Do not duplicate those or merge held mixed migrations/UI ancestry. Manus retains the independent scanner task. No new overlapping collaborator assignment, Finance integration or whole-module UAT completion is claimed.

**M4 private acceptance:** the earlier Open/lost-response recovery passed, but its Decide and one same-key diagnostic returned 409 without committed effects. The repeated-save shortlist invariant was corrected in `cca4772473b2a87411b3ca59a0a766fbd84187de`. A fresh real-cookie PR/Finance/Core-approved RFQ then passed one Open with original-key recovery and one independent internal Approve; owner checks linked the Core decision, two publication receipts, one unsent manual invitation, audit and outbox. The expired old Pending case was separately returned through the real UI with its original Open receipt and Finance snapshot unchanged. The positive graph stayed exact after that Return, 197 outside-fixture tables and all histories matched, and all synthetic sessions were revoked. The post-quiescence strict original comparison retained all 359 old IDs: 358 rows including five POs were exact; one old policy row had an authorized supersession, documented without concealing the strict failure. [Exact evidence and limits](docs/procurement/gap-closure/M4-INTERNAL-RFQ-PUBLICATION-ACCEPTANCE-20261026.json) exclude supplier delivery, external Finance verification and production M4 activation.

**Cleanup:** three completed compiled-only archives (299,087,290 bytes) were removed after member, active-process and mount checks. Raw source, test results, databases, backups and rollback products remain. Merged remote branches for PR117 and PR118 were deleted only after exact-tip/main-ancestry/open-PR checks; local worktrees and commits remain. [Cleanup record](docs/procurement/release/UAT-CLEANUP-CHECKPOINT-20261026.json) pins the private journals.

---

# Current deployed baseline — 26 October 2026

Published prerequisites are ERP main `c8eac4c54b009bbafe3cf916992abdbcf3562a61` and UI main `7f75d327a2bb8eb399f48b370da0cc53a0a831b9`. The paired technical release is live: product `d35ecc090b0de0abcab1e38be09fd25c52a1d0aa`, UI `e032805303e23e1658d5a66808d9b0d6f1ce991c`, image `c9ea9f7bd48c112bf20cc5cbb6f20d48850075942dfa37f366af9c41f352464b`, schema histories HRM56/Procurement28/CoreApproval1 and finite runtime accounts. [Current status and remaining gates](docs/procurement/gap-closure/CURRENT-EXECUTION-STATUS-20261026.md) supersede historical no-deployment paragraphs below.

Coordinator owns remaining UAT, gated capability activation, SMTP/expiry investigation and anonymous Procurement sign-in recovery. Manus retains the independent Core scanner assignment; no overlapping implementation is assigned. Keep original layouts/shared ERP controls and Core-owned identity/settings. At the initial technical cutover these capabilities were off; imports and finite maintenance are now enabled as recorded above. Operational approvals, manual payments and principal direct-order execution remain off until their affected release acceptance. No business-UAT sign-off or Finance integration is claimed.

---

**M3a source accepted — 26 October 2026:** Start assigned backend work from published ERP main `35045b7e51efca2e756dd678b9de6a62477b03bc` (PR108) or a descendant; UI main remains `704ac9c642d00e3672cd81e7c1df2d9cb50be160`. Fresh 14 focused / 1,295 Host checks, three real-cookie browser phases, independent owner/ACL/original-row verification and session cleanup passed. [Bounded acceptance and retained failures](docs/procurement/gap-closure/M3A-REQUEST-APPROVAL-ACCEPTANCE-20261026.json) distinguish API actions from actual UI, trust/owner-backed private tests from production, and manual Finance references from accounting. Coordinator continues principal approved-line/PO conversion and remaining M0/release work; Manus retains scanner task04. No production deployment or activation occurred; do not duplicate coordinator work or merge held UI ancestry.

**Publisher source accepted — 26 October 2026:** ERP main `27be07e0a97d61ea8fe57d4a15c3de527cbcfd43` includes merged PR106; UI main remains `704ac9c642d00e3672cd81e7c1df2d9cb50be160`. Start new assigned backend work from this main or a descendant. Fresh 28 focused / 1,293 Host tests and finite-role private two-tenant Store + genuine publisher acceptance passed: four rows, eight correctly tenant-attributed audits, unchanged original records/queue, clean stop. [Exact evidence and limits](docs/procurement/gap-closure/M0-CROSS-TENANT-PUBLISHER-VERIFICATION-20261026.json) distinguish acknowledgement from external delivery and production deployment. Coordinator continues M3a request/approval acceptance and remaining M0/release gates; Manus retains independent scanner task04. Production is unchanged; do not enable held workflows or duplicate coordinator work.

**Published UI baseline — 26 October 2026:** UI PR83 is merged in `georgemunganga/module-connect` main `704ac9c642d00e3672cd81e7c1df2d9cb50be160`. The narrow import/export/Core-scope port preserves the existing UI and excludes 139 held files. Clean committed validation passed TypeScript, 99 units, a fresh live build and 23 mocked browser tests; [evidence and remaining real-API gate](docs/procurement/gap-closure/M2A-UI-MAINLINE-VERIFICATION-20261026.json) are explicit. ERP main `788c944a0acb4eada5246bdd7a5793e78d21689d` includes M2a source acceptance. Start new assigned work from these published mains or descendants. Coordinator now owns the shared nonempty publisher tenant/audit fix and remaining integration/release work; Manus Core scanner task04 remains independent. Production remains unchanged. Earlier UI main references are historical.

**M2a source accepted — 26 October 2026:** ERP PR100 is merged in main `ff255065ab3240d7949315aafd62395db3d03838`; exact product source `88c561554b60f313881248e5cd581f6856e982d8` passed fresh builds, 25 focused / 1,291 Host tests, 12 real-cookie API cases, four real browser phases and independent owner/finite-role/lifecycle review. All 18 sessions are revoked and original data is preserved. Imports remain default off; production is unchanged. See [acceptance evidence](docs/procurement/gap-closure/M2A-NATIVE-IMPORT-ACCEPTANCE-20261026.json) and [remaining milestones](docs/procurement/gap-closure/CURRENT-EXECUTION-STATUS-20261026.md). Coordinator owns the separate narrow UI mainline port and release gates. Manus retains Core scanner task04; do not duplicate imports, merge held UI ancestry, or activate held workflows. Start new assigned backend work from this published main or a descendant. Earlier pending import/build paragraphs below are historical.

**Native import checkpoint — 26 October 2026:** Vendor preview/save/byte-identical replay passed real private Core-cookie HTTP and owner reconciliation. Item preview remains blocked: the diagnostic cohort located HTTP 503 at the database-boundary check while the catalog connection is open. Diagnostic source `cddd1525342a3ad840e84e568afb1ba96657df3b` passed 22 focused / 1,288 Host tests and genuine API/Tool builds. The unchanged Procurement/Approval results are carried from the earlier build, not rerun. No real import browser journey or production activation is accepted. The coordinator owns the fix; Manus should retain the independent scanner assignment. See the [current execution checkpoint](docs/procurement/gap-closure/CURRENT-EXECUTION-STATUS-20261026.md) for exact scope and remaining gates. Historical pending-build statements below are superseded by this checkpoint.

**Private schema update — 26 October 2026:** [M2a migration rehearsal](docs/procurement/gap-closure/M2A-PRIVATE-MIGRATION-VERIFICATION-20261026.json) passed genuine Up/empty Down/Up and 15 rollback vetoes with independently verified original-data/history/access preservation. This was on the isolated restore only. Runtime roles, real imports and production activation remain pending; coordinator ownership is unchanged.

# Current execution checkpoint — 26 October 2026

**This section supersedes historical status and baseline instructions below.** See [current baselines, exact test evidence, owners and remaining milestones](docs/procurement/gap-closure/CURRENT-EXECUTION-STATUS-20261026.md). ERP code baseline is published main `8eb2e305e66dba2f38763a2b029957ddec108b69`; UI main remains `403bfdac505d593921a5bab1ba3388896205325f`. Main PR99 CSV compilation is verified (14 focused passes), not pending.

Coordinator owns default-off M2a imports in ERP draft PR100 and UI draft PR82. Local verification passed 1,282 Host / 1,414 Procurement / 30 Approval tests, 68 focused tests, and six mocked UI browser cases. Private migration acceptance is recorded above; finite-role runtime and real-cookie browser acceptance remain pending. Production is unchanged. Manus retains Core scanner task04 and should not duplicate M2a or activate held workflows. Start new assigned work from published main and record exact baselines; do not merge held branch ancestry wholesale.

---

# Manus ERP collaboration handoff

**Core03 source verification completed (24 October):** The coordinator repaired and verified the shared frontend in ERP PR #74. TypeScript, the production client/SSR build, eleven role cases and seven isolated mocked browser cases passed. See [acceptance evidence](docs/collaboration/manus/CORE03-ACCEPTANCE-20261024.json). Manus should continue its other assigned work; Core03 is no longer available for pickup. No deployment or real API acceptance is claimed.

**Queued independent UI verification (24 October):** [MANUS-P35-UI-VERIFICATION-07](docs/collaboration/manus/MANUS-P35-UI-VERIFICATION-07.md) checks the existing Procurement UI draft [PR #71](https://github.com/georgemunganga/module-connect/pull/71) at exact commit `ae3694740288c5442d8f60863a68fa598f322f3f`. It covers the P30/P31/P33/P34/P35 frontend stack, including restored layouts, exact amounts, scoped access and unknown-write recovery. It is additive and queued for an available worker on separate hardware; it does not interrupt Core build, scanner, P33 UI or P35 backend tasks. The UI and P35 backend are held source, with capabilities default off until separately accepted runtime/schema gates. No mocked browser result from Task 07 can establish real API or production acceptance.

**Ready independent hardware task (24 October):** [MANUS-CORE-DOCUMENT-SCANNER-04](docs/collaboration/manus/MANUS-CORE-DOCUMENT-SCANNER-04.md) is assigned for Manus pickup on adequate non-production hardware, in a separate branch/checkout from `MANUS-CORE-BUILD-03`. It verifies the existing Core scanner and permits focused protocol fixes supported by real results. It does not replace the frontend assignment, require held Procurement branches, activate uploads or authorize production changes. Claim it in a focused draft PR and its own progress file; the coordinator reviews and releases.

**Accepted source checkpoint (23 October):** ERP PR #48 and UI PR #64 are merged on top of the payment, Core role and principal-approval milestones. Core users without employee links passed the RFQ draft create/read/replay/cancel workflow:65 actual private API requests,11 PostgreSQL constraint cases, a normal local-cookie Playwright create/reload/cancel/reload journey, independent retained-owner verification and both genuine downgrade vetoes. Failed attempts and the reviewed verifier capture compatibility are preserved in [P29 acceptance](docs/procurement/release/P29-ACCEPTANCE-20261023.json). This is source acceptance, not production deployment or whole-module completion. Production remains P24. P30 PO governance, RFQ publication/quotation drafts and receiving are coordinator-owned, still disabled pending combined schema/runtime/browser verification; they are not a new Manus assignment.

**Updated:** 24 October 2026 (UTC)
**Primary project:** https://github.com/georgemunganga/erp
**Milestone:** `MANUS-CORE-BUILD-03` — repair the shared ERP frontend TypeScript/build gate; vendor milestone accepted
**Owner:** Coordinator has claimed Core03 on 24 October 2026; Manus should not duplicate it. Separate scanner work remains independent.
**Status:** [MANUS-CORE-BUILD-03](docs/collaboration/manus/MANUS-CORE-BUILD-03.md) is claimed from published ERP main `ef67a5b3ef6712cdfdb917d43c6142027ec43722`; [progress](docs/collaboration/manus/CORE03-COORDINATOR-PROGRESS-20261024.md) records source and verification separately. The shared frontend TypeScript/build gate remains unverified. Vendor fallback was accepted and merged in UI PR #47. The coordinator owns settings shell and remaining Procurement CRUD controls separately; do not start overlapping work.

**Payment integration checkpoint:** ERP PR #44 and UI PR #62 are merged. Private payment and policy browser journeys and owner reconciliation passed. Production is still P24; migration, finite roles, feature configuration, deployment and UAT are separate release gates. P25 principal approval source is now merged; the coordinator is implementing the separate P30 publication/PO/receiving successors and production release prerequisites. Do not duplicate it. Existing Manus Core build assignment remains unchanged.

## Start from merged code

New assignments start from published `origin/main` in both existing repositories. The coordinator merges reviewed prerequisites before assigning a milestone. Do not start from an old task branch, a default branch missing the application, or an unpublished commit. Keep your existing work and port focused changes onto the new baseline; never reset or rewrite published history.

| Responsibility | Repository and source | Baseline |
| --- | --- | --- |
| ERP, backend contracts, schemas, standards and task coordination | `georgemunganga/erp`, `main` | Published main `298a857b0eaa88fdc3480bebcde2e1b8defece84` or a published main descendant; includes P23/P24 live acceptance and reviewed manual-payment foundations PRs #42/#43 (storage, shared invoice/credit capacity and Core company clock). Includes PR #44 Core workspace choices/governed manual payments, PR #46 Core role revocation hardening and PR #45 versioned Core-user request approval with retained-worker compatibility. Adds PR #48 verified principal RFQ draft persistence and genuine corrective migration. Private evidence is recorded; production release is pending. |
| Existing Procurement UI and API clients | `georgemunganga/module-connect`, `main`; `reference-ui/` and its existing `src/platform/` clients | Published main `403bfdac505d593921a5bab1ba3388896205325f` or a published main descendant. Includes the subsequent original-layout restorations through PRs #60/#61 and payment/Core selector integration PR #62 and exact-budget/approval integration PR #63 and verified RFQ draft adapter PR #64; this is the current public UI baseline. Preserve its shared ERP shell, branding, controls, real API adapters and recovery behavior. Restored presentation does not prove every workflow is connected. |
| Design reference | `Greenwebb/zoho-procurement-ui`, reference `6f0c305` | Existing vendor layout/editor; reuse the imported implementation already in the UI repository |

The ERP already serves this Procurement UI. Its source remains in the existing UI repository. ERP's `modules/hrm/frontend/module-connect/` is a tracked HRM snapshot, not the Procurement source or a Git submodule. Do not duplicate the UI there, re-import the reference project, or create another frontend. Coordination and backend work belong in ERP; a fork-based UI PR is acceptable when upstream UI access is read-only.

**Current access requirement supersedes older workerless read-only instructions below:** an active Core account with current module/action permissions and company/branch/record scope may use Procurement whether or not it is linked to an employee. An employee designation or link never grants Procurement authority. Do not fabricate or request an employee link for general CRUD. Preserve known linked-worker maker/checker conflict checks as veto metadata; inactive HR status must not erase those conflicts. Old operational worker-pinned contracts remain gated until their versioned Core-principal replacements are verified.

Before work, fetch `origin/main` in both repositories, record the exact starting commits and verify they contain the minimum baselines above. Branch from those main tips. Read ERP `AGENTS.md`, `docs/engineering/GW-ENG-STD-001.md`, UI `AGENTS.md` and `reference-ui/UI_COMPONENT_STANDARD.md`.

Production now includes the reviewed P20–P24 CRUD schema and Core-principal application, with [P23/P24 actual live release acceptance](docs/procurement/release/PROC-OPS-P23-P24-LIVE-ACCEPTANCE-20261022.md). Real-account vendor and recurring-purchase UI journeys passed with independent owner/audit reconciliation. P23 document/workflow activation prerequisites remain outstanding. Do not deploy source branches, apply migrations or enable workflow/Finance flags to complete this UI task.

**Current coordinator milestone:** [P24 recurring planning private acceptance](docs/procurement/release/PROC-07A-P24-PUBLISHED-ACCEPTANCE-20261022.md) is complete and merged. The coordinator completed the [combined P23/P24 production release gates](docs/procurement/release/PROC-OPS-P23-P24-RELEASE-PLAN-20261022.md) and owns the unfinished P25 principal-based request approvals. P25 is a draft, default-off source milestone, not deployed. Manus retains `MANUS-CORE-BUILD-03`; do not duplicate recurring CRUD, invoice review, settings, backend migrations or deployment. Finance/AP and payments belong to the future ERP Finance module, which is not built; no substitute ledger or fake funding/payment success is authorized.

**Branch-scope regression fixed:** preserve PR #51's verified legacy-name-to-ID hand-off before readiness or business requests. Do not silently clear a stored branch, infer another company, choose the first match, or reintroduce a read-only gate that strands the branch picker. See [the incident and real-account verification](docs/procurement/release/PROC-UI-BRANCH-HANDOFF-20261022.md). The actual signed-in account can open Vendors/My Requests and their new forms; this narrow acceptance does not certify every workflow.

**Approval work ownership:** PR #55 preserves the existing UI, handles the separate optional principal capability and fixes in-session same-command recovery. It does not enable approval execution or prove real API/PostgreSQL workflow success. PR #56 adds scoped locator-only Open/Decide recovery through explicit actor-owned GET acknowledgements after reload; no payloads/reasons/credentials or automatic new POSTs are stored. The coordinator owns remaining P25 runtime/authorization/real-identity/browser/release gates; do not duplicate this work or enable Finance/approval flags.

## Accepted coordination and remaining work

The claim/status contributions from ERP PR #9 are retained with their original author. That PR has no vendor implementation. Its historical missing-SHA observation and old branch references do not govern new work. The coordinator has confirmed that `445a5cb` is published; the new baseline table supersedes the old instructions.

The user correctly identified a layout regression: live routing used a separate simplified vendor page while the imported directory, sticky toolbar and detail tabs remained in `CatalogDetail.tsx`. The coordinator has restored the directory, compact header, split overview and URL tabs in the reviewed UI baseline. Preserve that work, the real API adapter and the explicit administrator read-only controls.

## Historical vendor milestone — accepted, not the active assignment

The following vendor scope and verification instructions are retained for traceability. The active `MANUS-CORE-BUILD-03` file defines Manus’s current owned files and acceptance; these older sections do not assign new vendor or settings work.

### Vendor owned files

- `reference-ui/src/features/LiveVendors.tsx`: focus on editor/form integration and concrete save/context safety; preserve the merged detail layout.
- Existing vendor form/presentation components in `Catalogs.tsx` or an extracted shared component, only where required to reuse the original editor.
- `src/platform/procurement-vendors-api.ts` and vendor-specific helpers/tests.
- ERP `docs/collaboration/manus/vendor-contract-verification.md` and progress log.

The new baseline includes vendor attribute update persistence: keyed CustomField/ReportingTag patches preserve omitted keys and retain explicitly cleared rows. Use this real contract; no workaround storage is needed. The new invoice P20 migration is coordinator-owned and must not be applied by Manus. Credits/catalog follow-up work is separate and not part of this baseline.

Backend supplier routes/contracts are read-only during this milestone. Report precise gaps for a separate owned backend task. Do not change migrations, identity, Core settings, global styles/navigation, shared controls, Finance, approvals, sourcing or other modules. Do not overwrite coordinator work or manufacture endpoints.

## Full milestone: vendor editor and supported workflows

Follow the ordered [V1–V6 checkpoints and CRUD/workflow register](docs/collaboration/manus/MANUS-PROC-VENDOR-01.md). Complete all checkpoints and report unsupported contracts as gaps, not implemented features.

Reuse [PROC-01](docs/procurement/PROC-01-SUPPLIER-ONBOARDING-AND-MANAGEMENT.md), [the vendor form specification](docs/procurement/PROC-UX-05-NEW-VENDOR-FORM.md), and their existing 28-section feature contracts.

1. **Preserve the supplied form.** Use the original primary-contact/company/display-name/contact header and the seven tabs: Other Details, Address, Contact Persons, Bank Details, Custom Fields, Reporting Tags and Remarks. Reuse the existing imported form with ERP controls, typography and branding. Do not replace it with a shorter new form.
2. **Map every field.** Record the source UI field, real DTO/API field, owner, validation, read/write support and persistence evidence. Use actual supplier contacts, sites, qualifications and allowed attributes. Keep Finance-owned bank/payment information separate; never store raw bank details in browser storage or invent Finance balances. Unsupported uploads, portal activation, email or other functions remain explicitly unavailable until their owning contract exists.
3. **Complete supported CRUD.** Empty/list/search/filter, create, read, edit and versioned withdrawal; supported contact/site/qualification add/edit/remove; submit/return/reject/approve/hold/reactivate only when permitted by the server. Preserve the existing directory and detail tabs. Restore the original New Transaction/More toolbar containers with permission-aware supported actions and clear unavailable states for missing contracts; never fake email, payments, cloning or deletion. No hard deletion of approved records or browser-local success fallback.
4. **Respect ownership and scope.** Currency defaults from Core company settings. Use stable company/vendor identifiers and current Core permissions/action flags, including authorized users without employee links. No fabricated employee, blanket administrator approval, supplier activation or policy default.
5. **Save safely.** An edit cannot fall back to create when its record/version has not loaded. Preserve unowned fields; do not send blank placeholders that erase data. Handle validation and optimistic-version conflicts without losing entered values.
6. **Prevent stale context.** Abort or ignore old requests after company, account or vendor changes. A delayed response or mutation must never populate or write another context. Do not reuse another record's ID/version.
7. **Handle uncertain outcomes.** Disable duplicate saves. If the server has no idempotency/recovery contract, a lost response is unconfirmed: do not automatically repeat the mutation or treat a same-name vendor as proof of persistence. Offer an explicit read-only reconciliation path and record the backend gap.
8. **Keep simple interactions.** Existing shared controls, floating selects, toggle switches, accessible tabs, centered empty states and the shared table. Import appears only when a real supported path exists. Loading, empty, validation, denied, conflict, unavailable and recovery states must work.

## Verification and deliverables

- TypeScript, focused units, unchanged-lockfile live-mode build and Playwright: one worker, no retries, preserved failed traces.
- Browser cases: original editor tabs/fields and detail layout at desktop/mobile; create/refresh/edit; edit-load failure cannot create; denied/read-only paths; validation preserves values; stale version; delayed company/account switch; duplicate-click/lost response; supported contacts/sites/qualification flows.
- Test only synthetic fixtures or an explicitly assigned disposable backend. No production credentials, production test writes, migrations or real personal/banking data in evidence.
- Distinguish mocked HTTP UI tests from actual API/PostgreSQL persistence. An HTTP200, screenshot or local-storage entry alone does not prove persistence.
- ERP contract matrix, workflow checklist, exact commands/commits/results, synthetic screenshots, API gaps and recovery behavior. Update [STATUS.md](docs/collaboration/manus/STATUS.md).
- Open a new draft ERP verification PR against `main` and link the actual UI PR/fork PR against the UI `main`. Include `MANUS-PROC-VENDOR-01` in both titles. Do not carry the old development branch's unrelated ancestry into a stable PR.
- Mark ready only after the supported milestone passes its acceptance cases. List unsupported owning-module contracts honestly; do not claim the entire Procurement module is complete.

## Merge and release ownership

Manus submits focused commits and PR evidence. The primary agent independently reviews and verifies the changes, merges reviewed prerequisites, then publishes the next task's exact main baselines. No new task is assigned against stale or unpublished code.

Do not merge, deploy, change production identities/data/permissions, apply production migrations, enable capabilities, force-push or rewrite history. Preserve existing work. ERP issues are disabled; use the handoff, milestone log and linked PRs rather than changing repository settings.

## Queued milestone after vendor acceptance

`MANUS-PROC-SETTINGS-02`: restore the imported Procurement settings index and navigation shell; maintain the original simple layout and module-specific groups. Audit each child against a real owning API. Global organisation, branding, locations, users and roles stay in Core. Do not mount browser-preview save handlers as live configuration. This is queued, not assigned concurrently with the vendor editor. The coordinator publishes fresh merged ERP/UI baseline SHAs after vendor acceptance.

## 24-hour coordination update — 22 October 2026

The new merged source includes exact catalog price edits, persisted invoice-linked credit drafts and safe editor recovery. It passed10 actual browser flows (real API/private PostgreSQL),152 API assertions/90requests,84 backend cases,42 units and111 mocked browser cases across48+63 batches on one application bundle. It is not yet production-deployed. The coordinator is actively verifying the original full seven-tab vendor layout with actual child persistence; this is fallback integration, not acceptance of an unseen Manus implementation. Receiving/approval source reconciliation and forward migration remain separate held work. Preserve all existing contributions; do not reset branches or copy a second frontend.

## Accepted vendor milestone — 22 October 2026

UI PR #47 merged normally at `5f88d54917525c6642d05e94409068f37212b5fa`; reviewed final feature head `26142f51d809efd3a5ec6364bfffa689e5b8e3c2`. Final source passed24 mocked browser cases and2 actual API/private-PostgreSQL browser cases, one worker/no retries, plus10 focused units, TypeScript and production build. Independent review found no remaining blocker in the reviewed saved-header reconciliation path. The final actual bundle was `index-BhacWz3_.js`; traces and source/asset manifests are retained privately. This is acceptance of supported vendor CRUD, not unsupported bank/portal/document contracts or production deployment.

**Collaboration reservation:** the coordinator already has a module-only settings shell, Home/employee catalog, permission/context invalidation and uncertain-save guards under verification. Do not implement those in parallel. Manus may review the fresh merged vendor form and return concrete layout/interaction defects in a focused PR or verification report, with no duplicate frontend or backend changes. Any new implementation milestone will name its owned files and a newly merged baseline.

## Current independent assignment

Manus owns [MANUS-CORE-BUILD-03](docs/collaboration/manus/MANUS-CORE-BUILD-03.md), a complete repair/verification milestone for inherited shared ERP frontend TypeScript failures. Start from the latest merged ERP main with the minimum baseline above, not the coordinator’s unmerged branches. Procurement implementation continues independently; the existing vendor and settings assignments must not be duplicated.

**Finance integration boundary:** The user confirmed AP and payments belong to the future Finance module, which is not built. Procurement may validate its commercial invoice records and prepare versioned hand-off contracts. No fake posting/payment success or substitute ledger is authorized. P25 private tests use explicitly synthetic manual evidence and do not establish external Finance authority.

## 23 October public UI release and source preservation

Bills list/editor/detail and Inbox restoration PR #57 is deployed. See [the release evidence](docs/procurement/release/PROC-UI-RESTORATION-DEPLOYMENT-20261023.md) and [original-layout integration register](docs/procurement/PROC-UI-ORIGINAL-PRESENTATION-RESTORATION.md). All 23 deployed-asset browser cases passed with mocked business HTTP; this is not actual database acceptance for every workflow. Backend/schema/flags are unchanged. Use the ERP-adapted imported layout reference `d104f596183e7a012a04578a679aac795e1489d4` and keep current shared branding, shell, API and recovery behavior. No reduced parallel forms or local demo mutations.

Thirteen fully merged remote UI branches were deleted after exact-tip/main-ancestry/open-PR checks, along with three unattached local branches. Fetch published main for new work. Unmerged and advanced branches, checked-out worktrees, evidence and backups remain. Manus retains `MANUS-CORE-BUILD-03`; the coordinator owns remaining Procurement integrations, including manual payments behind retained original screens. Finance absence must not remove Payments UI, but no bank execution, AP posting or fake Finance status is authorized.

## 23 October manual-payment foundation checkpoint

The coordinator owns `PROC-PAY-01`; [ERP PR #43](https://github.com/georgemunganga/erp/pull/43) contains mapped manual records, append-only receipts/history, P27 guards, shared invoice/credit capacity and the existing Core company clock. [The source acceptance receipt](docs/procurement/release/PROC-PAY-01-FOUNDATION-ACCEPTANCE-20261023.json) records1,319 Procurement tests,321 focused Core/API tests,59 real PostgreSQL checks, retained/fresh migrations and actual Program compatibility. Prior failures remain preserved; no hosted-CI, actual payment API/browser, finite-role or production activation is claimed.

The next coordinator milestone is typed payment policy and exact shared Core workflow linkage, then the current-authority/idempotent API and existing restored Payments UI binding. Do not invent a policy ID, require an employee link, copy a second workflow engine/frontend, manufacture Finance Paid, or enable demo mutations. Manus retains `MANUS-CORE-BUILD-03`; no overlapping payment implementation is assigned by this checkpoint. Start any later assignment only after the prerequisite merge and fresh published main baselines.

After payment history exists, rollback must retain the new invoice/credit guards or disable those writes. Old P24 application code must stay off write traffic over retained manual payment data.

## Payment governance implementation checkpoint — 23 October 2026

The coordinator owns draft [PROC-PAY-02 / PR #44](https://github.com/georgemunganga/erp/pull/44), based on merged ERP `9693296432a698c06b4bfc724ff394405b75cfaf`. It adds typed review policies, actual shared Core case/decision links, company-date observation evidence and P28; the existing Payments editor title is retained. Unregistered scoped payment reads and actor-owned command recovery are source work, not a live API. Payment command integration, private PostgreSQL/finite-role API acceptance and the original UI/Playwright payment journey remain coordinator-owned. Do not implement a competing Payments UI, add employee prerequisites, enable demo mutation or assign policy/grants by default. Manus retains `MANUS-CORE-BUILD-03` from the published main baselines above.

The P28 migration is not applied to production. Keep its schema checks and the existing operational-policy readiness pins coordinated; P28 extends the shared policy identity CHECK and old rendered P23 pins cannot silently certify it. No Finance/AP dependency is introduced for manual external-payment recording. No bank execution or fabricated Finance Paid result is authorized. GitHub issues are disabled; PR #44 and the versioned feature/handoff/verification records provide change trace. Hosted GitHub CI remains blocked by account billing; local evidence is recorded separately.

## Queued P35 backend verification — 24 October 2026

[MANUS-P35-BACKEND-VERIFICATION-06](docs/collaboration/manus/MANUS-P35-BACKEND-VERIFICATION-06.md) is a separate, queued source-verification assignment pinned to ERP branch `codex/p35-contract-review-source` at `f5cee78d57df4a0d22089b9a7b94fee844f27233`. It must not interrupt the active Core build, scanner or P33 UI workstreams. P32/P33/P35 remain held and default off pending genuine schema, runtime and owner evidence; the task's SDK and test results must be recorded before any further activation claim.


## Coordinator checkpoint — 29 October 2026

No new Manus milestone is assigned by this checkpoint. Published main baselines remain ERP `1c6a81d772c654e7a58254845d116e2cc3b0d7bd` and UI `47cac6a13a24e9db113383f8a9cf5b319494d526`; refresh and verify them before any subsequent assignment.

Coordinator-owned draft [ERP PR #136](https://github.com/georgemunganga/erp/pull/136) and [UI PR #94](https://github.com/georgemunganga/module-connect/pull/94) retain the original imported invoice UI and manual Core unit-review work. Backend `0f656d63` passes 1,592 Procurement and 671 host tests plus a focused regression. Original-UI review/recovery and native retained history passed; the recorded comparison is legitimately Blocked with no qualified claim. See the [current private workflow checkpoint](docs/procurement/gap-closure/M6C-PRIVATE-WORKFLOW-CHECKPOINT-20261029.md) for evidence and release limits.

Currency-aware invoice calculation and controlled correction after processing history remain coordinator-owned gaps. The Core precision and M6D documents are proposed contracts; do not implement a competing currency master, replace the imported UI, rewrite historical invoices, enable production capabilities or claim Finance integration from these private tests. New work must consume published, reviewed prerequisites; these draft heads are not published main baselines.


## M6D active continuation — 30 October 2026

Published main baselines remain ERP `1c6a81d772c654e7a58254845d116e2cc3b0d7bd` and UI `47cac6a13a24e9db113383f8a9cf5b319494d526`. Draft ERP PR137 and UI PR95 contain Core currency ownership and new invoice calculation/recovery; draft source `72537cd5` / UI `72c3882e` includes the source-picker correction. [Actual capture evidence and retained failures](docs/procurement/gap-closure/M6D-PRIVATE-CURRENCY-CAPTURE-CHECKPOINT-20261030.md) distinguish one saved invoice and original-key recovery from uncompleted matching. The coordinator continues actual mapping, independent unit review, qualified comparison, private fixture restoration and reviewed release. No competing currency master, employee prerequisite, policy weakening or replacement invoice UI is authorized. These draft heads are not published prerequisites for a new external assignment.

### B1 actual capture adoption checkpoint — 3 November 2026

PR165 / issue162 now has genuine migration `20261103163108_M7b1CommercialRoundDraftPlans`, actual1970 Procurement +377 Host/model/SQL evidence (`63f5f9b7…`), and private phase1 Up/capture (`384660f8…`). The new source adopts the full observed query together with migration ID and canonical hash `24e096b8…`; old M7A accepted shapes remain unchanged. Latest four new Host contracts and one named test supersession require source-bound381-case verification before the single captured publication26. No B1 flags, users, finite-role grants, API workflow, UI or production activation is claimed. Root separately reviews the genuine empty Down/Up packet; cookie CRUD and barrier/finite-role/retained Down acceptance follow later. Keep the original generator failures and raw/final source hashes in the portable migration-provenance document.

### B1 UI-facing backend follow-up

The source-only successor adds current source version/status, shared verified method/line projection, per-record actions, round-only provisional-source discovery, minimal event-scoped Core people paging and independent M4 enrollment/cancellation barrier hints. No original M4 maker gate is relaxed. It also enforces the reviewed B1-only Core SELECT safe union while retaining the same required Core minimum. Root-authored Core-read guard tests and source DTO/action/projection tests await the next source-bound Host run/publication27; pub26/schema/finite-role proofs remain separate actual evidence. Existing round command bodies, genuine migration, canonical schema hash and owning Procurement/Core source remain unchanged.

### M7A restored physical-shape source correction — 3 November 2026

Release issue [175](https://github.com/georgemunganga/erp/issues/175) holds the paired technical deployment pending fresh restore readiness. This focused draft starts from published ERP main `c2da0b6f59e8472d8af3fb6dc464485d7d2b6b34`; UI `26d4` remains unchanged and unmerged Plan PR171 is excluded. The source adds one exact complete seven-table quotation contract derived from saved authenticated production-backup restore metadata. Its four M7A CHECK definitions differ from `Json`; relative to `ProductionJson`, the restored M4 decision definition is a fifth difference. Existing contracts, catalog projection, model, migrations and privilege/owner denials remain unchanged. Both the digest matcher and database JSON equality admit the same third full contract; unknown changes and mixed CHECK variants remain denied.

The schema-only fixture is a projection of saved actual metadata, **not yet an actual invocation of the frozen seven-table SQL or compiled provider**. Source audit SHA-256 is `93fad9d7a623cde40ae6a8a8fd07782fddaf04354db6cb7930a9b9e36e28d62e`; the private projected-observation file SHA-256 is `563d562707a02a2d0f97dfd6e3988d8248c8fce204b9bd4650a0df5293859c02`. Root owns SDK verification, compiled old-negative/new-positive proof, actual SQL capture in the next isolated rehearsal, runtime/private PostgreSQL work and release approval. New regressions cover all three contracts, old-contract rejection of the observation, exact SQL alternatives, constraint/omission/scope/index changes, mixed variants and unchanged owner/privileged-role denials. Tests are authored but unexecuted at this source checkpoint; no deployment or activation is claimed.


### PROC-05A actual private primary checkpoint — 3 November 2026

Plan PR171/issue169 now includes published main556fbee2; only the B1 restore evidence document conflicted and current main evidence is retained. Tested backend6e85/tree `c4d0703b357ba2674cd4baffd1e90e80a069ef7b` is unchanged. Root actual548 Host / exact2004 Procurement reuse, model/unchanged B1SQL, dual-native finite-provider and owner-denial evidence, private execution20+2 and ordinaryRecord7, real shared policy Create/Submit/Publish, and UI3e/26 focused checks/new build/four-command browser primary path are documented in the [current checkpoint](docs/procurement/gap-closure/PROC-05A-PRIVATE-INTEGRATION-CHECKPOINT-20261103.md). The browser observed Create201/Submit201/Review200/Final200, reviewer-owner403, historical read-only/currentActive, revoked cohort cookies and empty owned cgroup. Strict post-primary native owner reconciliation, retained Down55000/privilege negatives and production release remain Root-owned/pending. Master145 stays NOT READY; no merge/deploy or production acceptance follows from this documentation update.


### PROC-05A final private successor — 4 November 2026

Actual executable sourcec93/tree `adbcb5c396deba114fe57659e04349dc49e129c7` passed551 Host cases with every prior548 identity retained and exact2004 Procurement reuse (not rerun). Only three optional established Core reads changed; minimums, Procurement/Core source and all migration SQL remain unchanged. New150-file publication manifest `6ff57b4c5cbbcee999b12706e7f0ba41dc05712948ce2a471c4e48ab7829ef76` and delta image `8fa2f59813d6670e71281b3f37ef752e7ce042cc1c349e5d63b7967b257baa66` are captured. Actual purpose-role private provider canary and production read-only oldGuard=false/newGuard=true passed; no production DDL/grant/env change or full-provider authority is implied. The earlier actual four-command UI3e/backend6e85 path now has strict original-row/13-domain/14-audit/3-outbox owner proof, retained Down55000 and six42501 denials with all three native states exact. [Final evidence](docs/procurement/gap-closure/PROC-05A-FINAL-PRIVATE-RELEASE-EVIDENCE-20261104.md) records hashes, chronology and purpose boundaries. Root still owns review/paired rollout; PR171 remains draft until reviewed, issue145 NOT READY. No deployment or alternate-flow UAT inferred.
