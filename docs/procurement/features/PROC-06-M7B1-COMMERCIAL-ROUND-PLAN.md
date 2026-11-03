# PROC-06-M7B1 — Commercial round plan

**Status: Engineering-reviewed and adopted technical baseline, 3 November 2026, following Root and independent m6d review of PR161 under the user’s full-module implementation authorization. Business-policy settings remain proposed/unapproved. No implementation, migration, runtime acceptance or activation is claimed by this document.** Track under [issue 145](https://github.com/georgemunganga/erp/issues/145). This is the bounded planning prerequisite for later formal bid submission/opening/evaluation, not completion of M7B.

Inspected published baselines: ERP main `b42aa7355d196b5c3c678721890b6f1479851b63`; existing Procurement UI main `5dd51dff2ff74087d3f72682025ded071edd4782`. Re-fetch published main before implementation. Payment issue158/PR159 and Core scanner ownership remain separate. No held `98609167`, P32, P36 or P38 branch is an adopted prerequisite or merge instruction.

## 1. Feature Identification

`PROC-06-M7B1`, Procurement → Strategic Sourcing → Commercial round planning. Tier3 under [GW-ENG-STD-001](../../engineering/GW-ENG-STD-001.md). Procurement owns the plan and its commercial rules; Core owns identities, permissions, organisation, approval execution, document access and platform audit contracts. Business policy owner/approver remains to be named in section28. The user has authorized completion of Procurement; Root and independent m6d review have adopted the engineering constraints in this specification for the next implementation from published main after PR161 merges. This is not a request for blanket management permission to design or implement draft CRUD.

“Publish plan” below means freezing an approved **internal plan**. It does not mean publishing the RFQ, delivering invitations, opening bidding, accepting an offer or opening commercial content.

## 2. Purpose & Business Outcome

Define, independently review and freeze the rules of a new commercial round before supplier participation or access to offers. A buyer can explain which requirements, schedule, criteria, team, disclosure rules and approved policy will govern the round. Later submission/opening/evaluation must consume that exact plan version rather than mutable settings or an evaluator's assumptions.

The B1 completion outcome is a persisted, recoverable, immutable internal plan and a verified publication barrier. There is no score, ranking, recommendation, award, PO, financial reservation or supplier delivery outcome.

## 3. Scope & Exclusions

Include explicit enrollment of a never-published principal RFQ, structured plan draft/revision/cancellation, current Core participant selection, a published-policy binding, independent Core review, internal plan publication, retained plan/history/command reads and preserved original UI sections.

**Conservative first baseline:** only a current `SourcingEvent.ExecutionVersion=2`, status `Draft`, before any publication opening/review/decision/receipt, supplier invitation/contact, bid/quotation or other commercial-round evidence. Reject legacy execution0/worker-era events, already-published/cancelled events, pending publication and historical M7A events. Existing generic/ad-hoc RFQs do not acquire governed provenance from their labels.

Exclude formal submission/withdrawal, sealing/opening execution, deadline jobs, supplier portal, message transport, bid capture changes, document upload/scan, scoring, evaluator decisions, recommendations, award/PO consumption and historical reclassification. B1 can publish its internal plan, but **cannot start an enrolled RFQ's supplier-facing commercial workflow** until the later submission/confidentiality prerequisite is implemented and accepted. Existing unenrolled M4/M7A behavior is retained, without claiming those offers are sealed.

No restart is implicit. A defined future restart must create a distinct round/plan lineage, preserve all prior disclosure and offers, authorize cancellation/communications and explicitly address demand holds. Editing a date, incrementing `SubmissionVersion`, cloning a bid or hiding old rows is not a restart. Until adopted, restart/adoption endpoints do not exist.

## 4. Parent & Related Features

Parent: [PROC-06](../PROC-06-STRATEGIC-SOURCING-AND-AWARD.md), [M7 roadmap](../gap-closure/M7-SOURCING-TO-AWARD-ROADMAP-20261030.md), [M7A](PROC-06-M7A-PRINCIPAL-SUPPLIER-QUOTATION-DRAFTS.md). Consume M4 approved-source RFQ provenance, supplier eligibility and shared Core approval. Keep M6 invoice and M7A contact/version/audit retention contracts unchanged. Current M7A completion/release evidence is in the [native draft acceptance](../gap-closure/M7A-NATIVE-DRAFT-WORKFLOW-ACCEPTANCE-20261103.md) and [production workflow record](../gap-closure/M7A-PRODUCTION-WORKFLOW-RELEASE-20261103.md); older proposed/checkpoint wording does not undo that bounded accepted draft workflow. Later B2 owns formal submission/closing/opening; later evaluation and M7C/M7D own their separate decisions.

Current code references, not proposed implementations:

- `CommercialRecords.cs`: `SourcingEvent`, invitations, `SupplierBid`, lines and legacy evaluation/award records.
- `PrincipalQuotationStorage.cs`: accepted V3 exact commercial snapshot/restore; `PrincipalQuotationCommercialTerms.cs`: complete documentary terms and totals.
- `SourcingManualQuotationEvidence.cs` / `ProcurementManualQuotationMapping.cs`: immutable command/version records and owned scoped keys.
- `ProcurementPrincipalQuotationRoutes.cs`, `...Reads.cs`, `...Commands.cs`: current quotation endpoints and authority/recovery behavior.
- `ProcurementPrincipalSourcingPublication*.cs`: actual M4 publication opening, policy, Core case/decision and source-hold validation.

Held P32/P36/P38 material may inform a reviewed comparison only. Do not copy old EF snapshots, migrations, permission fixtures, policies, unrelated modules or runtime configuration into current main.

## 5. Actors & Participants

Planner/round owner, independent plan reviewer/publisher, named future opening officers, technical evaluators, commercial evaluators, conflict reviewer and permitted auditor are **current Core principal IDs** with explicit action grants and event company/branch scope. One person may have multiple roles only when the adopted conflict/segregation policy permits it. Reviewer must be distinct from the current plan maker for this adopted engineering baseline; optional retained/current Core worker aliases are denial-only independence evidence, never authority.

No employee, job title, payroll record, HR department, email address, supplier contact name or UI organizer label substitutes for a Core principal. A handler may differ from the RFQ maker when explicitly granted plan access. Suppliers are referenced parties; no supplier login or portal role is introduced by B1.

## 6. Entry Points & Triggers

From the existing governed RFQ detail, a permitted planner chooses Commercial round plan before publication. An explicit enrollment command pins the eligible source. Draft edits and submit-for-review follow. The assigned Core reviewer opens the plan through its current task; a successful independent decision publishes the exact reviewed plan. History and original-key recovery remain available after new work is stopped.

Enrollment is opt-in and auditable. A migration, meta response, read, application startup or branch change must never silently enroll an event or synthesize a policy/plan.

## 7. Preconditions

For new work: current authenticated Core account/action/scope; selected explicit permitted branch; genuine current schema and finite RECORD profile; no pending model/migration mismatch; verified source envelope/approved PR holds/method/currency; eligible event from section3; no competing enrollment/plan review; no raw Company All write marker. Current supplier eligibility is checked when a later supplier action occurs; saving a plan does not certify perpetual supplier qualification.

Drafts may record explicitly incomplete input. Submit/publish require a unique applicable **approved, versioned round-planning policy** with verified original evidence, named current participants, complete schedule/criteria/disclosure inputs, genuine document pins where required, and no unsupported promised workflow. Missing policy/reference/service means unavailable or incomplete, never “use sample policy”.

The existing `SOURCING_PUBLICATION` policy approves internal RFQ publication; it is not automatically an evaluation or commercial-round policy. Reuse requires an explicit adopted extension and versioned contract, not reuse of a convenient policy ID.

## 8. Workflow

1. Load current source under Core scope; explain why existing/published events cannot enroll.
2. Enroll a qualifying untouched Draft in one owner transaction, pinning event/source version and digest. Do not alter quantities or PR holds.
3. Save/revise a complete replacement plan draft set against CAS, preserving removed criteria/team entries in immutable history.
4. Submit a complete plan snapshot for independent review through the existing Core approval engine; pin policy, source, draft version, team/conflict declarations and actual document uses.
5. Reviewer revalidates current grant, branch, independence, Core assignment/case/stage/version and exact snapshot. Return/reject requires a reason; neither publishes. Approval and owned plan publication commit atomically with the authentic Core decision, receipt, audit and outbox.
6. Show Published plan and its immutable version. Enrolled RFQ publication remains blocked until B2's full cross-endpoint confidentiality and formal-submission capability is accepted.
7. Stop new plan writes independently of retained reads/recovery. Do not automatically replay an uncertain command.

No second workflow engine, generic “approval succeeded” string or notification establishes publication. Publication is an owned domain outcome with the complete verified Core graph.

## 9. Status / State Transitions

Proposed plan states below are new domain states, not existing `SourcingEvent.Status` values.

| From → To | Command | Required guard / effect |
| --- | --- | --- |
| No enrollment → Draft | Enroll/Create | Untouched eligible source; scoped unique enrollment; initial snapshot/receipt |
| Draft → Draft | Revise | Locked CAS; exact replacement of criteria/team/schedule set; append history |
| Draft → InReview | Submit | Complete plan; approved policy; genuine Core case/stage/assignment; frozen submitted snapshot |
| InReview → Draft | Return | Assigned independent reviewer; current Core decision and required reason; new plan version |
| InReview → Rejected | Reject | Same authority/decision checks; reason; terminal review evidence |
| InReview → Published | ApproveAndPublish | All publication guards below; exact reviewed snapshot; atomic owned outcome |
| Draft or Rejected → Cancelled | Cancel | Current cancel grant, CAS, reason, no active review/Published plan or later commercial effects; preserve enrollment/history |

Rejected/Cancelled plans cannot be edited/reused in B1; Rejected may transition only through explicit Cancel as above. A fresh attempt or withdrawal from active review needs an adopted replacement/cancellation contract before implementation. Published plan contents cannot change. B1 offers no Published→Draft/Cancelled/restart shortcut. **Adopted source-cancellation sequence:** explicitly Cancel the Draft/Rejected plan first; then allow only the existing authorized M4 source-cancel command, under its unchanged original-owner/current-scope rules, when no active review or Published plan remains. If review is active, it must legitimately Return/Reject before plan cancellation; never fabricate a terminal Core decision. Retain enrollment, revisions and command receipts after source cancellation. The plan command never releases demand: only the authentic existing M4 source cancellation releases the event’s exact PR holds once. Do not strand demand by permanently forbidding source cancellation merely because a Cancelled enrollment remains. A Published plan’s cancellation/restart remains outside B1.

## 10. Permissions & Data Scope

Proposed new exact action names (not grants installed by this document): `procurement-round-plan-read`, `procurement-round-plan-manage`, `procurement-round-plan-submit`, `procurement-round-plan-review`, `procurement-round-plan-cancel`, with Core `procurement-access`. Review requires a current assigned Core task and independent participant proof. Existing quotation-read/capture, generic sourcing-read/edit, administrator role or employee membership does not imply plan management or commercial disclosure.

**Adopted round-specific access boundary:** do not reuse M4 `PrincipalOwned` / `PrincipalOwnedEvent` as the round handler’s authority loader. In `ProcurementPrincipalSourcingSourceRoutes.cs`, those loaders require both `SourcingEvent.OwnerSubjectId == actor` and source evidence maker == actor; that is correct for their existing own-record contract but rejects a separately permitted round handler. Add a round-specific scoped loader that verifies the exact retained original RFQ/source maker and source provenance independently of the current actor, then checks the current handler’s explicit round action and permitted event company/branch/task access. Preserve original maker fields and their evidence; do not transfer ownership, impersonate the maker, substitute an employee or globally relax M4 ownership checks. A different handler needs a positive scoped round grant, not merely a guessed event ID. That round grant does not confer M4 source-cancel authority or permit source/event relabelling.

Tenant/company/branch and explicit event access apply before query filtering, count, paging, choices and materialization. READ may admit Company All only as a union of current permitted branches; writes require one explicit event branch. No body/query header can relabel owned evidence. Participant and supplier lookups are purpose-limited and scoped before search/count. Revalidate current actions, branch, account state and assignment in every material transaction and original-key recovery.

### Confidentiality matrix — mandatory across every surface

`P` = authorized plan information only; `M` = minimal receipt/participation metadata without commercial content or competitor counts; `O` = offer content only under a later, explicit authorized opening; `—` = deny. B1 stores **no round offers**, so `M/O` are future restrictions, not implemented routes. Plan rules/team/conflicts can themselves be restricted by task; `P` is never unrestricted directory access.

| Audience / purpose | Plan draft/review | Published plan, before commercial opening | After later authorized commercial opening |
| --- | --- | --- | --- |
| Scoped planner/owner | P on accessible event | P, M if separately granted | P; O only with explicit commercial-read entitlement |
| Assigned plan reviewer | P for assigned review | P for retained decision | P; review permission alone never O |
| Supplier / supplier portal | — in B1 | Later supplier-safe own invitation/own acknowledgment only; never competitor data | Own published result only under later policy; never implicit competitor O |
| Manual capture handler | No automatic plan access | Later own custody acknowledgment only; confidentiality policy must explicitly define any own-offer exception | Own permitted O only; no general competitor access |
| Technical evaluator | Assigned technical rubric only | Noncommercial technical projection only after technical-release gate; price-bearing text/docs denied | Technical projection; commercial O only under separate grant/phase |
| Commercial evaluator/opening officer | P for assigned task as granted | M only; opening action does not imply pre-opening read | O for exact authorized phase/round/assigned scope |
| Award approver / requester / RFQ maker | No automatic plan access | P only if explicitly granted; no O | Only separately granted decision packet; maker/role alone never O |
| Auditor | Explicit audit grant, scoped P | P and access audit; sealed O denied by default | Scoped O only with adopted disclosure authority; privileged access audited |
| Generic sourcing/quotation reader, admin, service or export client | Existing unenrolled contracts only | Enrolled round P only if independently entitled; no inherited commercial access | Same current action/phase checks as interactive users |

### Endpoint/surface coverage — no “detail-only” sealing

| Surface, including existing endpoints | Required treatment for an enrolled future round |
| --- | --- |
| Sourcing collection/detail, invitations, publication reads and all lookup/dropdown/search endpoints | Filter inaccessible events first; supplier-safe projections; no hidden offer amounts/counts/status-derived competitive activity |
| `/sourcing/quotation-sources`, `/{id}/quotation-drafts` | No supplier terms, prices, discounts, tax, totals, sensitive notes or forbidden counts before release; apply matrix before paging/aggregates |
| `/{id}/quotation-drafts/{bidId}` and `/versions/{version}` | Same current phase/role gate on **every historic version**, active and dropped lines, full terms/totals/document pins; a guessed version cannot bypass sealing |
| `/sourcing/quotation-commands/{key}`, replay of any contact/create/revise/cancel command, and future round command recovery | Current actor/scope **and current content disclosure**. Exact saved response bytes/status remain immutable; if they contain forbidden content, deny the response rather than rewrite/redact bytes and call it exact recovery. Minimal acknowledgment would require a new explicit contract |
| New round plan/list/detail/history/versions/commands/task routes | Plan/task projection only; receipt/request/snapshot fields never create an offer-content backdoor |
| Formal bid list/detail/history/submission/recovery, comparison, evaluation and award routes | Future routes unavailable until an adopted phase-specific contract enforces the same matrix; legacy endpoints cannot accept new identifiers as old records |
| Core document list/search/preview/download/use links, object URLs and document versions | Check actual document audience/round/phase in owning Core transport on every access; no filename/scan-state/pin metadata leak or persistent pre-signed bypass |
| Audit/history/activity/notifications/outbox consumers/logs/traces/errors | References and safe state transitions only; redact at producer and reader; no request/response bodies, commercial digests as guessable verification APIs, amounts, terms, attachment names or competitor identity leakage |
| Export/CSV/print/report/analytics/cache/background jobs/compatibility routes | Same field/row/phase authority, counts and timing side channels included; disable where unavailable; no eager preload of sealed data into browser DOM/network/cache |

Current M7A APIs expose full authorized Draft snapshots and actor-owned receipts. They do **not** implement this formal confidentiality model today. B1's untouched-event rule plus publication barrier prevents enrolling already exposed offers; later activation must modify/test every relevant existing surface, not merely add a separate sealed endpoint.

## 11. Business Rules

**BR1 — version meanings.** Current principal quotations use `SupplierBid.PrincipalCaptureVersion=2`, active lines likewise2, and `SourcingManualQuoteVersion.SchemaVersion=2` with `SnapshotSchemaVersion=3` for the current complete commercial snapshot. The database also retains the earlier snapshot2 contract; do not silently reinterpret it as V3. `BidVersion` is the existing `SupplierBid.Version` optimistic version pinned by its immutable quote version; it is **not a separate BidVersion entity**. Current `SubmissionVersion=1` and null `SubmittedAt` do not prove a supplier submission. Do not reuse those counters as round number, plan version or formal submission sequence.

**BR2 — immutable commercial lineage.** A future formal submission must explicitly bind `(tenant, company, roundId, publishedPlanId/version/hash, SupplierBidId, BidVersion, SourcingManualQuoteVersionId, snapshot SHA256)` with verified complete V3 restore, contact/invitation/source lineage and genuine document uses. Preserve supplier reference/date/validity, all documentary payment/warranty/delivery/tax terms, exact discounts/totals and historical dropped lines through the pinned version. A latest mutable bid or status string is insufficient. B1 does not create such a submission or change current uniqueness on event/supplier/SubmissionVersion. Preserve the existing exact quotation input precision/range checks (`numeric(18,4)` representation) and canonical decimal-text totals; no JavaScript Number, silent rounding, repricing, FX conversion or widening of the existing amount contract is authorized.

**BR3 — plan input validity.** Stable criterion IDs/codes; explicit stage and pass/fail versus weighted type; deterministic order; nonempty required descriptions/rubrics; no duplicate IDs or unknown/null members. Weight/scale/max-score/threshold inputs use exact decimal strings and explicitly adopted storage bounds. Sum validation uses the policy's declared normalization unit; that mathematical unit is not an approved distribution of weights. Pass/fail criteria have no silently assigned weighted score. Unsupported formula/criterion kind cannot publish.

**BR4 — approved policy versus UI defaults.** Section21 identifies authoritative inputs. No hardcoded “standard” technical/commercial ratio, minimum score, supplier count, legal threshold, award limit, deadline duration, tie-break rule, exception approval or exchange rate. Missing approval is not supplied by sample data, a tenant admin screen or an inherited RFQ policy.

**BR5 — time.** Pin Core company time-zone ID and unambiguous UTC instants; validate opening precedes closing and proposed release times do not precede the corresponding close. Reject ambiguous/nonexistent local time unless the user chooses a valid explicit offset. Server time determines publish admission; supplier-reported, received and entered times remain distinct future facts. The plan stores its schedule separately: current M4 sets `SourcingEvent.OpensAt` to the internal publication instant; that is not an adopted formal bid-opening timestamp and must not be reused as one. B1 executes no clock-triggered close/open and accepts no late bid.

**BR6 — criteria lock.** Publish freezes requirements/source-line set, criteria/rubric/weights, stage gates, schedule, opening/disclosure, participant duties, conflict/recusal rules and all policy references. No edit-after-reading-bids or settings refresh can change the adopted version. Reviewer approves exactly the submitted hash/version, never an independently edited current screen.

**BR7 — publication guards (all mandatory).** Acquire locks in the adopted cross-feature order: **sorted PR requests → event → enrollment/plan → Core rows**; reload current Core actor/task/aliases/scope; expected event/plan/case/stage/assignment versions match; eligible source and PR holds unchanged; no source publication/commercial evidence has appeared; current approved policy and immutable submitted evidence match; required participants/actions/branch/conflicts are valid; required actual document pins verified; full plan shape/calculations/capacity bounds valid; opening schedule not already elapsed; exact Core approval decision retained; no competing terminal decision; finite RECORD/schema profile passes. Persist one plan outcome + Core decision links + audit/outbox + actor-owned receipt atomically. Any mismatch means zero partial effects.

**BR8 — operational barrier, inseparable from first enrollment write.** B1 must guard the **existing** M4 submit/publication decision path for an enrolled event: internal plan publication alone never authorizes RFQ commercial publication. Until a separately accepted later admission predicate covers formal submissions/documents/all confidentiality surfaces, reject that transition. Do not bypass by cancelling/deleting enrollment, clearing a flag, using an ad-hoc/legacy route, changing event IDs or resubmitting an old Core decision. Unenrolled existing events keep their current contract. The barrier is scoped to the exact enrolled event and its retained lineage; it must not globally forbid legitimate creation of other RFQs under the existing approved-source quantity/allocation rules. Do not relabel, move or substitute an event/source to evade the barrier. The barrier must ship and be tested in the **same release as the first enrollment write**, including draft CRUD; it cannot be deferred from owner persistence to a later HTTP/Core-review package. An empty additive schema may be prepared separately, but no command, fixture, backfill or live draft path may create an enrollment while the deployed publication paths ignore it. This barrier and its rollback behavior are release-critical, even though B1 otherwise changes only plan workflows.

**BR9 — shared lock order.** Every enrollment/plan mutation, review/publication bridge and source cancellation that touches this graph follows **sorted PR requests → event → enrollment/plan → Core rows**, matching the M4 source-hold order rather than taking an event/Core lock first and later requesting PR locks. Obtain the source identifiers through a scope-checked preliminary read, lock PR request rows in stable ID order, then lock/reload the event and validate that its source set/version still matches before proceeding. Lock enrollment/plan next and Core case/stage/assignment rows last in their stable order; recheck current authority and source evidence inside that transaction. A stale or changed set aborts without effects; no hidden helper may invert the order. Read-only retained queries keep their accepted snapshot/read profile. Acceptance must interleave enrollment/revise/decision with M4 publication and source cancellation to prove no inversion, deadlock retry duplication or partial graph.

## 12. Data Fields & Ownership

All entities below are **proposed**, unmapped and unimplemented. Final names/DDL require source and model review; do not assign a fabricated migration number.

| Proposed owned record | Required typed fields / relations |
| --- | --- |
| `CommercialRoundEnrollment` | ID, tenant/company, exact principal event and branch, original source evidence ID/digest/event version, mode/protocol version, enrolled-by principal, optional denial-only alias, server time; one initial enrollment/event; retained even on plan cancellation |
| `CommercialRoundPlan` | ID/enrollment, status, existing row CAS version, maker/current accountable owner, draft and published revision references; no embedded mutable commercial bid JSON |
| `CommercialRoundPlanRevision` | Append-only ID, plan/version/predecessor, operation, current actor/optional alias, original source/policy pins, schedule, immutable canonical snapshot/hash and server recorded time |
| Plan criteria / participant / schedule projections | Immutable scoped revision children: each physical row has a distinct primary ID, separate from its stable logical criterion/participant/schedule ID; unique `(planId, revisionId, logicalId)` under tenant/company ownership. Preserve logical IDs across revisions without updating/reusing prior physical rows. Store deterministic order, type/stage/rubric/explicit weight+scale/threshold where applicable, canonical principal duties/conflicts and complete retained revision representation |
| `CommercialRoundPlanReviewLink` | Exact immutable submitted revision/content snapshot/hash plus distinct immutable result revision, actual Core case/stage/assignment/decision IDs+versions, original policy version/evidence, maker/reviewer principal and denial-only aliases, terminal outcome/reason/time. Published result pins the submitted snapshot/hash unchanged; never rebuild reviewed content using a changed row/version |
| `CommercialRoundPlanCommandReceipt` | Actor+tenant+company+key, operation/target, canonical request hash, exact original response TEXT/status/hash, exact immutable result plan revision ID/hash (never only a pointer to the mutable current header), time; no hidden offer bodies |
| Policy/document references | Exact approved policy record+version+evidence/hash; actual Core document/use/version/audience references and hashes only where owning transport can verify |

Use composite tenant/company FKs and branch/event binding, restrictive deletes, scoped unique keys (enrollment/event; plan/revision; actor/key across operations), expected adjacent predecessor/version checks and append-only guards. Source must have actual principal provenance. Bind every published child set/approval to the exact revision and event. A JSON snapshot is additional immutable evidence, not a substitute for typed relational ownership.

**Adopted revision/result rule:** every successful command binds its own exact immutable result revision and original response bytes/status. Submission freezes the reviewed content snapshot/hash. A later publication command may create a new state/transition result revision, but it must reference the original submitted revision and exact reviewed snapshot/hash; distinguish result-transition metadata/hash from reviewed-content hash. Do not rebuild the reviewed snapshot with a new CAS/result version, overwrite prior revision children, or recover an old command from the latest mutable plan.

Later round/submission/opening/evaluation entities and changes to bid uniqueness are **not B1 migrations**. Keep legacy evaluation/award records and worker-era FKs segregated; never insert placeholder `SourcingAwardId`/PO ancestry.

## 13. Screens & Data Views

Preserve current RFQ list toolbar, filters, selections, actions, shell and `RfqDetailLayout` tabs **Overview / Items / Related / Comments / History**. Preserve imported six-step wizard **Basic Details / Items / Terms / Documents / Bidding Preferences / Team**. Use current governed `PrincipalPublication`/quotation sections and `RfqPresentation.tsx`; `RfqWizard.tsx` is a layout reference whose local demo/store mutations must not become live data.

Add plan fields within the existing Terms, Bidding Preferences and Team sections; keep approved Items read-only, Documents truthfully unavailable until Core transport works. Expose internal plan status/review action and history within existing detail placement. Original supplier-offered quotation terms remain distinct from buyer plan requirements. No reduced parallel Live page, replacement procurement app or redesign is authorized.

A permitted review queue reuses shared Core task presentation. Show named current Core accounts without employee-only filters; display historical labels honestly without granting authority. Permission/capability-hidden controls must not trigger writer lookup effects. All cached data/refresh keys include actor/auth epoch/company/branch/plan revision and phase/capabilities.

## 14. User Experience / Human Behaviour

Use plain labels: “Save plan”, “Send plan for review”, “Approve internal plan”, “RFQ publication is not available for this round yet”. Explain that a saved/published plan has not invited suppliers or opened bids. Show draft completeness separately from approval and publication availability.

Retain loading, empty, validation, read-only, denied, stale, unavailable, success and uncertain-save/recovery states. Preserve user input on known validation failure; stale CAS requires review, not overwrite. Show lock consequences and require review before the material approval; reason fields for return/reject/cancel. Accessible form labels, keyboard focus, shared errors and mobile/desktop scrolling must preserve the imported layout. Never download confidential content just to hide it in the UI.

## 15. Notifications & Communications

B1 produces safe internal plan/task facts only. Transactional outbox references plan/revision and recipient task; authorized consumers resolve current access before rendering. No supplier dispatch, invitation email/SMS/portal delivery, “bid received”, score, award or Finance message. Delivery failure is a retryable notification outcome, not reversal or a duplicate plan publication. No commercial values/conflict details in subject lines or push previews.

## 16. Documents & Attachments

Core owns upload, content/version, malware scan, current access, audience and retention. A plan may reference only an actual verified document version/use pin. Plan draft attachments are optional only if the adopted policy explicitly permits them; if required transport is absent, publish is blocked. No empty GUIDs, fabricated use key, filename-as-evidence or synthetic clean-scan result.

Current M7A HTTP transport rejects nonempty document selections; its empty document set does not prove formal submission evidence. No M7B release may treat prior V3 snapshot pin fields as evidence that upload/download/confidential transport is implemented. Future technical and commercial packets need explicitly separated documents and text; an “unpriced” file that contains prices is not safe merely because its category says technical. Access/revocation and signed-link expiry must be enforced by Core as well as Procurement.

## 17. Exceptions & Edge Cases

Reject concurrent publication/enrollment, source version/hold drift, legacy aliases, null/duplicate child sets, cross-event criteria/participants, foreign document/policy versions, unknown fields, unsupported algorithms, numeric overflow/unrepresentable precision, expired schedule, revoked/inactive/reassigned reviewers and conflicting decisions. Retain earlier versions after full-set revise removes children.

A planner/reviewer becoming inactive cannot be bypassed by inventing an employee or changing an evidence actor. Future reassignment/delegation needs an adopted Core route with durable history; B1 initially blocks until a legitimate current assignment exists. A published policy may be superseded without rewriting retained plan history; new publication must follow the adopted effective-date/revalidation policy. An overdue review does not silently approve or extend dates. Error responses must not reveal inaccessible event existence or offer activity.

## 18. Integrations / APIs / Events / Sandbox

All following routes/capabilities are **proposed wire contracts**, not currently registered endpoints. Prefix proposed `/api/procurement/v1/sourcing/{eventId}/round-plan`. Keep explicit scoped cookies/auth, private no-store, versioned strict typed JSON, exact decimal strings and bounded server paging. Route naming is subject to contract review before implementation.

| Proposed operation | Request / successful effect |
| --- | --- |
| `POST` base | Enroll+create, expected event version, complete draft input, idempotency UUID;201 owned draft/receipt |
| `GET` base / `/versions/{planVersion}` / `/history` | Current-authority scoped plan/version/history projections;200; Company All allowed only for reviewed reads |
| `POST /revise` | Expected plan/event versions and full replacement child sets;200 new draft revision |
| `POST /submit` | Expected plan/event versions, published policy reference and complete snapshot digest;200 authentic pending Core review and linked revision |
| `POST /decisions` | Exact expected plan+Core case/stage/assignment versions, decision and applicable reason;200 atomic Return/Reject/ApproveAndPublish outcome |
| `POST /cancel` | Expected plan/event versions+reason;200 terminal draft cancellation |
| `GET /commands/{key}` (final location to review) | Original actor/key outcome under current READ/scope and disclosure; original exact status/body for acknowledged command, without requiring RECORD/INSERT/new-work switches |
| `GET` scoped participant/policy/document choices | Minimal currently permissible selections; only necessary action-specific lookups, never general HR/user/supplier directories |

Canonical request hash includes schema/operation/target/expected versions/full fields; one actor/key across operations. Within a reviewed owning transaction: resolve current READ/scope first, inspect a matching retained receipt before new-work flags/current policy/INSERT guards, verify exact original request+result/ownership, return original response bytes/status. Different body/target/action with same key conflicts. Another actor/key or foreign scope does not disclose the receipt. Saved historical result may differ from latest plan state and must stay truthful.

Proposed errors:401 unauthenticated;403 current action denied;404 inaccessible/missing object or foreign private key;409 stale/CAS/conflicting command/source/review;422 invalid/incomplete input;503 unavailable schema/policy/document/owning store. No errors dump request/response/policy or private values. Unknown outcome retains original key; check GET recovery, never automatic POST retry with a new key.

Events proposed: `CommercialRoundPlanDrafted.v1`, `...Revised.v1`, `...Submitted.v1`, `...Returned.v1`, `...Rejected.v1`, `...Published.v1`, `...Cancelled.v1`. Payload contains scoped identifiers/version/action/time, no offers or personal directory data. Atomic owner record+receipt+audit+outbox, deduplicated by actual command/aggregate version. Actual Core decisions use the shared owner event contract. No bid-opening, award or financial event is emitted.

## 19. Audit / Security / Privacy

Threats: retrospective sealing of exposed offers; field leakage through historical snapshots/replay/exports/documents; identity/branch spoofing; role/task escalation; maker reviewing own plan through a worker alias; changing criteria after seeing bids; silent default policy; stale concurrent approval; downgrade to old endpoints. Tests in section26 must attack each path.

Audit enroll/revise/submit/decision/cancel/read of privileged evidence, denied privileged attempts and changes to disclosure/policy configuration. Record actual principal, optional denial-only alias, tenant/company/branch, exact target/revision/state, reason, Core links and correlation/key. Business events are append-only; generic audit and special markers reconcile exactly. No credentials, commercial bodies, hidden criteria/conflicts or document bytes in logs/analytics. Server-side field minimization applies to every endpoint in section10, including internal/support clients; an ERP administrator is not a confidentiality bypass.

Data classification: restricted internal commercial planning/participant conflict information. Core/public vendor data remains Core/vendor-owned. Retention/legal hold and privileged access procedure need named business approval; until then, retain evidence, deny destructive rollback and grant no default break-glass disclosure. Encryption and access follow existing Core/ERP Tier3 operations, with explicit future sealed-content/key design before B2.

## 20. Reports / Dashboards / Metrics

B1 may report scoped plan status, review workload and aging from server facts. Definitions distinguish Draft/InReview/Published internal plan from RFQ publication and delivery. Apply access before totals/counts; no bid participation counts, price comparisons, savings, rankings, supplier responsiveness or award metrics. Existing Current page filter/export wording remains accurate; export is unavailable until the matrix and its API are implemented. Operational metrics exclude raw identifiers/payloads except protected correlation references.

## 21. Configuration & Localization

Separate authority from convenience:

| Input category | Required authority / behavior |
| --- | --- |
| Method, competition route and applicable monetary/supplier thresholds | Explicit published business policy/version/evidence and applicability; missing/ambiguous policy blocks publication |
| Criteria, stage order, rubrics, weights, pass/fail conditions, technical/commercial minimums, ties, normalization and rounding | Explicit approved policy or policy-authorized event-specific plan input, independently approved and frozen; never sample ratios or automatic lowest-price wins |
| Deadlines, late offers, extensions, opening quorum, duties, conflicts, recusals, review/delegation and exceptions | Explicit approved rules; B1 records the plan, later slices enforce submission/opening; unsupported exceptions block publication |
| Required documents, audiences, retention and disclosure | Explicit business/privacy policy plus actual Core capability; absent service never implies optional evidence |
| Currency/unit/catalog/branch/time-zone references | Existing current owning Core/Procurement contracts; no duplicated master, payroll tax rule or inferred exchange rate |
| Presentation preferences | Locale/date display, collapsed sections, page size and default criterion order may be configurable conveniences; they grant no authority or approved business values |
| Operational capability switches | New proposed `roundPlanRead` and `roundPlanRecord` independent, default false. READ needs current READ+finite SELECT; RECORD adds exact write grants/schema+feature+policy guards. No automatic role/grant/policy seed |
| Security/technical invariants | Tenant/company/FK/CAS/idempotency/append-only/no-Core-identity-write/no-Finance-write, default-deny disclosure and explicit version lineage are system controls, not user-editable defaults |

A prefilled value remains a proposal until validated and included in the independently approved immutable plan. Record policy provenance for each event override and its approving authority. Numeric limits/scale are implementation storage contracts to publish and test; they are not approved procurement thresholds. Keep exact amounts; any future normalization/FX/rounding engine is separately adopted before scoring.

## 22. Non-Functional Requirements

Tier3 isolation and confidentiality are release gates. Bound collection/search/history/document/snapshot sizes and query budgets using reviewed operational limits, not invented commercial restrictions. Use keyset pagination for potentially long immutable histories; return truthful unavailable/retry states rather than partial unverified history. No broad HR/password/payroll reads or purchasing INSERT over Finance facts; execution and Core authentication connections retain their separate finite duties.

Define measurable latency/availability, concurrency, RPO/RTO, maximum retained plan size and audit retention with the operations/business owner before activation; this draft makes no unsupported SLA promise. Protect schema fingerprints, owner/membership/SET-role checks and finite actual enlisted transactions. Flags-off history/recovery must work without plan INSERT/UPDATE. Shared UI targets existing accessibility/responsiveness standards.

## 23. Postconditions

A successful write leaves exactly one coherent scoped plan revision/result, required Core owner links and durable audit/outbox/receipt, with unchanged prior versions, quotations, PR allocations, Finance and unrelated records. A Published plan pins exactly the reviewed rules and no supplier delivery/submission/opening is inferred. Enrolled event publication is still blocked pending B2 acceptance. A failed command leaves no partial owner, Core, audit or outbox effects.

Retained reads report the original contract/state and current allowed actions separately. Existing M7A V3 receipts/snapshots remain byte-compatible and readable under their unchanged unenrolled contracts.

## 24. Failure & Recovery

Known validation/stale/permission/service failures preserve entered fields and expose a truthful scoped error. An ambiguous network/database outcome remains pending locally with only actor/scope/operation/key/target locator metadata. Original-key GET verifies persisted receipt under current READ/scope/disclosure before acknowledging success. Never infer failure from a timeout or issue another business POST automatically.

Schema migration plan: genuinely scaffold from accepted **current** model after design approval; additive tables/keys/constraints; reviewed current snapshot; fresh and retained Up, unused Down/Up, and retained-evidence Down veto. Never transplant held migrations or renumber previously applied history. Model/schema/fingerprint acceptance and exact finite ACL recipes must be generated/reviewed together.

Rollback after enrollment must retain the event-publication barrier: old code that ignores enrollment cannot safely accept RFQ publication traffic. Disable affected source publication writes or forward-fix while retaining new evidence; do not drop enrollment, rewrite published plans or remove current schema guards. Plan READ/recovery remains available when RECORD is off. Backups, restore proof, compatibility of existing M4/M7A releases and owner reconciliation precede production change.

## 25. Acceptance Criteria

1. A workerless permitted Core planner enrolls only a genuine untouched principal Draft and saves/revises it through the existing RFQ sections; a separately granted handler need not be its original maker.
2. Existing published/pending/legacy/contacted/quoted events cannot enroll, including races; all existing offers/versions/contact/publication/PR-hold evidence remains unchanged. A separately permitted handler uses the round-specific scoped loader, while original maker evidence and M4 own-record denials stay unchanged.
3. Missing/unapproved/ambiguous policy or required Core document capability cannot publish. No manufactured weights, thresholds, authority, employee aliases or document pins appear.
4. Independent assigned review publishes exactly its submitted source/plan/policy/document hash/version, or returns/rejects with reason. Self/alias review, revoked grant/branch/task and stale/multiple terminal decisions fail with zero effects.
5. Published plan and previous full-set revisions are immutable; removed criteria/participants remain recoverable. Distinct child physical IDs preserve stable logical IDs with scoped `(plan, revision, logicalId)` uniqueness; every command binds its immutable exact result revision and publication pins the unchanged submitted content/hash. No new submission, scoring, evaluation, award, PO or Finance record is created.
6. Every enrolled event is blocked at existing and compatibility RFQ publication/submit/replay paths until later confidentiality/formal intake admission. The first release permitting any enrollment write already includes this barrier, including draft CRUD. Changing flags or plan cancellation cannot evade it; unenrolled M4/M7A regressions pass.
7. Company All list/history/recovery applies current permitted scope before count/paging; explicit-branch-only writes and direct foreign tenant/company/branch/key attempts fail without leaks.
8. Original-key ambiguous-save recovery returns exact authorized original bytes/status once, with no duplicate graph. Another actor, changed payload/target/action and content-forbidden historical receipt cannot disclose it. RECORD-off/INSERT withdrawal preserves authorized READ.
9. Actual current-model migration/finite-role/native constraints, concurrent CAS/uniqueness, atomic Core+Procurement audit/outbox reconciliation and rollback-retention proof pass with preserved old data. Explicit plan cancellation followed by original-authorized M4 source cancellation releases only the existing event holds; retained enrollment/history remains. Active-review/Published plan source cancellation and unauthorized handler cancellation deny; legitimate other RFQ creation remains governed by existing source balances.
10. Original desktop/mobile UI, normal-cookie live API journey, denied/loading/empty/error/stale/recovery states and shared component parity pass; mocked tests, private native acceptance and production release are recorded separately. No CI success is claimed while hosted checks are billing-blocked.

## 26. Test Scenarios

| Layer | Required meaningful evidence |
| --- | --- |
| Domain/application | Production-used plan factory through actual scoped owner staging; strict shape/decimal limits/full child sets, distinct physical/stable logical child IDs and scoped revision uniqueness; deterministic canonical snapshot/hash; every rule/policy provenance change alters reviewed revision; publication guard truth table |
| Host/contracts | Current workerless Core identity/action/branch/task; separate planner vs RFQ maker through round-specific access, original maker preserved and unchanged M4 own-record denials; optional worker alias denial; missing policy/Core service; all proposal routes and direct/compatibility bypasses; Company All and stale/revoked actor; exact receipt status/bytes and cross-operation key conflict |
| Publication bridge | Existing M4 submit/decision and same-key replay, cancellation and legacy/ad-hoc IDs against enrolled vs unenrolled events; first-enrollment-plus-barrier same-release gate; concurrent enrollment vs M4 review/publication/source cancellation with sorted PR requests→event→enrollment/plan→Core locks; old binary/flags-off rollback traffic denied safely |
| Confidentiality | Parameterized audience×phase×ALL surface matrix from section10: list/count/search, source/detail, every old version/receipt/replay, documents/downloads, history/audit, exports/print/reports, notifications/logs/cache and jobs. No offer DOM/network preload; B1 has zero offers and cannot publish the event |
| Native PostgreSQL | Genuine migrations/FKs/unique keys/append-only/retention; finite SELECT-only READ and finite RECORD with no HR identity/Finance writes/no owner/member/SET; real Core cookies and same enlisted transaction; current schema owner changes/required SELECT withdrawal fail closed |
| Concurrency/recovery | Concurrent initial enrollment, revise, review decision and source cancellation; stale expected versions; same-key identical and changed bodies; deliberately lost response; no auto repeat; one exact immutable result revision and owner/Core/audit/outbox graph; publish preserves the exact submitted snapshot/hash across a changed result state/version |
| UI | Preserved wizard sections/detail tabs/list controls on desktop/mobile; named workerless choices; no writer lookups when read-only; incomplete/denied/unavailable states; original-key recovery after record switch/grant withdrawal; no demo/local persistence |
| Regression/operations | Existing M4 source/publication and M7A v3 terms/version/dropped-line/cancel/receipt compatibility; full old-row and allocation fingerprints; native temporary role/session cleanup; restored backup; disabled-mode/rollback barrier; exact source/build pins and independent reconciliation |

Scoring/late-bid/opening/award acceptance is explicitly deferred to later child specs. Use synthetic protected fixtures with approved test-only policy evidence clearly distinguished from production approval. Never use a fixture's chosen numbers as business defaults.

## 27. Ownership / Dependencies

Adopted bounded engineering implementation sequence. Build from the published prerequisite after PR161 merge; normal source/runtime/release review still applies, without a new blanket management-approval prerequisite to draft CRUD:

- **B1a technical baseline — reviewed/adopted:** Root and independent m6d review adopt the never-published enrollment boundary, current quotation protocol/version meanings, round-specific scoped handler access preserving the original maker, shared lock order, first-write publication barrier, immutable physical/logical child identity and exact result/submitted-snapshot pins, and explicit plan-then-original-M4 source cancellation with retained enrollment. Engineering may select and record remaining typed configuration/draft contract details under the existing implementation authorization. Unresolved production policy is unavailable; it does not prevent draft implementation. Submit/Publish stays closed until actual business policy/Core/document prerequisites exist.
- **B1b owner persistence + first-write safety:** current-model generated plan/enrollment/revision/receipt schema, complete constraints, native finite-role/read-write profiles and retention/rollback proof. **Before the first enrollment write, include the round-specific authority loader, shared sorted-PR→event→enrollment/plan→Core lock order and existing M4 publication barrier in the same release.** Do not expose draft/enrollment writes as a persistence-only release while deferring the barrier to B1c. No seeded business approvals/grants and no bid/evaluation migration.
- **B1c scoped HTTP/Core review completion:** remaining current-principal commands, authentic shared Core links, immutable history/receipt recovery, bounded purpose-specific lookups and adversarial endpoint tests. Preserve and extend the already-shipped publication barrier and common lock/access contracts; this package is not their first installation.
- **B1d existing UI + acceptance:** wire the imported RFQ sections/shared controls to those published APIs, then normal-cookie multi-actor/native reconciliation and separate paired release review. No frontend rewrite or production activation in this document task.

Root retains active payment issue158/native/SDK/deployment ownership; scanner task153 stays independent. Before assigning a dependent build, publish reviewed technical prerequisite ERP/UI merges and exact baselines. Engineering technical adoption does not require production business policy to exist first; incomplete drafts remain allowed, while Submit/Publish guards cannot be bypassed. Coordinate independent review and sufficient isolated hardware; do not interrupt another SDK/runtime cohort. Core owners must accept approval/document/identity contracts; Procurement business owner approves policy/disclosure. Operations owns backup/restore/rollback/monitoring evidence. No held branch wholesale merge is allowed.

## 28. Open Decisions / Assumptions

The engineering baseline identified in sections10/11/27 is reviewed/adopted by Root and independent m6d review under the existing full-module implementation authorization. Remaining detailed technical choices below may be selected and recorded by Engineering within those constraints; they are not a blanket approval hold on draft CRUD. Production business-policy inputs remain **unapproved**, not values to fill with defaults. Record the responsible owner, decision/review reference/date and adopted technical version as applicable. Business approval/evidence is mandatory before the dependent Submit/Publish action, not before configuration or draft CRUD code:

| Decision | Proposed safe boundary / required decision |
| --- | --- |
| Business owner and policy approval | Name accountable sourcing/privacy/engineering owners; approve event eligibility and internal plan maker/checker baseline |
| Policy contract | New typed commercial-round planning policy versus explicit reviewed extension of existing policy; applicability/effective-date and authorized event overrides; genuine policy evidence/Core workflow mapping |
| Criteria and commercial rules | Supported criterion/rubric/formula kinds; explicit approved weights/scales/pass gates/thresholds/ties/normalization, not hardcoded examples. No automatic scoring/award in B1 |
| Schedule and later late/extension rules | Which instants/documents are required to publish a plan; approved supplier receipt/late/reopen/extension policy belongs to the later intake slice |
| Team/conflicts/reassignment | Exact role duties/quorum/delegation, reviewer independence/alias changes, participant confidentiality and conflict handling; unresolved assignment paths remain blocked |
| Documents and sealing | Required plan evidence, actual available Core transport, future separate technical/commercial audiences, key custody/privileged disclosure/retention. Formal offer intake cannot begin without accepted controls |
| Initial event eligibility | Adopt conservative never-published/no-commercial-evidence baseline. Any migration/adoption of old events or restart is a separate explicit design/approval, never an exception inferred by developers |
| Enrollment lifecycle/publication barrier | Engineering-adopted: no supplier publication; first enrollment and barrier ship together; Cancel Draft/Rejected plan before unchanged authorized M4 source cancellation, no active review/Published plan, retained enrollment/history and only M4 releases PR holds. No source/event relabelling or global prohibition of legitimate other RFQs. Further cancellation/restart semantics remain separate; no unenroll/delete bypass |
| BidVersion / later submissions | Confirm future exact v3 version-pin and line/document lineage, formal submission/withdrawal/revision keys and uniqueness without overloading existing `SubmissionVersion` or rewriting M7A drafts |
| Technical contracts | Final action/capability/route/entity names, numeric storage/snapshot/query limits, migration/finite-role profiles, source-scope access rules and measurable SLO/RPO/RTO |

**Ready boundary:** the concrete engineering baseline is reviewed/adopted for configuration, schema/adapters and draft CRUD under the existing full-module implementation authorization; the next build starts from published main after this reviewed PR merges. Round-specific access, sorted PR→event→enrollment/plan→Core locks and the same-release first-enrollment publication barrier are mandatory constraints, not deferred choices. Record unresolved business choices as explicit unavailable/incomplete inputs; they do not impose a blanket management-approval prerequisite on that work. Actual approved policy, current independent Core review and required genuine document/disclosure capabilities are gates on Submit/Publish and later commercial activation. This technical adoption makes no business-approval claim and does not authorize invented policy, sealing claims, held-code merges or production activation.
