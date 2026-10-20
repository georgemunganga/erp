# PROC-06-P01 — Internal RFQ publication and approved-demand allocation

**Status:** Proposed technical contract for independent review; no governed implementation or approved business policy. 20 October2026.  
**Hierarchy:** Procurement → Strategic Sourcing → Internal RFQ Publication → source conversion / review / publish / cancel.  
**Parent:** [PROC-06](PROC-06-STRATEGIC-SOURCING-AND-AWARD.md). **Roadmap:** [M5](PROC-ROADMAP-LIVE-BUILD.md). **Standard:** GW-ENG-STD-001 Tier3, existing28-section framework.

## 1. Feature Identification

`PROC-06-P01`, first governed internal RFQ increment. Operation IDs: `O1` select approved competitive demand; `O2` reserve and create linked draft; `O3` edit permitted draft intent; `O4` submit publication review; `O5` independent Core decision and publish frozen internal RFQ; `O6` return/reject/cancel and release unused demand; `O7` read own/assigned records and recover command receipts. Business role owner: Procurement governance; engineering owner must be assigned through the existing issue/change record. Source specification does not claim that an owner has accepted a spending policy.

## 2. Purpose & Business Outcome

Prepare a competition against genuinely approved, funded and unallocated demand without letting that demand simultaneously become direct orders or another RFQ. Publish only independently authorized immutable requirements. Success is a frozen internal RFQ publication and clearly pending supplier dispatch, or reasoned closure with reconciled reservation release. Publication is not supplier notification, bid opening, award, PO commitment or payment.

## 3. Scope & Exclusions

First supported route: internal RFQ, one selected legal entity/branch/currency per event, explicit active `CompetitiveSourcing` method, one or more approved PR lines with traceable quantities. Existing unlinked draft routes remain available. Included: scoped source picker, source reservation, versioned review/publication, eligible supplier invitation records and controlled pack references, exact command recovery and cancellation.

Excluded from P01: supplier portal identity, external email/SMS delivery, public tender, RFP technical-envelope rules, surrogate quotations, bid-content intake/opening/scoring, award, contract signature, order conversion, Finance commitment and Inventory/Assets effects. P02/P03/P04 below complete those operations in dependency order. `sourcing` must not become globally true merely because P01 passes; use a narrowly named internal-publication capability and command-level guards.

## 4. Parent & Related Features

Reuse PROC-02/03 immutable PR approval, canonical workers, pinned policy and Finance evidence; PROC-01 current supplier eligibility; PROC-CFG-01 independently reviewed method/policy; Core approval cases/stages/assignments/decisions and private document service; PROC-08 common source quantities; shared transaction/session fences, audit and outbox. Do not call HR leave approval or create another worker, permission, document or budget master.

## 5. Actors & Participants

Source buyer/draft owner, publication reviewer with explicit finite authority, requester as original demand owner, supplier steward, auditor and integration operator. Principal identity and canonical worker identity are distinct. Reviewer must be a different canonical worker from event creator, source requesters/makers and any actor excluded by the published policy; a second account for the same worker cannot compensate. Initial approver strategy is explicit named ProcurementManager assignment with supported Core One/All modes, not arbitrary committee/role lookup. Supplier users do not participate in P01.

## 6. Entry Points & Triggers

Reuse existing Requests approved-demand and RFQ list/detail/editor routes. Add a source-picker mode rather than replacing the imported UI. Existing own RFQ draft CRUD (`ProcurementSourcingDraftRoutes`) remains draft-only. A configured competitive PR method selects the sourced route; free text `Method="RFQ"` cannot supply execution authority. No automatic conversion on policy evaluation, scheduler or email delivery.

## 7. Preconditions

Current canonical actor, explicit operation permission, tenant/entity/site and complete source scope; approved PR business version/outcome and Core case; active current competitive method; confirmed required current Finance reservation under the existing trusted adapter/approved manual mode; reconciled complete allocation history; eligible source catalog provenance where applicable. Draft defaults use selected company currency. Publication additionally needs reviewed SOURCING policy, finite named reviewer grants, eligible invited suppliers, locked criteria/checklist, schedule, approved original document versions and an independently approved current publication case. Missing policy/owner evidence blocks publication; safe draft capture can remain usable.

