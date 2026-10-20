# PROC-11 — Original invoice unit review and exposure-owner dependency

Status: Proposed bounded next feature; source inspection only, 20 October 2026. Documentation branch based cc9f94a; owning source also inspected in the active P26 worktree. No implementation, model, migration, provider, approval-policy adoption, test execution or deployment is implied.

## 1. Feature Identification

Procurement → Invoice Validation → Original Document Line/Unit Review → Request review / independent decision / consume retained evidence. Extends P26 mapping/comparison and P27 Finance-handoff boundaries. This milestone can verify document-line meaning; Finance-history completeness remains a separate dependency.

## 2. Purpose & Business Outcome

Allow a designated reviewer to confirm that each proposed invoice unit and original line reference matches a specific accessible original document version. Produce durable attributable evidence for matching. Document malware scanning, a proposed unit string and successful matching are different facts.

## 3. Scope & Exclusions

Review one complete mapping revision against immutable invoice originals, issued source, policy and Core document versions. Reject/return and corrected proposal are supported. Exclude conversion synonyms, inferred OCR authority, tax/accounting validation, AP approval/posting, payments, complete-history declarations and administrative bypass. Initial units require exact configured owner convention; any real conversion needs a separately owned conversion policy.

## 4. Parent & Related Features

Reuse `InvoiceMatchMappingVersion`, original `SupplierInvoice/Line`, `InvoiceMappingEvidence`, current invoice matching/document routes, Core `ApprovalEngine`, `ApprovalCase/Assignment/Decision`, Core `DocumentDbContext` and version/hash/scan lifecycle, current canonical worker/record scope, shared coordinator/audit/outbox. No duplicate employee, document, item, UOM or approval master.

## 5. Actors & Participants

Proposal maker/capture operator; explicitly granted independent original-document reviewer; current matcher; authorized reader/auditor. Canonical WorkerId independence prevents multiple accounts for one person approving their own evidence. Original reviewer identity stays retained after offboarding; the current acting reviewer/matcher still needs valid identity and scope. No default grant to buyer/AP/manager roles is proposed.

## 6. Entry Points & Triggers

Proposed additions in existing invoice detail, not a new page: request review for current mapping; assigned review task; inspect original document and all declared line/unit references; approve, reject or return with reason; GET owned review outcome/recovery. Exact route/DTO names require implementation review. No GET performs a review/compare or silently opens a case.

## 7. Preconditions

Separate default-off feature and exact readiness; current company/branch/action/worker; current invoice and mapping revisions; immutable original digest; one supported published review policy; complete mapping lines; all documents owned by this invoice and Available, not retired; verified version hashes. A matching policy does not automatically authorize independent unit review: review assignment/independence rules need their actual governed policy owner.

## 8. Workflow

1. Request under current actor/source scope, exact version/UUID key. Lock through existing branch→catalog→transaction/source ordering, pin current invoice original/mapping/issued/document versions/hashes and explicit review-policy version. Create Core case/assignments and owned source pin atomically; no invented spending threshold.
2. Assigned reviewer reads only purpose-required invoice lines/documents. Current Core identity/action/branch plus assignment and canonical-worker independence checked again on decision.
3. Decision atomically persists Core vote and Procurement terminal review evidence. A comment or scan Available cannot approve. Return/reject preserves original proposal; correction creates a new proposal revision requiring new review.
4. Matcher explicitly compares later. Resolve retained successful review against exact current mapping/original/doc/policy pins; no network call while source locks are held. Match may still be Blocked because historical exposure is unavailable.
5. Replay requires current original-operation permission/identity/target scope and exact wire fingerprint; returns recorded result without making a fresh decision.

## 9. Status / State Transitions

Review: NotRequested → PendingReview → Verified / Returned / Rejected / Cancelled. These are proposed domain outcomes, not invoice accounting statuses. Evidence superseded by changed mapping/original/document/policy is Stale for new matching, with historical decision preserved. P26 Unverified proposals remain legitimate draft input; review feature being off does not block intake.

## 10. Permissions & Data Scope

Add narrowly named request/decide/read capabilities only through the existing Core action catalogue, empty bootstrap unless policy explicitly adopts grants. Assigned reviewer gets purpose-bound review/detail/document access, not unrestricted invoice/PO/HR directory access. Current tenant/company/selected branch/allowed branches and immutable source branch must agree before paging/detail/recovery. Department home/selected OrgUnit is not an access grant; unsupported department policy fails closed. Document bytes remain protected through Core ownership checks.

## 11. Business Rules

Every original line exactly once; no substituted document, mapping, invoice, source, unit or policy. Review signer and current consumer are distinct identities. No rounding/name-based matching or unit-synonym inference. A changed proposal needs fresh review; retrospective rewriting is forbidden. A successful unit review never supplies Finance history, claims, validated/AP-ready status or permission to post.

