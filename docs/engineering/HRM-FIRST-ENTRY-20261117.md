# Automatic HR/payroll entry and import labels

## 1. Outcome
A pending company opens HR/payroll configuration after Kuubu launch; complete companies retain their requested page.

## 2. Classification
Tier 3 HR/payroll; navigation and input translation only.

## 3. Ownership
Kuubu owns identity, enrollment, company/team grants. HR owns configuration and employee data.

## 4. Actors
Existing authorized HR administrators and HR operations users.

## 5. Entry
Existing Kuubu company/module selection and verified ERP session.

## 6. Authority
No grant changes. Existing API and scope middleware remain authoritative.

## 7. Company boundary
Use current server setup state and explicit non-confined shell result. Unknown scope never triggers setup.

## 8. State machine
Pending -> setup route; complete -> original destination. Loading/unknown -> no inferred completion or write. Pending SaaS wizard offers Back to Kuubu account to avoid a dashboard redirect loop.

## 9. Routing
Replace navigation prevents Back looping into the intercepted destination.

## 10. Legacy
Existing on-premise pending behavior retained; legacy complete state remains complete.

## 11. Branch users
Confined users and SaaS employees cannot be forced into organization-wide setup.

## 12. Configuration
Existing wizard retains employment, calendar, leave, statutory/pay-group rules and employee readiness steps.

## 13. Persistence
No schema, database, setup-status or entitlement mutation in this release.

## 14. Completion
Server Finish transition controls completion; revisiting settings does not restart completed companies.

## 15. Import problem
UI employment labels differed from workers import API categories.

## 16. Import mapping
Permanent/Employee -> employee; Contractor/Contingent -> contingent; Intern/Volunteer -> canonical codes, case-insensitive.

## 17. Contract semantics
Worker category differs from contract terms. UI explicitly states terms are configured separately; no permanent/fixed-term contract inferred or written.

## 18. Unknown input
Unknown and ambiguous types remain unchanged for server preview rejection; no blanket employee default.

## 19. Preview/apply
Normalize shared mapped rows before both preview and apply, including manually edited values.

## 20. User feedback
Show translation explanation and normalized preview before confirmation.

## 21. Profile menu
Add Back to Kuubu account and Log out of ERP in SaaS menus, reusing existing CSRF-protected logout. Central identity remains signed in.

## 22. Privacy
Use synthetic preview records only. Do not apply user spreadsheet or expose employee data in release evidence.

## 23. Failure recovery
Unknown/denied setup does not redirect; existing API error feedback and company reconnection retained.

## 24. Tests
Decision tests cover pending/complete, employee, confined/unknown scope, legacy and HR operations. Mapping tests cover known/unknown labels.

## 25. Build
Real API, ERP product mode, full HR enabled; TypeScript and production build required.

## 26. Deployment
Fresh immutable web publication and container; preserve native API/gateway routes and proxy mounts. No backend migration.

## 27. Rollback
Retain preceding web/proxy; restart preceding proxy if cutover health fails.

## 28. Acceptance
Live Test A pending entry and wizard through payroll/finish passed; completed A retained import route. Synthetic Permanent row preview: willCreate1/problems0, not applied. Test B Kuubu launch auto-opened setup; final release setup return to Companies passed. Levant server status remains complete. Four focused tests, TypeScript and production build passed. Source56da1e49, web28978/proxy first-entry03, backup11. No real employee import or pay run. Proof: inspection/qualifications/hrm-first-entry-production01 and03. Logout action and George native attendance remain separate outstanding checks.


## Import validation follow-up â€” 17 November
User still observed raw Permanent rejection from the API. Earlier web normalization
did not make the server contract tolerant of old open clients. Backend preview and
apply now share case-insensitive known-label normalization and persist canonical
worker categories; unknown categories never silently default to employee. Server
schema metadata lists accepted labels. No enum entries or contract policies are
auto-created from arbitrary input.

Important-field audit: existing required names/email/phone/type, email/phone/TPIN,
start date, child-history dates, in-file/existing identity conflicts, department,
pay-group/salary-effective-date/allowance/overtime checks remain. Follow-up adds
server name-text/NRC format checks, existing branch GUID/name resolution with
company/confinement guards, branch update/fill-missing handling and in-file NAPSA
duplicate protection. Branch/dept matches ignore case and surrounding whitespace;
unknown/ambiguous branch names require correction, never creation. Contract terms
and identity enrollment remain separate. Test raw labels through preview -> apply
-> persisted worker category, invalid important fields, duplicate NAPSA, branch
name/ID and scoped denial. No real employee spreadsheet is applied by this work.

Final follow-up collects required/basic format errors together per row, rejects
archived/unknown/ambiguous branch references and preserves meaningful spreadsheet
input. Browser cleanup no longer removes minus signs or identifier letters to turn
invalid data into valid data. Standard phone separators/clearly grouped numbers
normalize; malformed values remain visible to validation. Alphanumeric NAPSA/NHIMA
identifiers remain compatible with the existing server schema. No arbitrary enum,
contract policy, branch or identity is provisioned by this validation feature.

