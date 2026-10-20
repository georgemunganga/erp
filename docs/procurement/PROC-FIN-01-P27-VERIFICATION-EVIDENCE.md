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
