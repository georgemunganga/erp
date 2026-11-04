# PROC-PAY-03 — Purchase-order advances and invoice allocation

Status: implementation in progress; source contracts first. No production activation or completed payment route is claimed.

## 1. Feature Identification
PROC-PAY-03; Procurement → External payment reporting → PO advances → request/approve/record/confirm/allocate. Engineering owner: ERP team. Parent issues145/190/196.
## 2. Purpose & Business Outcome
Allow controlled payment before a bill exists, retaining the order authority and later allocation against a real bill without paying twice.
## 3. Scope & Exclusions
PO-backed advances, exact capacity and subsequent invoice allocation. Reuse existing payment/Core processes. Excludes bank execution, AP/GL postings, FX settlement and unsupported supplier balance adjustments.
## 4. Parent & Related Features
Manual invoice payments, principal PO governance, Core approvals, invoice matching, credits, original-command recovery.
## 5. Actors & Participants
Current Core requester, independent Accounts approver, external payment recorder, independent confirmer and authorised allocation reviewer. Employment is optional.
## 6. Entry Points & Triggers
Existing payment form selects an authorised PO instead of an invoice; later actual invoice intake permits explicit allocation of a confirmed advance.
## 7. Preconditions
Current action/branch/company scope; approved active source and supplier; exact currency; published finite policy explicitly permits advances; current approved commercial version; no unknown conflicting settlements.
## 8. Workflow
Create advance draft → submit/reserve → Accounts approve → record externally performed payment → independent confirm → allocate confirmed amount to actual same-PO invoice. Return/reject/cancel/dispute use existing Core transitions. Drafts reserve no capacity. Allocation is commercial evidence, never a journal.
## 9. Status / State Transitions
Reuse Draft/PendingApproval/Approved/PaymentRecorded/ConfirmedManually/Rejected/Cancelled/Disputed. Disputed and unconfirmed recorded money remains reserved. Allocation history is append-only with explicit reversal, never silent edits.
## 10. Permissions & Data Scope
Existing payment actions and independent actor checks remain; allocation requires explicit current authority. Company, supplier, branch, PO and invoice must match. Header all-branches is not write authority.
## 11. Business Rules
No synthetic invoice. Sum of reserved advances cannot exceed the approved PO or the configured advance cap. Exact arithmetic; do not round financial inputs to fit. Confirmed advance can be allocated only once up to its amount and actual invoice capacity. An invoice with unreconciled advances must not be paid as if no advance existed. Incomplete/conflicting evidence blocks.
## 12. Data Fields & Ownership
Typed source kind and ID; immutable source version/approved gross; existing request/decision/receipt/approval evidence; confirmed advance-to-invoice allocation/reversal. Procurement owns commercial evidence; Core owns identity/approval; Finance owns accounting/payment truth. Existing invoice records keep their identity and history.
## 13. Screens & Data Views
Extend existing payment source picker, amount/remaining display, history and invoice detail. Keep imported layout and shared components. Show advance, unallocated and applied amounts distinctly.
## 14. User Experience / Human Behaviour
Select PO or invoice explicitly. Explain unavailable source and unresolved allocations plainly. Preserve drafts on validation errors and retain source selection after uncertain responses.
## 15. Notifications & Communications
Use existing notification/outbox for assigned work and completed/disputed evidence; delivery failure does not undo financial state.
## 16. Documents & Attachments
Use existing Core references and real scanner/transport admission. Optional evidence does not become verified proof; no fabricated file pin.
## 17. Exceptions & Edge Cases
PO revision/cancellation, partial invoices, multi-PO invoices, credits, dispute, duplicate external reference, role withdrawal and scope changes require explicit guarded treatment. Unknown imported Finance facts block rather than imply zero paid.
## 18. Integrations / APIs / Events / Sandbox
Extend existing payment command/recovery and read APIs with typed source; retain invoice payload compatibility. Atomic request/decision/Core case/audit/outbox/receipt. Versioned future Finance handoff; isolated synthetic tests only.
## 19. Audit / Security / Privacy
Retain actor, aliases, source/version, policy, approvals, exact money, external reference and allocation/reversal reasons. Current backend authorization on every action/read/replay. No bank secrets in broad Procurement responses.
## 20. Reports / Dashboards / Metrics
Approved/reserved/recorded/confirmed advance, unallocated amount, allocated invoice amount and reconciliation exceptions; do not label as accounting balances or executed bank payment.
## 21. Configuration & Localization
Explicit published advance permission/currency cap and percentage. No invented company values. Existing Core currency, timezone and business-date services apply.
## 22. Non-Functional Requirements
Serializable lock order and aggregate checks across advance and invoice sources; bounded scoped reads; exact decimal transport; concurrency and duplicate protection.
## 23. Postconditions
Durable linked evidence, immutable command response and correct remaining capacity; no duplicate payment or phantom Finance posting.
## 24. Failure & Recovery
Rollback all companions on failure; actor-scoped original-key recovery before retry; retain original error/uncertain response; do not silently replay a money command.
## 25. Acceptance Criteria
Real employee-optional authorised PO advance → independent approval/record/confirm → actual invoice allocation; legacy invoice journey preserved; capacity/scoping/revision/security/retry controls pass at API/native/UI levels.
## 26. Test Scenarios
Positive and boundary capacities, high precision, repeated keys, duplicate evidence, current permission/branch withdrawal, self-approval, wrong company/supplier/source, stale/cancelled order, unknown Finance facts, disputed/unconfirmed advance allocation and concurrent overpayment.
## 27. Ownership / Dependencies
Root owns source contracts until published handoff; Manus issue196 remains the separate-computer implementation/review task. Reuse current Core/payment/PO components. Native schema, finite privileges, migration/recovery and independent review are release gates; no source-only feature is marked live.
## 28. Open Decisions / Assumptions
Actual company approvals/limits remain business inputs. Database representation/API rollout must preserve existing invoice FKs and records; never overload an invoice ID with a PO ID. Readiness captures must derive from actual generated schema. Multi-PO allocations and refunds need explicit contracts before activation.

Reference: [ERPNext advance payment entry](https://docs.frappe.io/erpnext/advance-payment-entry) supports order references and later invoice reconciliation. This ERP reuses that separation without duplicating Finance accounting.
