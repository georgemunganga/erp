# PROC-11-P26-SOURCES — Invoice-purpose source selection

**Status:** Source authored; no compilation, migration, API/browser or production verification.

## 1. Feature Identification
Procurement → Supplier Invoices → Invoice Matching → Select issued line/recorded acceptance.
## 2. Purpose & Business Outcome
An explicitly authorized matcher can find the invoice's exact source without broad PO or receipt-read access or manual UUID entry.
## 3. Scope & Exclusions
Bounded source/physical-fact reads only. No matching decision, allocatable capacity, invoice approval, payable, supplier bank data, Finance posting or new policy authority.
## 4. Parent & Related Features
Existing invoice intake, P26 mapping/comparison, sealed P24 issued authority, reconciled P25 history, Core identity/branch scope and existing matching policy.
## 5. Actors & Participants
Current account with both explicit `procurement-invoice-match` and `procurement-invoice-read`. Mapping saves separately require capture; no PO/receipt grant is added. Reuses existing matching current-account/worker resolution, with no new role or independent approver requirement for these reads.
## 6. Entry Points & Triggers
Existing Bills detail mapping panel, behind invoice-comparison activation/readiness. No new sidebar.
## 7. Preconditions
Owned invoice visible in current tenant/entity/selected and assigned branch; one effective independently published matching policy; exact same supplier/currency/branch and original PO pin for candidate orders. Sealed issued source must verify before usable fields are supplied.
## 8. Workflow
Choose a paged issued line. For three-way policy, choose a named reconciled goods-line or service certificate. Save using existing mapping contract and expected version. Server revalidates on every subsequent command.
## 9. Status / State Transitions
No business transition. Source state VerifiedIssued/Unavailable; physical history state VerifiedHistorical/NoRecordedAcceptance/Unavailable. These are read labels, not approval states. NoRecordedAcceptance describes records returned by this selector, not verified zero Finance exposure.
## 10. Permissions & Data Scope
Both policies are required at HTTP and current canonical point-of-use checks. Invoice branch is checked against current Core assignments/selection; candidate SQL then restricts supplier/currency/exact branch and intake PO pin before count/page. Unknown or cross-binding target404; broad read grants alone do not qualify.
## 11. Business Rules
Only current Issued revision and active lines. Reuse sealed verifier; existing policy derives method/acceptance kind. No client completeness flags. Literal escaped ILIKE search; page1–100000, pageSize1–25(default25), q<=120 and no controls. At most200 active source lines,500 related historical lines/states and2000 effects/goods/service rows before one selected-source bridge. Larger history returns unavailable, not fabricated zero.
## 12. Data Fields & Ownership
P24 owns sealed line unit/quantity/rate/tax; P25 owns accepted/returned facts; P26 owns no new selector record. All monetary/quantity values are exact strings. No data is persisted by these reads.
## 13. Screens & Data Views
Reuse Bills source/acceptance selectors. Show order number/line description, method and safe blockers. Historical physical quantity/value must not be labelled available-to-invoice, payable or remaining financial capacity.
## 14. User Experience / Human Behaviour
Loading, empty, denied, unavailable-history and failed states must differ. Preserve existing selection until a deliberate replacement. New command conflicts require refresh; selector hints never guarantee command success.
## 15. Notifications & Communications
None; reads create no notification/event.
## 16. Documents & Attachments
No documents, raw evidence JSON or private attachment references returned. Existing independently reviewed Core invoice-document version pins remain separate.
## 17. Exceptions & Edge Cases
Missing policy/currency409, invalid query422, unknown/scoped source404, missing source proof yields allowlisted unavailable blocker. History bounds/reconciliation failure returns Unavailable/items[]/total:null; verified empty page keeps actual total and NoRecordedAcceptance only when total0. Internal exception strings/policy JSON are never returned.
## 18. Integrations / APIs / Events / Sandbox
`GET /api/procurement/v1/invoices/{id}/matching/sources?page&pageSize&q` returns invoice/version/policy pins and paged issued-line candidates. Verified rows carry orderId/number,issuedVersionId,purchaseOrderLineId,description,unitCode,quantity,unitPrice,taxAmount,categoryCode,method,acceptanceKind. Unavailable rows omit values/method; blockers only MatchingCategoryUnavailable/IssuedEvidenceUnavailable.

`GET .../matching/sources/{orderId}/lines/{lineId}/acceptances?page&pageSize&q` returns invoice/source/policy pins, method/state/blocker, named reference/kind/sourceVersion and nullable goodsReceiptLineId/serviceAcceptanceId. Exact fields: acceptedQuantity,returnedQuantity,historicalNetQuantity,acceptedNetAmount,returnedNetAmount,historicalNetAmount; acceptedAt. Both return isAuthority/canAllocate/canPostToFinance:false; acceptance availabilityMeaning=HistoricalPhysicalFactsOnly. No mutation/event/API owner call. Existing private `--matching` probe gains source-only coverage.
## 19. Audit / Security / Privacy
Do not create business audit/evidence for GETs. Shared transaction/fence remains. No other-invoice allocations, supplier profiles, bank details, raw policy/evidence, Finance owner watermarks or broad PO payload. Internal history reconciliation may read related source allocations for integrity; those rows are not serialized.
## 20. Reports / Dashboards / Metrics
No new KPI. Existing technical request logs may observe latency/failure without invoice/supplier sensitive payloads.
## 21. Configuration & Localization
Existing matching policy controls methods; no new policy table/permission bootstrap. Exact normalized transport; labels use existing ERP localization later. Query/history bounds are deliberately finite initial limits.
## 22. Non-Functional Requirements
SQL count/page/search is scoped. At most25 sealed verifications per source page, with no ledger reconciliation per row. One bounded full-history bridge per requested acceptance source. No GET-created state, owner calls or financial capacity inference.
## 23. Postconditions
No business/database state changes; caller holds hints and source IDs only.
## 24. Failure & Recovery
GET refresh is safe; no automatic POST. Unknown histories stay unavailable. Unavailable result must not be converted by the UI into available0 or a passing comparison.
## 25. Acceptance Criteria
Explicit matcher/read can choose only invoice-compatible sources without PO/receipt grants; current scope/policy respected; quantities exact; missing/tampered source proof blocked; physical facts clearly historical; GET leaves invoice/receiving/mapping/claims/audit/outbox unchanged.
## 26. Test Scenarios
Authored mapped default-off/schema and broad/capture-role403 tests. Actual `--matching` source creates finite invoice-purpose matcher(noPO/receipt/capture), asserts sealed source and accepted exact facts, escaped wildcard search, page/query bounds, unknown invoice/order/line404, privacy and read-only footprints. Existing scope tests and upcoming actual restricted PostgreSQL gate remain required; authored tests are not passed evidence.
## 27. Ownership / Dependencies
Procurement-owned adapters; reuse Core current identities/branch fence, P24 source verifier, P25 existing bridge, P26 published policy and readiness. Parent owns SDK/migration/rehearsal scheduling. Frontend consumes these exact contracts only after verification.
## 28. Open Decisions / Assumptions
Larger-volume continuation protocol is deferred and safely unavailable above limits. Qualified matching claims still require real original-unit review and Finance history owner; installed owners remain unavailable. New API/schema/source is not live or release-approved.
