# P25 check definitions after PostgreSQL restore

The private M3a probe stopped before HTTP because P25 readiness compares the full text returned by `pg_get_constraintdef`. In the retained P29 restore, PostgreSQL rendered the varchar arrays in two P25 checks with a cast on each element instead of a cast on the array. The values, order and remaining expression were unchanged. The protected capture is `procurement-m3a-private-runtime-20261026_run01/actual-check-definitions.private.json`.

P25 readiness now accepts the original captured definition or the **exact** restored definition for `ck_pr_approval_outcome_evidence` and `ck_pr_finance_release_request`. Its 17 required check names, all other definitions, indexes, keys, columns, migration/model admission and finite-role checks remain exact. This decision does not alter database constraints, permissions or business authority.

Acceptance still requires a fresh genuine SDK test run and a read-only finite-role readiness probe against the retained private schema. The failed M3a run remains a failed attempt; no HTTP or business outcome is inferred from this source change.
