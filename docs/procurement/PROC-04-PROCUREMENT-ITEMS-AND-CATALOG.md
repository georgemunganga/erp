# PROC-04 Procurement Items and Catalog

**Status:** Draft for business review · **Version:** 0.1 · **Hierarchy:** Procurement → Items and Procurement Catalog → Item Reference and Employee Catalog → Operations · **Framework:** [ERP 28-section standard](../hrm/feature-specifications/ERP_Feature_Specification_Framework_Enterprise_Integration_Updated.docx) · **Module:** [PROC-00](PROC-00-PROCUREMENT-MODULE-ARCHITECTURE-AND-CAPABILITY-MAP.md)

**Implementation checkpoint, 19 October 2026:** P11 adds an optional, company-scoped catalog entry reference and published version to each purchase-request line. The employee catalog exposes only currently published, broadly eligible entries. Request draft create/update verifies item, supplier, unit, currency, unit price, quantity limits and effective period against the selected entry. Submission repeats those checks in a serializable transaction, so a withdrawn or changed price cannot be submitted from an old draft. The request detail retains the entry ID and published version. This is guided draft buying; it does not approve spend, reserve a budget, issue a PO or make site/contract-restricted catalog entries available. API workflow tests, mocked browser flows and a restored PostgreSQL migration rehearsal provide the current evidence; a real-session production transaction remains a release check.

## 1. Feature Identification

`PROC-04`, later phase after shared item ownership is agreed. Business owner: category/catalog lead; product, data and technical owners to name. Operations: `O1` create/classify item, `O2` approve/publish catalog entry, `O3` request from catalog, `O4` update price/availability, `O5` retire and reconcile open requests.

## 2. Purpose and Business Outcome

Give employees a guided, policy-approved view of common goods and services while retaining a controlled non-catalog path. Success means easier request entry, consistent descriptions/units/prices and higher contract/preferred-supplier use without confusing a catalog price with a final PO commitment.

## 3. Scope and Exclusions

Includes goods, services, asset candidates, consumables and subscription references; category, UOM, specifications, preferred supplier/contract/price, lead time, eligibility and catalog display. Inventory owns stock item quantities and valuation; Assets owns asset records; Finance owns tax rates. Punchout, public marketplace and arbitrary custom item code are deferred.

## 4. Parent and Related Features

Parent Items and Procurement Catalog. PROC-02 uses item/catalog snapshots in PR lines; PROC-01 supplies preferred supplier eligibility; Contracts supplies prices; Inventory/Assets may provide canonical item classifications; shared metadata and search provide presentation capabilities.

## 5. Actors and Participants

Category manager, item steward, contract owner, supplier steward, employee requester, Finance tax-reference owner, Inventory/Assets steward, approver and auditor.

## 6. Entry Points and Triggers

New item request, contract award/rate change, supplier suspension, UOM or tax-reference change, price expiry, catalog search/selection and item retirement.

## 7. Preconditions

Agreed item system of record and stable ID; valid category, unit, entity/site, active supplier/contract where shown; approved price basis and effective dates. Missing owner or invalid reference blocks publication.

## 8. Workflow

`O1`: steward creates classified item/reference with specs and source IDs. `O2`: category/contract owner reviews price, supplier and scope; publishes employee-facing entry. `O3`: employee searches by need, selects eligible entry and quantity; PROC-02 pins catalog version to PR line. `O4`: owner versions price, supplier or eligibility; future requests use new version. `O5`: retire/hide future selection while open PR/PO retain historical snapshot and receive impact review.

## 9. Status and State Transitions

Item reference: Draft → Reviewed → Active → Inactive/Retired. Catalog entry: Draft → Pending Approval → Published → Superseded/Expired/Withdrawn. Actor, required source/price evidence, policy, effective date, SLA and escalation accompany each transition. A retired entry never disappears from historical requests.

## 10. Permissions and Data Scope

`procurement.item.manage` and `.catalog.publish` require category/entity authority; employees read only eligible catalog entries for tenant, site, role and date. Price and contract fields can be restricted. No staff user changes stock or asset records through catalog permissions.

## 11. Business Rules

