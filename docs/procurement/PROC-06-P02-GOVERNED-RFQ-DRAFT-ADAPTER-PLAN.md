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

## Technical blocker resolution amendment — source inspection, not implementation

### A. Actual Core scope authority and supported first mode

The following distinctions are material:

- `ProcurementScopeMiddleware` in `ProcurementHostIntegration.cs` reads actual tenant Core `LocalUsers`, `UserBranchAssignments` and `WorkLocations`. Active assignments create confined branch access. `ProcurementExecutionRecordScope.Branches` re-reads current assignments and current selected/target branch validity inside the enlisted transaction. These are existing authoritative branch facts; no RFQ branch master is needed.
- `ProcurementAuthorityIdentity.ResolveCanonicalAccountAsync` resolves the active, unlocked canonical LocalUser and `ProcurementWorkforce.ResolveActiveWorkerHome` in the selected legal entity. Returned DepartmentId is the worker's current home organisation-unit affiliation, not a grant to view/procure all records of that department. Canonical WorkerId is required for commands; the early shell-scope WorkerId or a role label alone cannot authorize them.
- Middleware lines322–333 allow an **unconfined** user's X-Shell-Location to resolve to an OrgUnit when it is not a WorkLocation, then store it as `scope.OrgUnitId`. This is selected context, not a user-department access assignment. Existing spending-authority configuration also uses OrgUnitId to narrow grant management; a finite authority's `AllDepartments/DepartmentRef` is that authority's business limit, not a generic department-read/convert grant.
- No authoritative user→allowed-departments or generic resource data-scope assignment equivalent to UserBranchAssignments was found in the inspected current Procurement/Core authorization paths. Do not copy HRM organisation/role records to manufacture one.

**Supported initial RFQ adapter mode is existing company/branch scope only**, with current canonical identity/action permissions and original-owner restrictions. Company mode requires an otherwise valid canonical company membership and no current branch restriction; branch mode uses current Core assignments plus selected branch. Null branch records are usable only in genuinely unrestricted company context. Source DepartmentRef remains original business context and is retained, but is not a generic department authorization claim.

Before constructing RfqCurrentActor, the adapter must reject any nonnull selected OrgUnitId or a configuration requesting a department-limited mode with `RfqDepartmentScopeUnsupported` (unavailable actionable error, no writes). It must not clear the selection or treat it as unrestricted company context. No public command accepts `AllDepartments`, `Departments`, `OrganisationWide`, allowed branch arrays or worker IDs. For the explicitly supported company/branch mode only, the pure actor projection can use AllDepartments=true to mean **this mode has no department-specific scope policy**; it is not a newly granted department capability. Reject unsupported modes before invoking the planner, including receipt replay, reads and selectors. Future department support requires a real owning Core policy/assignment interface and tests; no schema is added for it here.

Current branch constraints still apply to the unrestricted company projection: no assignments means no current branch-assignment restriction only after the existing canonical/current-scope checks, never because an empty client list says so. OrgUnit/departments required later for finite publication authority remain separately evaluated against the full event and are not enabled by draft scope.

### B. Versioned immutable restoration — reuse patterns and exact proposed boundary

Inspected existing owner patterns:

- `PurchaseRequestAssignmentIdentity.ReadVerified` and `PurchaseOrderAssignmentIdentity.ReadVerified` hash retained JSON, decode their known typed envelope and compare tenant/company/principal/worker/grant columns to the actual owned row. `PurchaseOrderWorkerIdentity` explicitly retains original participant workers rather than re-resolving past makers through current account membership.
- `PurchaseRequestApprovalPinning.VerifyBusinessEvidence` binds the PR's retained request/evaluation/policy/Finance evidence. It also compares current business material where intended for **new execution**. Do not call it against today's changed PR to decide whether an old RFQ quantity hold may be released.
- `ReceivingIssuedAuthority.Verify` plus `ProcurementReceivingSourceResolver` verify actual issued/submission/source records, their commercial/source JSON digests and exact typed values before projecting receipts. Their PO/source types are not RFQ restoration DTOs; reuse the owner-proof discipline, not a fake issued PO.
- `ProcurementEvidenceEncoding` supplies exact invariant decimal formatting, canonical policy digest and retained payload SHA utilities. It does not by itself validate JSON ownership, business authority or completeness.

