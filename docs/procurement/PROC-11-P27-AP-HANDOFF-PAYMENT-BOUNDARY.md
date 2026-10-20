# PROC-11-P27 — AP handoff and payment visibility boundary

**Status:** Next bounded implementation plan; repository source audit only. No sender/callback/payment integration or migration is implemented here.
**Baseline:** `78e4687`. Dependencies: P25 accepted evidence, P26 operational matching/claims, separately authorized invoice approval/accounting validation, and an actual Finance owner contract.
**Tracking:** [Procurement matching/AP milestone #30](https://github.com/georgemunganga/module-connect/issues/30). **Standard:** GW-ENG-STD-001, Tier 3, 28-section framework.

## 1. Feature Identification

Procurement → Invoice Validation → Finance/AP Handoff → Prepare, deliver, reconcile and display Finance outcomes. The bounded implementation uses existing handoff/status records and outbox. It does not build an AP ledger or payment engine.

## 2. Purpose & Business Outcome

Send one approved immutable invoice snapshot to the designated Finance owner, recover uncertain delivery without duplicate liability, and show only authenticated Finance posting/payment outcomes. Users can see which boundary is blocked and who owns resolution.

## 3. Scope & Exclusions

Includes readiness, immutable payload preparation, stable source-version idempotency, durable sending/retry/reconciliation, authenticated AP results and read-only payment allocation/status projection. Each capability remains separately disabled until its authority/provider gates pass.

Excludes creating Finance accounts/payables/journals, tax accounting, payment approval/scheduling, bank transfers, supplier credits/refunds execution, recurring AP generation and batch payments. P26 ComparisonPassed or a new outbox row never implies invoice approval/AP acceptance.

## 4. Parent & Related Features

Reuse SupplierInvoice/Line, InvoiceMatchResult/Exception, Supplier.FinanceSupplierRef, FinanceInvoiceHandoff, FinancePaymentStatusRef, OutboxEvent, existing Core approvals/documents/access/audit and shared transaction coordinator. P26 supplies current immutable match evidence; a separate invoice approval/accounting gate supplies permission to hand off. Core/global configuration owns connection identity/secrets; Finance owns financial results.

## 5. Actors & Participants

Current authorized AP handoff operator, invoice approver, Finance service principal, reconciliation operator and scoped viewer/auditor. An integration principal is a genuine machine identity with exact tenant/entity/connection capability, not a fabricated HR employee. Current human role/worker scope applies to UI commands; authenticated owner identity/connection applies to inbound financial facts.

## 6. Entry Points & Triggers

Existing Bills detail gains readiness, handoff attempts/AP results and payment-status panels. Queue ready invoices only after full validation/approval. Finance callbacks or polling update read-only projections. Reconciliation handles unknown/rejected/conflicting outcomes explicitly. Existing Payables demo actions must not become live bank/payment operations.

## 7. Preconditions

Before prepare/send: current actor/action/company/selected branch; exact immutable invoice and mapping; current P26 comparison/claim; no unresolved hard exception; real invoice approval plus accounting/tax/coding authority; approved Finance supplier-account mapping and supported currency; accessible verified evidence; configured compatible Finance owner/transport/authentication/idempotency/reconciliation contract.

Supplier.FinanceSupplierRef is a reference, not proof that the current owner accepted its mapping. No default posting accounts, tax rules, payment method or Finance completion is guessed. Missing any prerequisite reports a precise blocker and creates no sendable handoff/outbox entry.

## 8. Workflow

1. Return readiness with current source versions and individual blockers. Intake/read remains usable while handoff is unavailable.
2. Under shared transaction/fence/source locks, verify approval/current match/accounting authority and pin a minimal immutable payload to existing FinanceInvoiceHandoff. Stable source-version key + payload digest identifies one intent; atomically enqueue existing Procurement outbox/audit/command receipt.
3. Worker claims an eligible outbox record using a lease. Publish/send outside business transaction with the original key and immutable payload. Transport acknowledgement means delivery only, not AP acceptance/posting.
4. Authenticated Finance response references exact connection/handoff/key/payload hash/invoice and stable owner message sequence. Deduplicate, validate and persist owner evidence/current status atomically.
5. On timeout/unknown response, query Finance by original idempotency key. Do not create a fresh key, liability or payment. Reconciliation must distinguish never accepted, pending, accepted, posted and unknown.
6. Project authenticated Finance invoice/payment allocation state to Bills detail. Do not calculate a Finance balance from browser records or infer Paid merely from a matching gross sum.

## 9. Status / State Transitions

| Dimension | States / authority |
|---|---|
| Local readiness | Blocked / Ready; computed from real source/approval/provider checks |
| Handoff intent | Prepared → Queued → Sending → TransportDelivered / RetryableFailure / OutcomeUnknown |
| Finance AP outcome | AwaitingResult → Accepted / Rejected → Posted; only authenticated owner fact with exact correlation |
| Reconciliation | Required → OwnerConfirmed / Conflict / Unresolved; manual notes alone do not set Posted |
| Payment visibility | NotConnected / AwaitingOwner / Scheduled / PartiallyPaid / Paid / Reversed / Failed; mapped from agreed Finance schema |

TransportDelivered is not Accepted; Accepted is not Posted; Scheduled is not Paid. Preserve conflicting/late facts as exceptions rather than regressing a known posted state. After any possible send, payload edits/cancellation require owner-confirmed safe compensation or rejection protocol, never local deletion.

## 10. Permissions & Data Scope

Existing invoice read remains; add explicit handoff prepare/retry/read/reconcile and Finance-result ingest permissions only after ownership review. Receipt recovery rechecks current human action/worker/branch and exact actor-owned target. Service callback authorization checks configured issuer/audience/connection plus bound tenant/entity/source, not caller-supplied organisation headers alone.

Scoped lists/details/counts/exports apply current company/selected branch/resource filters before paging. Ordinary procurement roles cannot mark Paid/Posted or inspect unrestricted Finance payloads. Existing payroll/hr_admin integration permissions do not grant Procurement AP access.

## 11. Business Rules

- One immutable approved invoice version/connection maps to one stable handoff key and payload digest. Same key/different bytes conflicts; repeated send/recovery cannot create another intent.
- Match currentness and separate invoice/accounting approval are required; no CurrentAndGranted=true/Complete=true stub provider.
- A generic string external reference, HTTP200 or broker publication is not Finance acceptance. Exact schema/result authenticity/correlation is required.
- Unknown remote acceptance freezes claim release and source replacement that could duplicate or erase obligations. Finance handoff attempts with missing posting ID remain potentially bound.
- Posted/Paid/credit/reversal authority stays Finance-owned. A PaymentRef or supplier credit header alone is insufficient.
- Receive payment allocation snapshots with explicit owner event ID, monotonic sequence, mode and invoice/posting allocation identity. Do not guess whether PaidAmount means delta, cumulative, gross or net.
- Credits, withholding, FX, refunds and reversed payments make invoice balance nontrivial. Show owner balance only if its schema proves coverage/freshness; otherwise show individual confirmed allocations/status and “Balance awaiting Finance.”

## 12. Data Fields & Ownership

| Existing object | Extend/reuse |
|---|---|
| FinanceInvoiceHandoff | Pin invoice/mapping/match/approval/accounting versions, connection/contract version, exact supplier-account mapping, immutable payload+SHA, original idempotency/correlation; operational state/attempt/last error/reconciliation evidence remain separate |
| FinancePaymentStatusRef | Keep append-only snapshots; add connection/owner event+sequence, AP posting/handoff/invoice/owner allocation identity, payload digest/time/status mode and source evidence. Exact decimal strings at API boundary |
| SupplierInvoice | Existing FinancePostingRef is projection only; retain authoritative owner/result reference/currentness. Never set by local UI or matching helper |
| OutboxEvent | Existing event identity/key/attempt/state/timestamps reused; extend claim token/lease/error/transport metadata only if required by the reviewed publisher store |
| InvoiceException | Reuse AP rejection/unknown/conflicting callback discrepancy ownership and resolution history |

Proposed **one new table only if no shared platform inbox is supplied**: FinanceIntegrationReceipt, an append-only authenticated owner-message receipt with connection/event ID, schema, source/key/hash, monotonic owner sequence, observed/received times, processing outcome and exact business target. This is dedupe/reconciliation evidence, not AP or payment ledger. Existing audit/outbox do not provide inbound message uniqueness; do not create a second generic integration-operation store.

Current payment unique(company, FinancePaymentRef) prevents append-only updates to one payment and multi-invoice allocations of a batch. Replace/add an explicitly reviewed event/allocation uniqueness key rather than overwriting rows or assuming one payment equals one invoice. Preserve legacy rows as unverified snapshots; no fabricated owner event IDs/approval backfill.

## 13. Screens & Data Views

Reuse LiveInvoices detail/document layout and ERP tables/components. Panels: readiness/blockers, immutable handoff reference, attempt/reconciliation history, AP response and confirmed payment allocations/freshness. Payments Made can later be a scoped Finance-backed read view; no payment-create control.

Batch Payments, Recurring Bills and Vendor Credits remain unavailable in live mode until their owner contracts exist. Their current local editors/record arithmetic are reference UI, not reusable backend authority.

## 14. User Experience / Human Behaviour

Use “Ready to send,” “Waiting for Finance,” “Delivery uncertain,” “Finance rejected” and “Payment confirmed by Finance.” Distinguish retrying the same intent from correcting a rejected version. After uncertain result retain original key/payload; recovery404 is inconclusive while a request may be in flight. Never show a manual Paid switch.

## 15. Notifications & Communications

Use existing audit/outbox infrastructure for authorized AP rejection/reconciliation tasks and Finance status notices. Notification failure cannot mutate financial state or trigger another posting. Financial handoff cannot use notification SMTP fallback or be marked delivered because an event lacked an email template.

## 16. Documents & Attachments

Reuse Core scanned immutable document versions and scoped references. Finance receives approved minimal evidence references or controlled copies under a defined access contract. No broad Procurement bank details/payment credentials; do not publish restricted full invoice documents in generic notification events.

## 17. Exceptions & Edge Cases

Missing approval/ledger owner, wrong supplier account/currency, disabled provider, duplicate invoice, source changed after prepare, send timeout, owner accepted but acknowledgement lost, conflicting same-key response, late rejection after posting, out-of-order payment snapshot, one payment allocated across invoices, payment reversal, partial credit/withholding and unknown balance require explicit states/reconciliation. Do not guess success to unblock UI.

## 18. Integrations / APIs / Events / Sandbox

Proposed scoped APIs:

- GET `/invoices/{id}/finance-readiness`, `/finance-handoffs`, `/payment-status`.
- POST `/invoices/{id}/finance-handoffs` with stable command key/expected source and authority versions; disabled until real validated-invoice authority exists.
- POST `/finance-handoffs/{id}/retry` with original intent, current scope and retryable state; no regenerated payload/key.
- POST `/finance-handoffs/{id}/reconcile` starts actual owner lookup/evidence workflow, never a free-form status setter.
- Authenticated owner-only POST `/integrations/finance/{connection}/results` (or agreed bus consumer) binds exact message identity/source/hash/version. No UI or arbitrary public account can assert Finance result.

Payload includes schema, tenant/entity, connection, invoice/version, supplier-account ref, currency, approved line coding/tax references and exact totals, match/approval/evidence hashes, idempotency/correlation. It contains no bank-transfer instruction. Finance returns accepted/rejected/posting identity plus exact result version; payment message separately identifies real invoice allocation and status semantics.

## 19. Audit / Security / Privacy

Preserve immutable intent and inbound facts, current actor/worker or real service identity, source/authority versions, key/hash, transport attempts and reconciliation owner/reason. Core tenant branch fence and same-Postgres transaction apply to human prepare/retry/recovery. Runtime workers have narrow per-connection source/queue rights; no HRM employee/payroll mutation grant. Reject wrong audience/tenant/entity/connection or replayed event with different payload, quarantine conflicts and redact secrets/errors.

## 20. Reports / Dashboards / Metrics

Separate queued/transport-delivered/owner-accepted/posted/payment-confirmed counts, failure/unknown age and reconciliation backlog. Include Finance as-of sequence/time and stale/unknown coverage. No local sum of invoice+demo payments as a Finance balance or total disbursed.

## 21. Configuration & Localization

Core/global integration configuration owns Finance connection identity, credentials/secrets, endpoint/bus routing and schema support. Procurement settings own handoff policy/coding mappings/readiness; Finance owns accounts/tax/payment rules. No hardcoded default provider/account or auto-approval. Organisation currency supplies defaults only; actual invoice/source/owner currency agreement remains exact. All money uses exact lexical codecs and agreed currency rounding semantics.

## 22. Non-Functional Requirements

At-least-once transport, stable consumer idempotency, bounded retry/dead-letter/reconciliation, recoverable leases, precise source concurrency, exact numeric/time/hash persistence and authorized pagination. Broker duplicate windows alone are insufficient to guarantee no duplicate AP posting. Network calls occur outside business locks with explicit prepare/send/result boundaries.

## 23. Postconditions

One durable handoff intent and its retry-safe transport/result history, or an honest blocker. Payment visibility contains only authenticated owner facts. No Procurement payable, journal, cash transfer, supplier ledger or financial reconciliation balance is created.

## 24. Failure & Recovery

Business transaction failure rolls back intent/outbox/audit/receipt together. Worker crash before/after remote acknowledgement resumes with original key; stale claim token cannot complete another worker's lease. Unknown commit uses command recovery, not automatic fresh command. After potential owner binding, source corrections are blocked or require explicit owner compensation. Conflicting/late callbacks remain retained and assigned for reconciliation; no destructive overwrite.

## 25. Acceptance Criteria

Current approved invoice authority is required beyond comparison; missing authority/provider blocks without intent/send. One intent/key survives replay/retry/lost acknowledgements. Real owner messages prove AP/payment status; wrong scope/message hashes/sequence reject. Current record access protects recovery/queues. Shared transport changes preserve existing HRM behavior without granting its payroll permissions to Procurement. No UI action initiates payment or invents a Finance balance.

## 26. Test Scenarios

Source-level readiness/authority deny tests; mapped API permission/selected branch/current worker/recovery; payload precision/hash/immutable source/version mismatch; actual PostgreSQL duplicate prepare/result/import and atomic rollback; two-worker lease race/crash/stale token; external fake-ack/timeout/reject/accept-posted sequences in a clearly labelled contract sandbox; source invalidation during queued send; owner lookup after ambiguous delivery; same-key/different response quarantine; multi-invoice payment allocations, repeated/out-of-order/reversed snapshots; no bank credentials in logs/events; real Bills UI reload/error/blocker and payments read-only behavior. Sandbox transport acknowledgements do not prove a production Finance owner is connected.

## 27. Ownership / Dependencies

P26 operational matching/current exposure and **separate invoice/accounting approval** precede send. Actual Finance owner must approve payload/result/idempotency/reconciliation/payment semantics and machine authentication. Existing HRM platform capabilities are inventoried below; reuse transport primitives through a reviewed extraction, not by disguising Procurement as payroll. Parent controls migration/test/runtime gates. No new schema version number is reserved until P25/P26 are verified.

## 28. Open Decisions / Assumptions

Finance/AP implementation/provider, invoice authority/approver and coding/tax ownership, machine principal provisioning, endpoint/bus auth, source freeze/correction protocol, owner dedupe retention/lookup, payment allocation/reversal/credit/withholding/balance semantics and support owners remain explicit configuration/contracts. Default is blocked/Not connected. No management approval, owner confirmation or successful AP/payment integration is fabricated.

## Appendix A — Existing integration infrastructure inventory

| Actual repository surface | Reusable capability | Boundary / missing implementation |
|---|---|---|
| Procurement Domain CommercialRecords: FinanceInvoiceHandoff/FinancePaymentStatusRef | Scoped handoff and append-only status projection; existing FKs/idempotency/payment index | No Procurement sender, owner callback or payment-read route found; fields lack immutable source/result/auth provenance |
| Procurement Domain Records.OutboxEvent + DbContext mapping | Atomic domain outbox with scoped unique idempotency key, attempts/state/time | No consumer/publisher store claiming procurement.outbox_events found |
| HRM Domain Entities/Integrations.cs + Application/Integrations.cs + Infrastructure/IntegrationOperations.cs | Durable operation, stable keys, payload snapshot, retry/reconciliation/download pattern | Payroll/HR-specific auth and source builders; manual accepted/matched reconciliation is not authenticated Procurement AP confirmation |
| HRM Infrastructure/Outbox.cs | Transactional writer, SKIP LOCKED claim/stale claim retry, backoff, NATS JetStream transport/message ID | EfOutboxWriter rejects non-hrm events; store selects hrm.outbox_messages; publisher creates HRM_EVENTS/mightyfin.hrm.> stream and HRM config |
| HRM Api/Program.cs outbox loop | Worker lifecycle/timeout/retry operational pattern | Notification SMTP-skipped/fallback behavior cannot acknowledge consequential AP delivery |
| HRM Api/ApiRoutesClean /hrm/integrations | Existing HRM operational console/actions | payroll/hr_admin roles and payroll payloads; do not repurpose as Procurement-wide API or grant HRM writes |
| Core backend projects | Existing Approvals and Documents domain/application/infrastructure/test services | No Finance/AP engine, generic inbound integration inbox or general connection/reconciliation service found in this source audit |
| Supplier.FinanceSupplierRef | Existing external accounting reference | Needs actual Finance-owned mapping/version proof; not a bank record or completeness attestation |

## Appendix B — File-by-file next implementation sequence

1. **Authority/provider contract first:** define `IValidatedInvoiceForAp` over real invoice approval/current P26 evidence and `IFinanceApOwner` transport/lookup/result semantics. Missing implementations return explicit unavailable proof; no stub successful provider. Keep `financeHandoff=false` and payment action gates closed.
2. **Procurement Application:** `InvoiceFinanceHandoffContracts.cs`/pure handoff state+payload builder validate exact pins, approval and readiness; reuse exact codecs. Add deterministic tests for comparison≠approval and unknown result binding. No duplicate financial calculations.
3. **Existing records/mapping:** extend CommercialRecords FinanceInvoiceHandoff/PaymentStatusRef and existing OutboxEvent metadata; add partial context guards/FKs/uniqueness. Add one authenticated receipt table only if a shared inbound inbox is not supplied. Preserve legacy rows as unverified and refuse destructive used-schema downgrade.
4. **Shared transport extraction:** extract the small envelope/NATS transport seam from HRM infrastructure into a shared Core capability only when implementing the real owner route; keep HRM writer/store/streams/auth unchanged behind its adapter. Add Procurement outbox store against existing table with lease/CAS/allow-listed AP event types. No copied HRM IntegrationOperationsService or second generic operations table. HRM outbox tests are relevant only to this shared change.
5. **Procurement Infrastructure:** `FinanceInvoiceHandoffService` coordinates prepare/current source locks/receipt/outbox; dedicated queue sender uses actual owner adapter outside transaction; result handler resolves authenticated connection and idempotent owner receipt; reconciliation does real owner lookup. No user-editable success setter or notification fallback.
6. **Host:** `ProcurementInvoiceFinanceRoutes.cs`, current permission catalogue, options/readiness/DI and machine auth policy; extend live meta granularly. Existing Core company/scope/document auth reused. No private Finance credentials through frontend DTO.
7. **Existing UI:** extend LiveInvoices/detail/document panels with readiness/status/history, exact strings and retained command recovery. Optional Payments Made live read adapter only after owner facts exist; keep local batch/payment/recurring/credit mutations gated out of live mode.
8. **Verification/release:** focused pure/model/mapped tests; additive migration/model check/apply twice; restricted PostgreSQL race/replay/lease/result/source rollback; controlled owner-contract sandbox; HRM regressions for extracted transport; real API Bills UI and independent review/UAT/owner acceptance/operations. Production enablement requires real Finance confirmation, not a sandbox green test.

**Evidence:** Read-only source audit and documentation. No code implementation, SDK/build/test/DB/browser operation, migration or deployment performed by this task.

## Appendix C — Concrete owner audit and smallest next boundary, 20 October 2026

This supplements the original source baseline; it does not replace the 28-section contract
or claim P25/P26 runtime completion. Reviewed current root source in
`procurement-order-execution`, the P26 candidate's contracts/options and capability roadmap
checkpoint `db1a9b9`. Only this existing plan is changed; no Finance code/schema is introduced.

### C1. Repository evidence: the receiving Finance owner is absent

| Exact source | Finding and implementation consequence |
|---|---|
| `backend/` | Present business projects are Core, HRM and Procurement. No Finance/AP domain, payable aggregate, supplier accounting-account master, invoice posting receiver or owner-query service was found. Do not treat names/references as a hidden Finance implementation. |
| `backend/procurement-api/src/Mightyfin.Erp.Procurement.Domain/CommercialRecords.cs`, `FinanceInvoiceHandoff`, `FinancePaymentStatusRef` | Existing Procurement records track external references/status only. They do not constitute a Finance-owned payable or inbound acceptance service. Reuse them for local projections, not Finance business truth. |
| `backend/procurement-api/src/Mightyfin.Erp.Procurement.Infrastructure/Data/ProcurementDbContext.cs` | Current handoff uniqueness is tenant/entity/key; payment uniqueness is tenant/entity/payment-reference and payment snapshots are append-only. Preserve these records and migrate exact event/allocation semantics before supporting repeated/multi-invoice payments. |
| `backend/hrm-api/src/Mightyfin.Erp.Hrm.Infrastructure/IntegrationOperations.cs`, static `Contracts`, `CreateFinancePostingAsync`, `ReconcileAsync` | The displayed `finance/available` label is a static payroll integration catalogue. Creation reads PayrollRun and emits payroll account names; reconciliation is a payroll/hr_admin manual accepted/matched setter. Neither proves an AP receiver or authenticated supplier-invoice result. Do not copy its account defaults or let its manual result setter mark Procurement Posted/Paid. |
| `backend/hrm-api/src/Mightyfin.Erp.Hrm.Infrastructure/Outbox.cs` | Existing SKIP LOCKED leasing, stale-claim recovery, backoff and JetStream message ID are reusable transport mechanics through a reviewed extraction. The concrete store is `hrm.outbox_messages`, and published/fallback-delivered is transport state. Reuse does not make SMTP or broker acknowledgement an AP business decision. |
| `backend/hrm-api/src/Mightyfin.Erp.Hrm.Api/ProcurementHostIntegration.cs`, `ProcurementScopeMiddleware` | Current Procurement request pipeline resolves workforce/local user, shell entity/branch, role and active worker for human commands. Machine ingress must not impersonate an employee, accept a UI admin role as Finance service proof, or trust caller-supplied shell headers for connection scope. |
| P26 `InvoiceMatchingExecutionContracts.cs` and `ProcurementInvoiceMatchingExecutionOptions.cs` | Historical-exposure owner and document-line mapping reviewer default explicitly Unavailable. Qualified comparison requires verified owner evidence, immutable mapping/documents and current source/receiving constraints. A new AP inbox with zero rows cannot make those older histories complete. |
| Existing `reference-ui/src/features/LiveInvoices.tsx` | Current live UI records and displays received invoices and Core document evidence. It has no valid send-to-AP authority hidden behind its status badge. Existing payment/batch editors are not evidence of a Finance API. |

**Important prerequisite missing before P27 send:** P26 comparison/claims are not an invoice
validation/approval transition. The current Core approval engine is reusable, but the
Procurement authority catalogue currently implements PR/PO-specific actions/policy bases.
Invoice approval/accounting authority needs its own documented exact action/policy/source
contract using that engine; reusing a PR/PO grant or merely setting invoice.Status=Validated
would create an unauthorized bridge. Supplier accounting mapping and tax/coding ownership
must also be resolved by the actual Finance owner rather than copied from payroll defaults.

### C2. Narrowest honest implementation sequence

1. **Readiness/read contract now:** implement the proposed scoped finance-readiness/history
   query over existing invoice/match/handoff references with explicit individual blockers:
   `InvoiceValidationAuthorityUnavailable`, `MappingEvidenceUnavailable`,
   `HistoricalExposureUnavailable`, `FinanceOwnerNotConfigured`,
   `FinanceSupplierAccountMappingUnavailable`, `MachineConnectionNotConfigured` as applicable.
   These describe source facts, not client-selected flags. Intake/documents and draft review
   stay usable. No blocker may create a Prepared/Queued handoff, outbox delivery or AP status.
2. **Validated-invoice boundary:** implement the missing Procurement invoice validation and
   accounting-approval workflow using existing Core approval/exact identity/branch fence,
   pinned current P26 claim/mapping/evidence and governed authority. Define authoritative tax,
   coding, supplier-account and currency evidence as typed owner interfaces. Unavailable
   owners remain unavailable. Acceptance is a pinned validated-invoice version, not a bare
   mutable status string or a ComparisonPassed row.
3. **Select and implement one real receiving owner:** if Finance is external, implement its
   documented authenticated intake/query adapter. If this ERP will own Finance, first define
   a minimal **Finance-owned AP intake** capability and its owner/schema/permissions; do not
   place it in Procurement or repurpose HRM's IntegrationOperation as the payable owner. The
   bounded business outcome is **ReceivedForReview** of an immutable approved request. It
   creates no AP ledger, posting, tax entry, payment schedule or balance. It is separate from
   a generic integration message receipt, which only proves authenticated delivery/deduping.
   Creating that new owner requires an explicit module definition, not an assumption that
   Finance already exists. No Finance module path/table name is reserved by this plan.
4. **Then connect preparation/sender/owner receipt:** use the existing Procurement handoff,
   transactional outbox/audit and command receipt. Send outside the transaction with one
   immutable key/digest. Couple owner dedupe and its intake creation atomically on the
   Finance side; callback/poll/query returns the same typed owner fact. Unknown delivery must
   query that same key. Do not send until steps 2–3 are actual capabilities.
5. **Owner review/posting remains separate:** ReceivedForReview means the Finance owner has
   custody for review; it does not establish a payable, accounting approval or Posted state.
   Actual AP acceptance/posting/credit/reversal needs the Finance-owned continuation and its
   evidence. Payment visibility is another owner-fact contract after that, not a substitute
   for completing AP or permission to enable local bank/payment buttons.

This is an executable dependency sequence, not a proposal to leave P26 permanently blocked.
It identifies exactly which missing owner capabilities must be implemented or connected.
They cannot be replaced with a successful stub, human-entered Complete=true or a silent
empty-history assumption to make the existing UI appear integrated.

### C3. Minimal versioned inbound contract after validated invoice

A proposed Finance owner intake API (or equivalent authenticated bus command) must bind:

- Schema/contract version, authenticated sending connection/service principal, tenant/entity,
  unique handoff intent and original idempotency key, canonical immutable payload SHA.
- Invoice ID/original version and digest, supplier and Finance-account mapping ID/version,
  currency, exact approved invoice lines/coding/tax references and approved totals as decimal
  strings; immutable Core document-version references/hash/access contract as required.
- Procurement validation authority reference: exact current invoice version, Core case/terminal
  decision and governed policy version, approved business-snapshot SHA and validity/freeze
  evidence. P26 comparison result/claim/mapping and owner exposure watermark/lease remain
  separately referenced. The receiver validates authoritative evidence; callers cannot grant
  themselves authority by submitting a convincing-looking case ID or Verified=true field.
- Source/receipt accepted allocation identity and original credit linkage when applicable,
  sufficient for later owner reconciliation without creating another goods/stock ledger.

The exact same scope/key/payload returns the recorded owner intake reference; the same key
with different bytes conflicts. Owner receipt authentication binds issuer/audience/connection,
not a browser workforce session or organisation header. The receiver checks destination
scope independently and stores source/connection provenance durably. A lost acknowledgement
is recovered by querying the original key; a 404 while requests are in flight is not proof
that no owner binding occurred.

Initial owner results are deliberately limited to `ReceivedForReview`, a correlated
`RejectedEnvelope` with reason, or an explicit unknown/pending outcome. An HTTP202/broker ACK
is only transport acknowledgement unless the authenticated response proves the atomic owner
intake record. `ReceivedForReview` never maps to SupplierInvoice.Approved/Posted/Paid. Later
Finance business decisions need their own separate authentic event/sequence and workflow.

A shared inbound receipt, if implemented in Core, may dedupe authentic connection messages;
it must not duplicate the Finance intake business entity or act as a generic success setter.
Do not create both a new generic operations table and another Procurement inbox when the
same receipt contract can serve the boundary. Decide ownership before migration generation.

### C4. Consumption/obligation ordering: avoid circular fake completeness

Finance intake, posting and external obligations can exist independently of a local P26
claim. The later real exposure owner must provide complete authoritative history with an
explicit scope, migration/legacy coverage boundary, watermark, lease and allocation digest;
it must include pending/unknown handoffs that may already bind the invoice. Starting a new
Finance intake table empty proves nothing about existing external or legacy payables.

One invoice/handoff/allocation identity must correlate local claims with owner obligations,
so transfer does not double-count both while also never releasing capacity prematurely.
An uncertain send keeps the local hold until an authenticated owner proves never bound,
rejected/compensated or transferred under the agreed protocol. The owner decides accounting
facts; Procurement preserves operational accepted capacity and its immutable claim history.
This contract must precede implementing a real `IInvoiceHistoricalExposureOwner` provider.
A received-for-review inbox alone cannot satisfy that provider's completeness contract.

### C5. Backend and existing UI contracts for the bounded next slice

- Human GET finance-readiness returns invoice/source/version refs, `handoffState`,
  `ownerState`, separate `paymentState`, `capabilities` and stable blocker code/message pairs.
  Unknown histories are Unknown/Unavailable, not empty complete arrays or zero balances.
  Current tenant/company/selected-branch/action filters apply before counts/pagination.
- Human prepare/retry/recovery uses an actor-owned stable command key and exact current
  validated source versions. `allowedActions` comes from current server guards. No Paid,
  Posted, accepted-result or free-text Finance-reference mutation is offered to the browser.
- Machine owner ingress/lookup needs a separate exact authenticated connection capability
  and scope adapter. Reuse Core token/security/audit primitives where present; add missing
  machine identity plumbing explicitly rather than silently bypassing workforce checks.
- Extend the existing LiveInvoices detail with clear sections for validation, handoff,
  Finance review and payment evidence. Examples: “Invoice review not complete”, “Finance
  connection not set up”, “Sent; confirmation pending”, “Received by Finance; review pending”.
  Preserve the existing table/forms/documents; do not create a second invoice editor.
- Payments Made becomes a read-only owner projection only when its provider is real. Keep
  Recurring Bills/Batch Payments/Vendor Credit execution separate and unavailable until their
  accounting/settlement contracts exist. Do not delete reference UI or present it as live.

### C6. Negative acceptance cases that must precede send/receiver enablement

1. Received or ComparisonPassed invoice, missing/expired/revoked invoice approval, mismatched
   source/mapping/claim/approval digest, stale worker/branch scope or unreviewed coding cannot
   prepare a sendable intent; all invoice/handoff/outbox/audit/receipt writes roll back.
2. Missing Finance owner/account mapping, disabled machine connection, fake issuer/audience,
   wrong tenant/company/connection, shell-header substitution and human admin token on
   machine ingress cannot create an owner intake/result or reveal another scope's status.
3. Same key/same digest retries return exactly one intent/owner intake; same key/new bytes,
   conflicting owner reference and callback hash/sequence conflict fail or quarantine, never
   overwrite confirmed history. Concurrent duplicate delivery must not create two intakes.
4. Worker crash before/after send or owner commit, stale lease completion, timeout and response
   loss preserve the original payload/key. Do not regenerate an invoice or release its claim
   merely because no local posting reference exists. Lookup404 while in flight stays uncertain.
5. ReceivedForReview/HTTP202/JetStream publish/manual reconciliation note cannot set AP
   Accepted/Posted or Paid. No payroll default accounts or notification fallback can cross it.
6. Empty new inbox/local rows cannot mark old exposure Complete. Missing legacy coverage,
   unavailable owner watermark/lease, pending unknown delivery and uncorrelated local/owner
   allocations block any comparison/claim release dependent on that history.
7. Invoice corrections/credit/reversal after possible owner binding require the exact owner
   rejection/compensation protocol; neither status edit nor deletion can erase obligations.
8. UI handles loading, exact empty/not-connected states, forbidden scope, stale version,
   uncertain retry/recovery and authentic owner review history without enabling payments.
   Exact decimal strings and references survive reload without JS-number rounding.

All eight groups require pure/current-authorization tests, mapped API tests and genuine
isolated PostgreSQL duplicate/race/rollback/lease tests; real owner contract sandbox and Bills
browser acceptance follow. No implementation or verification was executed by this audit.

## Appendix D — Bounded read-only invoice readiness source contract

Source base `42f9395`; this increment authors a useful read for the existing Bills detail.
It does not implement the sender, new approval workflow, machine receiver, Finance owner,
posting/payment capability or any new setting/table/provider. It is not deployed or tested
by the source author. The broader P27 dependency sequence above remains required.

**API:** GET `/api/procurement/v1/invoices/{id}/finance-readiness`, mapped with existing invoice
intake routes and existing `procurement-invoice-read` policy. It shares the exact existing
invoice list/detail tenant/entity/confined-branch query. It does not add a canonical-worker,
approver, machine-principal or handoff permission requirement to ordinary invoice readers.
Existing middleware still owns current session/company/workforce/context validation; this
increment neither bypasses nor broadens it. Inaccessible invoice returns the same 404,
unauthenticated/denied/scope requests retain existing 401/403 behavior. The existing broader
invoice reader's selected-branch semantics are not silently redesigned in this narrow slice.

**Typed view:** `InvoiceApReadiness` returns invoiceId/version/status, State=Blocked,
CanPrepareHandoff=false, owned evidence summary, blocker code/message pairs and next-step
code/message pairs. Evidence includes matching schema availability, recorded current-mapping
ID/revision and original-digest currentness, last comparison ID/version and original-digest
currentness, the existing safe `InvoiceMatchSummary`, and unresolved own-invoice exception
count. ComparisonCurrentness is explicitly NotRevalidated: this GET does not re-run matching,
resolve current supplier/receipt/Finance authority or claim that recorded facts are approval.
An operational invoice-version bump alone does not rewrite its original business digest.

Matching schema unavailable is `MatchingEvidenceUnavailable`, with unknown counts represented
as null, not zero or an empty complete history. This differs from the real `InvoiceNotCompared`
condition when the matching schema is available but no last result exists. Existing matching
schema readiness is used as a read-store capability; matching execution need not be enabled
for a reader to inspect already recorded history. No new availability setting is invented.
The first read selects only original intake columns, so unavailable P26 tables are not queried.

Mapping hash/line/original binding failures, changed mapping pointer, changed original invoice,
malformed comparison evidence and outstanding invoice issues receive explicit blockers. Any
mapping/result lookup is additionally bound to this invoice and current tenant/entity; the
projection sanitizes foreign/mismatching inputs and filters comparison lines/discrepancies to
this invoice's original line IDs using `InvoiceMatchReadProjection.Summary`. Full source JSON,
other invoices' allocations, private owner references, callback secrets and banking details
are never returned. Invoice version is re-read before response; an in-flight change yields
409 InvoiceReadinessChanged and asks the reader to refresh rather than combining two versions.

Two concrete missing capabilities always block sending: InvoiceValidationAuthorityUnavailable
and FinanceReceiverUnavailable. A local Status=Validated or recorded Outcome=Compared cannot
satisfy them. No handoff/payment totals or owner state are guessed. Next steps guide the
existing detail to original documents, line mapping or recorded comparison review and explain
that governed invoice approval plus a real Finance connection are still required. Frontend
hookup can render these messages inside existing LiveInvoices detail; no new page or send/
retry/pay button is introduced. This commit supplies the backend contract, not that UI hookup.

All queries are AsNoTracking and GET performs no SaveChanges, comparison, policy evaluation,
state creation, outbox/audit/receipt write or owner network call. No schema migration is needed.
The existing P26 schema/provider readiness gates continue to govern real matching execution.

Authored acceptance source:

- Mapped HTTP intake/read tests for schema unavailable vs available/not compared, exact current
  invoice version, AP reader/Core admin access, anonymous/permission/missing/company/confined-
  branch/cross-tenant denials, concrete next steps and repeated-read zero persisted effects.
- Pure projection tests for Compared/status-label nonauthority, original/mapping changes,
  corrupt evidence, unknown schema/counts and foreign evidence/privacy boundaries.
- Existing actual PostgreSQL --matching probe now requests readiness over a genuinely recorded
  blocked comparison and checks mandatory owner/approval blockers, filtered summary/privacy
  and unchanged invoice/line/mapping/result/exception/claim/exposure/receipt/handoff/payment/
  audit/outbox footprint. This remains authored, unexecuted verification source.

Definition of Done for this slice still requires central compilation, relevant API/pure and
restricted-runtime PostgreSQL checks plus independent review. The later Bills detail hookup
needs its own UI acceptance. No AP or full Procurement completion claim follows from this read.
