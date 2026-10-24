# PROC-06 P32 — Principal quotation submission, evaluation and award

**Status:** Source audit and implementation contract; not runtime-ready. **Version:** 0.1.
**Owner:** Procurement Engineering; business adoption decisions remain with Procurement governance.
**Source baseline:** ERP `f249dd374807591b4c543bc74fbd80dd82e0895c`.
**UI inspected:** module-connect `d548f57`, existing RFQ wizard/detail and principal publication adapters.
**Parent:** [PROC-06](PROC-06-STRATEGIC-SOURCING-AND-AWARD.md), including its P30 child delta.
No SDK, database, migration generation, browser or production changes establish acceptance for this document.

## 1. Feature Identification

`PROC-06-P32`: extend the principal-owned competitive sourcing lifecycle after the P30 manual quotation Draft. Operations: prepare immutable commercial controls before publication; record a supplier quotation submission; close/open a sealed round; independently evaluate; recommend line awards; obtain shared Core approval; emit an owned approved-award handoff. A manual quotation Draft alone does none of these.

## 2. Purpose and Business Outcome

Preserve original supplier offers and an independently governed, reproducible comparison leading to an approved line award or an explicit no-award outcome. Reuse existing Core users, approvals, documents and original PR allocation; do not create parallel employee, supplier, workflow, stock or Finance masters.

## 3. Scope and Exclusions

Initial execution scope is an explicitly configured, same-company/site/currency, invited, manually recorded sealed RFQ round. Original document evidence and reported supplier receipt time differ from server entry/submission time. Portal submission, external transmission, tender legal compliance, currency conversion, stock, payment and automatic PO/contract creation are separate owner integrations. No fallback to an unsealed round or estimated scores is permitted.

## 4. Parent and Related Features

Reuse P29 principal source pins and the authoritative `PurchaseRequestAllocation` ledger, P30 publication/quotation evidence, Core Documents use retention and Core ApprovalEngine. PR approval is demand authority; RFQ publication approval is audience/requirements authority; neither substitutes for an award approval. Existing direct-PO conversion remains DirectPurchase-only and must not accept an RFQ draft/quotation as a direct source.

## 5. Actors and Participants

Current Core buyer, manual-offer recorder, assigned opening officer, named technical/commercial evaluators, independent award maker/checker and authorized historical reader. Employee link is optional historical alias evidence, denial-only. Same-worker aliases may not bypass separation. Supplier ownership/delegation is not inferred from an internal buyer login. A future supplier portal needs its own actual identity contract.

## 6. Entry Points and Triggers

Extend existing `/rfqs/{id}` requirements/schedule/documents/related/activity surfaces and existing quotation editor/table action positions. Add purpose-scoped assigned tasks to the existing RFQ list view. Server-only contract paths are planned below; they are not registered by this audit. Imported wizard local previews cannot create a governed record or be treated as persisted controls.

## 7. Preconditions

A genuine source-verified principal draft and held quantities; an explicit commercial policy/configuration; exact requirements-document versions; immutable criteria/weights and named independent evaluators; explicit opening/closing instants and opening authority; eligible approved audience; current scoped actor; an actual enlisted owning transaction and supported document-retention fence. Missing owners, policy, finite authority or schemas return unavailable. No guessed reviewer, score threshold, bid count, deadline, calendar or monetary limit.

## 8. Workflow

Prepare controls while Draft → independently approve/publish the same source/pack/control digest → capture a manually obtained quotation Draft → submit an exact immutable offer/document version while within the configured window → close round by trusted server time and explicit close command → independently open sealed content → record assigned individual evaluations → freeze a recommendation → open actual Core award approval → persist approved/rejected/returned source outcome and owned handoff. Each step has its own original UUID/key/body recovery.

## 9. Status and State Transitions

Keep P30 `Draft/Cancelled` quotation snapshots byte-compatible. New submission, close/open, evaluation and award states live in versioned owned evidence; do not teach the P30 snapshot encoder to treat Draft as Submitted. A new commercial round can progress Prepared → Published/Accepting → Closed → Opened → Evaluating → RecommendationPending → AwardApproved/NoAward; cancelled/reopened rounds retain every old record and need explicit successor round identity. Past content never becomes editable after return/cancel.

## 10. Permissions and Data Scope

Current catalog only registers sourcing read/create/edit/convert/cancel/publish. New explicit actions are required for quotation submission, round close/open, evaluation, award recommendation and award decision, with empty bootstrap grants and role-management tests. Do not reuse `procurement-sourcing-publish` as all commercial authority. Actor/company/current Core branches and named assignment constrain SQL count/page before any content projection. Only own submitted quotation may be acknowledged to its authorized recorder; competitors/evaluators see nothing before verified opening. Historical recovery checks current original actor/action/scope but does not re-run today's commercial policy to rewrite an old response.

## 11. Business Rules

A configuration version freezes all criteria, normalized weights, scoring basis, maxima, mandatory pass/fail conditions and stage order before publication. Missing explicit scoring configuration blocks. Weights are exact bounded decimal values and normalization is validated without silently correcting input; normalization basis is a technical schema choice, not an adopted business score threshold. No price-score formula, tie-break, rounding, automatic disqualification or minimum passing score is assumed. Manual submission requires exact original quote/document/source version and trusted entry time; reported receipt time cannot backdate eligibility. Submitted correction is a successor version governed by the configured round rule. Closing does not reveal content; opening authority and stored opening proof are separate. Awards conserve source quantity/value and never release another consumer's holds.

## 12. Data Fields and Ownership

