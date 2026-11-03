# MANUS-PROC-M7B1-SCAFFOLD-05 — independent SDK reproduction and genuine scaffold

**Assignment:** Manus on the user-authorized separate non-production computer. **Tracking:** [issue170](https://github.com/georgemunganga/erp/issues/170), source [PR165](https://github.com/georgemunganga/erp/pull/165), documentation [PR168](https://github.com/georgemunganga/erp/pull/168). Status: ready for pickup after this task is published; no worker acknowledgment/execution is claimed. The GitHub issue's coordinator assignee tracks delivery, not a claim that an external process is running.

## Outcome, exact starting point and ownership

On an independent clean machine with measured adequate RAM/disk, reproduce the existing B1 source, verify its published genuine migration or generate one only while the source remains unscaffolded, and return exact tests/model/SQL/source provenance. Root's earlier low-RAM compiler attempts failed **before EF**. A newer local run03 has now built and genuinely generated `20261103163108_M7b1CommercialRoundDraftPlans`; its 1970/377 tests are still running at this task update. That generation is **in progress, not yet committed or accepted**. Fetch PR165's current published head before choosing verification versus generation; never infer that this in-progress ID is already a published prerequisite. Independent adequate-hardware verification is welcome and does not authorize a second migration to merge.

Fetch the **current head** of `georgemunganga/erp` source branch `codex/m7b1-round-plan-drafts-20261103` (original source-only pin **`2188f1bff287c7f293e77cd7e711d0cc63284352`**, backend tree **`53b6de1d91ed4743bd82a01f2f7788043c7b4d1a`**). Create your **own** branch/worktree, for example `manus/m7b1-scaffold-05`; never push, force-push or rewrite Root's PR165 branch. Record the actual fetched source/tree before execution. **If Root has already committed the genuine migration to PR165, verify that existing migration and run the prescribed tests/model/SQL checks; do not add another migration.** If the published branch remains the original 2188 source-only pin, independent genuine scaffolding is allowed. Any other unaccounted source drift requires recorded source comparison/coordination through the issue/PR, not an automatic merge of baselines. Main alone does not yet contain PR165's B1 source. Read `AGENTS.md`, `MANUS_HANDOFF.md` and [the adopted 28-section technical baseline](../../procurement/features/PROC-06-M7B1-COMMERCIAL-ROUND-PLAN.md).

Perform source checkout/mode selection first, then claim before the first restore/build with a focused draft PR titled `MANUS-PROC-M7B1-SCAFFOLD-05`, linked to issue170/PR165, plus `docs/collaboration/manus/m7b1-scaffold-05-progress.md`. Record source-base SHA/backend tree, current branch head, tool versions/hardware and an execution plan; a documentation-only progress commit does not change the pinned backend tree. Own only this reproduction/scaffold/evidence task. Root retains source integration, actual SQL/native/finite-role/schema-fingerprint adoption, original UI, feature activation and production release. Scanner153 is a separate task, not a dependency or substitute result.

## Strict bounds

- **No production or Root private fixture/backup/secret access.** Use no customer identity, cookies, DSNs, data, SQL role changes, independently launched runtime/API/browser job or PostgreSQL connection. Existing in-process TestServer and isolated SQLite/InMemory stores used by the prescribed test suites remain permitted test execution, not access to Root fixtures. Migration SQL generation is permitted; SQL **application** is Root-owned. Dummy unreachable design-time DSNs below prevent accidental reliance on a live database.
- No UI changes, employee prerequisite, policy/grant/threshold seed, native fingerprint fabrication, feature enablement, fake migration history, manual migration timestamp/name, copied old snapshot or held `98609167`/P32/P36/P38 merge.
- Preserve round-specific handler access, original maker evidence/M4 ownership, sorted PR→event→enrollment/plan→Core lock order, first-enrollment publication barrier, immutable child physical/logical identity, exact revision/recovery and retained source cancellation. Do not weaken them to pass a build.
- Keep all 59 new test cases (43 Procurement/16 Host) and prior stable cases. Retain failures honestly. A reproducible compilation/model/test defect may be proposed as a separate minimal evidence-backed commit on your branch for Root review; no removal of cases, suppression of analyzers, fabricated passing output or unreviewed business-semantic workaround.
- Do not commit `bin/`, `obj/`, restored packages/tools, binaries, raw TRX/log bundles, credentials or private machine configuration. Return protected artifacts with a sanitized summary and SHA256 manifest; only reviewed migration sources/progress/runbook belong in Git.

## Toolchain and portable setup

Use **.NET SDK 10.0.400 and dotnet-ef 10.0.11 exactly**. Record `dotnet --info`, actual selected SDK, EF tool version, OS/architecture, CPU, available RAM/disk and dependency provenance. The source's `rollForward` setting does not authorize silently substituting another SDK. Use your own known NuGet cache if complete, or the official `https://api.nuget.org/v3/index.json` feed; no private feed/Root cache/credential transfer is needed. Pin EF tooling in your evidence directory rather than changing repository dependencies/tool manifests.

All project paths in this recipe are **repository-relative**, run from the checkout root. `TASK_EVIDENCE` is your own fresh external evidence directory, not a Root local path. The recipe is an instruction to Manus; no SDK job was run to create this task.

```bash
set -euo pipefail
umask 077
cd "$(git rev-parse --show-toplevel)"
git fetch origin refs/heads/codex/m7b1-round-plan-drafts-20261103:refs/remotes/origin/codex/m7b1-round-plan-drafts-20261103
TASK_SOURCE_COMMIT="$(git rev-parse origin/codex/m7b1-round-plan-drafts-20261103)"
git switch -c "manus/m7b1-scaffold-05-$(date -u +%Y%m%d%H%M%S)" "$TASK_SOURCE_COMMIT"
git merge-base --is-ancestor 2188f1bff287c7f293e77cd7e711d0cc63284352 HEAD
if test "$TASK_SOURCE_COMMIT" = 2188f1bff287c7f293e77cd7e711d0cc63284352; then
  test "$(git rev-parse HEAD:backend)" = 53b6de1d91ed4743bd82a01f2f7788043c7b4d1a
  TASK_MODE=scaffold
else
  mapfile -t published_migrations < <(git ls-files 'backend/procurement-api/src/Mightyfin.Erp.Procurement.Infrastructure/Migrations/*_M7b1CommercialRoundDraftPlans.cs')
  test "${#published_migrations[@]}" -eq 1
  TASK_MODE=verify-existing
  # Compare this published source/model/migration with Root's PR165 result record.
fi
export TASK_EVIDENCE="$(mktemp -d -t m7b1-scaffold-05-XXXXXXXX)"
export TASK_NUGET_CACHE="${NUGET_PACKAGES:-$HOME/.nuget/packages}"
export DOTNET_CLI_TELEMETRY_OPTOUT=1 DOTNET_NOLOGO=1
export DOTNET_PROCESSOR_COUNT=1 MSBUILDDISABLENODEREUSE=1
export ConnectionStrings__Procurement='Host=127.0.0.1;Port=1;Database=never_opened_m7b1_scaffold;Username=invalid;Timeout=1'
export ConnectionStrings__Hrm="$ConnectionStrings__Procurement"
dotnet --info > "$TASK_EVIDENCE/dotnet-info.txt"
test "$(dotnet --version)" = 10.0.400
cat > "$TASK_EVIDENCE/NuGet.Config" <<'XML'
<?xml version="1.0" encoding="utf-8"?>
<configuration><packageSources><clear/><add key="nuget.org" value="https://api.nuget.org/v3/index.json"/></packageSources><disabledPackageSources><clear/></disabledPackageSources></configuration>
XML
dotnet tool install dotnet-ef --version 10.0.11 --tool-path "$TASK_EVIDENCE/tools" --configfile "$TASK_EVIDENCE/NuGet.Config"
EF="$TASK_EVIDENCE/tools/dotnet-ef"
"$EF" --version > "$TASK_EVIDENCE/ef-version.txt"
infra=backend/procurement-api/src/Mightyfin.Erp.Procurement.Infrastructure/Mightyfin.Erp.Procurement.Infrastructure.csproj
migrations=backend/procurement-api/src/Mightyfin.Erp.Procurement.Infrastructure/Migrations
proc_tests=backend/procurement-api/src/Mightyfin.Erp.Procurement.Tests/Mightyfin.Erp.Procurement.Tests.csproj
host_tests=backend/hrm-api/src/Mightyfin.Erp.Hrm.Tests/Mightyfin.Erp.Hrm.Tests.csproj
for target in "$proc_tests" "$host_tests"; do
  dotnet restore "$target" --configfile "$TASK_EVIDENCE/NuGet.Config" --source https://api.nuget.org/v3/index.json --packages "$TASK_NUGET_CACHE" --disable-parallel --force-evaluate -p:NuGetAudit=false
done
printf '%s\n' '/parallel-' > "$TASK_EVIDENCE/csc-serial.rsp"
build_flags=(-c Release -m:1 -p:UseSharedCompilation=false -p:RunAnalyzers=true -p:RunAnalyzersDuringBuild=true "-p:CompilerResponseFile=$TASK_EVIDENCE/csc-serial.rsp")
dotnet build "$infra" --no-restore "${build_flags[@]}" > "$TASK_EVIDENCE/pre-scaffold-build.log" 2>&1
```

The actual SDK's `CompilerResponseFile`→compiler `/parallel-` path may reduce memory pressure; record that the selected SDK honors it (sanitized diagnostic/targets/compiler invocation evidence), rather than assuming processor count alone disables compiler parallelism. Do not disable analyzers to fit memory. Use measured resource caps/headroom on the independent computer; the failing host's tight heap cap is not a required reproduction setting. If restore is fully cached/offline, replace only the feed with a documented own empty/local feed and retain the cache/assets provenance; do not claim a cold restore from a warm cache.

## Genuine generation, raw preservation and only permitted augmentation

In both modes, generate old M7A Up/Down SQL using the built source and retain its hashes. In `scaffold` mode this is the pre-generation baseline; in `verify-existing` mode compare additionally against Root’s published baseline hashes, without pretending you ran a pre-generation build. The mode guard below never adds a second migration:

```bash
mkdir "$TASK_EVIDENCE/before" "$TASK_EVIDENCE/raw-generated" "$TASK_EVIDENCE/final-generated"
old_m6e=20261030082157_M6eControlledInvoiceReplacement
old_m7a=20261030145538_M7aPrincipalManualQuotations
"$EF" migrations script "$old_m6e" "$old_m7a" --project "$infra" --startup-project "$infra" --context ProcurementDbContext --configuration Release --no-build --output "$TASK_EVIDENCE/before/m7a-up.sql"
"$EF" migrations script "$old_m7a" "$old_m6e" --project "$infra" --startup-project "$infra" --context ProcurementDbContext --configuration Release --no-build --output "$TASK_EVIDENCE/before/m7a-down.sql"
if test "$TASK_MODE" = scaffold; then
  "$EF" migrations add M7b1CommercialRoundDraftPlans --project "$infra" --startup-project "$infra" --context ProcurementDbContext --configuration Release --no-build --output-dir Migrations > "$TASK_EVIDENCE/genuine-scaffold.log" 2>&1
fi
mapfile -t generated < <(find "$migrations" -maxdepth 1 -type f -name '*_M7b1CommercialRoundDraftPlans.cs')
test "${#generated[@]}" -eq 1
migration="${generated[0]}"
migration_id="$(basename "$migration" .cs)"
[[ "$migration_id" =~ ^[0-9]{14}_M7b1CommercialRoundDraftPlans$ ]]
if test "$TASK_MODE" = scaffold; then
  cp "$migration" "${migration%.cs}.Designer.cs" "$migrations/ProcurementDbContextModelSnapshot.cs" "$TASK_EVIDENCE/raw-generated/"
  sha256sum "$TASK_EVIDENCE/raw-generated/"* > "$TASK_EVIDENCE/raw-generated.sha256"
else
  cp "$migration" "${migration%.cs}.Designer.cs" "$migrations/ProcurementDbContextModelSnapshot.cs" "$TASK_EVIDENCE/final-generated/"
  sha256sum "$TASK_EVIDENCE/final-generated/"* > "$TASK_EVIDENCE/final-generated.sha256"
fi
```

**Scaffold mode only:** all three raw generated files must survive byte-for-byte in protected evidence before editing anything. **Verify-existing mode:** do not rerun `migrations add` or append helper calls again; inspect the committed migration and compare its final files against Root’s reviewed raw/final hash record/exported evidence. Do not label committed augmented bytes as your own raw scaffold. If Root’s portable raw proof is not yet available, report that provenance gap explicitly while completing independent tests/model/SQL verification. Never invent migration IDs/history or reconstruct a Designer/Snapshot from a held branch. For scaffold mode, the only permitted migration augmentation is the existing `CommercialRoundPlanRetentionSql` helper calls from the pinned source's `Data/CommercialRoundPlanSchema.cs`:

```csharp
// End of genuinely generated Up, after all generated tables/keys/indexes:
migrationBuilder.Sql(Mightyfin.Erp.Procurement.Infrastructure.Data.CommercialRoundPlanRetentionSql.Install);
migrationBuilder.Sql(Mightyfin.Erp.Procurement.Infrastructure.Data.CommercialRoundPlanRetentionSql.AppendOnlyTriggers);

// Beginning of genuinely generated Down, before any generated drops:
migrationBuilder.Sql(Mightyfin.Erp.Procurement.Infrastructure.Data.CommercialRoundPlanRetentionSql.RetainedDownGuard);
migrationBuilder.Sql(Mightyfin.Erp.Procurement.Infrastructure.Data.CommercialRoundPlanRetentionSql.UninstallEmpty);
```

These are the actual existing helper names: Install/append-only Up and retained-guard/uninstall-empty Down; do not create renamed constants or hand-copy new trigger SQL. Keep helper calls in the migration transaction (no suppression). **Designer and ModelSnapshot stay exactly raw-generated bytes, including formatting.** Do not “fix” them manually or change old migrations/guards. Copy the final three files to `final-generated/`, hash them, and prove raw/final Designer and Snapshot equality with `cmp`. Explain the exact four-line main-migration augmentation diff.

## Required tests, pending-model proof and actual generated SQL

Run after generation/augmentation (a pre-generation model pass is not the result). Full expected suite: **1970 Procurement cases and 377 focused Host cases**, zero failed/skipped, preserving all 59 new and prior stable definitions. This exact Host filter is copied from Root's run02 recipe, not a narrowed substitute:

```bash
dotnet test "$proc_tests" --no-restore "${build_flags[@]}" --logger "trx;LogFileName=$TASK_EVIDENCE/procurement.trx" > "$TASK_EVIDENCE/procurement.log" 2>&1
host_filter='FullyQualifiedName~M4PrincipalRfqPhysicalChecks|FullyQualifiedName~ProcurementManualQuotationReadiness|FullyQualifiedName~ProcurementPrincipalQuotation|FullyQualifiedName~ProcurementInvoice|FullyQualifiedName~ProcurementPrincipalInvoice|FullyQualifiedName~CoreCurrencyPrecision|FullyQualifiedName~InvoiceReplacementCurrencyContract|FullyQualifiedName~Replacement_orgwide_workerless|FullyQualifiedName~Replacement_confined_workerless|FullyQualifiedName~Invoice_crud|FullyQualifiedName~ProcurementCompiledModelParityTests|FullyQualifiedName~Receiving_capability_|FullyQualifiedName~ProcurementManualPaymentRetainedReadTests|FullyQualifiedName~ProcurementManualPaymentGovernanceReadinessTests|FullyQualifiedName~ProcurementScopeMiddlewareTests|FullyQualifiedName~Unused_policy_retirement_retains_original_rules_and_does_not_create_authority|FullyQualifiedName~Policy_retirement_denials_make_no_policy_audit_or_outbox_changes|FullyQualifiedName~Any_retained_review_or_consumer_prevents_draft_retirement|FullyQualifiedName~Read_only_policy_hints_never_grant_retirement|FullyQualifiedName~Retire_hints_require_same_original_worker_and_principal_not_another_authorized_editor|FullyQualifiedName~Unavailable_identity_hides_only_retirement_while_policy_read_remains_usable|FullyQualifiedName~Unavailable_owning_schema_blocks_every_operational_mutation_but_keeps_historical_reads|FullyQualifiedName~ProcurementCommercialRoundContractTests'
dotnet test "$host_tests" --no-restore "${build_flags[@]}" --filter "$host_filter" --logger "trx;LogFileName=$TASK_EVIDENCE/focused-host.trx" > "$TASK_EVIDENCE/focused-host.log" 2>&1
"$EF" migrations has-pending-model-changes --project "$infra" --startup-project "$infra" --context ProcurementDbContext --configuration Release --no-build > "$TASK_EVIDENCE/no-pending-model.log" 2>&1
grep -Fq 'No changes have been made to the model since the last migration.' "$TASK_EVIDENCE/no-pending-model.log"
"$EF" migrations script "$old_m7a" "$migration_id" --project "$infra" --startup-project "$infra" --context ProcurementDbContext --configuration Release --no-build --output "$TASK_EVIDENCE/m7b1-up.sql"
"$EF" migrations script "$migration_id" "$old_m7a" --project "$infra" --startup-project "$infra" --context ProcurementDbContext --configuration Release --no-build --output "$TASK_EVIDENCE/m7b1-guarded-down.sql"
mkdir "$TASK_EVIDENCE/after"
"$EF" migrations script "$old_m6e" "$old_m7a" --project "$infra" --startup-project "$infra" --context ProcurementDbContext --configuration Release --no-build --output "$TASK_EVIDENCE/after/m7a-up.sql"
"$EF" migrations script "$old_m7a" "$old_m6e" --project "$infra" --startup-project "$infra" --context ProcurementDbContext --configuration Release --no-build --output "$TASK_EVIDENCE/after/m7a-down.sql"
cmp "$TASK_EVIDENCE/before/m7a-up.sql" "$TASK_EVIDENCE/after/m7a-up.sql"
cmp "$TASK_EVIDENCE/before/m7a-down.sql" "$TASK_EVIDENCE/after/m7a-down.sql"
sha256sum "$TASK_EVIDENCE/"*.sql "$TASK_EVIDENCE/before/"*.sql "$TASK_EVIDENCE/after/"*.sql > "$TASK_EVIDENCE/generated-sql.sha256"
git diff --check
```

Save full command lines/stdout/stderr/exits and TRX summaries/stable test identities. Capture final source/backend tree after any focused fix/augmentation. Generate a deterministic SHA256 manifest over logs/TRX/raw/final/SQL/tool/resource evidence without including package contents or secrets. Verify that SQL contains the intended SECURITY INVOKER/append-only/deferred-completeness/retained Down helpers and no unrelated schema changes. SQL text generation and no-pending-model proof do **not** prove actual Up/Down, native barriers, restored data, grants, API or UAT; those remain Root-owned. A zero tool exit without the expected tests/model output is not acceptance.

## Return and single-migration merge gate

Return a focused **draft PR** from your own branch, linked to issue170/PR165, with progress status, exact starting/final source/backend trees, tool versions, hardware/resource bounds, first failures, raw/final three-file SHA256 values, exact augmentation, 1970/377 TRX counts and preserved 59-case coverage, no-pending-model output, B1 Up/guarded Down hashes, unchanged old M7A Up/Down hashes, sanitized log/artifact manifest and protected evidence location. Keep failed attempts separately; no relabelling a compiler OOM as EF/native proof. Report a genuine source/tool/hardware blocker if the required result cannot be obtained.

Root’s local generation/test attempt is in progress. Re-fetch PR165 before scaffolding and again before publishing: if its genuine migration has appeared, do not add a second one. Publish your independent verification or unmerged scaffold evidence, then compare source/model/raw/final outputs and lineage with Root. **Only one genuine migration is selected/reviewed to merge.** Different generated timestamps do not authorize applying both; do not automatically merge/cherry-pick parallel migrations, rename IDs to make them match, edit history or push Root's branch. A superseded independent scaffold remains retained evidence until Root decides which one to integrate. No activation follows from this task, and no process/container/private fixture on Root's host may be stopped or changed by Manus.
