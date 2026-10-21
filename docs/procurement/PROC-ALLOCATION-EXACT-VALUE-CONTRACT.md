# Allocation exact values — audit correction and verification addendum

Status: Source inspection and proposed verification only, 20 October 2026. Based on shared-demand source d6eaef0. No application, model, migration or production changes accompany this document. This corrects the earlier claim that allocation storage still needs widening.

## 1. Feature Identification

PROC-ALLOCATION-EXACT: existing PurchaseRequestAllocation value contract, shared with Direct PO and future RFQ reservations.

## 2. Purpose & Business Outcome

Preserve original commercial evidence and exact derived allocation amounts without accidental storage rounding or duplicate consumption.

## 3. Scope & Exclusions

Document the actual effective model and remaining tests. No new widening migration, historical recomputation, Finance ledger, currency rounding policy or enabled RFQ workflow.

## 4. Parent & Related Features

Purchase request line routing; approved-source PO conversion/edit/issue/release; P24 commercial execution; proposed RFQ publication; shared demand reducer.

## 5. Actors & Participants

Authorized scoped buyer and future sourcing publisher; Core authorization and current actor checks remain authoritative. A migration operator cannot establish historical business correctness from numeric precision alone.

## 6. Entry Points & Triggers

Existing PO from-approved-sources and source-edit commands call the allocation owner. RFQ reservation/publication is not implemented by the shared-demand helper alone.

## 7. Preconditions

Current source approval/policy/Finance evidence, retained request submission and outcome, scoped target ownership, full allocation history and source locks must pass existing command guards. The shared reducer receives actual PO/RFQ target records, not caller assertions of ownership.

## 8. Workflow

Check complete demand ledger → compute every fallible exact product → reserve or resize in the same transaction as target/counters/audit/receipt → persist → recover/replay from durable command receipt. Issue transfers Reserved to Ordered; release stops consuming demand but retains the recorded row/value.

## 9. Status / State Transitions

Domain default is Pending with nullable TargetType/TargetId; these defaults are not evidence of an executable allocation. Current complete-ledger reducer accepts Reserved/Released for owned PO or RFQ targets and Ordered only for PO. Unknown legacy/default rows block reconciliation rather than disappear from availability.

## 10. Permissions & Data Scope

Use current command permissions, tenant/company/branch authorization and target access. Exact replay must still pass current authorization. Do not expose another company's target or amount to explain a blocked ledger.

## 11. Business Rules

Quantity/rate inputs use their existing exact four-place contracts. EstimatedValue is an exact quantity × command-time PO unit price product, up to eight places, calculated with ExactDecimalArithmetic before mutating counters. No rounding is implied by currency display. numeric(38,8) storage does not make arbitrary 38-digit values representable by .NET decimal; retain exact-arithmetic overflow denials and actual command bounds.

## 12. Data Fields & Ownership

Procurement owns the existing allocation row: request/line, method, status, target type/id, quantity, EstimatedValue, currency, actor/reason/time, scoped idempotency identity. Source/target pins and immutable evidence are owned by their existing submission/issued/receipt records; there is no additional shadow balance.

**Effective schema correction:** Data/ProcurementDbContext.cs declares EstimatedValue18,4 in its base allocation mapping at line349, but calls ConfigurePurchaseOrderExecution later at line501. Data/ProcurementPurchaseOrderExecutionMapping.cs line33 overrides it to38,8. ProcurementDbContextModelSnapshot also declares numeric(38,8). Migration 20261020114243_P24PurchaseOrderCommercialExecution.Up already alters the existing column from18,4 to38,8 without recomputing historical values. Reading only the earlier declaration produced the false audit finding.

## 13. Screens & Data Views

No new page. Existing source availability is quantity/approved-source evidence, not a claim that every historical EstimatedValue is reconstructed exactly. Any future allocation amount API projection must use an invariant exact decimal string; never route stored eight-place amounts through JavaScript Number.

## 14. User Experience / Human Behaviour

Display a useful reconciliation failure when history is unknown. Never present a legacy amount padded to eight decimal places as proof of original accuracy. Keep display formatting separate from stored/replayed evidence.

## 15. Notifications & Communications

Existing successful commands own notifications. Precision failures must not trigger success notifications or sourcing/PO progress.

## 16. Documents & Attachments

PR submission snapshots retain original submitted line information and digests; PO commercial submission/issued snapshots retain their own commercial evidence. Neither proves every pre-P24 allocation's command-time rate. Current catalog prices or current mutable PO lines are not a historical repair source.

## 17. Exceptions & Edge Cases

