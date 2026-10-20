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
- The readiness response also previews the pinned approval stages per request line. It flags lines with different routes and reports `canOpenCase: false`. The route preview is informational; authority limits, delegation, Core write access and atomic case opening remain required.
- The preview requires the pinned decision to cover every current active request line exactly once. Duplicate, missing, added or stale line IDs become named readiness blockers; no approval case may use that evidence.
- The read-only assignment preview looks up the requester's active HRMS line manager and active local procurement managers in the selected company. It excludes the maker and reports missing or ambiguous people. `BudgetOwner` stays unresolved until Finance supplies the cost-centre owner's authoritative identity; a department manager is not assumed to own the budget. Candidate names are guidance only and must be revalidated when an approval case opens.

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


## Next increment design — purchase-request approval execution

**Design status:** Proposed implementation contract, 20 October 2026. The sections below describe work to implement and verify; they do not enable a feature, establish approved purchasing policy, or claim production completion. P01 foundation evidence above remains historical evidence. Existing draft creation, editing and submission remain usable while approval prerequisites are unresolved.

### Boundary and supported first execution route

Deliver the complete path `open case → assigned queue/detail → decision → source outcome`, including history and recovery. Core owns reusable case/stage/assignment/decision execution; Procurement owns purchasing authority, submitted demand, policy and Finance evidence references, source transitions and business events. Do not call the HRMS leave approval service or duplicate Core execution tables in Procurement.

The first execution route supports one published, identical route across all active PR lines and sequential stages using the existing `One` and `All` policy modes. Translate `One` to a single resolved assignee/one required approval; translate `All` to all eligible assignees/all required approvals. A missing or ambiguous single assignee, mixed line routes, unresolved budget owner, unsupported mode or unavailable authority blocks case opening with named reasons. Do not replace them with an administrator, arbitrary candidate, sample amount or automatic approval. Broader routes, delegation and reassignment require their own verified contracts before activation; the first route must deny unsupported delegation rather than imply that stored delegation rows already work.

### Durable Procurement source and evidence contract

Add a Procurement-owned, append-only approval submission/evidence record linked to the Core case. Its minimum fields are:

- Tenant/entity, PR ID, stable submitted business version, Core case ID and opening command key/fingerprint.
- Immutable normalized submitted header/active-line snapshot and digest, including line IDs, quantities, unit prices, category/coding, currency, dates, supplier/catalog references and versions, tax/discount references and amount basis. Preserve original snapshot values alongside the digest so an auditor can reconstruct the decision.
- Policy evaluation ID, published policy ID/version, pinned route and the exact active line set. Policies must match the submission; changes cannot silently rewrite a pending case.
- Finance snapshot ID, Finance check/evidence/reservation references, checked amount/currency, reservation state and as-of time. Distinguish approved manual evidence from an automated Finance response; record the approved evidence mode. A reference typed by an operator is not proof of an external reservation API call.
- Authorizing amount, currency and named calculation basis, and versioned authority evidence for each stage/assignment: authoritative principal ID, permission, company/branch/department/category scope, effective dates and configured amount eligibility. Never infer an individual's spending limit merely from a role name.
- Opening actor/time/correlation and final source outcome/effect key through append-only completion evidence or audited source transition, retaining previous submissions after return/resubmission.

Enforce one execution instance per tenant/entity/PR/submitted business version, and one opening key per scoped operation. Core already uniquely indexes source module/type/ID/version; retain that constraint as a second guard. Database constraints must prevent foreign-scope references within each owning schema. Keep cross-module references explicit without foreign keys into HRM tables.

`PurchaseRequest.Version` remains an optimistic concurrency revision and increases for approval linkage/state updates. It is **not** the immutable submitted business version after a case opens. Pin `SubmittedVersion` and the submission snapshot once; opening requires the expected current revision, and every decision verifies the linked case, submitted version and unchanged business snapshot. Do not reuse the current readiness test `Version == SubmittedVersion` after operational updates. Request edit/withdraw/return paths must respect a linked active case; a new editable business revision requires closure of the old route and appropriate Finance release first. Finance evidence for an active case refers to the pinned submitted version rather than the latest operational header revision.

