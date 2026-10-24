# Core local-session private runtime — held source

Read the [28-section scope](../../../docs/procurement/release/P30-CORE-LOCAL-SESSION-RUNTIME.md). This is an isolated PR70+published-P30 prerequisite merge, with no production auth/identity API, model or migration change. No C#, DB, role, HBA or HTTP operation has run while authoring.

The existing Core model emits a private11-relation manifest through `--core-local-session-manifest`; it contains column names, never data or a readiness claim. The existing LocalIdentity handlers perform the actual password/session work. The new selected private mode requires an explicit typed Core connection, separate from Record/Execution, and rejects owner or undeclared connection keys before host startup. The app is constructed with `owner=""`. Real service contexts must prove Core/Record identities, and the existing enlisted transaction proves Execution identity with a no-write callback. No owner connection is delivered in app input/DI. Old private modes retain their defaults.

## Model and grant preparation

A separately compiled complete tool publication is required. An explicitly reviewed model-only invocation may emit `/result/core-profile.manifest.private.json` with `P30_RESTORED_CORE_SESSION=reviewed-private-only`; this opens no database connection. The manifest's physical/runtime flags stay false. It must be matched against the genuine accepted physical catalog and pinned before rendering.

The HTTP runner admits only the existing private PostgreSQL socket host path `/home/amizo/procurement-approval-execution/artifacts/procurement-approval/socket` from pinned container `10b7cb11af108c3a47ba557c0f903ac73439f88b060e7f7150c08c8fa99778fd`. It reuses the finite foundation's no-symlink and actual Unix-socket check, verifies the container's `/var/run/postgresql` bind, and presents that socket read-only at `/private-pg-socket` to the tool. This does not change authentication or create a second socket authority.

`render.py` only emits **blocked** review GRANT SQL and rollback-only zero-row SQL proof. It reuses PR70's grant formatter and reviewed StageB full218-table permission generator, adapting only the Core identity assertion, two table-name-to-OID lookups, one NULL-safe expected comparison, three column-only INSERT positives and eight exact auth/session column UPDATE positives. OID lookups avoid granting unnecessary Procurement/CoreApproval schema USAGE just to inspect forbidden-table permissions. Core receives schema USAGE only on hrm. It never receives audit reads or role/worker/master writes. No role creation/password hashing or SQL execution is performed by rendering.

Use a new protected direct child `procurement-p30-core-session-render-20261024_runNN` for rendering. The model-render plan needs `REVIEWED_CORE_SESSION_MODEL_PROFILE_RENDER_ONLY`, exact foundation/source/helper/catalog/model pins and the bounded new target name. Renderer and HTTP phase have different output prefixes, so they cannot consume/relabel one another's evidence.

## Independent Core-owned prerequisites

The runtime runner intentionally does **not** provision roles, alter HBA, create/migrate a DB, hash a user's password or copy Core employee/account records. A separately reviewed owner preparation must produce a fresh clone, three finite LOGIN aliases, exact direct grants, real SCRAM and two new workerless Core actors in a closed `p30-core-session-NN` tenant. It must use existing Core password/account ownership. It must preserve the original StageA/B NOLOGIN roles, original source rows and other ACL/defaults, with captured before evidence. Existing production accounts or a reused failed cohort cannot be adopted.

The granted actor has exactly current `procurement-access`; the other has no module actions. Both have one actual Core branch assignment, no employee link and no active preexisting session. The owning company/branch and a separate foreign company/branch must genuinely exist. A prepared witness alone grants nothing: runtime SQL and Core reads revalidate schema/ACLs, password authentication, stored actors, current actions and scope.

Owner role provisioning, genuine model/physical capture and post-HTTP owner audit reconciliation are explicit missing runtime gates in this source-only slice. The shipped example plan cannot launch.

## Private HTTP phase

