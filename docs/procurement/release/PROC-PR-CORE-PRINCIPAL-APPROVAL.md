# Core-principal purchase-request approval successor

**Current checkpoint:** genuine migration/recovery checks, the first finite-role
HTTP slice, and a private manual-evidence approval graph have passed; see section
26. The latter passed 31 HTTP requests, six exact replay/current-permission checks
and 35 raw storage denials, followed by an independent read-only review. The new
GET command acknowledgement is a subsequent source-only increment with 43 authored,
unrun mapped cases. P25 remains default-off and is not deployed. Finance/AP and
payments belong to the future Finance module. No external funding or payment
authority, real identity-provider acceptance or whole-module completion is claimed.

## 1. Status and provenance

P25 source integration starts from published ERP main `b6b62cd551ad134879cc47c7ceb32a0570201aed` (accepted P24). The prepared source `d2c75e0` was normally merged on isolated branch `codex/procurement-p25-principal-integration`. Their common ancestor is `dbf5107`; the only incoming commits absent from main are the ten bounded P25 source commits `be32829` through `d2c75e0`. No held ancestors outside that already published common history were imported. The automatic merges retain published P23/P24 fixes, current Core scope, recurring CRUD, and actual document/readiness corrections.

At the initial source-only checkpoint, all **141** existing backend migration/designer/snapshot artifacts were byte-identical to that main baseline (43 belonged to Procurement). Path-plus-SHA256 inventory digest: `f3e06c1f81f5b45efe63cec0481151c97cd7803a976e4adbbe83d4bb0fbc3152`. Subsequent genuine P25 generation and private acceptance are recorded in sections 19 and 26; current executable evidence is not inferred from earlier P23/P24 acceptance. Published main documentation through `c21190b` was normally merged after private acceptance, without executable changes.

## 2. User outcome

A currently authorized named Core user may submit a request, obtain an independent funding check and receive an independent approval without an employee record. Optional HR metadata conveys no permission.

## 3. Existing deficiency

PR capture is principal owned, while operational submission2, Finance checker1, spending identity1 and assignment1 require workers. Replacing one resolver would leave contradictory retained evidence.

## 4. Ownership

Core owns accounts, current grants, organisation and approval cases. Procurement owns source attribution, funding references, spending limits and request effects. Reuse the existing Core approval engine and scope projection.

## 5. Bounded delivery

Source checkpoints add explicit successor contracts/model guards, current Core actor bridges, new-only request provenance and funding capture, and version-dispatched authority/approval commands and hints. Production registration is still Unavailable; PrincipalExecutionEnabled defaults false. Existing manual CRUD remains usable without operational provenance when unavailable. The private host explicitly composes the actual captured provider for acceptance, as recorded in section 26; this does not change production registration.

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

The authored Down starts with ordinary transactional ACCESS EXCLUSIVE locks over the five evidence/source tables plus audit and outbox, then refuses any new request provenance, Finance2, authority2, submission3, assignment2 or corresponding durable audit/outbox evidence with SQLSTATE `55000`. It must not erase aliases, fill fake workers or cast a new receipt into an old version. Two EF-generated reverse zero-UUID defaults were removed: the first guard rejects nullable successor aliases before restoring the original NOT NULL columns. The actual unused PostgreSQL downgrade and reapplication passed as recorded below. Fifteen independent synthetic audit/event marker downgrade veto cases subsequently passed as recorded in section 26; five row-family veto cases remain pending.

## 21. Readiness and activation

Execution requires a default-off explicit principal-approval option plus owning-context exact successor schema and finite execution role. IsReady covers reads; CanRecord covers Procurement-only funding/configuration writes; CanExecute additionally covers the coordinated finite Core workflow role. Metadata principalApprovals is a separate optional capability computed on the actual configured execution context, never the normal read context. Current production registration is Unavailable for all three checks. Missing/unknown schema is unavailable. Old providers must dispatch exact old/new manifests rather than accepting a broader CHECK by implication.

