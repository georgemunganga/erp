# PROC-05A genuine migration adoption — 3 November 2026

> **Later actual checkpoint:** Published main556fbee2 is integrated without changing tested backend tree `c4d0703b357ba2674cd4baffd1e90e80a069ef7b`. Root actual548 Host / exact2004 Procurement reuse, model/SQL, dual-native providers, real shared policy lifecycle and bounded original-UI primary path are recorded in the [private integration checkpoint](PROC-05A-PRIVATE-INTEGRATION-CHECKPOINT-20261103.md). Earlier pending/not-run statements below are historical where that checkpoint explicitly supersedes them. Strict post-primary native owner/retention checks and production release remain pending; failures remain retained.

Issue169 / draft PR171 adopts actual EF migration `20261103211606_ProcurementPlanGovernedReview` from Root's isolated SDK run. Frozen generation source `d2c24ae4ea8f156406ec8ab23d94a992fc2c3921` includes published release main `f064f1a343e9897f4234bed7b6fd9586aebcded3`; accepted warm base is `6ec41f938409f9400bd627f727c0161af0043479`. Actual toolchain/provider follows the pinned packet, with raw generation evidence retained at `/home/amizo/erp-backups/procurement-plan-review-generation-sdk-20261103_run01`.

Root reports Infrastructure build succeeded (11 existing warnings), actual full Procurement suite **2002 passed / 0 failed / 0 skipped**, then Host compilation failed with CS0833 before Host tests. This was not an OOM/resource stop. The original terminal, restore/build/scaffold/test logs, TRX and raw-generation reconciliation are retained. No model-parity/SQL/native/Host success is inferred from that partial run; no database, publication or production changes occurred. Raw reconciliation admits exactly the three genuine EF files and preserves old publications. The prior1970 runtime was independently archived before warm binaries changed; it is not substituted for the actual2002 result.

Raw generated hashes:

| File | SHA256 |
| --- | --- |
| migration.cs | d65ee5034ba00e9eb97e7797117f805f83183b705721abda503f10e156724c4c |
| migration.Designer.cs | 7ed3a5ec01af12d17ed9213bd9991abc7f83f6315d4efbb8cd1b2e7d71cb1616 |
| ProcurementDbContextModelSnapshot.cs | e4b814d0608b572f47aa05cd7a3da0d24c01879a6c02073573a4e12477957ecc |

The designer and snapshot are adopted byte-identically from raw output. Migration.cs adds only the separately reviewed Down retention SQL prefix; Up is unchanged. Both B1 migration/designer remain byte-identical to published main and no B1 SQL file is edited. The actual delta is exactly two owned review tables, nullable plan branch plus scoped index, and replacement of the existing operational-policy identity CHECK to admit Plan schemaVersion2 evidence.

Host CS0833 arose from three inferred `Id` members in the PlanSubmitted audit object. The source fix explicitly names submissionId, caseId, policyVersionId and snapshotSha256. It is unexecuted until the next Root SDK slot. New migration-operation tests verify the bounded Up delta and that a transactional global Down guard precedes every DROP, locks all four tables, retains any receipt/submission/Plan policy identity or non-null branch, and performs no destructive data cleanup. These new tests have not run. Actual2002 predates these changes and cannot verify them.

Down locks `operational_policy_identity_evidence`, `plan_review_command_receipts`, `plan_review_submissions` and `procurement_plans` in ACCESS EXCLUSIVE mode before checking any retained rows. SQLSTATE55000 veto protects claimed/new Draft branch scope even without review rows and all terminal rounds/policy lineage across tenant/entity/status. A missing table/column fails before DROP. EF transaction suppression is absent. Native empty Down, independent retained-record vetoes, concurrency/partial-install failures and exact recovery remain required under the [generation/native plan](PROC-05A-GENUINE-GENERATION-AND-NATIVE-PLAN-20261103.md); the private retention proposal retains the detailed cohort matrix. Raw Down must never be applied in place of this reviewed guard.

The actual generated migration is source, not runtime admission. Plan readiness manifest stays unavailable pending actual provider checks and captured physical definitions. The post-Plan shared policy CHECK capture and migration-specific shared selector must be independently integrated before existing operational-policy workflows can admit this schema. Independent schema/security review owns that work; this adoption does not modify its readiness or generic Core read helpers. Root retains all further SDK/native/finite-role/API/UI/release operations.

Final adopted hashes:

| File | SHA256 |
| --- | --- |
| 20261103211606_ProcurementPlanGovernedReview.cs | 3458d8a508ba093e271fe5d916e4832b99e1eb6d5d9e0f61e53353f57a5333e4 |
| 20261103211606_ProcurementPlanGovernedReview.Designer.cs | 7ed3a5ec01af12d17ed9213bd9991abc7f83f6315d4efbb8cd1b2e7d71cb1616 |
| ProcurementDbContextModelSnapshot.cs | e4b814d0608b572f47aa05cd7a3da0d24c01879a6c02073573a4e12477957ecc |
