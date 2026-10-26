# M5a — Core-identity purchase order governance

Status: migration rehearsal passed; runtime integration in progress. Not release-ready or UAT-complete.

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
