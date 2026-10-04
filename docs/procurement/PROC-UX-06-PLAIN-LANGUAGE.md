# PROC-UX-06 — Plain language for Procurement

**Status:** UI copy standard for the Procurement frontend.  
**Purpose:** Help employees, managers, buyers, and vendors understand what to do without learning internal ERP terms.

## Writing rules

1. Name the thing the user is working with: **request**, **quote**, **order**, **delivery**, **bill**, or **vendor**.
2. Use action buttons that say what happens next: **Ask for quotes**, **Start order**, **Approve**, **Return for changes**, **Send to Finance**.
3. Put the next action and its reason in empty states, errors, and blocked states.
4. Keep status names short. Explain special rules beside the relevant action.
5. Preserve legal, tax, and accounting terms where accuracy requires them, with a short explanation nearby.
6. Keep stored status values, API fields, audit events, and permission keys stable; presentation labels can be simpler.

## Preferred screen terms

| Internal term | User-facing term |
| --- | --- |
| Purchase requisition | Purchase request |
| Requisition line | Item in a request |
| RFQ / sourcing event | Request for quotes / Get quotes |
| Bid evaluation | Compare quotes |
| Award | Choose a vendor |
| Purchase order / PO | Order (show PO number in details) |
| Goods receipt / GRN | Delivery received (show GRN number in details) |
| Service acceptance | Confirm service completed |
| Supplier invoice | Bill |
| Invoice exception | Bill needing review |
| AP handoff | Send to Finance |
| Budget reservation / encumbrance | Money set aside |
| Contract utilization | Contract spending |
| Supplier qualification | Required vendor checks |
| Legal entity | Company, where context makes this accurate |
| Non-catalog item | Item not listed in the catalog |

## Workflow guidance for the UI designer

For each page, show (1) what the record is, (2) its current status, (3) why it is waiting, and (4) the next available action. Use the plain label in headings, lists, buttons, and notifications. Show the formal term or reference number in the detail view when it helps Finance, Procurement, or audit work. Do not change approval conditions, record ownership, or financial calculations just to simplify wording.

For a page with no records, show a short explanation, an icon, and a working action such as **Add vendor**, **Create request**, or **Import vendors**. A blocked action should explain exactly what is missing and how to fix it.

## Acceptance checks

- An employee can find an item, create a request, and understand its status without knowing “requisition” or “RFQ.”
- A manager can tell what awaits approval and what will happen after approving.
- A buyer can still find formal references, policy details, and audit history when needed.
- A bill returned by Finance says why it was returned and what action is available.
- Display copy does not alter stored workflow states, permissions, or integration payloads.
