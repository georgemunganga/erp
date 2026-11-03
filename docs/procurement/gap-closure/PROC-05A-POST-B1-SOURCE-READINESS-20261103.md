# PROC-05A post-B1 source readiness — 3 November 2026

Issue169 / draft ERP PR171 follows the [adopted 28-section feature](../features/PROC-05A-GOVERNED-PLAN-REVIEW.md) and GW-ENG-STD-001. This is a source integration record, not test, database or release acceptance.

The Plan branch merged published main `dc6a082db74f9740104e2295988bc6f1f54027d8` in `01fb35a7632944215cf8e9a7bfffb4c0fa399b8f`. Its only conflict was host All-reader admission; resolution retains PlanReviewAllReadPath and B1 RoundPlanAllReadPath. The entire migration directory is unchanged relative to that main, including B1's genuine `20261103163108` migration, designer and snapshot. No duplicate Plan migration or manually modified snapshot exists.

## Source contract review

- Core account resolution and current permissions/branch membership govern commands; linked HR workers remain optional and serve alias/segregation checks. Named assignments use the shared Core ApprovalEngine and enlisted transaction, not a Procurement workflow engine.
- New Draft currency comes from the selected Core company; legacy scope claim confirms its stored currency and records a reason and expected version. No branch or replacement currency is inferred.
- Submitted values use decimal arithmetic and canonical four-place strings in the immutable snapshot. Review found missing line-version pins; the current source now seals line versions and rejects a changed version even with identical values. The added regression case remains unexecuted.
- Submit/decision/withdraw have selected-branch guards, expected versions, actor-bound original-key receipts, audit and outbox records. Retained reads and Company All admission remain separate from write capability. Delegation remains unavailable.

## Remaining specific gates

1. Generate and independently inspect one genuine additive EF migration after B1: `plan_review_submissions`, `plan_review_command_receipts`, nullable `procurement_plans.branch_ref` and its scoped status index, including modeled checks, unique indexes and FKs. The committed model is ahead of the current snapshot; no actual model-parity check was run here. Because shared readiness uses compiled model parity, integrating this source without the genuine migration can also disable other guarded Procurement operations.
2. Run actual SDK builds and relevant Procurement/Host cases on the integrated source, including the new version-pin case; source inspection and `git diff --check` do not replace them.
3. Rehearse actual migration/retention-safe rollback and capture physical checks, columns, indexes, FKs, finite-role restrictions and applied migration. `ProcurementPlanReviewSchemaReadiness.RequiredMigrationId` and `CheckDefinitions` remain intentionally empty. Populate only from verified actual schema evidence.
4. Verify cross-schema atomicity, exact scope/role/alias denial, concurrent commands, original-key recovery and retained history in real-cookie private API/UI and native reconciliation. No Finance write or forecast-to-PR action belongs to this feature.
5. Adopt explicit business policy/assignees/quorum and verify policy publication. Current policy schema admits only named Review and Final stages. Attachment-required policies and SLA/reminder configuration are not implemented by that schema; keep those capabilities unavailable until their own contract and evidence exist. Financial period remains a forecast label. Shared delegation remains a separate prerequisite.
6. Independently review pinned-policy lifecycle behavior: the current handler requires the pinned policy still be Published and unexpired for approval. The specification's policy-change-after-submit promise must be reconciled with that fail-closed admission before live acceptance; do not silently revise a submitted snapshot or substitute another policy.
7. Complete original-screen UI PR103 integration, independent Tier 3 review, operations/rollback and owner UAT. Plan switches stay off; no launch authority follows from source publication.

Root retains all SDK, database, runtime and release ownership. This integration ran source-only git inspection and whitespace checks; it ran no SDK, scaffold, migration, database, Docker, browser or production command. The original dirty `/home/amizo/newworld-erp` checkout was not edited.
