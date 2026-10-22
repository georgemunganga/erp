# P24 recurring actual API / PostgreSQL / browser acceptance integration

Status: inventory and proposed diffs only. No operational/model/schema code or runtime changed.
Prepared from prior probe `c4f65f2`, recurring backend `dd1b719`, UI `1e8f07f`, and current shared
CRUD fixture source. This planning branch is not the accepted genuine P23 implementation base.

Schema owner reports candidate P23 `5618595` with genuine migration
`20261022112145_P23CorePrincipalInvoiceUnitReview` and seven captured actual PostgreSQL CHECKs
(`docs/procurement/release/sql/P23-actual-pg17-check-capture.json`). That candidate is not released
or fully gated. **Wait for root's accepted P23 source/catalog/publication checkpoint before porting
implementation or generating P24.** Retain all prior migration artifacts; exclude P25 models.

## Existing reusable evidence source

`backend/hrm-api/tools/ProcurementCrudHttpProbe/RecurringPurchaseVerification.cs` at `c4f65f2`
already authors own create/read/update/no-op/close, exact decimal/default currency, stale/foreign/
other-owner denial, wrong supplier-contract denial, referenced-contract reparent veto even after
closure, no downstream-owner mutation, and fresh owning reload/audit checks.
`RecurringSystemUserPurchases` uses a genuine `SystemActor` with no worker. `RecurringContractRace`
holds the exact contract row, observes both actual HTTP transactions waiting on that backend PID
via `pg_stat_activity`, releases it, then requires exactly one commit and reloads the invariant.
These are authored source, not completed P24 PostgreSQL proof. Reuse them rather than a new engine,
runner or mock persistence.

## Concrete gaps and bounded proposed diffs

Paths in this table are exact repository paths; `Probe/` abbreviates
`backend/hrm-api/tools/ProcurementCrudHttpProbe/`.

| File | Current gap | Proposed diff after accepted P23 |
|---|---|---|
| `Probe/Program.cs` bootstrap | Deliberately ends at P22 after legacy P19/P20/P21/P22 rehearsals. Its current model cannot honestly expose P24 schema readiness. | Keep those rehearsals at their exact migration states; then apply the accepted genuine P23 and separately generated P24 before current HTTP. Preserve real no-pending-model/no-pending-migrations checks. Port explicit recurring verification opt-in from c4. Never fake metadata ready. |
| `Probe/CorePrincipalOwnershipVerification.cs`, `Program.cs::VerifyCanonicalSchema` | Global CHECK count40 belongs to P22. New P23/P24 checks would fail it; blindly increasing/skipping the count would hide drift. | Keep exact P22 checks in its isolated rehearsal stage. Verify retained P22 CHECK definitions and the captured P23/P24 per-table manifests at the full current schema; bind actual capture hashes. Preserve legacy Down/audit-only veto scripts and all-data snapshots. |
| `Probe/RecurringPurchaseVerification.cs` | Prepared on old combined branch; full supplier/contract/race journey uses legacy employee-backed Actor, while workerless journey is simpler. | Port only recurring partial source onto accepted P23+P24. Parameterize existing HTTP sender/race actor so a real workerless finite-action Core planner covers the full contract/race slice. Keep actual observed blocking, exact CAS/audit checks and no-effect footprints; no header-granted permissions. |
| `Probe/Program.cs` Core fixture / manifest | Current default SystemActor is ERP admin; private actor switch declares only supplier reviewer. | Add one actual workerless `recurringPlanner` with only access, supplier-read, contract-read/create/edit and recurring-read/create/edit, assigned to the fixture branch. Declare it only under explicit recurring opt-in after CLI proof. Existing actors/roles remain unchanged. Use the existing default actor as an independent other owner for 404 proof. |
| `Probe/run-private-postgres.py` | Root current runner lacks the earlier explicit `--recurring` switch. | Reuse c4's bounded flag/env handling: only absent or1; strip inherited recurring flag, set only when requested; write requested/completed marker to existing protected launch/completion records. No new runner, Docker/network/credential transports or production configuration inputs. |
| `Probe/Program.cs` browser completion | Existing basic CRUD fixture waits for shutdown without recurring-specific final owner proof. | Capture pre-browser recurring IDs and protected-owner footprint; after shutdown use fresh DB-only contexts to verify the declared planner's exact new records/audit/version/closure and no AP effects. Never use HTTP after the listener has stopped. No grant-revocation control endpoint. |
| `reference-ui/scripts/crud-browser-actors.mjs` and its existing tests | Only exact protected alias `systemReviewer` is accepted. | Add one fixed `recurringPlanner` alias requiring protected opt-in/completed-recurring manifest, real nonzero principal distinct from default actor and workerIdnull. Preserve all current default/supplier behavior; reject arbitrary GUIDs, roles and undeclared aliases. |
| `reference-ui/tests/actual/actual-crud-helpers.ts` | Owner SELECT allowlist has credit/invoice/RFQ/order only. | Add fixed `recurring` table mapping and bounded original-owner recurring row observer. Validate UUIDs; fixed table/columns; no arbitrary SQL/title interpolation, credentials, grants or writes. Preserve tenant/company assertions and optionally assert current planner owner. |
| `reference-ui/tests/actual/actual-recurring-purchases.spec.ts` (new test only) | Existing recurring suite uses mocked business responses, so no genuine saved CRUD proof yet. | Use existing actual CRUD proxy, real stored Core planner alias and unchanged existing UI. No Procurement response mocks. Cover create/reload/edit/no-op/close/history, exact values/dates/currency and branch headers, reference conflicts and unknown response behavior below. |
| `reference-ui/tests/actual/CRUD-CONTRACT.md`, existing runner README | No source-bound P24 run contract/proof limits. | Pin accepted P23/P24 source, generated SQL and actual schema manifests, full publication tree, UI source/static hashes, tool/runner/manifest identities, terminal CLI/PW/completion logs. Separate Core-action authorization from DB-role evidence. |

