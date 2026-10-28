# M6C: policy-controlled fileless invoice unit attestation

This is a child feature contract. It does not activate a policy, change production
behavior, approve an invoice, create a billing claim, or post to Finance.

## 01. Purpose

An independent reviewer may attest manually entered invoice line units when no
original file is retained. The assertion is a human attestation, not document
verification.

## 02. Existing ownership

`SupplierInvoice` and its original lines remain the document-of-record values.
`InvoiceUnitReviewSource`, Core case, outcome and command receipts retain the
review. No second invoice master or approval engine is introduced.

## 03. Policy identity

The existing `INVOICE_UNIT_REVIEW` policy key remains. A published policy with
`schemaVersion:1` retains its document-only meaning.

## 04. Policy version 2

The only new rules shape is `{"schemaVersion":2,"transactionType":"InvoiceUnitReview","evidenceMode":"ManualAttestation","stages":[...]}`.
Its stages use the existing named One/All independent principals and Core grants.
No missing mode or unknown field defaults to manual attestation.

## 05. Policy activation

Validation makes version 2 publishable as configuration. Runtime manual review
requires a separately enabled Host capability and an actually effective,
published version 2 policy. No policy is installed by migration or startup.

## 06. Document policy boundary

Version 1 continues to require actual available Core document versions, retained
use pins and exact hashes. Its JSON, source and evidence bytes are unchanged.

## 07. Manual source discriminator

New source text has `sourceSchemaVersion:4`. Omission retains the v3 document
source interpretation. The persisted source row schema and existing source key
remain unchanged.

## 08. Manual source line set

Each original active invoice line appears once with its line ID and confirmed
unit. The set equals the stored invoice's original line IDs and the current
mapping's line IDs. A unit is nonblank, bounded and exactly the mapping unit.

## 09. Original value pin

The source pins the original invoice digest and mapping ID, revision and digest.
Later monetary, line-set, mapping-pointer or unit changes fail verification.

## 10. No document claim

Schema 4 has no document version ID, document hash, document line reference,
document availability or use pin. Neither a zero GUID nor a fabricated hash is
stored as a substitute.

## 11. Maker reason

Open requires a normalized, nonblank bounded reason stating why manual
attestation is used. The reason is retained in immutable source text and the
original command receipt.

## 12. Reviewer reason

An Approve decision requires its own normalized, nonblank bounded reason. It
is retained in the actual Core decision and schema 4 terminal evidence.

## 13. Separation of duties

The maker, named reviewer and any optional genuine worker aliases satisfy the
existing principal independence checks. Worker linkage is not required.

## 14. Branch and company

The same tenant, legal entity, selected branch and current Core principal
permissions govern Open, Decide and readback. A company-wide grant does not
make a cross-company or wrong-branch source valid.

## 15. Core execution

Core creates the case, stages, assignments and decisions. The terminal verifier
reuses one common Core completion proof for document and manual evidence; no
parallel decision/quorum calculation is added.

## 16. Decision states

Only terminal Approved produces Verified units. Return and Reject retain a
nonverification outcome and cannot supply units to matching.

## 17. Manual evidence version

Terminal schema 4 records the evidence mode, source digest, original/mapping
pins, Core case and final decision, maker/reviewer identity, reasons, decision
time and exact line/unit set. It contains no document fields.

## 18. Legacy evidence dispatch

Evidence schema 2/3 continues through the retained document verifier. Schema 4
uses the manual verifier. Unknown schemas fail closed. Existing JSON is never
deserialized into a broader object and reserialized.

## 19. Current consumer

The comparison actor still needs current invoice-match permission, scope and
the exact current mapping. A historical reviewer signature alone does not
authorize a new comparison.

## 20. Matching scope

Confirmed units are input facts for unit comparison only. Accepted receiving
facts, owner capacity history, invoices' monetary originals and Finance
eligibility remain separately verified.

## 21. Receipt recovery

Original-key Open and Decide receipts retain request fingerprint and original
response bytes. Actor, scope, operation and key checks remain. A changed body
with a reused key is a conflict, not a new attestation.

## 22. Physical constraint

The existing outcome CHECK may add evidence schema 4 while retaining states and
all other predicates. This needs a genuine separate M6C EF migration and
snapshot, not a handwritten change to the frozen M6B scaffold.

## 23. Append-only guard

Source, assignment, outcome and receipt rows remain append-only. The manual
source has no document identifiers and its owning path creates no Core document
use pins or Core Docs dependency. The document source retains its pin checks.

## 24. Rollback

M6C Down must veto any retained schema 4 source/outcome/receipt, policy version
2, associated Core case, audit or outbox fact before restoring the old CHECK.
An empty new feature may be rolled back without touching legacy rows.

## 25. Negative cases

Reject missing reasons, absent/duplicate lines, changed unit/original/mapping,
document fields in manual evidence, fake case/decision, same maker/reviewer,
wrong company/branch, stale versions, expired/superseded policy and key reuse.

## 26. Byte compatibility

Acceptance must compare legacy v1/v2/v3 source/evidence/receipt bytes and
hashes before and after this change. The new optional source properties are
omitted from legacy serialization; no global null-ignore setting is applied.
That byte comparison has not yet run for this source slice.

## 27. Native acceptance

A private clone must prove genuine M6C Up/Down/Up, retained-data 55000 Down
veto, current finite roles, real-cookie manual Open/Decide/recovery and owner
readback. No production policy is activated by this source slice.

## 28. Release boundary

Manual attestation is explicitly labeled in API and UI. It does not claim a
PDF was reviewed, does not bypass Core Docs for schema 1, and does not create
an invoice approval, supplier payment or Finance posting.
