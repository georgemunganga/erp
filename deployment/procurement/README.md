# Procurement deployment and source boundaries

Updated 22 October 2026. This document distinguishes source verification from production deployment. The Procurement workspace uses real ERP adapters for enabled capabilities; browser-local records are not operational ERP records.

## Current routing (read-only verified)

- Procurement frontend source: existing `georgemunganga/module-connect`, `reference-ui/` plus its `src/platform/` API clients. Do not duplicate it into the tracked HRM snapshot or re-import the design repository.
- Public workspace: `/procurement/workspace/`; nginx serves `/home/amizo/procurement-ui/reference-ui/dist/` with SPA fallback. `/procurement` redirects to this workspace.
- Legacy frontend service: `amipower-procurement-ui.service`, Node22, `/home/amizo/procurement-ui/.output/server/index.mjs`, loopback3301. It is a separate fallback; restarting it does not replace the static workspace assets.
- ERP shell/HRM frontend: `amipower-erp-web.service`, loopback3300, existing tracked HRM frontend path. The shared host exposes `/api/hrm/` and `/api/procurement/v1/` through the same authenticated ERP origin.
- Routing configuration: `/etc/nginx/conf.d/erp-amipower.conf`. Preserve HRM/Core, shared assets, login and supplier routing when making a narrowly scoped workspace release.

## Source and runtime are different checkpoints

Reviewed source is merged through ERP PR14 (`42fc263`) and UI PR45 (`b2e824f`). These merges did **not** deploy or migrate production. The production backend remains the retained P19 release (`a7645e2`); new source includes P20InvoiceDraftMaintenance and later separately reviewed CRUD changes. Consult the [24-hour release board](../../docs/procurement/release/PROC-CRUD-24H-20261022.md), current release record and image/source manifests before deciding what is live.

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
