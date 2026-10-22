# P24 recurring planning integration readiness

Status: focused tests and genuine P24 generation complete; full SDK/publication, private provider, finite database HTTP and browser acceptance remain pending. No P24 SQL has been applied.

## Published prerequisites and owned integration

- ERP published `main`: `2af2eaf4915de5bea1329e2484de16b0ed0ee9de` (P23 acceptance and handoff).
- UI published `main`: `0adb132fe721de5e74551f1fa5758c30b11fb013`.
- Isolated backend branch: `codex/procurement-p24-recurring-integration`.
- Normally merged prepared backend `dd1b719388b40759009980402a67b60a0c0e7543`; acceptance inventory `4494434` copied as its own focused documentation commit.
- Reuse the prepared existing UI, its Core account/current action checks, shared controls and exact-value contracts. No second UI, ledger, scheduler, approval engine or Finance integration is introduced.

The older source/acceptance plans remain historical preparation records. Their instruction to wait for P23 is satisfied by the published prerequisites above. The P24 runtime and schema acceptance steps remain outstanding. P25 principal-based requisition approvals are excluded from this model.

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

## Actual acceptance still required

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
- Full affected suites/publications, actual restricted Procurement DB login HTTP, provider-role release checks and actual browser/owner completion remain pending.

## Prepared actual acceptance source (not executed)

The existing CRUD probe/runner now contain the focused c4 recurring port, real finite Core planner,
actual captured schema resources, separate restricted Procurement database login/second existing
host composition, actual no-op/CAS/close and permission/scope/owner negatives, same-row observed
contract race, and browser completion verifier. The ordinary owner-based legacy/browser path stays
`finiteRole=false`; the additional finite-DB HTTP proof is a separate explicit marker only after it
passes. Runner inherited recurring flags are stripped and set only by `--recurring`; credentials
are generated for its new private login, never committed or printed. All actual runtime/PW results
remain unrun at this source checkpoint. The app/domain/model checkpoint is unchanged by these
tool-only edits; no second module, public endpoint or Finance/scheduler function is introduced.
