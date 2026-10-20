# Shared-host release source alignment audit — 20 October2026

**Status: deployment hold remains. Read-only source/CI audit; no candidate build, database job, production inspection, image mutation or deployment performed.**

## Provenance and safe merge plan

| Source | Observed reference | Finding |
|---|---|---|
| Shared release checkout `/home/amizo/erp-core-main-sync` | `codex/erp-core-main-sync`, HEAD/origin main `1013866` at audit start | Predates retained loan-source restoration. Five existing HRM source edits and untracked `DEPLOYMENT-HOLD.md` are present. Do not build, discard or automatically merge this dirty checkout. |
| Retained v36 source | `/home/amizo/procurement-deploy-staging-20261019/backend/hrm-api` | Source baseline for the known nullable loan issue date/opening recovery contract, not a new deployment artifact. |
| First isolated integration | `codex/procurement-live-integration` `c4b8304` | Includes loan/import/setup restoration and workflow gates, but is not the final P25/P26 release source. |
| Current root P25 integration | `codex/procurement-order-execution` `72dd141` at audit | Descends from main `1013866` and includes `e5ed55c`; further central correction migration/gate is pending. |
| P26 consolidation | `codex/procurement-invoice-comparison-execution` `b0ab383` | Ordinary merge parents `f829035` and `e5ed55c`; root's subsequent catalog-tier fix/correction migration must still be merged. P26 schema/host verification remains pending. |

**Concrete plan:** after root finishes and seals its central correction migration and gates, merge that exact root SHA by ordinary merge into P26; retain P26 changes and the single central migration lineage. Generate/review/rehearse the genuinely additive P26 migration only in the coordinated final branch. Create a new clean release worktree from that exact reviewed final source SHA. Both audited root/P26 branches already descend from main `1013866`, so the eventual reviewed PR to main should carry the full source history rather than replacing individual libraries or cherry-picking unrelated dirty HRM edits. Recheck main ancestry at release time because this is a point-in-time observation. Do not advance main in this audit.

## Mandatory retained compatibility commits

These are already ancestors of current P25 and P26. **Do not cherry-pick them again.** They identify the required source content if another candidate branch is used.

- `e68416f`: strict `backend/hrm-api/tools/PayrollLoanCompatibilityProbe/Program.cs` and README. Requires decimal `OpeningRecoveredAmount`, string `RecordType`, nullable `DateOnly? IssueDate`, then JIT-prepares the actual Infrastructure recovery method. A self-consistent old assembly set must still fail the property contract.
- `4f910ee`: `Hrm.Domain/Entities/Payroll.cs`, `Hrm.Application/Payroll/PayrollServices.cs`, `Hrm.Infrastructure/Repositories.cs`, `Hrm.Infrastructure/Migrations/20261014090000_LoanOpeningBalances.cs`, `Hrm.Tests/LoanOpeningBalanceTests.cs`, `Hrm.Tests/PayrollHistoricalPeriodTests.cs`. Preserves imported loan opening recovery, nullable issue dates, correct outstanding/recovery bounds and payroll profile selection by the actual historical pay period.
- `66e1cf2`: `Hrm.Application/Shared/PayrollProfilesImportSchema.cs`, `Hrm.Application/Setup/SetupServices.cs`, `Hrm.Tests/M41Gap4ImportTests.cs`, `Hrm.Tests/SetupWizardTests.cs`. Preserves unspecified profile components/effective date in partial updates, legacy completed setup handling, web JSON payload handling and the retained taxable PAYE basis. `62213ae` is the equivalent integration-history commit; current root contains `66e1cf2`, not an omission.
- `32c9bd5`: `.github/workflows/procurement-verification.yml` and approval probe project. Includes preserved-host regression tests, atomic approval probe and full published-host loan probe in CI.

Paths above abbreviate projects under `backend/hrm-api/src/Mightyfin.Erp.`. The retained LoanOpeningBalances migration is **already part of the existing live lineage**; preserve its identity and nullable semantics. Do not infer that its destructive Down is safe on real opening-balance data.

