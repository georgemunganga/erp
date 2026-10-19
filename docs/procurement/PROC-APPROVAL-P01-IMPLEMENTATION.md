# Shared Core approval foundation — P01

**Status:** Implemented foundation; purchasing decisions disabled
**Date:** 19 October 2026
**Related:** ADR-002, module-connect issue #9

## What exists

- `backend/core-api` owns the reusable approval domain and `core_approval` PostgreSQL schema. It has no foreign keys to HRM or Procurement tables.
- Cases pin source module/type/ID/version and policy ID/version. Stages have a unique order, explicit quorum and assignments. Decisions retain actor, action, reason, time and idempotency key. Delegation records have effective dates.
- The decision engine checks the authenticated tenant/company, assignee, permission, maker/checker rule, active stage, reason on rejection/return and quorum. A repeated key with the same action is a no-op; reuse for another action fails.
- The database rejects physical deletion and updates to decision history through the application context. Version fields are concurrency tokens. The first migration creates five tables with tenant/company indexes and separate history.
- The ERP host composes the Core context with the same authenticated Procurement scope. It does **not** open an approval API or auto-apply the Core migration.
- A buyer-only read endpoint, `GET /api/procurement/v1/requests/{id}/approval-readiness`, checks the current submitted version against its pinned published policy and latest recorded Finance evidence. It returns named blockers and `isAuthority: false`; it does not open a task or update the request.

## Migration and rollback

`P01CoreApprovalFoundation` is additive. Use a protected backup, apply its idempotent script, verify five tables and one migration entry, and only then deploy a host build that references it. Rehearsal on a disposable PostgreSQL database applied the script twice with the same result. No production approval data exists yet; rollback can return to the previous host image without touching this schema. Dropping the schema is unnecessary and should not be done during ordinary rollback.

## Required before enabling approval

1. Resolve and publish an effective Procurement approval route without hard-coded monetary thresholds.
2. Verify current PR version, line amounts, policy version and authoritative Finance reservation in a transaction before opening or completing a case.
3. Implement a transactional decision handler with concurrency/retry handling and durable audit/outbox effects. A domain decision alone does not change a PR.
4. Validate effective delegation, authority limits and branch/department data scope server-side; the current engine supports assigned actors but does not yet resolve those policies.
5. Expose assigned queue, decision and history endpoints, then connect the Approvals UI.
6. Test concurrent quorum decisions on PostgreSQL, stale Finance responses, revoked assignments, self-approval, cross-tenant/entity access and idempotent retries.
7. Provision a dedicated Core approval database role before enabling writes; the current Procurement connection is composed for schema discovery only and has no grant on `core_approval` tables in production.

`/api/procurement/v1/meta` continues to report `approvals: false`. No PR or PO becomes approved from this foundation alone.
