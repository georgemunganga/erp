# Procurement / Core approval transaction rehearsal

This executable verifies the shared-database transaction boundary while keeping Core
approval tables and Procurement source/audit/outbox tables in their owning schemas.
It refuses a database whose name does not contain `rehearsal`. Use a disposable
PostgreSQL database with synthetic data, never a production connection.

```sh
dotnet run --project backend/procurement-api/tools/ApprovalTransactionProbe
```

Supply `APPROVAL_REHEARSAL_CONNECTION` through the local environment or protected
test-container environment. The executable applies both modules' migrations to the
disposable database. It does not publish a network listener or call external services.

Checks:

- Same physical connection and SERIALIZABLE database transaction for both contexts.
- Source, Core case/stages/assignments, explicit/automatic audit and outbox commit together.
- Failure before saves, after either context save, and after both saves leaves no records.
- Two assignees loading the same case revision: an intermediate vote advances the
  aggregate revision; the stale second save fails concurrency and leaks no decision;
  fresh reload completes quorum, and exact terminal replay changes no rows.
- A PostgreSQL-raised serialization SQLSTATE rolls back both schemas and retries the
  whole callback with fresh contexts; repeated deadlock SQLSTATE stops after three attempts.
- A disposable trigger aborts the first real EF `SaveChanges` with SQLSTATE40001;
  provider-wrapped exceptions still retry the whole transaction and leave one final effect.
- Pre-commit IO error and cancellation roll back without automatic IO retry.
- Mismatched canonical actor scope is denied before database work.

The SQLSTATE injection verifies handling of confirmed PostgreSQL aborts; it is not a
real deadlock reproduction. The pre-commit IO scenario does not prove recovery from an
unknown commit outcome. That recovery requires the actual command's durable idempotency
record and authenticated HTTP retry tests. This rehearsal does not implement/activate
Procurement approval routes, spending authority, funding, source guards, or notifications.

The coordinator may retry a callback up to three times. Load and lock complete current
business state inside the callback, use a stable command key and avoid remote calls or
other external side effects. Persist outbox messages in the transaction for later delivery.
