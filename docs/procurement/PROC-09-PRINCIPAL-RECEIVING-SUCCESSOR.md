# PROC-09 principal receiving successor — schema 2

**Status:** SOURCE DESIGN / bounded implementation in progress; default off. No DDL, tests or runtime acceptance claimed. Base: PO `2015a51111cf9432b1ff6b0ef1077f504130f58b`; subsequent reviewed retained-issued loader integrates by ordinary merge. Parent [Receiving and Inspection](PROC-09-RECEIVING-INSPECTION-AND-RETURNS.md). Standard GW-ENG-STD-001; Tier3. Existing UI is reused, not redesigned.

## 1. Feature Identification

PROC-09-PRINCIPAL: receive and inspect actual goods and certify actual service completion against a retained principal/schema2 issued purchase order. Procurement owns commercial receipt/acceptance evidence; Core owns identity/permissions/sites.

## 2. Purpose and Business Outcome

An authorized current Core account can create/revise/record a goods draft, make its independent configured inspection disposition, accept eligible goods, or certify a service deliverable without manufacturing an HR employee. Complete immutable observations and acceptance evidence reconcile exact remaining PO quantities and values.

## 3. Scope and Exclusions

Includes schema2 goods/service draft/read/revise/cancel, actual physical record, configured independent inspection qualification/rejection, positive acceptance/certification, scoped command recovery and bounded commercial return-request tracking. Excludes supplier dispatch automation, stock/asset/Finance/AP ledgers, invoices constituting acceptance, negative ledger effects without real owner consumption/compensation, and reinterpretation of worker/schema1 policy or records.

## 4. Parent and Related Features

Reuses PROC-08 principal-issued PO/submission/funding/Core decisions/source binding and witnessed supplier acknowledgment; PROC-03 governed policy publication, Core current accounts/actions/branches, existing exact decimal arithmetic and existing transaction/lease machinery. PROC-11 consumes a later explicit acceptance bridge, never a caller-complete flag.

## 5. Actors and Participants

Current receiver, configured named inspector, goods acceptor, named service certifier and document reader are canonical Core accounts. Nullable Core worker links are retained denial-only aliases for known-person separation; HR employee activity never grants access. Historical offboarding does not erase original signer evidence.

## 6. Entry Points and Triggers

Reuse existing imported purchase-receives, inspection and service surfaces and shared controls. New-source discovery consumes only stable schema2 issued lines in current Core scope. Metadata advertises actual operation availability only after owning schema/configuration proof; old generic receiving capability remains unchanged/off until its UI adapter is compatible.

## 7. Preconditions

Current active unlocked Core account/action and real company/branch reach; exact retained issued2 graph including independent approved Core PO case/decision/funding/source pins and Accepted witnessed acknowledgment. That acknowledgment is NOT goods delivery. Recording additionally requires actual observed receipt/delivery note/evidence, site and quantities; service certification requires deliverable and period evidence. New effects require independently published receiving schema2 policy/current lineage.

## 8. Workflow

Goods: draft create/revise → record immutable physical observation → configured independent inspection disposition when required → explicit positive acceptance. Qualified/held/rejected quantities remain distinct. Service: draft completion description/period/quantity → independent configured certification → positive acceptance. Command receipt, business result, counters, audit and handoff declaration commit in one enlisted transaction.

## 9. Status and State Transitions

Goods header Draft→Recorded (cancel Draft→Cancelled); inspection Pending→Passed/PartiallyPassed/Rejected exactly once per observation in first slice. Acceptance does not rewrite physical receipt or inspection. Service Draft→Certified (or Draft→Cancelled). Return request Draft→Cancelled only in bounded tracking slice; Requested/Approved/Dispatched/reversed are unavailable until an authoritative owner integration is implemented. No fake successful dispatch.

## 10. Permissions and Data Scope

Reuse procurement-receipt-view/create/edit/record/inspect/accept and procurement-service-certify. Core proves principal, grants and explicit branch/site; no UI-selected department grants. Unsupported department-scoped execution denies rather than borrowing HR masters. Assignment alone does not grant an action. Historical GET/recovery checks current original-operation access and retained scope, not current policy/catalog/employment eligibility.

