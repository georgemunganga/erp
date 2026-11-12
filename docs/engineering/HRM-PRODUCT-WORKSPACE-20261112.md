# Existing HRM workspace through Kuubu

## 1. Identification
Tier 3, 12 November 2026. Parent: product request authentication, selected ERP
session registry and HRM product worker scope. Candidate; deployment evidence is
recorded in MANUS_HANDOFF.md when promoted.

## 2. Purpose
Replace the isolated employee-directory entry with the existing HRM application,
navigation and real services for an explicitly authorized HR administrator.

## 3. Scope
Reuse current HRM routes, forms, release gates and services: people/lifecycle,
organisation, leave/time, requests/approvals, payroll, recruitment, performance,
offboarding, benefits, reports and HR configuration. Existing demo-only screens
remain held. Identity/access administration, integrations/security administration,
tenant creation and start-afresh reset are not admitted by this adapter.

## 4. Ownership
Laravel owns commercial identity/enrollment/licensing. Core owns company/team/
module/location authority. HRM owns workforce data and decisions. Creating a
worker never creates a login or grants access; email/domain matching is not used.

## 5. Actors
The selected Core person must hold hr_admin plus explicit hrm-access and
hrm-admin actions. Existing service permissions and segregation remain in force;
no payroll/Finance/identity role is fabricated.

## 6. Preconditions
Default-off ERP:ProductHrmWorkspace:Enabled and paired VITE_ERP_FULL_HRM=true,
existing product auth, current selected HRM session, parent identity/license,
Core membership and explicit grants. No new schema or database per user.

## 7. Compatibility boundary
Legacy company-wide operations are admitted only for a Core-provisioned tenant
containing exactly the selected company and only locations covered by the grant.
Historical/archived locations count. A partial grant never becomes global access.
An explicit company-wide grant can cover future owned locations; a finite grant
cannot create an uncovered location. Additional companies in the tenant deny.

## 8. Narrower grants
Previously qualified worker list/detail/create/update/archive retain their
separate product scope. They remain available through the bounded directory
when the full workspace gate is not satisfied. Other HRM roles/partial-location
workflows require their own resource qualification, not a blanket role mapping.

## 9. Entry
Kuubu Companies -> select company and HRM -> existing ERP session selection.
The existing /hrm/employees route renders the original employee page with the
HRM shell. Other HR routes reuse the same memory-only verified session adapter.

## 10. Authentication
No second ERP password/Keycloak session, default tenant header, stored bearer or
browser role picker establishes authority. Roles come from verified Core context.
Normal standalone/local authentication retains its existing provider and flags.

## 11. Requests
Only explicitly marked existing HRM route handlers/groups are admitted. Both
versioned and legacy paths use the same policy. Credential/team/role/branch-grant
routes and reset are blocked case-insensitively, including direct navigation.

## 12. Read scope
Existing tenant query filters derive the tenant from the private selected proof.
Compatibility coverage is checked before and after each handler. Worker/profile
filters retain the stronger product predicates before pagination/disclosure.

## 13. Location selection
The shell may narrow to an owned covered location. Stored preferences are checked
against verified current choices; they cannot choose a tenant/person or widen
authority. Shared company-wide legacy rules are used only after coverage proof.

## 14. Mutation boundary
Require exact application origin, protected operational CSRF and JSON/form data;
bound uploads to 16 MiB, retaining stricter service limits. Worker operations
retain their existing query/body rules and 64 KiB JSON boundary. Product DELETE
uses an explicit empty JSON body. Unsupported credentials/tenant selectors deny.

## 15. Transactions
Hold the existing shared company advisory fence during HRM execution, including
legacy GET handlers that initialize settings. Worker writes retain their existing
owning exclusive transaction fence without acquiring a second conflicting lock.

## 16. Commit
Every admitted HRM EF save requires a real transaction. Before its owning commit,
recheck current identity/license/Core authority and complete location coverage.
A save guard also checks persisted source ownership and both declared and logical employee/company/location references. Expiry, revocation, outage, unassigned workers outside finite scope and an uncovered new location abort/roll back. Sync
commits fail closed. No uncertain mutation is replayed automatically.

## 17. Business rules
Existing service validation, statutory settings, payroll calculations, approval
rules, documents and lifecycle state transitions are reused. Empty/new companies
receive honest empty states and prerequisites rather than browser-local samples.

## 18. Workforce identity
Worker SubjectId remains protected by product worker guards. Self-service needs
an explicit workforce identity link; no fabricated employee is created for the
workspace owner. Company/team access continues to be managed from Kuubu.

## 19. Setup
Keep the existing HR setup wizard available through navigation. Core company
setup is already explicit; unfinished payroll setup does not automatically lock
unrelated HR screens behind the wizard. Existing payroll prerequisites still apply.

## 20. Frontend lifecycle
Recheck on visibility/pageshow and periodically. Hide records on visibility loss
or failed authority. A stable expected context marker survives retries, preventing
silent rebinding of a mounted company's caches to a different selected company.

## 21. Branding
Reuse original shared UI controls, shell and layouts. Kuubu/Poppins and the hippo
mark remain the default; verified employer presentation is scoped to the current
company. Product mode does not restore another company's cached branding.

## 22. Failure recovery
Unavailable authority does not create a fallback identity or mock data. Return to
Kuubu or retry a read. Validation and permission failures use existing service
responses. The browser never automatically repeats a financial/HR mutation.

## 23. Audit
Existing entity audit participates in owning saves and request audit records
privileged outcomes. Never persist cookies, CSRF, proxy credentials or passwords
in evidence. Synthetic test data is separate from production HR records.

## 24. Deployment
Version API and SSR frontend together; no production migration is needed. Reuse
the source-verified unchanged dependency assemblies from the qualified runtime.
Retain partial failed builds and every publication independently, read-only.

## 25. Rollback
Retain previous API, web consumer and proxy configuration. Switch both upstreams
back together on failed readiness. Do not reverse migrations or restore over
customer data to roll back code. Existing Procurement and Perfex remain separate.

## 26. Automated acceptance
75 focused tests passed in the Linux fixture, including five native PostgreSQL
commit cases and legacy employee/Procurement regressions. The initial native test
fixture used a UUID where the domain requires a bounded numeric subject; corrected
fixture results are retained without changing the domain. Type checks and full
frontend/API builds passed. Browser/protocol evidence follows in the handoff.

## 27. Risks and limits
Compatibility is deliberately narrower than every possible HRM role/location
combination. Preserve source release holds. A module entry test is not payroll
financial acceptance, statutory validation, inbox delivery or a decision about a
real employee. Production browser verification must not add synthetic HR records.

## 28. Evidence and handoff
Infrastructure inspection/qualifications/full-hrm-build-06 and full-hrm-tests-03
retain the build and 75-case test receipts. Native/private browser and deployment
receipts, exact source pins and any remaining holds are recorded in the maintained
MANUS_HANDOFF.md checkpoint. Server build publications remain write-once.
