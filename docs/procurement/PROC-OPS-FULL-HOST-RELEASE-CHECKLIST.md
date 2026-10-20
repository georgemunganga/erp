# Full ERP host release alignment — next controlled cutover

**Status: preparation only. Release hold29 remains. No builds, SQL, Docker mutations, production inspection or deployment were performed for this checklist.**

This supplements [source alignment audit](PROC-OPS-RELEASE-SOURCE-ALIGNMENT-20261020.md). It does not repeat earlier completed checks or turn their historical results into approval for a later source/image. Inspection source: root `a07c1efc8f93de5bee0a1f4ff0b80c531abd733b` while central corrected P26 generation was in progress. Generated P26 ID/digest and final release SHA must be filled from root's sealed result, never guessed here.

## 1. Source and release identity

- [ ] Keep `/home/amizo/erp-core-main-sync/DEPLOYMENT-HOLD.md` and its five dirty HRM files under their existing ownership. That dirty checkout is not a build context. No checkout/reset/copy-over is part of this plan.
- [ ] Select one clean final backend SHA containing the reviewed Procurement/Core work, additive migrations, retained loan/import/setup source and any separately accepted HRM changes. Record branch, merge parents, commit, clean status, source tree/archive SHA256 and reviewed PRs. Do not cherry-pick retained preservation commits again when already ancestors.
- [ ] Preserve retained `SalaryAdvance.OpeningRecoveredAmount:decimal`, `RecordType:string`, `IssueDate:DateOnly?`, opening-balance repository arithmetic and `20261014090000_LoanOpeningBalances` identity. Preserve newer leave summary/branded PDF/missing-file regeneration and ERP role validation; do not blanket replace files with retained staging.
- [ ] Record frontend source/asset hashes independently from backend SHA; invoice PR34 contains verified mock tests and pending actual P26 integration. Receiving PR33's actual two-case/owner proof is separate evidence, not P26 approval. Keep issues27/30 and releasehold29 explicit.

## 2. Exact gate inventory — reuse matching evidence; run only missing/stale gates

Compare source files, model/DDL and candidate assembly hashes with root's existing evidence first. A documentation-only change does not justify another full suite. Reuse valid recorded gates when their tested source/binary contract is identical; run a listed gate only when required final-source coverage is absent or affected code changed. All SDK/Node/database slots remain coordinator serialized.

| Concern | Exact existing project/filter | Limit |
|---|---|---|
| Procurement domain/model invariants | `backend/procurement-api/src/Mightyfin.Erp.Procurement.Tests` (whole project) | P26 final generated model and checks must match tested source. |
| Core approval engine/coordinator | `backend/core-api/src/Mightyfin.Erp.Core.Approvals.Tests` (whole project) | Pair with existing ApprovalTransactionProbe actual PostgreSQL evidence; tests alone do not prove atomic integration. |
| Core document ownership/lifecycle | `backend/core-api/src/Mightyfin.Erp.Core.Documents.Tests` (whole project) | Pair with CoreDocumentWorkflowProbe and real scanner gate when enabling documents. Exact-byte browser scanner is tools-only, not production scanning proof. |
| Shared-host Procurement/API/scopes | `backend/hrm-api/src/Mightyfin.Erp.Hrm.Tests`, `FullyQualifiedName~Procurement` | Actual restricted runtime probes/real browsers remain separate. |
| Retained loan/import/setup/payroll/leave | Same HRM project, combined filter below | Existing source-preservation evidence is historical, not a new run made here. |
| Payslip admin dispatch/current-period preview | Same HRM project, `FullyQualifiedName~M34RunPayslipTests\|FullyQualifiedName~PayslipPreviewPeriodTests` | Four M34 cases use fake document URLs, two preview cases do not render PDF. This class lives inside PayrollEngineTests.cs but is **not matched** by a PayrollEngineTests class filter. |
| Shared import scope, if affected by final composition | Same HRM project, `FullyQualifiedName~M54ImportScopeTests` | Reuse existing evidence when unchanged; do not silently substitute broad import capability for ownership checks. |

Exact retained filter already used by the workflow/runbook:

```text
FullyQualifiedName~M41Gap4ImportTests|FullyQualifiedName~SetupWizardTests|FullyQualifiedName~LoanOpeningBalanceTests|FullyQualifiedName~PayrollHistoricalPeriodTests|FullyQualifiedName~PayrollEngineTests|FullyQualifiedName~PayslipLeaveSummaryTests
```

Use that filter with `dotnet test ... --configuration Release --no-build --no-restore --logger trx` **only after that exact final test assembly was built**. The established command paths and EF10.0.11 model-check commands are in the source alignment audit and `.github/workflows/procurement-verification.yml`; they are instructions, not automatic execution from this checklist. Keep any first failures and their correction lineage. Do not count parameterized assertions or reruns as additional independent test coverage. The prior CI account/billing failure is not passing CI; capture actual final run status when available.

### Remaining PDF runtime proof

