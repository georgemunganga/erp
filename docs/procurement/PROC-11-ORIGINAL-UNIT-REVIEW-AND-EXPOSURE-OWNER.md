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

## Step 2 implementation contract — source design, not activated

Related ADR: `docs/adr/ADR-PROC-INVOICE-REVIEW-OWNER-BOUNDARY.md`, Step 2 representation/adapter decision. Baseline `3613559`; pure retained verifier prerequisite `f859337` + `cadaffb` is reviewed source on its separate branch, not yet an installed provider/model. No schema generation, code activation or test execution accompanies this plan.

| Existing 28-section field | Concrete Step 2 increment |
|---|---|
| 1 Feature Identification | Original-unit review, owned immutable review source + existing Core execution. |
| 2 Purpose & Business Outcome | Independent human interpretation of proposed invoice document-line/unit references; no AP or capacity approval. |
| 3 Scope & Exclusions | One complete current mapping; named One/All review; read/decide/receipt. No unit conversions, spending thresholds, broad directory, Finance history or automatic OCR authority. |
| 4 Parent & Related Features | P26 immutable mapping/originals; reviewed v2 verifier; Core approval engine/documents and current identity/branch fences. |
| 5 Actors & Participants | Mapping maker requests; explicitly named distinct-worker reviewers decide; authorized matcher separately consumes retained evidence. Policy maker and publisher remain independent. |
| 6 Entry Points & Triggers | Existing Bills detail and assigned approval presentation; routes below. No GET creates data. |
| 7 Preconditions | Explicit actions/default-off feature/readiness; installed reviewed model; same physical metadata DB; exact current mapping/original/issued/document/policies; canonical current actors and supported named policy. |
| 8 Workflow | Open under invoice lock → immutable source+Core assignments → independent decisions → one terminal evidence → separate later comparison. Returned/Rejected proposals remain unverified. |
| 9 Status / State Transitions | Core Pending→Approved/Rejected/Returned controls execution. Procurement immutable terminal Verified/Rejected/Returned binds it. Stale is computed from changed current pins, never rewriting historical evidence. One attempt per mapping+matching-policy+review-policy tuple initially. |
| 10 Permissions & Data Scope | Three explicit empty-bootstrap actions; current assignment worker+branch before count/detail/download/decision. Only maker requests initially. Unsupported department grants fail closed; no broad invoice/PO read requirement for reviewers. |
| 11 Business Rules | Every original line once; exact claimed unit/document-version/hash/reference; distinct worker identities; no approved review from merely `VerificationState` or scanned PDF. Actual Core final stage/quorum/decisions verified by v2. |
| 12 Data Fields & Ownership | Four small owned records in ADR; stable IDs/short keys/scoped binding. Core cases/documents retained in their owning schemas. No second mapping/invoice/identity master. |
| 13 Screens & Data Views | Existing Bills mapping/review summary and shared assigned-task detail/modal; minimal purpose projection and pinned file links. No new sidebar. |
| 14 User Experience / Human Behaviour | “Review item units”/“Check the original invoice”; explain pending, stale and unavailable states. Human must inspect source lines; no auto-ticked commercial attestation. |
| 15 Notifications & Communications | Stable `supplier_invoice.unit_review_requested.v1` and `supplier_invoice.unit_review_completed.v1` outbox declarations only after durable writes; no email inside transaction. |
| 16 Documents & Attachments | Same-invoice pinned Available/nonretired Core version/hash. Purpose-only read/download reuses Core service through route-local pinned-version `IDocumentRecords` decorator plus Read-only owner adapter; no extra uploads or broad document enumeration. |
| 17 Exceptions & Edge Cases | Unknown policies/aliases/self-review/offboarding/missing file/current mapping change/ambiguous outcomes/old schema fail closed. Historical valid signer need not remain an active login; current actor must. |
| 18 Integrations / APIs / Events / Sandbox | Existing SERIALIZABLE shared adapter enlists Core identity/approval/document metadata; explicit policy resolver required. Private actual PostgreSQL fixture uses real Core engine/files/roles and synthetic document scanner only if clearly declared. |
| 19 Audit / Security / Privacy | Append-only source/assignment/outcome/receipt and consequential audit; no raw policy JSON, blob keys or unrelated invoice/Finance data on purpose routes. Payload digest validates binding, not authority. |
| 20 Reports / Dashboards / Metrics | Pending assigned review count and outcome/time from actual execution only; no savings/AP/payment KPI implied. |
| 21 Configuration & Localization | Real independently published review policy; initial named principals/One/All only. Engineering bounds are input-safety limits, not business approval thresholds. |
| 22 Non-Functional Requirements | Bounded lists/complete source≤200 lines; strict bounded JSON from v2; no network document analysis under locks; accessible existing ERP controls; cancellation and no-store download. |
| 23 Postconditions | Exact review source and terminal proof, or durable nonverified outcome; original invoice/mapping and all claim/AP ledgers unchanged. |
| 24 Failure & Recovery | Stable same UUID/body, original exact response; changed actor/body rejected. Unknown commit is reconciled via receipt, not blind new key. Only confirmed serialization abort reexecutes callback. |
| 25 Acceptance Criteria | Actual different-worker reviewer can finish a real Core case and another matcher verify exact units; review does not make comparison claimable while exposure history unavailable. |
| 26 Test Scenarios | Pure policy/source/result guards; full Save/append-only/CAS; mapped scope/permission/alias/no-effect; actual restricted PG atomicity/concurrency/recovery/key-name/Down tests; actual purpose document/browser path. None yet run for Step2. |
| 27 Ownership / Dependencies | Procurement review/policy/outcome; Core execution/identity/documents; parent central migration/release. Existing financial-owner absence remains a separate blocker. |
| 28 Open Decisions / Assumptions | Business must adopt actual named reviewers/stages/actions; confirm first-release maker-only opening and one-attempt rule. Strict policy schema/validator, source/outcome persistence and typed v2 reader/projection remain required implementation. No business amount limits are guessed. |

