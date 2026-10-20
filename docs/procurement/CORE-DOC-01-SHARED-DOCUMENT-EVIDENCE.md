# CORE-DOC-01 — Shared document evidence

Status: implementation specification; first consumer is supplier invoice intake. Related issue: [#23](https://github.com/georgemunganga/module-connect/issues/23). Prepared: 20 October 2026. This specification does not enable uploads or claim a deployed scanner.

Foundation checkpoint (20 October 2026): the final rebuilt Core document suite passed **58/58** tests covering scoped lifecycle and persistence, private storage, scanner framing/failure handling, idempotent upload recovery and append-only operation evidence. Core P01 (`20261020060744_P01CoreDocumentFoundation`) and P02 (`20261020063543_P02CoreDocumentUploadRecovery`) are generated; the model has no pending changes. Their combined idempotent SQL applied twice to disposable PostgreSQL, with relational isolation/constraint checks. A real PostgreSQL workflow probe additionally verified exact replay, conflicting replay, concurrent owner limits, scan/event persistence, denied cross-company access and authorized download evidence. Test databases and containers were removed. **Neither migration is applied in production.**

The private store streams bounded PDF/PNG/JPEG input to owner-only directories, checks extension/signature agreement, computes SHA-256, rejects client paths and symbolic links, and retains immutable targets. A signature is not a malware verdict. The ClamD adapter uses a private Unix socket, bounded framing/reads, content length/hash checks, definition age and before/after definition consistency. An isolated ClamAV 1.5.4 engine with official definitions 28158 (dated 19 October 2026, 06:26:01 UTC) returned Clean for the clean fixture and Infected for EICAR. The PostgreSQL workflow probe uses a deterministic scanner fixture; it does not prove production antivirus provisioning. Invoice adapter verification passed 36/36 mapped HTTP tests and 60/60 mocked browser release scenarios; TypeScript and 17 scope/readiness unit checks passed. These are separate layers of evidence and do not prove real production scanner/storage/identity composition. No production upload endpoint or scanner is enabled.

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

Authorize the owner → stage bounded bytes in a private temporary file and calculate the content signature/hash → serialize the owner’s metadata decision → resolve the upload key or enforce the file-count limit → atomically register quarantined metadata and operation evidence → commit the immutable storage key → persist a scan lease → scan → atomically record the result, scan evidence and operation event → expose availability. An identical upload-key replay retains the existing version; changed content or filename conflicts. A failed final move can be recovered with the same file/key without another metadata record. Before download, authorize the owner again and require a Clean, available version. Record document metadata and the exact version reference in the owning module; resolve incomplete linking through a retryable operation rather than creating an unverified reference.

## 9. Status / State Transitions

Version states: Quarantined → Scanning → Available / Rejected / ScanFailed. ScanFailed may return to Scanning with a bounded retry and recorded attempt. Expired scan leases return to the queue. Rejected content remains inaccessible. Retirement changes reference availability with a reason while retaining the original version and scan evidence. A new upload creates a new immutable version.

Every transition records the actor/service, current version, required inputs, guard, timestamp, outcome and failure path. Scanning never silently approves a business transaction.

## 10. Permissions and Data Scope

The module adapter checks view/capture/edit permission and tenant/company/branch ownership before invoking Core. Core independently verifies that its document belongs to the same tenant, company, owner module, owner type and owner ID. A guessed document ID cannot bypass owner authorization. Download and list use the same checks as upload. Storage keys and quarantine paths are never public URLs.

## 11. Business Rules

File extensions and browser content types are insufficient: verify a supported content signature, enforce limits while reading, generate storage paths, and sanitize display filenames. Only the scanner can set scan outcomes. A scanner error, timeout, stale definitions or exceeded scan limit fails closed. Reject executable/archive content. Business workflows reference a specific available version and hash. Replacing or retiring evidence after a consequential decision requires the owning module's correction policy.

## 12. Data Fields and Ownership

Core document: ID, tenant, company, owner module/type/ID, classification, title, state, creator/time, retention policy reference and legal hold. Core document also retains an owner-scoped upload key. Core version: ID, document ID, version number, original filename, detected media type, byte count, SHA-256, generated storage key, upload actor/time, scan failure code and controlled lifecycle fields. Scan attempt: version ID, lease/attempt, engine and definition version, start/end, outcome and safe error code. Core audit: actor, action, target, reason, result and correlation ID. Procurement retains its existing AttachmentRef and invoice evidence reference; it does not copy the blob or become a competing document master.

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

### Scanner adapter design reference

The [official ClamD protocol](https://docs.clamav.net/manual/Usage/ClamdProtocol.html) supports streaming file bytes with `INSTREAM`; a shared filesystem path is unnecessary. The adapter must use bounded chunk framing, response limits, cancellation/timeouts and configured scanner limits compatible with the upload policy. ClamD TCP has no built-in authentication or encryption, so any configured transport must remain on a trusted private boundary or use a protected tunnel; do not publish its port. Scanner version/definition health and freshness evidence must be verified before accepting a clean verdict. The adapter is implemented for a local Unix socket. The reproducible [scanner probe](../../backend/core-api/tools/CoreDocumentScannerProbe/README.md) records the isolated-engine procedure. Production scanner provisioning and review remain pending. The test engine peaked at 1,097,306,112 bytes (about 1.02 GiB), so it must not be added to the shared runtime without capacity planning.

### Remaining implementation sequence

1. Private storage verification completed: generated keys, bounded streaming, hashes, type signatures, interrupted-write cleanup, immutable targets and directory permissions. Storage is an infrastructure port; it cannot authorize an invoice or release quarantined content.
2. Scanner adapter and definition-health guards implemented. The 46-test suite covers clean/infected, stale/future/missing definitions, definition reload, content mismatch, invalid/ambiguous/truncated replies, unavailable socket, timeout and caller cancellation. Clean/infected results passed through an isolated real engine. Pausing only that daemon produced Failed with scanner-timeout after the 30-second bound, as required. The daemon was unpaused, stopped and removed after evidence capture; production provisioning remains pending before enabling uploads.
3. Add owner-authorized upload/list/download use cases with transaction-safe version numbering, upload idempotency, bounded owner file counts, audit and recovery of orphaned writes. Reuse Core identity and Procurement invoice scope checks.
4. Wire the existing invoice document controls to those routes. Playwright must cover upload, pending/failed scanning, authorized download and denied cross-branch/company access; retain separate real-engine evidence.
5. Rehearse and apply the reviewed Core metadata migration, provision a least-privilege runtime database role and persistent private directory, then release only a source-compatible host with readiness and authenticated smoke checks. Retain backup and rollback instructions. No production document release is approved by the foundation checks alone.

### Invoice document adapter contract — implementation in progress

- `GET /api/procurement/v1/invoices/{invoiceId}/documents` lists Core-owned versions for the authorized invoice.
- `POST` to the same path accepts raw `application/octet-stream`, a UUID `Idempotency-Key`, and percent-encoded `X-Document-Name`. Actual content signatures and byte count are checked server-side. The same key/body can recover a failed response or file move; changed content conflicts. No client storage path or scan verdict is accepted.
- `POST /{versionId}/retry-scan` requires invoice capture permission and an invoice still in Received. Retry count and durable leases are enforced.
- `GET /{versionId}/download` checks invoice permission, company/branch scope, owner/version association, a clean scan and the stored file hash/length before an audited attachment response with no-store and nosniff.
- Procurement retains its existing AttachmentRef projection. Core remains authoritative for file content and scan state; a projection failure is recoverable by replaying the same upload. No invoice matching, posting or payment is performed.

Runtime activation is explicit and defaults off: `CoreDocuments:Enabled`. It requires a separate `ConnectionStrings:CoreDocuments`, a private persistent `CoreDocuments:PrivateDirectory`, and private `CoreDocuments:ScannerSocket`. Limits default to 10 files/10 MB; `MaxOwnerFiles` and `MaxFileBytes` are bounded configuration. The scanner uses UTC definition timestamps, a 30-second timeout and a 48-hour definition-age bound. The daemon must use matching UTC configuration. Schema readiness gates the routes and capability response.

The final isolated-source suite passed 58/58 local tests, including final-move failure recovery, extension/type checks, conflicting retry, blocked downloads, scanner recovery, denied owner access, foreign company/owner lookup and detection of post-scan file corruption. Compiled Core source hashes match this worktree. Actual PostgreSQL migration/retry/concurrency/scope verification passed: twelve concurrent attempts with one existing document saved nine more and rejected three at the ten-file cap. The P01/P02 idempotent SQL applied twice without duplicating history; relational constraints passed and the synthetic result was two migrations, ten documents/versions/scan results and thirty-one operation events. The disposable database was removed. Mapped invoice HTTP and browser coverage are still being completed before activation. The UI and production rollout remain pending. Abandoned pre-registration staging files require a bounded cleanup procedure before production activation; committed versions must never be treated as temporary files.

The PostgreSQL probe is also configured in the verification workflow. Publishing a host candidate is followed by the retained live-loan compatibility check; the known source mismatch in issue 29 must fail that release gate until reconciled. GitHub account billing still prevents a passing hosted-run claim.
