# Interaction coverage and current authorization

The user confirmed that the same Zoho Books Global organization is ready at its dashboard and requested the full UI exploration. This clears the prior Organization Setup gate; it does not authorize submitting accounting, payment, security or external actions merely to reach further screens. Use only the already-selected organization and My Browser. The current project is separate from Zoho Procurement.

## Observed tenant interaction stage

Authenticated navigation verified the Books Global workspace context and the major navigation groups: Home, Items, Sales, Purchases, Time Tracking, Banking, Accountant, Reports, Documents, and Settings. The task inspected representative module lists/empty states, an item detail with Overview/Transactions/History, blank customer/invoice/quote/bill/journal editors, an invoice bulk-item dialog, a report index/detail with Table/Chart view, settings index/general preferences, Banking setup, Documents Inbox, Budgets and Transaction Locking. A sanitized route/family log is stored in `docs/verified-screen-patterns.md`; the private action log is in `reference-private/notes/tenant-capture-index.md`.

Blank unsaved forms and a non-mutating picker were opened and then canceled. No real customer/item/bill/invoice/quote/journal/payment/project/account was created or edited. No records were deleted, approved, exported, imported or sent. No bank/feed/integration connection or accounting/security setting was saved. The report Favorites toggle was accidentally activated when a report star control was mistaken for the row; it was toggled off and the Favorites view was checked and confirmed empty. Opening the report updated the app's system “Last Visited” timestamp; that access metadata is logged, and cannot be rolled back.

The authenticated-browser renderer showed screenshots in its tool results, but its “Clean screenshot path” value was blank and no PNG/JPG/WEBP files were saved locally. Therefore the screen imagery is visible in the task's browser results, not available as an image-file archive in `reference-private/screenshots/`. Keep the artifact status honest; do not claim a complete screenshot archive.

## Coverage method

For each accessible canonical screen, inventory visible buttons, menus, tabs, links, overflow menus, contextual/bulk actions and field affordances. Record entry point, prerequisites, enabled/disabled state, action category, resulting screen/dialog, validation, cancel/back behavior and visible state changes. A screenshot supports appearance; an interaction record supports behavior. Group workflows by list, form, detail, wizard, confirmation/status, allocation/matching, approval, communication/portal, payment/refund, document/template, report, settings or automation/integration families. Implement one reusable family and module-specific variations rather than duplicating equivalent pages.

Use reversible navigation, empty forms, opening/closing dialogs, and controls that do not submit. Low-impact fictional data may be used only in the prototype's browser-local store. No test data is to be created in the live tenant as part of this prototype work. If a safely reachable preview shows an external or consequential commit action, inspect the control but do not commit it.

## Hard action boundaries

Payments, bills/invoices/journals/credits, customer/vendor communications, portal invitations, approvals with business consequences, exports/imports of tenant data, bank/feed connections, integration setup, accounting/security configuration changes, and destructive actions are not ordinary UI exploration. Stop before their commit/submit steps. A general request to test the interface is not an exact payload authorization for a financial record submission or external action. Keep any such steps marked **observed control, commit not executed**, **blocked by safety boundary**, or **unverified** as applicable.

## Evidence labels

- **Public-doc candidate:** appears in official help, not verified for this Global tenant.
- **Tenant-verified read-only:** availability or state observed without business-data mutation.
- **Observed interaction:** a safe control was exercised and its effect recorded.
- **Observed control, commit not executed:** a consequential final action was not performed.
- **Blocked by safety boundary:** reaching the next step could have unacceptable consequences.
- **Unverified:** evidence is still absent.
- **Observed unavailable:** a plan/permission/feature limitation was visibly confirmed.
- **Synthetic demo:** fictional browser-local prototype behavior only.

## Implementation boundary

The frontend remains React + TypeScript + Tailwind, mobile-first, modular and fictional browser-local only. No backend, authentication, live Zoho API or actual accounting operations. No tenant screenshot should be placed in public assets, website storage, shared uploads, commits or backups. Unvisited states and any unavailable or risky workflow remain explicitly unverified rather than portrayed as exact replicas. The website remains unpublished.
