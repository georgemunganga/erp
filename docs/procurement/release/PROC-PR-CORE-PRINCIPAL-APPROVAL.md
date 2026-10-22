# Core-principal purchase-request approval successor

## 1. Status and provenance

P25 source integration starts from published ERP main `b6b62cd551ad134879cc47c7ceb32a0570201aed` (accepted P24). The prepared source `d2c75e0` was normally merged on isolated branch `codex/procurement-p25-principal-integration`. Their common ancestor is `dbf5107`; the only incoming commits absent from main are the ten bounded P25 source commits `be32829` through `d2c75e0`. No held ancestors outside that already published common history were imported. The automatic merges retain published P23/P24 fixes, current Core scope, recurring CRUD, and actual document/readiness corrections.

All **141** existing backend migration/designer/snapshot artifacts are byte-identical to that main baseline (43 belong to Procurement). Path-plus-SHA256 inventory digest: `f3e06c1f81f5b45efe63cec0481151c97cd7803a976e4adbbe83d4bb0fbc3152`. P25 changes the source model only and cannot be deployed until a genuine successor migration is generated and accepted. No SDK, database, migration generation or browser jobs have run for P25. Earlier P23/P24 acceptance does not verify this source.

## 2. User outcome

A currently authorized named Core user may submit a request, obtain an independent funding check and receive an independent approval without an employee record. Optional HR metadata conveys no permission.

## 3. Existing deficiency

PR capture is principal owned, while operational submission2, Finance checker1, spending identity1 and assignment1 require workers. Replacing one resolver would leave contradictory retained evidence.

## 4. Ownership

Core owns accounts, current grants, organisation and approval cases. Procurement owns source attribution, funding references, spending limits and request effects. Reuse the existing Core approval engine and scope projection.

## 5. Bounded delivery

Source checkpoints add explicit successor contracts/model guards, current Core actor bridges, new-only request provenance and funding capture, and version-dispatched authority/approval commands and hints. The actual generated-schema provider is still Unavailable; PrincipalExecutionEnabled defaults false. Existing manual CRUD remains usable without operational provenance when unavailable. None of these checkpoints has been compiled or executed.

## 6. Original request attribution

Only a new ERP Draft/version1 may capture principal identity1 with canonical CreatedBy/requester and the actual optional Core worker link. Bind request ID, tenant, company and source channel. Persisted older rows cannot be adopted or backfilled.

## 7. Known aliases

A retained optional worker link is denial-only person-alias evidence, including inactive/offboarded links. Distinct principals with a common retained or current known alias cannot approve one another. Null/null is not an alias match. Current accounts/grants remain independently required.

## 8. Finance checker2

New checker2 binds canonical current principal, optional known alias and the complete exact funding tuple. Manual Finance policy/reference/freshness/coverage/reservation rules remain unchanged. Finance is a future ERP module and no authenticated ERP Finance budget/reservation/AP/payment owner is installed. This successor only retains existing manual/recorded references under explicit configured controls. It is not a Finance attestation, verified reservation, AP posting or payment.

## 9. Finance compatibility

Finance0 and checker1 encoders remain literal legacy implementations. Checker2 is explicit and cannot be encoded as0/1. Historical rows remain unchanged; no current-account inference creates missing original attribution.

## 10. Spending identity2

New authority evidence2 permits optional known aliases while retaining exact original creator, current submission round, independent activation, recipient and business fingerprint. Original identity1 requires the original workers and exact existing digest representation.

## 11. Finite authority

Keep PurchaseRequest.Approve / procurement-approval-decide / EstimatedLineBase. Evaluate the complete logical grant family before current recipient identity checks. No overlap hiding, combined grants, FX or default limits.

## 12. PR submission3

Pin exact original principal attribution, original requester alias, current maker alias, exact business snapshot, published policy decision, Finance2 and case/command identity. Worker digest2 is absent; a distinct principal digest binds the new representation.

## 13. Assignment2

An explicit envelope binds tenant/company/submission/case/stage/assignment/principal/optional alias/strategy and complete selected authority. Strict case-sensitive, bounded JSON rejects unknown/duplicate properties. The legacy assignment reader never accepts it.

## 14. Current eligibility

Retained evidence never confers current permission. New command bridge must resolve current canonical Core accounts, exact action and company/branch reach at each command. Original creator offboarding cannot make a different account the maker.

## 15. Assignee strategies

For successor submission3, existing `ProcurementManager` strategy selects current Core principals with the exact approval action and complete finite authority; it does not require the literal `procurement_manager` role label. Custom Core roles are supported only through their explicit current action grants. The legacy worker resolver retains its original strategy semantics. One requires exactly one eligible current principal; All retains its existing semantics. Query HR manager only for LineManager, using the retained `RequesterCoreWorkerId` and requiring the current resolved active HR worker to equal that exact pin before reading its actual manager. An absent, inactive or rebound employee mapping makes that strategy unavailable. The old `RequesterWorkerId` snapshot is not a substitute for the retained Core alias. BudgetOwner remains unsupported until an owning contract exists.

## 16. Core approval execution

