# Licensed product admission

## 1. Identification
Tier3 Core feature,9 November2026. Implementation in progress; not live.
## 2. Purpose
Let an explicitly enrolled Kuubu person enter an authorized company and licensed
module with current permissions and branch scope, using the existing ERP.
## 3. Scope
Implemented: separately scoped, email-free person observation/reader and private
encrypted credential/rotation machinery, plus default-off operational OAuth
callback/browser handoff. These create no ERP session or grant. Next: actual
provider/browser wire, explicit company/module grants and selection, revocable
session, route/module enforcement and scoped context.
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
Bind the exact selected legal entity as well as its tenant. Legacy legal-entity
creation must not bypass purchased workspace allocation in SaaS mode; company
creation remains owned by the licensed provisioner. An unknown module route or
unbound entity must not inherit access from a broad legacy administrator role.
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
operational activation without the independent Core `ERP:OperationalIdentity:Enabled`
flag (default false), separate canonical confidential client UUID/secret and
existing private state directory outside application assets. This identity
continuity scheme is non-default and cannot satisfy ERP authorization.

### Operational callback/browser implementation contract

`GET /api/core/product-identity/operational/bootstrap` returns a CSRF token and
sets a protected host-only Secure/HttpOnly/Lax browser cookie; anonymous bootstrap
allocates no disk state. Same-origin JSON `POST /start` requires that cookie and
CSRF, rejects selectors/nonempty command bodies, revokes prior operational state
and rotates browser binding/challenge. Only the fixed Kuubu authorization URL,
S256, exact identity + ERP-session scopes, separate confidential client and
fixed operational callback are used. Protected state binds purpose, browser,
challenge, fixed return path and five-minute deadline. Pre-handler checks deny
invalid browser/state before code exchange; framework correlation remains required.

The callback discovers a canonical subject only through the strictly validated
current-person resource. It requires a short-lived Bearer/access/refresh pair,
stores it privately, consumes the browser challenge and emits a separate opaque
identity cookie with no provider tokens, email, roles or tenant. Successful
handoff revokes prior setup/invitation handles and clears pending identity;
ordinary product sign-in/logout revokes operational identity in return. Neither
direction alters the default ERP/local session. Session status freshly observes
the person and returns `operational_access_granted=false`/company-selection next
step; no browser company/module selector is accepted here. The interim callback
returns to that status endpoint; company/module selection UI remains next work.

Credential admission under the shared directory gate permits at most four active
credentials and six issues per ten minutes for one verified subject/client.
Revocation frees concurrent capacity but not recent-issue history; separate
workers/key-ring readers apply the same limits. Directory capacity/scans remain
bounded. Fixed-memory salted IP budgets limit anonymous bootstrap/start/status;
there is no success-proof cache or public identity directory. Owned five-minute
cleanup removes expired credentials after the existing grace. Cleanup failures
emit a generic warning. Operational dashboards/alerts and capacity/load tuning
remain deployment gates, not established by the code.
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

### Protected operational OAuth handoff —9 November2026

Published Core `86abac3b0e577ff070304828ed9aa95bc00a0040` adds a separately
flagged operational identity callback/browser handoff. It uses website consent
source `1ade23e7629d8a3004ac2caf8d3157691fa8681e` and unchanged Foundation
runtime `75cc91ff5025a54f3803fc34c4d297b12dcd5a98`. All remain draft/default-off.
This is identity continuity, not a licensed company/module ERP session.

Same-origin JSON start requires protected browser cookie/CSRF and rotates its
binding/challenge. Anonymous bootstrap creates no disk record. Protected purpose,
fixed callback/return path and five-minute browser state are checked before code
exchange, followed by framework correlation, confidential client and S256. Only
the exact identity + ERP-session scope pair is requested. Bearer lifetime,
refresh token and optional returned scopes are checked; subject discovery uses
the strict current-person resource, never unverified JWT/email claims.

Credentials stay encrypted server-side. The separate non-default identity cookie
contains opaque handle/binding and verified subject/issuer, without provider
tokens, tenant, email or roles. Successful handoff revokes old setup/invitation
handles; ordinary product sign-in/logout revokes operational state in return.
Identity status rechecks the current person and returns no operational access,
pending company selection. Sign-out/provider denial persist revocation. An outage
returns unavailable with no cached identity fallback. The callback currently
returns to the status endpoint; the actual selection UI remains required.

Under the shared directory gate, one verified subject/client can hold four active
credentials and receive six issues per ten minutes. Revocation frees concurrent
capacity but not recent-issue history; reopening preserves both limits. Fixed
memory salted IP budgets bound bootstrap/start/status/callback attempts. Owned
five-minute expiry cleanup retains existing grace and skips held rotation locks.
Monitoring/capacity tuning and distributed-storage qualification remain gates.

