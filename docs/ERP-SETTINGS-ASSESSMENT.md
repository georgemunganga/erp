# ERP settings assessment

Assessed: 16 October 2026. Scope: ERP Core, HRM, and Procurement settings directories, detail routes, editor ownership, access, and persistence. This is an implementation inventory, not a claim that every listed control is operational.

## Current state

| Area | Directory and editors | Persistence |
| --- | --- | --- |
| ERP Core | `/core/workspace/settings` lists 14 entries. Seven entries lead to five working editors: organization (also locations and departments), branding, users, roles, and branch access. Seven entries show an explicit planned editor. | Existing editors call the HRM API and its current data model. The URL and UI ownership are centralized; the backend has not yet been separated into a shared Core service. |
| HRM | `/hrm/workspace/settings` lists HRM-specific employment, leave, payroll, process, compliance, integration, and readiness settings. Existing editors are reachable under `/hrm/workspace/settings/{setting}`. | Jobs, holidays, payroll setup, leave types, contract types, compliance controls, integration operations, and readiness use live APIs, though individual tabs may be unavailable. Process design uses mock data and does not persist. The notification-delivery view is live and is now linked from the directory. |
| Procurement | `/procurement/workspace/settings` shows supplier, item, purchase request, purchase order, receiving, and other module previews. Shared settings URLs redirect to Core. | The separate reference frontend stores preview snapshots locally. Its settings do not save ERP configuration or enforce server-side policies. |

## Ownership decisions

- Core owns legal entities, locations, departments, users, shared roles, branding, shared defaults, and shared document/communication configuration.
- HRM owns employment rules, leave, attendance, payroll, HRM compliance, HRM integration hand-offs, and HRM notification-delivery operations.
- Procurement owns supplier and catalog configuration, sourcing, requests, orders, receiving, and procurement-side invoice validation.
- Finance owns accounting, taxes, payment terms, payables, and payments. Procurement may show their status or references, but should not own their editors.

## Confirmed gaps

1. **Core placeholders:** Reporting Tags, General, Currencies, Transaction Number Series, PDF Templates, Email Notifications, and Web Tabs have no connected editor or backend contract. Currency ownership needs an explicit Core-reference versus Finance-rate distinction before implementation.
2. **Shared role model:** The Core Roles screen is the former HRM role editor. Its current permission choices are HRM-oriented; a truly shared ERP role/permission catalog is not complete.
3. **Backend boundary:** Core-owned editors still use `/hrm` API routes and HRM persistence. Moving their UI does not yet create an independently owned Core API, schema, or service.
4. **HRM process preview:** Leave policies, shift rules, approval routing, form fields, automation, templates, and self-service controls on Process Design use mock data. They must not be treated as active company policy.
5. **Procurement ownership:** The reference settings catalog still contains Projects, Chart of Accounts, Customers, Recurring Bills, and Payments Made. Their editable masters belong to other modules. The Procurement catalog also lacks dedicated sourcing/RFQ, contract, inspection, returns, and invoice-match policy settings from the approved capability map.
6. **Procurement persistence:** Supplier portal, punchout, supplier/item/request/order/receipt, and invoice-related settings need server contracts, authorization, validation, versioning, and audit before they can control real workflows.
7. **Fine permissions:** Page entry and backend authorization need a role-by-role review for each setting. Directory visibility alone does not prove a user can save, and an API denial should have a clear UI state.

## Changes verified during this assessment

- Canonical detail URLs load the existing Core and HRM editors; legacy links redirect.
- All HRM settings detail pages now pass through the HRM sign-in/access gate. An unauthenticated visit to Process Design redirects to sign-in.
- Existing Core editor wrappers admit ERP administrators through the ERP gate; Core still requires an administrator role.
- The existing HRM notification-delivery screen is listed at `/hrm/workspace/settings/technical`; its legacy URL redirects there.
- The shared configuration page back link points to the HRM settings directory.

The browser check confirmed page rendering and routing. It did not mutate production settings or validate every save path. The next implementation work is the shared Core backend/permission contract and the Procurement settings contract, followed by testing of each editor's save and audit behavior.