### Proposed exact HTTP surface

All routes are under `/api/procurement/v1`; command inputs carry no caller-supplied reviewer identity, verified flag, document hash or policy authority.

- `POST /invoices/{invoiceId}/mappings/{mappingId}/unit-review-cases`, request action plus current mapping-maker worker. Input `{expectedInvoiceVersion, expectedMappingRevision, reason}` and UUID `Idempotency-Key`. After original-key recovery, server reads immutable issued proof without upstream receiving/PR locks and chooses exactly one current independently published review policy and matching policy, loads immutable units/documents and canonical named assignments. `201 {reviewId, invoiceId, mappingId, caseId, sourceVersion, caseVersion, state, invoiceVersion}`. No review mutates invoice version solely to store status; invoice row lock still serializes creation against remapping.
- `GET /invoice-unit-reviews?state=Pending&limit&offset`, request permission lists only own opened sources; review permission lists actual assigned sources. Read permission alone does not grant all-company discovery. Exact tenant/company/retained selected/allowed branch filtering happens before count/page. Detail uses same purpose authorization, not a broader direct-URL fallback.
- `GET /invoice-unit-reviews/{reviewId}`, bounded source facts, immutable invoice quantities/prices as exact decimal strings, declared units and pinned file labels, case/version/current action hints and decision history. No supplier bank, Finance snapshot, unrelated original documents or PO headers. Hints are not command approval guarantees.
- `GET /invoice-unit-reviews/{reviewId}/documents/{versionId}/download`, current assigned review worker or original requesting maker, exact pinned version/hash enforced by route-local records Find/List allowlist and Core ownership/scan checks; `Cache-Control:no-store`. File listing comes from pinned detail, not all invoice attachments.
- `POST /invoice-unit-reviews/{reviewId}/assignments/{assignmentId}/decisions`, review action; `{action:Approve|Reject|Return, reason, expectedCaseVersion, expectedAssignmentVersion, expectedInvoiceVersion}` plus same-command UUID. Current canonical actor/worker/configured permission, assignment, immutable source/current original/mapping/issued/docs/policies checked before Core mutation. Reject/Return requires a reason. Source staleness denies completion and displays stale; automatic cancellation of existing Core cases is outside this slice.
- `GET /invoice-unit-reviews/command-receipts/{operation:Open|Decide}/{key}`, current original command actor/worker/action and retained source scope; returns original status/body when committed, otherwise NotFound. No GET writes, policy adoption or automatic new-key retry.
- Existing invoice match history/readiness gains a minimal exact-current-mapping review summary. Matching remains an explicit separate command, and a valid v2 review cannot replace complete exposure history or change AP handoff availability.

Known failures: 401 unauthenticated; 403 missing action/canonical worker/independence; 404 inaccessible owned resource (including document version); 409 stale/CAS/policy/assignment/current-source or receipt conflict; 422 malformed input/unsupported rule document; 503 default-off/missing exact schema/separate owner store. Errors use safe stable codes and actionable text, not raw policy/exception content. Retained original-key recovery is authorized historical evidence; it does not revalidate or recreate a completed business effect under today's policy.