## 12. Data Fields & Ownership

Procurement owns immutable review pins and mapping evidence; Core owns execution cases/votes/doc versions and access. First model design must reuse `InvoiceMatchMappingVersion.VerificationState/VerificationEvidenceJson/Actor*/ReviewedAt` where appropriate. Because mapping records are append-only, do not edit an Unverified proposal in place. Decide explicitly whether an independently reviewed successor mapping references its original proposal or a separate append-only source-review record binds it; choose after relational/codec review, not by adding parallel invoice tables.

Retain invoice ID/version/original SHA, mapping ID/revision/SHA, proposal-maker canonical worker, issued source IDs, review-policy identity/SHA/version, exact full line IDs/unit/document-version/hash/line-reference set, Core case/completion/decision, reviewer principal+worker, outcome/time/reason/schema/evidence SHA and command identity. Current pointers/counters are operational, never mutable EF principal keys. Stable scoped IDs, explicit short physical key names and append-only guards are mandatory.

## 13. Screens & Data Views

Extend current Bills mapping/detail/document panels and shared assigned-task table. Show review state, reviewer outcome, exact version, stale reason and explicit next step. Keep raw owner envelopes/private other-invoice history server-side. Show current scoped original bytes through existing Core document service.

## 14. User Experience / Human Behaviour

Use “Check document lines and units”, “Waiting for review”, “Return for correction”, “Document changed—review again”. Never label unit review “Invoice approved” or “Ready for payment”. Preserve typed proposal/UUID during ambiguous save; GET recovery only until original outcome known. Clear old data/actions on actor/company/branch changes.

## 15. Notifications & Communications

Use existing assigned-task notifications/outbox after durable transition. Notifications cannot determine review success. No supplier contact, AP send or payment message in this milestone.

## 16. Documents & Attachments

Core Available verifies stored content identity and scanning only. Retain exact owner-document-version/hash per original line, with governed line references. New document versions do not retroactively alter reviewed bytes. Current retired/rejected/inaccessible required evidence blocks new consumption while preserving historical audit; immutable version hashes remain attributable. A separate document store needs the configured authenticated owner reader, not similarly named local tables.

## 17. Exceptions & Edge Cases

Alias maker/reviewer, offboarded original maker, revoked current reviewer, branch corruption, missing original bytes, duplicated lines, empty hash, malformed/duplicate JSON fields, concurrent mapping changes, policy expiry/revocation, document retirement/version replacement, partial decision quorum, timeout/commit loss, repeated key with changed body, late original decision after corrected proposal. Each fails closed or returns recorded historical outcome without new authority.

## 18. Integrations / APIs / Events / Sandbox

`InvoiceMappingReviewEnvelope` currently binds PrincipalId/WorkerId/ReviewedAt to the **current Compare context** in `InvoiceMappingEvidence.ReviewCovers`; it cannot directly represent another person's historical review. Introduce a versioned evidence contract separating retained authority signer/case/policy/document pins from current authorized consumer. Never re-sign an old review as if today's matcher performed it. Preserve old evidence as historical schema; no silent reinterpretation.

`IInvoiceMappingEvidenceReviewer` and `IInvoiceHistoricalExposureOwner` lack an enlisted DB/session parameter. A registered ordinary scoped DbContext is not automatically the coordinator's fresh physical transaction. Implement an explicit owning-session/read adapter or authenticated durable checkpoint input under that transaction; do not query an unrelated context or call remote owners while locks are held.

The external Finance owner contract below is a dependency only. Default Unavailable adapters stay until actual authenticated production owners are installed and independently verified.

## 19. Audit / Security / Privacy

Audit request/assignment/decision/rejection/return/supersession/consumption and denied sensitive exports as existing policy requires. Retain signer and command-consumer separately. No bank details, raw other-invoice allocations, credentials or document bytes in notifications/logs. Exact current authorization rechecks at API/backend; hiding buttons is insufficient.

## 20. Reports / Dashboards / Metrics

Review pending/age/returned/stale and audit attribution only. No invoice approval, payable, spending, savings or payment totals inferred from review counts.

## 21. Configuration & Localization

Default off; actual published policy decides reviewers/independence, scope and escalation. No thresholds, expiry duration or UOM aliases invented. UTC/invariant retained values; shared locale formatting and Core settings only. Feature flag cannot declare a missing owner/schema verified.

## 22. Non-Functional Requirements

Bound line/document/task counts; authorized filters before count/page. No long-running document/network operation under financial/source locks. Retry confirmed transaction aborts only with existing coordinator. Exact versions/hashes/CAS and named relational constraints; no mutable binding key. Current account/role/worker mutations have only the guarantees of their actual participating fences, not a fictional universal lock.