## 22. API compatibility

No client supplied identity/schema selector is accepted. Basic legacy numeric/exact request wire remains unchanged. Existing original worker receipts retain their bytes. New metadata is omitted from legacy audit representations where required; new versions use explicit successor payloads.

Authored successor (source only, uncompiled/unrun): `GET /api/procurement/v1/requests/{id}/approval-commands/{operation}/{commandKey}`, exactly `Open` or `Decide` and nonzero UUIDs. Its version-1 acknowledgement envelope contains `acknowledgement="Completed"`, operation/key/request/case/optional assignment/completion time and a strictly typed original result: case/request IDs, submitted/request/case versions, state/requestState/approvalState. Open has exactly those eight stored-result properties. Decide has exactly eleven: the common eight plus assignmentId, positive original assignmentVersion and decisionId, bound to the same retained assignment and actual immutable Core decision. These versions/state are historical acknowledgements, not current CAS or new action authority. No arbitrary stored JSON, financial value or command fingerprint is returned. PR3 only; unsupported old or unknown pin versions remain unavailable, never upgraded.

## 23. Atomicity and recovery

Keep current transaction coordinator, shared Core physical transaction, current record scope and original UUID command receipts. The tested runtime checkpoint preceding this authored adapter has no GET-by-command-UUID receipt endpoint. An ambiguous Open/Decide result can be reconciled by replaying its exact original POST key and payload, subject to the route's current authorization and evidence guards. GET detail/history shows workflow history; it does not acknowledge a particular command UUID. Durable client journaling and explicit receipt recovery remain release work. Historical receipt proof cannot authorize a new command or recipient transfer.

The authored GET adapter reads existing append-only receipts; it creates no journal, engine, migration or financial owner. Current canonical Core account, actual operation action (`approval-open` or `approval-decide`), company/branch/current-object and retained original-source scope are mandatory. The exact actor is included in the scoped receipt query. Another actor, missing UUID or wrong request receives no acknowledgement; `404` never proves that a command did not commit, and cannot authorize automatic replay/new UUID. Account/action revocation denies access. A still-authorized original actor can acknowledge past committed work after current spending authority, policy or Finance freshness changes; this does not authorize a new decision. The opener need not be the original request creator when the real command permitted another granted opener.

Open binds the original opening key to the immutable pin and Core case/source tuple. Decide additionally binds the actual immutable Core decision/command key/actor/assignment, retained assignment evidence and, for terminal results, the same-decision Procurement outcome. Original request/case/assignment versions are bounded above by today's monotonic versions without requiring equality or substituting current values. Stored result parsing is operation-specific and bounded, rejects duplicate/unknown fields, invalid IDs/versions/state tuples and mismatched bindings, and returns only the typed projection. The immutable pin/Core source business version is used; today's request business round/CAS or case state is not substituted. Both retained source and current object scope must be readable. Corrupt/unsupported/unknown-store evidence is unavailable, not a fabricated success.

Readiness is `Enabled` plus `PrincipalExecutionEnabled` and the registered actual owning read provider `IsReadyAsync`; missing/default Unavailable remains closed. It does not require an execution connection, `CanExecute`, current manual Finance policy, current financial limit or new HR employee identity. Actual PostgreSQL reads share one RepeatableRead READ ONLY snapshot for Procurement/Core case/identity, recheck the current actor and never SaveChanges, mutate/lock business rows or emit audit/outbox. Responses are `no-store`. Missing/transport/unavailable recovery stays uncertain until an actor-owned completed acknowledgement is obtained.

## 24. Pure acceptance

