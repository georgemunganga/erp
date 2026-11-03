# PROC-05A — Governed procurement plan review

**Status:** Implemented under issue169 / draft PR171; genuine schema, actual executable/provider verification and bounded normal-cookie original-UI primary flow are recorded. Strict post-primary native retention/side-effect acceptance and production release remain pending. See the [current private checkpoint](../gap-closure/PROC-05A-PRIVATE-INTEGRATION-CHECKPOINT-20261103.md).
**Published prerequisite baseline:** ERP `556fbee2f8253f9803e627c70088421f81f4392e` (B1 PR165, PR176 and restored-CHECK PR179 merged). Tested Plan backend6e85/tree `c4d0703b357ba2674cd4baffd1e90e80a069ef7b`; actual private UI `3e2783b1f373857ba11681e73407c13ab09e30a8`. This source synchronization preserves that backend tree.
**Hierarchy:** Procurement → Procurement Planning → Plan Review → Submit, review, decide, withdraw.

This child feature completes the governance of an existing plan draft. It does not approve a budget, reserve money, create a requisition or authorize a purchase order. The approved plan-line-to-PR link is a separate follow-on contract.

## 1. Feature Identification

`PROC-05A`, one company/branch-scoped review round for one immutable submitted plan version. Procurement owns the plan and review outcome; the shared Core Approval service owns case, stage, assignment, decision and delegation mechanics.

## 2. Purpose and Business Outcome

Department planners can send a forecast to named reviewers, receive a reasoned decision, and see an Active plan only after the configured review path finishes. Every actor and version is reconstructible.

## 3. Scope and Exclusions

In scope: submit, two configured stage purposes (review and final approval), return, reject, withdraw, direct named assignments, queues/history, versioned command recovery and notifications through the shared outbox. Delegated decisions are admitted only after a separately verified shared Core delegation resolver supplies current authority and scope; this increment does not duplicate or assume it. Excluded: Finance budget truth, spending authority, supplier/PO actions, plan utilization, automatic PR creation, period close, amendment of an Active plan, and arbitrary new workflow engines.

## 4. Parent and Related Features

Extends [PROC-05](../PROC-05-PROCUREMENT-PLANNING.md) and existing `/api/procurement/v1/plans` Draft CRUD. Uses Core identity, organization branch assignments, roles/permissions, shared ApprovalCase, policy versions, audit and outbox. PROC-02 consumes approved plan-line references later.

## 5. Actors and Participants

Planner/maker; named review assignee; independent final approver; Procurement planning administrator who configures assignments and policy; auditor. A Core account need not be linked to an HRMS employee. A requester may have multiple roles, but cannot decide their own round. Delegate is a future actor only when the shared Core delegation contract is proven.

## 6. Entry Points and Triggers

Submit from a saved Draft; queue task on submitted case; review/approve/return/reject from assigned task; maker withdraw before completion; maker revise a Returned plan and submit a new round. A replay of the original command key is a recovery request, never an implicit new transition.

## 7. Preconditions

Active authenticated Core principal; current entity and selected branch assignment; explicit action grant; same tenant/entity/branch as the plan; valid Draft with at least one active line; published effective plan-review policy; named configured assignees and quorum; no self-assignment; no current pending case for that plan. A new Draft stores the selected branch and Core organization default currency from verified scope, never from client input alone. Existing branchless Drafts remain owner-only until `POST /plans/{id}/claim-scope` records a CAS-checked reason, verified selected branch and explicit confirmation of the plan's stored currency. An organization-wide claim requires a separate explicit Core permission; role names never bypass it. No HQ/default branch or replacement currency is inferred. Missing policy, scope, assignee or storage readiness blocks submission/decision with a typed response. A free-text financial-period reference is a forecast label until a Finance period contract verifies it.

## 8. Workflow

