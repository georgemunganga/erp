# Actual Procurement draft CRUD HTTP/PostgreSQL probe

Status: first checkpoint at backend dfdf36a compiled and passed 109 assertions / 72 actual HTTP requests; the companion UI passed seven actual private browser cases. The second backend checkpoint including credit/catalog checks compiled and passed 152 assertions / 90 actual HTTP requests. Its additional browser gate is pending. Coordinator owns serial build/execution and release decisions. This tool is not permission to deploy or run production test writes.

## Guarded environment

The coordinator supplies a **fresh existing disposable database** through `ConnectionStrings__Procurement`. Only private `/authority-identity-private-socket` or loopback PostgreSQL hosts and `procurement_crud_http_*` database names are accepted. No CLI arguments, public TCP listener, database reset/drop/resume, forced disconnect or container cleanup. The tool refuses existing user tables. Keep the resulting database and first failure logs for forensic review.

Outer authentication is synthetic and tool-only. Two real synthetic Core worker-linked users, one real company/branch and a foreign company are persisted through HrmDbContext. Actual ProcurementScopeMiddleware, configured policies, normal ProcurementHostIntegration DI and mapped production handlers run through TestServer. Runtime uses the explicitly supplied private **owner connection**: this proves persistence/handler behavior, not finite-role privileges or real identity-provider authentication.

## Planned execution

- Core EnsureCreated first; actual Procurement migration to P19.
- A clearly labelled synthetic active/orderable supplier is persisted as an owner prerequisite. This is **not supplier approval workflow proof**.
- Raw seed one legacy invoice/line in actual P19 schema, excluding nonexistent new columns. P20 Up → unused Down → Up; compare every original invoice/line column, require legacy BranchRef remains null, legacy line default active and no pending model changes.
- Real context/meta GET: canonical synthetic worker, CRUD enabled.
- Vendor create/read/attribute update/reload, contact/address create/correct/remove with retained history, and versioned withdrawal; stale/foreign-company denials; fresh owning-context verification of retained values/children.
- Item create/read/update/deactivate. The new item is already inactive; deactivation explicitly exercises the idempotent no-op, **not active-item retirement or activation**.
- Request create/read/update/withdraw with owner reload.
- Plans, ad hoc RFQ, contracts and purchase-order drafts create/read/update/cancel. These are draft routes; no approved-demand conversion, invitation dispatch, contract activation or order issue.
- Existing configuration category/method create/read/edit/retire; no invented policy or approval defaults.
- Invoice capture with exact decimal strings beyond JavaScript precision, update with reason and retained removed-line history, stale correction denial, cancel and independent exact owning-context reload.
- Actual used P20 Down must raise 55000 with `Retained invoice branch or removed-line history prevents destructive downgrade.` The complete vendor/attribute/invoice/line/audit/outbox/migration-history footprint must remain unchanged.
- Final owning reload rejects unintended matching, receipt, AP handoff or payment-status records.

The successful JSON summary counts assertions and actual HTTP requests. These are one bounded run's counts, not a full-module coverage claim. No original-unit reviewer, Finance exposure owner, budget validation, supplier activation, payment, stock or financial ledger is supplied.

## Parent-controlled gate

Build the exact source with the existing cached SDK/packages, one serialized process within coordinator resource limits. Run precompiled DLL in the private network-none/socket runner. Preserve source/image identity, logs, database and initial failures. Do not retry just because an observation times out. Failure JSON includes only bounded stage/code/constraint diagnostics, never connection strings or payload data.

## Optional private browser transport

Default mode remains in-process TestServer. To run the same actual CRUD suite through a Unix socket and then keep it available for the parent browser gate, set exactly:

- `PROCUREMENT_CRUD_BROWSER_SOCKET=/crud-browser-socket/api.sock`
- Optional `PROCUREMENT_CRUD_BROWSER_MANIFEST=/crud-browser-socket/fixture.json`

The runner must supply the private `/crud-browser-socket` mount. Alternative paths, a symlink mount, stale socket or existing manifest are rejected; the tool does not overwrite them. Kestrel binds only that Unix socket, no TCP port. The CLI uses an HttpClient Unix `ConnectCallback` to exercise the same actual routes through this listener. The manifest is written with create-new semantics and owner-only mode only **after all CLI assertions pass**; `PRIVATE_BROWSER_READY` is then emitted and the process waits for normal shutdown. A failed CLI never publishes readiness/manifest.

The synthetic manifest contains only company/branch/actor/worker/supplier/legacy-invoice IDs, exact tool auth-header and socket names, and explicit proof limits—no credentials or production data. The parent owns the separate Vite Unix-socket proxy, Playwright traffic capture and independent post-browser database checks. CLI PASS does not cover later browser writes.

Tool-only authenticated Core presentation endpoints (`auth/me`, branding, legal entities, locations, shell) exist in socket mode only. User, worker, company and assigned branch are read from actual fixture Core records; branding is explicitly synthetic. They are not a production Core/auth implementation. Procurement context, metadata, error, command and read responses always come from the actual normal host routes and PostgreSQL; no Procurement response is mocked. Two synthetic actor IDs are the only accepted outer auth-header values.

