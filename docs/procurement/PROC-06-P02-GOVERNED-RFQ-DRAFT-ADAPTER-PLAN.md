# PROC-06-P02 — Governed RFQ draft adapter and persistence plan

Status: Proposed for technical review; source-only, 20 October 2026. Depends on the pure draft planner at40ef66b and reviewed common approved-demand/allocation helpers. No HTTP implementation, model, migration, database operation or activation accompanies this plan. Central integration must start from the then-current verified P26 model/migration; this branch's older snapshot is not the migration baseline.

## 1. Feature Identification

Procurement → Strategic Sourcing → Governed RFQ Draft → Create approved-source draft / cancel held demand / recover original command. Publication review, actual publish and supplier dispatch remain separate increments. Extends PROC-06-P01's28-section contract and PROC-ALLOCATION-EXACT-VALUE-CONTRACT.

## 2. Purpose & Business Outcome

Make approved competitive demand usable in a durable internal draft, with one quantity hold in the existing PR allocation ledger, safe cancellation and recoverable writes. Success means a saved source-linked draft and its real reservations, or an auditable cancelled draft with precisely released holds. No supplier has been contacted.

## 3. Scope & Exclusions

Supported first adapter: create and cancel original-owner sourced drafts; read owned draft and receipt. One company, exact branch (including explicitly supported company-level null), currency and configured competitive method per event. No edit/resizing, submission, rejection, reassignment, finite publication authority decision, Core case creation, supplier invitations, bids, awards, finance commitment or external messages. Existing legacy unlinked draft CRUD remains available only for execution0 records.

## 4. Parent & Related Features

Reuse approved PR evidence, existing source eligibility, method/catalog classification and Finance authority adapters, common PR allocation reducer, Core canonical account/worker and record scope, shared transaction/fences, existing audit/outbox, numbering conventions and current ERP UI. No second worker/supplier/policy/quantity master.

### Audited source and reusable boundary

| Existing source | Reuse / limitation |
|---|---|
| `ProcurementOrderSourceCommands.ConvertSources` | Current actor, receipt-before-new-effects, sorted PR locks, owner source load, classification/Finance boundary, transactional writes. Do not call the DirectPurchase-only adapter or copy PO-specific supplier/price policy into RFQ. |
| `ProcurementOrderExecutionSupport.Source` | Existing actual PR pin/outcome/Core decision/evaluation/policy/Finance/method plus complete PO/RFQ ownership loading. Factor a narrow reusable loader where appropriate; do not copy a second subtly different completeness query. Loading current rows alone is not their eligibility proof. |
| `ApprovedPurchaseDemand`, `CompetitiveSourcingSource`, `PurchaseDemandAllocationBalance`, `GovernedRfqDraft` | Immutable approved evidence and competitive eligibility; one ledger; pure reserve/cancel planning. Current adapter/fence/owner truth and durable evidence are still required. |
| `ProcurementApprovalTransaction.ExecuteAsync` | Fresh contexts, one physical PostgreSQL connection/transaction, SERIALIZABLE, pre-snapshot branch then catalog fences, retry only confirmed40001/40P01 aborts, not unknown commit. Can be used without creating a Core approval case. |
| `ProcurementApprovalExecutionLocks.CoreAsync/LockRequestAsync` | Core read context enlisted on same physical transaction; scoped PR row locks. No separately opened Core context for consequential authorization. |
| `ProcurementAuthorityIdentity.ActorAsync/ResolveCanonicalAccountAsync` and `ProcurementExecutionRecordScope.Branches` | Active canonical account/worker/current permissions and Core branch projection; keep selected scope and membership separate. No hrm-admin grant. |
| `ProcurementSourcingDraftRoutes` | Existing own unlinked Draft CRUD and presentation surface. Its current edit/cancel path knows nothing about allocation release and cannot handle governed rows. Add explicit legacy isolation before any execution1 row exists. |
| `ProcurementCommercialCommandReceipt` | Requires an actual scoped PurchaseOrder FK and PO operation semantics. Cannot record RFQ commands without a fake/nullable PO relationship. Keep unchanged. |
| `ProcurementApprovalCommandReceipt` / Core `ApprovalDecision` | PR/Core-case-specific recovery / case-bound decision idempotency. Neither is a generic RFQ create receipt. |

