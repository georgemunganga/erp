# Kuubu HR setup, payday and continuity

## 1. Identification
Tier 3; 13 November 2026. Child of the existing product HR workspace and payroll.

## 2. Purpose
Make first-time HR configuration explicit for Core-provisioned companies and
retain open form state during a temporary access-check outage.

## 3. Scope
Existing HR setup wizard, monthly payday rule, original HR forms and product
session adapter. No new identity provider, module deployment or billing system.

## 4. Ownership
Laravel owns identity, enrollment and subscriptions; Core owns company/team and
location access; HR owns employment configuration, employees, leave and payroll.

## 5. Actors
The existing HR administrator with explicit HR actions and complete coverage of
the selected company's locations. No additional grants or employee login links.

## 6. Entry
Kuubu Companies -> company -> HRM. First-time HR setup is linked from HR Home
and Configuration and remains reachable at /hrm/setup after completion.

## 7. Setup model
An absent HR setup row is pending for a Core-provisioned workspace. Existing
standalone legacy behavior remains unchanged. Reads do not create setup records.

## 8. Core prerequisites
Verified company/location setup and team ownership satisfy the Core steps.
HR setup cannot recreate organisation, locations or role grants through those steps.

## 9. HR prerequisites
Employment, leave and payroll confirmation are required for configuration finish.
Working time and policies remain editable; employee entry is a separate next action,
so configuration can finish without creating fictitious employees.
Employment can be reviewed without inventing predefined grades or position titles;
the explicit wizard action saves empty lists and individual employee roles remain separate.

## 10. Records
Existing setup states/step payloads remain authoritative. PayGroup gains payday_rule
with calendar-day as the backward-compatible default. No database per person.

## 11. Payday
Monthly last-working-day searches backward within the selected month, excluding
configured weekend days and public holidays. Calendar-day clamps to month end.
An invalid rule, nonmonthly use or calendar with no working date is rejected.

## 12. Period boundary
New periods receive concrete pay/cutoff dates. Existing periods and calculated runs
retain their dates when future group settings change; no retroactive payroll rewrite.

## 13. Calendar
The working-time step creates a default company calendar when absent. Holidays
come from configured business data; this change does not invent holiday declarations.
The wizard no longer preselects an inaccurate fixed annual holiday template.
Movable holidays and additional declarations require year-specific configuration;
see the [Zambian Public Holidays Act](https://www.parliament.gov.zm/sites/default/files/documents/acts/Public%20Holidays%20Act.pdf).
The background accrual worker discovers only active Core-registered allocations,
then uses a separately filtered/audited context and transaction for each company.
HTTP tenant headers and browser roles cannot select the job's tenant. Empty eligible
populations do not create an immutable monthly run; completed runs remain unique.

## 14. Payroll rules
The UI statutory summary matches the existing qualified 2026 setup engine. Company
registrations and worker statutory/payment details remain real-input prerequisites.

## 15. Continuity evidence
Public logs showed a503 workspace-context response followed by successful checks;
the old UI presented that outage as lost company access and cleared form state.

## 16. Failure classification
429/5xx/network failures pause access checks and offer bounded automatic read
retries.401,403 and409 require actual reconnection, permission review or company
selection. A temporary outage is never treated as successful authorization.

## 17. Form preservation
React Activity retains mounted form state while hiding the unverified workflow and
stopping effects. The memory-only request-header capability is cleared on failure
and restored only after a fresh verified context passes. Backgrounding a tab is
not logout; every business request still reauthorizes on the server. A missing
memory context stops a save before submission and requests a fresh check. Recovery
uses a higher modal layer so existing portaled dialogs cannot remain interactive.

## 18. Concurrency
Observe/refresh operational continuity before company context to avoid competing
with the same browser credential's rotation lock. Server locks/fences and quotas
are unchanged; no uncertain token refresh is replayed or protection weakened.

## 19. Scope changes
The existing exact context marker still rejects a company switch in another tab.
Successful rechecks refresh role/location presentation even when marker is unchanged.
Hard denial discards the old selected workspace rather than preserving authority.

## 20. API
Existing setup/configuration/pay-group APIs are reused. Optional paydayRule is
added to their current DTOs; existing callers default to calendar-day.

## 21. UI
Reuse shared buttons, cards, selects, wizard and navigation. Separate reconnect
copy from expired-session/changed-access copy. The user does not repeat ERP login
for a503 that recovers. No browser storage holds credentials or grants.

## 22. Authorization
All HR reads/writes still pass current identity, license, Core grant, tenant and
location checks. No UI cache or company email/domain grants data access.

## 23. Migration
20261113050000_PayGroupPaydayRule adds one nonnull text column, default calendar-day.
Its target model is authored from the updated snapshot; generate/inspect EF delta,
rehearse twice on an isolated restore and preserve a fresh protected backup.

## 24. Audit and recovery
Existing setup, HR write and payroll state audit remain in force. Immutable API/web
publications, keyring, sessions, intents and HR documents are retained. Rollback
restores a paired consumer/proxy; keeping the additive column is code-compatible.

## 25. Verification
Focused relational setup and payday cases cover absent state, Core ownership,
finish without employees, short/leap months, weekends, configured holidays and
invalid rules. Session-adapter tests cover recovery, cleared request headers and
hard401/403/409. Browser proof and release pins follow in the maintained handoff.

## 26. Business setup
Authorized policy: ZMW monthly, Monday-Friday8h/day, General at Head office,
permanent/fixed-term contract types, annual leave24days accruing2/month,
last working day payday. Apply only to the selected company through existing APIs.

## 27. Acceptance boundary
Use synthetic employees and payroll only in the isolated fixture. Never issue real
salary payments, invent registrations or auto-link employees to login identities.
Company configuration completion is separate from payroll release readiness.

## 28. Evidence and release
Record exact published source/image/web pins, migration hashes, backup, live company
readback, private employee/leave/payroll proof, continuity recovery and browser
entry in MANUS_HANDOFF.md. Do not claim unobserved behavior as passed.
