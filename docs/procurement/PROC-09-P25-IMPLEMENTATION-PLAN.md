# P25 — Implement existing Receiving records as live controlled transactions

**Status:** First positive adapter source candidate authored; no build, tests, migration generation/application, browser or production activation verified. See P02 source addendum for implemented scope and explicit gaps.
**Contract:** [PROC-09-P02, 28 sections](PROC-09-P02-RECEIVING-PERSISTENCE-API-ADAPTER.md).
**Candidate inputs:** PO/settings `80ff610` + fulfilment `db1f033`. P24 must first be integrated from the root's verified snapshot. Tracking: [#31](https://github.com/georgemunganga/module-connect/issues/31).

## Current evidence and exact gaps

| Current file/surface | Reuse | Missing adapter behavior |
|---|---|---|
| `backend/procurement-api/src/Mightyfin.Erp.Procurement.Domain/CommercialRecords.cs` | GoodsReceipt, GoodsReceiptLine, ReceiptInspection, ServiceAcceptance, SupplierReturn | Exact issued source, canonical workers, final evidence/sequence, policy pins; separate immutable physical values from current balances |
| `...Application/ReceivingContracts.cs`, `ControlledReceiving.cs`, `...Domain/ReceivingEvidence.cs` | Reviewed pure source, policy/actor/ledger and consumption contracts | Trusted transaction-scoped resolvers and persistence/hydration; projections are not client input |
| `...Application/ControlledReceiving.cs:RecordGoods` | Validates positive physical quantities, initial disposition and one final observation | Retain actual received/rejected evidence beyond PO quantity; add held/over-delivery read projection and warnings. No physical bound should discard truthful intake; certification independently bounds accepted authority |
| `...Infrastructure/Data/ProcurementDbContext.cs` | Existing five receiving DbSets and same-company FKs | Header and line must share same PO/issued version; immutable final guards; net precision; effect/source state mapping |
| `...Infrastructure/Data/ProcurementPurchaseOrderExecutionMapping.cs` | Existing commercial command receipt and append-only guard | Add receiving operation allow-list; no second command receipt table |
| `backend/hrm-api/src/Mightyfin.Erp.Hrm.Api/ProcurementHostIntegration.cs` | Shared company scope, DI, database, capability metadata | Receiving stays false currently; introduce off-by-default options and exact readiness check |
| `...Api/ProcurementOrderExecutionSupport.cs`, `...Infrastructure/Approvals/ProcurementApprovalTransaction.cs` | Current actor scope patterns, source lock ordering, same-Postgres/fence retry | Factor only proven common scope/lock/read helpers; do not copy an approval engine or depend on private PO helpers by UI name |
| `...Api/ProcurementPolicyDraftRoutes.cs:ValidateReferences` | Versioned Draft/Review/Published policy lifecycle | Strict receiving schema/type dispatch and Core reference checks; currently only PR/PO allowed |
| `backend/hrm-api/src/Mightyfin.Erp.Hrm.Application/ProcurementActionCatalog.cs` | Shared permission catalogue | Register receipt view/draft/record/inspect/accept/service actions; no default grants implicitly activating approval |
| `backend/hrm-api/src/Mightyfin.Erp.Hrm.Domain/Entities/Organization.cs:WorkLocation` | Existing company location IDs and archive semantics | No parent-branch hierarchy: first slice uses exact source/selected location, no inferred warehouse relationship |
| `...Application/InvoiceMatchingContracts.cs:InvoiceAcceptedFulfilment` | Existing receipt-line/service matcher identity, accepted/returned separate fields | Authoritative read projection over receiving effects; operational consumption persistence is still a later dependency |
| Frontend `reference-ui/src/features/TransactionEditor.tsx`, `components/data/LineEditor.tsx` | Current Purchase Receives form/table/layout | Replace browser-local save and name matching with exact source IDs and live adapter; current local fields include date, reference, notes, deliveryTo, source and quantity |
| Frontend `reference-ui/src/lib/ProcurementLiveGate.tsx` | Existing capability gating | No live receiving gate yet; keep closed until actual API readiness/role proven |

Ellipsis in backend paths above means the same preceding project root, not a proposed new subsystem. The API lives inside the existing host for reuse; domain/application and EF remain Procurement-owned.

## Slice A — settle record mapping before migrations