Proposed application boundary: **`GovernedRfqDraft.RestoreVerified(ownedEnvelope, typedOwnedPins, ownedRelations, historicalSources)`**, internal to a supported owner adapter; no public deserializer or caller-provided `verified=true`. `ownedEnvelope`/relations come from actual scoped persisted rows, not HTTP/body JSON. Inputs include actual SourcingEvent/current lines, original SourcingDraftEvidence and normalized SourcingDemandBinding rows, original source approval submissions/outcomes/decisions and their retained immutable request/policy/evaluation/Finance payloads. All lookups use tenant/company stable IDs. No missing record is replaced with today's method/catalog/PR/Finance state.

Restoration must perform, before constructing any Draft:

1. Require execution schema1, known envelope schema1, actual nonempty event/source/allocation IDs, same tenant/company, immutable original maker principal+worker and branch/currency/method, correct create-command/evidence pointers, unique line/source/allocation identities and complete exact relation counts. Unknown execution/schema versions, incomplete pins, duplicate/unknown fields or malformed JSON deny restoration. Existing execution0 records cannot be upgraded by deserialization.
2. Verify original retained JSON bytes against the stored SHA using the named schema's encoder. The SHA is integrity evidence within append-only owned rows, not a signature permitting caller-supplied replacement. Verify scalar columns and every normalized pin equal the typed decoded projection, including original submission/business version/Core case/outcome/decision/method revision/quote0 and all source hashes/worker identities. Unknown fields cannot be silently discarded and then rehashed as a different original. Reject duplicate JSON properties; ordinary permissive JsonSerializer alone is insufficient.
3. Verify each original PR schema2 request snapshot/evaluation/policy/Finance snapshot hash and actual immutable lineage with the existing owning field/digest contracts. Parse the original line from the original snapshot by exact line ID; check original unit/category/department, approved estimate and selected quantity bound. Compare retained worker IDs with original immutable PR identity evidence, never today's account mappings. Approved outcome and final Core decision/completion must name that same retained source; original grant/policy record retention is not current execution authorization.
4. Decode numeric tokens or invariant strings losslessly through existing exact codecs. Quantity/rate retain18,4; value retains38,8 **and actual .NET exact representability**. Recalculate selected quantity×original retained source estimate with ExactDecimalArithmetic and require exact equality to the saved allocation/binding value. Never recompute from current catalog/current PO/current PR prices. This is verification of this execution1 envelope, not a backfill of historical execution0 rounded rows. Unknown legacy original value stays unknown.
5. Rebuild the original immutable schema1 draft fingerprint using explicit versioned field order/ordered source pins and exact decimal string encoding. Do not use a changed future runtime DTO's default serialization as an unversioned codec. Require original expected fingerprint matches; verify current event state/lines and immutable scope/source pins without pretending current row version is original business version.
6. For active Draft cancellation, hand a verified original Draft plus actual current full allocation/owner ledger to Cancel. That helper additionally requires the exact retained EventLineId owner and original Reserved quantity/value. It emits CAS/release instructions; it does not write. For Cancelled read/recovery, preserve original create evidence and the separately recorded cancellation fact/receipt. Do not overwrite original fingerprint with a terminal-state hash or run Cancel again to hydrate history.

This requires a narrow restoration factory/codec to be implemented and independently reviewed before persistence activation; the current planner's private/assembly factory is **not yet a verified persistence deserializer**. The proposed normalized evidence/relations remain required; no current JSON-only workaround is approved.

Historical cancel intentionally verifies retained integrity/relationship/actual still-held quantities and **current canceller** authority/scope, but does not demand that an old catalog offer, method publication or original approver account is still currently active. A new create still requires today's owner eligibility/Finance. If original retained evidence or allocation ownership cannot be proven, fail closed with repair/recovery guidance; never invent a zero balance or release another hold. Original offboarded participants remain attributable; the canceller still needs the current supported original-owner identity until explicit reassignment exists.

