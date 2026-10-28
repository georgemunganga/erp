# PROC-09 / M6 — Principal invoice matching against issued orders and accepted receiving facts

## 1. Feature Identification

M6 records a scoped, versioned invoice mapping, a deterministic two-way or three-way comparison, and a qualified Procurement billing-capacity claim. It extends the existing invoice intake; it does not create another invoice master.

## 2. Purpose & Business Outcome

An invoice reviewer can explain which issued order lines and, where required, accepted receiving facts support each invoice line. A successful comparison may reserve only the supported quantity and value for that invoice. A blocked comparison retains its reasons without reserving capacity.

## 3. Scope & Exclusions

This child feature covers mapping, original document/unit review, comparison, exceptions, historical exposure reconciliation, and qualified claims. It creates no payable, payment, tax filing, inventory movement, stock return, or Finance posting. M5d goods-return drafts are proposals and do not reduce accepted quantity.

## 4. Parent & Related Features

The invoice intake owns `SupplierInvoice` and original lines. M5a owns the issued order revision and its Core approval/funding lineage. M5b/c own immutable goods/service acceptance facts and company-date evidence. M5d owns return proposals only. Existing invoice matching v1 rows and response hashes remain readable byte-for-byte.

## 5. Actors & Participants

Invoice capturer proposes mappings; an independent unit reviewer verifies original document units; an invoice matcher performs comparison. All are current Core principals, with optional worker aliases. A Procurement historical exposure owner supplies a complete authenticated checkpoint from its persisted invoice/claim history; it cannot be impersonated by client JSON or inferred from an empty local table. Finance remains the future posting owner.

## 6. Entry Points & Triggers

Existing `/api/procurement/v1/invoices/{id}/matching` routes gain an explicit principal-source version. Source and acceptance selectors are read-only previews. Mapping and compare POSTs require `Idempotency-Key`; original-key GET recovers a committed response.

## 7. Preconditions

The invoice is in a matchable intake state, has immutable original lines, and is unposted. The target PO is actually Issued with a current sealed revision for the same tenant, company, branch, supplier and currency. The active matching policy is published and current. Physical schema and finite role grants are verified before writes.

## 8. Workflow

Capture an exact mapping for every active invoice line to one issued order line. A three-way line also identifies one accepted M5 receiving fact; a two-way line has no acceptance link. Review original invoice documents and units independently. Reload all source, policy, owner and duplicate evidence under the comparison transaction, evaluate, then atomically persist result, exceptions, claim rows when qualified, receipt, audit and outbox.

## 9. Status / State Transitions

Mapping proposal increments the invoice mapping revision and leaves `MappingUnverified`. Comparison records `ComparisonBlocked` or `ComparisonPassed`. Only a qualified pass sets the active claim pointer and increments the exposure sequence. A blocked result creates no claim. Existing v1 result and claim states are historical and are not rewritten.

## 10. Permissions & Data Scope

`procurement-invoice-capture`, `procurement-invoice-read`, and `procurement-invoice-match` are explicit Core grants. Every route checks current principal, tenant, company, branch and selected versus All context; All is read-only. Recovery checks current scope and original actor/operation/key without reauthorizing a historical business decision.

## 11. Business Rules

Two-way compares invoice quantity, unit price, tax and total against issued order lines. Three-way additionally requires a positive accepted M5 fact for the same issued line and kind, with accepted quantity/value ceilings after effective approved returns. Cumulative claims across invoices cannot exceed order or accepted capacity except configured commercial tolerances; tolerances never create physical acceptance. Duplicate supplier invoice identity blocks qualification.

## 12. Data Fields & Ownership

The new mapping variant carries `principalReceivingFactId` and `principalReceivingSourceStateId` as typed, nullable links, mutually exclusive with legacy goods/service IDs. The mapping row stores an explicit nullable source schema discriminator: null retains the original v1/v2 payload path; 3 selects the principal payload. This is separate from Core identity schema 2. Unit review dispatches by this stored source discriminator and checks original document links; it never converts a principal fact into a legacy goods/service acceptance. Immutable comparison and allocation rows retain source schema version, issued version, policy digest, original invoice digest, acceptance fact digest/sequence, owner watermark and command key. Procurement owns claims; Finance/AP owns external posting.

## 13. Screens & Data Views

Read views show issued line, match method, accepted fact, available quantity as a qualified claim only when owner evidence is current, comparison discrepancy codes and recorded command receipts. Preview selectors label historical physical facts separately from reservable capacity.

## 14. User Experience / Human Behaviour

