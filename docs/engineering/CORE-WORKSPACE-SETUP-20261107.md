# Core workspace setup authority and provisioning

## 1. Identification
Tier3 Core child feature; company creation within the maintained shared ERP.
## 2. Purpose
Allow an explicitly enrolled verified payer controller to request a company,
without granting company access from an email, commercial read or login alone.
## 3. Scope
First increment consumes Laravel's separately scoped current payer observation.
Company binding/capacity/create/invite/admission commands follow; not live yet.
## 4. Parent
Core product identity, federated principals and machine commercial licensing.
## 5. Actors
Verified Kuubu identity with separately consented setup scope; payer owner/admin.
## 6. Entry
Future own-product setup action and fresh PKCE ceremony; no new public route here.
## 7. Preconditions
Default-off workspace setup, product identity and licensing enabled/configured.
An access token must come from the future protected server-side setup grant.
## 8. Workflow
Read fixed Laravel customer authority resource with the person's token. Bind
response to exact issuer, subject, configured client and selected payer. Recheck
immediately before a future explicit create transaction; never cache authority.
## 9. States
Current bounded observation, denied or unavailable. None creates an ERP session.
## 10. Permissions
Owner/admin observation is only payer control; Core still owns company creation,
capacity allocation and operational grants. Staff needs no billing membership.
## 11. Rules
Canonical lowercase ULIDs; exact subject/client binding. Action must be workspace
creation, payer control true and operational access false. Reject stale/future,
overlong/expired, malformed, duplicate or foreign responses. Cap local validity
at60 seconds including network time, using both wall and monotonic clocks.
## 12. Data
Observation holds only subject, client, payer, enrollment and deadline; never
stores the access token. Future company binding and slot allocation must persist
atomically in Core, with immutable ownership and idempotency receipts.

Inspection found no existing subscription workspace-capacity field. Before the
create command is enabled, Laravel must record the purchased allowance on the
subscription rather than infer unlimited workspaces from active status or read a
later mutable catalogue limit. Existing subscriptions without explicit allowance
must not gain capacity automatically. Core must allocate capacity transactionally
across all companies bound to that commercial enrollment; concurrent requests
and retries must not over-allocate or create duplicate companies.

The initial SaaS security boundary will be one Core tenant per company workspace
within the shared application/database, with separate explicit identity links
for each tenant. One payer can fund multiple such workspaces. This preserves the
existing tenant-filtered modules without granting access to every company from
one payer membership. Existing multi-company installations retain their own
model; no production tenant is rewritten. Protected operational admission and
explicit employee scope must still prevent legacy unassigned-branch fallback.
## 13. UI
Own Kuubu login and explicit setup consent are in website PR3/b01985ee. Core
must request setup scope only for setup; ordinary team sign-in stays unchanged.
## 14. UX
Denied setup must not be presented as invalid credentials. No automatic company.
## 15. Notifications
None for observation. Future invitation delivery follows committed Core state.
## 16. Documents
No access to HR, payroll, procurement or financial documents is conveyed.
## 17. Errors
Provider403/404 denies; other non200, bad JSON, timeout/network fail unavailable.
Caller cancellation propagates. Neither returns any previous observation.
## 18. Integration
Exact commercial-api.greenwebb.tech endpoint, GET bearer,10second/64KiB bounds,
no redirects/cookies or HttpClient request logging. No machine-token substitution.
## 19. Audit/privacy
No token/body/query logging or browser token exposure. Future create command
must audit exact actor/target/action/authority and committed result without token.
## 20. Reporting
Authority checks are not provisioned companies, sold subscriptions or users.
## 21. Configuration
ERP:WorkspaceSetup:Enabled defaults false; both product identity/licensing remain
prerequisites. No production credentials/configuration altered by source work.
## 22. Reliability
No cached authority fallback. A caller must discard expired observations and
re-read after revocation/outage; an in-flight bounded observation is not a lock.
## 23. Postconditions
No business database write, identity link, membership, company, role or session.
## 24. Recovery
Read-only retry is safe; failure does not provision. Source rollback needs no
schema migration. Retain old qualifications and use exclusive new test output.
## 25. Acceptance
Only an exact current provider observation is consumable; no broader grant.
## 26. Tests
Fixed endpoint/bearer, wrong subject/payer/client/action/enrollment, duplicates,
malformed/oversized JSON, revocation/outage, bounded expiry/clock rollback and
cancellation. Genuine Laravel/provider integration remains a separate gate.
## 27. Release gates
Protected setup OAuth grants, subscription workspace capacity, atomic Core
company/payer binding and slot allocation, idempotent create/audit, invitations,
operational sessions/revocation, company/branch/module enforcement, finite runtime
ACLs and public ingress/local-auth/query logging are required before launch.
One shared maintained application/database; no per-customer source or container.
## 28. Evidence
Runtime source `73b4afbe620328410090b5e1ff89b559ba3807b4` passed112
focused Windows tests (41 new authority cases) and163 focused Linux tests.
Linux full HRM regression passed2,134 cases, zero failures, with the existing
opt-in real provider case skipped in the no-network run (2,135 total). The old
real provider case is separately qualified at its source documented in
CORE-PROVIDER-INTEROP-20261107.md; it does not exercise this new setup reader.
Actual setup-scoped Laravel→Core consumer integration remains required.

Windows .NET10.0.403 log SHA256:
`c818d6514553908c016c7e3e8852976e00c8e666e89f270370f323b88ddf1170`.
Native SDK10.0.403 image is pinned to
`sha256:212fe4cb74627d5adf9817a4088906ae3edff1bd853bb27565d4b46731efe61e`.
Archive SHA256:
`87ca0503becdf503f2cfe2e376e2a2463996b7a6f8ed2fbe03d50973d710acdf`.
Linux focused log SHA256:
`9973286e3ff59ec9013d092d2cf6e488cc02062f544fc5fe46d3469ca6ea46eb`.
Linux full log SHA256:
`940689582b19dea8e128f7b8fc2c2a3f9f132578e6e0f5d3175a1b432a18af3f`.

Native workspace `/opt/kuubu-builds/workspace-authority-73b4afbe-01` used
exclusive source/evidence and plain independent copies of stopped prior build
caches; no retained output was edited or hardlinked. Container
`kuubu-workspace-authority-gate01` ran with network none; launcher
`kuubu-workspace-authority01.service` finished exit0. Build warnings in existing
unmodified ERP files remain; no new reader/test warning was reported.

Hosted run42417243189/check128537885549 never started its backend job:
GitHub's annotation says the account is locked for a billing issue. Independent
native results do not mean hosted CI passed. No production schema, runtime image,
key, grant, feature flag, public route, Google, Lenco or Perfex state changed.
