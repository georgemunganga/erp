# Manual payment PostgreSQL storage probe

Source-only until parent verification records a result. This is an explicit private, synthetic owning-store probe, not a payment API, bank test, permission-grant test or Finance integration. It refuses a nonempty initial database, TCP hosts and names outside its synthetic allowlist. The parent creates a new database on the existing private network-disabled PostgreSQL server and mounts only its Unix socket. Failed databases and logs are retained; the tool never drops a database or changes roles.

Checks use the real EF migration engine, empty down/up, audit-only downgrade veto, mapped request/revise/submit/approve/record/confirm with trusted synthetic actor projections, exact receipts and real PostgreSQL negative DML cases. Actual HTTP/Core permissions, concurrent outstanding capacity and browser integration remain separate gates. Company date here is an explicitly synthetic UTC observation; it does not replace the Core business-date provider.
