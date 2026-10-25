# Zoho Books UI-reference coverage plan

The separate Books project is initialized for the same user-selected **Global edition** organization. The user confirmed the dashboard is ready, and authenticated exploration began in My Browser. The home dashboard and Global/ZMW context were observed. Do not submit Organization Setup or click its Get Started action.

The user's follow-up requests button behavior, not screenshots alone, and permits interaction testing and creating fictional records where safe. This project's accepted boundary is still no actual accounting operations. Blank forms and low-impact UI controls have been inspected without saving. No live accounting record was created or changed. A report Favorite toggle was reversed and the Favorites state checked; opening Profit and Loss updated the system last-visited metadata. The [interaction coverage plan](interaction-coverage-plan.md) and [verified screen-pattern map](verified-screen-patterns.md) distinguish observed screens from unverified controls. The My Browser renderer showed image captures inline but returned no local screenshot paths; no raw image archive is claimed.

Public help currently surfaces as US-EN, often with `/us/books/` links and a Global edition selector. A selector does not establish Global behavior. Workflows below remain **public-document candidates** unless separately marked tenant-verified in the manifest. Plan entitlements, roles, feature enablement and exact organization-specific states remain partly unverified. The frontend is fictional, browser-local React + TypeScript + Tailwind, mobile-first and modular, with no backend, authentication, live Zoho API or actual accounting operations. Neither the website nor original reference screenshots may be published.

## Canonical screens and reusable components

Use one canonical reference for each meaningful screen family, then record module-specific differences rather than duplicate full pages per workflow. The **list/overview** family covers transaction lists, customers/vendors, accounts, items, projects, reports and settings indexes. Its reusable parts are navigation, page header, search, filters, sorting, tabs, selection controls, responsive cards/tables, pagination, status badges and empty/loading/error states. Which controls actually exist in each Books module remains unverified.

The **record-detail** family combines a record header, status, primary fields, line items or related records, document preview, activity/history and contextual actions. Manual-journal Details, PDF, Activity Logs and applicable Related List views are documented [7]. Other detail tabs remain candidates until their sources or tenant observations establish them. On mobile, a focused detail view can replace the desktop list/detail split without duplicating underlying components.

The **form** family combines entity selectors, field groups, date/term controls, line-item editors, totals, attachments and footer actions. Invoice, journal and item forms have source-supported field candidates [1] [7] [8]. Nested pickers, validation errors, save modes and cancel behavior need interaction evidence. The **wizard** family provides a stepper, file/configuration stage, mapping stage, preview stage and outcome/status panel. Journal import explicitly supports selection, mapping and preview [7]; other imports named only in overviews must not be assumed to share exact fields or steps.

The **confirmation/status** family covers safely observed dialogs and existing draft, sent, scheduled, paid, accepted/declined, published, approval or reconciliation states. A screenshot of a state is not evidence that its transition was executed. The **allocation/matching** family covers bank match/categorize review and credit/payment allocation candidates [3] [5]. The **approval** family covers approval settings and action affordances, with the warning that the reviewed approval help describes an old version [4].

The **communication/portal** family covers customer-portal navigation, quote response, invoice/payment receipt, invitation and email/public-link candidates [1] [2]. The **payment/refund** family captures safely reachable payment forms, existing receipt/detail states and credit/refund candidates without contacting gateways or transferring money. Detailed refunds and exact public-link flows remain unverified. The **document/template** family covers PDF/print preview and documented document views; template editing is a requested target, not established Global functionality.

The remaining families are **settings**, **reports** and **automation/integration setup**. Shared pieces include category navigation, section forms, preference controls, report filters, charts/tables, integration catalogs, connection dialogs and dependency notices. Users/roles, templates, automation and exact Analytics/CRM setup paths are still requested capture targets rather than verified capabilities [11] [13]. Never invent missing controls to make the inventory appear complete.

## Sales and receivables

The invoice documentation supports the candidate route Sales → Invoices, a new-invoice form, draft/send alternatives, email scheduling, payment-received options and import selection → configuration → mapping → preview [1]. Candidate fields include customer, invoice number, dates/terms, item lines, totals, notes and attachments. Sending and payment recording are consequential operations, so safely opened previews or existing states are preferable to committing them merely to obtain screenshots.