The current supported policy basis is `EstimatedLineBase` (quantity × estimated unit price before discounts, taxes and delivery). Display that basis explicitly. Tax-inclusive or FX-based authority must remain unavailable until its calculation, currency source, rounding and policy are configured and tested. No default financial threshold is introduced by this increment.

### Identity and authority resolution

Use one canonical authenticated principal identifier shared with the ERP identity service. Resolve HRMS worker/reporting-line references to active identity accounts; do not mix `Worker.SubjectId` and `LocalUser.Id` without a verified mapping. A worker's company membership must be current and an account must be active. Preserve the identity mapping and authority reference used at opening, then recheck current eligibility at each decision.

Assignments require explicit action permission **and** applicable record scope **and** configured authority for the pinned amount/category/currency. Current HRMS line manager is organizational evidence, not an automatic spending-authority grant. A requester/maker cannot decide their own case. `BudgetOwner` requires Finance's authoritative cost-centre mapping; department ownership is insufficient. Missing authority produces a blocker and leaves draft/submission work available. Revoked identity, permission, membership or authority blocks a pending decision and exposes an operator action; it must not silently select a replacement.

### Proposed HTTP contract

All routes use the existing authenticated `/api/procurement/v1` boundary and validated shell company/branch context. Backend authorization is mandatory even if the UI hides actions. Action identifiers below are proposed additions to the existing action catalogue; do not grant them broadly as a side effect of deployment.

| Operation | Route and command | Required access / result |
|---|---|---|
| Open | `POST /requests/{id}/approval-cases`, UUID `Idempotency-Key`, body `{ expectedRequestVersion }` | `procurement-approval-open`, scoped submitted request; resolve all guards server-side. Return `201` with linked case/submitted version/source revision; an exact replay returns the original case without new effects. |
| Assigned queue | `GET /approvals?state=Pending&limit=…&offset=…` | `procurement-approval-read`; return only actor-assigned active tasks within current scope, with request number, amount/basis, stage, age and task/case revisions. Paginate and bound results. |
| Task/detail | `GET /approvals/{caseId}` | `procurement-approval-read` plus assignment/current record scope; return pinned request evidence, active/pending stages, allowed actions and decision history. A decision permission alone does not grant unrelated records. |
| Decide | `POST /approvals/{caseId}/assignments/{assignmentId}/decisions`, UUID `Idempotency-Key`, body `{ action, reason, expectedCaseVersion, expectedAssignmentVersion, expectedRequestVersion }` | `procurement-approval-decide` plus assignment, current identity/authority/scope and maker/checker checks. Supported actions `Approve`, `Reject`, `Return`; reasons required for reject/return, bounded for all. Return persisted case/task/source outcome and current revisions. |
| Requester history | `GET /requests/{id}/approval-history` | Existing own-request read authorization and ownership, or explicitly authorized review scope. Return permitted history for all submitted versions without revealing unrelated tasks or sensitive authority data. |

Return `401` for no session, `403` for missing action permission, `404` for inaccessible/foreign-scope records, `422` for invalid input, and `409` for changed version/state, unmet policy/funding/authority, or a reused key with conflicting command. Return `503` when required schema/service access is unavailable. Include stable machine codes and plain-language next steps; never echo restricted Finance evidence or private identity data in errors.

Store an idempotent command fingerprint including action, assignment, normalized reason and expected source/evidence intent. Check exact completed-command replay after current access/assignment eligibility but before stale operational revision/terminal-state rejection, so a lost successful response can recover. A changed reason/action/target under the same key is a conflict. Replay returns the recorded outcome and never adds a second decision, approval quantity or outbox event.

### Atomic persistence and concurrency

Core and Procurement remain separate owning contexts/schemas in the same PostgreSQL database. Compose a dedicated application transaction using **one physical Npgsql connection and one database transaction**, enlisted into both contexts with `UseTransaction`; opening two independently configured connections and saving sequentially is not atomic. Provision a least-privilege execution role with the required reads and constrained Core/Procurement writes, separate from migration ownership. Do not use a broad production owner credential as the runtime fix.

Within a serializable transaction, lock the source request and linked case in a consistent order, load current lines/policy/Finance evidence and eligibility, validate expected revisions, perform the Core operation, update Procurement state, append explicit audit and insert durable outbox records, save both contexts, then commit once. All writers to relevant Finance/source state must follow compatible serialization/locking so a concurrent reservation release cannot race final approval. Refresh database-derived identity/authority evidence within the transaction; external authority cannot be assumed atomic, so pin versioned evidence and define expiry/revocation behavior. No email, webhook or remote Finance call is performed while holding database locks.

