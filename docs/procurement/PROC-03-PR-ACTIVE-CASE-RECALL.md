# PROC-03 Purchase Request Active-Case Recall (BE-03)

**Status:** Draft, default off; no route, UI action, migration or UAT acceptance yet. **Parent:** [PROC-03 Approval and Budget Control](PROC-03-APPROVAL-AND-BUDGET-CONTROL.md). **Scope:** the pending, already-opened Core approval case of one submitted Purchase Request version.

## 1. Feature Identification

`BE-03 / PROC-03-RECALL`, proposed v1. Procurement owns the request and its retained cancellation effects; Core owns the approval case; Finance owns reservation release.

## 2. Purpose and Business Outcome

Let an authorized maker stop a still-undecided approval case without erasing its history, then edit and resubmit only after an independent Finance release is confirmed.

## 3. Scope and Exclusions

This draft covers recall of one pending opened case. Existing pre-case Withdraw, Return and resubmit remain separate operations. Forward, Hold, manual Processed and cancellation after a decision or downstream consumption are excluded. No money threshold, approval-policy shortcut, employee prerequisite or new workflow engine is introduced.

## 4. Parent and Related Features

Use [PROC-02](PROC-02-PURCHASE-REQUISITION-AND-LINE-ROUTING.md), parent PROC-03, the shared Core `ApprovalEngine.Cancel`, Procurement approval-outcome history and the existing independent Finance-release contract. This is a new child operation, not a change to historical P25/P38 migrations.

## 5. Actors and Participants

The current Core principal who made the submitted request, an assigned Core approval participant for case validation, Procurement's transaction owner, the independent Finance release authority and an auditor. An optional dedicated recall permission is an open decision; it is default off.

## 6. Entry Points and Triggers

Proposed server-advertised action on the existing request-detail toolbar, available only when the server has verified the case, actor, scope and release prerequisites. A reasoned command carries expected request/case/source versions and an idempotency key. No new page is needed.

## 7. Preconditions

The scoped request is Submitted with exactly its pinned pending Core case and active assignment. Its maker, policy/source pin, request version, tenant, company and authorized branch still match current Core identity and grants. There is no prior decision, consumed allocation, PO or other downstream source effect. The Finance reservation/reference is retained and resolvable; an unavailable owner fails closed.

## 8. Workflow

Within the owning serializable transaction, lock request, Core case/assignment, submitted source and budget/release rows in a documented order; recheck current authority and all preconditions. Call Core `ApprovalEngine.Cancel` once with the exact reason/key. Persist a typed Procurement `Cancelled` outcome, release request, command receipt, audit and outbox atomically. Return a pending-release acknowledgement. A separate Finance authority later verifies and confirms the release; only then can the request move to editable ReturnedForChanges and be resubmitted as a new version/case.

## 9. Status and State Transitions

Proposed: Submitted + Core Pending → RecallPendingFinanceRelease + Core Cancelled → ReturnedForChanges only after verified Finance release. A request with no confirmed reservation still needs an explicit policy-owned no-reservation result before edit. Approved, Rejected, Returned, already Cancelled and any source-consumed case cannot enter recall. No transition labels funds `Released` on the cancellation command alone.

## 10. Permissions and Data Scope

The server checks current Core session, tenant/company/branch scope, original maker binding and current request edit or explicit recall grant as decided by Core/Procurement owners. Core's cancellation permission and active-assignment checks also apply. No stored worker or employee row is assumed. Reviewer authority alone never grants maker recall.

## 11. Business Rules

One cancellation outcome per submitted version/case; one original key and request digest per command. A prior decision or downstream order/source use is a hard denial, including races resolved after locks. Prior approval, reservation and source records stay immutable. Reapproval uses a fresh submitted version and Core case. Finance alone attests release; retry cannot manufacture a release.

## 12. Data Fields and Ownership

Procurement owns a typed cancellation outcome, source/submission/case pins, reason, actor, expected versions, command body/hash, release request, acknowledgement, audit and outbox. Core owns cancellation decision, stages, assignment and completion key. Finance owns release status/reference and its independently verified confirmation. A genuine new migration after `20261025100650_P38PrincipalSupplierAwardReview` must add the required `Cancelled` CHECK/owned facts and guarded Down; do not edit earlier migrations.

## 13. Screens and Data Views

Use the existing request-detail toolbar and approval history in `reference-ui/src/features/LiveRequests.tsx`. Show server-derived action availability, reason prompt, pending release and final release state. Preserve the current Withdraw/Return/Submit controls and their meaning; the original `RecordDetail.tsx` demo actions are presentation only.

## 14. User Experience and Human Behaviour

Explain that recall stops review but does not immediately free the budget or permit edits. Keep the toolbar action disabled/absent on missing availability, stale data or revoked access. Show the prior case and new submission separately; never optimistically display Released.

## 15. Notifications and Communications

An outbox event may inform affected assignees and the requester after commit, and Finance when a release request is durable. Delivery and templates are separate acceptance gates; a notification failure cannot roll back or duplicate the retained command.

## 16. Documents and Attachments

Retain the submitted version's document/use pins. Recall neither marks an uploaded document Clean nor alters its Core Documents owner. A later edited version must recheck any document requirement under its current policy.

## 17. Exceptions and Edge Cases

