# Synthetic approval submission PostgreSQL probe

This executable verifies Procurement submission evidence and relational constraints. It **does not** open a real Core approval case, authorize spending, invoke Finance, or prove atomic Core + Procurement approval execution.

Use only a fresh disposable PostgreSQL 17 database named `approval_submission_rehearsal`, reachable through a mounted Unix socket; TCP connections are refused by the probe. Apply the generated P0–P21 idempotent migration SQL twice before running. The expected Procurement migration count is 18. The synthetic operator/Finance references are fixtures, not approved policy or actual reservations.

With the repository mounted at `/work`, cached NuGet packages available, and a private socket mounted at `/pgsocket`:

```bash
export APPROVAL_SUBMISSION_TEST_CONNECTION='Host=/pgsocket;Database=approval_submission_rehearsal;Username=postgres'
dotnet restore tools/ApprovalSubmissionPostgresProbe --source /root/.nuget/packages -p:NuGetAudit=false
dotnet build tools/ApprovalSubmissionPostgresProbe --no-restore -m:1 /p:UseSharedCompilation=false
dotnet run --project tools/ApprovalSubmissionPostgresProbe --no-build --no-restore
```

Run from `backend/procurement-api` inside one SDK 10 container bounded to 900 MiB and one logical processor. PostgreSQL needs no published ports; use `--network none`, private disposable data and a shared temporary socket directory. Never substitute a production connection or attach production data volumes. Remove the disposable database/container and socket directory after collecting results.

The probe checks actual database timestamp precision, eight-decimal amount persistence, a valid pin after an operational revision, scoped visibility, immutable context behavior, exact source/version/policy/Finance foreign-key relationships and independent source/version, case and command-key uniqueness. Denial SQL uses transaction rollback; temporary direct SQL is strictly constraint testing and does not claim application-layer authorization. Logs, test TRX and migration SQL/digests belong in protected verification artifacts rather than application source.
