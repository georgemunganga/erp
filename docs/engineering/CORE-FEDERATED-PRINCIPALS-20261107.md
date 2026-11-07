# Federated Core principals

Source implementation; production migration and operational admission remain gated.

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
Pre-implementation discovery: LocalUser required Email, NormalizedEmail and
PasswordHash. LocalAuthenticationHandler emitted email claims unconditionally;
local password routes read/wrote those fields. ExternalIdentityLink already has
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
Local mode requires nonblank email, normalized email and credential record;
actual password verification still validates PBKDF2. Federated mode has null
login credentials and no password-change duty.
Do not reuse sentinel hashes, invented addresses, empty strings or email/domain
matching to join identities. Contact information is separate from login authority.
## 12. Data Fields & Ownership
Core owns credential mode, local credentials, display name and operational
principal. Provider owns immutable global issuer/subject and account recovery.
An optional shared contact/profile requires a separate explicit consent contract;
it is not a prerequisite for recognizing the federated principal.
## 13. Screens & Data Views
Account APIs expose credential mode and per-principal password capabilities,
not hashes. Clients must intersect them with deployment AuthCapabilities. Existing
local account screens remain supported. Before admission is enabled, federated
screens must direct recovery to the own Kuubu account flow and omit local password
setup/reset; the connected federated account UI is not implemented in this step.
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
Additive migration; PostgreSQL and SQLite credential-state check parity; no global email
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
The public product deployment must also disable anonymous local credential entry
and reject local-session admission into commercially bound tenants, regardless
of account mode. Local admin-create/reset routes must not become a SaaS licensing
bypass. Those product deployment/session policies are not implemented by merely
adding the credential discriminator; standalone local flows remain supported.
## 28. Delivery Evidence & Handover
Discovery sources: Domain/Entities/LocalIdentity.cs; API/LocalIdentity.cs;
Infrastructure/Data/HrmDbContext.cs local user/index/link mapping;
API/CoreWorkspaceContextRoutes.cs legacy empty-assignment fallback; provider
ProductSubjectController and ProductCommercialAccess. This document records the
implementation contract. This prerequisite is not a completed operational
admission flow.

Exact tested implementation `2c6f9085e029138ebdb0eb311c49a98ae81b9741`;
source archive SHA256
`78776212da87a436989cc87d8d253a8f5a7255826c67c78e3849b3de4bcd00ac`.
All122 focused HTTP/identity/session/workspace/licensing/constraint cases passed,
then28 genuine PostgreSQL scenarios (13 new principal constraints/upgrade cases,
five existing identity-link cases and ten existing tenant-session cases). The
complete HRM regression passed2,093 tests, zero failures/zero skips, in7m37s.

The actual migration `20261107194704_CoreFederatedPrincipals` keeps existing rows
local by default, permits absent credentials only in federated mode and adds a
credential-state check and immutable-mode PostgreSQL trigger. The upgrade case
inserts an account before the migration and verifies its original email, hash,
password verification and roles afterward. Populated rollback refuses to drop
federated principals. The generated Down's empty credential defaults were removed;
it never supplies invented credentials to force an old schema onto these users.
The EF-generated designer/snapshot changed only the expected local-user metadata.

HTTP tests map the real existing routes and handler with synthetic route authority
for authorized administrator/self-service cases. Both auth route aliases reject
federated password setup/reset/change, credential email and login-email edits;
even a preexisting credential link cannot be redeemed. A local session row cannot
admit a federated principal or update its activity. Existing local sign-in and
the established regression tests remain. The existing link test now identifies
its owning migration by name instead of assuming it is always the newest one;
all FK, raw binding, populated rollback and retained-history assertions remain.
No test was removed or weakened to make a credential bypass pass.

Retained native gate `/opt/kuubu-builds/federated-principals-2c6f9085-02`, launcher
`kuubu-federated-principals02.service`, SDK container
`kuubu-federated-principals-gate02` exited0/OOMKilled=false. SDK10.0.403 pinned
digest `sha256:212fe4cb74627d5adf9817a4088906ae3edff1bd853bb27565d4b46731efe61e`.
Only the dedicated native filter receives the synthetic control connection; the
full/focused jobs use their ordinary SQLite/InMemory fixtures. The internal
synthetic PostgreSQL container was returned to stopped. No production DB used.

Log SHA256:
- focused: `54e0df78ab9f3b30a4ee53f6dffe6badadbdd96170af5251590c65cdefa58493`
- native: `f85755de034cfb68db6e1eada4a8d9199aa5baa6d52602766df72ddbb42af7c7`
- full: `66d398cfb7e49101e4511072531faf7a74717619068a7d268badd5530271d60a`

An independent read-only metadata audit found the migration, credential check,
mode trigger and two-column identity FK in all13 new `federated_principal_*`
databases. They retain one local and five federated synthetic principals; every
federated email/normalized-email/password field is null, no password-change duty.
Receipt `/opt/kuubu-builds/federated-principals-native-metadata-01/receipt.json`.
The audit read only schema metadata and aggregate counts, no credential values.
Earlier sourcee06aa668 passed121 focused checks before EF scaffolding. Every
attempt is retained; subsequent work used exclusive directories and independent
plain cache copies from stopped workspaces, not hardlinks or historical edits.

Reproduce normal checks from the repo root with the .NET10 SDK:
```sh
dotnet test backend/hrm-api/src/Mightyfin.Erp.Hrm.Tests --filter 'FullyQualifiedName~FederatedPrincipal'
dotnet test backend/hrm-api/src/Mightyfin.Erp.Hrm.Tests
```
For genuine PostgreSQL, privately provide KUUBU_SESSION_TEST_POSTGRES pointing
only to a dedicated `kuubu_session_test_control` DB, with permission to create
new synthetic DBs. With that private variable set, run:
```sh
dotnet test backend/hrm-api/src/Mightyfin.Erp.Hrm.Tests --filter 'FullyQualifiedName~FederatedPrincipalConstraintTests|FullyQualifiedName~ProductIdentityLinkConstraintTests|FullyQualifiedName~LocalSessionTenantHttpTests'
```
Never supply a production connection.
The tests validate the exact control DB name, create uniquely named databases,
apply genuine migrations and retain their evidence. Do not print the connection.

Source published in stacked draftPR201, dependent onPR200/PR199. No production
migration, client/flag, image or ingress change. Private ERP readiness remained200
on image3ff07382. Public main7b7e3fc7 and qualified release6b47bbeb were re-read
and remain unchanged. Actual Laravel↔C# interoperability, federated UI, licensed
workspace admission and the section27 release gates still remain.
