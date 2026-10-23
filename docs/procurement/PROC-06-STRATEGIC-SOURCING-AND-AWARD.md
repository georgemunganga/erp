# PROC-06 Strategic Sourcing and Award

**Status:** Draft for business review · **Version:** 0.1 · **Hierarchy:** Procurement → Strategic Sourcing → Sourcing Event, Bid Evaluation and Award → Operations · **Framework:** [ERP 28-section standard](../hrm/feature-specifications/ERP_Feature_Specification_Framework_Enterprise_Integration_Updated.docx) · **Module:** [PROC-00](PROC-00-PROCUREMENT-MODULE-ARCHITECTURE-AND-CAPABILITY-MAP.md)

**Implementation note (18 October 2026):** The shared ERP host has an own-record sourcing draft API under `/api/procurement/v1/sourcing`. It can create, revise and cancel an internal event and its lines. Removed lines retain a cancellation state and reason. Approved PR-line conversion, method policy, publication, supplier invitations, sealed bids, evaluation and award are not enabled by this draft API.

**P29 source-draft decision (23 October 2026, pending migration and acceptance):** The governed `from-approved-sources` path must accept an approved P25 principal-identity PR line when its maker, requester, Finance checker and buyer are active, authorized Core accounts with an allowed transaction branch. Retain the original PR submission schema 3 and Finance checker schema 2; never fill missing worker IDs with synthetic values. A new RFQ evidence version records canonical principal IDs, optional historical Core worker aliases, source pin digests, the approval case and final decision. Version 1 worker evidence remains byte-compatible and is restored under its original checks. Create reserves only the selected available quantity; cancellation releases precisely those allocations after source locks, retained-evidence verification and CAS. An unknown write is recovered by the original idempotency key under the current actor and record scope. Read and cancel require current action permission and source branch access. This milestone creates an internal Draft only: publication, invitations, bids, evaluation, award and supplier dispatch remain unavailable. The route stays default off until the genuine owning migration, exact finite-role schema inspection and private no-employee acceptance are complete.

## 1. Feature Identification

`PROC-06`, purchasing phase. Business owner: sourcing lead; technical/product/data owners to name. Operations: `O1` choose method and create event, `O2` approve/publish, `O3` collect and seal bids, `O4` open/evaluate, `O5` recommend/approve award, `O6` cancel/reopen/release PR allocation.

## 2. Purpose and Business Outcome

Select suppliers under the required competition or justified exception route, with comparable offers and an auditable decision. Success is an approved line-level award or documented no-award; a supplier response alone never creates a PO.

## 3. Scope and Exclusions

Includes direct/single-source/emergency method determination, RFQ/RFP/tender events, invitations, questions, portal/offline/surrogate responses, sealed bids, compliance and scored evaluation, recommendation, award and line allocation. Contract signature, PO issue and supplier payment are separate. Method-specific legal requirements await policy review.

## 4. Parent and Related Features

Parent Strategic Sourcing; consumes approved [PROC-02](PROC-02-PURCHASE-REQUISITION-AND-LINE-ROUTING.md) lines, [PROC-01](PROC-01-SUPPLIER-ONBOARDING-AND-MANAGEMENT.md) eligibility and [PROC-CFG-01](PROC-CFG-01-PROCUREMENT-CONFIGURATION-AND-POLICY.md) method/evaluation rules. Contracts and Purchasing consume award; Supplier Portal can submit bids later.

## 5. Actors and Participants

Buyer/sourcing lead, invited supplier, authorized surrogate-bid recorder, technical/commercial evaluators, conflict reviewer, award approver, requester, compliance and auditor.

## 6. Entry Points and Triggers

Approved PR-line allocation, annual sourcing plan, contract expiry, approved direct/single-source/emergency exception, RFx schedule, supplier clarification, bid deadline or evaluation task.

## 7. Preconditions

