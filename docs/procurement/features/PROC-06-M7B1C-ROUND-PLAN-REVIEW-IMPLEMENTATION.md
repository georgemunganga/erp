# PROC-06 / B1c — Commercial round plan review and publication

**Document type:** Child feature implementation contract and acceptance plan  
**Status:** Draft engineering baseline; business policy and production activation pending  
**Source reviewed:** Published ERP main `c02f90a33fbce0983a8520d643098c1662a0ded5`, including the merged Plan source and shared Core read guards. The Plan production cutover remains a separate release gate.
**Parent:** [B1 commercial round plan](PROC-06-M7B1-COMMERCIAL-ROUND-PLAN.md). This delta inherits its authority, confidentiality, lock order and 28-section standard.

## 1. Feature Identification

`Procurement → Strategic Sourcing → Commercial Round Plan → Submit / Review / Publish`. This is the smallest complete transition from an existing B1 draft to an independently approved **internal** plan. It does not publish the RFQ to suppliers.

## 2. Purpose & Business Outcome

A scoped planner submits one immutable plan version; a different, currently assigned Core approver returns, rejects or approves exactly that version. Procurement records the owned result and allows an authorized user to reconstruct the decision and recover each original command response.

## 3. Scope & Exclusions

Implement Submit, assigned review task/detail, Return, Reject, ApproveAndPublish, original-key recovery and retained history. Supplier invitation, sealed offers, bid opening, evaluation, recommendation, award, PO and AP remain later slices. A Published plan must leave the enrolled source event's M4 publication barrier in force.

## 4. Parent & Related Features

Use B1 Create/Revise/Cancel, M4 source/PR holds, Core approvals, current Core identity/branch grants, shared policy publication, shared audit/outbox and actual Core document-use pins. Preserve existing M7A quotation draft meanings.

## 5. Actors & Participants

Planner: current scoped `round-plan-submit` actor; reviewer: a different current principal with a matching assigned Core case/stage and `round-plan-review` authority; policy publisher: existing Core policy administrator. Worker link is optional. Historical worker alias is retained as an additional self-review denial signal.

## 6. Entry Points & Triggers

Existing `POST /api/procurement/v1/sourcing/{eventId}/round-plan/submit` and `/decisions` currently return `503 RoundPlanReviewUnavailable` in `ProcurementCommercialRoundRoutes.cs`. Replace those handlers and add a finite assigned-task/detail read path. Review is user initiated; elapsed SLA never implies approval.

## 7. Preconditions

Existing enrolled Draft plan and immutable current revision; current explicit event branch for writes; unchanged eligible source and PR holds; current Core principal/action/scope; genuine schema and finite RECORD profile. Submit additionally needs one applicable approved versioned round-review policy, complete plan inputs and actual required document uses. Missing business configuration blocks Submit, while draft CRUD remains available under its own gates.

## 8. Workflow

1. Planner selects a Draft plan, submits its exact revision ID, plan/event versions and content hash with one UUID command key. The server reloads and validates all inputs under the adopted sorted PR → event → enrollment/plan → Core lock order.
2. In the same owning transaction, freeze the submitted content, pin the effective policy/version/evidence, create the authentic Core case/stage/assignment and append a scoped submission/result receipt. No independent generic status string counts as review.
3. Assigned reviewer reads the **submitted snapshot** through a task route that checks current Core assignment, branch and purpose; the general current-plan view cannot substitute for that view.
4. Reviewer sends Return or Reject with a reason, or ApproveAndPublish. Recheck current actor, assignment/delegation, maker/principal/alias separation, policy, source/holds, plan snapshot and expected Core versions. Commit the Core decision, owned result revision, audit/outbox and original response together.
5. Return creates an editable Draft result version while retaining the old submission/case/decision. A later Submit opens a new review round. Reject is terminal for this B1 plan except the adopted Cancel path. Published content cannot be revised.

## 9. Status / State Transitions