## 8. Workflow

1. Query approved competitive PR-line candidates with current scope applied before counts/pages. Show quantities derived from the single allocation ledger; unknown history is unavailable, not zero.
2. O2 sends exact source IDs/versions and quantities with UUID idempotency key. In one shared SERIALIZABLE transaction, acquire existing Core→catalog fences before snapshot, lock source PRs sorted by ID, then RFQ; verify complete authority and source ledger; create event/lines and `SourcingEvent/Reserved` allocations, audit/outbox/receipt atomically.
3. O3 edits only a sourced draft using event/source versions and reason. Validate the complete detached proposal first; reconcile added/removed/resized source reservations atomically. Source references, category/unit and purchase type come from pinned approved demand, not caller policy fields. No silent line replacement.
4. O4 validates event, effective sourcing rules, configured minimum competition, suppliers, criteria, document versions, deadlines and funding. Freeze submission/publication intent and worker/grant/policy evidence; open Core review. Submitted material fields lock. No supplier is marked contacted.
5. O5 reviewer decisions enforce current actor/scope/grants and independent workers. Final approval plus all source/policy/Finance checks creates a numbered immutable publication and marks internal state Published/Open according to trusted server schedule. Publication pin names every source reservation, specification, supplier invitation, original document version/hash and criteria/schedule. A durable outbox fact records publication; undelivered messages stay Pending.
6. Return clears no financial evidence. Source reservation remains explicitly held while returned draft awaits correction; correction needs a new review/version. Rejection/cancellation releases the event's unused RFQ reservations exactly once, records reason and invalidates future publish/open actions. Existing source PR Finance reservation is not silently released: it belongs to the PR and Finance, not this RFQ hold.
7. Exact replay returns the original immutable outcome only after current permission/scope checks. Historical read/recovery/cancel do not reauthorize a new commitment or pretend that a now-expired catalog offer is current. Unknown network outcome uses receipt lookup, never a new-key automatic POST.

## 9. Status / State Transitions

Legacy unlinked draft: existing Draft/Cancelled; no implied approved demand. Sourced event: Draft → PendingPublicationApproval → Published (future OpensAt) → Open (server time), with ReturnedForChanges, Rejected, Cancelled, Expired alternatives. Closed/Evaluation/AwardPending/Awarded belong to later increments. Returning/rejecting/cancelling never erases publication/history. Core owns case/stage/task states; domain owns the RFQ and its permitted effects. No automatic approval at SLA expiry. A queued future event's displayed Open state derives from trusted time or an idempotent owned transition, not browser time.

## 10. Permissions & Data Scope

Proposed explicit actions: `procurement-sourcing-convert`, `procurement-sourcing-submit`, `procurement-sourcing-publish`, `procurement-sourcing-cancel`, assigned review/read and existing draft actions. Final names enter the existing action catalog with no automatic financial grants. Publication spending tuple: permission `procurement-sourcing-publish`, action `SourcingEvent.Publish`, currency and basis `ApprovedLineBase`; amount is the entire event's selected approved quantities × pinned approved source unit estimates using exact arithmetic. One grant must cover the full event amount and every relevant branch/department/category; do not sum small limits or imply unlimited authority from a role. Policy publication, event publication and later award are separate authorities.

Own/assigned scope remains constrained by current Core membership and selected branch. Source and event legal entity/branch/currency must agree. External supplier access is absent. Read serializers expose no HR private fields, supplier banking information or nonexistent bid evidence. Fences cover only documented cooperative writers; do not claim account/role/worker revocations are wall-clock fenced unless their owners participate.

## 11. Business Rules

- Approved competitive source cannot enter the DirectPurchase PO route merely by changing a method label or route column.
- One common PR-line availability equation includes reservations for all known supported target types. Unknown allocation targets/statuses or cache mismatch fail closed.
- RFQ reservation is a demand hold, not an extra Finance encumbrance or ordered quantity. Requester approval remains original authority; RFQ publication review does not increase it.
- Criteria, bidder eligibility, deadline, supplier list and published specification/document versions cannot be silently edited after publication. Changes require a new reviewed publication revision; revision execution is outside P01 unless fully specified.
- Quote minimums come from the pinned PR sourcing rule plus approved event policy; never hard-code a default. P01 records requirements, not compliance with received quotes.
- Current supplier/category/catalog/method validity and event full-amount authority are rechecked at new consequential commands using their documented owner linearization contract. No old offer is perpetual permission for a new commitment.
- Cancelled demand or received/ordered balances cannot be released again. No accepted/supplier bid is fabricated by publishing.