Approved available demand, valid method/threshold policy, authorized sourcing owner, supplier eligibility or explicit invitation exception, criteria and weights fixed before publication, confidentiality and deadline configured.

## 8. Workflow

`O1`: choose method and source lines; document rationale/exceptions. `O2`: approve event then publish identical requirements and due date to eligible suppliers. `O3`: accept versioned portal/email/physical/surrogate bids with source evidence and received time; seal until close. `O4`: controlled opening; pass/fail compliance, independent weighted technical/commercial scores, conflict declarations and normalized comparison. `O5`: recommend line awards and rationale; separate authority approves; publish award references to Contracts/PO. `O6`: cancel or reopen with reason and supplier notice; release unawarded PR allocation.

P29 `O1` implementation boundary: an authorized Core buyer selects approved P25 PR lines with the same company, branch, currency and competitive method. The server locks requests in stable order, reloads the current Core actor and original approved case, checks current Finance evidence, catalog classification and available quantity, then atomically writes the Draft, exact line holds, immutable source pins, audit, outbox and command receipt. Read and cancel verify the retained principal pin and owned line/hold tuple; cancel releases only the event's holds. No employee record is required.

## 9. Status and State Transitions

Event: Draft → Pending Approval → Published/Open → Closed → Evaluation → Award Pending → Awarded/No Award; alternatives Cancelled/Reopened. Bid: Invited → Draft → Submitted/Withdrawn → Opened → Evaluated → Awarded/Not Selected/Disqualified. Each transition records actor, conditions, required documents, deadline, SLA and escalation. Submitted bid is immutable; correction is a version. Late bid is blocked or separately approved under policy, never backdated.

P29 source draft permits only Draft → Cancelled. The other transitions stay unavailable. A cancelled draft and its command receipt remain readable to the current authorized owner.

## 10. Permissions and Data Scope

Buyer manages assigned entity/category event; supplier sees only its invitation and own bid; evaluators see assigned phase and sealed content only after authorized opening; award approver is distinct where policy requires. Surrogate recorder cannot alter source receipt time or approve own award. Competitor prices and scores are field restricted.

P29 requires current Core `procurement-access` plus `procurement-sourcing-convert` or `procurement-sourcing-cancel`, the selected company and current permitted branch. Original requester and Finance checker independence is revalidated through the P25 Finance guard; optional HR worker data never grants action or branch access. A buyer may draft sourcing for a request they made unless an approved sourcing policy adds a separate restriction. Owned draft lists apply branch and owner filters before count and paging.

## 11. Business Rules

`BR-PROC-SRC-001` method follows effective threshold/category policy; `002` criteria/weights lock at publish; `003` no bid content before opening; `004` surrogate bid needs actor, supplier, source, original evidence and both received/entered times; `005` evaluator conflict must be resolved before score counts; `006` award quantity/value cannot exceed approved source balance without renewed authority; `007` sole source/emergency needs reason and designated approval.

## 12. Data Fields and Ownership

Procurement owns event ID/type, PR lines, specs, schedule, supplier invitations, Q&A, bid/version/source/evidence, compliance result, evaluator score/comment, normalized comparison, recommendation, award line and approvals. Supplier owns original offer; Procurement stores controlled snapshot. Finance owns budget; Contracts/PO own downstream agreement/order.

P29 adds source evidence version 2: event owner principal and optional historical worker alias; binding maker/requester principals, optional aliases, P25 principal identity digest, immutable approval/policy/Finance/method references; and actor-owned version 2 command receipt. Existing version 1 worker evidence and fingerprints remain immutable. Procurement owns RFQ evidence and allocation; Core owns account/branch/role and approval case; Finance owns the underlying funding authority.

## 13. Screens and Data Views

Sourcing queue, event editor, supplier invitation/publication view, sealed inbox, opening register, compliance checklist, individual evaluation, comparison table, recommendation/award detail, exception and audit timeline. Supplier portal view is isolated and narrower.

## 14. User Experience and Human Behaviour

