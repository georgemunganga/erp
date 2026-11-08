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
The typed browser client uses fixed same-origin, no-store, redirect-rejecting
requests, keeps CSRF out of URLs, and accepts only the expected setup response
contracts. Create retries reuse the caller's reviewed command/key; conflict and
capacity-exhaustion remain distinct recovery states. It stores no credentials or
session data. Screens, reviewed-command lifetime/reload recovery and real browser
acceptance remain to be implemented; a tested client is not a completed form.
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
No rendered setup form or genuine protected provider→create acceptance is claimed.

Laravel payer resource15adde5 separately passed112 tests/874 assertions on native
MySQL8.0.46. Current read-only checks returned200 for Perfex sign-in and the
existing private ERP /health/ready. No production enablement, migration, image,
credential, Google, payment, DNS or Perfex change occurred.