### C. Purpose selector and cache/invalidation contract

Proposed route: `GET /api/procurement/v1/sourcing/approved-sources?q=&page=&pageSize=`. Purpose is prepare a governed RFQ draft, so require current `procurement-access` and `procurement-sourcing-convert`; this does **not** imply broad PR-read, PO-read, worker-directory or supplier/bank access. Default25/cap25 rows, bounded search100 characters with escaped wildcard literals, deterministic required-by/request/line/ID ordering and bounded candidate evaluation. A page cannot be filled from an unbounded in-memory source scan.

Every GET resolves canonical tenant/company/principal/worker, current configured permissions, supported scope mode and current Core branches/selected branch as above. Current query/filter/header values never override those facts. Reuse a read-only invocation of the existing physical-connection pre-snapshot branch/catalog fences where consequential current eligibility is advertised; no source row lock or GET write/new state is needed. Define before implementation how bounded owner eligibility is evaluated: apply guaranteed scope/retained approved evidence/current method route filters before count/page; if complete rule/Finance/ledger reconciliation is necessary per candidate, bound the candidate window explicitly and report unavailable/hasMore semantics instead of claiming an exact eligible total from partial evaluation. Do not return a broad current-header count and then hide foreign retained sources after paging.

Minimal successful row: requestId/lineId, requestNumber, short original description, unit/category, required-by date, current requestVersion, exact availableQuantity and original approvedUnitEstimate, currencyCode, configured methodId/code/revision and explicit minimumQuoteCount. These are purpose-necessary approved-demand facts; no raw snapshot/policy/Finance JSON, original requester/maker worker IDs, Core case/decision IDs, authority grant IDs, private contact/banking fields or unrelated target/allocation rows. Request version and checkedAt are a snapshot hint for a future command, not a guarantee of reservation or publication. Unknown history/current owner eligibility is unavailable, never availableQuantity0 with a success implication. Expose only allowlisted safe blocker codes for already authorized candidate rows; never raw exception details.

**No shared/server eligibility cache initially.** Responses use `Cache-Control: private, no-store`; no offline or stale-service-worker authorization. Current scope/action/worker checks repeat on every request and receipt lookup. If a later cache is proposed it requires an owning authorization revision/invalidation contract; time alone does not prove current membership. No such revision is fabricated here.

Client query identity includes module/purpose, authenticated principal+canonical worker, tenant/company, selected branch, server-confirmed supported scope mode/current capability context, search/page and current sign-in generation. Scope/account/module navigation clears sensitive detail/actions and cancels/ignores old callbacks. Current permissions/capabilities are UI hints only; a cached grant never authorizes POST. An unsupported OrgUnit selection produces explicit unavailability rather than cached company-wide rows.

After authoritative create/cancel/recovery completion, invalidate the exact actor/company scoped RFQ list/detail, RFQ approved-source selector, PR detail/approved-demand source views and PO approved-source availability affected by the shared ledger. Do not delete recorded historical allocations or optimistically claim a counter change before confirmation. Keep original uncertain command UUID/body in a separate pending-command record keyed to actor/company/branch; request retry/refetch must not generate a new mutation key. Receiving/AP/budget actuals are unaffected. A source command409 refreshes current source/event versions and asks the user to review; it does not silently alter quantity or replay with a fresh key.

### D. Review decisions and next bounded implementation boundary

This amendment proposes resolving the department blocker by explicitly supporting current authoritative company/branch scope and rejecting unsupported department modes. It defines the missing typed restore/receipt/selectors without adding them. Parent review must accept scope behavior, strict evidence schema/normalization, bounded selector completeness/count semantics and route/cache update rules before model/HTTP work. Implementing a raw JSON→Draft cast, generic receipt backed by fake PO, selected-department “grant”, stale cached eligibility or current-price historical repair would violate this plan. P26's central actual model/migration/gates remain the prerequisite baseline; no SDK/Node/DB/browser jobs were run for this amendment.

