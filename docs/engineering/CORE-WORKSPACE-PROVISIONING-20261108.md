# Core purchased-capacity company allocation

## 1. Identification
Tier3 Core child feature; shared app/database, separate company tenants.
## 2. Purpose
Create an explicitly requested company within purchased workspace allowance.
## 3. Scope
Internal atomic company/allocation/principal/link/owner/audit/outbox transaction.
## 4. Parent
Product identity, payer setup authority, federated principals and licensing.
## 5. Actors
Verified enrolled payer owner/admin with separately consented setup authority.
## 6. Entry
Future protected setup grant invokes internal command; no public route here.
## 7. Preconditions
Current exact setup observation and machine lease, same enrollment and limit.
## 8. Workflow
Generate tenant, transactionally lock enrollment, resolve hashed subject/key
receipt, allocate slot, create company/HQ/owner/link/binding/audit/outbox and
recheck freshness before commit. No new global identity or operational session.
## 9. States
Committed immutable allocation/ownership/receipt. No hard delete or slot reuse.
## 10. Permissions
Payer control permits explicit setup only; no inferred email/domain/module role.
## 11. Rules
Global unique issuer/enrollment/slot and issuer/subject/key hash. Same request
returns committed IDs; changed payload conflicts. Slot must be within lease.
## 12. Data
Core binding stores exact subject/client/payer/enrollment, company/owner, slot,
purchased limit, request/key hashes and proof deadlines. Same-tenant composite
FKs prevent foreign company/owner linkage. Existing Core masters are reused.
## 13. UI
Future own Kuubu protected setup form; no browser tenant/role/quota input.
## 14. UX
Invalid, denied, exhausted, conflict and unavailable remain distinct outcomes.
## 15. Notifications
Transactional core.workspace.created.v1 outbox; no mail or payment side effect.
## 16. Documents
No HR/payroll/financial document or opening balance generated during setup.
## 17. Errors
DB failure/cancellation rolls back every company record; cancellation propagates.
## 18. Integration
Laravel owns commercial allowance; Core owns allocation, companies and grants.
## 19. Audit/privacy
Durable actor/target/slot/allowance/deadlines in transaction. No token or email;
hash idempotency key and canonical request instead of logging request payloads.
## 20. Reporting
Committed bindings count workspaces; sign-in/checkout clicks do not.
## 21. Configuration
Provisioning default off, requiring setup, identity and licensing prerequisites.
## 22. Reliability
PostgreSQL advisory transaction lock serializes enrollment; DB unique keys are
final guards. No network inside transaction. Recheck proofs before commit.
## 23. Postconditions
One company/HQ, credential-free role-free Core owner/link/binding/audit/outbox;
no billing membership, local credential/session or operational grant.
## 24. Recovery
Atomic rollback; receipt handles transport retry. Refuse populated migration
rollback; preserve old tenants and all retained qualifications.
## 25. Acceptance
Retries return same company; concurrent commands cannot over-allocate. Foreign,
expired or zero allowance and conflicting key/payload fail without partial writes.
## 26. Tests
Transaction/idempotency, authority/limit, FK/immutability, failure rollback,
native PostgreSQL concurrency, genuine migration and provider acceptance.
## 27. Release gates
Protected setup OAuth grant, public command/UI, invitation/owner policy, licensed
sessions/revocation, branch/module scope, finite DB ACLs and deployed recovery.
## 28. Evidence
Pending implementation qualification; no production enablement.
