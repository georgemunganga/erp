# PROC-06 Strategic Sourcing and Award

**Status:** Draft for business review · **Version:** 0.1 · **Hierarchy:** Procurement → Strategic Sourcing → Sourcing Event, Bid Evaluation and Award → Operations · **Framework:** [ERP 28-section standard](../hrm/feature-specifications/ERP_Feature_Specification_Framework_Enterprise_Integration_Updated.docx) · **Module:** [PROC-00](PROC-00-PROCUREMENT-MODULE-ARCHITECTURE-AND-CAPABILITY-MAP.md)

**Implementation note (18 October 2026):** The shared ERP host has an own-record sourcing draft API under `/api/procurement/v1/sourcing`. It can create, revise and cancel an internal event and its lines. Removed lines retain a cancellation state and reason. Approved PR-line conversion, method policy, publication, supplier invitations, sealed bids, evaluation and award are not enabled by this draft API.

**P29 source-draft decision (23 October 2026, pending migration and acceptance):** The governed `from-approved-sources` path must accept an approved P25 principal-identity PR line when its maker, requester, Finance checker and buyer are active, authorized Core accounts with an allowed transaction branch. Retain the original PR submission schema 3 and Finance checker schema 2; never fill missing worker IDs with synthetic values. A new RFQ evidence version records canonical principal IDs, optional historical Core worker aliases, source pin digests, the approval case and final decision. Version 1 worker evidence remains byte-compatible and is restored under its original checks. Create reserves only the selected available quantity; cancellation releases precisely those allocations after source locks, retained-evidence verification and CAS. An unknown write is recovered by the original idempotency key under the current actor and record scope. Read and cancel require current action permission and source branch access. This milestone creates an internal Draft only: publication, invitations, bids, evaluation, award and supplier dispatch remain unavailable. The route stays default off until the genuine owning migration, exact finite-role schema inspection and private no-employee acceptance are complete.

## 1. Feature Identification

`PROC-06`, purchasing phase. Business owner: sourcing lead; technical/product/data owners to name. Operations: `O1` choose method and create event, `O2` approve/publish, `O3` collect and seal bids, `O4` open/evaluate, `O5` recommend/approve award, `O6` cancel/reopen/release PR allocation.

## 2. Purpose and Business Outcome

Select suppliers under the required competition or justified exception route, with comparable offers and an auditable decision. Success is an approved line-level award or documented no-award; a supplier response alone never creates a PO.

## 3. Scope and Exclusions

Includes direct/single-source/emergency method determination, RFQ/RFP/tender events, invitations, questions, portal/offline/surrogate responses, sealed bids, compliance and scored evaluation, recommendation, award and line allocation. Contract signature, PO issue and supplier payment are separate. Method-specific legal requirements await policy review.

## 4. Parent and Related Features

Parent Strategic Sourcing; consumes approved [PROC-02](PROC-02-PURCHASE-REQUISITION-AND-LINE-ROUTING.md) lines, [PROC-01](PROC-01-SUPPLIER-ONBOARDING-AND-MANAGEMENT.md) eligibility and [PROC-CFG-01](PROC-CFG-01-PROCUREMENT-CONFIGURATION-AND-POLICY.md) method/evaluation rules. Contracts and Purchasing consume award; Supplier Portal can submit bids later.

## 5. Actors and Participants

Buyer/sourcing lead, invited supplier, authorized surrogate-bid recorder, technical/commercial evaluators, conflict reviewer, award approver, requester, compliance and auditor.

## 6. Entry Points and Triggers

Approved PR-line allocation, annual sourcing plan, contract expiry, approved direct/single-source/emergency exception, RFx schedule, supplier clarification, bid deadline or evaluation task.

## 7. Preconditions

Approved available demand, valid method/threshold policy, authorized sourcing owner, supplier eligibility or explicit invitation exception, criteria and weights fixed before publication, confidentiality and deadline configured.

## 8. Workflow

`O1`: choose method and source lines; document rationale/exceptions. `O2`: approve event then publish identical requirements and due date to eligible suppliers. `O3`: accept versioned portal/email/physical/surrogate bids with source evidence and received time; seal until close. `O4`: controlled opening; pass/fail compliance, independent weighted technical/commercial scores, conflict declarations and normalized comparison. `O5`: recommend line awards and rationale; separate authority approves; publish award references to Contracts/PO. `O6`: cancel or reopen with reason and supplier notice; release unawarded PR allocation.

## 9. Status and State Transitions

Event: Draft → Pending Approval → Published/Open → Closed → Evaluation → Award Pending → Awarded/No Award; alternatives Cancelled/Reopened. Bid: Invited → Draft → Submitted/Withdrawn → Opened → Evaluated → Awarded/Not Selected/Disqualified. Each transition records actor, conditions, required documents, deadline, SLA and escalation. Submitted bid is immutable; correction is a version. Late bid is blocked or separately approved under policy, never backdated.

## 10. Permissions and Data Scope

