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
Runtime source `3e967e965217800c0996b3d9c0bd23cc79e810af` passed197
focused Linux tests (four PostgreSQL-only skips),47 genuine PostgreSQL cases
with zero skips and2,168 full Linux regressions with zero failures. Five opt-in
cases were skipped in the normal full run: four were exercised in the47-case
native cohort, and the genuine provider protocol case passed separately on
Windows against actual Laravel and the published Kuubu client/relay.

Windows runtime `dae2c3409121c90b7448850b7036903a787494b9` passed146
focused cases (four native-only skips) and the real protocol case. The final
source differs only in the older federated rollback test: it now selects its
owning migration by name instead of assuming that migration is latest. No
production code changed between those Windows and final Linux runs. The first
native attempt retained46 passes and that one stale test failure; all new
allocation cases passed, then the corrected complete47-case cohort passed.

Native cases prove two concurrent allocations within a two-workspace allowance,
ten denied excess creates, twelve retries producing one company/audit/outbox,
cross-tenant FK denial, immutable bindings, no direct legal-entity quota bypass,
classic multi-company compatibility and populated rollback refusal. Post-save
failure, expiry and cancellation roll back all writes. Independent read-only SQL
audit of38 synthetic databases/26 committed allocations verified the three
triggers, two validated FKs, four unique indexes, capacity check, same-tenant
company/owner/link, no passwords/module roles/sessions and exactly one audit/
outbox pair per committed workspace. Counts cover both retained native attempts.

Migration `20261107231550_CoreWorkspaceAllocations` was generated with EF10.0.11,
then explicit immutability/company-count/rollback guards were added. No model
snapshot drift appeared on genuine migration. SDK10.0.403 image remains pinned
to `sha256:212fe4cb74627d5adf9817a4088906ae3edff1bd853bb27565d4b46731efe61e`.
Source archive SHA256:
`85d32af26cf45bf870fd0b52b5fe80466ab4de248f1464a978e239720f9729a4`.
Linux focused log:
`b8c85b822cb13892f359ef5364d4f17702713b373bd58f57cf30684abf5a3d1e`.
Native log:
`369e0869f7ed609846df0e5a57aa27f9345a0810eee5df41d2b64153fc75c3a9`.
Full log:
`cc47ee95b0dc5628a1ad77eeba29b2f9ed3d67f4c504c0a392aae9546b9cea51`.
Windows focused log:
`face404ce50b6df2e5622348cda8e8b58525d926cbfe2f78b7dc66013b1dcd9f`.
Actual provider log:
`c28e5034a7d01cb135166bbb432ab785a35d0382575eb5e23e6d767d379f1f72`.

Actual protocol uses foundation2ebee45 and websiteb01985ee with a fresh guarded
fixture explicitly seeded with two purchased workspaces. Audit confirmed two
identities, one customer/subscription, zero billing memberships and no auth-
created ERP user/link/session. This case proves identity/licensing capacity
interoperability; it does not call company creation using genuine setup OAuth.
That protected grant/consumer/command/UI journey remains a release gate.

Retained native workspace `/opt/kuubu-builds/workspace-allocation-3e967e96-02`;
SDK and isolated PostgreSQL containers exited and the test DB returned to stopped.
Local fixture processes stopped and ports8101/8120 closed. All source/cache/
evidence copies are independent; prior attempts/artifacts remain unchanged.
No production migration, image, key/client, flag, DB grant, DNS, payment or
Perfex change. Existing private API remains on image3ff07382 and health/ready200.
