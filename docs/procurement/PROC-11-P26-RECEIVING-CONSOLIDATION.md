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

## Coordinated correction and next verification sequence

Root `f3882d5` was ordinarily merged as `42f9395`; its single central
`20261020144755_P25ReceivingStableEvidenceBindings` migration, designer, snapshot and full
P27 plan were retained unchanged. Root subsequently reported baseline480 Procurement domain,
111 mapped API and model-no-drift gates plus fresh/legacy/used correction rehearsals passing.
Those results belong to that root baseline, **not** the combined P26 model or API.

Tool-only `ebe07f6` was cherry-picked as `7238a73`: cross-site receiving policy configuration
uses explicit company scope during fixture setup. `--matching` remains a separate single mode
and reuses those real receiving prerequisites. The AP readiness increment `df3df8b` is not
merged here pending independent review. No new SDK/Node/API/DB/browser job was run.

### Exact serialized gate order after root receiving clears

1. Freeze the final reviewed P26 source SHA, capture clean status and parent identities, and
   acquire the single SDK slot. From `backend/hrm-api`, compile the real host and run the
   bounded new domain/model and mapped gate tests first:

   ```sh
   dotnet build src/Mightyfin.Erp.Hrm.Api --configuration Release
   dotnet test ../procurement-api/src/Mightyfin.Erp.Procurement.Tests --configuration Release --filter 'FullyQualifiedName~InvoiceMatch|FullyQualifiedName~InvoiceMatching' --logger trx --results-directory ../../artifacts/p26/domain
   dotnet test src/Mightyfin.Erp.Hrm.Tests --configuration Release --filter FullyQualifiedName~ProcurementInvoiceMatchingGateHttpTests --logger trx --results-directory ../../artifacts/p26/http-gates
   ```

   The existing matched-route tests exercise default-off/schema/permission gates; they do not
   prove successful database commands. Expected P26 pending-model changes are not a P25 drift
   regression and must not be suppressed by editing the root snapshot before generation.
2. Only after source/model review, root generates the additive migration with the pinned EF
   tool from `backend/hrm-api`:

   ```sh
   ../../.tools/dotnet-ef migrations add P26InvoiceMatchingExecution --configuration Release --project ../procurement-api/src/Mightyfin.Erp.Procurement.Infrastructure --context ProcurementDbContext --output-dir Migrations
   ```

   Rebuild after generation. Independently review Up/Down/designer/snapshot and idempotent SQL.
   Keep both existing P25 migrations and `ReceivingOwnershipConstraintSql` manual FK overlay.
   No duplicate P25, missing owned constraints, silent legacy8dp coercion or unguarded used
   matching evidence destruction may be accepted. Add reviewed populated/used Down guards;
   generation alone does not produce an acceptable rollback.
3. Run the full Procurement suite, Core approval/document suites, scoped Procurement host
   suite and retained HRM compatibility gates from the release alignment runbook. Recheck
   Procurement/Core snapshots. Generate idempotent SQL against the exact preceding correction
   ID `20261020144755_P25ReceivingStableEvidenceBindings`; rehearse fresh and separately
   restored populated legacy upgrades twice, unused Down/Up and used-evidence denial. Raw
   PostgreSQL must exercise scoped/composite FKs, append-only runtime grants, unique commands,
   exact18,4/38,8 boundaries, original-intake preservation, exposure CAS and failed-command
   rollback; InMemory persistence tests do not prove them.
4. Root provisions a **new** guarded disposable `proc_approval_http_*` database and distinct
   owner/runtime roles, then from `backend/hrm-api` executes the existing actual HTTP tool:

   ```sh
   dotnet run --project ../procurement-api/tools/ApprovalHttpPostgresProbe --configuration Release -- --matching
   ```

   Required private environment variables are `PROCUREMENT_APPROVAL_HTTP_PROBE_CONNECTION`
   and `PROCUREMENT_APPROVAL_HTTP_PROBE_BOOTSTRAP_CONNECTION`. Never reuse an already consumed
   receiving/browser fixture. Matching mode must pass actual upstream PO/receiving first,
   then real invoice capture/mapping/policy/unavailable-owner comparison, unchanged receipt
   replay, changed body403/409, scoped history/recovery, concurrent comparison and zero fake
   claims/AP effects. A fixture setup failure is not a matching gate result. Capture logs,
   migration inventory, exact source/tool hashes and terminal status.
5. Existing Bills UI needs a separate typed, capability-gated mapping/comparison adapter and
   actual browser proof before exposing these routes. Retain existing branded layout and
   document controls; no new menu, fake unit verification or local transaction substitution.
   Production reviewer/history owners are still explicitly unavailable, so even a passing
   Blocked-comparison probe cannot enable qualified capacity claims, invoice approval, AP
   handoff or payment. Keep all related production flags off until their own contracts and
   gates pass; reviewed AP readiness is not a Finance ledger or payable acceptance contract.

This is the runnable plan, not execution evidence. Root owns migration generation, synthetic
connection provisioning, resource scheduling and any later release decision.
