# Registered workflow inventory

Generated from the live application registry: 124 locally executable workflows. This is a simulation catalog, not evidence that every path was inspected or completed in the selected Zoho tenant. See `../reference-manifest.json` for tenant evidence labels.

## Sales

### `create-customer` · Create customer

**Route:** `/workflows/create-customer` · **Effect:** `create → customers` · **Registry evidence label:** tenant-pattern

Customer identity (Business or display name*, Customer type*) → Contact details (Email*, Phone, Notes)

Capture a local customer profile that can be selected on sales transactions.
The customer profile is available in the local Customers collection for selection on future sales workflows.
**Next:** `create-quote`, `create-invoice`, `manage-customer-portal-message`

### `create-quote` · Create quote

**Route:** `/workflows/create-quote` · **Effect:** `create → quotes` · **Registry evidence label:** tenant-pattern

Quote details (Customer*, Quote title or number*, Quote date*, Valid until) → Quoted work (Line items*, Notes and terms, Quoted total, Discount amount, Tax rate (%), Shipping / additional charge)

Draft a customer quote with dates, narrative and itemized lines.
The quote and its line items were saved to the local Quotes collection with Draft status.
**Next:** `send-quote`

### `send-quote` · Send quote

**Route:** `/workflows/send-quote` · **Effect:** `status → quotes` · **Registry evidence label:** local-concept

Choose quote (Quote*) → Recipient context (Recipient email, Message note)

Prepare a local send event for a quote without contacting an external recipient.
The selected quote is marked Sent locally; no email or external delivery was performed.
**Next:** `accept-quote`, `decline-quote`

### `accept-quote` · Accept quote

**Route:** `/workflows/accept-quote` · **Effect:** `status → quotes` · **Registry evidence label:** local-concept

Select quote (Quote*) → Acceptance note (Acceptance note*)

Record a customer's local acceptance of a sent quote.
The selected quote is marked Approved locally and is ready for conversion to an invoice.
**Next:** `convert-quote-to-invoice`

### `decline-quote` · Decline quote

**Route:** `/workflows/decline-quote` · **Effect:** `status → quotes → quotes` · **Registry evidence label:** local-concept

Select quote (Quote*) → Decline reason (Reason*)

Record a local decline reason for a quote without deleting its history.
The quote's local activity records a decline; no external notification was sent.

### `convert-quote-to-invoice` · Convert quote to invoice

**Route:** `/workflows/convert-quote-to-invoice` · **Effect:** `convert → quotes → invoices` · **Registry evidence label:** local-concept

Choose accepted quote (Quote*) → Invoice dates (Invoice date*, Due date, Conversion note)

Create a local invoice artifact from an accepted quote while retaining the source link.
A local invoice artifact was created from the selected quote and linked back to its source.
**Next:** `send-invoice`

### `create-invoice` · Create invoice

**Route:** `/workflows/create-invoice` · **Effect:** `create → invoices` · **Registry evidence label:** tenant-pattern

Invoice details (Customer*, Invoice number or title*, Invoice date*, Due date) → Invoice lines (Line items*, Notes and terms, Invoice total, Discount amount, Tax rate (%), Shipping / additional charge)

Draft a customer invoice with dates, terms and itemized billable lines.
The invoice and its line items were saved to the local Invoices collection with Draft status.
**Next:** `edit-invoice`, `send-invoice`

### `edit-invoice` · Edit invoice

**Route:** `/workflows/edit-invoice` · **Effect:** `edit → invoices` · **Registry evidence label:** local-concept

Choose invoice (Invoice*) → Apply changes (Invoice date, Due date, Line items, Notes and terms, Invoice total)

Update dates, notes or line items on an existing local invoice draft.
The selected invoice was updated in the local Invoices collection; no external document was changed.
**Next:** `send-invoice`

### `send-invoice` · Send invoice

**Route:** `/workflows/send-invoice` · **Effect:** `status → invoices` · **Registry evidence label:** local-concept

Choose invoice (Invoice*) → Recipient context (Recipient email, Message note)

Mark an invoice as sent in the local workspace without performing email delivery.
The selected invoice is marked Sent locally; no email, PDF delivery or external send was performed.
**Next:** `record-invoice-payment`, `void-invoice`

### `record-invoice-payment` · Record invoice payment

**Route:** `/workflows/record-invoice-payment` · **Effect:** `payment → invoices → paymentsReceived` · **Registry evidence label:** local-concept

Payment details (Payment date*, Payment amount*, Payment note) → Allocate to invoices (Invoice allocations*)

Apply a positive local payment amount to one or more invoices using explicit allocations.
A local payment receipt was recorded and linked to the selected invoice allocations; no funds moved.

### `void-invoice` · Void invoice

**Route:** `/workflows/void-invoice` · **Effect:** `status → invoices → invoices` · **Registry evidence label:** local-concept

Choose invoice (Invoice*) → Void reason (Reason*)

Keep an invoice in local history while recording that it is voided.
The invoice now has a local voided activity entry; no accounting ledger or external document was changed.

### `create-credit-note` · Create credit note

**Route:** `/workflows/create-credit-note` · **Effect:** `create → creditNotes` · **Registry evidence label:** local-concept

Credit details (Customer*, Credit note number or title*, Credit date*) → Credit lines (Credit lines*, Credit amount, Reason and notes*, Discount amount, Tax rate (%), Shipping / additional charge)

Draft a customer credit note with a reason and itemized credit lines.
The credit note was saved to the local Credit Notes collection with Draft status.
**Next:** `apply-credit-note`, `refund-credit-note`

### `apply-credit-note` · Apply credit note

**Route:** `/workflows/apply-credit-note` · **Effect:** `credit → creditNotes → invoices` · **Registry evidence label:** local-concept

Choose credit (Credit note*) → Allocate credit (Invoice allocations*)

Allocate a local customer credit note against one or more invoices.
The credit note was linked to the selected invoice allocations in the local workspace; no balance was settled externally.

### `refund-credit-note` · Refund credit note

**Route:** `/workflows/refund-credit-note` · **Effect:** `credit → creditNotes → invoices` · **Registry evidence label:** local-concept

Choose credit (Credit note*) → Refund details (Refund amount*, Refund date*, Refund note*)

Record a local refund request against a customer credit note without moving funds.
The credit note now has a local refunded activity entry; no money was transferred.

### `create-recurring-invoice` · Set up recurring invoice

**Route:** `/workflows/create-recurring-invoice` · **Effect:** `create → recurringInvoices` · **Registry evidence label:** local-concept

Profile details (Customer*, Profile name*, Start date*, Cadence*) → Reusable invoice (Recurring lines*, Default amount, Profile notes, Discount amount, Tax rate (%), Shipping / additional charge)

Create a local recurring invoice profile with a customer, cadence and reusable lines.
The recurring invoice profile was saved locally with Active status; no schedule was registered.
**Next:** `generate-recurring-invoice`, `pause-recurring-invoice`

### `generate-recurring-invoice` · Generate recurring invoice

