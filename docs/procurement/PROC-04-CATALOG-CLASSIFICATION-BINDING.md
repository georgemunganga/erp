# PROC-04 — Catalog classification at the request/commitment boundary

Tracking: [#32](https://github.com/georgemunganga/module-connect/issues/32), separate adjacent trust correction.
Status: source implementation/regressions authored; no build, database or browser verification, no deployment claim.
Base: root `fd1c7d0`; isolated branch `codex/procurement-catalog-classification-binding`.

## 1. Feature Identification
PROC-04 catalog request classification binding, additive contract for PROC-02 requisitions and PROC-M5 controlled source conversion.
## 2. Purpose & Business Outcome
An employee selecting a Goods catalog item cannot declare SERVICE or another category to select a different purchasing/approval/receiving policy path.
## 3. Scope & Exclusions
CatalogItemId-backed new/edited PR lines, PR submission and new source-controlled commercial actions. No alternative master, synonym inference, arbitrary requester override, legacy rewrite, inventory classification guess or financial ledger.
## 4. Parent & Related Features
PROC-04 governed catalog, PROC-02 request and policy evidence, approved source allocation and PO execution, P25 sealed receiving classification.
## 5. Actors & Participants
Item steward owns item Kind/CategoryCode; requester selects item; existing buyer, approver and Finance actors retain their current capabilities.
## 6. Entry Points & Triggers
Create/update PR; submit stored PR; buyer approved-source eligibility/conversion; source PO edit, independent funding confirmation, commercial submit and issue.
## 7. Preconditions
Existing scoped item must be Active and purchasable, and owner Kind/category must produce supported canonical policy codes. Request type retains its existing 40-character storage bound; category is bounded to 100.
## 8. Workflow
Editable input validation loads existing scoped item owners with the existing item reference query. Omitted/blank type/category fill from the owner. Explicit values normalize with ProcurementPolicyCode then must equal owned canonical values. Validate every line/reference before changing header/lines. At submit/new commitment, existing stored values must already equal those canonical owner codes; reject missing, mixed-case, mismatching or unverifiable evidence rather than repairing it.
## 9. Status / State Transitions
No new states. Rejected input remains a draft. Approved/source/issued snapshots stay unchanged. Normal permitted draft correction/resubmission is available under existing version/Finance-release rules.
## 10. Permissions & Data Scope
Existing request ownership, worker, permissions and query filters apply. Existing PO Core current actor/branch fence and shared serializable transaction apply. The helper does not accept a caller-supplied authority/override flag.
## 11. Business Rules
`BR-PROC-CAT-007`: catalog classification comes from its owned item, not the employee's policy hints. No governed requester-override policy was found in PROC-04 §§8/10/11/12/19, PROC-02 or configuration. Therefore conflicts deny by default. A noncatalog line remains the existing explicitly controlled path; it cannot retain a CatalogItemId and override that owner's classification. No synonyms map GOODS to SERVICE or SUBSCRIPTION to SERVICE.
## 12. Data Fields & Ownership
Procurement's existing CatalogItem.Kind/CategoryCode is current source authority for this catalog adapter. Its display casing stays unchanged. PR persists derived canonical codes. Existing item/catalog master, PR/policy/submission/issued JSON are reused; no new column/table or competing owner.
## 13. Screens & Data Views
Existing catalog fields are already read-only in LiveRequests. API enforces the same contract for direct callers. Buyer source list reports NeedsReview with OrderSourceCatalogClassificationUnverified when binding is unproved.
## 14. User Experience / Human Behaviour
Omitted fields are filled transparently; explicit conflicts show a clear catalog classification error. Historical reads remain available. Restoring genuine owner/source agreement permits a new explicit command; records are not silently rewritten to make it pass.
## 15. Notifications & Communications
Existing audit/outbox only on successful commands. No new notification service.
## 16. Documents & Attachments
No changes to document ownership or attachments.
## 17. Exceptions & Edge Cases
Blank/invalid owner fields, inactive/missing owner, foreign-entity reference and item reclassification after draft/approval fail closed for new authority. Item edit requires deactivation and withdraws published prices; current submission already rejects stale publication versions. A missing price reference does not waive item classification binding. Classification drift after an approved source requires review rather than editing its pin.
## 18. Integrations / APIs / Events / Sandbox
Existing DTO shape and API routes remain. One pure Application helper and one scoped API resolver are reused by request and PO command boundaries. PO guards run after current identity/receipt checks but before new commercial mutation. Actual historical command receipts acknowledge recorded results, not newly qualified catalog authority; existing authorization remains required. Cancel, Finance release, delivery acknowledgement and historical read do not add this mutable-owner guard, preserving safe cleanup of existing obligations.
## 19. Audit / Security / Privacy
Conflicting create/edit cannot write header/lines/audit/outbox. New commitment denial cannot leave allocation/case/evidence/receipt effects because it executes under the existing shared transaction. An old mismatched approved source remains visible but cannot be used to create/approve issue of a new commitment. Sealed receiving still uses retained issued evidence, not current mutable catalog labels.
## 20. Reports / Dashboards / Metrics
No retroactive reclassification or historical report rewrite.
## 21. Configuration & Localization
Strict invariant policy codes shared with existing rule validator; display casing and unit contract remain owned independently. No override configuration is invented.
## 22. Non-Functional Requirements
Batch scoped item lookup, at most existing 100 PR lines. No per-line owner network calls, schema migration or duplication of Core/Finance/Inventory services.
## 23. Postconditions
Saved catalog PR line matches owned canonical category/type; current owner display Kind remains unchanged. New source-controlled commercial effects require proven current binding.
## 24. Failure & Recovery
422 for invalid draft/submission, 409 OrderSourceCatalogClassificationUnverified for new PO commercial actions. Existing request correction and governed catalog maintenance provide recovery. Finance release and unfunded cancellation must remain usable despite classification drift; this correction does not erase history or simulate release.
## 25. Acceptance Criteria
Omitted catalog fields fill GOODS/owner category; matching mixed-case input works; SERVICE/other-category spoof fails before mutation. An inherited tampered draft cannot submit or create policy evidence. Genuine GOODS-only policy matches an owner-bound line. Approved original evidence remains unchanged when current catalog drift blocks conversion/funding/submit/issue.
## 26. Test Scenarios
Authored mapped TestServer catalog publication scenario now asserts spoofed create/edit no persistence (fresh-context request/line/audit/outbox counts and exact unchanged reload), omitted owner fill, GOODS-only actual policy match, inherited tampered stored draft submission denial and unchanged owner Kind. Pure tests cover conflicts, absent/invalid owner, synonyms and exact stored semantics. Disposable actual PostgreSQL --orders probe source creates a genuine item-backed PR through request/budget/Core approval, changes only synthetic current owner data, requires exact classification blocker and unchanged PO/allocation/evidence footprint for conversion, funding, submit and issue; restoring owner agreement positively executes each step. These are authored scenarios, not test results.
## 27. Ownership / Dependencies
Procurement Application owns binding invariant; API owns scoped owner resolution. Existing Core actors/scopes/fence/approval engine, Procurement pins/transactions and owned item records are reused. Root owns centralized builds, actual SQL/HTTP/browser gates and distinct review before merge/enablement.
## 28. Open Decisions / Assumptions
No authorized catalog classification override exists in the reviewed contract. If the business requires one, define a separately governed exception with independent authority and pinned evidence before supporting it. Existing historical mismatches are not backfilled. No completion, production activation or independent-review claim is made by this source checkpoint.

## Distinct review correction — current owner/offer linearization

The first source checkpoint `8c03a52` was **not sufficient**: a SERIALIZABLE transaction can
acquire an old snapshot before waiting on its PR/PO lock, then see an older catalog owner.
There is no guaranteed SSI cycle to abort that schedule. Sequential drift tests did not
prove queued writer/reader safety. Its stored checker also did not require a selected price
publication to remain eligible for new commitments. The following source changes address
those two findings; they remain unexecuted until the centralized gates run.

### Explicit lock/snapshot contract (sections 7–9, 18–19, 22, 24)

- Reuse the existing Core physical session advisory-lease mechanism via an extracted
  `PostgresSessionAdvisoryLease`. CoreBranchAuthorizationFence retains its exact public API,
  key prefix/hash bytes and shared/exclusive cleanup behavior. Capability owners still own
  their keys and business policies; the primitive supplies locking only.
- Procurement defines a separate tenant + legal-entity catalog key. Order/coordinated
  commands acquire **Core branch shared → catalog shared → BEGIN SERIALIZABLE → domain locks**.
  Both leases are released after transaction/context disposal; every retry reacquires them
  before its new snapshot. This is intentionally a coarse company catalog fence.
- All eight existing item/catalog mutation routes (Create, Update, Activate, Deactivate,
  AddEntry, UpdateEntry, PublishEntry, WithdrawEntry) are covered by a mutating group endpoint
  filter. It acquires **catalog exclusive before the handler's first owner read or BEGIN**,
  uses the same open physical connection and holds it through Save/commit. PublishEntry
  retains its own existing transaction; no nested transaction is added. Deactivation and
  its published-entry withdrawals remain one Save under the same exclusive lease.
- PR Create/Update/Submit acquire catalog shared before any catalog owner query; Submit's
  existing SERIALIZABLE transaction starts after acquisition. These paths do not acquire a
  later Core branch session lease. Writers never acquire a Core branch lease, and no code
  upgrades shared catalog to exclusive. Read-only item/catalog browsing is not authority.
- **Reader-first:** once the command holds shared catalog authority it is ordered before a
  later catalog writer, even if waiting on a PR/PO domain lock. The writer must wait for the
  command transaction to finish. **Writer-first:** the command waits before BEGIN/snapshot,
  then observes the writer's committed retirement, offer change or classification.
- Acquisition observes cancellation. Failed/uncertain acquisition or unlock evicts/closes
  the physical session; unlock cleanup has the inherited five-second bound. Coordinator
  retries remain the existing three attempts only for confirmed PostgreSQL transaction
  aborts. No automatic retry of unknown commit/network outcomes is added.
- Nonrelational test-provider paths cannot prove session locking and explicitly bypass it;
  production relational paths require pinned Npgsql sessions (no multiplexing/no-reset).
  Direct SQL, migrations or future import writers must join this protocol before being
  supported owner mutation paths; arbitrary out-of-band DB writes are not certified here.

### Selected published offer contract (sections 8, 11–12, 17–18, 23–26)

The owned item binding now shares one pure `OfferMatches` predicate with existing draft
price validation. New stored-source commitments additionally require the selected
CatalogEntry to remain Published, current pinned PublishedVersion, effective now, broadly
eligible (no unresolved audience/site/contract restriction), and bound to exact item,
preferred supplier, unit, currency, unit price and permitted original PR requested quantity.
A pin preserves historical intent/price; it is not perpetual permission to buy against a
withdrawn offer. Missing legacy publication version fails closed. A mismatching current
item returns `OrderSourceCatalogClassificationUnverified`; an unavailable/stale selected
offer returns `OrderSourceCatalogOfferUnavailable`.

Actual historical durable receipt acknowledgement still follows current identity/scope
checks without a new eligibility decision. Cancel, release, historical read and delivery
acknowledgement do not require current catalog/offer eligibility, so legitimate obligation
cleanup is not stranded. They may use the coordinator's coarse shared lease but never
reinterpret an old receipt as a new authority grant. Issued/approved evidence is unchanged.

### Authored verification additions

`CatalogPublicationFenceVerification` adds a separate mapped catalog-owner TestServer on the
guarded synthetic bootstrap connection; the PO app retains its restricted runtime identity
and SELECT-only catalog grants. Reader-first: hold the PR row, queue actual conversion,
then actual mapped withdrawal must wait on catalog exclusive; release source lock, conversion
commits first, then withdrawal succeeds. The exact old conversion receipt remains readable;
a fresh-key conversion on the same approved source fails with the offer blocker and no
commercial footprint change. Unfunded cancellation remains possible after withdrawal.
Writer-first: hold catalog entry row, queue actual mapped withdrawal until its UPDATE waits
while owning the exclusive lease, then queue conversion, which must wait on shared catalog
before snapshot. Releasing the row lets withdrawal commit; conversion must fail with the
specific offer blocker and no new effects. Both schedules use bounded observation/waits.
Pure offer tests additionally cover publication version, effective-date boundary, owner/vendor,
price, quantity and unresolved audience/site/contract restrictions. Existing branch cleanup
and reader/writer probes exercise the extracted same lease mechanism too.

No SDK, SQL, database, browser, migration or production action has run for these additions.
Distinct review and actual mapped PostgreSQL concurrency evidence are required before this
correction can be described as verified or enabled.

## Distinct review correction — actual PO commercial intent and price tiers

Original PR selection eligibility does not prove a partial or edited PO qualifies for the
selected offer. A PR for ten may satisfy minimum ten while a later PO for one does not. No
cross-line/request/PO tier aggregation or negotiated replacement mechanism is implemented.
Therefore the catalog path applies min/max **per actual PO line** and fails closed rather
than inferring an aggregate discount.

At conversion before Reserve, source edit before ApplyLine/ResizeReservation, and new Finance
confirmation/commercial submission/issue, the selected current pinned catalog entry must
match actual PO supplier, source item identity, PO unit, currency, unit price and line quantity.
The original PR classification/publication checks remain necessary and unchanged. A source
edit validates a detached proposed line projection plus proposed supplier before any tracked
line/header/allocation mutation. No current command is rescued by rewriting historical terms.
`OrderCatalogCommercialTermsMismatch` explains an actual offer mismatch; old command receipt
recovery, cancellation and Finance release remain outside new commercial qualification.

PO units already fold case, so actual offer matching uses the established OrdinalIgnoreCase
unit convention (`each` and `EACH`), without EA/EACH synonyms or whitespace inference. Original
PR draft/stored catalog selection retains its previous exact unit semantics; the pure shared
offer predicate exposes the explicit commercial comparison mode only at this boundary.

Authored pure tests demonstrate original PR10 passing while actual PO1/min10 fails; max,
price, vendor, unit/synonym/whitespace and currency changes fail, with valid case-equivalent
PO units accepted. The actual mapped PostgreSQL probe additionally seeds a published min2/max2
offer and a genuinely approved PR2. New conversions for1/3, price99, another active vendor,
BOX and USD require the exact commercial blocker and no persisted effects. A valid PO2 with
EACH succeeds; proposed source edits below MOQ or substituting price/vendor/unit fail before
mutation. Withdrawal then leaves its exact historical conversion receipt unchanged and
unfunded cancellation usable. These are authored sources, not executed test evidence.
