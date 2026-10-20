# PROC-11A — Invoice matching foundation and integration gaps

**Status:** Source foundation, not an operational matching/posting workflow. **Issue:** [module-connect#30](https://github.com/georgemunganga/module-connect/issues/30). **28-section source contract:** [PROC-11B](PROC-11B-INVOICE-MATCHING-COMPARISON.md). **Parent:** [PROC-11](PROC-11-INVOICE-MATCH-AP-HANDOFF-AND-PAYMENT-VISIBILITY.md). No production activation, migration, browser or runtime test result is implied.

## Audited surfaces

| Surface | Existing behavior | Required integration gap |
|---|---|---|
| UI `reference-ui/src/features/LiveInvoices.tsx` | Bills list, structured intake, document panel; amounts use JS numbers | Retain UI; exact string transport and matching workbench binding follow a verified API contract. No fake match/payment action. |
| `ProcurementInvoiceIntakeRoutes` | Supplier/company/branch, dates, invoice amounts, immutable original lines; normalized duplicate index | Intake line transport omits PO line, goods receipt and service acceptance IDs. No source-line/UOM mapping or invoice revision workflow yet; a verified UOM projection is mandatory and never inferred from the PO. |
| SupplierInvoice/Line | Existing scoped header/line records and source reference columns | No validated immutable invoice snapshot, cumulative allocation version or match command receipt yet. |
| PO/Revision | Draft CRUD and source/revision fields | Require authoritative issued revision, permitted buying category/UOM and immutable line terms. Mutable current PO counters are not evidence. |
| GoodsReceipt/Line and ServiceAcceptance | Scoped models and accepted quantity/value fields | Require certified/accepted, unreversed evidence and exact issued-revision relationships; no fabricated accepted rows. Returns must reduce capacity. |
| InvoiceMatchResult | Append-only aggregate variances and policy ref | Needs immutable per-line comparison, policy/tolerance versions, source digest, command fingerprint and cumulative billing/credit allocation evidence before persistence. |
| SupplierCreditNoteRef | Commercial header and optional Finance ref | Header credit amount does not release invoice quantity. Need independently validated allocation to original billed line/evidence; Finance owns accounting/application. |
| Duplicate gate | Company-wide supplier + normalized invoice number + currency index; branch-safe conflict text | Include same identity in immutable evidence; reject duplicates from authoritative complete company projection without exposing hidden invoice details. |
| Precision | Generic commercial columns numeric(18,4) | Inputs must be exactly representable. Quantity × price has eight decimals; retain exact scaled-integer intermediate and explicit policy currency rounding. Never infer tolerance from rounding. |
| Module meta | `invoiceValidation` and `financeHandoff` false | Keep false until full source, policy, persistence/transaction, access and Finance gates pass. |

## Bounded supported source increment

A pure, deterministic Procurement evaluator consumes server-resolved projections of the existing records. It supports one issued PO revision per invoice, exact invoice-line mappings, category/currency-specific two-way or three-way policy, accepted goods quantity or certified service quantity/value, complete prior validated debit/linked-credit consumption and company-wide duplicate identities. It returns an immutable explanatory comparison/evidence digest and explicit blockers. An exact UUID command replay must recover identical evidence; a changed command/source/history/policy must conflict.

Evidence inputs are application contracts, not client authority. A successful pure comparison is **not** an approved invoice, AP posting, receipt, credit application or payment. No HTTP routes, authorization bypass, persistence schema, financial ledger, payment engine or UI is introduced in this increment.

## Exact next dependencies before operational matching

1. Issued PO/revision adapter verifies publication, current/explicit revision, supplier/currency/company/branch, active lines, category/UOM and immutable commercial terms.
2. Receiving adapter verifies accepted goods/service evidence against that revision, rejects reversed/rejected quantities, subtracts actual return/reversal effects, and exposes a consistent version/watermark.
3. Governed match policy supplies exactly one applicable rule per category/currency, effective period, approved version, explicit quantity/price/tax/total tolerance and monetary rounding. No hard-coded business tolerance.
4. Invoice correction/mapping contract and exact decimal JSON transport preserve original intake/evidence; Core documents remain source attachments.
5. Server-scoped match command transaction locks invoice plus PO/receipt consumption consistently, reloads complete validated allocations and duplicate identity, persists the immutable result/allocations/command receipt/audit/outbox atomically. Concurrency cannot over-consume the same accepted capacity.
6. Validated credits reference original charge allocations; over-credit, cross-source credit and credit-only header release fail. Finance independently confirms accounting credit application.
7. Permission/scope/independent exception approval, evidence immutability/composite constraints, real PostgreSQL races/replay/rollback, exact API contract and existing UI/browser proof.
8. Separate Finance AP acceptance/posting/status integration, idempotency and reconciliation. Matching never sets Posted/Paid.

This issue remains open after source implementation; dependencies and runtime acceptance are required for completion.
