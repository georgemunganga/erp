# Core-owned authenticated session management authorization

Independent source-only Core correction based on published Main `1917405282cbefb773386eeadc930349f478ef0c`. Original source commits `3d6daa3` and `2e358ad` were cherry-picked without importing the `5cb3d614` feature branch. This fix has no P25 approval-execution, receipt, schema, migration or feature-flag dependency. No SDK/Node/database/browser jobs, migrations, role grants or deployment performed by this author. The failed private normal-cookie browser attempt03 against the separate `5cb3d614` candidate remains valid defect evidence; this branch does not rewrite that failure or claim Main runtime verification.

## Defect and boundary

Production's default authorization policy intentionally requires `HrmStaffAccess.IsStaff`. LocalIdentityRoutes reused that policy on `/logout` and `/change-password`, which incorrectly denies a genuine active shared ERP account carrying only Procurement permissions. Its session cannot be revoked by the ordinary logout UI even though actual session authentication succeeds. This was observed by the parent against the real cookie/browser/private database.

One new named policy `shared-authenticated-session` requires authentication only. Program registers it once. Only LocalIdentityRoutes' two self-session/credential endpoints opt in, under both `/api/hrm/auth` and `/api/v1/hrm/auth`. There is no existing appropriate authenticated-only shared policy in this baseline. HRM default admission, hrm-admin, Procurement actions, tenant filters and business services remain unchanged; no new role or employee requirement.

Logout still hashes and revokes only the actual supplied opaque session cookie and deletes that cookie. It does not accept a target user/session ID. Current-password validation and the active current canonical local account lookup for password changes remain unchanged. The patch does not change password hashing, login lockout, expiry, token creation, credential reset, session renewal or the existing decision not to revoke all sessions on password change. Anonymous callers remain denied; expired/revoked/inactive accounts still fail the existing authentication handler.

## Privacy and request protections

No new identity fields, record projections, logs or secrets. Cookie Secure/HttpOnly/SameSite=Lax/path behavior and existing configured CORS remain unchanged. No AllowAnonymous is added. No new anti-forgery mechanism or exemption is introduced: these existing JSON endpoints do not gain a CSRF-token flow from this authorization correction, and this branch makes no new standalone anti-forgery security claim.

## Authored verification (not executed)

`SharedSessionAuthorizationHttpTests`:15 mapped cases using actual LocalIdentityRoutes, LocalAuthenticationHandler, LocalPasswordHash/LocalSessionTokens, real authentication/authorization and TimesheetAccess middleware. Fixed synthetic tenant + InMemory store only; no mocked principal/authentication handler. Outbox is a fail-if-used dependency for unrelated mapped routes, not a fake executed integration.

- 2 alias cases: real login cookie for a non-HRM workerless account, logout succeeds and stored session is revoked; another account's session remains unchanged; retained-cookie replay denies and cannot mutate revocation metadata.
- 2 alias cases: wrong current password denied with unchanged hash; correct self-password change succeeds; no worker/role/session invention; HRM default-policy probe and actual admin users route stay forbidden.
- 4 anonymous alias/action cases:401 and no password/session effects.
- 6 expired/revoked/inactive actual-session cases across aliases: both actions401 and retained password/revocation unchanged.
- 1 policy-boundary case: non-HRM caller denied default HRM policy, genuine employee admitted to that policy; actual admin route remains forbidden; exactly four mapped endpoints reference the new shared policy and none has AllowAnonymous.

The default HRM policy probe is explicitly a policy probe, not a substitute for workforce/payroll business-service integration. This fixture does not run the entire Program pipeline, CORS or actual PostgreSQL. Parent must compile/run the focused mapped tests then rerun the real ordinary-cookie logout/revocation browser case and observe the persisted session before claiming runtime completion. Core logout must not wait for the separate P25 Procurement approval release; neither workflow is activated by this fix.

Suggested focused filter: `FullyQualifiedName~SharedSessionAuthorizationHttpTests`. No release or test pass is inferred from this authored source.

## First focused execution failure / source-only fixture correction

Parent's first minimal Main SDK attempt compiled but all15 mapped cases failed during endpoint startup before any policy assertion: LocalIdentityRoutes.Map also maps user creation, whose IIdentityProvisioningService parameter was not registered. RequestDelegateFactory inferred it as UNKNOWN instead of a service. Evidence remains in `/home/amizo/erp-backups/core-shared-session-tests-20261022_run01`; these are startup failures, not failing authorization outcomes.

A narrow test-only correction registers an explicit throwing/uncalled IIdentityProvisioningService factory so all real route signatures bind correctly. It is not a substitute for authentication, Core identity, user provision or password/session behavior. If any test calls it, the fixture fails immediately. Actual production registers IdentityProvisioningService with its owning external dependencies; injecting that graph is outside this session-only fixture. All other mapped service parameters were source-checked: HrmDbContext, IOutboxWriter (existing fail-if-used boundary), IConfiguration and ILoggerFactory (WebApplication builder services), plus platform HttpContext/CancellationToken. No production policy or handler changes accompany this correction. Rerun and genuine browser/session-row verification remain pending.
