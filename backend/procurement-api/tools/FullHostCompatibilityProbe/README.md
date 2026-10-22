# Actual Program compatibility smoke — source candidate

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
