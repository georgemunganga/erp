# M5a — Core-identity purchase order governance

Status: private migration rehearsal, bounded API lifecycles and a fresh ten-action form-driven UI journey passed. Scope regression checks and release gates remain open. Not production-enabled or Procurement-UAT-complete.

## Latest verification — 27 October 2026

Normal-password/Core-cookie testing found two integration defects: the PR approval
readiness preview required the unrelated return permission, and an explicit All
branches PO command could inherit the middleware's default branch. The first fix
admits approval-open users while preserving existing return-review access; read
permission alone remains insufficient. The second checks the incoming All header
before governed writes, even when scope resolution supplies a branch. No role,
authority limit, company boundary or record scope was widened.

Product source `d901e3c048b386b8a88e44a353798144cb051fe2` built successfully,
passed 55 focused Host tests without failures or skips, reported no pending EF
model changes and produced the genuine 150-file API publication. The checks
include mapped HTTP preview permissions and seven middleware/default-branch
command regressions. Receipt SHA-256:
`5e2d2ab73efbaf9ea1b5b7f38bbf73febece9dad3b16be97cc29e5e78fd02511`.
These are focused results for this source, not a new full-suite result.

The isolated real-cookie continuation retained its original PR and PO after each
failed assertion. It completed independent PR/PO approvals, manual commitment
attestation, internal issue and manual acknowledgment. A genuinely dropped Issue
response was recovered with the original key using GET only, after exactly one
POST. A fresh unfunded draft rejected All-scope submission with
`OrderBranchRequired`; its version remained unchanged and no Submit receipt was
created. Ordinary draft cancellation released that draft's allocation. The
positive PO has two units Ordered and four approved units remain available.

Read-only owner reconciliation verified the shared Approved Core case, assignment
and decision links; eight receipts with intact response hashes; eight business
audit actions plus seven automatic entity audits; eight distinct outbox events;
the exact funding amount; null worker aliases; and the five original PO rows
unchanged. All five test sessions were logged out at the API checkpoint. Owner
receipt SHA-256:
`490fb7937cffeaff18885b985f32564b4caf65251fde6b41094125fa0b833126`.
The failed All-write test remains retained as a failure; its committed original
submission was recovered rather than repeated or relabeled as a rejection.

UI source `9b2defac773376db7be07e60792a2497f0b83fdd` restores the earlier
tax column and approved-request link in the governed PO detail. It passed type
checking, 47 focused transport/journal/rule checks and the live build. Real-cookie
Playwright verified buyer list/detail/tabs and Finance, reviewer, issuer and
dispatch queues using the existing ERP components, with no page errors, API
failures or page-driven business writes. Owner checks found no changed Procurement
or Core approval rows and zero active test sessions. UI owner receipt SHA-256:
`32c1e3cae3aae18deea29260eb4187bfd5d5cb7476de507a5c3ef8ef13b06664`.
This is page-read acceptance, not full imported-feature parity or form-driven UAT.

Four further real-cookie workflows passed on the same backend source: Return,
Reject, pending withdrawal through the actual shared Core cancellation engine,
and release of a funded Draft. Each retained Reserved allocation until independent
manual Finance confirmation, then cancelled the PO and restored the source. The
owner reconciled three retained Core cases (Returned, Rejected, Cancelled), four
release requests/confirmations, 21 receipts/business audit actions/outbox events,
17 automatic entity audits and the seven previously retained PO rows unchanged.
The approved source returned to six available units. Owner receipt SHA-256:
`00d63bc4cea57ef85890ec87100fd35f0a115295ec923cb7fc4d77861f0d5391`.

The next real-cookie test retained a successfully funded Draft whose Finance
detail incorrectly returned `409 OrderFinanceEvidenceChanged`. The read guard
treated funding evidence in Draft as corruption even though capture intentionally
leaves that status unchanged. Source `48cc33019dcfec7afe874e1493743f846125829b`
allows that valid read, retains all source/amount/snapshot integrity checks and
removes the second Funding action. Ten state/evidence cases cover the guard.
It passed 65 focused Host tests, no pending model changes and the genuine API
publication. SDK receipt SHA-256:
`6006c49be9231f2ea45129eb52f2f8191a8118fe828cfb7698392409a16f8410`.

The same original order then returned Finance detail successfully, with the same
funding evidence and no Funding action. Its original Funding key recovered the
existing 200 result by GET only; no business POST was performed. The Finance
actor had no buyer-wide PO read permission. Owner reconciliation found all
Procurement/Core rows and all 211 captured table schemas unchanged. Receipt:
`7340da100dfea965544f66d081b416d3a58dea59ca0a013482499e695eb0c9a2`.

