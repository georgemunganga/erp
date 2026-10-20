# PROC-08-P02 — Controlled issued-order amendments

**Status:** Draft design and guarded proposal source only. Depends on the separately verified PROC-08-P01 initial commercial workflow. No amendment API, migration, activation or deployment is claimed.

## 1. Feature Identification

Procurement → Purchasing → PO Change Management → Propose, review, approve, dispatch and apply a numbered amendment. Tracking: `module-connect#5`; later amendment issue may be split by the delivery owner.

## 2. Purpose & Business Outcome

Change an issued order with explicit source quantity, commercial authority, Finance and supplier-delivery evidence while preserving the terms already communicated to the supplier.

## 3. Scope & Exclusions

Same supplier/company/currency/source-line/unit amendments to quantity, price, tax, descriptions, delivery, payment terms and valid coding. Supplier replacement, changing units, adding unrelated demand, FX, retrospective invoice repricing and implicit Finance adjustments require separate approved routes. The first source slice creates guarded proposals only.

## 4. Parent & Related Features

PROC-08-P01 source allocations, immutable issued versions and execution coordinator; existing PurchaseOrderRevision; Core approvals; Finance adjustment evidence; receiving/invoice exposure controls.

## 5. Actors & Participants

Buyer proposes. Independently authorized commercial approvers decide. Finance independently confirms adjustment/release. Authorized delivery verifier records observed supplier delivery. Worker IDs enforce independence even when accounts differ.

## 6. Entry Points & Triggers

An authorized buyer opens Change Order from a known issued version. An exact command key and current commercial baseline are required; a proposed supplier change is redirected to cancellation/new-order policy.

## 7. Preconditions

Known delivered issued version matches current PO and scope; no conflicting live amendment; proposal line IDs exactly match current active source-backed lines. Proposed quantities cannot fall below accepted or invoiced quantities. Existing allocations and source counters reconcile.

## 8. Workflow

Create an independent revision draft → validate source/receipt/invoice exposures → reserve only positive quantity deltas → confirm an exact external Finance adjustment → open the same Core approval engine using the complete proposed PO value → independent decisions → seal the authorized amendment payload → queue dispatch → observe authenticated delivery → atomically apply the new commercial version and approved source quantity changes. The old issued terms remain authoritative until the defined effective-delivery point. Return/reject releases only the proposal's holds after Finance resolution.

## 9. Status / State Transitions

Revision: Draft → FundingPending → PendingApproval → Approved → DispatchPending → Applied. Alternatives: Returned, Rejected, Cancelled, DeliveryUnknown, DeliveryFailed, Superseded. These belong to the revision record; the original PO remains issued against its current version during proposal review.

## 10. Permissions & Data Scope

Reuse named PO amend/submit/approve/Finance/issue/dispatch permissions and current branch/company record scope. A proposal grants no new viewing rights. Buyer/requester/source-maker and assignment worker evidence is retained; current acting identity and grants are rechecked.

## 11. Business Rules

No direct editing of issued rows. No changing existing Ordered allocations back to Reserved. Positive deltas get separately identifiable Reserved holds; decreases release Ordered quantity only when the authorized new revision takes effect, bounded by accepted/invoiced exposure. The complete proposed amount determines authority; a small delta cannot bypass the approval value. No hardcoded financial thresholds or implicit source/Funds approval.

## 12. Data Fields & Ownership

Reuse Procurement's PurchaseOrderRevision with base issued-version ID/hash, numbered revision, reason, exact before/after snapshots and proposal state. Source delta evidence references existing PR line/allocation IDs and proposed quantities. Core owns execution records; Finance owns commitment adjustments; Procurement owns immutable reference evidence and commercial versions. No new task engine or financial balance.

## 13. Screens & Data Views

Extend existing PO change panel with before/after differences, affected quantities, funding/approval status and delivery state. Show the current supplier-facing version separately from a pending proposal. No new parallel PO screen design.

## 14. User Experience / Human Behaviour