## 23. Postconditions

One attributable independent review outcome for exact retained proposal/document/policy pins, or preserved returned/rejected proposal. No financial claim/ledger/AP/payment change. A subsequent comparison still requires authenticated complete owner history and all ordinary P26 guards.

## 24. Failure & Recovery

Core and Procurement review writes commit together or roll back. Stable durable operation receipt recovers uncertain writes under current original action/scope; no fresh key or automatic resubmission. Stale review cannot be repaired by editing evidence. Unknown downstream Finance consumption remains unavailable and blocks qualified claims.

## 25. Acceptance Criteria

An explicitly assigned independent reviewer can verify exact original document units with durable attribution; alias/self/foreign/stale/malformed cases deny. Proposal correction does not mutate originals. Another authorized matcher can consume the retained review under a distinct consumer identity. With Finance owner absent, comparison remains Blocked and no claim/AP-ready state appears.

## 26. Test Scenarios

Positive real Core case→independent decision→retained review→another matcher verifies document units. Negative aliases, missing/current revoked grants, offboarding, cross-branch direct URLs/counts, altered invoice/mapping/original/line/UOM/document hash/policy, unknown schema, inactive historical rows, duplicate/omitted lines and old response replay. Actual PG proves atomicity, exact scoped bindings, append-only facts, CAS/concurrent correction and commit-loss receipt cardinality. Real UI uses actual documents/task/mapping APIs; independent owner SQL verifies no Finance claims/postings. Default-off/legacy intake compatibility and unknown Finance history remain explicit. These tests are proposed, not run.

## 27. Ownership / Dependencies

Procurement engineering owns document-unit business review; Core owns workflow/document/access services; Finance owns historical AP/credit consumption and its completeness boundary. Neither payroll outbound CSV nor purchase funding confirms that consumption. Invoice final accounting/approval/AP handoff remains the separate P27 contract.

## 28. Open Decisions / Assumptions

Adopt exact review policy/permissions, corrected envelope schema and immutable source-review representation before implementation. Identify actual Finance system owner, authentication, full-history coverage/cutover, revocation/lease protocol and reconciliation semantics before qualified claims. No missing business authority is replaced by a manually entered Complete flag. Engineering can implement independent review without pretending that this resolves Finance ownership.

## External Finance exposure contract — dependency, not implemented AP

Versioned owner request identifies tenant/company/branch, supplier, PO+issued version, currency, matching-policy digest and original correlation. Authenticated connection binds issuer/audience/service principal to that scope; body headers cannot choose another company. Response pins owner-system/contract version, immutable message/checkpoint ID, source mapping, coverage epoch, monotonic watermark/sequence, observed/as-of time and explicit completeness scope/cutover.

Complete means all consequential historical debit/credit/correction/reversal allocations for the exact source, including migrated/offline/other-channel transactions and inactive originals. Each allocation has stable owner invoice/line/source/acceptance identity, exact quantity/net/tax/currency, original-debit link for credits and validation reference. Unknown mappings, gaps, unresolved imports/late transactions, partial pages or owner/local overlap block completeness. Zero is acceptable only as an actual owner-certified complete empty checkpoint covering every channel, never because local rows are absent.

Owner attestation requires authenticated origin and durable verified envelope/hash, not merely JSON Complete=true. Define a coverage lease or equivalent writer fence: every financial writer and revocation must honor it. Time-until alone cannot make a stale zero snapshot safe while external writers can post. Fetch/reconcile remote facts outside source locks; consume a locally durable authenticated checkpoint only with a valid owner coverage protocol inside the command transaction. Define allocation deduplication and local-claim incorporation/acknowledgment so owner and Procurement claims cannot double-count the same obligation.

Cutover must establish a reconciled opening baseline, all historic source mappings and outstanding uncertain transfers, then preserve ordered owner events/gap detection. Revocation, expired lease, sequence gap, conflicting payload for same message or source remapping blocks new claims. Existing claims are not silently removed; resolve with owner-backed correction/credit/reversal and reconciliation. Original-key recovery remains historical proof, not renewed financial authority.

Transport-delivered/HTTP200/funding-approved/payment-reference is not completeness or AP acceptance. Reuse existing audit/outbox/inbox contracts where genuinely supplied; no shared authenticated Procurement Finance inbox currently exists in inspected source. A future durable integration receipt is evidence/deduplication, not a second payable ledger. The exact owner/provider is unresolved; this document enables no Finance endpoint or machine permission.

## File-level implementation sequence