Final fresh API/test builds pass with27/20 existing warnings, none added.
Windows regression:523 passed/37 opt-in native skips. Native Linux:98 passed,
zero skips, plus holder/observer one pass each. Real ASP.NET OAuth handling with
synthetic provider responses tests PKCE/correlation, browser/state denials before
token exchange, purpose/lifetime/refresh denials, default-off, no implicit ERP
user/session, outage versus durable denial, restored-cookie denial and code
replay. Two-process shared key-ring/rotation/sign-out fencing remains passed.

Independent audit verifies seven C# sources,35 unchanged dependency DLLs,
586 test/151 API immutable runtime files,149 state files without plaintext
synthetic provider tokens and268 private Linux entries (0700 directories/0600
files). All three owned containers stopped, network none/no host ports, with
read-only binaries/SDK/scripts. Persisted revision fence and tombstone remain;
there is no unfinished publication in the process probe. Evidence:
`inspection/qualifications/operational-flow-core-03`, `operational-flow-linux-03`
and `operational-flow-verification-03/verification.json`.

Attempt01 retains two strict duplicate Set-Cookie failures. The callback now
revokes server state without emitting a redundant deletion before its replacement
cookie; assertions remain strict. Intermediate02 compilation is retained.
Final03 records its final source manifest before its first build; assertion
analyzer cleanup and explicit non-null observation checking remove new warnings.
No retained publication was overwritten.

Actual Laravel callback/refresh/store wire, connected browser/consent composition,
account-switch/recovery UX, company selection, explicit module/action/branch
grants, revocable bound ERP sessions and route enforcement remain required.
The new `ERP:OperationalIdentity:Enabled` flag defaults false; separate client,
secret and state path remain unconfigured. Classic local authentication and
main/UI-main/B1 are unchanged. No production schema/config/deployment/flags,
external mail/payment, Perfex or DNS change. Shared SaaS, Lenco, hosting/domain/
email products and dedicated/on-premise/offline/upgrade/restore qualification
remain in scope. Both backend PRs remain draft.


### Protected credential/rotation qualification

Published Core8b7cee073a303b9d9da8aa89b221a0f28ec28079 adds encrypted operational
identity credentials and rotating-token machinery. It is not registered by the
API and creates no operational cookie/session, company/module/branch grant or
business authentication. Foundation runtime75cc91ff is unchanged.

The private store binds a random handle/browser binding, exact confidential
client/subject, access/refresh tokens, revision and immutable eight-hour absolute
deadline. Separate private state/key directories and Data Protection purpose
isolate credentials. Capacity is bounded (default512, supported1–2048; directory
scan limit16,384). A directory gate coordinates admission, lock acquisition and
expiry cleanup. Rotation holds an exclusive per-handle lock; ordinary person
observations release it across the provider call and revalidate state afterward.
No successful-proof cache or failure fallback is introduced.

Before sending a rotating token, a write-through/disk-flushed revision/complement
journal fences that revision. Success atomically publishes a new encrypted
revision; lost/invalid acknowledgement, cancellation/crash or older-ciphertext
restoration cannot submit the old refresh again. Sign-out/provider denial writes
a durable tombstone, so an in-flight rotation cannot resurrect credentials.
Definitive rejection differs from transient unavailability. Fixed HTTPS refresh
accepts only bounded Bearer/token/expiry/scope responses. Returned person proofs
preserve their original monotonic origin and are further capped by access and
absolute expiry. Refresh never extends that absolute deadline.

Expired cleanup waits15 minutes, skips held locks and removes expired state,
fence/tombstone/temporary publications and lock inode under the directory gate
before returning capacity. The runbook documents filesystem/ownership, bounds,
uncertain-refresh recovery and pending production registration requirements.
It does not qualify distributed/NFS/SMB storage or multiple physical hosts.

Fresh API/test builds passed with27/20 existing warnings.503 Windows checks
passed/37 native opt-ins skipped (43 new credential/refresh cases alongside the
existing460 checks). Linux storage/refresh/person-proof suite passed78 cases,
zero skips. Separate holder/observer processes each passed: the observer skips
the locked rotation, revokes without waiting for it, and the holder denies
without observing a person or publishing credentials after sign-out.

