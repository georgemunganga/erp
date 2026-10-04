# PROC-BE-01 — Procurement field coverage and schema gates

**Status:** Reviewed against the P0 migration on 4 October 2026; gaps remain before business CRUD

**Hierarchy:** ERP → Procurement → Backend → Field coverage → Migration gates

This is a field inventory, not an assertion that another product's private database is visible. The Frappe/ERPNext comparison uses its public [Supplier](https://github.com/frappe/erpnext/blob/develop/erpnext/buying/doctype/supplier/supplier.json), [Item](https://github.com/frappe/erpnext/blob/develop/erpnext/stock/doctype/item/item.json), [Material Request](https://github.com/frappe/erpnext/blob/develop/erpnext/stock/doctype/material_request/material_request.json), [Material Request Item](https://github.com/frappe/erpnext/blob/develop/erpnext/stock/doctype/material_request_item/material_request_item.json), [Request for Quotation](https://github.com/frappe/erpnext/blob/develop/erpnext/buying/doctype/request_for_quotation/request_for_quotation.json), [Purchase Order](https://github.com/frappe/erpnext/blob/develop/erpnext/buying/doctype/purchase_order/purchase_order.json), [Purchase Receipt](https://github.com/frappe/erpnext/blob/develop/erpnext/stock/doctype/purchase_receipt/purchase_receipt.json), and [Purchase Invoice](https://github.com/frappe/erpnext/blob/develop/erpnext/accounts/doctype/purchase_invoice/purchase_invoice.json) DocType definitions. These are moving `develop` references, checked on the date above. They do not include site-specific custom fields. The [Zoho vendor portal](https://www.zoho.com/us/procurement/help/vendor-portal/sign-up/), [item form](https://www.zoho.com/us/procurement/help/items/create-items/), and [purchase-request form](https://www.zoho.com/qa/procurement/help/my-requests/create-purchase-requests/) were used as a second public UI benchmark.

Our source of truth is the approved Procurement feature specification and ERP module ownership. Frappe and Zoho are completeness checks; their accounting, stock, payment and HR fields are not copied into Procurement merely because they appear on a form.

## What exists in the P0 migration

The committed migration `20261004005431_P0Foundation` creates `suppliers`, `supplier_contacts`, `catalog_items`, `purchase_requests`, `purchase_request_lines`, `policy_versions`, `import_batches`, `audit_events`, and `outbox_events` in the `procurement` schema. Each carries `id`, `tenant_id`, `legal_entity_id`, creation/update metadata and `version`. Composite keys confine supplier contacts and request lines to their parent's tenant and entity. This schema was validated on a disposable database only.

| Record | Fields present now | Coverage result |
|---|---|---|
| Supplier | Number, legal/display name, country, registration key, status, category, Finance supplier reference, proposing worker | Identity **partial**; onboarding, sites, classification and controls missing |
| Supplier contact | Supplier reference, one name, role, email, phone, primary flag | **Partial**; separate salutation/name/phones/language and portal access missing |
| Catalog item | Code, name, kind, category, UOM, description, tax ref, status, inventory/asset refs | **Partial**; buying defaults, supplier/price, availability and effective dates missing |
| Purchase request | Number, requester, department, cost centre, project, currency, purpose, status, needed-by | **Partial**; location, delivery, on-behalf-of, notes and submission snapshot missing |
| Request line | Catalog ref, text/UOM, quantities, estimated unit price, approval status and route | **Partial**; category, supplier preference, tax/discount and line-level date/coding missing |
| Policy/import/audit/outbox | Versioned rules, import metadata, audit metadata and event envelope | Infrastructure **partial**; approval decisions, evidence and delivery mechanisms are later work |

## P1 — Vendor CRUD field gate

The [supplier feature](PROC-01-SUPPLIER-ONBOARDING-AND-MANAGEMENT.md) and [New Vendor form](PROC-UX-05-NEW-VENDOR-FORM.md) require the following before their corresponding CRUD UI is connected to the database.

