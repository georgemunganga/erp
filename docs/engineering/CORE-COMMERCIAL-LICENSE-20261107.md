# Core commercial licensing channel

## 1. Feature Identification
CORE-COMMERCIAL-LICENSE, Tier3 under GW-ENG-STD-001; Core engineering owns it.
## 2. Purpose & Business Outcome
Read a current company subscription without making ERP staff billing members.
## 3. Scope & Exclusions
Confidential machine token and validated short commercial lease. This component
does not admit users, create workspaces or grant operational permissions.
## 4. Parent & Related Features
Core product identity, explicit external links, Laravel product licensing PR3.
## 5. Actors & Participants
Core server and Laravel commercial authority; no browser credential handling.
## 6. Entry Points & Triggers
Internal ReadAsync(enrollment) from a future authorized workspace admission or
session refresh. Enrollment must come from Core's explicit company/payer binding.
## 7. Preconditions
Identity and licensing flags enabled; separate licensing client UUID/secret;
fixed reviewed HTTPS issuer. No identity client or user token fallback.
## 8. Workflow
Acquire/reuse an unexpired machine token under a single-flight lock; read the
exact enrollment resource; validate issuer/product/schema/ID and lease bounds.
## 9. Status / State Transitions
Unavailable, denied or authorized commercial lease. None means ERP admission.
## 10. Permissions & Data Scope
Machine scope product:license only; one exact canonical enrollment per call.
There is no HTTP route accepting a browser payer reference in this step.
## 11. Business Rules
ReadAsync returns no cached lease on failure. Callers may reuse a previously
returned lease only while IsCurrent remains true; never extend or serialize its
clock budget. Lease at most60 seconds and never past subscription expiry;
elapsed time and wall time both limit validity. Current
Core principal, company, branch and module permissions remain required.
## 12. Data Fields & Ownership
Laravel owns enrollment/subscription/modules; Core will own company binding.
Only machine token held in process memory, with conservative120-second maximum.
No new persistence, profile data, email matching or customer creation.
## 13. Screens & Data Views
None; existing branded login screens stay separate.
## 14. User Experience / Human Behaviour
Callers must distinguish commercial denial from temporary provider failure and
must not label commercial authorization as operational access.
## 15. Notifications & Communications
None. No payment, mail or invitation triggered by checking a lease.
## 16. Documents & Attachments
None; existing document authorization remains in force.
## 17. Exceptions & Edge Cases
Wrong binding, redirects, malformed/duplicate JSON, wrong scope/token type,
oversize or slow bodies, timeout, cancellation, stale/future dates, missing or
invalid modules, expired subscription/token, revocation and clock rollback.
## 18. Integrations / APIs / Events / Sandbox
POST fixed product-identity/token client_credentials; GET fixed licensing/
enrollments/{ULID}.10-second total request timeout,64KiB bounded JSON, no cookies,
redirects or automatic retries. No client secret in URL, logs or error messages.
## 19. Audit / Security / Privacy
No credential or provider body logging. Disable default HttpClient logging for
this named backchannel. Exact enrollment references stay on the Core server.
Machine token401/403 invalidates that token; never retry against another client.
## 20. Reports / Dashboards / Metrics
No new metrics yet. Later admission audit records a decision and correlation,
not tokens or raw provider payloads; observability gates live enablement.
## 21. Configuration & Localization
ERP:ProductLicensing:Enabled=false default, ClientId, ClientSecret. Fixed issuer
https://commercial-api.greenwebb.tech. Credentials delivered privately later.
## 22. Non-Functional Requirements
Bounded network/memory/concurrency; cancellation propagated; singleton token
coordination; immutable module set; no continuing lease after process restart.
## 23. Postconditions
Only a current commercial decision returned. No operational session or grant.
## 24. Failure & Recovery
Fail closed, retain no successful lease as outage fallback. Code rollback has
no schema/data migration. Flag off removes registration and outbound traffic.
## 25. Acceptance Criteria
Exact machine grant and resource; strict protocol rejection; concurrent token
single-flight; independent enrollment reads; expiry/clock rollback; cancellation;
no stale fallback or user/billing/tenant side effects; default-off registration.
## 26. Test Scenarios
Real HTTP client with controlled provider transport for all above boundaries;
existing identity/session tests; actual Laravel interoperability remains a
separate required integration gate, not implied by transport fixtures.
## 27. Dependencies & Release Gates
Core-owned company binding, explicit link admission, session credential storage/
refresh, runtime ACLs, company/branch/module checks, real provider interoperability
and release-baseline qualification before public enablement. Existing holds stay.
The admission layer must coalesce commercial reads per current company binding
and reuse only current leases, respecting the provider's per-enrollment quota.
Always recheck Core grants and binding/version; a commercial lease is not cached
user authorization and never survives its own expiry or a binding change.
## 28. Delivery Evidence & Handover
Exact compiled source `11879016777e077ec1adaa295aeaf7c149f753e9`, source archive
SHA256 `ec6abbc37b5f93626b250454cb2ba798b6a1217c0f54e7080c6053902b675ee7`.
All105 focused checks passed (46 new licensing cases plus59 existing identity/
session/workspace cases). Full HRM regression passed2,076 tests, zero failures
and zero skips, in8m32s. No established test was removed or weakened.

Retained native gate `/opt/kuubu-builds/commercial-license-11879016-02`, launcher
`kuubu-commercial-license03.service`; SDK container
`kuubu-commercial-license-gate02` exited0, OOMKilled=false. SDK10.0.403 pinned
digest `sha256:212fe4cb74627d5adf9817a4088906ae3edff1bd853bb27565d4b46731efe61e`.
Networking was disabled and no production database connection supplied. New
protocol tests use the actual HttpClient with controlled transport; the existing
normal suite uses its ordinary SQLite/InMemory fixtures. No schema changed in
this increment and no new genuine-PostgreSQL gate is claimed.

Log SHA256:
- focused: `1892ca3d073687cea2dc8771e67d3e38d71245371fc05cc27184f4282011b991`
- full: `acca261460dbbd708d6053a96d9ed760fc772dc94421b5358e3904f1eb2dbbaf`

Earlier sourcea8be9511 passed105 focused checks. Its full run was deliberately
stopped after review added JSON Accept headers, canonical UUID comparison and
strict date termination; its exit137 has OOMKilled=false and retained logs.
An initial launcher failed before the helper upload completed and created no
test directory. All attempts remain retained; final qualification used an
exclusive new directory and independent plain cache copies from the stopped
previous workspace, never hardlinks or modified historical publications.

Published in the existing stacked draftPR201, depending onPR200/PR199. Hosted
ERP CI was previously billing-locked; this evidence is native, not a claim of
hosted success. Real Laravel interoperability and operational admission remain
unqualified. No production clients/credentials, flags, image/database, public
ingress, Google or Lenco were changed. Private ERP readiness remained200 on the
previous image3ff07382; both own public login pages and API health returned200.
Perfex's login rendered normally in browser; a Python request received403, so
no claim is made that scripted probes qualify authenticated Perfex workflows.

Next principal prerequisite is specified in
[CORE-FEDERATED-PRINCIPALS-20261107](CORE-FEDERATED-PRINCIPALS-20261107.md).