| Current → next | Command | Owned evidence |
| --- | --- | --- |
| Draft → InReview | Submit | Frozen submitted revision/hash + policy + Core case/assignment + receipt |
| InReview → Draft | Return | Exact Core decision/reason + result revision + closed review round |
| InReview → Rejected | Reject | Exact Core decision/reason + terminal result revision |
| InReview → Published | ApproveAndPublish | Exact approved submitted hash + terminal result revision |

An active review cannot be cancelled through the existing Draft Cancel route. A stale reviewer decision cannot change a returned or superseded round. `SourcingEvent.Status` remains Draft after internal plan publication.

## 10. Permissions & Data Scope

Require separate current Core `round-plan-submit` and `round-plan-review` actions plus verified tenant/company/event branch. Writes reject raw Company All; review task reads are assigned-record and branch scoped, and any Company All read intersects currently permitted branches. No role name, employee lookup, planner identity, participant listing or guessed case ID grants review. Recheck current permission and task assignment on original-key recovery before returning saved bytes.

## 11. Business Rules

Every decision binds a specific submitted revision and content SHA256; policy supersession cannot rewrite retained evidence, and a new decision may still fail current authority or security checks. Reviewer cannot be the maker by stable principal or known current/submitted worker alias. Return/Reject require trimmed reasons. Submit validates the whole plan only with explicitly published category/method/currency/criteria/team/schedule/document rules; no sample weights, deadlines, quorum or approval thresholds. Existing M4 enrollment publication barrier stays active after Published.

## 12. Data Fields & Ownership

Procurement owns an immutable `RoundPlanReviewSubmission` (tenant/company/event/branch/plan, round ordinal, exact submitted revision/content hash, source/policy/evidence/version, Core case/stage/assignment IDs and versions, maker/alias, server time) and an immutable `RoundPlanReviewOutcome` (submission ID, exact Core decision/actor/assignment/version, result revision/status/reason/time). Final names require model review. Reuse existing B1 result revision and actor-key receipt for each successful command, extending their operation/status constraints. Core owns case/stage/assignment/decision rows; B1 stores verified references, not a second approval engine.

## 13. Screens & Data Views

Add Submit/Review actions and current task/history panels inside the imported RFQ `RoundPlanPanel`, `RoundPlanEditor` and existing detail tabs. Reviewer detail must show the verified submitted snapshot, not a later mutable plan. Preserve shared ERP toolbar, shell, search, action placement and six-step RFQ editor.

## 14. User Experience / Human Behaviour

Use “Send plan for review”, “Return for changes”, “Reject plan” and “Approve internal plan”. Show exactly which plan version is being decided, the pending approver and reason. After uncertain save, keep the original key and offer recovery; do not issue a new command automatically. State plainly that supplier RFQ publication is still unavailable.

## 15. Notifications & Communications

Emit internal task/outcome references through existing outbox/notification services. Resolve current recipient access at read time. No supplier email, portal invitation or offer data leaves B1c.

## 16. Documents & Attachments

Submit may pin only existing Core document-use/version/hash/audience records whose current access and scan state are verified. If policy requires a document and the owning transport is unavailable, Submit returns a typed unavailable/incomplete result. A filename or empty use list is not proof of a document.

## 17. Exceptions & Edge Cases

Reject stale event/plan/case/assignment versions, duplicate or foreign command key, changed source/PR holds, missing published policy, incomplete/unsupported criteria, invalid dates, actor or branch revocation, self-review through either alias, inactive reviewer, returned-round replay and document loss. A mismatch commits no partial Procurement/Core graph. Historical reads remain possible when new processing is disabled, subject to current READ/scope.

## 18. Integrations / APIs / Events / Sandbox

Use strict typed JSON and existing `Idempotency-Key`. Submit body: expected event/plan versions, submitted revision ID/content hash, selected published policy reference/version and any required document-use pins. Decision body: submission ID, Core case/stage/assignment/version, expected event/plan versions, decision and reason. Server verifies every supplied reference. Provide finite `GET` assigned tasks, `GET` immutable submission detail/history and actor-owned `GET /round-plan/commands/{key}` for original response. New capabilities default unavailable on older backends; the UI retains B1 draft operations. Events reference scoped IDs/version/status only, without plan or offer bodies.

## 19. Audit / Security / Privacy