## 12. Data Fields & Ownership

Reuse SourcingEvent, SourcingLine, SourcingInvitation and PurchaseRequestAllocation. Required bounded extensions: event branch/department context, explicit method identity/version, sourced execution version, approved source identities and publication/review pointers; immutable review/publication snapshot and retained canonical participant identities. Required publication evidence includes complete selected source references/quantities, original PR business/evaluation/approval/Finance pins, named reviewer/grant evidence, method/policy versions, effective times, schedule, criteria and supplier eligibility/document pins.

Add records only when immutable evidence or durable command effects require them; do not make one generic JSON blob the business model. Existing mutable draft fields alone cannot prove publication. New references must use tenant/entity composite ownership keys and stable EF principal identities; mutable Draft→published bindings cannot be principal alternate keys (P25/P26 lesson). Legacy records retain execution0/unverified; do not backfill approvals, workers, branch or source bindings from guesses. Values stay decimal; transport uses shared exact bounded string converters and `ExactDecimalArithmetic`, not numeric38 representability assumptions.

## 13. Screens & Data Views

Existing `LiveSourcing.tsx` supplies own draft list/edit/detail/cancel. Imported `RfqWizard.tsx` supplies reusable sections for items, schedule, supplier/team selection and documents; its local-demo save/publication behavior is not backend authority. Add source-linked draft mode, current source balance, publication checklist, review task, publication evidence and dispatch Pending view using existing tables/forms. Keep standalone drafts visibly unverified. No new menu is required. Assigned reviewers need purpose-specific detail without broad PO or source request access.

## 14. User Experience / Human Behaviour

Use plain labels: “Prepare quote request”, “Send for review”, “Approve publication”, “Published internally”, “Supplier delivery pending”. Avoid “Sent” until verified dispatch. Distinguish loading/empty/denied/stale/current-source-unavailable/unknown-write outcomes. Preserve typed drafts on validation errors, clear actor/company/branch-sensitive content immediately on scope change, and retain original command key/body per principal and scope for safe recovery. Exact quantities display without JavaScript rounding. Explain held demand separately from budget reserved by Finance.

## 15. Notifications & Communications

Review tasks and publication/cancellation facts are durable internal outbox events after commit. Reuse Core notification interfaces; existing HRM publisher is not evidence that Procurement delivery works. Supplier invitation rows start Draft/PendingDispatch; no email receipt is invented. Optional controlled manual supplier dispatch confirmation can follow a separate explicitly reviewed contract, like the PO dispatch boundary. Operator retries/dead letters/reconciliation must exist before external delivery is enabled.

## 16. Documents & Attachments

Reuse Core private documents, scanning, immutable versions and record access. Generalize the existing invoice-specific IDocumentOwnerAccess adapter with an owned dispatcher supporting sourcing records, rather than copying storage/upload/scanner services. Publication uses Available versions with hashes. Future sealed bid owners require field/document embargo until opening; a general attachment URL or invoice-read policy cannot grant bid access. Missing sourcing document owner adapter is an explicit publication blocker when policy requires files.

## 17. Exceptions & Edge Cases

Insufficient/unreconciled quantity; multiple buyers/RFQs/orders selecting the same demand; mixed currency/branch/method; stale source version; inactive supplier/category/item; quote count not configured; zero or overprecise quantities; expired reservation; reviewer alias/conflict or offboarding; deadline reached during review; cancelled event with pending notification; response lost after commit; a legacy unlinked event mistakenly treated as sourced. Each fails with an owned actionable state; cancellation must not be blocked solely because a source catalog offer was withdrawn after an earlier valid reservation.

## 18. Integrations / APIs / Events / Sandbox

Proposed additions under existing `/api/procurement/v1/sourcing`: bounded `/approved-sources`; `POST /from-approved-sources`; `PUT /{id}/source-draft`; publication review/open/assigned decision/history; `POST /{id}/source-cancel`; GET command receipt recovery. Exact paths/DTOs freeze with implementation review before a UI adapter changes; no API exists merely because listed here.

