# M6 principal invoice matching: source audit and implementation boundary

Source checkpoint: ERP `e03ac9735379b35f789d9e900fe849d7ab02f1f1` (merged M5d). This plan is source-only. No M6 migration, feature activation or business command has run.

## Existing contracts that can be retained

- `InvoiceMatchingFoundation` has deterministic two-way and three-way comparison, exact decimal arithmetic, tolerance and cumulative quantity/value checks. `InvoiceMatchExecutionEvidence` validates owner checkpoints and qualified claims. Existing invoice intake, original-document review, command receipts, exceptions, audit and outbox remain the owners of their respective records.
- Mapping and compare routes already require explicit Core permissions and keyed commands. `InvoiceMatchMappingVersion.IdentitySchemaVersion=2` supports a workerless Core principal; this is **actor identity**, not an M5 source-schema marker.
- `IInvoiceHistoricalExposureOwner` is registered as `UnavailableInvoiceHistoricalExposureOwner`. The fallback original-unit reviewer is also unavailable, though the retained unit-review route can provide a real review. A matching policy or an empty allocation table cannot turn either default into authority.
- The matching flag is default-off. Current readiness depends on the legacy receiving schema and existing matching tables. Production has not accepted M6 source/owner readiness.

## Concrete M5 integration gaps

1. `ProcurementReceivingInvoiceProjection.InvoiceEvidence` and both selectors call the legacy receiving loader and filter legacy goods/service rows with `ExecutionVersion=1`. M5b accepted facts are `PrincipalReceivingSourceState` and `PrincipalReceivingFact` with a separate typed payload and sequence.
2. `InvoiceMappingLine` and projected `InvoiceMatchDocumentLine` can name only `GoodsReceiptLineId` or `ServiceAcceptanceId`; `InvoiceMatchAllocation` can bind only the legacy `ReceivingSourceStateId` and legacy acceptance IDs. Treating an M5 fact UUID as one of these IDs would bypass ownership FKs and change v1 JSON semantics.
3. M5d return drafts are proposal-only. No approved return effect exists that can safely populate `ReturnedQuantity`/`ReturnedNetAmount` for M5 accepted facts. A draft must not reduce physical acceptance or replenish billing capacity.
4. Qualified claims are impossible under the installed default historical exposure owner. `CompleteHistory` deliberately refuses an incomplete checkpoint and owner/local overlap. Replacing it with a local-only count would be an unauthorized completeness claim.
5. The current matching result and mapping receipts are versioned but their retained v1 JSON bytes/hashes must stay unchanged. An optional property on the old record without a dedicated codec would risk rewriting them.

## Smallest coherent release slice

One new M6 source variant on the existing invoice matching routes: typed M5 issued revision plus M5 accepted fact mapping, current Core original-unit review, two-way/three-way evaluation, blocked results and exceptions, and qualified claims **only** when a real historical exposure owner returns a complete, current, independently reconciled checkpoint. The source variant remains default-off until its schema, roles, policy, unit-review and owner gates all pass. The owner implementation and overlap protocol are part of the slice, not a later optional enhancement. Finance posting is explicitly out of scope.

The release may be built in smaller reviewable PRs, but no PR by itself is labeled M6 complete:

1. Typed source projection and pure tests (no write flag): M5a issued graph, M5b fact chain/payload digest, exact acceptance quantity/value and kind, no M5d proposal effect. Retain legacy v1 loader unchanged.
2. Schema and codec: add a distinct mapping payload schema for M5 facts, explicit source-schema marker independent of actor schema, typed principal fact/source-state FKs on immutable claims, and targeted indexes/CHECKs. Generate the EF migration and snapshot with the official SDK. Down vetoes any retained M6 mapping/result/claim/receipt, then preserves old v1 tables and hashes.
3. Host transaction: branch by mapping source schema; load all M5 facts under the shared serializable coordinator with stable locks; verify current Core scope, original invoice/document evidence, issued revision, policy and duplicate inventory; run the pure comparison; record blocked result or owner-backed claim atomically with audit/outbox/receipt. Original-key recovery verifies current actor/scope and typed retained response, without treating history as a new approval.
4. Exposure owner: implement authenticated, bounded snapshot/lease reading from a named Finance/AP owner or a reviewed authoritative migration baseline. Bind owner reference, all prior allocations and watermark. Reconcile it with every active local claim, with no overlap or unexplained gap. Until this producer is accepted, the write gate permits blocked diagnostics only and makes zero qualified claims.
5. Private and release proof: genuine migration Up/Down/Up on a nonempty clone, full owner graph, finite roles, normal-cookie two-way/three-way/negative/recovery flows, original v1 row/digest preservation and no Finance posting. Production rollout is separately reviewed.

## Proposed file ownership

| Owner | Existing or new files | Change |
| --- | --- | --- |
| M6 Domain/Application | `InvoiceMatchingContracts.cs`, `InvoiceMappingEvidence.cs`, `InvoiceMatchingFoundation.cs`, `InvoiceMatchExecutionEvidence.cs`; new `PrincipalInvoiceSourceEvidence.cs` | Separate source schema/typed fact contracts, original-hash-safe codec, principal fact validation and claim ceiling. |
| M6 Infrastructure | `InvoiceMatchingExecutionRecords.cs`, `ProcurementInvoiceMatchingMapping.cs`, `InvoiceMatchingOwnershipConstraintSql.cs`; genuine new migration/Designer/snapshot | Typed principal source/fact FKs, append-only and CAS guards, exact physical readiness. |
| M6 Host | `ProcurementReceivingInvoiceProjection.cs`, `ProcurementReceivingInvoiceSelectors.cs`, `ProcurementInvoiceMatchingRoutes.cs`, `ProcurementInvoiceMatchingCommands.cs`, `ProcurementInvoiceMatchingReads.cs`, `ProcurementInvoiceMatchingExecutionOptions.cs`; new principal loader and owner adapter | Existing routes gain source-version dispatch, current Core authorization, current-source locks and typed receipts. Legacy branch is unchanged. |
| Core/Finance owner | `IInvoiceHistoricalExposureOwner` implementation and signed checkpoint producer, plus owner reconciliation tests | Complete history and overlap authority. No Finance posting API is added. |
| Tests | Existing `Invoice*Tests.cs` plus new principal-source/owner/HTTP tests | Meaningful v1 byte parity, workerless actor, stale/tamper, concurrency and real owner graph cases. |
| Documentation/UI | This child spec and M6 acceptance; existing invoice views only after route contract is frozen | Mark preview versus qualified capacity; no payment approval claim. |

## Decisions before code that can reserve capacity

Identify the historical exposure owner and its exact authenticated complete-set contract, including any pre-M6 invoice/claim baseline and duplicate ownership. Define which approved return-effect record will reduce M5 acceptance, if such a route is included; M5d drafts do not. Choose the mapping payload source schema and physical FK shape so existing v1 response hashes remain byte-identical. These decisions do not block implementing the pure M5 source verifier and its negative tests, but they block turning on qualified claims.
