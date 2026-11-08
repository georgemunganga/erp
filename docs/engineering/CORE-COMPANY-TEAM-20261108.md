# Core company membership and team access

## 1. Identification
Tier3 Core child feature; explicit company governance for hosted SaaS.
## 2. Purpose
Separate current company membership from historical allocation, billing authority,
identity authentication and module permissions.
## 3. Scope
Company membership persistence, atomic initial-owner membership, exact-identity
governance reader and confirmed role-change/revocation commands. Commands remain
internal and are under qualification. Invitation delivery/acceptance, public team
API/UI and operational admission remain subsequent work.
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
Role/revoke commands resolve scope from verified identity, take key-then-company
transaction locks on PostgreSQL, recheck current action authority, compare the
expected membership revision and commit receipt/audit/outbox with the mutation.
## 9. States
Active/inactive/archived membership; owner/administrator/member roles. Binding
identifiers cannot be retargeted or deleted by ordinary ORM changes.
## 10. Permissions
Owner may manage team/owners; administrator may manage team but not owners;
member has neither capability. These are not payroll/procurement/module roles.
No result creates a session, assigns a branch or authenticates a local user.
Role changes are owner-only. Administrators may revoke ordinary members, not
owners or fellow administrators. Replay requires the current action permission,
not merely possession of a successful old command key.
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
Membership revision is a positive optimistic concurrency token. Immutable team
command receipts bind verified issuer/subject/key globally and retain exact
company/member/actor, request hash and historical role/active/revision outcome.
The receipt's composite member FK also binds its company, not just its tenant.
## 13. UI
Existing own Kuubu login/setup retained. Team UI must consume authoritative Core
commands; no fake successful invitations or browser-local team persistence.
Inspected published websiteb01985ee: apps/account/pages/Team.jsx is a browser
prototype of billing delegates, not ERP company membership. API mode instead uses
ConnectedAccount, whose team section is not connected to mutation commands. Keep
billing delegation separate from company governance and module data access.
Existing Core settings routes reuse UsersConfiguration/UserDetail for local and
directory-backed accounts. Reuse their shared shell/controls where appropriate,
but do not send SaaS invitations through local password-account creation or treat
commercial/team preview state as a persisted Core grant.
## 14. UX
Later invitation UI requires explicit recipient, role/scope and confirmation.
Acceptance must require verified identity, explicit product enrollment and consent;
matching an email/domain alone cannot grant company access.
## 15. Notifications
No email or external delivery in this increment. Later invitation creation must
commit before delivery, use bounded expiry/revocation and avoid token logging.
Role/revoke changes emit core.company.member.access.changed.v1 transactionally;
no-op commands reserve their receipt without emitting a false change event.
## 16. Documents
No personnel/payroll or financial documents in company governance decisions.
## 17. Errors
Missing/inactive/foreign/expired/malformed proof returns no decision. Database
errors must not become access grants. Caller handles unavailable separately.
Commands distinguish invalid, denied, revision/key conflict, last-owner rejection,
changed/unchanged and replayed outcomes. Replaying a receipt is an acknowledgement
of a historical command, never proof of the member's current access. Inactive
membership cannot be reactivated through role change; it needs a separate
consented invitation flow.
## 18. Integration
Reuse Core legal entity, federated principal/link and purchased-slot allocation.
The reader is not registered/exposed as an operational admission shortcut. Caller
must establish verified product identity; raw browser issuer/subject are no proof.
## 19. Audit/privacy
Allocation audit/outbox add membership ID. No raw credentials or email lookup.
Future membership changes require explicit audited commands and concurrency guards.
Role/revoke commands commit membership, immutable receipt, audit and outbox in
one transaction. Post-save expiry/cancellation/failure rolls back every record.
## 20. Configuration
SaaS creation retains existing opt-in identity/setup/licensing/provisioning flags.
No global behavior switch is introduced for classic installations.
## 21. Security
Test mismatched subject/link/company/enrollment/slot, account/link/member/company
revocation, stale historical ownership and cross-tenant FKs. Role/revoke commands
protect the last locally usable owner: another active/unarchived owner membership
must have an active federated account, active issuer link and no current lock.
This checks known Core state, not the future availability of an external identity.
Native concurrency and public command authentication/CSRF remain exposure gates.
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
No-op receipts reserve their key so retry cannot undo a later change. Changed
payload/key reuse and stale revisions conflict. Receipt replay needs current
action authority and a current license; it does not reapply an old mutation.
The command migration refuses to drop populated receipts or changed revisions.
## 25. Acceptance
Identity, payer, current company membership and module authorization remain
separate. Classic local-auth deployments continue with SaaS flags disabled;
local login must never bypass shared SaaS tenant admission.
## 26. Tests
Company create/retry/rollback; bounded governance roles; revoked/foreign proof and
license denial; same-tenant/unique/check constraints; genuine migration/backfill/
rollback and model drift; full ERP regression and later browser team journeys.
## 27. Release gates
Native schema/allocation and full regression passed for49780a39. New role/revoke
schema/commands require fresh native/full qualification. Remaining: native
governance-reader/command execution, public verified-identity/origin/CSRF command
integration, invitation/acceptance delivery, team UI, licensed sessions,
deployed acceptance and dedicated/on-premise install/upgrade/restore qualification.
## 28. Evidence
### Role/revoke command increment — local qualification

Windows build passed;265 focused identity/federation/workspace/company/local-auth
tests passed, zero failures,13 opt-in skips (10 native, three actual provider/
browser). EF10.0.11 reports no pending model changes. Four new native cases cover
the real governance query, concurrent owner demotions, concurrent identical
retries, receipt scope/immutability and safe empty/populated migration rollback.
These native cases and new full regression have not run yet. No public command
endpoint or new operational session is enabled.

