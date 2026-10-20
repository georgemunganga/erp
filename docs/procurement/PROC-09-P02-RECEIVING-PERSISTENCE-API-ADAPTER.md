# PROC-09-P02 — Receiving persistence and API adapter

**Status:** Proposed implementation contract, source audit only. No migration, runtime adapter, test execution or activation is delivered by this document.
**Baseline:** Combined PO/settings `80ff610` plus reviewed fulfilment source `db1f033` (merged in this documentation branch).
**Tracking:** [module-connect#31](https://github.com/georgemunganga/module-connect/issues/31), dependency [#5](https://github.com/georgemunganga/module-connect/issues/5).
**Standard:** GW-ENG-STD-001, Tier 3; supplements [PROC-09-P01](PROC-09-P01-CONTROLLED-RECEIPT-EXECUTION.md).
**Implementation sequence:** [P25 adapter plan](PROC-09-P25-IMPLEMENTATION-PLAN.md). All new route/schema names below are proposals, not available APIs.

## 1. Feature Identification

Procurement → Receiving and Inspection → Controlled goods/service receipt → Persist drafts, record goods, inspect, accept, certify and expose accepted-source evidence.

P25 is the smallest useful live adapter: existing receipt CRUD followed by goods physical disposition/inspection/positive acceptance, or service certification, against a delivered immutable PO. Return/reversal domain contracts remain retained but their runtime execution is disabled until authoritative consumption and owner-correction evidence exists.

## 2. Purpose & Business Outcome

Make an existing Purchase Receives screen save real Procurement records and produce trustworthy accepted quantities for the invoice matcher. A receipt is neither a stock balance nor a Finance posting. Operators can distinguish delivered, held, rejected and accepted quantities, see remaining PO capacity and recover an uncertain command without creating another receipt.

## 3. Scope & Exclusions

Includes partial deliveries, four-decimal quantities, exact eight-decimal net values when CLR decimal can represent them, required inspection, quantity-priced service periods/deliverables, current scoped queues, document references and durable event publication.

Excludes authorizing over-acceptance, reinspection, conditional acceptance, serial tracking redesign, arbitrary milestone-value allocation, new PO revisions, invoice posting, Inventory/Asset execution and financial release. Truthful physical over-delivery and rejection evidence is retained as held/exception intake; it does not authorize acceptance or stock. Draft save does not require an invented published policy. Consequential transitions require a published supported receiving policy. Unknown classification blocks recording/certification; unknown consumption blocks negative effects. No receipt count, accepted counter, absent invoice or disabled matcher is evidence of zero consumption.

## 4. Parent & Related Features

Reuse these existing Procurement tables: `goods_receipts`, `goods_receipt_lines`, `receipt_inspections`, `service_acceptances`, `supplier_returns`. Existing invoice lines reference receipt-line/service IDs; preserve those identities.

Reuse `ControlledReceiving`, `ReceivingContracts`, `ReceivingEvidence`, `ExactDecimalArithmetic`, the canonical evidence codec, Core identity/current branch fence, the same-Postgres transaction coordinator, existing Procurement policy lifecycle, shared document access, audit and outbox. No second employee/site/supplier/approval master or Finance ledger.

## 5. Actors & Participants

Receiver, policy-assigned inspector, policy-assigned service owner, permitted goods certifier, buyer observing own PO progress and scoped auditor. Future correction requester/authorizer and downstream owner are distinct responsibilities. Resolve active canonical WorkerId from current identity; compare worker IDs for maker/checker, including aliases. Retained source workers remain historical evidence after offboarding; they do not authorize a current caller.

## 6. Entry Points & Triggers

Reuse `/procurement/workspace/purchase-receives` list/editor/detail and reusable line editor. Entry from an issued order carries exact PO, issued-version and PO-line IDs. Server-backed source picker supplies remaining quantities; descriptions are display only. Inspection and service tabs/queues reuse the same receiving workspace. Existing frontend local `Received (demo)` save and item-name matching cannot serve live mode.

## 7. Preconditions

Resolve in the command transaction: active principal, exact command permission, current company and branch memberships, selected branch, active Core location, source PO ownership/scope, supported issued/submission schema and canonical hashes, exact line lineage, successful authenticated PO delivery evidence and current issued authority.

Parse `PurchaseOrderSubmission.SourceEvidenceJson` by exact `orderLineId`: retained PR line/submission/outcome, source hash, category, `PurchaseType`, currency/UOM and approved rate. PurchaseType is already sealed; current mutable Item flags are not a substitute. No name-based join. Missing, duplicate, mismatched or unsupported references fail closed.

Receiving an existing obligation does not require a supplier to be eligible for a new PO; suspension routes the existing obligation under policy. It must not erase real delivery. Receiving never reserves Finance funds again.

## 8. Workflow

1. List scoped delivered issued PO lines and existing receipts. Draft creation pins intended issued version and line IDs but creates no acceptance authority.
2. Save/edit/delete only a genuinely unrecorded draft using expected record version. Reject cross-PO lines, unsupported values and hidden scope changes. Draft documents use the shared document service.
3. Record physical goods once. Persist the existing header/line's immutable physical fields and sequence; hydrate a `GoodsPhysicalObservation` from that row. Recording may leave all goods held/rejected; it does not accept stock.
4. If policy requires inspection, persist one final `ReceiptInspection` for the whole held quantity. Preserve qualified versus rejected quantities and independent inspector identity. No automatic acceptance from the inspection status alone.
5. Certify an allowed positive goods quantity or service deliverable. Append `ReceivingAcceptanceEffect`; update derived existing counters, per-issued-line sequence, audit, command receipt and outbox atomically.
6. Expose authoritative `InvoiceAcceptedFulfilment` using the existing acceptance record ID. Return/reversal requests can be documented as drafts, but negative execution remains unavailable without a complete consumption and owner-resolution adapter.
7. Downstream handoff is Pending/Not configured until a real owner acknowledges it. Notifications run after business commit through the existing delivery mechanism.

## 9. Status / State Transitions

| Record/action | From → To | Guard | Authoritative effect |
|---|---|---|---|
| Goods draft save/delete | Draft → Draft/deleted draft | create/edit permission, ownership/scope, expected version, no physical fact | None |
| Record physical line | Draft → Recorded/Held/Rejected | record permission, published policy/classification/site/source proof | Freeze physical observation, increment shared line sequence |
| Inspect | Held → Inspected/Rejected | inspect permission, assigned worker/checklist, independent worker where required | Freeze existing inspection; no accepted entitlement |
| Accept goods | Held/Inspected → Partially Accepted/Accepted | accept permission, policy, qualified physical capacity and source capacity | Positive acceptance effect and derived balances |
| Certify service | Pending Certification → Accepted | certify permission, assigned owner, explicit complete period/deliverable and rate | Immutable service certificate and positive effect |
| Cancel draft | Draft → Cancelled | edit/cancel authority, no recorded fact | No financial/acceptance effect |
| Reverse/dispatch return | Not enabled in P25 | complete current consumption + owner correction + independent authority needed | No negative effect while adapter unavailable |

Header progress is derived from its lines. PO commercial status, receipt progress and invoice progress remain separate dimensions. A recorded row cannot be reopened as a draft. P25 permits exactly one final inspection per physical observation; revised disposition requires a later compensating contract.

## 10. Permissions & Data Scope

Proposed catalogue additions, with final action identifiers reviewed alongside existing naming:

| Operation | Server permission | Scope/assignment |
|---|---|---|
| List/detail/source picker | `procurement-receipt-view` | Current company + selected branch + actual record/source scope |
| Draft create/edit/cancel | `procurement-receipt-create`, `procurement-receipt-edit` | Own draft, current permitted branch/site; edit does not grant certification |
| Record goods | `procurement-receipt-record` | Scoped source and receipt, allowed site |
| Inspect goods | `procurement-receipt-inspect` | Above + policy InspectorWorkerIds |
| Accept goods | `procurement-receipt-accept` | Above + policy maker/checker conditions |
| Certify service | `procurement-service-certify` | Above + policy ServiceOwnerWorkerIds |
| Reverse/return | Existing pure constants, not enabled | No grant or button bypasses missing authoritative adapters |
| Export/audit | Existing explicitly suitable audit/export grant, otherwise new reviewed grant | Same source scope, sensitive export logged |

Command permissions may imply narrowly documented task-detail visibility for that command, as PO Finance/dispatch do; they do not confer unrestricted procurement reads. Filter accessible resource IDs and queue state before count/pagination. Honor explicit selected branch even for an organisation-wide actor. Revalidate current principal/worker/scope/permission on exact replay and recovery; old receipts are not authority.

## 11. Business Rules

- Accepted capacity is cumulative for exact issued PO line, across every receipt and service fragment. Lock/CAS the same line state before calculating it.
- Physical received/initially rejected are immutable facts. `RecordGoods` intentionally does not bound cumulative physical observations by ordered quantity: actual delivery can exceed it, and several receipts can await inspection concurrently. Retain truthful received/rejected evidence and expose an over-delivery/held exception. Such quantities create no extra accepted authority, inventory posting or available invoice quantity. Do not add a physical-capacity rejection that discards actual evidence. A replacement remains a new observation; it cannot reuse the original physical acceptance capacity.
- Goods positive effects cannot exceed qualified physical quantity or reuse previously certified physical capacity after a return/reversal. Acceptance capacity remains independently bounded by sealed source authority.
- Service net amount equals exact quantity × sealed net unit price, before tax, with no additional discount inferred. Unique deliverable and completed period are required. No unsupported UOM, FX, tax-inclusive proration or current catalog rate substitution.
- `AcceptedQuantity` counters are projections, never evidence. Compare them with reconstructed effects; corruption is a blocker, not a reason to trust the larger balance.
- Store canonical DB-microsecond UTC timestamps; enforce sequence/chronology and no fact preceding retained delivery evidence.
- Zero quantity, negative positive-effect amount, missing classification/policy and unverifiable pins fail closed. A legitimately zero-price source can only follow the existing pure helper's reviewed constraints; do not invent minimum financial thresholds.
- Do not carry unknown/null purchase type forward as Goods. Classification policy must explicitly map the sealed value and category to Goods or Service; ambiguity blocks.

## 12. Data Fields & Ownership

| Existing record | Proposed extension / adapter mapping | Owner / mutability |
|---|---|---|
| GoodsReceipt | IssuedVersionId, SubmissionId, BranchRef, final receiver WorkerId, recorded time, source revision/hash; existing SiteRef, note, receiver subject retained | Procurement; header source/site/physical provenance frozen after any recorded line |
| GoodsReceiptLine | ReceivingSourceStateId, InitiallyRejectedQuantity, PhysicalObservationId, PhysicalPolicyVersionId, PhysicalEvidenceRef, PhysicalSequence, PhysicalEffectKey, PhysicalRecordedAt | Procurement; physical fields frozen; `GoodsPhysicalObservation` is hydrated, **not a parallel mapped table** |
| ReceiptInspection | PhysicalObservationId, ReceivingSourceStateId, QualifiedQuantity, InspectorWorkerId, PolicyVersionId, EvidenceRef, LedgerSequence, EffectKey | Existing inspection is the immutable final evidence; hydrate `GoodsInspectionEvidence` with existing inspection Id |
| ServiceAcceptance | ReceivingSourceStateId, source issued pin, OwnerWorkerId, PolicyVersionId, EvidenceRef, certification effect reference, CurrentAcceptedQuantity/CurrentAcceptedNetAmount derived companions | Existing certificate is matcher identity; freeze certified business fields |
| SupplierReturn | Existing draft/reference retained | Negative execution deferred; no duplicate return master |
| ReceivingAcceptanceEffect (new) | Existing pure immutable fact; scoped source/state reference, original positive effect, typed goods-line/service FK, signed exact quantity/net, worker/policy/document/sequence/key | Procurement append-only entitlement history; not a receipt master |
| ReceivingSourceState (new) | One immutable pin/classification JSON+digest per issued line; PO/submission/issued/line/PR IDs, current sequence/CAS, derived balances | Procurement concurrency/projection record; immutable pin, controlled mutable counters only |

Map observation identity explicitly: persist the pure observation's generated Id as `GoodsReceiptLine.PhysicalObservationId`; reconstruct the same Id, never generate a new one on read. Inspection uses existing `ReceiptInspection.Id`. AcceptanceRecordId remains `GoodsReceiptLine.Id` or `ServiceAcceptance.Id`. Base CreatedBy/At and current derived Version are not substitutes for pinned actor/business provenance.

Reuse `ProcurementCommercialCommandReceipt` (PO FK already fits receiving's source PO), with distinct receiving operation names and target ID in sealed response/fingerprint. No new idempotency engine/table is required. Replay fingerprints include exact source/version, target, expected version, quantities, document refs and operation. If the existing DTO serializer cannot carry these identities, extend its operation allow-list/response contract additively; retain old PO behavior.

Finance owns accounting/consumption posting state; Inventory owns stock; Assets owns its register; Core owns identity, locations, documents and approval execution. These module-owned IDs remain explicit references rather than cross-schema master duplication.

## 13. Screens & Data Views

Reuse the current Purchase Receives list, empty state, source picker, transaction editor and line table. Add server-backed Goods/Services tabs and inspection queue only where appropriate. Show supplier/order, delivered issued revision, site, delivery note/date, received/initial reject, held/qualified, accepted and remaining quantity; exact net price/value appears read-only. Service form adds deliverable, period and evidence. Detail separates physical history, inspection, acceptance history and handoff status.

List filters are name/number, PO, supplier, site, date and workflow state with visible scope. No global counts disclose inaccessible branches. Present simple action labels: Save draft, Record delivery, Inspect, Accept goods, Confirm service. No JSON/pin terminology in routine UI.

## 14. User Experience / Human Behaviour

Keep existing ERP shell, shared inputs/toggles/dropdowns/tables/empty states and branding. Show loading, empty/add, validation, permission denied, concurrency conflict and uncertain-result states. An absent policy explains what needs configuration while preserving drafts. A saved physical receipt is visibly locked with a history link. Do not preselect accepted=received when inspection is required. String money/quantity contracts avoid JS Number loss; UI comparisons and totals require the existing exact arithmetic approach or server values.

## 15. Notifications & Communications

Existing outbox/notification infrastructure routes inspection assignment, receipt/acceptance completion, blocked evidence and failed handoff messages to authorized recipients. Events are facts after commit; notification failure cannot undo business acceptance. No email exposes sensitive supplier/bank data or grants access through an unrestricted URL.

## 16. Documents & Attachments

Reuse shared Core documents and Procurement attachment references with receipt/inspection/service owner types, existing size/type/scanning/access gates and immutable final document version references. EvidenceRef in the pure contract must resolve to an accessible completed document/version or approved structured evidence reference, not a client-created string. Draft attachment unlink remains allowed; final evidence cannot be replaced in place. Document download uses the same source authorization as detail.

## 17. Exceptions & Edge Cases

Multiple receipts, simultaneous certifiers, alias workers, deactivated receiver, current site archived, source revision superseded, duplicate deliverable, rejected-only delivery, source hash mismatch, unrepresentable exact value, stale command version and unknown consumption all require explicit behavior. Legacy rows with Accepted labels but no pins/effects remain historical/unverified, excluded from authoritative matcher output until a separately approved reconciliation process; never silently backfill invented worker/policy evidence.

## 18. Integrations / APIs / Events / Sandbox

Proposed `/api/procurement/v1` contracts:

| Route | Contract |
|---|---|
| GET `/receiving/sources` | Scoped delivered issued-line picker; exact IDs/hash, category/type/kind, physical recorded/held totals, authorised acceptance remaining, exception indicators and decimal strings |
| GET `/receiving/worklist?view=receipts|inspection|services` | State/resource filtered before count/page; current action candidates with explicit blockers |
| POST `/receipts/drafts`, PUT `/receipts/{id}/draft` | Header + line exact IDs, expected versions, site/note/date; no acceptance authority |
| POST `/receipts/{id}/record` | Atomic multi-line physical disposition, command key, expected header/line/source versions, evidence |
| POST `/receipts/{id}/lines/{lineId}/inspect` | Final qualified quantity/checklist evidence under current assigned worker |
| POST `/receipts/{id}/lines/{lineId}/accept` | Positive quantity and evidence; current source/ledger CAS |
| POST `/service-acceptances/drafts`, PUT `/{id}/draft`, POST `/{id}/certify` | Stable certificate ID, exact source, deliverable/period/quantity/evidence |
| GET `/receipts/{id}`, GET `/service-acceptances/{id}` | Current scoped detail/history and permitted actions |
| POST `/receipts/{id}/cancel-draft` | No recorded facts, expected version; no Finance release |
| GET existing order command recovery with receiving operations | Actor-owned historical response, current operation permission/identity/target scope; not a new execution authorization |

Mutation decimal fields use exact strings (legacy numeric acceptance only if explicitly contract-tested); quantities four and net amounts eight fractional digits, CLR representability required. Money formatter must not round or widen legacy DTOs silently. `actionCandidates` never replace command-time validation.

Emit accepted events with immutable event/effect/source IDs, issued revision/hash, original acceptance record ID, quantity/net deltas as strings and source sequence. Inventory/Asset consumers deduplicate effects. An outbox row proves intent, not downstream posting. Supplier portal self-certification is excluded.

## 19. Audit / Security / Privacy

Reuse append-only business audit, no parallel security identity system. Audit draft mutation, record/inspect/certify/accept, export, denied consequential attempts where appropriate and idempotent outcomes. Capture current actor/worker, retained maker workers, source/policy versions, before/after counters, reason, correlation and command/effect IDs. Do not log raw restricted documents or secrets.

Core tenant session shared fence is acquired before beginning the transaction snapshot; Core branch writers use the paired exclusive fence. Command-first and revocation-first orderings have distinct expected outcomes. Locking after a snapshot is insufficient. Runtime role gets only explicit Procurement DML and Core read/execution privileges already reviewed, never HRM-wide writes.

## 20. Reports / Dashboards / Metrics

Report physical recorded, rejected, held, qualified, accepted, returned and administrative reversal separately. Accepted net amount is pre-tax; do not label it Finance actual spend. Age inspection from recorded time, service certification from explicit deliverable period. Historical legacy/unverified records are visible with that qualification but excluded from authoritative accepted totals. Metric sequence/watermark must expose freshness.

## 21. Configuration & Localization

Extend existing Procurement `PolicyVersion` lifecycle with a strict `RECEIVING` schema dispatcher; keep current PR/PO validators unchanged. Published rules select exact sealed category/purchase type/kind, allowed Core location IDs, inspection checklist, canonical assigned worker IDs, effective dates and maker/checker flags. No new policy engine or approval tables.

Current policy route accepts only PURCHASE_REQUEST/PURCHASE_ORDER, so receiving cannot yet be published through it. Published receiving JSON is versioned/hashed and immutable. Recording pins the policy; subsequent inspection/acceptance uses that retained version, checked against explicit withdrawal/expiry behavior (no silent latest-policy substitution). Policy amendment/migration of an in-flight observation is unsupported initially; expiry blocks consequential continuation pending an approved resolution.

Core WorkLocation has LegalEntityId and Type, **no parent-branch relationship**. P25 requires receiving SiteRef to equal the source/selected WorkLocation branch/site ID and to be unarchived in that company; an organisation-wide actor still needs an explicitly allowed location. Cross-location receiving remains blocked until Core owns an explicit location relationship/authority contract. Do not fabricate a Procurement site hierarchy or infer it from address/name.

Default organisation currency is used for draft context; source currency/UOM is immutable for execution. No sample monetary threshold becomes policy. Inventory/Asset routing requires explicit versioned classification beyond Goods/Service; absent routing is Not configured, never a guessed stock or asset transaction.

## 22. Non-Functional Requirements

Atomic persistence of all business effects, counters, audit, command receipt and outbox; bounded PostgreSQL serialization retry using fresh contexts. Exact numeric/JSON/date round-trip. Authorized pagination/search, accessible forms and useful errors. No additional unbounded host jobs. Operational target volumes/performance remain measurable acceptance inputs, not invented SLA numbers.

## 23. Postconditions

A successful record/inspect command leaves one stable final fact. A successful acceptance/certification leaves one append-only positive effect and reconstructed matching projection agreeing with derived counters. Exact replay has no additional sequence/counter/event effect. No result implies AP posted, goods stocked, asset registered, commitment released or supplier paid.

## 24. Failure & Recovery

Sorted source locks and optimistic versions protect capacity. On validation/permission/conflict, rollback all lines, audit/event/receipt side effects as appropriate. On serialization abort, reuse the coordinator's bounded whole-command retry; no stale tracked contexts.

For timeout, retain the original payload and command key; recover the actor-owned historical response. Recovery404 is not proof an in-flight original failed. A different payload with the same key conflicts. Current access is required for recovery and replay. Unknown/stale pins or incomplete ledger return a specific blocker; never reset sequence to zero because a lookup failed.

Accepted projection uses `ControlledReceiving.Balance`: accepted = positive minus administrative reversal, returned = dispatched business return, available = accepted minus returned. Matching consumes by existing receipt-line/service ID and issued revision, with durable current sequence. Do not subtract returns twice or map a return as an administrative void.

## 25. Acceptance Criteria

1. Existing list/editor saves scoped drafts without published receiving rules and without creating acceptance.
2. Delivered source version, sealed PurchaseType and published classification/site policy are proven server-side before recording/certifying.
3. Final physical/inspection/certificate evidence cannot be edited/deleted; existing counters change only with atomic validated effects.
4. Partial goods and service certification cannot cumulatively exceed issued authority; simultaneous attempts serialize and cannot double accept.
5. Required inspection and worker independence are enforced at mapped HTTP endpoints, including alias accounts and current branch revocation.
6. Exact string quantities/rates/net amounts survive HTTP/JSON/PostgreSQL; unrepresentable products reject without mutation.
7. Matcher adapter returns existing acceptance IDs, exact current issued hash/version and consistent accepted/returned accounting.
8. Unknown consumption leaves correction/return execution disabled; no Inventory/Asset/AP success is simulated.
9. Same-key exact retry/recovery reveals only current authorized actor-owned outcomes and creates one effect/outbox entry.
10. P25 migration is additive, preserves legacy draft/data, applies twice safely and leaves no pending model change; runtime least-privilege tests and real API UI tests pass before activation.

## 26. Test Scenarios

Run the reviewed pure receiving/matcher suites first, then new model/adapter/mapped HTTP tests and PostgreSQL rehearsal. Required cases: source/header/line/issued/tenant/company FK mismatch; duplicate observation/inspection/effect key/sequence; final-row mutation/delete; source classification tamper/null; unsupported source schema; site mismatches; no policy and expired policy; required inspection and alias independence; partial accepted capacity races across different receipts; multi-line final failure rolls all back; actual over-delivery and multiple pending inspection receipts remain recorded while concurrent certification cannot exceed accepted authority; one service deliverable across concurrent certificates; exact minimum/maximum representable products and overflow denial; DB microsecond/JSON canonical round trips; old scope replay/recovery; current principal suspension; reader-first/writer-first Core fence ordering; full reversal versus physical return projection; legacy Accepted rows excluded; no consumption adapter cannot produce a negative effect; outbox redelivery idempotency with no claimed owner acknowledgement.

Add Playwright against the real mapped API for empty/add/edit draft → record → inspection → accept, plus service draft → certify. Confirm normal error/loading/uncertain-result states and reload persistence. Local mock-only browser tests do not satisfy this gate.

## 27. Ownership / Dependencies

Procurement owns adapter/entities/migration/rules/effects. Core provides actual location/worker/permission/branch and document/audit services. Shared same-connection transaction/fence is reused. Finance/Inventory/Assets must explicitly own their later handoff and compensation contracts. P25 depends on reviewed P24 issued-version tables/current-source contract and actual HTTP PO gates; it must not generate a migration from a pre-P24 snapshot.

Implementation order and file ownership are explicit in the linked plan. Parent controls SDK/DB/browser slots and migration rehearsal; this documentation branch neither runs those jobs nor edits the frozen PO worktree.

## 28. Open Decisions / Assumptions

- Business must publish category/purchase-type mapping, inspection checklist/worker/site assignments and maker/checker choices. No default policy is silently approved.
- Confirm first site's equivalence to Core WorkLocation; subordinate warehouses/sites need a Core relationship contract before cross-location receipt.
- Confirm policy expiry/withdrawal resolution for already recorded goods; initial behavior blocks unsupported continuation without rewriting the retained policy.
- Physical over-delivery must be retained as held/exception evidence in the existing receipt detail. Business must choose its later disposition/return authorization. P25 does not authorize over-acceptance; an unaccepted physical return has no accepted-entitlement delta and needs its own documented physical-disposition route before execution.
- Inventory/Asset routing classifier, complete matcher consumption store and downstream correction authority remain subsequent explicit integrations. Their absence does not block supported positive acceptance, but blocks unsafe negative changes.
- Root must verify P24 migration numbering/current base and source tests before P25 generation. No author, product owner or operational approval is fabricated by this document.

## Source implementation addendum — P25 positive candidate (not verified)

The isolated implementation retains the 28-section contract above. Its source covers
existing-table draft create/edit/cancel, physical goods recording, one final inspection,
positive goods acceptance, quantity-priced service certification, scoped sources/receipt
and service worklists, exact response/recovery and conservative current-source guards.
No compiler, migration generation/application, PostgreSQL, browser or production job has
run for this candidate. Generated SQL/relational behavior and all proposed fields still
require independent review and centralized gates before P25 can be accepted.

### Exact compatibility design (sections9,12,18,24)

Legacy rows retain executionVersion0, nullable IssuedVersionId/SubmissionId and missing
binding GUIDs (`Guid.Empty`). A missing GUID is never an issued/approved identity.
Version1 requires verified nonempty issued binding; unknown execution versions reject.
Nonnullable `IssuedBindingId`, `SourceStateBindingId` and `PhysicalBindingId` companions
permit composite principal keys without EF converting historic nullable pins to NOT NULL.
New header-line/source/inspection/effect FKs join the same tenant/company/PO/issued/line
or exact physical observation. No authority/effect/worker backfill is proposed. Untouched
legacy data remains visible as unverified; finalization does not adopt it automatically.

Only `receiving_source_states` and `receiving_acceptance_effects` are new tables.
`GoodsPhysicalObservation` and `GoodsInspectionEvidence` remain hydrated pure projections,
not mapped duplicate records. Service original certified values freeze; companion current
values are controlled effect projections. Added/modified state sequence and accepted
receipt/service/PO/PR counters require matching newly tracked facts/effects; legacy values
cannot be overwritten to zero to make a new ledger appear complete.

`ReceivingLedger.Complete` now describes the complete controlled physical/inspection/effect
stream. A separate **required** `AcceptanceHistoryVerified` distinguishes whether all linked
PO/PR counters and legacy fulfilment have been reconciled. There is no default true/zero.
Unknown acceptance history may retain truthful physical/inspection evidence but blocks
certification and `Balance`/matching projection. The adapter inspects linked PO lines,
all controlled state facts and legacy receipt/service rows before accepting this flag.
No current consumption store exists: negative commands and matching consumption writes
remain unavailable, and metadata continues `receivingCorrections=false`.

### Approved structured evidence and policy publication (sections7,16,21)

The v1 `RECEIVING` JSON dispatcher uses the existing owned PolicyVersion draft → review →
independent publication lifecycle. Every exact category/purchase-type mapping, kind, site,
inspection checklist/named inspector, service owner and maker/checker flag is explicit.
There is no wildcard Goods default, published-policy seed or client `authorized=true`.
Review/publication rechecks current canonical Core workers and active company/site/category
references; independent review compares actual worker identities, including aliases.
Existing PR/PO schema behavior is retained.

The supported evidence mode is `ControlledManualReference` with an explicit approved
`evidenceReferencePrefix` in the independently published receiving rule. A consequential
command requires a bounded, nonempty reference beneath that prefix from its current
permitted/assigned attesting worker. This is retained **manual business evidence**, not a
claim that the ERP fetched/scanned/verified an external document. The existing document
service remains owner of uploaded files. Automatic document-only evidence mode is not
implemented and cannot be selected by this schema. Business must explicitly approve this
structured-reference evidence route or finalization remains unconfigured.

A physical observation pins its policy version. Consequential continuation requires that
same version still valid at the action time; supersession/expiry may block it as specified
in section28. No silent policy rewrite occurs. Historic detail/recovery does not authorize
continuation. Classification pins retain the original approved rule and hash separately
from current operational policy selection.

### Current transport details and bounded limitations (sections13,18,25–28)

- Commands accept string quantities through the shared `ExactDecimalJsonCodec` (identical
  source from7bd6555), four fractional digits/18 total precision and exact CLR representation.
  Net values are calculated by `ExactDecimalArithmetic`, never accepted as client authority.
- Current draft/command responses use HTTP200 and exact string fields; record errors map
  validation422, permission403, hidden target404, stale/evidence409 and disabled503.
- Record uses `{version,evidenceReference}`; inspection/acceptance/service certification
  uses `{version,sourceVersion,quantity,evidenceReference}`. Receipt line detail includes
  its current sourceVersion; service detail includes the current sourceVersion or0 when
  state does not yet exist. Every mutation requires an Idempotency-Key UUID.
- Existing commercial command receipts gain bounded receiving operation names, including
  ServiceCancel. Responses retain command schema and exact create/edit action permission.
  Existing order recovery dispatches receiving operations to current actor/target-scoped
  historical recovery. It explicitly returns replayAuthorized=false. A stored response
  never grants a new decision, state change, permission or scope.
- Existing `receiving` capability remains false until a compatible UI adapter is verified.
  Granular receivingDrafts/Goods/Inspection/Services depend on default-off deployment flags,
  committed schema/runtime privileges and supported current published rules; corrections
  are always false. No UI is redesigned/activated by this source increment.
- Worklists currently support receipt/service views with scope/state filtering before SQL
  count/page. A dedicated purpose-only inspector/service assignment queue and compatible
  existing-screen frontend adapter are not yet implemented; the first fixture explicitly
  grants scoped receipt-view to its inspector/certifier. These are declared followups,
  not implied read privileges or completed frontend acceptance.
- Shared Core branch lookup logic is factored into one execution adapter helper consumed
  by PO and Receiving; tenant session fencing remains in the existing coordinator before
  the SERIALIZABLE snapshot. Commands lock upstream PRs, PO and relevant source states;
  all persistence/audit/outbox/receipt effects commit together, with no network effects.
- Authored tests cover strict policy/default denial, legacy nullable/binding compatibility,
  physical identity/overdelivery projection, corrupt counters/pins, sequence/append-only
  model semantics and actual mapped HTTP/PostgreSQL goods/service, exact replay,
  missing-policy, current permission and concurrent acceptance scenarios. They are source,
  not results. Full API boundary/alias/site/multi-line rollback/raw FK and actual browser
  gates remain required; root owns resource scheduling and the single P25 migration.

Provenance: shared ExactDecimalJsonCodec is byte-identical7bd6555; source reuses that
parser rather than another decimal implementation. Synthetic PO stagecode/preseed
validation follows root3b57299. Inherited invoice tax-only fixture correction3b5bda9
was narrowly picked; no production matcher change is included by that correction.


## Independent review corrections after checkpoint 40c4c5a

This remains authored source, **not verified runtime readiness**. The independent source
review identified four consequential gaps and a disposition display defect. The follow-up
adds these controls without broadening activation or creating additional tables:

- The current PO must still reproduce its exact issued commercial snapshot, including
  branch, currency, supplier identity, buyer, terms, source references and every active line.
  Its submitted revision/case pointer and the approved Core case's source/policy identity
  must agree with retained submission/issued evidence. Original PO policy publication and
  effective-at-submission evidence/hash are checked; later expiry does not rewrite history.
  Supplier display/legal/country labels come from the retained snapshot, so a later supplier
  profile rename or suspension is not treated as evidence corruption.
- Acceptance completeness examines **all** linked PO lines, including inactive history,
  all existing receipt/service rows, verified controlled ledgers and PR/PO cached acceptance.
  A missing ledger does not establish zero. Both goods and service version-one consequential
  rows require an exact loaded state binding. A service draft's intended quantity alone
  does not establish certification. Newly persisted certification must have its matching
  typed positive effect, canonical worker and advanced source sequence.
- Service certification and exact replay both enforce the source site's membership in
  the published rule and current actor's site scope, even for organisation-wide users.
  A named owner does not waive a policy's site restriction.
- Final inspection rejection augments the derived rejected quantity; initial rejection
  and original received quantity stay immutable. Terminal acceptance compares certified
  quantity to final qualified quantity. Detail and command responses include each line's
  inspection/held disposition and current source revision, including multi-line receipts.

New source tests exercise header/Core/line corruption, historical inactive acceptance,
missing/malformed service and goods bindings, service site denial, stale rejection caches,
and a certified service lacking its positive fact. The actual HTTP/PostgreSQL probe adds
an inactive historical-line case and a governed site-restricted service-policy case.
These tests have **not been executed** in this worktree. P25 migration generation,
restricted-runtime SQL/HTTP verification, compatible UI integration and browser tests
remain centralized release gates; all production receiving capabilities remain default-off.

## Additive P25 read-contract increment (source only)

This increment adds no tables or permissions and does not enable receiving. Existing broad
receipt detail now includes `lineReadiness` with retained issued supplier/order/line labels,
exact-string capacity, immutable acceptance effect references and current action candidates.
Service detail has the corresponding `capacity`, `actionCandidates`, labels and effects.
Draft owner edit/cancel candidates remain usable when receiving policy is not configured,
provided current issued source and Core edit/scope checks pass. Candidates are hints; the
actual command/replay always revalidates canonical identity, policy, source, scope and effects.

New purpose-only routes use existing granular permissions, without `procurement-po-read`
or `procurement-receipt-view`:

| Route | Required action | Visibility |
|---|---|---|
| `GET /receiving/inspection-tasks?page&pageSize` | `procurement-receipt-inspect` | Current named inspector's current-policy/site/source-classification pending physical lines |
| `GET /receipts/{id}/inspection-task` | Same | Assigned lines only; omits whole-receipt notes/delivery note/document references |
| `GET /receiving/service-tasks?page&pageSize` | `procurement-service-certify` | Current named service owner's pending certifications in explicit policy sites |
| `GET /service-acceptances/{id}/certification-task` | Same | Assigned service record; no broad source PO or other receiving records |

The task queries resolve the current canonical worker/permission/Core branch scope and one
valid published policy, then apply explicit category/purchase-type/site/currency and scope
filters **in SQL before count/page** using immutable submitted source lineage. Page size is
bounded to100. Queue membership is not a promise that maker/checker, evidence or capacity
allows completion; detail returns current guarded candidate/blocker. General receipt reads
continue to require their existing broad receipt-view permission.

Read previews use an explicitly nonpersisting source-state projection and never create or
advance receiving state/effects. Unknown inherited acceptance yields `verified=false` and
null remaining quantities, not zero or positive availability. Missing required inspection
has known order history but null physical/acceptable quantity and a qualification blocker.
Returned/reversed physical fragments do not restore capacity to reuse the original arrival.

`documents` reports only real existing ProcurementAttachmentRef metadata, preserving Core
DocumentServiceId/version identity. It explicitly says content/upload access is unavailable:
the current Core document owner adapter supports supplier invoices, not receiving owners.
No filename-only browser selection or `CoreOwned` reference is represented as a scanned,
uploaded or downloadable file. A later shared Procurement document owner adapter must
add receiving ownership/access before those actions appear. Purpose-only tasks do not
expose whole-record document references that could belong to other unassigned lines.

Authored verification covers real mapped default-off/access gates; pure unknown/qualified/
returned-capacity and current-guard nonmutation; actual PostgreSQL probe task pagination,
unassigned denial, no broad PO/receipt read, and service previews creating no state. These
are source tests, not execution results. Migration/runtime/UI/browser gates remain open.

Historical read clarification: an explicitly retained published receiving policy may be
loaded after its effective interval to describe immutable recorded acceptance/inspection.
Current action hints still run the policy's effective-time guards and become false on
expiry. No retained-policy exception is applied to commands, replay or task assignment;
those retain their existing effective-now defaults. A draft without a retained policy
still needs a single configured current policy for positive-action hints.

### Retained scope before paging (review correction to aee034a)

Current mutable PO branch alone is insufficient to authorize a receiving list row. A shared
SQL read fence now compares current PO scope/identity/revision/number/supplier/buyer with
retained issued commercial evidence, the matching submission and its approved Core case
source/version/policy/maker pointers. The branch key must exist in the sealed document;
null branches use exact null equality. This fence composes into assigned inspection/service
queries, issued source picker, and version-one receipt/service worklists **before count and
page**. General v1 detail rejects a source outside this retained lineage as well. The detailed
Source verifier still checks full canonical intent and hashes; SQL predicates do not replace
that final proof or authorize a write.

The authored restricted-runtime probe preserves a legitimate A positive task, then uses the
bootstrap owner only to corrupt its current PO branch to B. A named current policy/site-B
owner restricted to Core branch B must see zero assigned tasks, sources and v1 worklist rows,
including zero counts; purpose detail is404 and broad detail409. The fixture restores both
branch and membership without granting the runtime PO-tamper/Core-write permission.

Legacy executionVersion0 has no fabricated issued authority. Because its historical branch
cannot be proved from a missing issued pin, read visibility is restricted to a current
organisation-wide receipt-view actor with **no selected branch and no Core branch restriction**.
Selected-branch and branch-confined requests omit these rows before count/page and receive404
for their detail, even when the mutable linked PO now points to that branch. No legacy site
or branch is guessed, backfilled or treated as matching/acceptance evidence. Existing records
remain intact and explicitly unverified for authorised organisation-wide inspection/recovery.
Unknown execution versions are excluded from bounded worklists/detail and cannot execute.


Legacy visibility verification (source only): the actual receiving PostgreSQL probe creates
untouched executionVersion0 goods/service rows against a real A source. Organisation-wide,
no-selection inspection succeeds and retains missing pins/uncertified flags; selecting A
hides rows and detail. After bootstrap-corrupting the current PO to B and restricting the
reader to B, selected and unselected B requests see no legacy rows/counts/details. Cleanup
asserts both legacy records and missing evidence remain unchanged. No verification result
is claimed until the central actual-runtime gate runs this scenario.

Service draft round-trip: normal authorised `GET /service-acceptances/{id}` additionally
returns nullable `notes` from the separate max2000 ServiceAcceptance.Notes field. Existing
Reason retains legacy/cancellation meaning; no legacy Reason is copied. Normal detail also
returns `cancellationReason` for Cancelled records. Purpose-only certification detail omits
both narrative fields. The source probe asserts normal notes
survive certification/cancellation and are absent from purpose detail. This adds one nullable
Notes extension column on the existing service table, no new table or legacy backfill.

Service notes correction: cancellation previously overwrote the same Reason field used by
draft Notes, so a later detail could lose the narrative or mislabel cancellation reason as
notes. P25 now adds separate nullable max2000 Notes on the existing service table. Legacy
Reason is left unchanged with its existing text storage; the cancellation API's1000-character
limit is not represented as a historical database constraint. No legacy field is narrowed
or copied. Authored actual-probe cases use1500-character notes through certification and a
separate cancelled draft, assert independent cancellationReason and purpose-view privacy.
Model tests cover nullable/max2000 metadata, unchanged legacy Reason and final-note immutability.
