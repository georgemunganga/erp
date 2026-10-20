# P26 receiving source consolidation

Status: source integrated, unverified. No SDK, Node, browser, API, migration or database job was run for this increment. P26 remains default-off; production original-unit reviewer and historical Finance owner adapters remain unavailable.

An ordinary merge incorporates root order-execution `e5ed55c` into the existing P26 branch, retaining both histories and the existing single central P25 migration. It includes the catalog classification fences and canonical request input, stable receiving model and ownership helper, corresponding tests and the private receiving browser fixture. No P26 migration or model snapshot is generated here; root owns the subsequent additive P25 correction and later P26 generation.

## Conflict resolution

Only the shared probe argument validation and README conflicted. The supported modes remain single `--matching`, `--receiving`, `--orders`, `--settings`, `--serve`, or `--serve` plus exactly one of `--orders`/`--receiving`. `--matching` still runs the real receiving prerequisite followed by unavailable-owner mapping/comparison checks. It cannot combine with serving. Both fixture instructions are retained.

## Matching readiness

Matching still requires the receiving schema-readiness prerequisite. It additionally executes `ReceivingOwnershipConstraintSql.ManifestQuery` on the matching-owned connection itself after matching model/migration, column and runtime-privilege checks. This uses the exact migration-owned manifest rather than a weaker duplicate definition. Missing, wrong-shape, nondeferred or unvalidated ownership FKs return false. The merged receiving PostgreSQL probe already authors all four transactional manifest-tamper denials and rollback restoration; no new unsafe schema-mutation mechanism is added.

This does not substitute for the root's additive correction migration or its real PostgreSQL gate. Matching has no path to assert ready while its own migrations are pending.

## Accepted-source and ordinary-read privacy audit

`ProcurementReceivingRoutes.InvoiceEvidence` continues to lock source requests/order and related historical states, reuses sealed `Source` and reconciled `Ledger`, and does not call state-creating `Resolve`. Related allocations across the same source PR are read internally for conservation; the acceptance stream returned is for the mapped issued order/its lines. No raw supplier-invoice rows or other-invoice document/validation references are returned by this bridge.

The comparison command retains complete owner/local consumption evidence internally. Ordinary invoice reads use `InvoiceMatchReadProjection.Summary` instead of returning raw `EvidenceJson` or `SourceWatermarksJson`: only original invoice-line comparisons/discrepancy codes and a watermark digest are exposed. Existing regression coverage explicitly puts another invoice identity, private validation reference and unrelated commercial data in raw evidence and asserts their omission. Mapping snapshots are restricted to the current invoice and scoped issued-order binding; command recovery revalidates the same invoice/order scope.

Review found no new cross-invoice response disclosure in the consolidated bridge/read projection. This is source review, not proof of positive claims or Finance/AP integration. Parent-owned compilation, model/migration and actual restricted PostgreSQL checks remain required before any activation.
