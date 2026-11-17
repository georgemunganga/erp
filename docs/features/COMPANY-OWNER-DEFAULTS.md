# Company owner module administration

An explicitly verified active Core company owner receives company-wide
administration when opening an enabled SaaS module. There is no branch-count
price limit in this policy. Commercial module entitlement and per-module seats
still apply; ownership does not activate an unpurchased module or another company.

CoreOwnerModuleAccess runs during workspace admission review, after current
identity, company membership and commercial authorization checks. It records the
owner's defaults using CoreModuleAccessCommands, including expected membership
and grant revisions, normal seat enforcement, immutable history, command receipt,
audit and outbox event. Repeat opens create no new version. Company scope includes
future branches. Existing employee self-service roles are preserved, but ownership
never creates an employee or associates one by name/email. There is no global
erp_admin grant and no fallback in CoreModuleAccessReader or session validation.

While a person remains an owner, a restrictive owner grant is superseded at their
next workspace admission. Remove ownership through Core's guarded membership
workflow to remove this default authority; changing a module grant alone is not
an ownership change. A membership revision change invalidates old sessions/grants.
Delegated administrators and members retain their explicitly assigned module roles,
actions and locations. Last-owner protection and normal business validation remain.

Kuubu's company Team and module-access screens manage SaaS membership and module
grants through ERP Core. Laravel owns commercial customers, enrollment, subscriptions
and entitlements; those records alone are not company membership. Existing standalone
instances retain local account/role and branch management. This SaaS policy does not
reinterpret local credentials, add central membership, change local administrators
or modify customer branch restrictions.

On 17 November 2026 the inspected SaaS directory contained Levant and George's two
Vcron trial companies. Levant and trial A already had company-wide HR owner grants;
trial B had a selected-location HR owner grant. New World Cargo's three active local
administrators had no branch restrictions. This inventory covers the accessible
deployments, not undiscovered customer installations.

Acceptance: seven owner-policy cases cover first/repeated access in HR and
Procurement, branch-scope supersession/history, preserving explicit employee role,
no elevation for delegated users, denying unlicensed/unrelated identities and
immediate loss after ownership removal. Fifty existing registry/HR/module HTTP
cases passed; four PostgreSQL-only registry cases were skipped locally.
