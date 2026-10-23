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

## P30 delta — Core-principal direct PO draft (source only, 23 October 2026)

This is the bounded draft successor to the retained worker PO flow. P29 and P25
are prerequisites; P30 is not an issued PO, Finance commitment, or new buying
policy. The existing `PROC-07` document concerns contracts, so this PO delta
belongs here in `PROC-08`.

1. **Identification:** `PROC-08/O1/P30`, direct-purchase approved-demand conversion.
2. **Outcome:** a workerless Core buyer can reserve an approved direct-purchase
   line in one internal PO draft and later release it without losing evidence.
3. **Scope:** picker, create, owned read/list, cancel, and actor-owned command
   recovery. Submission, Finance confirmation, approval, issue, dispatch,
   amendment, receipt and payment remain unavailable for this evidence version.
4. **Relations:** consumes current P25 Submission3, outcome, Core decision,
   checker2, direct-purchase policy route and supplier eligibility; retains
   the existing PR allocation ledger and PO/line masters.
5. **Actors:** current Core buyer, separate original PR maker/checker/approver,
   auditor. An HRMS worker link is optional alias metadata, not eligibility.
6. **Entry:** buyer opens scoped approved direct sources or a source-backed PO
   draft. No generic unlinked draft is converted implicitly.
7. **Preconditions:** explicit company/current branch/action, approved source,
   current manual checker authority, direct method with zero required quotes,
   eligible supplier, exact currency/UOM and source quantity. Capability is off
   until the genuine migration, finite role and integration acceptance exist.
8. **Workflow:** read candidate → lock requests in stable order → revalidate
   original/current source, supplier and amount → reserve linked quantities,
   immutable source pin and Create receipt atomically → read → cancel with
   reason/CAS and release exact quantity → Cancel receipt.
9. **States:** `Draft → Cancelled`; no other transition for P30 records.
10. **Permissions/scope:** separate `procurement-po-convert`, `-read`, `-edit`
    grants and current Core company/branch scope on every read/write/recovery;
    buyer sees own draft only. A stale HR link never widens authority.
11. **Rules:** PR approval is source authority only, never PO approval. No
    over-allocation, mixed company/currency/branch, inactive supplier, self-
    asserted checker/worker or silent supplier substitution. Existing worker1
    PO evidence keeps its original encoder and validity.
12. **Data/owner:** Procurement owns a versioned original direct-source binding
    per PO line, its current PO draft, allocation, audit, outbox and immutable
    command receipt. Core owns user/branch/action; Finance owns commitments.
13. **Views:** exact eligible source picker, owned draft/history and cancel
    impact. No Finance or supplier-facing badge from an internal draft.
14. **UX:** show approved versus proposed quantities and why a candidate needs
    review; unknown write prompts read-only receipt recovery before retry.
15. **Notifications:** none required for this bounded internal draft slice.
16. **Documents:** no issued PO PDF or supplier dispatch document is generated.
17. **Exceptions:** stale PR/PO CAS, concurrent RFQ/PO reservation, expired
    checker policy, suspended supplier, changed branch/grant, mismatched receipt
    key, missing retained source and failed database write all fail closed.
18. **API/events:** versioned `/orders/principal-direct-*` endpoints and
    internal draft/release events only; no Finance, AP or delivery event.
19. **Audit/security:** original source IDs, policy/finance digests, actor,
    reason, command key and old/new quantity remain reconstructable. Generic
    draft edit/cancel cannot modify P30 source-controlled records.
20. **Reports:** P30 drafts and released quantities may be counted as internal
    demand work; they are not committed or paid spend.
21. **Configuration:** dedicated default-off principal direct-source setting;
    existing direct method/quote policy supplies business limits.
22. **Non-functional:** serializable shared transaction, idempotent exact
    response, source locks, decimal arithmetic and scoped paging.
23. **Postconditions:** Create has one PO, one binding and reservation per
    source line, and one command receipt;
    Cancel has the same retained graph with released allocation and one new
    receipt. The PR line counter reconciles after each command.
24. **Recovery:** actor/current-action/current-branch scoped GET of recorded
    receipt; no guessed replay or second command on unknown outcome.
25. **Acceptance:** genuine workerless direct PR → Create/read/Cancel/recover;
    competing reservation cannot exceed source; no downstream PO/Finance facts.
26. **Tests:** pure source/binding invariants, mapped scope/CAS guards, actual
    finite-role PostgreSQL/HTTP and real local-cookie browser after migration.
27. **Dependencies/owners:** Procurement draft owner; P25/P29, Core and
    supplier owner prerequisites. Root owns generated migration and release.
28. **Open:** whether line-level edit and later principal PO approval/Finance
    successor are authorized. They are excluded until separately specified.

## P30 governed successor delta — Core-principal PO authority through manual acknowledgment

This is a source specification for the successor after the internal draft slice
above. It does not activate endpoints or reinterpret PR approval as PO approval.

