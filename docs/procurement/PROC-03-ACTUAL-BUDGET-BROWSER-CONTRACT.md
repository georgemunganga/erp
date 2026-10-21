# PROC-03 — Actual exact Budget browser acceptance fixture

Status: source authored only, not compiled/run. Backend base740a629; frontend test-only base9d9b4c5. The existing 28-section Budget UI addendum covers business behavior; this artifact defines its actual HTTP/owner acceptance boundary. No production source/model/schema/provider/permission change.

## Scope and identities

Reuse `ApprovalHttpPostgresProbe --serve --orders`, actual Core users/grants, separate migration owner and restricted runtime connection, owner-private manifest and loopback server. Add a `budget` manifest member only in that mode. Existing orders fixture/verifier remains unchanged and opt-in. No new CLI mode, authorization bypass or fake schema readiness. Header authentication and manual Finance reference are synthetic acceptance boundaries, not production authentication/external Finance attestation proof.

## Genuine source preparation

After existing Core actor roles/branches are finalized, create and submit one PR through real HTTP as the actual requester. One goods line: quantity1.0001, estimatedUnitPrice0.0001, exact source0.00010001 ZMW. The source remains Submitted/PendingPolicyAndBudget with no Finance check, Core case, approval pin/outcome or allocation. The actual policy supports8dp line-base evaluation; the fixture must fail rather than forge a submitted/funded/approved row if any API guard rejects it.

Read this source through Finance's real budget queue and retain both currentCAS `version` and authoritative `businessVersion`. The queue may also contain unrelated existing fixture requests; do not pretend its count is1. Retain original full PR+line JSON SHA and company-scoped counts after setup; owner verification runs after the browser and server shutdown.

## Manifest contract

Top-level existing loopback URL, probe tenant, company/branch IDs, X-Probe-User header and actors stay unchanged. New `budget` fields:

- requestId, lineId, number, named purpose; currentVersion and businessVersion from the actual queue/source.
- exact strings quantity1.0001, unitPrice0.0001, exactSourceAmount0.00010001, reviewedCheckedAmount0.0002, currencyZMW.
- existing Finance principal; fixed unique-per-fresh-database Finance/evidence/reservation references.
- noFinanceCheckOrApprovalSeeded=true for this source, authorityMeaning=SyntheticManualFinanceEvidenceOnly.

No passwords, access tokens, connection strings or invented policy authority are included.

## Browser boundary

Frontend `tests/backend-browser/budget.spec.ts` forwards every Procurement request/context/meta/read/command to the actual server with the finite Finance principal and current company/branch headers. Successful responses are fulfilled byte-for-byte; numeric financial lexemes are not reserialized through JSNumber. Only shell authentication/branding/organisation display is synthetic, following existing actual PO/receiving conventions.

Select the named actual request. Assert queue raw numeric0.00010001 and both versions, empty checked amount, and no preselected funding outcome. Enter real test references and explicitly choose Available. A proposed0.0001 is blocked locally with zero POSTs; explicit reviewed0.0002 then produces exactly one actual201. Assert outgoing checkedAmount string/currentCAS, returned retained businessVersion/canonical server attribution, persisted history and no approval/order/payment POSTs. The UI does not prove external available funds or approve the request.

## Independent post-stop owner proof

`PROCUREMENT_BUDGET_BROWSER_VERIFY_ON_STOP=1` runs only for serve/orders, after graceful shutdown. Using the existing separate owner provider, verify:

1. Original full PR+line digest unchanged; exact quantity/unit/source value, branch/company/requester, current/submitted versions, Submitted/unapproved states and all quantity counters unchanged.
2. Exactly one source BudgetCheckSnapshot with actual Finance subject/current canonical worker, independent requester worker, schema1 binding hash valid against full Finance tuple, SQL microsecond timestamp, exact0.0002, ZMW and all retained references/status.
3. No Core case, Procurement approval submission/outcome or allocation for this PR.
4. Exactly one BudgetChecked audit. Company-wide baseline changes are exactly one snapshot and one audit; outbox, Core cases/decisions, approval pins/outcomes, allocations, orders and policy-evaluation counts unchanged. This complements browser traffic's exactly-one-POST assertion; it does not claim an extra post-stop read-footprint interval.

## Planned operator gate (not run by author)

Root must build this tool from its own frozen reviewed source and start a brand-new disposable proc_approval_http_* database/server with distinct owner/runtime roles and actual migrations, as existing --serve --orders requires. Set the Budget verifier environment variable; leave ORDER_BROWSER_VERIFY_ON_STOP unset when running only Budget acceptance. Otherwise the independent order verifier correctly expects its own separate PO browser flow.

Run the test-only frontend branch against the retained verified Budget JS index-JuvMCZhV.js/CSS index-Bddc462I.css bundle. Use a reviewed read-only/copy/symlink in the new worktree's relative .budget-verification-dist, not an overwritten original candidate. No new app build is implied by this fixture/test-only slice. Dedicated actual config is playwright.budget-backend.config.ts, port3045, one worker, retries0,180s case/30s expectations/45s forwarding. Keep fresh .budget-backend-results and private source/manifest/traffic/artifacts/logs. Parent controls resource clearance, graceful stop and terminal owner proof.

## Remaining limits

No author build/SDK/Node/DB/browser jobs ran. Actual restricted P27 CLI is a separate acceptance record, not replaced here. A successful synthetic browser run cannot certify production authentication, Finance provider completeness, approved spending policy, malware protection, AP authority or deployment. No production mutation/feature enablement.
