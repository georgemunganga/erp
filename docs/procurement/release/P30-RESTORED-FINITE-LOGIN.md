# P30 restored-schema finite LOGIN — first release rehearsal slice

## 1. Feature identity
P30-LOGIN-01, Tier3 private release verification; source base c6e6f4b87e2a728082b746477a3b094c6478d39c. SOURCE ONLY, unexecuted. This does not activate a module or change production.

## 2. Problem
Accepted StageB05 proves SET SESSION AUTHORIZATION and zero-row SQL over the restored218-table schema. Its original roles are NOLOGIN; credentials and real login transport are unproven.

## 3. Outcome
Two fresh direct-grant LOGIN aliases authenticate through actual Npgsql connections and execute the same exact effective ACL/positive/42501 matrix without switching role/session identity. Full original rows/schema/ACLs and retained roles remain intact.

## 4. Non-goals
No Core HTTP/auth/session, legacy business CRUD, publisher, delivery, production restore approval, bank/Finance, employee provisioning, scanner, migration or deployment claim. Core runtime owner fallback remains a later explicit gap.

## 5. Ownership
Release harness owns disposable clone/alias admission and evidence. Existing StageB pure helpers own the reviewed218/4097 contract. PostgreSQL authenticates; Npgsql uses the real login. Core owns later runtime identity/session and publisher profiles. Procurement owns no copied Core identity engine.

## 6. Actors
Only the root coordinator may approve an exact private plan/execution window. No business actor/role is invented. New database aliases are infrastructure identities, not Procurement user permissions.

## 7. Source boundaries
Source is the accepted StageB05 clone, itself derived from StageA02 plus read-only supplement and the same-production-snapshot archive. The original5543-row projection is not canonical fixture data. Pin StageA/B result, final catalog/data/expanded ACL captures and renderer/matrix/helper bytes.

## 8. Data scope
A new fixed-pattern private clone and two new fixed-pattern aliases share one run suffix. Preserve the existing erp, procurement_api and StageB execution NOLOGIN role attributes/memberships. No role adoption, rename, password reset or existing database modification.

## 9. Records
Protected plan, exact input pins, source/clone snapshots, alias-only ACL delta, password-free role attributes, authenticated session evidence, negative authentication SQLSTATEs and protected result. No business rows or secrets enter Git/public logs.

## 10. States
INCOMPLETE → reviewed source plan → fresh source/target/alias admission → clone → aliases/direct grants → wrong-secret denial → authenticated matrix → unchanged-footprint evidence. Failure remains failure with retained artifacts; no implicit resume or cleanup.

## 11. Inputs
The private reviewed plan supplies exact private container/image/system identities, source result/captures, schema/data/ACL SQL, reviewed StageB helpers/matrix, runtime image/publication and private socket mount. Missing/null/changed input fails before Docker. No production DSN or caller-defined SQL.

## 12. Invariants
Only new aliases may gain exact direct table/column/schema grants. All prior grantee/grantor/options and all default ACLs stay exact. No default-privilege entry is added for the new aliases; this slice does not certify future schema transitions or future inherited PUBLIC rights.

## 13. Authorization
New aliases are LOGIN NOINHERIT, non-elevated, non-owner, no memberships/SET capability, connectionLimit-1, no expiry/config override. The owning private psql process alone provisions. Runtime input contains only the two finite aliases, never the owner connection.

## 14. Authentication
Deployment auth must be reviewed explicitly. Require scram-sha-256 local auth evidence and an actual wrong-secret28P01 denial for each new alias; trust/peer/password-skipping admission cannot pass the password-authentication claim. The runner never edits pg_hba or reloads PostgreSQL. An unmet auth contract is a blocker, not permission to weaken the test.

## 15. Exact ACL contract
Reuse the pinned StageB expected_targets and proof_sql generators. Translate only the two canonical grantee tokens to fresh aliases; do not normalize owners/other ACL tuples. Keep all218tables/4097columns and zero-row allowed statements/42501 negatives. Remove only the exact session-switch header and its owner-SET negative, replacing the latter with a catalog denial check; no SET ROLE/SESSION AUTHORIZATION is executed by runtime proof.

