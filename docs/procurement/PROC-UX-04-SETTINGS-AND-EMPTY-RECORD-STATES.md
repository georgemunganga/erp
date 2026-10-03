# PROC-UX-04 — Procurement settings and empty record states

**Status:** UI handoff draft, 3 October 2026. This document describes the frontend behavior and ownership boundaries. It does not approve business policy or activate Finance workflows.

## Settings structure

Keep **Configuration** as one administrator navigation link. Inside it, provide searchable groups for Organization Settings, Users & Roles, Taxes & Compliance, Setup & Configurations, Customization, Automation, Module Settings, and Custom Modules. Each setting shows its owner and whether a working screen exists. Shared organization, identity, tax, currency, workflow, document and notification services remain with their ERP owner; Procurement keeps buying and invoice-validation preferences.

The **Module Settings → Bills** page has six tabs: Preferences, Approvals, Fields, Validation Rules, Buttons, and Related Lists. Its Approvals tab explains No Approval, Simple Approval, Multi-Level Approval, and Custom Approval. No Approval skips an additional human approval only; required validation, matching, segregation of duties, and Finance posting controls still apply. A selection in the current UI is a review preview, never an active approval rule. Production activation requires a versioned rule, role and data-scope checks, authority limits, and a shared workflow-engine contract.

## Empty state rule

Show a centered icon, a short business reason to use the page, and a real primary action **when the current record set has no data**. If records exist but a search or filter returns zero matches, show a separate “No matching records” state with a clear-search action. Never show an Add button that opens a dead-end or invents a record without its prerequisites.

| Page | Empty state primary action | Record action |
| --- | --- | --- |
| Vendors | Add vendor; Import vendors | Create a proposed vendor, review, maintain contacts, suspend or deactivate with audit history. CSV imports remain Proposed. |
| Items | Add item for a buyer/admin; non-catalog request for an employee | Create and maintain procurement item references. |
| Purchase requests | Create request for an eligible requester | Draft and submit through policy and approval. |
| Sourcing events | Create event for a buyer/admin | Create an event from approved demand and control bids. |
| Evaluations | Open sourcing | Evaluation starts from a closed event and bids. |
| Contracts | Open sourcing | Contracts follow an approved award or agreement route. |
| Purchase orders | Open requests | Orders require approved demand or another authorized source. |
| Receipts | Open orders | Receipt requires an issued order. |
| Invoice inbox/Bills | Capture invoice for an eligible user | Intake, validate, match and hand off to AP. |
| Payment status | No create action | Status comes from Finance confirmation. |

## Vendor CRUD specifics

- **Create:** Add vendor opens the onboarding form; the record begins as Proposed or under review and cannot be used for an order until approved and active.
- **Import:** Accept a CSV of at most 200 rows and 1 MB, preview errors, reject duplicates, and add all valid rows as Proposed. Keep full bank data out of the import.
- **Read:** Vendor list and profile show entity-scoped lifecycle status, owner, contacts, qualifications, related transactions and activity.
- **Update:** Authorized supplier stewards/admins can add, edit and remove contact persons. Require name, role and valid unique email. Record changes in the activity history. Legal and payment changes use their existing review paths.
- **Delete equivalent:** Deactivate with a reason instead of deleting. Block deactivation while open purchase orders or invoices exist; retain historical references. Suspension remains a separate controlled action.

## Acceptance for UI review

1. A first-use entity with no vendors shows the centered “Every purchase starts with a vendor” message and working Add vendor and Import vendors actions.
2. A vendor search with no match says “No matching records” and can clear the query.
3. A CSV import previews row errors before enabling Import; imported vendors remain Proposed after refresh.
4. A supplier steward can add, edit and remove a contact, and changes remain after refresh.
5. Deactivation requires a reason and is blocked by open obligations.
6. Empty pages link to their actual prerequisite workflow. A Finance-owned payment page does not offer a Procurement payment action.

The current frontend persists these demo records in browser storage. Production CRUD needs server authorization, durable audit and transaction handling before operational use.