**Route:** `/workflows/generate-recurring-invoice` · **Effect:** `recurring → recurringInvoices → invoices` · **Registry evidence label:** local-concept

Choose profile (Recurring profile*) → Generation details (Invoice date*, Due date, Generation note)

Create one local invoice from an existing recurring invoice profile.
A local invoice was generated from the selected profile; no scheduler or external delivery was invoked.
**Next:** `send-invoice`

### `pause-recurring-invoice` · Pause recurring invoice

**Route:** `/workflows/pause-recurring-invoice` · **Effect:** `status → recurringInvoices → recurringInvoices` · **Registry evidence label:** local-concept

Choose profile (Recurring profile*) → Pause reason (Reason*)

Record a local pause on a recurring invoice profile without changing any external schedule.
The profile has a local paused activity entry; no recurring schedule was changed.

### `manage-customer-portal-message` · Invite or comment in customer portal

**Route:** `/workflows/manage-customer-portal-message` · **Effect:** `artifact → portalMessages` · **Registry evidence label:** local-concept

Choose portal action (Customer*, Portal action*, Invite email) → Portal message (Message or comment*)

Create a local customer-portal invitation or comment record for a selected customer.
The invitation or comment was saved as a local portal-message artifact; no account, authentication or message delivery occurred.

### `edit-customer` · Edit customer

**Route:** `/workflows/edit-customer` · **Effect:** `edit → customers` · **Registry evidence label:** local-concept

Customer details (Customer*, Display name*, Email, Phone)

Update customer details and review the changes before saving.
The contact information and activity history were updated.

### `resume-recurring-invoice` · Resume recurring invoice

**Route:** `/workflows/resume-recurring-invoice` · **Effect:** `status → recurringInvoices` · **Registry evidence label:** local-concept

Profile (Recurring profile*)

Reactivate a paused profile and continue generating its documents.
The profile is active and can generate its next document on demand.

### `import-customers` · Import Customers

**Route:** `/workflows/import-customers` · **Effect:** `import → customers` · **Registry evidence label:** local-concept

Source data (CSV data*) → Field mapping (I have reviewed the header mapping and fictional data*)

Map a small fictional CSV batch, review its rows, then add the records to this workspace.
The validated CSV rows were added to the browser-local workspace. Open the linked records to continue their lifecycle.

### `import-quotes` · Import Quotes

**Route:** `/workflows/import-quotes` · **Effect:** `import → quotes` · **Registry evidence label:** local-concept

Source data (CSV data*) → Field mapping (I have reviewed the header mapping and fictional data*)

Map a small fictional CSV batch, review its rows, then add the records to this workspace.
The validated CSV rows were added to the browser-local workspace. Open the linked records to continue their lifecycle.

### `import-invoices` · Import Invoices

**Route:** `/workflows/import-invoices` · **Effect:** `import → invoices` · **Registry evidence label:** local-concept

Source data (CSV data*) → Field mapping (I have reviewed the header mapping and fictional data*)

Map a small fictional CSV batch, review its rows, then add the records to this workspace.
The validated CSV rows were added to the browser-local workspace. Open the linked records to continue their lifecycle.

### `import-recurringInvoices` · Import Recurring Invoices

**Route:** `/workflows/import-recurringInvoices` · **Effect:** `import → recurringInvoices` · **Registry evidence label:** local-concept

Source data (CSV data*) → Field mapping (I have reviewed the header mapping and fictional data*)

Map a small fictional CSV batch, review its rows, then add the records to this workspace.
The validated CSV rows were added to the browser-local workspace. Open the linked records to continue their lifecycle.

### `import-paymentsReceived` · Import Payments Received

**Route:** `/workflows/import-paymentsReceived` · **Effect:** `import → paymentsReceived` · **Registry evidence label:** local-concept

Source data (CSV data*) → Field mapping (I have reviewed the header mapping and fictional data*)

Map a small fictional CSV batch, review its rows, then add the records to this workspace.
The validated CSV rows were added to the browser-local workspace. Open the linked records to continue their lifecycle.

### `import-creditNotes` · Import Credit Notes

**Route:** `/workflows/import-creditNotes` · **Effect:** `import → creditNotes` · **Registry evidence label:** local-concept

Source data (CSV data*) → Field mapping (I have reviewed the header mapping and fictional data*)

Map a small fictional CSV batch, review its rows, then add the records to this workspace.
The validated CSV rows were added to the browser-local workspace. Open the linked records to continue their lifecycle.

### `import-recurringBills` · Import Recurring Bills

**Route:** `/workflows/import-recurringBills` · **Effect:** `import → recurringBills` · **Registry evidence label:** local-concept

Source data (CSV data*) → Field mapping (I have reviewed the header mapping and fictional data*)

Map a small fictional CSV batch, review its rows, then add the records to this workspace.
The validated CSV rows were added to the browser-local workspace. Open the linked records to continue their lifecycle.

### `import-batchPayments` · Import Batch Payments

**Route:** `/workflows/import-batchPayments` · **Effect:** `import → batchPayments` · **Registry evidence label:** local-concept

Source data (CSV data*) → Field mapping (I have reviewed the header mapping and fictional data*)

Map a small fictional CSV batch, review its rows, then add the records to this workspace.
The validated CSV rows were added to the browser-local workspace. Open the linked records to continue their lifecycle.

## Purchases

### `create-vendor` · Create vendor

**Route:** `/workflows/create-vendor` · **Effect:** `create → vendors` · **Registry evidence label:** local-concept

Vendor identity (Vendor name*, Email, Work phone) → Vendor notes (Notes)

Save a fictional vendor record for use across purchases and bills.
The vendor is saved in this local Purchases workspace and can be selected on purchase records.
**Next:** `edit-vendor`, `create-expense`, `create-bill`

### `edit-vendor` · Edit vendor

**Route:** `/workflows/edit-vendor` · **Effect:** `edit → vendors` · **Registry evidence label:** local-concept

Choose vendor (Vendor*) → Update details (Vendor name*, Email, Work phone, Notes)

Update a local vendor record without changing any external account.
The selected vendor's local details are updated.
**Next:** `create-expense`, `create-bill`

### `create-expense` · Record expense

**Route:** `/workflows/create-expense` · **Effect:** `create → expenses` · **Registry evidence label:** tenant-pattern

Expense details (Expense title*, Vendor*, Expense date*, Amount*, Category*) → Billable context (Description, Mark as billable)

Capture a purchase expense and optionally mark it billable for a customer.
The expense is recorded locally with its vendor, amount, category, and billable choice.
**Next:** `edit-expense`, `convert-expense-to-billable`

### `edit-expense` · Edit expense

**Route:** `/workflows/edit-expense` · **Effect:** `edit → expenses` · **Registry evidence label:** local-concept

Choose expense (Expense*) → Update expense (Vendor, Expense date*, Amount*, Category, Description, Mark as billable)

Correct a locally recorded expense before it is converted or reviewed.
The selected expense's local details are updated.
**Next:** `convert-expense-to-billable`

### `convert-expense-to-billable` · Convert expense to billable invoice

