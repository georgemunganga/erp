# PROC-01-EXPORT — Vendor list CSV export

## 1. Feature Identification

**ID:** PROC-01-EXPORT. **Hierarchy:** Procurement → Supplier Management → Vendor list CSV export → Export all / Export search results. **Audit finding:** BE-01, bounded first slice. **Status:** Ready for implementation under the contract below; source/runtime acceptance not yet claimed. **Version/date:** 0.1 / 25 October 2026. **Owner:** Procurement domain with shared Core access/data-file infrastructure.

## 2. Purpose & Business Outcome

An authorized system user can download the permitted vendors as a human-view CSV, using the existing list menu. The result respects the same company and record visibility as the server list. A linked HRMS employee is not required.

## 3. Scope & Exclusions

Include synchronous CSV for All and View, scoped queries, explicit permissions, bounded rows, spreadsheet text protection and durable generation audit. Selected remains visible but unavailable until a server selection contract is accepted. Exclude imports, XLSX/JSON, bulk edits, deletion, saved views, arbitrary columns, queued exports, email/sharing, bank/tax data and programmatic round-trip guarantees. Supplier accounting balances and remittance instructions remain Finance-owned.

## 4. Parent & Related Features

Extends [Supplier Management](PROC-01-SUPPLIER-ONBOARDING-AND-MANAGEMENT.md). Reuse Core identity, organization context, roles, audit conventions and the existing shared HRM CSV quote behavior. Existing list presentation remains authoritative. Finance, Inventory and Assets retain their ledgers.

## 5. Actors & Participants

Authorized Procurement users need `procurement-vendor-read` and `procurement-vendor-export`. Core administrators assign permissions explicitly. Auditors inspect generation evidence. The API executes within its existing scoped record connection; the infrastructure owner verifies audit INSERT privilege. Designation or employee status does not grant this operation.

## 6. Entry Points & Triggers

Existing vendors list → More → Export → All or Search results. The adapter calls the typed endpoint only after readiness and both permissions are present. No demo store or browser-local record export is used for live rows.

## 7. Preconditions

An active authenticated Core account, valid selected company, permitted branch context, ready existing native reader, both named permissions, and an available audit writer. No employee link, supplier portal, scanner, Finance posting interface or new monetary policy is required. A runtime that does not recognize the new action leaves the control unavailable.

## 8. Workflow

1. Capture the current Core/session/company/branch context and chosen mode.
2. All omits search/status; View uses the server search/status across all pages. Refuse View while page-only filters are active; use default server order.
3. Recheck current server grants and existing record visibility before reading. Apply the same list query and scope before limiting.
4. Fetch at most 10,001 rows. Above 10,000, return a named error without CSV.
5. Render only the fixed columns with invariant values and protected quoted text.
6. Commit one ExportGenerated audit event, with a server export ID and metadata only, before constructing the file response.
7. Return UTF-8 CSV attachment. The UI starts the download only if its captured context still matches. Cancel/discard on context change and prevent duplicate clicks.

## 9. Status / State Transitions

No new business status column, table or export job is introduced. Request-local lifecycle: Idle → Generating → Download starting / Failed / Cancelled. The durable success event means Generated, not that a browser saved or opened the file. A retry is a new generation with its own export ID; underlying records retain their states and versions.

## 10. Permissions & Data Scope

`procurement-access` plus `procurement-vendor-read` plus `procurement-vendor-export` are required. Both new export actions have empty bootstrap membership and are excluded from BOTH legacy ERP-admin all-key resolution paths. Existing explicit grants and unrelated roles stay unchanged. System users without employee links are eligible. Tenant/company filters, creator/reviewer/read visibility and selected branch authorization match the existing reader. These are company master records, not branch stock balances; do not invent branch-owned row predicates. Company All permits only the exact two export GET routes, never prefix-wide write access.

## 11. Business Rules

