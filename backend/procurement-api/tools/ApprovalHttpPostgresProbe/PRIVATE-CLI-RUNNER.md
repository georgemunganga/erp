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

## Original invoice-unit review CLI mode

The same runner accepts `--mode unit-reviews`. It invokes the existing
`ApprovalHttpPostgresProbe.dll --unit-reviews` entrypoint with a fresh
`proc_approval_http_can_urev_*` database and distinct finite runtime login.
All existing publication, private-container, no-network, credential-file,
hash and terminal-result checks remain in effect. No other workflow or
transport is introduced. `--browser` is explicitly rejected for this mode;
there is no original-unit review browser-fixture contract yet.

Use this mode only after genuine P23 SQL has been applied to an isolated
database, its exact PostgreSQL constraint definitions have been captured and
reviewed into the owning readiness manifest, and a complete source-bound
publication has passed its gates. An empty readiness manifest remains
unavailable; the runner does not fill it, substitute readiness, or change
production configuration. The probe's synthetic pre-scanned documents do
not certify real upload/ClamD operation. Original-unit review does not
establish Finance historical exposure, matching qualification, AP posting or
payment authority. This addition is source-only until its actual run passes.

## Optional actual-browser Unix fixture (source candidate)

Use the same runner with `--mode receiving --browser` or `--mode rfq --browser`, a whole immutable publication and a **new** protected output directory. No host networking, port publication, Docker socket mount, source/restored database or production configuration is supported. Matching CLI remains unchanged; matching browser is deliberately not added here.

The runner creates a fresh finite runtime role/disposable database as in CLI mode, freezes all runtime files, then runs foreground `--serve --receiving` or `--serve --rfq` in network-none. Only `/procurement-browser/probe.sock` is listened on in this optional transport. Its new ordinary mount is 0700, socket/fixture manifest 0600; existing sockets/manifests are refused. Synthetic setup still uses existing genuine PR/Finance/Core approval routes and migrations. Receiving prepares real issued/delivered goods/service sources, no receiving effects. RFQ prepares a genuinely approved competitive source, no RFQ/holds/receipts. No operational handler or global flag/auth configuration changes.

Wait for `<output>/browser/fixture.json`. Pass its path to `PROCUREMENT_APPROVAL_BROWSER_MANIFEST`, and `<output>/browser/probe.sock` to `PROCUREMENT_APPROVAL_BROWSER_SOCKET` for the actual browser suite. The manifest's `baseUrl` remains a logical fixture URL for existing URL/path validation; `transport=unix`, fixed container `socketPath` and `noTcpListener=true` require the explicit host socket. Browser forwarding uses Node HTTP's Unix `socketPath`, with no business-response mocks, redirects, automatic retries or backend TCP listener.

Run existing receiving browser scenarios or the scoped actual RFQ source spec against a separately frozen canonical UI publication. After browser completion, stop **only** the sandbox container named in `launch-manifest.json` with a graceful `docker stop --time 90 <sandboxContainer>` from the orchestration host. Do not send a browser completion flag. Existing fixture-owned OnStop verifiers query fresh owner contexts and require exact effects/receipts/conservation/unchanged upstream footprints. Exit 0 and runtime hash equality are necessary; retain failed container logs, database, role and manifests. No used data is deleted to make a verifier pass. The runner does not execute browser jobs itself.

Source authored only. Required parent gates: merged tool compile, private finite fixture startup/schema/grant proof, frozen UI actual browser scenarios, graceful independent owner verification, unchanged runtime files. This source is not runtime acceptance or production readiness.
