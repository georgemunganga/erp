# Procurement live-data enablement — 19 October 2026

This record describes the database, API and UI deployment checkpoints. It does **not** certify the full procure-to-pay workflow.

## Changes applied

- Backed up the shared `erp` PostgreSQL database to a protected custom-format archive at `/home/amizo/erp-backups/erp-before-procurement-20261019.dump`. A separate restore rehearsal reproduced 97 HRM tables and all 55 then-applied HRM migrations. The rehearsal database was removed.
- Applied eight versioned Procurement EF Core migrations through `20261019160005_P11CatalogPriceProvenance`. The isolated `procurement` schema contains 56 tables, including the migration history table. P10 adds request version and Finance reviewer/evidence fields to budget checks. P11 adds a company-scoped catalog-entry reference and published-version check to request lines. No HRM migration was applied as part of this step.
- Before P10, backed up the shared database to `/home/amizo/erp-backups/erp-before-procurement-p10-20261019.dump` and applied the generated SQL to a disposable restored database. All four new evidence fields were verified there; the rehearsal database was removed.
- Before P11, backed up the shared database to `/home/amizo/erp-backups/erp-before-procurement-p11-20261019.dump`. Its generated additive SQL was applied twice to the restored `erp_procurement_p11_rehearsal` database to check idempotency, then applied once to production. The migration record, two columns and catalog reference constraint were verified in production; the rehearsal database was removed after verification.
- Created a dedicated `procurement_api` database role with use and DML rights on the Procurement schema. Its connection string is in the protected runtime `.env`; the credential is not in source control.
- Built the ERP API using the live HRM checkout plus the Procurement source from this repository. This preserved an uncommitted HRM migration and other live HRM changes absent from this checkout. The exact build context remains at `/home/amizo/procurement-deploy-staging-20261019` on the host; it contains no runtime credentials.
- Started the Procurement-enabled API image as `erp-hrm-api` with the existing ERP network, loopback port, document mount, and a protected persistent data-protection key mount. The previous API images and stopped containers remain available for rollback.
- Rolled forward through `procure-20261019-v13`. Earlier increments added independent supplier approval/rejection, qualification review with a recorded reason, audited purchase-request submission into `PendingPolicyAndBudget`, an append-only Finance budget decision API, and a requester-scoped budget status view. The Finance API requires a separate reviewer, request version, external reference, evidence and confirmed reservation for an `Available` outcome. It does not approve the request. v7 added independent category and item activation with audit. v8 added independently reviewed catalog price publication, supplier and item eligibility checks, overlap protection, withdrawal audit, and employee catalog visibility only for published prices. v9 requires a recorded reason to withdraw a catalog price and exposes that action in the item workspace. v10 lets employees search published prices from the request editor, stores selected catalog provenance per line, and rejects changed or withdrawn prices at submission. v11 adds independent, audited buying-method activation and blocks activation when a linked policy is not Published and effective. v12 adds a company-scoped policy-draft API with versioned, bounded JSON storage and audit hashing. v13 adds a permissioned, read-only validation operation for proposed v1 rule JSON, reporting field-specific issues without persisting or publishing it. There is still no publish or evaluation route. The image has a working health probe and the required GSSAPI runtime library.
- Built the Procurement workspace with `VITE_PROCUREMENT_LIVE_MODE=1` and installed it at the static Nginx path. The former build is retained at `/home/amizo/erp-backups/procurement-ui-dist-before-live-20261019`. API-backed draft pages read ERP data; unsupported routes retain the shared navigation and show an empty workflow status instead of sample transactions. The v14 UI adds `/procurement/workspace/settings/buying-rules`: a guided company-currency editor that checks proposed rules through the v13 API, saves only drafts, and refuses to simplify an existing draft containing conditions its form cannot represent.

## Verification

