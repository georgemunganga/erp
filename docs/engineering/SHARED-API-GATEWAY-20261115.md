# Kuubu shared API gateway

## 1. Identification
15 November2026; Tier3, existing Core/HR infrastructure.
## 2. Purpose
One stable HTTPS API origin for mobile consumers without duplicating ERP modules.
## 3. Scope
API edge, discovery, versioned attendance routing and reusable installation profile.
Native client registration/employee authorization must pass before advertising mobile readiness.
## 4. Owners
Edge owns transport/routing; Laravel identity/enrollment/billing; Core company grants;
HR owns worker links, attendance/payroll. Gateway never grants access.
## 5. Actors
Mobile employee, authorized supervisor, installation operator. No new grants issued.
## 6. Entry
SaaS api.kuubuerp.com; dedicated installs use their own configured HTTPS API origin.
## 7. Data
No new database or copied attendance; retain existing HR records and tenant boundaries.
## 8. Identity
Existing browser cookies stay on the browser origin. Native OAuth client and callback
must be explicitly registered; no embedded client secret or cookie transfer.
## 9. Employee binding
Verified identity must be explicitly linked to a worker in the selected company.
No matching email/domain grants access.
## 10. Permissions
Existing SaaS HR admission currently requires admin/full-company coverage. A staff
native adapter is a release gate, not solved by rewriting Host or trusting headers.
## 11. Workflow
Discover installation, authenticate, select authorized company, then use HR APIs.
## 12. Rules
Unknown modules default404; no generic proxy to arbitrary destinations.
## 13. Attendance
Reuse existing HR attendance methods; no new calculation rules at the edge.
## 14. Payroll
Attendance does not automatically approve payroll or publish draft payslips.
## 15. Replay
Do not automatically retry mutating requests. Native clock retries/idempotency need
qualification before mobile attendance launch.
## 16. Errors
401 without credential; forbidden cookie/scope injection;503 while native profile
is not qualified;404 unknown surface. Never pretend this is working staff sign-in.
## 17. API
Versioned /v1/hrm/me/attendance and its today/clock-in/clock-out routes map to the
existing /api/hrm/me/attendance paths only after native admission qualification.
## 18. Discovery
/.well-known/kuubu-api publishes API version, installation auth profile and explicit
mobile readiness; no client secrets, customer records or internal upstream addresses.
## 19. UI
No ERP/Kuubu UI changes. Native app chooses a configured installation base URL.
## 20. Transport
Public ACME TLS, encrypted verified loopback upstream, bounded bodies/timeouts,
no shared browser cookies, cache/no-store and no automatic POST retry.
## 21. Deployment
New immutable Caddy configuration release; clone existing proxy mounts/image,
retain old proxy, preserve the app-host block and certificate storage.
## 22. SaaS isolation
Routing provides no tenant authority. Never select a database from a caller hostname
or an unverified company/header value. API token validation must precede admission.
## 23. Dedicated inheritance
Same source/versioned route contract; per-install HTTPS origin, trusted upstream,
issuer/client callback, worker bindings, permissions and compatible migrations remain
operator configuration. Git pull alone cannot register mobile OAuth or change DNS.
## 24. Observability
Probe public edge and upstream readiness; do not log tokens, cookies, GPS or payroll.
## 25. Recovery
Validate inactive candidate; bounded cutover with old-proxy rollback; no schema change.
Keep existing publication paths and backups unchanged.
## 26. Verification
Real Caddy route tests against isolated upstream, unknown/forged request negatives,
public TLS/discovery/readiness and existing app-host checks. Native signed-in clock
acceptance remains a separate mandatory gate until client details are available.
## 27. Acceptance
Record edge live independently of mobile employee flow. Do not label a protected
401/503 or server DNS success as a successful clock-in test.
## 28. Handoff
Record exact source/config hash, consumers, previous proxy, public proof and holds in
MANUS_HANDOFF.md. Do not touch erp.newworldcargo.com as part of a question about it.
