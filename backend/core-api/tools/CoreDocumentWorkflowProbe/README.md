# Core document PostgreSQL workflow probe

Run only against a disposable database whose name includes `rehearsal`. This tool applies Core document migrations and writes synthetic fixtures. It never connects to the ERP host, reads an invoice, or executes Finance actions.

```sh
CORE_DOCUMENT_TEST_CONNECTION='Host=127.0.0.1;Database=core_docs_rehearsal;Username=doc_test' \
  dotnet run --project backend/core-api/tools/CoreDocumentWorkflowProbe
```

Use a dedicated isolated PostgreSQL instance, not an ERP database. The local verification server uses `postgres:17-alpine`, no network interface beyond its isolated loopback, no published port and a disposable tmpfs data directory. The runner joins that container's network namespace. Trust authentication is used only inside that disposable test namespace with synthetic data; this is not a production configuration.

The probe verifies:

- Applying migrations twice leaves a usable schema.
- Replaying an upload key retains the exact version; changed content conflicts.
- With one saved file, twelve simultaneous uploads yield nine saves and three limit failures.
- Exactly ten metadata versions, scan results and corresponding operation evidence remain.
- An authorized download is recorded and another company cannot resolve its version.
- Completed requests leave no private staging files.

Authorization and the scanner are fixtures here to isolate actual PostgreSQL transaction, locking, uniqueness and scope behavior. The mapped HTTP test separately uses ERP authorization/company/branch middleware. The [scanner probe](../CoreDocumentScannerProbe/README.md) separately checks a real ClamAV engine. This tool alone is not end-to-end or production acceptance evidence.

After the run, remove the disposable database/container and retain the result. `verify-document-constraints.sql` can also run against the rehearsal schema; it wraps its relational fixtures in a rollback.


## Optional exact document-use persistence probe

Add `-- --use-pins` to the command above after the central additive Core use migration has been generated and reviewed. The default mode, upload replay and ten-file/concurrency assertions are unchanged. Both modes require the existing disposable `rehearsal` database restriction. `--use-pins` reuses the same contexts, migration stream, private store and fixture scanner; it does not create an alternate schema or use `EnsureCreated`.

The opt-in helper first completes the existing workflow. It then verifies:

- Real transaction staging, Save and commit of an exact two-version review use set.
- Fresh-context original IDs/time/content/scan digest recovery, reversed selection order, changed-set/revision denial and no replay writes.
- A submicrosecond input at the final persisted scan boundary round-trips as canonical UTC PostgreSQL microseconds without changing the digest.
- A separate publication use retains those same versions without copying a blob.
- Rollback before and after Save leaves no use set, pin or other orphan/evidence change.
- Same-context managed retirement of persisted retained evidence is denied and original state remains unchanged.
- Foreign tenant/company contexts cannot resolve the use rows or recovery reference.
- Four isolated, named scoped FK denials and two named shape CHECK denials under parameterized Npgsql transactions. A genuine extra upload supplies another owner's valid parents; temporary cloned rows are constraint-negative fixtures, not authorized business outcomes. Every negative rolls back and compares fresh complete Core data, column/default/constraint/index definitions and migration history before checking original recovery again.

This is **internal persistence verification**. The fixture owner access intentionally allows synthetic uploads, and the scanner is deterministic. No purpose-scoped RFQ authorization, canonical worker/maker-checker verification, enlisted Procurement publication transaction, before-snapshot lifecycle lease, cross-transaction retirement/purge race proof, external delivery or runtime capability activation is established. The repository remains internal and unregistered. Managed context denial is not a substitute for the future cooperative lifecycle fence. This source addition has not been built or executed; retain actual logs only when the parent schedules the migration/PG gate.

The helper writes synthetic use evidence and one additional uploaded owner after the default ten-file assertions; do not interpret the final database as still containing only ten documents. Database/container lifecycle remains with the runner. The tool removes only its own temporary private file directory, as before; it never drops a database or logs a connection string.
