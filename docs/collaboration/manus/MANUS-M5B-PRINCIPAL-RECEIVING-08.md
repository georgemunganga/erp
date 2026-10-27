# Manus milestone 08 — Core-user receiving and returns

Status: ready to claim from the published prerequisites below. This is a full
receiving milestone, not permission to deploy or to replace the existing UI.
If the independent C04 scanner work is already active, record its handoff/state
before claiming another implementation stream. No new scanner daemon belongs on
the coordinator's constrained production/build host.

## Published prerequisites and coordination

- ERP audited main prerequisite: `fcdafac34351b3e0d312b4e49ff949dc2d1c0ede` (includes M5a PR119 and handoff).
- UI accepted main: `f1e237713d5f803212ec2ae72773be842ab0e3b6` (PR89, following PR87).
- Tested runtime products are backend `20985344252b0b394939cfceb87152d49f39e2e2`
  and UI application `720b108` (final tests `ba25a24`, documentation successor `f03a532`).
  Earlier M5a actual-UI acceptance used `79a19df`; current UI adds two reviewed
  navigation guards and permission-based guidance, with 587 units/build and
  all 444 mocked scenarios covered by full run plus affected-file rerun.
  See the [current UAT/gap register](../../procurement/gap-closure/PROCUREMENT-UAT-AND-GAPS-20261027.md);
  regression coverage does not replace this milestone's native receiving acceptance.
- Fetch current main and branch from these commits or published descendants.
  Never start from the older P31/P33/P35/P37/P38 or vendor task branches.
  Their implementations may inform a focused port; their mixed ancestry,
  migrations and snapshots are not accepted prerequisites.
- Claim through a draft ERP PR and `STATUS.md`; link a focused UI PR, using a
  fork if upstream is read-only. Issues are disabled in the ERP repository.
  Include exact starting/working SHAs, owned paths, tests and remaining defects.
- Root owns M5a production release, global Core/auth/settings, and M6 invoice /
  payment contract auditing. Manus owns the receiving milestone below. Do not
  enable production flags, change production accounts or deploy from this task.

## Outcome and boundaries

Implement the existing Procurement receiving experience end to end for a current
Core user with the appropriate action and data scope, whether or not that user
is an employee:

`Issued PO → draft receipt → delivery/service recording → inspection/certification
→ acceptance → controlled correction/return → auditable integration handoff`.

Preserve the imported list/detail/form layouts, tabs, toolbars, empty states and
shared ERP header/sidebar/buttons/inputs. Map them to real contracts. Do not
replace them with simplified parallel pages, browser-only data or a second
receipt/stock/asset register. Reuse the existing goods receipt, receipt-line,
service, inspection and return entities where appropriate; document any required
schema extension. Inventory owns stock, Assets owns the asset register, and
Finance owns accounting, credits and payments. An outbox event is not evidence
that another module posted anything.

Company/legal entity is the selected Core organisation. Default currency comes
from its settings. All is a permitted-branch read context, not an instruction to
post to an arbitrary branch; every mutation needs a permitted branch. Roles and
current permissions authorize people; job titles and employee records do not
provide implicit Procurement access. Preserve the existing PO issued-version,
source bindings, funding, Core approval and original-key receipt contracts.

## Required work and acceptance