The existing basic CRUD fixture declares `finiteRole=false`: it runs the actual handlers with a
private database-owner connection. A finite **Core action** planner does not change that database
proof limit. Keep the marker truthful. Production finite `procurement_api` ownership/ACL/default
privilege checks for the new P24 table are a separate provider/release gate; do not certify them
from owner-based browser reads. Reuse root's established privilege verification/provider pack.

## Actual browser scenarios

1. With the declared workerless planner and actual branch, choose the real approved supplier and
   an own same-supplier contract. Create with exact `12345678901234.5678`, explicit ISO dates and
   selected-company currency. Reload from API and independent owner SELECT; assert principal and
   null worker. Edit to `0.0001`, save unchanged once (exact +1 each), then reasoned close and reload
   read-only history. Verify three lifecycle actions and exact before/after audit versions.
2. Actual other Core owner cannot GET/PUT/close the record, and wrong company/branch/current action
   revocation deny without plan/audit/outbox mutation. A read-only actual actor cannot write.
   Header claims never supply authority. Reuse existing fixture mechanisms, not fake employee data.
3. Wrong supplier/contract returns422; a referenced contract cannot reparent before or after plan
   closure. Actual PostgreSQL serialization race is CLI proof, not a timing-based browser claim.
4. Inject a **response loss after forwarding the actual create to the private API and retaining its
   real201 response/record ID**, then abort only delivery to the browser. This is transport fault
   injection, not a fake business response. UI retains entered values and disables save; reload
   keeps its session marker and emits no second POST. GET/list and independent owner read show
   exactly one matching persisted planner record.
5. Do not claim server idempotency or a receipt-recovery endpoint: this planning API has neither.
   Unknown-create recovery is read-only reconciliation; the browser session stays blocked. GET or
   company/account changes must not clear an old principal's marker. A fresh test context is not
   proof of cross-tab exactly-once behavior.
6. Storage rejection occurs before POST. Malformed/wrong owner/branch/version acknowledgments and
   HTTP408/429/499 remain unknown; existing mocked cases stay useful for those fault boundaries
   but are not substituted for the real persisted happy path/lost-ack proof.

## Genuine schema / race / no-effect gates

- Generate recurring-only P24 from the accepted genuine P23 snapshot, with required original Core
  principal, nullable genuine worker, exact numeric/date/state/close CHECK, scoped FKs and indexes.
  Capture actual PostgreSQL definitions; no manual snapshot/ID or guessed readiness values.
- Fresh Up twice and used-P23 clone Up twice preserve all original schemas/data/roles/ACLs. Unused
  P24 Down/Up succeeds; any retained recurring row including Closed and meaningful plan audit veto
  Down at the first locked statement. Do not run combined destructive downgrades through successor
  tables before their independent ownership guards; migration commits are per migration.
- Exercise actual precision/date/null/owner/foreign-reference CHECK and FK violations in rollback
  transactions, with expected SQLSTATE/constraint identity and unchanged protected data.
- Reuse the observed row-lock race and require both actual requests reached PostgreSQL blocking
  before release, exactly one allowed outcome, and invariant supplier/contract reload. Retain first
  failed run; no retry or task-concurrency-only success claim.
- Before/after full protected-owner snapshots include invoices/lines/claims/Finance handoffs/payment
  references, receipts/acceptance/stock/asset events and outbox. Planning emits no PO, AP, reservation,
  scheduler or payment effects. Audit changes belong only to real recurring/contract draft actions.
- Run relevant Core-principal/mapped/domain tests, supported Node22 TSC/staged build, actual CLI and
  one-worker zero-retry Playwright, then fresh owning completion after exact fixture shutdown.

No SDK, model generation, SQL, PostgreSQL, Node, browser, scanner or deployment job ran for this
inventory. Implementation is held for root's accepted P23 checkpoint and serialized CPU assignment.
