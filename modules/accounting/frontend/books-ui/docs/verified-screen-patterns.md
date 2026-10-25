# Tenant-verified Zoho Books screen patterns

**Scope:** same user-selected Zoho Books Global organization, inspected in My Browser after the dashboard-ready signal. Captured 20 Oct 2026. This is a partial inventory, not exhaustive button coverage. The associated screenshot image results remained in the task conversation; no local image files were returned.

| Observed screen/state | Canonical family | Verified structural observations | Coverage status |
|---|---|---|---|
| Home dashboard | Dashboard | Top app bar; left module rail; Receivables/Payables summaries; Cash Flow and Income/Expense widgets; Top Expenses; Projects and Banking watchlist; right-side help/chat surfaces. | Tenant-verified read-only |
| Items list | List | “All Items” view, New action, More Actions, table columns, Advanced Search and item rows/empty states. | Tenant-verified read-only |
| Item detail | Record detail | Overview, Transactions and History tabs; transaction-type/status filter; empty transaction notice; creation history entry. | Tenant-verified read-only; no record values copied |
| Invoices list | List / onboarding empty state | “All Invoices”, New menu, invoice and recurring-invoice shortcuts, import link; empty-state education panel. | Tenant-verified read-only |
| New invoice | Transaction form | Customer, invoice number, order number, invoice/due dates and terms, receivable account, salesperson, subject, line items, notes, discount, shipping, adjustment, terms, attachments; Save as Draft, Save and Send, Cancel, Make Recurring. | Blank form only; commit not executed |
| Invoice transaction preferences | Settings dialog | Discount, additional charge, tax inclusion and rounding choices surfaced when opening a blank invoice; dialog was closed without applying values. | Tenant-verified display; no settings saved |
| Invoice bulk item picker | Dialog / picker | Search-or-scan field, selected count/quantity, Add Items and Cancel; initial picker showed no results; canceled with no selection. | Observed interaction |
| Customers list | List / onboarding empty state | “All Customers”, New, import and empty-state customer benefits. | Tenant-verified read-only |
| New customer | Contact form | Business/Individual choice, primary contact/company/display name, email/phone/language; tabs include Other Details, Address, Contact Persons, Custom Fields, Reporting Tags and Remarks; tax/currency/terms/portal fields; Save/Cancel. | Blank form only; no save |
| Customer Address | Nested form tab | Billing and Shipping address sections, contact/country/address/city fields, Copy billing address, address-format hint. Clicking Copy billing address with blank fields caused no record change. | Observed interaction on blank form |
| Customer Reporting Tags | Nested tab | Empty-state explains tags are configured under More Settings → Reporting Tags. A stale element index meant the expected Contact Persons tab was not verified; it remains open. | Tenant-verified display; partial interaction |
| Quotes list and form | List / transaction form | “All Quotes”, create/import affordances; blank quote fields include customer, quote/reference numbers, dates, salesperson/project, subject, items, notes, totals, terms, attachments, Save as Draft/Save and Send/Cancel. | Blank form only; commit not executed |
| Recurring Invoices list | List / onboarding | Empty-state description of schedule-based repeat invoice profiles and create/import affordances. | Tenant-verified read-only |
| Payments Received list | List / empty state | Empty state; Go to Unpaid Invoices and Import Payments affordances. No payment recorded. | Tenant-verified read-only |
| Credit Notes list | List / onboarding | Create/import affordances and credit-note lifecycle education. | Tenant-verified read-only |
| Vendors list | List / populated state | List columns include name, company, email, work phone, payable and unused-credit values. Personal values intentionally omitted. | Tenant-verified read-only; sensitive row data excluded |
| Expenses list | List / onboarding | Receipts Inbox link, “All Expenses”, Record Expense, import and lifecycle information. | Tenant-verified read-only |
| Receipts Inbox | Document inbox / feature prompt | Autoscan enablement prompt. “Enable Autoscan” was not clicked. | Tenant-verified display; enablement unverified |
| Bills list and blank form | List / transaction form | List includes New/Create a Bill/import. Blank bill has vendor, bill/order numbers, dates, payment terms, payable account, line-item account/quantity/rate/discount/tax/customer, notes, attachments and Draft/Open/Cancel/Recurring actions. Canceled without save. | Blank form only; commit not executed |
| Purchase Orders list | List / onboarding | Create-new affordance and lifecycle education; no order created or approved. | Tenant-verified read-only |
| Recurring Bills list | List / onboarding | Create/import affordances and recurring-profile explanation. | Tenant-verified read-only |
| Payments Made list | List / empty state | No-payment state; Go to Unpaid Bills and Import Payments. | Tenant-verified read-only |
| Vendor Credits list | List / onboarding | Create/import affordances and lifecycle education; no credit applied. | Tenant-verified read-only |
| Batch Payments list | List / onboarding | New Batch Payment affordance and multiple-bill description. No vendor payment initiated. | Tenant-verified read-only |
| Projects list | List / onboarding | Start/New/Create New Project and lifecycle summary. No project created. | Tenant-verified read-only |
| Timesheets list | List / empty state | New Log Entry, period/customer/project/user filters, Log Time and timer-extension affordance. No time entry/timer started. | Tenant-verified read-only |
| Banking initial state | Setup / onboarding | Connect Bank/Credit Card, Add Manually, Skip and help link. None were selected. | Tenant-verified display; connections unavailable/unverified |
| Manual Journals list and blank form | List / transaction form | List onboarding; blank journal has date/reversal date, publish-reversal option, number/reference/notes, reporting method, currency, account/contact/type/tax/debit-credit lines, attachments and Draft/Publish/Cancel/Recurring actions. Canceled without save/publish. | Blank form only; commit not executed |
| Chart of Accounts | List | Active Accounts, search, More Actions and account-name/code/type columns; standard account categories visible. Account details not copied. | Tenant-verified read-only |
| Budgets | List / onboarding | Create Budget affordance and introductory copy. No budget created. | Tenant-verified read-only |
| Transaction Locking | Settings / confirmation controls | Sales, Purchases, Banking and Accountant lock controls plus Configure and Lock All Transactions. No lock or configuration change made. | Tenant-verified display; commit not executed |
| Reports Center | Report index | Search, Create New Report, Home/Favorites/Shared/My/Scheduled sections, category navigation and a report catalog (72 displayed). | Tenant-verified read-only |
| Profit and Loss | Report view | Date-range and accrual/cash basis filters, More Filters, Run Report, Table/Chart views, compare/customize controls. Zero-valued report rendered. View toggle was tested and restored. | Observed interaction; access updates “Last Visited” metadata |
| Reports Favorites | Empty state | Confirmed there are no favorites after the temporary report star toggle was reversed. | Restored state verified |
| Documents Inbox | Document index | All Documents, Files, Bank Statements, folder/trash navigation, Create New Folder and Autoscan enablement. No document/folder created; Autoscan not enabled. | Tenant-verified read-only |
| Settings index | Settings catalog | Organization, Users/Roles, Taxes, Setup & Configurations, Customization, Automation, Module Settings, Integrations/Marketplace and Data Management groupings. | Tenant-verified read-only |
| Settings → General | Preferences form | Module-enable choices; work week; invoice/quote PDF/email, discount, tax/rounding, salesperson, profit margin, billable cost, secure public links and organization address format options. No values saved or toggled. | Tenant-verified display; commit not executed |
| Settings → PDF Templates → Invoice Templates | Document/template index | Template-type navigation for Quotes, Invoices, Credit Notes, Purchase Orders, receipts, statements, Bills, Expenses, Vendor Credits/Payments and Journals; Standard Template marked Default, status filter, Configure Export File Name, Edit and New affordances. No edit/new/configure action used. | Tenant-verified read-only |
| Settings → Automation → Email Alerts | Workflow-action index | Module filter, Configure Failure Preferences, New Email Alert and an empty email-alert state; sidebar exposes In-app Notifications, Field Updates, Webhooks and Functions. No rule or alert created. | Tenant-verified read-only |
| Settings → Automation → Workflow Rules | Workflow index | View Logs, New Workflow Rule, per-day usage counts for Functions/Webhooks/Email Alerts, module/status filters, Change Execution Order, Configure Usage Limit Notification and an empty workflow state. No workflow action used. Screenshot upload failed for this browser result; visible text remains in the action result. | Tenant-verified read-only; screenshot unavailable |

## Important limitations

- The renderer returned a blank local screenshot path, so no screenshot binaries are present in the repository. This index records appearance/structure from the browser results, not a saved raw-image archive. The final Workflow Rules action also reported a screenshot upload failure.
- Contact Persons details, report export/schedule/share, customer/vendor record detail beyond a safe row, many edit/details variants, import mapping/preview, bank reconciliation/matching, payment/refund allocation, approvals, template editing, rule creation/execution, taxes and integration connection flows remain unverified or were intentionally not committed.
- An accidental report Favorite toggle was reversed; Favorites was inspected and confirmed empty. Viewing the Profit and Loss report changed its system last-visited timestamp, which is access metadata and cannot be rolled back.
- Do not copy real names, contact information, record IDs, balances or other tenant values into the prototype. Use fictional local fixtures only.