The searched Core approval source has no generic durable business-command receipt abstraction. Reuse proven authorization/transaction/audit primitives, but an RFQ-owned receipt is justified by the actual missing target-appropriate store.

## 5. Actors & Participants

Original draft principal and canonical active worker; current administrator/ordinary requester rights are not automatically convert rights. No independent checker is required to save a safe draft because it does not publish. Future checker independence remains required before publication. Cancellation initially requires that same original principal and worker, explicit cancel permission and current scope. Offboarded-owner/reassignment recovery is not silently granted; it needs a separate supported policy.

## 6. Entry Points & Triggers

Proposed under existing `/api/procurement/v1/sourcing`:

- `POST /from-approved-sources`: title and selected request/line IDs, expected request versions, exact quantities; UUID Idempotency-Key. Caller does not supply event/allocation IDs, approved values, pins, worker or current permissions.
- `POST /{id}/source-cancel`: expected event version, original draft fingerprint and normalized reason; UUID key.
- `GET /{id}/source-draft`: owned retained draft/source/hold projection.
- `GET /command-receipts/{operation}/{key}`: discover recorded target/outcome when create response was lost, without knowing event ID. Only Create/Cancel allowlisted operations.

These are a proposed contract, not existing endpoints. No UI changes before API contract review. Keep the existing generic draft routes and explicitly separate their execution0 writes.

## 7. Preconditions

Feature/default-off execution option, dedicated execution DB configuration, exact schema readiness, current authenticated selected company, active canonical account/worker, explicit sourcing-convert/cancel plus module access, applicable branch/department scope and actual source records. Missing organisation/department-scope semantics block the affected scoped command; do not turn an unresolved scope into AllDepartments/OrganisationWide. Every source passes current approved evidence, competitive method, original matched quote rule (including explicit0), Finance authority/freshness and classification requirements.

## 8. Workflow

**Create transaction:**

1. Parse/bound body and exact quantities before acquiring owner fences; normalize title; validate UUID. Canonical wire fingerprint includes operation/schema/actor and original client fields, with exact decimal text and deterministic source ordering. Reject duplicate source line IDs or unsupported body shape.
2. Existing transaction acquires Core branch→catalog read fences before BEGIN/snapshot. Resolve current canonical actor, current record scopes and module/action permissions using enlisted Core context. No network I/O while locks are held.
3. Look up scoped durable receipt by operation/key; require current actor/worker and exact original wire fingerprint. Reauthorize the actual stored target's scope/ownership. If valid, return the stored response/status unchanged without new effects or new commercial eligibility checks. Conflicting original body gets409; foreign scoped receipt is not exposed.
4. For a genuinely new command, allocate server IDs once per command attempt, lock all selected PRs sorted by stable ID; read actual immutable PR evidence/current owner Finance/method/classification and full allocations/retained target ownership. Scope every source before exposing it. No filtering away inactive allocations.
5. Call planner only after all current owner validation. It validates every selected source/version and computes all effects before mutation. Persist event/lines, immutable original pin envelope, exact binding records, common allocation Reserved rows and PR held counters atomically. Set line route PendingSourcing only under supported transition. Save affected aggregate versions through context-owned CAS increment; never manually double-increment.
6. Write scoped audit, outbox fact and immutable RFQ receipt in the same transaction. Save to obtain actual event/source versions, encode a bounded versioned reply, then persist receipt. Commit. Public response does not claim sent/published.

**Cancel transaction:**

1. Resolve current actor and receipt-first retry under the same fences. Read own event/pins to discover source IDs, lock PRs sorted then event, and re-read event/version/pins. If discovery changed, abort; do not proceed with a different source set.
2. Restore the planner Draft only through a verified versioned codec and actual immutable bound evidence; validate expected event version and original fingerprint. Load full current ledger/target ownership. Do not rerun current method/catalog/Finance eligibility solely to release historical quantity.
3. Require every matching Reserved row's exact allocation/event/source/retained event-line ID and original quantity/value. Call Cancel; apply each Release once; update held caches, event Cancelled/reason, active source lines cancelled as an owned atomic transition, audit/outbox and receipt. Do not change PR Finance reservation or Ordered/Accepted quantities. PR route cache semantics must be specified/tested before writes; do not reset another event's route/holds.

