# Mightyfin ERP — HRM API

ASP.NET Core 10 (Minimal APIs) backend for the HRM module of the Mightyfin ERP.
It shares the existing `erp` PostgreSQL database and uses the `hrm` schema.

## Architecture

```
src/
├── Mightyfin.Erp.Hrm.Domain        # Entities (43 tables: workers, org, time, workflow, payroll, extras)
├── Mightyfin.Erp.Hrm.Application   # Services, DTOs, interfaces
├── Mightyfin.Erp.Hrm.Infrastructure# EF Core repositories, DbContext, interceptors
├── Mightyfin.Erp.Hrm.Api           # ASP.NET Core host, routes, auth
└── Mightyfin.Erp.Hrm.Tests         # xUnit tests (EF InMemory)
```

## Key conventions

- **Multi-tenant from day one.** Every entity carries a `tenant_id`. The EF
  context applies a global query filter per tenant and auto-populates
  `TenantId` on insert (`HrmDbContext.SaveChangesAsync`).
- **OIDC via Keycloak** (`workforce` realm) for production; dev mode enabled
  with `ERP__AuthMode=disabled` (open access, default tenant
  `HRM:DefaultTenantId`, falls back to `local-tenant`).
- **Audit trail.** A `SaveChanges` interceptor writes append-only
  `audit_entries` for every update/delete.
- **Unique route surface** matching the React frontend mock services under
  `/api/hrm/*` (see `src/Mightyfin.Erp.Hrm.Api/ApiRoutesClean.cs`).

## Run locally

```bash
dotnet run --project src/Mightyfin.Erp.Hrm.Api \
  --ConnectionStrings__Hrm="Host=127.0.0.1;Port=5432;Database=erp;Username=erp;Password=..." \
  --ERP__AuthMode=disabled
```

## Migrations

```bash
export DOTNET_ROOT=$HOME/.dotnet
dotnet ef migrations add <Name> \
  --project src/Mightyfin.Erp.Hrm.Infrastructure \
  --startup-project src/Mightyfin.Erp.Hrm.Api
dotnet ef database update \
  --project src/Mightyfin.Erp.Hrm.Infrastructure \
  --startup-project src/Mightyfin.Erp.Hrm.Api
```

## Docker

```bash
docker compose up --build
```

## Tests

```bash
dotnet test src/Mightyfin.Erp.Hrm.Tests
```

## Production notification credentials

The Amizopower Compose override uses `smtp.amizopower.co.zm` on port587 with STARTTLS. Supply `ERP_SMTP_PASSWORD` through the deployment's private `.env` or secret environment; Compose intentionally fails if it is missing. Never put the mailbox password in a tracked override, issue, log or rendered configuration output. Keep private environment files permissioned to their deployment owner.

The API queues account setup invitations; the outbox publisher sends them. Updating an environment file does not change a running container: recreate only the affected worker with the same qualified image and database/network/resource settings, retain its rollback container, and verify the specific notification reaches SMTP-delivered status. SMTP acceptance is not proof that the recipient opened the message. Preserve expiry checks and the existing retry workflow; do not create duplicate account links just to retry delivery.
