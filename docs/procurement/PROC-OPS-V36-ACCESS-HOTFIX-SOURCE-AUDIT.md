# V36-compatible access hotfix source audit

Status: read-only source audit, 21 October 2026. No build, container/image/runtime,
database inspection or production mutation was performed. No source files in the
user's checkout or retained staging tree were changed. This is a candidate recipe,
not proof that a reconstructed image is byte-identical to the live image.

## Reproducible anchors

- Git baseline `663aef22ac1a2b94e60d1b7514f15f112f2e08cf`: documented P19 / v36 rollout.
- Recovery record `1013866eecbc0517051826e9d710d2cbeb1428e4`: v37 mixed-assembly failure,
  v36 recovery and a versioned loan compatibility probe. Its docs identify the retained
  live image as `hrm-api_hrm-api:procure-20261020-v36`.
- Retained non-Git build source `/home/amizo/procurement-deploy-staging-20261019/backend`.
- Clean audit worktree `/home/amizo/erp-v36-access-hotfix-audit`, branch
  `codex/erp-v36-access-hotfix-audit`, created directly at the Git baseline.
- [Source hash manifest](artifacts/v36-source-baseline/source-hashes.json): 320 identical,
  13 differing, 12 absent retained tracked source/build files; three extra retained
  files. Only .cs/.csproj/.props/.targets/.sln/global.json/Dockerfile were read/hashed.
  Appsettings, credentials, environment/config files, bin/obj and dependencies are
  excluded. No secret/config equivalence is claimed.

Every tracked Core/Procurement production source file and every tracked migration
matches the retained tree byte-for-byte. Missing retained Procurement files are
build/test helpers, not a new production implementation. The single extra retained
migration is HRM `20261014090000_LoanOpeningBalances`; all existing tracked HRM
migration sources/snapshot are identical. There is no P20 or later Procurement
schema/operational workflow in the proposed P19 base.

## Required retained compatibility delta — do not copy mixed assemblies

Starting from clean P19, recover these **source hunks**, then compile the entire shared
host with all referenced Domain/Application/Infrastructure assemblies together:

| Retained owning source | Exact retained behavior needed in candidate | Exclusions |
|---|---|---|
| HRM Domain `Entities/Payroll.cs:152` | SalaryAdvance.IssueDate nullable; RecordType default salary-advance; OpeningRecoveredAmount decimal | No other domain/schema widening |
| HRM Application `Payroll/PayrollServices.cs:128` | SalaryAdvance DTO/CreateRequest nullable issue date + record type + opening recovery fields | Keep P19 WorkerPayslipPreview leave fields/interface/constructor dependencies |
| Same `PayrollServices.cs:890` | all/settled list behavior; named loan/advance validation; optional loan issue date; nonnegative two-decimal opening recovery below principal; installment below remaining; store original fields and include opening recovery in create result | Do not replace unrelated payslip/leave methods with the retained older implementation |
| Same `PayrollServices.cs:2167` | MapAdvance nullable date/retained type/opening fields | Preserve existing P19 payslip source behavior |
| Same `PayrollServices.cs:2393` | Cent residue in final installment and correct loan/advance deduction label | No new lending/Finance engine |
| HRM Infrastructure `Repositories.cs:1287` | Add opening recovery to calculated persisted advance recovery | Must ship with Domain fields and Application contract in the same full image |
| `Repositories.cs:1199` and `PayrollServices.cs:839` | Evaluate payroll profiles against the actual pay-period range; preserve/update EffectiveFrom | Preserve the existing retained historical-payroll fix |
| HRM Application `Shared/PayrollProfilesImportSchema.cs:150,184` | Preserve existing profile effective date when omitted; carry forward component assignments omitted from partial update file | No import module replacement |
| HRM Application `Setup/SetupServices.cs:148,247,552` | Legacy completed setup stays complete; known step validation and completed-step editing; Web JSON field binding; PAYE based on taxable | Preserve these existing retained setup fixes, not unrelated setup redesign |
| HRM Infrastructure `Migrations/20261014090000_LoanOpeningBalances.cs` | Include exact already-retained migration in candidate source/history inventory | **No generation or execution**; confirm existing live history/columns first |

The full differing PayrollServices file combines loan/profile fixes with unrelated
older-code reversions. Therefore a whole-file retained copy is unsafe. Required
retained regression sources are `LoanOpeningBalanceTests.cs`,
`PayrollHistoricalPeriodTests.cs`, the two partial-update/date additions in
`M41Gap4ImportTests.cs`, and the completed-step addition in `SetupWizardTests.cs`.
They supplement existing tests; absent retained test files must not be deleted.

## Retained differences that must not be blindly imported

- `JobsAdminServices.cs` retained version removes P19 Procurement role/category/action
  handling and ERP-admin role management. It is **not** a loan/import/setup dependency.
- `ApiRoutesClean.cs`/`Program.cs` retained versions remove P19 payslip leave routes and
  leave-summary registration. They are **not** required for loan compatibility.
- Retained `PayslipDocumentServiceImpl.cs` removes P19 branding and leave-summary PDF
  content; retained PayrollServices also removes leave reads/preview fields and missing
  PDF-file regeneration. Do not introduce these reversions as part of an access fix.
- P19 `PayslipLeave.cs`, `PayslipLeaveSummaryService.cs`, and tests missing in retained
  tree remain in the coherent P19 candidate unless a separately reviewed decision
  deliberately chooses an exact older retained-feature baseline.

A byte-for-byte retained-source reconstruction and a curated P19+retained-fix candidate
are different choices. The curated candidate preserves tracked functionality while
repairing known live loan contracts, but requires a complete build/regression gate.
The manifest alone cannot prove either matches the live executable. No current P29
source, newer model snapshot, assembly overlay or existing dirty checkout should be
used as the hotfix build context.

## No-DDL and release gates

1. Parent reviews/commits the curated source delta plus bounded admin GET/context fix
   on an isolated P19 branch. Do not merge current Procurement/P29 history. Keep a
   complete diff/manifest of all candidate files and immutable image tag/digest.
2. Include the recovery-record `PayrollLoanCompatibilityProbe` unchanged (1013866),
   build/publish the full shared API from one checkout and run it against a private
   copy of the **whole** candidate /app. It must show both loan fields and JIT the
   actual Infrastructure loan-recovery state machine. This proves only that contract.
3. Run the retained loan/historical/import/setup regressions, existing P19 Procurement
   scope/permissions/draft suite and the bounded admin read/null-worker tests. Verify
   existing branding/payslip/leave/role behavior is preserved; no only-Domain or
   only-Application binary replacement.
4. P19 Program applies `HrmDbContext.Database.Migrate()` unconditionally at startup
   (Program.cs:266), and retains its existing pending-model-warning suppression.
   **A no-DDL launch is not established by not generating a migration.** Parent must
   compare the complete candidate HRM migration catalog against actual live history
   and loan columns read-only; exact already-applied LoanOpeningBalances is required.
   Any pending/unknown migration stops deployment. Do not invoke apply-migrations-only,
   generate schema, alter snapshot or auto-adopt missing loan columns in this hotfix.
5. Shadow a restored private production clone with the complete candidate, compare
   data/schema/history before/after startup and authenticated reads, then verify
   existing payroll loan opening/recovery, settings and Procurement routes. No new
   writes or arbitrary Worker link for the target admin. Preserve failed logs.
6. Parent's release owner retains compatible live v36 image/config/complete app copy,
   performs health/authenticated admin+denied employee/foreign-scope smoke checks,
   and records code-only rollback. The whole current P29 deploy hold remains; this
   audit does not authorize or perform production changes.