The first focused run retained58 failures/10 passes: company creation's new
composite membership key omitted tenant_id at insertion. A single diagnostic
case reproduced the NOT NULL failure. Creation now supplies the trusted tenant
before tracking, matching the established composite-principal pattern. The
repaired focused company cohort passed69; the wider cohort then passed265.
The stronger company/member receipt FK makes company part of an EF principal key;
the binding immutability test verifies its early key rejection and unchanged
stored binding. Failure evidence was retained, not excluded from the suite.

The older qualified source below is a separate checkpoint and does not qualify
the new receipt/revision schema or role/revoke workflow.

### Qualified membership allocation —8 November2026

Source `49780a39a13c89cd496f92283c1f20ba6effb3c2` passed Linux focused237
with six native skips, the89-case native qualification cohort with zero skips,
and full Linux regression2,208 passed/zero failures/nine opt-in skips in8m8s.
All nine opt-ins ran separately: six allocation/membership PostgreSQL cases in
the native cohort and three actual provider/browser cases on Windows. The native
cohort includes real PostgreSQL migration/allocation/concurrency/physical guards,
shared HTTP/store checks and24 SQLite company-governance cases; it does not mean
all89 cases execute PostgreSQL. The governance reader itself remains SQLite-tested
until its native query/command integration gate is exercised before exposure.

Actual Laravel helperb71b29fe and published Kuubu websiteb01985ee passed the
refreshed own-login/enrollment/consent/create/reload/retry browser case in39s,
protected setup protocol in32s and ordinary identity/licensing in14s. Browser
DNS was disabled; every redirect stayed inside the test transport. Browser SQL
verified exactly one company/membership/federated owner/exact link/audit/outbox,
zero credentials/sessions/module roles and clean FKs. Protocol SQL verified two
separate company tenants with two exact owner-membership-link pairs and zero
operational grants. Purchased capacity, retry, revoked payer authority, suspended
license and cancellation denial passed. Ordinary staff retained zero billing
memberships. This is isolated cross-application acceptance, not deployed ingress.

Independent read-only PostgreSQL audit covered21 synthetic allocation databases,
16 company memberships, exact tenant/company/owner/link pairing, two validated
same-tenant FKs, role check, scope uniqueness and enabled binding trigger in each
schema. New creates have audit/outbox records; two migration-backfilled owners
retain their original identity binding, including one inactive membership from a
revoked link. Membership-linked principals remain credential-free/module-role-free;
the intentional classic local-admin fixture gains no inferred company membership.
Classic multiple companies remain supported. Test runner and PostgreSQL stopped0,
OOMKilled=false; local fixture/SSR ports closed. Read-only Perfex sign-in and the
existing private ERP readiness endpoint returned200. No production changes.

Evidence roots: inspection/qualifications/company-membership-linux-49780a39-01,
company-membership-browser-49780a39-01 and company-membership-protocol-49780a39-01.
Remote source/results retained at /opt/kuubu-builds/company-membership-49780a39-01;
independent audit at /opt/kuubu-builds/company-membership-audit-49780a39-01.
Verified SHA256:

- Source archive `0b336f22a1e5146a858f6b799556359e567160ff6fe303934efe5d0795d6b9b9`.
- Linux focused `11c31d50930a1d03b5770626d7eda907489a987f61a97265c6f0c2a33e8edc55`.
- Native cohort `21adb53025c0194fe5e5830e938608ef422e1e0f8f507e8717311044a0fc17ef`.
- Full regression `b23517d020b82c442936fe3991fe12b0a65308783f8d5d79d9cfe90c9dc4f42f`.
- Native SQL audit `0bf20bf0a0f5a7a95e3af22c32ae099eb1c058c1ba54cb1834967b79418b5d32`.
- Browser log `fb63331a21f2fcc2cd861fc575819fa69d94956d341745dbb41434e893812d8c`.
- Protected protocol `6735fde9d46e3337aac8e18d5bfec8b642c4355b9aaa31340bc64547739e7e01`.
- Ordinary protocol `9a6ccaf6d3c2797a0435f19b1cd322e2180981fe1806784e7cc9a6faacaf3d9e`.

### Earlier local implementation checkpoint

Local Windows SDK10.0.403 build passed. The focused identity/federation/workspace/
company/local-auth cohort passed238 tests, zero failures, with nine opt-in skips:
six PostgreSQL allocation/membership cases and three actual provider/browser
cases. EF10.0.11 reported no pending model changes. Earlier local increments
passed38/4 skips and237/9 skips; logs are retained. New company access tests use
real SQLite, not a fabricated repository response. Native migration/backfill/
immutability/rollback tests were pending at that checkpoint; they passed in the
later cohort above.

This source changes the allocation transaction/schema after the previously
qualified runtimea8134a9a. Its older full regression and actual browser receipts
must not be presented as qualifying this new increment. Its new PostgreSQL,
full Linux regression and refreshed actual integration are recorded separately
above rather than borrowed from the older runtime's receipts.
No production migration, privilege, runtime/image, feature flag or Perfex change.

Published source `49780a39a13c89cd496f92283c1f20ba6effb3c2` is retained in
inspection/qualifications/company-membership-49780a39-01. Backend archive SHA256
`0b336f22a1e5146a858f6b799556359e567160ff6fe303934efe5d0795d6b9b9`;
focused log `42690b1df85bdfc308bb7369686e4d6ad5b504e3aa6014111ca55238d1595320`;
build log `070ce0d467040e0729a1fc97c34f8b4a3fc6ad8ce21b541b787622adc392495b`.
