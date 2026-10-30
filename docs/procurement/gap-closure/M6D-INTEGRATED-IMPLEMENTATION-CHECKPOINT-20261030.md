# M6D integrated implementation checkpoint — 30 October 2026

Status: development implementation; not a production acceptance or complete Procurement UAT claim.

Core owns authoritative currency calculation references and original-command recovery. Procurement consumes them in the invoice transaction, preserves exact source quantity/rate, and records immutable rounded calculation evidence and capture receipts. The original invoice form and detail layout remain the presentation baseline. Current Core user permissions and company/branch scope apply without requiring an employee link. New capabilities default off.

## Verification completed

- Procurement automated suite: 1,668 passed on the integrated implementation. Subsequent changes were limited to provider-specific model checks and test fixtures; the affected 81 Procurement tests passed afterward.
- Host targeted suite: 696 passed and three new tests initially failed because their actors lacked configured Core roles. After correcting only that fixture setup, those three cases plus the line-number wire regression passed (4/4). These are separate runs, not a claimed single green 699-test execution.
- Provider model regression passed for the SQLite test provider; the PostgreSQL model and generated migrations remain authoritative for deployment.
- Analyzer builds and both EF pending-model checks passed. Genuine EF migrations were applied to a disposable clone only.
- All 17,882 original rows and the original table digests/security were preserved, apart from the exact migration-history appends. Four new tables were initially empty. A genuine guarded empty Down/Up passed. A rollback-only retained-calculation probe caused the first Down guard to reject before any DDL.
- Core UI type checking and six mocked browser cases passed. Procurement UI type checking, contract/journal tests and a mocked lost-response browser case passed. These do not substitute for the real-cookie integration journey.

Private evidence roots: `procurement-m6d-final-sdk-20261030_run01` through `run04`, and `procurement-m6d-native-20261030_run01`, under the host's protected evidence directory. Earlier compiler, fixture, provider and publication-tool failures are retained with their corrected successors; no failed run is relabelled as a pass.

## Remaining acceptance

1. Verify the published runtime against the tested source/assembly manifests.
2. Run real-cookie Core configuration and Procurement capture/mapping/independent unit-review/matching through the supplied UI. Prove interrupted-save recovery, stale-input denial, actor isolation and exact native reconciliation.
3. Verify retained history with new work disabled and the populated rollback veto; restore temporary test grants and revoke synthetic sessions.
4. Review and merge the stacked changes, deploy the accepted artifacts, and smoke-test the production release.

Controlled correction/replacement of a previously mapped or processed invoice remains a separate explicitly open workflow. M6D creates new invoices and must not rewrite the old blocked invoice or relax its matching policy. Finance posting/payment execution are not inferred from invoice capture.

## Independent review correction

The retained Core response is now reconstructed against its typed receipt and immutable version before either POST replay or GET recovery returns the original bytes. Publication recovery retains its original current-at-publication response even after retirement. New invoice branch existence is checked by Core on the owning Procurement connection and Serializable transaction; legacy v1 retains its existing path. Twenty focused Host tests passed on the corrected source, including rehashed wrong-response denial, historical response recovery and tenant/company/branch transaction checks. The successor 150-file publication was independently hash verified. Earlier failed compile/provider attempts remain separate evidence.

This successor changes API/test source only; the verified Core and Procurement UI builds remain unchanged. The real-cookie browser and native reconciliation remain outstanding.
