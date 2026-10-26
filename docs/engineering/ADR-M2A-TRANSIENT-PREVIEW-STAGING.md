# M2a transient preview staging

## Decision

The immutable mapped rows of a **Previewed, uncommitted** Vendor/Item CSV import are transient input. The existing 15-minute preview lifetime is their technical TTL. A finite maintenance role clears only expired uncommitted snapshot text, persists an `Expired` batch state, and appends one scoped audit event in the same PostgreSQL transaction. Original CSV bytes are never stored. Batch identity, actor, tenant/company, digests, row counts, and audit remain.

Committed import batches, row receipts, masters, and their audit are approved business history. Their retention and legal hold remain with the existing company governance. This ADR does not choose a period or automate their deletion. Persisted metadata and audit can still grow; broad activation needs a separate owner-approved lifecycle and capacity review.

## Admission and authority

New preview writes use one tenant/company advisory transaction lock before checking all uncleared, noncommitted snapshots. Default limits are three per actor/company and 64 MiB of canonical UTF-8 snapshot text per company. Expired but unswept and malformed old noncommitted snapshots still count. Invalid or excessive configured limits fail closed; a full quota is HTTP 429 `import-staging-full`. Repreview creates a separate batch only if quota permits, so it never overwrites an uncertain commit.

An API-only `BackgroundService` requires explicit `ConnectionStrings:ProcurementImportMaintenance` and `Procurement:MasterImport:Maintenance:Enabled`. Its username must differ from the Core and ordinary Record connections while targeting the same database. Physical role admission requires batch SELECT and only five batch UPDATE columns (snapshot text, state, version and update metadata), audit INSERT, and no other Procurement/Core/HRM table access, ownership or DDL. It has no credential fallback. New-work readiness requires a recent successful role admission and sweep. Misconfiguration, lost connectivity or extra privileges fail closed.

The worker processes at most 50 schema-version-1 Vendor/Item Previewed rows per transaction using `FOR UPDATE SKIP LOCKED`. It requires expiry, a nonnull snapshot and absent command/result fields. The row lock excludes in-flight commits. The same transaction clears the snapshot, persists Expired and adds a service-actor audit using tenant/company returned from that exact row. It does not mutate masters, receipts, committed batches or the ordinary EF write guard.

GET recovery uses current import and native action policies, Core scope, physical reader readiness and owning actor. It stays available if new-work activation or maintenance is unavailable. A still-Previewed row remains uncertain even after its clock expiry. Only the locked, persisted Expired state is terminal for clearing a browser command journal.

## Verification before activation

Source tests cover quota boundaries, invalid configuration, connection separation and health expiry. Genuine private PostgreSQL acceptance must cover two actors racing for one company quota, expired-but-unswept accounting, finite-role privilege denial including every non-update column, a long-running commit row lock against the sweep, rollback when audit INSERT is denied, one audit per expired row, strict 50-row batches, and unchanged committed batches, receipts and masters. A normal-cookie recovery check must prove current-grant revocation and persisted Expired handling while the worker and new-work flag are off. No production grant or role is created by this source change.
