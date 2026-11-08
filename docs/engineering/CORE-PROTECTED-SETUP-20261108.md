# Protected Kuubu company setup

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
## 13. UI
Own Kuubu login/explicit setup consent; select owned eligible payer, enter company
name, review and explicitly create. Handle loading/empty/denied/expiry/retry.
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
## 27. Release gates
Genuine provider→protected grant→create/UI, private runtime ACLs/shared-volume
operations, owner/invite policy, licensed sessions/refresh/revocation, branch/
module scope, public ingress/log redaction and deployed recovery acceptance.
## 28. Evidence
Pending implementation qualification; no production enablement.