**Route:** `/workflows/convert-expense-to-billable` · **Effect:** `convert → expenses → invoices` · **Registry evidence label:** local-concept

Choose billable expense (Expense*) → Invoice context (Customer*, Invoice note)

Link a billable expense to a customer invoice draft in the local workspace.
The expense is linked to a local customer-invoice draft; no invoice is sent.
**Next:** `create-bill`

### `create-purchase-order` · Create purchase order

**Route:** `/workflows/create-purchase-order` · **Effect:** `create → purchaseOrders` · **Registry evidence label:** tenant-pattern

Order header (Order number or subject*, Vendor*, Order date*, Expected date) → Order lines (Line items*, Order total, Notes, Discount amount, Tax rate (%), Shipping / additional charge)

Draft a purchase order for a vendor with line items and expected dates.
The purchase order is saved locally as a draft for review.
**Next:** `submit-purchase-order`

### `submit-purchase-order` · Submit purchase order

**Route:** `/workflows/submit-purchase-order` · **Effect:** `status → purchaseOrders` · **Registry evidence label:** local-concept

Choose order (Purchase order*) → Submission check (Submission note)

Move a local purchase order from draft into submitted review status.
The purchase order is marked Sent in the local workspace; no vendor message was delivered.
**Next:** `approve-purchase-order`

### `approve-purchase-order` · Approve purchase order

**Route:** `/workflows/approve-purchase-order` · **Effect:** `status → purchaseOrders` · **Registry evidence label:** local-concept

Choose submitted order (Purchase order*) → Approval note (Approval note)

Approve a submitted purchase order in the local review workflow.
The purchase order is marked Approved locally for conversion or tracking.
**Next:** `convert-purchase-order-to-bill`

### `convert-purchase-order-to-bill` · Convert purchase order to bill

**Route:** `/workflows/convert-purchase-order-to-bill` · **Effect:** `convert → purchaseOrders → bills` · **Registry evidence label:** local-concept

Choose approved order (Purchase order*) → Bill details (Bill date*, Due date, Bill note)

Create a local bill link from an approved purchase order.
A local bill draft is linked to the selected purchase order; no bill was paid.
**Next:** `edit-bill`, `approve-bill`

### `create-bill` · Create bill

**Route:** `/workflows/create-bill` · **Effect:** `create → bills` · **Registry evidence label:** tenant-pattern

Bill header (Bill number or subject*, Vendor*, Bill date*, Due date) → Bill lines (Line items*, Bill total, Notes, Discount amount, Tax rate (%), Shipping / additional charge)

Record a vendor bill with payable dates, amount, and line items.
The vendor bill is saved locally as a draft and is ready for review.
**Next:** `edit-bill`, `approve-bill`, `pay-bill`

### `edit-bill` · Edit bill

**Route:** `/workflows/edit-bill` · **Effect:** `edit → bills` · **Registry evidence label:** local-concept

Choose bill (Bill*) → Update bill (Vendor, Bill date*, Due date, Line items, Bill total, Notes)

Correct a local vendor bill before approval or payment.
The selected bill's local details are updated.
**Next:** `approve-bill`, `pay-bill`

### `approve-bill` · Approve bill

**Route:** `/workflows/approve-bill` · **Effect:** `status → bills` · **Registry evidence label:** local-concept

Choose bill (Bill*) → Approval review (Approval note)

Mark a vendor bill approved in the local payables workflow.
The bill is marked Approved locally and is available for payment or batch selection.
**Next:** `pay-bill`, `create-batch-payment`, `apply-vendor-credit`

### `pay-bill` · Pay bill

**Route:** `/workflows/pay-bill` · **Effect:** `payment → bills → paymentsMade` · **Registry evidence label:** local-concept

Choose bill (Bill*) → Payment allocation (Payment date*, Payment amount*, Bill allocation*, Payment note)

Record a local payment against one vendor bill with an explicit allocation.
A local payment record is linked to the selected bill; no money was transferred.
**Next:** `create-batch-payment`

### `create-batch-payment` · Create batch payment

**Route:** `/workflows/create-batch-payment` · **Effect:** `payment → bills → batchPayments` · **Registry evidence label:** tenant-pattern

Select bills (Bills*) → Batch allocation (Batch reference*, Payment date*, Batch amount*, Bill allocations*, Batch note)

Record one local payment allocation across multiple vendor bills.
The local batch payment and its bill allocations are saved; no money was transferred.
**Next:** `pay-bill`

### `create-vendor-credit` · Create vendor credit

**Route:** `/workflows/create-vendor-credit` · **Effect:** `create → vendorCredits` · **Registry evidence label:** tenant-pattern

Credit header (Credit reference*, Vendor*, Credit date*, Credit amount) → Credit lines (Credit lines, Notes, Discount amount, Tax rate (%), Shipping / additional charge)

Record a credit memo from a vendor for later application to bills.
The vendor credit is saved locally and can be applied to a bill or recorded for refund review.
**Next:** `apply-vendor-credit`, `refund-vendor-credit`

### `apply-vendor-credit` · Apply vendor credit

**Route:** `/workflows/apply-vendor-credit` · **Effect:** `credit → vendorCredits → bills` · **Registry evidence label:** local-concept

Choose credit (Vendor credit*) → Bill allocation (Bill allocations*, Applied amount*, Application note)

Allocate a local vendor credit against one or more vendor bills.
The credit allocation is saved locally against the selected bills.
**Next:** `pay-bill`

### `refund-vendor-credit` · Refund vendor credit

**Route:** `/workflows/refund-vendor-credit` · **Effect:** `credit → vendorCredits → bills` · **Registry evidence label:** local-concept

Choose credit (Vendor credit*) → Refund details (Refund date*, Refund amount*, Credit allocation, Refund note)

Record a local refund request against an unused vendor credit.
The refund intent is recorded locally against the vendor credit; no refund was sent.
**Next:** `create-vendor-credit`

### `create-recurring-bill` · Set up recurring bill

**Route:** `/workflows/create-recurring-bill` · **Effect:** `create → recurringBills` · **Registry evidence label:** tenant-pattern

Profile details (Profile name*, Vendor*, First bill date*, First due date, Frequency*) → Recurring amount (Recurring amount, Line items, Profile note, Discount amount, Tax rate (%), Shipping / additional charge)

Create a local recurring-bill profile for repeat vendor charges.
The recurring-bill profile is saved locally; no bill was generated yet.
**Next:** `generate-recurring-bill`, `pause-recurring-bill`

### `generate-recurring-bill` · Generate recurring bill

**Route:** `/workflows/generate-recurring-bill` · **Effect:** `recurring → recurringBills → bills` · **Registry evidence label:** local-concept

Choose profile (Recurring bill profile*) → Generation date (Bill date*, Generation note)

Generate one local bill from an active recurring-bill profile.
One bill draft is generated locally from the selected recurring profile; no payment was made.
**Next:** `approve-bill`, `pause-recurring-bill`

### `pause-recurring-bill` · Pause recurring bill

**Route:** `/workflows/pause-recurring-bill` · **Effect:** `status → recurringBills → recurringBills` · **Registry evidence label:** local-concept