1. Extend existing Domain records with the P02 pins/immutable fields. Keep legacy columns/data and draft CRUD. No new `GoodsReceiptV2`, `ServiceCertificate` or supplier/site masters.
2. Add `ReceivingSourceState` (Procurement-owned) containing immutable `ReceivingSourcePin`, canonical classification JSON/hash/reference, explicit PO/submission/issued/PO-line IDs and sequence/version. Counters are derived caches, never source authority.
3. Map `GoodsPhysicalObservation` by hydrating `GoodsReceiptLine` plus frozen header fields. Store its generated identity in the line, separate from the existing matcher line ID. Do not EF-map a duplicate observation table.
4. Hydrate `GoodsInspectionEvidence` from final `ReceiptInspection`; persist all required immutable fields. Store QualifiedQuantity explicitly, not by reinterpreting a possibly edited Notes/Status field.
5. Map new `ReceivingAcceptanceEffect` as append-only. Preserve pure Source pin and canonical correction pin compatibility but enable only positive Kind in P25. Use explicit nullable `GoodsReceiptLineId`/`ServiceAcceptanceId` persistence FK columns with an exactly-one check matching Source.Kind and AcceptanceRecordId; a polymorphic GUID alone is not a database relationship.
6. Service certificate business fields freeze after certification. Additional acceptance effects never rewrite a previous certificate. ServiceAcceptance.AcceptedQuantity/AcceptedAmount remain the immutable original certification values after finalization; add CurrentAcceptedQuantity/CurrentAcceptedNetAmount companions for effect-derived current balance. GoodsReceiptLine.AcceptedQuantity and PurchaseOrderLine.AcceptedQuantity remain controlled current projections. Never overwrite original service evidence to represent a later correction; the matcher derives authority from effects, not either cache.
7. Extend existing commercial command receipt operation allow-list with bounded names <=40 characters: ReceivingDraft, ReceivingRecord, ReceivingInspect, ReceivingAccept, ServiceDraft, ServiceCertify, ReceivingCancel. ResponseJson includes exact target IDs/version/sequence and stable string amounts. Recovery verifies current target scope. Keep existing PO operations unchanged.

### Proposed P25 migration shape

Suggested name: `P25_ControlledReceivingEvidence`. Generate only after root P24 is the current EF base; do not hand-edit the historic snapshot/migrations.

**New tables: two only** — `receiving_source_states`, `receiving_acceptance_effects`.

**Extended tables** — `goods_receipts`, `goods_receipt_lines`, `receipt_inspections`, `service_acceptances`. `supplier_returns` remains existing and unchanged until negative execution has a real adapter. Command-receipt receiving operations require source guard changes, not a new table. Existing policy-version JSON supports receiving without a new policy table.

Required constraints/indexes:

- Same-company composite FKs from source state to exact PO/submission/issued and PO line. Add alternate keys including PO identity where necessary to prove line belongs to that PO. Verify issued.SubmissionId references the same submission and line pin/hash is recomputed, not merely FK-present.
- Unique `(TenantId, LegalEntityId, IssuedVersionId, PurchaseOrderLineId)` source state. Immutable source pin; later commercial amendments cannot overwrite it or silently rebind receipts.
- Header alternate key including tenant/entity/header ID/PO/issued identity; receipt-line composite FK must match header and source state PO/issued identity. A line linked separately to an arbitrary valid PO line is insufficient.
- Scoped unique nonnull physical observation Id and effect key; final inspection unique physical-observation reference; final service deliverable unique `(source state, deliverable reference)` with a defined exact normalization rule (trim once at draft validation, case semantics explicit). No opportunistic name normalization.
- Effects FK to source state and one actual receipt-line/service record, retained policy version and optional original positive effect; verify same source, kind and company. `PhysicalObservationId` and `InspectionEvidenceId` link to existing extended records with suitable scoped alternate keys. P25 only positive effects; future negatives must not be enabled merely by a persisted enum. Physical received quantity may exceed ordered quantity; do not add a database check that confuses physical evidence with accepted authority.
- Scoped unique effect key, per-table source sequence, quantity/sign/precision checks; source-state CAS and complete combined-stream validation enforce cross-kind sequencing. A separate unique index on each table does **not** alone prove cross-table sequence uniqueness: all adapter writers must lock source state, allocate one next sequence, validate the union ledger and commit atomically. Runtime role cannot bypass application service through public endpoints.
- Quantities/physical counters `numeric(18,4)` and net values `numeric(38,8)` with exact CLR representability checked before write/read projection; no full38-digit promise. Rate/source quantity stay consistent with sealed four-decimal PO values.
- Additive nullable pins for legacy rows; explicit execution version/status distinguishes new final evidence. No default source hash/worker/policy or synthetic acceptance effect backfill. Rows lacking proof remain non-authoritative.
- Context guards reject final evidence modification/deletion, source pin edits and effect updates/deletes. Only allowed derived counter/status/version changes via validated application path. Add raw PostgreSQL tests for relational constraints and restricted-role behavior; context guards alone are not claimed to prevent malicious database-owner updates.