BR-EXP-001: Export never widens list visibility. BR-EXP-002: no implicit new grants, including legacy ERP admin. BR-EXP-003: no silent truncation above 10,000 rows. BR-EXP-004: CSV uses a fixed whitelist, not arbitrary JSON properties. BR-EXP-005: audit commit precedes file delivery; audit failure yields no file. BR-EXP-006: no record/workflow/financial changes. BR-EXP-007: preserve existing HRM/statutory export output. BR-EXP-008: All means the current permitted company scope; View means server search/status, not local page-only filters or ordering. BR-EXP-009: selected-ID/unknown filter parameters are rejected in this slice. BR-EXP-010: headers-only CSV is valid for zero matching rows.

## 12. Data Fields & Ownership

Procurement owns the exported master values. Fixed ordered CSV fields:

- `number`
- `displayName`
- `legalName`
- `countryCode`
- `status`
- `isOrderable`
- `createdAt`

Use human-readable stable column titles and document their mapping in the encoder tests. Null text is empty; booleans are invariant; timestamps are UTC ISO-8601. No internal row/user IDs, contacts, notes, prices, bank references, tax identifiers, attachment content, custom JSON or balances are exported. Core owns identity/company/access; the shared writer owns quoting, while Procurement owns projection and spreadsheet-text handling.

## 13. Screens & Data Views

Reuse LiveCatalogDirectory → LiveRecordDirectory → ListTools/StandardDataTable. Retain the imported menu, list, selection, filters, columns, empty state and actions. Add no parallel list screen. Use existing ERP controls, font, branding and busy/error patterns. Other live directories keep their existing unavailable export behavior.

## 14. User Experience / Human Behaviour

Use simple labels such as Export all and Search results (default order). Explain that page-only filters must be cleared before View export. Selected remains visibly unavailable. Show busy state and an actionable error; cancellation on context change is quiet. A success notice says the download is starting, not completed. Never expose SQL, connection identities or implementation diagnostics.

## 15. Notifications & Communications

No email, SMS, WhatsApp or external message. Feedback stays on the initiating page. Async ready notifications are N/A because this slice is bounded and synchronous.

## 16. Documents & Attachments

The generated attachment is CSV with UTF-8 BOM and a fixed safe filename such as vendors-YYYYMMDD.csv. It is not uploaded or persisted as a supplier document. No scanner dependency is introduced. Use the existing shared quote helper with unchanged defaults; Procurement forces quoted cells and protects dangerous spreadsheet-leading text before quoting. No stored master field is changed.

## 17. Exceptions & Edge Cases

Cover no rows, invalid/overlong query, foreign company, disallowed branch, company All, hidden drafts, revoked permission with a stale UI/session, audit privilege/storage failure, cancellation, scope switch after response, repeated clicks, 10,000/10,001 boundary, delimiters/quotes/newlines and formula-leading text. Preserve creator, manager, review and invoice-capture visibility branches exactly.

## 18. Integrations / APIs / Events / Sandbox

`GET /api/procurement/v1/vendors/export?search=&status=`; no actor, raw SQL, IDs, page, pageSize or arbitrary column parameters. Inherit current Core scope headers and credentialed client behavior. Success: attachment `text/csv; charset=utf-8`, server export correlation ID. Errors: existing authentication/scope responses; 400 invalid filter; 422 `export-limit-exceeded`; controlled unavailable response when query/audit storage fails. No downstream ledger/event integration. Sandbox tests use fictitious private identities and isolated data; no production record mutations.

## 19. Audit / Security / Privacy

