# BE-01: Vendor and Item export source checkpoint

Status: source verified; private database/cookie acceptance and deployment pending.

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

## Remaining release gates

Run the actual local-cookie API and browser journey against a fresh private PostgreSQL database with genuine migrations. Independently check owner-side record invariance, exact audit rows, stored Core role revocation, denied company/branch contexts, real 10,000/10,001-row queries and audit-INSERT denial. Compile the isolated fixture tool before execution.

The source uses the current Procurement model; it must not be deployed directly over the older live P24 schema. A compatible release or separately rehearsed schema rollout is still required. No production migrations, grants, feature flags or data were changed by this checkpoint. Imports, clone/bulk operations, selected-row export and saved views remain separate BE-01 work. The original UAT register remains unchanged.
