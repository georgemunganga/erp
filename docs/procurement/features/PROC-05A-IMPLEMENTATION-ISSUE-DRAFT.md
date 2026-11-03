# Issue draft: PROC-05A governed plan review

**Status:** Review before opening an implementation issue. **Source:** [28-section child specification](PROC-05A-GOVERNED-PLAN-REVIEW.md). **Baseline:** ERP `61d5a608f9cc6069bcf8d06ef51bdca036fd8d47`.

## Problem

The current plan API and UI save, edit, list and cancel owner Drafts only. There is no submission, named independent review, final approval, return/reject/withdraw, active forecast or review history. `ProcurementPlan` has no branch field; existing branchless Drafts cannot safely become branch-scoped by assuming a default branch. The shared Core Approval engine supports generic source records, but its current Procurement executor is purchase-request-specific and its existing delegation record does not establish a working plan delegation path.

## Bounded implementation

- Extend plan records with verified selected-branch ownership for new Drafts. Keep old branchless Drafts restricted; add an explicit CAS-audited owner/admin branch claim before they may submit. No guessed backfill.
- Add explicit, default-ungranted Core plan submission/review/approval/withdraw/read actions and a typed published plan-review policy. No monetary defaults or Finance reservation.
- Open a shared Core approval case with `SourceType=procurement_plan`, pinned plan/line versions and exact values, policy version, branch, maker, named independent assignments and quorum. Use `ApprovalEngine`; do not copy its state machine into Procurement.
- Implement Draft/Returned → Submitted → Reviewed → Active by the configured review/final decisions. Return, reject and withdraw complete a round and retain it. Final Core Approved decision and Procurement Active state commit together.
- Add actor-bound original-key command receipts, current Core permission/branch/SoD checks, audit/outbox, assigned queue/history and safe error/recovery contracts.
- Extend the original live Plan UI with returned `allowedActions`, plain-language task labels, reason prompts, history and exact-key recovery. Preserve existing Draft CRUD and page layout.
- Treat delegated decisions as unavailable until a separate shared Core current-delegation resolver and denial tests are established. Approved plan-line→PR trace is a follow-on issue.

## Acceptance

Workerless Core maker, reviewer and independent final approver complete one real-cookie private journey on a finite-role PostgreSQL clone. Return/resubmit, reject and withdraw retain previous rounds. Same-key replay returns the original result; changed payload, stale version and concurrent decisions do not duplicate work. Wrong tenant/entity/branch/actor and maker self-approval fail without writes; current revocation is effective. Company All read includes only assigned branches, and all writes use a concrete selected branch. Original business rows and Core identities remain intact; no Finance budget or PO row changes. Original UI actions, denied paths and recovery are exercised with real API, plus source/provider tests.

## Sequence and release hold

1. Review the child spec and resolve business assignee/policy/period choices.
2. Implement domain/API/source tests in a branch based on published ERP main. Do not generate or edit EF migration/snapshot while sourcing B1 owns the schema branch.
3. After B1 is published, generate genuine additive migration from that baseline, test finite roles/ownership/readiness, then private API and original UI acceptance.
4. Release remains default off until reviewed policy, operation evidence and business acceptance exist. No production switch, role grant, Finance posting or external supplier workflow is part of this issue.
