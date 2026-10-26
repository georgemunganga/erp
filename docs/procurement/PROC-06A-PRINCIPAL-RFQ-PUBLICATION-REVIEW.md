# PROC-06A Principal RFQ Publication Review

**Status:** Source implementation in progress; default off, migration and live acceptance pending. **Parent:** [PROC-06](PROC-06-STRATEGIC-SOURCING-AND-AWARD.md). This child feature covers one internal decision, not supplier delivery.

## 1. Feature Identification

PROC-06A, principal RFQ internal publication review. Procurement owns the sourcing event and supplier shortlist; Core owns current user, company/branch permission and approval case/decision.

## 2. Purpose and Business Outcome

A buyer submits an existing governed RFQ Draft for independent current Core review. Approval marks the RFQ internally Published and records ready-for-manual-delivery invitations. It does not assert a supplier was contacted.

## 3. Scope and Exclusions

In: scoped eligible supplier lookup, shortlist submission, assigned review, Approve/Return/Reject, detail/list and actor original-key recovery. Out: actual contact, portal/email dispatch, manual/supplier quote, sealed bids, opening, scoring, recommendation, award, PO issue and Finance posting.

## 4. Parent and Related Features

Consumes P25 principal request approval/Finance evidence, P29 principal RFQ Draft and allocation, current supplier eligibility, published sourcing policy and Core approval. P32 commercial rounds and P38 award remain successors.

## 5. Actors and Participants

Buyer with explicit `procurement-sourcing-submit`; distinct current Core reviewer with explicit `procurement-sourcing-publish`. No employee link is required. Supplier is a shortlist reference, not an authenticated participant in this slice.

## 6. Entry Points and Triggers

From the existing live RFQ detail, a buyer opens publication review. Assigned reviewers use the existing RFQ worklist. The client may recover an uncertain command by its original key.

## 7. Preconditions

Same tenant/company and authorized selected branch; P29 Draft and source hold verified; P25 Core review, current policy identity schema 2, effective policy, supplier qualification and manual Finance authority rechecked. Feature option, owning schema, finite Record/Execution/Core roles and current grants must all be ready.

## 8. Workflow

Open validates draft fingerprint/version, source/Finance/method, published policy and shortlist, then atomically creates a Core case and immutable Procurement opening/receipt/audit/outbox. An assigned reviewer decides through the Core case; the same transaction appends decision/receipt and updates event state. Approval creates shortlist invitations only in `ReadyForManualDelivery`.

## 9. Status and State Transitions

`Draft → PublicationReview → Published` on approval. Return or rejection makes the internal event `Draft` with retained decision. Existing P29 `Draft → Cancelled` applies only where original source release rules allow. No external-delivery state transition is included.

## 10. Permissions and Data Scope

`procurement-sourcing-submit` and `procurement-sourcing-publish` require explicit configured Core actions; legacy `erp_admin` and ordinary role bootstrap receive neither. Read/recovery recheck actor, current role, tenant/company/branch and retained event scope. A reviewer does not gain ownership of the maker's draft.

## 11. Business Rules

The published effective policy supplies competition/shortlist controls. No hard-coded money threshold or default reviewer. Maker/checker independence, current supplier orderability, unchanged approved PR allocation and no self-approval are required. A policy change or stale source blocks a new decision.

## 12. Data Fields and Ownership

Three new Procurement families: immutable publication opening (policy/source/supplier snapshot hash, Core case), decision (Core assignment/decision, actor, event version) and command receipt (actor, command digest, exact response text/SHA). Existing sourcing event/invitations and P29 hold are the only mutable business records.

## 13. Screens and Data Views

Reuse live `/rfqs` list/detail/editor and assigned reviewer view in the supplied layout. `principalSourcingPublication` metadata gates controls. Display invitation state as ready for manual delivery; never label it sent.

## 14. User Experience and Human Behaviour

Show current policy/shortlist, reviewer decision and original-key recovery. A failed or unknown POST leaves a pending command locator and does not invite a second POST. Denied or unavailable controls explain the server state.

## 15. Notifications and Communications

No supplier communication occurs. Audit/outbox record internal state only; they are not a delivery receipt. A later contact operation requires its own evidence and permission.

## 16. Documents and Attachments

No document upload or document authority is introduced. Later sealed offers and retained requirement documents use Core Documents under their own admission and purpose checks.

## 17. Exceptions and Edge Cases

Return, reject, suspended supplier, changed source/policy/Finance evidence, stale Core assignment/version, changed branch/grant and duplicate key all fail or resolve without duplicate business effects.

## 18. Integrations, APIs, Events and Sandbox

`/api/procurement/v1/sourcing/{id}/principal-publication-reviews`, assignment decision, list/detail/assigned/supplier lookup and `/principal-publication-commands/{key}` are the bounded API. Core case/decision and Procurement records commit on the same connection/transaction; audit/outbox carry internal event facts. No Finance/Inventory/SMTP integration effect.

## 19. Audit, Security and Privacy

Append-only opening/decision/receipt and current Core actor identity; no synthetic worker ID. Finite role ACL denies owner/superuser/DDL and mutation of immutable evidence. Audit/outbox inserts are scoped and linked to command keys.

## 20. Reports, Dashboards and Metrics

The own-company event list may show Draft/PublicationReview/Published and assigned review counts. It does not represent supplier delivery, bid response, savings or award completion.

## 21. Configuration and Localization

`Procurement:SourcingPublication:Enabled` defaults false. Existing published sourcing policy controls competition, reviewer sets and supplier count per entity. Currency/timezone come from current Core/Procurement rules; no fixed threshold or invented default policy.

## 22. Non-Functional Requirements

Serializable/same-save locks, stable idempotency response bytes, bounded shortlist and pagination, no-store private responses, exact physical PostgreSQL schema/ACL/readiness. Current source remains unavailable until genuine EF migration, catalog capture, finite roles and runtime acceptance.

## 23. Postconditions

Approved review retains case/decision/policy/source/receipt and invitations awaiting contact; original PR allocation remains held. Return/reject retains evidence and does not send supplier messages or release the PR hold automatically.

## 24. Failure and Recovery

An uncertain command is recovered with its original key under the current actor/branch/grants. A changed payload/key conflicts. Down migration must veto any retained publication opening/decision/receipt or linked audit/outbox evidence before dropping tables or reverting status constraints.

## 25. Acceptance Criteria

Workerless current Core buyer/reviewer can complete an independently approved internal review on private PostgreSQL; exact Core and Procurement graph plus no supplier delivery/quote/award/PO/Finance effects are read back. Default-off and every role/schema/policy/scope denial produce no writes. Production remains off until the affected acceptance is reviewed.

## 26. Test Scenarios

Meaningful source tests cover explicit grants including legacy admin denial; maker/checker, tenant/company/branch, stale version, supplier suspension, missing/changed policy and Finance pin, replay/changed key/concurrency/rollback. Genuine EF Up/empty Down/Up, populated SQLSTATE 55000 veto, finite ACL negative, normal-cookie HTTP/browser and independent owner proof are required.

## 27. Ownership and Dependencies

Procurement owns publication decision and supplier eligibility. Core owns identity, company/branch permissions, case/assignment/decision and documents. Finance owns evidence authority; no ledger posting is performed. UI integrates the existing live RFQ controls after backend contract acceptance.

## 28. Open Decisions and Assumptions

Business owner must publish an effective sourcing policy and reviewer set before activation. Supplier communication channels, sealed bid rules, evaluation methodology, award authority and residual split policy are deferred to later child features. No employee prerequisite or default amount threshold is assumed.
