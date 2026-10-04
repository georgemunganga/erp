# PROC-UX-09 — Reusing HRM's data movement pattern in Procurement

**Status:** Procurement frontend demonstration implemented 4 October 2026. Server integration remains a build dependency.

## Shared interaction

HRM's data movement flow is **choose file → match columns → preview each row → confirm accepted rows → export using the same fields**. Procurement now uses this pattern through a schema-driven transfer panel. Vendors and Items supply their own fields, duplicate checks, validation, write adapter, and export projection. The shared UI handles CSV reading, column matching, row review, download templates, and CSV export.

This is reuse of the ERP interaction and component pattern. The current HRM import component calls HRM-only server endpoints, so Procurement does not call those endpoints or write HRM data. The two frontends are separate deployments and do not yet import a single physical shared package.

## Procurement behavior now

| Record | Import result | Export scope | Sensitive fields |
|---|---|---|---|
| Vendors | Valid rows become **Proposed** within the selected legal entity's country. Normal onboarding and approvals still apply. | Vendors in the selected country; company name/ID, first contact, first category. | Bank and tax/payment details are excluded. |
| Items | Valid rows become **Inactive** for the selected legal entity. A buyer/admin reviews and activates them before employee use. | Items available to the selected entity; basic item fields. | Supplier offers and prices are excluded. |

An operator can map differently named spreadsheet columns. Invalid and duplicate rows show reasons; only ready rows can be saved. The CSV template uses the same labels as the export. Exports quote fields and prefix spreadsheet formula-like values so the file is treated as data.

The frontend currently accepts **CSV, up to 200 rows and 1 MB**. Excel users can save a sheet as CSV. HRM's Excel support and server-side preview are not yet connected to Procurement.

## Production contract to implement

Procurement needs its own authenticated schema, preview, apply, and export APIs, using the same shared ERP transfer service design as HRM. The server must enforce permission and legal entity scope, validate duplicates and referenced codes, record import/export audit events, use idempotent apply IDs, return per-row results, and limit file size and row count. Vendor import must never activate a supplier or import bank details. Item import must not create orderable catalog entries without review. Exports must be generated from server-scoped data with field-level access checks.

Until those APIs exist, the current UI uses local browser storage for its demonstration data. A download contains that browser's demo records only; it is not an ERP system-of-record export.
