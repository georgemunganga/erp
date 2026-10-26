# PROC-M2a Vendor and Item Server Import

**Status:** Draft implementation contract; no API, migration, grant, or live import is claimed. **Version:** 0.1. **Hierarchy:** Procurement → Supplier and Item Masters → Import. **Framework:** [ERP 28-section standard](../hrm/feature-specifications/ERP_Feature_Specification_Framework_Enterprise_Integration_Updated.docx). **Parents:** [PROC-01](PROC-01-SUPPLIER-ONBOARDING-AND-MANAGEMENT.md), [PROC-04](PROC-04-PROCUREMENT-ITEMS-AND-CATALOG.md). **Sibling:** [M2 selected CSV export](PROC-M2-SELECTED-MASTER-CSV-EXPORT.md).

## 1. Feature Identification

`PROC-M2a` adds server-owned CSV template, mapping, dry-run, and create-only commit for internal Vendor and Item masters. It is a child feature, not a claim that the existing browser demo imports ERP records. Operations are `O1` template/schema, `O2` map and preview, `O3` commit, `O4` recover the original outcome.

## 2. Purpose and Business Outcome

An authorized steward can bring a bounded set of new masters into the selected company without silently activating a supplier or publishing an item. Each source row has a stable result, and a lost HTTP response can be reconciled without creating the same master again.

## 3. Scope and Exclusions

M2a includes Vendors and Items, CSV only, server-approved fields, create-only Draft/Inactive records, explicit duplicate **skip** or **reject**, and durable preview/commit recovery. Vendor contacts, sites, bank or tax details, catalog rates, stock/asset quantities, price publication, approval, portal access, overwrite/update, selected-record import, XLS/XLSX, bulk delete, and external API import are later contracts. The existing individual-create flows remain available for exceptional records. No Finance, Inventory, employee, or document-clean assertion is introduced.

## 4. Parent and Related Features

PROC-01 owns supplier eligibility and review; PROC-04 owns item and catalog lifecycle. M2a may reuse the shared HRM import field-schema concepts, but Procurement owns authorization, scope, persistence, and recovery. It must use current Core identity and company/branch context rather than create a second identity or workflow engine. The supplier spec currently excludes bulk import until duplicate and scope controls are specified; this child supplies those controls without changing supplier approval.

## 5. Actors and Participants

Vendor steward with both vendor-create and the new vendor-import grant; item steward with item-manage and the new item-import grant; reviewer of ambiguous supplier identities; administrator assigning explicit grants; auditor observing durable metadata. A Core user need not have an employee record merely to import permitted masters.

## 6. Entry Points and Triggers

Use the existing Vendor or Item list toolbar's Import control and wizard. The live list currently passes no `onImport` callback and displays “Import unavailable” (`reference-ui/src/components/data/LiveRecordDirectory.tsx:70`). The original `ListImportWizard.tsx` is a local preview with CSV/TSV parsing, client sample CSV/XLS, field mapping, duplicate choices, and a synchronous callback; `features/Catalogs.tsx:435-630` changes local records only. M2a preserves that layout while replacing its data adapter and labels with server template, preview, commit, and recovery calls. Do not expose the demo callback on a live page.

## 7. Preconditions

Current Core principal, selected authorized tenant/company and valid branch context, explicit action grants, Procurement physical reader/write readiness, and finite DB grants must be available. The preview must carry a server template/schema version and canonical mapping. Commit requires the same company, actor, file digest, mapping, duplicate policy, and row payload digest as preview, a valid unexpired preview, a caller-supplied command key, and current grants. No guessed default company or employee link is accepted.

## 8. Workflow

`O1`: GET the server field schema and UTF-8 CSV template for one entity. `O2`: upload or submit bounded CSV plus explicit source-column mapping and duplicate policy; parse complete RFC-style records, validate *all* numbered rows and foreign references, classify duplicates, and return counts and row errors without writing masters. `O3`: the operator confirms a zero-error preview; POST the same canonical data and command key. Under one scoped database transaction, recheck current grant, preview identity/digest, uniqueness and references, create every eligible row and its immutable outcome; record skipped rows, batch result, and audit, then commit. Any unexpected row failure rolls back the entire commit and returns a fresh preview requirement or controlled conflict. `O4`: GET the scoped batch/command result after an unknown response; replay of the same key and digest returns the original result bytes and record IDs, while a changed digest conflicts.

## 9. Status and State Transitions

Import batch: `Previewed` → `Committed`, `Expired`, or `Rejected`; failed commit leaves no partially created master rows. A committed batch is immutable apart from bounded retention metadata. Imported Vendor is `Draft` and not orderable; imported Item is `Inactive` and not purchasable/published. Supplier submit/approve and Item activate/publish remain separate authorized actions.

## 10. Permissions and Data Scope

