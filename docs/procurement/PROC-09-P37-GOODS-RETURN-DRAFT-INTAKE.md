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
The return's `receiptId` is the retained Goods receiving **draft header** ID (`PrincipalReceivingFact.DraftId`), which is the physical FK target. The acceptance payload's `AcceptanceRecordId` is the distinct **receipt line** ID and must match the linked observation's `ReceiptLineId`; the observation's `ReceiptId` must match the fact's draft header. Eligible-source reads and return creation expose and retain the header ID after verifying both bindings.

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

P37 has its own default-off `Procurement:PrincipalGoodsReturnDrafts` option/readiness.
`ExecutionDatabaseConfigured` is derived from the explicit execution connection,
never trusted from a business configuration value. No default return authority
or quantity tolerance. Exact decimal input accepts invariant API syntax and UI renders localized text without changing stored value.

## 22. Non-Functional Requirements

CAS and unique actor-owned command keys; one physical transaction for head/revision/receipt/audit.
The P37 path uses the existing owning Serializable transaction, actual scoped
Core authorization fence and current actor/branch resolver. Its provider requires
P30 source READ on that exact owner plus P37-specific writes; a return Draft
never needs receiving-fact INSERT or PO/PR/receiving-counter UPDATE. Existing P30
Receiving command write guards are unchanged. Page after scope filter. No duplicated receipt ledger.

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


## Own physical readiness implementation checkpoint (source only)

The isolated `ProcurementPrincipalGoodsReturnStoreReadiness` verifies the actual
same-connection Serializable transaction and configured physical execution store,
then the existing P30 receiving SOURCE READ provider. It separately checks all
three modeled return tables, exact three CHECKs, eight modeled plus four authored
FKs, four authored owner keys, and all nineteen owned PK/AK/business/FK indexes.
The current-revision FK must be deferred; the three source-owner FKs must remain
immediate NoAction. Unknown extra owned indexes or changed definitions deny.
The genuine joint migration `20261025020252_P37PrincipalGoodsReturnDrafts` now
has an accepted private owner Up/Down/Up capture. Expected definition dictionaries
are imported byte-exact from that capture, including all nineteen owned indexes.
This does not establish finite-role or business runtime admission; see the joint
private schema acceptance receipt for exact scope and remaining gates.

The write matrix needs INSERT on the three P37 tables and append-only audit/outbox,
and UPDATE only revision/state/current_revision_id/version/update stamps on the
head. Revision, receipt, audit and outbox UPDATE (including column-only), all
owned DELETE/TRUNCATE, table-owner membership, elevated roles and schema CREATE
fail closed. Source read rights remain owned by P30/Core; this provider requires
no source-counter DML and grants no role. Current business actions still resolve
stored Core role/account/branch and optional aliases in the command callback;
neither SQL readiness nor historical receipt ownership grants current authority.

`ProcurementPrincipalGoodsReturnComposition.AddFoundation` is an uncalled future
composition seam, not a live registration. Host routes and metadata remain
unmapped/false. Authored tests cover source-read versus own-write purposes,
transaction ownership/isolation, default flags/missing capture, exact capture
keysets, immutable grant boundaries, current workerless Core authorization and
revocation/branch/forged-alias denial. No SDK, PostgreSQL or browser job was run
for this source checkpoint; real finite role, physical and HTTP proof remain.


## Pre-UI read-projection addendum (sections 10, 12–14, 18–19, 22, 25–28)

List/detail GET responses retain the existing `id`, `status`, `version`, scope/source IDs,
exact quantities, reason/reference and no-effect flags. They additionally return
`allowedActions: string[]`, with exactly `Edit` and `Cancel` only for the current
stored Core principal who created a Draft, currently has `procurement-return-edit`,
passes that HTTP authorization policy, and retains the same tenant/company/branch
admission. Otherwise the array is empty. These are current UI hints, not authority:
write callbacks continue their own current actor, original creator, state and CAS
checks. Existing branch/actor admission precedes reads and paging.

Eligible-goods rows and verified list/detail rows additionally return `labels`:
`purchaseOrderNumber`, nullable `supplierDisplayName`, `lineNumber`, `description`,
`unitCode`, `currencyCode`, `deliveryNoteReference`, and `receivedAt` (the actual
observation time, not a fabricated acceptance or dispatch date). SourceHistory
returns its already verified retained-issued graph alongside the verified ledger;
no duplicate source loader, new table or current supplier/catalog eligibility query
is introduced. Supplier display text comes verbatim from the original verified
issued commercial snapshot, line text from its verified PO line and the physical
note/time from the exact observation linked to the accepted fact. No fallback
supplier name or receipt number is manufactured. Labels convey identification,
not current return capacity, commercial approval or Inventory/Finance authority.

Original write and GET-command-recovery acknowledgements remain frozen under
`PrincipalGoodsReturnRules.ResponseJson`. Neither dynamic `allowedActions` nor
labels are persisted in them, and their response hashes/versions remain unchanged.
After any acknowledgement, UI must GET current detail before exposing a new edit.
Unknown writes keep original body/key and use GET recovery; no blind replacement
POST or inferred record ID is introduced.

The supplied UI has no goods-return transaction screen. Future contextual Returns
integration must reuse the existing Receiving toolbar/tabs/editor frame and shared
ERP controls, with a separately reviewed absent-false owner-ready capability. This
checkpoint does not register routes, enable flags, invent a Returns layout or wire
preview settings/vendor-credit demo actions. Its added source tests cover action
eligibility/scope/state, stored Core permission revocation despite a stale claim,
verbatim source labels and unchanged original acknowledgements. Twelve added cases
are authored but not executed here; fresh owning SDK/finite-role/runtime gates
remain separate. There is no database model or migration change.


## Integrated-next source checkpoint (uncompiled)

This lane ordinarily merges the default-off composition/private native hook
`51cd1ba7` with read projections `39cb74a0`. Normal Program registers the concrete
provider and guarded routes; `PrincipalGoodsReturnDrafts:Enabled` remains false
by default and availability metadata is false unless actual captured physical,
finite execution and upstream source admission pass. Mapping does not grant an
action or enable the feature. Existing root C2e SDK/runtime work is separate and
unchanged.

The private hook now compares GET list/detail using typed current fields and the
new labels/actions, while comparing original receipt bytes only for write replay
and GET command recovery. It requires empty actions for a same-branch noncreator
who has Read/Edit and for the creator after Edit revocation with Read retained;
the corresponding revise/recovery denials must not change persisted footprints.
Cancelled GETs are read only. Labels are compared to retained issued supplier,
PO-line and actual physical observation facts read from the owning private graph.
The earlier captured schema/private finite SQL receipts remain narrowly scoped;
this combined source, private hook, current authentication/HTTP and future UI
still require actual execution/independent owner acceptance. No whole-module,
production, dispatch, inventory, supplier-credit or Finance claim is made.
