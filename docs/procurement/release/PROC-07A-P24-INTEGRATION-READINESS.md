# P24 recurring planning integration readiness

Status: genuine P24 generation, private DDL/constraint rehearsals, full affected backend suites/publications and actual finite-database HTTP acceptance passed. Actual browser and fresh owner completion passed; release-role/deployment gates remain pending. P24 SQL has been applied only to disposable private acceptance databases; no production migration or enablement is claimed.

## Published prerequisites and owned integration

- ERP published `main`: `2af2eaf4915de5bea1329e2484de16b0ed0ee9de` (P23 acceptance and handoff).
- UI published `main`: `0adb132fe721de5e74551f1fa5758c30b11fb013`.
- Isolated backend branch: `codex/procurement-p24-recurring-integration`.
- Normally merged prepared backend `dd1b719388b40759009980402a67b60a0c0e7543`; acceptance inventory `4494434` copied as its own focused documentation commit.
- Reuse the prepared existing UI, its Core account/current action checks, shared controls and exact-value contracts. No second UI, ledger, scheduler, approval engine or Finance integration is introduced.

The older source/acceptance plans remain historical preparation records. Their instruction to wait for P23 is satisfied by the published prerequisites above. The remaining release-environment acceptance steps are listed below. P25 principal-based requisition approvals are excluded from this model.

## Reviewed model delta expected from genuine generation

Only one new `procurement.recurring_purchase_commitments` table: lifecycle, tenant/company scope, number, supplier/optional contract, required original Core owner principal/canonical subject, nullable genuine worker metadata, optional branch, title/frequency/ISO dates, exact `numeric(18,4)` amount/currency and closure fields. Scoped restrictive supplier/contract foreign keys, scoped unique number, principal/next-date index and the schedule/owner/closure CHECK belong to this table. No historical principal backfill or changes to released P23 tables are expected.

Before generation, all139 tracked migration/designer/snapshot files equal published P23. After genuine EF generation, only the Procurement model snapshot may change among those139; all other138 historical artifacts must remain exact. New migration/designer files must be genuinely generated. No invented timestamp or manual snapshot/designer editing is allowed.

The owning store now checks persisted closed state before allowing any modification. Loaded closed-field edits, detached amount changes and reopening cannot bypass the HTTP closed-state guard. Persisted original identity/CAS remain authoritative for detached updates; a different current actor cannot modify another owner's record even with unchanged identity fields. Current worker link changes are not ownership transfers or authority grants.

## Resource-safe generation sequence

After independent source/model review and exclusive CPU assignment, use the cached SDK/NuGet/EF toolchain, offline restores and one CPU. Compile the owning infrastructure and focused tests before generating:

```text
/ef/dotnet-ef migrations add P24CorePrincipalRecurringPurchasePlanning
  --project backend/procurement-api/src/Mightyfin.Erp.Procurement.Infrastructure
  --startup-project backend/procurement-api/src/Mightyfin.Erp.Procurement.Infrastructure
  --context ProcurementDbContext --configuration Release --output-dir Migrations
```

Record the actual emitted ID and untouched pre-generation hashes. Review the actual Up, Designer and snapshot. Add only the reviewed literal locked Down veto to the genuinely generated new migration: any retained plan (including Closed), meaningful plan audit or owned event evidence prevents destructive downgrade before any Drop. Do not rewrite old migrations or manufacture worker defaults.

Then run full affected Procurement domain and selected mapped-host suites, complete publications, actual pending-model checks and emitted provider SQL. Keep every first failure. No prior test count is a new result. Source integration changes the domain/model, so P23 domain results cannot substitute for P24 tests.

## Acceptance requirements and release boundaries

Port the existing `c4f65f2` recurring partial probe onto the current genuine P23/P24 fixture; preserve independent legacy P19–P22 rehearsals and add exact current P23/P24 catalog checks. Use a real workerless `recurringPlanner` Core account with finite stored action grants and branch membership. The existing CRUD fixture's database-owner connection remains explicitly `finiteRole=false`; finite database privileges need their own provider gate.

Required proof includes own create/read/list/edit/no-op CAS+1/close, closed history, immutable owner, other actor/company/branch/current-action denials, exact values/default organisation currency, wrong supplier-contract rejection, closed-plan reparent veto and observed PostgreSQL two-request serialization race. Lost delivery creates an uncertain UI result; no automatic repeat or fake recovery endpoint.

Private schema rehearsal must cover fresh and used-P23 Up twice, unused Down/Up, retained-row/closed/audit/event Down veto, exact catalog/CHECK/foreign key/precision negatives and preserved original data. Finite production-role permissions require actual new-table owner/ACL/default-ACL checks, not an owner-based browser claim.