Choose profile (Recurring bill profile*) → Pause reason (Pause reason*)

Pause local generation from a recurring-bill profile without deleting its history.
The recurring-bill profile is marked paused locally; existing bills are unchanged.
**Next:** `generate-recurring-bill`

### `resume-recurring-bill` · Resume recurring bill

**Route:** `/workflows/resume-recurring-bill` · **Effect:** `status → recurringBills` · **Registry evidence label:** local-concept

Profile (Recurring profile*)

Reactivate a paused profile and continue generating its documents.
The profile is active and can generate its next document on demand.

### `import-vendors` · Import Vendors

**Route:** `/workflows/import-vendors` · **Effect:** `import → vendors` · **Registry evidence label:** local-concept

Source data (CSV data*) → Field mapping (I have reviewed the header mapping and fictional data*)

Map a small fictional CSV batch, review its rows, then add the records to this workspace.
The validated CSV rows were added to the browser-local workspace. Open the linked records to continue their lifecycle.

### `import-expenses` · Import Expenses

**Route:** `/workflows/import-expenses` · **Effect:** `import → expenses` · **Registry evidence label:** local-concept

Source data (CSV data*) → Field mapping (I have reviewed the header mapping and fictional data*)

Map a small fictional CSV batch, review its rows, then add the records to this workspace.
The validated CSV rows were added to the browser-local workspace. Open the linked records to continue their lifecycle.

### `import-bills` · Import Bills

**Route:** `/workflows/import-bills` · **Effect:** `import → bills` · **Registry evidence label:** local-concept

Source data (CSV data*) → Field mapping (I have reviewed the header mapping and fictional data*)

Map a small fictional CSV batch, review its rows, then add the records to this workspace.
The validated CSV rows were added to the browser-local workspace. Open the linked records to continue their lifecycle.

### `import-purchaseOrders` · Import Purchase Orders

**Route:** `/workflows/import-purchaseOrders` · **Effect:** `import → purchaseOrders` · **Registry evidence label:** local-concept

Source data (CSV data*) → Field mapping (I have reviewed the header mapping and fictional data*)

Map a small fictional CSV batch, review its rows, then add the records to this workspace.
The validated CSV rows were added to the browser-local workspace. Open the linked records to continue their lifecycle.

### `import-paymentsMade` · Import Payments Made

**Route:** `/workflows/import-paymentsMade` · **Effect:** `import → paymentsMade` · **Registry evidence label:** local-concept

Source data (CSV data*) → Field mapping (I have reviewed the header mapping and fictional data*)

Map a small fictional CSV batch, review its rows, then add the records to this workspace.
The validated CSV rows were added to the browser-local workspace. Open the linked records to continue their lifecycle.

### `import-vendorCredits` · Import Vendor Credits

**Route:** `/workflows/import-vendorCredits` · **Effect:** `import → vendorCredits` · **Registry evidence label:** local-concept

Source data (CSV data*) → Field mapping (I have reviewed the header mapping and fictional data*)

Map a small fictional CSV batch, review its rows, then add the records to this workspace.
The validated CSV rows were added to the browser-local workspace. Open the linked records to continue their lifecycle.

## Banking

### `create-bank-account` · Add a bank account manually

**Route:** `/workflows/create-bank-account` · **Effect:** `artifact → bankAccounts` · **Registry evidence label:** tenant-pattern

Account details (Account name*, Account type*, Institution, Currency*) → Opening position (Opening balance*, Opening date*, Description)

Create a fictional local bank account without connecting to a provider.
The fictional bank account was saved to the local Banking workspace; no provider or tenant connection was made.
**Next:** `simulate-bank-provider-connection`, `import-bank-statement`

### `create-credit-card-account` · Add a credit-card account manually

**Route:** `/workflows/create-credit-card-account` · **Effect:** `artifact → bankAccounts` · **Registry evidence label:** tenant-pattern

Card details (Account name*, Account type*, Issuer, Card network*, Currency*) → Opening position (Opening balance*, Credit limit, Opening date*, Description)

Create a fictional local credit-card account for statement tracking.
The fictional credit-card account was saved locally for Banking workflows; no card provider was contacted.
**Next:** `simulate-bank-provider-connection`, `import-bank-statement`

### `edit-bank-account` · Edit a bank or credit-card account

**Route:** `/workflows/edit-bank-account` · **Effect:** `artifact → bankAccounts` · **Registry evidence label:** local-concept

Choose account (Account*) → Update details (Account name*, Institution or issuer, Currency*, Description)

Update metadata on an existing local Banking account without changing an external feed.
The selected local Banking account metadata was updated; no provider settings or external balances changed.
**Next:** `import-bank-statement`, `reconcile-bank-account`

### `simulate-bank-provider-connection` · Simulate a bank-provider connection

**Route:** `/workflows/simulate-bank-provider-connection` · **Effect:** `artifact → integrations` · **Registry evidence label:** tenant-pattern

Choose provider (Provider*, Connected account label*, Local bank or card account*) → Confirm simulation (Simulated history window*, I understand this is a local simulation*)

Preview a provider connection state locally without credentials, consent transmission, or network calls.
A local Active integration artifact was recorded for the selected account; no credentials, consent, sync, or network request was used.
**Next:** `import-bank-statement`

### `import-bank-statement` · Import and preview a bank statement CSV

**Route:** `/workflows/import-bank-statement` · **Effect:** `import → bankTransactions` · **Registry evidence label:** local-concept

Choose statement (Bank or credit-card account*, Statement CSV*, First row contains column headings*) → Map columns (Amount sign*, Duplicate handling*, Required date, description, and amount columns are mapped*) → Preview rows (Save the reviewed rows to local transactions*)

Map a fictional CSV to local bank transactions and review the normalized preview before saving it locally.
The mapped CSV preview was saved as local bank-transaction artifacts; no statement was sent to a bank or tenant account.
**Next:** `match-bank-deposit-to-invoice`, `categorize-bank-transaction`, `reconcile-bank-account`

### `create-bank-transaction` · Record a bank transaction manually

**Route:** `/workflows/create-bank-transaction` · **Effect:** `artifact → bankTransactions` · **Registry evidence label:** local-concept

Transaction details (Bank or credit-card account*, Transaction date*, Direction*, Amount*) → Description (Description*, Category)

Add a fictional deposit or withdrawal directly to a local bank account.
The fictional transaction was recorded in the local Banking workspace and remains available for matching or categorization.
**Next:** `match-bank-deposit-to-invoice`, `categorize-bank-transaction`

### `edit-bank-transaction` · Edit a bank transaction

**Route:** `/workflows/edit-bank-transaction` · **Effect:** `artifact → bankTransactions` · **Registry evidence label:** local-concept

Choose transaction (Bank transaction*) → Correct details (Transaction date*, Description*, Direction*, Amount*, Category)

Correct local transaction details before or after review without contacting a bank.
The selected local bank transaction was corrected; no bank ledger or external statement was changed.
**Next:** `match-bank-deposit-to-invoice`, `categorize-bank-transaction`