Show deadlines and time zone consistently, separate technical and price evaluation when configured, and make conflicts visible without revealing competitors. Buyers need an evidence checklist for offline offers; suppliers need clear receipt confirmation. Comparison explains normalized cost assumptions.

## 15. Notifications and Communications

Invite supplier; notify clarifications fairly per policy, approaching deadline, opening/evaluation tasks, award decision and no-award/cancellation. External notices are supplier-scoped; internal confidential evaluation stays internal. Delivery failures are visible.

## 16. Documents and Attachments

Specifications, RFx pack, supplier questions, original bids, surrogate source document, evaluation sheets, conflict declarations and award memo are scanned, versioned and retained with confidentiality labels. Published pack and bid submission versions cannot be silently changed.

## 17. Exceptions and Edge Cases

Supplier portal outage, physical bid at deadline, late bid, one bidder, no compliant bids, evaluator withdrawal, conflict discovered after score, price currency mismatch, PR line change mid-event, supplier suspended before award and reopened event.

## 18. Integrations, APIs, Events and Sandbox

Consumes PR allocation, supplier status, policy, documents and workflow. Emits `RFQPublished`, `SupplierQuotationReceived`, `SupplierSelected`/award and cancellation facts after commit with tenant/entity, event/bid/line versions, correlation/idempotency. Supplier channels use scoped authentication; sandbox tests sealed access, surrogate evidence, duplicate bid and late delivery.

P29 exposes `GET /sourcing/approved-principal-sources`, `POST /sourcing/from-approved-principal-sources`, `GET /sourcing/principal-source-drafts`, `GET /sourcing/{id}/principal-source-draft`, `POST /sourcing/{id}/principal-source-cancel`, and actor-owned `GET /sourcing/principal-command-receipts/{operation}/{key}`. It emits only internal `sourcing.draft_reserved.v2` and `sourcing.draft_cancelled.v2`. All writes require a UUID idempotency key; a conflicting payload returns conflict and a lost response is recovered with the original key. No RFQ publication event is emitted.

## 19. Audit, Security and Privacy

Append invitation, publication, supplier response time, opening, individual scores, conflict, recommendation and award. Restrict sealed bids, competitor data and evaluator identity as policy requires. Export/privileged reads are logged; no normal user deletes submitted evidence.

## 20. Reports, Dashboards and Metrics

Competitive coverage, bidder participation, cycle time, single-source/emergency frequency, evaluation turnaround, no-award reasons and savings baseline with declared method. Do not call estimate-versus-award difference savings without approved methodology.

## 21. Configuration and Localization

Methods, quote minimum, publication duration, business calendar, evaluation templates/weights, mandatory criteria, late-bid rule, currencies, locale and supplier channels are effective-dated by entity/category.

## 22. Non-Functional Requirements

Deadline and seal enforcement uses trusted server time; supplier submissions are idempotent and securely stored. Concurrent evaluators cannot overwrite scores. Accessibility, upload limits, availability near close and recovery targets need pilot agreement.

## 23. Postconditions

Award records approved supplier and line quantities/value with complete evaluation evidence and source PR links; no-award releases allocation or leaves an explicit reroute task. PO/contract conversion sees only approved award.

## 24. Failure and Recovery

Failed publication remains Pending, not Open. Unknown bid upload outcome is checked by submission key; supplier gets receipt only on durable save. Failed award handoff retries by key; sealed contents remain protected through outages.

P29 uses one owning transaction and sorted PR locks. A stale source version, reduced allocation balance, changed current authority, changed scope, or mismatched retained digest blocks before a new hold or release. On an uncertain response the client checks its actor-owned receipt before another POST. Cancel keeps the original source evidence and releases each exact allocation once.

P29 downgrade must first lock and inspect every sourcing event, draft evidence, demand binding and command receipt across all tenants. Any principal version 2 row vetoes Down with SQLSTATE `55000`, including a cancelled draft and its retained evidence. The guard runs before worker-only constraints or columns are restored; an operator must not delete history to force a downgrade.

