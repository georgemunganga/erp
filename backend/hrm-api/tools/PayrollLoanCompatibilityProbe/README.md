# Payroll loan compatibility probe

This checks a known contract between deployed HRM Infrastructure and Domain binaries. It prepares the loan-recovery state machine with the .NET JIT without invoking the method or connecting to a database. Run it against a private copy of the complete `/app` directory from the candidate image:

```sh
dotnet run --project backend/hrm-api/tools/PayrollLoanCompatibilityProbe -- /path/to/copied-app
```

Exit 0 means that the retained live loan model is present and this method can be prepared. Exit 1 records a model, loading or JIT error; exit 2 indicates invalid input. The check requires decimal OpeningRecoveredAmount, string RecordType and nullable DateOnly IssueDate because existing production records depend on them. A self-consistent older assembly set must fail this check too. This is a targeted deployment check; API authorization, financial behavior, migrations and other binary contracts require their own tests.

On 20 October 2026 the probe reproduced `MissingMethodException` for `SalaryAdvance.get_OpeningRecoveredAmount()` in `payroll-audit-20261020-v37`, which replaced Domain/Application over a different Infrastructure build. The retained Procurement v36 assembly set contained both loan-opening fields and passed the same JIT check. See [module-connect issue 29](https://github.com/georgemunganga/module-connect/issues/29).