Authored: workerless Finance2, authority2 and PR3 positives; original alias/subject/business-version/amount tamper; explicit downgrade/mixed-version denial; assignment tuple/unknown/duplicate JSON denial and exact eight-decimal amount retention. Focused domain/model gate passed **191/191** at `c1301cc`; all corresponding dependency bytes remain exact at final API candidate `71e71aa`. Focused mapped/API gate passed **23/23** at `71e71aa` (no skipped cases), including workerless checker capture, custom-role finite-authority selection, retained alias/LineManager denials and bounded read SQL projections. Initial failed checkpoints are retained: 167/180 domain, then 22/23 host before the required needed-by fixture was corrected. The first post-generation full-domain checkpoint at `7672fa4` passed 1,249/1,252 and stopped before host/model/SQL: three legacy evidence fixtures lacked their owning request. Test-only correction `e23c73951b2ca9923e0c4c10f284100877f59299` seeds explicit legacy worker-owned sources, filters the intended evidence audit and adds a missing-source/no-effects negative. At that frozen application/test source, the full Procurement domain suite passed **1,253/1,253** and selected Procurement/shared HRM, handwritten-model, Loan and InvoiceUnitReview host cohort passed **586/586**, with zero skipped/failure/error counters. Both expected nonempty TRX cohorts are validated, not inferred from process exit. No actual PostgreSQL successor workflow success is claimed.

## 25. Required mapped acceptance

The acknowledgement adapter authors **43** focused mapped cases (UNRUN): original Open/Decide result after later request/case revisions, independent authorized opener, no execution connection/manual Finance policy, read-only provider dispatch, current action revocation/restoration, wrong actor/key/request/company/branch/locked account, default/off/missing read provider, strict corrupt/duplicate/unknown/unsupported projection denial and invalid operation/key. Each checks unchanged projected Core/Procurement/audit/outbox facts. These explicitly synthetic owner projection/readiness fixtures are not actual workflow, finite-role, PostgreSQL or Finance acceptance. Existing executed runtime receipts are not claimed to cover this new source.

Still required: canonical workerless maker/checker/approver positives; stale claims, revoked/locked/archived/wrong-tenant/wrong-branch accounts; all retained/current alias combinations; original maker offboarded; finite limits/overlap; LineManager unavailable; no mutation on denied commands.

## 26. Required actual acceptance

Genuine migration source has been generated and independently reviewed. The `e23c739` post-generation gate rebuilt Infrastructure, reported no pending model changes, and emitted genuine P24→P25 Up, P25→P24 unused-only Down and idempotent Up SQL. All tracked source and 143 migration/designer/snapshot artifacts remained byte-identical throughout the gate; no SQL was applied and no publications were made. Evidence: `/home/amizo/erp-backups/procurement-p25-post-generation-20261022/20261022_162515/result.json` and its two TRX files. SQL SHA-256: Up `2ed7b6d2dc090adfed0fc712483aabb9bae5e5b06546a0831e72468df3e44fba`; Down `1b298d0a0fb8ccd42ab1f0b93260ef02d89896c1a21b3209728286e75492ba1c`; idempotent `0aa6c7cdd0de23ddfb59bd7179591cc48ba20af99be11784b8b0f18bf98c553c`. The subsequent isolated PostgreSQL17 basic capture passed at `/home/amizo/erp-backups/procurement-p25-private-capture-20261022_browser03/result.json`, SHA-256 `99990a425f07fc1f3c8135bd1b3f116ded9e6e8e3f140a366a149777408737fd`. Genuine fresh and used Up, twice-repeated idempotent Up on each, unmodified unused Down and re-Up passed. Existing data, catalog, history, role and object privileges were preserved; the seven newly dropped/re-added columns alone moved by their exact table-specific physical ordinal count. The original accepted P24 browser database, including its workerless Closed version-4 record and ordered creation/update/closure audits, remained unchanged. PostgreSQL template cloning required reproducing only the new clone's database-level ACL; full clone equality was mandatory before migration, with no source or global-role changes. Earlier failed checks and databases are retained, not counted as passes.