Reuse `ProcurementApprovalTransaction`, Core case execution, canonical identity adapter and existing Finance evidence contract. Core source types distinguish `sourcing_publication` from future `sourcing_award`. Events describe committed facts (RFQ draft reserved/submitted/publication approved/published/cancelled), schema-versioned with tenant/entity/source/version/correlation/command identity. No portal authentication, external Finance reservation or bidder payment call is added. Synthetic full pipeline must create approved competitive demand through real PR/Finance/Core routes, never insert fake Approved source rows.

## 19. Audit / Security / Privacy

Append source selections/balance effects, material changes, review/decisions, worker/grant/policy pins, publication and cancellation reasons and exact command effects atomically. Current actor checks apply to receipt recovery. No normal role deletes evidence or alters published snapshots. Scope before counts/pages/exports and purpose-specific reviewer projection are mandatory. Keep private bid/team/HR data out of generic lists, notifications and logs. Named issuer identity is distinct from canonical worker; retain worker evidence for legitimate offboarding recovery without reactivating departed staff.

## 20. Reports / Dashboards / Metrics

First increment supports truthful own/assigned RFQ counts, pending publication review age, internally published versus dispatch-pending and reserved/unreleased demand. No bidder participation, competitive savings, award success or supplier performance metrics yet. Define calendar/time-zone denominator and owner before control KPIs; internal publication must not increment a “supplier invitations delivered” count.

## 21. Configuration & Localization

Add a SOURCING schema validator/projector to the existing governed PolicyVersion publication dispatcher. Do not weaken PR/PO/receiving/matching validators. Policy defines supported RFQ method, publication authority strategy, competition minimum, required checklist/documents, schedule bounds and independent-worker restrictions; first mode excludes exceptions/reopen/late-bid rules. Configuration drafts can be incomplete; publication requires current owner/reference proof. No amounts/default grant/quote count/time limit are prescribed. UTC storage, explicit displayed time zone, selected company currency and existing numbering service apply.

## 22. Non-Functional Requirements

Bounded before-pagination SQL, exact quantities/money, retry-safe atomic effects and controlled session/transaction lifetime. No network email/scanner/Finance call while holding source locks: consume trusted prevalidated owner evidence, retain outbox and fail closed if unavailable. Reuse dedicated least-privilege execution connection: Core execution DML only as authorized, immutable decisions INSERT/SELECT, Core identity/org SELECT only; add finite Procurement sourcing/evidence grants, not schema ownership. Apply the ERP accessibility/localization/load/error conventions. Performance/availability targets follow approved volumes; no invented SLA numbers.

## 23. Postconditions

A sourced draft holds exactly its selected uncommitted quantities in the one authoritative allocation ledger. A published internal event has independent immutable authority/source/requirements evidence and PendingDispatch invitations; no quotation or award exists. Cancel/reject releases only unused event-owned holds and records reason, with PR Finance authority untouched. Shared source caches reconcile, and every successful mutation has auditable durable replay evidence.

## 24. Failure & Recovery

Any failed validation/CAS/authority/DB operation rolls back event, common allocations/caches, Core cases/decisions, audit/outbox and command receipt together. Retry only confirmed transaction aborts, not unknown commit responses. Exact command fingerprints include source versions/quantities/proposed intent/reason. Unknown outcomes require current authorized GET recovery; 404 is not permission to repost under a new UUID. Do not suppress historical records to clean failed fixtures. Configured/used additive schema cannot be destructively downgraded; review a protected unused-only Down and forward/protected-restore path.

## 25. Acceptance Criteria

1. Draft capture defaults selected company currency and grants no competition/publication authority.
2. Only actually approved competitive PR lines with current scope and complete balances can reserve into sourced RFQ; direct-only or unknown routes fail.
3. Concurrent RFQ/RFQ and RFQ/PO selection cannot exceed approved quantity or hide a hold from the other target's reconciliation.
4. Publication requires a different canonical worker and one finite grant covering complete event amount/categories/scope/time; self/alias/expired/revoked/partial-limit attempts fail.
5. Frozen criteria/suppliers/source/document/deadline/policy hashes agree with the Core case and emitted publication; no caller flag can substitute proof.
6. Cancellation/rejection releases only its still-reserved quantities exactly once; current withdrawn offer does not erase history or block necessary release.
7. Lists/counts and assigned task details deny other tenant/entity/selected branch and hide unavailable source metadata before paging.
8. UI uses actual API and original command recovery; actual database proves no duplicate allocation/publication/outbox or false supplier-delivery state.

