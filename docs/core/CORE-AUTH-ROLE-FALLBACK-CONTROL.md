# Core local role fallback control

Status: independently source-reviewed; 12 targeted regression tests passed in the combined P25 candidate on 23 October 2026. No production deployment or native identity-provider acceptance is claimed.

Local session roles come from the current tenant's role master when any role row exists. A configured inactive or archived row revokes its role and never re-enables a role stored in the account's legacy CSV. The CSV compatibility fallback applies only when the current tenant has zero role-master rows. Another tenant's rows do not affect that decision. The same distinction governs which roles can be assigned to an account and whether an active HRM administrator remains.

The existing role-management rule still prevents an ordinary update from disabling the final active HRM admin grant. The previously possible all-disabled state therefore mainly concerns imported, historical or direct database changes. Procurement separately revalidates current active, non-archived Core roles on each module request; this control addresses the inherited Core/HRM local-session path.

Review and run the mapped tenant/role tests before integration. Preserve the legacy empty-master behavior until a separately approved migration removes it; do not grant any role or module action from an inactive, archived or foreign-tenant definition.

## Verification

The combined candidate `3b4a7cbee779e67c8aca7c3582d3a493b15965c5` includes the unchanged Core patch from `9244b963835b3b880e9e8a85ede9df28fe8cdcba`. All 12 `LocalIdentityRoleFallbackTests` and `JobsAdminTests` passed with zero skips/failures. The combined build and private probe publication succeeded, with tracked sources unchanged. See the adjacent JSON for evidence hashes. The earlier temporary-build attempt compiled but all tests failed to load SQLite from a no-exec mount; that failed result remains retained and is not counted as acceptance. Offline package vulnerability lookups were unavailable; this run does not claim a dependency security scan.

## Inherited follow-up

In a tenant with no configured role-master rows, first creating only a non-admin role can disable a legacy CSV administrator's access. This predates this patch; the role bootstrap path needs a separate last-admin-preservation design and acceptance test. The existing HRM role-management guard and general ERP administrator predicates also use different admin markers; retain that distinction until its intended authority is documented.
