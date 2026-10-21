# Procurement authority PostgreSQL probe

Source-only until the coordinated SDK verification slot and finalized P21/P22 model are available.

Use a private disposable PostgreSQL database named `proc_authority_probe_<unique suffix>`.
Set `PROCUREMENT_AUTHORITY_PROBE_CONNECTION` privately; never use the production connection.
Run `dotnet run --project backend/procurement-api/tools/ProcurementAuthorityProbe`.

The probe creates the model in that disposable database and verifies independent activation,
atomic supersession rollback (including audit), retirement of old limits, revocation,
published-field immutability and concurrent logical-version allocation. It does not execute
payments or touch production. It does not claim migration coverage: the additive P22 migration
must separately be generated from the finalized P21 snapshot and applied twice in a disposable
migration rehearsal before release. Canonical identities in this isolated persistence probe are
fixtures; the separate HTTP tests exercise the real shared identity/organization adapter.