### `match-bank-deposit-to-invoice` · Match a bank deposit to an invoice

**Route:** `/workflows/match-bank-deposit-to-invoice` · **Effect:** `bank-match → invoices` · **Registry evidence label:** local-concept

Choose deposit (Bank transaction*) → Choose invoices (Invoice*, Invoice allocations*, Matching note)

Link a local deposit to one or more fictional customer invoices with an explicit allocation.
The local bank transaction was linked to the selected invoice allocation; no payment was sent or posted externally.
**Next:** `undo-bank-match`, `reconcile-bank-account`

### `match-bank-withdrawal-to-bill` · Match a bank withdrawal to a bill

**Route:** `/workflows/match-bank-withdrawal-to-bill` · **Effect:** `bank-match → bills` · **Registry evidence label:** local-concept

Choose withdrawal (Bank transaction*) → Choose bills (Bill*, Bill allocations*, Matching note)

Link a local withdrawal to one or more fictional vendor bills with an explicit allocation.
The local bank transaction was linked to the selected bill allocation; no vendor payment was sent or posted externally.
**Next:** `undo-bank-match`, `reconcile-bank-account`

### `undo-bank-match` · Undo a bank transaction match

**Route:** `/workflows/undo-bank-match` · **Effect:** `bank-match → bankTransactions` · **Registry evidence label:** local-concept

Choose matched transaction (Matched bank transaction*) → Confirm undo (Reason*, Remove the local match*)

Remove a local match while leaving the underlying bank transaction and invoice or bill records intact.
The local match link was removed while the bank transaction and source records remained available for review.
**Next:** `match-bank-deposit-to-invoice`, `categorize-bank-transaction`

### `categorize-bank-transaction` · Categorize a bank transaction

**Route:** `/workflows/categorize-bank-transaction` · **Effect:** `bank-categorize → bankTransactions` · **Registry evidence label:** local-concept

Choose transaction (Bank transaction*) → Choose category (Category*, Categorization note)

Assign a local category to an unmatched bank transaction.
The selected local bank transaction received the chosen category; no journal or external bank entry was posted.
**Next:** `uncategorize-bank-transaction`, `reconcile-bank-account`

### `uncategorize-bank-transaction` · Uncategorize a bank transaction

**Route:** `/workflows/uncategorize-bank-transaction` · **Effect:** `bank-categorize → bankTransactions` · **Registry evidence label:** local-concept

Choose categorized transaction (Bank transaction*) → Confirm uncategorize (Reason*, Clear the category*)

Clear a local category so a bank transaction can be reviewed again.
The local transaction category was cleared and the transaction is ready for another review; no external entry changed.
**Next:** `categorize-bank-transaction`, `match-bank-deposit-to-invoice`

### `create-bank-rule` · Create a bank transaction rule

**Route:** `/workflows/create-bank-rule` · **Effect:** `artifact → bankRules` · **Registry evidence label:** local-concept

Rule identity (Rule name*, Bank or credit-card account*, Transaction direction*) → Conditions and result (Description contains*, Category*, Rule is active*)

Save a local categorization rule for future fictional bank transactions.
The fictional bank rule was saved locally and can be reviewed before applying it to local transactions.
**Next:** `edit-bank-rule`, `categorize-bank-transaction`

### `edit-bank-rule` · Edit a bank transaction rule

**Route:** `/workflows/edit-bank-rule` · **Effect:** `artifact → bankRules` · **Registry evidence label:** local-concept

Choose rule (Bank rule*) → Update rule (Rule name*, Description contains*, Category*, Rule is active*)

Update a local bank rule without executing it against transactions.
The selected local bank rule was updated; it was not run automatically or connected to a live feed.
**Next:** `categorize-bank-transaction`

### `reconcile-bank-account` · Reconcile a bank account

**Route:** `/workflows/reconcile-bank-account` · **Effect:** `bank-reconcile → reconciliations` · **Registry evidence label:** local-concept

Choose statement (Bank or credit-card account*, Statement ending date*, Statement balance*) → Select transactions (Included transactions*, Difference note) → Confirm reconciliation (Confirm this local reconciliation*)

Compare a local account statement balance with selected local transactions and record a reconciliation artifact.
A local reconciliation artifact was recorded with the selected account, statement balance, and transaction allocations; no bank balance was changed.
**Next:** `undo-bank-reconciliation`

### `undo-bank-reconciliation` · Undo a bank reconciliation

**Route:** `/workflows/undo-bank-reconciliation` · **Effect:** `bank-reconcile → reconciliations` · **Registry evidence label:** local-concept

Choose reconciliation (Reconciliation*) → Confirm undo (Reason*, Undo the local reconciliation*)

Reverse a local reconciliation artifact while retaining the underlying account and transaction records.
The local reconciliation was marked undone; the account, statement source, and bank transactions remain available for review.
**Next:** `reconcile-bank-account`

## Accountant

### `create-account` · Create account

**Route:** `/workflows/create-account` · **Effect:** `artifact → accounts` · **Registry evidence label:** local-concept

Account details (Account name*, Account code*, Account type*) → Account settings (Description)

Add a local chart-of-accounts entry with its classification and starting details.
The account was added to the local chart of accounts and is ready to use in this workspace.
**Next:** `edit-account`, `set-opening-balances`

### `edit-account` · Edit account

**Route:** `/workflows/edit-account` · **Effect:** `artifact → accounts` · **Registry evidence label:** local-concept

Choose account (Account*) → Update details (Account name*, Account code*, Account type*, Description)

Update the name, code, classification, or description of a local account.
The selected local account now uses the revised details.
**Next:** `disable-account`, `set-opening-balances`

### `disable-account` · Disable account

**Route:** `/workflows/disable-account` · **Effect:** `artifact-status → accounts` · **Registry evidence label:** local-concept

Choose account (Account*) → Reason (Reason*)

Remove an account from active local selection without deleting its history.
The selected account is marked disabled in the local chart of accounts; its existing history remains available.
**Next:** `create-account`, `edit-account`

### `set-opening-balances` · Set opening balances

**Route:** `/workflows/set-opening-balances` · **Effect:** `journal → journals` · **Registry evidence label:** local-concept

Opening entry (Opening date*, Description*) → Balance accounts (Opening balance lines*)

Record a balanced opening journal for the selected accounts.
The balanced opening journal was recorded in the local journal workspace; no external ledger was updated.
**Next:** `create-journal`, `publish-journal`

### `create-journal` · Create journal

**Route:** `/workflows/create-journal` · **Effect:** `journal → journals` · **Registry evidence label:** tenant-pattern

Journal details (Journal date*, Journal number*, Reporting method) → Journal lines (Journal lines*) → Notes (Notes)

Draft a balanced manual journal with account-level debit and credit lines.
The balanced journal draft was saved in the local workspace and is ready for publishing.
**Next:** `publish-journal`, `reverse-journal`

### `publish-journal` · Publish journal

**Route:** `/workflows/publish-journal` · **Effect:** `status → journals` · **Registry evidence label:** tenant-pattern

Choose journal (Journal*) → Publishing check (Publish date*, I confirm this balanced journal*)