Use the same cases, stages, assignments, decisions, CAS and terminal projection. Do not add an approval engine or weaken immutable submission/effect receipts. Decision/release readers must dispatch by explicit retained contract version.

## 17. Store guards

Original attribution is immutable against persisted owned data, not caller-supplied OriginalValues. Added checker2/authority2 attribution matches actual current scope Core link. Added assignment2 must match submission3 and preserve principal/known-alias independence, exact amount and selected grant terms.

## 18. SQL constraints

Add null-safe request provenance and submission principal checks. Extend version discriminators explicitly; require worker columns for identity1, allow only nonzero optional aliases for2, and reject new fields on legacy submissions. Keep the existing P22 basic request identity constraint unchanged.

## 19. Migration ordering

No migration/snapshot is edited here. P23 invoice unit review and P24 recurring are now published prerequisites; root owns genuine P25 EF generation after this source review. Capture exact PostgreSQL CHECK/FK/index manifests before readiness can be true. The expected source delta adds **no table, key, index, FK, Core entity or Finance owner**.

| Existing table | New field or nullability change | Retained semantics |
| --- | --- | --- |
| `purchase_requests` | `requester_identity_schema_version` required integer, default `0` | `0` keeps historical rows unadopted; new gated capture uses `1` |
| `purchase_requests` | `requester_core_worker_id` nullable UUID; `requester_identity_sha256` nullable varchar(64) | Optional genuine alias and immutable original-attribution digest; both absent on version `0` |
| `purchase_request_approval_submissions` | `requester_principal_id` nullable UUID; `requester_identity_sha256` and `principal_identity_sha256` nullable varchar(64) | All absent on legacy submission `1/2`; required nonzero/hash proof on submission `3` |
| `assignment_authority_evidence` | `identity_schema_version` required integer, default `1` | Original assignment payload `1` remains exact; successor is explicit `2` |
| `spending_authority_identity_evidence` | Existing `actor_worker_id` and `recipient_worker_id` become nullable UUID | CHECK still requires both for identity `1`; identity `2` permits only nonzero optional aliases and PR approval action |

Total: **seven new columns and two existing UUID columns made nullable**. Existing budget checker alias, submission aliases and assignment alias are already nullable; their CLR/JSON successor contracts do not add more nullable database columns.

Two new CHECKs: `ck_pr_original_principal_identity`, `ck_pr_approval_principal_identity`. Six replaced CHECKs: `ck_pr_approval_submission_identity`, `ck_pr_approval_worker_identity`, `ck_budget_checker_identity`, `ck_pr_finance_identity_version`, `ck_authority_identity_evidence`, `ck_pr_assignment_authority_identity`. All other existing schema objects, including canonical P22 basic request identity and P24 recurring state/ownership constraints, stay exact. Genuine EF generation `20261022161353_P25CorePrincipalRequestApproval` from accepted source `71e71aa334091993685a8fe10469cabf73e0813f` confirms seven added columns, two nullable alterations, eight added CHECKs and six dropped CHECKs, with no tables, indexes or FKs added. The generated Up, Designer and snapshot are exact archived EF output; Designer model body equals the snapshot. All 140 earlier migration artifacts are unchanged; only the current Procurement snapshot advances. SQL was subsequently applied only in isolated rehearsal databases as recorded in section 26; P25 is not applied in production.

## 20. Downgrade

The authored Down starts with ordinary transactional ACCESS EXCLUSIVE locks over the five evidence/source tables plus audit and outbox, then refuses any new request provenance, Finance2, authority2, submission3, assignment2 or corresponding durable audit/outbox evidence with SQLSTATE `55000`. It must not erase aliases, fill fake workers or cast a new receipt into an old version. Two EF-generated reverse zero-UUID defaults were removed: the first guard rejects nullable successor aliases before restoring the original NOT NULL columns. The actual unused PostgreSQL downgrade and reapplication passed as recorded below. Used-row, audit-only and outbox-only downgrade veto cases remain pending.

## 21. Readiness and activation

Execution requires a default-off explicit principal-approval option plus owning-context exact successor schema and finite execution role. IsReady covers reads; CanRecord covers Procurement-only funding/configuration writes; CanExecute additionally covers the coordinated finite Core workflow role. Metadata principalApprovals is a separate optional capability computed on the actual configured execution context, never the normal read context. Current production registration is Unavailable for all three checks. Missing/unknown schema is unavailable. Old providers must dispatch exact old/new manifests rather than accepting a broader CHECK by implication.

## 22. API compatibility

No client supplied identity/schema selector is accepted. Basic legacy numeric/exact request wire remains unchanged. Existing original worker receipts retain their bytes. New metadata is omitted from legacy audit representations where required; new versions use explicit successor payloads.

## 23. Atomicity and recovery

Keep current transaction coordinator, shared Core physical transaction, current record scope and original UUID command receipts. Ambiguous results recover by existing GET receipts with current access. Historical receipt proof cannot authorize a new command or recipient transfer.

## 24. Pure acceptance

