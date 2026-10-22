# Core-principal purchase-request approval successor

**Current checkpoint:** genuine migration/recovery checks, the first finite-role
HTTP slice, and a private manual-evidence approval graph have passed; see section
26. The latter passed 31 HTTP requests, six exact replay/current-permission checks
and 35 raw storage denials, followed by an independent read-only review. The new
GET command acknowledgement has compiled and passed 43 new mapped cases within
a 67-case focused suite; its actual PostgreSQL/HTTP gate remains pending. P25 remains default-off and is not deployed. Finance/AP and
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

Compiled and mapped-tested successor (actual PostgreSQL/HTTP acceptance pending): `GET /api/procurement/v1/requests/{id}/approval-commands/{operation}/{commandKey}`, exactly `Open` or `Decide` and nonzero UUIDs. Its version-1 acknowledgement envelope contains `acknowledgement="Completed"`, operation/key/request/case/optional assignment/completion time and a strictly typed original result: case/request IDs, submitted/request/case versions, state/requestState/approvalState. Open has exactly those eight stored-result properties. Decide has exactly eleven: the common eight plus assignmentId, positive original assignmentVersion and decisionId, bound to the same retained assignment and actual immutable Core decision. These versions/state are historical acknowledgements, not current CAS or new action authority. No arbitrary stored JSON, financial value or command fingerprint is returned. PR3 only; unsupported old or unknown pin versions remain unavailable, never upgraded.

## 23. Atomicity and recovery

Keep current transaction coordinator, shared Core physical transaction, current record scope and original UUID command receipts. The tested runtime checkpoint preceding this authored adapter has no GET-by-command-UUID receipt endpoint. An ambiguous Open/Decide result can be reconciled by replaying its exact original POST key and payload, subject to the route's current authorization and evidence guards. GET detail/history shows workflow history; it does not acknowledge a particular command UUID. Durable client journaling and explicit receipt recovery remain release work. Historical receipt proof cannot authorize a new command or recipient transfer.

The authored GET adapter reads existing append-only receipts; it creates no journal, engine, migration or financial owner. Current canonical Core account, actual operation action (`approval-open` or `approval-decide`), company/branch/current-object and retained original-source scope are mandatory. The exact actor is included in the scoped receipt query. Another actor, missing UUID or wrong request receives no acknowledgement; `404` never proves that a command did not commit, and cannot authorize automatic replay/new UUID. Account/action revocation denies access. A still-authorized original actor can acknowledge past committed work after current spending authority, policy or Finance freshness changes; this does not authorize a new decision. The opener need not be the original request creator when the real command permitted another granted opener.

Open binds the original opening key to the immutable pin and Core case/source tuple. Decide additionally binds the actual immutable Core decision/command key/actor/assignment, retained assignment evidence and, for terminal results, the same-decision Procurement outcome. Original request/case/assignment versions are bounded above by today's monotonic versions without requiring equality or substituting current values. Stored result parsing is operation-specific and bounded, rejects duplicate/unknown fields, invalid IDs/versions/state tuples and mismatched bindings, and returns only the typed projection. The immutable pin/Core source business version is used; today's request business round/CAS or case state is not substituted. Both retained source and current object scope must be readable. Corrupt/unsupported/unknown-store evidence is unavailable, not a fabricated success.

Readiness is `Enabled` plus `PrincipalExecutionEnabled` and the registered actual owning read provider `IsReadyAsync`; missing/default Unavailable remains closed. It does not require an execution connection, `CanExecute`, current manual Finance policy, current financial limit or new HR employee identity. Actual PostgreSQL reads share one RepeatableRead READ ONLY snapshot for Procurement/Core case/identity, recheck the current actor and never SaveChanges, mutate/lock business rows or emit audit/outbox. Responses are `no-store`. Missing/transport/unavailable recovery stays uncertain until an actor-owned completed acknowledgement is obtained.

## 24. Pure acceptance

