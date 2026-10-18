# Core capability audit for Procurement integration

**Prepared:** 18 October 2026
**Status:** Source audit; verify behavior and ownership before enabling Procurement transactions
**Standard:** GW-ENG-STD-001 Tier 3

The ownership decision and activation gates are recorded in [ADR-001 — Procurement ownership and ERP Core reuse](ADR-001-MODULAR-OWNERSHIP-AND-CORE-REUSE.md). Review this audit alongside that decision before wiring any additional UI action.

| Shared capability | Existing ERP source | Procurement decision and gap |
|---|---|---|
| Sign-in, active worker, company/branch context | Shared HRMS host and `ProcurementScopeMiddleware` resolve a scoped Procurement principal; `/api/procurement/v1/context` exposes company and role context. | Reuse now. Real-session, company-switch and cross-tenant route tests remain required. |
| Roles and permission grants | Local sessions resolve named Procurement actions from active Core tenant roles. Seeded roles retain documented bootstrap grants until an administrator sets explicit actions; Core Roles now displays and edits them. | Verify OIDC mapping separately: token roles still use the bootstrap map. Test object scope separately from role and review grants before financial actions. |
| Organisation, departments, locations | HRMS/Core records are resolved through the shared host; Procurement stores stable references. | Reuse IDs, not names. Define Core ownership and change/inactivation behavior for referenced departments and locations. |
| Branding and shell | Shared company branding routes and global UI theme exist. | Reuse; no Procurement copy of branding settings. |
| Workflow/approvals | `IWorkflowService` exists, but its implementation requires HR roles, resolves an employee manager and applies leave-specific effects; cancellation is explicitly leave-only. | **Not a generic Procurement approval engine yet.** Define a shared workflow contract or a scoped Procurement adapter with approval stages, authority limits, maker/checker, policy version, delegation and outbox. Do not call HR leave workflow as if it approved purchasing. |
| Documents | `IDocumentsService` exposes worker and employee documents. | **Not a generic supplier/RFx/invoice evidence service yet.** Create a shared record-document contract with owner type/ID, tenant/entity/supplier audience, scan state, version and retention before relying on attachments. |
| Notifications | Employee notifications and delivery retry routes exist. | Reuse transport patterns after a shared recipient/template contract exists. Supplier audiences need separate scoped delivery. Notification delivery is not approval evidence. |
| Audit | Procurement `ProcurementDbContext` appends scoped field-name audit events; HRMS has its own audit paths. | Reconcile to a shared export/search contract, keep sensitive values out of audit, and verify append-only semantics at normal application roles. |
| Events and outbox | Procurement has an outbox table but no dispatcher; HRMS has an outbox publisher. | Reuse dispatch mechanics through an explicit versioned event/consumer contract; define retries, dead letters and reconciliation before Finance/Inventory effects. |
| Finance/AP, Inventory, Assets | Procurement schema stores external references only. | Their source ownership remains outside Procurement; live interfaces, idempotency and callback status are unresolved. |

## Dependency sequence

1. Verify Core identity/entity scope, tenant-configurable local grants and external identity mapping.
2. Define shared record-document and workflow contracts; keep HR leave rules in HRMS.
3. Connect Procurement audit/outbox to shared transport and monitoring.
4. Introduce Finance budget/AP and Inventory/Asset adapters with contract tests.
5. Enable each Procurement UI action only after its server transition and dependency outcome are authoritative.

This audit prevents a shared class name from being mistaken for a reusable domain contract. Current safe draft routes do not activate a supplier, reserve money, issue an order, validate an invoice or post to Finance.
