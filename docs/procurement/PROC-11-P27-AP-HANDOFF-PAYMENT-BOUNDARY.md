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