## 16. API and transport
Add one opt-in mode to the existing FullHostCompatibilityProbe before ordinary host startup. It opens real Npgsql connections with structured private credentials, verifies session_user=current_user and reads its own role metadata, then runs hash-bound proof SQL in rollback transactions. It starts no API.

## 17. Credential delivery
Generate random32-byte private passwords only after admission. Provision through stdin, never CLI argv. Deliver structured finite input in a new0600 file below a0700 evidence directory, mounted read-only into the verifier. No secret output, connection-string logging or production environment inheritance. Redact generated passwords from protected subprocess diagnostics too. The dedicated provisioning connection suppresses statement/error-statement/duration/transaction-sampling logging before its password-bearing transaction; no global PostgreSQL logging change occurs.

## 18. Persistence/transactions
Clone accepted source with ownererp; no migrations. Provision new cluster-global aliases only when absent and capture their exact attributes. Direct ACL additions commit only on the clone. Proof statements roll back and no business rows may change. Original source role/data/catalog captures are rechecked.

## 19. Concurrency
One coordinator serial window, idle source, fresh target/aliases/output, no force-disconnect. Require at least1GiB+200MB free on the actual pinned PostgreSQL data filesystem and evidence filesystem before Docker and before clone. A timed-out verifier can only be stopped by its revalidated immutable container ID/configuration, never a reused name. Snapshot/source identity must match accepted evidence before clone. A changed file, role, ACL or footprint aborts. Failed targets/aliases remain for review; do not recycle them.

## 20. Failure/recovery
Explicit no-resume semantics. Retain redacted logs and the first failed stage. If authentication or ACLs fail, no HTTP/business acceptance follows. No automated role/database drop, grant repair, source rewriting or production retry.

## 21. Audit/events
Infrastructure-only journal and hashes, no Procurement business audit/outbox effect is created. The original publisher remains stopped for this slice; copied production outbox must never be sent to real transports.

## 22. UI
None. Existing ERP/Procurement UI is unchanged.

## 23. Compatibility
Old roles/source tables/history/default ACLs remain exact. Accepted StageB matrix application provenance stays3942; newer harness source is not proof of current API behavior. Later release work must revalidate the actual application and Core runtime/publisher role contract.

## 24. Migration/rollback
No generated migration or model change. This is private infrastructure provisioning, not a downgrade test. Do not remove failed databases/roles automatically or alter accepted retained evidence.

## 25. Tests
Pure author tests cover bounded names/outputs, exact token/header adaptation, no runtime role switch, direct alias ACL union and unchanged other grantees/defaults, altered helper/input rejection, credential non-argv boundaries and fail-before-launch defaults. Runtime auth/matrix/footprint assertions are authored, unrun.

## 26. Acceptance
Distinct source review, exact private plan, compiled verifier hash, actual wrong-secret denials, two real finite-login matrix passes, original data/structure/default/other ACL/role equality and protected result. No API/publisher claim may be set true by this mode.

## 27. Release holds
Current source cannot run with the shipped incomplete example. Private source/capture/auth/runtime pins and reviewed status must be supplied by the coordinator. No DB, role, credential or SDK job was run while authoring.

## 28. Next dependency
A separate restored-schema HTTP mode must preserve the empty-fixture check, remove owner Core runtime fallback, supply a Core-owned authentication/session profile, exercise real legacy CRUD/current scope and verify retained old rows. HRM-only publisher startup/least privilege and safe transport remain separate before deployment.

Source references: [probe mode](../../../backend/procurement-api/tools/FullHostCompatibilityProbe/RestoredFiniteLoginVerification.cs), [gated runner and review instructions](../../../tools/procurement/restored_finite_login/README.md). PostgreSQL describes [HBA metadata as current file contents, not necessarily loaded rules](https://www.postgresql.org/docs/17/view-pg-hba-file-rules.html); actual wrong-secret denial is therefore also mandatory. [Superuser object grants record the object owner as grantor](https://www.postgresql.org/docs/17/sql-grant.html); the expanded ACL equality must independently confirm that behavior.
