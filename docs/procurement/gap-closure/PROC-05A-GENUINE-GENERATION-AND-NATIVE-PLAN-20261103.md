# PROC-05A genuine generation and native verification plan — 3 November 2026

**Status:** Inert plan only, issue169 / draft PR171. No generation, SDK, container, database, schema capture or runtime action is authorized by this document itself. Root owns those steps and their scheduling. There is no external Manus claim for issue169.

## 1. Freeze prerequisites before opening the SDK slot

Plan source now includes published ERP main `950856f7e01e3e2d7c628ddbf67dbac846644803` (accepted merged PR176) through actual source merge `6e4b61376e3e103ace3ecfa262cb5a3bb3ad2957`. This retains published B1 main `dc6a082db74f9740104e2295988bc6f1f54027d8` and genuine migration `20261103163108_M7b1CommercialRoundDraftPlans`. Fetch/merge completed without conflicts; the migration directory remains byte-identical to accepted main. PR176's quotation restored-shape hotfix and actual fixture/tests are retained. No warm candidate was substituted.

Root reports accepted hotfix tested source `6ec` with backend tree `868d0f34952c4e3aa776463fe41f7c06ab960340`, 429 Host passes, 1970 Procurement reuse, no-pending-model/unchanged-SQL and actual old-false/new-true probe. Those are prerequisite hotfix results, not verification of this Plan integration. No SDK/build/model/database check was run on the merged Plan source. At actual SDK pickup, re-fetch current main and record any newly accepted prerequisites before freezing the generation source.

Root must finish the active B1 technical-release/SDK ownership window and explicitly allocate the next slot. Use an isolated worktree and empty dedicated build/artifact directories. Preserve the original dirty checkout and all existing production/private services and data. Record source commit, clean status, toolchain/provider versions from the actual executed environment, and existing migration/snapshot hashes before generation. Do not regenerate B1 or introduce another migration with B1's contents.

## 2. Exact expected model delta against the genuine B1 snapshot

Inspect the actual EF result rather than assuming this inventory is exhaustive:

| Owning source | Expected physical delta | Required review |
| --- | --- | --- |
| `ProcurementPlan.BranchRef` and `ProcurementDbContext` index | Nullable `procurement.procurement_plans.branch_ref` plus tenant/entity/branch/status index | Existing branchless rows remain null; no default, backfill or inferred branch/currency |
| `PlanReviewMapping` | New `plan_review_submissions` table | Base scope/audit/version columns; amount precision18,4; immutable snapshot/policy/actor pins; tenant/entity/plan and tenant/entity/policy FKs; both unique bindings; `ck_plan_review_submission` |
| `PlanReviewMapping` | New `plan_review_command_receipts` table | Base scope/audit/version columns; actor/original-key/fingerprint/result fields; plan FK; unique tenant/entity/key and decision lookup indexes; `ck_plan_review_receipt` |
| `OperationalPolicyIdentityMapping` | Replace existing `ck_operational_policy_identity` | Add `PROCUREMENT_PLAN_REVIEW` to policy-key allowlist and schemaVersion2-only subset, preserving every existing key, lineage/actor/hash/time/append-only predicate |

The shared CHECK replacement is a material fourth delta, not optional cleanup. The genuine designer/snapshot must contain it. No Core Approval table or Finance schema change is expected; Plan continues using the existing shared Core engine. Any additional generated operation needs explained ownership and independent review before proceeding. Do not hand-copy model mappings into a snapshot, fake a generated designer, or claim no-pending-model without the actual EF check.

## 3. Genuine generation evidence and rollback review

Generate one additive migration from the frozen accepted baseline with the repository's actual pinned toolchain, retaining raw generator output and initial hashes before any review-driven changes. Capture final migration/designer/snapshot hashes, complete operation inventory and generated SQL. Run actual model parity and applicable Procurement/Host tests only in Root's allocated slot. Record exact command, source, result and evidence path; source/diff checks are distinct.

Review Up for the four deltas above and no unrelated recreation, renaming, grants, data transformation or existing-row deletion. Review Down as destructive to Plan evidence: in the private clone exercise empty-state rollback where appropriate and prove rollback is refused/held when retained submissions/receipts or Plan policy identity rows exist. Never silently delete those rows to make a rollback pass. Shrinking the shared CHECK also becomes invalid once any Plan policy identity evidence exists. Preserve B1 enrollments/revisions/barriers and all existing policy evidence through Up and failure recovery. Where a safe Down cannot preserve records, document a forward-only recovery and leave old binary writes disabled rather than weakening physical constraints.

## 4. Actual readiness capture and integration hold

After genuine Up on the isolated private owner store, collect the actual PostgreSQL `pg_get_constraintdef` output for all three changed checks, physical column/nullability/type/precision/default metadata, unique/partial indexes, FKs, applied migration history and owner/role facts. Never derive a purported physical fingerprint from model SQL text or amend an old fingerprint by string substitution.

Two readiness integrations are required in the same reviewed candidate:

