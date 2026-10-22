# P23/P24 production release plan

22 October 2026. **Preparation only: production is still on P22.**

## Scope

Publish the accepted P23/P24 backend and existing Procurement UI, enabling ordinary recurring-purchase planning CRUD. Invoice original-unit review, document scanning, operational approvals, sourcing, order issue, receiving execution and Finance remain gated. A recurring plan does not create an order, invoice, reservation, payment or scheduled job.

Finance/AP and payments belong to the future ERP Finance module. No replacement ledger, synthetic funding confirmation or payment status is introduced. The selected Core organisation is the company; new plans default to its configured currency. Current Core permissions and company/branch/record scope govern access; an employee link is optional.

## Frozen source and artifacts

- ERP published baseline: `b6b62cd551ad134879cc47c7ceb32a0570201aed`.
- UI published baseline: `6fca23d4687668e85445c1c56ca92eefac1ab27c`.
- Tested backend/tool source: `be5d87e3bfdc2b5d6d6be576ce321173e8c6b9f5`.
- Tested UI source: `b9f9ff15b97d89db1f89f6234929ed89feb0b0b4`.
- All 150 API publication files are retained unchanged in the prepared 479-file native startup harness tree. API tree: `67eca5087ef711666c99cb903d3689b119ed177f59b417cf5f3473a507fa1f2e`; combined tree: `486aa27b49c53e55d5183a5dd332746c779b5996e30aa0a2379604b6bd06e408`. These are distinct artifacts; preparation does not prove startup.

See [published P24 acceptance](PROC-07A-P24-PUBLISHED-ACCEPTANCE-20261022.md) for actual domain, API, PostgreSQL and Playwright results and their limits. P25 approval changes are on a separate branch and are excluded from this release.

## Actual live baseline

The read-only audit found both existing writers healthy on image `sha256:25c1bcd3865e210d6c430f8c9dbbf49d34696b4247d9c4231b0306bd1cbe78d6`, with Procurement history through `20261022085116_P22CorePrincipalOwnership`. Preserve their configurations, mounts, keys, documents, identity assignments and runtime roles.

The current database contains 186 application tables and 3,364 original columns. Old release providers covering 159 tables are insufficient. The successor rehearsal explicitly preserves the current inventory, owners, database properties, table/column ACLs, memberships and default privileges.

Apply the original genuine SQL in order, as the existing owner `erp`:

1. P23 `20261022112145_P23CorePrincipalInvoiceUnitReview`; Up SHA `b75973c75ad791590ee6a6e1d9d4c38f44c83ac4ed6ddb24192fb7ce0500b3f7`.
2. P24 `20261022144102_P24CorePrincipalRecurringPurchasePlanning`; Up SHA `f3138f0741033b2b0b5484f4c526695ad265468ac384c9f4cc91e761d80264c8`.

Expected final Procurement inventory: 88 tables and 45 validated CHECKs, verified by exact structure/constraint/index manifests rather than counts alone. The four P23 tables include `invoice_unit_review_receipts`; `invoice_unit_review_command_receipts` was an incorrect name in an early planning note, corrected by a retained erratum. The fifth new table is `recurring_purchase_commitments`.

No additive database grants are expected: the observed owner default ACL already supplies ordinary Procurement CRUD. Verify the actual five new table ACLs after restore and migration. Do not label the inherited broader audit-table privileges as database-enforced append-only security, or private `SET ROLE` checks as actual finite-role login proof.

## Required gates

1. Refresh live source/configuration/static/store/session inventory and reject unexplained drift. Recheck disk headroom before every heavy phase; serialize SDK, restore and runtime work on the single-CPU host.
2. Take a protected, consistent read-only production dump and restore it to a new isolated private database. Preserve owners and ACLs. Verify current P22 structure, database properties and the full original privilege boundary before applying either Up.
3. Apply the two exact Up files privately, reconcile history and complete final schema manifests, and prove all original values and privilege boundaries preserved. Exercise the inherited finite role with rollback-only SQL, retaining its narrower evidence label.
4. Run the actual compiled API Program and existing HRM-only publisher on separate new private clones, using the complete frozen publication and current native base image. Require no pending HRM/Procurement migrations or model drift. Observe the actual successful publisher claim query; do not activate Procurement dispatch or external delivery.
5. Build and independently inspect the complete immutable native candidate, retaining the original appsettings bytes and process settings. Stage the exact tested UI files.
6. For cutover, stop the publisher and API, verify no unknown writers, take fresh database/document/key/static backups and verify their restore before live DDL. Preserve the original containers and all evidence.
7. Apply the same two Up files live, reconcile history, original data/privileges and new table ACLs, then start both verified candidate processes with their preserved configurations. Keep operational, scanner and Finance flags disabled.
8. Publish assets first and atomically replace the index last at the actual static alias `/home/amizo/procurement-ui/reference-ui/dist`. Verify served HTML, JS and CSS bytes; restarting the separate frontend service is not static deployment.
9. Verify actual signed-in recurring create/default currency/reload/edit/no-op/close and current permission/scope denials, with independent persisted-record/audit checks. Recheck HRM health, publisher stability and unavailable Finance/document capabilities.