### Implementation sequence / Definition of Ready

1. Distinct review of this ADR and 28-section/API contract. Integrate and test the reviewed `cadaffb` pure contract without activating a provider.
2. Bounded pure review-policy parser/resolution plan and source-open/terminal transition contracts, preserving Core source/case ownership. Business-owned named principals/stages are required for actual published policy; empty configuration must block.
3. Add the three explicit action catalogue entries, policy configuration type/validator/maker-checker current scope, default-off feature and unavailable schema readiness. Register no bootstrap reviewer role or fabricated thresholds.
4. Central additive model/migration after stable scoped keys and exact mutation guards pass Npgsql model review. Preserve all P24–P26 literal overlays and used-evidence Down locks/stops. No migration is generated in this design branch.
5. Actual enlisted open/decide/read/receipt/document adapters, then typed retained v2 reader/projector at Compare. Do not inject caller-bound schema-1 “Verified” evidence to bridge the gap.
6. Pure/mapped/actual restricted PostgreSQL/actual browser gates with independent persisted ownership and no-Finance-effect verification. Production remains disabled until review/policy/DoR, operational and release controls pass.


Step2 design correction: invoice→case is the only new-effect lock order; the issued proof is read-only immutable owned evidence. Do not invoke the upstream-locking receiving bridge after the invoice lock. Receipt replay authenticates the original actor/action/retained scope first but does not reapply today's policy/document/commercial eligibility. Core IDs are verified explicit references, with no new cross-schema FKs. The existing owner-access interface is owner-level only; pinned version/hash enforcement belongs in a route-local records Find/List decorator feeding the unchanged Core download service.

## Pure step 1 source checkpoint — retained review schema2

Source-only `InvoiceRetainedUnitReview` adds a separate strict schema2 envelope/verifier; it is **not wired** into Compare, Claims, DI or a production reviewer. Existing caller-bound schema1 helper and retained history are unchanged. Schema1 cannot be consumed by this new verifier as independent review; unknown schema/fields, duplicate JSON properties or missing data fail closed. Production `UnavailableInvoiceMappingEvidenceReviewer` and `UnavailableInvoiceHistoricalExposureOwner` remain unchanged.

The pure authority input requires actual owned review identity/version and integrity hash, original invoice/line set/current mapping, full Core case/stage/assignment/decision facts, retained canonical assignment-worker pins and distinct matching-policy/review-policy versions/digests. It checks exact completion/decision/source/assignment/quorum, alias-maker independence and unique canonical workers within each quorum. Current matcher identity/permission/company/selected-branch/membership/document hashes are separate from historical reviewer identity/time. Operational invoice version may advance while original digest and mapping remain exact. Current required evidence/policy revocation or document unavailability invalidates new consumption; history is retained.

These typed inputs are owner projections, never public request DTOs or proof that arbitrary caller-created objects are authentic. A future adapter must load them through authenticated owning stores/enlisted transaction, validate policy semantics/current identity/record scope and document access, and retain their real provenance. The proposed review source type/key/permission constants are unregistered pure contract identifiers, not an adopted assignment policy. Synthetic tests use Core engine decisions and typed policy records solely to test evidence bindings; their policy JSON is explicitly synthetic, not a production configuration or approved policy parser.

**Step 2 is not Definition of Ready yet:** decide immutable review representation (existing mapping successor versus separate source-review fact), exact original proposal lineage and version semantics, and real review-assignment/policy publication/selection before persistence/API. Existing matching policy must not be re-labelled as a review policy. No database model is changed by step1.

**Required document-access prerequisite before API:** `ProcurementInvoiceDocumentAccess.RequireAsync` currently requires invoice-read for every non-upload operation. A purpose-only assigned reviewer cannot use it yet. Add a validated same-invoice **current Core review assignment + current canonical actor/action + selected/allowed branch + immutable retained source scope** ownership path for the specifically permitted metadata/download operation. Keep owner module/type/invoice/version checks, Core scanning/retirement/privacy and upload restrictions. Do not solve this by granting broad invoice-read, accepting an assignment ID from a client as authority, trusting an old task cache or bypassing owner access. Test foreign/withdrawn/completed/stale assignment, wrong invoice/document, branch revocation and no general invoice-list expansion. This prerequisite is recorded only, not implemented by the pure evidence helper.

