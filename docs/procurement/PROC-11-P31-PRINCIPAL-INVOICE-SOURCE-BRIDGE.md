# PROC-11 P31 — Principal PO and receiving invoice source bridge

**Status:** integration audit and next bounded milestone; source only. P30 PO and principal receiving activation, their owning migrations and runtime evidence are prerequisites, not outcomes of this document. Audit source: `/home/amizo/procurement-p30-integrated-source` on 23 October 2026. Apply GW-ENG-STD-001 and the 28-section feature standard.

**Source checkpoint after 563d8df5:** An isolated typed schema-2 model and an unregistered `POST /invoices/{id}/principal-matching/mappings` command now exist. The command requires current invoice and Core actor access, a genuine retained PO2/receiving read, available Core original document versions, a new P31 readiness provider, and one atomic mapping/state/receipt save. It returns `isUnitVerified=false`, `matchingAuthorityAvailable=false`, `canAllocate=false`, and `canPostToFinance=false`. The matching options default to disabled; no P31 provider, migration, Program registration, published policy or production capability is supplied by this checkpoint. The separate `GET .../commands/{key}` returns only the same actor's retained map response after current scope and action checks. Existing legacy invoice capture and matching routes are unchanged.

**Next owning steps:** Generate the additive Procurement migration from the actual combined P30 model and inspect all exact composite FKs, pointer ownership, nullable acceptance checks, append-only controls and downgrade veto against real PostgreSQL. Implement independent original-unit Core review with pinned document hashes and a separate purpose-only assigned reviewer reader; do not reuse P22's legacy mapping FK. Implement the source-wide authenticated exposure owner and complete legacy/principal credit/claim history under the same lock before a positive comparison; default owner remains unavailable. Only then may a typed `Compare` command create a claim and publish a capability. An unverified mapping is never an AP or payment status.

**Review transition source:** `PrincipalInvoiceUnitReviewTransitions` prepares a distinct `principal_invoice_original_unit_review` Core case from the exact P31 mapping, original document SHA pins and published P22 policy shape. It retains assignment principal and optional worker aliases, and the owning guard requires the same Core case graph and review receipt in the enlisted transaction. There is no registered review HTTP command or completion service yet; constructing a case plan does not verify original units. Existing P22 routes remain bound to legacy mappings.

## Current contract and precise gap

Manual supplier invoice intake already persists `SupplierInvoice` and lines as `Received` through `ProcurementInvoiceIntakeRoutes.cs` and `ProcurementInvoiceIntakeEditing.cs`, with tenant/company/branch scope, exact amounts, duplicate handling and no employee prerequisite. The principal-based invoice matching actor in `ProcurementInvoiceMatchingRoutes.cs` resolves a current Core principal and retains an optional worker alias. P22 original-unit review (`ProcurementInvoiceUnitReviewRoutes.cs`, `ProcurementInvoiceUnitReviewCommands.cs`) likewise uses current Core principal, independent reviewer and purpose-scoped original documents. These **identity paths** can be reused.

Their **commercial and physical source paths cannot yet read schema-2 PO/receiving facts**. `ProcurementInvoiceMatchingSelectors.cs` joins `OrderIssuedVersions`; `ProcurementReceivingInvoiceSelectors.cs`, `ProcurementReceivingInvoiceProjection.cs` and `ProcurementReceivingInvoiceReviewSource.cs` verify worker-era `OrderIssuedVersions`, `ReceivingSourceStates`, `GoodsReceiptLines` and `ServiceAcceptances`. The selector filters acceptance rows to `ExecutionVersion == 1`. `ProcurementInvoiceMatchingCommands.cs` calls that projection for mapping and comparison. `ProcurementInvoiceUnitReviewCommands.cs` and `ProcurementInvoiceUnitReviewReads.cs` call the legacy issued projection. A schema-2 PO may therefore be captured on an invoice but is **not** a verified matching or unit-review source. Do not reinterpret an Accepted supplier acknowledgment as delivery.