Recurring invoices are described by frequencies, but detailed recurring-profile management and auto-charge controls were not established in this survey [1]. The customer portal supports quote viewing and response, invoice views, payment history and online payment when an associated gateway exists. Customer portal access depends on an email address, and saving an invitation sends it externally. Sales-order portal visibility is preference-dependent [2].

Map this domain to list, customer detail, transaction form/detail, wizard, communication/portal, document and payment families. Customer maintenance, organization-side quote/sales-order lifecycle, full recurring-profile controls and credit-note application/refund are requested targets whose detailed layouts or steps remain unverified. Record every safely reachable toolbar/menu action, form picker, document tab and portal-affordance prerequisite before implementing their synthetic equivalent.

## Purchases and payables

The Bills overview supports bill list/detail candidates and references to journal and recorded-payment views. It names Create Bill, Record Payment, Import Bills, Apply Credits to Bill and Create Vendor Credits, but these names alone do not establish exact fields or authorize consequential submissions [3]. Approval help describes Settings → Organization → Approvals → Purchase Approvals and names Bills, Vendor Credits and Purchase Orders. It explicitly covers the old approval version and links to a newer flow, so its labels must not be treated as current tenant truth [4].

Map purchases to list, transaction form/detail, import wizard, credit/payment allocation and approval/settings families. Vendors, expenses, purchase-order lifecycle, full vendor-credit/payment-made screens and Documents Inbox remain unverified targets. Existing bills or safely opened forms may reveal controls; posting bills, recording payments, applying credits or approving transactions must not be done casually for UI capture.

## Banking and cash

Banking help names account/feed setup, transaction intake, matching/categorization, rules and reconciliation [5]. The account guide describes manual account creation or available automatic-feed providers, account/date/currency selection and external connection prerequisites [6]. The overview names manual transactions, imports and undo-last-import; matching, categorization, adjustments and multi-statement matching; rules; reconciliation and undo/delete reconciliation [5]. Detailed wizard fields, matching rules and reconciliation inputs remain unverified.

Map banking to account setup, account/transaction list, intake wizard, matching/allocation, rule settings and reconciliation families. Capture provider catalogs and safe setup dialogs only where opening them has no unintended effect; do not authorize feeds, log into banks or move money. Matching, categorization and reconciliation can affect accounting state, so existing states and commit-not-executed evidence are preferable to altering real books. Do not infer that any feed provider or workflow exists in the selected Global organization.

## Accountant and tax

Manual-journal help supports Accountant → Manual Journals, journal lists/forms, date/number/notes/reporting-method/currency fields, account/contact/type/tax/debit-credit lines, draft/publish alternatives and detail/PDF/activity views [7]. It describes import file options, mapping and preview. Drafts and published journals have different accounting consequences; the availability of a draft option does not make a live financial record submission automatically safe [7].

Map journals to list, form, line-item editor, detail, document and import-wizard families. Chart of accounts, opening balances, transaction locking, organization multicurrency and edition-specific tax/VAT/GST setup remain requested targets with detailed coverage unverified. Journal currency/tax fields do not establish organization-wide configuration. Do not generalize US tax examples to Global.

## Items, inventory, projects and time

Item help supports candidate lists/forms/details, item type/name, sales and purchase information, optional tracking, accounts, opening stock/rate, reorder point and vendor information [8]. Inventory quantities can be affected by sales and purchase transactions; a standalone stock-adjustment flow was not established. Project help names Time Tracking enablement, projects, billing/budget methods, tasks, management views and import [9]. Timesheet help names manual entries, timers, imports, calendar/management views and billing from time, without this survey establishing every detailed field or billing step [10].

Map these modules to record list/form/detail, project card/list, timesheet calendar and import-wizard families. Reversible field/picker tests and suitable fictional records may be possible under the updated permission, but stock changes, time billed to customers and invoice issuance are consequential. Do not enable modules to manufacture availability. Item tracking and linked customer/project/task/employee context must be observed rather than invented.

## Reports, settings and integrations

Report help names business overview, sales, inventory, receivables, payments received and activity areas, plus charts, PDF layout, scheduling, sharing and access management. It states that draft transactions are excluded from reports [11]. The report index/view and report-management families should therefore track existing report controls and safe filter/layout behavior separately from configuration or scheduled delivery actions.