UI source `79a19df28b1d3e95d4ab8798c7f27cd1ff53f9d0` additionally preserves
the currency supplied by the assigned-review API without converting its exact
amount to a JavaScript number. Completed purpose-only issue, acknowledgment and
release tasks return to their current queue rather than reload a closed detail
endpoint. Type checking, 51 focused UI tests and the live build passed. The
form-driven five-role workflow and lost-response/reload journey remain in progress
at this documentation checkpoint; these source changes are not yet UI acceptance.

Complete form-driven workflow UAT remains separate. Manual Finance attestation is not an external Finance
integration, and manual response observation is not supplier delivery. Production
flags, schema, grants and deployed binaries are unchanged by this private work.

## Latest verification — 26 October 2026

Source `1c53a0721a1187488f8925372a3e2c9370e912a8` passed the actual offline SDK
build, 1,448 Procurement tests and 1,356 Host tests, with no failures or skips.
The focused counts were 13 and one. EF reported no pending model changes and
exported the genuine guarded transactional migration scripts.
SDK receipt SHA-256: `fc2c628d220ae3f5b9da6a3f7b831dbc43f5cede6a5f109059c9ada81999a1e2`.

The private nonempty PostgreSQL rehearsal cloned the retained 203-table source,
applied the migration to 211 tables, rolled back the empty increment in a
transaction and reapplied it. Retained rows and captured schema were preserved;
14 separate rollback-only marker families each produced SQLSTATE `55000` and
left the target unchanged. These deliberately constraint-relaxed markers prove
the rollback guards, not valid business workflows. Production was untouched.
Native receipt SHA-256: `35cc665fdfd0e4f45ef6b86a11e68722b0193575699b82c0256a03418659d0d0`.
The 22 physical CHECK definitions were captured with `pg_get_constraintdef(oid,false)`;
capture SHA-256: `a48bdc7bff64636bd4b416540d9fa53fa73ec5df3ffdb3f73b82f9cf86527bc0`.

The following source increment adds a dependency-free physical inspector,
same-connection readiness composition, narrow parent compatibility, and governed
buyer list/detail reads. Submit/Issue previews share the command guards and
remain subject to revalidation on save. The original M3b Create/Cancel contracts
are retained; funded drafts expose a release request instead of ordinary cancellation.
This subsequent source requires its own build and actual finite-role/API/browser
verification. No new operational flag has been enabled in production.

The follow-up source `108d442d4ca7c385a09ed6c76d4b0610919a46af` then built,
passed 35 focused Host checks, reported no pending EF model changes, and produced
a genuine 150-file API publication. Receipt SHA-256:
`31a681c403ac15e3286c3fe00490c1426fdeef54f8fa7f9a7ea8b12763138747`.
The first readiness build failed on a missing namespace import; that failed
receipt remains retained and is not relabeled as a pass.

The eight new private tables now have verified SELECT/INSERT-only execution
grants, with all original table/column ACLs and business rows preserved at that
checkpoint. Five separate workerless private actors have only their required
buyer, Finance, reviewer, issuer or dispatch actions and one permitted branch.
These are private identity/ACL fixtures, not production grants or business UAT.
The newest source additionally exposes the immutable source digest directly on
the assigned review and aligns read/write metadata with explicit branch scope;
that small follow-up still needs verification against its own source revision.

Revision `caa14f87cdbe6410eb4a859710fb2f5e5a1fc9b8` subsequently passed the
same 35 focused checks, no-pending-model check and genuine API publication
(receipt `35ae1b98f00668ad0dfb25cccad01e8637219ef381151327508226ede7517e0a`).
An actual private normal-cookie configuration run then activated the PR authority
but correctly refused PO authority creation: the PO schema gate was mistakenly
given the configuration Record connection while requiring the Execution account.
The failed run is retained; the PO grant and policy records were not created,
and both configuration sessions were logged out.

The current fix asks the configured finite Execution connection for read-only
physical proof and retains the existing Record-role authority-write guards.
It neither widens Record permissions nor relaxes expected execution identity.
Three regression cases cover the connection dispatch and fail-closed behaviour.
The fix needs its own SDK and real-cookie continuation; this is not a passed
business workflow or release acceptance.

This increment implements the governed lifecycle following the existing M3b
internal PO draft. The feature specification remains
[PROC-08](PROC-08-PURCHASE-ORDERS-AND-AMENDMENTS.md). It reuses the existing
purchase order, lines, approved PR source bindings, allocation records and
command receipt store. It does not introduce a second PO master or Finance
ledger. A Core user with the required action and record scope may participate
without an employee link.

