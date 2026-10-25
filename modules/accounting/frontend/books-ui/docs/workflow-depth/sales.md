# Sales workflow depth

This domain is intentionally limited to **customers, quotes, invoices, credits, recurring invoices and local customer-portal messages**. Every flow persists only local demo artifacts or local status/history entries. Sending, payment, refunds, schedules, authentication and portal delivery are not performed.

| Flow id | Steps | Validation | Persisted / linked result | Evidence |
|---|---|---|---|---|
| `create-customer` | Customer identity; Contact details | Name and customer type required; email required | Creates a local `customers` record with contact values and Draft status | tenant-pattern |
| `create-quote` | Quote details; Quoted work | Customer, quote reference and date required; line rows required | Creates a Draft `quotes` record with selected customer name and line rows | tenant-pattern |
| `send-quote` | Choose quote; Recipient context | Quote required; optional local recipient/message context | Sets selected quote to known `Sent` status; records local context; no email | local-concept |
| `accept-quote` | Select quote; Acceptance note | Quote and acceptance note required | Sets selected quote to known `Approved` status | local-concept |
| `decline-quote` | Select quote; Decline reason | Quote and reason required | Adds `declined` artifact-status activity to the selected quote without deleting history | local-concept |
| `convert-quote-to-invoice` | Choose accepted quote; Invoice dates | Source quote and invoice date required | Converts `quotes` to a linked local `invoices` artifact, retaining source linkage | local-concept |
| `create-invoice` | Invoice details; Invoice lines | Customer, invoice reference/date and line rows required | Creates a Draft `invoices` record with party, dates and lines | tenant-pattern |
| `edit-invoice` | Choose invoice; Apply changes | Existing invoice required; at least one editable value is expected by the renderer | Updates the selected local `invoices` artifact and its values/history | local-concept |
| `send-invoice` | Choose invoice; Recipient context | Invoice required; optional local recipient/message context | Sets selected invoice to known `Sent` status; no PDF/email delivery | local-concept |
| `record-invoice-payment` | Payment details; Allocate to invoices | Positive payment amount, payment date and allocation rows required; allocations use invoice `recordId`/`amount` | Creates a local `paymentsReceived` artifact linked to invoice allocations; no funds move | local-concept |
| `void-invoice` | Choose invoice; Void reason | Invoice and reason required | Adds `voided` artifact-status activity to the invoice; no ledger mutation | local-concept |
| `create-credit-note` | Credit details; Credit lines | Customer, reference/date, positive amount, reason and line rows required | Creates a Draft `creditNotes` record | local-concept |
| `apply-credit-note` | Choose credit; Allocate credit | Credit note and invoice allocation rows required; allocation amounts are positive | Links `creditNotes` to `invoices` allocations locally; no external balance settlement | local-concept |
| `refund-credit-note` | Choose credit; Refund details | Credit note, positive refund amount, date and reason required | Adds `refunded` artifact-status activity to the credit note; no money moves | local-concept |
| `create-recurring-invoice` | Profile details; Reusable invoice | Customer, profile name, start date, cadence and line rows required | Creates an Active local `recurringInvoices` profile; no scheduler is registered | local-concept |
| `generate-recurring-invoice` | Choose profile; Generation details | Recurring profile and invoice date required | Creates a local `invoices` artifact from the selected recurring profile; no schedule runs | local-concept |
| `pause-recurring-invoice` | Choose profile; Pause reason | Profile and reason required | Adds `paused` artifact-status activity to the recurring profile; no external schedule changes | local-concept |
| `manage-customer-portal-message` | Choose portal action; Portal message | Customer, Invitation/Comment choice and message required; email is optional local context | Creates a `portalMessages` artifact for an invitation or comment; no auth, account or delivery operation | local-concept |

## Contract notes

- The definitions export `const flows: FlowDefinition[]` and import the shared contract types.
- Record selectors use the source effect entity (`customers`, `quotes`, `invoices`, `creditNotes` or `recurringInvoices`). Payment and credit allocations use rows shaped by the renderer as `recordId` plus `amount`.
- Typed entity statuses use only `DemoStatus` values from `src/types.ts` (`Sent` and `Approved` here). Non-DemoStatus outcomes such as declined, voided, refunded and paused use `artifact-status` actions.
- Each flow has two operational entry steps. Review, back, cancel and completion affordances remain the responsibility of the shared renderer.
- Tenant evidence is used only for the observed blank customer, quote and invoice entry structures. Lifecycle variants and unverified forms are marked `local-concept`.
