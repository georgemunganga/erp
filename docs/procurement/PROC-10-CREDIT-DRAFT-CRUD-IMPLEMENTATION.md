# PROC-10 O-credit: invoice-linked manual credit reference Draft CRUD

**Status:** Source proposal for the release-only P19 + invoice-maintenance baseline. Not operational credit application or a Finance approval. Parent: [PROC-10](PROC-10-INVOICE-INTAKE-AND-SUPPLIER-ADJUSTMENTS.md); return authority remains [PROC-09](PROC-09-RECEIVING-INSPECTION-AND-RETURNS.md); overall delivery remains [live roadmap](PROC-ROADMAP-LIVE-BUILD.md). Existing capability matrix does not become complete through this slice.

## 1. Feature Identification

Procurement → Invoice intake and supplier adjustments → Invoice-linked credit reference → Capture/read/correct/cancel draft. Reuse SupplierCreditNoteRef; no new ledger or independent identity service.

## 2. Purpose and Business Outcome

Persist the supplier's credit document number, exact amount and reason against an existing scoped bill so AP can review it. A captured reference is neither posted nor applied to a Finance balance.

## 3. Scope and Exclusions

Manual invoice-linked Draft CRUD only. Exclude standalone/return-only credits, refunds, payments, posting, application, approval, portal submission and external delivery. No mutation of original invoice/documents or receipt/Inventory quantities.

## 4. Parent and Related Features

PROC-10 owns documentary intake; PROC-09 owns returns; PROC-11/Finance owns posting and application. Core owns actor, company/branch authorization and shared documents. Existing invoice release scope/exact-decimal helpers are reused.

## 5. Actors and Participants

Currently authorized invoice readers and capturers; draft creator or explicitly authorized Procurement manager/ERP admin may correct/cancel. Workerless read-only administrators receive no write exemption. No supplier portal identity is inferred.

## 6. Entry Points and Triggers

Existing vendor-credit screen selects a real visible bill; create/edit/cancel occur through the existing host's versioned Procurement API. Refreshing a page never writes.

## 7. Preconditions

Current tenant/company/branch scope; current invoice-read and invoice-capture for writes; visible bill with same derived supplier/currency; actual credit number; positive exact numeric(18,4) amount; reason1..1000. Draft source identity is fixed after create. No supplied FinanceCreditRef/status/vendor/currency/branch accepted.

## 8. Workflow

Read scoped bills → choose bill → capture Draft → read saved reference → correct Draft with CAS/change reason or cancel with CAS/reason. New writes lock the owning invoice and check registered credit ceiling atomically. Unknown save outcome requires explicit GET reconciliation, never automatic retry.

## 9. Status and State Transitions

New manual reference: Draft → Cancelled. Draft correction keeps Draft. Historical Received/other states remain readable if invoice-bound and scope-consistent, but have no actions. Any FinanceCreditRef freezes edit/cancel, regardless of status. No SentToFinance/Applied state minted.

## 10. Permissions and Data Scope

GET requires invoice-read; POST/PUT/cancel require both invoice-read and invoice-capture. SupplierInvoice visibility is the one shared invoice scope predicate, applied before counts/pages. Credit must match its invoice supplier and currency. Legacy invoice branch null is companywide/unselected only; no guessed branch. Mutation maker-or-manager check is in addition to current action permission.

## 11. Business Rules

New credit number is trimmed/uppercase canonical1..80; raw input retained in creation/correction audit. Reject case-insensitive same-company/supplier duplicate, including cancelled references. Positive amount and cumulative noncancelled registered amount must not exceed captured bill gross; this ceiling is not Finance outstanding balance. Unknown/inconsistent legacy credits (status outside Draft/Received/Cancelled, wrong supplier/currency, nonpositive amount, cancelled yet Finance-bound) block further writes for review. Stored amount precision is guaranteed by the existing PostgreSQL numeric(18,4) column; this slice does not infer pre-import precision or claim to detect historical rounding. HTTP inputs and exact aggregate arithmetic are checked independently. Exact scaled arithmetic rejects overflow/rounding. Invoice row lock + SERIALIZABLE transaction + CAS guard races; no limit splitting or negative credits.

## 12. Data Fields and Ownership

Existing SupplierCreditNoteRef fields only: Id/scope/metadata/version, SupplierId, SupplierInvoiceId, CreditNumber, Status, Amount, CurrencyCode, FinanceCreditRef, Reason; SupplierReturnId remains null for this slice. Derive supplier/currency/branch through the existing invoice. No schema change expected; legacy standalone rows remain untouched and unavailable through this invoice-linked API.

## 13. Screens and Data Views