## Implemented source boundaries

- A separately disabled `PrincipalExecutionEnabled` option and physical
  readiness contract gate new operational routes.
- Immutable evidence records cover manual commitment attestation, submission,
  assigned authority, Core decisions, authorized internal versions, observed
  supplier responses, release requests and confirmed external release.
- Submission opens the existing shared Core approval case. Procurement pins
  commercial terms, policy, source, funding and assigned authority; Core owns
  the task mechanics and decisions.
- Positive approval and issue revalidate current source, policy, scoped actor
  and authority. Return/reject retain current actor and assignment checks but
  do not require renewed financial authority merely to stop the transaction.
- A funded PO cannot use the original unfunded draft cancellation route.
  Release requests preserve the source allocation. Only independently
  confirmed external release frees the allocation and cancels the order.
- A buyer withdrawing a pending order invokes the real Core cancellation
  engine in the shared transaction, cancels its unfinished tasks and retains
  exact Core case/decision IDs. This is a withdrawal, not an approval. It does
  not depend on an offboarded approver recovering access.
- Manual supplier-response evidence retains the exact dispatch procedure
  reference, observed time and recorded time against the frozen authorized
  version. Recording it does not send anything to the supplier.
- New command responses have a strict typed envelope linked to the exact
  evidence, order, actor, operation, key and captured result version. Recovery
  returns the original historical response without repeating its effects.
  Existing M3b Create/Cancel response contracts remain separate.

## Segregation decision

Finance attestation and PO approval must be independent from the PO buyer and
the originating PR requester/submitter, including currently linked worker
aliases. External release confirmation must additionally be independent from
the release requester. Authority activation keeps its existing independent
review. Recording supplier acknowledgment is not another approval and does
not silently require an additional employee. Stronger stage-specific staffing
requirements require a documented business policy rather than hard-coded
headcount assumptions.

## Required completion evidence

Pre-integration source `3fbc01674c09ab2544639a6921ba2e0c460c7c4b`
compiled all affected projects and passed 12 Procurement and one Host focused
test in the actual offline SDK. The earlier compile failure is preserved;
the funding-evidence type qualification corrected it. This result is not a
full-suite or database workflow acceptance.

Accepted M4 main `63360f128911b0cd66eb4e44e0f79fabcd1a2345` was integrated in
`418c352b28eea51f477bda204fd5f5f6234c25d1`, preserving the typed PR, sourcing
and PO action/permission pairs and their separate gates. Genuine EF generation
from that source produced `20261026201259_M5aPrincipalOrderGovernance`: eight
new tables, 27 indexes, three CHECK replacements and receipt-operation width
10 to 20. The Designer and snapshot are unmodified generated files. Official
raw SQL and generation evidence are retained privately.

The adopted migration prepends an authored, transactional retained-evidence
veto before any Down operation. It covers the eight new tables plus new
receipts, advanced principal PO states, schema-2 PO authority, linked Core PO
cases and business audit/outbox markers. Existing M3b schema-2 Draft/Cancelled
orders with only Create/Cancel receipts are not themselves M5 history. The
DDL-order test and native marker/Up/Down rehearsal are not yet run for this
adopted checkpoint. Down must run inside the EF migration transaction; do not
use the unguarded raw scaffold or a no-transactions Down export.

No M5 schema, new operational capability or UI integration is deployed by this
checkpoint. Readiness registration and full native workflow acceptance remain
open.

Before merge and release:

1. Keep the accepted M4 source/model integration and typed PR/RFQ/PO authority
   union intact while implementing physical readiness; preserve each feature flag.
2. Compile all affected projects with the real SDK and run domain, authority,
   receipt, permission and Core transaction tests. Independently review the
   final diff.
3. Re-export and verify the adopted guarded migration with the official EF tool,
   then rehearse Up, empty transactional Down and all retained-evidence Down
   vetoes against a restored database. Preserve existing rows and old M3b
   Draft/Cancelled compatibility. Generated migration/snapshot adoption alone
   is not native acceptance.
4. Implement and register physical readiness with the exact new schema,
   constraints, histories and finite role rights. Preserve old M3b readiness
   compatibility deliberately; do not turn off the parent safeguards.
5. Verify that the inherited legacy worker-based route refusal for schema-2
   principal POs remains enforced. Add module metadata and authoritative action availability for each current
   actor without requiring unrelated buyer permissions.
6. Connect the existing imported PO screens without replacing their layout.
   Run normal-cookie Playwright workflows for funding, independent approval,
   issue and manual acknowledgment, plus return/reject, pending withdrawal,
   release confirmation, stale/concurrent commands, scoped denials, revoked
   users and original-key recovery after lost responses.
