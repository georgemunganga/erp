# Company-scoped HR employee numbers

## 1. Identification
Tier 3, 13 November 2026; child of the existing Kuubu HRM product workspace.

## 2. Purpose
Allow each company to start its own employee sequence without a different
company's EMP-0001 preventing employee creation.

## 3. Scope
Worker uniqueness index only. Existing identifiers and employee numbers stay unchanged.

## 4. Ownership
HRM owns workers; Core owns the selected company/tenant and grants. No login,
enrollment, billing subscription or team membership is created by adding a worker.

## 5. Actors
Existing authorized HR administrators and worker services; no additional grants.

## 6. Preconditions
The selected company must already pass product identity, license, module and
location checks. This change does not relax those checks.

## 7. User action
The existing Add employee form requests an automatically assigned number or
supplies a number, then saves the employee through the existing worker API.

## 8. Contract
The number is unique within tenant_id, including archived workers. It is not a
global identifier. The worker UUID remains the stable record identifier.

## 9. Data
Replace global IX_workers_employee_no with unique
ix_workers_tenant_id_employee_no over tenant_id and employee_no.

## 10. States
No worker lifecycle transition changes. A rejected duplicate remains unsaved.

## 11. Rules
Different tenants may reuse EMP-0001. The same tenant may not reuse it. Query
filters and product write guards remain mandatory. An explicit duplicate receives
employee-number-exists validation before persistence; the database remains the
authoritative concurrent uniqueness constraint.

## 12. Permissions
Existing hr_admin, explicit HR actions and location scope are unchanged.

## 13. API
Existing worker endpoints and payloads are reused; no new endpoint or DTO.

## 14. UI
Existing employee wizard and errors are reused. No alternate employee editor.

## 15. Integration
Company selection stays in Kuubu. Employee numbers confer no identity access.

## 16. Audit
Existing worker write audit remains in force. Migration evidence records index
definitions and row counts, without employee personal information.

## 17. Failure
Same-company conflicts fail the database constraint. A migration failure rolls
back transactionally; no employee is renumbered or removed.

## 18. Concurrency
The database unique index is authoritative; existing product company write
serialization remains in force for automatic sequence generation.

## 19. Migration
20261113001500_CompanyScopedEmployeeNumbers creates the replacement index before
dropping the global index. The target model is authored from the updated snapshot.
Generate and inspect the EF idempotent delta before applying it.

## 20. Recovery
Take a fresh protected backup and rehearse the delta on an isolated restored
database. Prefer application rollback retaining the forward-compatible index.
Down deliberately fails if legitimate cross-company duplicate numbers exist.

## 21. Security
No cross-tenant joins, header authority, email matching or default tenant fallback
is introduced. Never publish credentials or employee payroll records in evidence.

## 22. Payroll boundary
Payroll remains a separate calculated/reviewed/released workflow. Synthetic test
employees and salary calculations belong only in isolated qualification databases.

## 23. Company policy
Company hours, leave entitlement, contracts and payday require confirmed policy.
Statutory setup does not certify employer-specific obligations or registrations.

## 24. Regression
Sixty focused worker, workspace and payroll checks passed. A relational test
verifies reuse across two tenants, filtered reads, and rejection
of a duplicate inside one tenant. The EF idempotent delta applied twice on an
isolated production restore, preserved worker counts, accepted cross-tenant reuse,
and rejected same-tenant reuse. Probe rows were rolled back.

## 25. Acceptance
Isolated native BFF/PKCE workflow passed: two employees with company/location
assignments and salary profiles; November leave accrual; two-day paid leave
approval; payroll preflight, input locking and calculation. Monthly gross 15,000
and 50,000 produced net 10,924 and 31,512.20 respectively, checked independently.
The higher case exercises the NAPSA ceiling. Payslip preview metadata is available;
this is not evidence of approved/released payslip PDFs or bank payment.
Insufficient leave, repeat monthly accrual and premature payroll release are
rejected without adding employees, accruing twice or creating a payment.

The 2026 payroll inputs were checked against primary sources:
[ZRA PAYE](https://www.zra.org.zm/tax-information/),
[NHIMA contribution guidance](https://www.nhima.co.zm/download/document/0e98ec61c02019110104d23eb0.pdf),
and [NAPSA's 2026 contribution notice](https://www.linkedin.com/posts/national-pension-scheme-authority_public-notice-revision-in-contribution-ceiling-activity-7417941633180090368-y4Dr).

## 26. Deployment
Use a fresh immutable API publication and image. Keep the current qualified web
publication and persistent keys, intents and HR documents. Record exact pins.

## 27. Holds
No real payroll payment, production test employees or invented registration IDs.
Live company policy and employer registrations remain separate setup inputs.
The current session exposed no Codex browser surface; the selected native Chrome
window was minimized and could not be acquired. No fresh visual acceptance is
claimed for this qualification. Native API proof does not replace that check.

## 28. Evidence
Local qualification receipts are retained under inspection/qualifications; update
MANUS_HANDOFF.md with published source, image, migration and live readback results.