Register explicit named actions `procurement-vendor-import` and `procurement-item-import` and their closed policies. Require vendor-import **and** vendor-create for Vendor; item-import **and** item-manage for Item. Exclude both actions from legacy ERP-admin implicit expansion, as with existing explicit-only export/return actions. A template request, preview, commit, and recovery all enforce current Core principal and company/branch context; recovery is restricted to the owning actor or an explicitly authorized scoped steward with a separately defined support contract. Query duplicate candidates inside tenant/company and the existing master visibility/authority boundary before returning any candidate detail; do not reveal foreign-company or hidden supplier data. Company-wide masters are not made branch-owned by this feature; branch selection remains a Core context admission check.

## 11. Business Rules

`BR-M2A-001`: server-approved columns only; unknown mapped target, duplicate target, missing required target, and malformed encoding fail preview. `002`: no client `status`, price, banking, portal, tax, contact, approval, or purchasing eligibility field is applied. `003`: item code is normalized and unique within tenant/company; a duplicate is skipped or rejects the whole preview according to the chosen policy. `004`: vendor country plus normalized registration is the existing unique key when supplied. An import row without a dependable vendor identity key is held as ambiguous for individual review; M2a does not infer uniqueness from a name or email. `005`: two identical keys within one file are classified before commit. `006`: preview decisions are revalidated under the commit transaction; concurrent creation cannot convert a reject into an overwrite. `007`: no partial success is reported as complete. `008`: same command key/same canonical request returns the original committed result; same key/different request conflicts. `009`: generated server identifiers and default currency come from the native create policy, never the browser demo.

## 12. Data Fields and Ownership

Vendor template starts with `legalName`, `displayName`, `countryCode`, and `registrationNumber` as the import identity key; optional safe classification/description fields require an explicit allowlist and native validator parity. Native Vendor creation requires the first three, generates Number and company currency default, and saves Draft/nonorderable (`ProcurementVendorRoutes.cs:153-185,884-905,958`). Item template starts with `code`, `name`, `kind`, `categoryCode`, `unitCode`; an optional description can follow native validation. Native Item creation requires those five, normalizes Code, and saves Inactive (`ProcurementItemEndpoints.cs:150-162,368-386,443`). Template labels, canonical keys, allowed values and examples are versioned; the demo's `sku`, `rate`, `status`, and Vendor contact fields are not accepted aliases without an explicit server mapping decision. Master data remains in Procurement; Core owns identity and organization references.

## 13. Screens and Data Views

The existing `ListTools`/`ListImportWizard` stages (configure, map, preview, done) remain. Replace local sample generation with a server template and show schema version; show all physical row numbers, errors, duplicate disposition, and totals from the server. Commit button becomes asynchronous and unavailable for errors or stale context. Done view displays committed/skipped counts and a durable batch link; unknown write state blocks blind repeat and offers scoped recovery. Keep browser-only `vendor_contacts` option disabled on live import until its own contract exists.

## 14. User Experience and Human Behaviour

Explain that imported Vendors require subsequent review and Items require activation. Distinguish “skipped duplicate” from “invalid or ambiguous”; never silently turn demo `overwrite` or `add` into a production update. Preserve the supplied layout and accessible keyboard progress. Warn before leaving a prepared preview and retain only harmless local mapping preferences; do not persist CSV content or credentials in browser storage.

## 15. Notifications and Communications

Commit returns an in-app batch outcome and scoped link. No external notifications are assumed. Existing supplier/item lifecycle notifications begin only at their own later transitions. Operational errors include a correlation ID without raw row values in broad logs.

## 16. Documents and Attachments

The CSV is import input, not an approved supplier document. M2a stores its digest and minimal metadata, not a new Clean scan claim or reusable evidence reference. If policy requires scanning uploads or retaining original files, that is a named document-owner decision before activation; no uploaded supplier qualification is treated as verified.

## 17. Exceptions and Edge Cases

Handle CSV quoted commas, escaped quotes, embedded newlines, BOM, Unicode, empty columns, duplicate headers, duplicate target mappings, malformed/oversize records, invalid reference codes, intra-file duplicates, concurrent DB duplicates, expired previews, actor/company changes, revoked grants, stale mapping version, transport loss, and second request with the same command key but different bytes. Existing HRM `ImportRowParser.ParseCsvLine` parses one line and is insufficient alone for quoted multiline records. The demo's 25 MB client limit is not a server admission policy; size, row and cell limits need an approved finite value before API activation.

## 18. Integrations, APIs, Events and Sandbox

Proposed native endpoints under each existing `/api/procurement/v1/vendors` or `/items`: `GET /import/schema`, `GET /import/template`, `POST /import/preview`, `POST /import/commit`, and `GET /import/{batchId}`; exact route names may be finalized with the owning API contract. Versioned preview request carries file digest, mapped rows, mapping, duplicate policy and schema version. Commit carries batch ID, identical canonical request digest, and command key. Return typed row outcomes and stable error codes; avoid raw exception messages. Use a fresh private database and real Core principal/finite roles for acceptance. Shared HRM `/api/hrm/import` remains HRM-owned and must not become a Procurement authorization bypass.

## 19. Audit, Security and Privacy

