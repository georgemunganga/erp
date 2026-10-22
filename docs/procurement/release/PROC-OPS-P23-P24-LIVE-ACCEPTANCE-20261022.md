# P23/P24 live release acceptance

22 October 2026. **Deployed and verified for ordinary vendor CRUD and recurring-purchase planning. The entire Procurement module is not complete.**

## Deployed scope

- Tested backend source: `be5d87e3bfdc2b5d6d6be576ce321173e8c6b9f5`, included in published ERP main `b6b62cd551ad134879cc47c7ceb32a0570201aed`.
- Tested UI: `b9f9ff15b97d89db1f89f6234929ed89feb0b0b4`, included in published UI main `6fca23d4687668e85445c1c56ca92eefac1ab27c`.
- Both API and existing HRM-only publisher run image `sha256:f768eea40d381429c4546218916142da34d2bdcf29db6b6b27810fd3c54a067d`. Complete frozen SDK publication; existing appsettings, process configuration, keys, documents and runtime roles preserved.
- Genuine migrations applied separately as the existing owner: P23 `20261022112145_P23CorePrincipalInvoiceUnitReview`, then P24 `20261022144102_P24CorePrincipalRecurringPurchasePlanning`.
- UI published at the actual static alias `/home/amizo/procurement-ui/reference-ui/dist`; public JS `index-CQY6kKFO.js`, CSS `index-B_T57PCm.css`. All five public artifact bodies matched accepted hashes.

Finance/AP and payments belong to the future ERP Finance module, which is not built. Recurring purchases are planning records: no automatic PO, bill, reservation, ledger, scheduler or payment. P25 principal approval source remains on draft PR #33 and is excluded from this release.

## Release controls actually completed

The [release plan](PROC-OPS-P23-P24-RELEASE-PLAN-20261022.md) was executed through the separately reviewed retry pack. Both live writers were stopped; no unknown database sessions remained. Fresh database, documents, keys and static backups were retained. File extraction reproduced bytes, ownership, permissions and xattrs. The fresh database dump was restored into the preverified isolated empty target with full owners/ACLs, then the exact two Up files passed privately before live DDL.

After each live Up, original values and privilege boundaries passed reconciliation. Final history, exact live constraints/indexes and five new table ACLs passed. The isolated restore verifier used only the explicitly proven private CHECK decompilation variants; live verification retained original raw definitions. Both candidate processes started, native live/ready returned 200 and neither process restarted. Assets were published before the atomic index replacement. Old containers/assets and all backup/recovery evidence remain retained; no Down or automatic old-image rollback occurred.

## Actual public Playwright journeys

A normal sign-in used the actual existing Core account and current company/branch scope. Its employee link was absent. No account, role, permission, employee, supplier activation or spending-authority changes were made.

1. **Vendor:** real UI create with organisation-default currency, reload/fresh read, edit, reload and list search. Exactly one POST and one PUT; retained non-orderable Draft at version 2; no contact, bank, portal or approval actions.
2. **Recurring purchase:** real UI create using that owned Draft vendor, default currency, exact decimal/date fields, reload, edit, unchanged save, close and closed/read-only reload/list. Exactly four normal UI mutations, versions 1–4; one retained Closed planning record.

Both journeys passed without page errors, mocks, forced clicks or business API writes outside the UI. The existing session survived the deployment. The temporary test session was then revoked through normal logout, and reuse was unauthenticated; other user sessions and credentials were not changed. Synthetic records remain identifiable and retained for audit.

## Independent owner and denial checks

Separate read-only owner SQL confirmed vendor version 2, recurring version 4/Closed, actual principal/company/branch, null worker identity, exact amount and four ordered planning audit versions. Ten Procurement audit rows were added; no outbox event for either test record was created.

