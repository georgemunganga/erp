# PROC-11 Invoice Match, AP Handoff and Payment Visibility

**Status:** Draft for business review · **Version:** 0.1 · **Hierarchy:** Procurement → Supplier Invoices and Procure-to-Pay → Invoice Validation and Finance Handoff → Operations · **Framework:** [ERP 28-section standard](../hrm/feature-specifications/ERP_Feature_Specification_Framework_Enterprise_Integration_Updated.docx) · **Module:** [PROC-00](PROC-00-PROCUREMENT-MODULE-ARCHITECTURE-AND-CAPABILITY-MAP.md)

**Implementation boundary:** [PROC-11A gap map](PROC-11A-MATCHING-FOUNDATION-GAP-MAP.md) and [PROC-11B 28-section comparison foundation](PROC-11B-INVOICE-MATCHING-COMPARISON.md) define the bounded source-only increment. Operational matching and AP posting remain disabled until authoritative source, persistence and Finance gates pass.

## 1. Feature Identification

`PROC-11`, procure-to-pay phase. Business owners: AP and Procurement validation leads; product/data/technical owners to name. Operations: `O1` two/three-way match, `O2` route exception, `O3` approve variance, `O4` hand off, `O5` reconcile AP result, `O6` sync payment status.

## 2. Purpose and Business Outcome

Prevent unsupported supplier liabilities while giving Procurement read-only visibility of Finance outcome. Success is a validated invoice tied to PO/receipt evidence, one idempotent AP handoff and truthful Posted/Paid status from Finance.

## 3. Scope and Exclusions

Includes configurable two/three-way comparison, duplicate gate, quantity/price/tax/total variance, exception ownership, validated snapshot, AP acceptance/rejection and payment-status projection. Finance owns tax accounting, supplier ledger, approval for payment, disbursement and bank reconciliation.

## 4. Parent and Related Features

Parent Supplier Invoices and Procure-to-Pay; consumes PROC-10 intake, PROC-08 PO and PROC-09 accepted fulfillment; supplier/contract refs from PROC-01/07. Finance AP is downstream owner.

## 5. Actors and Participants

AP matcher, buyer, receiver/service owner, budget/variance approver, Finance AP service/operator, supplier contact for corrections, auditor and reconciliation job.

## 6. Entry Points and Triggers

Invoice Ready for Match, new receipt/PO revision, corrected invoice, approved variance, Finance acknowledgment/rejection, AP posting, partial/full payment or credit application.

## 7. Preconditions

Validated invoice version and supplier/entity; eligible PO or authorized non-PO route; accepted receipt/service evidence where policy requires; effective tolerance/match rule and Finance handoff schema. Unresolved duplicate or hard exception blocks sending.

## 8. Workflow

`O1`: compare invoice lines to immutable PO and accepted unreversed receipt, apply field/category policy and record source versions. `O2`: create exception with discrepancy, owner, deadline and evidence. `O3`: correct invoice/PO/receipt or obtain scoped variance decision, then rerun match. `O4`: send one approved snapshot with stable key to Finance. `O5`: store AP acceptance/posting or rejection; correct/retry via new version when needed. `O6`: project Finance payment/credit status and reconcile conflicting/late callbacks.

## 9. Status and State Transitions

Procurement: Match Pending → Matched / Exception → Validated → Handoff Pending → Handed to AP / AP Rejected. Finance projection: Awaiting Acceptance → Posted → Partially Paid/Paid or Payment Exception; credit/void states reflect Finance source. Actor, condition, evidence, SLA and recovery accompany transitions. Paid is impossible from Procurement action alone.

## 10. Permissions and Data Scope

AP matcher acts within entity; buyer/receiver resolves only assigned discrepancy type; variance approver has separate authority and no prohibited self-conflict; Finance callback is service-authenticated and tenant/entity checked. Supplier portal sees only permitted own status, never internal coding or bank details.

Invoice capture stores the selected Core branch. Branch-confined AP users can list and open only invoices for their selected authorized branch, and cannot attach an order from another branch. Legacy invoices without a branch remain available only to users with company-wide access until an authorized backfill establishes their origin. Duplicate invoice detection remains company-wide and returns no hidden invoice details.

## 11. Business Rules

`BR-PROC-MAT-001` two/three-way method by category and evidence need; `002` tolerances per field/entity, never silent global; `003` reversed/rejected receipts excluded; `004` duplicate or unresolved hard variance blocks AP; `005` one approved snapshot maps to one handoff key; `006` Finance owns Posted/Paid/credit-applied status; `007` material source correction reruns match.