Handle two concurrent decisions, maker-versus-reviewer races, revoked branch/grant, wrong case or source version, stale policy, consumed PO allocation, unavailable Finance, duplicate release callback and conflicting key reuse with explicit denials or recovery receipts.

## 18. Integrations, APIs, Events and Sandbox

Proposed scoped POST recall and GET command recovery use existing Procurement/Core transaction and receipt patterns; route names, availability DTO and Finance callback shape remain design decisions. Do not expose a route until mapped schema, Core cancellation replay and Finance owner adapter have actual positive and denied tests. Outbox publishes only committed facts.

## 19. Audit, Security and Privacy

Retain original submission, actor identity, reason, Core decision, request/case/source versions, reservation reference, release request and confirmation with tenant/company scope. Audit/outbox companion checks must reject orphan or altered effects. Do not log private request content or treat an HTTP 200 as proof of Finance release.

## 20. Reports, Dashboards and Metrics

Count pending recall, completed release and reconciliation failures as separate states sourced from owned records. Do not count recalled requests as approved or funds as available until the Finance confirmation is retained.

## 21. Configuration and Localization

The route/capability is default off. Current versioned policy determines whether edit permission suffices or a named recall permission is required; the permission decision is open. Localize reason/status labels through existing UI conventions. No amount threshold is hardcoded.

## 22. Non-Functional Requirements

Lock order, CAS and serializable retry must keep Core, Procurement and release effects consistent. Bounded retries query original-key recovery before any repeat. Owner snapshots prove no writes on denial and no extra effects on replay.

## 23. Postconditions

Successful recall has one terminal Core cancellation, one typed Procurement outcome and one durable release request tied to the original submission. The request is not editable until an independently verified release/no-reservation resolution is retained. A later resubmit gets a new source version and case.

## 24. Failure and Recovery

Unknown command response is recovered by original key and exact body; conflicting reuse denies. Finance timeout leaves pending release and blocks edit/resubmit. A transaction failure leaves the request/case/reservation unchanged. Down migration refuses retained cancellation facts, audit or outbox history before any destructive schema step.

## 25. Acceptance Criteria

Existing pre-case Withdraw, Return and resubmit tests remain green. A current maker with eligible pending case can recall once and recover exact acknowledgement; a real independent Finance confirmation precedes edit/resubmit. Cross-scope, stale, revoked, decided, consumed and mismatched-source attempts cause no effects. The new migration rehearses empty Up/Down/Up and populated-history Down veto with constraints enabled.

## 26. Test Scenarios

Test UI server-derived availability and reason; API positive, exact replay, conflicting key and lost response; Core maker/current grant/branch negatives; decision race and version CAS; wrong source, confirmed reservation and no-reservation paths; Finance outage/duplicate/foreign confirmation; owner before/after graph and no-effects denials; migration CHECK, FK, retained audit/outbox and Down teardown. None is marked executed by this draft.

## 27. Ownership and Dependencies

Procurement: state machine, source guards, outcome, receipt and UI adapter. Core approval owner: cancellation/identity/assignment contract. Finance owner: authoritative release and independent confirmation. DB owner: genuine post-P38 migration and guarded rollback. QA: actual private HTTP, browser, owner and schema evidence before any release claim.

## 28. Open Decisions and Assumptions

**Procurement governance + Core access owner:** edit permission versus a new explicit recall action, default off until agreed. **Core approval owner:** maker/assignment selection and retained cancellation replay. **Finance budget owner:** release request/confirmation protocol, no-reservation evidence and timeout reconciliation. **Procurement + DB owner:** final intermediate status, route/availability DTO and migration shape. **Product owner:** whether Forward/Hold/Processed belong to later bounded slices. These decisions are not approved by this draft.

### Existing source subset and evidence boundary

- Pre-case Withdraw: `backend/hrm-api/src/Mightyfin.Erp.Hrm.Api/ProcurementRequestRoutes.cs:187-239`; Return and resubmit: same file `:241-370`. Existing UI: `reference-ui/src/features/LiveRequests.tsx:327-413` and `LiveApprovals.tsx:156-225`.
- Core cancellation primitive: `backend/core-api/src/Mightyfin.Erp.Core.Approvals.Domain/ApprovalEngine.cs:49-91`. Open/decide/Finance routes: `backend/hrm-api/src/Mightyfin.Erp.Hrm.Api/ProcurementApprovalExecutionRoutes.cs:18-35,155-245`.
- Current Procurement CHECKs and outcome/release rules exclude cancellation: `backend/procurement-api/src/Mightyfin.Erp.Procurement.Infrastructure/Data/ProcurementApprovalOutcomeMapping.cs:31-34,61-65`; `backend/procurement-api/src/Mightyfin.Erp.Procurement.Application/PurchaseRequestApprovalOutcomes.cs:27-142`. Latest retained P38 migration: `backend/procurement-api/src/Mightyfin.Erp.Procurement.Infrastructure/Migrations/20261025100650_P38PrincipalSupplierAwardReview.cs`.
- The [BE-03 gap plan](gap-closure/PROCUREMENT-IMPLEMENTATION-AND-UAT-PLAN-20261025.md) remains incomplete; its UAT register remains `NOT_EXECUTED`. Source presence does not establish active-case recall, Finance release or live acceptance.
