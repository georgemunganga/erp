# Sales and receivables

> Evidence status (2026-10-20): The selected Global organization reached the Books dashboard and authenticated reference exploration has begun. The candidate workflows below remain public-document research, not tenant proof; observed screens and action boundaries are in [verified patterns](../verified-screen-patterns.md) and [interaction coverage](../interaction-coverage-plan.md). No tenant accounting record was created or changed; a temporary report Favorite toggle was reversed and verified empty.

Public help pages remain candidate evidence only and do not establish this Global tenant’s UI or data. The later tenant observations are recorded separately; they do not verify every sales lifecycle or consequential action.

## Source-supported candidate workflows

- **Create and send an invoice:** Sales → Invoices → **+ New** opens an invoice form with customer, dates/terms, items and totals; the documented outcomes include saving a draft or sending, and the invoice can also be scheduled for email. Invoice entry can mark payment received at creation, or a payment can be recorded later. [1]
- **Recurring invoices:** Zoho describes recurring invoices as invoices on a selected frequency, with examples including weekly and monthly. The source does not document the recurring-profile management steps in detail, so treat those screens and lifecycle actions as unverified. [1]
- **Quotes/estimates and portal response:** The customer portal lists quotes, shows quote details, and lets customers accept or decline; comments support negotiation. The organization can respond to portal comments, edit a declined quote and submit it again. [2]
- **Customer portal access:** From Sales → Customers, a customer’s portal access can be configured from the customer detail area. The customer needs an email address; saving sends an invitation. This is a write action, not a capture step. [2]
- **Portal receivables:** Customers can view invoices and recorded payments. Online invoice payment requires a payment gateway associated with the invoice; partial payment is available only when enabled for that invoice. The portal also presents outstanding invoices and available credits. [2]

## Shared screen families

Likely reusable families for later planning are Sales list/detail pages; customer detail and portal-access settings; transaction forms and document detail/preview; and customer-portal dashboard, quote, invoice, payment-receipt, and sales-order views. These are documentation-derived families, not verified Global screenshots. [1] [2]

## Capture boundary and dependencies

Keep any eventual capture read-only: observe visible screens only after the dashboard-ready signal; do not create, edit, delete, send, invite, approve, record a payment, or change settings. Avoid exposing customer-identifying or payment information in any capture. Portal access depends on a customer email; online payment depends on a configured gateway, and sales-order visibility in the portal depends on a portal preference. Edition and plan entitlements, payment methods, and actual Global-tenant configuration remain unverified. [2]

## Unverified candidates

Standalone customer creation/maintenance, sales-order creation or fulfillment, the detailed creation/application/refund flow for credit notes, and recurring-invoice profile setup or auto-charge are listed candidate scope but are not established here as end-to-end workflows. The invoice documentation says invoices may be set recurring and credit notes may be created, while the portal documentation mentions sales orders and available credits; those references do not establish the missing workflow screens. [1] [2]

## References

[1]: https://www.zoho.com/books/help/invoice/ "Invoices | Help | Zoho Books"
[2]: https://www.zoho.com/books/help/customer-portal/ "Customer Portal | Help | Zoho Books"
