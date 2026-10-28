# PROC-10 Invoice Intake and Supplier Adjustments

**Status:** Draft for business review · **Version:** 0.1 · **Hierarchy:** Procurement → Supplier Invoices and Procure-to-Pay → Invoice Intake and Commercial Adjustments → Operations · **Framework:** [ERP 28-section standard](../hrm/feature-specifications/ERP_Feature_Specification_Framework_Enterprise_Integration_Updated.docx) · **Module:** [PROC-00](PROC-00-PROCUREMENT-MODULE-ARCHITECTURE-AND-CAPABILITY-MAP.md)

**Implementation boundary (20 October 2026):** The first live slice records a structured manual invoice for a scoped company as `Received`, with supplier, date, currency, lines, calculated totals and a case-insensitive trimmed invoice-number duplicate key. It supports an optional PO reference belonging to the same supplier. AP processors can capture; scoped invoice readers can inspect. The shared Procurement audit records creation. The UI has list, empty, capture and detail states. Source-document upload, tax verification, correction/versioning, credit notes, matching, AP posting and payment status remain pending. A `Received` invoice cannot be handed to Finance by this slice.

## 1. Feature Identification

`PROC-10`, procure-to-pay phase. Business owner: AP intake lead with Procurement commercial owner; data/technical/product owners to name. Operations: `O1` receive/import, `O2` identify/validate, `O3` detect duplicate, `O4` correct/version, `O5` record credit/refund commercial reason, `O6` route to match.

## 2. Purpose and Business Outcome

Bring supplier invoices and commercial credit evidence into a controlled queue regardless of channel, without losing the original document or creating duplicate liability. Success is one identifiable, versioned invoice ready for matching or a named intake exception.

## 3. Scope and Exclusions

Includes manual entry, controlled inbox/document/API and later portal channel; supplier/entity identity, invoice fields, evidence, duplicate detection, corrections and commercial credit/return references. OCR may prefill later but cannot silently certify values. PROC-11 matches and hands off; Finance owns AP credit posting, refunds and payments.

## 4. Parent and Related Features

Parent Supplier Invoices and Procure-to-Pay; uses PROC-01 supplier, PROC-08 PO, PROC-09 receipt/service, PROC-07 contract where relevant. PROC-11 consumes validated intake; supplier portal can submit later.

## 5. Actors and Participants

AP intake processor, buyer, supplier contact/portal user, supplier steward, Finance tax/accounting reviewer, document/OCR service, exception owner and auditor.

## 6. Entry Points and Triggers

Email inbox, document upload, manual entry, API or supplier portal (when enabled), credit note, supplier correction, delivery return and duplicate alert.

## 7. Preconditions

Authenticated/scoped channel, intact scanned file or accepted structured payload, identifiable supplier/entity, invoice identity/date/currency/totals and evidence. Missing PO is a policy exception, not silent matching bypass.

## 8. Workflow

`O1`: capture original bytes and source receipt time, assign intake ID. `O2`: identify supplier/entity, enter/confirm fields, totals and tax references; OCR suggestions require validation. `O3`: check normalized duplicate key and related documents; hold suspect for human review. `O4`: return for correction or create linked new version while preserving original. `O5`: record commercial reason and original transaction for credit/refund due; Finance later posts/applies. `O6`: send validated snapshot to PROC-11 matching.

## 9. Status and State Transitions

Received → Identification → Validation → Ready for Match; branches Missing Information, Duplicate Review, Rejected, Corrected, Voided. Credit: Proposed → Verified → Sent for Finance Processing → Applied/Refunded status from Finance. Actor, evidence, SLA, escalation and immutable prior version accompany each transition; Paid is not an intake state.

## 10. Permissions and Data Scope

AP processor sees assigned entity; supplier portal user sees own submissions only; buyer sees commercial references but not restricted accounting/bank fields. Correction/void/duplicate disposition need scoped authority. Supplier cannot edit after submission except by new version.

