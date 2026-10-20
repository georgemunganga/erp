# PROC-03 Approval and Budget Control

**Status:** Draft for business review · **Version:** 0.1 · **Hierarchy:** Procurement → Employee Requests and Guided Buying → Approval and Budget Control → Operations · **Framework:** [ERP 28-section standard](../hrm/feature-specifications/ERP_Feature_Specification_Framework_Enterprise_Integration_Updated.docx) · **Module:** [PROC-00](PROC-00-PROCUREMENT-MODULE-ARCHITECTURE-AND-CAPABILITY-MAP.md)

## 1. Feature Identification

`PROC-03`, Phase 1. Business owners: Procurement governance and Finance budget owners; product, data and technical owners to name. Operations: `O1` evaluate policy/funds, `O2` assign approvals, `O3` decide, `O4` resolve exception, `O5` re-evaluate after change, `O6` release/reconcile reservation.

## 2. Purpose and Business Outcome

Authorize demand through the correct people and an authoritative funding decision. A successful decision identifies the PR version, policy version, budget result, approvers, reasons and any Finance reservation; no unauthorized request becomes orderable.

## 3. Scope and Exclusions

Includes configurable single, hierarchical, sequential, parallel and conditional approval; budget check/reservation reference; delegation, reassignment, escalation and exceptions. Shared Workflow executes tasks and Finance owns budget balances. This feature does not post accounting entries or invent funds during an outage.

## 4. Parent and Related Features

Consumes [PROC-CFG-01](PROC-CFG-01-PROCUREMENT-CONFIGURATION-AND-POLICY.md) policy and [PROC-02](PROC-02-PURCHASE-REQUISITION-AND-LINE-ROUTING.md) submitted version; HRMS provides reporting line, Finance budget/commitment result, and Purchasing consumes approval authority.

## 5. Actors and Participants

Requester, line manager, department and budget owner, Procurement reviewer, exception authority, delegate, Workflow service, Finance service/operator and auditor. The requester is the subject even when another user submits on their behalf.

## 6. Entry Points and Triggers

PR submission/resubmission, material PR change, Finance reply, approver action, delegation expiry, worker/manager change, SLA breach, cancellation and reservation release.

## 7. Preconditions

Current immutable PR version; resolvable published policy; active assigned approver; valid entity/coding and Finance contract or approved evidenced manual path. Missing authority, hard rule or Finance result blocks final approval.

## 8. Workflow

`O1`: resolve policy and request Finance check; distinguish advisory availability from confirmed reservation. `O2`: construct ordered/parallel tasks with quorum and conflict check. `O3`: assigned actor approves, returns or rejects, recording reason where required; final approval only after all gates pass. `O4`: hard failure routes to named authority and Finance resolution, never silent override. `O5`: material PR revision invalidates affected pending authority and starts a new instance. `O6`: cancellation/change asks Finance to adjust or release commitment and reconciles its response.

At case opening, resolve canonical active accounts and retain the maker/requester worker IDs and each assigned worker ID. These references are immutable evidence, not copied HR records. Later historical offboarding does not itself prevent an independent active Finance officer from resolving the original return/rejection reservation.

## 9. Status and State Transitions

Budget: Pending → Available / Partially Available / Insufficient / Not Configured / Exception Pending → Resolved or Denied. Approval: Not Started → Pending → In Progress → Approved / Returned / Rejected / Withdrawn; revised request creates a new approval instance. Actor, conditions, evidence, SLA start/pause, escalation and next state are recorded for every transition. Escalation reassigns or warns; it never auto-approves.

## 10. Permissions and Data Scope

`procurement.approval.decide` requires assigned task, active workforce identity, tenant/entity/department scope and sufficient delegated amount/category authority. Requester/final approver conflict is denied unless approved compensating policy exists. Finance alone issues budget override/reservation. Approvers see relevant lines and evidence, not unrelated employee or supplier data.

Independence compares stable worker identity as well as principal ID, so another login for the same person does not satisfy maker/checker rules. Decisions still require current active canonical assignees, current permissions and finite spending authority. A release verifier must be currently active and authorized, and differ from the retained maker, requester and terminal decision worker.

## 11. Business Rules

`BR-PROC-APP-001` pin policy/version and decision inputs at submission; `002` hard budget failure blocks; `003` delegate never exceeds delegator; `004` return/reject/exception needs reason; `005` material changes trigger reapproval; `006` quorum is explicit; `007` no self-final-approval under normal policy; `008` Finance alone confirms reservation and release.

## 12. Data Fields and Ownership

