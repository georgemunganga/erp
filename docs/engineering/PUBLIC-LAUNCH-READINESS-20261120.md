# Kuubu public-launch readiness checklist

**Release decision: HOLD — remaining live-account acceptance and unfinished features are explicit below.**

Latest evidence: [completion test report](PUBLIC-LAUNCH-COMPLETION-20261120.md).

## 1. Outcome
A new customer can subscribe or use an operator-issued trial, create a company, set up HR/payroll manually or by import, invite staff and reach the correct ERP workflow without assistance or access leakage.

## 2. Scope
Kuubu account website, ERP Core and HR/payroll. The page register contains all source routes and expanded settings, including aliases and record routes. Procurement, Vcron and on-premise compatibility have separate release gates; passing this HR audit does not qualify them.

## 3. Ownership
Laravel owns identities, explicit product enrollment, commercial customers/subscriptions/billing. Core owns company structure and shared access. HR owns employees, employment assignments, leave and attendance. Payroll owns effective-dated pay configuration, runs and release/payment controls. No duplicate authority is introduced.

## 4. Actors
Company owner, HR administrator, payroll maker/reviewer, manager, employee, billing-only contact and unrelated company user. George's existing synthetic trial companies are the authorised change scope. His owner session alone cannot qualify least-privilege controls.

## 5. Evidence rules
CHECKED means actual recorded evidence for the named action only. OBSERVED means the page rendered. NOT TESTED and REQUIRES CONTEXT are release holds for included features. A denied unsafe action can be an expected PASS; a generic error/empty list is not permission proof. Prior evidence is labelled prior rather than rerun.

## 6. Current baselines
API correction679b01c179ff7d9ae8d5559c3ffa50f2498d17d3 overlays deployed d41303dd + ownerf081d5a3 + rate/date12ca4a95; webcf64a06f3676064c12fa6e64af061484c5b52c13. Live API29009/web29011, proxy kuubu-launch-completion02-proxy01, backup25. Laravel current native-identity-8e6c3a9-deploy02 matches99 application/config/route/migration/lock files in local Git source. Earlier baseline pins in audit entries are historical; this section supersedes them.

## 7. Customer entry and identity
- [ ] Fresh verified registration, email verification, password login/recovery and logout.
- [ ] Google registration/login and deliberate linking to an existing password identity.
- [ ] Session expiry, refresh, multiple tabs and useful recovery without accidental account merge.
- [ ] Invoicing-only customer creates neither login nor product/company access.
- [ ] Explicit enrollment and invitation boundaries; no email/domain-based membership.

## 8. Subscription, invoice and activation
- [ ] Multiple modules and seats per module produce the saved server-authoritative total.
- [ ] Payment cancellation, reopen, pending, duplicate callback and final confirmation.
- [ ] Activation occurs only after confirmed payment or an authorised operator trial.
- [ ] Branded invoice/receipt PDF agrees with saved totals and billing scope.
- [ ] Renew/cancel/expire trials and subscriptions without provisioning duplicate companies.
Real-money actions require an authorised purchase or isolated provider sandbox; none are executed by this page sweep.

## 9. Company and team
- [ ] Create manually; retry and refresh preserve exactly one company.
- [ ] Owner opens licensed modules without first granting themselves ordinary team access.
- [ ] Invite, accept, change/revoke roles and location scope; rejected access remains denied.
- [ ] Switching companies cannot carry workers, drafts, module grants or amounts across tenants.

## 10. Core organisation prerequisites
- [ ] Legal entity, location and department create/read/edit/close flows persist.
- [ ] References prevent unsafe removal and preserve historical reporting.
- [ ] Canonical Core settings and HR links open their intended editor in SaaS.
**Observed blocker PL-001:** shared Core routes render the generic Kuubu ERP/company chooser even for the trial owner. Root product guard admits only /hrm paths. Source-only placeholder editors are listed separately and are not assumed usable.