The new source owners are `ProcurementPrincipalOrderRetainedIssued.cs` (`QueryBoundIssued` before count/page; `LoadAsync` complete retained PO, source, Core approval and Accepted acknowledgment proof), `PrincipalReceivingSourceState`/`PrincipalReceivingFact` in `PrincipalReceivingPersistence.cs`, and `PrincipalReceivingLedger`/`PrincipalReceivingAcceptance` in `PrincipalReceivingContracts.cs` and `PrincipalReceivingEvidence.cs`. The principal receiving projection currently says `matchingAuthorityAvailable=false`; this is a truthful hold. Source state pins PO/line/issued/submission/funding/acknowledgment/PR line/binding and digests; fact stream distinguishes observation, inspection and **positive acceptance**. No fact is a Finance, stock or asset posting.

`InvoiceMatchMappingVersion` and `InvoiceMatchAllocation` (`InvoiceMatchingExecutionRecords.cs`) retain a single `IssuedVersionId` and optional **legacy** `GoodsReceiptLineId`, `ServiceAcceptanceId`, `ReceivingSourceStateId`; their existing FK/check/owner rules must be inspected before adding a separate, typed principal acceptance reference. Never put a `PrincipalReceivingFact.Id` in a legacy receipt/service ID column or copy principal facts into legacy receiving tables. `InvoiceIssuedOrder` and `InvoiceAcceptedFulfilment` (`InvoiceMatchingContracts.cs`) are reusable immutable application projections if a versioned adapter verifies their source first; do not change worker-era encoders or infer a complete owner history from local empty rows.

`ProcurementInvoiceMatchingCommands.cs` additionally requires a published matching policy, P22 retained original-unit review or authenticated document reviewer, and `IInvoiceHistoricalExposureOwner`. The default exposure owner is **Unavailable**. A valid PO/receipt bridge alone must not produce `ComparisonPassed` or a capacity claim when original units, owner coverage or complete acceptance history are missing. `ProcurementInvoiceFinanceReadinessRoutes.cs` is read-only explanation, not AP handoff. `ProcurementManualPaymentInvoiceReads.cs` already offers a payment-only, current Core principal, branch-scoped `Received` invoice picker and labels; its preview explicitly does not claim capacity. The independently approved manual external-payment workflow may continue to consume a received invoice under its own policy, but neither a PO2 match nor a manual confirmation may be relabeled Finance-posted or bank-paid.

UI reuse: `reference-ui/src/features/LiveInvoices.tsx` already has capture/detail, document panel, comparison panel and original-unit-review entry, guarded by distinct capabilities and grants. `InvoiceMatching.tsx`, `InvoiceReview.tsx`, `LivePayments.tsx`, and their `src/platform/procurement-{invoice-matching,invoice-review,invoice-unit-review,manual-payments}-api.ts` clients are the existing surfaces. Keep the imported detail tabs and line editor. Extend the source picker/acceptance evidence labels only after the backend contract and capability are real; old rows remain on their old route. A separate payment panel is not a substitute for invoice comparison.

## 1. Feature Identification

PROC-11-P31: verified principal issued-PO and positive receiving acceptance source for existing supplier-invoice mapping/comparison and original-unit review. This is a versioned source adapter, not a new invoice master.

## 2. Purpose and Business Outcome

Let an authorized Core user map a captured invoice to an actual schema-2 issued PO and its actual accepted goods/service quantities while retaining original documents, decision history and exact exposure controls.

## 3. Scope and Exclusions

First complete slice: scoped source and acceptance discovery, retained principal-source projection, typed mapping/claim references and P22 issued-source review compatibility, with fail-closed comparison. Exclude AP posting, tax ledger, supplier payment, bank execution, stock and asset entries, automatic receiving correction/return and unverified legacy adoption. If owner-history coverage cannot be provided, show the source and blocker but do not mint a matching claim.

## 4. Parent and Related Features

PROC-07 principal PO; PROC-09 principal receiving; existing PROC-11 intake/matching/unit-review; PROC-12 manual external payment. Core owns accounts, actions, branch scope, documents and approval case. Finance/AP owns liability, posting, settlement and bank evidence.

## 5. Actors and Participants