Authored: workerless Finance2, authority2 and PR3 positives; original alias/subject/business-version/amount tamper; explicit downgrade/mixed-version denial; assignment tuple/unknown/duplicate JSON denial and exact eight-decimal amount retention. Focused domain/model gate passed **191/191** at `c1301cc`; all corresponding dependency bytes remain exact at final API candidate `71e71aa`. Focused mapped/API gate passed **23/23** at `71e71aa` (no skipped cases), including workerless checker capture, custom-role finite-authority selection, retained alias/LineManager denials and bounded read SQL projections. Initial failed checkpoints are retained: 167/180 domain, then 22/23 host before the required needed-by fixture was corrected. The first post-generation full-domain checkpoint at `7672fa4` passed 1,249/1,252 and stopped before host/model/SQL: three legacy evidence fixtures lacked their owning request. Test-only correction `e23c73951b2ca9923e0c4c10f284100877f59299` seeds explicit legacy worker-owned sources, filters the intended evidence audit and adds a missing-source/no-effects negative. At that frozen application/test source, the full Procurement domain suite passed **1,253/1,253** and selected Procurement/shared HRM, handwritten-model, Loan and InvoiceUnitReview host cohort passed **586/586**, with zero skipped/failure/error counters. Both expected nonempty TRX cohorts are validated, not inferred from process exit. No actual PostgreSQL successor workflow success is claimed.

## 25. Required mapped acceptance

The acknowledgement adapter passed **43** focused mapped cases within the **67/67** selected acknowledgement/readiness/authority suite: original Open/Decide result after later request/case revisions, independent authorized opener, no execution connection/manual Finance policy, read-only provider dispatch, current action revocation/restoration, wrong actor/key/request/company/branch/locked account, default/off/missing read provider, strict corrupt/duplicate/unknown/unsupported projection denial and invalid operation/key. Each checks unchanged projected Core/Procurement/audit/outbox facts. These explicitly synthetic owner projection/readiness fixtures are not actual workflow, finite-role, PostgreSQL or Finance acceptance. Existing executed runtime receipts are not claimed to cover this new source.

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

### Acknowledgement compilation and mapped acceptance

At source `a7b3813fcd470dddc1573360440d5e46ed76efe4`, all **67** selected tests passed: 43 acknowledgement cases and 24 existing authority/readiness cases; zero failures, skipped or errors, with unchanged tracked source and no production access. Receipt: `/home/amizo/erp-backups/procurement-p25-command-ack-test-20261022/result.json`, SHA-256 `b706367c1580c4c6008c92db642a3f6e01430c9a727cd5ed0034afce9a30b4a9`. These synthetic mapped owner projections do not establish the actual Npgsql read-only transaction, finite-role GET or browser behavior. Independent source review corrected the initial eight-field-only decision parser to the actual eleven-field producer and added monotonic upper bounds before this test run. The subsequent actual twelve-GET runtime acceptance is recorded below; the mapped tests remain a separate evidence class.

### Actual actor-owned acknowledgement acceptance

At frozen source `e50344a4e02361c699a7c716d60668af629a3206`, the genuine private Program/finite-LOGIN/PostgreSQL harness passed **43 actual HTTP requests, including 12 GET acknowledgements**. It compared original Open before and after terminal approval, the exact eleven-field original Decide result, actor/scope/key linkage, missing-key and wrong-request404, current Open/Decide permission revocation403 and restoration, and default Unavailable503. Both acknowledgements remained available after the private host's manual policies were removed and ExecutionConfigured was set false; this is an options-only test, not proof that the actual execution connection was removed. Every GET required `no-store` and an unchanged complete logical store footprint. Absence still never proves noncommit.

The same run repeated the six exact POST replay/conflict/current-permission checks, all35 owner storage denials and genuine retained-graph Down55000 veto. Exactly two receipts, one decision/outcome and three successor events remained. Final preservation confirmed the old roles/memberships/database catalog, existing Docker objects, publication and runtime bytes unchanged. No production resources changed; the two new finite test LOGINs and private database remain retained.

| Evidence under `/home/amizo/erp-backups/` | SHA-256 |
| --- | --- |
| `procurement-p25-get-ack-http-20261022_run01/result.json` | `4f7ee7a586a329947918137a05c12908e7c8de22a849275a7cffc7766b647f4d` |
| `procurement-p25-get-ack-http-20261022_run01/probe-result.json` | `5dd86e1c2f09c8dc0fc9ac8f638cfee9dee28348a76bdcff8ddc473079340d72` |
| `procurement-p25-get-ack-http-20261022_run01/finally-preservation.json` | `28859c4a9b00f70e2e3ff314456e4c364df9257c3c78371a789f50908e14e36b` |
| `procurement-p25-independent-getack01-20261022/review-receipt.json` | `468600a9e5278d7fb21a4705a4566e3980bd541bb96cf403ff29742e323d7b90` |

Independent read-only owner review verified the exact private PostgreSQL identity/socket, frozen tracked source and all477 publication/runtime files; Approved request CAS4 versus business/submitted2; principal, submission, checker, assignment and request/Finance-snapshot digests; exact2 ×5.1234 =10.2468ZMW; six active Core users and zero workers; two bound original8/11-field receipts; one decision/outcome and three successor events. The negative request remained Submitted/version2. Finance-release, AP and payment facts were absent. This independently verifies retained facts and hash-bound runtime evidence; the reviewer did not replay the transient GETs, ACL changes, raw failures or downgrade. Native identity-provider authentication and external Finance authority remain unverified.

