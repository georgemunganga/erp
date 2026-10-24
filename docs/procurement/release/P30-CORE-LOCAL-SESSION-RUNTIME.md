# P30 Core-owned local-session runtime — private restored cohort

## 1. Feature identity
CORE-LOCAL-RUNTIME-01, Tier3. SOURCE ONLY. Prerequisites: reviewed LOGIN PR70 at54d8b271 and published P30f282a28e, merged ordinarily in a separate branch. No production activation.

## 2. Problem
The existing private PrincipalApprovalHost uses the owner Hrm connection even when Procurement Record/Execution are finite. CoreRuntimeConnection changes pooling only. Existing current-identity projections cannot authenticate local accounts or persist sessions/audits.

## 3. Outcome
One explicitly selected private restored-auth cohort uses a distinct finite Core connection with the owning Core handlers, current permissions and optional worker metadata. Missing/invalid Core connection fails closed before host startup; no owner fallback.

## 4. Non-goals
No new auth store/API, identity engine, employee prerequisite, Procurement identity grant, general HRM CRUD, account administration/password-link delivery, publisher, live credential rotation, production deploy or schema change. Other private modes retain their existing behavior.

## 5. Ownership
Core owns LocalIdentity/HrmDbContext, organisation, roles and both audit families. The existing FullHostCompatibilityProbe owns private transport/assertions. Reviewed StageB/PR70 helpers own finite alias grant rendering and admission; do not copy an ACL or authentication engine.

## 6. Actors
Private pre-provisioned Core accounts in a new closed fixture tenant, including a workerless granted actor and an ungranted actor. Database LOGIN aliases are infrastructure principals, distinct from these business accounts.

## 7. Source scope
A new clone of the accepted StageB restored source retains all original source rows/schema/ACL/defaults. Three fresh finite aliases: Core-session, Record, Execution. Original NOLOGIN roles are retained. No adoption or modification of production accounts.

## 8. Data scope
Existing tenant/entity/branch services remain authoritative. SQL login grants do not create company membership or business action grants. Selected source company/branch and actor facts come from independently prepared private Core rows, not a caller's permission boolean.

## 9. Records
Private plan, actual mapped Core permission manifest, exact alias-only ACL union, private credentials, authenticated session observations, source/clone footprint/audit hashes and bounded result. Credentials/tokens/password hashes never enter Git or public output.

## 10. Lifecycle
Incomplete→distinct review→compiled manifest→physical profile/SCRAM admission→new clone/aliases/private actors→normal-cookie cohort→independent owner reconciliation. Failure remains retained failure with no repair/reuse/drop.

## 11. Inputs
Hash-bound prepared source, current schema/history/roles, accepted StageB matrix/helpers, exact mapped Core profile, runtime publication/image, private socket and fresh actor/credential facts. Shipped plan is incomplete; no role/DB/credential generation occurs during authoring.

## 12. Reads
Full mapped LocalUser/LocalSession/TenantRoleAssignment columns are required by existing entity materialization. Organisation/branch/optional-worker reads remain explicit projections. Core history is SELECT-only. No blanket HRM table access.

## 13. Writes
local_users UPDATE only failed_login_count,locked_until,last_login_at,password_hash,must_change_password,password_changed_at. local_sessions INSERT mapped fields and UPDATE only last_seen_at,revoked_at. audit_entries and privileged_action_events INSERT-only. No local_users/worker/role/branch-master INSERT/DELETE or audit UPDATE/DELETE/TRUNCATE.

## 14. Authority
New aliases LOGIN NOINHERIT/non-owner/non-elevated/no memberships. Core auth writes never broaden the Procurement roles: their existing NoCoreIdentityWrites/read-projection checks remain. Normal handlers retain current password, cookie, session, role and scope checks.

## 15. Authentication
Use existing LocalIdentity handlers and Program's real local scheme. Never the private subject substitution. Require actual finite session_user=current_user, real SCRAM and wrong-secret28P01 before HTTP; no HBA changes or fabricated success.

## 16. Host composition
An explicit typed restored-Core connection contract selects the private cohort. HrmDbContext uses that connection; existing Procurement connections stay separate and same physical store. Reuse the existing private factory startup-settings bridge with finite values only, then assert effective configuration and actual Core/Record/Execution services; owner credentials are never supplied to that bridge. Startup migration flagfalse, bootstrap credentialsblank, accrualdisabled, publisher never started. Core/HRM pending migrations fail through existing CoreSchemaStartup.

## 17. HTTP proof
Planned actual normal cookie login→me→current Core workspace/Procurement context→own password change→logout→old cookie denied, with both auth prefix aliases. Wrong password and ungranted account deny without commercial changes; exact current principal and worker-null facts asserted. No mocked Procurement responses or identity header.

## 18. Persistence
Use existing HrmDbContext/AuditInterceptor. Login/session changes and their entity audits use existing saves; privileged action audits use existing separate scope. Owner post-check must require both audit families, not treat logged audit failure as acceptance.

## 19. Enlistment
Procurement transactions keep existing CoreAsync on the Procurement connection for read-only identity evidence. Do not replace that with Core-session credentials, weaken NoCoreIdentityWrites or invent cross-owner writes.

## 20. Recovery
This is private no-resume verification. Lost auth response does not imply creating another account/session secretly; original command/request observations and owner reconciliation decide acceptance. Failed targets/aliases/logs remain. No public credential logs.

## 21. Audit
Owner verifies allowed actor-only login/password fields and session creation/revocation, exact append-only audit families and unchanged other accounts/business/source rows. Audit payloads remain redacted by existing interceptor; raw tokens never exported.

## 22. UI
Existing normal-cookie UI remains; no new page/layout/identity flow. This bounded source slice does not claim a new browser run.

## 23. Compatibility
Normal modes keep their existing constructor defaults/connection behavior. Explicit restored mode requires real local auth and rejects worker/claim substitutions. Legacy HRM features sharing HrmDbContext are outside this minimal profile, so no whole-host production role claim.

## 24. Schema/operations
No model, migration, HBA, role, grant or DB change is executed. Existing genuine Hrm/Core/Proc schema must already be ready. Private runner preserves source/default/other-grantee ACLs, uses exact reviewed inputs and disk/container/time bounds.

## 25. Tests
Pure tests cover exact profile columns/operations, deny writes to ownership/roles/workers/audits, selected-mode no fallback, required normal auth/same-store identities, credential and source admission, and planned audit/denial contract. Authored C#/HTTP proof remains unexecuted until separate compile/private gates.

## 26. Acceptance
Distinct source review, owning model/profile capture, real physical grants/SCRAM, normal cookie/self-password/revocation positive and denial cases, owner audit/no-effect proof. No provider/readiness override, fake employee or invented business grant.

## 27. Release holds
Source-only until compiled and independently reviewed. Private scanner, publisher, account reset/admin workflows, full legacy HRM profile, production deployment and UAT remain separate. Ordinary source readiness cannot imply actual login or whole-module completeness.

## 28. Next Core scope
Core owns separate account administration/reset-link/outbox and full HRM runtime grant profiles. Existing publisher process has its own delivery/retry rights and safe transport; never start it against copied production pending outbox. Reuse existing deployment services with distinct credential delivery, not a Procurement-owned auth/publisher replacement.