Proposed append-only evidence: commercial control revision (event/source/publication-policy pin; explicit round times; criteria; evaluator principal/optional alias pins; pack use references); offer submission (event/invitation/supplier, exact P30 original quote revision/hash, received/entered times, frozen content and document use set); close/open records (round, expected version, sealed submission-set watermark and actor); evaluation submission (opening, original offer, criterion plan, assigned principal/alias, score/pass/conflict facts); award recommendation/lines (evaluation-set hash, original source/allocation pins and exact quantity/38,8 product); Core-case opening/assignment/decision/source-outcome pins; owned command receipts. Reuse `SupplierBid`, `BidEvaluation`, `SourcingAward` IDs as named owning projections only when exact parent bindings and append-only evidence are verified. No client-supplied reviewed/verified/approved boolean.

## 13. Screens and Data Views

Preserve imported `RfqWizard` panels/step arrangement and existing `TransactionDetailFrame` RFQ detail tabs, `RecordTable`, ERP Select/Input/Modal and toolbar. `PrincipalPublication` already embeds shortlist/manual contact/quotation Draft editing; extend these existing controls, not a second sourcing application. Named lookup labels are scoped, paged and nonauthoritative. Existing source estimates are not supplier submitted prices. Missing operations stay honestly unavailable rather than executing browser-local demo mutations.

## 14. User Experience and Human Behaviour

Use plain labels: Draft quotation, supplier response recorded, sealed, closed, opened, your evaluation, recommendation and award approval. Distinguish current server action candidates from final authority. Preserve entered values on known 422; stale 409 refreshes/reconciles; unknown mutation disables resubmission and uses original GET receipt. A different actor/company/site unmounts the editor and ignores late callbacks. Hide competitors' amounts, counts and document names until the relevant purpose is authorized.

## 15. Notifications and Communications

Only owned outbox declarations after commit. Internal task notification may reference scoped IDs; no sealed prices/documents in events. Supplier delivery remains the existing explicit external/manual contact record unless a real sending owner acknowledges it. Approved award handoff is not a sent award notice or PO. Event versions distinguish submission/close/open/evaluation/recommendation/approved award.

## 16. Documents and Attachments

Reuse Core uploader/scanner/version/owner-access and durable use pins. Submission pins exact clean scan, content hash, original document/version and actor-authorized owner scope. Opening/evaluator downloads prevalidate the exact pinned version; owner-wide access alone is insufficient. Shared physical store/enlisted transaction and pre-snapshot write fence are required for first composition. Managed retirement must respect committed uses; interfaces alone do not establish race safety. Requirements pack and supplier offer uses are distinct, even when bytes match. Missing document owner/lease fails closed; a free-text reference is not scanned evidence.

## 17. Exceptions and Edge Cases

Zero/one/many offers, no award, partial/split line award, withdrawn/suspended supplier, late offer, changed manual Draft, conflicting evidence, offboarded evaluator, same-worker account alias, reopened round, quantity consumed by another target, missing sealed original, malformed policy, unknown legacy evaluation and store outage. Preserve completed historical signer facts; currently acting/future assignees must reauthorize. Explicit current policy expiry can block new effects without making historical receipts unreadable. No fabricated zero allocations or acceptance.

## 18. Integrations, APIs, Events and Sandbox

Planned routes reuse `/sourcing/{eventId}`: commercial-control Draft/revision; own quotation submission/history; close/open round; purpose evaluation tasks/detail/submit; award recommendation/open/Core decision/history; actor-owned command receipt. Every mutation carries expected event/target/round versions and UUID key; exact wire fingerprint and response persisted atomically. List/selectors apply bound source/site/assignment filters before count/page. No GET writes. Actual names/DTOs freeze only with the implementing slice and client contract; no speculative UI requests. Core executes One/All/quorum decisions; Procurement owns source effects and event/outbox declaration.

## 19. Audit, Security and Privacy

Retain original actor principal and optional alias on every stage, including policy maker/submitter/reviewer and intended evaluation/award recipients. Current account rebinding must not transfer original award authority. Append-only evidence, stable scoped parent IDs, exact normalized same-parent ownership constraints and hash verification are mandatory. No mutable CAS field as EF key. Deny UPDATE/DELETE/TRUNCATE on evidence under finite runtime roles and check column grants. Sealed content is not returned in broad event/quote history or logged as errors. Generic legacy bid routes remain denied for execution2 parents.

## 20. Reports, Dashboards and Metrics

Expose scoped round progress and assigned work only from real records. Participation, disqualification, awarded amount and cycle time need completed original evidence. No simulated score/rank/savings, no partially evaluated recommendation as approved, no Finance commitment/available balance inferred from awarded value.

## 21. Configuration and Localization

Existing SOURCING publication schema1 includes count/pack/criteria/deadline flags and publication approval stages only; it has no actual weighted criteria, named evaluator plan, seal/open authority or award approval policy. A new versioned explicit configuration/publication projection is necessary. Unknown fields/versions remain read-only; no default thresholds or automatic reuse of publication approvers as award approvers. Core organisation currency defaults the input, exact source currency remains pinned. Dates use trusted UTC instants with clear local presentation; business calendar exceptions require an owning configured policy.

## 22. Non-Functional Requirements

Bound all schemas/lists/documents/criteria and strict JSON duplicate/depth/size checks using existing evidence helpers. Maintain exact numeric18,4 inputs and checked38,8 products/sums; no Number/string rounding or DB coercion. Use sorted original PR locks → event/round → Core-case/document coupling according to one declared transaction order. Same physical-store constraints, retry/failure classification and unknown-commit receipt recovery must be tested with real Npgsql. No detached authority/readiness boolean.

## 23. Postconditions

