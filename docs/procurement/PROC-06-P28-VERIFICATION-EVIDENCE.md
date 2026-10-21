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
