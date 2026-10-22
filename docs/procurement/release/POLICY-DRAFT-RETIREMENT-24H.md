# Unused buying-policy draft retirement — release CRUD addendum

## 1. Reference and status

PROC-SETTINGS-DRAFT-RETIRE-01; GW-ENG-STD-001 Tier 3. Source candidate with the bounded mapped/API/PostgreSQL verification recorded below. UI retirement browser acceptance and production deployment remain pending. No schema change is required.

## 2. Outcome

An authorised company configuration editor can retire a duplicate or abandoned unused buying-policy draft and retain its history.

## 3. Current UI and reuse

Extend the existing Buying rules table and shared Panel, Field, Input and Button components. Add no page, menu, master, workflow engine or generic JSON configuration store.

## 4. Owned entity

Reuse Procurement PolicyVersion. Retain its ID, key, policy version number, rules, author, dates and history. Change only State through existing SaveChanges stamping and optimistic concurrency.

## 5. Schema applicability

Current release PolicyVersion.State is varchar(30), with no enumerated state CHECK. Retired fits the existing schema. No migration, backfill or principal-key change is required. Confirm against the agreed release model/DDL before integration; advanced held policy worker-evidence hooks are outside this patch.

## 6. Actors

Current authenticated worker-linked Procurement configuration editors. Existing read-only ERP administrators may read rows, but receive no retire action. No new role, permission or implicit grant.

## 7. Permission

POST requires existing procurement-configuration-edit. GET business/action hints additionally evaluate that permission server-side. Frontend hiding never replaces the route check.

## 8. Scope

Use the existing ProcurementDbContext tenant/company query filters and ScopeMiddleware. Foreign-company IDs return 404. Policies are company-level configuration; no branch record or department grant is invented.

## 9. Entry and request

POST /api/procurement/v1/configuration/policies/{id}/retire with {version,reason}. Positive current CAS, nonblank trimmed reason, maximum 1,000 characters. Client must have saved ID and safe integer CAS; no DELETE endpoint.

## 10. Allowed transition

Only Draft -> Retired. PendingReview, Published, Retired and unknown states deny. Retirement does not publish, replace or shorten an effective policy and never approves a purchase.

## 11. Review and publication history

Require all ReviewRequestedAt/By/Reason and PublishedAt/By/PublicationReason fields to be null. Any PolicyVersion audit action outside DraftCreated, DraftUpdated and ordinary automatic create/update blocks retirement. This also blocks a returned draft whose metadata was incorrectly cleared but retained review history remains. Unknown future evidence fails closed.

## 12. Consumption and ownership

Deny any scoped PolicyEvaluation, ProcurementMethod, Supplier.StatusPolicyVersionId, SupplierDecision, SourcingEvent or InvoiceMatchResult reference, including inactive, blocked, retired or cancelled records. This is the complete current release domain policy-reference inventory. PRs reference evaluations, not a direct policy field; release POs have no direct policy field. Do not assume the inventory covers unmerged future modules.

## 13. Related reference writers

Existing buying-method create/edit previously accepted any existing policy. They must reject State=Retired. Both method reference writers and retirement execute SERIALIZABLE transactions so concurrent reference creation and retirement cannot both validate stale predicates. Other current live policy consumers select Published; arbitrary external owner SQL is outside the application guarantee.

## 14. Persistence and concurrency

Read current row and eligibility, then set State and append explicit audit in the same transaction/SaveChanges. Version is an existing concurrency token. Conflict or database serialization/deadlock returns 409 without automatic retry. On denial/exception dispose transaction without commit. No hard deletion or audit mutation.

## 15. Audit and events

Reuse AddAudit, action DraftRetired, canonical scope SubjectId, exact reason, policy identity/dates/state and existing raw RulesJson SHA-256 convention. SaveChanges also appends its ordinary update audit: one explicit retirement plus one automatic update audit, not one total. Outbox, Finance, stock, documents and approvals are unchanged.

## 16. Read contract

List/detail add draftRetirementVersion:1 and canRetireDraft:boolean. The latter is true only for permitted current editors and eligible unused drafts. List eligibility is one correlated scoped query over page IDs before response, not per-row GET calls. Existing response fields remain intact. Create/update results conservatively default canRetireDraft=false until a fresh list/detail.

## 17. UI compatibility

Retire unused draft appears only with explicit version1 marker, true action, Draft state, safe CAS and current configuration-edit permission. Missing/unknown markers, absent action and old backend remain readable without a misleading retirement button.

## 18. Confirmation and retained records

Shared inline panel explains retained history and asks for a reason. Explicit Confirm retirement POST; no auto POST on load. Retired rows remain listed with their status. Unsupported older policies are not simplified or rewritten by this action.

## 19. Failure and recovery