## 11. Business Rules

Reuse ExactDecimalArithmetic and18,4 quantity /38,8 computed value. Observation records physical excess truthfully, but positive accepted quantity cannot exceed actual qualified/nonrejected physical capacity or remaining issued authorization/configured tolerance. No guessed tolerance: schema2 policy requires explicit conditions. Service total accepted quantity/value cannot exceed exact issued scope. Each durable effect/key applies once; full history including inactive/legacy unknown evidence must reconcile before positive writes.

## 12. Data Fields and Ownership

Use additive principal/schema2 owned source, draft/observation/inspection/acceptance/receipt representations. Stable source pins bind tenant/company/branch, PO/issued/submission/funding/ack/line/PR source, immutable hashes, classification, unit/currency/quantity/net price. Source state holds explicit CAS/sequence and sums. Principal+nullable alias is stored at each consequential action. No user/employee/vendor master duplication or reconstruction from current prices. Existing v0/v1 rows/bytes/FKs/readers remain unchanged.

## 13. Screens and Data Views

Reuse imported list/detail/editor/inspection/service components. Show ordered, observed, rejected, qualified, held, accepted and remaining separately using exact strings. Purpose-only tasks expose assigned evidence/allowed actions without requiring broad PO/PR read; do not expose unrelated supplier/other-record data. Stable issued2 fence runs before search/count/page.

## 14. User Experience and Human Behaviour

Preserve supplied layout/toolbar/tabs and Core branding; unsupported actions have truthful unavailable reasons. Loading/empty/denied/stale/unconfirmed states use existing patterns. Unknown POST blocks blind repeats; original key/body may be explicitly replayed only under exact server idempotency, reload recovery is GET-only. Every mutation rechecks actual current permission/source version.

## 15. Notifications and Communications

Acceptance handoff event describes a committed Procurement fact and identifies exact source/effect/quantity/value. No stock creation, asset custody, supplier email, Finance posting or AP-ready claim. Notifications do not replace durable audit/receipt. Outbox dispatch remains owning infrastructure, not a new engine.

## 16. Documents and Attachments

Retain delivery-note/evidence references and observed timestamps. Do not add a raw Core document ID input or fake upload. Future upload/download must use shared Core document owner/version access; adapter absence remains unavailable. Evidence text is not a clean-scan/document-retention claim.

## 17. Exceptions and Edge Cases

Reject cross-scope/mismatched issued/line/source/mapping/alias pins, missing historical source, changed acknowledgment, unknown history and unsupported policy. Preserve actual physical excess, but do not accept it by default. Changed current supplier/catalog eligibility does not rewrite or conceal a valid historical receipt; current new-use policy remains separately enforced.

## 18. Integrations, APIs, Events and Sandbox

One reusable ProcurementPrincipalOrderRetainedIssued loader/query fence is extracted by PO owner, not copied into receiving. Current Core account adapter is separate from retained commercial validation. New versioned command contract shares existing external UI field meanings and exact strings; naming/capability registration stays unavailable until parent reviews actual implementation and central schema. No new external API claims.

## 19. Audit, Security and Privacy

Append-only observations, inspection/effects/command receipts and original actor/alias/source hashes; managed draft revisions remain auditable. No sensitive credential/account-directory leaks. Strict JSON schema/version/type/duplicate validation reuses existing encoding. Optional worker provenance never becomes a prerequisite; current and retained aliases may only deny self-checks, never invent authority.

## 20. Reports, Dashboards and Metrics

Only reconciled scope/counts reflect actual facts: physical delivery, rejected/held, accepted quantity/value and service completion. No invoice/payable/current stock balance or Finance reserve assertion. Unknown reconciliation remains Unknown, not a zero metric.

## 21. Configuration and Localization

Receiving schema2 policy stores named Core principal assignments with explicit goods/service/site/category/checklist/independence choices and controlled publication identity lineage. Existing schema1 worker arrays stay schema1 and read-only to new editor. No implicit named reviewer, default threshold, employee conversion or publication authority. Currency comes from actual company; never hardcoded.

## 22. Non-Functional Requirements