## Recovery

Before schema application, separately verified restart of the exact baseline may be possible. After schema application or candidate writes, use forward recovery. Do not restart the P22 image casually, apply destructive Down, restore over accepted writes, fabricate employees or rewrite history. Retain old UI assets; reverting an index requires explicit old-UI/new-API compatibility review and never implies a database rollback.

## Restricted preparation evidence

Operational scripts, dumps, configuration and records stay outside Git under `/home/amizo/erp-backups/`.

- Read-only audit/plan: `procurement-p23-p24-release-audit-20261022T153955Z`.
- Initial full-ACL successor pack: `procurement-p23-p24-full-acl-prep-20261022`. Executed successor: `procurement-p23-p24-full-acl-restore-variants-prep-20261022/plan.v3.REVIEW.json`, SHA `361bb66db3630b3b722e1e2c396dc55ed48afff3106528f102f2f1b144e56783`.
- Prepared full native harness: `procurement-p23-p24-native-prep-20261022`.
- Reviewed successor runtime adapters: `procurement-p23-p24-native-runner-prep-20261022-v3`.

## Completed private gates

- Fresh production-backup restore, exact genuine P23/P24 Up, original data/owner/ACL/default-ACL preservation and rollback-only finite `SET ROLE` checks passed. Final production schema/privilege preservation passed. Receipt: `procurement-p23-p24-full-acl-actual-20261022_review02/result.json`, SHA `8584c850f5a4d269e65cae102245e1b9791b391900bd5a971d16b5482e6ed50a`.
- Actual API Program started on a separate private clone using the complete frozen native harness. HRM/Procurement pending migrations and model changes were zero, and table values/schema were unchanged. Final acceptance: `procurement-p23-p24-native-program-20261022_run01/execution-acceptance.json`, SHA `41bc947112c3894d8c749be2d3b0520f74ee5db3fb071a2416548fc80e6d6d93`.
- The actual HRM-only publisher entrypoint executed a successful EF claim query on another private clone; immutable payloads and source preservation passed. Receipt: `procurement-p23-p24-native-publisher-20261022_run01/result.json`, SHA `813a8cb6d380c338cb026bae2abd95e3c1c22be18c8ede5d5661ac2bae20fa4f`. No Procurement dispatcher or external delivery success is claimed.

Retained failures: the first rehearsal stopped before database operations because generated SQL permissions were insufficiently restricted; new protected byte-identical copies fixed its input contract. The next restore stopped before Up because PostgreSQL reconstructed 11 literal-array CHECK casts differently. Narrow comparison and actual read-only array evaluation proved identical types, values, order and all text outside the single cast fragment. The successful successor accepts only these exact private restore forms (nine survive P23/P24); production raw checks and every other constraint/index remain exact. No database repair or general normalization was used.

These are private owner/SQL/startup proofs, not production deployment or authenticated public acceptance. **Immutable candidate inspection, a fresh writer-stopped backup and restore, live cutover and signed-in CRUD acceptance remain pending.** Hosted Actions remain blocked by the GitHub account billing lock; local evidence is not hosted CI success.

## Reviewed cutover preparation

The source-only coordinator and plan have passed separate root and independent review. All 12 raw acceptance gates and the actual browser-observed five-file UI artifact matched. The coordinator preserves both writers, verifies the separate empty restore target before building and again before stopping either writer, requires fresh database/filesystem restore proof, and marks forward recovery before the first live Up attempt.

- Coordinator: `procurement-p23-p24-cutover-prep-20261022/cutover.py`, SHA `69931b0c3261b0d1cf6d2baafb69a69f3cf72ef64a242e6c40d6691a37f3245a`.
- Reviewed plan: `plan.REVIEW.json`, SHA `cf600f2639a04b792a8ab8e1f606c257f5cc4a5c5cc88b15f846cfb50f7212a2`.
- Independent source review: `procurement-p23-p24-cutover-independent-review-20261022/source-review.json`, SHA `2c03576763b614eb57c60a0543d3b6227ff2a3465b8d3af5b9f4b2b647c2e9fd`.
- Separate new empty private target creation is prepared under `procurement-p23-p24-empty-target-prep-20261022`; it must preserve the retained private source and cannot create or repair roles.

No coordinator mode, candidate image build, writer stop, production migration or static cutover has executed at this checkpoint. Disk headroom and creation of the new isolated empty restore target remain prerequisites. Four verified ignored inactive build caches were removed (64,927,957 logical bytes); tracked files, active build directories, published artifacts, backup/test evidence and databases were preserved.

P25 remains excluded from this release. Its current source has passed 1,253 domain tests; the full API cohort and actual migration SQL emission are still running and are not recorded as passed.