## Final source review checkpoint and migration lessons

Read-only comparison against the P26 worktree at `bedcb5f` found **no changes** to the audited `ProcurementHostIntegration`, `ProcurementAuthorityIdentity`, `ProcurementExecutionRecordScope` or `ProcurementApprovalTransaction` files relative to the RFQ planning base. The company/branch versus selected-org-unit distinction and existing pre-snapshot fence/transaction contract therefore remain applicable. The inspected current files contain `20261020164745_P26InvoiceMatchingExecution`; generation must still inspect the then-current central snapshot/history rather than treat this source checkpoint as a frozen integration baseline. Existing source precision and P25/P26 exact ownership overlays remain owned by their original migrations. This checkpoint does not claim that P26's ongoing actual tests/rehearsals have passed.

### Explicit remaining plan choices

1. **PR route and aggregate versions:** create may change Unassigned→PendingSourcing only after competitive eligibility. Cancellation preserves PendingSourcing as the intended competitive route, matching the existing PO release pattern of retaining DirectPurchase; a route string is not the quantity balance. Do not reset a shared PR line to Unassigned or another route because one event was cancelled. Future route reassignment requires its own governed command. PR status/approval/Finance remain unchanged; only AllocatedQuantity and owned modification metadata change. Group instructions by request, check the original request version once, apply all source-line effects, and let context CAS advance the tracked PR aggregate once per actual persistence save. A receipt save with no new PR change must not increment it again. Event version likewise advances only for actual owned transition. Verify actual saved versions in replies.
2. **Bounded selector semantics:** first implementation returns a page of authorized candidate rows, at most25, with each row Ready or Unavailable after bounded complete owner/ledger reconciliation. Guaranteed current tenant/company/branch and retained approved-source identity filters run in SQL before candidate count/page. Return `candidateTotal` explicitly labelled as pre-reconciliation candidates, never `eligibleTotal` or an unlabeled total suggesting all can reserve. Each unavailable authorized candidate returns only its IDs/label and allowlisted reason; omit quantity/price/private evidence until owner verification succeeds. Keep stable deterministic paging and no recursive page-fill scan. If the complete history limit is exceeded, that candidate is Unavailable; never sum a prefix or fabricate zero availability. This removes the unresolved exact-eligible-total promise. All returned versions remain advisory until POST rechecks.
3. **Recovery authorization:** use the original action permission (Create→sourcing-convert, Cancel→sourcing-cancel), module access, current canonical actor+worker and actual immutable target ownership/company/branch; no broad read grant or dedicated unlimited recovery role. No client event ID is required for Create recovery. Receipt selection must be scoped/actor-bound before serialization and reauthorize stored target. Unknown or inaccessible receipts use nonrevealing404; lost permission denies403. The result cannot confirm absence of an earlier commit.
4. **Restore implementation boundary:** the strict versioned factory/codec in amendment B and normalized owned relationships are still required code work, not an unresolved authorization policy. Existing planner receipt/Draft factories cannot be used directly as a trusted DB deserializer. Define exact typed persistence columns alongside implementation review before model generation; no alternative generic JSON or PO-linked receipt is accepted by this plan.

### Mandatory physical key / migration discipline from P26

Every **new alternate key on an existing table** must have an explicit short stable physical name (`HasName`) and be tested with the actual Npgsql relational model. InMemory model tests cannot detect PostgreSQL's63-byte identifier truncation. A generated migration must not rename/drop a retained key with dependent FKs simply because a new convention-generated key truncates to the same name. Review the diff against the actual preexisting database constraints and retain their physical identities.

Use stable EF tenant/company/ID relationships, explicit short unique-index names and ValueGeneratedNever for mutable relationship/completion properties. Do not make counters, state, completion timestamps or later source-binding pins alternate/principal EF keys. Where a cyclic exact ownership binding requires it, use migration-frozen literal deferred NO ACTION FK SQL and full structural manifest verification, preserving all original P25/P26 overlays. Runtime helper edits must not silently rewrite older migration SQL. Exact FK tests must include wrong source/event-line/scope and named immediate validation in actual PostgreSQL.