Reuse pre-snapshot Core/command lease ordering where those owners participate; do not add current catalog eligibility to historical reads ; use the enlisted same physical store. Lock requests→PO→receiving state in deterministic order; do not lock upstream after a downstream snapshot. Unique scoped keys and source CAS fail conflicts atomically. Bound selectors before paging; no unbounded per-row reconciliation. No new background timer or speculative service.

## 23. Postconditions

Physical record is not acceptance. Acceptance has one exact retained source, actor/alias, configured policy/inspection proof and positive exact effect. Source counters equal the complete immutable effect stream, and PR/PO owning counters remain consistent through the owning boundary. Returns/corrections cannot reduce that stream while owner consumption is unknown.

## 24. Failure and Recovery

All effects/audit/receipt are atomic. Exact replay revalidates current original actor/action/scope then returns original stored JSON without re-running today's commercial eligibility; a new key is a new command, never a replay. Fresh contexts after aborted transactions; retry only known aborted serialization/deadlock. GET original result survives later lifecycle changes when current access permits. Lost response never fabricates absent success.

## 25. Acceptance Criteria

Workerless Core receiver/inspector/certifier succeeds only with real scoped action/assignment and exact source proof. Same-principal or shared retained/current worker alias self-check denies. Offboarded historic signer does not strand read/recovery. Goods partial, rejection/held, overdelivery observation and service partial certificates conserve exact totals. Duplicate keys create no second effect. Source binding corruption is hidden before count/page.

## 26. Test Scenarios

Author pure transitions/history/alias/capacity/exact8/replay tests, model guards and scoped mapped negative tests. Parent serializes compilation, genuine additive migration and actual PG17 fresh/used/Down safety/ACL/raw FK/CHECK/rollback/concurrency proofs. Reuse actual private browser/no business mocks for original imported UI. No source-authored test is execution evidence.

## 27. Ownership and Dependencies

PO owner supplies reviewed retained-issued2 graph/fence; Core supplies current identity/grants/branches and published policy lineage; Procurement owns receiving. Existing inventory/asset/Finance owner interfaces remain separate. Parent owns central migration/snapshot generation and activation. This branch must not mutate active root or PO owner's in-flight source.

## 28. Open Decisions and Assumptions

The bounded positive workflow is technically authorized for source implementation. Before runtime activation: review schema2 receiving policy publication/canonical principal selector, exact operation DTO compatibility, genuine schema/finite-role readiness, acceptance handoff owner contract and actual evidence gates. No unresolved consuming-owner contract is resolved by fake zero: negative effects/physical returns stay unavailable. Supplier Accepted acknowledgment means witnessed manual response only. No migration/design snapshot/DI activation is generated by this source branch. Parent review precedes merge.

### Source checkpoint: bounded pure decisions and draft revisions (unverified)

`ab1210f` defines this delta; ordinary merges retain PO release/issued proof through `d0db773`. `26ec8e1` adds typed version2 source/facts, optional-worker current Core projection, strict principal-named schema2 policy and pure positive decisions. The next source slice adds multi-line goods/service owned draft revisions, explicit Record versus Accept separation and cancellation reason retained separately from notes. Create ignores supplied draft line IDs and generates server IDs (existing create semantics); revise preserves stored line/source identity, never chooses by a display name. Changes to a known source pin cannot be disguised by omitting its line ID.

At the initial pure-source checkpoint there were no EF tables, migration, registered routes, provider or capability activation for this successor. Later source checkpoints below supersede that implementation inventory; no runtime activation is implied. Pure policy projection is NOT independent publication proof; actual current Core publication lineage and alias revalidation must precede future new effects. Complete retained policy/history projection is required for balance reconstruction; old superseded publication may explain an old fact without authorizing a new one. Tests authored in these slices are synthetic pure evidence, not executed/HTTP/PostgreSQL acceptance. Durable receipts, persistence guard/mapping, real policy lifecycle/lookups, source-query fence, actual versioned endpoints/recovery and handoff plumbing remain explicit next increments before this workflow can run.

### Owned persistence source checkpoint (not schema/runtime acceptance)