Release acceptance: API d41303dd on28986; web1bee5302 on28979, first-entry04
proxy; backup13. Publish18 suite192pass/3 opt-in PostgreSQL skips, HTTP import
handlers3pass with isolated SQLite persistence, frontend5pass/TypeScript/build.
Current runtime App DLL matches publish18. Protected import/mobile routes401;
setup/import/Procurement/readiness200. No production employee sheet applied.
Browser automation unavailable for this follow-up; no new authenticated production
browser claim. No migration/identity/grant change. Unknown values remain errors;
Check missing data retains reviewed department/grade creation. This does not claim
a whole-import transactional/replay redesign. Full operational evidence is recorded
in the latest MANUS_HANDOFF entry and inspection/qualifications release folders.


## First-payroll audit corrections — 20 November 2026

The two synthetic QA HR Manual/Import 20261118 companies reached calculated November runs with one worker each (gross ZMW5,000, employee deductions300, net4,700); neither was approved, released or paid. The user subsequently authorized correcting the audit defects. These changes remain UI-only and consume the existing scoped HR APIs; no schema, statutory rules, grants, identities or billing records change.

Manual employee and guided-step drafts live only in the current AppProvider memory, scoped by authenticated subject and company, and reset on reload/company change/logout or success. Department detours preserve input within that session. An acknowledged worker is reused after assignment failure; an unconfirmed create is blocked pending directory reconciliation. Existing different assignment terms stop retry rather than silently overwrite. Completed flows clear their step. Product first-time setup now directs to full employee forms/import, salary assignment and payroll eligibility rather than the separate minimal employee grid. Legacy setup remains available.

Employee profiles show contract and salary prerequisites from server records and explicit follow-up actions. Import classifications never imply a permanent contract; an authorized explicit edit may create the missing assignment using the employee's saved company, department, location and start date. Fixed-term end dates are required and validated. Existing assignment edits remain separate authoritative writes. Read failures show unavailable readiness rather than a fabricated completion. Statutory registration and bank requirements are described at their actual release/payment-file stages.

Live payroll entry uses real employer/group labels and no fabricated salary estimate. The employer selector follows the company selected in the shell. Detail projections use the scoped legal-entity directory; an unavailable match is labeled unavailable. Statutory employee deductions exclude employer contributions; gross/net engine results are untouched. Kuubu light-shell semantic status colors override inherited dark-mode status colors as complete text/background pairs, and employee-name buttons explicitly use foreground text. Shared components, tenant font and layout remain.

Validation: TypeScript noEmit passed; actual React forms/shared providers exercised with synthetic HTTP/router passed seven browser checks (department detour, storage privacy, partial-create retry, salary CTA, company reset, first payroll step, real-name/no-fabricated-estimate review). Retained evidence: inspection/qualifications/hr-journey-fixes-ui04; earlier attempts retained. Production build and authenticated live acceptance are recorded separately at release; these isolated checks do not claim production persistence. Rollback retains the current proxy and web, replacing only the web upstream and preserving API/native gateway, Procurement, storage and certificate mounts. No on-premise deployment is included.


### Published correction acceptance — 20 November 2026

Functional source 53ac66da2dc5effb7832ba674cd1bdc146a6f145 (prerequisites126d7e07,47113705) is pushed on codex/kuubu-product-consumer-20261107 and published in immutable hr-journey-fixes-publication03; public SSR29005/proxy kuubu-hr-journey-fixes03-proxy01. The initial visibility release was followed by two retained successors: legal-entity envelope handling and shared table color aliases resolved inside the product scope. The latter fixes direct --color-* consumers that inherited root dark colors despite light Kuubu surfaces. Actual compiled-CSS browser checks cover header/cell/action/status/hover; authenticated live table computed styles now resolve product foreground rgb(0,75,59), header rgb(244,247,239), muted rgb(84,103,92). Do not qualify earlier screenshots as the final color fix.

Seven form checks passed again with the actual wrapped legal-entity response, final TypeScript passed, and production build04 passed. Live George manual company opens via Kuubu (one uncertain selection recovered using its offered retry), completed setup is not restarted, and new payroll starts at period step1 with the actual employer/group. Imported test employee contract was explicitly selected and persisted through Edit details; its existing local-format synthetic phone required correction to the form's international format. Read-only database proof fixes-contract01 shows one imported contract, unchanged worker/run counts and both calculated runs gross5000/deductions300/net4700. No release, payment-file generation, payment or statutory submission.

The maintained website source remains apps/kuubu-account-hub (outside this Git checkout). Vercel production dpl_J9ossGTjxZnvxYbK5ww63eNWXWDA serves www.kuubuerp.com; immutable reviewed source manifests are inspection/qualifications/hr-journey-website-deployment-02.66 account tests passed/2 integration tests skipped; connected build passed. Trial continuation queries the authorized company directory by enrollment ID and opens existing workspaces rather than requesting a second company. Subscription cards load company bindings and omit continued setup for known-bound trials. Owner/team guidance no longer makes team grants a prerequisite for the owner. This website upload does not imply a GitHub source push; the configured GitHub account cannot resolve Greenwebb/kuubuerp-website. One incomplete source upload failed before activation and is retained as dpl_6Bd8D6ApZ3Bq5DNDTgA3qk1RYCgQ.

Backend f081d5a3/API28999, native gateway/Procurement routes, all database/document/session mounts and on-premise instances are unchanged. Backup configuration21 follows the new proxy; timer active, both public health routes200. Previous proxies/web outputs retained; obsolete web containers stopped. Final server headroom324MiB remains a separate capacity limitation. Runtime source manifests, cutover receipts, operations and browser evidence live under inspection/qualifications/hr-journey-fixes-production01 through03; failed attempts retained.
