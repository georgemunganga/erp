# Federated Core principals — next admission prerequisite

Design only; not implemented or enabled by the commercial licensing client.

## 1. Feature Identification
CORE-FEDERATED-PRINCIPALS, Tier3, Core engineering owner under GW-ENG-STD-001.
## 2. Purpose & Business Outcome
Represent an explicitly admitted person without inventing an email or a second
password account. Preserve existing standalone local accounts and deployments.
## 3. Scope & Exclusions
Explicit credential mode and nullable local credentials for federated principals;
database invariants, password-route denial and honest account capability output.
Workspace provisioning, invitations, product sessions and public enablement are
separate dependent features, not implicit effects of this schema preparation.
## 4. Parent & Related Features
Core external identity links, scoped local sessions, commercial licensing channel.
## 5. Actors & Participants
Existing local account holder; explicitly admitted federated principal; Core
administrator. Laravel remains the global identity/recovery authority.
## 6. Entry Points & Triggers
Existing local sign-in, reset, setup, change-password and administrator routes;
future explicit Core workspace/invitation command creates a federated principal.
## 7. Preconditions
Current authoritative code: LocalUser requires Email, NormalizedEmail and
PasswordHash. LocalAuthenticationHandler emits email claims unconditionally;
local password routes read/write those fields. ExternalIdentityLink already has
the exact tenant/user composite FK and immutable issuer/subject binding.
## 8. Workflow
Keep existing rows in local credential mode by additive default. A future
authorized provisioning transaction creates federated mode with no local email,
normalised login or password hash and an explicit display name. It then creates
the exact issuer/subject link and Core grants. Authentication never provisions.
## 9. Status / State Transitions
Credential mode is immutable through ordinary account edit/recovery routes.
Federated identity activation, suspension and session revocation follow explicit
Core commands. No password reset converts a federated principal to local mode.
## 10. Permissions & Data Scope
Modes do not confer roles. Tenant, company and branch grants are separate and
must be assigned explicitly. Empty branch assignments must not accidentally give
new federated employees the legacy organisation-wide fallback.
## 11. Business Rules
Local mode requires nonblank email, normalized email and valid local credential
record. Federated mode has null login credentials and no password-change duty.
Do not reuse sentinel hashes, invented addresses, empty strings or email/domain
matching to join identities. Contact information is separate from login authority.
## 12. Data Fields & Ownership
Core owns credential mode, local credentials, display name and operational
principal. Provider owns immutable global issuer/subject and account recovery.
An optional shared contact/profile requires a separate explicit consent contract;
it is not a prerequisite for recognizing the federated principal.
## 13. Screens & Data Views
Account APIs expose credential capabilities, not hashes. Existing local account
screens continue to work. Federated screens direct identity recovery to the own
Kuubu account flow and do not offer local password setup/reset.
## 14. User Experience / Human Behaviour
No fake successful reset/setup, no duplicate account creation and no silent
identity linking. Missing workspace grants remain an honest empty/denied state.
## 15. Notifications & Communications
Local credential mail only for eligible local principals. Team invitations are
explicit Core invitations, not Laravel billing memberships. Email invitation
acceptance needs both the specific invite and verified intended recipient proof;
a domain match or email lookup alone never establishes access.
## 16. Documents & Attachments
No changes to existing business-document permissions or retention.
## 17. Exceptions & Edge Cases
Blank credentials, unknown mode, direct SQL invalid state, null/missing contact,
same global subject in distinct tenants, duplicate links, inactive/archived user,
locked user, attempted password setup/reset on federated user, attempted mode
switch by patch/admin reset and legacy sessions after mode changes.
## 18. Integrations / APIs / Events / Sandbox
Subject resource currently returns only issuer/subject, verified/product/schema
and operational_access_granted=false. Preserve that privacy boundary. No required
profile claim exists today; do not assume email/name is already attested.
## 19. Audit / Security / Privacy
Audit explicit principal creation/state/grant changes with trusted actor/tenant.
Never audit raw credentials or provider tokens. Preserve existing binding evidence
and database rejection of reassignment/delete. Privileged mode conversion, if
ever supported, needs a separate reviewed security workflow and session revocation.
## 20. Reports / Dashboards / Metrics
Keep global identity, personal enrollment, payer enrollment and Core principal
counts separate. Authentication success must not be reported as ERP admission.
## 21. Configuration & Localization
No new public feature flag enables workspace access in this prerequisite. Local
mode remains the migration default; product identity/licensing flags stay off.
## 22. Non-Functional Requirements
Additive migration; PostgreSQL and SQLite constraint parity; no global email
uniqueness requirement for federated principals; preserve exact tenant/user FK.
## 23. Postconditions
Data model can represent a real federated principal without a local login.
No workspace, external identity link or session appears automatically.
## 24. Failure & Recovery
Migrate existing local rows without changing credentials. Refuse schema rollback
that would lose federated principals or require fabricated credentials; roll code
back while retaining evidence. Qualify the actual runtime role's finite ACLs.
## 25. Acceptance Criteria
Existing local flows unchanged; invalid modes/credential combinations rejected
in context and genuine PostgreSQL; federated principal cannot use or obtain a
local password credential/session via any existing recovery/admin route.
## 26. Test Scenarios
Existing local identity/session regression; new federated null-credential rows;
all password/reset/setup/edit routes including existing links; raw SQL constraints,
cross-tenant/identity FK, null claim handling, account DTOs, migration and rollback.
## 27. Dependencies & Release Gates
Still required: Core-owned company/payer binding and setup authority, invitation
acceptance, fresh PKCE link admission, encrypted server-side provider grants,
refresh/revocation, licensed operational session, complete company/branch/module
checks, public anonymous intake scope, private runtime ACLs and actual provider
interoperability. A billing member may read commercial access today; that read
alone must not be treated as authority to create or rebind an ERP company.
## 28. Delivery Evidence & Handover
Discovery sources: Domain/Entities/LocalIdentity.cs; API/LocalIdentity.cs;
Infrastructure/Data/HrmDbContext.cs local user/index/link mapping;
API/CoreWorkspaceContextRoutes.cs legacy empty-assignment fallback; provider
ProductSubjectController and ProductCommercialAccess. This document records the
next implementation contract, not successful tests or a completed admission flow.
