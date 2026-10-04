# PROC-UX-08 — Shared ERP shell and Procurement navigation

**Status:** Implemented in the Procurement UI as a first reuse pass, 4 October 2026.

## What users share

The header, desktop and mobile side menu, search dialog, notifications, entity switcher, module switcher, theme control, and button component are platform UI. The same shell reads a module definition, so a new module supplies navigation data instead of copying the layout. The side menu can collapse on desktop; its choice is saved locally. Mobile navigation closes after a destination is selected.

The shared button component uses the HRM control sizes and rounded shape, including larger default and icon targets. Its color still comes from the active UI theme. Forms and records continue to use the same button component.

## What Procurement owns

Procurement owns its menu labels, paths, visibility by role, page content, workflows, status language, settings, and record actions. Its navigation remains:

Home → Getting started (admin) → My Requests → Approvals → Items → Vendors → Procurement (requests, quotes, orders, deliveries, contracts) → Payables → Budgets → Analytics → Settings (admin).

The header Approvals shortcut resolves to the active module's approvals page. Switching from the public Procurement app to HRM opens `/hrm`.

## Current integration boundary

The live HRM and Procurement frontends are separate deployments and repositories. They use the same shell pattern and compatible UI primitives, but they do not currently import one physical package. HRM's authenticated identity, organization access, setup gate, and live counts remain in HRM. Procurement currently displays demonstration data and must not imply those HRM services are connected.

When the two frontends are consolidated, move the platform shell primitives and design tokens into a versioned shared package. Keep `hrmModule` and `procurementModule` as module-owned inputs. Connect approvals, notifications, identity, and organization scope through shared APIs at that point; do not share mock data stores.

## UI review checks

1. Procurement links open Procurement pages in expanded, collapsed, and mobile menus.
2. A role sees only its permitted Procurement entries.
3. Collapsing the desktop menu does not hide a route: grouped links open in a menu beside the icon.
4. Header Approvals opens `/procurement/approvals` in Procurement and `/approvals` in the local HRM demo.
5. Buttons remain usable on narrow screens and with keyboard focus.