| Check | Result |
| --- | --- |
| Procurement schema preflight against production API and DB | Passed |
| Local `/health/live` and `/health/ready` | HTTP 200 |
| Public ERP root | HTTP 200 |
| Public anonymous `/api/procurement/v1/meta` | HTTP 401 |
| Container health | Healthy |
| Procurement HTTP workflow tests | 17 passed, including catalog price publication and withdrawal |
| Item Playwright flows | 5 passed against mocked ERP responses, including price review |
| Focused price withdrawal control and Playwright flow | Passed; missing reason is rejected |
| P11 migration rehearsal and production constraint/column check | Passed; additive SQL reapplied cleanly in restored DB |
| Guided catalog request API test | Passed, including wrong price/currency, mixed lines, version retention and withdrawn-price submission block |
| Guided request Playwright flows | 7 passed with mocked ERP responses |
| Method activation API test | Passed: self-review, stale version, missing reason, draft/Published linked policy, cross-company denial |
| Configuration Playwright flows | 5 passed with mocked ERP responses, including buying-method review |
| Policy-draft mapped HTTP test | Passed: permission/company scope, malformed input, sequential versions, UTC dates, stale edit and absent publish route |
| Policy-rule validation mapped HTTP test | Passed: authorized valid and invalid shapes, denied buyer, field-specific issue, no persisted policy |
| Buying-rule editor Playwright flows | 4 passed with mocked ERP responses: create/reload/edit, invalid rules, denied edit, detailed-draft protection |
| v10 UI type check and production bundle | Passed; versioned JS/CSS assets return HTTP 200 |
| Live request Playwright flows | 6 passed, including pending and Finance-confirmed budget messages |
| Live-adapter Playwright flows | 29 passed against mocked ERP responses (one stale text assertion updated and rerun) |
| Public Procurement workspace, vendor route and approval route | HTTP 200 |
| Public item route and versioned UI asset after v8 | HTTP 200 |
| Public anonymous catalog price publication | HTTP 401 |
| Public anonymous catalog price withdrawal after v9 | HTTP 401 |
| Public anonymous catalog and request APIs after v10 | HTTP 401 |
| Public Settings reference-data route and versioned assets after v11 | HTTP 200 |
| Public anonymous buying-method activation after v11 | HTTP 401 |
| Public anonymous policy-draft API after v12 | HTTP 401; publish path HTTP 404 |
| v13 shadow and live readiness, anonymous rule validation | Shadow and live `/health/ready` HTTP 200; public validation HTTP 401; production migration preflight passed |
| Public buying-rules Settings route and versioned UI assets | HTTP 200; UI build/type check passed |
| Public browser without ERP session | Sign-in state; no demo vendor data |

The public reverse proxy does not expose `/health/ready`; its HTTP 404 does not indicate an API readiness failure. Authenticated Procurement CRUD and browser flows still require a real-session production check. The Playwright suite above uses mocked ERP responses and is not that check.

## Deployment caveat and recovery

The host has Docker Compose v1.29.2, which fails to recreate current Docker images with `KeyError: 'ContainerConfig'`. The attempted Compose recreation stopped the former API container. Production was restored with `docker run`, using the same host port, network, environment, document mount and preserved login keys. Use Docker Compose v2 or a reviewed direct-container procedure for subsequent releases; do not repeat the v1 recreate command.

The active image tag is `hrm-api_hrm-api:procure-20261019-v13`. Earlier containers are stopped under `erp-hrm-api-before-*` names, and their tagged images remain available. The current UI build is v14; its prior v11 build is `/home/amizo/erp-backups/procurement-ui-dist-before-v11-20261019`. The v13 shadow container and transient rollout environment file were removed after verification. Before rollback, verify current schema compatibility and retain the database backup. A code-only rollback will leave the additive Procurement schema in place. To roll back the static frontend, replace `reference-ui/dist` with the retained previous directory using a reviewed file move.

## Remaining release gates

1. Test an authenticated employee session and a denied out-of-scope session against Procurement routes.
2. Verify create, update, list, and validation for each live draft record through the production browser and API.
3. Replace empty workflow placeholders with backed integrations as each capability is implemented; preserve the menu and avoid sample transaction data in live mode.
4. Connect the Finance budget decision to the authoritative Finance budget/reservation interface; the present API records a Finance review with external evidence but does not calculate or reserve funds itself.
5. Implement and verify policy evaluation, delegated approvals, PO issue, receiving, invoice matching, and AP hand-off before calling the module complete.
