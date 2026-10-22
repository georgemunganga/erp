# P26 draft command coordinator: unregistered source slice

Parent contract: [PR draft command recovery](PROC-PR-DRAFT-COMMAND-RECOVERY.md), sections 3, 6, 9–12, 15–20 and 25–28. This slice starts at `13659556698e2a6c5b588aa5f3c046923288eb79` (accepted intent source atop persistence `f43b71ac1a7903056f8c5d8ef74a42690d5890fa`). It adds a host building block and pure contract tests. No mapper, route, feature flag, registration, schema, provider, UI or existing CRUD implementation changes.

## Owning action integration required before use

`PurchaseRequestDraftCommandExecution` takes mandatory callbacks for actual owning write/read readiness, the existing transaction-neutral business action, and actual persisted `ToDetail` serialization. These are explicit integration seams; no callback implementation or fallback Ready is supplied here.

Current `Create`, `Update`, `Submit` and `ToDetail` are private in `ProcurementRequestRoutes`. Submit currently begins and commits its transaction and returns an `IResult`. Before route integration, extract those existing actions to accept the coordinator's contexts/scope and return the saved request ID. They must throw a controlled rejection instead of returning an early `IResult`, never open another connection/transaction, never call external services, and preserve the existing validators, CAS, policy/submission effects, line behaviour and audit set. Keep Update's existing `UpdatedAt` bump, including an otherwise unchanged update. Do not recreate these rules in the coordinator.

The serializer must read the actual saved detail under this transaction and use the original route's response options. It returns one raw JSON string, preserved exactly in receipt and reply; dirty tracked state from serialization is rejected. The readiness callback must inspect this same owning connection/transaction, not approval execution or Finance configuration. Future dispatcher maps controlled rejection/status without exposing raw database/exception text.

## Command and historical read contracts

- Detach/revalidate the accepted DTO and its mutable line list before any await. No default currency or current catalog is resolved during this intent step.
- Per attempt: fresh physical Procurement connection; Core branch shared session lease, catalog shared session lease, exclusive scoped command session lease, then Serializable BEGIN. Lease namespace is tenant/company/operation/key; actor/target/context do not split the unique family. Shared lease cleanup remains owned by the existing helper.
- Resolve current stored Core actor, actual operation/access grants and current company/branch/department after leases. Optional worker metadata never grants access. Reuse existing owned-request principal/legacy `CreatedBy` filtering and effective scope. Current Core object checks match middleware's branch availability and active department rules.
- Read scoped receipt before the action callback or mutable catalog/default/CAS/state validation. Wrong actor/context hides receipt; changed fingerprint/target conflicts. Unique-key race returns retained409 for explicit recovery, never executes again in an aborted transaction.
- Business changes, ordinary audit/policy effects and receipt share this transaction; the receipt follows actual persisted versions and serialization. No receipt-only generic audit is introduced.
- Retry at most three fresh attempts only for confirmed PostgreSQL serialization/deadlock abort. No timeout, transport, cancellation, unknown commit or unique-violation retry. Do not deliver a response until commit returns; uncertain commit is an unconfirmed command, not a new-key retry.
- Historical actor-owned GET uses RepeatableRead, `SET TRANSACTION READ ONLY`, current original-operation grants and owning **read** readiness only. It does not require new-write, execution or Finance activation. Read original receipt bytes/status/hash and check current record ownership/visibility separately; do not compare original result with today's parent state/version or resolve current defaults/catalogs. A missing404 is never noncommit evidence.
- Only Create gets the existing request-detail Location. The acknowledgement's `originalResultJson` is a JSON **string** preserving raw response bytes, not a parsed/reformatted replacement detail.

Keyed route composition must replace the current catalog-only endpoint filter with this single ordered owner. Wrapping that filter would acquire catalog before branch and is unsupported. Unkeyed routes remain unchanged while successor dispatch is absent.

## Acceptance still required

Tests in `PurchaseRequestDraftCommandExecutionTests` cover deterministic namespace, operation policies, confirmed-abort classification, exact replay output, actor/context hiding, changed intent/target conflict, base metadata and detached DTO intent. They are authored source tests, **unrun in this slice**. They do not prove real authorization, transaction rollback, lock order, readiness, HTTP or PostgreSQL behaviour.

Before activation: compile/focused tests, genuine P26 migration/model review, actual PG17 CHECK parsing/constraint tests, finite read/write readiness, same-key parallel writer and lost-response proof, confirmed rollback at both saves, unknown-commit nonretry, full ordinary audit-set preservation, current-grant/scope races, historical GET after parent advancement/new-writes disabled, and original-data/ACL preservation. Existing143 migration artifacts remain unchanged. Finance/AP/IdP/legacy operational approval acceptance is outside this source slice; no provider or feature is enabled.