## 9. Status / State Transitions

Governed Draft→Cancelled only in this adapter. Legacy Draft/Cancelled remain execution0; their status strings do not imply governed pins. PendingPublicationApproval/Published/Open are unreachable. In-memory planner Receipt is not persisted authority: verified restoration plus current event state/version and durable receipt distinguish a valid replay from an attempted new cancellation after terminal state.

## 10. Permissions & Data Scope

Current account permission AND actual record scope AND original owner/worker are required for create/read/cancel/recovery as applicable. No PR-read grant is inferred merely to display a sourced draft; source picker/detail requires a purpose-scoped projection. Bound owned immutable branch/source scope applies before counts/page and receipt serialization. Mutable current event header corruption must not retarget retained A evidence to B. Selected branch and allowed branches must both hold; department context must map to an actual Core-supported policy before constructing RfqCurrentActor. A reviewer role cannot bypass this scope.

## 11. Business Rules

Use the one common allocation ledger; RFQ Reserved consumes PRLine.AllocatedQuantity, not Finance budget or OrderedQuantity. Exact EstimatedValue is selected quantity×original approved unit estimate, numeric38,8 already supplied by P24. Explicit0 quotes is a valid voluntary competitive route; missing/null/invalid count blocks. MethodId+revision/code, original approval/version/worker and Finance pins remain immutable. No create accepts a previously active/released target tuple as a new event. Cancel uses target type plus ID and exact line binding, not an untyped UUID or current mutable line alone.

## 12. Data Fields & Ownership

### Minimal proposed additive persistence (no model implemented)

| Existing / proposed record | Fields / purpose |
|---|---|
| Existing SourcingEvent | Add execution schema/version default0, retained branch scope and maker canonical worker, configured method reference/revision, source-draft evidence pointer/digest as needed. Company/tenant/owner/status/version/number already exist. Execution1 pins cannot be changed through legacy setters. Nullable new fields preserve execution0 without guessed backfill. |
| Existing SourcingLine | Existing PR-line FK, event FK, description/unit/quantity/category/date and status remain. Persist planned stable line IDs. No parallel item or sourcing quantity balance. Source-linked fields must match immutable envelope and PR source. |
| Proposed SourcingDraftEvidence | Append-only schema1 original create intent/worker/scope plus versioned source pin payload/hash. Separate material columns for event/maker/branch/command/schema identities; JSON is retained evidence with strict decoder, not permission or ownership substitute. Original draft fingerprint does not change on cancel. |
| Proposed SourcingDemandBinding (one per selected line) | Stable event/event-line, request/PR-line, existing allocation ID, original approval submission/outcome/case/decision, original quantities/rate/value/currency/method/source revision and evidence identity. Immutable normalized relational ownership for shared ledger/pins. Existing allocation is the only quantity effect. |
| Proposed SourcingCommandReceipt | Real scoped RFQ event FK, Create/Cancel, UUID command key, actor principal+worker, canonical original wire fingerprint, response schema/status/body/digest, completed time and result version. Unique tenant/company/operation/key. Append-only; no required PO/PR/Core-case fiction. |

No generic “idempotency service” is introduced just for these records. Factor shared pure fingerprint/receipt-validation conventions only where behavior truly matches.

### Relational and mutation contract

All parents use stable tenant/company/ID keys. Bind event-line to actual event and PR-line to actual request; binding allocation must be the same tenant/company/request/PR-line and SourcingEvent target. Prefer explicit normalized binding FK constraints plus owned exact SQL overlays where EF cannot express immutable polymorphic equality; verify full structural definitions in readiness. Preserve existing P25/P26 manual FK overlays. Mutable status/current evidence pointers must never become EF alternate/principal keys.

Unique event+PR-line and event-line+binding/allocation prevent duplicate source consumption. Exact amounts/quantities and positive/nonempty identity checks, Execution1 required evidence, append-only evidence/bindings/receipt and immutable execution scope/maker/method require EF guards plus database constraints/privileges appropriate to the existing runtime model. New facts cannot be written by generic CRUD or direct permission hiding. Context revision guards require PR+event tracked aggregates for every real effect, including intermediate saves; all effects remain one transaction.

## 13. Screens & Data Views

