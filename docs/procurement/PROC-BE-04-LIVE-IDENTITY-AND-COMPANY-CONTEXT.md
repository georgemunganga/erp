# PROC-BE-04 — Live Procurement identity and company context

**Document type:** Backend/frontend integration milestone

**Status:** Implemented in source and tested; live activation pending

**Prepared:** 4 October 2026

**Hierarchy:** ERP → Procurement → Shared Access → Live Identity and Company Context → Resolve Context

## 1. Feature Identification

`PROC-BE-04` connects the separately deployed Procurement UI to the existing ERP session and introduces a server-owned context for the first draft CRUD screens. It follows [PROC-BE-03](PROC-BE-03-FIRST-CRUD-OPERATIONS.md).

## 2. Purpose and Business Outcome

A Procurement screen must know whether it is showing demonstration data or serving a real employee. Live operations use the authenticated employee and verified company; browser-selected demo personas never authorize data access.

## 3. Scope and Exclusions

In scope: read-only identity/context discovery, strict company defaulting, frontend mode detection and visible mode state, tests and deployment preparation. Excluded: live CRUD screen wiring, supplier portal identity, configurable multi-company delegation, production migration and live activation.

## 4. Parent and Related Features

Parent: Procurement Foundation. Related: ERP login, HRMS worker profile, Organization legal entities and locations, [PROC-BE-02](PROC-BE-02-SHARED-HOST-ACCESS.md) scope middleware, and [PROC-BE-03](PROC-BE-03-FIRST-CRUD-OPERATIONS.md) CRUD routes.

## 5. Actors and Participants

Employee, shared ERP identity handler, HRMS worker record, Procurement scope resolver, Procurement UI, and deployment operator. The server resolves identity and company; the browser only displays them.

## 6. Entry Points and Triggers

The UI checks `/api/hrm/auth/me` and then `/api/procurement/v1/context` when `VITE_PROCUREMENT_LIVE_MODE=1` is set at build time. The existing same-origin `hrm_session` cookie is sent by the browser. Opening or refreshing the Procurement UI rechecks the context.

## 7. Preconditions

The HRMS session is valid; the employee is active, mapped to a workforce subject, and has a Procurement admission role; the worker's current company is resolvable from HRMS; the Procurement module is configured in the shared ERP host.

## 8. Workflow

Browser starts in loading state → checks ERP session → requests Procurement context → server resolves tenant, worker and authorized company → UI enters live-ready mode. On preview hosts or when live mode is disabled, the UI remains an explicit demo. When live mode is enabled and identity/context fails, it enters unavailable state with retry or sign-in guidance; it never falls back to demo records.

## 9. Status / State Transitions

Frontend mode: `Loading → Demo | Live-ready | Unavailable`. The demo is the default when the live build flag is off. Retry reloads and rechecks identity. Logging out or session revocation invalidates live context on the next check. No purchasing record changes state in this milestone.

## 10. Permissions and Data Scope

The context endpoint requires `procurement-access` and an active worker in the resolved company. The initial company choice is restricted to the worker-verified company; tenant-wide legal-entity membership alone is not authority. Existing branch assignment restrictions still apply. The UI's role/entity selectors from localStorage are demonstration state only and must not become API headers.

## 11. Business Rules

Use HRMS stable IDs, not company names, for any future API scope header. An employee mapped to a nondefault tenant company should resolve to their company when no header is sent. Invalid or forged entity/branch selections return a denial. A failed live context must not expose or imply real Procurement records.

## 12. Data Fields and Ownership

Context fields include tenant ID, worker ID, current company ID/name, optional branch and org-unit IDs/names, roles/allowed actions, and permitted company choices. The authenticated subject remains server-side. HRMS/Organization owns these references. Procurement owns no copied employee profile or payroll information.

## 13. Screens and Data Views

The shared Procurement shell displays Demo, Live-ready or Unavailable clearly. Demo selectors are hidden or disabled in live mode. Internal transaction screens and the separate supplier portal are hidden behind a holding state until their API and external identity wiring milestones are accepted.