Publish a balanced local journal after confirming the selected entry.
The selected balanced journal is marked Recorded in the local workspace; no external posting occurred.
**Next:** `reverse-journal`

### `reverse-journal` · Reverse journal

**Route:** `/workflows/reverse-journal` · **Effect:** `journal → journals` · **Registry evidence label:** tenant-pattern

Choose journal (Journal*, Reversal date*) → Reversal lines (Reversal journal lines*, Reason*)

Create a balanced local reversal entry for a previously recorded journal.
A balanced reversal journal was saved locally and linked to the selected journal; no external ledger was changed.
**Next:** `publish-journal`

### `create-budget` · Create budget

**Route:** `/workflows/create-budget` · **Effect:** `create → budgets` · **Registry evidence label:** local-concept

Budget details (Budget name*, Start date*, End date*) → Plan amount (Category*, Planned amount*, Notes)

Set up a local budget with a period, category, and planned amount.
The budget was saved in the local workspace with its period, category, and planned amount.
**Next:** `create-journal`, `lock-transactions`

### `lock-transactions` · Lock accountant transactions

**Route:** `/workflows/lock-transactions` · **Effect:** `artifact → settings` · **Registry evidence label:** tenant-pattern

Lock scope (Lock date*, Module*) → Lock reason (Reason*, I confirm this period can be locked*)

Set a local accounting lock date to prevent changes before a completed period.
The Accountant lock setting was saved locally with the selected cutoff date; no tenant settings were changed.
**Next:** `unlock-transactions`

### `unlock-transactions` · Unlock accountant transactions

**Route:** `/workflows/unlock-transactions` · **Effect:** `artifact → settings` · **Registry evidence label:** tenant-pattern

Unlock scope (Module*, I confirm this period can be unlocked*) → Unlock reason (Reason*)

Remove a local Accountant transaction lock after recording the reason.
The Accountant unlock decision was saved locally; no external or tenant lock was changed.
**Next:** `lock-transactions`

### `configure-taxes` · Configure taxes

**Route:** `/workflows/configure-taxes` · **Effect:** `artifact → settings` · **Registry evidence label:** local-concept

Tax definition (Tax name*, Rate (%)*, Tax treatment*) → Tax timing (Effective date*, Description)

Save a local tax configuration for use in accounting entries.
The tax name, rate, treatment, and effective date were saved as a local setting; no tax authority or tenant setting was contacted.
**Next:** `configure-currencies`, `create-journal`

### `configure-currencies` · Configure currencies

**Route:** `/workflows/configure-currencies` · **Effect:** `artifact → settings` · **Registry evidence label:** local-concept

Base currency (Base currency*, Exchange-rate mode*) → Display preferences (Decimal places*, Notes)

Set local base-currency and exchange-display preferences without connecting to a provider.
The local base currency and display preferences were saved; no live exchange rates or external integration were used.
**Next:** `configure-taxes`, `create-journal`

### `import-journals` · Import Manual Journals

**Route:** `/workflows/import-journals` · **Effect:** `import → journals` · **Registry evidence label:** local-concept

Source data (CSV data*) → Field mapping (I have reviewed the header mapping and fictional data*)

Map a small fictional CSV batch, review its rows, then add the records to this workspace.
The validated CSV rows were added to the browser-local workspace. Open the linked records to continue their lifecycle.

### `import-budgets` · Import Budgets

**Route:** `/workflows/import-budgets` · **Effect:** `import → budgets` · **Registry evidence label:** local-concept

Source data (CSV data*) → Field mapping (I have reviewed the header mapping and fictional data*)

Map a small fictional CSV batch, review its rows, then add the records to this workspace.
The validated CSV rows were added to the browser-local workspace. Open the linked records to continue their lifecycle.

## Items & Projects

### `create-item` · Create item

**Route:** `/workflows/create-item` · **Effect:** `create → items` · **Registry evidence label:** tenant-pattern

Item details (Item name*, Item type*, Description) → Pricing and stock (Unit rate*, Opening quantity, Opening stock note) → Reporting (Reporting tag, Available for selection)

Add a local inventory or service item with its selling details and reporting category.
The item was added to this local workspace with its price, opening quantity, and reporting label.
**Next:** `edit-item`, `adjust-item-stock`

### `edit-item` · Edit item

**Route:** `/workflows/edit-item` · **Effect:** `edit → items` · **Registry evidence label:** local-concept

Choose item (Item*) → Update details (Item name*, Item type*, Description, Unit rate) → Reporting label (Reporting tag)

Update an existing local item without changing its stock history.
The selected local item was updated; its prior stock movements remain unchanged.
**Next:** `adjust-item-stock`

### `adjust-item-stock` · Adjust item stock

**Route:** `/workflows/adjust-item-stock` · **Effect:** `inventory → items` · **Registry evidence label:** local-concept

Select item (Item*) → Adjustment (Quantity delta*, Reason*, Adjustment date*)

Record a positive or negative local quantity adjustment with an auditable reason.
The signed quantity change and reason were recorded in the local item history.
**Next:** `generate-inventory-report`

### `create-project` · Create project

**Route:** `/workflows/create-project` · **Effect:** `create → projects` · **Registry evidence label:** tenant-pattern

Project setup (Project name*, Customer, Description) → Dates and budget (Start date*, Target date, Project budget) → Project category (Category)

Start a local project with a customer, dates, budget and working description.
The project was created in the local workspace and is ready for tasks and time entries.
**Next:** `create-task`, `create-time-entry`

### `edit-project` · Edit project

**Route:** `/workflows/edit-project` · **Effect:** `edit → projects` · **Registry evidence label:** local-concept

Choose project (Project*) → Project details (Project name*, Customer, Description) → Dates and budget (Start date, Target date, Project budget, Category)

Revise the local customer, dates, budget or description for a project.
The selected project details were updated locally; existing time entries remain linked.
**Next:** `create-task`, `create-time-entry`

### `create-task` · Create project task

**Route:** `/workflows/create-task` · **Effect:** `artifact → tasks` · **Registry evidence label:** local-concept

Task details (Project*, Task name*, Task description) → Schedule and estimate (Start date, Due date, Estimated amount, Work type)

Add a local task to a project with an owner, due date and billable estimate.
The task was added to the local project task list and can receive time entries.
**Next:** `create-time-entry`, `start-timer`

### `edit-task` · Edit project task

**Route:** `/workflows/edit-task` · **Effect:** `artifact → tasks` · **Registry evidence label:** local-concept

Choose task (Task*) → Task details (Project*, Task name*, Task description, Work type) → Schedule (Start date, Due date, Estimated amount)

Update an existing local task while keeping its project link and logged time.
The selected local task was updated without removing its existing time history.
**Next:** `create-time-entry`, `start-timer`

### `create-time-entry` · Log time entry

**Route:** `/workflows/create-time-entry` · **Effect:** `create → timesheets` · **Registry evidence label:** tenant-pattern

Work context (Project*, Task, Customer) → Time details (Work date*, Hours*, Hourly rate, Work note*) → Billing choice (Time type*, Include in invoice candidates)