The successor now defines five additive owned tables: `principal_receiving_sources`,
`principal_receiving_drafts`, `principal_receiving_revisions`,
`principal_receiving_facts`, and `principal_receiving_receipts`. No existing receiving
record or legacy issued/version envelope is widened or rewritten. Stable scoped
IDs are EF keys; source sequence, draft revision, current status and exact balance
fields are concurrency-controlled values, never alternate keys. Explicit short
names avoid PostgreSQL identifier truncation. Text snapshots retain exact original
bytes; normalization binds every source/fact/header column to typed evidence.

`PrincipalReceivingStore` is an unregistered enlisted persistence primitive. It
requires the actual owning relational transaction, complete retained history,
loaded owning commercial parents and current Core/source authorization supplied
by the API coordinator. It cannot confer authority. A first source refuses legacy
receiving history or nonzero PO accepted counters instead of adopting unknown
history as zero. Positive facts require same-save durable command receipts and
advance the source cursor. Positive acceptance updates the existing PR/PO counters
with exact deltas; the old counter guard accepts only the validated successor
facts in addition to its existing legacy effects. It does not create stock,
asset, Finance or credit entries. Missing full history fails closed.

Each draft revision is immutable. The small head must advance exactly once with
its matching revision and receipt. Terminal revision snapshots cannot rewrite
original notes, quantities, commercial pins or physical references. Fact and
receipt rows are append-only; foreign-scope objects are rejected before scope
stamping. Inspection and acceptance leave the original physical fact unchanged.
Exact recovery returns original stored receipt bytes after the caller's current
actor/action/retained-record access check, without today's policy or supplier
eligibility being substituted for historical authority.

Six additional source tests cover normalized source/fact crossbinding, duplicate
and unknown JSON, exact eight-place acceptance recovery, unknown empty histories,
terminal snapshot immutability, scoped model keys/precision/names and pre-stamping
foreign-scope rejection. They have not been executed. Earlier fourteen pure tests
also remain unexecuted. No generated migration, snapshot/designer edit, provider
registration, endpoint activation, database probe or UI claim accompanies this
checkpoint. Before central generation the exact source/draft/fact ownership
indexes and any manual deferred ownership overlay must be reviewed, followed by
actual PostgreSQL constraint/round-trip/concurrency/recovery tests. Existing
capabilities remain unchanged; successor readiness remains unavailable.


### Guarded application adapter checkpoint (source only)

The successor now authors concrete handlers reusing the existing goods/service
request DTOs under explicit versioned successor paths:

- `POST /receipts/principal-drafts`, `PUT/GET /receipts/{id}/principal-draft`,
  `POST /receipts/{id}/principal-cancel`, `POST /receipts/{id}/principal-record`.
- `POST /receipts/{id}/lines/{lineId}/principal-inspect` and
  `POST /receipts/{id}/lines/{lineId}/principal-accept`.
- `POST /service-acceptances/principal-drafts`,
  `PUT/GET /service-acceptances/{id}/principal-draft`,
  `POST /service-acceptances/{id}/principal-cancel` and `principal-certify`.
- `GET /receiving/principal-command-receipts/{operation}/{key}`.

All paths are relative to `/api/procurement/v1`. They are authorable adapters,
**not registered host endpoints**. `ProcurementPrincipalReceivingOptions.Enabled`
and `ExecutionDatabaseConfigured` default false. The only supplied readiness
implementation always returns false; missing DI never creates a provider or
assumes readiness. Genuine owner-context schema and finite role proof must be
implemented and centrally accepted before host registration or a capability.

New effects run inside the existing `ProcurementApprovalTransaction` and enlisted
Core context. Core remains the current account/role/site authority; no HR employee
is required. The shared retained-issued2 loader locks original requests before the
PO and rechecks the exact retained commercial graph. Supplier manual acknowledgment
is only a response witness. Goods recording still requires actual observed date,
physical quantity, delivery-note reference and approved evidence reference.
Current policy selection requires an effective published RECEIVING policy and the
actual owned publication lineage. Named assignments are explicit Core principals.
Denial-only worker aliases and current rebind checks supplement principal-based
maker/checker separation. Replaying a stored command reauthorizes its original
actor/action/branch before returning original bytes, ahead of current business
eligibility. GET recovery performs no source creation or eligibility mutation.

