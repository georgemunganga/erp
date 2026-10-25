# Purchases workflow depth

**Domain:** Purchases
**Definition:** [`src/workflows/domains/purchases.ts`](../../src/workflows/domains/purchases.ts)

These flows operate only on fictional local workspace records. They do not send vendor messages, move money, schedule jobs, call Zoho/API services, or change tenant data. The shared renderer adds review, back, cancel, and completion stages around the operational steps below.

| Flow id | Steps | Validation | Persisted / linked result | Evidence |
|---|---|---|---|---|
| `create-vendor` | Vendor identity; vendor notes | Vendor name required; email uses email input | Creates a local `vendors` record with contact details and notes | local-concept |
| `edit-vendor` | Choose vendor; update details | Existing `vendors` record selected; name required | Updates the selected vendor record | local-concept |
| `create-expense` | Expense details; billable context | Vendor/date/amount/category required; amount > 0 | Creates local `expenses` record, including billable choice | tenant-pattern: Vendors and Expenses list entry affordances were observed; values are fictional |
| `edit-expense` | Choose expense; update expense | Existing `expenses` record selected; amount > 0 | Updates vendor, date, category, amount, note, and billable flag | local-concept |
| `convert-expense-to-billable` | Choose billable expense; invoice context | Existing expense selected; customer required | Converts/links `expenses` to a local `invoices` draft; no invoice is sent | local-concept |
| `create-purchase-order` | Order header; order lines | Vendor/order date/order reference required; lines available | Creates a local `purchaseOrders` draft | tenant-pattern: Purchase Orders list create affordance and lifecycle entry were observed; blank values only |
| `submit-purchase-order` | Choose order; submission check | Existing `purchaseOrders` record selected | Sets the selected purchase order to known `Sent` status locally; no vendor delivery | local-concept |
| `approve-purchase-order` | Choose submitted order; approval note | Existing `purchaseOrders` record selected | Sets the selected purchase order to known `Approved` status locally | local-concept |
| `convert-purchase-order-to-bill` | Choose approved order; bill details | Existing purchase order selected; bill date required | Converts `purchaseOrders` to a local `bills` draft link | local-concept |
| `create-bill` | Bill header; bill lines | Vendor/bill date/reference/amount required; amount > 0 | Creates local `bills` draft with payable lines | tenant-pattern: Blank bill form was observed with vendor, dates, payable lines, notes, and draft actions; no tenant values copied |
| `edit-bill` | Choose bill; update bill | Existing `bills` record selected; amount > 0 | Updates the selected bill locally | local-concept |
| `approve-bill` | Choose bill; approval review | Existing `bills` record selected | Sets the bill to known `Approved` status locally | local-concept |
| `pay-bill` | Choose bill; payment allocation | Existing bill selected; payment amount positive; `allocations` targets `bills` | Creates a local `paymentsMade` record linked to the bill; no money transferred | local-concept |
| `create-batch-payment` | Select bills; batch allocation | One or more bills selected; positive amount; bill allocations required | Creates a local batch-style `paymentsMade` record with multiple bill allocations; no money transferred | tenant-pattern: Batch Payments list new affordance and multiple-bill description were observed; payment was not initiated |
| `create-vendor-credit` | Credit header; credit lines | Vendor/date/reference/amount required; amount > 0 | Creates a local `vendorCredits` record | tenant-pattern: Vendor Credits list create/import affordances and lifecycle education were observed; no credit was applied |
| `apply-vendor-credit` | Choose credit; bill allocation | Existing `vendorCredits` record selected; bill allocations required; positive amount | Links the credit to selected local `bills` allocations | local-concept; allocation behavior was not verified in tenant |
| `refund-vendor-credit` | Choose credit; refund details | Existing credit selected; positive refund amount; optional bill allocation | Records a local refund intent against the credit; no refund sent | local-concept; refund behavior was not verified in tenant |
| `create-recurring-bill` | Profile details; recurring amount | Vendor/first date/frequency/amount required; amount > 0 | Creates a local `recurringBills` profile; does not generate a bill | tenant-pattern: Recurring Bills list create/import affordances and profile explanation were observed; no profile was saved |
| `generate-recurring-bill` | Choose profile; generation date | Existing recurring profile selected; bill date required | Generates one local `bills` draft from the profile; no payment made | local-concept; generation behavior was not verified in tenant |
| `pause-recurring-bill` | Choose profile; pause reason | Existing recurring profile selected; pause reason required | Marks the recurring profile paused through the local recurring action; existing bills remain unchanged | local-concept; pause behavior was not verified in tenant |

## Contract notes

- Every definition has `domain: "Purchases"`, a stable kebab-case id, 2 operational entry steps, a contract-defined effect kind, a completion title/message, and optional next-flow links.
- `record` and `multi-record` fields select from the effect entity's collection where required. Payment amounts are positive and payment/credit flows use `allocations` against `bills`.
- Status effects use only known `DemoStatus` values (`Sent` and `Approved`). Recurring setup uses `create`, generation uses the profile-selecting `recurring` effect, and pause uses `artifact-status` with the local `paused` action rather than inventing a typed status.
- Completion text describes local persistence or linking only; it never claims delivery, payment, refund, scheduling, or external synchronization.
