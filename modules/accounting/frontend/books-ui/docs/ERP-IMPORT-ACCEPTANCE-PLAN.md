# Accounting UI import acceptance plan

Status: **AUTHORED, NOT EXECUTED**. This is an isolated UI preview import, with no Accounting backend, financial authorization, posting, bank connection or durable ERP records. Core owns identity, branding and company/branch context. No live Core authentication or deployment proof is claimed by mocked browser results.

The imported baseline contains 69 tracked files and 124 distinct workflow fixtures. Preserve the registry, feature fields, engine and all original cases. `scripts/test-workflow-engine.mjs` checks local effects, receipts, exact modeled local calculations/validation and generates fictional workflow fixtures; running it writes local result/fixture files. Its source is not evidence that those tests passed after this import. The original `tests/workflows.spec.ts` suite covers all 124 flows plus required-field/cancel, mobile layout, contextual bill action, module create routes and major route rendering. Root adapts only ERP admission/base URL/storage setup for that existing suite.

The separate `playwright.erp.config.ts` admits only `tests/erp-integration.spec.ts`, uses the operator-started loopback server on port 3402, one Chromium worker and zero retries. No web server is started by the config. Choose the existing reviewed browser cache or an explicit `CHROMIUM_PATH`; no package/browser installation is required by these files. `ACCOUNTING_ERP_RESULTS_DIR` can place result JSON, screenshots and retained failure traces in a protected evidence directory.

Eight new mocked integration cases cover:

1. Anonymous Core `me` response redirects to `/sign-in?returnTo=` with the exact local Accounting route/query. An inert local sign-in landing page records the URL; changing the mocked `me` response and navigating back simulates the return. No credential form or login POST is performed.
2. Non-admin user cannot mount the preview or its local stores.
3. Core workspace-context 403 fails closed, without legacy fallback or local store initialization.
4. Workerless ERP admin uses synthetic Core company, nondefault Poppins font and distinct public color tokens, with an explicit browser-local preview disclosure.
5. Module switcher retains assigned Accounting/Procurement/HRM links and puts ERP Core last. An ERP-admin-only fixture does not gain HRM. Global settings links to the compatible Core directory `/core`; the newer settings route deployment remains an audited gap.
6. Desktop sidebar collapses and expands while preserving imported workflow navigation.
7. Mobile drawer preserves navigation and avoids document overflow.
8. A fictional customer workflow completes and reloads its local result/data in the exact principal/company namespace. Old Books storage keys remain unused.

Every integration case automatically installs the network guard before navigation. Only same-origin static GET/HEAD requests and exact mocked Core GET endpoints are admitted: `/api/hrm/auth/me`, `/api/hrm/branding`, `/api/core/v1/workspace-context`. Every network mutation is aborted and fails the case; all other API paths, external/IdP requests and uncaught client errors fail. There is no remote fixture fetch, credential value or live branding/account data. The fixtures use `example.test` and synthetic IDs. Real host compatibility fallback (`404` to existing HRM choices) remains a separate source/acceptance concern; a `403` must never trigger it.

Planned operator commands from the UI directory, after the separately reviewed dev/preview server is ready:

```sh
node scripts/test-workflow-engine.mjs
npx playwright test --config playwright.erp.config.ts
```

Run the adapted original suite separately with its reviewed full-import config. Run typecheck/build through existing scripts and record each actual result independently. Do not label 124 local engines or mocked browser cases as 124 accounting server workflows. Preserve failed runs and their traces; a fresh passing result must reference the exact committed app/test source and staged asset manifest. Production backend/schema/role changes are outside this plan.