Actual API/PostgreSQL and existing UI Playwright run serially on private fixtures, followed by independent saved-data verification after controlled shutdown. Capture full source/publication/UI/fixture identities and retain failed traces. No production migration or capability enablement is authorized by this source checkpoint.

Finance/AP is a future ERP module and is not built. Planning creates no PO, reservation, liability, invoice, payment, stock or asset transaction. Never substitute a zero Finance history, invented funding success or mock ledger for that unavailable owner.

## Actual generated checkpoint — 22 October 2026

- Actual EF ID: `20261022144102_P24CorePrincipalRecurringPurchasePlanning`.
- First gate `20261022_142858`: infrastructure build and12 domain cases passed, then new mapped-test fixture compilation failed on its missing API namespace. No generation ran in that failed attempt; logs/results are preserved.
- Corrected source `5d476fc`: actual20 mapped recurring HTTP cases passed; the12 domain results were reused only after exact unchanged dependency-byte verification. Genuine EF generation then exited0/no OOM.
- Evidence: `/home/amizo/erp-backups/procurement-p24-recurring-20261022/20261022_143906/result.json`; raw generated files retained under `genuine-gen01` before custom Down.
- Exactly138 historical artifacts unchanged; only genuinely refreshed Procurement snapshot changes among the139 prior artifacts. Generated Designer target-model body equals snapshot build-model body exactly.
- Up remains byte-identical to the generated archive. New migration Down alone adds the reviewed first-statement exclusive locks and SQLSTATE55000 retained-row/audit/owned-event veto before generated DropTable. Any Closed row vetoes as well.
- Actual private DDL capture subsequently passed at `/home/amizo/erp-backups/procurement-p24-private-capture-20261022_1452/result.json`: fresh/used genuine upgrades, twice-idempotent Up, exact unused Down/reUp, independent synthetic Closed/audit/outbox55000 rollback vetoes and complete prior-data/catalog/history/source preservation. These are DDL-only facts, not financial, authentication or positive workflow proof.
- The actual P24 CHECK resource is `sql/P24-actual-pg17-check-capture.json`, SHA `19bd4610fb2250b8ae18b28146c5882c0cfef1fadbf284f4f808baee161e50db`, copied byte-for-byte from the private capture and embedded into the probe. No placeholder schema-ready fact is supplied.
- Full affected backend and actual restricted Procurement DB-login HTTP results are recorded below. Production-role release checks and deployment remain pending.

## Integrated acceptance source and actual backend results

The existing CRUD probe/runner now contain the focused c4 recurring port, real finite Core planner,
actual captured schema resources, separate restricted Procurement database login/second existing
host composition, actual no-op/CAS/close and permission/scope/owner negatives, same-row observed
contract race, and browser completion verifier. The ordinary owner-based legacy/browser path stays
`finiteRole=false`; the additional finite-DB HTTP proof is a separate explicit marker only after it
passes. Runner inherited recurring flags are stripped and set only by `--recurring`; credentials
are generated for its new private login, never committed or printed. Standalone runtime and actual browser/fresh-owner completion passed as recorded below. The app/domain/model checkpoint is unchanged by these
tool-only edits; no second module, public endpoint or Finance/scheduler function is introduced.


### Source-bound backend verification

- Frozen application/tool source `be5d87e3bfdc2b5d6d6be576ce321173e8c6b9f5`: actual full Procurement domain **1,206/1,206**, selected mapped-host/API **563/563**, and the corrected provenance focus **1/1**, all with zero skips.
- Four complete publications (API, CRUD probe, compatibility host, operational probe), three actual pending-model checks and the re-emitted P24 Up/Down/idempotent SQL passed. All 820 frozen inputs and all 141 current migration artifacts remained unchanged throughout this gate. Historical artifact preservation remains as recorded in the generation checkpoint.
- Evidence: `/home/amizo/erp-backups/procurement-p24-recurring-20261022/full_20261022_150705/result.json`. The hash-bound `final-manifest-binding.json` clarifies a stale top-level API-manifest alias in the finalizer; the `publications.api-publish.manifestSha256` value is the actual accepted manifest. Publication bytes did not change. The real tool publication shares 148 API dependency files exactly; its entry-specific `web.config` differs and it omits the API static-web-assets manifest. This is mapped-handler evidence, not a native production-host rehearsal.
- Prior Core Documents 112 and Core Approvals 20 results were reused only after byte-equality verification of their complete unchanged source dependencies. They were not rerun. Their separate reuse receipt is retained alongside this gate.
- First full attempt retained 1,205 passes/one fixture failure: a provenance test used the current command clock against a fixed expired authority. The first fixed-time correction then exposed the same fixture's deliberately unproven creation timestamp being freshly stamped by storage. Both failures are retained. The final test-only correction uses an open-ended expiry for that single legacy fixture and submits after its actual persisted creation time, preserving the exact invented-unpersisted-lineage denial and unchanged-state assertions. Production expiry/provenance rules and the shared draft helper were not changed.
- Independent actual PostgreSQL storage negatives: **15/15**, including 10 named CHECK violations, three scoped foreign-key violations, numeric overflow and duplicate scoped number, with exact expected SQLSTATE and an unchanged six-table footprint after each rollback. Evidence: `/home/amizo/erp-backups/procurement-p24-constraint-negatives-20261022_1517/result.json`. This is storage-only evidence, not API authorization.