## 25. Acceptance Criteria

Given unpublished criteria, bid submission is unavailable. Given a sealed bid, evaluator cannot read it before authorized opening. Given surrogate entry, original source and times are mandatory. Given unresolved conflict, score/award blocks. Given approved award, quantities cannot exceed PR allocation.

## 26. Test Scenarios

Test RFQ/RFP/tender/direct/sole-source routes, no/one/many bids, late/offline/surrogate submission, sealed access, independent scoring, currency normalization, conflict, supplier suspension, duplicate event replay and deadline boundary.

P29 acceptance additionally requires real workerless Core buyer/requester/checker accounts; wrong company/branch/current role; schema 1/2 identity downgrade and known-alias conflict; edited source digest; mixed currency or method; partial quantity and competing holds; stale version; unknown Create/Cancel response recovery; exact zero remaining balance; and proof that no supplier invitation, Finance posting or publication is created. Generated migration, exact PostgreSQL 17 constraint capture and finite record/execution role checks are release gates.

For the genuine P29 migration rehearsal, create a principal version 2 source draft in a private PostgreSQL database, cancel it, then attempt P29 Down. Assert SQLSTATE `55000`, unchanged migration version and retained event/evidence/binding/receipt rows. Repeat with each version 2 table populated independently where the schema permits, and verify a worker-only database can migrate Down. These are database acceptance checks, not source-string assertions.

## 27. Ownership and Dependencies

Sourcing lead owns method/evaluation; governance owns award authority; supplier steward owns eligibility; Finance owns funding. Depends on PROC-01/02/03 and CFG; Contracts/PO consume award.

## 28. Open Decisions and Assumptions

Approve sourcing thresholds, tender legal process, bid seal/opening authority, surrogate channels, scoring model, conflicts policy, savings method, supplier portal timing and retention. Owners: Procurement governance, Legal/Compliance and Finance; resolve before implementation.

## P30 child-feature delta — governed publication and manual supplier quotation

**Status:** Design and source preparation only. **Hierarchy:** Procurement → Strategic Sourcing → RFQ Publication and Manual Supplier Response → Approve Publication / Record Quotation Draft. P29 internal principal drafts must pass their separate private acceptance and be published before this capability is enabled. This delta does not authorize supplier transmission, submitted bids, evaluation, award, PO creation, or payment.

### 1. Feature Identification

`PROC-06-P30`; operations `P30-01` prepare and independently approve a publication, `P30-02` record the internally published RFQ and selected supplier audience, `P30-03` record a manually obtained supplier quotation as Draft, `P30-04` revise/cancel that Draft. Publication and quotation capabilities have distinct default-off readiness checks and evidence versions; old worker RFQ draft evidence remains version 1.

### 2. Purpose and Business Outcome

An approved internal RFQ becomes a fixed, reviewable request for prices, with an approved supplier audience. A buyer can preserve a supplier's offline quotation as an internal Draft against those exact RFQ lines. Neither record by itself claims that a message was delivered or that a supplier formally submitted a bid.

### 3. Scope and Exclusions

This increment covers P29 principal RFQ drafts only; current Core principals may have no HR employee. It includes independent approval, a published snapshot, an approved current supplier shortlist, manual delivery tracking, and draft quotation capture/read/revise/cancel. Supplier portal access, email/WhatsApp sending, bid submission/sealing/opening, comparative evaluation, award and Finance effects are excluded until their own full contracts exist.

### 4. Parent and Related Features

Parent `PROC-06`; source authority is the P29 RFQ's retained P25 approved PR line, Finance check and held allocation. Consume Core identity, branch/action scope and approval cases, Procurement `SOURCING` policy, supplier approval status, and shared document service only when its owner-access contract is available. Later bid submission and evaluation consume the published snapshot and quotation Draft.