Submitted offers are immutable and sealed; opening fixes an exact complete submission set; evaluations/recommendation reference that set and frozen criteria. Only an owned final approved Core proof yields an approved award, with line quantities within original reserved demand and exact value. PR holds are not duplicated or silently released. PO/contract handoff remains blocked until its separate consumer accepts an exact owned award source. No Finance, dispatch or stock effect occurs implicitly.

## 24. Failure and Recovery

Atomic source records/evidence/counters/receipts/audit/outbox share the existing `ProcurementApprovalTransaction`. Original key recovery returns stored bytes under current original actor/scope/action. Changed payload/target/actor conflicts or denies. Never automatically POST after uncertain submission/create. Retained source restoration uses immutable original evidence; do not require today's catalog or supplier eligibility just to read old facts. New award effects do require the adopted current eligibility/authority rules. Unsupported legacy or mixed allocation evidence stays unavailable. Used-evidence Down first locks every affected source/evidence/receipt/audit/outbox table and vetoes retained history; no backfill, history deletion, rounded downgrade or CASCADE.

## 25. Acceptance Criteria

No employee exists in the positive fixture. Before opening, even a named evaluator cannot read sealed content through detail/list/document/export/error paths. Changed quote/document/criterion/publication/assignment/actor/site/version pins deny with zero effects. After genuine opening, only the assigned purpose projection is readable. Independent exact evaluations plus configured Core approval are required for an approved award. Concurrent line awards and other held consumers cannot overallocate. Historical read and original key recovery remain available after supported policy/supplier/account changes. Default-off and unavailable owning schema remain closed.

## 26. Test Scenarios

Pure tests: strict explicit configuration, duplicate/unknown fields, zero/false preservation, exact weighted arithmetic, frozen plan/hash, scope/alias/SoD, exact original quote version and evidence, deadline boundaries, no opening before close, no evaluation before opening, incomplete/conflicted evaluations, partial quantity conservation, authentic Core One/All/reject/return proof. HTTP tests: actual Program finite roles/default-off/schema gate, all own/foreign purpose reads and counts, stable keys/stale CAS/rollback. PG17 tests: genuine migrations and captured exact CHECK/FK manifests, JSONNULL/ownership/append-only/column permissions, concurrent awards/closing/submission and unknown response recovery, fresh owner reconciliation, fresh/used/DownUp plus used-evidence veto. Existing imported UI gets focused mocked and actual API browser paths; no business responses mocked in actual acceptance.

## 27. Ownership and Dependencies

Procurement owns commercial plan, round, submission, evaluation, award and PR allocation decisions. Core owns canonical current accounts/scopes, workflow execution, document storage/scan/use and audit transport. Existing Finance policy evidence authorizes source funding only as documented, never supplier payment. Dependencies: accepted P29/P30 baseline; explicit commercial configuration adoption; actual Core document use composition; finite award action/authority registration; central additive model/migration/readiness generation. Current work is isolated source, not a merge/activation instruction.

## 28. Open Decisions and Assumptions

Business: scoring basis/formulas/normalization and pass criteria, price/tax/delivery comparison, tie-break/no-award rules, conflicts/independence, opening authority, late/correction/reopen policy, deadline/calendar, award limit and approval stages, split awards and supplier eligibility instant. No answers are invented. Technical implementation can proceed on explicit typed inputs with unsupported/missing choices blocked. Initial reviewable code slice is pure immutable plan/submission/round contracts and meaningful negative/conservation tests; no provider/routes/models activated by that slice. Persistence proposals follow exact validated contracts; central owner alone generates genuine migrations/snapshots.

### Observed source gap and reuse register

| Surface | Actual source | Current capability / required delta |
|---|---|---|
| P30 internal publication | `PrincipalRfqPublicationCoreWorkflow.Open`, lines66–69 | Explicitly rejects required pack/criteria/closing controls. Existing Published records cannot be adopted as P32 authority; new pre-publication control snapshot/proof required. |
| Manual quotation | `ProcurementPrincipalSourcingManualQuoteRoutes`, `PrincipalManualQuotationDraft`, `PrincipalManualQuotationSnapshot` | Real Draft/create/revise/cancel/history only; no submitted original, seal, deadline or evaluation authority. Preserve encoder bytes and existing routes. |
| Existing commercial tables | `CommercialRecords.cs`, `ProcurementDbContext.Configure` | Bid/evaluation/award rows and ordinary scoped links exist, but no frozen criteria/assignment/opening/award evidence. These tables alone prove no governed milestone. |
| Original allocation | `CompetitiveSourcingSource`, `GovernedRfqDraft`, `ProcurementApprovedDemandLoader` | Reuse one existing PR ledger and exact P24 value schema; no second award-reservation balance. |
| Shared workflow | Core `ApprovalEngine`, P30 `PrincipalRfqPublicationCoreWorkflow` | Reuse real One/All/decision/CAS machinery with a distinct award source type and pinned authority; no new engine. |
| Shared documents | Core `EfDocumentUsePinStore`, `IDocumentUseAccess`, `ProcurementInvoiceUnitReviewDocumentUses` | Reuse actual fenced owning adapter and exact version-purpose access, not a copied uploader or owner-wide download. Procurement RFQ composition still required. |
| Existing RFQ UI | `RfqWizard`, `RfqPresentation`, `PrincipalPublication` at UI d548f57 | Preserve layout/components. Imported Wizard local Draft is not an executable commercial plan; principal detail currently says no supplier bid/award and owns only manual Draft editor. |
| Permissions | `ProcurementActionCatalog` | New actions/finite authority contract absent; empty bootstrap, actual Core role picker/tests, no reuse of generic edit/publish as award authority. |

