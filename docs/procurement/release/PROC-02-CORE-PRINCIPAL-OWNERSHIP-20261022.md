# PROC-02: Core principal ownership correction

**Status:** Source prepared; tests authored but not run. Genuine migration generation, compilation,
private PostgreSQL rehearsal, browser verification and deployment are still required.
**Decision:** User clarification on 22 October 2026. Active Core system user + assigned Procurement
module/action permissions + authorized data scope governs ordinary request access. An HRMS
employee and job designation are optional. This supersedes the earlier employee-only baseline.

## Ownership and data contract

| Field | Meaning and owner | New records | Retained pre-change records |
| --- | --- | --- | --- |
| `requesterPrincipalId` / `requester_principal_id` | Stable Core `LocalUser.Id`; Procurement stores a reference, not another user master | Required by server creation route, from resolved current `scope.PrincipalId` | Null, without guessed backfill |
| `requesterWorkerId` / `requester_worker_id` | Optional genuine HRMS employee reference | Actual optional resolved worker, or null | Preserve existing value exactly |
| `createdBy` | Original authenticated actor attribution | Existing authenticated canonical subject stamping | Preserve original text exactly |
| Tenant/company and branch/department | Current authorized Core organization scope | Server stamped; selected company supplies default currency | Preserve existing original scope |

No user GUID is inserted into a worker field. `Guid.Empty`, an employee name, email match or job
name is never a stand-in for either identity. The create/update DTOs accept no ownership fields;
extra client fields cannot select an owner or an employee. Response detail adds nullable
`requesterPrincipalId` and makes `requesterWorkerId` genuinely nullable. Existing exact amounts,
line IDs/versions, company currency and `requestEditVersion` remain compatible.

## Ordinary workflow and permissions

The mapped create, own list/detail, update, submit and withdraw routes retain their existing
named permissions. Current Core account validation and configured action grants are performed by
the shared host scope owner. Route ownership queries use stable principal ID and current
company/tenant query filters, plus selected/confined branch and department restrictions.

- Draft and ReturnedForChanges updates use the current version and preserve original identity.
- Submitting still validates dates, item classification, exact amounts and buying rules. It enters
  Submitted/PendingPolicyAndBudget; submission alone is neither approval nor a budget reservation.
- Withdrawal retains state, reason, approval linkage and Finance reservation release guards.
- Other users cannot read/edit/withdraw the request. An employee alias does not confer ownership.
- Revoked permissions or a suspended/inactive Core account deny access despite old session claims.
- Removing/deactivating an employee profile does not remove an active Core user's ordinary request
  rights. Existing historical worker metadata remains as recorded; a later request may have none.

## Legacy compatibility without identity rewriting

For a row with null `RequesterPrincipalId`, ownership may be projected only when its original
`CreatedBy` exactly equals the currently verified Core user UUID in canonical `D` format. This is
an existing exact creator comparison, not a migration or a new historical identity attestation.
Noncanonical, unknown, imported or unmatched creator values remain denied. Do not infer identity
from an employee, current worker reassignment, names, email or case-insensitive string parsing.

The runtime projection does not populate `RequesterPrincipalId`; editing or withdrawing a legacy
row preserves its null principal, original worker and original creator. The owning context rejects
changes to these three persisted identity fields, including opportunistic legacy backfill. Any
future approved ownership transfer needs its own explicit permission, evidence and workflow.

## Operational approval and Finance boundary

Existing worker-specific approval pins keep their original schema, digests and JSON representation.
A nullable employee does not become a dummy worker. A genuine employee-based path remains compatible
when the request's immutable principal metadata exactly equals its already-pinned canonical
`CreatedBy` identity and a genuine nonempty requester worker exists. The old snapshot already binds
that exact creator and worker; the added immutable principal is equivalent metadata, not new authority.
Existing current worker, assignment, amount, source, Finance and maker/checker guards still apply.

Readiness, assignee resolution, case opening, pin verification and owning-store evidence attachment
reject a mismatched principal or a system-only request lacking the worker required by the old schema.
Such requests can still use ordinary CRUD and submission; a future principal-aware operational
contract must provide actual approval and reservation evidence before positive downstream use.
This change invents neither employee IDs nor principalized Finance evidence.

Old genuine employee-based history remains attributed and readable through its existing contracts.
No stored snapshot, approved decision, Finance reservation or policy version is silently rewritten.
The legacy approval snapshot serializer deliberately excludes the equivalent metadata field to
preserve old hashes; its callers verify the strict creator equivalence when that field is present.

