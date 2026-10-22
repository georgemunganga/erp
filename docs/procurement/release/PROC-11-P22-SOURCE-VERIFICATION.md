# PROC-11 P22 source verification checkpoints

This is bounded evidence, **not release/runtime approval**. Production unchanged; execution defaults
off; genuine P22 migration/catalog/finite-role/browser proof still pending. Finance historical exposure
remains unavailable. Canonical P21 migration/designer/snapshot have not been rewritten.

## Source and integration

Branch `codex/procurement-invoice-unit-review-execution`, own isolated worktree
`/home/amizo/procurement-invoice-unit-review-execution`, originally based on `9445dc1`.
Ordinary merge `92164c3` retains published root `5267381`, including actual fixture corrections and
private UDS transport. Existing modes remain alongside standalone `--unit-reviews`.

Independent source review closed original policy binding, exact typed receipts/converse terminal
receipt, actual Core Added/Saved provenance, immutable original transaction lifetime, append-only
Core decisions, purpose-only Core download audit and actual execution-store capability checks.
These reviews do not prove EF/PostgreSQL/runtime correctness.

## First run — preserved failure

- Frozen source `92164c3`; container `procurement-p22-source-20261022_071908`.
- Evidence: `/home/amizo/erp-backups/procurement-p22-source-20261022/20261022_071908`.
- Offline same-container restore succeeded; Procurement Infrastructure compiled.
- Stopped at `CS0246` in InvoiceUnitReviewStorageTests: missing EF Metadata namespace.
- Exit1, not OOM. No tests executed. Import corrected in `663307b`.
- `gate.log` SHA256: `bf4bc123d074d3fcd8591357cb5979cd4636c8e3aa103d615fff98f355348b83`.

## Second run — Procurement pass, host compile stopped

- Frozen source `663307b`; container `procurement-p22-source-20261022_072300`.
- Evidence: `/home/amizo/erp-backups/procurement-p22-source-20261022/20261022_072300`.
- Same-container cached offline restore; network none,1CPU,1200MiB,processor1,shares256,nice10.
- **All Procurement.Tests:1151 passed,0failed,0skipped**, reported test duration26seconds.
- Next host build compiled HRM Infrastructure/Core Documents, then stopped at `CS0173` in
  ProcurementPolicyDraftRoutes438: new string validation issues mixed with existing PolicyRuleIssue.
- Host focused tests and ApprovalHttpPostgresProbe build **did not run**.
- Narrow correction `9d9f19e` maps unit-review publication coverage to existing typed
  `PolicyRuleIssue("stages",message)`; corrected host source remains unverified pending next slot.
- Existing and new nullable/compiler warnings are retained in logs; no warning-free claim.
- Disk stayed>=2.5GB. No database, SQL, migration generation, deployment or runtime mutation.
- `gate.log` SHA256: `a957e75a45161dfa663afe230751cb867a7d0a40fc7f4b221ffd76dc5bdbbe3c`.
- `results/procurement-all.trx` SHA256: `43882a568757733bfbd395eee423bd260bb9abcfd5a9092ba169a378fe1395fb`.

The heavy slot was explicitly handed to the separate minimal HRM reconciliation gate after terminal
failure. Evidence directories/source manifests/scripts are retained; no interrupted run is called pass.

## Required next gates

1. Compile corrected host; focused InvoiceUnitReview + ProcurementApprovalCapabilityHttpTests,
   ProcurementOrderCapabilityHttpTests and ProcurementSourcingCapabilityHttpTests.
2. Build actual private probe, preserving standalone --unit-reviews and existing private UDS modes.
3. Review complete P22 model, generate one additive P22 centrally, freeze actual PostgreSQL CHECK and
   migration manifests (current placeholders intentionally false); rebuild/drift/idempotent SQL.
4. Genuine private finite role: real upstream source/docs/policy/open/task/download/decision/recovery,
   different current matcher consuming retained v2 units, unknown exposure still Blocked/noAP/claims.
5. Actual both-owner-save rollback, savepoint-compatible positive and updated-old-Core-orphan denial;
   named FK/CHECK/unique/cross-scope/concurrency/ACL negatives, unchanged legacy rows, used Down veto.
6. Existing UI port/actual browser then full release compatibility and approval gates. No current bundle,
   full-module integration, qualified Finance/AP or production deployment claim follows from this file.