### First bounded code slice — evaluation definition only

`PrincipalRfqEvaluationPlanDefinition.Parse` is a standalone typed, immutable
configuration parser. Its separately named `SourcingEvaluationDefinition`
version1 is not an accepted SOURCING publication schema or an available API.
Every criterion explicitly supplies code, Technical/Commercial phase,
Scored/PassFail kind, mandatory true/false, maximumScore, weight and named Core
principal IDs. For this initial weighted definition, at least one Scored
criterion is required; scored weights must sum **exactly** to the explicitly
supplied normalizationTotal. No normalization basis is defaulted: a caller
choosing1 versus100 receives exactly that representation. PassFail has explicit
null maximum/weight, never an invented zero score. PassFail-only evaluation is
unsupported by this initial definition rather than silently reinterpreted.

The parser reuses existing bounded exact numeric18,4 decoding and canonical
recursive duplicate-property/digest verification, with65,536 UTF8-byte and
16-depth limits. Collections are copied and read-only. It preserves named IDs,
false and null fields without claiming that those accounts are currently
eligible or independent. `IsPublicationAuthority` is always false; no module
provider, API, configuration lifecycle, EF model, migration, feature metadata or
runtime service registration consumes the definition in this slice.

Thirteen pure cases are authored in
`PrincipalRfqEvaluationPlanDefinitionTests` (9 facts and a four-case theory):
explicit values/false/null; collection mutation denial; explicit normalization;
raw excess precision/overflow/zero/negative; numeric rounding; duplicates and
old/unknown/missing fields; named-assignment identity/emptiness; pass/fail
representation; bounded work; and canonical digest/change binding. These cases
are **not run**. Current Core actor/reviewer resolution, retained policy origin,
pre-publication snapshot/document retention, timed round, immutable supplier
submission, seal/opening, score calculation and Core award workflow remain
subsequent implementation and acceptance gates, not claims of this parser.

### Second bounded slice — immutable prepublication candidate

`PrincipalRfqPrepublicationPlan.Prepare` restores the original owned principal
RFQ envelope with its normalized demand bindings, exact header CAS and original
fingerprint. It copies a separately versioned snapshot containing the source
pin digests/exact8-place values, explicit future UTC microsecond window,
evaluation definition hash/criteria, exact criterion→principal/optional-worker
assignments and complete Core document-version/clean-scan identity. No P30
quotation object or bytes are changed or accepted as a submitted bid.

The proposed `procurement-sourcing-evaluate` action is a named pure-contract
constant only: it is not registered or granted. Exact current server Core
projections must cover every configured evaluator once; company/site/action,
retained maker/requester independence and known worker aliases are checked.
Two principal accounts with one known worker cannot masquerade as independent
evaluators. These pure checks do not prove active account retrieval, current
maker-account aliases or transactional authorization; the future owning adapter
must use the existing live Core resolver/fence and `IndependentNow` guards.

The requirements pack, when present, is an exact version set of Core `DocumentUsePin`
evidence. An empty set is permitted only when the selected published policy does
not require a pack. It must belong to this event/tenant/company, original maker and one
Review use keyed to this candidate revision (`sourcing_commercial_plan` /
`sourcing_event` owner). Core's existing Restore validator checks retained
scan/version/hash consistency; the candidate is not a blob/read-availability
check or proof that those use rows were committed. Procurement Application
references Core Documents **Domain** to reuse that primitive, not another
uploader or database. Later Publication is a separate durable reference to the
same version set after actual owned approval. Purpose download must authorize
only pinned versions, never broad owner history.

Only explicit `SameCurrencyOnly` + `OriginalSeparateComponents` is supported in
this initial candidate. A different comparison currency, assumed1:1 rate,
FX conversion, assumed tax-exclusive/inclusive conversion or invented rate is
rejected. Original tax/delivery components are retained evidence, **not** a
normalized comparable amount or an approved tax calculation. Core/Finance tax
configuration and a separately adopted published comparison policy own any
future normalization; a policy and authoritative rate/effective-date source
are prerequisites to widening this boundary. No score/formula/award is computed.

`CanPublish` remains false. The following are actual next adapter gates:
full original PR/Core/Finance/ledger verification using the existing shared
loader; original-source lock order; published commercial-policy/configuration
identity binding; canonical current account/branch/action and alias resolution;
actual pre-snapshot same-store document fence/enlisted acquire/persistence;
immutable plan persistence and original receipt; binding the new snapshot to a
new publication/Core proof; scoped purpose reads. None has a boolean-ready
fallback, interface-only lock claim, DI registration, route or EF model in this
slice. Existing P30 Published records remain ineligible for automatic adoption.

Thirteen additional pure synthetic cases are authored (10 facts plus3 normalization
theory cases). They cover workerless exact evidence; copied bytes/read-only
assignments; CAS/state/source/binding mutation; current scoped action; retained
maker alias and duplicate-worker accounts; exact document actor/scope/reference
and use purpose; exact nonempty sets; FX/tax rejection; and explicit trusted-time
precision. These are **unrun source tests**, not genuine document-owner/source
provenance, durable-hold or current Core authorization acceptance.

### Published pack requirement correction and current-alias owner contract

`PrincipalRfqPublishedPlanPolicy.Select` derives the required-pack flag from the
actual independently published SOURCING policy plus original Created/Submitted/
Published identity lineage, selected against every exact source classification.
It pins policy ID/number/CAS, rules SHA and published-identity ID/SHA in the
candidate. A caller cannot pass a detached `required=true/false`. Different source
fingerprint, observation instant, mixed controls or missing published matching
policy blocks preparation. Empty documents are accepted when that actual
matched flag is false; supplied documents always retain their strict exact-set
scope/scan/content checks. This remains structural owned-input verification,
not proof of live database selection or a commercial policy adoption.

