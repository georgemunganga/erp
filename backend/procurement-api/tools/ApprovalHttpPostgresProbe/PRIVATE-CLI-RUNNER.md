# Canonical finite-role private CLI runner

Source candidate based on canonical `0287ab9`. This tracked runner reuses the existing actual `ApprovalHttpPostgresProbe`; it adds no application service, schema, identity master, authorizer, Finance owner or alternate workflow engine. Previous orchestration provenance: held `artifacts/p26/provision-private.py`/`run-actual-matching.sh` and `artifacts/p28/provision-private.py`/`run-rfq.py`. Do not execute those old fixed-worktree/moving-DLL scripts for this candidate.

## Parent-controlled sequence

After the serialized full source/tool compile succeeds, supply the **complete** publication and an existing immutable local .NET10 ASP.NET runtime image ID. Use a new protected output for each mode. The runner generates a fresh `proc_approval_http_can_*` database and distinct `proc_approval_runtime_can_*` login on the exact running network-none private PostgreSQL container. It accepts no source/production connection or old database name. Provisioning failures retain any newly created role/database; no cleanup or retry is automatic.

```bash
python3 backend/procurement-api/tools/ApprovalHttpPostgresProbe/run-private.py \
  --mode receiving \
  --publication /protected/complete-reviewed-workflow-publication \
  --runtime-image sha256:<existing-reviewed-local-dotnet10-image-id> \
  --output /protected/new-unique-canonical-receiving

# A separate new role/database is mandatory; do not reuse the consumed receiving fixture.
python3 backend/procurement-api/tools/ApprovalHttpPostgresProbe/run-private.py \
  --mode matching \
  --publication /protected/complete-reviewed-workflow-publication \
  --runtime-image sha256:<same-reviewed-local-image-id> \
  --output /protected/new-unique-canonical-matching
```

It freezes/hashes every runtime file, rejects root/nested publication symlinks and recursive output placement, verifies the private container's state/network mode before DDL, and launches `--pull=never --network=none` with only read-only publication and the existing private Unix socket mounted. There is no source/config/Docker-socket/NuGet mount, TCP listener, browser/serve mode or background external worker. New runtime/bootstrap connection values live only in a mode0600 private env file and process memory, never argv/manifests/stdout. Keep every output directory restricted; do not commit credentials or HTTP logs.

The existing tool applies genuine canonical Procurement and Core approval migrations, uses synthetic Core/HRM identity bootstrap only, then provisions/asserts its finite runtime contract. The new runtime login starts nonowner, nonsuperuser, without role memberships or broad schema/HRM writes. Actual API handlers create PRs/Finance evidence/approval/source POs/issue/delivery/receipts and independent policy/authority lifecycle evidence. Manual policy configuration and supporting approved supplier prerequisites are **explicit synthetic sandbox fixtures**, not production policy or external Finance proof.

Receiving proves its supported positive goods/service workflow only after actual execution succeeds. Matching currently proves exact intake/mapping/selectors and a truthful durable **Blocked** comparison/replay/recovery because the authoritative original-unit review and historical Finance exposure owners remain unavailable. It cannot claim qualified matched/AP/payment authority. The source provenance/compilation manifest must be reviewed by the parent separately from the runtime hashes.

Python syntax/`--help` are the only completed checks for this runner. No PostgreSQL provisioning, SDK, actual HTTP or browser invocation was performed during authoring. No runtime PASS or production activation follows from this document.
