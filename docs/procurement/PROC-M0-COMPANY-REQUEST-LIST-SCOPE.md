# M0 — Company-wide request-list scope

Status: ready for bounded implementation; not yet runtime accepted or deployed. Baseline: deployed P24 product `b3e190ba`, existing UI `7cf8f55f`. Parent findings: CORE-02/AUD-03; no whole-M0 closure claim.

## 1. Feature Identification

M0-READ-01: explicit All-branch request collection reads and matching company-owned planning/contract collection context.

## 2. Purpose & Business Outcome

A permitted user selecting All branches sees their own requests from every currently permitted active branch, with honest counts/paging and branch labels. A request change never silently chooses a branch from that company-wide view.

## 3. Scope & Exclusions

Exact GET collections `/requests`, `/plans`, `/contracts` under `/api/procurement/v1`. Requests are branch-owned; plans/contracts are company-owned and keep their current owner predicates. Other operational All routes, review queues, detail/edit routes, approvals, budgets, financial authority and new organisation-wide grants are excluded.

## 4. Parent & Related Features

Existing Core workspace choices, Procurement context/meta, request CRUD, company-master CSV scope, supplied request directory and shared table. Core remains organisation/assignment owner.

## 5. Actors & Participants

Current authenticated Core account with module and route permissions; employee linkage is optional. Request ownership remains the current requester principal. Core administrators manage assignments separately.

## 6. Entry Points & Triggers

Existing Organisation context → All branches → My Requests/Purchase Requests. Existing company-owned plan/contract lists use the same acknowledged context. No new page or scope master.

## 7. Preconditions

Active Core account, current explicit action grants, valid tenant/company and at least one current permitted branch for confined accounts. Explicit All cannot also supply a branch. Existing no-assignment Core organisation-wide semantics are preserved, not redefined here.

## 8. Workflow

Authenticate/recheck current grants → validate header contract → load current Core branch/company intersection → resolve All without choosing a branch → apply request tenant/company, permitted-branch and original-owner predicates → count/order/page → display existing directory with branch labels. Mutation from explicit All returns a branch-selection error before any request/source write.

## 9. Status / State Transitions

Reads cause no business transition. POST/PUT/PATCH/DELETE request commands with explicit All are refused. Selected-branch and legacy non-All commands retain their existing state/version rules.

## 10. Permissions & Data Scope

No new role or grant. Require `procurement-access` and each current collection action. Confined All includes only current active assigned branches in the chosen company. Another maker, branch C, another company or tenant is excluded before count/page. Existing company-wide actors may see their own historical branchless requests; confined actors cannot.

## 11. Business Rules

Do not infer access from employee home/designation. Missing or archived assignments cannot expand a confined scope. Preserve the existing no-assignment organisation-wide contract pending separate AUD-03 policy work. Company-owned contracts/plans do not acquire invented branch ownership. Request review commands require a branch; do not present a first-branch review queue as company-wide.

## 12. Data Fields & Ownership

Reuse request `BranchRef`, requester principal, tenant/company and Core branches/assignments. Add optional `branchRef` to collection summaries for display; no new persisted field/model/migration. A legacy own request with no branch is labelled Company-wide only for an already organisation-wide actor.

## 13. Screens & Data Views

Reuse supplied request list, shared directory/table, top selector, pagination and existing branch-required workflow state. Show a Branch column only in All. Keep the buying-rule review panel for selected-branch context; All is a browsing view, not a partial review queue.

## 14. User Experience / Human Behaviour

Explain that a branch must be selected to open/change a request. Preserve list layout and controls; no parallel simplified screen. Do not show a raw ID as a branch label or claim a page-local result is a complete export.

## 15. Notifications & Communications

No notification on reads or denied mutation. Existing actual request commands retain their owned notifications.

## 16. Documents & Attachments

No attachment changes; current document authorization and scanner requirements are unaffected.

## 17. Exceptions & Edge Cases

Malformed/co-present scope headers, archived branch, foreign company, stale/revoked assignment/grant, missing principal, empty permitted set, branchless history, pagination boundaries and rapid scope/account switches must deny or reset safely.

## 18. Integrations / APIs / Events / Sandbox

Core-owned assignment projection is read-only. Existing route/body/response contracts remain, with an additive optional summary branch ID. No new event or public integration; actual normal-cookie/private PostgreSQL proof is separate from mapped synthetic-auth tests.

## 19. Audit / Security / Privacy

Reads and denied writes must leave Procurement source, audit and outbox unchanged. Preserve normal Core session bookkeeping. Verify direct API calls, not only UI visibility; refresh authority on existing sessions.

## 20. Reports / Dashboards / Metrics

Only request collection counts and pages are covered. No dashboard, review-queue, approval-queue, complete-query export or whole-ERP aggregation claim.

## 21. Configuration & Localization

No new policy thresholds or permission defaults. Core supplies company currency, active branch names and existing permitted defaults. Reuse normal user-facing labels.

## 22. Non-Functional Requirements

Apply branch/owner filters in the database before count/paging; bounded existing page sizes and cancellation. Scope/account changes invalidate old list state; no client-side security filtering.

## 23. Postconditions

All result/count reflects exactly permitted own records; no new branch is chosen or grant inferred. Selected-branch reads/changes and company-owned masters retain behavior.

## 24. Failure & Recovery

Existing read retry is safe. Failed All commands preserve all business records and require explicit branch selection. Keep unavailable/read-failure states; never fall back to local demo records.

## 25. Acceptance Criteria

All A+B includes both own branches, excludes C/foreign company/tenant/other maker before count/page; partial branch revocation takes effect for the same session; selected A excludes B; explicit-All request writes have zero effects; plan/contract owner/company scope is retained; UI displays branch labels and no first-branch review queue.

## 26. Test Scenarios

Mapped middleware and real route tests cover allowed/denied/malformed/current-assignment/count/page/legacy branchless cases and selected-branch CRUD. Genuine P24 PostgreSQL and normal-cookie Playwright prove the UI/API path with independent source/audit snapshots. Regress existing CSV/session/first-use tests as appropriate; record source/build/schema and failures.

## 27. Ownership / Dependencies

Core owns current account/company/branch authority. Procurement owns request read/command rules; existing shared UI owns presentation. API-only changes keep the accepted P24 App/Domain/Infrastructure graph. Release API/publisher together after verification; R0 still gates any future schema change.

## 28. Open Decisions / Assumptions

This slice does not redefine unassigned versus explicit organisation-wide access, enable All on every operational route, assign privileges, or unblock unsupported review/approval/Finance workflows. Detail/edit remain branch-bound in the current UI. M0 remains partial until those separate operation/policy checks are accepted.