This policy prerequisite applies to workflow freeze, **not ordinary commercial
Draft CRUD**. Draft persistence will keep incomplete configuration/optional pack
selection without inventing a publication policy, and disclose missing controls
honestly. A later owning freeze must resolve CURRENT canonical accounts and
worker aliases for the RFQ maker, every original demand maker/requester and all
evaluators under the actual Core fence; it must run the existing current-alias
independence checks in addition to retained aliases. Historical null worker IDs
cannot excuse two newly linked accounts for one worker. No test fixture or pure
candidate establishes that runtime guarantee.

### Third bounded slice — Draft-only typed persistence proposal

Commercial Draft creation/revision has no publication-policy, mandatory-pack,
scanner, Finance or scoring prerequisite. `PrincipalRfqCommercialDraft` permits
an empty explicit input (unconfigured evaluation/window/comparison and zero
selected requirements). Present complete evaluation definitions remain strict;
foreign FX/tax assumptions and malformed selections are not invented or silently
coerced. It checks current supplied owner/action/site and the retained original
RFQ envelope/bindings. Invalid/stale revise validates completely before mutating
the header. Original revisions remain immutable; historical restoration also
verifies the same-command original receipt and response-bound snapshot SHA.
No quotation is submitted, source quantity changed, supplier contacted or award
made. The proposed distinct configure action is still unregistered/ungranted.

Additive proposed tables: `rfq_commercial_drafts` (one per scoped event; mutable
logical revision/SHA only), `rfq_commercial_revisions` (immutable typed snapshot
and original actor/key), `rfq_commercial_receipts` (immutable original key/body
fingerprint/response and revision). They reference genuine scoped original RFQ
source rows, never a placeholder PO. No existing column/precision/backfill is
changed. New entity keys use stable scoped IDs with explicit short physical
names. An ordinary unique index on original immutable source tuple preserves
nullable legacy branch semantics; it is **not** a new EF alternate key.

`RfqCommercialDraftOwnershipConstraintSql.Install` proposes four exact deferred
NO ACTION bindings: header→original schema2 source/event/branch/fingerprint/
envelope SHA; revision→same header/event/source/site/owner; receipt→same immutable
revision/event/source/site/command/actor/snapshot SHA; header current revision/
SHA→the matching immutable revision. Mutable head revision is never an EF key.
The source-helper is not an executed migration or readiness check. Parent must
generate/freeze genuine DDL and exact PG17 physical manifests centrally; these
pending models intentionally cause drift until that separate reviewed step.
Down must lock new Draft/evidence/receipt tables plus inspected original event/
source/audit/outbox in a documented order, veto ANY retained new record/evidence
before dropping anything, and preserve unknown legacy values/history.

Same-context Save guards deny cross-scope/orphan evidence, wrong actor/command,
missing same-save revision+receipt, rewritten original source pins, evidence
UPDATE/DELETE and mutations beyond head revision/SHA. They recheck tracked
owned original source and exact typed snapshot. They do **not** provide SQL
privileges, race fencing or authorization by themselves. New model/negative
checks use the Npgsql model without opening a connection; InMemory negatives
only exercise managed scope/append-only code and do not claim physical FK proof.
Four pure Draft transition/recovery cases and three model/guard cases are
additionally authored, all unrun.

This slice supplies no registered repository/handler/loader. The next bounded
adapter must resolve real current Core participants/aliases, use existing sorted
request→event locks/complete source loader, perform current owned action/scope
checks, stage exact snapshots/receipt/audit together, and use the existing
transaction's actual same-store pre-BEGIN document fence only for freeze/uses.
A missing document service or publication policy must not prevent ordinary
empty Draft CRUD; workflow freeze remains unavailable until those actual owners
are proven. No detached readiness or synthetic document pin can enable it.

### Fourth bounded slice — unregistered owner repository and real adapters

`PrincipalRfqCommercialDraftStore` now stages actual tracked Draft/revision/
original receipt/audit/outbox rows on an already active owning Npgsql transaction.
It does not Save/Commit or authorize a caller projection. Current module action/
owner/Core branch validation must precede its reads; it additionally confines
selected-branch queries and denies unsupported department/missing confined
scope. GET recovery validates the original immutable revision and receipt,
without current policy/document/catalog eligibility; changed key body/operation
conflicts and wrong original actor/target is hidden. Draft revision checks logical
CAS before mutation. Schema and finite-role acceptance are still unverified.

Unmapped `CommercialSave` and `LoadCommercialSource` in the existing RFQ route
partial reuse actual current Core `PrincipalActor`/`PrincipalOwnedEvent`, original
sorted PR locks → event lock, `ApprovedDemandSourceLoader.LoadRetainedAsync`,
complete allocation owners/history and original source verification. There is
no new copied query/evaluator engine, no current publication-policy lookup in
Draft Save and no change to source holds/counters. Current action/site/owner is
verified before original receipt lookup; a replay never re-runs today's new-use
eligibility. All methods are private/unmapped. Missing options or actual-owning
P32 schema provider fails unavailable; only an unavailable implementation exists,
with no registration or capability. Existing P30 handlers remain unchanged.

Preparation's actual Core evaluator resolver loads aliases for ALL original
makers/requesters and named evaluators in one bounded owning query (including
archived historical makers with explicit tenant confinement). It resolves each
current evaluator's live account/action/site separately, caps the unique set100,
and reuses the same factored denial-only Core alias rule as `IndependentNow`.
Historical null links cannot bypass newly linked same-worker accounts. Three
additional pure alias-denial tests are authored, unrun; actual Core/HTTP/PG
interleavings remain required acceptance. Existing `IndependentNow` lookup and
authorization behavior is preserved; only its shared pure predicate is factored.

