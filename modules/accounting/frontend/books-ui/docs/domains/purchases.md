# Purchases and payables

> Evidence status (2026-10-20): The selected Global organization reached the Books dashboard and authenticated reference exploration has begun. The candidate workflows below remain public-document research, not tenant proof; observed screens and action boundaries are in [verified patterns](../verified-screen-patterns.md) and [interaction coverage](../interaction-coverage-plan.md). No tenant accounting record was created or changed; a temporary report Favorite toggle was reversed and verified empty.

**Public-doc candidate note only.** The later tenant capture verifies some purchase list and blank-form screens, separately documented in `verified-screen-patterns.md`; public help pages do not verify this Global tenant or establish every purchase workflow.

## Candidate capture scope

- **Bills:** Candidate read-only sequence: Bills overview/list → bill detail → journal view or recorded-payment detail. The public overview also names Create Bill, Record Payment, Import Bills, Apply Credits to Bill, and Create Vendor Credits; these are documented workflow entry points, not permission to perform them. For capture planning, inspect only already-existing, non-sensitive records and visible navigation after readiness; do not create, edit, import, pay, apply credits, approve, or otherwise change state. [1]
- **Purchase approvals:** The help page documents Settings → Organization → Approvals → Purchase Approvals, with choices for no approval, all permitted approvers, or multi-level approvers. Treat settings/configuration and approval actions as out of bounds for read-only capture. It lists Bills, Vendor Credits, and Purchase Orders as covered transactions and notes up to 10 approvers. This page is explicitly for the **old** approvals version and links to a newer flow. [2]
- **Unverified candidates:** Vendors, expenses, purchase-order create/detail lifecycle, vendor-credit lifecycle beyond the names above, payments-made screens beyond bill payment references, and Documents Inbox are in the requested candidate domain but are not substantiated as screen flows by the fetched sources. Keep them out of a confirmed workflow map until supported by public documentation or a dashboard-ready, authorized observation.

## Shared screen families to plan for

The sources support planning for a transaction overview/list, transaction detail with related actions or journal/payment references, and an organization-level purchase-approval settings page. A create form is a candidate for Bills because Create Bill is named, but its fields and exact layout are not documented by the overview page. Do not assume other modules share these screens or labels.

## Dependencies and uncertainty

The Bills help page is headed **US-EN** and its links resolve under `/us/books/`; it also exposes a Global-English edition selector. That is not evidence that its details are Global-specific. The page lists combined expense-and-bill record limits for Professional, Premium, Elite, and Ultimate, so limits are plan-dependent; this does not establish the selected organization’s plan or entitlements. [1] The approvals page also displays US-EN and a Global selector, and warns that it covers the old approvals version. Availability, exact labels, approval setup, role access, and candidate modules in the selected Global organization remain unverified. No public-doc screenshots were used or treated as proof of tenant state.

## References

[1]: https://www.zoho.com/books/help/bills/ "Overview - Bills"
[2]: https://www.zoho.com/books/help/transaction-approval/enable-transaction-approval.html "Enable Transaction Approval"