Append actor, role context, current scope, old/new state, submitted/result revision hashes, policy and Core decision references, reason, timestamp and correlation ID. Enforce authorization before lookup, count, history, replay, export and attachment access. No sealed offer content exists in B1c; do not use this release to weaken later confidentiality requirements.

## 20. Reports / Dashboards / Metrics

Expose scoped counts and aging for Draft, InReview, Returned, Rejected and Published plans and current assigned tasks. Define start/end timestamps and owner before publishing turnaround metrics. No sourcing savings or award metric is implied.

## 21. Configuration & Localization

Add a typed commercial round review purpose to the existing versioned operational policy administration and Core approval configuration; do not seed an approval chain or infer one from `SOURCING_PUBLICATION`. Use company currency/time zone and exact decimal strings from B1. Business approves applicable criteria, release schedule, duties and thresholds before Submit is enabled.

## 22. Non-Functional Requirements

Maintain tenant/entity isolation, finite SELECT/INSERT/UPDATE columns, scoped pagination, 2 MB body limit, no-store responses, bounded retries and cancellation. Original-key recovery must work with new writes disabled and must not open an unrelated record connection. Retention and backup objectives follow ERP policy.

## 23. Postconditions

Each command has one authentic Core+Procurement outcome, one immutable result revision, actor-owned original receipt and append-only audit/outbox. Return preserves prior submitted content; Published fixes the approved content and still blocks supplier-facing RFQ publication.

## 24. Failure & Recovery

Known validation/conflict returns 422/409 with no effects; unavailable owned schema/policy/Core/document returns typed 503; unknown transport outcome directs the actor to `GET` the original key. Same actor/key/body returns original status/body even after later versions; different payload conflicts and a foreign actor/key is hidden. Finance and payment state are unaffected.

## 25. Acceptance Criteria

1. Workerless scoped planner submits a complete exact revision under an actually published applicable policy; an independently assigned current Core reviewer returns it, and the planner edits/resubmits without altering the first round.
2. A second review approves the exact submitted hash; Procurement publishes one internal plan result and retains the Core decision, prior revisions, receipt, audit and outbox atomically.
3. Self-review, wrong branch/company, unassigned/inactive reviewer, revoked permission, changed worker alias, stale CAS, policy/document/source drift and incomplete plan are denied with zero partial effects.
4. Original actor/key recovery returns each original response after later changes; key conflict, foreign actor and lost response cannot create duplicate Core cases or plan results.
5. Enrolled event M4 publication remains blocked with either B1 flag off; existing unenrolled M4 behavior and retained B1 draft history remain valid.

## 26. Test Scenarios

Source/domain/Host tests cover the four transitions, return/edit/resubmit, exact hash and Core assignment binding, current authority/alias checks, failed policy/doc guards, concurrency lock order and typed errors. Genuine migration tests cover predecessor SQL preservation, actual old B1 row survival, exact new constraints/trigger/function definitions, empty Down/Up and populated `55000` veto. Native finite-role tests deny extra HRM/Core/Finance rights; normal Core-cookie multi-actor API and imported UI tests verify task display, same-key recovery and no supplier/Finance effect. Preserve accepted B1 controls and their evidence; never treat a mocked test as runtime proof.

## 27. Ownership / Dependencies

Procurement owns B1c records/API; Core owns identity, approval, policy administration and documents. Start source from published main `c02f90a33fbce0983a8520d643098c1662a0ded5`, which includes the merged Plan model; generate one genuine successor migration and snapshot from that current model. Existing B1 schema fingerprints, operational policy identity CHECK/readiness, payment/Plan and source publication gates need updated actual catalog captures where their real model changes. Plan's production cutover remains pending and is not evidence for B1c runtime readiness. B2 formal offers remains a separate milestone.

## 28. Open Decisions / Assumptions

Business must approve the round policy purpose, applicable category/method rules, approver assignments/quorum, criteria completeness/weights, document requirements, schedule, reviewer duties and retention. Engineering may implement typed draft/review plumbing with default-off processing and fail-closed missing configuration; no production policy values are assumed. Proposed table/API names above are review slots until the current-model migration and Core contract are accepted.
