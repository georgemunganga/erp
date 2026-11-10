# Product ERP session selection UI

## 1. Identification
Tier 3 Core frontend integration, 10 November 2026. Implemented and verified in an isolated build. Not live.

## 2. Purpose
Connect ordinary members to the existing server-owned ERP session review/selection lifecycle from the branded company screen.

## 3. Scope
Self-review, explicit company/module/scope confirmation, selected-cookie acknowledgment and session logout. Existing worker-screen transport and actual-provider worker browser integration remain required next.

## 4. Ownership
Laravel owns identity/enrollment/subscription; Core owns grants and selected session authority. Browser review is informational and never creates a grant.

## 5. Actors
Verified enrolled company members, including members without owner permission-editor access.

## 6. Preconditions
Existing operational identity/CSRF and company discovery. Default-off server session configuration remains unchanged. No local identity fallback.

## 7. Entry
Existing /workspace-access company review; a separate Your ERP session region serves ordinary members and owners alike.

## 8. Review
Only explicit module review fetches the exact company/link/module grant. Validate response fields, version/revision, nonempty roles/actions and coherent company/finite scope. Reject unknown fields and foreign selectors.

## 9. Confirmation
Finite scope retains exact reviewed locations. Company-wide scope requires a separate checkbox. Subscription module visibility and company membership alone confer no operational access.

## 10. Commands
Generate one UUID command key when the user confirms. Freeze exact serialized body and nested snapshot in component memory; no credentials or command persistence in browser storage.

## 11. Transport
Fixed same-origin endpoints, cookie credentials, no-store, redirect:error and bounded request timeouts. Mutations use existing protected operational CSRF; no Authorization/tenant/shell headers.

## 12. Selection
POST the reviewed version/revision/scope with explicit confirmation. Validate matching company/module and future expiry. Read current server session after selection and match scope/location set before displaying success.

## 13. Recovery
Lost/malformed/temporary responses retain the exact attempt for explicit retry. No automatic retry, fresh command or successful display on an uncertain result. Definitive conflicts/denials discard review.

## 14. Switching
Company-keyed component lifetime cancels pending reads, removes prior review and clears shared query data. Parent controls disable while a session operation runs. Switching display does not grant a new session.

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
No server contract/migration change. Existing standalone and owner editor remain intact; generic legacy ERP transport still requires product integration.

## 24. Acceptance
The isolated SSR build and TypeScript check pass. 135 contract tests and 36 Chrome tests pass, including 10 new contracts and eight new session UI cases. Lint has no errors and five existing warnings. All 377 frozen UI inputs and 491 build output files were verified; the local listener stopped. Evidence: inspection/qualifications/product-session-ui-verification-02/verification.json. Browser cases use synthetic API responses. Actual identity-provider and persisted worker operations through this UI still require verification.

## 25. Deployment
Source/draft only. No production auth flag, DNS, proxy, Perfex, payment or mail modification.

## 26. Rollback
Revert this UI increment; retain existing server sessions/grants and immutable evidence. Never disable authorization to admit the frontend.

## 27. Evidence retention
Exclusive independent copies for every build/check attempt; failed/partial publications remain write-once. No mutable output hardlinks.

## 28. Programme
Next integrate selected authority into the existing shell/employee screens and qualify actual branded provider -> session -> persisted worker operations. Remaining modules, billing/Lenco, full SaaS rollout and dedicated/on-premise lifecycle remain outstanding.