No frontend, auth master, public test endpoint or production adapter is introduced. Private fixture shell routes disappear when this standalone tool stops.


## Exact request draft companion (source authored; execution pending)

`RequestExactValuesVerification.cs` runs inside the existing guarded private tool, before the other draft scenarios. It uses normal mapped category/item create and independent `Other` activation, then actual catalog price create and independent publication (the existing supplier is explicitly a synthetic approved/orderable owner prerequisite, not a supplier-approval proof). It then proves:

- Maximum `99999999999999.9999` string original quantity/rate transport, additive exact GET companions and unchanged legacy numeric fields.
- Current-CAS header correction retaining hidden header/line/discount/source fields; a removed line remains with its original quantity and Cancelled quantity/status.
- Actual PostgreSQL owner reload, old-CAS conflict, foreign-company Core denial and nonowner read denial; full owning-table snapshots unchanged for denials.
- Raw scale/long-significand/underflow/range lexeme rejections before POST/PUT data or audit writes.
- Legacy numeric input with two exact eight-place products (sum `0.00000002`), no currency rounding requirement.
- Selected genuinely published catalog IDs/version and exact price preserved through draft correction/reload.
- No request submission/approval/budget/policy evaluation and no unrelated Procurement table/history/outbox/audit changes during request operations. Full table snapshots reuse the existing independent `PolicyStoreRows` owner helper; intended request audit rows are excluded only in the success-protected snapshot.

These new scenarios are **not executed yet**. Existing PASS counts describe earlier sources and must not be reused as this candidate's evidence. The complete tool's assertion/request totals are computed at runtime; no fixed new totals are claimed. It retains existing no-production connection/prefix and synthetic outer-auth/owner-connection limitations, other CLI/browser modes and original source/DB preservation behavior.

## Canonical governed-source compatibility candidate

This tool-only increment is authored against canonical P21 with operational options still disabled. It keeps the real released P19 legacy fixture and P20 unused Down/Up preservation check, using old-schema column projections rather than materializing new P21 invoice fields against P20. It then applies the **actual** `20261022052824_P21CanonicalGovernedProcurementFoundation` before mapping current HTTP routes; no pending-model/migration check is stubbed and no history is invented. Original legacy invoice fields compare identically after the additive upgrade. All nine exact ownership FKs, all 39 validated model CHECKs and actual migration/model readiness must remain present both before HTTP and after the expected used P20 Down veto.

The existing synthetic HRM `EnsureCreated` remains only a tool identity fixture. Core.Approvals and Core.Documents use distinct schemas; with execution/document options absent and false, the bounded CRUD handlers do not require or create those schemas. This candidate explicitly verifies all operational metadata capabilities remain false and both schemas remain absent after the CRUD matrix. It is not PR approval, PO issue, receiving, document-owner or qualified invoice-matching proof; the separate genuine workflow tool owns those gates.

The tracked `run-private-postgres.py` replaces the old ignored fixed-worktree launcher for this candidate. It freezes and hashes the **entire** supplied publication (rejecting root/nested symlinks and recursive output placement), inspects the exact private PostgreSQL container's running/network-none state before creating a fresh database, and launches only a preexisting immutable local .NET10 runtime image with `--pull=never --network=none`. Only read-only runtime and private Unix socket are mounted; no source, actual configuration directory, NuGet cache or Docker socket is exposed. Credentials remain inherited environment only and never enter manifests/log commands. Optional browser transport uses the existing Unix socket only and publishes readiness only after all actual CLI checks pass. Databases/logs are retained on failure; no automatic retry/reset/cleanup occurs.

```bash
python3 backend/hrm-api/tools/ProcurementCrudHttpProbe/run-private-postgres.py \
  --publication /protected/complete-reviewed-tool-publication \
  --runtime-image sha256:<existing-reviewed-local-runtime-image-id> \
  --output /protected/new-unique-canonical-crud-run \
  --browser
```

Parent must review source/build provenance and serialize execution after the current SDK/browser gate. Python syntax and `--help` alone were checked; C# compilation, canonical-source CLI/browser counts and this runner's PostgreSQL execution are **pending**. Earlier PASS counts above belong to their stated older sources and do not certify this increment.

## Recurring purchase follow-up (source prepared, not yet executed)

`PROCUREMENT_CRUD_RECURRING=1` adds actual HTTP create/list/detail/exact-value edit/close,
Core-default currency, maker isolation, stale/null/wrong-supplier/precision denials,
retained closed-contract references, independent owning-store/audit checks, and two
concurrent HTTP writers contesting contract supplier reassignment versus plan creation.
A private owner row lock must make both real transactions observably wait before
release; merely starting two tasks does not count as a race proof.
The journal contains no new Finance/AP/payment records. This is purchase planning,
not recurring bill generation.

The genuine P22/P23 additive migration chain and corresponding schema verification
must be integrated before this flag can run successfully. This source increment does
not skip the existing no-pending-model/no-pending-migration checks or manufacture a
schema receipt. No new runtime/test pass is claimed by adding the flag or test source.