## 26. Test Scenarios

Pure tests: complete ledger reducer known/unknown target/status, exact boundaries/nonrepresentable products, typed direct versus competitive method, full grant category/scope, alias workers, publication pins, duplicate source IDs, cancellation and transfer planning. Model tests: exact same-company parent/source ownership, stable keys, immutable evidence and no guessed legacy pins. Mapped API tests: draft versus sourced operations, source-scoped picker/task, explicit permissions, policy review/current Finance, denial-before-mutation and unchanged original evidence.

Actual PostgreSQL/HTTP tests must cover source locks/order, concurrent RFQ/RFQ and RFQ/PO, current cooperative catalog/branch fences, stale receipt replay after revocation, atomic Core/domain rollback, unknown response recovery, independent worker, bad source/document/hash/lineage, protected migration Up/unused Down/used veto and least-privilege DB role. Exact staged existing UI then real browser must reserve approved competitive demand, submit, independently publish, read actual PendingDispatch and cancel a separate uncommitted event with persisted balance proof. No generic in-memory “Approved” source fixtures can replace that gate.

## 27. Ownership / Dependencies

Procurement owns RFQ intent, commercial route, demand allocation and publication policy/evidence. Core owns identity/org/permissions, generic approval execution, documents, audit transport/notifications and shared infrastructure. Finance owns reservation truth; Inventory/Assets are not touched. Required first implementation changes are in the appendix below. Parent orchestrator controls shared resource gates, migrations, integration review and release; this document author ran no new tests or code jobs.

## 28. Open Decisions / Assumptions

Technical contract review must accept the common target-aware allocation semantics and publication tuple before implementation. Business must publish actual company RFQ rules, reviewer grants, competition/criteria/document requirements and owner-authorized Finance evidence mode; configuration remains off by default meanwhile. Missing external delivery/portal is an explicit P01 exclusion, not hidden success. Existing freeform draft methods/categories and unlinked legacy RFQs are not automatically valid governed inputs. P01 is the next candidate to satisfy DoR after technical review; full PROC-06 remains draft, not build-ready end to end.

## Appendix A — Exact shared source-balance change plan

### Existing source evidence

- `Application/PurchaseOrderSourceAllocation.cs:Check` currently combines genuine approved-PR/Finance evidence, **DirectPurchase-only** method/route checks, and a ledger restricted to TargetType PurchaseOrder. Reusing it unchanged for RFQ is wrong; weakening its direct route would open an unapproved order bypass.
- `Domain/Records.cs:PurchaseRequestAllocation` already owns request/line, target type/id, quantity/value/currency, status, actor/reason/time and idempotency. `Data/ProcurementDbContext.cs` already scopes its PR/line FK and unique command identity. It is the common quantity record; do not introduce a sourcing-specific shadow balance. Its existing EstimatedValue mapping is numeric(18,4), while exact four-place quantity×price can require eight decimals. Before recording new exact products, widen the existing value column to a compatible eight-place contract (bounded by actual decimal representability), retain all old values and guard precision-losing downgrade; do not let SQL silently round the evidence.
- `Application/ProcurementSourcingDraftRules.cs` rejects linked PR lines; `API/ProcurementSourcingDraftRoutes.cs` only own unlinked drafts. Existing source-less events must remain so until explicitly converted through verified demand.

### Refactor sequence (proposed, not implemented)