Invoice capturer, mapper/comparer, assigned original-unit reviewer and auditor are current Core principals. Worker aliases are optional denial-only identity links; no employee row is required. Finance/AP is an external owner, not a role inferred from Procurement match success.

## 6. Entry Points and Triggers

Existing `/api/procurement/v1/invoices` CRUD, `/matching/sources`, `/matching/sources/{orderId}/lines/{lineId}/acceptances`, `/matching/mappings`, `/matching/compare`, `/unit-reviews` and `/finance-readiness`. Add version discrimination within these bounded source contracts, not a broad unscoped PO directory.

## 7. Preconditions

Actual P30/P31 schema/finite-role readiness and enabled capability; current published matching and unit-review policy; invoice Received/current and same tenant, company, branch, supplier and currency; current authorized Core action; complete retained issued2 and receiving ledger proof. Original document review and authenticated exposure owner remain independently required for a qualified claim.

## 8. Workflow

Capture invoice → search scoped PO2 lines (source fence before count/page) → inspect real positive acceptance references or explicit no-acceptance state → save immutable mapping revision → original-unit review where required → compare with complete source/owner evidence → show discrepancy or qualified Procurement claim. Preserve existing recovery and history endpoints. Matching and manual payment remain separate workflows.

## 9. Status / State Transitions

Existing invoice Received → MappingUnverified → ComparisonBlocked or ComparisonPassed; original-unit review Open/Pending → verified/returned/rejected under its own case. No new `Posted`, `Paid` or `FinanceApproved` transition. A missing or stale PO/acceptance/owner fact yields `Unavailable`/blocker, not zero.

## 10. Permissions and Data Scope

Reuse `procurement-invoice-read`, `procurement-invoice-capture`, `procurement-invoice-match`, P22 request/review actions. Resolve current Core principal and company/branch before source count, page, detail or replay; bound allowed branches explicitly for `QueryBoundIssued`. P22 assigned reviewer still needs purpose scope, not broad PO/PR read. Never let old global worker-only scope decide schema-2 reach.

## 11. Business Rules

Retained PO2 approval, funding, issue, supplier acknowledgment, source digest and branch pins must verify at read time; acknowledgment proves only supplier response. Count only `PrincipalReceivingAcceptance` positive facts after full ledger/counter reconciliation, inspection/rejection/return disposition and immutable receipt binding. Two-way matching may omit acceptance only where the published category rule permits; three-way matching needs actual accepted evidence. Preserve published tolerances and exact decimal values. Missing history, identity or evidence blocks a claim.

## 12. Data Fields and Ownership

Keep `SupplierInvoice`/`SupplierInvoiceLine` as sole invoice originals. A proposed additive typed principal mapping source must retain schema discriminator, PO/issued/line/source-state IDs, positive acceptance fact ID, source/effect digest and ledger sequence; inspect existing `InvoiceMatchMappingVersion`, `InvoiceMatchAllocation`, `InvoiceMatchExposureState` FK/check/unique rules before selecting exact schema. Do not overload legacy receipt/service IDs. Procurement owns these commercial facts; Core owns people/documents; Finance owns AP/payment status.

## 13. Screens and Data Views

Reuse LiveInvoices detail and InvoiceMatching/InvoiceReview panels. Show PO number, supplier, ordered/accepted/previously claimed exact quantities and a clear `source unavailable` reason; hide false comparison authority. Principal receiving labels come from retained scoped records, not UUIDs or current catalog price. Preserve original tabs/actions and truthful capability gating.

## 14. User Experience / Human Behaviour

User chooses only a real currently visible PO2 line and real acceptance; unavailable sources remain explainable without inviting blind retries. Unknown write outcome locks repeat and uses actor-owned GET receipt recovery. Keep exact quantities as strings and old worker-era invoices readable.

## 15. Notifications and Communications

Existing invoice exception/assigned review notifications only after committed records. No message says a supplier has been paid or Finance has posted a liability from comparison alone.

## 16. Documents and Attachments

Reuse Core document version/hash ownership for original invoice units and P22 purpose-scoped pinned downloads. Receiving physical evidence references do not stand in for scanned original invoice lines.

