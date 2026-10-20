# PROC-08 Purchase Orders and Amendments

**Status:** Draft for business review · **Version:** 0.1 · **Hierarchy:** Procurement → Purchasing → Purchase Order Lifecycle → Operations · **Framework:** [ERP 28-section standard](../hrm/feature-specifications/ERP_Feature_Specification_Framework_Enterprise_Integration_Updated.docx) · **Module:** [PROC-00](PROC-00-PROCUREMENT-MODULE-ARCHITECTURE-AND-CAPABILITY-MAP.md)

**Implementation boundary (20 October 2026):** The first technical slice lets a scoped buyer create, view, revise and cancel an internal PO draft against an active vendor. Lines can be retired with a reason while their stored history remains. Drafts do not imply an approved source, budget reservation, commercial commitment or supplier dispatch. Submission, approval, issue, amendment of an issued PO, receiving, and Finance commitment are governed later milestones; they must validate the source and authority described below before activation.

## 1. Feature Identification

`PROC-08`, purchasing phase. Business owner: purchasing lead; product/data/technical owners to name. Operations: `O1` convert approved demand, `O2` approve/issue, `O3` acknowledge, `O4` amend, `O5` blanket release, `O6` cancel/close/reconcile.

## 2. Purpose and Business Outcome

Issue a controlled commercial commitment from approved demand and terms. Success is an immutable supplier-facing PO version, complete source authority and accurate open quantity/value/commitment across revisions, receipts and invoices.

## 3. Scope and Exclusions

Includes standard PO, optional blanket/standing PO and releases, approval, dispatch, acknowledgment, versioned change order, cancellation and closure. It does not create a Finance ledger, inventory stock, receipt or invoice. Blanket PO and direct-buy routes enable only after their separate policy gates.

## 4. Parent and Related Features

Parent Purchasing; consumes PROC-02/03 approved lines and funds, PROC-01 active supplier, PROC-06 award or PROC-07 contract where applicable, CFG policy. PROC-09 receipts and PROC-11 matching consume issued PO.

## 5. Actors and Participants

Buyer, PO approver, supplier contact/portal user, requester, Finance commitment owner, receiver, AP reviewer, workflow and dispatch operators, auditor.

## 6. Entry Points and Triggers

Approved line direct route, sourcing award, contract/framework release, blanket release request, supplier acknowledgment/change request, amendment, late delivery, cancellation or close.

## 7. Preconditions

Approved available line/award/contract authority, eligible supplier at issue, valid entity/coding/currency/tax references, Finance funding evidence, delivery/billing details and required PO approval. Duplicate source conversion is blocked.

## 8. Workflow

`O1`: atomically allocate approved source lines and draft PO with supplier/terms snapshot. `O2`: validate totals and materiality, approve, generate immutable document, dispatch and log result. `O3`: capture acknowledgment/rejection/changes. `O4`: propose numbered delta, recheck authority/funds, approve and issue revised version. `O5`: create release against active blanket ceiling/period. `O6`: reconcile receipt/invoice and unused commitment, cancel valid remainder or close with reason.

## 9. Status and State Transitions

Commercial: Draft → Pending Approval → Approved → Issue Pending → Issued → Acknowledged → Closed; alternatives Returned, Change Pending, On Hold, Cancelled, Expired, Disputed. Receipt and invoice progress are separate dimensions, not commercial status replacements. Each transition identifies actor, approved source/version, evidence, SLA, notification and recovery. Issued version is immutable.

## 10. Permissions and Data Scope

Buyer drafts within assigned entity/category; distinct approver issues authority; supplier sees only its issued PO; receiver can record receipt but cannot alter commercial terms. Cancel/amend requires explicit scoped permission and separation rule. Supplier bank data is not shown on PO views by default.

## 11. Business Rules

`BR-PROC-PO-001` no issue without approved source or documented direct exception; `002` supplier Active at issue; `003` source quantity/value and blanket ceiling cannot be exceeded; `004` material change creates revision and reapproval; `005` no dispatch before approval; `006` closure requires fulfillment/invoice disposition and commitment release; `007` every issued version is retained.

## 12. Data Fields and Ownership