## 11. First-time setup
- [ ] Fresh company reaches setup; completed company opens the requested workflow.
- [ ] Employment, work calendar, leave and payroll defaults save correctly.
- [ ] Completion links to full manual employee entry, import, compensation and payroll readiness.
- [ ] Completion never implies every employee has salary/bank/statutory readiness.
Prior synthetic manual/import setup and first-calculation evidence exists; not every setting lifecycle is qualified.

## 12. Manual employee setup
- [ ] Employee and agreed assignment save; department detour retains the session draft.
- [ ] Create/read/edit contract, date, location, department and emergency/payment information.
- [ ] Retry after partial success does not create duplicate employees or assignments.
- [ ] Invalid important fields fail before submission with actionable correction.
Prior manual employee/assignment creation and partial-create UI regression checks passed; retirement and role-negative cases remain untested.

## 13. Import alternative
- [ ] Mapping, Check missing data, preview, correction, apply and saved-record verification.
- [ ] Mixed-case known classifications, permitted branch/department creation and scope denial.
- [ ] Explicit contract completion remains separate from worker classification.
Prior import and explicit contract persistence evidence exists. Import is optional; it cannot substitute for a complete manual path.

## 14. Salary components and statutory rules
- [x] One synthetic custom component: create, read, edit and archive verified through UI and database. Other component kinds and negative validation remain untested.
- [ ] Protect required/default components and configured statutory semantics.
- [ ] Archived components cannot silently alter historical runs.
- [ ] Tax/contribution changes are effective, authorised, auditable and reflected only where intended.

## 15. Pay groups, periods and calendars
- [ ] Defaults exist for a fresh company and manual configuration is sufficient without imports.
- [ ] Create additional groups where supported; edit, status changes and period generation.
- [ ] Frequency, currency, last-working-day payday, cutoff and holidays remain coherent.
- [ ] Multiple groups select the correct population and effective profile.
Source inspection shows group read/edit; extra-group creation is not yet qualified.

## 16. Basic pay and compensation
- [ ] Assign basic pay, group, effective date and optional allowances manually.
- [ ] Edit a profile; verify effective dates, duplicate prevention and previous run stability.
- [ ] Missing profile/basic and wrong-period assignments are visibly excluded or blocked.
Prior manual basic5000 assignment and calculated run passed; profile retirement/history and all combinations remain unqualified.

## 17. Attendance, breaks and work inputs
- [ ] Employee linkage, permitted location and check-in/out.
- [ ] Break/departure/return, exceptions, approvals and offline/retry semantics.
- [ ] Payroll includes only approved applicable attendance/overtime.
- [ ] Mobile SaaS and separately configured on-premise instances preserve boundaries.
This owner is not an employee in the fresh QA companies; self-service clock-in requires the proper linked test actor. Do not grant access just to make a screen green.

## 18. Leave and variable pay
- [ ] Leave types, periods, balances, request/edit/cancel and manager decisions.
- [ ] Unpaid leave, overtime, claims, advances and deductions reach the correct period.
- [ ] Revocation, effective dates, rounding and duplicates are covered.

## 19. First payroll calculation
- [ ] Manual route and import route reach calculated payroll independently.
- [ ] Exact employee population and saved engine components match totals.
- [ ] Company/group labels are real; no fabricated estimates or demo employer.
Prior live proof: both companies, one worker/run each, gross5000, employee deductions300, net4700, employer contributions300. Runs remain CALCULATED.

## 20. Review, approval and release
- [ ] Maker/checker and permitted authority transitions.
- [ ] Review exceptions, reject/correct/recalculate with a traceable history.
- [ ] Statutory reference readiness blocks release when missing.
- [ ] Released payslip visibility and correction/version rules.
Not executed in the live synthetic companies; their statutory references are deliberately incomplete. Expected disabled release does not qualify successful release.