Core Workflow/Approval owns reusable approval cases, stages, assignments, decisions, delegation execution records and task history, including execution actors, timestamps and reasons. Procurement owns the immutable submitted PR/version, policy evaluation, purchasing authority/evidence snapshot, Core case reference and source business outcome; it must not maintain a competing execution ledger. Finance owns budget balances, reservations, transfers and releases; Procurement retains the exact Finance response/evidence ID, version, amount, currency, time and status used for its decision. HRMS owns workforce/reporting relationships and Core Identity owns login principals and access grants. Amounts carry an explicit calculation basis and currency: the existing policy implementation supports estimated line base before discounts, taxes and delivery; tax-inclusive or FX-based authority requires a separately configured and verified calculation. See [the next execution increment contract](PROC-APPROVAL-P01-IMPLEMENTATION.md#next-increment-design--purchase-request-approval-execution) for planned persistence, authorization and transaction requirements; these are not a claim that approval execution is enabled.

New submission evidence uses schema version 2: nullable-for-upgrade `MakerWorkerId`, `RequesterWorkerId`, `WorkerIdentitySha256` bind tenant, company, source, submitted version, case and maker principal. New assignment evidence retains `AssigneeWorkerId` in the hashed identity/authority envelope. Old version 1 rows stay unchanged with null worker evidence; no identity is guessed or backfilled. Core remains owner of the identities; these are minimal immutable references with no cross-Core foreign key.

## 13. Screens and Data Views

My Approvals, decision summary, policy/budget explanation, pending quorum, exception request and evidence, delegation view, approval timeline, budget reconciliation queue. Show current owner and next action.

## 14. User Experience and Human Behaviour

Approvers see exactly what value and scope they authorize, what changed, and why they were selected. Return and rejection have distinct meanings. A missing manager produces a named escalation, not an invisible stalled task. Budget figures show source and as-of time.

## 15. Notifications and Communications

Notify assigned approver, requester on decision, budget/exception owner, new delegate, and operator on Finance failure. SLA warnings and breach messages use the configured business calendar; confidential line content stays behind authorized links.

## 16. Documents and Attachments

Budget exception, delegation authority and supporting approval evidence link to exact PR/policy version, with scanning, restricted access, version and retention. A missing mandatory document blocks the transition.

## 17. Exceptions and Edge Cases

Vacant or circular hierarchy, requester also manager, inactive delegate, concurrent parallel decisions, changed FX rate at threshold, stale Finance check, partially funded request, worker transfer and reservation release failure all get explicit owner and state.

Missing, legacy or hash-inconsistent worker evidence blocks execution/release with an explicit evidence error. Historical account deactivation or employee termination after a recorded return is distinct from missing evidence: valid version 2 records allow independent Finance recovery without reactivating departed staff.

## 18. Integrations, APIs, Events and Sandbox

Versioned Finance check/reserve/release and shared Workflow task contracts carry tenant/entity, PR version, policy version, amount/currency, correlation and idempotency. Publish approval decisions after commit. Sandbox tests hard failure, delayed/duplicate Finance reply, delegate expiry and concurrent quorum without real funds.

## 19. Audit, Security and Privacy

Append route, budget response, delegation chain, decision, reason, override and prior/new state. Enforce server-side scope and segregation on task APIs and exports; protect budget evidence and log privileged access.

Retained worker references follow approval-evidence retention/access rules. They contain no payroll, password, private profile or bank details. Worker identity and authority digests are verified on reads used for execution; append-only guards protect application writes.

## 20. Reports, Dashboards and Metrics

Approval aging, per-stage SLA, return/rejection reasons, hard budget blocks, exception frequency, orphan tasks and reservation mismatches. Define time window, business-hours clock, currency conversion and owner before KPI publication.

## 21. Configuration and Localization

Effective thresholds, approval strategy/quorum, tax-inclusive basis, currency/FX source, delegation scope, SLA calendar, exception authority and notification channels are versioned by entity and transaction type. No sample amount is a default.

## 22. Non-Functional Requirements

Concurrent decisions are serialized/version checked; commands and Finance callbacks are idempotent. Task/Finance outages and reconciliation are observable. Latency, volume and recovery targets require launch estimates; accessible decision screens are required.

## 23. Postconditions

Approved PR version has all required decisions and valid Finance evidence; returned/rejected versions cannot authorize PO. A confirmed reservation has one Finance reference, and historic decisions remain attributable after manager changes.

## 24. Failure and Recovery

Finance outage leaves Pending, not Passed. Unknown reserve/release outcome is queried by stable key before retry. Duplicate decision returns prior outcome; conflicting concurrent decision is rejected and user sees current task state. Operators reconcile without editing history.

Finance release after historical offboarding uses the pinned maker/requester identities and the terminal decision assignment identity. It never falls back to a mutable inactive account mapping. Legacy version 1 cases remain blocked pending a separately approved, audited identity-evidence migration/recovery procedure; this release supplies no automatic override.

## 25. Acceptance Criteria

Given hard budget failure, final approval and conversion fail. Given requester self-approval conflict, decision is denied. Given valid delegation, decision records delegate and delegator. Given material PR revision, prior approval cannot authorize it. Given task SLA breach, status remains pending until a real decision.

A newly opened case retains exact current canonical worker mappings. After a return, offboarding historical actors does not strand an independent active Finance release. An active alias linked to any retained participant worker is denied. Missing or tampered worker IDs/hash, including legacy version 1 evidence, never allow release or approval.

## 26. Test Scenarios

Test sequential/parallel/no-approval policy, threshold edge, delegation expiry, missing manager, self-approval, return/resubmit, multi-entity access, partial funding, Finance outage/replay, concurrent quorum, release mismatch and inaccessible evidence.

Include historical maker/requester/decision-worker offboarding, aliases of each participant, malformed identity envelopes, digest mismatch, unchanged legacy rows after upgrade, blocked legacy execution, and same-person review through different accounts. Exercise the actual mapped API with real disposable PostgreSQL; model tests alone do not establish release readiness.

## 27. Ownership and Dependencies

Procurement governance owns authority rules, Finance owns budget and commitment, HRMS owns reporting line, Workflow owner supplies execution. Depends on PROC-CFG-01 and PROC-02; PO and sourcing depend on approved output. Named operational owners are pending.

## 28. Open Decisions and Assumptions

Approve authority matrix, partial funding, budget check versus reservation point, interim manual evidence, tax-inclusive threshold basis, FX source, delegation/compensating rules, SLAs and Finance contracts. Owners: Procurement governance, Finance and Workflow; target date before implementation.
