# Procurement live-data enablement — 19 October 2026

This record describes the database and API deployment checkpoint. It does **not** certify the full procure-to-pay workflow or authorize the UI live-mode switch.

## Changes applied

- Backed up the shared `erp` PostgreSQL database to a protected custom-format archive at `/home/amizo/erp-backups/erp-before-procurement-20261019.dump`. A separate restore rehearsal reproduced 97 HRM tables and all 55 then-applied HRM migrations. The rehearsal database was removed.
- Applied all six versioned Procurement EF Core migrations through `20261018090240_P9DraftCommercialLineLifecycle`. The isolated `procurement` schema now contains 56 tables, including the migration history table. No HRM migration was applied as part of this step.
- Created a dedicated `procurement_api` database role with use and DML rights on the Procurement schema. Its connection string is in the protected runtime `.env`; the credential is not in source control.
- Built the ERP API using the live HRM checkout plus the Procurement source from this repository. This preserved an uncommitted HRM migration and other live HRM changes absent from this checkout. The exact build context remains at `/home/amizo/procurement-deploy-staging-20261019` on the host; it contains no runtime credentials.
- Started the Procurement-enabled API image as `erp-hrm-api` with the existing ERP network, loopback port, document mount, and a protected persistent data-protection key mount. The previous API images and stopped containers remain available for rollback.
- Rolled forward through `procure-20261019-v2` to `procure-20261019-v3`. This added independent supplier approval/rejection, qualification review with a recorded reason, and a working image health probe. The v3 image includes the missing GSSAPI runtime library.

## Verification

| Check | Result |
| --- | --- |
| Procurement schema preflight against production API and DB | Passed |
| Local `/health/live` and `/health/ready` | HTTP 200 |
| Public ERP root | HTTP 200 |
| Public anonymous `/api/procurement/v1/meta` | HTTP 401 |
| Container health | Healthy |
| Supplier HTTP workflow tests | 13 passed |
| Vendor Playwright flows | 7 passed against mocked ERP responses |

The public reverse proxy does not expose `/health/ready`; its HTTP 404 does not indicate an API readiness failure. Authenticated Procurement CRUD and browser flows still require a real-session check before the UI live-mode flag is enabled.

## Deployment caveat and recovery

The host has Docker Compose v1.29.2, which fails to recreate current Docker images with `KeyError: 'ContainerConfig'`. The attempted Compose recreation stopped the former API container. Production was restored with `docker run`, using the same host port, network, environment, document mount and preserved login keys. Use Docker Compose v2 or a reviewed direct-container procedure for subsequent releases; do not repeat the v1 recreate command.

The active image tag is `hrm-api_hrm-api:procure-20261019-v3`. Earlier containers are stopped under `erp-hrm-api-before-*` names, and their tagged images remain available. Before rollback, verify current schema compatibility and retain the database backup. A code-only rollback will leave the additive Procurement schema in place.

## Remaining release gates

1. Test an authenticated employee session and a denied out-of-scope session against Procurement routes.
2. Verify create, update, list, and validation for each live draft record through the browser and API.
3. Enable frontend live mode only after real-session checks and each required route is ready. The current global live gate blocks unsupported pages, including the dashboard and Approvals; a live build would hide much of the existing UI. The deployed frontend therefore remains in demo mode. Browser tests above verify UI contracts using mocked ERP responses and are not a production-session test.
4. Implement and verify submission, delegated approvals, budget control, supplier activation, PO issue, receiving, invoice matching, and AP hand-off before calling the module complete.