Append one `SupplierCsvExport` / ExportGenerated event with fresh export ID, actor, tenant/company scope, safe branch context, normalized-filter digest, row count, generated payload digest and correlation. Do not store raw search terms, CSV rows, bank/tax data, credentials or contact values in logs. Audit must be durable before file output; generation does not prove delivery. Confirm actual DB-role INSERT privilege before activation. Source privacy/CSV guidance: [OWASP CSV Injection](https://community.owasp.org/attacks/CSV_Injection). For this human spreadsheet format use quoted fields, escaped quotes and a tab prefix for formula-leading text, including leading-whitespace and full-width cases. No universal sanitizer or safety after arbitrary downstream transformations is claimed.

## 20. Reports / Dashboards / Metrics

Expose generation count, row count, failures/limit rejections and duration through existing audit/technical reporting. No spend, tax, stock, savings or payment metric is inferred. Reports respect existing audit access; no new dashboard is required.

## 21. Configuration & Localization

No new preference toggle or duplicated export engine. Availability is the existing native reader readiness plus current explicit named grants and verified runtime audit capability. Column schema is versioned as 1. CSV delimiter is comma; numeric/date/boolean representation is invariant. UI labels follow current module terminology and Core branding. No currency conversion or invented business threshold is introduced.

## 22. Non-Functional Requirements

Materialize a single bounded projection with a stable unique tie-break order; propagate cancellation and existing query timeouts. Hold no unbounded record list or broad reflection serializer. Audit insert failure returns no CSV. Downloads remain within the current client/session scope. Run meaningful encoding, authorization, query/scope, boundary, database privilege, real API and Playwright checks. Reuse existing dependencies; no schema migration is planned.

## 23. Postconditions

Authorized success returns exactly the permitted matching whitelist and records its generation. Existing master rows, workflow states, versions, Finance ledgers, Inventory and Asset records remain unchanged. A missing export permission, invalid scope or unavailable audit channel returns no data file.

## 24. Failure & Recovery

The UI may retry after a named error. Cancel query/render/audit before commit when the request is aborted. If generation was committed but transport failed, the audit stays Generated; a retry produces a new audit ID. Abort and discard late responses on company/branch/principal context changes. Preserve original failed test evidence; no fallback to local-demo CSV.

## 25. Acceptance Criteria

AC1: explicit-grant user without an employee can export allowed rows. AC2: read/manage alone and legacy ERP admin alone cannot export. AC3: All/View preserve server scope and visibility across pages. AC4: only the fixed columns appear and zero rows produce headers. AC5: 10,001 rows return a limit error, not a partial CSV. AC6: protected text stays in one quoted cell and known formula-leading inputs are neutralized for the declared viewing format. AC7: every returned file has a durable metadata-only audit. AC8: no file on revoked grant, foreign scope or audit failure. AC9: imported desktop/mobile toolbar layout is retained; scope-change responses are discarded. AC10: public runtime is enabled only after its separate compatible release acceptance.

## 26. Test Scenarios

Test distinct caller grants, fallback/seeded ERP admin exclusions and explicit custom role; stale claim revocation; company A/B and branch denial; all-branch finite path guard including rejected writes; exact list/export visibility and search/status parity; empty and 10,000/10,001 cases; field whitelist and no sensitive columns; separator/quote/newline/Unicode/leading whitespace and formula prefixes; unchanged HRM golden bytes; audit privilege denial and success with unchanged business-row hashes; normal-cookie private API/browser generation and independent CSV parsing; frontend busy/error/no-preview fallback, page-filter refusal, selected unavailability, scope-change abort and delayed-response discard. Test the real private DB role, not only a fake principal.

## 27. Ownership / Dependencies

Procurement owns projection, scopes and domain audit. Core owns current identity, grants, company/branch context and shared file encoding conventions. Existing imported UI/shared components own presentation. Coordinator owns implementation/release; this does not assign overlapping Manus work. Public P24 remains a separate compatible build; do not deploy the newer 91/93-key host on an older schema merely with flags off.

## 28. Open Decisions / Assumptions

Settled for this slice: All/View CSV only, 10,000 technical row bound, explicit grants, fixed fields, no new schema or preference flag, no employee dependency. Confirm actual audit write channel/role before coding the handler. Imports, selected IDs, XLSX/JSON, arbitrary filters/custom columns, async delivery and broader BE-01 operations remain separate increments. Current feature documentation is a build contract, not a claim of implemented or deployed exports.

