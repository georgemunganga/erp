# P23 original invoice-unit review — private acceptance

Date: 22 October 2026 (UTC). Status: **accepted for default-off source integration**, not production activation or completion of Procurement.

## Published sources

| Source | Reviewed feature | Published main |
| --- | --- | --- |
| ERP backend | `87c2d351e2c5e0a5460044cb5ac58899e5d33f5e` | PR #24, `d2d45117d050357d84a63d90313782955bc80700` |
| Existing Procurement UI | `cec39de0d9ba55c36cabc1c46b3decf6a1520f4a` | UI PR #53, `0adb132fe721de5e74551f1fa5758c30b11fb013` |

The merged ERP backend is byte-identical in source to the reviewed feature. Main changes since its base were documentation only. Existing branch recovery, branding, navigation and shared controls are preserved; no second frontend is introduced.

## Ownership and behaviour

Procurement owns original-invoice unit review records and rules. Core remains owner of accounts, current permissions/scopes, organisation, Documents and the shared Approval engine. Named active Core users can participate without employee records. Canonical principal ownership and genuine optional worker aliases enforce maker/reviewer separation. Historical identity-1 evidence is not rewritten.

The workflow captures the exact original document, opens a named independent review, restricts document access to that purpose, records the decision and supports source-bound replay/recovery. It preserves upstream purchasing records. Reviewing document units does not approve an invoice or grant accounting/payment authority.

## Executed gates

| Gate | Result and limits |
| --- | --- |
| Application SDK | Fresh source `20430afe3cdb68532ca98782b49e7e7b783170cc`: **44 focused and 543 selected host tests passed**, zero failures/skips; four complete publications and three actual EF no-pending-model checks. |
| Unchanged dependencies | Domain **1194**, Core Documents **112**, Core Approvals **20** results explicitly reused through unchanged dependency-source proofs; not described as freshly rerun. |
| Genuine schema | `20261022112145_P23CorePrincipalInvoiceUnitReview`; actual private PostgreSQL Up, unused Down/re-Up, original data/catalog preservation and retained audit/outbox/genuine Core-case downgrade vetoes. All **139 tracked migration artifacts** pinned. |
| Restricted API/PostgreSQL | Source `0d454ff`: actual upload, mapping, review opening, narrow reads/download, independent decision, self/alias denials, replay/recovery, blocked Finance comparison, atomic rollback and retirement-fence checks passed. Nine named CHECK/FK negatives and twelve append-table mutation denials passed. |
| Execution boundary | Actual metadata accepts the restricted execution role and rejects the database owner. Three downstream Procurement reference tables are readable in the fixture; their insert/update/delete/truncate attempts remain denied. No Finance write authority is added. |
| UI units/build | **42 focused units**, supported Node22 TypeScript check and staged production build passed. |
| Controlled browser | **15 Playwright cases passed**, one worker, zero retries, covering policy settings, permission/denied paths, incomplete drafts, uncertain outcomes and recovery. These use controlled API fixtures. |
| Actual browser | **1 Playwright case passed**, one worker, zero retries, against the real scoped Procurement/Core API and private PostgreSQL, using workerless Core maker/reviewer accounts. |
| Independent saved-state check | After graceful fixture shutdown, fresh owning contexts verified source3/outcome3/wire1, one Core decision/two receipts, exact document pins/download audit, unchanged upstream data and no AP authority. |

The final complete 154-file runtime and tracked source remained unchanged. Its 152 application/dependency files match the tested application publication; later commits change the verification tool only. Whole publications were used throughout, without individual DLL replacement.

## Evidence references

Private evidence is retained under `/home/amizo/erp-backups/`; restricted traffic, runtime connections and credentials are not uploaded.

| Record | Identity |
| --- | --- |
| Fresh SDK | `procurement-p23-core-principal-20261022/20261022_132128` |
| Accepted standalone | `procurement-p23-unit-runtime-20261022_1347` |
| Actual browser and owning completion | `procurement-p23-unit-browser-20261022_1408/acceptance.json`, SHA-256 `6ec275cb2db6a2d5b2288105c197ca20b25fbd0775a4316f43a917ac67440cde` |
| Root UI acceptance | `procurement-unit-review-browser-acceptance-20261022_1410/acceptance.json`; links the actual browser and the separately retained fifteen controlled cases |
| Complete final tool publication | SHA-256 manifest `f5f50791b98b583da8dd3c86e5b6134bf2376cefd547d7ce521d35b41ddd01ca` |
| Genuine forward SQL | SHA-256 `b75973c75ad791590ee6a6e1d9d4c38f44c83ac4ed6ddb24192fb7ce0500b3f7` |
| Actual CHECK capture | SHA-256 `852061b7fbc71a789ed74b7700e7982378dca7f23865ae7951c969f7f123f423` |

The final fixture has a retained final hash and timestamps preceding Playwright. No pre-Playwright fixture hash was captured, so no before/after fixture-hash proof is claimed. Source/runtime before/after hashes and fresh owning data checks are recorded.

## Findings corrected and preserved

- Two readiness predicates were incorrectly executed without `SELECT`. Actual PostgreSQL SQLSTATE 42601 established the defect; the fix preserves every permission/ownership predicate. Actual restricted-role positive and owner-role negative checks now pass.
- The test harness had missing explicit Core buyer grants, an incomplete standalone route graph, owner/runtime connection confusion and missing read grants for downstream-reference absence checks. Corrections exercise real stored grants/routes and preserve write denials.
- The first actual browser test passed but owning completion failed because its assertion used canonical policy JSON hashing instead of the terminal contract's raw UTF-8 digest. Stored evidence matched the application contract. Only the verifier changed; a fresh browser plus owning completion then passed.

All failed/interrupted runs remain retained with their actual outcomes. No production guard was weakened and no earlier failure was relabelled as a pass.

Hosted ERP backend CI did not start: GitHub run `39793372639`, job `119874653373`, reports an account billing lock and no job steps. The manual automated gates above are recorded; no hosted-CI pass is claimed.

## Activation holds and next milestone

Actual API/browser tests use synthetic outer shell authentication and a known-byte scanner. They are not production SSO or real ClamD acceptance. Stored permission revocation was tested before the browser started; no mid-browser revocation claim is made.

The product owner confirmed Finance/AP and payments belong to a future, unbuilt ERP Finance module. Complete Finance exposure, qualified invoice approval, AP posting and payments remain unavailable. No substitute ledger, fake balance/reservation/history or fake Ready provider is introduced. Procurement outbox persistence is not delivery acceptance for the HRM publisher.

Production remains on the separate P22 backend and released branch-recovery UI. No production schema, accounts, employees, roles, settings, flags or publisher activation changed during this milestone. Real document scanner/storage/finite-role and owning-module integrations remain production activation gates.

**Next: P24 recurring-purchase planning CRUD.** Reuse the prepared backend/UI, Core company currency and current principal/scope contracts. Generate the genuine successor migration only after this published P23 prerequisite, preserve all existing migration artifacts, and verify actual CRUD/reload/concurrency plus the existing UI. Scheduling, payable generation and payment execution remain Finance-owned future work. P25 principal purchase-request approval follows P24; its prepared source is not acceptance.
