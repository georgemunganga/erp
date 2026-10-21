# P29 host readiness closure — source candidate

This bounded follow-up uses backend base `5387d1b`. Parent reported actual private P29 runtime PASS at `/home/amizo/erp-backups/procurement-p29-http-policies-20261021_090640/result.json`, including the real finite-role positive and 14 readiness negatives. That external checkpoint does not certify this new host/lookup source, deploy it, enable operational RFQ publication or complete the used-evidence rollback gate.

The opt-in private probe no longer replaces this registration; its next run will use the normal host composition. The normal host now registers the stateless `ProcurementSourcingPolicyIdentitySchemaReadiness`. Its own actual owning-store checks remain authoritative; registration is not positive schema readiness. Existing sourcing mutation/effective-configuration guards continue to require it. Other policy keys and operational RFQ execution options/grants are unchanged.

The existing scoped reviewer lookup injects its actual owning `ProcurementDbContext` and returns `configurationVersion: 1` only after the registered provider succeeds on that context. Missing, false, unsupported or failed discovery produces 0. Discovery failure does not block otherwise authorized, Core-scoped names. Current actor checks, exact selected-ID validation, count-before-pagination scope filtering, private-field exclusion, no-store and `isAuthority: false` remain intact. Marker1 is supported configuration persistence capability, never reviewer/action/financial approval or publication authority. Mutation commands repeat the real guard rather than trusting a client marker.

Authored mapped tests use explicitly labelled synthetic readiness to cover ready1, false0 with blocked sourcing mutation/no effects, missing0 and failed0 with names preserved. Existing identity/branch lookup tests and non-SOURCING policy behavior remain relevant. The host registration test expects the actual provider type. No tests/builds/SQL/browser or production configuration changes were run in this worktree; parent must run the relevant gates and actual CLI through normal host composition before accepting this candidate.

## Executed normal-host regression checkpoint

Frozen integration `445a5cb` passed **306/306** combined mapped API tests, zero
failed/skipped (2m35s), including Procurement, Finance checker schema, Core
organisation/configuration and entity-tree coverage. The actual PostgreSQL probe
then built successfully in the same serialized pipeline. Existing compiler/analyzer
warnings remain retained; two existing BudgetBrowserFixture nullable warnings were
reported by that tool build. Evidence is retained in
`artifacts/p29/verify-normal-host-445a5cb.log` and `artifacts/p29/api/`.

This supersedes the preceding source-only status for those bounded checks. The
complete genuine P29 retained-Down rehearsal also passed separately under
`/home/amizo/erp-backups/procurement-p29-postgres-20261021_0911/`; the original
private-host workflow/14 readiness negatives passed on executable `5387d1b`.
The next actual browser gate will exercise computed availability and policy writes
through normal host composition, with separate owner reconciliation. This document
does not claim those later browser results, production migration/deployment or
operational RFQ publication.
