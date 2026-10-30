# M7A scoped capture contracts — unregistered source slice

Status: implementation candidate for review, not compiled or activated. Based on `34516123`. The existing 28-section M7A child-feature specification remains the feature contract; this note records this source slice's ownership and schema decisions.

## Included

- Existing `SupplierBid` and `SupplierBidLine` remain the commercial records. A pure factory creates these in the owned published event's tenant/company; it does not copy employee, supplier, catalog or Core access masters.
- Proposed `SourcingManualContact` records a buyer's report of external contact. It pins event, invitation, supplier, branch, publication opening/decision, current event version, principal, optional denial-only Core worker alias, observation/reference and exact command acknowledgment. It sends nothing.
- Proposed `SourcingManualQuoteVersion` combines an immutable commercial snapshot and the complete actor-owned command receipt. It pins the existing `SupplierBid.Version`, contact, predecessor, exact snapshot, command digest, operation, response bytes/status/digest and recording time. It proves internal draft capture, never supplier submission/evaluation/award.
- Factory inputs reuse `PrincipalRfqPublicationReceiptResult.Matches` for the actual retained M4 receipt/opening/decision contract. This is not a substitute for the owning M4 source loader, approved-demand hold verification, current Core authority, current supplier eligibility, document access, locks or transaction-level consistency.
- Every factory supplies `TenantId` and `LegalEntityId` before `AddScoped`. The two unregistered sidecar types join the existing pinned-evidence scope guard, so a foreign context rejects them before any attempted model attachment instead of relabelling them.
- Exact recovery verifies the complete expected typed snapshot and producer reply, including actor/key/command binding. A hash-shaped digest, a `Manual` label or a `Draft` status is insufficient. Core READ/action/data-scope checks remain mandatory at the future endpoint; this pure helper does not provide authentication or authorization.

## Version and receipt decisions

Create captures existing bid version 1. Revise/Cancel receive a retained pre-edit bid/line projection and previous protocol version; the candidate still carries the locked original EF version. The factory computes the result version as original + 1; the future owning SaveChanges must increment the actual bid once in that same transaction. Line-only revisions must explicitly mark the existing bid header as Modified in the owning transaction, so its existing EF Version increments even when only lines changed. The pure factory neither marks an EF entry nor mutates/increments the domain row. No `DraftRevision` column or independent revision counter is introduced. Successful atomic bid+sidecar persistence is not claimed here.

Revise/Cancel recording time cannot precede the previous version recording time; the same instant is allowed. This does not impose a current-clock comparison on historical recovery.

Cancel requires a reason and preserves the prior commercial terms. Existing `SupplierBid` uniqueness on tenant/company/event/supplier/submission-version remains unchanged. **A cancelled quotation does not free that key.** Recreating a cancelled quotation requires an explicit policy and index/SubmissionVersion decision before routes are implemented; this slice does not claim recreation works.

The new protocol's sidecar is required to revise or recover a principal-captured quotation. Existing legacy Manual bids are not silently upgraded. Historical recovery must restore the exact historical typed version, rather than compare an old receipt with the latest mutable bid.

## Planned database controls — not applied

Before EF mapping/migration, review table names and complete constraints against the approved child feature:

- Contact uniqueness: tenant/company/actor/command-key; a separate invitation/contact cardinality decision before enabling repeat contact.
- Quotation command uniqueness: tenant/company/actor/command-key; version uniqueness tenant/company/bid/existing bid version.
- Restrictive composite tenant/company FKs to event, invitation, supplier, publication opening/decision, contact, bid and predecessor. Ensure predecessor belongs to the same bid; adjacent existing BidVersion is enforced by the factory and must be covered by transaction/native tests.
- Version 2, valid positive IDs/versions, canonical principal/optional alias, allowed observed channels/operations, complete response/snapshot, length limits, timestamp relationships and exact decimal constraints. Use text for exact response/snapshot bytes; do not normalize saved acknowledgment bytes through JSONB.
- Append-only sidecar enforcement, source/bid optimistic concurrency, shared command-key locking/cross-operation conflicts, durable audit/outbox classification and destructive Down retention vetoes.

No DbSet, mapping, migration, startup/readiness registration, route or feature flag is added. Existing model parity should remain unchanged. Read availability and original-key recovery must remain independent of stopping new work; new commands must require their separate current write/profile guards after original-key lookup.

## Evidence and remaining gates

Source tests cover the existing commercial factory → actual owning `AddScoped`/InMemory SaveChanges path, foreign sidecar scope denial, deliberately unmapped sidecars, complete receipt/snapshot tampering, workerless principal capture, old-key private recovery, retained version increments, cancellation and legacy-provenance denial. These tests are not PostgreSQL or positive sidecar persistence evidence, and have not yet been run for this slice.

Remaining gates: independent source review; bounded compiled tests; mapped model/genuine migration and snapshot; native restrictive-FK/concurrency/retention tests; Core-principal source loader and server permissions; actor-owned original-key-first replay before new-work guards; existing UI wiring; real-cookie Playwright happy/denied/recovery cases; independent full-owner reconciliation; release/rollback/operations evidence. No live workflow, supplier delivery, supplier bid submission, award, payment or module-completion claim follows from this source slice.