Authored pure tests cover a different matcher consuming retained evidence without re-signing, same maker consuming another person's review, operational version advance, current scope/membership/permission, all original/mapping/issued/policy/document/line bindings, genuine Core completion/decision/assignment facts, canonical alias and quorum attacks, malformed/duplicate/unknown/missing JSON, old/new schema denial and stale policy/document/original data. No SDK/DB/browser job ran for this checkpoint; source review, central compilation and later real ownership/runtime tests remain required.

### Distinct source-review follow-up (unexecuted)

The initial reviewer duplicate-property concern was retracted after inspecting the actual base: `ProcurementEvidenceEncoding.PolicyJsonDigest` already recursively rejects exact duplicate JSON names. The v2 verifier continues to use that existing guard; no second duplicate canonicalizer is introduced. Separate corrections make schema2 deserialization case-sensitive and integer fields strict JSON numbers, rejecting case aliases and quoted schema/version counters without changing legacy evidence or exact monetary-string APIs. Before shared canonicalization, evidence work is limited to256KiB UTF8, depth16 and10,000 JSON nodes; these are technical envelope limits, not procurement spending policy. Authored root/nested duplicate and alias, quoted counter, size/depth/node tests are source-only.

Core retained-proof follow-up requires the actual final decision to belong to the highest-sequence final stage, exact stage vote count at its closing quorum, unique decision keys and nondecreasing vote chronology across stages (equal timestamps accepted). These checks reject impossible retained records and do not execute another approval engine. A genuine two-stage Core-engine quorum fixture, equal-time positive, earlier-stage completion, out-of-order stage vote and extra-vote negatives are authored. No jobs ran; independent closure review and central compile/tests remain pending.

## Step 2 bounded pure source increment — authored, unexecuted

The corrected ADR is followed by pure `InvoiceUnitReviewPolicyRules` and `InvoiceUnitReviewTransitions`; no action registration, policy publication route, EF mapping, migration, DI/provider activation or production configuration is changed. Reviewed Step1 `f859337`/`cadaffb` is carried as unchanged prerequisite source. Historical documentation above records its earlier DoR state; the Step2 representation is now proposed explicitly, while actual policy adoption and adapters remain pending.

Policy schema1 is exactly `{schemaVersion:1, transactionType:"InvoiceUnitReview", stages:[{code, mode:"One"|"All", principals:[canonicalCorePrincipalId]}]}`. Strict known fields, shared duplicate-property canonical validation, bounded UTF8/depth/stage/member counts and ordinal canonical GUIDs prevent fallback authority. The engineering safety limits are 64KiB, depth16, 16 stages, 32 named members/stage and 128 total stage memberships. These are parser/resource limits, not spending or business approval thresholds. Array order is stage sequence; members are sorted deterministically. Current owner-projected account/worker/action/allowed-branch eligibility is required for every named reviewer, with maker and same-worker aliases excluded. One maps to Core Any/1, All to Core All/full set. Exact actual case graph/worker pins must match the derived policy projection; merely carrying its policy ID cannot reduce quorum.

The source-open plan binds actual immutable invoice original/mapping/issued evidence and document version hashes, current matching and independent review policies, original maker, generated review/case IDs and complete Core assignments. The caller must load the issued projection from real immutable owning evidence without upstream locks, and exact Available/nonretired document hashes through the owning Core metadata adapter. A typed input/hash is not a signature or proof of arbitrary caller-created objects. The plan constructs and validates a real Core case, then new decisions revalidate current exact evidence/CAS/worker/policy graph before delegating state/quorum/vote handling to the existing `ApprovalEngine.Decide`. It creates no duplicate engine and does not declare units Verified, create terminal review outcome evidence, amend invoice/mapping, consume exposure or enable AP.

Original-key receipt recovery must happen in the eventual owning adapter **before** these new-effect functions; no pure function claims to recover a persisted command or install the one-attempt tuple constraint. The first-release maker-only opening restriction is explicit in this source plan and remains subject to business adoption before activation. Terminal outcome sealing, actual typed v2 reader/projector, purpose-version records decorator, independently governed policy publication, source persistence/guards and all actual runtime tests remain future slices.

Authored pure tests cover deterministic One/All projection, unsupported/ambiguous/oversized policy input, aliases and current branch/action/identity, weaker substituted Core graph, genuine Core One→All completion and Return, stale original/mapping/document/policy/case/issued/versions, no-effect rejection and operational versions advancing without changing immutable issued proof. They assert originals, proposal verification state and Finance claim/posting remain untouched. No SDK/DB/browser test has run for these new contracts.