Reuse existing RFQ list/detail/editor; sourced mode shows real current source quantities and held demand. Preserve imported controls and mark unsupported publish/dispatch/bids unavailable with reason. Legacy own drafts remain distinguishable. Do not use the new pure planner as browser state pretending persistence.

## 14. User Experience / Human Behaviour

Plain labels: “Save quote request”, “Demand held”, “Cancel quote request”, “Checking whether your save completed”. Loading/empty/403/404/stale409/validation422/unavailable503 and unknown-response states are distinct. Keep original UUID/body keyed to actor/company/branch until authoritative recovery; hide mutation buttons while outcome is uncertain. Scope navigation aborts/ignores stale callbacks and clears old details.

## 15. Notifications & Communications

Committed draft-reserved/cancelled facts use existing Procurement OutboxMessage with stable per-operation key and original correlation. No supplier email, invitation rows or delivery receipt. Notification failure does not undo a committed hold; operator retry/reconciliation retains original fact identity.

## 16. Documents & Attachments

No file requirement is invented for draft create/cancel. Source pin references original PR snapshot/specifications; neither current catalog nor current mutable line is historical original evidence. Future RFQ attachment integration must reuse Core private documents/current ownership/scanning/version/hash adapters and remains outside this slice.

## 17. Exceptions & Edge Cases

Duplicate source IDs, stale PR/event versions, mixed branches/company/currency/method, zero/unrepresentable/excess quantity, unconfigured Finance or scope, unknown target/status/legacy allocations, owner-line replacement, tampered evidence/hash/parent linkage, response loss, concurrent PO/RFQ/RFQ holds, same UUID across different target types, withdrawn catalog after valid hold and departed original owner. Unknown/inconsistent retained history blocks, never becomes empty capacity. Administrative recovery after offboarding needs a separately accepted capability.

## 18. Integrations / APIs / Events / Sandbox

Proposed Create response201/Cancel200: eventId/number/state/version, source-draft fingerprint, exact-string source quantities/value, reservation state and permitted next action (no publication). Recovery200 includes stored original status and response with no reserialization drift; unknown authorized key404 means outcome unconfirmed/not found, not proof to generate another key. Key mismatch409. API errors use existing bounded code/message/details conventions with no raw policy/SQL/private account facts.

Wire receipt fingerprint and planner fingerprint serve different purposes: planner Create includes server event ID, while a retried HTTP caller does not know it. Check original canonical wire fingerprint first, load the saved target ID/pins from receipt, then restore/recover; do not generate a new ID and compare it to the old planner hash. Store schema versions and original response bytes. Canonical numeric/string transport forms normalize to the same exact decimals only through the existing strict codec.

## 19. Audit / Security / Privacy

Original evidence and receipt append-only; status transitions carry actor/current canonical worker, before/after, normalized reason, command/correlation and source effects. Nonowner/cross-branch/other-company gets nonrevealing denial, including recovery when target ID initially unknown. No raw worker profile/HR/bank data or source allocation details for unrelated events. Runtime DB role remains finite; Core identity SELECT and no new Core case DML needed for create/cancel.

## 20. Reports / Dashboards / Metrics

Own sourced draft count, held/released quantities and cancellation age only after real reads. No dispatch/published/savings/quotes-received/award/AP metrics. Exact event estimate is commercial estimate, not Finance balance or payable amount.

## 21. Configuration & Localization

Narrow new capability for governed sourced drafts remains false by default and is distinct from global sourcing/publication. Existing method/policy and selected organisation currency apply; no new policy/grant defaults. Numbering uses existing ERP service if available, otherwise the established RF prefix/unique identity convention with later governed series work, not a second number master. Invariant exact decimal transport; locale display/time UTC as shared standard.

## 22. Non-Functional Requirements

Maximum200 unique source lines (or a smaller reviewed transport bound matching source/SQL budgets); bounded source/target reads and canonical action lookup. Lock order Core branch fence→catalog fence→BEGIN→sorted PRs→event. Complete retained history is required; if operational history needs a bound, exceeding it returns Unavailable rather than partial sums. No upload/network/notification operation under transaction locks. Fence guarantees are cooperative: branch-assignment/catalog writers participate; role/account/worker/organisation/direct SQL changes are not magically linearized by those fences.

## 23. Postconditions