The existing policy configuration lifecycle now recognizes strict schema2
Receiving documents alongside unchanged schema1 documents. Its company checks
use Core currency, current sites and canonical named accounts; no employee master,
new policy engine or default inspection/acceptance authority is created. Parser,
validation and preview remain non-authoritative; publication still uses the
existing independent owned policy lifecycle. The successor current selector never
falls back to an older/unproven policy when the current policy fails verification.
Historical stored facts use retained publication evidence and original event-time
rules, without requiring former signers' present employment or permissions.

All exposed amounts use exact plain-decimal strings. Goods acceptance targets the
original receipt **line** while its relational fact/receipt owner is the real
receipt **head**; these identities are separately checked. The storage regression
covers this distinction. Three additional mapped closed-gate cases prove missing
or default configuration prevents entry into a transaction and ordinary employee
sessions acquire no receiving/inspection action. These are authored, unrun tests;
they do not substitute for positive mapped/PG/CAS/recovery/constraint acceptance.
Discovery/worklists, purpose-limited assigned views, manual exact relational
ownership overlays, full-save positive fixtures and actual schema readiness remain
explicit next gates. No new UI, inventory, assets, Finance ledger, invoice matching
eligibility, returns or negative correction effects are activated.

### Scoped source discovery checkpoint (source only)

`GET /receiving/principal-sources` is an authored receipt-create-authorized
selector, capped at 25 candidates per page. The shared P30 retained-issued query
fences current tenant/company/explicit Core branches and retained binding branch
agreement **before** search/count/page. Publication proof/readiness precede counts.
Each bounded result then passes the full owning retained-issued verifier and
explicit schema2 receiving classification; failures abort instead of returning
unverified commercial metadata. The count is labelled scoped commercial-source
candidates, never a promise of operational capacity. Exact original source facts
remain hints requiring full command validation.

GET never creates a source state. An absent successor state returns nullable
balance/cursor values and a truthful history classification. Any legacy goods or
service row (including inactive or incomplete legacy records), legacy source state
or unexplained nonzero PO accepted counter prevents adoption as a zero ledger.
A synthetic negative query test covers an old accepted line with no source state;
it does not claim valid legacy business creation or PostgreSQL FK proof. Existing
successor ledgers also reject newly encountered mixed legacy history. No provider,
route registration, flag or database changes are part of this discovery source.

### Normalized membership and assigned-purpose source checkpoint

A sixth additive table, `principal_receiving_line_bindings`, now retains exact
membership of every immutable draft revision. It uses stable revision/source row
UUIDs, not current revision/sequence values as EF keys. Logical goods line IDs
survive revision; service logical line ID is the real certificate/draft ID.
Membership binds the actual head, original PO/issued/source line, original site
and exact physical/intended/rejected quantities. New memberships require the
same-save newly added revision, and every revision requires its exact complete
membership set. Facts reference their true revision membership. Goods observation,
inspection and acceptance targets must match the original logical line; no orphan
membership may be adopted. The in-progress, never-deployed schema2 source envelope
also now pins the exact P30 original binding ID. Legacy v1 envelopes are unchanged.

Source classification/kind/currency and retained original/issued hashes are
normalized and checked against the original typed source. Ordinary explicitly
named unique indexes support migration-owned exact same-parent relationships;
mutable existing PO-line source bindings and all CAS fields remain outside EF
alternate keys. `PrincipalReceivingOwnershipConstraintSql` authors ten deferred
NO ACTION FKs binding exact issued/funding/submission/hash, acknowledgment,
original commercial binding/site/hash, PO/request line, draft/issued, revision,
source, fact-line and receipt-head tuples. Its structural manifest checks actual
relations/column order, validation, deferral, update/delete behavior and immediate
valid unique referenced indexes. These constants are **not installed**. The root
must freeze them in a genuine centrally generated migration after review and run
raw PostgreSQL violations/rollback/readiness/tamper probes. Down must lock affected
owner tables and veto any retained new evidence/receipt/membership/fact/usage
before removing overlays or generated tables; no CASCADE or backfill is proposed.