## 17. Exceptions and Edge Cases

Mixed legacy/principal source IDs, stale issued or acceptance sequence, reversed/held/rejected goods, partial service, offboarded original signer, source branch/supplier mismatch, duplicate invoice, missing original units, missing owner history and 2-way/3-way policy disagreement all fail closed with specific blockers. Do not silently union incomplete histories.

## 18. Integrations / APIs / Events / Sandbox

Build a versioned internal source reader behind existing matching routes and an issued-only P22 reader. Use the same enlisted Procurement/Core transaction and explicit Postgres source locks in canonical order for mapping/compare; list discovery is read-only and fenced before paging. Existing compared event remains a Procurement fact with `canPostToFinance=false`. No new Finance handoff event until Finance owner contract and proof exist.

## 19. Audit / Security / Privacy

Retain actor principal, optional worker alias, policy/digests, original line/document versions, source state/acceptance fact/receipt pins, idempotency key, prior/new invoice pointers and reason. Keep supplier banking/person data out of purpose selectors. Source-corruption must not leak another branch's row in totals.

## 20. Reports / Dashboards / Metrics

Count principal-source invoices as captured, mapped, compared, blocked or under review by actual state; do not count a mapping as a qualified match or manual external payment as Finance paid. Distinguish unavailable owner coverage from zero prior claims.

## 21. Configuration and Localization

Reuse published invoice matching category/currency/tolerance/2-way/3-way rules and original-unit-review policy. No invented default threshold, rounding or country tax rule. Display locale may vary; persisted exact source amounts and hashes do not.

## 22. Non-Functional Requirements

Bound selector rows and complete retained ledger loads. Enforce pre-count scope, finite role/schema readiness, same physical transaction for proof, optimistic concurrency and idempotent response receipts. Keep old worker-era readers stable and mixed-mode costs measured before enablement.

## 23. Postconditions

Successful comparison stores only a validated Procurement match/result/claim tied to original invoice and genuine source evidence. Blocked comparison stores a reason/history without AP, stock or payment side effects.

## 24. Failure and Recovery

Rollback invoice pointer, mapping/result, allocation, outbox, audit and receipt atomically. An uncertain POST is reconciled by exact actor-owned GET receipt; mismatched fingerprint/key or changed scope denies. Stale source/policy/owner proof needs new review, not silent automatic retry.

## 25. Acceptance Criteria

Workerless Core capturer/mapper and assigned reviewer can complete the verified source path with real grants. A scoped PO2 line is visible before count/page only after retained proof; an acceptance is usable only when its complete positive fact/receipt history reconciles. Legacy source IDs remain unchanged. Missing owner/original-unit evidence blocks a claim. No Finance posting/payment/stock row is created.

## 26. Test Scenarios

Author typed projection/precision and mixed-ID denial tests; mapped source-before-count, Core alias/SOD, 2-way/3-way partial/held/rejected/returned, stale ledger/receipt, historical policy, duplicate/exposure and unknown-result recovery tests. Parent later runs genuine migration/snapshot/readiness, finite-role PostgreSQL negatives, current private HTTP and imported UI browser tests. Source-authored tests alone are not acceptance evidence.

## 27. Ownership / Dependencies

P30 PO and P31 receiving source contracts must be reviewed and actually activated first. Invoice team owns only the versioned bridge/mapping and match; Core document/case services retain their authority. Finance owner coverage may remain unavailable, which correctly holds qualified claims. Parent owns generated migration and release gates.

## 28. Open Decisions / Assumptions

Decide the additive FK/unique/retention design for typed principal acceptance mapping after inspecting actual invoice schema and owner SQL, and whether a published category allows two-way principal services before acceptance. Decide the authenticated historical exposure owner interface for qualified claims. Confirm P30/P31 actual rollout and UI capability names only after real schema/role/HTTP proof. No AP or bank integration is assumed.

### Source checkpoint: read bridge, not match activation