### 5. Actors and Participants

The RFQ buyer prepares publication and manually records a supplier quotation; a separately authorized named Core reviewer decides publication. Supplier master owner confirms orderable supplier state. A supplier is an external source of an offline offer, not an authenticated actor in this increment. Auditor reads retained versions. No employee or worker record is mandatory.

### 6. Entry Points and Triggers

An authorized buyer opens a verified P29 Draft and explicitly requests publication review. The reviewer receives a Core assignment. After approval, the buyer records an external delivery attempt or manually obtained quotation using the published RFQ number and an approved shortlisted supplier.

### 7. Preconditions

The P29 Draft remains current, uncancelled and sourced from genuinely approved competitive demand. All selected holds and source digests restore successfully; supplier shortlist members are active, approved and within the selected company. A published/effective `SOURCING` policy has reviewed principal identity evidence, explicit audience and limits, a named reviewer with current Core action/branch authority, and no self-approval. Required criteria, pack, close time and supplier count are checked against the policy; missing rules or evidence block rather than default.

### 8. Workflow

Buyer freezes a candidate snapshot containing the exact RFQ/PR lines, method, terms, criteria, schedule, source digests, supplier shortlist and current policy version. Core opens a named independent review case; return sends it back for a new numbered candidate, rejection ends the attempt. Approval commits a publication fact and immutable snapshot atomically with the Core decision and Procurement audit/outbox. The buyer may then record individual **manual** supplier contact attempts; status distinguishes Attempted, ConfirmedReceived and Failed, with channel, time, actor and evidence. A supplier offer arriving by an allowed channel creates a quotation Draft linked to one shortlisted supplier and the exact published lines; buyer may revise or cancel under CAS until a future controlled Submit workflow exists.

### 9. Status and State Transitions

RFQ: Draft → Publication Pending → Published; Publication Pending → Returned/Rejected; Returned → Publication Pending with a new candidate; Draft/Returned → Cancelled under source-release rules. Published requirements and source references are immutable; further change needs a separately approved amendment. Shortlist delivery is tracked separately from publication: NotAttempted → Attempted → ConfirmedReceived/Failed; a retry is a new attempt. Manual quotation: Draft → Revised Draft → Cancelled; Submitted/Open/Evaluated/Awarded remain unavailable. Core case status is not inferred from the RFQ status field.

**P30 bounded implementation checkpoint:** The first internal publication creates shortlist rows in `ReadyForManualDelivery`. A buyer can record one externally reported contact for a shortlist row; the event remains `Published`, and the invitation becomes `ManualContactRecorded`. The record stores channel, reference, reported time and recorder; `SentAt` and portal delivery fields stay empty. The Draft quotation flow requires that contact and supports one buyer-owned draft per invited supplier, in-place draft line revision with an append-only full snapshot, and cancellation. Follow-up contact attempts, supplier-submitted quotations, evidence-document attachment, closing/deadline enforcement, evaluation and award require separate contracts and remain unavailable. The publication and Draft quote capability stays off until the owning P30 migration and physical readiness capture are complete.

### 10. Permissions and Data Scope

Require current `procurement-sourcing-publish` for buyer submission and `procurement-sourcing-publication-approve` for assigned reviewer, with selected company, branch, record, supplier and Core case scope rechecked at decision time. The reviewer must differ from the buyer and from forbidden requester aliases; delegation cannot exceed current authority. A buyer may read only assigned RFQs and their shortlisted supplier's quotation Drafts; supplier users gain no access in this increment. Current scope checks precede receipt replay and pagination.

### 11. Business Rules

