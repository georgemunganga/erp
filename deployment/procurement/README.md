# Procurement deployment and source boundaries

Updated 24 October 2026. This document distinguishes source verification from production deployment. The Procurement workspace uses real ERP adapters for enabled capabilities; browser-local records are not operational ERP records.

## Current routing (read-only verified)

- Procurement frontend source: existing `georgemunganga/module-connect`, `reference-ui/` plus its `src/platform/` API clients. Do not duplicate it into the tracked HRM snapshot or re-import the design repository.
- Public workspace: `/procurement/workspace/`; nginx serves `/home/amizo/procurement-ui/reference-ui/dist/` with SPA fallback. `/procurement` redirects to this workspace.
- Legacy frontend service: `amipower-procurement-ui.service`, Node22, `/home/amizo/procurement-ui/.output/server/index.mjs`, loopback3301. It is a separate fallback; restarting it does not replace the static workspace assets.
- ERP shell/HRM frontend: `amipower-erp-web.service`, loopback3300, existing tracked HRM frontend path. The shared host exposes `/api/hrm/` and `/api/procurement/v1/` through the same authenticated ERP origin.
- Routing configuration: `/etc/nginx/conf.d/erp-amipower.conf`. Preserve HRM/Core, shared assets, login and supplier routing when making a narrowly scoped workspace release.

## Source and runtime are different checkpoints

The accepted source baselines are ERP `6994de01400a97becc6ed4c5e95cb63a0db082c3` and UI `403bfdac505d593921a5bab1ba3388896205325f`. A read-only production audit on 24 October confirmed Procurement history through `20261022144102_P24CorePrincipalRecurringPurchasePlanning`, 191 application tables, and healthy API/publisher processes. P30 ERP PR50 and UI PR65 remain draft integration releases; the stacked P31 invoice PR51 is disabled source only. Source merges and private test results do **not** deploy or migrate production. Consult the [live P23/P24 acceptance](../../docs/procurement/release/PROC-OPS-P23-P24-LIVE-ACCEPTANCE-20261022.md), [current workflow roadmap](../../docs/procurement/PROC-ROADMAP-LIVE-BUILD.md), [CRUD coverage](../../docs/procurement/release/PROCUREMENT-CRUD-COVERAGE-20261024.json), and exact image/source manifests before deciding what is live.

Enabled CRUD includes only capabilities accepted by the live backend metadata and adapters. Missing or failed responses show a real error/unavailable state. Never fall back to browser-local data after an API failure. Procurement owns its records and procurement validation; Finance owns budgets, accounting, AP and payments; HRMS/Core own employee and organisation identities.

## Repeatable workspace release

1. Record exact reviewed ERP and UI commits and their companion contract/migration requirements. Preserve current source/assets/image and recovery evidence.
2. Use the existing cached Node22/dependencies and unchanged lockfiles. From the chosen UI worktree's `reference-ui/`, run `VITE_PROCUREMENT_LIVE_MODE=1 npm run build:staged`. This writes `.verification-dist`, not public `dist`.
3. Verify TypeScript, relevant units, staged Playwright, denied/conflict/unknown-write states and actual private API/PostgreSQL browser flows. Record candidate asset/source hashes and keep first failure traces. Mocked API tests do not prove database persistence.
4. For backend/schema changes, first verify a recoverable backup, private clone migration/startup and actual retained HRM/Core compatibility. Do not generate broad inferred HRM migrations to repair unrelated historical snapshot drift.
5. Release the exact verified backend/schema and static candidate through the coordinated release process; atomically replace the workspace assets only after its owning API contract is ready. Preserve the existing service/routing topology. An ordinary static workspace release does not need a legacy Node service restart.
6. Verify authenticated representative journeys, denied scopes, readiness, nested workspace routes, assets and retained Core/HRM entry points. Record the image, commits, migration IDs, time and outcome; a source merge or HTTP200 alone is not completion.

## Recovery

P20 adds nullable invoice branch ownership and retained invoice-line activity. Its Down deliberately refuses to destroy used branch/removal history. Do not force a database downgrade, remove migration-history rows or discard records to make a rollback appear successful. Rehearse a compatible forward repair or restricted code/feature rollback, retaining the database and audit evidence. Existing held governed-workflow migration branches have a different lineage and must be reconciled before deployment; never copy both P20 branch additions into one runtime chain.

No production deployment, migration, identity/role change or payment operation is performed by updating this document.

Later principal approval, manual-payment, sourcing and commercial migrations add retained-data downgrade vetoes. A P24-to-P30 rollout requires its own complete backup/restore, exact migration/finite-role rehearsal and authenticated acceptance pack; the earlier P23/P24 release procedure is a reference, not evidence for that successor rollout. Existing production writers and flags stay unchanged during private verification.
