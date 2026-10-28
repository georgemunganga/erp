# M5b receiving implementation — 28 October 2026

Status: implementation and verification in progress; not a production release or whole-module UAT sign-off.

## Scope and ownership

Root owns this milestone. It connects the original receiving UI to Core-principal goods drafts, revisions/cancellation, physical observations, inspection, independent acceptance and service certification, using retained M5 issued-order evidence. Employees are optional aliases; current Core permissions and branch scope are authoritative. The existing PROC-09 28-section specification remains the business reference. Returns, reversals and downstream ledger posting are separate contracts and are not claimed implemented by this slice.

ERP baseline: `bbc9404dfdb68ffaa33bd49246b9734b641a4eac`.
UI baseline: `f1e237713d5f803212ec2ae72773be842ab0e3b6`.

## Implemented source

- Six owning receiving tables, persistence guards and immutable evidence/receipt records; original source graph is reverified, and unexplained legacy receiving history blocks adoption.
- Genuine EF migration `20261028160000_AddPrincipalReceivingExecution`, generated from the current model. Eleven deferred ownership constraints and supporting indexes preserve source relationships. Down locks and vetoes any retained receiving graph/audit/outbox evidence; no balance reset or history deletion.
- Dedicated physical/schema/finite-role readiness composes existing M5 read proof. Configured and enlisted connections are checked; new-work shutdown does not strand read/recovery.
- Principal-scoped API and metadata, Core-principal receiving policy validation/lookup, separate observation/inspection/acceptance, exact-decimal quantity/counter checks, idempotency, audited transitions and explicit Procurement-owned outbox effects.
- Company All is supported for the five reviewed collections. Writes require a selected branch and current authority. An acknowledgement is supplier-response evidence, not proof of delivery.
- Original UI receiving adapters retain ERP controls/layout, explicit legacy protocol, branch-aware navigation and a session-scoped uncertain-save journal. Original-command recovery uses GET and never silently reposts a write.

## Verification and release holds

The first source snapshot passed all 26 focused receiving domain tests in an offline, resource-bounded SDK container. Subsequent receipt-privacy and migration integration changes require follow-up tests. UI typecheck/contract/browser verification is underway; an actual UUID journal validation failure was found and is being corrected. Do not count unrun checks as passing.

Required before rollout: current host/domain tests, genuine EF pending-model check and migration SQL, native nonempty Up/empty Down/retained-evidence veto rehearsal, fresh physical CHECK capture, finite runtime permission tests, authenticated real API/browser receiving journey and reconciliation, paired deployment/rollback evidence. Production is unchanged. Core company business-date handling for service period end, returns/reversals, Inventory/Assets and Finance posting are not asserted complete.

## Verification checkpoint — current source

The current API builds successfully. The final focused host run passed **58/58**, and the corrected follow-up receiving domain run passed **27/27**, including receipt privacy and corruption checks. Earlier compiler/test-fixture failures remain in the private evidence and are not counted as passing attempts. EF reports no pending model changes and generated the official Up/Down scripts; the API publication contains 150 files.

On an isolated, nonempty M5a database clone, official Up passed exact physical verification for all six tables/CHECKs, eleven deferred ownership bindings and five parent indexes. All 211 retained table fingerprints were preserved, allowing only the exact new migration-history row. Empty Down restored all 211 retained fingerprints; re-Up restored the exact captured receiving footprint. An uncommitted audit-only fixture produced the expected SQLSTATE 55000 downgrade veto, and disconnect rollback removed that fixture while preserving the original footprint. This tests the audit-only veto, not every possible retained business-graph marker.

The original receiving UI transport passed Node22 TypeScript, 99 focused unit/contract checks and 27 mocked browser cases. A subsequent Core-account receiving-policy editor increment is under separate verification; do not apply the earlier UI receipt to untested increments.

Remaining admission holds: finite runtime grants on the isolated clone, current Core role fixtures for receiving actors, real-cookie positive API/owner reconciliation, final settings UI verification, service business-date handling and subsequent return/reversal integration. Production remains unchanged; no deployment or whole-module acceptance is claimed.

## Receiving settings UI checkpoint

UI PR91 now includes explicit new-draft selection of Core-account receiving policies, named Core-account pickers for inspectors/acceptors/service owners and preserved schema-1 employee-policy editing/history. It uses the original form and ERP controls. Current UI source `0145bce` passed Node22 TypeScript, **607/607** unit/contract tests and the production build. The combined focused browser run passed the 27 receiving scenarios and five settings scenarios; one new settings test used the wrong assertion for the custom Select. After a test-only correction, all **6/6** settings cases passed. This is not represented as a single33-green invocation or a full444-scenario rerun.

On the isolated clone, finite receiving execution grants and three synthetic-only role fixtures have been applied under exact target/CAS guards. Core accounts remain workerless. The genuine normal-cookie API has completed receiving policy creation/review/publication and is waiting for the effective policy window before the Goods workflow. That running test is not acceptance; owner reconciliation and fixture/session cleanup remain required.

## Accepted private Goods workflow and cleanup

The broader Procurement backend suite passed **1,478/1,478**. The normal-cookie Goods API test passed policy create/review/publish, receiving Create/Revise/Record/Inspect/Accept, original-key GET recovery and denied/stale/overcapacity probes. An independent read-only owner verifier accepted the five command receipts, three typed facts, one retained M5 source chain, exact counter/net deltas, policy identity lineage and audit/outbox graph while preserving old rows outside the declared effects and captured schema/table ACLs.

The initial owner verifier rejected five system-auth activity audits. A constrained successor used published authentication/interceptor source plus a read-only key-only supplement: each audit changed only LastSeenAt on one of the five exact owned sessions after the five-minute refresh interval. All other source/quantity/row/ACL checks remained intact. Initial failure evidence is preserved; no business command was replayed.

All five cookies were logged out, zero active cohort sessions were observed, the private API/proxy were stopped and all three synthetic receiving role fixtures were restored. The complete tenant-role-assignment fingerprint matches the pre-fixture baseline. Private clone ACL additions remain documented for reproducibility. Production was not changed.

Acceptance is **Goods API single-source/captured-footprint only**. It does not establish real service/correction/return workflows, end-to-end live UI forms, every branch/tenant scenario, all audit/default/function privileges, Finance/Inventory posting, production readiness or whole Procurement UAT. Service business-date and returns/corrections remain next implementation work.
