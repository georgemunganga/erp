# Procurement outbox → shared delivery integration plan

**Status:** independent source audit and bounded plan only; no dispatcher, schema, configuration, database role, message or production change. **Source baseline:** published ERP `7fb6163cd681b2b0ed5c2b3bc03d5b34f4690928`, 22 October2026. GW-ENG-STD-001 Tier3 applies. This plan is not a claim that notifications or downstream integrations are operational.

## 1. Current trace and reusable contracts

| Source | Actual contract / limitation |
| --- | --- |
| `backend/hrm-api/src/Mightyfin.Erp.Hrm.Api/Program.cs`, registration and `RunOutboxPublisherAsync` | Resolves `IOutboxPublisherStore`→`EfOutboxPublisherStore`, batches50, delivery deadline5..120seconds, bounded polling, NATS primary or explicitly enabled SMTP. The production publisher is intentionally HRM-only; do not change its configured store or infer Procurement delivery from its health. |
| `backend/hrm-api/src/Mightyfin.Erp.Hrm.Infrastructure/Outbox.cs`, `EfOutboxWriter` | Enforces `hrm.` event prefix; writes HRM `OutboxMessage` with tenant, public ID, subject, environment and correlation. Existing unit-of-work owns transaction. It is not a Procurement event writer. |
| Same file, `EfOutboxPublisherStore` | Actual SQL claims `hrm.outbox_messages` using `FOR UPDATE SKIP LOCKED`, marks publishing, reclaims after5minutes and completes by row ID. Backoff is10×2^min(attempt−1,6), i.e. maximum640seconds despite outer900 cap. No terminal attempt/deadletter threshold or lease-owner completion token. Reuse mechanisms with explicit safeguards; do not claim these missing controls already exist. |
| Same file, `IHrmEventPublisher` / `NatsHrmEventPublisher` | Established transport, ack checking, bounded envelope serialization, stable `MsgId=row.PublicId`. Stream `HRM_EVENTS` covers only `mightyfin.hrm.>`. Current envelope has tenant but no legal entity. Interfaces accept concrete HRM rows; extract a neutral adapter boundary rather than create a second transport stack or cast Procurement rows. |
| Same file, `ISmtpNotificationFallback` / `SmtpNotificationFallback` | HRM template and recipient allowlist only. Current loop records unsupported email events as `smtp-skipped` successful/published, including after NATS failure. This behavior MUST NOT acknowledge Procurement integration events: it would discard obligations. SMTP cannot substitute for Finance/Inventory integration delivery. |
| Same file, `EmployeeNotificationService` | Inbox/read state uses HRM outbox and subject, workforce role checks and HRM destinations. It is not a Core account inbox and cannot serve workerless Procurement users by renaming a URL. Reuse presentation/API patterns through a Core-owned delivery projection, not fake employee or copied HRM business records. |
| `backend/hrm-api/src/Mightyfin.Erp.Hrm.Application/Messaging.cs` | Existing writer/unit-of-work, delivery summary/retry and inbox DTO patterns. Generalize only the transport/delivery contracts proven common; Procurement retains event semantics and recipient policy. |
| `backend/procurement-api/src/Mightyfin.Erp.Procurement.Domain/Records.cs`, `OutboxEvent` | Tenant+legal entity, event ID/type/version, aggregate type/ID, payload, producer idempotency key, Pending state, attempts/next-attempt/published time plus base creation/version metadata. No recipient/branch/environment/correlation/lease-owner/error/deadletter fields. CreatedBy is provenance, not notification recipient. |
| `...Procurement.Infrastructure/Data/ProcurementDbContext.cs` | `OutboxEvents`, unique `(TenantId,LegalEntityId,IdempotencyKey)`, index `(State,NextAttemptAt)`. Normal query filter is tenant+company. Save guard requires authenticated scope and versions modifications. No background Procurement publisher adapter exists; do not use unscoped request context or blanket IgnoreQueryFilters as a new delivery authority. |

Repository source search found no durable Procurement event consumer or generic shared notification dispatcher. NATS broker acceptance is distinct from downstream execution and user delivery. Integration status screens/contracts do not prove that a consumer exists.

## 2. Existing producer semantics