`BR-PROC-SRC-P30-001`: No publication from legacy ad hoc, cancelled, altered, unapproved or unverified demand. `002`: No implicit threshold, quote count, reviewer or supplier audience. `003`: A Core approval is distinct from the buyer's preparation and from supplier contact. `004`: `Published` records a fixed internal business fact and does not set `SentAt`, delivery success or supplier acknowledgement. `005`: One manually entered quotation Draft is supplier scoped and uses exact `numeric(18,4)` quantity/price plus exact derived totals; no float conversion or silent rounding. `006`: A quotation line cannot exceed the published line quantity, and all lines reference the same RFQ and currency. `007`: Revision/cancellation preserves prior offer versions and source times; no silent overwrite of the original external evidence.

### 12. Data Fields and Ownership

Procurement owns publication candidate/version, snapshot JSON and digest, P29 source/hold references, Core case/decision links, `SOURCING` policy ID/version/digest, buyer and reviewer principal IDs with optional historical worker aliases, explicit supplier shortlist, manual contact attempt, quotation Draft/line versions, source channel/reference, timestamps, correction reason and command receipts. Core owns current people, roles, branches and approval decisions. Supplier Management owns approval/active status. Shared Documents owns scanned file bytes and access; Procurement stores verified references only. Finance owns funding; no payable or payment field is created here.

### 13. Screens and Data Views

Keep the imported RFQ wizard and detail tabs. Add a publication readiness checklist, named reviewer and supplier shortlist to the existing Terms/Documents/Bidding/Team areas when backed by the API. Detail shows Pending/Returned/Published with Core decision evidence, the fixed requirements version, separate manual delivery attempts and scoped quotation Drafts. Existing unavailable controls remain visible until their server actions are verified.

### 14. User Experience and Human Behaviour

Use separate actions for “Request publication approval”, “Record manual delivery attempt” and “Record quotation Draft”. Explain that internal publication does not contact a supplier. On a returned review, retain the buyer's input but require a new candidate and current policy/source validation. Show the supplier's own currency and an explicit mismatch error rather than a converted total.

### 15. Notifications and Communications

Core may notify the named internal reviewer through the shared notification service after case commit. No external email, SMS, WhatsApp, portal invitation, or supplier acknowledgement is sent or implied. A manually recorded contact attempt is a buyer-attested event with evidence and delivery state, not a notification delivery receipt.

### 16. Documents and Attachments

A publication pack and original offline quotation document are required only when the configured policy says so. References are accepted only after shared document ownership, malware status, supplier/RFQ scope and immutable version are verified server-side; until that contract is integrated, document IDs are rejected rather than treated as proof. Store hash, classification and retention reference in Procurement, not document bytes.

### 17. Exceptions and Edge Cases

Handle changed PR source or Finance evidence, expired policy, inactive buyer/reviewer, reviewer alias conflict, suspended supplier, insufficient shortlist, same supplier twice, changed contract/category, late or unverified manual contact, quote for another RFQ line, duplicate supplier reference, currency mismatch, partial quote, stale CAS and unknown response. A failed external contact never rolls back a published RFQ, and no automatic retry sends a message.

### 18. Integrations, APIs, Events and Sandbox

Reuse the existing Procurement execution transaction and Core case engine; add versioned publication and quotation endpoints only after owning migration and finite readiness. Every mutation requires UUID `Idempotency-Key`, actor-owned GET recovery, exact CAS, scope/permission recheck, append-only audit and outbox. Proposed committed facts are `sourcing.publication_requested.v1`, `sourcing.published_internal.v1`, `sourcing.manual_contact_recorded.v1`, `sourcing.quotation_draft_recorded.v1`, and `sourcing.quotation_draft_cancelled.v1`. Event names become authoritative only with implemented schemas. No `RFQSent`, `SupplierBidSubmitted`, `Awarded` or payment event is emitted.

### 19. Audit, Security and Privacy

Record actor, Core decision, policy version, source/supplier IDs, old/new state, reason, timestamp, immutable snapshot digest and command correlation. Keep buyer/reviewer independence and tenant/company/branch isolation at database and HTTP boundaries. Supplier quotes are commercially confidential; search, export and activity views must follow the same scope. Do not log document contents, personal contacts, quote bodies or credentials.