`ProcurementApprovalTransaction.ExecuteWithSourcingDocumentsAsync` reuses its
existing physical-session branch/catalog → exact Core document owner fence →
SERIALIZABLE transaction machinery. It fixes owner type `sourcing_event` and
never invents an invoice owner. The new narrow `CommercialDocumentUseAccess`
reauthorizes actual Core actor/owner/site on every operation and permits only
Acquire of the exact version set for a **same-save Added** owned revision plus
its verified Added original receipt. It denies history/download/publication and
unowned/detached revisions. `StageCommercialRequirementsForReview` uses the real
`EfDocumentUsePinStore` and flushes Core evidence on the same physical connection/
transaction; subsequent owner failure rolls everything back. It is not called by
ordinary Draft Save, so pending scans/integration do not block empty Draft CRUD.
An empty optional selection does not require a document store.

This does NOT activate a Prepare endpoint: final preparation must persist the
candidate/published-commercial-policy binding as owned immutable workflow evidence
and acknowledge it atomically before any publication consumption. That successor
workflow storage/composition, genuine P32 schema/readiness/provider, catalog
registration with empty bootstrap, finite grants, original-key GET endpoints,
actual Core/document alias/race/rollback proofs and existing UI binding remain
open gates. No boolean projection/clean scan/current helper or retained use itself
is approval. No code here submits/seals/opens a bid, evaluates a score or awards.

### Fifth bounded slice — durable prepared candidate and original receipt

`CommercialPrepare` is now an **unmapped**, default-off owning composition. It
requires real current actor/action/target branch before any original-key lookup;
replay returns the exact original preparation response without current policy,
scan or source-eligibility reevaluation. For a new command it locks original
requests → event, verifies retained demand/complete allocation, checks both
expected event and commercial Draft CAS, resolves all current evaluator/known
maker aliases, and creates a successor immutable commercial revision with the
same command's Draft receipt. It then acquires actual Core requirements uses
under the existing pre-BEGIN sourcing owner fence and owning transaction.

A missing definition/window/comparison configuration, independently published
SOURCING policy/lineage, named current evaluator, required pack or clean selected
scan blocks Prepare atomically. Original category/purchase type comes from the
retained PR submission snapshot, not today's selected UI classification. An
optional empty pack performs no document lookup/acquire/scanning. Ordinary Draft
Save remains separate and needs none of these workflow prerequisites. Source
PR holds/counters and RFQ status/version remain unchanged by preparation.

Two additive append-only proposed tables are now included:
`rfq_prepared_plans` and `rfq_preparation_receipts`. Prepared evidence references
the exact successor commercial revision, source event/version/site, original
actor/optional alias, independently published policy identity/rules, definition,
window, criterion assignments and exact requirements pins. Its immutable storage
envelope binds **both** the candidate snapshot and actual Core use-set ID/
selection digest/count; self-consistency cannot ignore a changed use pointer.
Receipt joins the exact original Draft command/key/body/actor and snapshot,
returns `PreparedCandidate` with `canPublish:false`, and survives later Draft
revisions. Parent RFQ, supplier bid, evaluation, award and Finance state are not
changed. Owned audit/outbox declare only an internal prepared candidate.

Four further exact deferred NO ACTION FK proposals bind prepared plan→same
original revision/source/site/actor/snapshot, prepared plan→actual immutable
Published identity/policy/rules/evidence SHA, receipt→that exact plan and
receipt→same original Draft command/actor/key/body. All EF keys remain stable
scoped identities; the new targets are ordinary immutable unique indexes. No
Core tables/models/keys are copied. Core use-set IDs are explicit owner references
verified by the actual shared context, not a cross-owner fake entity or arbitrary
client flag. Total proposed P32 owned tables: five (three Draft, two preparation).
All eight ownership overlays still require central genuine migration generation,
physical PG17 capture/readiness and finite-role negative proofs.

The existing transaction binds its actual enlisted Core Documents context to
Procurement before callback execution. Save guards observe successful actual
EF Core use-set INSERTs in this transaction (not a supplied boolean or updated
row), require identical live connection/transaction, exact original owner/use/
revision/actor and selection digest/count, then match every retained pin/document/
version/hash/CAS/scan evidence ID against the immutable prepared snapshot. Empty
optional packs require no Core use. Failed source/policy/plan/save or transaction
commit rolls back both schemas. Managed guards deny preparation UPDATE/DELETE,
orphan acknowledgements, adopted old commercial revisions or detached/fake
Core use evidence. This is authored code, **not** runtime/locking proof.

Four additional pure prepared-evidence/recovery/crossbinding cases and expanded
Npgsql-model/managed append-only assertions are authored, unrun. They use clearly
synthetic shapes and do not claim actual Core use acquisition or current account
provenance. Required actual acceptance includes all-branch A/B vs C, current alias
rebind (including previously null and archived makers), optional empty pack,
required missing/failed scan, wrong owner/version/scan/use set, success→fresh
owner reload, same-key original recovery/changed-body conflict, concurrent
revision/preparation, whole-transaction rollback after Core flush, pool/lease
cleanup and genuine used-evidence Down veto across all five tables plus uniquely
linked audit/outbox/Core document use references. No source DB is deleted or
history rewritten to achieve a downgrade.