## 14. User Experience / Human Behaviour

The user should know whether a record is real before clicking Add or Save. Unavailable states should offer a retry and a route to ERP sign-in. A company selector should not show companies the worker cannot use.

## 15. Notifications and Communications

No business notification is sent. Access failures show a short user-facing explanation without exposing tenant or employee details.

## 16. Documents and Attachments

None. Document access must later use the same live context and record permission.

## 17. Exceptions and Edge Cases

Handle anonymous, expired/revoked session, inactive worker, missing worker mapping, unrelated role, tenant with no company, worker in a nondefault company, forged headers, and API outage. An OIDC subject without the supported workforce mapping remains denied.

## 18. Integrations / APIs / Events / Sandbox

The bridge reads ERP auth and HRMS/Organization data inside the shared API host. The live ERP nginx proxy already forwards `/api/*` to the HRMS API on the current Amizopower host. No business event or external write is produced. Preview builds keep demo mode explicit.

## 19. Audit / Security / Privacy

The HttpOnly session cookie is not read by JavaScript. Context returns minimum identity and organization data needed for UI decisions. Backend route policies and record scope remain authoritative after UI wiring. Cross-site request protection for cookie-authenticated mutations is a separate live-write gate.

## 20. Reports / Dashboards / Metrics

No business KPI. Operators may monitor 401/403 context failures, unavailable responses, and session-to-context success rate without logging private employee details.

## 21. Configuration and Localization

The frontend live-mode switch is off by default until the API, database and UI screens are ready. Company labels come from Organization. Permission grants remain initial role-based values pending tenant configuration.

## 22. Non-Functional Requirements

Context lookup should be fast, scoped and repeatable. The UI must preserve clear loading and error states and never display demo records as live data. Browser refresh and company change should trigger revalidation.

## 23. Postconditions

A successful live context provides server-issued IDs and permissions for later CRUD calls. A demo session is explicitly labeled. No supplier, item, request, budget or payment record is changed.

## 24. Failure and Recovery

An API outage or rejected context yields Unavailable and retry. A revoked session requires sign-in. A stale company selection is discarded and re-resolved server-side; it is not guessed from the browser's mock company name.

## 25. Acceptance Criteria

1. A valid active worker receives only a company they are authorized to use.
2. A worker in a nondefault company resolves correctly without a browser header.
3. Anonymous, inactive, unrelated-role and forged-scope requests do not receive a live context.
4. Live-mode failure never displays demo transactions as operational data.
5. Demo builds remain clearly labeled and do not send writes to the live API.
6. Backend tests and frontend build pass before any release.

## 26. Test Scenarios

Eight focused backend tests passed for context field minimization, permission exposure, active-worker checks, branch/company scope, and a worker in a nondefault company. The frontend production build and TypeScript check passed. An end-to-end session test against a deployed API remains required for session revocation, HTTP 401/403 behavior, API outage, and live flag activation.

## 27. Ownership / Dependencies

ERP identity/HRMS owns session and worker mapping, Organization owns company/location, Procurement API owns context response, UI owns explicit mode state, and deployment owns enabling the module after its separate schema migration.

## 28. Open Decisions / Assumptions

Multi-company worker assignments, branch-scoped Procurement row filtering, tenant-configurable permission grants, OIDC subject mapping, CSRF protection for live mutations, and production activation order remain later gates. This milestone returns one verified company choice unless those policies are settled.

## Activation order

1. Review and apply the separate Procurement migration to the target ERP database; configure `PROCUREMENT_DB_CONNECTION` for the shared API and deploy that API build.
2. Verify `/api/procurement/v1/context` through a real ERP session for allowed, denied, inactive and nondefault-company users.
3. Wire the Vendor, Item and Request screens to their draft APIs, including live permission, error and version handling. Keep demo transactions hidden in live mode.
4. Build the frontend with `VITE_PROCUREMENT_LIVE_MODE=1`, deploy it, and verify the end-to-end flow. The default build leaves the current clearly labeled demo available.
