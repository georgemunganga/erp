# P28 governed sourcing DDL preservation rehearsal

**Status: source-only helper, not executed.** The accepted P27 helper is unchanged. This bounded CLI follows its private-container, all-column batched snapshots, migration-history and full Procurement schema-dump approach. Root supplies independently reviewed idempotent zero-to-P28 Up SQL, P28-to-P27 Down SQL and a NEW actual used-P27 `--legacy-sourcing` source database. No schema/SQL/authority is generated here.

Required arguments are shown by `python3 rehearse.py --help`:

- fixed private container `procurement-approval-rehearsal-postgres` only;
- explicit actual used-P27 `--source-db`, bounded `proc_approval_http_*` prefix, distinct new clone names;
- exact migration `20261021065049_P28GovernedSourcingDrafts`;
- reviewed `--up-sql` and `--down-sql` containing that ID and exact `GovernedSourcingRollbackWouldLoseEvidence` stop;
- NEW private `--output` directory. Existing outputs/databases are never overwritten.

No network DSN/credentials, original history deletion, database drops or forced connection operations are accepted. Source is queried and cloned only. Root must close its source fixture normally before cloning; this helper never disconnects it. Private logs/output use umask077. SQL operations set a60second statement timeout and10second lock timeout; failures/logs/clones are retained for inspection. This is not a general untrusted-SQL sandbox: input scripts must be reviewed by root first.

## Required source provenance

The source must have actual P27 migration history and all four Finance identity fields, and neither `execution_version` nor the three new P28 tables. It must retain used funding/submission/outbox evidence. A same-tenant/company `Cancelled` SourcingEvent must join to an existing `SourcingEvent / DraftCancelled` audit with genuine legacy `before`/`after` objects: PascalCase `Id`, `Status: Draft -> Cancelled`, and no P28 evidence-key tokens. Parent establishes actual HTTP creation/cancellation provenance; database shape alone is not cryptographic proof that a real endpoint produced it. The helper does not manufacture that positive history.

## Cases

Ten NEW databases: fresh, used, unused and seven isolated negatives. Each applies Up twice and compares exact data/history/schema after the first application. Every source clone preserves all original columns, including original sealed text JSON/hash bytes and existing history. The unused clone then runs Down, requires full original P27 schema/data/history equality including the genuine old cancellation, and runs Up twice requiring exact original upgraded snapshot equality.

Seven owner-only synthetic **negative** clones exercise the stop separately:

1. Partial event metadata, dropping only `ck_sourcing_owned_draft` before setting one new field.
2. One malformed/recovery `sourcing_draft_evidence` row.
3. One malformed/recovery `sourcing_demand_bindings` row.
4. One malformed/recovery `sourcing_command_receipts` row.
5. Audit-only unique `SourcingEvent / DraftReserved`.
6. Audit-only `DraftCancelled` using the49f0b56 governed envelope keys: executionVersion, sourceEvidenceId, originalDraftFingerprint, commandKey and changes.
7. Outbox-only actual `sourcing.draft_cancelled.v1` event type and producer-shaped payload.

For each new-table negative, only that clone/table's child FK/CHECK constraints may be removed to insert a deliberately non-authoritative owner-only recovery row; PK/unique/NOT NULL stay. No original table rows/history or source database are deleted. These rows are NOT valid domain snapshots, approved demand, matching allocations, worker authority, publication or Finance evidence. Baseline snapshots are captured **after** deliberate fixture changes; failed Down must emit the exact stop and leave data/schema/history completely identical to that baseline. The original source's complete Procurement snapshot is checked again at the end.

## Evidence and limits

All `docker exec` operation stdout/stderr are numbered and retained, including failures. From the upgraded used clone, `p28-checks.tsv` and typed `p28-checks.json` retain ordered table/constraint names, exact `pg_get_constraintdef(oid,false)`, exact `pg_get_expr(conbin,conrelid,false)` and convalidated for all four P28 CHECKs; a missing/unvalidated constraint fails the helper. `postgresql-version.txt` records the actual engine used. These are returned PostgreSQL expressions for later independently reviewed readiness freezing, not inferred SQL normalization. Full migration history snapshots include every history column (including ProductVersion), not IDs alone. A PASS result contains source/clone names and SQL SHA256 values, without DSNs. Full Procurement schema dumps normalize only pg_dump's random restrict/unrestrict tokens. Data snapshots are logical all-column comparisons, not physical database backups; unrelated schemas are outside this helper. Actual PostgreSQL execution, exact manifest/CHECK/FK freeze, scoped runtime grants, HTTP/concurrency behavior, receiving/Finance/AP integration and production enablement require separate gates. AST parsing, `--help`, and `git diff --check` do not certify SQL execution.