Authored: workerless Finance2, authority2 and PR3 positives; original alias/subject/business-version/amount tamper; explicit downgrade/mixed-version denial; assignment tuple/unknown/duplicate JSON denial and exact eight-decimal amount retention. Focused domain/model gate passed **191/191** at `c1301cc`; all corresponding dependency bytes remain exact at final API candidate `71e71aa`. Focused mapped/API gate passed **23/23** at `71e71aa` (no skipped cases), including workerless checker capture, custom-role finite-authority selection, retained alias/LineManager denials and bounded read SQL projections. Initial failed checkpoints are retained: 167/180 domain, then 22/23 host before the required needed-by fixture was corrected. The first post-generation full-domain checkpoint at `7672fa4` passed 1,249/1,252 and stopped before host/model/SQL: three legacy evidence fixtures lacked their owning request. Test-only correction `e23c73951b2ca9923e0c4c10f284100877f59299` seeds explicit legacy worker-owned sources, filters the intended evidence audit and adds a missing-source/no-effects negative. At that frozen application/test source, the full Procurement domain suite passed **1,253/1,253** and selected Procurement/shared HRM, handwritten-model, Loan and InvoiceUnitReview host cohort passed **586/586**, with zero skipped/failure/error counters. Both expected nonempty TRX cohorts are validated, not inferred from process exit. No actual PostgreSQL successor workflow success is claimed.

## 25. Required mapped acceptance

Still required: canonical workerless maker/checker/approver positives; stale claims, revoked/locked/archived/wrong-tenant/wrong-branch accounts; all retained/current alias combinations; original maker offboarded; finite limits/overlap; LineManager unavailable; no mutation on denied commands.

## 26. Required actual acceptance

Genuine migration source has been generated and independently reviewed. The `e23c739` post-generation gate rebuilt Infrastructure, reported no pending model changes, and emitted genuine P24→P25 Up, P25→P24 unused-only Down and idempotent Up SQL. All tracked source and 143 migration/designer/snapshot artifacts remained byte-identical throughout the gate; no SQL was applied and no publications were made. Evidence: `/home/amizo/erp-backups/procurement-p25-post-generation-20261022/20261022_162515/result.json` and its two TRX files. SQL SHA-256: Up `2ed7b6d2dc090adfed0fc712483aabb9bae5e5b06546a0831e72468df3e44fba`; Down `1b298d0a0fb8ccd42ab1f0b93260ef02d89896c1a21b3209728286e75492ba1c`; idempotent `0aa6c7cdd0de23ddfb59bd7179591cc48ba20af99be11784b8b0f18bf98c553c`. The subsequent isolated PostgreSQL17 basic capture passed at `/home/amizo/erp-backups/procurement-p25-private-capture-20261022_browser03/result.json`, SHA-256 `99990a425f07fc1f3c8135bd1b3f116ded9e6e8e3f140a366a149777408737fd`. Genuine fresh and used Up, twice-repeated idempotent Up on each, unmodified unused Down and re-Up passed. Existing data, catalog, history, role and object privileges were preserved; the seven newly dropped/re-added columns alone moved by their exact table-specific physical ordinal count. The original accepted P24 browser database, including its workerless Closed version-4 record and ordered creation/update/closure audits, remained unchanged. PostgreSQL template cloning required reproducing only the new clone's database-level ACL; full clone equality was mandatory before migration, with no source or global-role changes. Earlier failed checks and databases are retained, not counted as passes.

The unregistered read-only readiness candidate now contains the actual migration ID, 17 literal PostgreSQL CHECK definitions and three exact partial-index predicates for its selected model. No expression normalization or production registration is introduced. The manifest itself grants no readiness: real schema, current connection and finite-role checks still apply. Used-evidence veto, raw NULL/zero/CHECK/FK negatives, actual finite-role HTTP pipeline and independent persisted effect proof remain required. Prove separate finite read, Procurement-only record, and Core workflow execution roles positive. Individually revoke required SELECT/INSERT/UPDATE, grant append-column UPDATE or TRUNCATE, assume table/schema/database/elevated owner role, weaken a named key/FK/CHECK/index, remove history, or use the wrong store: readiness must be false with all data unchanged. Record role must fail with any Core workflow DML; execution role must fail with decisions UPDATE/destructive DML, delegations/history DML, or HRM identity DML. Keep production roles unchanged until a separately reviewed permission release. Existing P23 tests and owner-only runs do not prove this successor.

## 27. Downstream limits

New submission3 does not imply approved-demand RFQ/PO/receiving compatibility. Those worker-based source consumers need explicit successors before use. No Finance ledger, payment or historical-exposure authority is supplied here.

## 28. Remaining implementation

Finish full mapped end-to-end successor workflow coverage, historical release/recovery and audit/downgrade marker review, metadata readiness integration and exact PostgreSQL readiness manifests. Focused and full affected domain/selected host regression, genuine migration generation and SQL/drift gates are complete as recorded above; private finite-role HTTP/PG proof and release review remain pending. Earlier migration artifacts are unchanged, the current snapshot reflects genuine P25 output, and production activation remains false. RFQ/PO/receiving conversion is not supplied by this milestone.