The capturer sees exact mapping and source blockers before submission. The reviewer sees original document/unit evidence. The matcher receives a clear blocked reason and can refresh sources; no UI claims that comparison approves payment.

## 15. Notifications & Communications

Comparison emits a Procurement outbox event indicating result and `canPostToFinance=false`. It sends no supplier message and triggers no Finance posting.

## 16. Documents & Attachments

Original invoice document versions and SHA-256 hashes are verified through the current Core document owner. Missing, retired, unavailable or changed document evidence blocks unit review; a client-supplied hash is not authority.

## 17. Exceptions & Edge Cases

Block for stale invoice/mapping/issued revision, missing principal fact, changed acceptance payload/sequence, wrong kind or scope, incomplete return history, duplicate invoice identity, expired policy, incomplete owner history, overlapping owner/local claims, stale exposure sequence and any source over-capacity. Do not substitute a legacy acceptance ID for a principal fact ID.

## 18. Integrations / APIs / Events / Sandbox

The source adapter reads the retained M5a issued graph and M5b/c receiving graph in the same execution database transaction. A Procurement-owned exposure reader runs in that transaction, from an explicit reviewed baseline plus all later invoice, claim, credit/handoff and manual-payment records. It returns a lease-bound complete-set envelope with an exact row-set digest and sequence; no network call occurs while locks are held. Private acceptance uses finite Core/Record/Execution roles, normal cookies and a restored nonproduction database.

## 19. Audit / Security / Privacy

Record principal and optional worker alias, mapping review, policy, source digests and result in append-only evidence. Do not log invoice text, supplier identity, document content or raw database errors. Unknown owner or source evidence fails closed. Core role grants never inherit legacy administrator permission implicitly.

## 20. Reports / Dashboards / Metrics

Expose counts of proposed, blocked and qualified comparisons and discrepancy codes from recorded evidence. Avoid presenting Procurement capacity claims as payable or paid amounts.

## 21. Configuration & Localization

The current `INVOICE_MATCH` policy selects two-way or three-way by category and currency with explicit field tolerances, rounding and effective window. Configuration and publication use existing independent Core review. Units and decimal scales retain exact canonical representation; currency is never converted implicitly.

## 22. Non-Functional Requirements

All graph reads are bounded, deterministic and transactionally consistent. Comparison and claim insertion are serializable with a stable lock order. Replays return exact recorded bytes; mismatched key/body conflicts. The feature is default-off until physical, owner, policy and role readiness pass.

## 23. Postconditions

A mapping has one immutable version and typed source links per active invoice line. A blocked comparison has result, exception/audit/receipt evidence and zero new claims. A qualified comparison has one active result pointer, claims matching every compared line, one incremented exposure sequence and owner-backed coverage evidence.

## 24. Failure & Recovery

Lost responses recover by original key. A DB rollback leaves no result, claim, receipt or outbox. Owner outage or expiry records a blocked result only where complete safe diagnostics can be retained; it never creates a claim. Earlier v1 results remain readable after rollout or feature disablement.

## 25. Acceptance Criteria

Normal-cookie tests prove principal issued/accepted source selection, independent original-unit review, two-way and three-way outcomes, blocked and qualified claim graphs, complete owner-history admission, exact original-key recovery, actor/scope denials, stale and over-capacity denials, and no Finance posting. Read-only owner reconciliation verifies old invoice rows and hashes unchanged.

## 26. Test Scenarios

Exercise workerless Core actors, wrong tenant/company/branch, All-write denial, duplicate invoice, changed source digest, stale policy, partial/expired owner history, owner/local overlap, concurrent invoices on one PO line, service accepted-value ceiling, changed accepted quantity, M5d proposal without approved return effect, replay, receipt tamper and disabled-write historical reads. Native migration Up/Down vetoes retained M6 records while retaining v1 rows.

## 27. Ownership / Dependencies

Procurement Domain/Application own typed mapping, pure comparison and claim validation. Procurement Infrastructure owns new FK/check/index constraints and retained v1 compatibility. Host routes own current Core authorization and transactional source loading. A separate Procurement owner component owns the complete historical exposure baseline and current-set reconciliation; it is required before qualified claims are enabled. Finance posting remains a separate future integration.

## 28. Open Decisions / Assumptions

The owner baseline must classify every retained v1/v2 matching allocation and supplier invoice linked to an issued PO, every credit reference, Finance handoff/status reference and manual-payment record, including their active pointers and immutable IDs. An unknown or contradictory row blocks the baseline; a zero-row result without a captured, approved baseline is not completeness. The current default adapter returns unavailable. A credit text reference or M5d return draft is not an approved capacity-release effect. Finance posting may be added later without changing this Procurement claim owner.