| Step | Required change | Guard that stays intact |
|---|---|---|
| A1 | Extract narrow immutable approved-demand verification from PO Check into a Procurement application helper accepting the real PR pin/outcome/Core decision/policy/Finance evidence. No target route or action authorization inferred. | Exact tenant/entity/version/hash/worker/Finance lineage; current actor remains API-owned. |
| A2 | Introduce one complete known-target allocation reducer over existing rows: PurchaseOrder Reserved counts held, Ordered counts ordered, Released counts neither; SourcingEvent Reserved counts held, Released counts neither. No SourcingEvent Ordered and no implicit unknown/legacy status acceptance. | Sum(all supported Reserved)=PRLine.AllocatedQuantity; sum(PO Ordered)=PRLine.OrderedQuantity; accepted<=ordered; available=approved-cancelled-held-ordered; exact arithmetic and unknown/mismatch denial. |
| A3 | PO direct adapter composes A1+A2 plus its existing active DirectPurchase method, matching pinned zero-quote rule and direct route checks. Update every PO source availability/reserve/resize/order/release caller to the same reducer. | Existing direct PO behavior and conservation tests remain; competitive PR still cannot directly create PO. No fake PO placeholder for an RFQ. |
| A4 | RFQ adapter composes A1+A2 plus current CompetitiveSourcing method and pinned rule demanding that same route. Reserve existing allocation target SourcingEvent and exact event line binding in one transaction; no financial ledger. | Source quantities/prices/categories/units/version remain owner-pinned; event scope/method checks independent. |
| A5 | Add source-owned FK/guard/readiness and idempotent reservation effect evidence linking allocations to actual RFQ/source lines; keep stable identity keys. Map known target ownership explicitly, since TargetId is polymorphic and a simple UUID alone is insufficient. | Same tenant/entity/request/line/event/quantity; no foreign target or arbitrary string accepted. Do not duplicate target authorization in the frontend. |
| A6 | Return/reject/cancel reconciles only matching event-owned Reserved rows, retains history/reason and adjusts caches atomically. Unknown prior commit recovers exact receipt. | No deletion, no direct Finance reservation release, no freeing Ordered/accepted quantity. |

For each approved PR line define `Qavailable = Qapproved - Qcancelled - ΣReserved(PO,RFQ) - ΣOrdered(PO)`. An RFQ hold is counted exactly once; invitation, bid, evaluation and recommended award are not additional PR reservations. All source operations serialize on the same upstream PR locks. The reducer must reject a ledger it cannot prove complete, including inactive retained allocations; never filter away a conflicting target.

### Later award transfer (contract constraint, not P01 execution)

Award authority binds each selected immutable bid version/line and event-reserved quantity. It does not add a second PR hold. At later approved award→PO conversion, atomically transfer the exact quantity: retire/release the prior RFQ reservation with explicit transfer provenance, create any exact residual RFQ reservation and the new PO reservation, retain predecessor/transfer-group evidence and reconcile the total. Quantity conservation is unchanged during transfer; only the holder changes. A recommendation or award label cannot enable the current DirectPurchase-only PO route. That consumer needs an explicit typed approved-award proof adapter, selected supplier/quoted terms and Finance commitment; no `bypassCompetition=true` input. Design immutable transfer linkage/version/CAS before implementing partial/split transfers.

## Appendix B — Smallest subsequent internal bid-to-award boundary

After P01 proof, **P02 internal surrogate quotations** reuses SupplierBid/SupplierBidLine/Invitation with explicit manual channel, immutable original Core document versions, received-versus-entered trusted times, actor/worker, bid submission version, current invited supplier and sealed record/document access. The recorder necessarily sees data being entered; document that access rather than claiming cryptographic blindness. Before opening, other buyers/evaluators cannot read bid contents. No portal identity or email-delivery success is assumed.

**P03 controlled opening/evaluation** adds authorized close/open evidence, named evaluator workers, required conflict/no-conflict attestations, independently resolved conflicts, locked criterion definitions and append-only evaluation rounds/corrections. Current BidEvaluation unique(bid,evaluator,stage,criterion) plus immutable writes cannot represent corrected rounds; design explicit version/round/supersession before enabling edits. No default scoring weights or winner algorithm are invented.

**P04 recommendation/independent award** binds actual eligible supplier and submitted bid/version/line, normalized exact quoted terms, criteria/results/rationale, event/source remaining quantity, original+current Finance and independent full-amount finite award authority. Existing SourcingAwardLine lacks selected bid-line/version/unit-price evidence; add owned immutable pins/composite constraints before claiming award proof. Only after this and the typed A6 transfer successor can an approved award be consumed by PO. Reopening, sealed technical/commercial envelopes, formal tender, external portal, split-award complexity and exchange-rate normalization require separately reviewed supported contracts.
