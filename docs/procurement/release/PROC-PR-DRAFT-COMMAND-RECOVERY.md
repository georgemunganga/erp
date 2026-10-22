# Purchase request draft command recovery

**Status:** DISCOVERY ONLY — proposed contracts, not an implemented feature or release approval.
**Feature:** `PROC-02-RECOVERY` · **Version:** 0.2 · **Prepared:** 22 October 2026
**Hierarchy:** Procurement → Purchase Requisitions → Draft command recovery → Create / Update / Submit / Read original result.
**Baseline ERP:** `1917405282cbefb773386eeadc930349f478ef0c`.
**Baseline UI:** `30e17e9aad1bf86d3c94086b42fca590c9b0f05f` (published PR55/56).
**Standard:** [GW-ENG-STD-001](../../engineering/GW-ENG-STD-001.md), ERP **Tier 3**; retain the [28-section feature framework](../PROC-02-PURCHASE-REQUISITION-AND-LINE-ROUTING.md).

This document authorises no schema change, API registration, flag activation or deployment. The user has authorised ordinary Procurement CRUD work; its implementation does **not** require spending thresholds, supplier activation or a new business approval. Engineering contracts and verification below must be completed before this recovery capability is described as ready. P25 approval execution remains a separate source/release dependency and is not activated by this proposal.

## 1. Feature Identification

| Attribute | Definition |
| --- | --- |
| Child feature | Recover the original outcome of an owned PR Create, Update or Submit command |
| Operations | `Create`, `Update`, `Submit`, explicit read-only acknowledgement |
| Business owner | Procurement product owner; name to be recorded |
| Engineering owner | Assigned ERP backend/UI maintainers; reviewer distinct from implementer for consequential controls |
| Classification | Tier 3: consequential buying records, tenant boundaries, confidential request text |
| Delivery status | Discovery specification only; Technical decisions proposed in §28; Definition of Ready not yet met |

## 2. Purpose and Business Outcome

Prevent a lost HTTP response, reload or repeated click from creating a second request or repeating a consequential mutation. An authorised user can distinguish a **recorded original result** from the current record without relying on email, similar text, current state or a guessed identifier.

Success means one committed business result, its audit and its command receipt; explicit retries/acknowledgements produce no additional business effects. This is application idempotency and recovery, not a claim of exactly-once network delivery.

## 3. Scope and Exclusions

In scope: current owned-request draft creation/update, submission into policy/budget review, exact command replay, actor-owned historical acknowledgement, a thin browser write guard and existing-screen recovery controls.

Excluded: approval Open/Decide contracts; withdraw/return-for-changes; RFQ/PO/receiving execution; Finance reservations, budgets, accounting, AP and payments; background resubmission; automatic supplier/catalog substitution; new identity/master data; an additional workflow or notification engine. Existing P25 Open/Decide acknowledgement and UI locator work is related, not a substitute for these three missing command receipts.

## 4. Parent and Related Features

Parent: [PROC-02 requisition and line routing](../PROC-02-PURCHASE-REQUISITION-AND-LINE-ROUTING.md). Related: [Core identity and optional employee ADR](../ADR-003-CORE-USER-AUTHORIZATION-AND-OPTIONAL-EMPLOYEE.md), [approval/budget boundary](../PROC-03-APPROVAL-AND-BUDGET-CONTROL.md), exact request draft projection, catalog fencing and P25 principal provenance.

Source audit at the pinned baselines:

