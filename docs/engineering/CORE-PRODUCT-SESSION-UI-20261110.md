# Product ERP session selection UI

## 1. Identification
Tier 3 Core frontend integration, 10 November 2026. Implemented and verified in an isolated build. Not live.

## 2. Purpose
Connect ordinary members to the existing server-owned ERP session review/selection lifecycle from the branded company screen.

## 3. Scope
Self-review, explicit company, module and scope confirmation, selected-cookie acknowledgment and session logout. Product mode opens the employee directory for the selected HRM session and supports branch filtering, search, paging and archive.

## 4. Ownership
Laravel owns identity/enrollment/subscription; Core owns grants and selected session authority. Browser review is informational and never creates a grant.

## 5. Actors
Verified enrolled company members, including members without owner permission-editor access.

## 6. Preconditions
Existing operational identity/CSRF and company discovery. Default-off server session configuration remains unchanged. No local identity fallback.

## 7. Entry
The existing /workspace-access company review includes a Your ERP session region for members and owners. An eligible HRM selection opens the product directory at /hrm/employees.

## 8. Review
Only explicit module review fetches the exact company/link/module grant. Validate response fields, version/revision, nonempty roles/actions and coherent company/finite scope. Reject unknown fields and foreign selectors.

## 9. Confirmation
Finite scope retains exact reviewed locations. Company-wide scope requires a separate checkbox. Subscription module visibility and company membership alone confer no operational access.

## 10. Commands
Generate one UUID command key when the user confirms. Freeze exact serialized body and nested snapshot in component memory; no credentials or command persistence in browser storage.

## 11. Transport
Requests use fixed same-origin endpoints, cookies, no-store, redirect errors and bounded timeouts. Archive uses protected operational CSRF. The directory reads current scope from Core and sends its expected session marker on worker requests. The API compares the marker against the session cookie before reading data or applying a mutation. Only the cookie and current server-side proof establish authority.

## 12. Selection
POST the reviewed version/revision/scope with explicit confirmation. Validate matching company/module and future expiry. Read current server session after selection and match scope/location set before displaying success.

## 13. Recovery
Lost/malformed/temporary responses retain the exact attempt for explicit retry. No automatic retry, fresh command or successful display on an uncertain result. Definitive conflicts/denials discard review.

## 14. Switching
Company selection cancels pending reads, removes prior review and clears shared query data. The directory holds employee records in component memory. Hiding the page or losing access unmounts the record view. Reopening the page checks the same session before loading records. Active tabs recheck access every 30 seconds.

## 15. Logout
Clear selected UI/review/cache before logout; only confirmed no-content completes logout. Uncertain logout offers explicit retry and never claims completion. Parent logout remains separately authoritative.

## 16. Data scope
Server resource filters remain authoritative. No client narrowing can widen selected company/branch authority.

## 17. Permissions
Owner permission editing remains separate. UI hides no server authorization checks and does not translate governance roles into HRM roles.

## 18. State
Initial -> review -> confirmation -> pending -> selected, or definite denial/uncertain retry. Selected display is not broad ERP operational acceptance.

## 19. Cancellation
Abort on unmount; late results cannot replace a different company review. Cancellation does not imply a server mutation rolled back.

## 20. Audit
Existing Core session issuance/revocation audit remains authoritative. Do not log bodies, cookies, tokens or raw provider responses.

## 21. Failures
Expired identity returns to sign-in; denied grants require review; unavailable providers do not produce local sessions. Foreign current session cannot be displayed as selected company.

## 22. Accessibility
Shared buttons/cards, semantic region and headings, explicit checkbox label, status/alert announcements, disabled pending controls and responsive layout.

## 23. Compatibility
Core adds a session marker and worker capabilities to the product workspace response. The request marker is optional for existing API clients and mandatory in the directory client. The change requires no migration. Standalone routes continue through their existing shell.

## 24. Acceptance
The isolated SSR build and TypeScript check pass. 135 contract tests and 36 Chrome tests pass, including 10 new contracts and eight new session UI cases. Lint has no errors and five existing warnings. All 377 frozen UI inputs and 491 build output files were verified; the local listener stopped. Evidence: inspection/qualifications/product-session-ui-verification-02/verification.json. Browser cases use synthetic API responses. Actual identity-provider and persisted worker operations through this UI still require verification.

The worker directory increment was verified separately. 46 focused API and scope tests, one native PostgreSQL regression, 135 contracts and three product browser tests passed. The browser tests use fixture HTTP responses with the production UI build. Build, TypeScript and targeted lint also passed. See inspection/qualifications/product-worker-browser-verification-01/verification.json.

## 25. Deployment
The directory is available in a frontend built with VITE_ERP_PRODUCT_MODE=true and paired with ERP:AuthMode=product. Use the isolated environment for qualification before enabling the deployment.

## 26. Rollback
Revert this UI increment; retain existing server sessions/grants and immutable evidence. Never disable authorization to admit the frontend.

## 27. Evidence retention
Exclusive independent copies for every build/check attempt; failed/partial publications remain write-once. No mutable output hardlinks.

## 28. Programme
Verify branded sign-in, company selection and persisted worker archive in one browser run against Laravel and Core. The employee detail and create/edit forms still need scoped reference data and reviewed assignment operations. Remaining modules, billing with Lenco, SaaS rollout and dedicated or on-premise installation and updates remain outstanding.
