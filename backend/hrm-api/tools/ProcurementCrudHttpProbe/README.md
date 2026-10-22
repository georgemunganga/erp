# Actual Procurement draft CRUD HTTP/PostgreSQL probe

Status: authored, uncompiled and unrun. Coordinator owns serial build/execution and release decisions. This tool is not permission to deploy or run production test writes.

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