**Selected-branch intake boundary (28 October 2026):** Company All is a read scope. Capture, correction and cancellation require an actual nonempty selected permitted branch and reject both the raw All header and resolved All scope with a clear 422 before accessing invoice storage. A middleware default branch must not silently authorize a raw-All write. New intake does not infer branch selection from an optional PO. Existing invoice reads and retained null-branch data are preserved; editing/cancellation never reassign a retained invoice branch. A selected branch still cannot expose a different or unknown retained branch. Company-wide master data scope is unchanged.

## 11. Business Rules

`BR-PROC-INT-001` preserve original source document; `002` duplicate normalized supplier/invoice/entity key blocks second handoff pending disposition; `003` supplier/entity/currency/totals required; `004` OCR is untrusted input until confirmed; `005` correction links prior version; `006` credit reason references original invoice/receipt/PO; `007` Finance owns posted credit and refund status.

## 12. Data Fields and Ownership

Procurement owns intake ID/source/time, supplier/entity refs, invoice number/date/currency, line/tax/total snapshot, PO/receipt/contract refs, file checksum, version, duplicate result and credit commercial reason. Finance owns tax determination, AP bill/credit account, refund and payment. Contact and account details are restricted.

## 13. Screens and Data Views

Invoice inbox by source/status, capture/confirm form, duplicate comparison, document preview, correction history, credit/return link, owner queue and handoff-to-match status. Scope applies to search and export.

## 14. User Experience and Human Behaviour

Show received versus validated versus matched clearly. Let staff compare duplicate candidates without exposing other tenants. Highlight uncertain OCR fields and total mismatch; never label a received document as approved for payment.

## 15. Notifications and Communications

Notify AP on new intake, supplier on missing/corrected document through approved channel, owner on duplicate/aging, buyer on credit from return, operator on failed document/OCR delivery. External messages omit internal coding.

## 16. Documents and Attachments

Original invoice, corrected invoice, credit note, delivery/return proof and correspondence are scanned, checksummed, versioned, classified and retained. Match and Finance handoff refer to exact evidence version.

## 17. Exceptions and Edge Cases

Same invoice number with punctuation/case variant, supplier merger, invoice before PO/receipt, partial invoice, multi-currency document, negative credit, wrong entity, unreadable file, inbox duplicate, OCR error and supplier suspension after valid delivery.

## 18. Integrations, APIs, Events and Sandbox

Document/inbox/API/portal adapters feed normalized intake; supplier and PO/receipt refs resolve through contracts. Emit `SupplierInvoiceReceived` and intake validated/credit-requested facts after commit with tenant/entity, invoice version, source, correlation/idempotency. Sandbox tests duplicate channels and OCR uncertainty; no real payment data.

## 19. Audit, Security and Privacy

Append source, original checksum, field confirmation/correction, duplicate disposition and credit reason. Restrict financial documents by entity/supplier, log download/export and mask sensitive payment data. No ordinary user deletes submitted invoice.

## 20. Reports, Dashboards and Metrics

Intake volume/channel, identification time, duplicate holds/prevented handoffs, incomplete documents, correction rate, credit due and aging. Define denominator and confirm duplicate count methodology.

## 21. Configuration and Localization

Allowed channels, required fields, invoice-number normalization, tax-reference validation, OCR confidence threshold if enabled, document formats, locale/currency and SLA are effective-dated by entity/country.

## 22. Non-Functional Requirements

File ingestion is malware scanned and idempotent across retries; large files and email bursts queue safely. Search/preview enforce scope; document availability and recovery targets await volume. Accessible confirmation UI.

## 23. Postconditions

Ready invoice has original evidence, validated identity/totals, duplicate disposition and stable version for matching. Credit reason links original transaction; Finance posting/refund remains external.

## 24. Failure and Recovery

