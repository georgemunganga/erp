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
- Reviewed full-ACL successor pack: `procurement-p23-p24-full-acl-prep-20261022`; plan SHA `96452b9961974b5b2bb37fd176079fb604a09eb13fe3ec7af969e4fca3a4d818`.
- Prepared full native harness: `procurement-p23-p24-native-prep-20261022`.
- Reviewed successor runtime adapters: `procurement-p23-p24-native-runner-prep-20261022-v3`.

The retained SDK input validator has passed against actual P24 evidence. **Fresh restore, native startup, publisher execution, cutover and authenticated live acceptance are still pending.** Hosted Actions remain blocked by the GitHub account billing lock; local evidence is not hosted CI success.
