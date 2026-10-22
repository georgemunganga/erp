# Disposable spending-authority identity persistence probe

This tool applies migrations and writes **synthetic records**. Run only against a newly created disposable database whose name starts `authority_identity_`, using the verified private rehearsal PostgreSQL socket mounted at `/authority-identity-private-socket` (or a separately verified disposable loopback server). It refuses other hosts/database names. Never supply a production connection.

Provide `ConnectionStrings__Procurement` through a protected environment variable; do not put credentials into commands, screenshots or Git. Run the existing .NET10 SDK image with this checkout and package cache mounted, network disabled (`--network none`) and the private server’s existing socket bind source mounted read-only at `/authority-identity-private-socket`, then `dotnet run --project backend/procurement-api/tools/SpendingAuthorityIdentityProbe --configuration Release`.

Checks: P29→P30 empty upgrade/down/re-up; persisted legacy row unchanged without identity backfill; actual grant/evidence create/submit/activate and reload; append-only SaveChanges guard; real PostgreSQL owner/created/submitted FK, named uniqueness and empty-worker CHECK failures; used Down and audit-only Down rejected under the retained locks; grants/evidence/audit/outbox/history footprint unchanged after denials; no pending model change. Prints a bounded JSON result without credentials or rows.

This is a persistence/migration probe under a disposable database owner. It is **not** proof of Core canonical account swaps, finite runtime database privileges, mapped HTTP authorization, browser acceptance, production readiness or completed Procurement. Those remain A3–A5 in `docs/procurement/PROC-AUTH-WORKER-01.md`.