## 21. Payment and accounting closeout
- [ ] Bank readiness, controlled file generation and protected download.
- [ ] Payment approval, release, reconciliation and closeout.
- [ ] Accounting reports and corrections match saved records.
Not executed. No salary payment, bank release or statutory submission is authorised by this page audit.

## 22. Lifecycle and linked records
- [ ] Onboarding, moves, contract changes and offboarding preserve effective history.
- [ ] Position, department and location references stay valid after updates/closure.
- [ ] Termination handles final-period payroll and revokes only intended access.

## 23. Supporting module pages
- [ ] Recruitment, performance, learning, relations, documents and announcements.
- [ ] Either qualify their actions and dependency chains or explicitly keep them outside the public launch scope.
- [ ] No connected-looking navigation to preview, demo-only, unfinished or unsaved editors.

## 24. UI and downloads
- [ ] Light/dark inheritance, text contrast, responsive tables, keyboard focus and error feedback.
- [ ] Dates/currency labels and signed-in company are clear.
- [ ] CSV/PDF/print outputs respect company scope, content and branding.
Prior payroll text/color fix passed actual compiled-CSS and live browser checks. That does not qualify every page's layout.

## 25. Recovery and capacity
- [ ] Backup restore rehearsal against the current schema and owning application.
- [ ] Disk/database capacity, log retention, health, alerts and rollback tested.
- [ ] Session continuation tolerates ordinary navigation and bounded retries.
**PL-002:** page sweep reproduced session HTTP429/reconnect. Owning API logs confirm /api/core/product-identity/operational/session429. Investigate request amplification, shared reverse-proxy source address and appropriate authenticated budgets; do not disable abuse controls or treat all affected pages as independent failures.
**PL-003:** fresh read-only server observation320.7MiB free on a50GB volume. Establish safe operational capacity before admitting public customers.

## 26. Page-by-page evidence
See [the complete page register](PUBLIC-LAUNCH-PAGE-CHECKLIST-20261120.md). Private/synthetic raw snapshots are retained locally under inspection/qualifications/public-launch-audit-20261120-01; they are not committed as public employee data. Original incomplete/loading observations are retained; reruns use new filenames.

## 27. Fix and retest policy
Record severity, exact reproduction, owning UI/API/domain records, downstream impact, fix source and failing/passing evidence. Test with disposable fixtures or an isolated restored database. Never rewrite prior proof or approve public launch because a build or homepage passed. Changes remain a separate reviewed step after this audit.

## 28. Public-launch acceptance
- [ ] Scope is explicit, every included page has qualified actions, and excluded features cannot be mistaken for supported production workflows.
- [ ] No unresolved critical/high defects in entry, scope, payroll calculations, persistence or recovery.
- [ ] Full manual and import pipelines pass; role-negative and company-isolation cases pass.
- [ ] Payment/release/closeout has appropriate isolated acceptance evidence.
- [ ] Restore/capacity/monitoring and rollback are qualified.
- [ ] Exact deployed sources and operator handover recorded.
Until these are satisfied the decision remains HOLD; this is an honest readiness checklist, not a claim that the whole ERP is production-ready.


## Live lifecycle acceptance additions

- Salary component in QA HR Import 20261118: created manually as QA Launch Allowance 20261120, code input normalised to qa-invalid, fixed amount75; reopened and edited to100; appeared as an explicit optional amount in Compensation; archived manually; verified absent from the pay-assignment dialog afterward. Owning database confirms one archived component, amount100. It was not assigned to a worker. Existing calculated run remains gross5000, deductions300, net4700. This qualifies the tested component lifecycle and candidate list, not custom-component calculation or all payroll-setup tabs.
- PL-004: existing pay-profile effective date appears blank in the HTML date input when opening Edit pay. The persisted date is2026-11-01; the DTO displays11/01/2026 and MapProfile uses DateOnly.ToString() without an invariant ISO format. Compensation passes that string directly to a native date input. The profile was cancelled without saving. Standardise the API date contract and test edit/history semantics before qualifying compensation updates.
- Session-rate failures also recurred during the longer navigation session, so normal-navigation resilience is not yet qualified. The successful linked-page observations are retained separately; a transient recovery does not close PL-002.


