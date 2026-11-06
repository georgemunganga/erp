# Kuubu qualification deployment

This stack is an isolated step toward the shared Kuubu service on2.24.140.104.
It is not a per-customer deployment template or a declaration of SaaS readiness.
One application runtime will serve explicitly authorized organizations after
tenant/session/admission and module enforcement are qualified.

Use an exclusive host directory such as `/opt/kuubu-qualification`. Refuse to
reuse existing directories, project containers or database volumes during initial
setup; inspect partial attempts and retain their evidence rather than deleting
or restoring over them. Existing ERP installations elsewhere are unaffected.

## Inputs and owners

Copy the Compose file into the exclusive host directory. Create root-private
`project.env` with the verified ERP image reference and the inspected PostgreSQL17
repository digest. API images use unique version tags; never overwrite an older
published tag. This source's Docker test-and-publish gate must complete first.

The private directory contains separate `owner.env`, `runtime.env` and PostgreSQL
bootstrap password files. These are never committed or printed. The bootstrap
administrator is only for database/role initialization. Create a nonsuperuser
schema-owner login and a separate nonsuperuser runtime login; the API must never
receive the bootstrap/owner password. No default customer or administrator
identity is seeded. The PostgreSQL secret must be readable by the actual pinned
image's postgres UID while its host parent stays root-private.

Both application environments explicitly set production mode, local auth,
`ERP__Database__MigrateOnStartup=false`, the correct `ConnectionStrings__Hrm`,
empty `ConnectionStrings__Procurement`, empty local-admin email/password and no
external identity/bootstrap configuration. A selected infrastructure tenant is
only a qualification context, not a customer organization or membership grant.

The named owner command applies the existing genuine Core/HRM migrations. New
business tables must never be hand-created to imitate a migration history. Core
approval/documents/Procurement owners and their exact finite-role/native schema
contracts require separate qualification before their capabilities are enabled.
Do not import held main migrations or turn on financial/provisioning flags.

## Acceptance sequence

1. Verify image identity/source and nonroot PDF rendering before creating a stack.
2. Initialize the private database network/volume and distinct database roles.
3. Validate Compose with `config --quiet`; never print expanded secret values.
4. Run the owner service explicitly and retain its actual migration result.
5. Configure runtime ACLs from the owning contracts; no superuser, schema/database
   ownership, owner-role membership or DDL is needed by the API. Preserve native
   append-only/audit boundaries rather than granting unrestricted table DML.
6. Start the API on loopback and verify health, anonymous denials, actual schema
   history, zero default users and backup/restore. No public DNS or proxy route
   is created by this Compose file.
7. Complete tenant resolution, global identity admission, explicit company/team
   mappings and cross-tenant/module denial tests using unrelated synthetic
   organizations before routing customer traffic. Cookie login alone is not
   product enrollment or ERP organization access.

Backups and restoration use exclusive independent databases/directories; retain
the current state before switching. Code rollback keeps prior image/configuration
and requires explicit schema compatibility checks. Do not use `down -v` or remove
the database volume as a routine recovery step. Off-host encrypted recovery,
retention and customer launch acceptance remain required.