1. Planner reviews Draft, including estimated lines and branch, and sends **Submit for review** with expected plan version and command key.
2. Procurement pins plan version, line IDs/versions and exact decimal values, policy ID/version, branch, owner, stage assignments and a canonical snapshot hash. In one transaction it moves the plan to Submitted and opens `core_approval` case `SourceType=procurement_plan`.
3. Named review assignee checks the frozen forecast and records Approve or Return/Reject with required reason for non-approval. Review Approve advances the shared case to the final stage and makes the plan Reviewed.
4. Named independent final assignee decides. Final Approve completes the Core case and makes the plan Active atomically. The Core case state Approved and plan Active are the visible approval result; a separate unguarded Activate action is not created.
5. A Return completes the round and leaves a Returned plan editable by its maker. A revised submission creates a new pinned round, preserving prior decisions. Rejection closes that round and leaves the plan Rejected; a new plan or explicit future reconsideration contract is required.
6. Maker may withdraw a pending round with reason; the shared case/round becomes Cancelled/Withdrawn and the parent plan returns to Draft for edit and resubmission. Draft and Returned plans may use the terminal cancel route with a reason, current version, and a closed review round; prior round evidence remains intact. Cancelling an Active plan is outside this increment.

## 9. Status / State Transitions

| Current plan | Actor/action | Next plan | Shared case |
| --- | --- | --- | --- |
| Draft or Returned | Maker Submit | Submitted | Pending, Review active |
| Submitted | Assigned reviewer Approve | Reviewed | Pending, Final active |
| Submitted or Reviewed | Assigned actor Return | Returned | Returned |
| Submitted or Reviewed | Assigned actor Reject | Rejected | Rejected |
| Reviewed | Assigned final approver Approve | Active | Approved |
| Submitted or Reviewed | Maker Withdraw | Draft | Cancelled; round history Withdrawn |

Initial supported stage purposes are exactly Review and Final; assignees and quorum come from business configuration. No transition silently creates a Finance reservation. Returned or withdrawn Draft edits increment plan/line versions. Completed rounds are never reopened in place. Stale or contradictory commands produce conflict; identical original-key replay returns its recorded result.

## 10. Permissions and Data Scope

Add explicit Core actions `procurement-plan-submit`, `procurement-plan-review`, `procurement-plan-approve`, `procurement-plan-withdraw`, and `procurement-plan-review-read`; no bootstrap role grant. Existing plan Draft read/create/edit remains separate. Read and command admission intersects current Core tenant, entity, selected branch, active account, action grant and named assignment. Company All may aggregate only records in the actor's current authorized branch set; every write requires a concrete selected branch. Branchless legacy plans remain restricted to their owner or a principal with an explicit organization-wide claim permission until claimed. The maker can read their own rounds; assigned actors see only their tasks; an authorized auditor sees scoped history. No broad company read follows merely from an approval permission.

## 11. Business Rules

`BR-PLN-001` one pending case per plan; `002` only a frozen version may be decided; `003` no maker/final approver identity or linked alias self-approval; `004` no approval without published effective policy and named assignees; `005` direct named assignment only until current Core delegation is proven, then delegates cannot exceed the delegator's current action, scope, amount/period restrictions or effective dates; `006` return/reject/withdraw require reason; `007` final approval is forecast governance only; `008` a branch change or lost assignment fails closed; `009` no inferred default value threshold or auto-approval; `010` an unclaimed branchless plan cannot be submitted; `011` scope claim cannot alter currency or silently reinterpret historical estimates.

## 12. Data Fields and Ownership

ProcurementPlan retains its existing fields and gains a typed owning branch reference for new governed rounds. A legacy claim stores prior unknown branch, new branch, stored currency confirmation, actor, reason and expected version in audit. A plan-review submission pin stores plan ID/version, canonical line snapshot hash, submitted amount/currency, branch, Core case ID, policy ID/version, maker ID, stage assignment IDs, timestamps and status. A plan command receipt stores operation, actor, key, request fingerprint, result reference/JSON and timestamp. Existing shared Core Approval tables own case/stages/assignments/decisions/delegations. Procurement audit records own business transitions. Finance owns budgets and fiscal-period truth; Core owns account, branch and role truth. The existing `ApprovalInstanceRef` string cannot by itself prove the frozen plan/line scope.

## 13. Screens and Data Views

Extend the existing plan list/detail/editor with Claim scope for eligible legacy drafts, Submit, pending stage/assignees, Review, Return, Reject, Approve, Withdraw, round history and source version. Queue uses the shared approvals navigation and names the plan/financial period/branch. Every action is shown only when returned `allowedActions` and current permissions agree. Display **Forecast approved; budget not reserved** on Active plans.