The actual isolated durable-marker negative suite passed all 15 independent cases (five generic audit markers, two explicit audit actions, seven versioned event types and one payload-only marker). Each required the original downgrade guard's SQLSTATE `55000` and exact message, followed by complete unchanged scratch data/catalog/history/sequences/privileges; the retained source also remained unchanged. These are synthetic transactional fixtures, not positive workflow evidence. For safety, the original Down statements remained in order with an additional `P0001` sentinel before the original COMMIT, preventing persistence if the expected guard failed. This instrumented negative suite must not be described as literal unchanged Down execution; the earlier unused Down ran the original SQL. Receipt: `/home/amizo/erp-backups/procurement-p25-durable-markers-20261022_review01/result.json`, SHA-256 `0062a36e6eef079ce52d9d2c4956b86f1bb7a1268943cf3273fd9ba2b44a8eb2`.

The unregistered read-only readiness candidate now contains the actual migration ID, 17 literal PostgreSQL CHECK definitions and three exact partial-index predicates for its selected model. No expression normalization or production registration is introduced. The manifest itself grants no readiness: real schema, current connection and finite-role checks still apply. Five row-family used-evidence veto cases, raw NULL/zero/CHECK/FK negatives, actual finite-role HTTP pipeline and independent persisted effect proof remain required. At source `b871caff22c65deae0a8f5afc1a28e6d13b96572`, all four focused captured-manifest/readiness tests passed (zero skipped/failure/error; tracked source unchanged). Receipt: `/home/amizo/erp-backups/procurement-p25-captured-manifest-test-20261022/result.json`, SHA-256 `d2e8f5d6d0393d6f1e9d348aac96933d3a1e7da3f84a75c89e68aeb08887515e`. These tests prove complete selected-model key coverage and unavailable-store/default-provider denial; they do not establish finite-role or positive workflow readiness. Prove separate finite read, Procurement-only record, and Core workflow execution roles positive. Individually revoke required SELECT/INSERT/UPDATE, grant append-column UPDATE or TRUNCATE, assume table/schema/database/elevated owner role, weaken a named key/FK/CHECK/index, remove history, or use the wrong store: readiness must be false with all data unchanged. Record role must fail with any Core workflow DML; execution role must fail with decisions UPDATE/destructive DML, delegations/history DML, or HRM identity DML. Keep production roles unchanged until a separately reviewed permission release. Existing P23 tests and owner-only runs do not prove this successor.

### Current integration corrections

At source `9744dbc93651d84e01d3a523ddfbe4166c26605a`, request-approval readiness previews use the configured execution coordinator to inspect its finite execution connection. The ordinary read/record connection does not acquire Core workflow write grants merely to show the preview. Metadata retains the separate current manual-evidence-policy requirement, and actual commands recheck all record, actor, authority and funding guards.

The PR spending-authority people selector uses current Core account candidates only when the successor option and owning-store read readiness both pass. Workerless recipients need the explicit current approval action and selected company/branch reach; role labels confer no substitute authority. PO/RFQ selectors keep their existing legacy branch. All **24** focused readiness and spending-authority mapped-route tests passed with zero skipped/failure/error and unchanged tracked source. The four new activation/readiness combinations also check missing employee metadata, current action revocation, wrong branch and unchanged PO lookup behavior. Receipt: `/home/amizo/erp-backups/procurement-p25-principal-lookup-test-20261022/result.json`, SHA-256 `ee45118d89f7b106e496df0668064853eccf2463127140b0bd8b16c073878d82`. Mapped schema doubles remain explicitly synthetic; actual finite-role and configured-connection runtime acceptance is still required.

### First actual finite-role HTTP checkpoint

At source `010870a1498c5afd568f89be5fd0987cf2a0100a`, the actual Program host passed **eight HTTP requests** against a fresh isolated PostgreSQL17 database. Real restricted record/execution LOGINs passed their distinct readiness checks; the record role could not execute Core workflow writes, the execution role could not masquerade as the record-only role, and the bootstrap owner was rejected as finite readiness. The private host retained default Unavailable registration in its first phase, then used the actual captured readiness provider in a separately labelled test composition.