Create has one real scoped event and actual source lines, immutable verified source evidence/bindings, exactly one Reserved common allocation per selected line, reconciled held caches, audit/outbox and durable original-key receipt. Cancel preserves evidence, marks owned rows Released/event Cancelled, releases each hold once and leaves ordered/accepted/Finance unchanged. Every returned version is the actual persisted context version.

## 24. Failure & Recovery

Validation/mismatch/CAS/constraint failure rolls back event, lines, binding, allocations/counters, audit/outbox and receipt together. Serialization/deadlock retries use the existing full transaction retry with stable original command/body; do not retry arbitrary timeouts/disconnections. Replays reauthorize current actor/worker/target but do not require a now-withdrawn commercial offer to exist. A transaction returning a response is not successful until commit; unknown outcomes preserve key/body for receipt recovery. Recovery GET never writes or manufactures a missing receipt.

Future additive migration starts from reviewed current central P26 model. Up preserves all legacy rows as execution0; never invent branch/worker/pins from current account. Down must lock inspected sourcing/evidence/binding/receipt/allocation/audit/outbox dependencies before EXISTS; block used/configured governed evidence or any irreversible legacy mutation, preserve existing P24/P25/P26 overlays. No second allocation widening: P24 already has38,8 and narrowing loss guards. Parent owns generation and actual fresh/upgrade/used/unused rollback rehearsals after model review.

## 25. Acceptance Criteria

Only actually approved competitive sources create a real linked draft with exact held amounts; legacy CRUD cannot mutate it. Current canonical access and complete ledger scope hold before new effects and replay. Cross-target concurrent reservations never overallocate. Cancel releases the exact original target-line hold once after withdrawal without touching Finance. Unknown writes recover original status/body under the same key, with no duplicate event/allocation/audit/outbox. Immutable pins/relationships/default-off readiness deny incomplete or tampered schema. No publish/dispatch capability appears.

## 26. Test Scenarios

- Pure planner tests at40ef66b remain required, not assumed executed by this plan.
- Model: stable scope/ID keys, no mutable EF principal key; append-only identity/evidence/receipt; exact pin/binding/event-line/request/allocation relations; numeric38,8 and context-owned version increments; legacy execution0 preservation.
- Mapped HTTP: current explicit action/worker/company/branch/department denials, raw exact input errors, no-effect footprint, same payload/key replay, changed original version/reason/quantity conflict, source-less legacy update/cancel blocked on execution1, sourced read privacy.
- Actual restricted PostgreSQL: raw mismatched target/event-line/allocation/request FKs, full readiness overlay tamper checks; actual command create, independent-context exact0.00000001 read, cancel, unchanged Finance/ordered/accepted and audit/outbox/receipt cardinalities.
- Concurrent RFQ/RFQ and RFQ/PO holds on the same PR using real row locks; losing command conflict/confirmed-abort retry, no negative capacity. Alias/offboarding/branch revocation plus replay privacy at existing owner boundaries.
- Forced rollback after allocation before receipt; commit-loss recovery without target ID; simultaneous identical UUID/body returns same event/outcome; same UUID/different body denies. Retained source/pin/hash/target-line replacement and unknown legacy histories fail closed.
- Fresh migration twice, protected legacy upgrade twice, unused Down/Up, used Down veto, prior P25/P26 owned constraints intact; no production application. Later real browser uses unchanged imported RFQ UI and actual API/SQL verifier.

## 27. Ownership / Dependencies

Procurement owns RFQ intent, pin/binding/receipt and common PR allocation effects. Core owns identity/org/record scopes and shared approvals/infrastructure; Finance owns PR reservation truth. No new approval engine or PO placeholder. Parent owns model integration/migrations/resource gates/release. This plan can be reviewed independently while P26 completes; no old branch snapshot is adopted wholesale.

## 28. Open Decisions / Assumptions

Before implementation review must settle exact current Core department-scope projection (missing blocks restricted commands), normalized binding/SQL constraint shape, versioned restore codec and response format, event-version/PR-route-cache changes on create/cancel and scoped recovery permission (reuse original action, not broad read). Actual publication policy/finite authority and external supplier/document adapters remain blocked future work. No reviewer approval is asserted here; this is the concrete plan required before model/HTTP work.