Failed file scan leaves Quarantined/Invalid, not Ready. Unknown inbox/API outcome reconciles by checksum/source key. Duplicate callback does not create second intake; failed OCR leaves manual route. Conflicting credit status is reviewed.

## 25. Acceptance Criteria

Given invoice through email and portal with same identity, one duplicate review blocks second payable. Given OCR mismatch, human confirmation is required. Given corrected invoice, original remains. Given credit, original invoice/return link and commercial reason are mandatory.

## 26. Test Scenarios

Test manual/email/API/portal (when enabled), normalization, same number different supplier/entity, damaged file, OCR uncertainty, partial/negative credit, supplier suspension, unauthorized document access, duplicate replay and upload outage.

## 27. Ownership and Dependencies

AP intake lead owns capture, Procurement owns commercial evidence, Finance owns posted bill/credit/refund. Depends on supplier, PO, receipt, documents and identity; PROC-11 consumes output.

## 28. Open Decisions and Assumptions

Confirm invoice system of record, intake channels at launch, duplicate-key scope, tax validation owner, OCR timing, supplier portal timing, credit/refund workflow, retention and service targets. Owners: AP, Procurement, Finance and Security; resolve before implementation.

### Release-only manual intake maintenance (P19-compatible additive slice)

This bounded implementation corrects or cancels **Manual / Received** intake before any source document, mapped receipt/service/PO line, matching result/exception, credit reference, Finance handoff/payment/posting reference exists. It does not modify an original uploaded invoice or establish AP approval, posting, payment, matching or external Finance authority. Original capture metadata remains; correction audits retain the complete prior header/lines and reason. Removed draft lines are retained as inactive, never physically deleted; cancellation preserves amounts and lines.

`PUT /api/procurement/v1/invoices/{id}` accepts `{version,draft,reason}` with the existing intake draft shape. `POST /{id}/cancel` accepts `{version,reason}`. Both require current invoice-read plus invoice-capture permission, original creating principal or an explicitly permitted ERP administrator/Procurement manager, current company/branch visibility, current CAS version and a nonempty reason of at most 1,000 characters. A successful correction returns Received; cancellation returns Cancelled. Stale or processed inputs return 409, invisible records 404, unauthorized actions 403, invalid exact inputs 422. An uncertain storage response is 503: refresh/reconcile via GET before another intentional write; no automatic POST/PUT retry or idempotency guarantee is introduced.

List/detail expose `intakeCrudVersion:1`; detail `allowedActions` contains only currently applicable `Edit`/`Cancel`. Existing numeric outputs remain compatible, while detail header `exactAmounts.{netAmount,taxAmount,grossAmount}` and every active line's `exactAmounts.{quantity,unitPrice,taxAmount}` are invariant decimal strings. Numeric and string inputs are parsed without floating point or pre-rounding, constrained to actual numeric(18,4), and exact line products/header sums must fit unchanged storage. Missing new-intake currency uses the selected Core company's currency. Existing currency remains stable on omitted edit currency.

The release migration adds only nullable `SupplierInvoice.BranchRef` and default-true `SupplierInvoiceLine.IsActive`. Branch is observed from the authorized selected Core branch (or same-company linked order); edits do not relabel it. Legacy null branch is not guessed/backfilled and is visible only to unconfined company-wide callers without a branch selection. New UI mutation controls require the version marker and exact aliases. The independently held P20–P31 governed branch also adds BranchRef: that future migration ancestry must be reconciled explicitly, never imported to release this CRUD slice.

Authored mapped HTTP coverage: exact strings/large boundary, add/update/retire/reload/cancel and audit preservation, stale CAS, immutable downstream/source evidence, nonrepresentable precision/products, duplicate identity, reason bounds, currency default, observed branch retention, foreign company/other maker/permission denial, and unknown-branch confinement. InMemory tests prove mapped behavior, not PostgreSQL isolation/DDL/production readiness. Root owns additive migration review, real PostgreSQL and browser gates; no execution results are claimed here.
