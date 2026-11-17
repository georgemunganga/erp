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


## Import validation follow-up — 17 November
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
