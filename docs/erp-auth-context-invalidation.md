# Shared ERP authentication context invalidation

Owner: Core identity/platform. This extends the existing login/logout lifecycle; it does not create a new identity, role, employee or authorization service.

Successful local sign-in, completed local logout, successful OIDC callback and explicit OIDC sign-out publish a random public nonce under `erp.auth-context.v1` and the same-document event `erp:auth-context-changed`. No account identifiers, credentials or claims are included. Storage failure does not prevent the local event or change login/logout outcomes. Normal document redirects remain unchanged. No signal is emitted on automatic token refresh.

Procurement's companion shared-shell consumer must clear prior scope/role projections, unmount old editors, re-fetch actual auth/context/metadata and reject late responses from the prior generation. It must retain ordinary tab-switch edits and ignore token-refresh storage events. Readiness remains server supplied; a public event may trigger revalidation but never grant access. Backend authorization still evaluates every operation, and already committed writes are not cancelled by UI invalidation.

Limitations: manual/external cookie changes, older login producers, unavailable cross-tab storage or missed events require a normal reload; this is not an atomic cross-window server session fence. Future comparisons should use canonical admitted account identity from the server, not worker identity or token decoding.

Verification: two built-in Node producer tests passed (`node --experimental-strip-types --test modules/hrm/frontend/module-connect/src/platform/auth-context.test.mjs`). Companion UI units, browser checks and whole frontend build remain required before release. No schema, production account, runtime setting or deployment is changed by this source checkpoint.