After source review, compilation and owning preparation, `run.py` can be admitted only with a separate exact plan SHA/status and `P30_CORE_SESSION_HTTP_EXECUTE=reviewed-private-only`. It requires full publication hashes, actual source/catalog/base-ACL/matrix/helper pins, current HBA metadata and a protected structured credential input. It uses only read-only owner observations outside the app to verify private cluster/schema and exact alias-only ACL union. HBA must already have the reviewed first matching SCRAM rules; actual stored verifier kind and wrong-secret28P01 must agree. No HBA reload or credential repair is available.

The app receives only finite Core/Record/Execution credentials plus actual prepared actor facts; unknown/owner fields are rejected. Input is0600 below0700 and mounted RO; no connection string/password is placed in Docker argv/environment or logs. Only the explicit opt-in flag is passed by Docker. Inside that private process, the existing `PrepareEntryPointEnvironment` factory bridge installs ONLY the three finite connections before Program reads configuration; no owner value is supplied. Effective configuration and actual services are then rechecked. This process-local bootstrap bridge is not a new credential or identity engine. Runtime image is an existing immutable local ID, networknone,1100MiB/no swap/oneCPU, read-only root and exact mounts. Timeout stops only the inspected immutable created ID/image/label/mount/resource tuple. One output name is tied to each DB suffix; no resume/drop/reuse.

The source HTTP cohort uses normal password responses and the real opaque session cookie, not identity headers: both existing auth aliases, login/me, Core workspace/Procurement context, foreign-scope and admin denial, wrong current password400, own password change, logout/revoked-cookie denial, old password denial/new password success and cleanup logout. The ungranted Core actor can authenticate but cannot access Procurement or account administration. No employee is created, and operational Procurement/Finance/docs features and accrual remain disabled. No publisher is started.

## Audit and acceptance hold

The HTTP result is **`PASS_PRIVATE_CORE_SESSION_HTTP_AWAITING_OWNER_AUDIT`**, never an owner/audit acceptance. The coordinator phase result remains `CORE_SESSION_HTTP_PHASE_COMPLETE_OWNER_RECONCILIATION_REQUIRED`; `ownerAuditVerified` and original-row preservation-after-HTTP stay false. The Core login cannot read its INSERT-only audit tables to self-certify them.

Independent owner reconciliation must pin the original before snapshot and actual request/actor/session graph, verify only the two prepared actors' six user fields and session creation/revocation changed, require both redacted `audit_entries` and `privileged_action_events` for the correct actors/paths/statuses, reject orphan/missing/foreign audit rows, and preserve every other original account/business/role/branch/outbox row. A logged audit error plus an HTTP200 is failure. This observer/profile acceptance adapter is deliberately not replaced by a caller boolean or generic event count. A complete owner result is required before any wider runtime claim.

## Source verification

Ten Core pure tests pass, including use of the hash-pinned retained StageB generator on an explicitly synthetic mapped-profile fixture; fourteen inherited PR70 pure tests also pass. These do not claim an actual C# manifest capture or physical SQL execution. Python AST and diff checks pass. C# compilation, mapped manifest capture, finite grants/authentication, HTTP and owner reconciliation remain unrun.

```sh
P30_REVIEWED_STAGE_B_PACKAGE=/absolute/accepted/stage-b-prep \
PYTHONDONTWRITEBYTECODE=1 python3 -m unittest discover \
  -s tools/procurement/core_local_session_runtime -p test_profile.py -v
```

General HRM CRUD, account provisioning/reset links, full Core role deployment and the existing separate outbox publisher remain distinct Core-owned work. In particular, the shared HrmDbContext serves other HRM features; a minimal auth profile is not permission to promote a reduced role as whole-HRM production readiness.

The selected-mode false list explicitly includes InvoiceUnitReview and ManualPayments. `--preflight-core-session-config` authors a no-DB/no-startup C# regression using the actual `RequireConfiguration`: baseline selected values are false, each late/inherited effective true is denied, and the unselected legacy mode retains its documented settings. That preflight is also called before the source HTTP cohort. It is authored but unrun without SDK; the Python source guard passed.