## Audit checkpoint and ordered fixes

169 route entries are mapped;66 unique route entries have retained browser observations (including redirect aliases), not66 qualified business screens.38 rendered observations remain action-unqualified;11 return a company chooser;10 require session/reconnect retest;3 were captured while access was pending;2 while data was pending;2 are explicit release-disabled pages.81 static routes and22 record-context routes remain untested in this batch. One component lifecycle has separate UI/database proof. The sweep was interrupted repeatedly by PL-002; no blanket page-pass claim is made. Original incomplete/failed snapshots remain retained alongside successful in-app-navigation reruns.

Fix order: (1) session continuity/budget attribution/recovery, (2) authorised Core settings routing, (3) invariant ISO date contracts and effective-dated compensation/history acceptance, (4) operational storage capacity/restore rehearsal, then resume the remaining page and actor checks. Shared access controls must remain authoritative; do not solve navigation by removing company/module/role validation. No application fix or deployment was performed by this audit. The only business-data write was the synthetic component lifecycle; the final test component is archived and unassigned, and employee pay/run totals stayed unchanged.

Raw evidence and case statuses: inspection/qualifications/public-launch-audit-20261120-01/workflow-cases01.json; checkpoint-manifest01.json hashes the retained packet. Runtime health200 and an active backup timer are recorded separately from unqualified restore/capacity acceptance. No production approval, payslip release, salary payment, account reset, access grant, Perfex change or on-premise update occurred.


## Authorised blocker corrections — 20 November 2026 (qualification in progress)

The user authorised fixing PL-001 through004 and confirmed the product rule: Core has no separate subscription price; company access derives from at least one valid module subscription/trial, with owner/delegated permissions still enforced. No Core SKU, enrollment bypass, role bootstrap or new commercial authority was added.

PL-001: product root routing now admits the exact Core namespace alongside HRM under the existing verified workspace provider. Similar prefixes/foreign paths remain denied. SaaS users/roles/branch-access pages direct to Kuubu Team & access rather than invoking standalone credential or legacy grant editors. Existing on-premise behavior is behind the unchanged non-product branch. This fix must be browser-qualified with existing company/module authority; it does not certify every Core setting editor or every module-only company configuration.

PL-002: the prior guest address budget applied to all protected API calls and session checks, exhausting60/minute behind the proxy. A protected operational cookie ticket must now match its protected browser binding, issuer, purpose, expiry and account subject before receiving a session partition. A verified session has600 requests/minute, all verified sessions together6000/minute and a fixed4096-key memory bound. All normal credential, licence, company, grant, branch, CSRF and per-request checks remain. Anonymous/invalid credentials and start/prepare keep the existing guest budgets; invented headers or tokens cannot select a trusted partition.429 responses carry Retry-After60 and no-store; the frontend defers automatic retries for that window while keeping forms suspended until proof returns. No issuer or company access is granted by a rate-budget decision.

PL-004: payroll profile effective dates and pay-period dates emit invariant yyyy-MM-dd from their existing owning service DTOs. Calculations, salary amounts, tax rules, profile revisions and database schema are unchanged. Culture regressions exercise the public service projection under en-US/en-GB/fr-FR/ar-EG; live edit/save/reload remains a separate acceptance step.

PL-003: only three confirmed stopped build containers were removed: full-hrm-build01, full-hrm-build02 and account-private02-native-modules01. Their private command/metadata/log/diff evidence is retained in /opt/kuubu-production/maintenance/storage-20261120-01. Bind-directory device/inode/ownership/mode/mtime were unchanged. No images, volumes, publications, deployments, database or rollback containers were removed. Free space rose from320MiB to3366MiB. Future growth/capacity and restore acceptance remain independent release gates.