### 20. Reports, Dashboards and Metrics

Count publication requests, returned/rejected/approved cases, internally published RFQs, manual delivery states and quotation Drafts by entity/category/period. Draft offers do not count as bidder participation or competitive coverage. Publish metrics only with defined denominator, business date, currency basis and owner.

### 21. Configuration and Localization

The effective `SOURCING` policy supplies quote minimum, audience, required pack/criteria, close time, reviewer and authority; no hard-coded value is introduced. Store trusted UTC instants plus company zone for display, exact currency and UOM references, and per-entity numbering. A missing zone uses the documented Core UTC default.

### 22. Non-Functional Requirements

Core approval and Procurement publication must commit atomically across the already shared transaction; if that cannot be proven, capability remains off. Retained snapshots are immutable and bounded. Enforce no-store sensitive reads, bounded paging/attachments, exact decimals, accessible denied/loading/recovery states and finite role/database rights. P29 and this increment retain separate rollout flags.

### 23. Postconditions

Approval produces one internally Published RFQ with pinned source, policy, Core decision and shortlist, while PR holds remain unchanged. A manual quotation Draft is buyer-owned, supplier linked and editable only through revision; it creates no award, PO or Finance handoff. Rejected/returned attempts preserve evidence and do not publish.

### 24. Failure and Recovery

Unknown mutation outcome retains only the original operation/key/record scope for GET recovery; it does not resend POST with a new key. On DB or Core failure, no partial publication or quote should survive. Supplier contact failures are explicit attempts with reason and may be followed by a separate attempt. Historical policy, approval and offer evidence remains readable after an actor is deactivated, subject to auditor scope.

### 25. Acceptance Criteria

A workerless authorized buyer can request review on an approved P29 RFQ; a distinct current Core reviewer can return/approve under a genuinely published `SOURCING` policy. The same person or a stale/unauthorized reviewer cannot approve. Published snapshot, Core decision and audit are linked and exact; no external delivery is claimed. A buyer can record/revise/cancel a scoped manual quotation Draft against an approved shortlisted supplier using exact amounts. Legacy worker evidence and all unavailable later transitions remain unchanged.

### 26. Test Scenarios

Test private PostgreSQL up/down with retained publication/quotation veto; principal no-employee happy path; missing/wrong policy, source, supplier, reviewer, branch, tenant and finite grants; self-approval; duplicate/idempotent request; lost response GET recovery; changed source and stale CAS; exact maximum amounts; denied document reference; no email/portal dispatch and no award/PO/Finance writes. Include normal-cookie browser checks in the imported RFQ presentation after the owning API is accepted.

### 27. Ownership and Dependencies

Procurement product owns policy choices and supplier-contact practice; Procurement engineering owns RFQ/quotation records; Core owns identity, branch and approval case; Supplier Management owns eligibility; Documents owns file validation. Release waits for P29 publication/acceptance, an independently reviewed principal `SOURCING` policy lifecycle, generated migration and private finite-role/normal-cookie proof.

### 28. Open Decisions and Assumptions

Business must set minimum quotes, approved shortlist rules, required documents, close-period rules, allowed offline channels, whether an internal publication may precede any actual supplier delivery, and amendment/withdrawal authority. Engineering must confirm document owner-access API and exact Core case source/version contract. No answer is inferred from the original local demo wizard.

### P32 commercial completion source audit

The next bounded principal quotation submission/closing/evaluation/award contract
is [PROC-06-P32](PROC-06-P32-PRINCIPAL-COMMERCIAL-COMPLETION.md). It records the
actual P30 gap: required pack/criteria/closing controls are still rejected by
publication, and the real quotation path creates internal Drafts only. Existing
published P30 events cannot silently acquire frozen criteria/deadline authority.
P32 remains source-only/default-off until its independently reviewed controls,
owner integrations, genuine migration and finite runtime acceptance are complete.