The current Core account had no employee. Request create/submit persisted original identity version 1, Submitted business/current version 2, quantity 2 and exact unit price 5.1234. Four owner-only negative storage probes each established a valid one-row baseline, received the actual named CHECK violation and restored the full footprint. Missing funding/checker evidence and missing manual-evidence policy blocked opening; an ungranted account was denied. Independent owner reload inside the tool confirmed no budget checker, submission pin, assignment, Core case, AP hand-off or payment record. Normal identity-provider authentication is **not** proved: the fixture replaces only authentication with an allowlisted subject and retains real current Core grants, scope, authorization and actual database roles.

Actual receipt: `/home/amizo/erp-backups/procurement-p25-http-first-20261022_run02/result.json`, SHA-256 `dc5cfd0987be15c383ee976ee20109f5a0023576a516cab80bc89f3c78945c81`. It binds the probe (`ea3bf3a4f4f20ce266398c9f2d77a7dd92180fa856d8724b0ae72611f2b78f2c`), successful frozen SDK receipt and all 477 published files. Final preservation (`ab733a0d9bb1739aacf7f620df576ad61ef2eaa84b3227068f6e63e9c2fb3574`) confirmed the original roles/memberships/database catalog, existing Docker objects, publication and runtime-copy bytes unchanged. Exactly two new test LOGINs and the new private database are retained; no production resources changed.

A separate actual read-only owner review independently recomputed the original principal digest, verified Submitted/current business version 2, quantity 2 × 5.1234 = 10.2468 ZMW, six active Core users and zero workers. Four scoped request/line audit rows identify the actual maker. Checker, submission, assignment, Core case, AP hand-off and payment tables were empty. It also compared tracked source to the exact source commit archive and both complete publication/runtime trees to the manifest. Receipt: `/home/amizo/erp-backups/procurement-p25-independent-first-http-20261022/review-receipt.json`, SHA-256 `6cebb104590c15cfd0c8bed8113ff1e8fdc4dfa4be770d8549a1e080ae956942`. A failed initial read-only audit ordering query and its corrected query are retained; no database mutation was performed by the reviewer.

Earlier checkpoints are retained: the initial harness build missed a test-host import; the first runtime passed finite-role/default-provider checks but request creation failed because its fixture role lacked the two catalog SELECT dependencies used by the real route. The correction added those two reads only to the test record role; it changed no production grants or business guards. A fresh database was used for the passing run.

That first checkpoint proves create/submit/unavailable-funding only. The subsequent positive graph below provides additional bounded evidence; neither checkpoint is a production release or whole-module acceptance.

### Actual private manual-evidence approval and storage checkpoint

Source `bc7d58995af6b0294894b537c5c35b43a6da7c60` passed **31 actual HTTP requests** against a fresh private PostgreSQL17 database. Independent Core users created and activated finite spending authority, published buying policy and activated a category through existing APIs. A workerless requester submitted classified demand; a distinct authorized checker recorded explicitly synthetic manual evidence under the private policy reference `PRIVATE-P25-MANUAL-EVIDENCE-NOT-EXTERNAL-FINANCE`. The actual configured execution role opened submission3/assignment2 and a distinct custom-role Core user approved it. Current principal/permission/scope and original evidence checks remained active; no employee or literal manager-role label supplied authority.

All **six** original-key replay, changed-body conflict and current action-revocation cases passed. Same-key/body Open and terminal Decide returned their exact original responses without effects; changed bodies returned the exact key-conflict code. Removing the actual stored Open/Decide action denied even an old-key replay. Exact Core grants were restored, and each command preserved the complete logical store footprint. The detail GET also preserved its footprint and exposed the correct action without granting workflow DML to the read role. At source `3d1282a`, the corresponding 24 focused API/readiness tests passed with zero failures, skipped or errors; the subsequent `bc7d589` delta changes only raw-test input/diagnostics.

The same completed graph passed **35 owner-only storage probes: 34 named CHECK violations and one NOT NULL violation**. Every probe first updated exactly one valid baseline row, then received the expected error and rolled back to the original complete footprint. The genuine pinned Down SQL refused the retained dependent graph with its exact `55000` guard. These are not independently isolated family fixtures: their count remains **zero**, and owner-only raw probes do not prove authorization or Finance authority.

