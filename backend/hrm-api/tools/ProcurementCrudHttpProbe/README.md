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

## Source candidate — workerless supplier lifecycle

Added source-only on `codex/procurement-system-supplier-proof`, baseline `de4c6b2`. **Not built or run yet.** The isolated coordinator gate must execute this candidate before claiming the proof.

The existing `systemUser` Core account (no employee) creates a separate vendor and real category/contact/qualification through mapped HTTP. A second real Core `systemReviewer` account has no employee and only explicit stored access/vendor read/manage/approve/suspend grants plus company-branch membership. It returns the submission, the maker corrects/resubmits, and the independent reviewer explicitly assesses the synthetic qualification and approves. Actual grant revocation, foreign scope, stale/repeated approval, maker self-approval, active withdrawal and hold self-release must fail without changing supplier/contact/qualification/decision/audit/outbox evidence. Existing third Core actor places the hold; the workerless reviewer independently reactivates. A separate draft is withdrawn through HTTP. Fresh owning queries check final principal attribution, exact versions, qualification review, SupplierDecisions and audits.

No supplier status is owner-seeded for this workflow. The older `approvedSupplierId` remains an explicitly separate synthetic prerequisite; it is not retroactively relabelled as workflow evidence. Qualification evidence is synthetic human assessment, not external compliance or document-service verification. No bank/payment verification is invented. Database connection remains a private owner connection, so finite **application grants** do not prove restricted PostgreSQL role privileges. Outer identity-provider authentication remains synthetic.

Only after all CLI checks finish does the protected browser manifest include `actors.systemReviewer={principalId,workerId:null}` and `browserActorSwitch:"manifest-alias-v1"`. Test-only browser proxy may map the fixed alias to this manifest UUID; it must strip aliases and arbitrary actor headers before injecting the allowlisted actor. This does not extend production authentication. CLI PASS scope now explicitly names this separate supplier lifecycle while still excluding requisition/PO approvals, Finance, stock and full-module completion.

## P24 recurring planning opt-in

`run-private-postgres.py --recurring` retains the ordinary fresh owner/bootstrap and legacy
P19–P22 rehearsal. It then applies the actual generated P23/P24, compares the exact legacy
CHECK inventory plus hash-bound actual P23/P24 captures, and reuses real mapped routes.
Never skip pending migrations or supply guessed schema-ready values.

The opt-in creates a separate real private database LOGIN with no elevated flags, memberships
or ownership. A second composition of the existing module host uses that restricted
Procurement connection for actual plan create/read/list/edit/no-op/close, while its existing
Core projection connection remains separate. It receives only supplier/contract/history reads,
plan SELECT/INSERT/UPDATE and audit SELECT/INSERT. Actual role identity and rollback-only denied
DDL/Core/Finance/append-evidence operations are checked independently. This is distinct from
finite current Core action grants and from the ordinary owner-based CRUD/browser fixture.
The global `finiteRole=false` marker remains truthful; the nested recurring proof becomes true
only after the real finite-database HTTP subset passes.

The new workerless Core planner has an explicit stored action list and branch membership.
The original administrator remains a separate permitted other owner, and the supplier reviewer
has no recurring actions. The existing request sender/real handlers and c4 recurring/race partial
are reused; no API or runner is duplicated. The contract race requires observed PostgreSQL
blocking of both actual HTTP transactions before releasing its exact row-lock barrier.

With `--browser --recurring`, the protected manifest exposes only the fixed `recurringPlanner`
alias after CLI proof completes. It includes actual company currency and a real unassigned
branch for denial checks. A controlled stop must complete the fresh owning database-only check:
exact two new browser plans (one closed through versions1/2/3/4, one original lost-delivery
create), current workerless owner, exact amounts/dates/currency/audit, preserved original plans,
contracts and downstream records. Do not call HTTP after the listener stops. Exit0 alone or UI
screenshots without this owner completion are insufficient.

No test connection, role creation, fake login, scanner, Finance history, ledger or scheduler is
registered in the production application. The future ERP Finance/AP owner is unavailable;
planning creates no payable, reservation, invoice or payment. Failed runs retain their private
DB/role/logs and are never reset or automatically retried.