General preferences help documents Settings → Organization Settings → Setup & Configurations → General and names module enablement and saving. It notes an Inventory add-on prerequisite of an active supported Zoho Inventory organization [12]. Integration help names Online Payments, card-verification settings and a Books ZSC Key area. Gateways may involve external login or credentials; Payflow Pro and Payments Pro are described as deprecated and available to existing users only [13].

Map this domain to reports, settings/category forms, integration catalog and connection/dependency surfaces. Do not toggle modules, connect services, expose/regenerate keys, change permissions or schedule external reports for discovery. Users/roles, templates, automation and Analytics/CRM setup remain requested targets, not established Global-specific paths. The prototype may later simulate relevant interactions locally, clearly labeled as fictional rather than implementing real automations or integrations.

## Button coverage before building

For every reachable screen, enumerate visible buttons, links, tabs, overflow menus, contextual/bulk actions and field affordances. Record the entry point, prerequisite, enabled/disabled state, action class, resulting screen/dialog, validation, cancellation and visible state change. Attach canonical reference IDs and an evidence label. A control that cannot be executed safely gets an observed-control/commit-not-executed or blocked label, never an invented outcome.

Inspect existing states first. Use safe reversible tests and low-impact fictional records only when they add necessary evidence and cannot affect genuine business processes. Do not enter synthetic records into the live tenant. The current observed screens and interactions are indexed in `verified-screen-patterns.md`; private action notes are Git-ignored. A temporary report Favorite was removed and verified absent; report viewing updated system Last Visited metadata. The screenshot renderer supplied no local image paths, so a private file archive is still outstanding. Exact high-impact authorization and applicable financial submission safeguards remain required where relevant. These boundaries make coverage and limitations explicit.

The [capture protocol](capture-protocol.md) defines private storage and evidence labels; [interaction coverage](interaction-coverage-plan.md) defines behavioral recording. The sanitized manifest distinguishes observed tenant screens, public candidates and unverified workflows. Public candidate support must not be promoted to tenant verification merely because a screenshot in a help article resembles the desired layout.

## Prototype structure and evidence-dependent design

The later frontend will compose one mobile-first shell and reusable list/card/table, form/line-item editor, detail, wizard, dialog, status, allocation, report, settings and integration components. A workflow registry maps modules into these families, with data/configuration variations rather than one duplicated page per workflow. Synthetic fixtures and a project-specific browser-local state store keep all demo interactions isolated from Zoho. Local calculations and status changes are only fictional simulations, not posted accounting entries or real payments.

Sales and purchases compose list/form/detail/document plus communication/payment or approval variants. Banking composes setup/intake/matching/reconciliation. Accountant composes journal/detail/import. Items, projects and time compose record and calendar/management variants. Reports/settings/integrations compose indexes, focused views and configuration/dependency surfaces. This is information architecture, not an assertion about Zoho's exact visual language.

The accepted Procurement style is only a starting point; its workspace is not present here, so exact tokens have not been recovered. Verified Books screenshots must determine actual chrome, typography, spacing, control positions and meaningful variations. Do not claim implementation fidelity before those references exist. The project stays separate, private and unpublished.

## References

[1]: https://www.zoho.com/books/help/invoice/ "Invoices | Help | Zoho Books"
[2]: https://www.zoho.com/books/help/customer-portal/ "Customer Portal | Help | Zoho Books"
[3]: https://www.zoho.com/books/help/bills/ "Overview - Bills"
[4]: https://www.zoho.com/books/help/transaction-approval/enable-transaction-approval.html "Enable Transaction Approval"
[5]: https://www.zoho.com/books/help/banking/ "Zoho Books Banking help overview"
[6]: https://www.zoho.com/books/help/banking/add-accounts.html "Zoho Books Add Accounts help"
[7]: https://www.zoho.com/us/books/help/accountant/manual-journal.html "Manual Journals | Zoho Books Help"
[8]: https://www.zoho.com/books/help/items/ "Items | Help | Zoho Books"
[9]: https://www.zoho.com/us/books/help/projects/ "Projects | Help | Zoho Books"
[10]: https://www.zoho.com/books/help/timesheet/ "Timesheets | Help | Zoho Books"
[11]: https://www.zoho.com/books/help/reports/ "Reports | Zoho Books Help"
[12]: https://www.zoho.com/books/help/settings/preferences.html "Preferences | Zoho Books Help"
[13]: https://www.zoho.com/books/help/settings/integrations.html "Integrations | Zoho Books Help"