Record billable or non-billable time against a local project and task.
The time entry was saved locally against the selected project and is available for invoice selection.
**Next:** `invoice-project-time`, `start-timer`

### `start-timer` · Start timer simulation

**Route:** `/workflows/start-timer` · **Effect:** `artifact → tasks` · **Registry evidence label:** tenant-pattern

Timer context (Project*, Task) → Timer note (Work note, Time type*)

Start a local timer simulation for a project task; no device timer or external service is used.
A local running-timer marker was stored for the selected project task; no real timer was started.
**Next:** `stop-timer`

### `stop-timer` · Stop timer simulation

**Route:** `/workflows/stop-timer` · **Effect:** `artifact-status → tasks` · **Registry evidence label:** local-concept

Choose running timer (Running task timer*) → Capture elapsed work (Elapsed hours*, Work date*, Work note, Include in invoice candidates)

Stop a local timer marker and optionally turn its elapsed duration into a time entry.
The local timer marker was stopped and its elapsed details were recorded without starting a real clock.
**Next:** `create-time-entry`, `invoice-project-time`

### `invoice-project-time` · Convert project time to invoice

**Route:** `/workflows/invoice-project-time` · **Effect:** `time-invoice → timesheets → invoices` · **Registry evidence label:** local-concept

Choose customer (Customer*) → Select time records (Time records*, Invoice date*, Due date) → Invoice note (Invoice description)

Select local timesheet records and stage them as invoice line items.
The selected local time records were linked to a new invoice draft for review; nothing was sent.
**Next:** `create-time-entry`

### `close-project` · Close project

**Route:** `/workflows/close-project` · **Effect:** `status → projects → projects` · **Registry evidence label:** local-concept

Choose project (Project*) → Closeout note (Closeout date*, Closeout note*, Confirm project close*)

Mark a local project closed after its remaining work and invoice review are complete.
The project was marked closed in the local workspace with its closeout note; historical records remain available.
**Next:** `generate-inventory-report`

### `add-project-reporting-tags` · Set project reporting tags

**Route:** `/workflows/add-project-reporting-tags` · **Effect:** `artifact → settings` · **Registry evidence label:** local-concept

Choose project (Project*) → Reporting labels (Reporting tags*, Project reporting group)

Attach local reporting labels to a project for grouped views and exports.
The selected project’s reporting labels were saved locally for later grouping; no tenant settings changed.
**Next:** `edit-project`, `generate-inventory-report`

### `generate-inventory-report` · Generate inventory report

**Route:** `/workflows/generate-inventory-report` · **Effect:** `artifact → reports` · **Registry evidence label:** local-concept

Report window (As-of date*, Item category) → Report labels (Report name*, Reporting tag filter)

Create a local inventory snapshot using item category and stock filters.
A local inventory report snapshot was created from current workspace item data; no external report was run or shared.
**Next:** `adjust-item-stock`, `add-project-reporting-tags`

### `import-items` · Import items

**Route:** `/workflows/import-items` · **Effect:** `import → items → items` · **Registry evidence label:** local-concept

Choose CSV (Items CSV*) → Import mapping (Import name*, Default item type, Import note)

Load item rows from a local CSV preview and map them into the item collection.
The CSV was staged for local item import and mapping review; no external system was contacted.
**Next:** `create-item`, `generate-inventory-report`

### `import-projects` · Import Projects

**Route:** `/workflows/import-projects` · **Effect:** `import → projects` · **Registry evidence label:** local-concept

Source data (CSV data*) → Field mapping (I have reviewed the header mapping and fictional data*)

Map a small fictional CSV batch, review its rows, then add the records to this workspace.
The validated CSV rows were added to the browser-local workspace. Open the linked records to continue their lifecycle.

### `import-timesheets` · Import Timesheets

**Route:** `/workflows/import-timesheets` · **Effect:** `import → timesheets` · **Registry evidence label:** local-concept

Source data (CSV data*) → Field mapping (I have reviewed the header mapping and fictional data*)

Map a small fictional CSV batch, review its rows, then add the records to this workspace.
The validated CSV rows were added to the browser-local workspace. Open the linked records to continue their lifecycle.

## Settings & Reports

### `settings-organization` · Update organization profile

**Route:** `/workflows/settings-organization` · **Effect:** `artifact → settings` · **Registry evidence label:** local-concept

Organization details (Organization name*, Legal name, Organization email*, Organization phone) → Address and locale (Display address, Country*, Base currency*, Time zone*)

Maintain the local organization identity and contact details used across the workspace.
The organization profile is saved locally and will be used for future document previews.
**Next:** `settings-branding`, `settings-general-preferences`

### `settings-branding` · Configure organization branding

**Route:** `/workflows/settings-branding` · **Effect:** `artifact → settings` · **Registry evidence label:** local-concept

Brand assets (Logo, Primary color*, Accent color) → Document presentation (Logo placement*, Show organization footer, Footer text)

Set the local logo, colors, and footer treatment for generated document previews.
Branding preferences are saved locally; no logo or document was sent externally.
**Next:** `create-template`, `settings-organization`

### `settings-user-access` · Add a workspace user

**Route:** `/workflows/settings-user-access` · **Effect:** `artifact → settings` · **Registry evidence label:** local-concept

User identity (Full name*, Email*, Phone) → Access and notifications (Role*, Show notification preference, Local status*)

Create a local user access entry with a role and notification preference.
The user and selected role are stored as a local access entry; no invitation was sent.
**Next:** `settings-role-access`, `settings-general-preferences`

### `settings-role-access` · Define a role permission set

**Route:** `/workflows/settings-role-access` · **Effect:** `artifact → settings` · **Registry evidence label:** local-concept

Role details (Role name*, Role description*) → Permissions (Permissions*, Restrict report visibility)

Create a local role with explicit module permissions for workspace demonstrations.
The role definition is stored locally and can be selected for future user entries.
**Next:** `settings-user-access`

### `settings-general-preferences` · Update general preferences

**Route:** `/workflows/settings-general-preferences` · **Effect:** `artifact → settings` · **Registry evidence label:** tenant-pattern

Workspace preferences (Work week*, Enable projects, Enable time tracking, Use secure public-link previews) → Transaction presentation (Tax mode*, Rounding*, Show discount fields, Show salesperson fields)

Configure local work-week, invoice, tax, link, and module display preferences.
General preferences are saved locally and will shape later workflow previews.
**Next:** `settings-number-series`, `settings-terms`

### `settings-number-series` · Configure number series

**Route:** `/workflows/settings-number-series` · **Effect:** `artifact → settings` · **Registry evidence label:** local-concept

Series selection (Document series*, Prefix*) → Sequence rules (Next number*, Number padding*, Reset sequence each year)

Set the local prefixes and next numbers used by document examples.
The selected number series is stored locally for future document drafts.
**Next:** `create-template`, `settings-terms`

### `settings-terms` · Define payment terms

**Route:** `/workflows/settings-terms` · **Effect:** `artifact → settings` · **Registry evidence label:** local-concept