Comparison against the retained source found no further missing retained behavior in the narrowly reviewed `JobsAdminServices`, `PayrollServices` and `PayslipDocumentServiceImpl`: current differences are newer ERP role/category permission validation and newer leave-summary/branded PDF behavior and document regeneration. Keep them. A blanket copy of retained files would remove those newer fixes. The five dirty shared-checkout changes include unrelated payroll period/correction/worker changes; they are not a substitute for the retained-source commits and remain independently owned/unreviewed for this release.

## Full-source candidate compatibility gates

Commands below are **instructions, not results from this audit**. Run sequentially in the clean final release worktree with the coordinated SDK/resource slot. From `backend/hrm-api`:

```sh
dotnet test ../procurement-api/src/Mightyfin.Erp.Procurement.Tests --configuration Release --logger trx --results-directory ../../artifacts/release/procurement
dotnet test ../core-api/src/Mightyfin.Erp.Core.Approvals.Tests --configuration Release --logger trx --results-directory ../../artifacts/release/core-approvals
dotnet test ../core-api/src/Mightyfin.Erp.Core.Documents.Tests --configuration Release --logger trx --results-directory ../../artifacts/release/core-documents
dotnet test src/Mightyfin.Erp.Hrm.Tests --configuration Release --filter FullyQualifiedName~Procurement --logger trx --results-directory ../../artifacts/release/host
dotnet test src/Mightyfin.Erp.Hrm.Tests --configuration Release --no-build --no-restore --filter 'FullyQualifiedName~M41Gap4ImportTests|FullyQualifiedName~SetupWizardTests|FullyQualifiedName~LoanOpeningBalanceTests|FullyQualifiedName~PayrollHistoricalPeriodTests|FullyQualifiedName~PayrollEngineTests|FullyQualifiedName~PayslipLeaveSummaryTests' --logger trx --results-directory ../../artifacts/release/retained-host
dotnet publish src/Mightyfin.Erp.Hrm.Api --configuration Release --no-restore --no-self-contained --output ../../artifacts/release/api
dotnet run --project tools/PayrollLoanCompatibilityProbe --configuration Release -- ../../artifacts/release/api
```

Use the final source's pinned EF tool to check pending model changes for ProcurementDbContext, ApprovalDbContext and DocumentDbContext, using their existing design-time project paths in the workflow. Run reviewed migrations twice on a separately restored database, populated legacy preservation and rollback/recovery checks, then all applicable restricted-runtime HTTP/PostgreSQL probes, including new receiving and matching when ready. Keep the real UI/API browser milestones tied to exact source/assets; mocked browser results do not prove the final host/schema. Those gates require operator-owned private connection values and are intentionally not replaced with guessed credentials here.

Build the complete host image from this same frozen source using `backend/hrm-api/Dockerfile` with repository-root build context. It publishes API plus referenced Domain/Application/Infrastructure/Core/Procurement together. Hash the complete candidate assembly set and image, privately extract the candidate `/app`, and run the same strict compatibility probe against that extraction too. No Domain/Application DLL overlay over v36 Infrastructure. Record migration inventory, image/source identity, rollback implications, authenticated route checks and feature flags before any cutover. A health endpoint or loan JIT success alone is not release acceptance.

## Current CI evidence gap

Read-only GitHub API audit confirmed latest listed Procurement workflow run [39411594000](https://github.com/georgemunganga/erp/actions/runs/39411594000), source `c4b83044d1289c5e5ced42d513d1bb246ffa4811`, completed as failure with **zero executed steps**. Job `118590421448` annotation states: “The job was not started because your account is locked due to a billing issue.” Earlier run39371540618 has the same failure. There is no passing CI evidence from either run, and neither covers current P25/P26 source. Billing resolution is an account-owner action; rerun the workflow on the final candidate afterward. Until then preserve explicit local evidence and do not describe CI as green.

This audit neither refreshes production health nor releases the existing deployment hold. Parent-owned final source/migration, runtime and release review remain required.
