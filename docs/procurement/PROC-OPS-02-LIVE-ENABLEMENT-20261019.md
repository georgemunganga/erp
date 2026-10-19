# Procurement live-data enablement — 19 October 2026

This record describes the database, API and UI deployment checkpoints. It does **not** certify the full procure-to-pay workflow.

## Changes applied

- Backed up the shared `erp` PostgreSQL database to a protected custom-format archive at `/home/amizo/erp-backups/erp-before-procurement-20261019.dump`. A separate restore rehearsal reproduced 97 HRM tables and all 55 then-applied HRM migrations. The rehearsal database was removed.
- Applied eleven versioned Procurement EF Core migrations through `20261019180916_P14RequestCorrection`. The isolated `procurement` schema contains 56 tables, including the migration history table. P10 adds request version and Finance reviewer/evidence fields to budget checks. P11 adds a company-scoped catalog-entry reference and published-version check to request lines. P12 adds six review/publication evidence fields to policy versions. P13 pins the request version, policy version, amount basis and bounded decision JSON to each rule evaluation. P14 adds the current return reason, actor and time to purchase requests. No HRM migration was applied as part of this step.
- Before P10, backed up the shared database to `/home/amizo/erp-backups/erp-before-procurement-p10-20261019.dump` and applied the generated SQL to a disposable restored database. All four new evidence fields were verified there; the rehearsal database was removed.
- Before P11, backed up the shared database to `/home/amizo/erp-backups/erp-before-procurement-p11-20261019.dump`. Its generated additive SQL was applied twice to the restored `erp_procurement_p11_rehearsal` database to check idempotency, then applied once to production. The migration record, two columns and catalog reference constraint were verified in production; the rehearsal database was removed after verification.
- Before P12, backed up the shared database to `/home/amizo/erp-backups/erp-before-procurement-p12-20261019.dump`. The generated additive SQL was applied twice to a restored disposable database, then applied once to production. Six policy review/publication evidence columns and the P12 migration record were verified; the rehearsal database was removed.
- Before P13, backed up the shared database to `/home/amizo/erp-backups/erp-before-procurement-p13-20261019.dump`. The generated additive SQL was applied twice to a disposable restored database, then applied to production. Four policy-evaluation evidence columns, the unique request-version index, and the P13 migration record were verified. The restore contained 97 HRM tables and 56 Procurement tables; the rehearsal database was removed.
- Before P14, backed up the shared database to `/home/amizo/erp-backups/erp-before-procurement-p14-20261019.dump`. The generated additive SQL was applied twice to a disposable restored database, then applied to production. Three nullable return-evidence columns and the P14 migration record were verified. The restore contained 97 HRM tables and 56 Procurement tables; the rehearsal database was removed.
- Before Core approval P01, backed up the shared database to `/home/amizo/erp-backups/erp-before-core-approval-p01-20261019.dump` (protected custom format). A disposable restore contained 97 HRM and 56 Procurement tables. The P01 SQL applied twice there and once in production; production has five `core_approval` tables and one Core migration history entry. The rehearsal database was removed. Approval actions remain disabled.
- Created a dedicated `procurement_api` database role with use and DML rights on the Procurement schema. Its connection string is in the protected runtime `.env`; the credential is not in source control.
- Built the ERP API using the live HRM checkout plus the Procurement source from this repository. This preserved an uncommitted HRM migration and other live HRM changes absent from this checkout. The exact build context remains at `/home/amizo/procurement-deploy-staging-20261019` on the host; it contains no runtime credentials.
- Started the Procurement-enabled API image as `erp-hrm-api` with the existing ERP network, loopback port, document mount, and a protected persistent data-protection key mount. The previous API images and stopped containers remain available for rollback.
- Rolled forward through `procure-20261019-v20`. Earlier increments added independent supplier approval/rejection, qualification review with a recorded reason, audited purchase-request submission into `PendingPolicyAndBudget`, an append-only Finance budget decision API, and a requester-scoped budget status view. The Finance API requires a separate reviewer, request version, external reference, evidence and confirmed reservation for an `Available` outcome. It does not approve the request. v7 added independent category and item activation with audit. v8 added independently reviewed catalog price publication, supplier and item eligibility checks, overlap protection, withdrawal audit, and employee catalog visibility only for published prices. v9 requires a recorded reason to withdraw a catalog price and exposes that action in the item workspace. v10 lets employees search published prices from the request editor, stores selected catalog provenance per line, and rejects changed or withdrawn prices at submission. v11 adds independent, audited buying-method activation and blocks activation when a linked policy is not Published and effective. v12 adds a company-scoped policy-draft API with versioned, bounded JSON storage and audit hashing. v13 adds a permissioned, read-only validation operation for proposed v1 rule JSON. v15 adds a guarded `Draft` → `PendingReview` transition with author, reason, record-version, company-currency and active-reference checks. v16 adds a read-only synthetic preview and fallback/overlap coverage analysis; its response states `isAuthority: false`. v17 adds independent publish/return decisions, review notes, future effective-date guards, controlled supersession of an open-ended published version, and a read-only effective-version resolver with `isAuthority: false`. v18 automatically records one non-authoritative buying-rule evaluation per submitted request version when one published rule is effective, exposes requester status and a permissioned buyer queue, and keeps blocked results visible. v19 adds a scoped buyer detail view and reasoned `Submitted` → `ReturnedForChanges` → `Submitted` correction loop; submission and return audit events preserve the submitted contents. Return and withdrawal are blocked while Finance has a confirmed reservation or an approval instance is active. It does not approve a request or reserve funds. v20 composes a separate Core approval foundation and adds a buyer-only, read-only approval check that lists policy and Finance blockers. The image has a working health probe and the required GSSAPI runtime library.
- Built the Procurement workspace with `VITE_PROCUREMENT_LIVE_MODE=1` and installed it at the static Nginx path. Future production builds should use `npm run build:live` in `reference-ui`. The former build is retained at `/home/amizo/erp-backups/procurement-ui-dist-before-live-20261019`. API-backed draft pages read ERP data; unsupported routes retain the shared navigation and show an empty workflow status instead of sample transactions. The current UI includes `/procurement/workspace/settings/buying-rules`: a guided company-currency editor that validates and saves drafts without discarding unsupported conditions. The author can request review; an authorized independent reviewer can publish or return with a note visible to the author. A build without the live flag was detected and immediately replaced before closing the earlier milestone; the current served bundle was checked for the live access gate and reviewer controls.

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
| Buying-rule editor Playwright flows | 6 passed with mocked ERP responses: create/reload/edit, invalid rules, denied edit, detailed-draft protection, review submission and sample preview |
| Policy-review mapped HTTP and Playwright flows | Passed: author/current-version/reason/currency guards, locked reviewed draft, UI review transition |
| Policy preview unit, HTTP and Playwright flows | 2 unit tests, focused mapped HTTP check and sample UI flow passed; boundary, overlap, result controls and `isAuthority: false` verified |
| P12 migration rehearsal and production verification | Passed twice on restored database, then applied to production; six columns and migration record verified |
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
| v15 shadow and live readiness, anonymous review, migration preflight | HTTP 200 readiness; public review HTTP 401; preflight passed; container healthy |
| v16 shadow and live readiness, anonymous preview, migration preflight | HTTP 200 readiness; public preview HTTP 401; preflight passed; served live-mode preview bundle and assets HTTP 200 |
| v17 policy publication mapped HTTP suite | 21 passed, including independent review, fallback coverage, return note, effective resolution and audited successor closure |
| v17 buying-rule browser suite | 8 passed with mocked ERP responses; one resource-contention timeout passed when rerun alone |
| v17 shadow and live readiness, anonymous policy decisions, schema preflight | HTTP 200 readiness; public effective/publish/return routes HTTP 401; preflight passed; no migration required |
| v17 live-mode Settings bundle | Served `/procurement/workspace/settings/buying-rules` and versioned JS/CSS HTTP 200; inspected bundle for live gate and reviewer controls |
| v18 request rule evaluation mapped HTTP suite | 23 passed, including immutable request-version evidence, blocked buyer follow-up, retry and access denial |
| v18 request and policy browser suite | 17 passed with mocked ERP responses; UI type check and live-mode build passed |
| P13 restore rehearsal and production verification | Additive SQL applied twice to restored database; four columns, unique request-version index and migration row verified in production |
| v18 shadow and live readiness, new route access, schema preflight | Both `/health/ready` HTTP 200, live container healthy, anonymous queue/status/evaluate HTTP 401, migration preflight passed |
| v18 live-mode UI bundle | Served request and buying-rule routes with versioned JS/CSS HTTP 200; inspected bundle for live gate |
| v19 request correction mapped HTTP suite | 24 passed, including buyer scope, self-return denial, stale version, retained audit evidence, resubmission and confirmed Finance reservation guards |
| v19 request and policy browser suite | 18 passed with mocked ERP responses; UI type check and live-mode build passed |
| P14 restore rehearsal and production verification | Additive SQL applied twice to restored database; three nullable columns and migration row verified in production |
| v19 shadow and live readiness, new route access, schema preflight | Both `/health/ready` HTTP 200, live container healthy, anonymous review/return HTTP 401, migration preflight passed |
| v19 live-mode UI bundle | Served request route and versioned JS/CSS HTTP 200; inspected bundle for live gate |
| P01 Core approval rules and schema | 8 Core tests passed; separate Core and ERP host builds passed; SQL applied twice to restored PostgreSQL and once to production |
| v20 request approval readiness | 3 pure rule tests and 1 mapped HTTP test passed; anonymous API denied; current request, pinned policy and recorded Finance evidence checked without approval authority |
| v20 browser and bundle | Focused Playwright flow passed with mocked ERP responses after a resource-contention setup timeout; TypeScript and live-mode build passed; live route and bundle served HTTP 200 |
| v20 shadow and live API | Both `/health/ready` HTTP 200; new route returned anonymous HTTP 401; Core schema has five tables and one migration row |
| v21 vendor hold mapped HTTP suite | 26 passed, including independent manager, current version, reason, open-order review, company scope, and repeat-action denial |
| v21 vendor hold browser and bundle | Focused Playwright passed with mocked ERP responses; TypeScript and live-mode build passed; public vendor route and versioned bundle HTTP 200 |
| v21 shadow and live API | Shadow and live `/health/ready` HTTP 200; new hold and open-order routes returned anonymous HTTP 401; no schema migration |
| v22 vendor reactivation | 26 mapped HTTP tests passed, including independent reviewer, reason, diligence review, stale version and repeat-action denial; TypeScript and focused Playwright passed |
| v22 shadow and live API | Shadow and live `/health/ready` HTTP 200; new reactivation route returned anonymous HTTP 401; vendor page and versioned bundle HTTP 200; no schema migration |
| v23 supplier eligibility | Pure expiry/renewal test and mapped request API test passed; request references and catalog price publication check current vendor qualification evidence |
| v23 shadow and live API | Shadow and live `/health/ready` HTTP 200; anonymous request creation and catalog reads returned HTTP 401; no migration or UI rebuild |
| v24 catalog eligibility | Mapped HTTP test passed: expired qualification hides the published price; a newer verified qualification restores it. Shadow and live readiness HTTP 200, anonymous catalog HTTP 401, workspace page HTTP 200; no migration or UI rebuild |
| v25 approval route preview | Pure parser, mapped HTTP and focused Playwright passed; buyer UI shows pinned stages or mixed-line warning while `canOpenCase` and `isAuthority` remain false. Shadow and live readiness HTTP 200, anonymous readiness HTTP 401, requests page and versioned bundle HTTP 200; no migration |
| Public browser without ERP session | Sign-in state; no demo vendor data |