## 12. Data Fields and Ownership

Procurement owns match ID, invoice/PO/receipt versions, invoice branch reference, comparable qty/price/tax/total, tolerance/policy version, exception and decision, approved handoff snapshot, delivery attempts and Finance refs. Core owns the referenced branch and organization currency; capture defaults to that currency when none is supplied. Finance owns AP bill, journal, tax treatment, due date as posted, credit application, payment date/ref. Procurement stores minimal status projection.

## 13. Screens and Data Views

Match workbench with line comparison, exception queue, resolution/approval detail, handoff attempts and AP status, reconciliation dashboard and supplier-scoped payment visibility when portal exists.

## 14. User Experience and Human Behaviour

Explain each discrepancy in quantity/amount/currency and identify next owner. Show Received, Matched, Sent, AP Accepted, Posted and Paid distinctly. Do not tell supplier payment is scheduled or completed until Finance confirms.

## 15. Notifications and Communications

Notify discrepancy owner, approver, AP on validated item, operator on failed/unknown handoff, supplier through approved channel on correction or confirmed payment status. Sensitive coding and bank data stay out of broad notifications.

## 16. Documents and Attachments

Match snapshot references exact invoice, PO, GRN/service certificate, credit and exception evidence versions. Access, scan, retention and legal hold follow source classification; Finance receives secure references or approved immutable copies.

## 17. Exceptions and Edge Cases

Invoice before receipt, PO price changed after service, partial receipt/invoice, tax difference, closed PO, suspended supplier with valid old liability, duplicate invoice, foreign currency, reversed receipt after AP handoff, Finance rejection and conflicting Paid callback.

## 18. Integrations, APIs, Events and Sandbox

Versioned AP contract includes tenant/entity, supplier ref, approved invoice/line/coding/evidence snapshot, source IDs, correlation/idempotency. Finance replies with acceptance/rejection, AP ID, posting and payment statuses. Emit `SupplierInvoiceMatched`, `SupplierInvoiceApproved` and handoff facts after commit. Sandbox tests duplicate send, unknown outcome, late/conflicting callback and credit without real payments.

## 19. Audit, Security and Privacy

Append source comparison, rule/version, discrepancy, actor decision, handoff payload hash/attempt, Finance reply and status reconciliation. Server enforces entity/supplier/field scope; privileged exports and callbacks logged.

## 20. Reports, Dashboards and Metrics

First-pass match, exception rate/type/age, AP handoff time, rejection/retry age, accepted-versus-posted totals and payment-status freshness. Define denominator, as-of time and currency conversion.

## 21. Configuration and Localization

Match method, price/quantity/tax/total tolerances, exception authority, non-PO route, invoice number normalization, entity tax references, currency basis, SLA and status-sharing rules are effective-dated.

## 22. Non-Functional Requirements

Handoff and callback are idempotent/concurrency safe, with durable delivery log and reconciliation. Match scales to document volume; result explains source versions. Targets for latency, recovery and status refresh require Finance agreement.

## 23. Postconditions

Validated invoice has resolved evidence and one stable handoff; AP acceptance/rejection and payment projection are linked without changing original invoice or recreating payable. Exceptions remain owned until resolved.

## 24. Failure and Recovery

Finance outage leaves Handoff Pending, not Posted. Unknown send outcome queries Finance by key before retry. Rejected payload is corrected in a new snapshot/version. Conflicting status is quarantined; reversal after AP posting creates coordinated Finance exception, not silent local deletion.

## 25. Acceptance Criteria

Given invoice qty above accepted goods tolerance, handoff blocks. Given authorized two-way service, missing goods receipt is not required. Given repeated send, Finance sees one payable. Given AP rejection, owner/reason visible. Given local user action, Paid cannot be set without Finance confirmation.

## 26. Test Scenarios

Test two/three-way goods/service, partial/reversed receipt, price/qty/tax boundaries, duplicate, exception authority, concurrent match, Finance outage/replay, AP rejection, credit/status callback and tenant/field isolation.

## 27. Ownership and Dependencies

AP lead owns matching/posting acceptance, Procurement owns commercial validation, Finance owns payable/payment. Depends on PROC-01/08/09/10, Finance AP contract and document/workflow service.

## 28. Open Decisions and Assumptions

Confirm two/three-way categories, tolerance matrix, non-PO invoices, tax owner, exception authority, Finance payload/status contract, payment visibility freshness and reversal handling. Owners: AP, Procurement and Finance; resolve before implementation.