## Slice B — trusted resolution and transaction service

Proposed files (Procurement Application unless stated):

- `ReceivingPersistenceProjection.cs`: deterministic hydration/verification between existing rows and pure observation/inspection/effect/ledger types; round-trip identity, source JSON/hash, timestamp and exact decimal checks. No I/O in codec.
- `ReceivingPolicyRules.cs`: schema parser/validator and explicit classification projection over existing PolicyVersion. Extend policy route dispatch without passing receiving JSON through PR/PO approval-rule parser. Publishing uses existing maker/checker controls and validates named Core location/worker/document refs.
- `ReceivingExecutionContracts.cs`: exact-string command/read DTOs and bounded error codes; known unknowns remain blockers. Shared exact JSON converter must support8dp net values, not reuse18,4 authority-limit converter.
- `Infrastructure/Receiving/ReceivingExecutionResolver.cs`: current Core principal/worker/grants/scope, sealed source/submission/lineage/classification/policy/location, complete ledger and document evidence resolution on the shared connection/transaction.
- `Infrastructure/Receiving/ReceivingExecutionService.cs`: commands call the pure helpers and persist accepted facts/projections/audit/outbox/command receipt. Reuse shared transaction coordinator, canonical timestamp and exact arithmetic. No external owner HTTP calls inside the database transaction.
- `Infrastructure/Data/ProcurementReceivingMapping.cs`: partial context mapping and immutable/Added guards; no unrelated context rewrite.

**Locking and completeness:** acquire the existing Core tenant session shared fence before transaction BEGIN. Reuse consistent order: sorted upstream PR locks where existing PO operations require them → PO row → sorted receiving source-state rows → receipt/certificate rows. Multi-line record locks all source states in deterministic order before any allocation. Initial source-state insertion races use the unique key and whole-transaction retry, not a partially committed empty state. Recompute source/current authority after locking.

Within each source state load all final observations, inspections and effects in one consistent transaction; check contiguous combined sequence, chronology and every pinned reference against persisted rows. Complete=true is a service conclusion from authoritative reads and verified state, never a DTO default. A zero-length ledger is valid only if locked state.Sequence=0 and all authoritative fact sets are verified empty. Incomplete/error queries do not produce empty arrays with Complete=true.

Future matching consumption writer must acquire the same source-state/acceptance locks before consuming/releasing capacity, with shared lock ordering. Until that actual adapter and owner compensation resolver exist, negative receiving commands cannot claim consumption Completeness. Positive acceptance does not require fabricated Finance consumption zero. Do not hold Finance balances or re-run PO commitment confirmation at receipt.

**Derived counters:** calculate all exact next values before mutating tracked objects. Update receipt accepted balance, current service projection and PO accepted quantity in the same transaction; PR received/fulfilment projections only if their existing semantics are explicitly mapped, never update Ordered/Reserved allocation quantities as a substitute. Rehydrate and compare preexisting counters with facts; disagreement blocks correction and reports reconciliation.

## Slice C — actual host routes/read contracts

Create `backend/hrm-api/src/Mightyfin.Erp.Hrm.Api/ProcurementReceivingRoutes.cs` with focused draft/command/read partials as needed. Extend existing `ProcurementHostIntegration`, permission catalogue and schema readiness pattern. Keep host composition thin; domain rules stay Procurement-owned.