The current Dockerfile supplies Python/native Pango/Cairo/fonts and installs WeasyPrint. `PayslipDocumentServiceImpl.GenerateAsync` actually launches `weasyprint`, returns a local file URL and includes branded/period-scoped leave data. `PayrollServices.NeedsPayslipDocument` regenerates a missing local artifact. No reviewed dedicated test found here proves native rendering plus missing-file recovery; M34RunPayslipTests substitutes a fake document service.

Before releasing affected PDF behavior, use the COMPLETE candidate image and a fresh synthetic isolated HRM fixture, not a production payroll run: render a document with the actual service, verify PDF bytes/expected employee-period/branding/leave content and permitted preview, remove only that synthetic private artifact, then verify regeneration through the existing service path. Retain renderer/version/font/dependency/output hashes and ensure no payroll calculation/release is triggered. This is a concrete remaining gate, not a passed result or permission to delete existing employee PDFs.

## 3. Full publish/image provenance

- [ ] Publish `backend/hrm-api/src/Mightyfin.Erp.Hrm.Api` in Release from the one frozen source. Preserve full API/Domain/Application/Infrastructure/Core/Procurement publish set, deps/runtimeconfig, configuration template and assembly SHA256 manifest.
- [ ] Run strict `backend/hrm-api/tools/PayrollLoanCompatibilityProbe` against a private full publish copy. It checks all three retained model property types and JIT-prepares Infrastructure's actual loan-recovery method without DB calls. A self-consistent older model must fail; health200 does not replace this contract.
- [ ] Build the complete image using **repository-root context** and `backend/hrm-api/Dockerfile`; its COPY includes HRM/Procurement/Core src and publishes all referenced assemblies together. Record exact source/tree digest, SDK version, base/runtime image digests, dependency lock/package inventory, WeasyPrint/native/font versions, build log and immutable resulting image ID/digest. Docker tags alone do not prove provenance; current Dockerfile uses moving base/package references, so capture what actually resolved.
- [ ] Privately extract the complete image `/app`, compare ALL publish/image assembly hashes and run the strict loan probe against that extraction. No Domain/Application DLL overlays, divergent Infrastructure, test-header authentication, tools-only scanner or synthetic financial policies inside the production image/config.
- [ ] Record the rollback image/binary contract and database compatibility together. Keep retained v36 available, but confirm its compatibility after additive DDL and any new data; do not deploy the rejected copied build/v37/v38 set.

## 4. Production P19 → final P26 schema path (operator-owned, not executed here)

According to the retained hold/runbook, production remains through P19. This checklist performs no fresh production query; the coordinator must capture a read-only actual inventory before planning DDL. Never infer a live migration state from a branch snapshot or health endpoint.

| Ordered new Procurement migration | Required contract |
|---|---|
| `20261020055251_P20SupplierInvoiceBranchScope` | Nullable invoice branch scope; preserve legacy unknown branch, no invented assignment. |
| `20261020073918_P21PurchaseRequestApprovalSubmission` | Immutable source/policy/Finance submission evidence. |
| `20261020084544_P22SpendingAuthorityConfiguration` | Existing governed finite authority configuration. |
| `20261020095652_P23PurchaseRequestApprovalExecution` | Outcome/assignment/command/Finance-release evidence; not a Finance ledger. |
| `20261020114243_P24PurchaseOrderCommercialExecution` | Exact money and controlled PO evidence; guarded Down. |
| `20261020133820_P25PositiveReceivingExecution` | Receiving source/effect persistence, existing-record extensions/notes; legacy unverified behavior. |
| `20261020144755_P25ReceivingStableEvidenceBindings` | Additive stable EF keys and exact deferred ownership overlay; both P25 migrations are required. |
| **Root's actual generated P26 ID** (pending at inspection) | Matching evidence/allocation overlay/required-null checks, precision and evidence-preserving Down guards. Record actual ID/source/script digests after central generation/review; do not invent a migration here. |

Also inventory existing Core approval `20261019183218_P01CoreApprovalFoundation`, Core documents `20261020060744_P01CoreDocumentFoundation` and `20261020063543_P02CoreDocumentUploadRecovery`, and HRM's already-live loan migration. These contexts have separate histories: Procurement `procurement.__procurement_migrations`, approvals `core_approval.__EFMigrationsHistory`, documents `core_document.__EFMigrationsHistory`, HRM `hrm.__hrm_migrations`. Actual table quoting follows owning EF context. Review history/diff before applying anything; an already-live migration is not a new upgrade step.

**Concrete startup limitation:** `Program.cs` startup and `--apply-migrations-only` call **HrmDbContext.Database.Migrate only**. They do not migrate Procurement/Core approval/Core documents. Even with workflow flags off, new invoice/ordinary CRUD reads can reference P20+ columns, so required owner-reviewed schema must precede serving the new host. Inventory/rehearse pending HRM migrations too because startup may apply them automatically. Do not create a broad inferred HRM snapshot correction for inherited drift.

