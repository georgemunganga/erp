# BE-01: Vendor and Item export source checkpoint

Status: source and private database/cookie/browser acceptance verified; bounded P24-compatible backport deployed. See the [release receipt](P24-SCOPED-CSV-RELEASE-20261025.md); production-user business UAT remains unperformed.

This closes the first implementation slice of BE-01, not the full finding. The existing imported Export menus now call scoped server CSV endpoints. All records and search results use the same visibility rules as the corresponding directories; selected-row export remains unavailable until it has a server contract. Page-only filters block search-result export rather than silently changing its meaning.

## Published source

- ERP: `codex/procurement-scoped-exports-20261025` at `86ed727774853140fee93a35aa6ce05362e71edf`.
- Procurement UI: `codex/procurement-scoped-exports-ui-20261025` at `7cf8f55ff2d17e219c7e4985b2b635f80ee5b407`.
- Contracts: [Vendor export](../PROC-01-VENDOR-CSV-EXPORT.md) and [Item export](../PROC-04-MANAGED-ITEM-CSV-EXPORT.md), each using the 28-section feature standard.

The APIs require the existing reader permission and a separate explicit export grant. Legacy administrator fallback does not confer these new grants. Company scope precedes filtering and the 10,000-row limit. Successful files require a persisted audit record, use fixed non-sensitive columns, protect known spreadsheet-formula prefixes and send `Cache-Control: no-store`. Export reuses the existing HRM CSV quoting behavior without changing HRM import/export output. Core owns identity and permissions; an employee link is not required.

## Verification completed

- Official offline .NET Application/API/Tests compilation succeeded against the exact source above.
- Focused backend checks: **76 passed**, covering permission classification, configured grants, scope, CSV formatting and HRM encoder compatibility.
- Full host regression suite: **1,378 passed**, zero failed or skipped. Domain tests were not rerun; the unchanged parent carries its separately recorded 1,697 passing Domain tests.
- UI TypeScript and Vite build passed. Five CSV units passed. Five new browser scenarios passed across retained runs, including permissions, limit errors, page-only filters, late scope changes and the compact toolbar. Three existing catalog layout and fourteen request layout browser scenarios also passed. These are mocked API browser checks, not database acceptance or business UAT.
- Initial failures and their corrections remain in the private evidence directories; no failed run was relabeled as a pass.

Private evidence: `procurement-csv-exports-sdk-prep-20261025_run01` and `procurement-scoped-exports-ui-validation-20261025_run01` through `run10`, under the operator's existing ERP backup directory. Credentials and raw private artifacts are not committed.

## Private integration acceptance

The genuine current API and separately compiled fixture tool ran against a fresh PostgreSQL 17 database with genuine Core and Procurement migrations. Six fictional Core users had no employee links. Core authentication and Procurement used separate finite database roles; a second host lacked audit INSERT permission.

- Real-cookie HTTP acceptance passed with **13** new export audits independently reconciled to response identifiers, byte hashes, row counts, actors, company and branch context. Checks included company/branch denial, explicit-grant denial, list/export parity, fixed columns, formula protection, empty files, malformed filters, the actual 10,000/10,001-row boundary and audit-write failure returning 503 without a file.
- The genuine live-mode UI build used Node 22.23.2 and `VITE_PROCUREMENT_LIVE_MODE=1`. Both actual Playwright journeys passed: Vendor export and Item limit rejection followed by a bounded search-result export. Independent before/after database snapshots found exactly **two** matching new audits and unchanged master records.
- The same-cookie revocation test passed: one successful export, the exact synthetic Core role grant removed, then 403 using the same session. Its separate owner reconciliation found one export audit and precisely the expected role change.
- All old audits remained unchanged. The fixture shut down through its protected completion path with exit 0; private proxies stopped. Production data, permissions and schema were unchanged.

The private receipt is `procurement-csv-export-runtime-prep-20261025_run02/terminal-acceptance.private.json`. Product source is `86ed7277`, fixture source `65c656ec`, and UI source `7cf8f55f`; fixture changes were subsequently integrated without changing the accepted application source.

Earlier failed attempts are retained. These exposed a fixture namespace/table grant error, an oversized negative-test query, a preview-mode UI build and duplicate test discovery. The initial live browser inspection also caused extra requests: Playwright 1.62.1 can refetch a response when `response.body()` uses its Chromium fallback. The corrected proof hashes the actual download stream and retains strict one-audit-per-download reconciliation. Final live artifact evidence is in UI validation `run11`; the earlier `run10` preview build is not a live-release artifact.

## Selected-row export successor: privately verified, deployment pending

The [M2 private successor](P24-M2-SELECTED-CSV-PRIVATE-VERIFICATION-20261025.md) now has a bounded selected-ID API/UI contract and actual normal-cookie PostgreSQL/browser/owner proof. It preserves existing All/search exports and verifies full fixed CSV cells/bytes for selected sets up to100; missing/invisible partial sets return no file/audit. This supersedes the source-only unavailability note for that child slice; it does not alter the historical receipts or claim an M2 deployment. Current production remains the separately recorded M0 release.

## Remaining release gates

The source uses the current Procurement model; it must not be deployed directly over the older live P24 schema. The [bounded P24-compatible release](P24-SCOPED-CSV-RELEASE-20261025.md) is deployed; later model source still requires its own separately rehearsed schema rollout. No production migrations, grants, feature flags or data were changed by this checkpoint. Imports, clone/bulk operations, selected-row export and saved views remain separate BE-01 work. These technical checks do not mark any business UAT status as passed.

[PR93](https://github.com/georgemunganga/erp/pull/93) contains the bounded P24-compatible backport. Its [separate P24 private verification](P24-SCOPED-CSV-PRIVATE-VERIFICATION-20261025.md) passed 92 focused/1,057 Host tests, 13 real-cookie HTTP exports, first-use Core choice with zero owner delta, two browser exports and same-cookie revocation with independent owner reconciliation. The bounded P24 source is now deployed; real production-user CRUD/business UAT remains unperformed. The inherited operational All-branch limitation is recorded in the [scope compatibility finding](../../core/KNOWN-P24-BRANCH-SCOPE-COMPATIBILITY-20261025.md); this export slice does not certify global operational aggregation.
