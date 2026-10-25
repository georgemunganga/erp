# Accounting module UI foundation

Status: UI-only import and ERP adaptation. No accounting API, ledger, migration, bank connection, tax computation authority or Finance posting is implemented by this change. Follow GW-ENG-STD-001 and the existing 28-section specification process before backend work.

## Purpose and source

Provide an Accounting workspace in the existing ERP using the user-supplied Books UI, preserving its detailed screens and 124 reference workflows. The exact 69-file source import from `Greenwebb/zoho-books-ui-reference` at `c3cb7a536b82bdc60683ad49e1b165cd58562afb` is committed separately before adaptation. [Provenance](UI-IMPORT-PROVENANCE-20261025.json) records every original file hash; [import checklist](../../modules/accounting/frontend/books-ui/docs/ERP-IMPORT-ACCEPTANCE-PLAN.md) distinguishes inherited evidence from tests executed here.

Accounting is the UI entry for the future Finance/accounting domain. It does not create a second financial system alongside Finance. The existing disabled Finance workspace remains unchanged while its broader product scope is settled.

## Routes and presentation

The module is mounted at `/accounting/workspace/`. A Vite base and router basename keep deep links/assets under that prefix. The ERP catalogue exposes Accounting as **UI preview** for ERP administrators; ERP Core remains last. This is preview admission through the existing Core identity, not a new financial permission catalogue. No employee record is required. Global configuration links to the existing `/core` directory while the newer hierarchical settings deployment is tracked separately.

The imported Sales, Purchases, Banking, Accountant, Items/Projects, Documents, Reports, Settings and Workflow Library pages remain. Shared ERP Button, Card, Input, Textarea, floating FormSelect, Switch, top-bar frame and sidebar header are reused. Boolean preferences use Switch; multi-record selection remains a checkbox interaction. Core branding supplies the company mark, colors and selected font (Inter, Montserrat and Poppins have licensed self-hosted font subsets); the source workflow/page layout is retained. The sidebar supports expanded/collapsed desktop and mobile drawer navigation. Titles remain outside cards.

One visible preview notice explains that sample records and resulting actions remain browser-local. The local model can demonstrate states such as Paid/Published/Reconciled but cannot establish those facts in the ERP. Exported preview data is sample data, not Finance reporting.

## Shared ERP boundaries

| Responsibility | Owner and Accounting use |
| --- | --- |
| Identity, accounts, roles and data scope | Core; read the existing session and current permitted organization choices. Do not create a module login, employee requirement or production grant. |
| Organization, branches, branding, fonts and shared settings | Core; consume presentation/context and link to Core settings. Local configuration workflows remain sample references. |
| Supplier procurement profile, PR/RFQ/award/PO/receiving | Procurement; Accounting's duplicate-looking sample screens are reference views, not new authoritative purchasing stores. |
| Supplier/customer accounting account, AP/AR, chart of accounts, journal/ledger, settlement and reconciliation | Future Finance/accounting backend; must receive governed Procurement handoffs and preserve existing capacity/ownership contracts. |
| Inventory stock and asset lifecycle | Inventory/Assets; preview stock adjustment or item flows do not create stock balances or asset registers. |
| Workflow execution, audit, documents, notifications and integrations | Existing shared Core capabilities; local sample artifacts are not substitutes. |
| Customers/sales/projects | Separate owned contracts still to define; the imported UI alone does not settle their ERP ownership. |

There is no new backend schema in this import. Existing suppliers/items/organization data must be referenced through the owning contracts when live integration is built; do not recreate masters to fit these pages.

## Preview data and shared session

The gate reads `/api/hrm/auth/me`, `/api/hrm/branding` and `/api/core/v1/workspace-context` using the existing same-host session. Historical `/hrm/` endpoint names are compatibility transport names, not a dependency on active employment. An anonymous user returns to the existing `/sign-in?returnTo=...`; an ordinary user without administrative preview access receives an explicit denied screen. Current Core context 403 fails closed. Only an older host 404 permits the existing owned organization/shell fallback, with branch assignment intersection.

Three local data kinds—workspace records, workflow results/artifacts and unfinished wizard drafts—use `greenwebb.accounting.preview.v1.<principal>.<company>` namespaces. Changing companies remounts all providers to avoid carrying sample edits across contexts. These browser partitions are UI hygiene, not server-side authorization or durable financial isolation.

Core settings and sign-out use the existing owners. All other imported business actions have no network writer. No payment credentials, bank transfers, mail sending, production document upload or external integration are introduced.

## Acceptance and future implementation

Run strict type checking and production build, all 124 local engine paths and existing browser workflow cases, then separate ERP admission/branding/navigation/mobile/local-persistence cases. Mocked Core reads test the UI adapter; they do not prove real authentication or accounting backend behavior. Keep failed attempts, fixture provenance and exact accepted asset/test versions distinct. Actual run results are recorded in the import acceptance receipt.

Before any live feature: define the parent/child/operation; expected workflow/states; current Core permissions and record scope; rules and authoritative data owner; inputs/API/effects; audit, failure/retry, tests and migration/release evidence. A valid local preview receipt cannot authorize a real financial action.
