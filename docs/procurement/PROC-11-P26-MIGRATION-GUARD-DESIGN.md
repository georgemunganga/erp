# P26 migration guard design — actual combined source model

**Status: source design and corrections only. No EF generation, SDK/test, database or deployment execution.** Root owns generation after the combined-source compile gate. Root P25 evidence was ordinarily merged through `5b79372`; its passed480-domain/111-API, migration, actual CLI and real browser/owner-verifier results remain evidence for that baseline, not P26.

## Actual model delta

| Owned table | Required additive change |
|---|---|
| `invoice_match_mapping_versions` (new) | Scoped base ID/version/audit; invoice/issued-order pins; positive mapping revision; original/mapping SHA; immutable mapping/verification JSON; principal/worker/time. Unique tenant/entity/invoice/revision; exact issued-order and invoice FKs. |
| `invoice_match_exposure_states` (new) | Scoped base; PO/issued/supplier/currency pins; sequence and Unknown/Verified/Stale coverage; nullable owner reference/watermark/SHA/expiry. Unique tenant/entity/PO/issued. Verified requires owner evidence metadata; local empty rows are not owner completeness. |
| `invoice_match_allocations` (new) | Scoped base; exact invoice/line/result/exposure/PO/issued/POline/supplier/receiving refs; debit/credit-link fields (current CHECK only permits Debit/no original debit); quantity18,4, net/tax38,8; positive exposure sequence and validation/owner refs. Unique result/invoice-line and exposure/sequence/invoice-line; exact relational ownership. |
| `supplier_invoices` (existing) | MappingRevision(int0), CurrentMappingVersionId(nullable), LastMatchResultId(nullable), ActiveMatchClaimResultId(nullable), MatchingState(NotCompared), MatchRevision(bigint0). Composite pointers scoped to this invoice, no legacy inferred mapping/claim or changed intake amounts. |
| `invoice_match_results` (existing) | ExecutionVersion(default0), nullable mapping/PO/issued/actor/command/hash/evidence/source-watermark fields, InvoiceVersion(bigint0). Scope/invoice/issued/mapping relations and unique nonnull command key. Execution1 requires owned exact evidence; legacy0 not promoted. Existing nullable TaxVariance widens18,4→38,8; QuantityVariance/PriceVariance remain18,4. |
| `invoice_exceptions` (existing) | Nullable MatchResultId, SupplierInvoiceLineId, DiscrepancyKey(max150), OwnerWorkerId, ResolvingMatchResultId, with same-invoice composite FKs. Preserve legacy exception text/status/owner data. |
| P25 goods/service parents | New unique **indexes**, not EF alternate keys, on tenant/entity/id/PurchaseOrderLineId/SourceStateBindingId for the two P26 exact allocation overlays. No receiving values are rewritten. |

All base columns/names/nullability and generated constraint/index names must be verified against the generated artifact, not guessed from this table. Original invoice/line net/tax/quantity/rate fields retain their existing18,4 schema; this migration does not reinterpret prior rounded intake as verified original-unit/document proof. No Finance ledger/payment table, supplier master or new identity master is introduced.

## Combined-model correction before generation

The audit found that P26 allocation `.HasPrincipalKey(...SourceStateBindingId)` reintroduced mutable receiving completion pins as EF keys, undoing P25's stable-key correction. Source now uses stable tenant/entity/record-ID EF relationships for goods/service parents. Separate unique indexes support literal `InvoiceMatchingOwnershipConstraintSql.Install` deferred exact FKs binding allocation receiving record **and PO line and state**. Their absence is never covered by application validation alone.

Root must append that literal Install to generated Up after the parent indexes and allocation table exist. Preserve both existing P25 migrations, the full `ReceivingOwnershipConstraintSql` three-FK overlay and its structural manifest. P26 Down must remove its own dependent overlay constraints before dropping referenced indexes; it must not remove P25 constraints. Matching schema readiness requires both exact manifests on its own database connection, including columns/order, referenced unique index validity, validated FKs, NO ACTION, DEFERRABLE INITIALLY DEFERRED. No generated-only FK substitute or disabled validation is acceptable.

Source model regression asserts GoodsReceiptLine SourceStateBindingId/PhysicalBindingId/ReceivingSourceStateId/PhysicalObservationId and ServiceAcceptance SourceStateBindingId/ReceivingSourceStateId/CertificationEffectId occur in no EF key and have ValueGenerated.Never. Existing exact receiving-state scope remains separate. These assertions await execution.

