# Receiving dependency audit for the bounded CRUD release

Read-only audit, 22 October 2026. Release backend checkpoint `dfdf36a`, UI `e3c6e7c`; subsequent credits/settings/catalog candidates require separate verification. No receiving code, migrations or production changes are made by this audit.

## Current release capability

`backend/procurement-api/src/Mightyfin.Erp.Procurement.Domain/CommercialRecords.cs` already defines GoodsReceipt, GoodsReceiptLine, ReceiptInspection, ServiceAcceptance and SupplierReturn. These records do not establish a live workflow. Release `ProcurementHostIntegration.cs` maps only `ProcurementOrderDraftRoutes.cs`; `receiving=false`. There is no receipt API, source picker, receipt-specific current permission policy, issued-version table or verified supplier-delivery evidence in this baseline. PO CRUD creates Draft, permits draft corrections, and cancels; it does not approve or issue an order.

## Why the existing receiving implementation cannot be copied alone

The held implementation at `/home/amizo/procurement-rfq-finance-identity-integration` intentionally verifies its source even for draft creation:

- `ProcurementReceivingDrafts.cs`: both SaveReceipt and SaveService call Source before drafting; drafts carry exact issued-version/PO-line references.
- `ProcurementReceivingSourceResolver.cs`: Source requires current Issued PO/revision, immutable OrderIssuedVersion and OrderSubmission, actual Approved Core PO case, Delivered evidence and exact sealed source/classification.
- `ProcurementReceivingRoutes.cs`: shared transactional coordinator, actual schema readiness, permissions and separate draft/final execution options.
- `ProcurementReceivingSourceResolver.cs`, `ReceivingIssuedAuthority.cs`, `ReceivingAcceptanceHistory.cs`: current PO/sealed lines, policy and retained history are verified without guessing unknown acceptance or consumption.
- P25 plus its additive stable-key correction reuse existing receipt/service records, adding two source/effect tables, scoped bindings and deferred exact-ownership SQL FKs. Ordinary mutable EF alternate keys must not be copied back.

A release draft PO has none of that proof. Do not mark its Status Issued, accept an arbitrary issued GUID, bind by description/name, infer Goods/Service from the current item, use zero accepted/consumed history, or call a saved receipt draft stock acceptance. A new unissued-order observation/intake contract would be a separate business feature; it is not an equivalent shortcut to the existing source-bound receiving UI.

## Minimal dependency path for actual receiving

1. Reconcile release and held migration ancestry. Release P20InvoiceDraftMaintenance and held P20InvoiceBranchScope both add supplier_invoices.branch_ref; copying both migration histories would duplicate the column. Preserve the release IsActive line-retention addition and all used invoice data.
2. Integrate reviewed current Core action/scope and PR approval/retained-source/Finance contracts needed by direct-buy conversion, keeping company policy configurable and unsupported owners unavailable.
3. Integrate P24 PO commercial execution: approved line allocation, finite independent approval, current supplier and Finance checks, sealed issue plus separately confirmed delivery. Reuse the existing Core engine and shared transaction/fences; do not create another approval engine or ledger.
4. Reconcile P25 receiving source/effect persistence and stable evidence bindings, actual permission/readiness/API adapter, scope-filtered source picker, current policy governance (including original-worker proof in the held P31 source), and existing receipt/service UI adapter. Draft and positive execution flags stay separate.
5. Run meaningful source tests, fresh/restored/legacy migration preservation and named FK/CHECK/unique/Down proofs, then actual API/browser/independent owning-DB receiving gates on one exact candidate. Inventory/Assets/Finance handoff is not implied by receipt persistence.

## Existing tests to reuse

Held Procurement.Tests: ControlledReceivingTests, ReceivingIssuedAuthorityTests, ReceivingAcceptanceHistoryTests, ReceivingPolicyRulesTests, ReceivingPersistenceModelTests and ReceivingPersistenceProjectionTests. Host tests include receiving gate/read contracts. Actual fixture: `backend/procurement-api/tools/ApprovalHttpPostgresProbe` receiving mode; existing frontend `reference-ui/tests/backend-browser/receiving.spec.ts` covers goods inspection/acceptance and partial service certification with owner verification. Their earlier passes belong to their original source/schema; they do not verify this release tree.

## Small actionable CRUD work instead

The current release already has `PUT /items/{id}/catalog-entries/{entryId}` and retained draft CatalogEntry records. Existing LiveItems only adds, reviews and withdraws prices. Complete its **draft edit** using the existing form components, explicit CAS, exact decimal companions and complete TaxBasis/AudienceCode projection; no new schema or policy activation. That is a direct missing adapter, rather than a new receiving architecture.

For configuration, categories and methods already have real list/create/detail/edit/retire APIs. Another bounded settings completion is typed PURCHASE_REQUEST/PURCHASE_ORDER **unused policy draft retirement**, using existing PolicyVersion lifecycle and permissions, explicit no-prior-review/no-published/no-consumer checks and retained audit; never a generic JSON settings store. It needs its own small specification and source/API tests before implementation. Currency, company, user, branch and tax ownership remain Core; a Procurement payment-terms/branding clone is not an appropriate fallback.
