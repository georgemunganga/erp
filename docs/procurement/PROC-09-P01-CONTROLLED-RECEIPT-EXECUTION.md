# PROC-09-P01 — Controlled Receipt and Acceptance Evidence

**Status:** Draft specification and read-only audit; no runtime implementation or migration.  
**Hierarchy:** Procurement → Receiving and Inspection → Receipt and Acceptance → Record / Inspect / Certify / Correct.  
**Base:** Initial PO source checkpoint `9a2c074`; depends on its reviewed issued-version and authorization concurrency contracts.  
**Related:** [PROC-09](PROC-09-RECEIVING-INSPECTION-AND-RETURNS.md), [initial PO contract](PROC-08-P01-COMMERCIAL-EXECUTION-CONTRACT.md), [engineering standard](../engineering/GW-ENG-STD-001.md).  
**Tracking:** [module-connect#31](https://github.com/georgemunganga/module-connect/issues/31); depends on #5 and #30. Receiving implementation remains a separate next milestone.

### Audited source evidence (read-only)

| Surface | Evidence / current gap |
|---|---|
| UI `reference-ui/src/app/App.tsx:104`, `lib/ProcurementLiveGate.tsx:34` | Purchase Receives has generic route scaffolding but no live receipt adapter/gate allow-list. Live mode correctly withholds unsupported receiving. |
| UI `features/TransactionEditor.tsx:276`, `:304`, `:322`; `components/data/LineEditor.tsx:56` | Existing receive layout can be reused. Save uses local records; source rows compare by item name and only one order's quantity, not exact IDs/cumulative accepted balance. No real inspection/service/reversal effects. |
| UI `features/Settings.tsx:949` | Partial-receipt / PO-reference toggles are UI scaffolding, not authoritative receiving policy. |
| [CommercialRecords.cs](../../backend/procurement-api/src/Mightyfin.Erp.Procurement.Domain/CommercialRecords.cs) | Existing GoodsReceipt/Line, ReceiptInspection, ServiceAcceptance and SupplierReturn fields cover basic draft data, but lack exact issued pins, immutable effect/reversal provenance, actor workers and complete integration acknowledgements. |
| [ProcurementDbContext.cs](../../backend/procurement-api/src/Mightyfin.Erp.Procurement.Infrastructure/Data/ProcurementDbContext.cs) | Receipt tables/DbSets/simple same-company FKs exist. Generic lifecycle money precision is 18,4; receipt evidence is not append-only; independent header and PO-line FKs do not prove both belong to the same PO. |
| [ProcurementActionCatalog.cs](../../backend/hrm-api/src/Mightyfin.Erp.Hrm.Application/ProcurementActionCatalog.cs) / [ProcurementHostIntegration.cs](../../backend/hrm-api/src/Mightyfin.Erp.Hrm.Api/ProcurementHostIntegration.cs) | No receipt/inspection/service/return action family or route composition; `receiving` capability remains false. No domain receipt execution helper found. |
| Matching branch `InvoiceMatchingContracts.cs:45`, `InvoiceMatchingFoundation.cs:111` | Precise issued-order/accepted-fulfillment projection exists as pure foundation, not a live source adapter. Consumed balance and reversal/return semantics must agree before activation. |

## 1. Feature Identification

`PROC-09-P01` produces authoritative fulfillment evidence. Operations: create/edit receipt draft; record physical goods; inspect; accept/reject a quantity; certify a service; request/authorize/dispatch an unconsumed return; append a correction; expose matching and downstream handoff projections. No implementation is authorized by a button's presence alone.

## 2. Purpose & Business Outcome

Record what actually arrived or was satisfactorily performed against a known supplier-facing PO version. Distinguish physical delivery, inspection, accepted entitlement and subsequent corrections so invoices can use a complete, current, auditable accepted balance.

## 3. Scope & Exclusions

The next bounded execution contract covers partial goods receipts with required inspection, explicit service certification and controlled unconsumed corrections/returns. Drafts remain usable without inventing approval/tolerance policy; consequential transitions require configured policy and current authority.

It excludes warehouse balances/valuation, asset registration/depreciation, payment/AP posting, automatic financial release on receipt/return, supplier refunds, replacements treated as new authority, FX/UOM conversion and retrospective correction of already consumed/posting-bound acceptance. Unknown consumption or owner compensation blocks the consequential correction and creates a visible exception. Separate receipt capabilities may be enabled incrementally after their gates; none is currently enabled by this document.

## 4. Parent & Related Features

Consumes delivered immutable `PurchaseOrderIssuedVersion` and its Procurement submission/source references. Uses existing `GoodsReceipt`, `GoodsReceiptLine`, `ReceiptInspection`, `ServiceAcceptance`, `SupplierReturn`, Core identity/organization/documents/audit and the reliable transaction/outbox patterns. Supplies the existing invoice matching foundation's `InvoiceAcceptedFulfilment` contract. Inventory and Assets remain independent owners.

## 5. Actors & Participants

Receiver, assigned inspector, service owner, return requester/approver, buyer, AP reviewer, authorized auditor and future authenticated integration principal. Roles never imply record scope or spending authority. Source-maker/requester/buyer/assignment worker identities are retained for configured segregation; currently acting identities and authority must still be valid. Supplier users cannot internally certify acceptance.

## 6. Entry Points & Triggers

Reuse `/procurement/workspace/purchase-receives` and its existing list/editor layout. Enter from a scoped issued PO or expected-delivery list; select exact source line IDs. Inspection queues follow recorded held goods; service tasks follow a delivered milestone/period. Correction/return starts from a specific original accepted record, not a typed supplier name.

## 7. Preconditions

The source resolver must prove tenant/company, selected branch, current data scope, known issued version, matching immutable snapshot/submission, successful delivery evidence and active line authority. `PO.Status == Issued` or a document number alone is insufficient. A draft pins the source issued-version ID/hash; an amendment does not silently rebind it.

The receiving location must be an active Core location in the correct company and actor scope. Inspection/evidence/segregation rules must resolve to an approved version. A suspended supplier's existing delivered obligation follows receiving exception policy; do not reuse the new-order supplier activation check to erase an actual delivery. No new Finance reservation occurs at receiving.

## 8. Workflow

1. Load authorized expected PO lines with ordered quantity, physically recorded quantities, accepted net balance and current version.
2. Save a draft with stable IDs, delivery note/date/site and goods quantities, or a service deliverable/period. Draft validation errors preserve data.
3. Record the physical goods once. The immutable observation distinguishes received, accepted, rejected and still-held quantities; recording does not certify acceptance or stock.
4. For inspection-required goods, an assigned inspector records an immutable result/checklist/evidence; a pending, failed or unknown inspection cannot auto-accept. Conditional acceptance requires an explicit policy/decision and reason.
5. Accept only the permitted quantity from recorded held goods. Append acceptance evidence and atomically update derived PO/source PR accepted counters, audit, command receipt and outbox. Service certification uses a separate guarded transition with explicit deliverable/period, quantity and accepted net value.
6. Return requests/approvals have no negative quantity effect. A verified physical return dispatch appends a negative accepted-balance effect; an administrative reversal links the exact erroneous original acceptance. Neither operation overwrites the original.
7. Before negative effects, prove complete matching consumption and downstream status. Consumed, posted, unavailable or ambiguous evidence routes to AP/owner resolution; do not guess a zero consumed balance.
8. Publish accepted/corrected facts. Configured Inventory/Asset consumers acknowledge their own processing; absent integrations remain Not configured, not Complete.

## 9. Status / State Transitions

| Record | Transition | Actor / guards | Atomic effect / failure path |
|---|---|---|---|
| Receipt draft | Draft → Recorded | Authorized receiver; exact source/site/line IDs; expected revision; required evidence | Persist physical observation; audit/event/receipt; validation leaves draft |
| Goods line | Recorded/Held → Under Inspection | Assigned inspector; inspection policy | Inspection work/result evidence; no accepted balance yet |
| Goods line | Held/Inspected → Partially Accepted/Accepted | Authorized certifier; successful required inspection; remaining source capacity | Append positive acceptance effect and counters; stale/concurrent capacity conflicts |
| Goods line | Held/Inspected → Rejected | Authorized disposition; reason/evidence | Append rejection; never becomes matching/inventory entitlement |
| Service certificate | Draft/Pending Certification → Accepted/Partially Accepted | Assigned service owner; period/deliverable; authorized quantity/value | Immutable certificate/positive effect; no inferred percentage-only acceptance |
| Return | Requested → Approved → Dispatched | Scoped requester then independent configured approver; current Core decision if approval required; dispatch proof | Only Dispatched reduces accepted entitlement; failure retains pending request |
| Correction | Requested → Reversed/Partially Reversed | Specific original evidence; independent authority; complete unconsumed/downstream proof | Append compensating effect; original unchanged; impossible compensation stays blocked |
| Handoff | Not configured / Pending → Acknowledged / Failed | Real configured owner integration and authenticated response | Owner reference/correlation; no manual fake acknowledgement |

Return approvals reuse Core when policy requires execution tasks; no new approval engine. Missing return authority/policy keeps the request pending. Operational progress is separate from original commercial PO approval and invoicing progress.

## 10. Permissions & Data Scope

Proposed names extend the existing Procurement action catalog; these are not current grants:

| Permission family | Participant | Data scope / conditions |
|---|---|---|
| `procurement-receipt-read/create/edit/record` | Receiver | Assigned company/site/PO; edit only own authorized drafts |
| `procurement-receipt-inspect/accept` | Inspector / receiver where policy permits | Assigned category/site; required inspection; maker/checker restrictions |
| `procurement-service-read/create/certify` | Service owner | Assigned PO deliverable/project/site; evidence and independent checks |
| `procurement-receipt-reverse` / `procurement-service-reverse` | Authorized correction actor | Exact original acceptance and permitted unconsumed amount |
| `procurement-return-read/request/approve/dispatch` | Return participants | Exact receipt/site; no self-approval; configured authority and source limits |
| `procurement-receipt-handoff-read/reconcile` | Operator / integration principal | Assigned owner integration/event only; no stock/asset write rights implied |

Requester receipt requires explicit organization policy; buyer/AP privileges do not automatically grant certification. Current Core branch/role membership and selected context must be checked within the agreed authorization linearization contract, including exact replay. The shared SERIALIZABLE snapshot/fence question remains a dependency, not solved by a second local identity service.

## 11. Business Rules

- Bind by stable PO/issued-version/line IDs, never item descriptions, names or list positions.
- Per physical line, cumulative accepted + rejected + held disposition cannot exceed recorded received quantity. Excess physical goods may be recorded as held/quarantined; their acceptance is blocked unless an explicit approved tolerance/exception exists. No silent tolerance.
- Across all receipts/services for a PO line, net accepted quantity cannot exceed authorized ordered quantity plus a separately authorized allowance. Pending return/approval does not free capacity. Replacement delivery does not create extra order authority.
- Service certification must satisfy quantity and value ceilings independently; overlapping period/milestone/duplicate certification is checked using stable references and policy, not date text alone.
- Goods and services cannot both consume the same source capacity as unrelated streams. Their classification derives from pinned source/category/item evidence.
- `PurchaseOrderLine.AcceptedQuantity` and linked PR accepted counters are projections; append-only accepted/correction effects are authoritative. Ordered commitments do not decrease automatically on a return.
- A service or goods negative effect cannot reduce below already validated matching consumption without the explicit AP/credit correction workflow. Unknown consumption is not zero.
- No receipt, return or correction creates Finance postings, releases a budget, or rewrites supplier bank information.

## 12. Data Fields & Ownership

### Existing schema and exact additive proposal

| Record | Existing useful fields | Proposed additions / ownership clarification |
|---|---|---|
| `GoodsReceipt` | PO, number, site, delivery note, receiver, received date; header handoff refs | Scoped issued-version ID/revision/hash, branch, normalized external note identity, recorded actor WorkerId/time, pinned receiving-policy reference. Header handoff refs alone cannot represent multi-line owner processing. Procurement owns receipt. |
| `GoodsReceiptLine` | Receipt/PO line, received/accepted/rejected quantities, batch/serial, status/reason | Explicit unit/currency, source category/item/classification projection, held disposition, source evidence references. Accepted/rejected columns are derived caches, not mutable authority. Stable ID is the goods acceptance projection key. |
| `ReceiptInspection` | Receipt line, inspected/defect quantities, inspector/checklist/notes/time | Assigned/acting WorkerIds, policy/checklist version/hash, result/evidence refs and optional prior inspection link. Final results append-only; reinspection adds evidence. |
| `ServiceAcceptance` | PO line, period, accepted quantity/amount, owner, deliverable/reason/certification time | Issued-version/revision/hash, branch/unit/currency, policy, certifier WorkerId, explicit milestone/reference and certified net-value basis. Final certificate immutable; corrections are linked effects. Stable ID is service projection key. |
| `SupplierReturn` | Receipt line, quantity/reason, supplier return ref, inventory adjustment ref, dispatch time | Original acceptance/effect references, pinned issued version, Core case/decision refs if required, requester/approver/dispatcher WorkerIds, authoritative dispatch/evidence and outcome links. Procurement owns commercial return; Finance owns credits/refunds. |
| New acceptance/disposition effect | No authoritative ledger currently exists | Typed goods-line XOR service-certificate FK, PO/line/issued version, effect kind, signed quantity/net value, original effect for correction, exact unit/currency, actor WorkerId/time, policy/evidence refs and stable effect key. Append-only Procurement facts; not a general JSON workflow table. |
| Command recovery | Existing PR/PO receipt patterns | Reuse canonical digest/exact decimal/transaction patterns; receiving-owned actor-bound operation/target/key/fingerprint/response receipt. No second workflow/idempotency engine. |
| Integration acknowledgement | Only loose header refs exist | Per accepted/correcting event + target owner + external acknowledgement/ref/status/correlation. No local Inventory balance or Asset register. Owner integration can remain disabled. |

Use tenant/company composite FKs. Bind receipt line to the same PO as its header, and service/return/effects to the exact source line/version/scope; separate independent FKs to two unrelated same-company objects are insufficient. Unique scoped receipt/effect/command keys and correction-parent constraints prevent duplicate effects. Preserve legacy draft rows; do not mark old records accepted or infer issued-version pins in a backfill.

Quantities/UOM precision follows validated four-decimal source quantities; quantity × unit-price and service net values must preserve eight-decimal calculation where required (`numeric(38,8)` or reviewed equivalent), with explicit currency rounding at display/accounting boundaries. Current generic lifecycle precision `(18,4)` is not sufficient evidence for eight-decimal service net calculations. Migration naming/number waits for P24 and domain review.

## 13. Screens & Data Views

Reuse the current Purchase Receives list/editor/table controls and shared ERP forms, dialogs, filters, empty states and documents. Add a live API adapter and stable source-line picker; replace local persistence for enabled routes. Show ordered, recorded, accepted, rejected, held, returned/reversed and remaining separately. Reuse the detail page for inspection/service/return panels rather than copying a new module shell. The current generic editor lacks those guarded panels; do not claim its local save implements them.

## 14. User Experience / Human Behaviour

Use plain labels: Record Delivery, Waiting for Inspection, Accept Delivery, Confirm Service, Send Back to Vendor, Correct Receipt. Explain blocked quantities and the exact source version. Loading, empty, validation, stale-data, permission-denied, unknown-save/retry, success and downstream-not-configured states are required. Never show Accepted/Stock updated/Asset created merely because a request was saved or queued.

## 15. Notifications & Communications

Core notification service consumes committed events for inspection/certification tasks, rejected/held goods, overdue acceptance and correction/return exceptions. Notify AP of reduced eligibility and operators of actual configured handoff failures. Notification failure does not roll back accepted business evidence; retries are deduplicated.

## 16. Documents & Attachments

Reuse Core document service and Procurement attachment references. Register receipt/inspection/service/return resource authorization adapters beside the existing invoice adapter. Enforce company/site access, document readiness/scanning/versioning/retention; do not copy upload infrastructure or expose files through supplier IDs alone. Attachment IDs do not authorize certification by themselves.

## 17. Exceptions & Edge Cases

Mixed goods/services, multiple partial receipts, duplicate delivery notes, wrong item/site/unit, serial duplicates, damaged/rejected/over-delivered goods, unavailable inspection, supplier suspension after issue, concurrent receivers/inspectors, stale issued versions, renamed catalog labels, unknown save outcome, partial return, return after invoice consumption, stock/asset processing before reversal and historical offboarding all have explicit blocked or compensating paths. Document unavailable required evidence preserves a draft/held state.

## 18. Integrations / APIs / Events / Sandbox

Proposed API surface: scoped expected lines and receipt list/detail; receipt draft create/edit/record; line inspections and acceptance/disposition; service draft/certification; correction/return request/decision/dispatch; history and handoff status. Every consequential command uses expected source/record versions and a UUID idempotency key; request bodies cannot self-declare source approval, current grant, Finance truth or owner acknowledgement.

Use one PostgreSQL transaction for effects/counters/audit/outbox/receipt; enlist Core context on that connection when needed. Consistent lock order must be shared with PO changes and future matching: sorted PR source roots → PO → logical receipt/acceptance sources → invoice/consumption roots where required → Core execution case. Do not introduce a competing invoice-first lock order. Re-read complete capacity and consumption under the same agreed protocol; unknown/network integration work occurs outside locks.

Publish versioned committed `goods_receipt.recorded`, `goods_acceptance.certified`, `service_acceptance.certified`, `receipt_acceptance.corrected`, `supplier_return.dispatched` facts with tenant/company/branch/site, immutable PO revision, stable line/event/parent IDs, exact quantity/value/UOM/currency, sequence/watermark and correlation. Consumers deduplicate and reconcile out-of-order positive/compensating facts by source sequence; no exactly-once network claim.

### Authoritative matcher projection

The reviewed matching foundation already expects `InvoiceAcceptedFulfilment`; implement a server-owned adapter, not a UI object factory:

| Matching field | Authoritative receiving source |
|---|---|
| `Id` | Stable `GoodsReceiptLine.Id` or `ServiceAcceptance.Id`, matching existing invoice-line FK fields |
| `Version` | Monotonic logical acceptance/projection revision, not arbitrary UI update time |
| `TenantId`, `LegalEntityId`, `BranchRef`, `PurchaseOrderId`, `RevisionId`, `PurchaseOrderLineId` | Verified source/issued-version evidence and scoped relationships |
| `CurrencyCode`, `UnitCode`, `Kind` | Pinned source/certificate basis; no name or currency inference |
| `State`, `AcceptanceReference`, `AcceptedAt` | Certified posted facts only; pending/held/rejected is ineligible |
| `AcceptedQuantity` / `AcceptedNetAmount` | Certified positive total less administrative reversal effects |
| `ReturnedQuantity` / `ReturnedNetAmount` | Verified dispatched business returns, excluding administrative reversals already deducted above |
| `Reversed` | Full administrative reversal/void only; a partial correction must not discard the remaining valid balance |

Thus matching eligibility is `certified − administrative reversals − dispatched returns`, without subtracting one correction twice. Service value and quantity are both bounded. The adapter must return a complete watermark/consumption boundary and reconcile persisted matching allocations atomically; original acceptance facts remain unchanged. Pure matcher comparison is not AP validation or payment authority.

Inventory owns stock movement/bins/valuation; Assets owns register/tags/custody/depreciation. Publish facts only when item/site mapping is known; unknown stock/asset mapping is visible Needs mapping, not fabricated identifiers. Disabled owner integrations remain Not configured. A real configured consumer owns its acknowledgement and compensating action.

## 19. Audit / Security / Privacy

Append consequential facts with actor/service identity, canonical WorkerId where human, time, source/record/policy versions, old/new derived balance, reason, document refs, effect key and correlation. Final acceptance/inspection/correction evidence is immutable to normal roles. Audit export/search respects scope. Physical deletion of posted receipt/acceptance/return evidence is prohibited.

## 20. Reports / Dashboards / Metrics

Expected deliveries; recorded-but-unaccepted; inspection/service backlog; gross certified versus net eligible acceptance; rejected/returned/reversed quantities; accepted-but-unmatched; unknown consumption; configured handoff failure/age and unmapped stock/asset records. Definitions distinguish operational quantities, committed value and Finance actuals.

## 21. Configuration & Localization

Approved receiving/service/return policies cover site/category inspection, authorized certifiers, requester/receiver segregation, over-delivery acceptance, required documents, return authority, timing and SLA. No sample thresholds, silent tolerance, guessed owner mapping or automatic no-approval path. Use Core company/currency/site/UOM/localization and the existing versioned policy/role services. Missing optional Inventory/Asset integration does not invent a ledger; missing mandatory acceptance rules blocks certification.

## 22. Non-Functional Requirements

Atomic source/effect consistency, cumulative concurrency safety, exact decimal arithmetic, bounded confirmed-abort retry and actor-bound exact replay. A changed payload under the same key conflicts. Current permission/branch checks follow the shared authorization concurrency contract; do not weaken SERIALIZABLE or grant broad HRM updates. Restore/reconcile procedures must preserve event ordering and original evidence. Accessible/mobile receiving and document outage states reuse ERP standards.

## 23. Postconditions

A recorded receipt proves physical observation only. A certified acceptance has exact immutable PO/source/policy/actor evidence and reconciled net counters. A negative effect cannot exceed its original accepted/unconsumed entitlement. Matching sees the agreed authoritative projection. No Inventory/Asset/Finance success is claimed without that owner's confirmed record.

## 24. Failure & Recovery

Failed saves leave no partial accepted counters/effect/outbox. Unknown replies recover through the exact key; no resubmitted new acceptance to guess whether the first committed. Concurrent capacity changes return a refreshable conflict. Original observations/certificates are corrected by linked compensating facts. Missing consumed-balance or owner compensation evidence creates an exception; it does not silently free capacity or reverse a posting. Downstream failures retain accepted Procurement evidence and observable same-event retry.

## 25. Acceptance Criteria

1. Only a verified delivered PO revision supplies receivable lines; foreign/old/unissued sources fail.
2. Two concurrent partial acceptances cannot overrun ordered or recorded-held capacity.
3. Required inspection and service deliverable/value rules cannot be bypassed by a status/body field.
4. Exact retries create one logical acceptance and one effect; conflicting retries deny.
5. Returns/reversals retain original evidence, never double-subtract and cannot consume more than unconsumed entitlement.
6. Core current actor/site/branch scope and worker-based independence hold on direct URLs, replays and queued commands under the approved concurrency contract.
7. Matching projections reconcile accepted minus return/reversal with complete consumed history.
8. Unconfigured Inventory/Assets/Finance remain explicitly unconfigured, with no duplicated stock/asset/financial records.

## 26. Test Scenarios

Author domain tests for partial splits, held/inspection failures, goods/service classification, service period/amount ceilings, eight-decimal values, source pins and net-effect arithmetic. Actual PostgreSQL/mapped HTTP tests must prove wrong header/line/PO/version/site FKs, unauthorized/alias/current-scope denial, simultaneous receipt/inspection/matching/correction, expected-version races, exact/conflicting retries and same-transaction rollback. Include duplicate/out-of-order integration facts, unavailable consumption, return after invoice consumption, full/partial administrative reversal versus business return and a new issued revision invalidating a draft. Real API Playwright extends existing receipt screens; no browser-local substitute for persistence evidence.

## 27. Ownership / Dependencies

Receiving/service owners own certification policy; Procurement owns receipt/acceptance effects and commercial returns; Core owns identity, organization, documents, approvals/audit/platform mechanics; Finance/AP owns postings/credits/payment and consumption resolution; Inventory/Assets own their ledgers. Depends on the initial PO verified issued-version contract, shared authorization linearization, Core document adapter registration, receipt/matcher lock/consumption agreement and separate reviewed schema/runtime privileges. No owner service is implied implemented by this specification.

## 28. Open Decisions / Assumptions

Confirm receiving sites, service owner assignments, inspection/conditional acceptance, requester receipt and segregation, explicit tolerance authority, service net/tax treatment, external delivery-note duplication semantics, return approval/replacement rules, consumed/posting-bound correction policy, Inventory/Asset item/site mapping and real integration readiness. Select the initial enabled operation subset and deployment tier gates after PO verification. These unresolved decisions are visible; do not fill them with hardcoded monetary policy or demo completion states.