## Up safeguards

1. Start from exact preceding `20261020144755_P25ReceivingStableEvidenceBindings`, using an isolated copy/restored rehearsal. Review all generated operations: only the model delta above; no duplicate P25 or unexpected receiving-key churn.
2. Widen TaxVariance without rounding/casting smaller. New nullable metadata and explicit zero/default discriminators preserve old results/invoices. No batch promoting legacy rows or manufactured evidence.
3. Install new exact ownership overlay after its unique indexes. Verify both P25 and P26 manifests. Reject invalid retained constraints rather than dropping/replacing them silently.
4. Publish idempotent SQL; apply twice fresh and on populated P25/legacy snapshots. Compare original invoice/line/result/exception and complete receiving rows, migration inventory and retained v36 payroll contract. No business effects should be seeded by applying DDL.

## Down — first operation must be a literal used-evidence stop guard

A transactionally executed guard must acquire `ACCESS EXCLUSIVE` locks on every inspected matching, invoice, result, exception, commercial receipt, audit and outbox table **before** its first existence check. Retain those locks through the migration transaction so concurrent commands cannot create evidence between inspection and DDL. The guard must run **before** dropping any pointer, FK, check, index, column or table. Refuse with an actionable exception if any of these exists:

- Any row in any of the three new matching tables, including an Unknown/sequence0 exposure checkpoint or an unverified mapping. Blocked results and unverified proposals are retained evidence too; no claimed-capacity-only loophole.
- Any invoice nonzero MappingRevision/MatchRevision, nonnull mapping/result/active-claim pointer, or MatchingState other than the untouched default NotCompared (null/unexpected raw values also require review).
- Any existing match result ExecutionVersion<>0 or any nondefault new execution metadata: mapping/PO/issued pins, InvoiceVersion<>0, actor/worker/command/hash/evidence/source-watermark values. Do not rely only on Outcome, because legacy outcome strings overlap.
- Any exception new matching/result/line/discrepancy/worker/resolving-result metadata populated. Preserve legacy exceptions unchanged rather than deleting them to permit reversal.
- Any durable commercial receipt for InvoiceMapping/InvoiceCompare, operation-named matching audit evidence, generic audit records with RecordType InvoiceMatchMappingVersion/InvoiceMatchExposureState/InvoiceMatchAllocation (regardless create/update action), or supplier_invoice.compared.v1 outbox evidence. Do not strand replay or audit evidence by removing its schema.
- Any nonnull TaxVariance that cannot round-trip to numeric18,4 exactly: `abs(tax_variance)>99999999999999.9999 OR tax_variance<>round(tax_variance,4)`. No USING round()/truncation or overflow fallback. Check before narrowing even when ExecutionVersion0.

A pristine matching schema with unrelated retained legacy invoice/results may reverse only after these checks pass, within the same transaction. Retained external Finance/AP policy/configuration records are not erased by Down. If used evidence exists, roll back code using compatible disabled capabilities or restore a reviewed backup; do not delete evidence or force the migration history.

## Required actual verification source/gates

- Fresh and restored populated Up twice; original field/count/hash preservation; genuinely unused Down/Up; each independent used-evidence/precision stop condition leaves schema/data/history unchanged.
- Actual same-scope wrong-record/line/state allocation FKs and tenant/company/invoice/issued ownership; unique command and exposure sequence; ordinary readers/runtime cannot UPDATE/DELETE immutable mapping/results/allocations.
- The authored HTTP tool adds P26 manifest tamper checks (missing, lookalike, nondeferred, unvalidated) with rollback, plus owner-only synthetic allocation insert/rollback against an actual **Blocked** comparison and actual receiving parents. Valid relation inserts are rolled back; foreign goods/service binding must fail the named deferred exact FK. No synthetic owner completeness, successful commercial claim or payable is asserted.
- Full P26 domain/model/API tests plus real restricted `--matching`: unavailable real document-review and Finance-history owners must yield Blocked and no claims/AP. Concurrent comparison, replay, scope/current-grant and zero-GET-write gates remain required.
- Existing full actual receiving workflow must remain usable under the **combined P26 model**, so record/certification can update completion bindings without EF key mutation. P25 baseline passes alone cannot prove that regression absent.

No P26 migration or readiness is claimed until the parent completes generation, distinct SQL review and these actual gates. This document does not lift the shared-host deployment hold.