Client validates returned ID/key/policy-version/state/CAS exactly. Network, malformed or wrong response remains unconfirmed. Stale, forbidden or unconfirmed command locks repeat submission. Check saved rules performs GET only and closes the attempt; fresh business facts and a new explicit user command are required for any later action. No fabricated idempotency receipt or current-authority claim from history.

## 20. Context lifecycle

Buying rules child remounts on tenant/company/branch/worker changes. Abort old list results, clear old retirement state, ignore late retirement completion after unmount. Do not render a prior company's policy/action while selected shell company differs from readiness. Existing UI design and normal policy editor remain in place.

## 21. Mapped HTTP acceptance source

Eighteen authored cases in ProcurementPolicyRetirementHttpTests (one positive, five validation/scope/permission denials, eleven state/history/reference denials, one read-hint denial). Positive retains rules/dates and exposes no effective policy; stale duplicate has no second audit. Positive also proves both create/edit method references to Retired are rejected. Footprint compares current row plus audit/outbox/method counts on denials. Fixture uses synthetic auth and InMemory; no SQL/concurrency proof is claimed.

## 22. Client acceptance source

Six Vitest cases cover explicit marker/action/safe CAS; scoped ID/CAS/reason; no POST for unavailable action/blank reason; mismatched confirmation; stale conflict; lost response one attempt only. Existing policy client defaults and successful existing call behavior remain compatible; errors gain a status-bearing Error subtype.

## 23. Browser acceptance source

Five additional mocked Playwright cases in existing live-policies.spec.ts: explicit reason and retained row after reload; absent marker; ineligible owner action; interrupted response; stale conflict. Confirm zero automatic repeat and Check saved rules GET-only. These do not prove actual persistence or production identity.

## 24. Actual PostgreSQL acceptance still required

Run mapped retirement and method-reference races in a disposable upgraded release DB. Race the last eligible Draft with an independently committed method create/edit using two connections; require only one successful incompatible outcome, preserved audit and no new retired-policy reference. Assert exact row/rules/dates after retirement, 2 added audit rows, outbox unchanged. Denied/replayed/foreign-scope requests preserve full database footprint. SQL state and no-effects, not timeout alone, must prove conflict. No fake schema readiness.

## 25. Release verification

Parent serial gates: focused mapped HTTP class + existing policy/configuration regressions, client units, TSC, staged build, existing policy mock browser + five new cases; actual TestServer/PostgreSQL CRUD probe and browser scenario before positive deployment claim. Parent chooses existing probe extension after source review, not a new app adapter.

## 26. Rollback and compatibility

Code rollback retains Retired rows readable and non-effective; old edit and submit require Draft so stay blocked. Old method writers could link Retired records again if rolled back; document that operational compatibility limit. No destructive migration or evidence removal. No use of advanced held P29/P31 proof tables or live flags.

## 27. Implementation files

Backend ProcurementPolicyDraftRoutes.cs, ProcurementConfigurationRoutes.cs and new ProcurementPolicyRetirementHttpTests.cs. Frontend LivePolicies.tsx, procurement-policy-api.ts, new policy-retirement-api.test.ts and existing live-policies.spec.ts. Patch is staged only under /tmp/proc-policy-draft-retirement.

## 28. Remaining gates and exclusions

Current status: authored, uncompiled and unexecuted. No source registration outside existing routes, no new publish/approval permission, no current authority inferred, no Finance/stock side effect. Integrate only after current release candidate gates finish and exact source is reviewed/frozen. Maintain governance-worker evidence separately before combining held stacks.

## Verification addendum — 22 October 2026

The original18 authored retirement cases and the existing84-case mapped cohort passed together:102 passed, zero failures/skips. One initial expected foreign-scope status was corrected to the existing Core403 (before record lookup), without changing authorization; the original failed run is retained.

A private real PostgreSQL/API run initially exposed an actual wrapped serialization abort returning500. Commit `2e6a0df` narrowly recognizes underlying PostgreSQL40001/40P01 in the existing transaction error paths and returns409. It does not swallow other exceptions or retry commands. The failed run remains retained. The corrected candidate passed268 assertions across114 HTTP requests, including an overlapping policy retirement/method reference race with exactly one compatible commit and no newly committed reference to a retired policy. Source/runtime manifests bind the private run to `2e6a0df`.

Private evidence: `artifacts/crud-release-24h/private/browser_20261022_055126/launch-manifest.json` and `probe.log`; mapped gates under `artifacts/crud-release-24h/policy-increment/`. These local artifacts include protected fixture metadata and are not committed. The API fixture uses synthetic outer identity and an owner database connection; this is not genuine SSO or finite-role database proof. UI browser retirement and final integrated release gates are still pending. No production migration, policy publication or deployment occurred.
