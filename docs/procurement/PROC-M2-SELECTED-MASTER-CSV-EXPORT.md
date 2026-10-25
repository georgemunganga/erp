# Selected Vendor and Item CSV exports

## 1. Feature Identification

PROC-M2-SELECTED-CSV. Procurement → Master data operations → Selected Vendor/Item CSV → Export selected records. M2 / BE-01. Status: implementation and verification in progress; no deployment or UAT claim.

## 2. Purpose & Business Outcome

Download exactly the records selected in the existing directory, without rebuilding the table or downloading unrelated records.

## 3. Scope & Exclusions

Vendor and Item human-view CSV only. Preserve All/search exports, columns, formula protection and filenames. Imports, bulk changes, merge, saved views, round-trip format and operational transaction exports remain separate.

## 4. Parent & Related Features

Existing scoped master CSV, shared directory selection, Core company/branch context and current permissions. This extends the accepted P24/M0 compatibility baseline without a later-model dependency.

## 5. Actors & Participants

Permitted signed-in Core system users, whether or not linked to an employee. Core determines current identity, membership, action grants and scope.

## 6. Entry Points & Triggers

Select rows on the loaded Vendor or Item directory page → existing export menu → Export Selected. A direct API call has the same validation and authorization.

## 7. Preconditions

Valid active Core company/branch context; current access plus relevant read/manage and explicit export permissions; genuine native reader/model/migration readiness. One to 100 distinct nonempty record GUIDs.

## 8. Workflow

Snapshot selected visible IDs; send one bounded GET ids query using the current session and scope headers. Recheck server permissions and visibility, query the exact set, require every ID to be returned, generate fixed CSV, durably append one export audit, then download. Ignore cancelled or stale-context responses.

## 9. Status / State Transitions

UI: ready → preparing → downloaded or actionable error. No business record lifecycle change. A rejected selection produces neither a CSV nor an export-success audit.

## 10. Permissions & Data Scope

Reuse procurement-access; vendors additionally require vendor-read and vendor-export, items item-manage and item-export. Existing company/tenant and vendor visibility filters apply before selected-ID filtering. IDs do not grant access. Missing, removed, hidden, foreign-company and foreign-tenant IDs share the same unavailable response.

## 11. Business Rules

ids is a single comma-separated exact D-format GUID set, maximum 100, with no empty/zero/duplicate IDs. Repeated ids, unknown query keys and combining ids with search/status fail 400. An absent ids parameter keeps old behavior; an empty ids parameter never means All. A partial visible set fails 404 without a partial file. Sort by existing directory default, not client order.

## 12. Data Fields & Ownership

Procurement owns vendor/item rows. Core owns identity/context and current grants. Selection is transient. Existing AuditEvent stores actor, correlation, current scope, exact row count and payload/filter SHA-256. Canonical selected IDs contribute to the filter hash; raw rows and banking data are not added to audit.

## 13. Screens & Data Views

Reuse the supplied directories, shared selection controls and export menu. Existing loaded-page selection and all other table functions stay. Clearly distinguish selected, full-search and All exports.

## 14. User Experience / Human Behaviour

No new form or layout. Show the selection count; explain that selection applies to the loaded page. Disable export while loading, failed, busy or denied. Clear selection on page/filter/context changes. For an unavailable selection, ask the user to refresh and select again.

## 15. Notifications & Communications

Inline preparing/download/error feedback. No email, background notification or supplier communication is introduced.

## 16. Documents & Attachments

A server-generated UTF-8 BOM CSV attachment, fixed quoted columns, CRLF and existing spreadsheet formula protection. No source-document upload or scanner dependency.

## 17. Exceptions & Edge Cases

Empty/duplicate/over-limit IDs, malformed GUIDs, hidden/missing IDs, permission revocation, foreign company/tenant, stale branch/account, interrupted download and storage/audit failure. Never fall back to client-row serialization or an All download.

## 18. Integrations / APIs / Events / Sandbox

GET /api/procurement/v1/vendors/export?ids=<set> and corresponding items route. Current cookies/scope headers, no-store, attachment response and X-Export-Id remain. 400 invalid filter; 404 unavailable selection; 401/403 authorization; 503 reader/storage failure. Use synthetic private PostgreSQL fixtures for integration proof. No downstream event or Finance call.

## 19. Audit / Security / Privacy

One durable ExportGenerated audit is required before successful file response. Denied/invalid/partial sets do not create success audit. Preserve existing formula protection and minimum fixed projection. Do not log selection contents, credentials or CSV payload.

## 20. Reports / Dashboards / Metrics

Existing audit row-count and payload hash support independent reconciliation. This does not add analytics or change dashboard totals.

## 21. Configuration & Localization

No new tenant setting, permission grant or numbering series. Existing date-based filenames and English CSV headings remain; no currency conversion or financial totals are added. The 100-row selection cap matches the maximum loaded page.

## 22. Non-Functional Requirements

One bounded server query, maximum 100 selected IDs and existing authorization filters; stable order; no-store; responsive existing table. Abort/discard stale scope requests. A missing required audit blocks file release.

## 23. Postconditions

Successful bytes contain exactly the currently permitted selected set and one matching audit. Business masters, versions, states, roles and outbox remain unchanged.

## 24. Failure & Recovery

Preserve UI context and actionable error. Refresh and reselect unavailable records. An interrupted authorized read/export can be explicitly retried; each generated file is independently audited. Do not automatically retry a lost response or claim that download delivery is known.

## 25. Acceptance Criteria

Existing All/search exports remain correct. Selected vendor/item exports contain exactly chosen rows; any inaccessible member rejects the entire set with no CSV/audit. Current authority, workerless accounts, cross-company/tenant fences, revoke and stale-context checks hold. Existing directory and mobile layout remain intact. Actual API/browser/PostgreSQL and independent owner evidence precede release.

## 26. Test Scenarios

Parser boundaries including 100/101, zero/duplicate/repeated/combined filters; exact CSV/audit for both masters; hidden/missing/foreign selections; permission revocation and no-audit role; All/search regression; existing table selection/download desktop/mobile; loading/error/scope cancellation. Distinguish unit/mocked presentation from native HTTP/browser/owner proof.

## 27. Ownership / Dependencies

Coordinator owns the bounded Procurement adapter; Core services remain authoritative. No schema/model/migration/project changes; no scanner or Finance dependency. Current image and compatibility branches stay deployable separately from held later-model work.

## 28. Open Decisions / Assumptions

Assume selected means the currently loaded page, explicitly stated by the existing directory. Cross-page selection and import/merge policy are separate scope. Business acceptance and production-user UAT remain pending until observed; source and tests alone do not close M2.