Still required: separate revoked spending-authority historical-read versus new-command denial, full finite-privilege matrix, retained/current alias and legacy compatibility, browser reload/interruption recovery and real identity-provider acceptance. Default registration and production execution remain unavailable.

### Actual finite-LOGIN privilege mutation acceptance

At source `61817758410547970d51f98e308aa8c74906e580`, the separate new private database passed **18 actual privilege mutation cases**, then the complete43-request approval/GET graph, six POST replay/current-action checks,35 storage denials and retained-graph Down veto. Record and execution connections were actual distinct restricted LOGINs, not owner `SET ROLE`. Each case verified the effective targeted privilege on the same connection as the real readiness provider, required the affected write modes to close, restored exact raw table/column ACLs and the complete logical footprint, and re-established both role baselines. Read readiness intentionally remains available for some write-only defects; no blanket all-modes-false claim is made.

Cases cover missing request/Core-permission SELECT, checker/decision INSERT, authority/assignment/request UPDATE; prohibited evidence/decision/identity column UPDATE; audit/outbox or case TRUNCATE; and forbidden Core case/delegation INSERT or decision DELETE. Six explicitly declared owner-only ACL initialization steps occurred before the new fixture baseline, preserving business rows, global roles/memberships and default privileges. This avoids equating original NULL ACLs with empty arrays after temporary grants. These are new-fixture prerequisites, not edits to accepted databases or production privileges.

| Protected evidence under `/home/amizo/erp-backups/` | SHA-256 |
| --- | --- |
| `procurement-p25-privilege-http-20261022_run01/result.json` | `be2aca899d3d0a924f74bf39acec4b5e453f34fa8af974fae156f9de5409a261` |
| Same run, `probe-result.json` | `cb6473734f14a8145cc7902acaf09a13e28edb50e0ff8bf62739ce3a1528c4fa` |
| `procurement-p25-independent-privilege01-20261022/review-receipt.json` | `0210be0229b97a6054e5b8f48b5912a34f555fcb66b753ee13e7adc0c5685ac0` |

Independent read-only owner review compared all477 publication/runtime files and frozen source, the five final prepared relation/column ACL projections, both finite role flags/no memberships/no ownership, and24 effective privilege expectations. The retained Approved graph, digests, exact amount, receipts and absence of Finance effects were verified again. The18 transient mutations and restoration assertions remain attributed to the hash-bound runtime, not independently replayed. No physical DDL/OID restoration or schema-defect matrix is claimed. The initial tool compile failure due to an ambiguous Core/HRM delegation type is retained; the explicit Core qualification changes no application guard or schema.

The controlled UI prerequisite is separately merged as UI PR56, main `30e17e9aad1bf86d3c94086b42fca590c9b0f05f`:98 focused unit cases, staged TypeScript/build and37 controlled Playwright cases passed. This establishes scoped locator/reload interaction behavior against controlled responses, not actual browser/API/identity-provider integration. P25 remains draft/default-off and undeployed.

### Actual schema, current-account and authority-revocation acceptance

Frozen source `5470deed2cafea204e2e27ab26a429914d6b7d48` passed a fresh private run with **59 actual HTTP requests**, the18 finite-LOGIN privilege cases, five schema defects, six current-account/selected-scope denials, six POST replay checks,35 storage denials and the retained-graph Down55000 veto. The59 requests include the original12-GET acknowledgement cohort and an additional historical Open acknowledgement after actual authority revocation. This is explicitly synthetic private authentication/manual evidence; it establishes neither native identity-provider acceptance nor external Finance authority.

Each schema case removed one real protection: receipt leaf primary key, scoped receipt/request foreign key, original-request principal CHECK, partial unique authority-origin index, or genuine P25 migration-history row. The restricted LOGINs retained actual SELECT while all affected read/record/execute readiness closed. Exact captured logical definitions, raw ACLs, history and role baselines were restored before proceeding. Physical DDL OIDs are deliberately not claimed to be restored. The schema verifier preserves the first failure and reports bounded secondary restoration failures rather than masking the original diagnostic.

Actual pending decisions were denied for inactive, locked, archived and foreign-tenant Core accounts, and for the two selected-scope header combinations; all temporary account fields and full persisted footprints were restored. The foreign-company case pairs that company with the original company's branch: this proves relationship/scope denial, **not** isolated authorization against an otherwise valid foreign-company/branch pair. The real constructor-only foreign entity and unassigned branch remain retained.

