# P27 — retained Finance checker identity verification

Status: source integrated; verification pending. Recorded 20 October 2026.

## Candidate and boundary

`cc7ac5c` on `codex/procurement-finance-identity-integration` consolidates reviewed Finance checker binding, persistence, consumer guards and owning-context readiness onto the P26 application baseline. It preserves historical unbound evidence; it does not infer a worker for old rows. New positive funding use requires the retained independent checker and current authority. Core remains owner of identities and grants; this change adds no employee master, budget ledger or payment executor.

This record distinguishes source review from execution. No P27 migration, test, PostgreSQL rehearsal, browser run or production rollout has passed at this checkpoint. The prior P26 passes do not certify this new source.

## Required verification sequence

1. Restore from the protected local package cache and compile the new domain/persistence tests. Retain actual runner counts and any failure before correction.
2. Generate one parent-owned `P27FinanceCheckerIdentity` migration. Expected change: four columns and two CHECKs, with no new principal key, cross-module FK, table or financial precision change. Inspect actual generated operations rather than assuming this expected shape.
3. Seal Down with deterministic locks and a used/partial identity, approval pin, audit and outbox evidence veto before dropping any field. Never remove business history to permit rollback.
4. Capture the actual PostgreSQL17 CHECK expressions and freeze the readiness manifest. Missing, lookalike, unvalidated or inaccessible schema must fail closed under the actual finite runtime role.
5. Rehearse fresh and populated P26 upgrades, repeated upgrade, unchanged legacy bytes, unused rollback/re-upgrade and loss-denied rollback with unchanged schema/history/data.
6. Run mapped business API and genuine restricted PostgreSQL cases for capture/reload, exact reference replay, alias/rebinding, grant revocation, legacy read/review-required behavior, source approval/order use and retained cancellation/recovery. Retain separate source scopes/results.
7. Review RFQ's dependent version-dispatch restoration and owning-context schema gate before its separate migration. Do not import RFQ schema into P27.

## Release

Production remains unchanged. Existing full-host release, recovery and independent review gates remain. An authentic retained checker is not proof of complete external Finance commitment history or payment authority.


## First focused compile/test gate — retained failure

Session71911 restored from the read-only local cache and compiled the new domain/application/infrastructure/test source. Actual runner result: **43 passed,1 failed,0 skipped,44 total**. The exact legacy tuple assertion failed because JsonElement reserialization in the new v1 wrapper escaped the timestamp plus sign as a Unicode escape. The legacy0 encoder/golden digest passed; the script stopped before migration generation. No P27 migration or runtime pass is inferred.

The source correction embeds only the owned legacy encoder's validated JSON using Utf8JsonWriter.WriteRawValue, retaining its exact bytes without changing global JSON escaping or accepting caller raw JSON. The existing failing exact-byte assertion is retained. Distinct review and a fresh focused rerun are required; first-run TRX/log remain in artifacts/p27/checker-domain and prepare-schema.log.


## Corrected focused gate — PASS

Session6810 rebuilt source213a6af (later commits change documentation only) and passed **44/44**,zero failed/skipped,27seconds. The exact nested legacy golden-byte assertion now passes without relaxing it. Evidence: artifacts/p27/prepare-schema-corrected.log and checker-domain-corrected TRX. Distinct source review accepted the owned raw-JSON boundary. The separate migration-generation command follows this pass; its output/review/rehearsal remain pending at this checkpoint.


## Generated migration — source reviewed, runtime pending

Corrected preparation session6810 terminated0 after44/44 tests and generated `20261020190554_P27FinanceCheckerIdentity`. Actual Up is exactly four additive fields/two CHECKs; no key/FK/index/table/precision rewrite or backfill. Root sealed Down with the first four-table lock/used-or-partial metadata/pin/audit/outbox field-token veto; exact actual BudgetChecked audit is covered without inventing an event. Distinct source review accepted migration, Designer and Snapshot consistency. SQL generation, actual PostgreSQL17 literal freeze/rehearsal, full regression and actual restricted HTTP remain pending. Generation is not database application or migration acceptance.