1. **Identification:** `PROC-08/O2/P30`, commercial approval and issue for an
   original schema-2 direct PO draft. Worker-era commercial evidence stays schema 1.
2. **Outcome:** current independent Core people can attest external commitment,
   approve the PO under its own published policy and spending grant, freeze an
   issued version, and record a witnessed supplier acknowledgment.
3. **Scope:** Finance-reference recording, submit, assigned decisions, issue,
   manual acknowledgment and actor-owned recovery. Payment, AP, bank posting,
   supplier messaging and dispatch automation are outside this increment.
4. **Relations:** P30 direct-source bindings, current PR Finance checker2,
   `PURCHASE_ORDER` policy, PO-specific `PurchaseOrder.Approve` grants, shared
   Core approval case/stage/assignment/decision and Procurement outbox/audit.
5. **Actors:** original Core buyer, independent Finance recorder, explicitly
   assigned independent PO approver, authorized issue actor, independent
   acknowledgment recorder. HR worker links are optional denial-only aliases.
6. **Entry:** only a verified P30 draft; no unlinked, RFQ or worker-era PO is
   promoted by a principal command.
7. **Preconditions:** current company/branch/actions, source allocation and
   commercial snapshot, active supplier, one independently reviewed published
   PO policy and one effective manual commitment configuration with an external
   approval reference. The reference is an attestation of approval outside the
   app, not in-app proof that Finance approved it. Governance must validate its
   owner before activation. No configured PO-specific authority means no submission.
8. **Workflow:** draft → independent commitment evidence → policy route and
   Core case → current assigned decisions → approved → issue freeze → manually
   observed supplier outcome. Each command locks the PO and source/case rows.
9. **States:** `Draft → PendingApproval → Approved → IssuePending → Issued`.
   `Returned`, `Rejected` and `Disputed` are explicit outcomes requiring
   separately specified resolution; they confer no edit.
10. **Permissions/scope:** `procurement-po-finance-confirm`, `-submit`,
    `-approve`, `-issue`, `-dispatch-confirm`, and `-read` stay separate. A role
    grant is never a monetary limit; Core company/branch and exact assignment
    scope are rechecked at every transition and receipt recovery.
11. **Rules:** Finance evidence describes a human-verified external reference,
    not a Finance ledger entry. PO policy and PO-specific spending authority
    determine approvers. Buyer/PR requester/Finance recorder and approver
    independence includes current and retained Core worker aliases.
12. **Data/owner:** Procurement owns immutable commercial, Finance-reference,
    authority, issue and acknowledgment evidence with version/hash pins; Core
    owns case execution; Finance owns real commitment balances and payments.
13. **Views:** current order details, recorded commitment (clearly manual),
    assigned tasks, decision history, frozen version and observation history.
14. **UX:** each step states what is recorded and what remains unconfirmed;
    issue authorization never displays a delivered badge.
15. **Notifications:** Core task notification can follow the recorded case;
    no automatic supplier notification in this increment.
16. **Documents:** issued snapshot hash is retained. A manual acknowledgment
    needs channel, observed date, external reference and evidence reference.
17. **Exceptions:** changed policy, authority, account alias, supplier, source,
    Finance reference, amount, branch or case state blocks progress. Unknown
    outcome requires GET of the exact command receipt.
18. **API/events:** distinct principal commands and schema-2 facts. Internal
    issue event cannot be interpreted as a successful outbound delivery.
19. **Audit/security:** same-key replay is actor/current-action scoped; old
    worker encoders and receipt rows remain byte-stable. Append-only evidence
    and no raw bank, personal or supplier portal credential exposure.
20. **Reports:** pending PO approvals and issued versions may be counted;
    manual references are not a payment or finance-ledger balance.
21. **Configuration:** no default amount, approver or manual commitment
    authority. Effective-dated published PO policy and explicit approved
    Finance policy are mandatory.
22. **Non-functional:** exact decimals, sorted locks, serializable transaction,
    immutable snapshots, idempotent command receipts and finite-role readiness.
23. **Postconditions:** Core decisions plus independently pinned Procurement
    outcomes reconstruct who authorized the commercial version and what was
    actually observed of supplier acknowledgment.
24. **Recovery:** receipt-only GET after uncertain write; no resubmission under
    a new key until the prior outcome has been reconciled.
25. **Acceptance:** workerless buyer/approver with valid PO grant can complete
    internal approval; self/alias approval, borrowed PR grant, missing Finance
    policy and suspended supplier fail closed; issue is not delivery.
26. **Tests:** pure invariants, mapped transaction/guard cases, exact P30
    migration/finite-role PostgreSQL proof, normal-cookie HTTP and UI proof.
27. **Ownership/dependencies:** Procurement and Core approval owners implement
    the route; Finance confirms whether each external reference is valid.
28. **Open:** Finance owner must verify the external approval reference and
    manual commitment procedure before activation. Funded draft cancellation,
    release on Return/Reject, revision rules and supplier channel controls
    remain product decisions. They cannot be inferred from the worker path.