## 14. User Experience / Human Behaviour

Show the exact forecast being approved, not a mutable current draft. Scope claim asks the user to confirm the saved currency and selected branch before resubmission; it never recalculates prior estimates from a changed organization default. Require clear reasons, pending actor and task age. A lost response shows the original key's lookup option and keeps the button locked until recovery; no duplicate Submit. Scope changes clear stale UI data. Terms remain plain: Send for review, Review plan, Approve forecast, Send back, Reject, Withdraw.

## 15. Notifications & Communications

After commit, outbox events trigger review/final tasks, return/reject/approval/withdrawal notices, and overdue task reminders when a configured SLA exists. The initial policy schema has no SLA/reminder configuration and no Plan notification consumer is claimed; delivered notices and reminders remain unavailable pending adoption and verification of the existing shared delivery services. No notification implies an approval. Recipients are the current authorized assignees or maker, and messages omit sensitive line values unless the channel is authorized.

## 16. Documents & Attachments

Existing attachment references may support the forecast; attachment-required policy must be configured and checked before submission. No invented proof file, upload or signature capability is claimed. Retain document IDs/version in the submitted snapshot where required. The initial schemaVersion 2 policy has no attachment requirement field and rejects unsupported fields; attachment-required policies remain unavailable until required Core document verification and snapshot contracts are adopted.

## 17. Exceptions & Edge Cases

Missing/deactivated assignee; unclaimed legacy branchless Draft; maker also configured as reviewer; changed branch assignment; policy superseded before submit; policy changed after submit; duplicate command key with changed payload; concurrent decisions; changed line after pin; canceled Draft; returned plan resubmitted; current scope lost while a page is open; Finance outage. A policy change after submission does not rewrite the pinned case. Ordinary replacement closes the original effective window; an existing round retains that Published/Superseded version and its verified historical publication proof, even after expiry. New submissions still require a current Published/effective policy. Retired (currently unused Draft retirement only), Revoked or unknown states fail closed. Current access/SoD is still checked at each action. Delegation requests remain unavailable until the shared Core resolver and denial tests exist.

## 18. Integrations / APIs / Events / Sandbox

Planned API: `POST /plans/{id}/claim-scope` (expected plan version, verified selected branch, stored currency confirmation and reason); `POST /plans/{id}/review-submissions`; `GET /plans/{id}/review-history`; `GET /plans/review-tasks`; `GET /plans/review-submissions/{submissionId}`; `POST /plans/review-submissions/{submissionId}/assignments/{assignmentId}/decisions`; `POST /plans/review-submissions/{submissionId}/withdrawals`; `GET /plans/commands/{key}` for actor-bound recovery. A review task returns both `caseId` and its distinct verified `submissionId`; the latter opens the immutable submission detail, and an invalid or missing pending round fails closed. The four review GET paths admit explicit Company All only with a current permitted-branch intersection; all plan mutations require a selected branch, and a raw All header cannot fall back to one. ClaimScope returns a canonical camel-case JSON receipt (`id`, `branchRef`, `currencyCode`, `version`, `scopeClaimed`). Commands require exactly one `Idempotency-Key` UUID and expected source/case/assignment versions as applicable; the legacy X header is not accepted. Use one Procurement/Core transaction and shared `ApprovalEngine`; publish versioned PlanSubmitted, PlanReviewed, PlanReturned, PlanRejected, PlanApproved and PlanWithdrawn facts through the outbox. Events are business facts, not Finance postings. A sandbox or private clone must use genuine Core cookies and finite roles. Metadata exposes separate `planReviewRead` and `planReviewWorkflow` booleans: current scoped retained reads can remain available while new work is disabled or its write grants are withdrawn. Every plan/review projection supplies actor-scoped `allowedActions`; absent capability fields mean false to older clients.

## 19. Audit / Security / Privacy

Append actor, current role/scope, action, old/new state, policy version, case/assignment/decision IDs, reason, correlation ID, source version and snapshot hash. No payroll/HR personal data copied. Histories remain readable after a principal leaves; they do not grant that principal current action. Denied access must not disclose another plan's existence.

## 20. Reports / Dashboards / Metrics