An assignment decision must also advance the case concurrency revision even when its stage has not reached quorum. Otherwise two `All` decisions can both observe insufficient approval count and leave the stage incorrectly pending. Preserve immutable decisions, source unique indexes and decision-key constraints. Treat PostgreSQL serialization/deadlock failures as rollback of the **whole** unit; retry only a bounded whole command with the same key and fresh reads, or return a retryable conflict. Dispose failed contexts/transactions before retry. Do not retry only the last `SaveChanges`.

Outbox messages are inserted in the same transaction as the authoritative decision/source outcome and use stable event/effect keys. Dispatch/retry/dead-letter handling occurs after commit; failed notifications never reverse an approved database outcome. If the Procurement outbox is not yet dispatched operationally, expose that limitation and implement its recovery path before claiming notification delivery.

### Source outcomes and Finance safety

| Core outcome | Procurement effect |
|---|---|
| Open/pending or intermediate approval | Keep PR `Submitted`; set an explicit pending approval state and case reference. No line becomes orderable. |
| Final `Approved` | Revalidate pinned source/policy, current authority and valid full-amount reservation; atomically set PR approved state and each active line's approved quantity/status. Preserve cancelled lines and avoid resetting allocation/receipt counters. Publish one versioned purchase-request-approved event. Only subsequently verified conversion logic can consume available approved quantities. |
| `Returned` | Preserve decision/reason and close Core case. If the linked reservation remains confirmed or release is unknown, set a non-editable `ReturnPendingFinanceRelease` source state; create a durable Finance release/reconciliation request. Only authoritative release evidence allows the audited transition to `ReturnedForChanges`. Never reuse the existing pre-case return handler to bypass release. |
| `Rejected` | Preserve rejection history and make demand non-orderable immediately. Track reservation release separately with an owner and retry/reconciliation state; do not report budget as released until Finance confirms it. |

Release integration is Finance-owned. The first execution implementation may accept only the approved evidenced manual release path until an API exists, but it must persist the release request/reference, outcome and audit and provide an authorized resolution action. Unknown outcomes remain pending; do not fabricate a release or create another reservation on retry. Resubmission creates a new immutable submitted version and new case; older cases cannot authorize it. Cancellation/reassignment/delegation remain denied until their full source/Finance effect contracts are implemented.

### Required verification and activation evidence

- Open using a published matching policy, active canonical assignees, configured authority and confirmed permitted-mode Finance evidence; prove queue, detail, intermediate decision, final line outcome, history and outbox from real API calls and the existing UI.
- Deny missing/unpublished policy, missing/extra/stale line evidence, mixed routes, ambiguous/inactive manager, unresolved budget owner, missing/expired authority, unsupported currency/basis/delegation and requester self-decision. No denial opens partial tasks or changes approved quantities.
- Deny direct-URL/API foreign tenant/company/branch/department/task access, unrelated requester history, revoked grants and inactive identity. Test read/export surfaces as well as mutation.
- Test insufficient/partial/not-configured funding, missing reservation/evidence, stale source version, replaced/released reservation, expired evidence when a freshness policy exists, and Finance outage. Never manufacture a success from timeout.
- On real PostgreSQL, inject failure between each context save and before commit; assert no half-open case, dangling source link, lone audit/outbox record or partial approval. Exercise concurrent open attempts, concurrent `All`/quorum completion, approve versus reject, final decision versus Finance release and return versus source changes.
- Replay successful open/decision after a lost response, including terminal cases; assert one case/decision/source effect/event. Changed command payload under the same key conflicts. Verify bounded serialization retries and recovery after process restart.
- Test operational version increments after case opening, unchanged submitted snapshot, attempted business-input mutation, return pending release, confirmed release, edit/resubmit and historic-case denial against the new revision.
- Verify additive migrations/constraints, least-privilege grants, migration idempotency and rollback compatibility on a disposable database. Keep audit/decision history immutable and retain evidence after account deactivation.
- Run focused browser tests for assigned queue, empty/loading/denied/error states, plain-language evidence, reason validation, stale task refresh, lost-response recovery and requester outcome visibility. Use existing ERP components and screens.

