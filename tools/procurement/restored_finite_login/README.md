# P30 restored finite LOGIN rehearsal — SOURCE ONLY

This extends the existing `FullHostCompatibilityProbe` with one early `--restored-finite-login` mode. It does not start the API, run migrations, send an outbox, or exercise business CRUD. It keeps the existing StageA/B NOLOGIN roles and accepted source database intact.

Read the [28-section contract](../../../docs/procurement/release/P30-RESTORED-FINITE-LOGIN.md) first. `plan.INCOMPLETE.example.json` deliberately cannot execute. No DB, role, credential, SDK or container operation was performed while authoring this slice.

## Reused contract

The private coordinator must pin the accepted StageB05 `finite_session_sql.py`, `acl_delta.py` and exact `route-table-matrix.REVIEW.json`, plus the accepted final StageB catalog/data/expanded ACL captures and StageA supplement result. `contracts.py` adapts only the exact session identity header, its exact owner-role negative and the two identity comparisons. The resulting authenticated proof executes no `SET ROLE` or `SET SESSION AUTHORIZATION`; the denied owner capability is checked through catalog permissions instead. All218 relation and4097 column expectations and zero-row positive/42501 negatives remain from the reviewed generators.

The alias adapter translates only the two grantees. The bootstrap-superuser process issues exact direct grants on the new clone and the later expanded ACL comparison verifies the recorded owner grantors. It changes no existing default ACL and does not reuse the earlier default-privilege activation adapter.

## Required private inputs before review

Copy the example into a new protected private preparation directory; never commit a populated private plan or captures. Supply and independently review:

- Exact immutable runner/contract hashes and plan bytes; all `files` entries mandatory with absolute non-symlink paths and SHA256. Existing StageB final output contains the accepted catalog/data/ACL; capture current full role/member metadata using the literal `ROLE_SQL` read-only query after the root grants a read-only window. Historical partial role captures cannot substitute for it.
- Exact private PostgreSQL container/image/system identities, idle accepted StageB source and a **new** `proc_p30_login_20261024_NN` target; corresponding `p30_record_login_NN` / `p30_execute_login_NN` roles must not exist.
- Current exact minimal HBA projection from `HBA_SQL`, reviewed for both new aliases/database. First matching local rule must already be `scram-sha-256`; unsupported complex selectors fail closed. The launcher cannot edit/reload HBA. A private cluster with `trust` is a genuine launch blocker. Wrong-secret28P01 remains mandatory even if the file says SCRAM, because file contents need not be loaded.
- Newly compiled whole publication manifest (relative file path → SHA256, no extras/omissions), immutable local runtime image ID, and the exact existing private socket host directory `/home/amizo/procurement-approval-execution/artifacts/procurement-approval/socket`. The pinned private PostgreSQL container `10b7cb11af108c3a47ba557c0f903ac73439f88b060e7f7150c08c8fa99778fd` must bind this real, non-symlink directory at `/var/run/postgresql`, with an actual Unix socket inside. The verifier mounts that same directory read-only at `/private-pg-socket`. The publication remains under the protected backup root. Arbitrary socket directories and symlink aliases are denied. Compilation is a later separately authorized SDK gate.
- A new `/home/amizo/erp-backups/procurement-p30-finite-login-20261024_runNN` output. No symlinks, parent traversal, reuse or input overlap. The runner has no repair/resume/drop path.

Only after distinct review may the coordinator change status to `REVIEWED_PRIVATE_FINITE_LOGIN_READY`, bind the exact plan SHA, and use the explicit environment/CLI admission:

```sh
P30_FINITE_LOGIN_EXECUTE=reviewed-private-only python3 run.py \
  --plan /absolute/private/reviewed-plan.json \
  --expected-plan-sha256 REVIEWED_SHA256 \
  --output /home/amizo/erp-backups/procurement-p30-finite-login-20261024_runNN \
  --execute-reviewed-private-login
```

This example is documentation, not permission to run. Private cluster settings may change independently; plan pins must describe the reviewed actual environment, never repair it silently. No production connection is accepted by the runner.

## Secret and execution boundary

The minimum reviewed free-space reserve is1GiB+200MB on both evidence and the actual Docker-pinned PostgreSQL data filesystem, checked before Docker and again before cloning. Secrets are generated only after admission and cloning, passed to private PostgreSQL through stdin, then stored in0600 structured input below a0700 directory. Secret-bearing SQL is not retained. Its dedicated owner connection disables statement, error-statement, duration and transaction-sampling logging before beginning the password-bearing transaction; these session settings disappear when that psql connection exits, with no global setting change. The finite verifier has RO publication, RO input, RO socket directory, one RW result directory, no network, no owner connection, read-only root, bounded CPU/RAM/deadline and no Docker log driver. Its entrypoint is the existing tool, not the application host. Diagnostics are redacted even in protected coordinator logs; C# prints only a generic failure or scope-specific PASS. Wrong-password rejection is attempted before each successful Npgsql login. Both before and after proof, `session_user=current_user` is asserted.

Failed clone/roles/container/evidence remain for diagnosis; only a timed-out verifier is stopped by its revalidated immutable created ID/image/labels/exact mounts/resources, never removed. Owner SQL has server statement/lock/idle-transaction timeouts; a CLI timeout still leaves the attempt failed with no resume, and the coordinator must inspect retained target/roles after server work settles. It is not a rollback/cleanup claim. Protected input remains credential material and requires normal operator-controlled retention. No credential appears in a shell argument, Docker environment or checked-in file.

## Author verification

From the ERP worktree:

```sh
PYTHONDONTWRITEBYTECODE=1 python3 -m unittest discover \
  -s tools/procurement/restored_finite_login -p 'test_*.py' -v
```

Thirteen portable pure tests plus one optional source-only integration test exist. Set `P30_REVIEWED_STAGE_B_PACKAGE` to the retained accepted StageB05 preparation directory to regenerate the real reviewed218/4097 contract using pinned helper bytes. That optional test reads only files and never connects to PostgreSQL. Fourteen tests passed locally with it enabled. C# compilation and real login/authentication/ACL/footprint execution are **unrun**.

## Exact next gap

Even a future pass is only `PASS_PRIVATE_RESTORED_FINITE_LOGIN_ACL_ONLY`. The existing principal HTTP fixture still requires an empty DB and uses a separate Core runtime connection; do not remove that safeguard or point it at this clone. Restored-schema API acceptance needs a Core-owned authentication/session login profile, no owner fallback, current physical readiness and real legacy CRUD/owner no-effect checks. Publisher ownership and safe transport must be reviewed separately; this mode never starts one.