First checks:52 focused backend tests passed,14 frontend contract tests passed and TypeScript passed. Broader tests and publication use a new exclusive source workspace from deployed baselined41303dd plus owner patchf081d5a3, with only the rate/date changes and their tests overlaid. No model or migration change or unrelated backend deployment is intended. Fresh immutable API/web publications and readiness-checked cutover retain the previous pair for rollback; final live results follow below.


Broader qualification completed:620 passed/15 skipped on the deployed-baseline composite, with the original619-pass/1-fail/15-skip attempt retained. The unmodified prior owner-baseline DLL reproduced the same stale grant-version assertion independently. Its updated test now asserts the persisted revision2 company-wide owner grant, required HR-admin action, no issued session, idempotent repeated review and licence denial. No production permission implementation changed to satisfy the test. API publish, final TypeScript and final frontend build02 passed; live activation/acceptance follows separately.


## Deployed blocker corrections — 20 November 2026

API correction `12ca4a956e4d6d847dd6f3ad8f9dc2907226d095` overlays deployed backend `d41303dd56ec0196e040044fbc55d0ae5faddbe7` plus owner patch `f081d5a34e4ed0e0c1ecf6604e62ba81938bf90d`; it does not deploy all backend changes on this branch. Web `ffeb04a6a44b39ed2780d9cae6f4c5d1fb802b3c` includes live Core company labels/current date. Both are pushed to `codex/kuubu-product-consumer-20261107`. Qualification:620 backend passes/15 environment-dependent skips,14 frontend contracts, TypeScript and final build03 passed.

Current API/web/proxy: `kuubu-launch-blockers01-api01` / `kuubu-launch-blockers03-web01` / `kuubu-launch-blockers03-proxy01`; ports29006/29008. Immutable API publication01 and web publication02 retained. Configuration/backup23 lives in `/opt/kuubu-production/releases/launch-blockers-20261120-web03`; existing API environment retained. Previous containers stopped/retained for rollback. First web activation failed before public cutover on directory permissions; failed publication retained and independent successor built. No schema, Perfex or on-premise change.

- PL-001: owner HR trial opens Core organisation, with real company names, current as-at date and employee references. SaaS Users gives central Team and access guidance. Core has no separate charge with at least one active module subscription/valid trial. Membership, owner/delegated permissions and business-module licences remain enforced. Procurement-only Core entry, delegated/negative actors and all Core mutations are not qualified here. Entity/location editors remain read-only in this view.
- PL-002:16 authenticated reload/navigation observations had no reconnect prompt. New API session sample31 HTTP200/no429 and one cancelled request logged500. Isolated HTTP regression accepts90 verified session reads; fabricated authority stays guest-limited. Rapid page changes cancelled in-flight database/HTTP requests; raw cancellation500 evidence is retained, not represented as successful responses. Network interruption/session-expiry recovery remains separate acceptance.
- PL-003: immediate exhaustion relieved; final3233.4MiB free, both health endpoints200, backup timeractive. Only three stopped failed build containers removed after retaining private metadata/log/diff. No bound publication, image, volume or database deleted. Growth, restore rehearsal and alerting remain launch gates.
- PL-004: synthetic existing profile opens with ISO2026-11-01; once data fully loaded, same-value save/reopen retained group, date and basic5000. Earlier incomplete/failed save snapshot retained; no group silently substituted. Owning SQL confirms one import profile and both runs still calculated/unpaid5000 gross/300 deductions/4700 net. Future-date/history edits remain separate cases.

Fresh proof: `inspection/qualifications/launch-blocker-fixes-20261120-01/browser01` includes Core/team/profile/navigation and readable payroll screenshot. SQL packet `hr-first-run-20261118-01/launchblockerspostsave02.private.json` confirms saved state. No payroll approval, release or payment executed. Original169-route audit counts remain unchanged: these targeted retests do not qualify every page/action. Full public launch remains **HOLD** for untested CRUD, actors/isolation, payroll closeout and restore/capacity gates.
