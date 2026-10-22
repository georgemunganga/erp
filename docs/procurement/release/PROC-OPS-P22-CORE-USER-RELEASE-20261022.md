# P22 Core-user Procurement release — 22 October 2026

**Status:** Deployed; authenticated live CRUD acceptance pending. Full Procurement workflows remain incomplete.
**Deployment completed:** 11:15:15 UTC.
**Standard:** GW-ENG-STD-001, Tier 3; existing child specifications and ownership boundaries apply.

## Released identities and scope

- Backend application: `5f199c84c95dab582f273d914652aa7d6a42a42e`, included in ERP main merge `2491e104c0a87bb9fc2a409196172bde9093fa5d` (PR #21).
- UI application: `36127ad692c83035a0e8270306b00b1f1278455c`; browser-test source `225928208619c4d076bf233a596fceae2767374c`, included in UI main merge `63054fc2400d0d5d6cc14350470b6cf169b073e6` (PR #50).
- Both API and existing publisher use verified full native image `sha256:25c1bcd3865e210d6c430f8c9dbbf49d34696b4247d9c4231b0306bd1cbe78d6`.
- Procurement schema advanced from P19 through P20, P21 and genuine P22 `20261022085116_P22CorePrincipalOwnership`. Earlier applied migrations and history were not rewritten.
- Existing live Procurement static alias received the verified application assets. Restarting the separate frontend service was not used as a substitute for publishing this alias.

General Procurement access and CRUD use an active current Core account, current action grants and tenant/company/branch/record scope. An employee link is optional. Request ownership records the actual Core principal and optional actual worker; no employee, permission grant, principal-to-worker substitution or historical ownership backfill was fabricated. Known Core-linked-worker aliases remain denial-only maker/checker evidence even when HR status changes.

This release preserves bounded draft maintenance and the supported supplier lifecycle. Operational approvals, sourcing awards, order issue, receiving, Finance/AP, portal and advanced workflows are not enabled merely because their schema or UI exists. Existing configuration, feature flags and account/role assignments were preserved. The existing publisher remains HRM-only; Procurement outbox dispatch was not silently configured.

## Verification

- 509 current host/API tests passed in one run, no failures or skips. Earlier 993 domain passes were reused only after 455 dependency files were verified unchanged; they were not rerun.
- Actual private PostgreSQL/API: 563 assertions across 191 requests passed, including workerless Core users and retained supplier-alias denials.
- All 13 actual API/PostgreSQL browser scenarios passed in one uninterrupted 4.8-minute run, one worker and zero retries/skips/flaky cases. No Procurement business responses were mocked. Outer authentication/Core shell and supplier prerequisites were explicitly synthetic; the database connection was an owner credential.
- Actual private Program startup and the real HRM-only publisher claim query passed on isolated clones. The executed 479-file harness tree contains all 150 complete API publication files unchanged; these are distinct tree identities.
- A fresh backup taken with both writers stopped passed database restore, original data, database properties, ownership, table/column/default ACL and migration rehearsal checks. Document/key/static backup extraction preserved bytes, numeric ownership, permissions and xattrs.
- Exact owning Up SQL ran as `erp`. Live final history, nine ownership FK manifests, 40 validated CHECKs, original data at schema application, original privilege boundary and inherited new-table runtime grants passed.
- After deployment, public HTML, JavaScript and CSS matched the exact frozen candidate bytes. Public sign-in returned 200; unauthenticated Procurement context returned 401. API readiness passed and both new processes were running with zero restarts.

Private SET ROLE permission checks are not finite-role login/HTTP proof. Public asset/denial checks are not authenticated user CRUD acceptance. Hosted Actions did not execute because of the GitHub account billing lock; local results are not labelled passing hosted CI.

## Retained release and recovery history

1. The first maintenance attempt captured a consistent backup but its private restore client could not traverse the existing PostgreSQL-owned socket directory after dropping all capabilities. No restore or live schema application had begun. Exact original containers and P19 history were verified before restarting the baseline services. Original plan, state and failure evidence remain retained.
2. A separately reviewed retry client added only `DAC_OVERRIDE` to the private, network-isolated client; production transport and host permissions were unchanged. Actual private read-only connection, empty target and database-property checks passed before the next maintenance attempt. A new writer-stopped backup and full restore/rehearsal then passed.
3. Live migration verification completed at 11:10:12 UTC. The initial candidate startup passed API health and publisher stability, then an exact-string configuration check stopped both candidates: the publisher's prior default user string was empty while Docker inherited explicit `root` from the new image.
4. A separate forward recovery verified both immutable image defaults resolve to exactly UID 0, GID 0 and group 0. It allowed only this equivalent publisher user representation; all other configuration, environment, flags, mounts, network and process settings remained checked. Only the exact owned new candidates restarted. No old-image restart, Down migration, database restore or accepted-write deletion occurred after schema application.
5. Recovery checked current schema and privileges and recorded current business data without requiring equality against a pre-start backup. No original-column changes were observed at that checkpoint. This observation does not authorize discarding future operational writes. Assets were published first and the index replaced atomically last.

The retained original API and publisher containers remain stopped for evidence. Forward recovery is required once the new application may have accepted principal-only records; the old P19 application must not be restarted as a casual rollback.

## Evidence and remaining acceptance

Restricted evidence is retained outside Git under `/home/amizo/erp-backups/`, including current SDK `20261022_103506`, `crud-core-alias-01`, `program-alias-103506`, `publisher-alias-103506`, the initial and retry cutover directories, and `procurement-p22-forward-resume-20261022`. Do not commit credentials, records, traces, keys or dumps.

The user has been asked to refresh the live workspace and confirm Vendors and Purchase Requests open under their existing sign-in. Actual authenticated live create/reload/edit/withdrawal, current scoped permissions and personal acceptance remain open. No employee link is required or requested. P23 invoice-review and policy work continues separately with genuine migration, authorization, recovery and browser gates before its own release.