## Original Finance tuple read privilege correction — source only

Independent review confirmed that SELECT on only the new checker columns could satisfy the new-field manifest while denying the original Finance tuple used by queue/materialization/reference-replay queries. Read and capture readiness now additionally require finite table SELECT on the single owned BudgetCheckSnapshot table, matching the existing finite table INSERT guard. No Core or schema-wide grant is added. The rollback-only actual restricted-role probe now revokes table SELECT, grants only the three new checker columns and requires both read/capture readiness false, then restores privileges by rollback. This API/tool correction does not change the accepted-source model/migration or the44 focused domain test inputs; its actual SQL/runtime outcome remains pending.


## Full domain/model regression — PASS; API gate active

Session62163 passed **601/601 Procurement domain/model tests**,zero failed/skipped,55seconds, using the compiled source213a6af domain/application/model code. Migration-source1d0498a and API/tool-only f08ae1f do not change those test inputs; their compilation, drift/SQL and runtime checks remain separate. Domain TRX: artifacts/p27/domain-all; shared job log: artifacts/p27/source-verification.log. The offline shared-test restore also passed. The API/test/tool compilation and mapped Procurement/Finance readiness cases are still active at this checkpoint; no API, generated-SQL or private PostgreSQL pass is inferred.


## Full API, tool and schema generation gate — PASS

Session62163 completed all scripted stages on executable source f08ae1f: **197/197 mapped Procurement/Finance-readiness API tests**, zero failed/skipped (4m35s), probe build successful, and no pending EF model changes. The earlier601 domain/model pass remains separately scoped. Actual idempotent zero-to-P27 and P27-to-P26 SQL were generated. Independent review accepted the additive Up and locked evidence-preserving Down for private rehearsal. Up SHA256 `8a69372315278c5e8cf5880ee8c6ffd2720ebd58d2526231c191d38f23a35e3f`; Down `6f8091cd0339db7d19389efd30698e93d9aa2c11189fc8b0bae77a643aa72d35`.

Evidence: artifacts/p27/source-verification.log, procurement-api TRX and p27-up.sql/p27-down.sql. This is source/mapped verification, not an actual restricted PostgreSQL/HTTP or production pass. The later additive Budget businessVersion follow-up is outside this run and requires its own gate.


## Actual PostgreSQL17 migration rehearsal and literal freeze — PASS

On21October, reviewed migration20261020190554 was applied with the generated scripts to a fresh private database and seven clones of retained actual usedP26 database `proc_approval_http_p26_matching_20261020_184252`. Repeated Up, original column values/sealed JSON/hash bytes, full history, unused Down/Up and six isolated evidence-loss Down denials passed. Source database remained unchanged; all fixtures and failures are retained, with no database deletion.

Protected evidence `/home/amizo/erp-backups/procurement-p27-postgres-20261021_062926/` contains197 operation logs and PASS result.json, exact SQL hashes and database inventory. Owner-only rollback-negative fixtures are explicitly synthetic and do not establish valid Finance identity. The actual PostgreSQL17 pg_get_expr output for both CHECKs matched readiness constants by exact string equality (no whitespace/SQL rewriting); actual-check-expressions.tsv and literal-manifest-result.json retain that proof.

Runtime-role readiness/privilege negatives and genuine capture/replay/rebinding/revocation/legacy denials remain the next independent HTTP gate. This migration proof does not enable production workflows.


## Additive Budget business-version gate — retained compile failure

First follow-up session19058 compiled the application API but stopped before test execution: new test Send() supplied the Finance role positionally where that helper expects optional company Guid. Corrected only the call to named `role:`; production query/assertions/authorization remain unchanged. Original business-version-verification.log is retained. This follow-up has no test pass yet and does not invalidate or replace the earlier197-case source scope.