Already-persisted events include request approval opened/decision/terminal state, Finance release requested/confirmed, PO draft/approval/dispatch requested/delivery observed, sourcing draft reserved/cancelled, receiving recorded/inspected/accepted and invoice compared. Exact producers are:

- `ProcurementApprovalExecutionRoutes.cs` / `ProcurementApprovalExecutionReads.cs`.
- `Mightyfin.Erp.Procurement.Application/PurchaseRequestApprovalOutcomes.cs`.
- `ProcurementOrder*Commands.cs` and `ProcurementOrderExecutionSupport.cs`.
- `ProcurementSourcingExecutionRoutes.cs`, `ProcurementReceivingCommands.cs` / `ProcurementReceivingRoutes.cs`, `ProcurementInvoiceMatchingCommands.cs`.

Names are mixed: `purchase_request.*.v1`, `purchase_order.*.v1`, `sourcing.*.v1`, `supplier_invoice.compared.v1` and `procurement.*.v1`. Do not silently rename persisted EventType/EventVersion/idempotency keys. Supplier CRUD visibility alone does not mean supplier-change events are already produced. Inventory, Finance and supplier dispatch effects remain their owning contracts; a publisher must not create them just because an event exists.

## 3. Bounded target contract

1. Introduce a shared, module-neutral delivery envelope/transport port in the existing platform boundary. Retain HRM row/store adapters and behavior compatibility; add a Procurement adapter. No second NATS/SMTP client engine, HRM table copy, fake workforce identity or domain rule duplication.
2. Proposed envelope: stable event ID derived from module+tenant+company+existing row ID, original type and version, occurredAt, module, tenantId, legalEntityId, aggregateType/Id, original producer key, safe payload and explicitly sourced correlation/environment. Distinguish delivery ID from business effect key. Missing legacy correlation must be null or a clearly labeled delivery correlation, never fabricated original request evidence.
3. Use a reviewed Procurement NATS subject namespace such as `mightyfin.procurement.<registered-kind>` with a versioned exact event-type→subject registry. Do not concatenate arbitrary persisted strings into broker subjects. Preserve original event type in envelope; avoid duplicate `procurement.procurement` paths. Stream/retention/dedup-window/consumer owners must be approved before activation. Existing HRM stream remains unchanged.
4. Freeze first-event allowlist, payload schema and destination capability before draining backlog. Unknown/unsupported/malformed events are quarantined with operator-visible reason, never marked published or sent through a generic email fallback. Inventory/Finance/PO dispatch subscriptions remain disabled until each consumer contract and owner is ready.
5. Claim only explicitly configured tenant/company partitions with short atomic lock/claim transactions, bounded batch, cancellation and no external delivery inside DB transaction. Use durable lease owner/token/expiry and compare-token completion so a stale worker cannot complete a reclaimed row. Current Procurement metadata lacks a lease token and sanitized failure/deadletter record: decide a versioned owning schema change or shared delivery sidecar before coding. Existing HRM stale reclaim alone is not enough proof. No migration ID is assigned here.
6. At-least-once delivery: ack→local completion can fail. Preserve stable broker MsgId across retries; broker dedup is window-bounded. Consumers need durable `(consumer,tenant,company,eventId)` idempotency recorded atomically with their effect. Financial release keys and PO effect keys remain independently enforced. Do not claim exactly-once delivery or mark Paid/Issued/StockReceived based on broker acknowledgement.
7. Define retryable failures, bounded exponential backoff+jitter, attempt cap, poison-event deadletter and authenticated scoped inspect/requeue with reason/audit. No requeue with a new event ID, silent TTL purge, or deleting original event. Keep durable audit independent of notification success.
8. Notification projection resolves actual current Core account/assignment IDs and current legal-entity/branch access. Employee is optional. A creator is not automatically an approver/recipient; supplier email is not an authorized internal account. Require explicit audience/channel templates and supplier-scoped policies. Since branch is absent from the generic outbox row, derive it only through a versioned producer payload/immutable source reference with validated scope; unknown branch/audience is blocked rather than companywide broadcast.
9. Separate broker publish status, integration consumer outcome and per-recipient channel/read status. Generic transport acknowledgment cannot mark every notification delivered. Do not use SMTP failure fallback to bypass a required integration consumer.