### Actual restricted-database HTTP acceptance

- Standalone existing runner `--recurring` completed with **696 assertions across 246 actual HTTP requests**, including unchanged baseline CRUD and the new recurring slice.
- A separate real private Procurement database login passed 14 independent rollback-only privilege denials and actual create/read/list/edit/unchanged-edit/close through the existing mapped handlers. Actual Core users/grants, selected company/branch, own-principal visibility, revocation, stale CAS and closed-state controls were exercised. Core projection still uses its explicitly separate owner connection; external identity-provider authentication remains synthetic.
- The owner-based broader CRUD/browser fixture retains `finiteRole=false`; only the independently executed recurring HTTP subset has `recurringFiniteDatabaseHttp=true`. No restricted production-login or finite Core-database claim is made.
- The original observed two-HTTP-transaction contract race passed: both transactions reached the same actual PostgreSQL barrier before release, only one incompatible change committed, and a fresh owning reload preserved supplier/contract agreement. Exact no-op audit version pairs and unchanged downstream owners were checked.
- Evidence: `/home/amizo/erp-backups/procurement-p24-recurring-cli-20261022_1521/acceptance.json`, SHA `d4f519cff0bce5fbdecb77f9dae9608d6a3ef47eb8bd13a4cb2ade3ef4cf2012`. Source/publication/runtime hashes were unchanged; the disposable database, real restricted role and logs are retained.
- The separately created actual browser fixture and post-shutdown owner result are recorded below. These bounded planning results are not full Procurement completion.


### Actual browser and post-shutdown owning verification

- Existing UI source `b9f9ff15b97d89db1f89f6234929ed89feb0b0b4`, with its previously verified staged bundle, passed **2/2 actual Playwright cases** in 50.7 seconds, one worker, zero retries/skips: complete recurring CRUD/no-op/close with independent action/scope denials, and a genuine saved 201 response lost only at browser delivery, followed by blocked reload with no repeated POST. Procurement responses came from actual mapped handlers and the private database.
- Root UI evidence: `/home/amizo/erp-backups/procurement-p24-actual-browser-20261022_1529/result.json`. All 456 tracked UI source files, five staged asset files and the protected fixture SHA remained unchanged.
- The fresh browser fixture first independently repeated the CLI acceptance with 699 assertions/246 actual HTTP requests, including the restricted-database subset. Its broader browser host still uses its explicit owner connection; the browser is not relabelled as a finite-database browser run.
- After the browser terminal result, the exact owned Unix-only host was gracefully stopped with SIGTERM. The existing process then performed fresh owning-context reads and exited0: exactly two new workerless-principal plans; one Closed at versions1/2/3/4 with exact amount/reason/audit; one original Draft at version1 for lost delivery; no repeated create and no changes to prior plans, contracts, suppliers, unrelated audits or downstream owners.
- Final combined acceptance: `/home/amizo/erp-backups/procurement-p24-recurring-browser-20261022_1526/acceptance.json`, SHA `863f68c97ffbcf2cb35d76895a024d26c8af600e824c6874f9bc172e207a94f7`. It binds the actual browser result, post-shutdown owner completion, complete publication, runtime and source hashes. Fixture SHA before/after: `7e5b5394465956afc733de4060692159818686209f33c4ffcf0d6d0492fa8d95`.
- Both private runtime processes are stopped; acceptance databases, restricted roles, logs, publications and failed attempts are retained. No production SQL, role, configuration or deployment was changed by these gates.

### Remaining release work

Review/merge the tested backend/UI prerequisites, rehearse the combined production-owner migration and actual runtime role/ACL/default-ACL boundaries on a fresh protected restore, then follow the controlled deployment and real public sign-in/UAT gates. The private minimum-grant login proof does not replace production role reconciliation. Do not downgrade after retained planning data exists. Finance/AP, invoice generation, payment scheduling and scheduler execution remain outside this planning feature and are not enabled or claimed.