| Protected evidence | SHA-256 |
| --- | --- |
| `procurement-p25-http-positive-20261022_run05/result.json` | `2b5f693d3541e811a6e569a6a5a033d36aa1b3cd22a4aca40f6d7401455e4da5` |
| Same run, `probe-result.json` | `3916bf9b4692d26e20f5b16184a01a35d085c225b093da7cb754c3364fd95063` |
| Same run, `finally-preservation.json` | `28859c4a9b00f70e2e3ff314456e4c364df9257c3c78371a789f50908e14e36b` |
| `procurement-p25-independent-positive05-20261022/review-receipt.json` | `95c9bdf25482ce5e786aefbc207eb4eabd41cc4236cd2aea843ef0f47ab44c10` |

All paths are under `/home/amizo/erp-backups/`. The accepted launcher binds the successful frozen SDK receipt and all 477 publication/runtime files. Existing roles, memberships, database catalog, Docker objects and publication bytes were preserved; only the new private database and its two finite test LOGINs were added. No production resources changed.

The independent reviewer used actual read-only owner queries and independently recomputed original principal, submission, checker, assignment JSON and request/Finance snapshot digests. It verified Approved CAS4 versus submitted business version2, quantity2 × unit price5.1234 = **10.2468 ZMW**, six active Core users, zero workers/aliases, exact authority actor lineage, one Core decision/outcome, two receipts and three version2 events. The original negative request remained Submitted/version2. Finance release, AP hand-off and payment facts remained absent. Authority identity digest shape/lineage was reviewed, not independently recomputed; the rolled-back 35 denials, six replay checks and Down veto are hash-bound runtime evidence, not independent reruns.

Failed runs remain retained. Runs01/02 stopped at readiness because their test demand lacked the category required by the existing spending evaluator. Run03 exposed the read-preview/execution-role mixup corrected in `3d1282a`. Run04 completed approval/replay but stopped on a raw test expecting CHECK before an existing NOT NULL rule; `bc7d589` targets empty text instead to test the canonical-principal CHECK. No business, identity or database guard was weakened to obtain the passing run. Earlier SDK/fixture failures and two corrected read-only reviewer query errors are also retained.

### Required acknowledgement actual acceptance (pending)

After source review/compile, extend the genuine finite-role Program harness to GET the original Open and Decide UUIDs before/after terminal effects, compare typed original result and full store footprint, revoke/restore actual operation grants, prove wrong-principal/scope/key absence and retain uncertain404 semantics. Revoke current spending authority separately and prove historical acknowledgement remains read-only while a fresh mutation is denied. Use the actual read role with no Core workflow DML or execution connection/manual-policy requirement; keep normal default registration unavailable until separately reviewed activation. Corrupt/unknown response/schema/store cases must never return completed acknowledgement. Actual snapshot/no-write and interrupted-response UI recovery acceptance remain pending; no browser changes are supplied by this source increment.

## 27. Downstream limits

New submission3 does not imply approved-demand RFQ/PO/receiving compatibility. Those worker-based source consumers need explicit successors before use. No Finance ledger, payment or historical-exposure authority is supplied here.

## 28. Remaining implementation

Compile and execute the newly authored actor-owned GET acknowledgement acceptance; keep its mapped/runtime/UI holds explicit. Complete durable browser journaling/reload recovery, real identity-provider/browser acceptance, the full finite-privilege revocation/widening matrix, actual retained/current alias and legacy compatibility negatives, and release review. The bounded private finite-role approval graph, replay/current-action denial and 35-case graph storage proof have passed as recorded above; they do not replace those remaining gates. Earlier migration artifacts are unchanged, the current snapshot reflects genuine P25 output, and production activation remains false. RFQ/PO/receiving successors and future Finance-owned budget/AP/payment integrations remain separate work.
