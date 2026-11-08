# Kuubu company-team screen

## 1. Identification
Tier3 Core child feature, 8 November2026; separate branded product-management flow.
## 2. Purpose
Let an explicitly authorized company owner/administrator inspect the current team
and review role changes/revocations without confusing billing or module permissions.
## 3. Scope
/team company/member pages, confirmed role/revoke review, bounded pagination,
request recovery and team sign-out. Invitation creation/acceptance/delivery and
licensed operational ERP sessions are not implemented by this screen.
## 4. Parent
CORE-COMPANY-TEAM-HTTP-20261108.md; native-qualified API source2607cd69. Preserve
the existing setup and local/dedicated settings flows and one maintained ERP source.
## 5. Actors
Verified Kuubu identity with explicit current Core owner/administrator membership.
An ordinary member uses a future operational workspace selector, not this manager
directory. A commercial payer role never supplies company-management authority.
## 6. Entry
/team has its own Kuubu identity shell, alongside /setup, outside the default ERP
AuthProvider/BrandingProvider. Continue with Kuubu requests the fixed team OAuth
intent. This is not a new password form or a replacement for the branded website
login. Activation/navigation from the website/setup remains release integration.
## 7. Preconditions
The opt-in backend boundary must be enabled and reachable at the same origin.
Unavailable503 renders an unavailable state without a broken sign-in button.
No new production flag or ingress is enabled by the frontend source change.
## 8. Workflow
Read proof, list authorized company metadata, select company, then read licensed
current members. Review person/company/operation and explicitly check confirmation.
Only then send the mutation with the current member revision and a stable UUID key.
## 9. States
Loading, sign-in required, unavailable, company selection, loading team, current
team, review, sending, uncertain/denied result and refreshed team. Expiry removes
team actions and requests a fresh product sign-in. No optimistic permission grant.
## 10. Permissions
Affordances use actor_role from the licensed member response, not company-list
metadata. Owners may review role changes and revoke members. Administrators may
review revoking ordinary members only. Backend current-state rules are final.
## 11. Rules
Selected company and link must match the loaded member page. Changing selection
aborts old requests and clears their state. No client tenant/payer/actor subject
is sent. Membership availability and identity availability remain separate.
## 12. Data
Proof/CSRF, company/member pages and any uncertain command remain in memory only.
No bearer, cookie value, password, localStorage/sessionStorage or synthetic ERP
principal. Browser request body uses only the reviewed selectors/operation/revision.
## 13. UI
Reuse KuubuIdentity and pinned shared ERP Button/Card/Select/Table/Dialog controls.
Desktop uses a table; mobile uses stacked rows with visible actions. Use semantic
colors and minimum2.75rem controls. Do not add fake invitation controls.
## 14. UX
Review names the company/person, explains role boundaries and has an initially
unchecked confirmation. Changing the target role resets confirmation. Once sent,
review fields lock so an uncertain request can be retried unchanged. Dialog cannot
close during transmission. Keyboard/focus behavior uses the shared Radix wrapper.
## 15. Notifications
Only in-page error/status messages. No invitation email or other external message.
## 16. Documents
No payroll, accounting, procurement or personnel documents are requested.
## 17. Errors
401 asks for sign-in;403 reports changed access/subscription; LastOwner explains
that another active owner is needed; other409 requires refresh. Unknown network,
503 or malformed successful responses preserve the reviewed command for retry.
Only bounded known status codes influence copy; raw error bodies are never shown.
## 18. API
product-team-api.ts consumes the fixed same-origin team prefix with credentials,
no-store, redirect:error, bounded timeout and optional abort signal. Validate
response shape, UUIDs, roles, revisions, page limits and no operational grant.
POST includes the CSRF header and confirmed=true only after the review action.
## 19. Events
Only Core emits transactional mutation receipts/audit/outbox. On any success,
including an Existing historical receipt, ignore its snapshot as current state and
fetch members again. The UI never synthesizes a new business event or role grant.
## 20. Configuration
No frontend issuer/tenant override. Existing backend deployment flags remain off
until qualification and public-routing prerequisites are satisfied.
## 21. Security
No authorization rests on email/domain, payer, a company-list role or browser
storage. Client checks improve UX only. A synchronous send guard prevents double
submission; server idempotency/revisions remain authoritative. In-flight responses
cannot replace another company's page; all public product pages bypass fake ERP
admission. No external production requests are permitted in the browser harness.
## 22. Privacy
Display names and company-governance state only. No email, global subject, payer,
tenant or principal IDs in the visible interface. Tokens never enter URLs/logs.
## 23. Reliability
Use separate abort controllers for bootstrap/company paging and selected-member
requests. Revoke stale action state before loading a new scope. No automatic
mutation retries or silently chosen replacement command after an uncertain result.
## 24. Recovery
Retry an uncertain attempt with exactly the same key/body while the screen remains
open. Closing/reloading discards the in-memory attempt and fetches current state;
there is no automatic resend after reload. A later explicitly reviewed request
uses that current revision; Core prevents concurrent stale writes. Team sign-out
clears only the pending team proof, not an unrelated ERP session.
## 25. Acceptance
Node contracts, TypeScript, ESLint, production SSR build and isolated Chrome checks
cover own branded sign-in, confirmation/no double send, historical retry/fresh
read, bounded admin actions, last-owner denial, access denial, pagination,
unavailable boundary and mobile actions. These use a synthetic API, not the actual
Laravel OAuth provider/Core browser integration. That remains a release gate.
## 26. Release
Qualification in progress. First UI build passed18 contracts, TypeScript and nine
browser cases; visual review found mobile actions offscreen in the wide table.
Keep that publication unchanged. The second build replaces the mobile table with
stacked rows and strengthens the mobile browser assertion. Do not borrow first-
build results as proof of the changed second build. No production deployment.

UI02 independently passed the production SSR build, TypeScript, ESLint, all18
Node contracts and all nine Chrome scenarios, zero failures. The strengthened
mobile check opens the visible member action from a stacked row. Desktop/mobile/
review screenshots were inspected. All487 output files retained their size,
SHA256 and nanosecond modification time after SSR/browser consumption. The local
SSR process stopped and port3188 closed. No VITE_* environment values were present
at build time and no environment files were copied into the qualification source.
Evidence: inspection/qualifications/company-team-ui-02/verification.json,
build/contracts/typecheck/browser logs and team-desktop/mobile/review.png.
UI01 and its earlier offscreen-actions screenshots remain unchanged. The React
review covered scope cancellation, stable retry intent, no shared SSR identity
state, confirmation/focus/keyboard behavior and bounded rendering. Actual
provider/Core/browser acceptance and production activation remain pending.
## 27. Ownership
Core owns governance, Laravel verified identity/enrollment/licensing, Perfex its
current invoice/payment responsibilities. GreenWebb portal and Kuubu remain
separately branded, separately authorized product surfaces.
## 28. Remaining work
Complete actual provider/Core/browser qualification;
implement invitations/acceptance/delivery; connect qualified navigation/activation;
qualify operational sessions, tenant/module isolation, recovery and dedicated
installation/licensing/upgrades. The ecosystem goal remains incomplete.
