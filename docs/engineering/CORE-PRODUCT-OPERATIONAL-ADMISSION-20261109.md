# Licensed product admission

## 1. Identification
Tier3 Core feature,9 November2026. Implementation in progress; not live.
## 2. Purpose
Let an explicitly enrolled Kuubu person enter an authorized company and licensed
module with current permissions and branch scope, using the existing ERP.
## 3. Scope
Implemented: separately scoped, email-free person observation/reader and private
encrypted credential/rotation machinery. These create no ERP session or grant.
Next: operational OAuth callback/browser handoff, explicit company/module grants
and selection, revocable session, route/module enforcement and scoped context.
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
