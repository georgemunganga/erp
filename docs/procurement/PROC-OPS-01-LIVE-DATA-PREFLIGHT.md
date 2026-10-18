# Procurement live-data preflight

**Status:** Draft release evidence, 18 October 2026. This check is read-only and is not a release approval.

## Current deployed state

The ERP API and shared PostgreSQL container are healthy. The running API has an empty `ConnectionStrings__Procurement` setting, and the shared database contains the `hrm` schema but no `procurement` schema. The deployed Procurement workspace remains a demo build. Draft APIs and UI adapters exist in source; they are not yet connected to production data.

Do not enable the live UI on this deployment until the Procurement schema is migrated, the API receives a restricted Procurement connection, the new host is deployed, and real-session authorization and tenant isolation are verified. The remaining purchasing, approval, budget, receiving, invoice and Finance workflows require their own acceptance evidence.

## Repeatable schema check

From the ERP checkout, run:

```bash
bash backend/hrm-api/scripts/procurement_release_preflight.sh API_CONTAINER DB_CONTAINER [DB_USER] [DB_NAME]
```

The script checks that the API has a non-empty Procurement connection setting, the shared database has the `procurement` schema and migration history, and applied migration IDs exactly match the checkout. It prints no connection string or password, does not call the application API and makes no database changes. Exit `0` means only those schema checks passed; exit `2` means a blocker was found.

The current deployment returns two blockers: missing API connection and missing schema. The same script passed against a disposable PostgreSQL 17 container after applying all Procurement source migrations through the isolated migration host. The disposable containers were removed after the check.

## Remaining release evidence

1. Approve a backup and restore plan, migration window and rollback decision for the shared ERP database.
2. Create a least-privilege Procurement database credential and apply source migrations through the dedicated migration command, without changing HRM tables.
3. Deploy the shared ERP API with `ConnectionStrings__Procurement` supplied through protected configuration; rerun the preflight.
4. Verify a real active ERP session can select an authorized company and use each draft API; test denied actions, another worker's draft, another company and expired/inactive identity.
5. Build the Procurement workspace in live mode only for capabilities whose API and user journey passed acceptance. Confirm the deployed bundle contains no fallback demo rows on live routes.
6. Record a post-deploy smoke test, monitoring owner and support path. Keep approval, payment, stock and supplier activation gates closed until their workflows pass separate controls.

See [the live build roadmap](PROC-ROADMAP-LIVE-BUILD.md) for capability status and [the backend README](../../backend/procurement-api/README.md) for the isolated migration command.
