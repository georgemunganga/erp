# P28 — governed sourcing drafts verification

Status: source compilation correction; migration and execution pending.21October2026.

## Candidate and boundary

Combined64db156 integrates independently reviewed guarded sourcing source, discovery/UI contract and owning-store schema reader onto verified P27 source. Scope is internal source-backed draft hold/create/read/cancel/receipt recovery; no publication, bids, evaluation, award or PO transfer is implied. Existing common approved-demand allocations, Core identity/approvals and Finance evidence are reused.

## First source gate — retained compile failure

Session92469 restored offline dependencies and compiled domain/application, then infrastructure compilation found a C# name collision: the singular DbSet property SourcingDraftEvidence shadowed its domain type in nameof/type-pattern expressions. No tests or migration generation ran. The property is renamed SourcingDraftEvidenceRecords with existing query/test callers updated; domain/table names, schema mappings, business guards and assertions are unchanged. Original artifacts/p28/prepare-schema.log is retained. The corrected gate must pass before migration generation/review.

## Required next gates

1. Full Procurement domain/model tests; generate parent-owned P28 migration.
2. Review additive schema/manual exact ownership overlays and locked evidence-preserving rollback.
3. Actual PostgreSQL17 fresh/used-P27 upgrade/repeat/preservation/rollback rehearsal; freeze literal manifest, no fake readiness.
4. Mapped API/build plus actual finite-role RFQ HTTP privileges, source/allocation/replay/recovery/cancellation/scope/concurrency checks.
5. Existing nine mocked sourcing browser results remain separately scoped; actual backend browser proof follows.

Production remains unchanged; default-off execution and full-host release hold remain.


## Second source gate — retained test compilation failure

Session57784 compiled the corrected infrastructure, then found two test-only assignments to init-only FinanceIdentitySchemaVersion. The negative restoration test now constructs a deliberately corrupted retained-history clone through the existing JSON serialization pattern; production immutability is unchanged and the same downgrade/unknown-schema rejection assertions remain. No tests or migration generation ran. Original prepare-schema-corrected.log remains retained.


## First completed domain run — retained700PASS/1FAIL

Session38463 completed701 tests:700passed,1failed,zero skipped (17seconds). The failure expected a presentation-specific plain decimal substring in retained JSON, but the existing canonical G29 encoder correctly stores exact product0.00000001 as string`1E-08`. The assertion now independently checks JSON string kind, exact G29 golden bytes and bounded38,8 parse/value; production encoding/digests remain unchanged. No migration ran. Original TRX/log remain.

This exposed a real separate UI gap: governed RFQ detail accepted only plain decimal strings. A new isolated frontend follow-up will normalize bounded exponent strings through the already shared exact decimal helper before presentation, with no financial Number conversion. Earlier mocked RFQ browser passes used plain-wire fixtures and do not certify that follow-up.


## Corrected domain/model gate and generated migration — PASS/source review pending

Session85098 on executableebca1f1 completed701/701 Procurement domain/model tests, zero failed/skipped (16seconds), and generated20261021065049_P28GovernedSourcingDrafts; session terminated0. Original700/1 failed TRX remains separate. No schema was applied.

Actual generated change: seven additive SourcingEvent fields, three immutable owner-evidence/binding/receipt tables and their keys/indexes/checks; no old financial precision rewrite/backfill. Parent seals four frozen exact deferred ownership overlays after their referenced unique indexes. Down locks original demand then owned sourcing/evidence/audit/outbox tables, rejects any used/partial metadata, evidence tables and actual audit/outbox producer evidence before dropping those overlays and generated additions. Distinct migration/Designer/Snapshot review, compiled SQL/drift, PostgreSQL17 freeze/rehearsal and API/runtime acceptance remain pending.
