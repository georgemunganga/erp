# Actual Program compatibility smoke — source candidate

## Payment owner-upgrade extension — 23 October 2026

`--rehearse-payment-owner-upgrade` admits only a NEW bounded Core-principal clone of the exact retained `proc_release_backup_20261022_smoke_p24_native01` source. It requires precisely the Core company-timezone and P27 payment migrations to be pending, a matching real owner model, and retained companies. The owning EF engines apply both changes and repeat them; all old business rows/history and unrelated columns/constraints/indexes must remain identical. The only normalized additions are the new company timezone column, three payment tables and their two known migration-history rows. A complete post-upgrade fingerprint also verifies repeatability. The real Core date provider is tested in the company's actual tenant context; retained company configuration must veto a destructive Core downgrade. Program never starts in this preparation mode.

The prepared private clone can then be the read-only catalog source for `--rehearse-fresh-hrm`, or the template for a separate actual Program smoke. Every execution creates a new target and checks its retained source before/after; never migrate the original template or reuse a failed target. Fingerprints do not claim sequence-state, role/grant, trigger/function or external-integration equality. Runtime role authorization and full payment API/browser workflows remain separate gates.

The first executed owner upgrade, fresh owner engine and actual Program gates passed against source `a6c7a40`, with source preservation and zero pending owner migrations/models. Final candidate verification and exact protected receipt references belong in the Procurement release record; source-only text below describes the earlier tool development history.

This is an offline verification tool, not a new host or production configuration. It references the existing HRM API entry assembly through `WebApplicationFactory<MonthlyLeaveAccrualWorker>` and executes its actual `Program` with TestServer. It copies no routes, identity authorizer or migration engine.

Before starting Program it reads **actual** HRM and Procurement migration/model differences on the new clone. Any pending migration, model difference or unavailable check blocks startup. In particular, the existing HRM Program suppresses its pending-model warning; this tool reports that inherited gap rather than claiming compatibility through the suppression. No migration/worker/publisher command is passed. Program’s own migration call is reached only after that no-pending preflight.

The **test harness only** removes the concrete MonthlyLeaveAccrualWorker registration before host startup, rejects unexpected application hosted workers, blanks local bootstrap credentials, uses existing local authentication, clears log providers to avoid restricted data leakage, and disables Procurement execution/CoreDocs options. It sends only unauthenticated root and database readiness GETs. Metadata permission failures and business authorization are separate gates. It captures no user records or sessions. All table values/history and column/constraint/index definitions must be unchanged after shutdown. Sequence state, roles/grants and external integration are not measured.

## Parent-owned execution, after compilation/review

Publish this complete project offline into a new protected output. Do not copy individual DLLs into a retained app publication. The runner requires a preexisting immutable local .NET10 ASP.NET runtime image ID, uses `--pull=never --network=none`, and shares only the private PostgreSQL Unix socket plus read-only publication/protected output. It creates a **new** clone of the retained approved canonical private backup; it never starts against that original or production. The actual Program is never started in outbox publisher or migration-only mode. A failure retains the clone and all protected evidence.

```bash
python3 backend/procurement-api/tools/FullHostCompatibilityProbe/run-private.py \
  --publish-dir /protected/full-smoke-publication \
  --runtime-image sha256:<existing-reviewed-local-runtime-image-id> \
  --target-db proc_release_backup_20261022_smoke_unique \
  --default-tenant-file /protected/verified-deployment-default-tenant.txt \
  --output /protected/new-unique-host-smoke
```

Source authored only: no build, runtime, database or startup result is claimed here. A model drift blocker must be diagnosed and reviewed, never bypassed with fake history, ignored warnings or a fake ready provider. The restored source and retained canonical clone remain unchanged; do not delete business evidence to force acceptance. No production deployment or option activation follows automatically.