| Field group | Missing or incomplete fields / records | Recommended owner and shape |
|---|---|---|
| Legal identity | Trading name, company/registration ID type, tax identifier reference, website, supplier type, local/international and preferred/strategic flags | Supplier profile and effective-dated classification. Tax identifiers restricted. |
| Primary contact | Salutation, first/last name, work phone, mobile, language; additional contacts and their roles | `supplier_contacts`; keep separate contact rows and primary uniqueness per supplier. |
| Addresses and sites | Billing/correspondence address, physical/service sites, country, province/region, city, postal code, delivery capability | `supplier_sites`/`supplier_addresses` child rows, scoped foreign keys. |
| Commercial defaults | Default currency, payment-term reference, preferred category/product scope, tax category/reference | Supplier settings; terms/tax definitions owned by Finance/Core. |
| Eligibility | Application stage, orderable flag, effective/expiry date, hold/suspension/blacklist reason, decision actor/time and policy version | Controlled status/decision records; do not equate `Active` text alone with eligibility. |
| Diligence | Risk tier, qualification type/issuer/number/expiry, review result, document version and reviewer | `supplier_qualifications` and review/decision records; document bytes in shared document service. |
| Finance link | AP supplier account reference already exists; payment-verification status/token, callback ID and change version are missing | Finance owns bank account and full remittance details; Procurement stores restricted verification references only. |
| Portal and extensibility | Portal-access state, supplier user mapping, custom fields, reporting tags, remarks and document references | Portal identity is scoped to supplier; custom values validated by shared metadata service or typed extension record. |

Frappe's Supplier DocType includes supplier group/type, language, default currency, payment terms, tax ID, primary address/contact and hold/prevent-PO controls. Zoho's portal onboarding also asks for business, address and bank details. Our previous UI prompt includes these tabs, but the current migration does **not** persist most of them. A visible form is not evidence of backend coverage.

**P1 exit:** approved field/requiredness matrix by Draft, Proposed and Active; country-specific rules decided; normalized supplier/address/contact/qualification tables migrated; contact and site scope tests; Finance verification contract; safe import mapping; duplicate detection; approval/activation state tests. No full bank number in general Procurement tables, audit, search or events.

## P2 — Item and catalog CRUD field gate

The [item/catalog feature](PROC-04-PROCUREMENT-ITEMS-AND-CATALOG.md) needs these additions:

| Field group | Missing or incomplete fields / records | Ownership |
|---|---|---|
| Item buying data | Purchase UOM/conversion, purchase eligibility, manufacturer/brand/part number, lead time, minimum order quantity, specification/version, image/document reference | Procurement item reference if Core/Inventory has no canonical item owner; source mapping must be decided first. |
| Catalog entry | Supplier, contract, site/location visibility, effective dates, currency, unit price, tax basis, quantity limits, published version | Separate `catalog_entries`/price versions; do not store one universal price on `catalog_items`. |
| Finance and stock refs | Expense-account/category ref, stock classification, asset flag/class, inspection requirement | External stable IDs/flags; Finance, Inventory and Assets own ledgers and classifications. |

Frappe's Item DocType has many stock, manufacturing and accounting fields. The relevant buying checks are purchase UOM, minimum order, lead time, supplier items, purchase eligibility and inspection need. Zoho's item form exposes cost price, purchase account and preferred vendor. We should model their Procurement-facing equivalents without duplicating stock balance or valuation.

**P2 exit:** canonical item owner decided; item and catalog-entry migrations reviewed; dated price/eligibility behavior tested; supplier suspension makes entries unorderable; import cannot publish/order an item automatically.

## P3 — Purchase-request CRUD field gate

The [PR feature](PROC-02-PURCHASE-REQUISITION-AND-LINE-ROUTING.md) requires:

| Field group | Missing or incomplete fields / records | Shape |
|---|---|---|
| Header | Branch/site, delivery location/address reference, on-behalf-of subject, plan/funding reference, approver notes, submitted-at/version, source/channel | PR header plus immutable submission snapshot. |
| Line | Purchase type, category, line need date, preferred supplier, estimate currency/discount/tax basis, line cost centre/project and specification version | PR line fields; line coding can differ from header. |
| Policy and budget | Policy evaluation/version, Finance budget check ID/status/as-of, reservation reference/status, exchange-rate source | Decision/snapshot child records; Finance owns available budget. |
| Routing | Route/allocation ID, method, target RFQ/contract/PO ID, quantity/value, actor/reason/time, idempotency and reconciliation status | Separate line-allocation table; quantity counters cannot prove full lineage alone. |
| Evidence | Attachment references, version/checksum/scan status and comments | Shared document/collaboration services with scoped references. |

Frappe's Material Request and child item definition show request type, schedule date, company, item, quantity/UOM, warehouse, project, cost centre and ordered/received progress. Zoho's request form additionally shows expected date, delivery address, reason, notes to approver, reference, category, preferred vendor, estimated rate and discount. Our current PR migration lacks several of these, especially delivery and line-level financial detail.

**P3 exit:** header/line/additional child migrations; authoritative budget and workflow contracts; line allocation history; submit/revise concurrency tests; no PO creation from unapproved or cancelled demand.

## P4 onward — Tables not created yet

| Child area | Minimum Procurement-owned records to plan | External owner boundary |
|---|---|---|
| Approvals/budget | Approval instance/step/decision, policy evaluation, budget-check and commitment reference | Shared workflow executes approvals; Finance owns budget balances and reservation truth. |
| Sourcing | RFx event/items/invitations, bid header/lines/versions, evaluation scores, award/line award | Supplier portal has scoped access; no Finance ledger. |
| Contracts | Contract/version, rates, amendments, ceiling drawdowns, renewal obligations | Legal/e-sign integration supplies signed artifact. |
| Purchasing | PO/header/lines/schedules, revisions, dispatch and supplier acknowledgment, blanket releases | Finance owns accounting commitment. |
| Fulfilment | Goods receipt/lines, inspection, service acceptance, return and credit references | Inventory owns stock balance; Assets own asset register. |
| Invoices | Intake, supplier invoice/lines, match result, exception, AP hand-off, credit/refund references | Finance owns AP posting, payable, payment and bank reconciliation. |
| Cross-cutting | Document references, comments, notifications and integration delivery/reconciliation | Shared ERP services may own storage/delivery; Procurement keeps scoped references. |

Frappe's RFQ, PO, receipt and invoice DocTypes confirm the need for supplier, date, line, currency, tax, terms, address, delivery, return and source-document references. They **do not** justify putting full AP payment execution or stock valuation into our Procurement schema. The P0 migration intentionally has no RFx, PO, receipt, invoice or contract tables yet.

## Migration and review rule

1. Treat P0 as a **foundation migration**, not the complete Procurement database.
2. Before each child CRUD milestone, convert its section 12 field groups into a field dictionary: database column or child table, type/precision, requiredness by state, owner, classification, validation, source, API/UI mapping, and retention.
3. Resolve fields that need shared ownership or policy before generating a migration. Use normalized child tables for addresses, contacts, prices, decisions and allocations rather than an unreviewed JSON blob.
4. Generate a new additive EF migration for the child; keep the committed P0 migration unchanged. Test it on a disposable copy, including tenant/entity isolation, constraints, indexing and upgrade from the prior migration.
5. Connect a CRUD screen only when every visible field has a persistence/API mapping or an explicit external owner/reference; verify save/reload/export, not just form rendering.

**Conclusion:** The current schema has the right bounded foundation, but it does **not** yet have all fields needed for Vendor, Item, Purchase Request or the full procure-to-pay lifecycle. P1 vendor fields are the first schema expansion; P2 and P3 follow their own reviewed migrations.