Any RFQ history/receipt usage Down guard must first lock every inspected affected/dependent table in a documented consistent order before EXISTS checks; a same-transaction unlocked check can race a new command. Veto used governed draft evidence, binding/receipt/allocation effects and relevant audit/outbox facts before dropping/narrowing anything; normal create/update history counts as usage, not only final publication. Never round/drop an unknown historical amount or infer original values from today's price. No new migration, alternate key or DB job is created here.

### Remaining activation gates

- Parent acceptance of this proposed scope/selector/restore/route behavior and current P26 integration baseline.
- Implementation and independent review of the versioned restore codec, normalized owned evidence/receipt mappings, legacy execution0 write isolation and exact structural readiness.
- Central model/migration generation from the then-current verified model with physical-name/overlay/Down review; no stale snapshot import.
- Real current actor/Finance/classification/source/CAS/replay handlers and finite runtime grants; mapped/domain/relational/actual concurrency/recovery/precision/migration/browser tests.

No additional department master/grant, allocation precision migration or approval engine is needed for the bounded draft milestone. Actual publication remains blocked by its separate governed policy/finite authority/Core/supplier/document contracts. All evidence in this checkpoint is source inspection only.

## Explicit action catalogue and capability prerequisite

Current `Application/ProcurementActionCatalog.cs` registers only sourcing-read/create/edit for existing sourcing drafts; **sourcing-convert/cancel are not currently registered**. `ProcurementPermissions.ResolveConfiguredActionsAsync` filters configured role actions against that catalogue, and `AddPolicies` creates policies from the same keys. Therefore planner permission strings alone cannot authorize or expose the proposed HTTP operations.

Before a governed adapter can activate:

- Register exact separate `procurement-sourcing-convert` and `procurement-sourcing-cancel` action keys in the existing catalogue with **empty bootstrap grants**. Keep generic sourcing-create/edit behavior legacy-only; do not alias them to the new actions or auto-grant existing buyer/manager roles. Add the same explicit action descriptors to Core's existing role-management permission picker/validation contract; no parallel role store. Existing ERP administrator handling must remain the current central policy, not a new hrm-admin or unlimited financial grant.
- Conversion/new purpose selector uses convert; cancellation uses cancel. Owned sourced reads may use convert or cancel under explicit server composition, while original-key recovery requires its original operation's action. Neither legacy-read/create/edit alone nor PR/PO permissions grants these actions. Every command re-resolves current configured actions and canonical worker; role label/old JWT/UI hiding is insufficient. Existing catalogue fallback must be tested: empty/unknown-action rows cannot bootstrap these newly empty-grant actions.
- Add a narrow option such as `ProcurementSourcingDraftExecutionOptions.Enabled=false`, execution-database-configured requirement and exact owned schema readiness. Publish a separate `capabilities.sourcingSourceDrafts` only when those technical checks pass; include explicit current convert/cancel permission hints. Keep existing `sourcingDraft` for legacy CRUD separate and global `sourcing`/publication/dispatch false. Metadata capability never grants command authority. No permissive route is mapped because metadata is true alone.
- Legacy generic edit/cancel/detail/list must isolate execution0 or deliberately route execution1 through the governed owner/action path; they must not bypass new permissions, retained scope/pins, shared-ledger release or current canonical worker. Existing generic create must not accept caller-provided execution1/source/pin fields.

Required role-management/mapped tests: catalogue contains both actions exactly once; ordinary bootstrap employee/buyer/manager and legacy sourcing-only roles do not receive them; explicit active Core role action grants round-trip through existing role create/update/read and resolve to policy checks; archived/revoked/missing configured role/unknown action does not silently grant; current permission removal denies command and recovery despite stale claims. Assert convert-only cannot cancel, cancel-only cannot convert, legacy-edit cannot cancel governed event, unsupported department mode denies even with action, and capabilityfalse/schemaunready blocks actual routes while legacy CRUD remains functional. Tests must also verify no PR/PO/publication authority is newly assigned. These are required future tests, not executed evidence; this amendment changes documentation only.