Term details (Term name*, Days until due*, Due-date rule*) → Availability (Available for sales, Available for purchases, Use as local default)

Create a reusable local payment-term option for transaction forms.
The payment term is stored locally and is available to later transaction drafts.
**Next:** `settings-reminders`, `create-template`

### `settings-reminders` · Configure payment reminders

**Route:** `/workflows/settings-reminders` · **Effect:** `artifact → settings` · **Registry evidence label:** local-concept

Reminder stages (Enable reminder timeline, Days before due*, Days after due*) → Reminder content (Reminder subject*, Reminder message*, Preview channel*)

Set local reminder stages and preview wording without sending messages.
Reminder stages and preview copy are saved locally; no reminder was scheduled or sent.
**Next:** `settings-terms`, `schedule-report`

### `create-template` · Create a document template

**Route:** `/workflows/create-template` · **Effect:** `artifact → templates` · **Registry evidence label:** tenant-pattern

Template identity (Template name*, Template type*, Layout*) → Template content (Branding profile, Visible sections*, Footer text)

Create a local template draft for invoices, quotes, bills, or other document previews.
The template draft is stored locally and can be edited or made the local default.
**Next:** `edit-template`, `set-default-template`

### `edit-template` · Edit a document template

**Route:** `/workflows/edit-template` · **Effect:** `artifact → templates` · **Registry evidence label:** tenant-pattern

Choose template (Template*, Layout*) → Update presentation (Visible sections*, Footer text, Show organization logo)

Update an existing local template's layout and displayed sections.
The selected local template now uses the revised preview settings.
**Next:** `set-default-template`, `create-template`

### `set-default-template` · Set a default document template

**Route:** `/workflows/set-default-template` · **Effect:** `artifact-status → templates` · **Registry evidence label:** tenant-pattern

Template family (Template type*) → Default selection (Template*, Mark as default*)

Choose the local default template for one document family.
The selected local template is marked as the default for that document family.
**Next:** `edit-template`, `create-template`

### `create-folder` · Create a document folder

**Route:** `/workflows/create-folder` · **Effect:** `artifact → folders` · **Registry evidence label:** tenant-pattern

Folder details (Folder name*, Folder type*) → Folder notes (Parent folder, Description)

Add a local folder for organizing uploaded document examples.
The folder is created in the local document index; no files were moved or uploaded.
**Next:** `upload-document`, `process-receipt`

### `upload-document` · Add a document to the inbox

**Route:** `/workflows/upload-document` · **Effect:** `artifact → documents` · **Registry evidence label:** tenant-pattern

Document file (Document file*, Document name*, Document type*) → Organization (Folder, Notes)

Store a local document example in the Documents index and optionally place it in a folder.
The document example is stored in the local Documents index; no OCR or external upload ran.
**Next:** `process-receipt`, `create-folder`

### `process-receipt` · Process a receipt document

**Route:** `/workflows/process-receipt` · **Effect:** `document-process → expenses` · **Registry evidence label:** local-concept

Receipt selection (Receipt document*, Create local draft*) → Review extracted values (Merchant or vendor*, Receipt date*, Amount*, Category)

Turn a local receipt document into a reviewable expense or bill draft.
The selected receipt is linked to a local expense draft for review; no OCR service or posting ran.
**Next:** `upload-document`, `create-folder`

### `save-report-filter` · Save a filtered report

**Route:** `/workflows/save-report-filter` · **Effect:** `artifact → reports` · **Registry evidence label:** local-concept

Report definition (Saved report name*, Report*, Accounting basis*) → Filters (From date*, To date*, Additional filters)

Save a report definition with local filters for repeat viewing.
The filtered report definition is stored locally under Saved Reports; it was not run against an external service.
**Next:** `share-report`, `schedule-report`

### `share-report` · Share a saved report

**Route:** `/workflows/share-report` · **Effect:** `artifact → reportShares` · **Registry evidence label:** local-concept

Report selection (Saved report*, Recipient emails*) → Access level (Access*, Include filter details, Share note)

Create a local report-share entry for selected workspace recipients.
The report-share entry is stored locally; no email, link, or external permission was issued.
**Next:** `save-report-filter`, `schedule-report`

### `schedule-report` · Schedule a report preview

**Route:** `/workflows/schedule-report` · **Effect:** `artifact → reportSchedules` · **Registry evidence label:** local-concept

Schedule target (Saved report*, Recipient email*, Preview format*) → Timing (Frequency*, Day*, Mark schedule active)

Store a local recurring report schedule without creating a background job or sending a report.
The schedule metadata is stored locally for preview; no scheduler was started and no report was delivered.
**Next:** `save-report-filter`, `share-report`

### `create-automation-rule` · Create an automation rule

**Route:** `/workflows/create-automation-rule` · **Effect:** `artifact → automationRules` · **Registry evidence label:** tenant-pattern

Rule trigger (Rule name*, Module*, When*) → Criteria (Criteria*, Rule active in local simulation)

Define a local workflow rule with criteria and an active state for safe simulation.
The rule is stored locally and can be tested with sample values; no webhook, email, or function ran.
**Next:** `create-automation-action`, `test-automation-rule`

### `create-automation-action` · Add an automation action

**Route:** `/workflows/create-automation-action` · **Effect:** `artifact → automationActions` · **Registry evidence label:** tenant-pattern

Rule and action (Automation rule*, Action*) → Action details (Action title*, Message or field update*, Local target*)

Attach a safe local action description to an existing automation rule.
The action definition is stored locally; it will only be represented in a local test result.
**Next:** `test-automation-rule`, `view-automation-log`

### `test-automation-rule` · Run a local automation test

**Route:** `/workflows/test-automation-rule` · **Effect:** `automation-test → automationRules` · **Registry evidence label:** local-concept

Rule and sample record (Automation rule*, Sample record name*, Sample status*) → Expected outcome (Sample amount*, Expected outcome*, Show eligible actions)

Evaluate a rule's configured criteria against sample values without executing custom code or network actions.
The rule was evaluated locally against the sample values; no custom code, network call, or external action ran.
**Next:** `view-automation-log`, `create-automation-action`

### `view-automation-log` · Review an automation log

**Route:** `/workflows/view-automation-log` · **Effect:** `artifact → automationLogs` · **Registry evidence label:** tenant-pattern

Log selection (Automation rule*, Outcome*) → Review note (Log details*, Next action*)

Inspect a local automation test log and record a follow-up note.
The local review entry is saved in Automation Logs; no workflow action was dispatched.
**Next:** `test-automation-rule`, `create-automation-rule`

### `setup-integration` · Simulate an integration setup

**Route:** `/workflows/setup-integration` · **Effect:** `artifact → integrations` · **Registry evidence label:** local-concept

Integration choice (Integration*, Connection name*, Simulation mode*) → Scope and safeguards (Requested scopes*, Enabled for local preview, Setup notes)

Record a local integration configuration without authenticating, connecting, or calling a provider.
The integration configuration is stored locally in disabled simulation mode; no credentials or network connection were used.
**Next:** `settings-general-preferences`, `view-automation-log`