## 4. Finite publisher role and startup boundary

- Provisioning is a separate reviewed owner artifact, never API-startup grants. Dedicated Procurement publisher account needs only SELECT and exact delivery-state/lease/attempt timestamps/version updates on its owning queue/delivery metadata, plus schema USAGE. No INSERT/UPDATE business source, audit/identity/Core master/Finance/approval evidence, no DELETE/TRUNCATE/CREATE/ownership/elevated membership. New delivery history may require append-only INSERT into its own reviewed table.
- Keep request API append privileges and publisher completion privileges separate. RLS or an equivalent explicit deployment partition policy must bind permitted tenants/entities; cross-company polling is not authorized merely because the process can connect. Do not reuse superuser/owner credentials or assume production HRM role is finite.
- A dedicated process mode must construct only the required services and perform no implicit HRM migration/bootstrap. Current Program executes HrmDbContext migrations and LocalIdentityBootstrap BEFORE `--run-outbox-publisher`; reusing that path blindly introduces owner/bootstrap dependencies. Refactor with explicit reviewed compatibility tests, without altering the existing production publisher until new gates pass.
- Broker credentials are separate scoped secrets with publish-only registered subjects and bounded ack permissions. Stream creation/admin permissions should belong to provisioning where feasible. No arbitrary webhook URL or payload recipient can expand routing authority.
- No configuration/role/stream change is approved by this document. Keep Procurement publisher off and HRM-only production publisher unchanged.

## 5. Ordered implementation and acceptance milestones

| Milestone | Concrete change / required proof |
| --- | --- |
| D0 contracts | Register first finite event family, payload/audience/routing and replay/backlog policy; agree shared envelope/transport port and lease/deadletter storage owner. Record security/data minimization and retention. |
| D1 shared transport extraction | Neutral transport adapter using existing NATS/SMTP implementation; unchanged HRM tests/stream/templates and genuine HRM publisher smoke. Unsupported Procurement integration cannot become SMTP success. No real external delivery in tests. |
| D2 Procurement durable store | Genuine owner-generated migration if required; finite role readiness; scoped lock/lease/CAS completion, restart/reclaim, bounded retries/deadletter and audit. Actual PostgreSQL concurrency, stale completion, partition isolation, privilege-denial and migration/recovery gates. |
| D3 broker integration | Private local isolated broker only, no production messages. Prove exact namespace/envelope/ack, repeated event stable MsgId, outbox commit/rollback and broker outage/restart. Consumer durable dedup proves one effect after ack-loss/replay; broker-only success is explicitly insufficient. |
| D4 notifications | Core-only maker/independent approver recipients, actual current grants/branch; missing/revoked/foreign recipients blocked, no sensitive attachment/bank data in messages. Shared UI inbox/read/retry with scoped permissions and no workforce prerequisite. |
| D5 operational release | Reviewed finite role/partition/stream credentials, backlog dry inventory and allowlist, safe initial disabled deployment, test environment acceptance, monitoring queue age/attempts/deadletters/consumer lag, delivery runbook and rollback. Explicit activation approval, small approved partition/canary, preserve pending events on rollback. |

Minimum adversarial cases: malicious EventType/JSON, unknown version, duplicate producer key, same row IDs across companies, stale completion token, lost publish ack, consumer crash after effect, cancelled row loop, unavailable broker, SMTP unsupported event, timeout after remote success, revoked recipient/branch scope, disabled destination, poison backlog, no sensitive logs/exports and no source-state/AP/stock mutation by publisher. Full user-facing acceptance includes pending/sent/failed/retry states and current Core-only access.

## 6. Open decisions and release limits

Engineering/operations must choose first consumers, tenant partitions, broker provisioning owner, retention/dedup horizon, lease duration, retry cap/backoff, deadletter/requeue authority, shared inbox owner and actual recipient policy. Existing HRM sender branding is HRM-specific; shared Core branding should be applied through the owned template contract rather than copied defaults.

No runtime, broker, SMTP, API, database or UI acceptance was executed for this audit. This document adds no action permissions, flags, schema, role grants or outbound messages. It does not turn an existing Procurement outbox row into an executed integration.
