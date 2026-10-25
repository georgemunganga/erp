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

`procurement-return-read`, `procurement-return-create` and `procurement-return-edit` are separate explicit Core actions with no ordinary role or legacy administrator bootstrap grant. Filter tenant, legal entity and active allowed Core branch before count, source lookup and response. Current action and branch are rechecked before replay. Creator owns revise/cancel; read is scoped by current action. No HR location or employee inference. Existing `procurement-return-dispatch` is separate future authority.

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

Persist exact actor principal, branch, command key, timestamp, reason and immutable source IDs/hashes. Never expose out-of-scope source existence, count or commercial payload. Retained revision and receipt lineage is verified on read and before write,
including the original Create 201 versus Revise/Cancel 200 response status.
Each receipt physically binds the immutable revision's draft, revision number,
actor and command key, and its branch binds the retained head. The keys exclude
the mutable header cursor. New metadata events must be Pending, zero attempts
and without a publication timestamp in the same save.

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

## Schema and activation proposal

The genuine EF migration must add only three typed tables: `principal_goods_return_drafts`, `principal_goods_return_revisions`, and `principal_goods_return_receipts`. Scoped source ID foreign keys and exact receipt-to-revision-command and
receipt-to-head-branch foreign keys are in the model. The receipt CHECK also
binds operation, original result revision and HTTP status. Those independent keys are **insufficient** to prove a common P30 owner. The authored but uninstalled `P37PrincipalGoodsReturnOwnershipSql.AfterUpSql` adds exact composite keys and foreign keys binding head→source `(branch, PO, PO line, issued)`, head→Acceptance fact `(source, receipt, branch)`, and head→Goods receipt `(branch, PO, issued)`. It also adds a deferred exact current-revision/state foreign key because the head and first revision are inserted together. The genuine migration must invoke this overlay after generated table creation, capture the actual catalog definition, and run wrong-fact/receipt/PO-line raw SQL negatives. `BeforeDropSql` removes the P37 overlay only after the first-Down veto. No migration, designer or snapshot may be hand edited to claim these uninstalled constraints.

The first `Down` statement must raise SQLSTATE `55000` if **any** P37 head, revision or receipt exists, including cancelled drafts; only an empty P37 schema may downgrade. Runtime Record permissions must be finite: scoped SELECT on the three new tables and specific P30 read columns, INSERT on all three, UPDATE only mutable head revision/state/version/audit columns, with no DELETE/TRUNCATE and no supplier, Inventory or Finance write. The separate execution connection is required to have the same owner transaction and declared finite privileges. The route remains unregistered and `principalGoodsReturnDrafts=false` until actual migration, catalog/ACL capture, native workerless Core HTTP proof and independent owner readback pass. Its current source checkpoint is not a live capability.

## Original UI reuse plan

The imported Receiving detail already presents accepted Goods facts and source line context. Add an optional “Request return” action only when a future server detail candidate and P37 capability both permit it; use the existing editor shell, exact quantity control and detail tabs. Present the Draft in a scoped directory with reason and source links. Do not reinterpret the legacy `SupplierReturn` row or add local demo state. This UI adaptation follows backend acceptance and is outside this source checkpoint.


## Integration review correction (2026-10-25)

The P35-based integration adds the exact immutable command receipt FKs,
pristine new-event checks and retained original status validation before replay.
Authored model/rules/persistence regressions cover wrong branch, actor, key,
revision and response status, plus already-published/retried metadata events.
These C# tests have not run. Generated migration, Designer and snapshot files
remain unchanged; the official generation profile must bind this new source
before execution. No readiness or runtime acceptance is claimed.


## Genuine generation checkpoint (2026-10-25)

Official EF generated `20261025013416_P37PrincipalGoodsReturnDrafts` from
`2d485b11`: three tables, eight mapped FKs, three CHECKs and ten explicit
indexes; no existing model entity definitions changed. Genuine Designer and
snapshot are installed byte-identically. A separate authored migration-body
change calls the existing owner overlay last in Up, and first-Down retention
veto then overlay teardown before generated drops. Raw generation artifacts
remain immutable. See release generation/overlay JSON receipts. This is not
compiled migration SQL, a PostgreSQL roundtrip or runtime readiness. Those
gates remain pending; routes/capability stay unavailable.