This closes the earlier *source* prepared-plan storage gap only. P32 still has
no routes/service registrations/positive schema provider/capability or generated
migration; genuine source compilation/tests/SQL/private runtime are pending.
The current SOURCING policy does not adopt scored formulas, sealed offer rules,
closing/opening authority or award approval. Those missing explicitly published
controls block Publish/submission/seal/open/evaluation/award even when a candidate
is prepared. P30 internal Published/Draft quotation bytes are not adopted as
submitted or sealed bids. Current next milestone is independent review of this
bounded composition, then central physical schema/runtime proof and a separately
versioned governed commercial publication contract.

### Sixth bounded slice — sealed manual offer protocol (source only)

The next effect is a **separate** immutable submission from one verified P30
manual quotation Draft. It requires a durable P32 commercial round whose
published controls bind the prepared-plan ID and digest, the actual independent
Core commercial publication case/approved decision, source RFQ/version, branch,
comparison currency, exact open/close instants and supplier invitation set.
P30's internal RFQ `Published` state and the `PreparedCandidate` response do
not prove this round. Until that owned publication transition and genuine
schema/readiness exist, submission and sealing stay unregistered/default off.

For one invited supplier, an authorized internal recorder supplies the exact
manual Draft ID and expected Draft revision/version, supplier-reported receipt
instant, and a Core document version containing the original offer. The server
uses its own UTC observation for entry/submission-window eligibility; reported
receipt time is evidence only and never backdates a late submission. The owner
loads the retained Draft version and current editable lines, verifies their
canonical snapshot/receipt, invitation and supplier, current source/branch,
original clean document version/hash and Core use pin. One serializable command
then freezes exact offer lines and totals, original evidence and actor/optional
worker alias into an append-only submitted version plus its original-key/body/
response receipt. The old Draft rows are not relabeled as supplier-submitted.
Same-key replay returns the original bytes after current actor/action/branch
checks; changed body/key binding conflicts. Unknown outcome permits GET-only
recovery. A correction requires an explicit successor submission; no overwrite.

Closing uses trusted server time at/after the configured close instant. In the
same transaction it locks the round and all its exact submitted versions,
captures an ordered set digest/count and immutable close actor/receipt, then
marks the round Closed by CAS. Broad reads still reveal no bid content. A
separate named opening transition must verify that watermark and independent
Core opening authority before any evaluator sees supplier prices or documents.
No portal delivery, award, PO, AP, stock or Finance effect follows submission
or close. Internal recorder identity never becomes supplier authentication.

The first authored source increment is the exact submission/close transition
contract and negative rule tests. Execution additionally needs mapped owned
round/submission/seal/receipt rows, same-save guards, exact composite owner
FKs, Core document use under one enlisted transaction, finite privilege and
actual PostgreSQL/normal HTTP proof. It must not reuse the P30 Draft status,
quotation snapshot encoder or existing publication approval as a shortcut.

### Seventh bounded slice — independent commercial-round approval contract

This is the prerequisite to an accepting round, with the same 28-section
sequence as sections 1–28 above. **Identification/purpose:** a distinct
`SOURCING_COMMERCIAL_ROUND` policy and `sourcing_commercial_round` Core case
approve a particular prepared-plan revision for supplier-response handling;
they never approve an award. **Scope:** one legal entity, RFQ source and branch,
one exact requirements/criteria pack, currency, original invited supplier
set, UTC open/close instants, one named closer and explicitly named independent
reviewers. No general role name or default amount threshold supplies an
approver. **Related records:** P25 approved demand, P29/P30 retained RFQ and
publication graph, P32 prepared candidate, Core approvals/documents and the
existing published policy lifecycle.

**Actors/entry/preconditions:** the current prepared-plan owner may request
commercial review only with its own named action and branch reach; a separate
configured current Core reviewer has `procurement-sourcing-commercial-approve`.
The owner resolves the original P30 publication approved case, invitation set,
source holds, prepared-plan/draft receipt, published P32 policy and Core
document use before opening the case. The named closer must have current
`procurement-sourcing-round-close` at the source branch. Current Core accounts
need no HRMS worker link; known linked aliases remain a denial-only SOD check.

**Workflow/states:** ReadyToReview → PendingCoreReview → ApprovedRound or
Returned/Rejected. The prepared candidate itself remains immutable. A Core
Approved state creates a new append-only P32 decision/effect and an accepting
round only after exact case, assignment, actor, policy, prepared plan, supplier
set and original source proofs are rebuilt. Return/Reject retain the case and
reason; a corrected proposal needs a new prepared revision and new case.
Sealed bids cannot start from Pending/Returned/Rejected. Closing is a later
independent command and does not reveal content; opening/evaluation/award each
need separate future authority.

**Permissions/rules/data:** configuration editor, commercial review requester,
commercial approver, manual-offer recorder and named closer are distinct Core
actions with empty new-role grants. Current tenant/company/branch and named
assignment filter before any count/read. Policy schema1 must explicitly say
ManualSurrogateOnly, require an original scanned offer document, require exact
UTC window and named approval/close principals. It may define multiple One/All
stages with exact principal IDs; no inferred threshold, evaluator, minimum bid
count, scoring formula or supplier portal actor. A reviewer cannot be the RFQ
maker, prepared-plan maker, original PR maker/requester or the same known
worker under another account. Freeze case/source version, prepared-plan ID/SHA,
published policy ID/version/rules SHA, close/open instants, named supplier IDs,
stage/assignment IDs, current alias observations, close principal/alias and
original requirements use set/digest in immutable opening evidence. The final
decision binds one real Core decision and a same-command result receipt.

