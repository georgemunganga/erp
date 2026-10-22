# P24 recurring purchase planning — published source acceptance

Date: 22 October 2026 UTC. Status: **source milestone accepted and merged; production release pending**.

| Repository | Accepted source | Published main |
| --- | --- | --- |
| ERP backend, PR #30 | `8f3c5000aeb22fda2df26795d3e0004fc5ef2f4d` | `9f77b03c29a7537f97a759fd5d586ceb2879e0b2` |
| Existing Procurement UI, PR #54 | `4a76f4e014ca1d9289afd1d059d9135025ad45be` | `6fca23d4687668e85445c1c56ca92eefac1ab27c` |

Tested application/tool source is `be5d87e3bfdc2b5d6d6be576ce321173e8c6b9f5`; tested UI is `b9f9ff15b97d89db1f89f6234929ed89feb0b0b4`. Later accepted commits change documentation only. The merge introduces no additional application changes.

## Behaviour and ownership

Recurring purchase plans support create, scoped list/read, edit, unchanged-update revision and retained closure. Core owns active accounts, permissions, company/branch scope and organisation currency. A linked employee is optional. The original principal remains owner; worker aliases never grant authority. Closed records cannot be overwritten or reopened. Contract supplier changes are blocked when referenced by plans, including closed plans, with an actual concurrent-write safeguard.

These are planning records. They do not schedule or create POs, reservations, bills, liabilities, payments, inventory or assets. The future ERP Finance module owns AP and payments and is not built. No fabricated Finance balance, history, reservation or successful provider is substituted.

## Executed acceptance

- **1,206** full Procurement domain tests and **563** selected API/host tests passed, zero skips; the corrected provenance focus also passed.
- Four complete publications and three actual model checks passed. All 820 frozen inputs and 141 migration artifacts remained unchanged during the final gate.
- Genuine migration `20261022144102_P24CorePrincipalRecurringPurchasePlanning` passed private fresh/used upgrades, repeated idempotency, unused rollback/reapply, retained-evidence vetoes and original data/catalog/history preservation.
- **15** actual PostgreSQL constraint negatives passed with unchanged persisted facts. These are owner-storage probes, not API authorization evidence.
- Actual restricted-login HTTP passed **696 assertions / 246 requests**, fourteen privilege denials, CRUD/version/closure, scope/current-action revocation and the observed two-request contract race.
- Existing UI: **53 Vitest** and **4 proxy tests**, Node22 typecheck/build and controlled browser cases passed. The initial ambiguous Edit locator failure and corrected-case pass remain separately recorded.
- **2 actual Playwright cases** passed in 50.7 seconds, one worker, zero retries/skips. After controlled shutdown, fresh owner reads confirmed closed versions1/2/3/4 with audit, exactly one lost-delivery draft, and unchanged prior/downstream records.
- Source, runtime, staged UI and the protected fixture hash were unchanged. All private application runtimes stopped; databases, evidence and publications are retained.

The browser's database connection and Core projection are owner-based; only the separate restricted Procurement HTTP subset proves finite database rights. Outer authentication is synthetic. No production SSO, native production-host execution, Finance/AP, scheduler or whole-module completion is claimed.

## Evidence and findings

See [the detailed source/schema/API/browser record](PROC-07A-P24-INTEGRATION-READINESS.md) and UI `reference-ui/tests/actual/P24-UI-VERIFICATION-20261022.md` in the existing UI repository.

Final private backend acceptance: `/home/amizo/erp-backups/procurement-p24-recurring-browser-20261022_1526/acceptance.json`, SHA-256 `863f68c97ffbcf2cb35d76895a024d26c8af600e824c6874f9bc172e207a94f7`. Root browser evidence is retained under `procurement-p24-actual-browser-20261022_1529/`.

Two inherited provenance-fixture failures were corrected without changing production expiry or lineage rules. The fixture now uses its persisted creation time and an open-ended test authority. The original failures remain retained. A hash-bound receipt also clarifies a stale top-level API-manifest alias; the accepted API publication manifest is `3669b25f9706ef53f01ab2e0e083831dd6e0a34a93057b9d136fc203cd8a1721`.

Hosted backend job119918179912/run39805692960 did not start: GitHub reports an account billing lock and no job steps. The private automated gates above passed; no hosted-CI pass is claimed.

## Release and successor work

Production still uses the released P22 backend and branch-recovery UI. Rehearse combined P23/P24 migrations on a protected restore, reconcile the actual runtime role/ACL/default ACL, verify native startup and rollback/forward recovery, deploy the paired frozen API/UI and verify real sign-in/live CRUD. Do not destructively downgrade after retained plan or audit evidence exists. Existing operational workflow flags stay gated; no Finance or document-scanner activation accompanies source integration.

P25 principal-based request approvals follows this published schema prerequisite. Manus retains its separate shared Core frontend build task and must start from the published baselines above. This milestone does not complete approvals, sourcing/ordering/receiving, all settings/portal/analytics or Finance integrations.
