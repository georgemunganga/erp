# PROC-08-P01 — Controlled standard purchase-order execution

**Status:** Proposed source implementation contract; not deployed or enabled. **Date:** 20 October 2026. **Hierarchy:** Procurement → Purchasing → Standard Purchase Order → Convert / Submit / Approve / Issue / Revise. Complements PROC-08; existing draft CRUD remains available.

## 1. Feature Identification

PROC-08-P01. First supported commercial route: approved direct-buy demand to a standard PO, using existing Core approval execution and Finance-owned commitment evidence. Excludes blanket/framework/award conversion until their authority contracts are implemented.

## 2. Purpose & Business Outcome

Consume each approved source quantity once, obtain independent PO authority and confirmed Finance commitment, retain an immutable commercial version, and expose the actual delivery outcome.

## 3. Scope & Exclusions

Includes line allocation, draft price/quantity review, submission, Core approval, exact retries, immutable issue/revision, dispatch recovery and controlled cancellation. No payment, budget balances, supplier activation shortcut or invented financial threshold.

## 4. Parent & Related Features

Uses PROC-01 supplier eligibility, PROC-02/03 approved request evidence, CFG policies/methods, Core identity/roles/approvals/transaction coordinator, shared documents/outbox and Finance evidence contracts.

## 5. Actors & Participants

Buyer, independently authorized PO approver, Finance verifier, supplier-delivery operator/service and auditor. Buyer, source requester and Finance verifier conflicts are checked explicitly.

## 6. Entry Points & Triggers

Approved-line picker into existing buyer PO draft; submit; Finance commitment evidence; assigned task decision; issue request; delivery confirmation; amendment and cancel request. Existing UI is extended in place later.

## 7. Preconditions

Same tenant/company/currency; current approved source case/outcome/snapshot; nonnegative reconciled source counters; active procurement method explicitly configured as DirectPurchase with zero required quotations; eligible Active supplier. Other methods remain visible but cannot bypass sourcing.

## 8. Workflow

Lock source requests in stable order before the PO and Core case. Reserve available quantities and create/update source-backed draft atomically. Pin submitted commercial fields, sources, published PO policy and authoritative Finance commitment. Open/decide using Core engine and shared serializable coordinator. Approval alone does not dispatch. Issue seals a version and enqueues delivery; only authenticated known delivery success advances IssuePending to Issued. Unknown/failed dispatch stays actionable and retry-safe.

## 9. Status / State Transitions

Draft → PendingApproval → Approved → IssuePending → Issued. Returned revisions require controlled editing and resubmission; rejected demand stays non-orderable. Issued terms are immutable: amendment creates a numbered proposed revision, rechecks allocation/Finance and repeats approval before replacing the current issued version. Receipt/invoice progress remains separate.

## 10. Permissions & Data Scope

Existing buyer-owned draft read scope is retained. New explicit convert/submit/approve/issue/amend/dispatch-confirm permissions are server-side, company/branch scoped and revalidated against current canonical identity. Assigned-task access is independent of buyer record ownership; no self approval or role-implied amount limit.

## 11. Business Rules

Available quantity = approved − cancelled − allocated − ordered; accepted/invoiced quantities are subsets, not additional deductions. Allocation rows and counters must reconcile. Reservation moves to ordered once at known commercial issue; duplicate commands never consume twice. PO quantity cannot exceed approved source authority. Policy approval thresholds evaluate the full PO line-base total for every applicable category, preventing line splitting from lowering the route. Price delta requires the PO's explicit full-value approval and sufficient confirmed Finance commitment. No free-form draft may issue without a separately approved exception route.

## 12. Data Fields & Ownership

Reuse PurchaseOrder, PurchaseOrderLine, PurchaseRequestAllocation and PurchaseOrderRevision. Procurement adds immutable commercial submission/source/policy/Finance reference evidence and command recovery/dispatch facts. Core retains cases/tasks/decisions; Finance retains commitment and balance truth. No second approval engine or Finance ledger.

## 13. Screens & Data Views

Extend existing approved-line picker, buyer draft/detail, Approvals, revision history/diff and Finance status panels. Show proposed/approved quantities and amounts, tax-inclusive Finance amount, actual dispatch state and next owner.

## 14. User Experience / Human Behaviour

Plain-language blockers explain missing approval, remaining quantity, expired supplier checks, configuration, Finance evidence or delivery failure. Loading, empty, stale, denied and exact-retry recovery states use shared ERP controls.

## 15. Notifications & Communications

Durable outbox facts for allocation, submission, approval, issue requested/confirmed/failed, revision and cancellation. Notification failure cannot erase a committed decision; delivery success is never inferred from queue insertion.

## 16. Documents & Attachments

Seal exact approved commercial payload/checksum per revision and link shared document service evidence when rendering/delivery requires it. Supplier-facing document generation and dispatch must use the sealed revision, not mutable live fields.

## 17. Exceptions & Edge Cases

Concurrent buyers, split line allocations, partial orders, suspended supplier after approval, changed source unit/currency, stale authority, Finance revocation, unknown dispatch, competing amendments and receipts after issue must fail safely or remain visibly pending.

## 18. Integrations / APIs / Events / Sandbox

