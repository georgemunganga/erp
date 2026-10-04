# Mightyfin ERP — Procurement API foundation

ASP.NET Core 10 module, following the HRMS Domain → Application → Infrastructure → API layout. It is designed for the existing ERP PostgreSQL database with its own `procurement` schema and `__procurement_migrations` history. It does not create or alter HRMS tables.

The `Api` project and Docker image are an **isolated development/migration host**. The existing HRMS API project now composes Procurement in source when `ConnectionStrings:Procurement` is configured, reusing its authentication and resolving company/branch scope. Production permission mapping and rollout remain a P0.5 gate; this project is not a decision to launch Procurement as a separate production service.

The **P0** migration establishes the foundation. The additive **P1–P3 field-coverage** migration adds supplier sites, qualifications, categories, attributes and decisions; catalog price entries; purchase-request allocations, budget checks and policy evaluations; and document-service references. The current schema has 19 Procurement tables. Business CRUD routes are still a separate milestone. Financial ledgers, payment details, stock balances, and employee records remain owned by their modules. External references use stable IDs without cross-schema foreign keys.

## Build

```bash
dotnet build src/Mightyfin.Erp.Procurement.Api/Mightyfin.Erp.Procurement.Api.csproj
```

For an isolated local stack, run `docker compose -f compose.dev.yml up --build`. This creates a disposable development ERP database, applies the Procurement migration in a separate job, and serves the API on `127.0.0.1:18081`. It does not use the live HRMS database.

## Migrate an isolated or approved ERP database

```bash
ConnectionStrings__Procurement='Host=...;Database=erp;Username=...;Password=...' \
  dotnet run --project src/Mightyfin.Erp.Procurement.Api -- --apply-migrations-only
```

The API does **not** migrate on ordinary startup. Use the dedicated migration command before starting the service and verify the resulting schema. Never point this command at production without the normal backup/change process.

## Run

```bash
ConnectionStrings__Procurement='Host=...;Database=erp;Username=...;Password=...' \
ERP__OidcAuthority='https://identity.example/realms/workforce' \
  dotnet run --project src/Mightyfin.Erp.Procurement.Api
```

`ERP:AuthMode=oidc` is the default. Development-only `ERP:AuthMode=disabled` supplies a fixed synthetic identity. No business CRUD route is exposed yet. `/health/live` and `/health/ready` are probes; `/api/procurement/v1/meta` requires authenticated tenant and legal-entity claims. The shared ERP host path is documented in [PROC-BE-02](../../docs/procurement/PROC-BE-02-SHARED-HOST-ACCESS.md); action-specific permissions and end-to-end login tests remain gates before frontend/API integration.

See [the P0 milestone](../../docs/procurement/PROC-BE-00-BACKEND-FOUNDATION-MILESTONES.md) and [field-coverage audit](../../docs/procurement/PROC-BE-01-FIELD-COVERAGE-AND-SCHEMA-GATES.md) for schema ownership, migration checks, and remaining CRUD gates.