| Existing source | Observed contract / consequence |
| --- | --- |
| [PR routes, lines 20–29, 66–173](https://github.com/georgemunganga/erp/blob/1917405282cbefb773386eeadc930349f478ef0c/backend/hrm-api/src/Mightyfin.Erp.Hrm.Api/ProcurementRequestRoutes.cs#L66) | Create generates request/line IDs; Update uses version concurrency. Neither consumes a command UUID or stores a CRUD receipt. |
| Same file, `Submit`, lines 229–303 | Serializable transaction, request lock/version, policy assessment and submission audit exist; response loss is not acknowledged by command ID. Reuse this behaviour. |
| Same file, `ToDetail`, lines 495–524 | Actual DTO includes requester principal/nullable worker, BranchRef/DepartmentRef, exact line amounts and RequestEditVersion=1. It does not echo tenant/legal-entity IDs. |
| [UI request adapter, lines 232–250](https://github.com/georgemunganga/module-connect/blob/30e17e9aad1bf86d3c94086b42fca590c9b0f05f/src/platform/procurement-requests-api.ts#L232) | Update checks ID/+1; Create/Submit need stronger owner/state/result binding. No command receipt API for these operations. |
| [UI request screens, lines 163–173, 224–243, 340–347](https://github.com/georgemunganga/module-connect/blob/30e17e9aad1bf86d3c94086b42fca590c9b0f05f/reference-ui/src/features/LiveRequests.tsx#L163) | Save protection is component memory; reload/context resets it. Submit lacks a durable intent; 408/429/499 are not classified as unknown by the existing save catch. |
| [Merged PR approval journal](https://github.com/georgemunganga/module-connect/blob/30e17e9aad1bf86d3c94086b42fca590c9b0f05f/reference-ui/src/lib/request-approval-journal.ts) | Trusted scope, browser-tab nonce, storage readback and exact-locator confirmation are reusable concepts. Its Open/Decide union/GET/result shapes must remain separate. |

### Existing receipt and helper reuse assessment

| Existing implementation | Reuse | Preserve its domain boundary |
| --- | --- | --- |
| `ProcurementCommercialCommandReceipt`, `PurchaseOrderCommercialEvidence.cs:98`; `ProcurementOrderExecutionSupport.cs:124–141` | Immutable operation/key/actor/fingerprint/original response pattern | Requires PurchaseOrderId; never create a placeholder PO for PR recovery. |
| `SourcingCommandReceipt`, `SourcingDraftPersistence.cs:53`; `ProcurementSourcingExecutionRoutes.cs:124–148` | Scoped unique command key, recorded response status/version/digest and replay validation | Requires sourcing event and legacy non-null worker evidence. Do not copy its employee requirement into PR CRUD. |
| `InvoiceUnitReviewCommandReceipt`, `InvoiceUnitReviewRecords.cs:49`; `InvoiceUnitReviewMapping.cs:47–58` | Append-only guard, scoped FK/unique key, size/hash/version validation, optional worker metadata | Requires review source; its RequestJson/document workflow is unnecessary for PR recovery. |
| `ProcurementApprovalCommandReceipt`, `PurchaseRequestApprovalExecutionEvidence.cs:4`; `ProcurementApprovalExecutionMapping.cs:16–25,47–81` | Existing PR-scoped receipt/replay, actor and immutable-result validation concepts | Requires a real Core approval case/optional assignment. Draft CRUD must not create a fake case or overload Open/Decide operations. |
| `ProcurementEvidenceEncoding.cs:10–34` | Existing canonical JSON digest (duplicate-key/precision checks), invariant decimal encoding, SHA256, PostgreSQL microsecond timestamp handling | Reuse encoding; define this command's inputs/version explicitly, not a second hashing algorithm. |
| `ProcurementApprovalTransaction.cs:31–65`; existing catalog/branch fences and request locks | Same physical transaction, fresh-context rollback discipline, retry only confirmed serialization/deadlock aborts | Ordinary PR commands need the Procurement store and current Core reads, not Core approval DML or a Finance-enabled execution connection. Reuse/extract only the necessary transaction plumbing. |

Additional source findings that constrain this proposal:

- `ProcurementCurrencyDefaults.cs:8–20` is **Create-only**. `ProcurementRequestRoutes.cs:128` retains the stored currency when Update omits it. A new recovery contract must preserve this difference.
- `ProcurementRequestRoutes.cs:556–576` defines the complete accepted request/line DTOs. `catalogPublishedVersion` is returned/derived by `ApplyLine:474–476`, **not** an accepted line input. Do not add it to the mutable contract.
- `PostgresSessionAdvisoryLease.cs:15–59` already supplies pinned-session acquisition, cancellation cleanup and verified release. `ProcurementCatalogAuthorityFence.cs:10–22` requires acquisition before transaction/snapshot; `CoreBranchAuthorizationFence.cs:20–28` has the existing preceding tenant-wide branch fence. Reuse these mechanics and their order.
- `SpendingAuthorityFamilyTransaction.cs:22–31` shows SHA256/first signed big-endian Int64 advisory-key derivation, but acquires its transaction lock after BEGIN. Reuse the derivation pattern, **not** this configuration-family adapter for PR commands.
- `ProcurementCatalogFencing.cs:20–40` currently acquires the catalog lease in an endpoint filter. The keyed path must integrate its outer branch lease there or replace that filter with one owning ordered adapter; it must not acquire branch after catalog or nest another connection/transaction.
- `ProcurementRequestRoutes.cs:536–548` owns principal, selected-branch/confinement and optional department filtering. Recovery must preserve it; employee status is not a recovery permission. `ProcurementContextRoutes.cs:50–75` already exposes the verified nullable `orgUnit` alongside `branch`; the new thin readiness projection must retain both IDs/null explicitly instead of deriving a department from HR or an untrusted saved value.

A narrowly owned PR draft receipt record is proposed because every existing family has an incompatible required domain target. It is **not** a new identity system, universal receipt service or workflow engine. The proposed names are `PurchaseRequestDraftCommandReceipt` and `procurement.purchase_request_draft_command_receipts`; they remain specification contracts until implemented. No migration has been generated.

## 5. Actors and Participants

- Current active Core system user with the named action and record scope. HR employee linkage is optional metadata, never login, CRUD ownership or recovery authority.
- Existing Core identity/permissions/branch service: authenticates and revalidates current account/grants at each use.
- Procurement application: owns PR validation, state, receipt and audit.
- Support/auditor: scoped read-only investigation; no implicit ability to replay or acknowledge another actor's command.
- Finance is not a participant in plain draft recovery. Submission records demand/review prerequisites; it does not reserve money.

## 6. Entry Points and Triggers

Existing New request, Edit request and Send for review controls. Recovery starts only from an explicit **Check saved result** action after an unconfirmed command or reload. In-memory retry, if offered, reuses the exact frozen original key and payload; reload stores only a locator and therefore uses GET only.

Do not retry on mount, focus, reconnect, timer, notification, list search or configuration change.

## 7. Preconditions

Authenticated current Core principal; actual tenant, selected company and server-effective branch; current action permission and own-record scope; valid exact input; writable supported schema/finite runtime role; verified storage claim before browser mutation. Update/Submit require a loaded original request and expected business version.

Missing effective branch is unknown, not company-wide null. Company currency defaults come from actual organisation settings. No employee, supplier activation, purchase approval, Finance policy or accounting integration is a prerequisite for ordinary draft CRUD.

## 8. Workflow

1. Validate local fields before claiming; freeze the exact accepted command and original context. Generate one nonzero UUID command key for this logical attempt.
2. Persist/read back a scoped metadata locator before POST/PUT. Failure blocks transmission. No request body/reason is stored in the browser journal.
3. Server validates a single canonical UUID header, operation/input contract, current Core principal/permission and tenant/company/effective scope.
4. Compute the versioned immutable fingerprint using existing canonical encoding. Look up the scoped operation/key **before** rerunning mutable-state/CAS/catalog checks for a previously completed command.
5. Existing matching receipt: revalidate current actor permission and original scope; same fingerprint/target returns the original status/body without new business work. Different fingerprint/target is conflict; wrong actor/scope reveals no receipt.
6. No receipt: lock/reload the owned target for Update/Submit, apply existing rules/catalog fencing and expected version. Create allocates a new real request and line IDs, never a placeholder.
7. Persist business changes, existing policy effects where applicable, exactly one consequential audit and the immutable original-result receipt in **one** Procurement transaction. If multiple SaveChanges calls are needed to obtain final versions, they remain inside that same uncommitted transaction.
8. Commit before responding. Failed commit or unknown transport is not reported as success. Unknown commit/network outcomes are not automatically retried.
9. Client clears only its exact locator after a strictly bound successful result in the original current context. After reload, explicit GET validates the original acknowledgement, clears that locator, then reads current PR state separately. A historical Draft result never becomes a current editable draft.

Concurrent identical keys must resolve to one committed result. The unique constraint is authoritative; after a unique-key race, roll back/dispose the losing transaction and read the winner's matching receipt on a fresh context, or return a retained conflict for explicit GET. Do not continue an aborted PostgreSQL transaction, retry arbitrary failures, or execute the command with a new key. The proposed lock contract is fixed below; no distributed lock/queue is required.

### Proposed transaction and lock contract

Use a narrowly named `PurchaseRequestDraftCommandTransaction` adapter around the existing physical Procurement connection, shared lease and request-lock helpers, not the Finance/Core-approval execution coordinator. For keyed commands acquire in this order: **Core branch shared session lease → Procurement catalog shared session lease → exclusive command session lease → BEGIN Serializable → receipt lookup → owned request row lock for Update/Submit → business work**. Revalidate current account/action/scope after leases and before business work; the branch fence does not claim to fence account/role writers. Do not hold this transaction across network delivery or external services.

Derive the command lease key from UTF8 bytes of `greenwebb.procurement.pr-draft-command.v1\0{tenant}\0{company:D}\0{operation}\0{key:D}`, SHA256 and `BinaryPrimitives.ReadInt64BigEndian` of the digest. Exclude actor, branch and target because the unique index excludes them: all contenders for the same database key must serialize. Hash collisions only serialize unrelated commands; they do not authorise access. Bound lease waits using request cancellation/timeout, never steal a lease.

Acquire the command session lease **before BEGIN**, using `PostgresSessionAdvisoryLease`, so a waiter does not create a serializable snapshot before the winner commits. Release in reverse order after commit/rollback, on the same physical session; preserve the helper's clear-pool/close handling when acquisition/release is uncertain. The existing endpoint catalog filter must be integrated once into this ordering, not nested beneath it. All commands lock at most one PR; existing approval/Finance writers keep their request-first domain lock order and do not acquire this command lease.

Use the scoped unique index as the final safeguard, including clients/providers that do not participate in advisory locking. A uniqueness failure is not a successful replay: roll back, dispose the transaction and inspect the exact scoped receipt on a fresh context. Only a validated same-actor/fingerprint receipt returns history; otherwise retain a conflict/unknown result. Reattempt a whole transaction only after confirmed serialization/deadlock abort (maximum three, fresh contexts), never on uncertain commit/connection failure. Actual PostgreSQL parallel tests must verify this adapter; a nonrelational fixture cannot certify it.

## 9. Status and State Transitions

| Dimension | From → To | Guard / meaning |
| --- | --- | --- |
| PR Create | absent → Draft, version 1 | Valid own Core account, category/catalog/currency/reference checks |
| PR Update | Draft → Draft; ReturnedForChanges → ReturnedForChanges | Own target, expected version; +1 even unchanged or line-only update |
| PR Submit | Draft/ReturnedForChanges → Submitted | Existing needed-by/line/reference/previous-case guards; +1; PendingPolicyAndBudget |
| Receipt | absent → Completed | Same atomic commit as the business/audit effect; thereafter immutable |
| Browser | clear → claimed/in flight → confirmed | Exact result releases only the matching marker |
| Browser | in flight → unconfirmed → GET acknowledgement | Reload/proxy failure preserves block; negative GET does not imply absence |

No new public PR state such as Paid, Approved or Failed is introduced. Unconfirmed describes client knowledge, not a server business status. No durable Processing receipt is proposed; absent cannot prove that an overlapping transaction will never commit.

## 10. Permissions and Data Scope

| Operation | Existing current permission | Required record boundary |
| --- | --- | --- |
| Create / recover Create | `procurement-request-create` plus module access | Original actor, actual tenant/company and pinned original branch/department scope |
| Update / recover Update | `procurement-request-edit` plus module access | Original actor and owned request in current/original authorised scope |
| Submit / recover Submit | `procurement-request-submit` plus module access | Same own-record and original-scope rules |

A valid session or ERP-admin label alone grants nothing. GET acknowledgement requires the original operation permission; it must not depend on current mutability, spending limits, current approval execution or future Finance availability. It does require actual read-store/schema readiness and current record-scope authorisation. Revoke a grant or branch reach and access is denied even with a saved locator. Cross-actor UUID discovery returns no private result. Browser nonce is only a partition, not server authentication proof.

## 11. Business Rules

- **PRR-01:** one unique `(tenant, legal entity, operation, command key)`; exact actor, target and immutable fingerprint binding. UUID collisions under another actor do not confer access.
- **PRR-02:** fingerprint includes schema version, operation, canonical actor/scope, target (where known), expected version and every accepted mutable header/line field. Preserve meaningful line order and null distinctions. Never hash rounded JavaScript numbers.
- **PRR-03:** canonicalise with existing input parsing/Clean/Trim/decimal rules; reject duplicate/unsupported input fields according to a reviewed allowlist. Request status, ownership, counters or authority cannot be supplied as mutable inputs.
- **PRR-04:** unspecified Create currency is a stable **organisation-default intent** in the fingerprint; unspecified Update currency is **retain stored request currency** intent. The first accepted currency is pinned in the recorded result. Replay must not resolve a newly changed default and silently create a different result or false conflict.
- **PRR-05:** replay/GET returns recorded history after current actor/scope checks. It does not rerun current catalog prices or policy to recreate a result. A new logical command uses current rules normally.
- **PRR-06:** Update/Submit CAS and existing state/line guards remain authoritative. Receipt replay is checked before rejecting a legitimately completed original command because its record has since advanced.
- **PRR-07:** repeated command/GET adds no duplicate PR/line/policy evaluation/business audit/outbox effect. Operational access logs are distinct and must not be described as business mutations.
- **PRR-08:** no error, matching current values, list entry, history entry, GET404 or operator checkbox releases an unknown marker. A reviewed first-attempt pre-write validation response may release only that attempt if explicitly specified; status-only 4xx handling is insufficient.
- **PRR-09:** no fabricated worker, Finance reservation, approval case, supplier activation or ledger entry.

## 12. Data Fields and Ownership

Preserve the existing draft inputs: purpose, currencyCode, neededBy, costCenterRef, projectRef, deliveryLocationRef, deliveryAddressText, fundingSourceRef, notesToApprover, and lines. Lines retain id, description, unitCode, requestedQuantity, estimatedUnitPrice, catalogItemId/catalogEntryId, preferredSupplierId, categoryCode, purchaseType, neededBy, discountPercent, taxCodeRef, costCenterRef, projectRef and specificationDocumentRefId. Update adds expected `version`; Submit retains its version-only body.

Preserve actual returned `requesterPrincipalId`, nullable `requesterWorkerId`, `branchRef`, `departmentRef`, `requestEditVersion:1`, all documented lifecycle fields and exactAmounts strings. A company-wide current scope may legitimately open an owned request whose original BranchRef is non-null; Update/Submit preserve and validate the loaded original branch, not an invented null. The actual DTO does not echo tenant/company; use authoritative request context rather than requiring fabricated fields.

Proposed domain class **`PurchaseRequestDraftCommandReceipt : ProcurementRecord`**, mapped only in Procurement to **`purchase_request_draft_command_receipts`**. Proposed scoped unique index: **`ux_pr_draft_receipt_command`** on `(tenant_id, legal_entity_id, operation, command_key)`; scoped real-request FK on `(tenant_id, legal_entity_id, purchase_request_id)`. Actor is deliberately not part of uniqueness, so a foreign actor cannot reuse a guessed key to fall through to a new command. Do not reuse an unrelated target-bound receipt table.

Proposed Procurement-owned receipt minimum:

| Field | Constraint / purpose |
| --- | --- |
| Standard scoped record ID, tenant/legal entity, created attribution/version | Reuse ProcurementRecord and scoped FK conventions |
| Operation / CommandKey | Create/Update/Submit; nonzero UUID; scoped unique key |
| ActorPrincipalId | Actual current canonical Core ID; immutable original actor |
| PurchaseRequestId | Nonzero real target; Create assigns it in the owning transaction |
| ContextBranchRef / ContextDepartmentRef | Nullable effective branch/department at command acceptance; unknown is never encoded as null |
| OriginalBranchRef / OriginalDepartmentRef | Actual request provenance after loading/creation, immutable; not master copies or employee requirements |
| FingerprintSchemaVersion / CommandFingerprint | Version 1; deterministic lowercase SHA256 of accepted command intent |
| ResponseSchemaVersion / ResponseStatusCode / ResultVersion | Version 1, original 201/200, positive persisted version |
| ResponseJson / ResponseSha256 / CompletedAt | Bounded exact original response, digest and normalised UTC completion time |

No full request body is necessary in a receipt if the canonical immutable fingerprint and exact original response provide the required proof. ResponseJson may itself contain confidential request notes; restrict and retain it accordingly. No duplicate user/employee/company/catalog masters. Worker identity, if present in the historical result, remains optional original metadata and is not a new required receipt authority field.

Browser marker: only bounded scope, browser-tab nonce, operation, local command UUID, known request ID or null for Create, expected version where applicable, and local fingerprint. This is a locator, never a server completion claim.

## 13. Screens and Data Views

Reuse New request, Edit request, request detail/Send for review and the existing shell recovery slot/shared Panel/Button/status/link controls. No new menu, page layout, table or design system.

States: initial/empty form, loading, validation error, saving, confirmed, unconfirmed/blocking, recovery unavailable/denied, original result found and current-record refresh. Loading/permission denial never exposes demo records. Show a short command reference for support; hide another actor's markers. A recorded original result is clearly historical and does not grant current edit/submit actions.

## 14. User Experience and Human Behaviour

Disable duplicate mutations immediately. Keep the existing exact draft values in memory while the original tab is active. Explain: “The save result was not confirmed. Check the saved result before sending again.” Never say “not saved” after an unknown outcome.

In-memory exact retry is explicit and uses the original key/body; changed inputs are not allowed on that attempt. Reload offers GET only because the browser did not retain a request body. Recovery controls mount inside existing AppShell content, respect sidebar/top navigation and preloader, remain available for own history when mutation capability is off, and use normal keyboard/pointer interaction. No forced clicks or fixed sleeps in acceptance tests.

## 15. Notifications and Communications

No new notification delivery is needed. Existing request-submission notifications/events, if genuinely implemented and enabled, remain transaction-owned and deduplicated. Neither replay nor acknowledgement enqueues another event. Procurement outbox delivery remains a separate controlled capability; do not infer SMTP or external delivery from a row or log message.

## 16. Documents and Attachments

Retain existing specificationDocumentRefId references and their validation. Recovery neither copies a file nor bypasses Core document access, retention or scanning. Browser storage contains no attachment content. No new upload or scanner prerequisite is introduced for a draft that does not require a document under its existing rules.

## 17. Exceptions and Edge Cases

Same UUID/same intent concurrently; changed payload/version/target under the same key; case/zero/multiple UUID headers; malformed or duplicate JSON; decimal overflow; line reordering; changed default currency; archived references after original completion; current account/permission/branch revocation; independent Core accounts linked to the same employee; no employee; branch/company switch during hashing or response; browser storage denied/corrupt; timeout after actual commit; stale receipt digest/result; current request advanced or cancelled; source FK missing; retained receipt with unavailable schema; late responses after resource navigation; browser/session cleanup.

All must retain identity/scope and historical attribution without manufacturing aliases, resetting an unknown marker, or using “All records” as a fallback.

## 18. Integrations, APIs, Events and Sandbox

**Proposed interface, not registered:** preserve existing mutating routes and bodies; add one required `Idempotency-Key` header when the new command capability is enabled:

| Route | Operation / original success |
| --- | --- |
| POST `/api/procurement/v1/requests` | Create / 201 + exact original RequestDetail |
| PUT `/api/procurement/v1/requests/{id}` | Update / 200 + exact original RequestDetail |
| POST `/api/procurement/v1/requests/{id}/submit` | Submit / 200 + exact original RequestDetail |
| GET `/api/procurement/v1/request-draft-commands/{operation}/{key}` | Actor-owned acknowledgement, no business effects |

The GET route cannot require a request UUID for Create: a lost creation response means the client may not know it. Update/Submit locators contain the expected target and must validate it against the returned result.

Proposed typed GET DTO **`PurchaseRequestDraftCommandAcknowledgement`**, with exactly these version-1 fields: `acknowledgementSchemaVersion:1`, `acknowledgement:"Completed"`, `operation`, `commandKey`, `actorPrincipalId`, `requestId`, `contextBranchRef`, `contextDepartmentRef`, `originalBranchRef`, `originalDepartmentRef`, `commandFingerprint`, `completedAt`, `originalStatusCode`, `responseSchemaVersion:1`, `responseSha256`, `originalResultJson`. The final field is the exact stored JSON **string**, not a reserialized object: verify its UTF8 SHA256 before parsing it as the existing RequestDetail. Validate own principal, known target, expected/result versions, original provenance, exact amounts and every envelope link. Tenant/company come from the authenticated scoped request; do not invent their absence in RequestDetail. Do not copy the eight/eleven-field approval acknowledgement into this DTO.

Return ordinary mutation successes with their existing RequestDetail body/status. Read back final persisted request/line versions and PostgreSQL-normalised timestamps without another mutation, serialize that original result once, store those exact UTF8 JSON bytes and return them unchanged on first success/replay. Echo `Idempotency-Key`, `X-Procurement-Command-Fingerprint` and `X-Procurement-Response-Sha256` response headers; the thin client verifies key, digest/raw body and strict typed result before clearing. Replayed Create preserves the original 201 and Location pointing to its real request. GET is 200 with the acknowledgement envelope, not another 201 creation. These are proposed additive contracts, not current response headers.

Proposed read/write availability is separate: historical GET requires the verified owning read schema/current operation permission, not a currently writable command capability. Default unavailable/readiness false until actual schema/finite-role proof. A keyed request must never silently fall back to unkeyed mutation when the new store is unavailable. The activation decision is key-required for all three operations: a missing header returns `400 command-key-required` before business work once activated; old clients must refresh rather than write without recovery. Legacy routes retain their current behaviour while the successor is off and must not be advertised as idempotent.

Sandbox: reuse existing private mapped-HTTP/PostgreSQL fixture and normal UI transport; label synthetic outer authentication explicitly. No public TCP, production credentials, fabricated employee or Finance effects. No API/schema in this table exists merely because it is specified.

## 19. Audit, Security and Privacy

Record one procurement audit per successful logical command, atomically with business state/receipt. Preserve the existing submission audit/policy snapshot; add command metadata to the new versioned event rather than a second submission audit. Draft Create/Update need explicit consequential audit evidence if currently absent. Replays and GET do not rewrite that audit. Keep command UUID, action, actor, target, prior/result versions and correlation; no credential/body dump in technical logs.

Receipts are append-only to normal application roles: the finite runtime role receives SELECT/INSERT on this new receipt table, not UPDATE/DELETE/TRUNCATE or ownership; scoped request FK deletion is restrictive. Existing PR write grants do not imply receipt mutation grants. Actual database privileges and adversarial tests must prove this, not just EF guards. Restrict response notes/specification references and exports. Current actor/tenant/company/original-branch scope is checked server-side for direct URLs. Owner/admin test execution is not finite-role authentication evidence. Retention/backup/legal hold must preserve recovery promises; never prune receipts while automatic same-key replay is still promised.

Tier-3 threat cases include cross-tenant UUID lookup, actor spoofing, key collision, stale grants, result tampering, browser storage mutation and duplicate financial/operational effects. Independent review is required before release.

## 20. Reports, Dashboards and Metrics

No new dashboard is required. Operational counters may expose scoped completed commands, conflicts, blocked recovery, lookup failures and age to support staff. Distinguish transport attempts from committed logical commands; GETs/replays are not new PR volume or savings. Define time windows, owner, access and retention before publishing metrics. Do not report an unknown client result as failed procurement.

## 21. Configuration and Localization

Reuse Core organisation/company currency, branch selection, permissions, branding, fonts, controls and localisation. Existing procurement numbering remains server-owned. No hardcoded company/tenant/currency or spend thresholds.

Use UTC instants with explicit offset, ISO date-only request dates and invariant exact decimal strings. Display locale affects presentation only. New recovery read/write switches and limits are technical, default closed and versioned; schema constants are populated only from genuine reviewed migration/actual PostgreSQL capture. No control is made Ready through a Boolean alone.

## 22. Non-Functional Requirements

Apply Tier-3 recovery/tenant/accessibility requirements. Bound command/header/body/result sizes, query timeouts and retained browser metadata. Use scoped unique lookup indexes, deterministic parsing and response digests. Respect WCAG 2.2 AA interaction goals and existing responsive shell. No mandatory extra service, broker or cache for this slice.

Retries are bounded and only for confirmed transaction aborts using the existing PostgreSQL failure classification/fresh-context discipline. Unknown commit is never automatically rerun. Set actual performance and retention targets during DoR; do not invent throughput guarantees. Backups/restores, alert ownership and rollback/forward-recovery procedures must be reviewable before production activation.

## 23. Postconditions

Success: exactly one owned PR result/version/line set, its required audit/policy evidence and one immutable receipt, committed together. Exact replay returns the same original body/status and no second effects. Valid GET acknowledges the original command, then UI separately fetches current state. Rejection: no partial business/audit/receipt effects. Unknown transport: server may have committed; original locator remains blocked until a valid acknowledgement or a separately governed reconciliation.

## 24. Failure and Recovery Behaviour

- Storage unavailable or failed readback: no mutation leaves the browser.
- HTTP408/429/499, proxy/unrecognised status, network abort, malformed/wrong-actor/wrong-version result or 5xx: retain locator. No automatic retry/new UUID.
- GET401/403/404/409/503 or malformed result: retain locator. A read cannot prove that an in-flight command will never commit. Login/context refresh does not erase another scope's pending intent.
- Only an explicitly reviewed pre-write validation response for the first attempt may allow correction without reconciliation; exact code/origin and zero-write proof are required. A later rejection cannot disprove an earlier unknown completion.
- Until the proposed server receipt exists, the honest current behaviour is a durable block plus **View saved records / contact Procurement**. Ordinary GET/list/history or identical current fields cannot acknowledge a local UUID. No unsafe Reset button, “confirmed absent” claim or invented GET is permitted.
- Client guards do not prevent duplication in a new browser/tab, after deliberate storage deletion or through another API client. Server receipt uniqueness/transaction proof is required for the reliability guarantee.

Migration/recovery plan: after the accepted P25 baseline, assign a genuine successor migration centrally; no ID is reserved here. Add only the scoped receipt objects/constraints/indexes required by the approved design. Never backfill invented receipts for historical requests or alter original requester provenance. Stage code/readiness and UI, prove current read/finite writer grants, then enable keys only with compatible clients. Preserve old receipt bytes/digests and data/ACL baselines during upgrade.

Unused Down must restore the exact earlier schema/data/privileges. Once any completed receipt exists, a reviewed transactional pre-DDL SQLSTATE55000 veto must prevent destructive loss of its recovery promise; forward repair/compatible code is the recovery strategy. An old-image restart is unsafe if it silently reopens unkeyed writes against a live keyed contract. Test the actual rollback/forward plan, not just migration generation.

## 25. Acceptance Criteria

1. A permissioned current Core user without an employee creates/updates/submits using existing normal screens and organisation currency defaults.
2. Every committed command has one scoped immutable receipt and required business audit; fault injection at each save/commit boundary leaves all-or-nothing results.
3. Same-key identical replay returns the exact recorded status/body/version after current actor/scope checks; changed intent/target/version conflicts without effects.
4. Concurrent identical Create commands produce one request/line set; concurrent keys obey existing CAS/state rules for Update/Submit.
5. Real successful commit followed by lost response, reload and explicit GET completes recovery without another POST/PUT, audit, policy result or notification.
6. Cross-actor/tenant/company/branch access, current grant revocation and malformed/zero keys fail server-side with no private result or side effects.
7. Exact DTO fields, nullable worker metadata, original BranchRef/DepartmentRef, decimal/date values, IDs and +1 update/submit versions survive replay.
8. Unknown/error/current-record matches do not clear markers; storage failure prevents transmission; late results cannot update another record/context.
9. Read-only recovery works after later state/configuration changes without granting current mutation rights or enabling Finance/approval/PO/receiving execution.
10. Actual migrations, append-only/finite-role constraints, recovery and independent review are evidenced; source/UI presence alone does not satisfy completion.

## 26. Test Scenarios

| Gate | Required actual/automated evidence |
| --- | --- |
| Domain/input | Every accepted field participates in canonical intent; duplicate JSON, exact decimal/overflow/null/date/line-order/default-currency tests; original ownership/branch provenance |
| Database | Genuine Up/idempotent SQL, actual PG constraints/FKs/indexes; concurrent identical/different UUIDs; no-op/line-only +1; original data/history/sequence/ACL preservation; unused Down/re-Up and each used-receipt55000 veto |
| Transaction faults | Before business save, between business/audit/receipt persistence, before commit and actual commit-lost-response; no partially committed graph or repeated effects |
| Mapped HTTP | Current Core-only user plus exact grants/scope; same key/body, modified body/target/version, wrong actor/tenant/company/branch; current permission revocation; exact original ACK after current state advances |
| Finite DB role | Real finite-role connection/authentication and API pipeline; receipt UPDATE/DELETE/TRUNCATE, broad identity/Core DML, ownership/elevation and missing required grants rejected; owner probes labelled separately |
| UI unit | Shared storage claim/readback/exact-match clear, malformed/denied storage, trusted scope/nonce, strict real response/ACK parser, unknown status retention, local validation before claim |
| Controlled browser | Each Create/Update/Submit commit then response loss/reload; zero automatic retransmits; wrong/malformed/negative ACK retained; actor/company/branch/resource switches; actual pointer target after preloader; ordinary shared UI controls |
| Actual PG + browser | Existing private Unix mapped host, genuine migrated store, actual persisted record/audit/receipt counts and independent owner reload; no business-response mocking or forced clicks; synthetic outer auth clearly labelled |
| Release | Full relevant regression/model/build checks, current public asset hashes/health, normal authenticated smoke within authorised scope, independent review and retained failures |

Concrete fixture assertions for the proposed slice:

- Use a current Core-only maker (`workerId:null`) with finite stored Create/Edit/Submit actions and one authorised branch; a second Core-only actor with the same actions must not replay/GET the first actor's key. Test an account with company-wide scope reading its own non-null original BranchRef without rewriting that provenance.
- Create with currency omitted, commit once, change the real organisation default under its existing owner, then replay/GET: generated request/line IDs, original currency, original 201/Location/body bytes and digest stay identical. Update omitted currency retains the original request currency. Explicit different currency/body under the same UUID conflicts.
- Barrier-start two real physical PostgreSQL requests with one Create UUID; assert one PR, one line set, one receipt and one logical audit. Start two different UUIDs with the same Update version: exactly one accepted +1 result; no-op Update also advances exactly +1. Submit replay remains identical after actual current state/version advancement.
- At each persistence boundary inject a confirmed rollback; independently reload business, audit, policy, receipt, sequences and original data/ACL footprint. Separately drop the actual HTTP response **after** a real committed201/200; the browser reloads, invokes only actor-owned GET using its saved key (Create still has no known PR ID), then fetches current detail. Assert zero second mutation requests.
- Validate response headers/raw UTF8 digest and the exact acknowledgement originalResultJson before parsing. Wrong key/principal/target/version/scope, mutated result bytes, duplicate JSON, unknown fields and digest mismatch must keep the locator; GET404/403/409/503 and HTTP408/429/499 never reopen a form automatically.
- Verify effective context branch **and orgUnit** are retained from the real context projection; unknown/absent context is not null. Storage readback failure prevents transmission; a late result after route/account/company/branch/department/nonce change cannot clear the old marker or mutate the new screen.
- Exercise activated missing-header400, keyed-but-unavailable503, missing meta flag default-false, and successful historical GET after disabling new writes. Prove no implicit unkeyed fallback and no dependency on approval/Finance execution flags.

No test in this specification has been run for this proposed receipt family. Existing Open/Decide, recurring or owner-only proofs do not certify it.

## 27. Ownership and Dependencies

Procurement owns PR rules, receipt, replay/acknowledgement and business audit. Core owns actual identity/grants/organisation/branch fences and shared UI; HRMS employee linkage is optional. Shared encoding/transaction/storage mechanics are reused under their existing owners. Finance/AP/accounting/payments remain future Finance responsibilities, not implemented by this feature.

P25 source/release integration must preserve original Core-principal provenance and default-off approval/readiness guards. This recovery design does not turn an unaccepted P25 branch into a production prerequisite for ordinary current CRUD; the successor schema/API capability is planned against the centrally accepted baseline. Migration order, independent reviewer, support owner and exact scope must be assigned before implementation starts. Manus Core frontend build work is separate and untouched.

C01–C20 applicability, following the Tier-3 system definition:

| Capability | Assessment for this child feature |
| --- | --- |
| C01 Organisation/tenancy | Required: actual tenant/company/original branch and scoped keys |
| C02 Identity/authentication | Required: existing current Core principal/session, no new credentials |
| C03 Roles/permissions | Required: current original-operation grants, default deny |
| C04 Data scope/ownership | Required: exact actor/own record/original and current scope |
| C05 Domain entities/data | Required: PR plus narrowly owned immutable receipt |
| C06 Workflow | Required: reuse PR command orchestration; no second workflow engine |
| C07 States/transitions | Required: PR state and separate client-knowledge/receipt states |
| C08 Business rules | Required: canonical intent, CAS, current validation vs historical replay |
| C09 Approval/delegation | Conditional: existing approval locks respected; no new approval/authority for draft CRUD |
| C10 Audit/history | Required: one consequential audit, immutable recorded original result |
| C11 Events/notifications | Conditional: preserve existing real submission effects; no duplicate/new delivery |
| C12 Documents/media | Conditional: existing specification references; no new upload/scanner flow |
| C13 APIs/integrations | Required: explicit keyed command/GET contracts and failure semantics |
| C14 Configuration | Required: default-closed technical capability and compatible rollout |
| C15 Reporting | Conditional: support counters only, scoped definitions before publication |
| C16 UX | Required: existing controls/layout, honest loading/blocked/recovery states |
| C17 Security/privacy | Required: Tier-3 threat/tenant/receipt-body/retention review |
| C18 Reliability/operations | Required: atomicity, observability, backup, forward recovery |
| C19 Testing/quality | Required: actual PG/HTTP/browser/negative/recovery evidence |
| C20 Lifecycle/governance | Required: versioned spec/ADR/migration/release and independent review |

## 28. Technical Decisions, Open Decisions and Assumptions

### Eight proposed implementation decisions

These decisions resolve the original design alternatives using the pinned source. They are **specification decisions**, not implementation or readiness evidence.

| Decision | Concrete contract | Source basis / required verification |
| --- | --- | --- |
| D1 — ownership and successor migration | New `PurchaseRequestDraftCommandReceipt` / `purchase_request_draft_command_receipts`, fields/index/FK in §12; narrow genuine migration after the centrally accepted P25 baseline. No changes to old receipt families, no backfill, no reserved migration ID. Append-only receipt with original result; used Down veto before DDL. | Existing four families in §4 require unrelated targets/case/legacy worker. Actual migration/finite grants/Up/Down/data preservation remain unproved. |
| D2 — UUID and fingerprint | Exactly one `Idempotency-Key`, 36-character lowercase nonzero UUID D form; accept any UUID version satisfying that contract, no v4/v7-only restriction. Reject whitespace, comma/multiple header values and noncanonical case before writes. `PurchaseRequestDraftCommandIntentV1` builds an explicit typed logical input, then uses existing `ProcurementEvidenceEncoding.PolicyJsonDigest`, not raw-body bytes or a second hash engine. | Existing TryParseExact D validators and `ProcurementEvidenceEncoding`; exact rules below require field-by-field tests. |
| D3 — current/original identity and scope | Current Core principal and original-operation action required for command/replay/GET; worker optional. Receipt actor must match exactly. Request must remain owned by that principal and visible under existing branch/confinement/department filters. Pins include original request branch/department plus accepted effective context branch/department. Recovery requires the original effective context selected, still currently authorised; company-wide null is valid only when positively supplied/authorised by server context. No old employee linkage or role label grants authority. | Existing OwnRequests/ScopedRequests and current Core middleware. Wrong actor, company, original scope or selected context yields no receipt data; test both company-wide ownership of a branch-origin request and explicit branch selection. |
| D4 — default currency | Create omitted/null/whitespace currency hashes as `OrganizationDefault`; resolve actual selected organisation currency **only after no receipt is found**, persist it in the request/result. Update omission hashes as `RetainRequestCurrency` and retains the loaded request currency. Explicit currency trims/uppercases. Replay uses original receipt before any default/catalog re-resolution. | Actual `ProcurementCurrencyDefaults` and Update line128 differ. Settings changes after first committed acceptance cannot change replay; a confirmed aborted attempt has no pinned committed result and may read the new current default on its fresh attempt. |
| D5 — concurrency and atomicity | Ordered pre-BEGIN session leases and one Serializable Procurement transaction in §8; new adapter reuses shared lease, existing request lock and failure classification. Scoped unique key final authority. Business + consequential audit + exact result receipt commit atomically; unknown commit is never automatically retried. | Shared session lease/fence contracts; actual lock/wait/unique-race/fault tests, no nonrelational locking claim. |
| D6 — unknown Create ID acknowledgement | Actor-owned GET `/api/procurement/v1/request-draft-commands/{operation}/{key}`; exact operation is case-sensitive. No PR ID is needed in URL. Typed acknowledgement in §18 binds the actual generated ID and original result. GET404 never proves absence; no list/history acknowledgement. | Create allocates ID server-side. Current approval GET requires known request/case and cannot substitute. |
| D7 — compatible activation | Add optional meta capability `requestDraftCommandRecovery` (missing/false means unavailable), backed by actual separate read/write readiness. Historical GET checks read readiness/current original-operation permission even when new writes are disabled. Keyed requests while write-unavailable return503, never unkeyed fallback. Activated routes require keys, reject legacy missing-key writes400; stage compatible clients before activation and do not roll back to an image silently accepting unkeyed writes. | Existing old routes/bodies remain unchanged when successor off. Metadata is a projection of verified schema/configuration, not a switch that creates readiness. |
| D8 — thin client and release rules | Extract low-level claim/readback/exact-token confirmation from existing guards once; separate PR CRUD wrapper, preserve approval/recurring wrappers. Store locator/fingerprint/scope/tab nonce only, no body/reason. Before send claim/readback; same-tab frozen payload can explicitly retry the same UUID; reload GET-only. Clear only exact bound success/ACK in current context. For this first slice **all server errors retain** the marker; no status-only or presumed pre-write exception. | Existing journals provide mechanics, not an implemented CRUD receipt. Validation runs locally before claim; server-rejected/unknown attempts require separately governed reconciliation until a proven pre-write rejection contract is designed. This UX limitation is explicit, not an unsafe Reset. |

### Canonical input version 1

`PurchaseRequestDraftCommandIntentV1` is an application DTO for hashing only, not a second public request body. Its root includes `fingerprintSchemaVersion:1`, operation, canonical actual tenant/company/actor, accepted effective branch/department, target ID (null for Create), expected version (null for Create), and a normalised `input` object. Replay uses stored context pins; it does not substitute the actor's new employee, department or company defaults.

The keyed path first rejects duplicate properties including case aliases and unknown properties against the existing DTO allowlist at every object level. Existing `ProcurementOriginalValueInput` alone does not enforce that allowlist; this is an explicit proposed keyed-path validation addition. Header/line names use the existing camelCase fields in §12; Submit input is exactly `{version}`. No client-supplied status, ownership, branch, department, catalogPublishedVersion, counters or audit fields. Preserve existing interpretation of optional line `id` (Create does not assign the supplied ID; Update uses it to identify an existing line); include the supplied nullable ID in the intent so changed body never silently matches.

Normalise purpose/description/unit using the existing Trim semantics; other optional text via existing Clean; category/purchaseType via `ProcurementPolicyCode.NormalizeOptional`, before any catalog-derived classification. UUIDs use D form, dates `yyyy-MM-dd`, decimals use `ProcurementEvidenceEncoding.Decimal` **as strings**, and nullable discount remains distinct from zero. Preserve array order, optional IDs and nulls. Missing/explicit null members are equivalent where the existing DTO has that same meaning. Catalog published version/classification, generated IDs/timestamps/numbers, policy assessment and computed totals are server-derived original-result evidence, not invented caller inputs. No current reference query participates in the fingerprint.

Proposed keyed-only transport bounds: 256 KiB UTF8 input, existing 1–100 lines and existing field/decimal constraints, 1 MiB UTF8 original result. Reject oversized input before mutation; if the serialized original result exceeds its bound, roll back the entire still-uncommitted command rather than commit without a receipt. GET has the matching bounded envelope allowance. Do not silently truncate notes, results or fingerprints. Add boundary tests and review the new bounds against actual supported draft sizes before implementation sign-off.

### Definition of Ready and remaining evidence

**Definition of Ready is not yet declared met.** The eight design choices are now concrete for review; remaining gates are:

1. Independent review of the exact request normalisation/DTO/headers, scope pins, serializable adapter and endpoint-filter integration; validate compatibility and the bounded-input sizing against actual callers. No production guards are changed by this draft.
2. Assign centrally ordered successor migration, exact generated constraints/readiness capture and real finite-role grants after accepted P25; no model/migration/API exists for this proposal today. Historical receipt retention/legal hold, support owner, practical timeout/retention targets and independent reviewer still need an accountable owner.
3. Specify the support reconciliation procedure for a retained locator whose server rejected before writing but has no durable completed receipt. Do not require business spend approval for ordinary CRUD, or replace this technical recovery gap with a fabricated Finance dependency. A later pre-write-release contract is a separate reviewed enhancement, not assumed here.
4. Implement and execute §26 against genuine PostgreSQL, finite-role HTTP and actual lost-response browser transport. Pin actual source/artifact/schema/proof provenance; preserve failures. Existing CRUD, Open/Decide/P25 tests, source documentation and owner-only probes are not this feature's acceptance.
5. Record corporate-standard adoption and release approvals honestly. This draft records engineering proposals, not management sign-off, DoR completion, implementation, activation or whole-module readiness.