**UI/communications/documents:** use the existing RFQ detail approval task and
original tabs; show plain Pending/Approved/Returned/Rejected and exact blocker
reasons. No supplier notice or price disclosure follows this internal approval.
Core owns retained requirement bytes/use pins. **Exceptions/integrations:**
missing policy or published identity, altered current source/supplier audience,
wrong branch, alias rebound, stale case, changed use pin, unavailable Core case
or unknown command outcome all fail closed. Core ApprovalEngine owns votes; P32
owns the exact commercial effect, receipt and audit/outbox declaration.

**Audit/metrics/config/NFR:** preserve original case and business snapshot,
current signer, reason, exact key/body/response hash, policy version and
source/document digests; do not expose sealed prices. Counts distinguish
pending cases from approved rounds. No policy defaults are shipped. Same-store
serializable transaction, finite SQL roles, scoped list-before-count and
actor-owned GET-only recovery are required. **Postcondition/failure:** only
one genuinely approved round may accept submissions; unknown writes remain
locked until exact receipt recovery. **Acceptance/tests/ownership/open:**
source/owner tests must prove independently published policy, Core case/alias
SOD, branch and supplier-set immutability, Return/Reject, stale CAS, duplicate
key, lost-response recovery and zero effects on each denied branch. Genuine
P32 DDL/readiness/private HTTP/browser gates remain outstanding. Business must
configure reviewers, closer and windows; Finance and award authority stay
outside this decision.

### Eighth bounded slice — retained commercial evidence owner proposal

**Identification, purpose and scope:** the additive P32 owner proposes six
append-only record families: round opening, every Core vote, the unique terminal
Core decision, manual offer submission, round seal and actor-owned command
receipt. They preserve
commercial evidence for one published RFQ and branch; they do not alter the
P30 sourcing publication, supplier bid Draft, Finance or award ledgers.
**Parents, actors and entry:** a genuinely published prepared plan and P32
operational policy identity precede a distinct Core commercial case. The
current requester, assigned reviewer, surrogate recorder and named closer
execute their own scoped actions under an enlisted serializable transaction.
Core identity does not depend on an HRMS worker; a known worker alias is a
denial-only SOD signal.

**Workflow, states and rules:** Open retains the exact plan/source/policy/case
and frozen supplier set; Decide records one complete Approved, Returned or
Rejected Core outcome. Only Approved plus server-observed open window permits
SubmitOffer. SubmitOffer retains the original document version/hash, exact
line snapshot and recorder without making that recorder a supplier identity.
Close at/after the window seals the actual persisted offer set. Return/Reject
cannot create accepting supplier authority. The Core history verifier checks
each stage, assignment, exact One/All quorum, order, terminal decision and
completion key. The published policy parser rejects repeated reviewers and
closer overlap before configuration can publish.

**Ownership, fields, security and recovery:** exact tenant/company/event/
branch keys link every new record; prepared plan SHA and operational policy
identity fields bind opening to immutable parents, and supplier bid and
invitation match event/supplier. Every Decide response targets its own vote;
only a terminal vote may also point to the one terminal decision. The terminal
decision points back to that vote by a one-way exact-owner FK, giving the
insert order Core vote → Procurement vote → terminal effect → receipt without
an EF ownership cycle. The optional receipt-to-terminal FK includes the vote
ID, so an acknowledgement for vote A cannot name terminal decision B from
the same round. A Pending One/All-stage vote has an actor-owned original
response and GET-only recovery but grants no supplier offer authority. Typed
receipt target FKs and same-save guards join each effect to the original
command. Reads/replay still require current
Core action and branch checks before returning the original response; a
receipt row alone grants nothing. No price appears in general sourcing lists.
Opening and offer snapshots are self-hashed and re-bound to outer IDs. Core
document use must be enlisted with the business command, never inferred from
an uploaded filename or a caller boolean.

**Screens, notifications, exceptions and NFR:** existing RFQ detail/tasks keep
the imported presentation and show pending review, returned reason, sealed
offer count and close status only to scoped roles. Unknown outcomes require
GET-only reconciliation; duplicate keys with different payloads conflict.
The server uses its UTC observation for all windows. There is no supplier
email, portal acknowledgment, scoring, award, PO, AP or payment effect here.
**Acceptance, tests, dependencies and open decisions:** pure transition and
EF-model/guard tests are authored but unrun. New model and store are source
only: no generated migration, physical ownership capture, finite privileges,
readiness implementation, endpoint registration or normal-cookie proof yet.
Any direct SQL path must preserve the same exact owner and append-only bundle
invariants; first Down must veto retained P32 evidence and linked shared
policy/Core histories. The business still must approve reviewers, closer,
windows and evaluation rules before runtime activation.

The retained `SupplierBid.InvitationId` is nullable for older draft/portal
rows. P32 leaves it nullable and adds a managed nonpartial unique index over
tenant, company, bid, event, supplier and nullable invitation. The authored
P32 migration companion adds the exact nonnull offer-to-bid invitation FK
against that index; a new offer cannot match a legacy NULL invitation. Before
enablement, actual PostgreSQL must prove a valid surrogate bid insert, a
retained NULL legacy bid remaining readable, and same-company wrong
invitation raw inserts rejected. The EF model or source tests alone do not
establish this physical result.

P32's six new owner tables follow the five already modeled commercial Draft
and preparation families, making eleven P32-owned families in the eventual
physical capture. Exact zero/six installation compatibility keeps old P30
manual quote Drafts writable before the new tables exist; a partially applied
six-table owner fails closed. The multi-stage Decide endpoint remains held
until the actual same-transaction Core vote, vote evidence, optional terminal
effect, original receipt and historical One/All replay are exercised against
the generated migration. A source-only InMemory guard cannot establish the
Core foreign-key and audit bundle.
