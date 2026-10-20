# P26 private migration rehearsal

**Source-only helper; not executed.** Root runs this only after generated migration/SQL review and resource clearance. It creates four new disposable databases in the existing private `procurement-approval-rehearsal-postgres` container. It never drops, resets, terminates connections to, or writes to the retained source database.

## Inputs and execution

Use the actual generated P26 ID and reviewed **idempotent zero-to-P26 Up** SQL plus **transactional P26-to-P25-stable-binding Down** SQL (including its first literal guard and exact overlay drops). The scripts must include EF migration-history changes. Explicitly inspect generated SQL before execution; argument validation is not a SQL security sandbox. The private container's existing `postgres` owner connection is used with no credentials in arguments or logs. This is migration-owner rehearsal, not runtime privilege evidence.

```sh
python3 docs/procurement/sql/p26-rehearsal/rehearse.py \
  --source-db proc_approval_http_receiving_review_20261020_152211 \
  --prefix proc_approval_http_p26_YYYYMMDD_HHMMSS \
  --migration-id ACTUAL_GENERATED_MIGRATION_ID \
  --up-sql /private/reviewed-p26-up-idempotent.sql \
  --down-sql /private/reviewed-p26-down.sql \
  --output /private/new-p26-evidence-directory
```

The prefix/output must be new; no database reuse or evidence overwrite. Source must contain used P25 acceptance effects and no P26 schema/history. If cloning fails because another session is using the template, stop the synthetic source server through the normal controlled path; do not force session termination. Helper-created databases are retained even on failure for diagnosis. Cleanup is a separate reviewed operator step.

## Exact gates

1. Fresh template0 database: Up twice; one exact P26 history row; full structural P25/P26 overlay manifests from their owning source constants.
2. Three clones of the actual passed P25 database. Before Up, only the populated clone additionally gets a synthetic **unverified** legacy invoice/line/result/exception through `legacy-original.sql`, using only P25 columns. Original quantity2/rate12.3456/net24.6912/tax1.2345/gross25.9257, dates, owner/reference/text, revisions and four-place signed variances must survive. Clone-specific complete baseline values/history are captured after this seed; the other clones and original source remain unchanged. Up twice; every preexisting Procurement row/column compared as PostgreSQL JSON with Python exact `Decimal` parsing (no float/scale-string false positive); old migration history retained. No new acceptance is inferred.
3. Populated clone with **used P25 receiving and unused P26**: guarded Down succeeds, complete old values/history restored; Up twice succeeds, same old values and both overlay manifests. This specifically catches an incorrectly broad P25-use veto in P26 Down.
4. Separate used clone: one owner-created Unknown/sequence0 exposure fixture (actual scoped issued parent) must block Down with `P26RollbackRequiresEvidencePreservingRecovery`.
5. Separate precision clone: unverified legacy0 result with tax variance0.00000001 and all P26 execution metadata absent must block Down with `P26RollbackWouldLoseTaxPrecision`. This fixture intentionally exercises storage protection, not original invoice or matching workflow validity.
6. Each denied Down preserves all current Procurement data, complete migration inventory and schema dump; exact overlays still valid. `pg_dump` client-only random restriction nonces are excluded from equality. Original retained P25 source values/history rechecked at the end.

Each subprocess stdout/stderr is retained privately, including the first failure. The helper intentionally leaves all clones intact. A result file is written only after every gate passes. It does not run SDK generation, HTTP/browser/probes or production operations.

## Remaining centralized evidence

This helper covers representative **used-table** and **precision** stop paths, not all independent guard branches. Root must separately rehearse invoice-pointer/result/exception metadata, generic matching audit, receipt and outbox stop fixtures; the unused-schema reverse includes the populated clone's synthetic legacy original/line/result/exception data; additional customer-specific legacy shapes remain a separate reviewed gate. Full actual HTTP/restricted runtime, cross-scope FK denials, owner completeness unavailable/no claims, manifest-tamper tests and combined-model receiving transitions remain separate gates. Review the generated Down's lock/guard-first ordering and Up's literal exact overlays after unique indexes before any execution. Applying DDL never establishes invoice validation, Finance posting or AP authority.