Counts by submitted/reviewed/active/returned/rejected status, turnaround and aging by assigned stage and department, estimated forecast total by currency and period. Label forecasts as estimates; no budget utilization or savings claim until a separate reconciled source exists. Define numerator, denominator, clock and currency conversion before publishing KPI controls.

## 21. Configuration & Localization

Published effective plan-review policy defines required stages, assignee resolution and quorum; no hard-coded monetary threshold or role-name authority. The initial schemaVersion2 is entity-global with no branch conditions: current Core organization-wide configuration actors and named Review/Final assignees are required, independently of shell branch/department/All selection. Branch-specific policy conditions, SLA and mandatory-document configuration require separately adopted extensions and remain unavailable in this baseline. Store canonical dates/decimals; display company timezone/currency. If no valid configuration exists, submission stays unavailable with a reason.

## 22. Non-Functional Requirements

Atomic cross-schema state, bounded queue pages, accessible keyboard decision form, exact decimal preservation, authorization-filtered search/export, retry-safe receipts, append-only history and observable integration failures. Performance target awaits expected planning volume.

## 23. Postconditions

An Active plan has one completed approved Core case bound to its frozen version and branch, plus audit/outbox evidence. A Returned or Rejected round retains every assignment/decision. A Withdrawn round has no live assignment and leaves its parent plan Draft. No budget or PO row is written by this feature.

## 24. Failure & Recovery

Transaction failure leaves both plan and Core case unchanged. Lost response is recovered by the original actor/key without a second write. A stale version returns 409 and current detail; uncertain persistence locks the UI until original-key lookup. Outbox retry never repeats the decision. Delegate/role/branch revocation denies a pending task immediately.

## 25. Acceptance Criteria

One workerless Core maker can submit a valid Draft under published configuration; one independent reviewer and final approver complete it; plan becomes Active only on final approval. Return permits a revised new round; rejection and withdrawal preserve prior history. Same-key replay is stable, changed payload conflicts, stale versions and concurrent decisions do not double-activate. Wrong actor/entity/branch/tenant and self-approval are denied without write. Finance budget/PO balances are unchanged.

Actual bounded checkpoint: Root verified548 Host cases and exact2004 Procurement reuse, dual-native finite-role provider admission, real shared policy Create/Submit/Publish and original-UI Create201/Submit201/Review200/Final200. Reviewer-owner403, immutable historical view/currentActive and cookie/runtime cleanup were observed. Full post-primary native reconciliation, retained Down veto and production acceptance remain pending; the [checkpoint](../gap-closure/PROC-05A-PRIVATE-INTEGRATION-CHECKPOINT-20261103.md) separates these evidence levels.

## 26. Test Scenarios

Unit tests: state matrix, exact decimal/snapshot hash, policy/assignee absence, SoD including linked aliases, legacy branch/currency claim, Withdraw→Draft→Edit→Resubmit, CAS and receipt fingerprint. If delegation is admitted later, add expiry/current authority tests first. Provider tests: Core/Procurement atomicity and finite role/column grants. Private real-cookie API and original UI: maker→reviewer→approver; return/resubmit; rejection/withdraw; current role/branch revocation; lost response/recovery; concurrent duplicate decisions; all-branch read versus concrete write; retained original rows, audit/outbox and no Finance mutation. Source tests alone do not establish live acceptance.

## 27. Ownership / Dependencies

Procurement planning owns business states and submission pin; Core owns accounts, branch assignments, generic approval records and current delegation. Shared policy administration owns published configuration. UI consumes returned permissions/actions. The genuine additive migration was generated after B1's actual published migration and adopted with raw-identical designer/snapshot; no replacement or duplicate migration is permitted. Root owns remaining native and release acceptance.

## 28. Open Decisions / Assumptions

Engineering adopts exactly two initial stage purposes, Review and Final, plus the explicit CAS scope-claim route. Business assignees, quorum, SLA, period verification and launch policy remain unapproved configuration; that does not block source implementation but prevents live activation. Additional parallel stages, amendments and closure require later scope decisions. Shared delegation requires its own current-authority resolver and acceptance before being enabled; the existing record type alone is insufficient. No plan approval switch or default route is activated by this document.
