# ADR-002: Shared ERP approval contract and Procurement authority

**Status:** Proposed for engineering review, 19 October 2026
**Owner:** ERP Core and Procurement engineering
**Related:** [PROC-02](PROC-02-PURCHASE-REQUISITION-AND-LINE-ROUTING.md), [PROC-CFG-01](PROC-CFG-01-PROCUREMENT-CONFIGURATION-AND-POLICY.md), module-connect issue #9

## Context

The current HRM `WorkflowServiceImpl` contains a manager-first assignment, HR roles, worker IDs and leave effects. Its `WorkflowRequest` and delegation records are HRM-owned. Calling that service from Procurement would import HRM authority rules into purchasing. Procurement currently stops a submitted request at `PendingPolicyAndBudget`; a Finance budget decision records evidence but does not approve the request.

## Decision

Introduce a Core approval contract with tenant and company scope, transaction type, immutable source record/version, effective policy ID/version, ordered stages, named assignees, sequential or parallel quorum, effective-dated delegation, decision history and idempotent completion effects. The contract must evaluate permission, assignment, data scope, authority limit and maker/checker before recording a decision. A module adapter owns its domain guards and effects: HRM continues to own leave balance effects; Procurement owns PR approval state and later PO authority. Core owns reusable task/decision mechanics and audit integration.

Do not migrate existing HRM workflow rows into Procurement or treat the HRM `WorkflowRequest` table as a general approval ledger. Keep an adapter so current HRM flows continue while the shared contract is introduced incrementally.

## Procurement guard at final PR approval

The candidate request must still be `Submitted` at the pinned version; its effective policy must be published; the latest Finance decision must refer to that same request version, cover the full request currency/amount and confirm reservation. The decision actor must be assigned, in the same tenant/company and distinct from the requester. If any guard fails, leave the request pending or blocked with a reason. A stale Finance result, an overdue task or an escalation cannot authorize a PO. Only an approved PR line with available unallocated quantity may become an order source.

## Delivery sequence

1. Define Core contracts and persistence model with explicit migration and rollback plan; preserve HRM behaviour through an adapter and regression tests.
2. Add Procurement policy publication/effective resolution and approval-route configuration without hard-coded amounts.
3. Open a version-pinned approval instance only after policy and Finance checks are current; expose assigned queue, decision and history APIs with permission and company checks.
4. Wire the existing Approvals UI to the API; verify self-approval, cross-company, stale version, delegation, quorum, retry and Finance rejection paths.
5. Permit PR line conversion and PO commitment only after the above guard passes; test that neither a UI state nor a Finance callback alone can issue an order.

## Consequences

This adds shared platform code and a versioned approval store, but keeps purchasing and HR rules in their owning modules. Existing HRM approval endpoints continue to operate during migration. Procurement cannot honestly report final approval until the policy, Finance and Core workflow pieces pass together.