Use plain terms: Change Order, Current Order, Proposed Changes, Waiting for Funding, Waiting for Approval, Waiting for Vendor Delivery. Preserve entered drafts after errors. Explain which existing receipts/invoices prevent a decrease.

## 15. Notifications & Communications

Notify assigned approvers, Finance resolution owners, the buyer and authorized supplier participants. A dispatch request is not a successful communication. No sensitive Finance evidence in email.

## 16. Documents & Attachments

Reuse shared Core documents for justification, revised supplier quotation, Finance adjustment reference and delivery evidence. Retain every sealed commercial version and access-scoped attachments.

## 17. Exceptions & Edge Cases

Concurrent amendments, receipt/invoice growth during review, source demand consumed elsewhere, expired authority, supplier suspension, tax/currency changes, rejected adjustment, ambiguous delivery, same-key conflicting replay and historical offboarding must not alter the current issued payload silently.

## 18. Integrations / APIs / Events / Sandbox

Reuse the same PostgreSQL transaction coordinator and Core approval engine. Stable events distinguish proposed, approved, dispatch-requested, delivery-observed and applied. Finance confirms exact old/new commitments and source reservation deltas; no network calls while locks are held. Synthetic fixtures only.

## 19. Audit / Security / Privacy

Append-only submitted snapshots and decisions; current canonical worker authorization and retained independence; scoped command receipts and exact hash checks. No overwriting issued terms or deleting previous revisions.

## 20. Reports / Dashboards / Metrics

Current commitments separately from proposed adjustments, revision turnaround, held source deltas, rejected changes, unknown deliveries and unapplied approved revisions. Avoid double-counting original and proposed values as actual spend.

## 21. Configuration & Localization

Use published PO policy/ApprovedLineBase and exact currency. Business must define effective delivery/acknowledgment policy, materiality, receipt freezes and Finance adjustment semantics. Missing policy blocks execution, not saving a proposal draft.

## 22. Non-Functional Requirements

One authoritative commercial baseline, atomic apply/rollback, bounded SERIALIZABLE retries and stable exact command receipts. Preserve exact decimal calculation; do not use binary floats. Lock source requests in stable order, PO, then Core case consistently.

## 23. Postconditions

Draft proposals leave live PO, accepted/invoiced quantities, Ordered allocation rows and Finance balances unchanged. An applied amendment has one immutable issued version, exact authorized quantity effects and independently confirmed external evidence.

## 24. Failure & Recovery

Unknown dispatch or Finance outcomes leave the original effective commercial baseline intact. Reconcile by stable references. Exact retries recover the original recorded outcome; conflicting commands fail. Cancellation/rejection releases only verified unused proposal holds and retains all evidence.

## 25. Acceptance Criteria

No active order is silently overwritten. A new receipt can invalidate a proposed decrease. Neither a second account for the maker nor a PR-only grant can approve a PO amendment. Apply-once quantity effects survive races and lost replies. A new revision cannot be based on an obsolete issued version.

## 26. Test Scenarios

Guarded proposal tests: stale/foreign baseline, duplicate/missing/replaced line IDs, changed units/currency/supplier, no-op change, accepted/invoiced floors, decimal deltas and unchanged original records. Later API/PG tests must cover double holds, concurrent receipts/approvals/apply, failed Finance adjustments, return/reject releases, unknown delivery, replay and tenant/branch authorization.

## 27. Ownership / Dependencies

Procurement maintains commercial/source invariants; Core execution/identity/documents are shared; Finance verifies adjustments. Implementation depends on the initial PO milestone's reviewed API, P24, restricted runtime privileges and real API test evidence. No migration is generated in this source-only slice.

## 28. Open Decisions / Assumptions

Confirm supplier acknowledgment versus observed delivery as effective point; define receiving/invoice freezes while a revised commitment is approved but undelivered; define line-level budget transfer/adjustment evidence and partial-cancellation rules. Draft proposal source does not resolve or activate these policies.