- [ ] Retain an owner-approved recoverable ERP backup including every relevant schema/history plus private document bytes/config references. Existing `deployment/hrm/verify-backup-restore.sh` dumps/restores only `hrm`; it is insufficient for ERP four-schema/document recovery.
- [ ] Use the final reviewed SQL/migration owner on fresh/restored private rehearsal databases: Up twice, exact history/manifests, populated legacy values, stable binding/nullable-evidence denials, used/unused Down and precision/unknown protection. Reuse root's matching evidence when its SQL hash/source is exact. Never run the rehearsal helper against production.
- [ ] Existing `docs/procurement/sql/p25-legacy-rehearsal/*` and `sql/p26-rehearsal/rehearse.py` document scopes/retained DBs. Review actual generated SQL first; scripts are not a security sandbox. The P26 helper retains clones and cannot substitute missing independent guard/HTTP/runtime gates.
- [ ] Match runtime grants/readiness against the exact overlays and actual current Core branch/catalog fencing; migration owner credentials must not become the application execution identity. Database DDL establishes storage, never employee authority, approved policy, trusted history, invoice approval or AP posting.
- [ ] Do not downgrade used matching/receiving evidence or truncate precision to make rollback work. P24/P25/P26 guarded Down can refuse; record code rollback vs data recovery explicitly before cutover.

## 5. Configuration and disabled-feature readiness

Record redacted key names/state, not connection values/secrets. For first full-host alignment keep new execution off: `Procurement:Approvals:Enabled=false`, `Procurement:Orders:Enabled=false`, `Procurement:Receiving:DraftsEnabled=false`, `Procurement:Receiving:PositiveExecutionEnabled=false`, `Procurement:InvoiceMatching:Enabled=false`, `CoreDocuments:Enabled=false` unless a separate approved production document activation gate exists. Existing approved draft CRUD should remain available only when its schema/permissions are ready; no demo fallback.

`ConnectionStrings:ProcurementApprovalExecution` is the separate bounded same-PostgreSQL transaction identity; a missing value must not be treated as authority to enable a workflow. CoreDocs configuration, if separately activated, uses its owning `ConnectionStrings:CoreDocuments`, private directory, real Clamd socket/limits and schema readiness. Same-physical-store validation for matching must be exact; no guessed/stale local tables. Tools-only known-byte scanner is excluded from production.

Actual `/api/procurement/v1/context` + `/meta` must use authenticated company/branch/user permissions. Check absent/off capability flags, informative disabled transitions, accessible draft CRUD, role/scope denials and current invoice branch protection. Do not satisfy readiness by flipping flags, seeding fake approved rules/manual Finance references, or installing fake complete exposure/reviewer providers. `invoiceQualifiedClaims`, `invoiceValidation` and `financeHandoff` remain false while owners are unavailable. Avoid linking UI capability rollout to an independently unverified shared-host release.

## 6. Existing runbooks/scripts — exact applicability

| Existing file | What it does / required caution |
|---|---|
| `docs/procurement/PROC-OPS-02-LIVE-ENABLEMENT-20261019.md` | Retained Amizo v36/P19, rejected overlays, source/import/setup/P20 history and rollback constraints. Historical, not fresh production status. |
| `docs/hrm/M36-INCIDENT-RUNBOOK.md` | Existing incident/support process; attach exact source/image/DDL/backup incident references. |
| `deployment/hrm/capture-release.sh` | Tags existing `mightyfin/hrm-api:local` and web local images; it does not build or verify provenance. Mutates image tags—do not execute as an audit. |
| `deployment/hrm/rollback-release.sh` | Validates those tags/compose then can replace services; requires backup reference for execute. Hardcoded/default mightyfinance URL/env/layout differ from Amizo; coordinator must review actual deployment mapping first. |
| `deployment/hrm/go-live-rehearsal.sh` / `performance-smoke.sh` | Health/admission/performance checks; defaults target mightyfinance. Set reviewed actual target; health/pass does not cover business/migrations/assembly compatibility. Restore substep is HRM-only. |
| `deployment/hrm/verify-backup-restore.sh` | Creates/drops a generated isolated HRM restore DB; reads live with pg_dump. This is an operator DB job, not harmless read-only audit or full ERP backup proof. |
| `.github/workflows/procurement-verification.yml` | Existing scoped tests/publish/loan/model gates; real status and final SHA matter. Includes base retained filter but not actual native PDF rendering or all latest real receiving/P26 browser gates. |
| `deployment/procurement/README.md` | Earlier browser-local demo deployment description is stale relative to current live adapters; do not use it to infer current feature enablement or replace the tested assets. |

## Release decision record (all pending here)

- [ ] Final exact source/assembly/image/SQL/UI identities reviewed; retained HRM contracts preserved.
- [ ] Required final-source gates and actual runtime/browser/owner proofs recorded with limitations; remaining PDF/CI/security/UAT exceptions explicitly decided by responsible owner.
- [ ] Recoverable backup, rehearsed migration sequence, bounded runtime config/grants and code/data rollback reviewed.
- [ ] Hold29 addressed by coordinator; no independent deployment racing another session; support/monitoring/post-cutover smoke owner assigned.

This checklist neither releases the hold nor authorizes a production change. P26 central schema/actual-runtime work remains the current priority.
