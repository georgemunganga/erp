# PROC-08 / M5a — Purchase order governance with Core identities

Version 0.1 · 26 October 2026 · Draft behaviour specification for technical and business review.

This bounded specification supplements [PROC-08](../PROC-08-PURCHASE-ORDERS-AND-AMENDMENTS.md). Its implementation is in [draft PR119](https://github.com/georgemunganga/erp/pull/119); UI transport work is in [draft UI PR87](https://github.com/georgemunganga/module-connect/pull/87). It is not an approved production policy, release acceptance, or completed Procurement UAT. Source contract inspected: `caa14f87` on the PR119 branch. That source is newer than the verified `108d442d` API publication.

## 1. Feature Identification

Procurement → Purchasing → Purchase Order Governance → Record funding, submit, decide, authorize issue, record supplier response, request release, confirm release. This increment follows the existing M3b approved-request-to-internal-order draft.

## 2. Purpose & Business Outcome

Let authorized ERP users progress the same purchase order through independent funding and Core approval, retaining its approved request sources and commercial terms. A user needs the current action and data scope; an employee record is optional.

## 3. Scope & Exclusions

Includes revision-one direct-source PO governance, manual external funding evidence, named Core assignments, original-key recovery and controlled pre-issue release. Excludes automatic supplier delivery, bank/payment execution, Finance ledger postings, resubmission, amendments, receiving, returns and final commercial closure. Those remain follow-on work; this increment is not the complete PO lifecycle.

## 4. Parent & Related Features

Depends on Core identity, roles, branches, shared approvals and audit; Procurement PR approval, source allocations, supplier/catalog eligibility, policy and spending authority. It reuses the existing PO, lines, source bindings and receipt store rather than introducing parallel masters.

## 5. Actors & Participants

Buyer; independent Finance recorder; named PO reviewer; authorized issuer; dispatch confirmer; independent release confirmer; policy/authority administrators; auditor. Issuer and dispatch may be the same person where their current permissions permit it. No extra employee/headcount requirement is implied. Funding and approval independence includes current linked employee aliases.

## 6. Entry Points & Triggers

Existing Purchase Orders list/detail; Finance work queue; assigned approval queue; issue/dispatch work queues. Funding starts from an unfunded principal draft. Supplier acknowledgment records an externally observed response against an internally authorized issued version.

## 7. Preconditions

Authenticated active Core account, permitted company/branch, required action, valid principal source bindings, genuine migrations and verified finite execution database role. New-work options default off. Submit, positive approval and issue additionally require the current source, funding, policy and independent authority guards defined by each command. Funding validates its manual attestation policy and independence; acknowledgment binds the retained authorized version and configured dispatch procedure; release confirmation validates the retained request, current independent confirmer and external release attestation.

## 8. Workflow

1. Buyer creates the existing internal draft from approved PR lines; source quantities become Reserved.
2. Independent Finance records a manually attested external commitment, exact currency/gross amount, source reservation references and evidence time. Procurement does not reserve funds in a ledger.
3. Buyer submits. Revalidate commercial/source/funding facts and resolve the effective policy and eligible independent reviewers. Open the shared Core case atomically with Procurement submission/assignment evidence.
4. Assigned reviewer approves, returns or rejects. Positive approval revalidates current financial and spending authority. Return/reject retain actor, assignment and source checks without requiring renewed spending authority just to stop the process.
5. Authorized issuer revalidates the completed approval and current evidence, freezes the authorized version and moves source allocations to Ordered once. Status becomes IssuePending; nothing has been sent to the supplier.
6. Dispatch confirmer records an external Accepted/Rejected response with the allowed channel, supplier/evidence references and observed time. Status becomes Issued or Disputed.
7. Before issue, buyer may request external release of a funded commitment. Pending approval is cancelled through the real Core engine in the same transaction. Quantities remain Reserved until independent Finance confirms the external release; only then does the PO become Cancelled and allocations become Released.

## 9. Status / State Transitions

| From | Action | To | Required retained outcome |
| --- | --- | --- | --- |
| Draft | Record funding | Draft | Immutable funding evidence; no artificial PO row-version increase |
| Draft | Submit | PendingApproval | Revision 1, Core case, submission and named assignments |
| PendingApproval | Approve | PendingApproval / Approved | Core quorum/stages and mirrored decision |
| PendingApproval | Return / Reject | Returned / Rejected | Reason and durable Core decision |
| Approved | Authorize issue | IssuePending | Frozen issued version; allocations Ordered |
| IssuePending | Record Accepted / Rejected response | Issued / Disputed | Response against exact issued version |
| Draft / PendingApproval / Approved / Returned / Rejected | Request release | ReleasePending | Funding retained; pending Core case cancelled where applicable |
| ReleasePending | Confirm external release | Cancelled | Independent evidence; allocations Released |
| Unfunded Draft | Existing M3b cancellation | Cancelled | Original M3b cancellation contract |

Returned/rejected orders cannot be edited or resubmitted through this increment. Issued/Disputed orders cannot use its pre-issue release route. These are explicit follow-on workflow gaps, not implied transitions.

## 10. Permissions & Data Scope

Actions use `procurement-po-read`, `-convert`, `-edit`, `-submit`, `-finance-confirm`, `-approve`, `-issue` and `-dispatch-confirm` as applicable. Buyer reads remain buyer-owned and branch-scoped. Finance, reviewers, issuers and dispatch have purpose-specific reads without requiring buyer-wide read access. Explicit All branches is read-only and bounded by current Core company/branch scope, buyer ownership or named assignment as applicable; saving requires a selected permitted branch. Each command, direct URL and recovery checks current actor, company and scope server-side.

## 11. Business Rules

A PR approval never substitutes for PO approval. Positive actions bind the original source digest, exact commercial snapshot, currency, revision, current policy and funding evidence. Finance and reviewers must be independent of the buyer and originating requester/submitter; release confirmation must also be independent of the release requester. Funded drafts cannot use unfunded cancellation. No silent supplier substitution, budget override, automatic approval or external posting occurs. Current M5a routing supports the implemented ProcurementManager/One or All mapping; unresolved, ambiguous or unsupported routing fails closed.

## 12. Data Fields & Ownership

Procurement owns the same PO/lines/source allocations plus append-only funding, submission, assignment, decision, issued-version, acknowledgment, release-request and release-confirmation evidence. Fields include scoped IDs, command keys, exact source/commercial digests, policy/grant revisions, Core case/task/decision IDs, principals/optional worker aliases, references, reason and observed/recorded times. Core owns identities and approval mechanics; Finance owns actual commitments/releases and accounting. Lines use numeric(18,4); gross totals use numeric(38,8), subject to application decimal representability. UI transport preserves decimal strings.

## 13. Screens & Data Views

Retain the imported PO list/detail structure, tabs, forms and shared ERP controls. Add purpose-specific task views within that experience. Show verified status/evidence and server action eligibility. A funded Draft shows release actions instead of ordinary Cancel. Internal authorization and externally observed response must be distinguishable.

## 14. User Experience / Human Behaviour

Use plain labels, supplier/branch names and exact amounts. Cover loading, empty work queue, validation, stale evidence, denial, unavailable workflow, saved result and unknown save outcome. Preserve form values and the original command key after uncertainty. Server previews do not guarantee that a later save will succeed; commands revalidate under locks.

## 15. Notifications & Communications

Domain audit/outbox events record the transition in its transaction. Event publication/notification delivery is secondary and may fail independently. Assignment notifications, supplier communication delivery and SLA escalation still require their own verified integration; this specification does not assert their production acceptance.

## 16. Documents & Attachments

Manual evidence uses restricted external reference fields in this increment. Uploaded evidence, scanning, retention and owner access must use the shared Core document service when integrated. A reference string is not proof that a document was uploaded, scanned or delivered.

## 17. Exceptions & Edge Cases

Cover stale order/case/task versions, expired funding/policy/grants, revoked permissions/branch assignment, worker-alias conflicts, changed supplier/catalog eligibility, mismatched amount/currency/reservations, duplicate keys and another actor's receipt. Return/reject and pre-issue withdrawal remain controlled escape routes when positive financial authority is stale. Issued-order disputes and amendments remain follow-on scope.

## 18. Integrations / APIs / Events / Sandbox

Use the existing `/api/procurement/v1/orders` group. M5a commands are `principal-funding`, `principal-submit`, `principal-decisions/{assignmentId}`, `principal-issue`, `principal-acknowledgment`, `principal-release-request` and `principal-release-confirmation`. Purpose reads and actor-owned `principal-governance-command-receipts/{operation}/{key}` use the same scope. Core and Procurement contexts share one serializable database transaction and existing authorization fences. Private test fixtures have no production or external financial/supplier effect.

## 19. Audit / Security / Privacy

Persist actor, target, scoped IDs, transition, reason, policy/authority references, command key and correlation evidence. Eight evidence tables are append-only for the finite executor. Prevent cross-company/branch access, forged receipt/source bindings and unauthorized direct API actions. Do not expose credentials or bank details through UI messages, logs or notifications.

## 20. Reports / Dashboards / Metrics

Potential measures are funding/approval/issue backlog, assignment age, disputed orders and pending releases. Define denominators, time windows, currency treatment and owners before treating them as control KPIs. M5a task lists are operational reads, not accepted spend/accounting dashboards.

## 21. Configuration & Localization

Core owns company, selected currency, identity and branch configuration. Procurement owns its effective policies, action-specific authority and manual evidence/dispatch settings. No live thresholds, Finance procedures or delivery channels are invented by the implementation. Private synthetic policies are labelled and isolated. Reuse shared localization and number/date/currency presentation.

## 22. Non-Functional Requirements

Default deny, tenant/company isolation, current record scope, transactional concurrency, deterministic exact decimals, durable audit and retry-safe effects are required. Preserve accessible shared UI controls. Latency, volume, recovery objectives and state SLAs require business/operations confirmation; a passing unit suite does not establish them.

## 23. Postconditions

A confirmed command has one retained actor/key-bound receipt and matching business artifact. The current PO/source allocation/Core case reflect its permitted transition. Unrelated records are preserved. Recorded manual evidence never implies a Finance posting, supplier transmission or bank payment.

## 24. Failure & Recovery

Persist the original UI command/key/body/source pin before one POST. After lost acknowledgment, use same-key GET recovery; do not automatically repost or generate a replacement key. Historical receipts prove their original result, not the latest state. Confirmed transaction aborts may use the existing bounded retry mechanism. Unknown outcomes require recovery. Migration Down is transactional and refuses once M5-specific evidence/outcomes exist; use fix-forward after business use.

An isolated Service UAT clone made by logical PostgreSQL dump/restore retained all 220 table rows and repaired ACL parity but failed physical readiness. A read-only comparison of 96 Procurement CHECK constraints found 26 changed deparse spellings after restore; ten intersect P25/M5a/M5b literal readiness manifests. Every one of those ten differences is the parser's redistribution of a `varchar[]` to `text[]` cast inside `ANY`, with the enum values, operators, column guards and predicates unchanged. The protected comparison receipt is `procurement-m5-service-native-prep-20261028_run01/service-runtime/check-deparse-comparison.private.json`, SHA-256 `15cd065afe53b6a687a649ce173ddbaae1f9ee747bbc4e7964c6161f9c83c52a`. Readiness may admit only the ten qualified canonical/restored pairs captured there; arbitrary SQL normalization, changed names, altered guards and broader grants remain denied. The first Service configuration attempt returned `AuthoritySubjectUnavailable` before any grant or business record was created. The restored-form source change still requires genuine SDK verification and a fresh private Service run before this scenario passes.

## 25. Acceptance Criteria

Require genuine migrations and finite-role schema admission, workerless real-cookie actors, independent funding and approval, exact frozen issue/response, controlled release and correct allocation changes. Verify branch-only writes, scoped All reads, permission-only purpose queues, current guard enforcement and actor-bound original-key recovery. Full acceptance also needs integrated original UI screens and owner/database evidence; source or schema tests alone cannot satisfy it.

## 26. Test Scenarios

Run positive Funding→Submit→Approve→Issue→Accepted; rejected supplier response; Return/Reject with expired positive authority; pending-case withdrawal; funded-Draft release with independent confirmation; unfunded legacy cancellation; multi-stage progress; lost response/hard reload/original GET; duplicate/concurrent keys; stale versions; wrong actor/company/branch; All-write refusal; disabled new work with scoped historical reads; tampered source/evidence; retained-row and rollback/recovery checks. Record any scenario not executed as open.

## 27. Ownership / Dependencies

Procurement engineering owns the adapter/domain evidence and feature integration. Core owners own identity/branch/permission/approval contracts. Finance/business owners approve live evidence procedures and authority policies. A distinct reviewer verifies high-risk changes and actual acceptance evidence. Named human product/engineering/operations owners and UAT sign-off remain to be recorded.

## 28. Open Decisions / Assumptions

Confirm production approval mapping, manual Finance/dispatch procedures, evidence age, SLA/delegation, whether No Approval is required, and audit/retention obligations. Complete amendments/resubmission, receiving/returns, invoice/matching/payment visibility, supplier portal and remaining module milestones separately. At this document's creation, M5a has private schema/ACL/identity evidence and source tests; complete real-cookie business/browser UAT and production activation are still open.
