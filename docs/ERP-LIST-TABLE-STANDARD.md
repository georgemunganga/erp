# ERP list table standard

Use the shared list component for ordinary record directories. The visual pattern combines the Procurement request list's quiet card and tab bar with HRM's search, filters, selectable rows, configurable columns, and page-specific actions.

## Anatomy

1. Page heading with the main create action and any import/export actions.
2. One list card containing optional view tabs, search, relevant filters, column settings, active filter chips, the table or empty state, and a count/pagination footer.
3. A primary record link in the first meaningful data column. Where whole-row opening helps, support mouse and keyboard activation without intercepting checkboxes, links, or action buttons.

## Component ownership

- `ListPage` in Core/HRM handles table layout, local search/filter/sort, selection, columns, accessible row opening, empty state, and footer slots. The page supplies columns, data, server paging, authorization, and actions.
- `RecordTable` gives HRM lists with specialized filters or inline controls the same table header, spacing, borders, and hover treatment. It accepts native table markup, so existing cells, forms, buttons, permissions, and server paging stay with the page. Compensation, configuration, leave, people, talent, payroll queue, and time lists use it.
- `StandardDataTable` in Procurement handles the same table body and row interactions. Procurement `RecordList` keeps its existing ListTools, workflow menu, export/import, and status tabs; it supplies page data and working pagination.
- The separate Procurement/Supplier frontend keeps its richer `ListPage` controls for sorting, export, clipping, and column widths, with the same card, tabs, toolbar, row, and footer styling.
- A page with a special table, such as an editable line grid, approval matrix, or financial reconciliation, keeps its own interaction model. It can use the shared visual tokens without forcing those workflows into a directory table.

## Rules for new list pages

- Keep search and filtering appropriate to the data source. Do not client-sort a partial server page as though it were the whole dataset; use server sorting for that case.
- Preserve the record-specific actions and permissions. The table must never grant an action merely because it displays a button.
- Show an honest row count and working pagination. Selection and “select all” apply to the visible rows unless the page explicitly offers cross-page selection.
- Show a useful empty state and create/import action where the user has permission.
- Use semantic theme colors and the global ERP font. Avoid hardcoded module colors in shared table chrome.
- Keep interactive controls keyboard accessible and label checkboxes, sort controls, row actions, and the table's current view.
