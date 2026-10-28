# M6C Host: explicit fileless manual unit attestation

## 01. Purpose

Enable explicitly governed review of manually entered invoice units without uploading a file; do not claim a document was checked.

## 02. Ownership

Procurement owns invoice/unit decisions. Core owns identity, scope, policy publication evidence, approval cases/stages/assignments/decisions, audit and optional documents.

## 03. Tier and risk

Tier3: tenant/entity financial evidence, independent reviewer authority, immutable originals, exact numeric values and recoverable commands.

## 04. Baseline

Implementation starts from accepted B source7f44d5f37be73579b7f5daa63c8e63402869986f. This specification is source-only; no DB, live policy, runtime or production activation.

## 05. Actors

Workerless Core principals may request, review or consume units through existing permissions. Maker and named reviewer remain independent including any genuine worker aliases.

## 06. Default

Existing schema1 DocumentReview and original document endpoints stay the default. New ManualAttestationEnabled is false unless explicitly configured.

## 07. Policy

Only current Published INVOICE_UNIT_REVIEW schema2 with explicit evidenceMode ManualAttestation permits manual Open; same independently published operational identity chain. No policy seed or default mode fallback.

## 08. Entry points

Add explicit manual readiness/Open and manual task/detail/decision/recovery endpoints. Separate purpose makes document-free semantics visible and preserves original document request codecs.

## 09. Original invoice

Use current structured SupplierInvoice and all active original lines, digest, version and current mapping pointer. Manual invoice intake/payments already remain fileless and unchanged.

## 10. Mapping

First manual path requires existing source-schema3 principal mapping with no document pins on every line. Exact line IDs and entered unit strings must match the current mapping; no guessed conversion.

## 11. Issued owner

Reconstruct all issued lines through the authoritative retained principal-issued/Core helper in the same actual Serializable transaction; no legacy issued aliases or accepted-quantity shortcuts.

## 12. Source

Reuse append-only InvoiceUnitReviewSource plus exact sourceSchemaVersion4/ManualLines/ManualReason. Source identity schema3 remains distinct from terminal evidence schema4.

## 13. Core workflow

Use the existing Core approval engine and shared completion verifier. No new quorum/assignment/decision engine or synthetic Approved status.

## 14. Open

Require read/request permissions, real selected branch, current effective matching/manual review policies, independent named current reviewers, canonical maker reason and schema2 keyed request.

## 15. Decision

Require current real assignment, expected invoice/case/assignment versions, independent reviewer, canonical reason and ManuallyEnteredUnitsConfirmed for Approve. Reuse actual Core ApplyNewDecision/Seal.

## 16. Scope

Current tenant/company/branch/actor checks run server-side; explicit or raw All never authorizes writes. Historical principal/worker aliases cannot widen current access.

## 17. Reads

Manual task/detail/original-key recovery read retained source4 after manual writes are disabled, subject to current purpose permission and scope. Document-only reads retain real Core Documents owner checks.

## 18. Consumption

Manual source4 must dispatch to InvoiceManualUnitReviewEvidence through typed retained source/Core authority; legacy document verifier stays schema2/3. No caller verified boolean or fake document hashes.

## 19. Capacity

Verified units are not a complete owner/capacity claim. Compare still proves approved baseline, full current inventory, typed receiving facts, exact supplier/branch/currency/unit and hard ceilings; no Finance prerequisite is invented.

## 20. Receipts

Manual requests use schema2; responses retain existing response schema1 plus typed source/evidence discriminators. Actor+operation+key recovery preserves historical bytes; changed same-key body conflicts.

## 21. Events and audit

Explicit ManualUnitReviewOpened/ManualUnitReviewDecision and versioned invoice.manual_units_reviewed event, in the same transaction. No original-document audit claim for manual evidence.

## 22. Physical schema

Selectively adopt held model CHECK outcome evidence schema IN(3,4). Root generates the genuine EF migration/snapshot/export. No handwritten migration or new tables/columns anticipated.

## 23. Readiness

Retain finite same-database/role/no-owner/Core fences. Add applied-C conditional exact native/restored CHECK admission, leaving older schema3 captures strict; manual mode never requires Core Documents tables.

## 24. Downgrade

First Down must veto any retained source4/request2/outcome4/manual policy2/Core descendant/audit/outbox marker before CHECK restoration. No data deletion/counter reset.

## 25. UI contract

Future UI presents entered lines, maker/reviewer reasons and manual attestation wording, no empty document rows. Existing document UI remains separate. UI implementation/acceptance is not supplied by this Host slice.

## 26. Tests

Pure/manual authority and legacy byte tests; Host gate/scope/unknown strategy/flagoff recovery tests; reject fake docs, same actor, stale versions, wrong scope, missing reason/attestation and unknown schemas.

## 27. Native acceptance

Require genuine empty Up/Down/Up, retained rollback-only veto and full old-row/schema/ACL preservation, then real-cookie manual Open/independent Decide/recovery/Compare plus owner reconciliation. No acceptance claim before execution.

## 28. Release boundary

Source only and default off. No production migration/deploy/flag change, invoice approval, supplier payment, OCR/document scan, AP posting or full UAT sign-off. Core Documents remains the independent real owner for document review.