- Follow P02 proposed routes and current tenant/company/selected branch semantics. Drafts require correct source identity but do not require approved classification/policy; invalid/inactive source context is shown clearly, not silently replaced.
- GET sources/details resolve only supported delivered issued versions. Purpose-specific inspection/service queue readers get only needed source context and document refs. Current visibility/state filtering precedes count/page.
- Finalization options remain explicitly off by default; advertise granular metadata `receivingDrafts`, `receivingGoods`, `receivingInspection`, `receivingServices`, `receivingCorrections` (last false) plus supported source/policy schema versions. Existing `receiving=false` remains until a compatible frontend understands the exact contract; no metadata true merely because tables exist.
- Schema readiness verifies migration/version, exact tables/columns/indexes and configured policy—not table count alone. Separate actor action candidates from system-level capabilities.
- Read actions show string quantity/rate/net values and immutable source revision/hash companions. Changes to legacy draft numeric DTOs require compatibility tests; additive endpoints are preferred.
- Every idempotent command checks current actor, command permission and source/target scope before returning saved response. Exact replay has current command-specific guard semantics; historical recovery explicitly does not authorize a new command. Wrong-body same key409, foreign/inaccessible target404/403 as existing convention.
- Map validation422, stale state/capacity409, current permission403, hidden record404, disabled/not-ready503 and unknown/incomplete evidence409 with stable actionable codes. Do not convert an arithmetic error into a success with rounded totals.
- Preserve controlled draft delete/cancel and returned UI values after error. Never allow a legacy generic PUT/delete to mutate a now-final receipt through a second endpoint.

## Slice D — existing UI adapter, no redesign

In the frontend repository, build the live receiving adapter alongside the established live requests/orders API adapters. Reuse current Purchase Receives list/editor/detail, source chooser, table and document components; no second navigation shell or duplicate local live database.

Replace TransactionEditor `item.name` capacity matching with exact PO-line/issued IDs. Preserve date/note/location/header fields; current `deliveryTo` display string resolves via Core location lookup ID. Receiving source lines show ordered, already physically recorded, rejected, held, qualified, accepted and remaining authorised acceptance quantities. Over-delivery and multiple pending inspections are visible exceptions, not rejected saves or extra stock. Keep unaccepted physical return/disposition distinct from accepted entitlement reversal. Typed input strings retain precision. Add inspection/service fields/actions to those existing layouts with contextual panels and clear permission/policy blockers. Local Settings partial-receipt switches do not become policy evidence.

Enable each live action only from compatible capability metadata and mapped allowed actions, while server remains authoritative. Refresh real detail after command; uncertain results retain key/payload and use scoped recovery. No Received demo status or browser-local transaction is presented as live ERP data.

## Slice E — verification gates (root schedules scarce host jobs)

1. **Source review:** existing record reuse, approved policy/classification parser, exact source hash bindings and no current Item/name fallback. Review P24/P25 boundary and unchanged PR/PO behaviors.
2. **Pure/model tests:** run reviewed receiving/matcher tests; add hydration roundtrips and final-row denial/counter guards, header-line composite relationship metadata, precision/scale and unsupported classification/policy cases.
3. **Mapped controller tests:** genuine DI/routes/auth/scope; successful goods draft→record→inspect→accept and service draft→certify; all permission, alias, site, current source and replay denials. Include draft-save-without-policy positive and finalize-without-policy denial.
4. **Disposable PostgreSQL rehearsal:** P0–P25 SQL applies twice, no pending model changes, exact decimal/timestamp/JSON round-trip; wrong-source/version/company FK denials; append-only context checks; raw allowed/denied runtime privileges. Use synthetic data, no production mutation.
5. **Actual concurrency:** two receipts competing remaining capacity, duplicate command key/deliverable, multi-line rollback, current source version race, current branch writer fence reader-first and writer-first cases, timeout/recovery and one audit/outbox/effect per command.
6. **Matcher projection proof:** accepted identity=existing line/certificate; positives/admin reversals/returns distinguishable; projection totals/Version agree with effects. No live negative adapter unless complete consumption and downstream owner proof have their own runtime tests.
7. **Real API Playwright:** goods and service paths with reload persistence, empty/create/edit/error/inspection action gating and hidden foreign scope. UI mock coverage is supplementary.
8. **Release:** independent review, explicit business receiving policy approval, root-scheduled UAT/release/deploy/hash smoke/rollback evidence. Issue31 stays OPEN until this evidence exists; no generic claim that Procurement or Receiving is complete.

