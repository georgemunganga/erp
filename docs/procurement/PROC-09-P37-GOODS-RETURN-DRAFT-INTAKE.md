# PROC-09 P37 Goods Return Draft Intake

**Status:** Source design, default off until genuine schema and finite role proof. **Parent:** [PROC-09](PROC-09-RECEIVING-INSPECTION-AND-RETURNS.md). This child defines only the proposed return, before approval, physical dispatch, stock adjustment or supplier credit.

## 1. Feature Identification

P37, Procurement → Receiving → Goods Return Request → Draft intake. Owner: receiving lead.

## 2. Purpose and Business Outcome

Record an attributable, editable proposal to return accepted goods without altering the original acceptance.

## 3. Scope and Exclusions

Create, list, read, revise and cancel a goods return Draft. No submit, approval, dispatch, replacement, inventory movement, invoice match adjustment, supplier credit, refund or Finance posting.

## 4. Parent and Related Features

Consumes P30 principal goods acceptance and its issued PO/source evidence. Later return execution and PROC-11 matching may consume a separately authorized outcome, never this Draft.

## 5. Actors and Participants

An active Core principal with current Procurement return action and company/branch scope. A linked HR employee is optional. The receiving actor and future approver may differ; no approval is inferred here.

## 6. Entry Points and Triggers

Receiving detail can open a draft return for an accepted goods line. An authorized return directory can search eligible accepted facts within accessible branches.

## 7. Preconditions

The source is an immutable P30 Goods Acceptance fact with verified receipt, issued PO line, supplier and branch; accepted quantity is positive. The actor has current Core action and scope. A draft quantity is positive and no greater than the selected accepted fact quantity at capture time.

## 8. Workflow

Select verified goods acceptance → enter exact proposed quantity and reason → save Draft → revise with expected version or cancel. Every write returns a command receipt and can be recovered by original actor/key. Submit/approve/dispatch are future transitions.

## 9. Status and State Transitions

Draft → Draft (revision), Draft → Cancelled. Cancelled is terminal and read only. No other state is accepted by the P37 API.

## 10. Permissions and Data Scope

Read and write are separate actions. Filter tenant, legal entity and active allowed Core branch before count, source lookup and response. Current action and branch are rechecked before replay. Creator owns revise/cancel; read is scoped by current action. No HR location or employee inference.

## 11. Business Rules

One draft targets exactly one accepted Goods fact, original receipt, issued PO line and source state. Quantity is an exact decimal 18,4; no rounding. A draft records intent only: it neither reserves accepted quantity nor makes it return eligible. Multiple drafts may propose overlapping quantities; later Submit must recheck current capacity and define reservation authority. No invented tolerance or approval policy.

## 12. Data Fields and Ownership

Procurement owns typed return head (scope, branch, source/fact/receipt/PO line, creator, status/version), append-only revisions (quantity, reason, evidence reference, original pins/hash), and command receipts (actor/key/request/response hash and result version). P30 receipt facts remain immutable. Inventory and Finance own their future outcomes.

## 13. Screens and Data Views

Reuse the existing Receiving detail section for a return entry and a scoped Return drafts list/detail. Display original accepted quantity, requested quantity, status and reason; do not show available-to-return as an authoritative balance.

## 14. User Experience and Human Behaviour

Label the action “Save return request draft”. Explain that saving does not send goods or change stock, invoices or supplier balances. Preserve entered input after an uncertain save and offer read-only command recovery.

## 15. Notifications and Communications

No supplier or external notification at Draft. Internal audit/outbox event is transactional and cannot imply dispatch.

## 16. Documents and Attachments

An evidence reference may point to a separately verified Core document. A free-form reference is not proof of uploaded document access; P37 will not claim attached document authority until a Core use-set exists.

## 17. Exceptions and Edge Cases

Wrong branch/company, service acceptance, rejected or unaccepted goods, corrupted original fact/receipt/source, stale version, duplicate key with different body, owner change, over-fact quantity, concurrent drafts, supplier suspension and uncertain response all fail safely. Existing drafts survive changed policy but writes still need current authorization.

## 18. Integrations, APIs, Events and Sandbox

Versioned `/api/procurement/v1/returns/principal-drafts` list/detail/create/revise/cancel and `/commands/{key}` recovery. No Inventory/Finance call and no supplier send. Emit only `goods_return.draft_created/revised/cancelled` internal audit/outbox facts.

## 19. Audit, Security and Privacy

Persist exact actor principal, branch, command key, timestamp, reason and immutable source IDs/hashes. Never expose out-of-scope source existence, count or commercial payload. Retained revision and receipt lineage is verified on read and before write.

## 20. Reports, Dashboards and Metrics

Draft count and age may be reported after scope filtering. Draft quantities are never counted as returned stock or supplier credits.

## 21. Configuration and Localization

P37 has its own default-off option/readiness. No default return authority or quantity tolerance. Exact decimal input accepts invariant API syntax and UI renders localized text without changing stored value.

## 22. Non-Functional Requirements

CAS and unique actor-owned command keys; one physical transaction for head/revision/receipt/audit. Page after scope filter. No duplicated receipt ledger.

## 23. Postconditions

Successful Draft write adds its own evidence and changes no P30 receiving/PO/request counters, stock, AP or Finance records.

## 24. Failure and Recovery

Unknown POST/PUT outcome keeps the key; GET recovery returns the saved response after current actor/action/branch checks. Same key/different action/body conflicts; no automatic mutation retry.

## 25. Acceptance Criteria

Create/revise/cancel/recover from an actual verified accepted Goods fact with a workerless Core actor; reload same typed data. Deny service/rejected source, other branch/tenant, stale CAS, changed body key and corrupted source without effects. Compare original receipt, PO/PR, Inventory and Finance rows before/after.

## 26. Test Scenarios

Exact 0.0001 and 18,4 boundary; zero/negative/over-fact; concurrent drafts; source receipt tamper; actor offboarding/branch loss; alias collision; unknown write recovery; historical cancellation. Native HTTP and independent owner SQL evidence are required before enablement.

## 27. Ownership and Dependencies

Procurement owns P37 Draft; P30 owns accepted goods and immutable source proof. Core owns identity/documents/audit framework. Inventory/Finance retain stock/accounting. Requires genuine EF migration and captured finite ACL/readiness.

## 28. Open Decisions and Assumptions

Return submit/approval, reservation, physical carrier handoff, supplier notice, credit and replacement rules are separate future slices. No P37 Draft can be interpreted as approval or returned quantity.
