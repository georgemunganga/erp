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
No cached lease fallback during failure. Lease at most60 seconds and never past
subscription expiry; elapsed time and wall time both limit validity. Current
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
## 28. Delivery Evidence & Handover
Implementation and qualification evidence will be recorded after running checks.