## Subsequent bounded milestones

- Complete authoritative invoice-consumption persistence under the shared acceptance lock, then controlled unconsumed correction/return dispatch with current canonical approval, retained original fragment and owner evidence. Reuse SupplierReturn and Core approvals; no new return engine.
- Real Inventory/Asset consumers with versioned classification routing and authenticated idempotent acknowledgements. Procurement records commercial handoff status only.
- Reinspection, authorized over-acceptance, physical exception disposition/unaccepted goods returns, replacements, cross-location receipt and advanced milestone schedules require explicit child-feature extensions, not guessed settings defaults. Basic held/over-delivery evidence is already part of P25 intake; later authorization is not inferred from recording it.

**Evidence for this plan:** repository source inspected only. No builds/tests/migrations/DB/browser jobs were executed by this documentation task.

## Physical evidence clarification (follow-up review)

`ControlledReceiving.RecordGoods` allows multiple observations, including multiple outstanding inspections whose combined received quantity exceeds the issued order. This is not itself invalid: it represents what arrived. Required controls belong at certification: one final physical observation per receipt line, one complete inspection when required, positive certifications no greater than that observation's qualified quantity minus **gross prior positive certifications**, and cumulative accepted entitlement no greater than sealed issued authority. Returns/reversals never re-enable reuse of the same physical observation.

Example: two deliveries of 80 against an order of 100 may both be recorded/held. After inspection, certifying 80 from the first leaves only 20 authorised acceptance for the second; its remaining 60 stays held/rejected/exception evidence and is not stock or invoice eligibility. Received 120 / rejected 20 against an order of 100 records both actual values, then at most 100 can be certified. Received 120 / rejected 0 must not be rewritten to 100 merely to pass validation. Display physical totals and held/disposition independently from accepted capacity.

Add mapped HTTP/concurrency tests for those cases and confirm no acceptance/counter/outbox entitlement is created by physical intake alone. If source classification or policy is unknown, preserve draft intake and original documents, clearly marked not authoritative acceptance; never silently relabel its purchase type to finalise. An unaccepted physical return has zero acceptance effect and must not be forged as a negative effect referencing an unrelated accepted fragment.


## Mandatory review regression gate (source follow-up to 40c4c5a)

Before P25 activation verify exact current issued header/line/Core-pointer agreement,
retained original policy proof, historical inactive PO acceptance and symmetric malformed
v1 goods/service bindings. Run the current-site service-policy denial/replay case and the
final inspection rejection/qualified-completion case. Physical truth must remain available
when inherited acceptance is unverified, while certification remains blocked. No successful
result or migration execution is implied by the authored fixes/tests.

### Read-contract gate

The additive purpose-only task/detail and current guarded read hints require centralized
verification before UI activation. Run the actual receiving probe with scoped receipt/PO
read temporarily removed from assigned inspector/service-owner fixture roles; verify queues,
page/count, unassigned404 and general read403, then actual guarded commands. Confirm a
service draft preview does not insert a source-state row, and legacy unknown capacity is
nullable/unavailable. Core receiving document uploads/downloads remain explicitly unsupported;
only persisted references are displayed. No P25 table is added by this read increment.

### Unknown legacy historical-scope gate

ExecutionVersion0 visibility is organisation-wide/no-selected-branch only until a separately
reviewed authoritative legacy reconciliation can prove historical scope. Selected or Core
branch-confined readers receive no legacy list rows/counts or detail; a current PO A→B change
does not transfer unproved historical visibility. Verify goods and service list/detail paths,
organisation-wide inspection and unchanged missing pins/records in the actual runtime probe.
No migration, backfill, certification, data deletion or new read permission is introduced.

### Service narrative preservation

Add nullable Notes(max2000) to existing ServiceAcceptance independently of legacy/cancellation
Reason. Draft save/edit writes Notes; cancellation writes Reason and retains Notes. Normal detail
returns notes and cancellationReason separately; purpose-only detail omits both. Preserve legacy
Reason text storage with no backfill/length narrowing. Include nonnull service Notes in protected
Down detection. Rehearse1500-character notes through actual SQL certification and separate draft
cancellation, plus purpose privacy; source-authored tests are not runtime evidence.
