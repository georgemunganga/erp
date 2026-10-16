# ERP settings ownership and routes

## One route convention

Every module opens its settings directory at `/{module}/workspace/settings` and its setting editors at `/{module}/workspace/settings/{setting}`.
The current directories are `/core/workspace/settings`, `/hrm/workspace/settings`, and `/procurement/workspace/settings`. For example, company branding is `/core/workspace/settings/branding`, while payroll setup is `/hrm/workspace/settings/payroll`. Existing `/core`, `/core/branding`, and `/hrm/configuration/...` links redirect to the owning editor so bookmarks remain usable.

## One owner for each setting

ERP Core owns company identity, legal entities, branches, departments, users, roles, branding, and other shared ERP defaults. HRM owns employment, leave, attendance, payroll, HRM compliance, and HRM integration rules. Procurement owns supplier, catalog, sourcing, purchase request, purchase order, receiving, and procurement invoice validation rules. Finance owns accounting, tax calculation, payment terms, and payment execution.

Module settings directories list only their owner's settings. A legacy URL for a shared editor redirects to the owning module. Other modules consume the resulting configuration through shared ERP APIs and do not maintain a second editable copy.

## UI and implementation

The Procurement settings screen established the search-and-card pattern. HRM and Core use the shared `SettingsDirectory` component with module-specific catalogs. Their existing ERP headers and navigation remain in place. Procurement is deployed as a separate frontend and currently uses its own implementation of the same pattern; moving it into a shared frontend package can follow when the two builds share a release process.

Directory links do not imply an editor is live. Core currently has live organization, branding, user, role, and branch-access screens. Those pages reuse the existing ERP editors, including useful controls from the former HRM configuration UI. Entries without backend contracts explicitly show a planned editor. Procurement's reference workspace remains a frontend demo, and its settings previews do not save ERP configuration.
