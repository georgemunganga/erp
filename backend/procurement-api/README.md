# Mightyfin ERP — Procurement API foundation

ASP.NET Core 10 module, following the HRMS Domain → Application → Infrastructure → API layout. It is designed for the existing ERP PostgreSQL database with its own `procurement` schema and `__procurement_migrations` history. It does not create or alter HRMS tables.

The `Api` project and Docker image are an **isolated development/migration host**. The adopted ERP architecture calls for one modular ERP deployment. Production host composition with HRMS identity and organization scope is a P0.5 gate; this project is not a decision to launch Procurement as a separate production service.

This is foundation milestone **P0**, before business CRUD routes. The database contains entity-scoped Suppliers and contacts, Items, Purchase Requests and lines, versioned policy, import batches, audit metadata, and an integration outbox. Financial ledgers, payment details, stock balances, and employee records remain owned by their modules. External references use stable IDs without cross-schema foreign keys.

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

`ERP:AuthMode=oidc` is the default. Development-only `ERP:AuthMode=disabled` supplies a fixed synthetic identity. No business CRUD route is exposed yet. `/health/live` and `/health/ready` are probes; `/api/procurement/v1/meta` requires authenticated tenant and legal-entity claims. The production shared-login bridge is a gate before frontend/API integration.

See [the P0 milestone](../../docs/procurement/PROC-BE-00-BACKEND-FOUNDATION-MILESTONES.md) for schema ownership, migration checks, and CRUD readiness criteria.
