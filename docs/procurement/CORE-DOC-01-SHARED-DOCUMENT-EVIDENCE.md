# CORE-DOC-01 — Shared document evidence

Status: implementation specification; first consumer is supplier invoice intake. Related issue: [#23](https://github.com/georgemunganga/module-connect/issues/23). Prepared: 20 October 2026. This specification does not enable uploads or claim a deployed scanner.

Foundation checkpoint (20 October 2026): the final rebuilt Core document suite passed **58/58** tests covering scoped lifecycle and persistence, private storage, scanner framing/failure handling, idempotent upload recovery and append-only operation evidence. Core P01 (`20261020060744_P01CoreDocumentFoundation`) and P02 (`20261020063543_P02CoreDocumentUploadRecovery`) are generated; the model has no pending changes. Their combined idempotent SQL applied twice to disposable PostgreSQL, with relational isolation/constraint checks. A real PostgreSQL workflow probe additionally verified exact replay, conflicting replay, concurrent owner limits, scan/event persistence, denied cross-company access and authorized download evidence. Test databases and containers were removed. **Neither migration is applied in production.**

The private store streams bounded PDF/PNG/JPEG input to owner-only directories, checks extension/signature agreement, computes SHA-256, rejects client paths and symbolic links, and retains immutable targets. A signature is not a malware verdict. The ClamD adapter uses a private Unix socket, bounded framing/reads, content length/hash checks, definition age and before/after definition consistency. An isolated ClamAV 1.5.4 engine with official definitions 28158 (dated 19 October 2026, 06:26:01 UTC) returned Clean for the clean fixture and Infected for EICAR. The PostgreSQL workflow probe uses a deterministic scanner fixture; it does not prove production antivirus provisioning. Invoice adapter verification passed 36/36 mapped HTTP tests and 60/60 mocked browser release scenarios; TypeScript and 17 scope/readiness unit checks passed. These are separate layers of evidence and do not prove real production scanner/storage/identity composition. The independently reviewed frontend follow-up `f95d0a6` is published after seven invoice scenarios and wider 62-case coverage (61 initial passes, one unchanged Finance loading case passed on focused rerun); see the operational record for exact evidence. No production upload endpoint or scanner is enabled.

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


## Durable document-use pins — controlled feature addendum

**Feature ID:** CORE-DOC-01-U1. **Prepared:** 21 October 2026. **Status:** proposed prerequisite; source audit at `6ec30de`, no implementation, migration, activation or release evidence for this addendum. Core remains the owner although this existing shared specification resides in the Procurement documentation directory. Earlier foundation verification above does not establish durable-use readiness. The first proposed consumer is [RFQ internal publication O4/O5](PROC-06-P01-INTERNAL-RFQ-PUBLICATION-AND-SOURCE-ALLOCATION.md#o4o5-implementation-delta--next-bounded-m5-milestone).

### 1. Feature Identification

Core → Documents → Durable Document Uses → Pin a reviewed version, retain a publication use, authorize an exact pinned download and prevent managed retirement/purge. This extends the existing service; it is not another uploader or document engine.

### 2. Purpose & Business Outcome

A review and its resulting publication retain the exact available requirements pack they used. A later replacement, record change or managed retirement cannot silently remove that evidence. Procurement owns why a use exists; Core owns content, versions, scan evidence and retention enforcement.

### 3. Scope & Exclusions

Initial support requires the document metadata and consuming workflow to share one proven physical PostgreSQL database and transaction. O4 creates review-use references; O5 adds separate immutable publication-use references to the same versions. There is no blob copy. External document stores, supplier delivery, arbitrary public links, a generic records-retention engine and automatic expiry/disposal are excluded. No period is prescribed.

### 4. Parent & Related Features

Extends this shared document feature. Consumes Core identity, authorization, document storage/scanner and the existing Procurement/Core approval coordinator. Relates to RFQ publication revisions, assigned Core reviews, immutable publication evidence and original-key command recovery. Invoice document behavior must remain compatible.

### 5. Actors & Participants

The current canonical worker with explicit owning-record preparation permission; the currently assigned independent reviewer; the consuming workflow service; authorized auditors; and future explicitly authorized retention operators. Canonical worker independence and record/site scope are verified through existing Core/module contracts, never inferred from a role label or client worker ID. Historical signers need not be fabricated as currently active.

### 6. Entry Points & Triggers

Existing owner-authorized upload and scan precede O4. O4 submission freezes an exact pack and pins it when opening the Core review. O5 final approval creates the publication and its own uses atomically. Purpose-limited downloads start from the authorized review/publication. Managed retirement/purge must consult uses before changing availability or removing bytes. Exact route names remain subject to contract review; no new endpoint is asserted as implemented.

### 7. Preconditions

Current actor/action/tenant/entity/site and record access; exact consuming revision and pack fingerprint; a ready same-store enlisted document adapter; immutable version ownership; Available state with matching successful scan evidence; supported document limits. Missing owner coupling, schema/grants, scan evidence or physical-store proof blocks the command. Scanner/network work must already be complete outside the business transaction.

### 8. Workflow

Upload/scan through existing Core → select exact stored versions → acquire the agreed owner lifecycle fence before the SERIALIZABLE snapshot → enter the existing transaction and source/event/case lock order → authorize current record/worker and load exact owned document/version/scan rows → validate expected revisions/hash and Available state → record review uses with O4/Core case and original receipt → commit together. O5 revalidates the required current conditions on that same pack and adds publication uses with final Core outcome/publication/audit/outbox/receipt in one transaction. Finalization never substitutes whichever file is newest. Returning/replacing a pack requires explicit new revision/review; historical references remain.

### 9. Status / State Transitions

Existing version states are Quarantined → Scanning → Available / Rejected / ScanFailed; bounded failed/expired-lease retries are already defined by Core. Document retirement is distinct from version scan state. A use is not a scan or approval status. Proposed use creation is immutable; any later release requires separately attributable evidence and a defined authorized retention policy. Default behavior is retain while uses exist, with no automatic release on return/cancel/expiry. Final use lifecycle/physical schema is still to be designed.

### 10. Permissions & Data Scope

Reuse exact canonical tenant/entity and owning record/site checks. A reviewer may download only versions pinned to a review currently accessible to that assignment; this does not grant owner-wide listing/history, upload, invoice access or another publication's files. Core `IDocumentOwnerAccess.RequireAsync(owner, action)` has only Read/Upload and no version argument: an exact-version purpose route or restricted records closure must therefore validate the permitted version/hash before shared download. This is a required extension, not an existing capability. Replay reauthorizes current original actor/action and retained target scope.

### 11. Business Rules

Only stored owned versions with matching clean scan evidence can become new uses. Client scan/review flags, AttachmentRef projections and URLs are not authority. O4 review and O5 publication have distinct immutable use identities, even when they reference identical bytes. Active uses prevent managed retirement/purge; replacement uploads never release old uses. A transaction-only lock cannot provide retention after commit. No new scan freshness period or business approval limit is invented.

### 12. Data Fields & Ownership

Proposed Core use evidence includes stable use identity; tenant/entity; owner module/type/record; consumer purpose/review or publication revision; document/version ID and version number; SHA-256/size/media type; scan-evidence ID, matching hash, clean verdict, engine/definitions and timestamps; actor/time; command/recovery reference; and consuming pack fingerprint. Document/version concurrency revisions are validation inputs, not mutable EF principal keys. Procurement retains immutable use references and its pack digest, not a second content master. Storage keys remain private. Model/key/constraint design is not authorized by this documentation change.

### 13. Screens & Data Views

Reuse existing RFQ preparation/detail, assigned review and document controls. Show the exact frozen filename/version and honest availability/blockers. Existing upload/retry UX remains Core-backed. No new navigation or parallel page design is required. Unsupported retirement/replacement actions must remain unavailable with a reason.

### 14. User Experience / Human Behaviour

Explain that submitted files are retained for the review and publication. Replacing the pack starts an explicit revision/review rather than silently changing a pending decision. Keep pending, denied, unavailable, stale, failure and uncertain-write states distinct. A lost response uses original-key recovery before any new command.

### 15. Notifications & Communications

Existing scan notifications remain separate from business approvals. Publication outbox evidence is an internal fact, not proof that a supplier received a file. Any future notification links require the same purpose/version authorization and must not attach quarantined or otherwise inaccessible content.

### 16. Documents & Attachments

Reuse immutable private blobs, existing type/size/count limits and scanner. Current RegisterUpload accepts only a new document/version number 1; there is no exposed append-version operation to promise here. New uploaded immutable versions can be selected explicitly. Retain bytes and metadata together while uses exist. Business retention durations and release authority remain unset/configurable; legal hold behavior must be explicitly integrated rather than inferred from the currently present field.

### 17. Exceptions & Edge Cases

Wrong owner/version/hash; retired/quarantined/rejected/missing blob; clean-state row without required scan evidence; separate/ambiguous document store; changed pack or assignment; incomplete O4/O5 transaction; concurrent retirement/finalization; duplicate key/body conflict; policy withdrawal; current actor offboarding; returned/rejected review; and historical publication download. Fail closed rather than invent a ready/retained result. Do not infer physical blob protection against operators bypassing managed storage APIs.

### 18. Integrations / APIs / Events / Sandbox

**Minimal proposed port:** a Core-owned `AcquireUsePinsAsync(owner, purposeReference, exactSelections, commandReference, cancellationToken)` returning typed authoritative pins; names/DTOs are provisional. Infrastructure is constructed with the actual owning connection/transaction, while the application port need not expose EF types. Extend the owner-access dispatcher for RFQ without replacing invoice behavior. Reuse the existing same-store check/enlisted DocumentDbContext pattern. Emit append-only use evidence atomically; do not invoke external scanning/delivery within the transaction. No production fake-ready provider.

### 19. Audit / Security / Privacy

Audit pin creation, denied retirement/purge, any separately authorized release, and exact-version downloads. Persist immutable purpose/version/actor evidence without content, storage paths or secrets in logs. Core must enforce retained uses at managed lifecycle write boundaries, including direct context mutation guards where supported. A new lifecycle writer must not bypass the fence/reference check. Raw administrative DB/filesystem operations require operational controls; application locks alone cannot cover them.

### 20. Reports / Dashboards / Metrics

Scoped retained-use counts, blocked lifecycle operations, missing-byte incidents, failed pin commands and reconciliation of consumer references to Core uses. No file content or unrelated owner history in metrics. Definitions and operational thresholds remain configuration decisions.

### 21. Configuration & Localization

Same-physical-store-only gate, supported owner/purpose allowlist, bounded selections, file limits, current scanner configuration, retention policy reference and explicit release/lifecycle authority. Durations remain unset pending business approval. No assumed automatic disposal. Reuse shared localization and plain language; do not recreate organization or worker settings.

### 22. Non-Functional Requirements

Bound inputs and queries; deterministic owner/version lock order; cancellation and cleanup; no network scan under locks. Couple lifecycle writers before the SERIALIZABLE snapshot and retain durable uses after commit. Factor the existing owner advisory key algorithm rather than duplicate incompatible keys. Current upload xact locking is not a retirement fence. Read-only checks, optimistic tokens alone and row KEY SHARE do not establish retirement exclusion. Choose and verify the actual lock protocol with all managed writers before implementation.

### 23. Postconditions

On successful O4/O5, exact use evidence and consuming workflow outcomes/receipt commit together, with no copied blob or partial approval/publication. The selected bytes remain subject to managed retention protection. Failed commands introduce no use, vote, publication or invitation side effects. Existing legacy document consumers remain unchanged.

### 24. Failure & Recovery

Reuse transaction retries only for confirmed aborts and durable original-key recovery for uncertain commits; do not blindly repeat an unknown commit. Reconcile exact stored response and use identities without regenerating pins from current files. Restore metadata, retained references and blobs consistently. Default retain while uses exist; a missing blob blocks download and raises support evidence, never manufactures a replacement. Down/release safety must protect used evidence and be rehearsed after a real model exists.

### 25. Acceptance Criteria

O4 pins the exact reviewed pack; O5 adds its own immutable references to those same versions. A shared transaction rollback leaves neither Core case/use nor Procurement outcome/receipt. Managed retirement/purge cannot remove pinned evidence before or after commit. Assigned users can download only exact authorized pins. Quarantine, rejection, owner/hash mismatch and separate-store ambiguity block new uses. Original-key replay does not duplicate uses. No capability is enabled by the specification alone.

### 26. Test Scenarios

Real PostgreSQL tests must cover both lifecycle-race orderings; snapshot/fence acquisition ordering; transaction failure after each save; wrong tenant/entity/site/owner/version/hash; missing/failed scan evidence; exact-purpose download denial; two purposes retaining one version; historical reviewer offboarding vs current consumer authorization; unknown commit and GET recovery; cancellation cleanup/pool reuse; and separate connection rejection. Test lifecycle entry points and context guards, not only the publication caller. Existing upload/scan/download tests must remain green. Browser tests reuse existing pages; mocks do not replace actual retained-state proof.

### 27. Ownership / Dependencies

Core owns the pin/reference contract, metadata writer coupling and private content retention. Procurement owns publication/review purpose, business revision/pack fingerprint and current source/approval rules. Operations owns durable blob storage, backup/restore and privileged maintenance. Depends on source-reviewed O4/O5 state/authority design and actual Core documents readiness, not merely foundation schema connectivity.

### 28. Open Decisions / Assumptions

Approve owner type and explicit actions; exact purpose/version access closure; Core use model and append-only release evidence; before-BEGIN fence/row-lock protocol covering all managed writers; policy for pending/rejected/returned/cancelled reviews; scanner freshness at new use; pin bounds; retention release authority/durations; required pack categories; and operational byte-retention/restore procedure. Choose whether pinned documents remain non-retirable or gain an explicit retained-history download contract; do not silently bypass today's retirement denial. Cross-store support remains unavailable. This document authorizes no schema or application activation.

### Source audit anchors (6ec30de)

- `backend/core-api/src/Mightyfin.Erp.Core.Documents.Application/RecordDocuments.cs:6–27`: owner/action/records APIs; no use-pin or exact-version access action. Upload/scan/download lifecycle remains in the same service.
- `backend/core-api/src/Mightyfin.Erp.Core.Documents.Infrastructure/EfDocumentRecords.cs:13–25`: registration begins its own transaction and takes an owner upload xact lock; it must not be naively nested in publication. Find/List are unlocked reads; SaveState supports scan/download only.
- `backend/core-api/src/Mightyfin.Erp.Core.Documents.Domain/DocumentRecords.cs:57–65`: Retire currently checks scope and marks retirement; no use callback/hold check. Version scan evidence and immutable metadata are existing reusable facts.
- `backend/core-api/src/Mightyfin.Erp.Core.Documents.Infrastructure/DocumentDbContext.cs:93–112`: deletes/evidence mutation denied; retirement fields remain mutable without use-reference coupling. LegalHold has no implemented management workflow here.
- `backend/core-api/src/Mightyfin.Erp.Core.Documents.Application/IDocumentBlobStore.cs:14–20`: stage/store/private-read only; no committed purge/retention callback. PrivateDocumentStore does not overwrite committed content.
- `backend/hrm-api/src/Mightyfin.Erp.Hrm.Api/ProcurementInvoiceDocumentRoutes.cs:24–38,44–70`: invoice-only access and separately configured document context; preserve existing behavior when introducing an owner dispatcher.
- `backend/hrm-api/src/Mightyfin.Erp.Hrm.Api/ProcurementInvoiceMatchingCommands.cs:104–121`: proven composition pattern checks same store and enlists DocumentDbContext on the actual executing connection, but currently reads hashes without retention locks/pins.
- `backend/procurement-api/src/Mightyfin.Erp.Procurement.Infrastructure/Documents/InvoiceDocumentStoreBinding.cs:8`: conservative explicit host/port/database comparison; ambiguous aliases/multihost are not accepted.
- `backend/procurement-api/src/Mightyfin.Erp.Procurement.Infrastructure/Approvals/ProcurementApprovalTransaction.cs:64–99`: existing branch/catalog fences precede BEGIN, Procurement/CoreApproval share one connection/transaction, confirmed abort retry only; no document context/fence is currently included.


### U1 source contract checkpoint — not runtime implementation

The next isolated source slice adds `DocumentUsePin` and its consuming reference/expected-selection value contracts in Core Documents Domain, plus the proposed `IDocumentUsePinStore` Application port. Pin construction derives immutable content/owner/scan identity from supplied domain records and rejects scope, expected identity/revision/hash, availability and completed-clean-evidence mismatches. It does not establish that supplied records came from a database: the future owner adapter must load/authorize them under the agreed owning transaction. Exact duplicate comparison returns the original single pin; the store must additionally enforce exact whole-selection-set equality and current replay authority. Historical recovery reads retained evidence rather than recreating it through current availability.

Review and Publication uses have distinct stable keys and retain the same underlying version without copying bytes. No model mapping, migration, provider, routes, registration, retention enforcement, lock or publication activation is introduced. In particular, `CoreDocument.Retire` remains unchanged and **does not yet consult pins**. The future coupled lifecycle guard is an interface obligation, not a detached boolean check or an implemented protection. Defaults remain retain while uses exist; release/expiry policy is not fabricated.

`DocumentUsePinTests` authors pure positive/negative cases for exact content, separate purposes, duplicate identity/reason-free evidence, actor/reference changes, scope/owner/hash/CAS, quarantine/scanning/rejected/failed/retired content, scan binding and invalid source identity. These tests have not been built or executed in this slice; actual persistence, retirement races and same-store rollback remain later gates.
