# Items & Projects workflow depth

This domain contains **15 local workflows** for items, stock, projects, tasks, time logging, timer simulation, time-to-invoice staging, project closeout, inventory reporting and reporting tags. Each flow uses 2–3 operational entry steps; the shared renderer supplies review, back, cancel and completion handling.

No workflow sends email, charges a payment, starts a native timer, schedules work, changes a tenant setting, or calls an external API. Completion text describes only the local workspace result.

| flow id | steps | validation | persisted/linked result | evidence |
|---|---|---|---|---|
| `create-item` | Item details; Pricing and stock; Reporting | Item name, type, unit rate and non-negative opening quantity; optional tag and availability checkbox | Creates an `items` record with name, category, description, amount, quantity/reason and reporting label | tenant-pattern — Items list/New entry is observed; form values are local |
| `edit-item` | Choose item; Update details; Reporting label | Required existing item, name and type; non-negative rate when supplied | Edits the selected `items` record while leaving stock history intact | local-concept |
| `adjust-item-stock` | Select item; Adjustment | Required item, signed numeric quantity delta, reason and date | `inventory` effect records quantity delta/reason against the selected item | local-concept |
| `create-project` | Project setup; Dates and budget; Project category | Required project name and start date; customer is a record selection; budget cannot be negative | Creates a `projects` record and stores the selected customer name | tenant-pattern — Projects list/Create New entry is observed; form values are local |
| `edit-project` | Choose project; Project details; Dates and budget | Required existing project and name; customer is selected from `customers`; budget cannot be negative | Edits the selected `projects` record; existing time links remain associated | local-concept |
| `create-task` | Task details; Schedule and estimate | Required project and task name; estimate cannot be negative | Creates a local `tasks` artifact linked to a `projects` record | local-concept |
| `edit-task` | Choose task; Task details; Schedule | Required task/project/name; estimate cannot be negative | Edits the selected artifact in the `tasks` collection without deleting time history | local-concept |
| `create-time-entry` | Work context; Time details; Billing choice | Required project, date, positive hours, work note and time type; customer/task are record links | Creates a `timesheets` record linked to project, optional task and customer; marks invoice eligibility locally | tenant-pattern — Timesheets/New Log Entry and timer affordance are observed; exact form is local |
| `start-timer` | Timer context; Timer note | Required project; task, note and time type are captured locally | Creates a `tasks` artifact action marker `timer-started`; no real clock starts | tenant-pattern — timer extension is observed on Timesheets; simulation behavior is local |
| `stop-timer` | Choose running timer; Capture elapsed work | Required task timer, date and non-negative elapsed hours; billable is explicit | Applies `timer-stopped` to the selected task artifact and stores simulated elapsed details | local-concept |
| `invoice-project-time` | Choose customer; Select time records; Invoice note | Required customer, one or more `timesheets` records and invoice date; due date is optional | `time-invoice` links selected timesheets to a local `invoices` draft for review; nothing is sent | local-concept |
| `close-project` | Choose project; Closeout note | Required project, closeout date/note and confirmation checkbox | Applies local `closed` action to the selected project artifact; historical records remain available | local-concept |
| `add-project-reporting-tags` | Choose project; Reporting labels | Required project and tag string; optional reporting group | Stores project tag changes as a local `settings` artifact action; no tenant settings are changed | local-concept — reporting-tag UI structure was observed on customer forms, not project forms |
| `generate-inventory-report` | Report window; Report labels | Required as-of date and report name; category/tag filters are optional | Creates a local `reports` artifact snapshot request from workspace item data; does not run/share an external report | local-concept |
| `import-items` | Choose CSV; Import mapping | Required CSV attachment and import name; default item type is selectable | Stages a local `import` targeting `items` for mapping/review; no external system is contacted | local-concept |

## Contract notes

- Entity effects use supplied `DataKey` values (`items`, `projects`, `timesheets`, and `invoices`); task, report and settings operations use the contract’s artifact collections.
- Customer/project/item/task/time selections are record or multi-record fields. Selected customer records are intended to persist by their displayed name, per the shared workflow contract.
- Status-like project closeout deliberately uses `artifact-status` with `action: "closed"` because the supplied typed `DemoStatus` union has no `Closed` value.
- All examples use fictional/local values only and do not copy tenant names, balances, IDs or contact data from the observed screens.
