# PROC-11-P26 — Invoice matching persistence and API adapter

**Status:** Proposed next milestone; source audit and implementation contract only. No matching API, migration, provider, approval or Finance posting is implemented by this document.
**Baseline:** Root `5278267`, reviewed invoice matching foundation and planned P25 receiving adapter. Issue [#30](https://github.com/georgemunganga/module-connect/issues/30); dependencies [#31](https://github.com/georgemunganga/module-connect/issues/31) and [#5](https://github.com/georgemunganga/module-connect/issues/5).
**Standards:** GW-ENG-STD-001 Tier 3, existing 28-section child-feature framework. Complements [PROC-11B](PROC-11B-INVOICE-MATCHING-COMPARISON.md), [P25 adapter](PROC-09-P02-RECEIVING-PERSISTENCE-API-ADAPTER.md) and [P26 implementation plan](PROC-11-P26-IMPLEMENTATION-PLAN.md).

## 1. Feature Identification

Procurement → Supplier Invoices → Invoice Matching → Verify mapping, compare, retain evidence, reserve Procurement billing capacity, assign/resolve discrepancies and invalidate stale results.

The bounded operational result is **ComparisonPassed with a current Procurement allocation claim**, or **ComparisonBlocked** with owned discrepancies. Neither result is invoice approval, tax validation, AP authority or payment. Existing intake remains usable when execution dependencies are unavailable.

## 2. Purpose & Business Outcome

An AP processor can map an original invoice to exact issued order lines and accepted goods/services, understand discrepancies and retain a retry-safe result. Two invoices cannot each consume the same remaining billable capacity through concurrent matching. History can explain what evidence was considered, what was reserved and why a later source change requires review.

## 3. Scope & Exclusions

Supports a single delivered issued PO revision per invoice, independent invoice-unit evidence, category/currency-specific two-/three-way policy, partial goods/services, exact policy rounding, complete prior debit/validated linked-credit history and immutable results. A passing comparison may create a Procurement capacity claim only with authoritative complete exposure coverage.

Excludes non-PO invoices, FX/UOM conversion, arbitrary service milestone values, amended-revision carryover without a reviewed map, approval/variance override, new credit execution/application, Finance/AP posting and payments. Importing validated historical credits is evidence of an existing owner decision, not permission to create one. Unknown Finance/AP exposure remains unknown, never an empty set. Department-confined matching remains blocked until an authoritative invoice department adapter exists.

## 4. Parent & Related Features

Reuse SupplierInvoice/Line (intake), InvoiceMatchResult (immutable comparison), InvoiceException (assigned discrepancy), existing normalized duplicate indexes, FinanceInvoiceHandoff and FinancePaymentStatusRef (references/status only), existing Core documents and policy lifecycle. Reuse Procurement transaction coordinator, Core tenant branch fence, current principal resolver, shared audit/outbox and exact arithmetic.

P25 owns accepted truth. Finance owns financial exposure, liabilities, credit application and postings. P26 owns commercial matching claims, not a competing AP/stock ledger.

## 5. Actors & Participants

Current authorized AP matcher, mapping verifier, discrepancy owner, policy author/reviewer, Finance exposure owner/integration principal and auditor. Buyer/receiver/service owner supply corrections in their own domains. A current permission is distinct from record scope and independent approval authority. Resolve canonical WorkerIds; account aliases cannot bypass maker/checker. No supplier portal principal can certify internal receipt or matching authority.

## 6. Entry Points & Triggers

Reuse Bills list/detail and document panel in `LiveInvoices.tsx`. Add a mapping/comparison panel and owned exception queue in those layouts. Match starts explicitly from the received invoice, or from a controlled rerun after a mapping/source/policy/history change. External events can mark review needed but do not autonomously approve or post an invoice.

## 7. Preconditions

At command execution: current actor/action/company/selected branch; unchanged intake and mapping revisions; accessible scanned original document; exact verified invoice UOM for every mapped line; supported immutable issued/submission/source evidence; applicable published match policy; required current P25 accepted history; complete company duplicate identity and complete billing/Finance exposure coverage with a verified watermark.

Invoice status must be Received/MatchPending/Exception (or an explicitly retained equivalent for rematch), not posted/paid/credited. A Finance handoff attempt with an uncertain result blocks release/rematch, even if FinancePostingRef is null. Missing provider, policy, coverage, owner identity or history produces a blocker, not a self-declared successful projection.

## 8. Workflow

1. Capture via existing intake; preserve original quantity/price/tax, invoice identity, line IDs and document history.
2. Save a versioned line-mapping proposal: exact issued revision/PO line, existing receipt-line or service ID where required, invoice unit as stated in original evidence, independent verification and document/version/line reference. Do not copy UOM from the PO and label it verified.
3. Under the shared transaction/fence and locks, resolve authoritative invoice/mapping/source/policy/accepted/history/duplicate evidence. Revalidate current principal and scope even for exact retries.
4. Evaluate the existing deterministic foundation with eight-decimal acceptance support (source correction c97a1ad, verification pending). Persist an immutable InvoiceMatchResult with input snapshot, hashes, source versions and line/discrepancy evidence. Blocked results create no billing claim.
5. On a passed comparison with complete exposure coverage, atomically create one immutable debit allocation per supported invoice line, set the invoice's active result/claim pointer, increment billing exposure sequence, audit and outbox. This is Procurement capacity reservation, not invoice approval.
6. Create/reassign exception ownership for blocked lines. A comment or Resolve button cannot turn a failed comparison into a pass. Correct the owning record or mapping and rerun; retain earlier results/exceptions.
7. Source, accepted effects, policy withdrawal, duplicate identity or exposure changes make the result stale or review-required. Currentness is recomputed server-side; stale results cannot progress to any future approval/AP action. Keep old claims conservative until safe atomic replacement/release is proved.
8. Finance approval/posting integration is a separate milestone and remains disabled. No automatic Paid/Posted/Approved state is set.

## 9. Status / State Transitions

| Record / transition | Guard | Effects |
|---|---|---|
| Invoice Received → MatchPending | Mapping saved, current version/scope | Retain proposal; no allocation |
| MatchPending/Exception → ComparisonBlocked | Real comparison blockers | Immutable result, owned exceptions, no new claim |
| MatchPending/Exception → ComparisonPassed | Current policy/source/accepted/exposure, valid comparison | Immutable result + capacity claim; **not Approved/Validated** |
| ComparisonPassed → ReviewRequired | Evidence changed or unavailable | Invalidate currentness; retain conservative claim/history |
| ReviewRequired → ComparisonPassed/Blocked | Safe replacement protocol and fresh evidence | Append new result; atomically replace permitted unbound claim, never erase old evidence |
| Exception Open → Assigned/InProgress | Current owner/reassign authority | Ownership audit, no matching override |
| Exception → Resolved/Superseded | New result proves discrepancy absent, or authorized cancellation | Link resolving result/reason; old discrepancy evidence retained |
| Claim active → superseded/released | No unknown/attempted/bound AP effect and authoritative owner/exposure proof | Append lifecycle evidence and atomic pointer change; no financial credit |

Comparison lifecycle is separate from invoice business status. Use dedicated match state/current-result fields rather than misleading legacy Approved/Posted labels. Claims are never silently released by an event, expired policy, deleted mapping or failed notification.

## 10. Permissions & Data Scope

Reuse `procurement-invoice-read`/capture for existing responsibilities. Proposed explicit actions: `procurement-invoice-map`, `procurement-invoice-match`, `procurement-invoice-match-read`, `procurement-invoice-exception-manage`, `procurement-invoice-claim-release`; exposure attestation/import requires a separately governed Finance owner permission, not AP capture. No bootstrap monetary authority is inferred.

Every action checks current canonical principal, current permission, company, selected branch and record scope. Explicit selected branch filters even an organisation-wide actor. Filter accessible invoice/source IDs and actionable states before counts/pages. Department context fails closed until mapped. Match-detail readers see only their scoped invoices; no unrestricted raw input/history JSON is returned to frontend.

Company-wide duplicate/exposure queries may read hidden-branch authority internally. Public output is a generic conflict/blocker and opaque watermark/digest, not hidden invoice IDs/numbers/branches. Recovery requires current operation permission and original actor ownership plus current invoice/source scope before revealing a saved response. Historical receipt recovery does not authorize re-execution.

## 11. Business Rules

- Original intake values/documents remain immutable. Mapping changes append a new mapping version; corrections to commercial values require a separate controlled invoice revision, not overwriting intake.
- Each invoice line maps to one exact PO line and at most one goods/service acceptance of the expected kind. Multiple acceptance fragments require separate explicit invoice line mappings; no fuzzy description/name join or implicit split.
- One published rule per category/currency; explicit field absolute/percent tolerances, combination and money rounding/scale. No hardcoded business thresholds or silent global tolerance.
- Accepted truth uses P25 existing record ID and exact issued source. Accepted=positive effects minus administrative reversals; Returned=dispatched business returns; available=accepted minus returned. Do not subtract returns twice.
- Pure comparison currently allows configured cumulative quantity tolerance but cannot create receiving entitlement. An adapter policy must explicitly forbid invoice availability beyond authoritative accepted quantity unless a separately approved exception model exists. Initial P26 uses a hard accepted-capacity reservation ceiling; nonzero accepted-overage requests become an owned exception rather than invented receipt/stock. Preserve foundational variance diagnostics.
- Service certified net value is a hard ceiling. Exact eight-decimal receiving net must not be rounded up into more authority or truncated by four-decimal helper conversions.
- A local empty allocation set is not complete Finance exposure. Coverage must enumerate original debits/credits with owner provenance and a coherent current watermark or prove controlled ownership of a closed source scope. Missing/stale/ambiguous coverage blocks claim creation/release.
- Credit allocations reference exact original debit and same supplier/company/currency/issued revision/PO line/acceptance. Each quantity/net/tax component and cumulative totals cannot exceed the original. Quantity restoration requires proportional net/tax credit individually and cumulatively under retained rounding rules; split midpoint credits cannot restore all quantity without the required monetary remainder. Value/tax-only credit restores no quantity.
- Current invoice credit route remains unsupported by the foundation; do not feed a credit header as a negative invoice. Existing SupplierCreditNoteRef.Amount or FinanceCreditRef alone never frees quantity.
- Rematch must exclude the current invoice's *replaceable* active claim exactly once after proving release/replacement safe, while every other active claim/historical financial exposure remains counted. No double debit, orphan credit or self-history bypass.
- Effects and arithmetic are computed before tracked mutation; multi-line failure rolls back claim/result/exception/counter/outbox changes atomically.

## 12. Data Fields & Ownership

| Existing/new record | Required fields / ownership |
|---|---|
| SupplierInvoice | Existing intake retained; add mapping revision/version reference, LastMatchResultId and separate ActiveMatchClaimResultId, match state/revision and last checked source/exposure watermark. Intake identity and Finance state are not inferred from match state |
| SupplierInvoiceLine | Existing PO/receipt/service refs remain; add independently verified invoice UnitCode and verification document/version/line reference, verifier principal/WorkerId/time, source issued-version/line mapping revision. Original amounts unchanged |
| InvoiceMatchResult | Extend existing append-only result with invoice/mapping version, issued/submission/policy IDs and hashes, actor WorkerId, command key/fingerprint, immutable canonical input/evidence JSON+SHA, source/acceptance/exposure watermarks, exact per-line comparison/discrepancies and recorded outcome |
| InvoiceException | Reuse existing owner/status/reason/due date; add exact MatchResultId/InvoiceLineId/discrepancy key, owner WorkerId, resolving result/reassignment version. Context history/audit retains prior assignment/actions |
| InvoiceMatchAllocation (new) | Immutable Procurement claim/import fact: result/invoice/line, issued/PO line, typed acceptance FK, supplier/currency, debit/credit kind, exact quantity/net/tax, original debit FK, owner provenance/reference and exposure sequence |
| InvoiceMatchExposureState (new) | One serialized PO/issued exposure resource: current sequence/CAS, owner coverage mode/reference/watermark/digest/validity, explicit coverage state, current active claim selection. Not a Finance balance or payable |

Add InvoiceMatchMappingVersion for immutable per-invoice mapping snapshots before the first comparison, with exact schema/digest, complete line identities and verifier/document provenance. Claim lifecycle changes retain old/new pointers and owner proof in existing command receipt/audit; immutable allocations remain. The linked plan defines the three justified new tables and their constraints.

Reuse existing ProcurementCommercialCommandReceipt for PO-backed operations (distinct bounded operation names, invoice target in sealed fingerprint/response). It already has source PO, actor, key and response. Non-PO matching is unsupported, so no second idempotency service is needed. Matching/claim state remains Procurement-owned; Finance/AP payment records are not created.

## 13. Screens & Data Views

Extend LiveInvoices detail with source/mapping, comparison, exception and history panels. Retain current document upload/view. Show issued revision, line quantities, invoice unit, accepted/returned/remaining, rounded invoice net/tax and exact certified net ceiling. A clear result reads “Comparison passed — approval and Finance posting are separate.”

List filters: invoice number/vendor, match state, discrepancy, assigned owner, PO, period and branch. Comparison detail displays permitted line evidence and next owner; restricted upstream payload is audit-only. Use existing ERP components and no new menu/shell.

## 14. User Experience / Human Behaviour

Preserve original input on error. Explain “Choose the unit shown on the invoice,” “Select accepted delivery,” “Finance history needs verification” and “Source changed; compare again.” Keep normal loading/empty/denied/stale/uncertain/recovery states. Do not show an enabled Approve/Pay button merely because comparison passed. Use decimal strings and exact arithmetic; current LiveInvoices Number(...) and JS sums must be replaced for new live contracts without rounding previews into authoritative input.

## 15. Notifications & Communications

After commit, use existing outbox/notifications for assigned exceptions, source review needed and completed comparison. Recipient/current scope is checked; a failed notification does not undo the result or allocate again. No supplier/AP/payment message is sent as if Finance accepted the invoice.

## 16. Documents & Attachments

Reuse existing scanned Core invoice document versions and ProcurementAttachmentRef. Unit verification binds exact original version and line/section evidence; document name alone is insufficient. Final match pins document hashes/versions; later upload changes currentness but not the old evidence. Download requires current invoice document scope. Source PO/receipt/service proof is resolved by owning adapters, not reuploaded as a competing master.

## 17. Exceptions & Edge Cases

Missing unit, wrong receipt kind, old amendment version, hidden duplicate, partial delivery, fully returned versus administratively voided acceptance, unknown history, source correction, ambiguous policy, exposure duplicate import, concurrent invoices, alias-account verifier, offboarded historic worker, failed AP attempt without posting ID, duplicate command key, unrepresentable exact value, credit midpoint split and legacy approved labels all need explicit blockers.

Legacy invoices, accepted counters, Finance references or prior UI statuses without full provenance remain visible but excluded from authoritative completed claims unless an approved migration/reconciliation attests them. Do not synthesize zero-history baseline or policy/worker identity during backfill.

## 18. Integrations / APIs / Events / Sandbox

Proposed `/api/procurement/v1/invoices` routes:

| Route | Contract |
|---|---|
| GET `/{id}/match-context` | Current scoped source/mapping choices, exact values, source/acceptance/exposure status and blockers |
| PUT `/{id}/match-mapping` | Expected invoice/mapping version, command key, exact line/issued/acceptance IDs and independent unit evidence; retain prior immutable mapping |
| POST `/{id}/compare` | Stable command key, expected invoice/mapping/source/policy/exposure versions; trusted server resolves all projections |
| GET `/{id}/match-results`, GET `/{id}/match-results/{resultId}` | Scoped immutable results plus separately computed currentness/reasons, no raw hidden history |
| GET `/exceptions`, POST `/{id}/exceptions/{exceptionId}/assign` | Scoped queue and controlled owner assignment with expected version/reason |
| POST `/{id}/exceptions/{exceptionId}/resolve` | Links a new verifying comparison/cancellation; not a tolerance override |
| POST `/{id}/match-claims/release` | Off until approved release/Finance state proof; explicit operation/key/reason |
| GET `/{id}/match-commands/{operation}/{key}` | Actor-owned recorded historical response after current access checks; no replay authorization |

Exact decimal string transport for quantities/unit prices/tolerances and amounts; schema distinguishes four-decimal inputs, rounded invoice money and eight-decimal certified source net. JSON numeric compatibility only if actual API tests prove exact preservation. Source IDs and owner evidence are server-validated; no DTO Complete/current-and-granted flags.

Events `invoice.comparison_recorded.v1`, `invoice.match_claimed.v1`, `invoice.match_review_required.v1` carry immutable result/invoice/source/effect IDs and sequence/correlation. They are not `invoice.approved`, posted or paid events. Inbound Finance coverage has an explicit versioned authenticated owner contract; absent adapter returns Unknown, never Complete=true.

## 19. Audit / Security / Privacy

Acquire existing Core shared tenant session fence before transaction snapshot; current branch writers use paired exclusive fence. Same-source locks serialize receiving/corrections/matching. Enforce current canonical worker/permission/scope at command/replay/recovery; historic pinned workers support evidence, not current access.

Persist append-only results/allocations and current pointer changes atomically with audit/outbox. Hashes verify integrity, not source authenticity. Keep full exposure/duplicate/source snapshots restricted; frontend receives filtered discrepancy DTOs only. Narrow runtime database grants and direct URL/API authorization are mandatory.

## 20. Reports / Dashboards / Metrics

Separate comparison pass/block rate, current valid claims, stale/review-required claims, exception age/owner and AP outcomes. A passed comparison is not first-pass invoice approval or Finance posting. Include source/exposure watermark freshness and currency policy basis; no cross-currency sum without explicit reporting conversion.

## 21. Configuration & Localization

Extend existing PolicyVersion lifecycle with strict INVOICE_MATCHING schema and reference validation. Explicit category/currency method, acceptance kind, tolerances/combination, money scale/rounding and exception ownership rules are approved configuration. No hardcoded “normal” tolerance or amount. Keep PR/PO/RECEIVING policy parsers separate by supported schema dispatcher.

Define published policy transition/withdrawal behavior and retained original rounding for historical debits/credits; do not reinterpret old credit proportionality with today's changed rounding rules. Organisation currency supplies intake default; exact issued/invoice currency agreement remains mandatory. No implicit conversion.

## 22. Non-Functional Requirements

Atomic multi-record persistence, bounded serialization retry with fresh contexts, exact decimal/canonical JSON/DB-microsecond date round-trip, complete bounded queries and resource-scoped pagination. If history exceeds supported volume, return an explicit capacity/coverage blocker or use a reviewed complete aggregate proof; truncating rows cannot preserve Complete=true. Existing source supports up to 200 invoice lines; enforce known request bounds and no unbounded API evidence exports.

## 23. Postconditions

Persist one immutable comparison and, only on supported complete positive execution, one current commercial claim per invoice line. Every exception has an explicit accountable owner or visible assignment-required state. Original source/evidence remains reconstructable. No AP liability, tax accounting, payment, bank instruction or stock transaction is created.

## 24. Failure & Recovery

Lock before reading authority/capacity; revalidate source/currentness after locking. One transaction contains mapping/result/allocations/state/exception/audit/receipt/outbox effects. Arithmetic/guard/concurrency failure rolls all back. Same key/different intent conflicts. Exact original key/payload recovers old result only after current access; changed source does not rewrite old snapshots. Recovery404 is not proof the original in-flight request failed; retain original payload/key.

On source change, compute stale status deterministically from retained hashes/sequences/policy/coverage. An asynchronous marker accelerates UI but is not the sole protection. Keep claims held until safe replacement/release; failed/unknown AP outcomes require owner reconciliation. Do not release capacity with a pretend supplier credit.

## 25. Acceptance Criteria

1. Existing invoice intake/documents remain usable; match is gated independently.
2. Mapping proves original invoice UOM, exact issued PO/acceptance IDs and current scope without mutating original amounts.
3. Two-/three-way comparisons persist immutable input/result/line evidence and correctly owned discrepancies.
4. Only complete verified exposure permits an allocation claim; missing Finance history visibly blocks and creates no fabricated provider evidence.
5. P25 existing acceptance IDs/sequence/hash and accepted-minus-return/reversal semantics bind exactly; eight-decimal net ceilings retain precision.
6. Concurrent invoices cannot overconsume source/accepted capacity; stale rematch cannot double debit or silently release AP-bound claims.
7. Original linked credits preserve quantity/net/tax and cumulative midpoint invariants; current credit execution remains unavailable.
8. Source/mapping/receipt/correction/policy/history changes invalidate currentness before future progression; old evidence stays immutable.
9. Current permission/worker/branch checks protect commands and receipt recovery; hidden branch duplicates/exposure are not disclosed.
10. No result grants approval/AP/payment authority; real API/UI and disposable PG evidence is required before capability activation.

## 26. Test Scenarios

Cover original intake precision/scale and immutable values; independent UOM evidence not copied from source; wrong unit/issued/source/kind/company/branch denied; policy missing/ambiguous/expired/withdrawn/rounding changed; correct two-/three-way partial cases; eight-decimal service ceiling just below/at/above invoice rounding boundary; accepted/returned/admin void distinct; current history excludes replaceable self claim exactly once; unknown/stale coverage blocked; concurrent invoices and compare/rematch/release races; multi-line rollback; duplicate key/result tamper; current revocation/alias independence; scope-filter before queue count; hidden duplicate details suppressed; original debit/credit FKs, orphan/cross-source/over/midpoint split credits denied; failed/unknown AP handoff forbids claim release; source correction invalidation; actual PG precision/composite-FK/append-only/runtime-privilege and apply-twice tests; real API Playwright reload/recovery/error/exception-owner flows. Foundation tests alone do not prove these adapters.

## 27. Ownership / Dependencies

Procurement owns mapping/match/claim/exception state and source adapter. P25 owns accepted history and shared source-state serialization. Core owns canonical identities, current permissions/scope, location fence, documents, audit/workflow infrastructure. Finance owner supplies coverage and later approval/AP/credit/reconciliation. Root schedules SDK/PG/browser gates and migration numbering after P25; no source in this plan alters the frozen root gate.

## 28. Open Decisions / Assumptions

- Approve exact match policy, mapping verifier independence and exception ownership; no business authority is fabricated by this document.
- Finance must define exposure coverage and concurrency protocol. A static manual note cannot claim an external ledger remained unchanged. Without a verified owner protocol, P26 can persist a blocked comparison but cannot create/release live claims or enable invoice validation.
- Eight-decimal accepted-value correction is authored separately in c97a1ad and requires full foundation verification before adoption. The baseline four-decimal MoneyInput path blocks valid P25 amounts; the correction uses exact common eight-place integer units without rounding away source authority.
- Historical policy transitions/credits require retained original rounding and compatible source scope. Unsupported cross-policy/amended-revision history blocks rather than being silently ignored.
- Confirm P25 final SourceState/effect signatures before code and P26 generation; accepted-history completeness differs from structurally complete rows. Full operational validation/approval/AP remain subsequent explicit milestone(s).