Independent audit verifies the persisted revision1 fence/complement and revoke
marker, no unfinished publication in the process probe, no plaintext synthetic
provider credentials in87 state files, and private modes for150 Linux entries
(directories0700/files0600).35 independent dependency DLLs and586 test/151 API
runtime files retain hashes/size/mtime. All three owned containers are stopped,
use network none, publish no host ports and mount binaries/SDK/scripts read-only.

Evidence: inspection/qualifications/operational-credentials-core-02,
operational-credentials-linux-02 and
operational-credentials-verification-02/verification.json. Build01 is retained:
its test span comparison crossed await (CS4007); separating the awaited value
fixes it, and property access removes two new primary-constructor capture
warnings. No protected publication was overwritten.

These tests use synthetic provider responses and a real shared Linux key ring/
filesystem for the process probe. Actual Laravel refresh + store wire composition,
callback/browser binding, authenticated per-person admission/rate limits, owned
cleanup/monitoring and session recovery UX remain required before registration.
Next: operational OAuth handoff and explicit module/action/branch grants,
followed by revocable company sessions, route enforcement and browser acceptance.
No classic local-authentication behavior is changed.

No production deployment/schema/config/flag, external mail/payment, Perfex
migration, DNS, frontend/Laravel runtime or main/UI-main/B1 change. Both PRs stay
draft. Shared SaaS, dedicated/on-premise deployment/offline licensing/upgrades/
backup restore, Lenco and hosting/domain/email integrations remain in scope and
unqualified where recorded on the milestone board.

### Earlier person-proof contract

Published Foundation75cc91ff5025a54f3803fc34c4d297b12dcd5a98 and
Corece5bc76ff618d0eee98648b723100f248a3802d1 implement the first licensed ERP
admission contract: current, email-free person observation under the exact
`product:identity product:erp-session` scopes and `erp-session-v1` purpose.
`PRODUCT_ERP_SESSION_ENABLED` defaults false. Only a confidential Kuubu identity
client is eligible; public/foreign clients, ordinary identity/cookie proofs,
mixed authorities, selectors and request bodies are rejected. Foundation
rechecks verified personal enrollment/client/token state and caps observation
to30 seconds and actual token-record expiry. It creates no customer, enrollment,
company, membership, session or permission grant. A refresh token is not a
current identity proof: real refresh followed by personal-enrollment revocation
still denies the resource.

The Core reader validates the exact field set, issuer/client/subject/product/
purpose, true verification and false operational grant. Duplicate/missing/extra,
wrongly typed, foreign/stale/oversized/non-JSON responses fail closed. Deadline
includes transit and wall/monotonic caps; denial never reuses a previous proof;
caller cancellation propagates. No email or provider token is retained in the
observation. The reader is not yet registered for operational admission.

Fresh API/test builds pass with27/20 existing warnings.460 focused Core tests
passed/36 native opt-ins skipped, including35 new observation cases. Foundation
PHP8.5.9/Linux full regression passed133 tests/1081 assertions, plus Pint18 files;
PHP8.4.12/Windows focused compatibility passed11 tests/122 assertions, plus Pint.
137 Foundation source files and35 independently copied Core dependency DLLs
retain hashes. Linux runs with network none, no host ports and read-only archive/
overlay/script inputs; its owned container is stopped. No production environment
or credentials are copied into the fixture.

Initial Windows/Linux attempts are retained with three failures: Laravel's
getJson helper serializes an empty array into a GET body, so the strict new
resource correctly returned422. The corrected test helper uses the same
body-free GET as Core; the explicit malicious-body test still returns422.
No production guard or assertion was removed to obtain the passing result.
Evidence: inspection/qualifications/operational-subject-core-01,
operational-subject-php-linux-02, operational-subject-foundation-02 and
operational-subject-verification-01/verification.json. Failed01 attempts remain.

This is a person-proof contract, not completed ERP access. Actual provider-to-Core
wire composition for this resource and native MySQL qualification of this
increment remain unproven. Protected operational OAuth/refresh state, explicit
company/module selection, module/action/branch grants, revocable ERP sessions,
route enforcement and browser acceptance remain to implement/qualify. Inspection
confirms federated users cannot use legacy local sessions or role CSV, and empty
legacy branch assignments mean organization-wide access; SaaS admission must
use explicit grants without inheriting that fallback. Classic local mode remains
unchanged for dedicated installations.

No frontend change, operational session/role/branch grant, schema change,
production activation, live setting, payment, Perfex migration, DNS or
main/UI-main/B1 change. Existing operator/SMTP and browser evidence retains its
original source scope. Lenco, hosting/domain/email products, SaaS deployment and
dedicated/on-premise installation/offline licensing/upgrades/backup restore
remain in the full programme; both PRs remain draft.
