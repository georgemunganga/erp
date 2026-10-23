# Core local role fallback control

Status: source-only security hardening candidate, 23 October 2026. No runtime or production acceptance is claimed here.

Local session roles come from the current tenant's role master when any role row exists. A configured inactive or archived row revokes its role and never re-enables a role stored in the account's legacy CSV. The CSV compatibility fallback applies only when the current tenant has zero role-master rows. Another tenant's rows do not affect that decision. The same distinction governs which roles can be assigned to an account and whether an active HRM administrator remains.

The existing role-management rule still prevents an ordinary update from disabling the final active HRM admin grant. The previously possible all-disabled state therefore mainly concerns imported, historical or direct database changes. Procurement separately revalidates current active, non-archived Core roles on each module request; this control addresses the inherited Core/HRM local-session path.

Review and run the mapped tenant/role tests before integration. Preserve the legacy empty-master behavior until a separately approved migration removes it; do not grant any role or module action from an inactive, archived or foreign-tenant definition.