The launcher freezes and hashes every published runtime file before launch and verifies that snapshot afterward. Publication symlinks are refused. The probe catches initial/final footprint failures with exception types only; no SQL, data, connection strings or exception messages are printed. Python syntax validation is source-only evidence; C# compilation and actual startup remain pending.

## Read-only HRM owner-model diagnostic

The optional launcher flag `--diagnose-hrm-model` passes exactly that flag to this binary. It retains the same fresh-clone, Unix-socket, network-none, immutable-publication and unchanged-footprint gates. This mode **never starts Program**, including when no drift exists, and never generates/applies migrations. Exit 2 still denotes pending owner migration/model drift; the launcher accepts it only for this explicit diagnostic mode with the expected diagnostic status and unchanged footprint.

The diagnostic initializes the actual owning Npgsql snapshot with `IModelRuntimeInitializer`, compares it with `IDesignTimeModel` using the actual `IMigrationsModelDiffer`, and requires its pending result to agree with the unchanged production guard. It emits only allowlisted operation/schema/table/column/type/nullability/length/name/count metadata. No EF operation is serialized wholesale. Default values/expressions, seeds, rows, connections, annotation values and raw SQL are excluded; SQL operations receive a source hash only.

Read-only catalog checks accompany every emitted operation. They compare current target-model columns/types/nullability, named validated primary/unique/FK constraints (including FK column order, parent, delete action and nondeferrability), and valid named indexes. Unsupported non-table operations explicitly require owning review; they are not marked equivalent. Extra-column counts are reported separately, and required shape equality does not claim extra manual checks/triggers/defaults/sequence state equivalence. Partial-index expression formatting can conservatively report a mismatch; no SQL expression normalization is attempted. Expected handwritten LoanOpeningBalances/M55BrandingFont migration IDs are checked against actual applied history, without synthesizing history.

Source inspection suggests inherited snapshot lag: the current HRM model includes SalaryAdvance/CompanyBranding whereas the retained snapshot omits those entity/table mappings; handwritten migrations may already have created them. This is a hypothesis until the real diagnostic runs. Never apply a naive generated CreateTable/AddColumn diff to that database. If catalog evidence establishes every difference is already represented by applied owning DDL, the owner may review a targeted snapshot-only reconciliation or a guarded no-DDL reconciliation migration; otherwise it must design the smallest additive owning migration from the actual schema. Existing migrations/history/rows and the startup block remain intact throughout.

This increment is source-only and uncompiled. No diagnostic result, snapshot repair, database mutation or actual Program compatibility is claimed.

## HRM owner reconciliation checkpoint (22 October 2026)

The actual retained catalog established that the handwritten migrations already own all
18 reported differences: four tables, seven columns and seven indexes. Three explicit
model names now match their existing PK/index/FK contracts; existing delete behaviour,
filters, types and nullability are retained. The refreshed snapshot records that proven
model without a new migration or history entry. The temporary generated draft was
archived outside Git and was never applied; every earlier migration file is unchanged.
The real initialized snapshot/current Npgsql differ and `HasPendingModelChanges` gates,
plus focused model/loan regressions, passed 7/7. This does not establish host startup.

The first two actual Program attempts retained clean owner model/migration gates and
unchanged table/schema fingerprints, but failed startup. The tool now records fixed
phase names, numeric root/readiness HTTP statuses, worker/bootstrap booleans and method
names only. It never records exception messages, response bodies, SQL or configuration.
Its isolated environment and deferred web configuration share the same safe settings:
the actual minimal Program reads database/auth settings before `builder.Build`, so they
must exist before its entry point executes. The content-root setting is supplied using
the WebHost `ASPNETCORE_` environment prefix. These are tool composition changes, not
application readiness overrides; actual startup remains gated on its real result.

### Fresh owning migration engine, separate from idempotent SQL scripting

The original full-chain EF idempotent SQL failed on a handwritten raw `UPDATE` without
a semicolon inside the generated PL/pgSQL wrapper. That SQL and failed new database are
retained. No old migration or generated SQL is patched to conceal the limitation.