7. Verify actual Core tasks, Procurement evidence, allocations, receipts,
   audit and events in the same committed transaction; verify failed actions
   leave no partial effects and all test sessions are closed.
8. Publish reviewed commits and release evidence, rehearse backup/restore and
   finite production grants, deploy the verified products and run production
   smoke checks before enabling the workflow for the approved launch scope.

Receiving, returns, commercial amendments, blanket orders, invoice matching,
manual payment records and the eventual Finance adapter remain separately
tracked increments. This checkpoint does not complete those features or
claim full Procurement UAT acceptance.


### 27 October: readiness admission optimization under verification

A private one-CPU timing sample measured `/meta` at 8.336 seconds and the
acknowledgment detail at 8.734 seconds. This is not a production benchmark.
Governed routes previously ran a standalone draft proof and then a governance
proof that already composes that same parent proof, at both admission stages.
The revised dispatcher selects the full composed governance provider once per
stage, retaining configured admission, the actual owning-transaction proof on
every retry, write privileges, Core locks, current scope and all feature gates.
Draft-only routes retain their original parent provider. Nothing is cached across
requests or actors. Seven dispatch regression cases accompany the change. Product source
`2bebdf70b80a9846e0a1ec399a355b32ada40147` passed 72 focused Host tests
with no failures or skips, the no-pending-model check and the genuine 150-file API
publication. Receipt SHA-256:
`86de839ac1fa001da701d94d90eeb44ff2bce83049e2b6e8baafc1fa4d1c76af`.
The seven dispatch tests are helper-level checks, not actual transaction retry
or rollback tests. Existing mapped HTTP and middleware checks remain in the
focused selection. No production performance improvement is claimed.

The remaining UI forms committed acknowledgment, release request and independent
release confirmation successfully with three distinct original journal-matched
keys. The harness then failed because its final assertion expected eight unique
keys instead of three. Preserve that failed result; do not repeat these mutations.
Read-only reconciliation then verified ten receipts/business audit actions/outbox
events for the two retained orders, exactly one issued version, the independent
release, correct Ordered/Released allocations, unchanged 211-table schema and
normal logout for the final attempt. The original counting failure remains
retained. Owner receipt SHA-256:
`3263f1bccd28515ef3e625b627058cec6e7b4e1216c8a56138b7c9f1a7f495d1`.
This continues an interrupted journey and is not uninterrupted full UI UAT.


An independent read-only review of the readiness change and principal-order
command guards at `2bebdf70b80a9846e0a1ec399a355b32ada40147` identified no
concrete blocker in the inspected files. This is not comprehensive security,
concurrency or UAT sign-off. The five distinct private actors are a fixture:
server policy enforces the documented buyer/requester/approver and independent
Finance boundaries, rather than requiring five separate people. Issuer and
acknowledgment actors require their current action and record scope; additional
staffing separation remains a business policy decision.


### Fresh uninterrupted UI acceptance — 27 October

Backend product `2bebdf70b80a9846e0a1ec399a355b32ada40147` and UI product
`79a19df28b1d3e95d4ab8798c7f27cd1ff53f9d0` completed a fresh isolated
normal-password/Core-cookie five-role browser journey. The source PR was created,
manually funded and approved through the API; ten subsequent PO commands were
performed through the real existing UI: Create, Funding, Submit, Approve, Issue,
Acknowledgment, then Create/Funding/ReleaseRequest/ReleaseConfirm on a second PO.
All ten browser POSTs matched their durably saved original journal bodies/keys.
The genuine Issue response was deliberately dropped after server 201; the same
browser session reloaded and recovered its original result by GET, after exactly
one Issue POST. No successful command was replayed.

Read-only database reconciliation verified ten immutable receipts and response
hashes, ten business audit actions and ten outbox events; both actual shared Core
approvals; one issued version; two exact funding amounts (10.2468 and 5.1234 ZMW);
one acknowledgment and one independently confirmed release. The happy PO is
Issued with two Ordered units; the second is Cancelled with one Released unit,
leaving four of the six approved units available. All thirteen pre-existing PO
rows and the 211-table schema are unchanged. All five normal-cookie sessions
logged out and zero active actor sessions remained. Owner receipt SHA-256:
`c640cdc6c166ed2d89ef17c10f30fc2a1183633e602771db924844d3436d9117`.

This accepts this bounded UI journey, not all Procurement features or UAT.
Manual funding and supplier-response observation remain distinct from Finance
posting and supplier delivery. A cold private metadata read took 21.736 seconds;
there is no production performance improvement claim.