1. Populate Plan's currently empty `RequiredMigrationId` and two-table `CheckDefinitions` only with the actual migration identity and captured definitions. Preserve exact full-set checks and provider/model/applied-history gating.
2. Extend `ProcurementOperationalPolicyIdentitySchemaReadiness` with a migration-specific post-Plan shared CHECK capture and selection. It currently selects the owner-baseline capture when that migration is applied; that older CHECK excludes `PROCUREMENT_PLAN_REVIEW` and will reject the post-Plan physical CHECK for **all** operational-policy consumers. When the actual Plan migration is applied, select only its verified new shared capture; do not continue selecting the owner-baseline check or broaden comparison to a policy-key substring. Keep pre-Plan captures usable only for their exact earlier applied-history states. If a real backup/restore spelling alternative is needed, capture and independently verify it through the existing exact alternative mechanism.

The Plan two-table manifest is not a replacement for the shared policy capture. Do not relax the shared checks, bypass policy identity readiness, accept missing/newer unknown captures, or treat a configured feature flag as readiness. Compiled model parity also protects existing guarded operations: Plan source must not enter a runtime before genuine migration/snapshot and both readiness updates are integrated. Current published B1 main is unaffected by this source-only hold.

## 5. Meaningful verification matrix after real capture

| Scenario | Required result/evidence |
| --- | --- |
| Clean B1 clone → genuine Plan Up | All four intended deltas; old policy/B1 rows and fingerprints retained; actual model parity clean |
| Valid post-Plan schema and finite read role | Shared policy identity readiness succeeds for existing receiving/invoice/unit/payment/owner-baseline retained readers and Plan retained reader without writes |
| Valid post-Plan schema and finite execution/configuration roles | Intended Plan and policy operations admitted; no superuser/owner/assumable elevation, broad Core writes, Finance writes, append-only mutation or DELETE/TRUNCATE authority |
| Post-Plan applied history but old owner-baseline CHECK | Shared identity and Plan admission fail closed; no writes |
| Post-Plan CHECK but missing migration, empty/wrong capture or wrong CHECK predicate | Fail closed even if `PROCUREMENT_PLAN_REVIEW` text appears; test dropped/extra predicate and unknown key |
| Pre-Plan applied history on its compatible compiled binary | Existing exact capture remains accepted; no Plan capability appears |
| Partial installation, migration failure, changed unique index/FK/column/precision or missing privilege | Typed unavailability/denial, no partially opened Core case or receipt |
| Snapshot/source mismatch or pending model/migration | Guarded operations unavailable; never claim a false healthy baseline |
| Unused empty rollback versus retained Plan evidence | Proven empty rollback behavior and explicit retention hold/forward recovery with all original rows preserved |
| Normal-cookie maker → reviewer → final approver | Workerless Core principals supported; exact line IDs/versions/decimals; one pinned shared Core case; Active only after final decision; audit/outbox/receipt consistency |
| Policy replaced/expired after Submit | Exact original policy/provenance retained; new submissions select current policy; wrong ID/version/rules/Revoked/Retired denied |
| Current permission/branch/account/alias/assignment/delegation change | Immediate denial with no decision or state change, even when retained policy proof is valid |
| Lost response, same-key replay, changed payload, concurrent votes/withdrawal | Stable actor-bound receipt or conflict; one committed effect; atomic plan/Core state |
| Return/edit/resubmit; reject/withdraw; options off | Historical sealed round remains readable under current scope; new round distinct; writes disabled independently |
| Original UI PR103 and Finance reconciliation | Original screens and Company All versus selected-branch behavior verified; no budget reservation, PO or Finance balance mutation |

Provider tests must exercise the actual readiness selector and real owning transaction; pure helper tests alone cannot prove this compatibility. Native owner reconciliation must compare relevant existing policy/B1/Plan/Core/audit/outbox rows before/after, verify only the authorized cohort changed, and preserve all legitimate records. Do not invent a private fixture, account, policy, grant, backup or rollback acceptance.

## 6. Delivery and final gates

Commit/push the single reviewed genuine migration/designer/snapshot with both actual readiness captures and their regressions, then record exact published candidate and bounded evidence. Distinct reviewer checks source/SQL/grant/rollback and shared-workflow compatibility. Root owns private API/original UI/native acceptance and any later feature activation, production backup, migration and release decision. Business configuration remains explicit; attachment-required policies, delivered notices and SLA/reminders remain unavailable as recorded in the [source readiness record](PROC-05A-POST-B1-SOURCE-READINESS-20261103.md). A generated migration or green source suite alone is not live acceptance.

## Concrete inert SDK packet

The source merge `d2c24ae4ea8f156406ec8ab23d94a992fc2c3921` incorporates published B1 release main `f064f1a343e9897f4234bed7b6fd9586aebcded3`. Packet `/home/amizo/erp-backups/procurement-plan-review-generation-sdk-20261103_run01` contains actual pinned source/overlay JSON, archive-aware R2 publication guard, guarded `prepare_overlay.py`, mandatory-offline-restore and genuine-generation `run.sh`, exact bounded launch argv/monitor contract, NuGet configuration and serial compiler response. It binds accepted warm `6ec41f938409f9400bd627f727c0161af0043479`, exact27 backend file changes,23 protected current publications and29 history entries including6 archived; all B1 migration and SQL references are preserved.

Only shell/Python syntax parsing was performed; no warm overlay, SDK/container/database execution or generated EF output is claimed. Root must claim169, bind current actual stopped-private-PG and resource/SDK-slot evidence, review exact packet hashes and run through its bounded supervisor. Generated raw migration/designer/snapshot and tests/model/SQL results remain pending; shared post-Plan policy CHECK capture/selector is still a separate native integration gate. Packet preparation does not authorize publication or runtime activation.