The isolated source slice adds `PrincipalInvoiceSourceProjection` and unregistered
`ProcurementPrincipalInvoiceSourceBridge` / `ProcurementPrincipalInvoiceSourceRoutes`.
The bridge composes the P30 retained-issued SQL and current Core branch fence
before count/page; its bounded detail reader verifies the full retained PO2,
reconciles complete principal receiving/PR/PO counters, rejects mixed legacy
histories, verifies fact-to-command receipts and retained publication, and exposes
only positive acceptance IDs/sequences/exact quantities and amounts. The old
invoice selector and comparison routes are unchanged. The proposed read routes
are `/api/procurement/v1/invoices/{id}/principal-matching/sources` and
`/.../sources/{orderId}/lines/{lineId}/acceptances`; they are not registered in
the normal host and require an independently enabled option and both actual
P30/P31 read readiness contracts. Every response declares
`matchingAuthorityAvailable=false`, `canAllocate=false`, and
`canPostToFinance=false`. There are no POST routes, new invoice rows, schema
changes, Finance facts or production capabilities in this slice.

### Next additive owned schema and command contract (requires separate review)

The current `InvoiceMatchMappingVersion`, `InvoiceMatchResult`,
`InvoiceMatchExposureState`, `InvoiceMatchAllocation` and
`InvoiceUnitReviewSource` have hard owning FKs to *legacy*
`PurchaseOrderIssuedVersion` / legacy receiving rows. Their existing SQL
overlay `InvoiceMatchingOwnershipConstraintSql` binds goods/service IDs to
legacy receipt/service and source state IDs. A PO2 ID cannot enter those columns
without violating ownership. The next change therefore needs distinct
principal source records and command receipts, or carefully reviewed additive
discriminated FK pairs; it cannot only add an `executionVersion` flag.

Suggested minimum owned records (names to confirm against generated metadata):

| Record | Stable ownership and required pins |
| --- | --- |
| `PrincipalInvoiceMappingVersion` | Invoice ID/version/original digest, tenant/company/branch, supplier/currency, principal PO2/issued ID, ordered invoice-line membership, each PO line and optional principal acceptance **fact** ID, source-state ID and sequence, original document version/line reference, actor Core principal/nullable denial-only worker alias, immutable mapping digest/revision. |
| `PrincipalInvoiceUnitReviewSource` | Exact principal mapping and issued IDs/digests, pinned Core document versions/hashes, current original invoice units, distinct Core review case/assignment/decision and actor-owned command receipts. Reuse P22 review policy and purpose document reader, never its legacy issued FK or worker-only envelope. |
| `PrincipalInvoiceComparison` | Mapping and original invoice/version, published matching policy/digest, retained issued/acceptance watermarks, review outcome, authenticated exposure-owner watermark/lease, exact result/exception/evidence digest and command receipt. `Compared` is Procurement validation only. |
| `PrincipalInvoiceExposureState` / `PrincipalInvoiceClaim` | PO2 issued/line and actual positive acceptance fact IDs with a current owner completeness lease, exact claimed quantity/net/tax, invoice/result links, CAS sequence and append-only claims. No local empty-table-as-complete inference. |

Model FKs must include tenant/company and exact parent pairs. A claim acceptance
FK should bind `(tenant, company, acceptance_fact_id, principal_source_state_id,
PO_id, issued_id, PO_line_id)` to an immutable unique parent projection or a
deferred exact owner overlay; its accepted kind/positive quantity must be
checked against the retained fact and current complete ledger. Two-way rows
must have **no** acceptance fact and a published category rule permitting that
method. Three-way rows require one actual positive principal acceptance fact.
All parent/child names, nullable CHECK logic, append-only guards, indexes,
owner privilege manifests and Down veto must be generated/rehearsed after the
P30/P31 migrations are accepted. Existing worker-era rows and literal encoders
remain byte-for-byte stable.

The principal comparison command can reuse the pure `InvoiceMatchingFoundation`
only after its typed source, original-unit review and authenticated exposure
owner contracts are provided. It must not call the legacy `InvoiceEvidence` or
write `InvoiceMatchAllocation` using principal fact UUIDs. Keep the new matching
capability false and the old P22/legacy read paths intact until a genuine
same-transaction PostgreSQL/finite-role/HTTP/browser proof passes.