`BR-PROC-CAT-001` catalog entry references an Active valid item; `002` preferred supplier must be orderable at use/PO issue; `003` price has currency, unit, tax basis and validity; `004` PR pins catalog version; `005` unavailable entry routes to governed non-catalog flow; `006` retirement cannot mutate open transaction snapshots.

## 12. Data Fields and Ownership

ItemRef: stable code, name, type, category, description/spec, UOM, tax/category refs, stock/asset flags and source version; canonical owner to decide with Inventory/Assets. CatalogEntry: tenant/entity/site, visibility, supplier/contract refs, price/currency/unit, lead time, limits, effective period, status/version; Procurement owns view. Stock balance, asset register and tax rate stay external.

## 13. Screens and Data Views

Employee catalog search/filter and item detail, non-catalog fallback, steward item list/editor, entry preview, approval/version comparison, expiry queue and usage report. Mobile and keyboard selection must work.

## 14. User Experience and Human Behaviour

Start with the user's need, not SKU knowledge. Display unit, lead time, price validity, preferred supplier and any location restriction. Distinguish estimated catalog price from final quote/PO amount; explain why an item is unavailable and offer permitted alternatives.

## 15. Notifications and Communications

Notify category/contract owner of expiring price, supplier suspension and approval task; notify requesters with open drafts when selected entry becomes invalid. Use record links and no confidential contract price in broad messages.

## 16. Documents and Attachments

Specifications, rate sheets, warranty/term references and product images are scanned, versioned and scoped. Published entry records exact source document version. Retention follows transaction and contract policy.

## 17. Exceptions and Edge Cases

Same item in multiple currencies/units, supplier suspension after selection, contract ceiling exhaustion, stock/asset classification dispute, expired price, changed tax code, duplicate item, location-specific availability and catalog item removed during draft.

## 18. Integrations, APIs, Events and Sandbox

Versioned item/stock/asset reference, contract price, supplier eligibility and tax-reference APIs. Publish catalog-entry published/retired facts after commit. Contracts carry tenant/entity, item/entry version, source owner, effective date, correlation and idempotency. Sandbox tests stale price and supplier changes with synthetic items.

## 19. Audit, Security and Privacy

Append item classification, price/supplier/version change, approver and publication. Enforce entity/site/contract field scope and log export. Catalog content should not expose restricted supplier terms.

## 20. Reports, Dashboards and Metrics

Catalog adoption, non-catalog request share, preferred-supplier utilization, expired entries, price variance between catalog and PO, and search terms with no result. Define denominator and as-of date.

## 21. Configuration and Localization

Item types, category trees, UOM conversions, site visibility, preferred supplier rules, price/contract precedence, language, currency display and effective date are configurable by entity. Shared tax and currency data remain external.

## 22. Non-Functional Requirements

Search and eligibility filters enforce scope and remain responsive at catalog volume. Versioned cache cannot serve retired or newly blocked entries beyond agreed delay. Accessible results and no-result states are required.

## 23. Postconditions

Published entry is discoverable only to eligible users; selected PR line retains exact item/catalog/price source version. Retirement blocks new selection while historic records resolve.

## 24. Failure and Recovery

Reference or contract outage prevents publication/use of unverified price; draft request retains input and asks for revalidation. Retry publication idempotently; reconcile stale search index against authoritative entry version.

## 25. Acceptance Criteria

Given eligible active entry, employee can create PR line with pinned version. Given expired price or suspended supplier, catalog order path blocks or routes to exception. Given retirement, historic PR retains original description/price while new search hides entry.

## 26. Test Scenarios

Test goods/service/asset/subscription, unit and currency variants, location scope, version change mid-draft, duplicate item, price expiry boundary, supplier suspension, search isolation, index delay and integration replay.

## 27. Ownership and Dependencies

Category owner governs entry, item master owner remains to decide, Contracts/Supplier/Finance/Inventory/Assets supply references. Depends on PROC-CFG-01, PROC-01 and PROC-02 interface; shared search/document service needed.

## 28. Open Decisions and Assumptions

Decide canonical item owner, catalog launch timing, price and contract precedence, non-catalog restrictions, UOM conversions, tax references, site visibility and performance volumes. Owners: Procurement, Inventory, Assets, Finance and ERP platform; resolve before implementation.