The explicit `--rehearse-fresh-hrm` mode instead exercises the application's actual EF
migration path. Use the same launcher/publication/image arguments, a new target matching
`hrm_model_reconcile_20261022_<unique>` and that flag. The launcher creates the target
from `template0`, never from a source database. The tool first requires a real matching
owner model and zero user objects/history, then calls the real `MigrateAsync` twice.
It requires all HRM table-data/schema/history fingerprints unchanged on the second run,
no pending migrations/model changes, and catalog/history equality with the fixed retained
canonical database, which it reads only. It never starts Program, changes roles or
executes Down, and it refuses a reused/nonempty target. No runtime success is claimed
for this newly authored mode until the parent-owned actual gate runs. Sequence state
and finite-role provisioning remain outside this owner-schema check.


### Scope corrections established by the actual private gates

The actual entry point now starts and returns root/readiness HTTP 200. Its required
unchanged-data gate correctly rejected a harness configuration omission: without the
explicit deployment default tenant, existing `PrincipalTenantAccessor` chose its
`local-tenant` fallback and normal Program startup seeded 14 role assignments there,
with audit rows. No application seeding logic is disabled. Actual Program composition
now requires a protected explicit default-tenant file, checked read-only against the
unique active company/account tenant and deployment configuration before execution.
Its value is never printed or committed. Read-only diagnostic and fresh modes do not
start Program and do not need that tenant value.

The real fresh `MigrateAsync` ran twice successfully: no pending migrations/model,
and all fresh HRM table data/schema/history remained identical on the second run.
The exact full fresh/retained catalog gate correctly exposed one existing import-owned
table, `hrm.legacy_frappe_records`. Its authoritative DDL is in
`scripts/import_frappe_people.py`; `docs/hrm/FRAPPE-EMPLOYEE-MIGRATION.md` specifies why
its original records are preserved. No column, constraint, index or history shared by
fresh and retained differed, and there were no fresh-only objects. The comparison
therefore excludes **only that exact table's** objects from the retained EF-owned
comparison. Every other extra/different object still fails. Full retained HRM catalog
and all HRM table data, including that imported archive, must remain unchanged before
and after. The importer table is never created, deleted, adopted or backfilled by EF.
Both failed results and their databases remain retained; corrected runtime acceptance
is pending a new private gate.

### Executed owner gates — source `db837e3`

The exact API and tool were rebuilt together after removing the obsolete application
`PendingModelChangesWarning` suppression: build succeeded with zero errors (18 inherited
warnings). The earlier focused real-model/loan suite passed 7/7; unchanged tests are not
counted again as a new suite run.

On a new clone using the explicitly verified deployment tenant, the actual unmodified
entry point passed root/readiness HTTP 200 with both real pending-migration counts zero
and both real pending-model checks false. Bootstrap, leave accrual and outbox publishing
were off, and **all table values/history and schema definitions were unchanged**.

On a separate new `template0` database, two actual HRM `MigrateAsync` runs passed, with
zero pending migration/model differences, identical full HRM data/schema/history after
the second run, exact fresh-owned catalog/history equality, and complete retained HRM
catalog/data preservation including the sole import-owned archive. No old migration was
edited, no new history entry was invented, and no production database was touched.

Protected evidence is retained under
`/home/amizo/erp-backups/hrm-model-reconciliation-20261022/`: the exact source/publication
manifest and build log are in `0814-db837-api-tool`, actual Program result in
`0820-actual-program-verified-tenant`, and fresh owner-engine result in
`0822-fresh-engine-import-owned`. Prior compile, wrapper-script and fixture-composition
failures remain retained. The old full-chain idempotent wrapper-script semicolon issue
is still a documented limitation; this acceptance proves the actual EF migration path,
not repaired script text. Sequence state, finite-role business authorization, external
integrations, production upgrade/deployment and operational Procurement workflow are
separate gates and are not inferred from these owner checks.