Pre-P24 values may have already been rounded by numeric(18,4). Widening preserves those recorded values exactly but cannot restore lost digits. Missing original evidence remains unknown. A zero EstimatedValue can be a legitimate zero-price source or a legacy recorded amount; the value alone does not distinguish them. No automatic repair/backfill is authorized.

## 18. Integrations / APIs / Events / Sandbox

Existing ProcurementOrderSourceCommands and ProcurementOrderExecutionSupport own transaction/receipt orchestration. Receipt lookup binds operation/key/actor/fingerprint/target; changed payload with the same key conflicts. For unconfirmed conversion, recover by the existing command-receipt endpoint, because the target ID may not yet be known. Tests must use a private actual PostgreSQL sandbox and real commands, not mocked allocation persistence.

## 19. Audit / Security / Privacy

Retain old values and original evidence. Any later evidence-based reconciliation would require a separately specified, authorized, audited correction process; never silently rewrite history. Source-only inspection here proves neither deployed migration state nor runtime correctness.

## 20. Reports / Dashboards / Metrics

Do not conflate EstimatedValue with tax-inclusive commitment, payable amount, Finance actuals or remaining allocatable value. Demand capacity remains the existing complete quantity ledger and guarded caches.

## 21. Configuration & Localization

Organisation currency remains authoritative where already configured. Store exact invariant decimals; locale separators and money formatting belong to presentation. No added configurable rounding assumption.

## 22. Non-Functional Requirements

Preserve bounded exact arithmetic, existing SERIALIZABLE transaction boundaries/source-lock order, stable receipts and access checks. No new background reconciliation or additional service.

## 23. Postconditions

A successful reservation has exactly one scoped allocation, matching quantity/counters, exact product and one durable command result. Resize changes that existing allocation under current versions; issue/release retain its recorded amount.

## 24. Failure & Recovery

Existing P24 Down takes ACCESS EXCLUSIVE locks including purchase_request_allocations before inspecting evidence. It blocks used/configured execution and separately rejects estimated_value != trunc(estimated_value,4) or abs(value)>=100000000000000 before narrowing to18,4. This already supplies the requested precision-loss downgrade guard; no duplicate migration is proposed. Legacy preservation and restore/forward correction remain required after use. Lost-response replay must not recalculate historical values from today's rates or add a second reservation.

## 25. Acceptance Criteria

Actual migrated PostgreSQL stores and reloads0.0001×0.0001=0.00000001 without loss; precise mixed products survive independent-context reads, resize, replay and recovery. Current permissions/source versions/ownership and no-overallocation remain enforced. Existing widened schema and downgrade protection are verified rather than assumed from one mapping declaration.

## 26. Test Scenarios

Planned, not run by this documentation change:

- Real approved-source conversion with quantity0.0001/rate0.0001; owner SQL and fresh EF context both read EstimatedValue0.00000001; source counters and receipt agree.
- Nontrivial1.2345×2.3456=2.89564320 with no tax/rounding inference; resize to another four-place quantity and check exact stored product.
- Exact replay, changed-payload/key conflict, lost-response receipt recovery with initially unknown target ID; assert one allocation and unchanged audit/outbox/counters on replay.
- Concurrent reservations exceeding the same source balance: one valid outcome, other conflict/retry-safe denial; no overallocated ledger or extra receipts.
- Force failure after allocation creation but before transaction commit: no allocation/counter/target/audit/outbox residue; successful retry reuses the original command identity.
- Upgrade a protected pre-P24 fixture containing a recorded rounded amount; assert byte-equivalent numeric value and row identity preserved, not repaired. No claim that the retained value equals original unrecorded product.
- Existing P24 Down precision/range guard on eight-place amount, exact maximum18,4 and zero, plus used-evidence stop; run rollback-only or disposable database. Existing P24 rehearsal records already cover some downgrade checks; do not imply these new business-product tests have passed.
- Future operational RFQ tests must use a real publication command and approved-source pins. Shared reducer synthetic RFQ rows alone cannot prove RFQ persistence/replay.

## 27. Ownership / Dependencies

Single current value writer is PurchaseOrderSourceAllocation.Reserve/ResizeReservation. Shared PurchaseDemandAllocationBalance is a pure reducer, not another writer. Future RFQ reservation requires an explicit Procurement command using the same ledger/transaction ownership; no separate sourcing balance or duplicate schema. Parent owns subsequent actual gates and migration sequencing.

## 28. Open Decisions / Assumptions

No schema correction remains for this specific column. The outstanding work is targeted actual exact-product/recovery evidence and future RFQ command implementation. Whether any pre-P24 row has sufficient immutable original evidence for a separately approved correction is unknown and must be assessed per record. This document withdraws the earlier18,4 future-gap assertion in c0877da/shared-demand documentation and leaves existing source changes intact.