Procurement owns PO ID/number, source line/award/contract refs, supplier/site/address snapshot, entity, buyer, ship/bill-to, line qty/UOM/price/tax reference, currency, terms, delivery schedule, revision, approval, dispatch and acknowledgment. Finance owns commitment; Procurement stores reference/status. Blanket PO owns ceiling/period/releases; framework contract remains separate.

## 13. Screens and Data Views

Approved-demand conversion, PO draft/approval, issued document, revision diff, supplier dispatch/acknowledgment, blanket balance/releases, open receipt/invoice balance, cancellation impact and reconciliation queue.

## 14. User Experience and Human Behaviour

Display source-approved versus proposed values and highlight deltas. Show supplier exactly which version is current; show buyer dispatch failure without pretending order was received. Closure warns about open receipts/invoices and remaining commitments.

## 15. Notifications and Communications

Approver task, supplier issue/revision/cancellation, acknowledgment request, buyer on rejection/dispatch failure, receiver before expected delivery, Finance on commitment adjustment. External messages include only permitted terms.

## 16. Documents and Attachments

Issued PO PDF, terms, award/quote refs, supplier acknowledgment and revision evidence are scanned/versioned/retained. Each dispatch refers to exact approved document checksum/version.

## 17. Exceptions and Edge Cases

Supplier suspended between draft/issue, PR line converted concurrently, supplier requests altered terms, partial receipt before revision, currency/tax change, duplicate dispatch, blanket ceiling race, cancellation with invoice in flight.

## 18. Integrations, APIs, Events and Sandbox

Finance commitment check/adjust/release, supplier delivery/portal, source-line allocation and downstream receipt/match contracts. Emit `PurchaseOrderApproved`, `PurchaseOrderIssued`, revised/cancelled facts after commit with tenant/entity, PO/source/version, correlation/idempotency. Sandbox simulates dispatch unknown outcome, blanket race and supplier rejection.

## 19. Audit, Security and Privacy

Append source authority, approval, field delta, issued document, dispatch attempt, acknowledgment and close reason. Enforce tenant/entity/supplier/field scope for APIs, search, attachments and exports; log privileged changes.

## 20. Reports, Dashboards and Metrics

PO cycle time, unissued/unacknowledged orders, open commitment, overdue delivery, revision frequency, blanket utilization, cancelled value and purchase price variance with declared currency basis.

## 21. Configuration and Localization

PO number series, approval materiality, direct/blanket eligibility, ceiling/tolerance, delivery/payment terms, tax reference, template, locale/currency and supplier delivery channel are effective-dated by entity.

## 22. Non-Functional Requirements

Conversion and blanket release are concurrency safe; issue/dispatch retries idempotent. PDF and delivery failure observable; accessible review and version diff. Latency/volume targets await launch data.

## 23. Postconditions

Issued PO has one current approved immutable version and exact source/funding references; supplier delivery state known; remaining balances and commitments reconcile after revisions/closure.

## 24. Failure and Recovery

Unknown dispatch result is checked before resend with same key. Finance adjustment failure blocks issue/closure or leaves visible pending reconciliation. Failed source allocation compensates without over-consuming line balance.

## 25. Acceptance Criteria

Given unapproved PR or suspended supplier, issue fails. Given price/quantity delta above policy materiality, revision requires reapproval. Given duplicate issue retry, one commercial version exists. Given blanket limit reached, further release blocks. Given partial receipt, only valid remainder can cancel.

## 26. Test Scenarios

Test PR/RFx/contract conversion, approval, dispatch/retry, acknowledgment, multiple revisions, supplier suspension, concurrent source and blanket allocation, partial receipt/invoice, entity isolation and commitment release failure.

## 27. Ownership and Dependencies

Purchasing lead owns commercial process; Procurement owns PO; Finance owns commitment, supplier owns response. Depends on PROC-01/02/03/06/07 as applicable and CFG; Receiving/Invoice consume PO.

## 28. Open Decisions and Assumptions

Confirm PO approval versus PR authority, direct buy, blanket PO launch, legal terms/templates, dispatch channel, amendment materiality, acknowledgment requirements and close/release rule. Owners: Procurement, Legal and Finance; resolve before implementation.
