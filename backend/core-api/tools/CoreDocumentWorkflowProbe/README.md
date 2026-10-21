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
