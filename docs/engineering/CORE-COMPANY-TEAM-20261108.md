# Core company membership and team access

## 1. Identification
Tier3 Core child feature; explicit company governance for hosted SaaS.
## 2. Purpose
Separate current company membership from historical allocation, billing authority,
identity authentication and module permissions.
## 3. Scope
Company membership persistence, atomic initial-owner membership and exact-identity
company governance reader. Invitation delivery/acceptance and operational session
admission are subsequent parts of this feature, not yet implemented.
## 4. Parent
Product identity, purchased workspace allocation and existing Core company/branch
masters. One maintained ERP supports hosted SaaS and dedicated/on-premise modes.
## 5. Actors
Verified enrolled Kuubu identity, explicitly appointed owner/administrator/member.
Billing payer owner/admin is not automatically a company member.
## 6. Entry
Explicit company creation establishes its first owner in the same transaction.
Internal reader consumes a verified issuer/subject/link and current license lease;
there is no public team endpoint or new authentication scheme in this increment.
## 7. Preconditions
Qualified company allocation and federated identity link, active account/company,
active explicit membership and licensed allocated workspace slot.
## 8. Workflow
Company create commits membership with the company/owner/link/allocation/audit/
outbox. Reader joins exact tenant/company/principal keys and fresh current state;
it returns governance capabilities only after checking the matching license.
## 9. States
Active/inactive/archived membership; owner/administrator/member roles. Binding
identifiers cannot be retargeted or deleted by ordinary ORM changes.
## 10. Permissions
Owner may manage team/owners; administrator may manage team but not owners;
member has neither capability. These are not payroll/procurement/module roles.
No result creates a session, assigns a branch or authenticates a local user.
## 11. Rules
Require exact reviewed issuer, canonical numeric subject, active exact identity
link, federated active/unlocked principal, explicit same-company membership,
active allocated company and current matching enrollment/slot capacity. No email,
domain, customer membership or historical owner receipt substitutes for membership.
## 12. Data
CoreCompanyMembership: ID, tenant, company, local principal, bounded role, active
and archive flags, existing creation/update provenance. Unique tenant/company/user;
same-tenant company and principal FKs; database role check. No credentials or
provider bearer. Historical allocation remains immutable.
## 13. UI
Existing own Kuubu login/setup retained. Team UI must consume authoritative Core
commands; no fake successful invitations or browser-local team persistence.
## 14. UX
Later invitation UI requires explicit recipient, role/scope and confirmation.
Acceptance must require verified identity, explicit product enrollment and consent;
matching an email/domain alone cannot grant company access.
## 15. Notifications
No email or external delivery in this increment. Later invitation creation must
commit before delivery, use bounded expiry/revocation and avoid token logging.
## 16. Documents
No personnel/payroll or financial documents in company governance decisions.
## 17. Errors
Missing/inactive/foreign/expired/malformed proof returns no decision. Database
errors must not become access grants. Caller handles unavailable separately.
## 18. Integration
Reuse Core legal entity, federated principal/link and purchased-slot allocation.
The reader is not registered/exposed as an operational admission shortcut. Caller
must establish verified product identity; raw browser issuer/subject are no proof.
## 19. Audit/privacy
Allocation audit/outbox add membership ID. No raw credentials or email lookup.
Future membership changes require explicit audited commands and concurrency guards.
## 20. Configuration
SaaS creation retains existing opt-in identity/setup/licensing/provisioning flags.
No global behavior switch is introduced for classic installations.
## 21. Security
Test mismatched subject/link/company/enrollment/slot, account/link/member/company
revocation, stale historical ownership and cross-tenant FKs. Team mutations still
need last-owner protection, concurrency and bounded delegation before exposure.
## 22. Operations
New schema must be qualified through genuine PostgreSQL migrations and finite
runtime grants before deployment. Preserve existing release holds and Perfex.
## 23. Postconditions
Successful explicit creation adds exactly one owner membership and no operational
session/module roles. Retry returns existing records without adding membership.
## 24. Recovery
All allocation failures roll back membership too. Migration preserves historical
allocation receipts and may seed only their explicit original owner; it must not
infer grants from email, local admin roles or commercial membership. Populated
membership rollback must not silently discard access records.
## 25. Acceptance
Identity, payer, current company membership and module authorization remain
separate. Classic local-auth deployments continue with SaaS flags disabled;
local login must never bypass shared SaaS tenant admission.
## 26. Tests
Company create/retry/rollback; bounded governance roles; revoked/foreign proof and
license denial; same-tenant/unique/check constraints; genuine migration/backfill/
rollback and model drift; full ERP regression and later browser team journeys.
## 27. Release gates
Native schema qualification, full regression, audited invitation/accept/revoke/
role-change commands, last-owner/concurrency guards, team UI, licensed sessions,
deployed acceptance and dedicated/on-premise install/upgrade/restore qualification.
## 28. Evidence
Local Windows SDK10.0.403 build passed. The focused identity/federation/workspace/
company/local-auth cohort passed238 tests, zero failures, with nine opt-in skips:
six PostgreSQL allocation/membership cases and three actual provider/browser
cases. EF10.0.11 reported no pending model changes. Earlier local increments
passed38/4 skips and237/9 skips; logs are retained. New company access tests use
real SQLite, not a fabricated repository response. Native migration/backfill/
immutability/rollback tests are implemented but have not run for this source.

This source changes the allocation transaction/schema after the previously
qualified runtimea8134a9a. Its older full regression and actual browser receipts
must not be presented as qualifying this new increment. Genuine PostgreSQL,
full Linux regression and refreshed actual integration checks remain required.
No production migration, privilege, runtime/image, feature flag or Perfex change.