Reuse existing vendor-credit list/detail/editor. List has safe bill/supplier labels, number/status/currency/exactAmount/version; detail has reason/current allowed actions. Picker uses existing invoice list/detail, not a duplicated vendor or Finance service.

## 14. User Experience and Human Behaviour

Feature controls require metadata capabilities.vendorCreditDraft plus creditCrudVersion1; absent metadata is false. Show Draft / Cancelled, explicit documentary-reference wording and captured-gross ceiling. Preserve entered strings on stale CAS; explicit reload discards edits. Failed/unknown writes do not silently re-POST. No fake credit balance or fake Finance handoff.

## 15. Notifications and Communications

Only durable existing audit records in this slice. No email/external Finance dispatch or delivered notification claim.

## 16. Documents and Attachments

CreditNumber records the supplier document number, not an uploaded/scanned document ID. No attachment/scan/download feature is invented. Future shared Core Documents linkage requires its own contract and authority gate.

## 17. Exceptions and Edge Cases

Hidden bill →404; missing action→403; stale version/locked/history conflict→409; invalid exact amount/reason or captured-gross ceiling excess→422; storage uncertainty→503. Duplicate response does not disclose hidden bill/credit identities. Archived supplier may still have documentary credit; no new purchasing authority results.

## 18. Integrations, APIs, Events and Sandbox

GET/POST `/api/procurement/v1/vendor-credits`; GET/PUT `/{id}`; POST `/{id}/cancel`. POST `{supplierInvoiceId,creditNumber,amount,reason}`; PUT `{version,draft:{creditNumber,amount,reason},reason}`; cancel `{version,reason}`. GET list supports page/pageSize/invoiceId plus search (up to160 characters over credit number, bill number and supplier display name) and optional status Draft/Cancelled, all applied before count/page. Responses expose creditCrudVersion1 and exactAmount string alongside legacy numeric amount; detail current allowedActions Edit/Cancel. Sandbox uses genuine scoped invoice capture and finite roles; no provider mocks in actual-PG proof.

## 19. Audit, Security and Privacy

Same transaction retains create raw number and exact before/after correction/cancellation plus business reason and actor. Do not physically delete. No other invoice allocations, financial private references or raw document bytes exposed. No new grants or role-implied financial authority.

## 20. Reports, Dashboards and Metrics

Persisted documentary reference counts/amounts can be displayed with scope. They cannot be reported as posted credit, reduced payable balance or refunded amount.

## 21. Configuration and Localization

Use bill currency unchanged, Core company/branch selection and existing permission configuration. No hardcoded thresholds or currency rounding policy. Numbers travel exact invariant strings; display localization is presentation only.

## 22. Non-Functional Requirements

Scope and SQL paging before projection; bounded100 page; shared owning invoice lock for cumulative ceiling; no network calls inside transaction. Existing source schema and finite privileges must be available; root owns actual migration compatibility and runtime gates.

## 23. Postconditions

One persisted Draft or Cancelled reference with stable source identity, exact monetary tuple, audit/version and truthful UI. Invoice amounts/status/documents, AP, Inventory and Finance outcomes remain unchanged.

## 24. Failure and Recovery

Transaction rolls back reference and audit together. Serialization/deadlock/stale→409 refresh; unexpected storage failure→503 unconfirmed. No idempotency key or blind exact replay guarantee added: GET explicit review before another intentional write. Duplicate number remains reserved after cancellation.

## 25. Acceptance Criteria

Genuine invoice capture→credit Draft→edit→cancel persists/reloads exactly, preserves audit and bill identity; current permissions/scope prevent cross-company/branch/maker writes; non-Draft/Finance-bound freezes; cumulative concurrent credit cap cannot exceed captured gross; no posting/handoff/outbox invented.

## 26. Test Scenarios

Numeric/string18,4 boundary and >4dp/raw-underflow rejection; sums/overflow; duplicate mixed-case/trim; two concurrent creates and create-vs-edit/cancel; stale CAS; hidden bill/unknown legacy branch; denied actor; historical Received/Finance-bound; inconsistent legacy registered history; unknown transport response GET review; no original bill or Finance/Inventory side effects. Mapped InMemory proof is not PostgreSQL isolation proof.

## 27. Ownership and Dependencies

AP intake owns documentary capture, Procurement owns this draft, Finance owns application/posting. Root owns release migration/model/real-PG gates. UI consumes exact backend capability/version, reuses existing layout. This is independent of held P20–P31 governance ancestry.

## 28. Open Decisions and Assumptions

Bounded technical choice: invoice-linked only, current invoice read/capture actions, canonical uppercase newly stored credit number with raw audit, captured-gross conservative ceiling and no external effects. Broader credit approval/application/tax/return linkage/document upload remain explicitly unavailable until owner contracts and acceptance evidence exist. No business approval is claimed by this source proposal.