Audit template/preview as bounded metadata where policy requires; commit records actor, scope, batch/command ID, file and mapping digests, row counts, outcome and correlation. Do not put raw CSV, banking/tax/contact values, or full error payload in audit/logs. Duplicate detection and row errors must not expose a hidden supplier or foreign company. Enforce file parser resource limits, explicit field allowlist, current authorization at every phase, no cache of sensitive preview rows in a global static dictionary, and `Cache-Control: no-store` on sensitive responses.

## 20. Reports, Dashboards and Metrics

Scoped batch status/history can report created, skipped, rejected and expired counts, validation-error categories, and recovery attempts. It is not a supplier approval or item publication report. Avoid per-row sensitive values in broad dashboards.

## 21. Configuration and Localization

Set finite upload byte, row, column and cell limits by approved configuration, with hard ceilings; no default is inferred from the local demo. Use server-owned template version and company reference codes. CSV encoding and dates use documented invariant forms, while display labels may be localized. Duplicate policy defaults to `reject` until a user explicitly selects `skip`; overwrite is unavailable.

## 22. Non-Functional Requirements

Streaming/bounded parse and cancellation must avoid unbounded memory and DB locks. A preview must not write masters. Commit must be transactional, retry-safe, and recoverable after process restart. A unique database constraint, not just a preflight query, enforces each accepted natural key. Index and timeout choices are measured with private synthetic volumes before activation.

## 23. Postconditions

Successful commit leaves only server-created Draft Vendors or Inactive Items, immutable row outcomes and one durable batch result. Skipped duplicate rows leave existing masters unchanged. A failed or denied commit leaves master, receipt and audit facts unchanged except explicitly recorded safe failure metadata where a policy permits it.

## 24. Failure and Recovery

On 4xx validation/duplicate errors, return row-specific causes without applying any row. On unknown 5xx/network outcome, the UI performs scoped GET recovery using the original batch/command key and refuses a new key until reconciled. Same-key replay returns exact original result; conflict never reuses a stored result for different data. Expired preview requires a fresh preview. Restore/retry must recheck current scope and grants; revocation cannot be bypassed by a stored preview.

## 25. Acceptance Criteria

AC1 server template and mapped dry-run match native required fields; AC2 row errors and duplicate classifications preserve physical line numbers including multiline CSV; AC3 commit creates only Draft/nonorderable Vendor or Inactive Item; AC4 skip/reject leaves existing masters byte-identical; AC5 atomicity and DB uniqueness hold under concurrent commits; AC6 process restart and lost response recover exact original result; AC7 changed command payload and revoked/foreign scope are denied; AC8 browser wizard uses real cookie/API without local-demo writes; AC9 independent owner checks batch, row, audit, scope and master facts in a private database; AC10 no bank, tax, price, contact or approval side effect.

## 26. Test Scenarios

Pure parser/schema tests cover all CSV edge cases, mapping collisions and allowlist omissions. API tests cover missing action, create/manage conjunction, legacy ERP-admin exclusion, company/branch/tenant change, no employee, preview expiration and current grant revocation. Private DB tests cover exact natural-key constraints, two concurrent operators, transaction rollback on late duplicate, result-byte replay, changed-key conflict, restart recovery, and unchanged existing rows. Browser tests use existing wizard stages with real cookies, server template, dry-run errors, skip/reject and lost-response recovery. Verify that no downstream Vendor approval, Item activation or catalog publication occurred.

## 27. Ownership and Dependencies

Procurement owns native routes, `Supplier`/`CatalogItem` validation, batch/row facts and their migration. Core owns current principal, company/branch selection and role grants. HRM shared import types can supply field-schema conventions, but its current `ImportExportService` static in-memory preview (`Shared/ImportExportService.cs:42-142`) and `/api/hrm/import` route are not sufficient for Procurement scope or restart recovery. The existing `ImportBatch` (`Records.cs:344`, `ProcurementDbContext.cs:29,141,520`) already has tenant/company, type, filename/digest, state, counts, expiry and ResultJson. **Prefer extending that owned table**, with a unique scoped command key, actor, canonical request/mapping digests and version/CAS, plus immutable child row receipts for row number, disposition and created record ID. It has no such uniqueness or row linkage now; a genuine migration after the current P38 lineage and private Up/Down/physical verification are required before use. Keep original CSV bytes outside this table. Do not add a second batch table or duplicate the Core workflow engine.

## 28. Open Decisions and Assumptions

Owners must approve: (1) exact safe optional Vendor/Item field allowlists and reference-code validation; (2) import-only Vendor identity when registration is absent—this draft rejects those rows as ambiguous and directs individual intake; (3) finite file/row/cell limits and preview lifetime; (4) permitted support-user recovery, if any; (5) CSV original retention/scanning policy; (6) transaction timeout and batch size that meet measured resources; (7) row-receipt retention and privacy; (8) final route names and whether a server-side opaque preview payload is needed beyond a digest plus resubmitted canonical rows. None is a claim of approved business policy, current runtime support, or production deployment.
