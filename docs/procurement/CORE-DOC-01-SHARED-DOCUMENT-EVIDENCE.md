# CORE-DOC-01 — Shared document evidence

Status: implementation specification; first consumer is supplier invoice intake. Related issue: [#23](https://github.com/georgemunganga/module-connect/issues/23). Prepared: 20 October 2026. This specification does not enable uploads or claim a deployed scanner.

Foundation checkpoint: `Mightyfin.Erp.Core.Documents.Domain` implements scoped ownership, immutable content metadata, bounded scan leases/retries, fail-closed outcomes and retained retirement. Its 13 lifecycle tests passed locally on 20 October. Persistence, storage, the real scanner, owner adapters and UI wiring remain pending; no Core document migration or upload endpoint has been enabled.

## 1. Feature Identification

Core → Documents → Record Evidence → Upload, inspect scan status, download, append a version and retire a reference. Core owns document metadata, immutable file versions, storage and scan evidence. Procurement owns the commercial record and its document references.

## 2. Purpose and Business Outcome

An authorized user can attach the original invoice and later retrieve the exact evidence used in a decision. Files become usable only after a successful security scan. An invoice remains Received until its separate validation and matching requirements are satisfied.

## 3. Scope and Exclusions

First release accepts PDF, PNG and JPEG source evidence, with a maximum of 10 files per owner record and 10 MB per file. These are configurable technical limits matching the existing vendor form. Office files, archives, OCR, e-signature and migration of existing employee files are separate increments. Adding a file never approves a supplier, invoice or payment.

## 4. Parent and Related Features

Shared Core identity, organization scope, permissions, audit and storage configuration; Procurement invoice intake, supplier qualifications, RFx responses and receipts. Existing worker-document records remain HRM-owned and are not reused as invoice records.

## 5. Actors and Participants

Record readers, record editors, a scanner service principal, authorized retention administrators and read-only auditors. Supplier users require an explicit supplier-scoped record authorization adapter before any external upload is enabled.

## 6. Entry Points and Triggers

The owning module exposes record-specific upload/list/download operations. The first adapter is `/api/procurement/v1/invoices/{invoiceId}/documents`. The Core service is called only after the adapter authorizes the actual invoice. A pending scan, scanner recovery, version upload or authorized retirement starts the corresponding background action.

## 7. Preconditions

Authenticated tenant, company and actor; permission to act on the owning record; current authorized branch; supported owner type; valid upload limits; private persistent storage. The scanner must be configured and have current definitions before a version can become Available. Its absence never produces a Clean result.

## 8. Workflow

Authorize the owner → validate request and content signature → stream to a private generated storage key while calculating SHA-256 and enforcing size → save quarantined version and audit → scan → atomically record Clean/Rejected/Failed result → expose availability. Before download, authorize the owner again and require a Clean, available version. Record document metadata and the exact version reference in the owning module; resolve incomplete linking through a retryable operation rather than creating an unverified reference.

## 9. Status / State Transitions

Version states: Quarantined → Scanning → Available / Rejected / ScanFailed. ScanFailed may return to Scanning with a bounded retry and recorded attempt. Expired scan leases return to the queue. Rejected content remains inaccessible. Retirement changes reference availability with a reason while retaining the original version and scan evidence. A new upload creates a new immutable version.

Every transition records the actor/service, current version, required inputs, guard, timestamp, outcome and failure path. Scanning never silently approves a business transaction.

## 10. Permissions and Data Scope

The module adapter checks view/capture/edit permission and tenant/company/branch ownership before invoking Core. Core independently verifies that its document belongs to the same tenant, company, owner module, owner type and owner ID. A guessed document ID cannot bypass owner authorization. Download and list use the same checks as upload. Storage keys and quarantine paths are never public URLs.

## 11. Business Rules

File extensions and browser content types are insufficient: verify a supported content signature, enforce limits while reading, generate storage paths, and sanitize display filenames. Only the scanner can set scan outcomes. A scanner error, timeout, stale definitions or exceeded scan limit fails closed. Reject executable/archive content. Business workflows reference a specific available version and hash. Replacing or retiring evidence after a consequential decision requires the owning module's correction policy.

## 12. Data Fields and Ownership

Core document: ID, tenant, company, owner module/type/ID, classification, title, state, creator/time, retention policy reference and legal hold. Core version: ID, document ID, version number, original filename, detected media type, byte count, SHA-256, generated storage key, upload actor/time and idempotency key. Scan attempt: version ID, lease/attempt, engine and definition version, start/end, outcome and safe error code. Core audit: actor, action, target, reason, result and correlation ID. Procurement retains its existing AttachmentRef and invoice evidence reference; it does not copy the blob or become a competing document master.

## 13. Screens and Data Views

Reuse the existing document section on the invoice/vendor forms: upload, filename/type/size, security-check status, retry guidance and download when available. Empty state offers Add document. Permission-denied, oversize, unsupported content, upload failure, rejected content and unavailable scanner each have a distinct plain-language message.

## 14. User Experience / Human Behaviour

Show progress and preserve the invoice when an upload fails. Explain that a file is waiting for a security check without exposing scanner internals. Disable download until available. Confirm retirement with a reason. Do not show success before persistence is confirmed or discard a prior usable version when a replacement fails.

## 15. Notifications and Communications

Notify the uploader when delayed scanning completes or needs action; notify the support owner after repeated scanner/storage failures. Messages contain authorized record links and filenames only where appropriate; never attach quarantined content to email.

## 16. Documents and Attachments

Every version is immutable. Signed or decision-linked versions remain attributable after retirement. Uploaded files are private, stored outside the web root, and served as attachments with safe response headers. Legal hold prevents physical disposal. Retention periods require an organization policy; no automatic deletion is enabled by an assumed duration.

## 17. Exceptions and Edge Cases

Empty/truncated upload; disguised executable; filename traversal; duplicate retry; same idempotency key with different content; concurrent version creation; owner removed or no longer editable; branch/company switch; permission revoked before download; malware; scanner unavailable; stale scan lease; missing blob; storage exhausted; DB failure after storage write; and legal hold during retirement/disposal.

## 18. Integrations / APIs / Events / Sandbox

Core exposes storage and scan interfaces with local private storage and a configured antivirus adapter as initial implementations. Module routes use shared ERP authentication and shell headers. Upload retries have owner-scoped idempotency; conflicting content returns 409. Publish versioned document facts after metadata commits using an outbox when downstream consumers are connected. Sandbox uses fake scanners for deterministic clean/infected/timeout tests and an isolated real scanner for release verification.

## 19. Audit / Security / Privacy

Audit upload, new version, scan decision, download, retirement, retention change and disposal. Keep content, credentials, paths and sensitive business data out of logs. Restrict quarantine access to the scanner and storage service. Bound scan time, decompression and concurrent work. Validate signatures and scanner definitions; do not substitute a filename check for malware scanning. Transport and storage protections follow the shared ERP Tier 3 standard.

## 20. Reports / Dashboards / Metrics

Pending scan count/age, failed/rejected scans, scan latency, storage failures, orphan reconciliation, missing blobs and authorized download audit. Access follows tenant/company and support role scope. Operations metrics contain no file content.

## 21. Configuration and Localization

Core manages private storage location, allowed types, file/count limits, scanner configuration and definition freshness, retry/lease limits, retention policy and legal hold authority. Procurement controls required evidence by transaction type/state. Dates and file sizes use shared locale formatting. No organization, employee or permission master is recreated.

## 22. Non-Functional Requirements

Stream bounded content; cap concurrent scanning; keep files private; support cancellation; preserve durable scan attempts; avoid holding a DB transaction during external scanning. Startup/readiness distinguishes storage and scanner availability. Establish throughput and scan SLA after measuring the production host; a health response alone is insufficient release evidence.

## 23. Postconditions

The user has an attributable, scoped, persisted version and an honest scan state. Only Available content can be downloaded or used as required decision evidence. Invoice state and Finance ownership remain governed by their separate workflows.

## 24. Failure and Recovery

Use generated temporary objects and an explicit cleanup/reconciliation process for failed metadata writes. Missing files block download and surface a support reference. Scanner outages retain quarantine and retry safely. A scan lease prevents duplicate workers from publishing conflicting outcomes. Repeated upload keys return the existing result when content matches. Test restore of metadata and blobs together. Roll back code only with a compatible document schema and file format; retain immutable evidence.

## 25. Acceptance Criteria

An authorized AP user uploads a real invoice; its byte hash and scope are recorded; download is blocked before a clean scan and allowed afterwards. Another tenant/company/branch or an unauthorized supplier cannot list, upload or download it. Invalid/oversized content creates no available version. Infected, failed, stale-definition and timed-out scans never release content. A repeated upload cannot create duplicate evidence; a replacement preserves the earlier version. Invoice matching cannot accept missing or quarantined required evidence.

## 26. Test Scenarios

Test the acceptance cases through domain rules, persistence and mapped HTTP routes. Add actual Postgres migration/restore verification, real scanner clean/infected fixtures, filesystem failure/recovery and concurrent idempotency/scan-lease cases. Playwright covers empty/upload/pending/available/error/read-only states and reload. A real authenticated production smoke check verifies the owner adapter and private download; mocked browser responses alone do not prove integration.

## 27. Ownership and Dependencies

Core owns the service and operations contract; Procurement owns invoice attachment eligibility and record access; security/operations owns scanner freshness and retention administration. Reuse the ERP runtime, organization scope, identities, permission grants and existing AttachmentRef model. First delivery requires Core schema/migration, storage/scan adapters, scoped invoice routes, existing UI integration, audit/recovery and release evidence.

## 28. Open Decisions / Assumptions

Confirm the retention policy/administrator, document classification defaults and scanner deployment capacity. The proposed initial type/size/count limits are technical defaults and do not establish legal retention or commercial approval policy. External supplier access and Office/OCR support remain explicit later increments. Record scanner configuration and real release evidence before enabling Available documents in production.