Keep `approvals: false` until the complete supported route, source/Finance guards, schema/runtime grants, real PostgreSQL concurrency tests, HTTP authorization checks and browser evidence pass. Then expose readiness per company/policy rather than treating a global capability flag as spending authorization. Production activation additionally requires the existing shared-host compatibility and coordinated release gates; this design does not claim those gates have passed.

### Atomic execution infrastructure and spending authority rehearsal — 20 October

The isolated integration candidate passed **180/180 Procurement tests** and **20/20 Core approval tests**. P22 (`20261020084544_P22SpendingAuthorityConfiguration`) adds only grant/category tables; its model matched the compiled snapshot and idempotent SQL applied twice to disposable PostgreSQL17 with19 Procurement migration records.

The shared-connection coordinator passed actual PostgreSQL rollback before saves, after either context save and after both; one-commit source/Core/audit/outbox; stale intermediate vote rejection, fresh quorum completion and terminal replay; bounded confirmed-abort retries; real EF SaveChanges wrapper retry; cancellation; and mismatched-scope denial. A separate PostgreSQL authority probe passed independent publication, atomic supersession rollback including audit, revocation without old-limit revival, immutable fields and concurrent logical-version allocation.

Testing exposed and corrected replacement category graph state, an oversized synthetic request number, an EF relational package conflict, and Npgsql wrapping serialization failures in InvalidOperationException. First-failure logs are retained alongside final results. Protected evidence and SHA-256 manifest: `/home/amizo/erp-backups/procurement-approval-transaction-20261020/`. Tested retry fix source is `1b28f54` on `codex/procurement-approval-transaction`.

These checks prove the named infrastructure and persistence behaviour. They do not prove the newly written full HTTP command path, real user sessions, production database grants, operational notification delivery or production activation. The mapped HTTP/PostgreSQL workflow and browser integration remain separate gates. No production API/schema change was made.

### Policy supersession and pending cases

Publishing a new policy currently retains the predecessor's `Published` state and closes
its open-ended `EffectiveTo` at the successor's future start. The audit action is named
`Superseded`; it is not a state change. New submissions select the policy effective at their
submission time. A case already pinned to the predecessor continues to validate that exact
policy ID/version and canonical rules hash against its original submitted timestamp, even
when work finishes after the successor starts. A current wall-clock check must not strand it.
Draft, pending-review, revoked or otherwise unrecognized policy states do not authorize an
existing case. An explicit revocation workflow is not supplied by this increment; changing
published rule content remains a pin mismatch, not an implicit reroute. Scope, published
status, policy version, effective-at-submission bounds and pinned hashes remain mandatory.

### Execution database access and activation

Approval execution is disabled by default. Activation additionally requires an explicit
`ConnectionStrings:ProcurementApprovalExecution` connection. With the feature enabled but
that connection absent or blank, execution routes return `503` with
`ApprovalExecutionDatabaseNotConfigured`; readiness reports the same unmet setup check.
The shared host connection is not an approved substitute for the dedicated execution role.
Normal module draft/read contexts can retain their separately managed connection.

Before production enablement, provision and verify a least-privilege execution role in the
same PostgreSQL database. It needs the scoped Procurement source/line reads and controlled
updates, append-only evidence/audit/outbox inserts and reads, Core case/stage/assignment
execution inserts and controlled updates, and Core decision insert/read. It must not have
`UPDATE`/`DELETE` on decision, submission, command receipt, authority-pin, outcome, release,
confirmation or audit history. Outbox delivery updates belong to a separate publisher role.
Shared identity, worker/reporting-line, company/location/department, role and branch-grant
access is `SELECT` only. The canonical account adapter projects the required account linkage,
lock status and roles; Procurement execution does not need password hashes, payroll, bank
records or identity master writes. No runtime role receives migration ownership.

Core and Procurement contexts enlist one physical execution connection/transaction, including
the read-only Core identity/organization adapter. Verify role/schema access, unauthorized
history mutations, company/tenant isolation, rollback, and a real mapped HTTP workflow under
that dedicated role before enabling the feature. The disposable HTTP probe explicitly supplies
its own isolated test connection; a synthetic database owner/superuser run does not prove
production grants. Protected deployment configuration, role credentials and review evidence
must be recorded in the operational release pack. Capability stays off until these gates and
the browser/source/Finance checks pass.


