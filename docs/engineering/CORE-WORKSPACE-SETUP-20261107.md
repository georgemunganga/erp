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
Pending implementation and actual qualification; no production deployment.
