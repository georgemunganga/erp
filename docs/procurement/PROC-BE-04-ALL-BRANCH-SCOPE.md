# PROC-BE-04 addendum — Core selected branch and All permitted branches

**Status:** Source authored; host and browser verification pending. This addendum does not claim deployment.

## 1. Feature identification

Shared ERP shell branch selection for Procurement reads and branch-bound commands.

## 2. Purpose

Make the selected company and its permitted branch set explicit without using an omitted header as an All instruction.

## 3. Scope

Core workspace choices, shared shell transport, request/order/invoice reads and company master reads. Other routes retain their existing branch contract until individually reviewed.

## 4. Related features

PROC-BE-04, PROC-12 and Core workspace context; no new organisation or identity owner.

## 5. Actors

Current Core account and its current branch assignments; optional worker mapping grants nothing.

## 6. Entry points

Shell Organisation context, `/api/core/v1/workspace-context`, Procurement context, and listed Procurement APIs.

## 7. Preconditions

Authenticated current Core principal, current Procurement action, selected company and active Core master locations.

## 8. Selection model

A branch GUID means one branch. `branchScope: all` with empty branch means All permitted. Empty branch without the marker is unset and cannot open a live page.

## 9. Initial default

Use the sole currently permitted `WorkLocation.Type=headquarters`; otherwise the sole permitted branch. If neither is unique, use an explicit company All read selection. Never choose by GUID or name.

## 10. Current authority

The backend intersects All with active current Core branch assignments for the selected company on every request. Zero assignments retain Core organisation-wide semantics, limited to active branches in that company.

## 11. Company boundary

A selected company must be active and in the current Core workspace choices. Another-company branches do not enter the permitted set.

## 12. Branch boundary

Request, legacy order and invoice list/detail reads filter to permitted branch IDs before count, page, or lookup. Missing legacy branch references are excluded under All.

## 13. Company masters

Supplier and item masters remain company-scoped, including their finite currently mapped child/status routes. Category and method configuration plus the published catalog are company-scoped reads. Supplier open-order summaries filter branch facts before count; the company-wide suspend guard still considers all obligations without exposing order identifiers from other branches.

## 14. Transaction creation

All is rejected for branch-bound create, submit and other write routes. The operator selects a branch before starting a new branch transaction.

## 15. Existing transaction commands

The current command transport still requires a selected branch; command-by-record All has not been adopted without a per-route retained record-branch proof.

## 16. Documents

Shared Core document access remains governed by its existing owner and scope contract; All grants no generic document browse.

## 17. Errors

Malformed, conflicting or unsupported All headers are rejected. The live gate keeps the shared header visible and prompts a branch for unsupported feature pages instead of opening a blank transaction page.

## 18. API contract

The shell sends `X-Shell-Entity` and exactly one of `X-Shell-Location` or `X-Shell-Branch-Scope: all`. Capability metadata remains company-scoped.

## 19. Audit and privacy

The header is a selection, never permission. No denied branch IDs, records, counts or source labels may be serialized.

## 20. Reports

No report or export authority is added by this scope selection.

## 21. Configuration

Core WorkLocation `Type=headquarters` is the configured initial preference; no Lusaka literal or new default field is invented.

## 22. Performance

The server applies the finite current permitted branch set in SQL before count and pagination.

## 23. Postconditions

A read shows only records in the current permitted set. Branch-bound writes still name one valid branch.

## 24. Idempotency

Changing branch selection changes request context, not original command keys or persisted receipts.

## 25. Concurrency

Every request re-resolves current Core assignments; revocation takes effect before data reads.

## 26. Verification

Source tests cover explicit/unset transport, configured default choice, A+B assignment with C excluded, revocation, and All POST denial. SDK and browser execution remain pending.

## 27. Rollback

No schema change is introduced. The UI and backend source branches can be reverted independently before activation.

## 28. Remaining route inventory

All is source-enabled only on list/detail of vendors, items, catalog, configuration category/method reads, contracts, plans, requests, legacy orders and invoice intake, plus existing manual payments. All company-master vendor/item child commands remain authorized by their existing policies. RFQ, receiving, principal PO, credit, policy, reviewer queues, unsupported catalog subroutes, document and export routes need individual pre-count/current-Core review; they deliberately reject All. This is a bounded scope increment, not full Procurement All-route completion.