Buyer manages assigned entity/category event; supplier sees only its invitation and own bid; evaluators see assigned phase and sealed content only after authorized opening; award approver is distinct where policy requires. Surrogate recorder cannot alter source receipt time or approve own award. Competitor prices and scores are field restricted.

## 11. Business Rules

`BR-PROC-SRC-001` method follows effective threshold/category policy; `002` criteria/weights lock at publish; `003` no bid content before opening; `004` surrogate bid needs actor, supplier, source, original evidence and both received/entered times; `005` evaluator conflict must be resolved before score counts; `006` award quantity/value cannot exceed approved source balance without renewed authority; `007` sole source/emergency needs reason and designated approval.

## 12. Data Fields and Ownership

Procurement owns event ID/type, PR lines, specs, schedule, supplier invitations, Q&A, bid/version/source/evidence, compliance result, evaluator score/comment, normalized comparison, recommendation, award line and approvals. Supplier owns original offer; Procurement stores controlled snapshot. Finance owns budget; Contracts/PO own downstream agreement/order.

## 13. Screens and Data Views

Sourcing queue, event editor, supplier invitation/publication view, sealed inbox, opening register, compliance checklist, individual evaluation, comparison table, recommendation/award detail, exception and audit timeline. Supplier portal view is isolated and narrower.

## 14. User Experience and Human Behaviour

Show deadlines and time zone consistently, separate technical and price evaluation when configured, and make conflicts visible without revealing competitors. Buyers need an evidence checklist for offline offers; suppliers need clear receipt confirmation. Comparison explains normalized cost assumptions.

## 15. Notifications and Communications

Invite supplier; notify clarifications fairly per policy, approaching deadline, opening/evaluation tasks, award decision and no-award/cancellation. External notices are supplier-scoped; internal confidential evaluation stays internal. Delivery failures are visible.

## 16. Documents and Attachments

Specifications, RFx pack, supplier questions, original bids, surrogate source document, evaluation sheets, conflict declarations and award memo are scanned, versioned and retained with confidentiality labels. Published pack and bid submission versions cannot be silently changed.

## 17. Exceptions and Edge Cases

Supplier portal outage, physical bid at deadline, late bid, one bidder, no compliant bids, evaluator withdrawal, conflict discovered after score, price currency mismatch, PR line change mid-event, supplier suspended before award and reopened event.

## 18. Integrations, APIs, Events and Sandbox

Consumes PR allocation, supplier status, policy, documents and workflow. Emits `RFQPublished`, `SupplierQuotationReceived`, `SupplierSelected`/award and cancellation facts after commit with tenant/entity, event/bid/line versions, correlation/idempotency. Supplier channels use scoped authentication; sandbox tests sealed access, surrogate evidence, duplicate bid and late delivery.

## 19. Audit, Security and Privacy

Append invitation, publication, supplier response time, opening, individual scores, conflict, recommendation and award. Restrict sealed bids, competitor data and evaluator identity as policy requires. Export/privileged reads are logged; no normal user deletes submitted evidence.

## 20. Reports, Dashboards and Metrics

Competitive coverage, bidder participation, cycle time, single-source/emergency frequency, evaluation turnaround, no-award reasons and savings baseline with declared method. Do not call estimate-versus-award difference savings without approved methodology.

## 21. Configuration and Localization

Methods, quote minimum, publication duration, business calendar, evaluation templates/weights, mandatory criteria, late-bid rule, currencies, locale and supplier channels are effective-dated by entity/category.

## 22. Non-Functional Requirements

Deadline and seal enforcement uses trusted server time; supplier submissions are idempotent and securely stored. Concurrent evaluators cannot overwrite scores. Accessibility, upload limits, availability near close and recovery targets need pilot agreement.

## 23. Postconditions

Award records approved supplier and line quantities/value with complete evaluation evidence and source PR links; no-award releases allocation or leaves an explicit reroute task. PO/contract conversion sees only approved award.

## 24. Failure and Recovery

Failed publication remains Pending, not Open. Unknown bid upload outcome is checked by submission key; supplier gets receipt only on durable save. Failed award handoff retries by key; sealed contents remain protected through outages.

## 25. Acceptance Criteria

Given unpublished criteria, bid submission is unavailable. Given a sealed bid, evaluator cannot read it before authorized opening. Given surrogate entry, original source and times are mandatory. Given unresolved conflict, score/award blocks. Given approved award, quantities cannot exceed PR allocation.

## 26. Test Scenarios

Test RFQ/RFP/tender/direct/sole-source routes, no/one/many bids, late/offline/surrogate submission, sealed access, independent scoring, currency normalization, conflict, supplier suspension, duplicate event replay and deadline boundary.

## 27. Ownership and Dependencies

Sourcing lead owns method/evaluation; governance owns award authority; supplier steward owns eligibility; Finance owns funding. Depends on PROC-01/02/03 and CFG; Contracts/PO consume award.

## 28. Open Decisions and Assumptions

Approve sourcing thresholds, tender legal process, bid seal/opening authority, surrogate channels, scoring model, conflicts policy, savings method, supplier portal timing and retention. Owners: Procurement governance, Legal/Compliance and Finance; resolve before implementation.
