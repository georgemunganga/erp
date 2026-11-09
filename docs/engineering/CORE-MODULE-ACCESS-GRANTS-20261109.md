# Explicit company module grants

## 1. Identification
Core Tier3 feature,9 November2026; internal ledger qualified, end-user workflow not live.
## 2. Purpose
Let an explicit company owner deliberately grant/revoke a current member's
module roles/actions and company or work-location scope without creating a login,
employee, payer membership or ERP session.
## 3. Scope
Versioned grant ledger, current grant resolution and transactional owner commands.
HTTP/UI grant management, ERP session creation and route/data enforcement follow.
No existing local credential/session/role/branch behavior is changed here.
## 4. Parent
Licensed product admission, explicit company membership and Core workspace binding.
Keep the existing ASP.NET/Docker codebase for SaaS and dedicated/on-premise.
## 5. Actors
Fresh verified/enrolled Kuubu person; active explicit company owner; target Core
member; separate commercial license service; future operational session adapter.
## 6. Entry
Internal application commands take a current operational-person proof plus exact
actor link/company, target membership, current membership/grant revisions,
explicit module/roles/actions/scope, confirmation and actor-scoped idempotency key.
## 7. Preconditions
Current person, owner link/principal/membership and bound purchased company slot;
selected module currently licensed. Assignment targets an active federated member
with an active explicit identity link. Revocation may retire an inactive member's
old grant. Billing authority and email/domain confer none of these powers.
## 8. Workflow
Review current member/revision and module catalog; choose roles/actions/scope;
confirm; serialize key/company with team commands; recheck current authority;
append changed grant version, immutable receipt, audit and ID-only outbox event
atomically. Exact replay reports the original command outcome, not current access.
## 9. States
Latest version is granted or revoked. Earlier versions are never merged or used
as fallback. Membership revision changes invalidate old grants, including revoke/
rejoin. Reassignment after a change needs a new explicit grant version.
## 10. Permissions
Initial grant management is company-owner-only, including explicit self-grants.
It grants no payment/spending/approval limits or exemption from module business
rules. Governance administrator/member roles do not imply operational authority.
## 11. Rules
Modules initially hrm/procurement. Reuse known module roles and registered
Procurement action keys, without legacy role-to-action bootstrap or erp_admin
fallback. HRM initially uses its existing hrm-access/hrm-admin policy boundaries
and workforce role profiles; finer HRM route qualification remains required.
## 12. Data
Core owns append-only grant versions, location rows and command receipts. Version
binds tenant/company/target membership, its revision, module, explicit role/action
snapshot, scope, predecessor and issuing membership. Composite keys prevent
cross-company target/predecessor/receipt binding and cross-tenant branch binding.
Work-location company ownership is rechecked on every decision; moving/archiving
a branch invalidates its old scoped grant without breaking legacy branch moves.
## 13. UI
Future owner editor reuses company/team controls, exact member ID/revisions and
shared dialogs. All-company scope has its own unchecked confirmation. No module
launch until the separate operational session and route gates are implemented.
## 14. UX
Show current member/module permissions, stale-change conflict and uncertain write
recovery. Retry the identical reviewed key; never silently retry changed authority.
## 15. Notifications
Only ID-only grant-change outbox events; no mail/payment/provisioning activation.
## 16. Documents
No new business-document access follows from creating a grant record alone.
## 17. Errors
Unknown module/role/action/scope/duplicate/empty branch set is invalid; missing
authority/current license denies; revision/key mismatch conflicts; provider/DB
failure is unavailable. Cancellation rolls back; no partial grant/audit/receipt.
## 18. Integration
Future adapter must resolve latest grant and recheck current person, exact
membership/link, license/module and work locations for each operational request.
No issuer tokens, roles CSV or browser assertions can substitute for this read.
## 19. Audit/privacy
Append-only audit records versioned before/after role/action/scope configuration,
actor and correlation. Events carry IDs/revision/state only. No credentials,
email directory or provider proof bodies are persisted in this ledger.
## 20. Configuration
No HTTP registration or production activation in the initial ledger increment.
Migration is additive; owning deployment applies it explicitly. New records are
not created from historical owners, roles, employees or email matches.
## 21. Security
Test foreign company/member/link/license/branch, non-owner, expired proofs,
cross-tenant constraints, unknown permissions, no implicit owner grants, stale
membership/version, revoked latest version, branch moves, replay and concurrency.
## 22. Operations
Qualify real PostgreSQL apply/rollback and locking independently of SQLite unit
checks. Populated rollback must refuse to destroy grant/evidence history. No
production migration runs during development or test preparation.
## 23. Postconditions
A command records an explicit decision only. No authentication principal, ERP
session, employee, password, billing access or operational side effect is created.
## 24. Recovery
Reload latest membership/grant after conflict. Exact-key replay reports historical
outcome; current resolver must still deny revoked/replaced/stale grants. Never
delete a version or clear revocation to recover access.
## 25. Acceptance
Complete ledger increment when schema constraints, atomic commands/current
resolution and real DB concurrency/rollback tests pass. Full admission remains
unproven until browser/editor/session/module/branch isolation is verified.
## 26. Tests
Focused relational/model tests plus native PostgreSQL cases; retain failed builds
and fixtures, use exclusive new publication roots and read-only consumers.
## 27. Release gates
Grant HTTP/UI, revocable sessions, default authentication integration, module route
and data-scope enforcement, real browser composition and production ops remain.
Lenco/hosting products and dedicated/offline/upgrades/restore remain programme work.
## 28. Evidence
Record executed source/runtime/DB qualifications here after verification.

The internal ledger/current-resolution increment passes556 Windows checks with39
environment-specific opt-ins skipped, and82 native PostgreSQL checks with no skips
or failures. The latter includes all25 module-grant cases, team commands and
startup-policy checks. Real migrations apply; empty rollback/reapply has no model
drift; populated rollback refuses to remove versions/receipts. Four simultaneous
same-key commands produce one version, receipt, audit and event. Relational tests
reject foreign-tenant locations and retargeted predecessors/receipts. Current
resolution rejects revoked or membership-stale grants and archived locations.
Legacy branch company moves remain supported by the new relational keys.

Evidence: `inspection/qualifications/module-grants-core-02` (full fresh build and
disconnected model comparison), `module-grants-core-03` (independent corrected
test assembly and full regression), `module-grants-native-02` and
`module-grants-verification-02/verification.json`. Ten C# inputs and49 independent
dependency DLLs match their manifests. All585 native runtime inputs remain
unchanged; the owned test/database containers exited, with no host ports and
read-only binary mounts/root filesystem for the test runner. No production
migration, flag, deployment, payment, external mail, Perfex or DNS change occurred.

Native01 is retained:80 passed/one fixture failure because the fixture attempted
to insert a second company into an allocated SaaS tenant. The existing allocation
guard correctly refused it. Core03/native02 seed the foreign branch in its owning
tenant, test archival invalidation and separately preserve legacy company moves;
no production guard was bypassed or weakened. API/test builds retain27/20 existing
warnings. HTTP/editor/session/route qualification remains required; these tests
use internal current-person/license fixtures and do not establish the combined
branded browser/Passport/ERP journey or production readiness.