The authored GET `/receiving/principal-worklist` uses normalized membership and
retained issued scope before count/page, capped initially at ten heads. It verifies
full original commercial proof and preserves historical directory access without
requiring today's operational policy or old signers' employment. It makes no
current balance/action/readiness claim. Existing head detail/recovery remain the
owned history and original-result surfaces.

Purpose-only GET queues are `/receiving/principal-inspection-tasks`,
`principal-acceptance-tasks`, and `principal-service-tasks`; details are
`/receipts/{id}/principal-inspection-task`, `principal-acceptance-task`, and
`/service-acceptances/{id}/principal-certification-task`. They reuse imported
receiving purposes and actions, not broad receipt-read permission or a new UI.
Current canonical Core action/branches and the exact current independently
published schema2 named policy filter before count/page. Page/detail parameters
are bounded to 25 lines and expose explicit total/offset/limit. Each returned
record additionally verifies complete owned source/history. Purpose projections
omit notes, maker identities, commercial prices/snapshots and broad document
history. Missing inspection, changed pinned policy, exhausted capacity or current
alias/self-independence produce allowlisted blockers and suppress action hints.
Hints never authorize a command; actions repeat canonical authorization and CAS.
An assignment is a name in policy, never a new grant.

Authored tests additionally cover exact named-task/classification/site predicates,
no employee/inspector fallback for service owners, actual Npgsql SQL generation of
pre-page joins/fences, and same-save orphan-membership rejection with no saved rows
or audit. Existing mapped closed-gate tests now include assigned tasks/directory.
No tests or SDK/DB/Node jobs have run for these source increments; actual runtime,
full-save, scope/privacy/concurrency/replay/rollback and PostgreSQL constraints
remain acceptance gates. All successor capabilities/provider activation remain
closed. Return/correction negative ledger effects remain unavailable pending real
consumption-owner contracts.

### Counter and original-command integrity correction (source only)

Distinct review identified missing checks against owning accepted-quantity
counters and each fact's original command receipt. The correction normalizes
fact operation and adds `fk_prcv_exact_fact_receipt`, making eleven exact manual
overlays. It binds tenant/company, original command key, receiving draft, actor,
branch and exact operation to the immutable receipt's unique tuple. Retained
loads verify every fact's matching receipt, response digest, result target,
PO/issued IDs, kind/status and original result revision. Missing/wrong receipts
and duplicate/malformed result JSON never become complete history. Same-save
new facts defer receipt verification only until the final mandatory Save guard;
this is actual EF Added state, not a caller-supplied verification flag. A deferred
FK enforces the same relationship at commit after central migration installation.

Every quantity-bearing read/new-effect path reconciles full successor history to
all owning PO lines, including inactive rows, and the PR accepted aggregate.
Correct deltas over a previously corrupted counter are insufficient. Append now
performs asynchronous reconciliation before adding a fact or changing a cursor
or counter. The API also verifies each related source's original retained
commercial/Core/publication proof. Target writes retain the original request→PO
lock order. Related immutable proof inspection does not acquire new upstream
locks after the target PO; mutations already hold the original shared request
lock, and reads use the owning SERIALIZABLE snapshot. Current actor authorization
and expected source cursor remain mandatory.

Mixed legacy receiving or unexplained counters remain explicitly unavailable;
this successor does not adopt them or invent a historical zero. This bounded
correction verifies successor histories; supporting an independently reconciled
legacy/successor aggregate requires its existing owner evidence contract rather
than assuming legacy counters trustworthy. Historical original-key recovery
remains original-result inspection, not a new effect or fresh balance claim.

Four additional unrun integrity tests cover two-source aggregate conservation,
joint/partial PO/PR counter tampering, exact receipt crossbinding/missing receipt,
recomputed wrong-target and duplicate JSON, and an Append negative proving no new
facts/receipts/cursor/audit changes on a diverged counter. The deliberately
incomplete InMemory negative seed is labelled as such and claims no valid
PR/PO producer, authorization, relational or PostgreSQL acceptance. Actual used
DDL/raw FK/counter tamper/concurrent transaction tests remain central gates.