Existing `/orders` draft DTOs remain compatible; additive typed source-line references and new command routes use UUID idempotency keys/revisions. Finance proof is server-resolved through approved integration/manual policy, not caller-declared confirmation. Sandbox uses synthetic evidence and Core cases, never real supplier dispatch or funds.

## 19. Audit / Security / Privacy

Record actor, reason, source/case/policy/Finance versions, delta, immutable payload, dispatch reference and correlation. Preserve cancellation/reversal history; no physical deletion. Restrict evidence/exports and cross-company task URLs.

## 20. Reports / Dashboards / Metrics

Allocated versus available demand, pending approvals/commitments, unissued/failed deliveries, current revision and commercial balance. Report currency/basis and exclude drafts from actual spend.

## 21. Configuration & Localization

Explicit procurement-method execution route defaults Unconfigured. Published PO policy uses existing ApprovedLineBase; Finance coverage includes captured tax and all supported commercial costs. PO authority action/basis is added to the existing grant catalogue while preserving strict PR action validation. No FX, guessed taxes or sample limits.

## 22. Non-Functional Requirements

One physical PostgreSQL transaction spans source allocation, Core execution, source outcome, audit and outbox. Fresh contexts and bounded confirmed-abort retries reuse command keys; no network side effects inside locks. Exact decimal calculations preserve eight places where four-place quantities/prices multiply.

## 23. Postconditions

Each live PO line has traceable approved source allocation; approved and issued states have immutable policy/Finance/actor evidence. Issued commercial payload is retained and dispatch state is known independently.

## 24. Failure & Recovery

Lost responses replay saved fingerprinted outcomes. Changed same-key payload conflicts. Unknown Finance/delivery outcomes are queried/reconciled by stable reference; no new commitment or supplier order is created automatically. Cancellation releases only valid remaining source quantity and requires Finance release when committed.

## 25. Acceptance Criteria

Unapproved/exhausted/foreign demand, ineligible supplier, missing authority/policy/commitment and mismatched unit/currency cannot issue. Concurrent allocation cannot exceed approved quantity. Full approved issue retains one immutable version/effect per command; material revisions cannot bypass approval.

## 26. Test Scenarios

Test exact/conflicting conversion/revision/issue retries; source counter reconciliation and races; source value delta; supplier suspension/qualification expiry; permission, maker/requester and tenant/branch denial; Finance gross-amount mismatch/revocation; same-connection rollback; concurrent Core quorum; dispatch unknown/success/failure; immutable issued versions and stale amendment rejection.

## 27. Ownership / Dependencies

Procurement owns commercial invariants and supplier engagement, Core owns task/identity mechanics, Finance owns commitments, and deployment owner provisions schema/grants/integrations. Production enablement requires independent review and evidence after source implementation.

## 28. Open Decisions / Assumptions

Approved direct-buy method codes, PO route/authority, manual Finance policy/freshness, tax/document templates, dispatch integration and amendment/cancellation release rules require business configuration. Missing decisions block commercial issue, not existing draft CRUD. This document does not claim those configurations or the workflow are complete.

### Implementation record — PROC-M5 / module-connect#5 (source only)

The isolated `codex/procurement-order-execution` branch has candidate source for:

- Existing-action-compatible PR/PO spending authority contracts, with mutually exclusive permission/basis matching.
- Full PR line allocation evidence and source-version guards for direct-buy conversion, preserving standalone draft CRUD.
- PO commercial submission, Core stages/assignments/decisions, assigned queue and authorized history; no second approval engine.
- Exact PO snapshot/gross-amount/source-reservation Finance confirmation under explicit deployment-owned manual policy.
- Sealed issue authorization, one outbox effect and separate independent authenticated delivery observation. `IssuePending` is not `Issued`.
- Immutable worker identities from original PR pins and PO assignment envelopes. Different accounts for one worker cannot provide independence.
- Return/reject original-evidence verification, plus explicit Finance release/restoration evidence before reopening a returned draft or cancelling a rejected order. Historical offboarding does not require resolving old approver accounts to perform the independent Finance resolution.

Candidate typed HTTP contracts are in `ProcurementOrderExecutionContracts.cs`. Routes are defined separately in `ProcurementOrderExecutionRoutes.cs` and composed behind **off-by-default** `Procurement:Orders:Enabled`. Every candidate command checks the dedicated execution connection and actual scoped schema/access readiness before opening a transaction. Metadata additionally requires one current approved Finance evidence policy and approved delivery policy/channel configuration. No deployment option, database migration, production data or feature activation is changed by this record.

Numbered post-issue amendments remain a subsequent source increment: the currently issued payload must stay authoritative while a proposed delta is reviewed; reserved positive deltas must not reclassify an existing ordered commitment. Existing order editing rejects any approved/submitted/issued state. This guard is not a claim that the amendment workflow has been implemented.

Before composition/enablement: compile/full Procurement and HTTP tests; inspect/generate separate P24 after P23; isolated PostgreSQL apply-twice/rollback/replay/concurrency and scoped-constraint tests; restricted runtime grants/schema readiness; existing buyer/approver UI adapters and real API Playwright. All new candidate source remains unverified until these gates produce evidence. No completion or deployment claim is made.