| Capability | Required result |
|---|---|
| Existing-screen contract inventory | Map every receiving/inspection/return control to its actual read/command, DTO, permission, branch scope, state, persisted fields and side effects. Preserve page-specific functions. Identify unsupported controls explicitly; do not hide them by rebuilding the screen. |
| Draft CRUD | Create, read, edit and remove/cancel eligible drafts using current versions. Lists filter/sort/page through authorized server data. Required fields, empty/error/denied/loading/success states work. Records survive reload. Approved/accepted history is never erased. |
| Goods receipt | Link exact PO/line/issued-version and delivery location; record partial deliveries, accepted/damaged/rejected quantities, delivery note and dates. Remaining quantities reconcile across multiple receipts. Default zero excess unless an effective approved tolerance/exception permits it. |
| Inspection | Retain checklist/result/inspector and supporting references. Inspection-required quantities cannot become accepted before the configured result. Partial acceptance and rejection retain original evidence and reasons. |
| Service acceptance | Use service period/milestone, scope, quantity/value and evidence references; certify only the authorized remaining PO scope. Avoid forcing warehouse/stock fields onto services. |
| Corrections and returns | Accepted records use linked reversals/adjustments or returns, not overwrites. Record return reason, quantities, authorization and confirmed return/replacement/credit references. Do not release original approved demand or invent a supplier credit/payment automatically. Define invoice/credit exceptions when a returned quantity was already invoiced. |
| Shared controls | Reuse current Core identity, permission/data-scope resolution, approval/delegation where required, documents, audit, notifications and transaction infrastructure. No fabricated employee, no permission widening, no new global settings service. Procurement owns its versioned receipt preferences/tolerances. |
| Integration handoff | Accepted goods/returns and asset candidates publish versioned, retry-safe durable events through the shared outbox. Report handoff pending/accepted/failed truthfully. Do not maintain duplicate stock, asset, budget or payment balances. |
| Reliable commands | Persist the action, exact evidence and required audit/outbox atomically. Use original-key recovery and stale-version rejection; double clicks, retries and concurrent partial receipts must not double count quantities or events. Preserve historical recovery when new work is disabled. |
| Readiness and migration | Reuse the finite execution transaction and reviewed readiness pattern. Capture genuine generated EF migration/snapshot changes and exact Up/Down behavior. Preserve canonical M3b/M5a rows, checks, original response bytes and legacy/principal route separation. Never weaken parent checks to make a test pass. |

Draft CRUD should work with safe configured defaults. Positive acceptance still
requires its real source, current permission/scope and applicable policy; missing
policy cannot be replaced by a hard-coded monetary threshold or a fake approval.
Document what is configurable and who owns it.

## Specification and owned implementation

Before implementation, audit the current source and complete/update the existing
28-section child-feature specification for Goods Receipt, Service Acceptance,
Inspection and Returns, using the existing feature ID registry. Cover workflow,
transitions/guards, permissions/data scope, rules, fields/ownership before UI,
events, audit and tests. Use GW-ENG-STD-001 and the module's established record
ownership. No duplicate specification IDs or renamed modules.

Own narrow receiving/inspection/return domain/application/infrastructure files,
receiving routes/contracts/tests, their existing UI adapters/screens and task
records. Shared registration/readiness or permission-catalog edits must be small
and declared in the PR. Preserve the four newly reviewed All order queues. Do
not change sourcing, PO commercial amendments, invoicing/payments, global
branding/auth, the ERP shell or another module's ledger. Root will coordinate
migration order and review snapshot adoption; do not merge another pending
migration branch or run migrations against production.

## Verification and delivery evidence

1. Run meaningful rule, permission, state, quantity, decimal and integration tests
   with exact commands/counts and tested commit. Build/typecheck affected products.
   Preserve genuine failures and fix/retest; do not relabel mocks as persistence.
2. Rehearse migration Up and rollback/recovery on an isolated populated restore;
   preserve original rows and finite ACLs. Protect retained accepted evidence from
   destructive Down. Keep synthetic/protocol tests distinct from native evidence.
3. Use Playwright with actual normal Core cookies and real private APIs, including
   workerless authorized users. Complete two partial goods receipts, inspection,
   service certification, correction/return and reload/history paths. Exercise
   unauthorized actor, company/branch, All-write, stale-version, over-receipt,
   duplicate/concurrent command and flag-off historical recovery cases.
4. Independently reconcile the committed receipt/inspection/return records,
   original PO/issued-version bindings, net quantities, audit, outbox, old rows and
   session logout. Verify failed actions leave no partial business effects. No
   supplier delivery, stock posting, external Finance or scanner acceptance is
   claimed without its own actual proof.
5. Deliver focused ERP/UI PRs, 28-section specifications, UI/API mapping, generated
   schema changes, test evidence, defaults/configuration notes and release/rollback
   instructions. State completed and remaining operations explicitly. Root reviews
   and merges; production rollout remains coordinator-owned.

Use isolated hardware with bounded jobs and sufficient memory/disk. Stop and
clean only owned test processes/caches after preserving evidence. Never remove
production databases, backups, rollback artifacts, credentials or others' work.
No production secrets or customer documents belong in Git, logs or screenshots.
