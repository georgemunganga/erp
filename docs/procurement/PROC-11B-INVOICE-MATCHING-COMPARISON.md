# PROC-11B — Invoice matching comparison foundation

**Status:** Source authored; unverified and not operational. **Issue:** [#30](https://github.com/georgemunganga/module-connect/issues/30). **Parent:** [PROC-11](PROC-11-INVOICE-MATCH-AP-HANDOFF-AND-PAYMENT-VISIBILITY.md). **Gap/dependency map:** [PROC-11A](PROC-11A-MATCHING-FOUNDATION-GAP-MAP.md). No migration, transport, UI or production activation in this increment.

## 1. Feature Identification

Procurement → Supplier Invoices and Procure-to-Pay → Invoice Matching → Compare configured two/three-way evidence. Source entry point: `InvoiceMatchingFoundation.Evaluate`. Engineering implementation owner: Procurement; business policy owner: AP/Procurement governance, individual to be named.

## 2. Purpose and Business Outcome

Explain whether one received invoice is consistent with an issued order revision and, when required, accepted goods or services, including earlier partial bills and validated linked credits. A comparison result grants no posting/payment/approval authority.

## 3. Scope and Exclusions

Supports one issued revision per invoice, exact PO-line mapping, category/currency two-way or three-way rules, goods quantity, service quantity plus certified net-value ceiling, complete prior consumption, duplicate detection and immutable evidence/replay. Excludes operational API/persistence, tax law calculation, non-PO invoices, FX, UOM conversion, value-only milestone billing, revision carryover without authoritative mapping, tolerance override, AP, ledger, credit accounting and payments. Department-confined matching blocks until an authoritative department adapter exists.

## 4. Parent and Related Features

Consumes existing SupplierInvoice/Line, PurchaseOrder/Line/Revision, GoodsReceipt/Line, ServiceAcceptance, SupplierReturn and approved policy references. PROC-10 owns intake; PROC-08/09 own issued/accepted truth. Core documents own attachments; Finance owns liabilities and commercial credit application. No duplicate source masters.

## 5. Actors and Participants

Future authorized AP matcher, Procurement policy owner, receiver/service certifier, discrepancy owner and auditor. Current evaluator takes a server scope; it does not authenticate a caller or substitute for current backend permission checks.

## 6. Entry Points and Triggers

Future received-invoice mapping/correction, changed source evidence, new acceptance, validated credit or explicit rerun. Current source-only library is not mapped to an HTTP endpoint or UI button.

## 7. Preconditions

Current canonical authorized actor; tenant/company and allowed branch; invoice state Received/MatchPending/Exception; exact invoice version; explicit issued revision and accepted evidence references; exactly one applicable category/currency rule; effective approved policy; complete duplicate and prior-consumption watermarks. Invoice UOM must be verified independently—intake currently has no UOM field, so the projection factory leaves it blank unless supplied by a verified mapping adapter.

## 8. Workflow

Freeze/sort input projections → hash command/scope/source/policy/history → recover exact prior comparison or reject changed replay → validate scope, identity, publication/effectivity and completeness → validate exact numeric representations and mappings → validate prior debit/credit links → compare line prices and cumulative PO/accepted quantity, tax and gross amounts → verify invoice header/line totals → return explanatory immutable comparison evidence. Never update source status, counters, receipts or Finance.

## 9. Status and State Transitions

Foundation outcome: Compared or Blocked. These are comparison results, not persisted invoice states. `ComparisonPassed` means no comparison discrepancy; `CanPostToFinance` is always false in this foundation. Existing invoice state remains unchanged. Operational Matched/Validated/Handed-to-AP transitions require separately verified command persistence and downstream contracts.

## 10. Permissions and Data Scope

Future match action requires its explicit registered permission and canonical server identity, record scope, document access and exception authority. Current invoice read/capture permissions do not imply matching or approval. The evaluator consumes `IProcurementScope` and denies tenant/company/branch mismatch or confined-without-branch. Department context fails closed because the current invoice schema cannot prove its department. No unrestricted route or role grant is added.

## 11. Business Rules

- One explicit approved rule per category/currency; missing/duplicate rules block. TwoWay requires no acceptance mapping; ThreeWay requires the exact goods/service acceptance kind and matching line/revision/UOM.
- Quantity/price/tax/total each have nonnegative absolute and percentage limits plus explicit AllLimits/AnyLimit combination. Zero/zero means exact. No silent global limit or per-partial-bill tolerance reset.
- Invoice/PO/acceptance must agree on supplier, currency, scope and revision. Returned quantities/value reduce capacity; reversed evidence cannot certify a bill. Zero usable accepted quantity cannot be bypassed with tolerance.
- Prior validated allocations are complete and scoped. A credit references its original debit and the same order line/acceptance; cumulative credits cannot exceed original quantity/net/tax. Positive quantity restoration requires proportional net/tax credit under configured rounding, both per credit and cumulatively per original debit. Splitting midpoint credits cannot restart rounding to restore full quantity without the corresponding money/tax credit. A separately validated linked value/tax-only remainder may complete the commercial credit but never restores additional quantity by itself.
- Cumulative quantities cannot exceed order/accepted capacity beyond explicitly configured quantity tolerance. Certified service net value is a hard ceiling; commercial variance cannot invent service acceptance.
- Header net/tax/gross must equal configured line rounding and exact line sums. No arbitrary credit or mutable PO InvoicedQuantity counter is trusted.

## 12. Data Fields and Ownership

`InvoiceMatchDocument.From` projects existing invoice/lines with IDs, versions, scope, supplier, normalized identity, status, source mappings, exact amounts and coding. Issued/accepted projections retain stable IDs/version/publication references, category/UOM and terms; they are not source masters. Policy carries scope, approved version/reference, effective interval, money scale/rounding and field/category limits. Consumption retains validated debit/credit source IDs and exact quantities/net/tax. Evaluation retains UUID, actor, fingerprint, outcome/discrepancies, line comparisons, evidence JSON/hash. Future persistence should extend/reuse existing InvoiceMatchResult with dedicated immutable allocations/receipt evidence and composite constraints; no such schema is claimed here.

## 13. Screens and Data Views

Reuse the existing Bills list/detail, document panel and future match comparison/exception views. No UI is added or replaced. Existing intake line forms do not yet map PO line, receipt/service or verified UOM. Show comparison source/version and plain discrepancy descriptions only after a real API exists.

## 14. User Experience and Human Behaviour

Explain the field/line and remaining accepted quantity/value; identify the next owner. Never describe Compared as approved, posted or paid. Preserve original intake and show corrective mapping rather than silently editing invoices or receipts.

## 15. Notifications and Communications

No notification is emitted by this pure foundation. Future transactional persistence writes audit/outbox before any notification dispatch. No supplier or Finance communication is fabricated.

## 16. Documents and Attachments

Reuse existing Core document references and immutable invoice evidence. Future authoritative adapters must verify accessible/scanned original invoice, issued PO revision and acceptance certificates. A string reference alone is not proof of a trusted external source.

## 17. Exceptions and Edge Cases

Duplicate identity, wrong supplier/revision/UOM, missing acceptance, reversed/returned delivery, insufficient certified value, over-credit, incomplete history, stale policy, precision overflow, mismatched header, already allocated invoice, changed UUID replay and unsupported department/value-only/non-PO workflows block. An old-revision consumption allocation is not ignored: explicit amendment carryover mapping is required before comparison.

## 18. Integrations, APIs, Events and Sandbox

Current public application contract is typed immutable projections plus `InvoiceMatchCommand` and `IProcurementScope`; no HTTP/API or background job is mapped. Future adapters must verify actual issued/accepted truth inside a transaction and persist result/allocations/receipt/audit/outbox atomically. Stable command recovery must use its retained snapshot while current authorization is rechecked; changed original command/evidence conflicts. Finance AP contract is independent. Tests use synthetic pure evidence only; no real successful PO/receipt/Finance rows are created.

## 19. Audit, Security and Privacy

Canonical decimal strings, stable collection ordering, SHA-256 source fingerprint and evidence digest preserve comparison inputs/results. The company-wide duplicate set is retained as a digest/watermark in output evidence, not hidden invoice IDs. Full source/consumption evidence is internal restricted audit material; a future UI API must return scoped comparison DTOs, not raw internal evidence. Hashes provide integrity checks, not source authenticity or authorization. Future persistence must be append-only with narrow DB privileges and current API checks.

## 20. Reports, Dashboards and Metrics

No live metrics are claimed. Future first-pass match/exception metrics must distinguish Blocked comparisons, operational validations and actual AP outcomes; source version and reporting freshness must be explicit.

## 21. Configuration and Localization

Money scale (0–4), ToEven/AwayFromZero rounding and every tolerance combination are explicit approved policy input. There is no default monetary threshold/tolerance. Input decimals must be exactly numeric(18,4); multiplication uses scaled BigInteger intermediates with eight-place precision before configured rounding. No binary floating point, implicit FX or silent UOM conversion. Invoice normalization follows existing intake Trim/ToUpperInvariant contract.

## 22. Non-Functional Requirements

Deterministic/culture-independent comparison and canonical evidence. Invoice lines capped at 200; authoritative adapters must also bound source/history sizes and retain complete verified aggregates where volumes require them. Future operational persistence must serialize shared consumption, enforce version checks and prove two concurrent invoices cannot overbill one accepted capacity. Library tests do not prove network or DB transaction behavior.

## 23. Postconditions

Return immutable Compared/Blocked evidence and line comparisons. No invoice/PO/receipt/credit/Finance mutation, audit write, notification or payment. `invoiceValidation` and `financeHandoff` capabilities remain disabled.

## 24. Failure and Recovery

Missing authoritative evidence leaves the invoice received/unmatched. Exact original input plus UUID recovers original immutable evaluation; changed intent/source/policy/history or tampered response conflicts. Future lost-commit recovery requires a durable command receipt, current access revalidation and retained original snapshots; unknown outcomes must not create another allocation or AP posting.

## 25. Acceptance Criteria

Two/three-way partial comparison succeeds only with exact required evidence/rules. Over-consumption, invalid credit, wrong scope, incomplete history and duplicates block. Exact midpoint rounding is policy controlled; oversized product never silently rounds/overflows into a match. Source records remain unchanged and no foundation result posts to Finance.

## 26. Test Scenarios

Authored unit cases cover two/three-way partial goods/services, cumulative order/acceptance limits, returns/reversals, certified value, valid/invalid/orphan/over/amount-only credits, quantity/tax/total boundaries, missing/ambiguous/expired/currency/company policy, source tenant/entity/branch/supplier/revision/UOM denial, duplicate privacy, incomplete/history replay, exact rounding/max precision, canonical culture/scale and intake projection immutability. Tests are unexecuted at source submission. Required later: backend permissions, real PostgreSQL race/replay/rollback, actual mapped HTTP and existing UI browser verification.

## 27. Ownership and Dependencies

Procurement/AP governance approves rules; owning PO/Receiving services supply real evidence; Core supplies identity/scope/documents/audit infrastructure; Finance owns AP/credits/payments. Dependencies and migration/transport gates are listed in PROC-11A and issue #30. No shared identity/document/Finance infrastructure is duplicated.

## 28. Open Decisions and Assumptions

Business policy owners must approve category methods, money rounding/scale and field tolerances. Implement verified source/UOM mapping, amended-revision carryover and historical allocation contracts; define invoice correction/independent exception authority and AP payload/reconciliation. Resolve department/value-only/non-PO routes before supporting them. Keep issue open and capabilities off until those contracts, persistence and release evidence exist.
