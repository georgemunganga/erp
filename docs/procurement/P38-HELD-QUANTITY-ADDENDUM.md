# P38 held-quantity source addendum

Applies to sections 7, 11, 14, 24–28 of `PROC-06-P38-PRINCIPAL-SUPPLIER-AWARD.md`. This candidate is not wired into an endpoint, EF context, database query, permission, or readiness provider. No award or PO is created.

## Original hold and claim set

The owning adapter must, under the existing serializable Procurement/Core transaction, lock sorted P29 request lines, the sourcing event, the selected sourcing lines, and the P36 current cursor before reading capacity. It must load and verify the original `SourcingDemandBinding` and its one `PurchaseRequestAllocation` with `TargetType=SourcingEvent`, `Status=Reserved`, and exact `SourcingLine.Quantity`. `PurchaseDemandAllocationBalance.AvailableQuantity` is net of the RFQ hold and cannot be used as the P38 cap.

The complete claim query is scoped by tenant, legal entity, sourcing event and sourcing line across every commercial round. It must include all lines for every matching review submission, its frozen P36 revision, original receipt, actual Core case, policy and all decisions, plus any owned terminal outcome. The pure function cannot prove that a caller supplied all matching rows or took locks.

Pending reviews and `CoreApprovedPendingAward` consume exact `QuantityExact` per source line. A terminal Core Return or Reject without an owned outcome still consumes. Release requires the stored outcome to match `PrincipalRfqAwardCoreHistory.VerifyTerminal` against the actual Core case, stages, assignments, policy and complete decision history. Orphans, duplicate claim IDs/keys, wrong scope/source, malformed exact values or Core mismatch deny the whole read; none are silently omitted. Exact four-decimal integer arithmetic rejects over-allocation.

Existing worker-era `SourcingAward`/`SourcingAwardLine` and `PurchaseOrder.SourcingAwardId` have no reviewed P38 handoff. The adapter must query them for this source and deny when any related legacy award or award-backed order exists. Direct principal P30 PO reservations are separate PR-line `PurchaseOrder` allocations and do not consume an existing reserved SourcingEvent hold. A later P38 award-to-PO route needs an explicit transfer rule so a pending/approved claim and issued order are not counted twice.

## Boundary and next proof

The private candidate checks supplied typed facts and uses the existing Core terminal replay helper for releases. It does not prove database origin, query completeness, current actor/branch authority, original quote-component prices, document use, physical FK/ACL behavior, or the future business-award handoff. The current P38 insert guard remains unavailable. Genuine migration, captured physical schema, finite grants, owning loader, and positive/negative API proof remain release gates. Focused tests are authored and unrun.

All supplied immutable P38 facts must carry nonempty owned IDs, canonical maker principal, command key and lowercase SHA-256 pins, matching scope, and original creation actor/time/version metadata. Even a legacy order with no award FK is rejected if a caller supplies it from another tenant/company; the owning query must additionally prove that its result set is complete for the target source.

This source increment removes the separate pending Core replay: `PrincipalRfqAwardCoreHistory.VerifyRetained` checks the same retained source, named policy, assignment pins and engine decisions for Pending and terminal cases. `VerifyTerminal` remains terminal-only. The reducer treats a verified pending return value as reserved and requires a verified stored terminal outcome before releasing. This is a pure graph check, never proof of database origin or current authority.