After accepting the original Approved graph, the harness created a second genuine pending request/assignment using the same active grant, invoked the actual public authority-revoke command, and restored only its temporary Core action grant. The spending grant remains **Revoked/version4**, with its original three immutable Created/Submitted/Activated identity rows and a separate revocation audit. The earlier actor-owned Open acknowledgement still returned200; a new decision on the second assignment returned409 `ApprovalAssigneeAuthorityUnavailable` without persisted changes. No owning SQL reactivated the authority. The first graph retains two receipts; the second retains one Open receipt and no decision/outcome. Final global receipt count is three, not two.

| Protected evidence under `/home/amizo/erp-backups/` | SHA-256 |
| --- | --- |
| `procurement-p25-boundary-http-20261022_run01/result.json` | `0d1b43bb701645a11c5dcd68c4694600a9f8fe3d603780610c4d9e49fa3dcf58` |
| Same run, `probe-result.json` | `6a42a7aa597adb76ddddb6ac34e7d6a95a4903d84db985ec0f4df1b8c26e63e8` |
| Same run, `finally-preservation.json` | `28859c4a9b00f70e2e3ff314456e4c364df9257c3c78371a789f50908e14e36b` |
| `procurement-p25-independent-boundary01-20261022/review-receipt.json` | `eb1e024ffc6f779e06b334ed6a53fe23a40ac11a45e9d2699fef7e19fc3fe49c` |

Independent read-only owner review bound the exact private cluster/socket, frozen source, all477 publication/runtime files, final schema definitions/history, prepared ACLs and24 effective finite-privilege expectations. It recomputed the first graph's principal/submission/checker/assignment/request snapshot digests and exact10.2468ZMW amount, observed both graphs, restored six Core accounts, absent temporary revoke grant and the durable revocation audit. Transient HTTP, privilege/schema mutations, raw denials and downgrade remain hash-bound runtime evidence, not independent replays. The reviewer retained a preparation-only Python quote error before execution; no SQL review errors or mutations occurred. All original roles, memberships, database catalog entries, Docker resources and publication bytes remained preserved. P25 remains draft/default-off and undeployed; alias/legacy, normal authentication/browser and release gates remain outstanding.

### Actual retained-alias and historical-requester offboarding acceptance

Frozen source `bb932c5b7e47016270014bb6c3bf42bae1f61fac` passed a separate fresh **54-request actual HTTP** cohort: the original43 requests plus11 retained-alias/offboarding requests. This deliberately did not repeat the already accepted18 privilege mutations, five schema defects, six account/scope cases, final authority revocation,35 current-graph raw denials or Down execution; those remain separately bound boundary evidence.

After proving the original workerless graph, the harness added one explicitly synthetic contingent HR worker and linked the synthetic requester **before** creating a new actual request. It then cleared that current link and terminated/archived the worker, preserving the request's original Core-worker identity. Temporarily linking the checker to that retained worker denied Open with409 `FinanceEvidenceVerifierInvalid`; temporarily linking the assigned approver denied its preview actions and Decide with409 `ApprovalIdentityChanged`. All denied commands/detail reads preserved the full footprint. After restoring the independent checker/approver, an actual decision succeeded even while the original requester account was inactive and the historical worker unavailable. No real account or employee was changed.

First-error-preserving finally checks restored all three temporary Core account changes and the synthetic worker's declared baseline, refusing unknown concurrent states. Original row hash multiplicities were preserved; the additional worker and approved graph remain retained. The authority remains Active/version3 here, because the separately accepted irreversible revocation cohort was not repeated. Both approved graphs retain their own two original command receipts and three successor events; global totals are two cases/decisions/outcomes, four receipts and six successor events. This is new principal lineage, not genuine old worker-v2 approval execution.

| Protected evidence under `/home/amizo/erp-backups/` | SHA-256 |
| --- | --- |
| `procurement-p25-alias-http-20261022_run01/result.json` | `2022079a5880cd73e871b573661e554ca499af05f8b3da65ee0bf15d5f3e5479` |
| Same run, `probe-result.json` | `325a170cf37c5fc718ed751f052445d84e992768852ae976ca77fe62566ffe7f` |
| Same run, `finally-preservation.json` | `28859c4a9b00f70e2e3ff314456e4c364df9257c3c78371a789f50908e14e36b` |
| `procurement-p25-independent-alias01-20261022/review-receipt.json` | `eaefc9f4bde908921f567ec47d793e5802a65132eb149465c4e2434b783f1625` |
| Same independent review, `supplement.json` | `67f306102874236daf341cda6e9caa81833702106ad8b1d9114dd96d0207d604` |