### Retained worker identity and offboarding compatibility

New submissions use snapshot schema version 2. At opening, the transaction resolves active
canonical maker/requester identities and each assigned principal's worker, then retains
`MakerWorkerId`, `RequesterWorkerId`, and `AssigneeWorkerId`. The submission identity digest
binds tenant, entity, source, submitted version, Core case, maker principal and both workers.
Assignment `EvidenceJson` is an envelope containing its worker and exact finite authority
result; its digest binds both. New assignment writes validate source/case, immutable grant
identity, current revision, amount/context, worker evidence and digest. There are no Core FKs
and no copied identity masters.

Existing schema version 1 submissions remain unchanged. New columns are nullable for
migration compatibility, with an explicit v1-all-null/v2-complete database check. Historical
assignment rows lacking their worker envelope remain unchanged. Read-side execution and
release reject missing/legacy or inconsistent evidence with an explicit blocker; the upgrade
never guesses a worker from today's possibly changed account mapping. A separately approved,
audited historical identity-resolution migration would be needed for such legacy cases; no
runtime bypass is supplied.

Approvals still require current active canonical assignees, current permissions, current
finite configured authority and all other source/policy/Finance checks. Release after a
terminal return/rejection instead compares the current active authorized Finance verifier
against retained maker/requester workers and the terminal decision assignment's retained
worker. Historical offboarding, locks or account deactivation do not require reactivating
those people to release the original reservation. Alias accounts for those same workers
remain ineligible, including exact-retry paths. The original Finance evidence, full amount,
currency, reservation, controlled manual policy and independent verification requirements
continue to apply. Tests deliberately mutate only disposable fixture evidence to prove
legacy/digest mismatch rejection and use real mapped HTTP to confirm independent recovery
after historical worker/account offboarding. These source assertions are not a test result.

### P23 migration verification checkpoint — 20 October 2026

`20261020095652_P23PurchaseRequestApprovalExecution` adds five Procurement-owned
execution/effect tables and the nullable retained worker identity fields. Composite local
foreign keys bind assignment evidence to its submission/case and immutable grant identity;
outcomes and release evidence bind the exact submitted source and reservation. No foreign
key or migration changes a Core, HRM or Finance-owned table.

The combined source passed **213/213 Procurement Release tests**. Infrastructure then
built with zero warnings/errors and EF reported no pending model changes. The generated
idempotent SQL was applied twice to disposable PostgreSQL 17: both runs succeeded, the
history contained 20 migrations, and all five new tables were present. Private logs/SQL/TRX
are retained under `procurement-approval-transaction-20261020` in the operator evidence pack.
This is schema and unit verification; mapped shared-host, restricted-runtime HTTP and actual
API browser gates are still pending. Production remains on P19 with execution disabled.

P23 preserves legacy rows without guessing historical worker identities. Once v2 submissions
or execution evidence exist, **do not run the Down migration as an operational rollback**:
it removes retained evidence and the prior constraint cannot represent v2 submissions.
Disable execution and use a compatible code rollback or reviewed forward repair. A destructive
schema rollback requires a separately verified database restore and reconciliation plan.

### Submission evidence verification — 20 October 2026

P21 (`20261020073918_P21PurchaseRequestApprovalSubmission`) adds immutable Procurement submission evidence and scoped source/evaluation/policy/Finance relationships. Its idempotent SQL applied twice to isolated PostgreSQL17, retaining18 Procurement migration entries. The final source passed **133/133** Procurement tests and the real PostgreSQL probe: unchanged evidence survives jsonb property ordering/number formatting and UTC microsecond timestamps, eight-decimal amount roundtrip remains exact, operational source revisions preserve the submitted version, and uniqueness plus cross-tenant/entity/source/version/policy/Finance mismatches are denied. Decimal canonicalization rejects lossy precision rather than using floating point.

Protected SQL, logs and TRX: `/home/amizo/erp-backups/procurement-approval-submission-20261020/`. This proves submission evidence/constraints, not a real Core case, approval decision, spending authorization, atomic multi-context execution or production activation. Authority configuration, actual command execution and source outcome work are being verified separately. Production remains throughP19; P20/P21 are not applied by this evidence.
