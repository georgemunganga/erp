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