Independent read-only owner review verified the exact private source/cluster/socket, SDK source snapshots, all477 publication/runtime files, finite roles without elevation/membership/ownership, three retained requests and both approved graphs. It recomputed original principal, submission, checker, assignment and raw snapshot digests, and independently verified each2 ×5.1234 =10.2468ZMW source. All six current Core accounts had null worker links, while the one genuine synthetic worker remained active/unarchived with no SubjectId; the new request/pin retained its original worker, and the pin maker, checker2 and assignment2 aliases remained null. Finance/AP/payment owning facts remained absent. Temporary alias/offboarding outcomes and exact-restoration/no-effect claims are hash-bound runtime evidence, not independent reruns. Normal local-cookie/browser, external IdP, legacy execution and production release are not established by this cohort. P25 remains draft/default-off and undeployed.

### Actual normal-cookie UI attempt — partial CRUD only; logout release hold

Frozen API source `5cb3d614dee9284aabdac219b3e28ee9d795eb56` and UI test source `b7ac2c740446d2a8dc93eae21a7cc0848be5bf43` ran against a fresh private PostgreSQL database using the real local password/session-cookie scheme, an isolated HTTPS transport and the unchanged staged application bundle. No private subject header authenticated that host (actual401 control).

The browser exercised real login, workerless Core context, request create/version1, edit/version2, reload, withdrawal/version3 and an ungranted account's403 denial. **The one browser scenario FAILED at logout: zero passes, one failure, no retries/skips.** Existing logout inherits the HRM staff default policy, blocking a valid non-HRM Core account. Both synthetic sessions remained unrevoked. This is a Core session-policy defect to repair; CRUD success does not make the whole scenario pass.

Independent read-only owner inspection verified all477 published/runtime files and before/after row-digest multiplicities for all88 Procurement tables. Original rows were preserved; the browser added exactly one PR, one line and seven audit rows. The third request is Withdrawn/version3 with exact quantity3.5678 × unit price10.2345 =36.51464910ZMW, no worker link, and no approval or Finance-owning facts. Six explicitly activated synthetic accounts were used; no real user, production system or external identity provider was touched.

Protected evidence under `/home/amizo/erp-backups/`:

| Receipt | SHA-256 |
| --- | --- |
| `procurement-p25-cookie-browser-20261022_run03/result.json` (FAILED) | `f2e036bfc7ddd6ab7c05d4694013bb9406770fc137bb647dcfa0f48ae676a4f2` |
| `procurement-p25-local-cookie-http-20261022_run03/result.json` (43 setup requests + separate401 control; not browser acceptance) | `970f536860a69eceab4ebf621de860b3ca1f1895ba18da6cee99077ee7cabcb3` |
| `procurement-p25-independent-local-cookie-owner-prep-20261022/partial-review-receipt.private.json` | `0d8e003477ed58138faca9fd8f58e11a8fc1dff51a3784ab3773d4b577c4f817` |
| Same directory, `before-browser-procurement-row-digests.private.json` | `8de837b3050139d197fa9244c99d5f46fb56ee549cd22bfb4aebbb10899413d4` |

Earlier failed fixture preparation and the later disk preflight abort are retained. The preflight abort created no database; its exact immutable runtime copy was verified and reused read-only by the final attempt. The baseline capture's first read-only catalog check rejected the owned `__procurement_migrations` table; its source/diagnostic were retained and the exact table was allowlisted before a successful baseline capture and before any browser actions. No failed run is relabelled successful. P25 remains draft/default-off and undeployed.

## 27. Downstream limits

New submission3 does not imply approved-demand RFQ/PO/receiving compatibility. Those worker-based source consumers need explicit successors before use. No Finance ledger, payment or historical-exposure authority is supplied here.

## 28. Remaining implementation

The actual finite-role PostgreSQL/HTTP GET acknowledgement acceptance has passed as recorded above. The controlled durable browser journaling/reload prerequisite and the actual schema/current-account/revocation checks above have passed within their stated limits. Complete real identity-provider/browser acceptance, any remaining scoped ownership variants, remaining legacy compatibility negatives, and release review. The bounded private finite-role approval graph, replay/current-action denial and 35-case graph storage proof have passed as recorded above; they do not replace those remaining gates. Earlier migration artifacts are unchanged, the current snapshot reflects genuine P25 output, and production activation remains false. RFQ/PO/receiving successors and future Finance-owned budget/AP/payment integrations remain separate work.