## Model delta and migration gates

Expected additive/nullable delta:

1. Add nullable UUID `procurement.purchase_requests.requester_principal_id` without a default or
   data backfill.
2. Make `requester_worker_id` nullable while preserving every existing value.
3. Add scoped `(tenant_id, legal_entity_id, requester_principal_id, status)` index, retaining the
   existing worker index for historical references.
4. Add null-safe `ck_purchase_request_identity` excluding empty UUIDs only when fields are present.
   Before apply, inspect whether old rows contain empty worker IDs; report blockers rather than
   replacing/deleting them. Do not fabricate an employee to satisfy a constraint.

No cross-module Core user table, HRMS employee table, ledger or duplicate master is created. The
shared identity adapter verifies current account grants; Procurement owns its stored reference.

Generate genuine **P22 Core principal ownership** after source compilation and central CPU handoff
from the exact current P21 snapshot. Original-unit review remains unreleased and must follow as P23;
recurring planning follows as P24. Never rewrite released P01–P21 migrations or manually edit a
snapshot. Generate migration/designer/snapshot via EF, inspect the exact delta, then add a reviewed
first-operation Down veto for retained principal-owned or null-worker records before dropping the
field or restoring NOT NULL. Consider retained consequential identity audit evidence too.

Fresh Up twice and used-P21 clone Up twice must preserve all old values/history. Unused Down/Up
must restore the exact schema; retained-data denial must leave full data/catalog/history unchanged.
Capture actual PostgreSQL CHECK definitions and update exact readiness/inventory manifests for the
real final sequence, preserving existing constraints. Do not bypass pending-model/migration guards.

## Authored acceptance evidence (not yet executed)

- Pure ownership and model tests: explicit principal with no employee; other creator/worker alias
  cannot inherit access; exact legacy creator only; no backfill; immutable persisted identity;
  nullable distinct model fields and scoped index.
- Mapped HTTP through real Core scope middleware: no-employee system user creates with company
  default currency, ignores forged client identity fields, reloads, edits, submits and withdraws;
  cross-user/company denial, stale version denial and unchanged denied-operation footprint.
- Employee deactivation with active Core account: read/edit/withdraw retained request continues,
  original worker is preserved, and a new request has no fabricated worker.
- Current Core permission/account revocation denies stale claims before effects.
- Canonical legacy creator can edit/withdraw without populating the new field; noncanonical legacy
  attribution remains inaccessible and cannot be mutated.
- Operational tests preserve genuine worker pins when immutable principal metadata exactly equals
  the already-pinned canonical creator; mismatched principal and missing employee remain denied.
  Existing source and SoD checks stay in force.

The shared host identity agent owns scope resolution and central fixture Core role seeding. These
route/model tests must run against that source, followed by actual PostgreSQL HTTP and Playwright.
No test handler may assign roles/permissions from request headers into Core merely to make a test pass.


## Owning-store review correction

For Modified requests the guard now reads persisted identity and version through the same scoped
Procurement context before trusting EF OriginalValues. Detached callers cannot substitute both
current and original owner/worker/creator values and transfer ownership with a valid version.
Missing/out-of-scope or stale persisted versions raise a concurrency error; mismatched identity
raises immutable-identity denial. Added negative tests cover all three detached identity forgeries,
stale CAS and foreign-company attachment, with independent full request/line/audit/outbox snapshots.
These regressions are authored, not executed.

Added null-principal rows remain compatible with existing trusted owner/migration fixtures and
historical domain graphs; there is no public request input for this path, and the actual create route
always requires and stamps its resolved Core principal. This is a documented infrastructure boundary,
not permission for normal runtime code to seed historical identity. A separate legacy import use case
would need explicit ownership, validation and review; no new runtime bypass or fixture flag was added.

## Selected review scope consistency

Own-request queries, submitted review reads and return-for-changes commands now share the same
selected/confined branch and selected department predicates. An organization-wide reviewer selecting
branch A cannot read or return a request from branch B merely because the account has broader
company rights; clearing that selection restores its actual company scope. The same rule applies
to selected departments. The shared query also denies a missing/empty resolved Core principal.
Existing review permission, self-review, state, version, approval-link and Finance-release guards
remain in force.

Two mapped HTTP theory cases (branch and department) seed real current Core reviewer permissions
without employee linkage, submit a request in scope B, prove selected-A read/return denial leaves the
complete owning footprint unchanged, then prove selected-B read and authorized company-scope return.
These cases are authored but not executed; no migration/model change is involved.