1. Extend `Application/InvoiceMatchingExecutionContracts.cs`, `InvoiceMappingEvidence.cs`, `InvoiceMatchExecutionEvidence.cs` with independently signed review authority versus current-consumer binding and negative cross-binding tests; retain default Unavailable.
2. Review mapping/evidence representation in `Domain/InvoiceMatchingExecutionRecords.cs`, then `Infrastructure/Data/ProcurementInvoiceMatchingMapping.cs`: no in-place evidence rewrite; exact source/FK/append-only/short-name/CAS design before central migration.
3. Add explicit empty-bootstrap review action(s) to existing `hrm-api/Application/ProcurementActionCatalog.cs`; reuse `Api/ProcurementAuthorityIdentity.cs`, `ProcurementExecutionRecordScope.cs`, Core approvals engine/context and coordinator for request/task/decision/recovery. No generic HRM role grants or parallel engine.
4. Use `Api/ProcurementInvoiceDocumentRoutes.cs`/`ProcurementInvoiceMatchingCommands.DocumentHashes`, Core document ownership/version/hash readiness and private download; correct production adapter transaction enlistment explicitly.
5. Extend existing Bills `InvoiceMatching.tsx`/document/task UI after typed contract review. Run actual API/PG/browser gates with distinct reviewer. This only activates unit review; Finance dependency and P27 remain unavailable.
6. Separately negotiate/implement the actual authenticated Finance coverage reader/checkpoint/reconciliation protocol above. `Hrm.Infrastructure/IntegrationOperations.cs`, `Hrm.Domain/Entities/Integrations.cs` are payroll outbound handoffs/operator notes and cannot serve as complete AP-consumption authority. `PurchaseOrderFinanceEvidence`, `BudgetCheckSnapshot`, `Supplier.FinanceSupplierRef`, `FinanceInvoiceHandoff` and `FinancePaymentStatusRef` are funding/references/projections, not that owner.

## Pure step 1 source checkpoint — retained review schema2

Source-only `InvoiceRetainedUnitReview` adds a separate strict schema2 envelope/verifier; it is **not wired** into Compare, Claims, DI or a production reviewer. Existing caller-bound schema1 helper and retained history are unchanged. Schema1 cannot be consumed by this new verifier as independent review; unknown schema/fields, duplicate JSON properties or missing data fail closed. Production `UnavailableInvoiceMappingEvidenceReviewer` and `UnavailableInvoiceHistoricalExposureOwner` remain unchanged.

The pure authority input requires actual owned review identity/version and integrity hash, original invoice/line set/current mapping, full Core case/stage/assignment/decision facts, retained canonical assignment-worker pins and distinct matching-policy/review-policy versions/digests. It checks exact completion/decision/source/assignment/quorum, alias-maker independence and unique canonical workers within each quorum. Current matcher identity/permission/company/selected-branch/membership/document hashes are separate from historical reviewer identity/time. Operational invoice version may advance while original digest and mapping remain exact. Current required evidence/policy revocation or document unavailability invalidates new consumption; history is retained.

These typed inputs are owner projections, never public request DTOs or proof that arbitrary caller-created objects are authentic. A future adapter must load them through authenticated owning stores/enlisted transaction, validate policy semantics/current identity/record scope and document access, and retain their real provenance. The proposed review source type/key/permission constants are unregistered pure contract identifiers, not an adopted assignment policy. Synthetic tests use Core engine decisions and typed policy records solely to test evidence bindings; their policy JSON is explicitly synthetic, not a production configuration or approved policy parser.

**Step 2 is not Definition of Ready yet:** decide immutable review representation (existing mapping successor versus separate source-review fact), exact original proposal lineage and version semantics, and real review-assignment/policy publication/selection before persistence/API. Existing matching policy must not be re-labelled as a review policy. No database model is changed by step1.

**Required document-access prerequisite before API:** `ProcurementInvoiceDocumentAccess.RequireAsync` currently requires invoice-read for every non-upload operation. A purpose-only assigned reviewer cannot use it yet. Add a validated same-invoice **current Core review assignment + current canonical actor/action + selected/allowed branch + immutable retained source scope** ownership path for the specifically permitted metadata/download operation. Keep owner module/type/invoice/version checks, Core scanning/retirement/privacy and upload restrictions. Do not solve this by granting broad invoice-read, accepting an assignment ID from a client as authority, trusting an old task cache or bypassing owner access. Test foreign/withdrawn/completed/stale assignment, wrong invoice/document, branch revocation and no general invoice-list expansion. This prerequisite is recorded only, not implemented by the pure evidence helper.

Authored pure tests cover a different matcher consuming retained evidence without re-signing, same maker consuming another person's review, operational version advance, current scope/membership/permission, all original/mapping/issued/policy/document/line bindings, genuine Core completion/decision/assignment facts, canonical alias and quorum attacks, malformed/duplicate/unknown/missing JSON, old/new schema denial and stale policy/document/original data. No SDK/DB/browser job ran for this checkpoint; source review, central compilation and later real ownership/runtime tests remain required.