All 186 original table projections were compared with the writer-stopped baseline. Outside the expected supplier/audit additions, only the same Core session's `last_seen_at` and existing HRM publisher retry bookkeeping changed. Original Core audits were retained; additional Core audits belonged only to that session or the existing publisher. All old HRM outbox identities/payloads remained exact; only `available_at`, `publish_attempts` and `updated_at` advanced. The other 181 original table projections were exact, including procurement financial/operational records.

Unauthenticated Procurement returned 401; an unavailable branch returned 403. Actual metadata kept approvals, order execution, governed sourcing conversion, receiving execution, invoice comparison/documents/original-unit review/validation, Finance handoff and supplier portal unavailable. Live finite Procurement runtime sessions were observed; the owner query is not a finite-role login probe. Native HRM/API health and shared public auth/shell responsiveness passed separately.

## Restricted receipts

Private data, sessions, dumps and operational scripts remain outside Git under `/home/amizo/erp-backups/`.

| Receipt | SHA256 |
| --- | --- |
| `procurement-p23-p24-cutover-retry-prep-20261022/release-acceptance.json` | `c62a3f7d75b7bea5bbb00e8b1b6a4576e970088afdf217f3fa812ba849efdf25` |
| `procurement-p23-p24-public-proof-independent-review-20261022/review.json` | `277f90c8b2541dfbbc69ef8886cd9fd8cd2658a929f67fd05ec042ae83f84e98` |
| `procurement-p23-p24-cutover-retry-prep-20261022/state.private.json` | `5991abfbef777700f6b3a2e7e9f8b640a87253d858e5a4da5100b4a483230f28` |
| `procurement-p23-p24-cutover-retry-prep-20261022/files-backup-acceptance.private.json` | `7659cba9f25465bfa19821d6a5989a303b74e33d9993ad53f5b99749e983b371` |
| `procurement-public-vendor-uat-20261022-run01/result.private.json` | `defd7b6e2e62a31c143032198cb75b6f8d41b48bc998e4db165c5d438404cc7b` |
| `procurement-public-p24-uat-20261022-run01/result.private.json` | `aa1cb1a680ad1efeaac3ab7ba19a5da7c5ddb19294dace7953c538519ffbfb0e` |
| `procurement-public-p24-session-20261022/owner-acceptance-v3/result.json` | `42273d73c96c64026decda89710dc8328fd070c4a68ef908b3528c59c3221e1c` |
| `procurement-public-p24-session-20261022/post-deploy-checks.json` | `fb91d8db74ce226ef5bb7a73684871ed2305f5ad4668e02e14b36a367d656a58` |
| `procurement-public-p24-session-20261022/temporary-session-cleanup.json` | `d4f92bb1065da770acc6a62a50b9294e6552520b48cf4e70bea4b3adeb96881d` |

The coordinator deliberately ends at `candidate-healthy-await-authenticated-public-smoke`; the later public browser/owner/health receipts complete that remaining gate without rewriting its frozen phase receipt.

## Retained failures and limitations

- First image preparation stopped before build/live mutation because the client service file used spaced delimiters. The retry used canonical `key=value` bytes with independently verified identical connection values and unchanged scripts/gates.
- The first vendor test plan failed its output-path guard before browser startup or any write; a new corrected plan completed the journey.
- Initial owner comparisons stopped on expected Core heartbeat audit and existing publisher retries. The successful comparison strictly validates their named fields and immutable payloads against the retained actual SQL capture; it did not rerun business mutations or broadly ignore tables.
- Inherited broad audit-table privileges are not database-enforced append-only security. Existing HRM publisher retry activity is not evidence of external notification delivery. No Procurement dispatcher or Finance integration is activated.
- GitHub-hosted CI remains blocked by the account billing lock; local/private/public evidence is not hosted CI success.
- P25 source passed 1,253 domain and 586 host tests plus the EF model check and SQL emission. Actual P25 PostgreSQL, finite-role workflow and end-to-end approval acceptance remain outstanding; readiness stays unavailable.