The public reverse proxy does not expose `/health/ready`; its HTTP 404 does not indicate an API readiness failure. Authenticated Procurement CRUD and browser flows still require a real-session production check. The Playwright suite above uses mocked ERP responses and is not that check.

## Deployment caveat and recovery

The host has Docker Compose v1.29.2, which fails to recreate current Docker images with `KeyError: 'ContainerConfig'`. The attempted Compose recreation stopped the former API container. Production was restored with `docker run`, using the same host port, network, environment, document mount and preserved login keys. Use Docker Compose v2 or a reviewed direct-container procedure for subsequent releases; do not repeat the v1 recreate command.

The active image tag is `hrm-api_hrm-api:procure-20261019-v25`. The current UI build uses the live-mode flag and includes review, return, publish, sample preview, buyer rule queue, request correction, approval blockers and route preview, vendor hold review and return to active; its prior v11 build is `/home/amizo/erp-backups/procurement-ui-dist-before-v11-20261019`. The v25 shadow container, transient rollout environment file and temporary build swap were removed after verification. The v22, v23 and v24 tagged images remain available for rollback. Older stopped Procurement containers and images were removed, then unused Docker image layers were pruned, recovering about 5.6 GB of Docker space before the v23 build. Before rollback, verify current schema compatibility and retain the database backup. A code-only rollback will leave the additive Procurement schema in place. To roll back the static frontend, replace `reference-ui/dist` with the retained previous directory using a reviewed file move.

## Remaining release gates

1. Test an authenticated employee session and a denied out-of-scope session against Procurement routes.
2. Verify create, update, list, and validation for each live draft record through the production browser and API.
3. Replace empty workflow placeholders with backed integrations as each capability is implemented; preserve the menu and avoid sample transaction data in live mode.
4. Connect the Finance budget decision to the authoritative Finance budget/reservation interface; the present API records a Finance review with external evidence but does not calculate or reserve funds itself.
5. Implement and verify delegated approvals, governed budget release/exception handling, PO issue, receiving, invoice matching, and AP hand-off before calling the module complete. The P13 rule evaluation records evidence only; it cannot approve spending or reserve a budget.
