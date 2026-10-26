# M4 RFQ publication policy concurrency

An approved publication decision rechecks the one currently effective `SOURCING` policy, its captured maker and reviewer identity, its rules digest, and the rule for every retained demand line. A policy change committed before the decision transaction begins is visible to this check. A returned or rejected case can still be resolved without a current publication policy.

Publication decisions and policy supersession both use PostgreSQL `SERIALIZABLE` transactions. If they overlap, the database admits an equivalent serial order or aborts a transaction with a serialization failure; the approval coordinator retries confirmed `40001` and `40P01` failures. An overlapping decision may therefore complete before the supersession in the serial order. There is no promise that the decision waits for the policy writer.

The execution role has `SELECT` on policy versions and cannot author policy changes. PostgreSQL 17 requires `UPDATE` privilege for `SELECT ... FOR SHARE`, so the decision uses the serializable policy read instead of a policy row lock. The completed-change checks are covered by `PrincipalRfqPublicationCoreWorkflowTests.Approval_requires_the_opening_policy_to_remain_current_and_identical`. This is a transaction and privilege contract; a physical concurrent private PostgreSQL probe remains a separate runtime check.
