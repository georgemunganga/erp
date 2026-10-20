# P26 — Invoice matching persistence/API implementation plan

**Status:** Concrete source-only plan, not a generated migration or working API. Baseline `5278267`; matching exact-value correction is the separate source commit `c97a1ad` (verification pending).
**Contract:** [28-section adapter](PROC-11-P26-INVOICE-MATCHING-ADAPTER.md). Tracks [module-connect#30](https://github.com/georgemunganga/module-connect/issues/30); P25 accepted adapter dependency [#31](https://github.com/georgemunganga/module-connect/issues/31).

## 1. Audited source and gaps

| Existing file | Evidence / implementable gap |
|---|---|
| `backend/hrm-api/src/Mightyfin.Erp.Hrm.Api/ProcurementInvoiceIntakeRoutes.cs` | List/Get/Capture only; input lines description/qty/unit price/tax/coding, no verified unit/source mapping. Scope filters only IsConfined, not explicit selected branch. Capture totals use native decimal multiplication/Sum and no four-place precision check, then numeric(18,4) can silently coerce; new matching must not certify a rounded intake as original proof |
| `...Api/ProcurementInvoiceDocumentRoutes.cs` | Existing scanned Core document/version adapter and ProcurementAttachmentRef; reuse. Its scope follows IsConfined only, so align explicit selected branch and current principal checks for sensitive matching document access |
| `backend/procurement-api/src/Mightyfin.Erp.Procurement.Domain/CommercialRecords.cs` | Existing SupplierInvoice/Line/MatchResult/InvoiceException and Finance references; no verified UnitCode, issued pin/mapping history/current match pointers/complete allocation history |
| `...Application/InvoiceMatchingContracts.cs` | `From` intentionally leaves UnitCode blank without verified dictionary. Issued/acceptance/consumption/duplicate/policy projections are caller-supplied contracts, not authenticated evidence |
| `...Application/InvoiceMatchingFoundation.cs` | Deterministic one-revision comparison, strict historical debit/credit validation and evidence hashes. Current credit invoice route unsupported; comparison has CanPostToFinance=false. No permission/transaction/API writes |
| `...Application/InvoiceMatchExact.cs` | Baseline common four-place units conflict with P25 exact eight-place net ceilings. Source correction c97a1ad uses BigInteger eight-place common units; quantity/rate and rounded invoice money contracts remain unchanged |
| `...Infrastructure/Data/ProcurementDbContext.cs` | Existing scoped simple FKs; InvoiceMatchResult modification/deletion guarded append-only. Original invoice/line business fields are not universally guarded against future overwrite. Header/line/issued/acceptance relationships need exact composite constraints |
| `...Api/ProcurementPolicyDraftRoutes.cs` | Existing approved version lifecycle; currently PR/PO schema dispatcher only. Add INVOICE_MATCHING validator with category/currency/acceptance/rounding/tolerance references without weakening other families |
| `...Api/ProcurementHostIntegration.cs` | invoiceValidation=false, financeHandoff=false. Keep separate granular comparison readiness and approval/AP gates |
| `...Application/ProcurementActionCatalog.cs` in HRM host | Current invoice read/capture actions only; add explicit mapping/matching/exception/claim-release actions, with no implicit Finance/spending grant |
| UI `reference-ui/src/features/LiveInvoices.tsx` | Existing Bills list/intake/detail/document panel. Number(input) and JS totals lose exactness; source/matching/exception actions not connected |

`...` retains the preceding project's root. No new backend server or second Core service is required.

## 2. Freeze explicit ownership and source contracts first

P25 owner reports the following source-only contract (names to confirm at its final commit):

- `ReceivingSourceState`: unique company/IssuedVersionId/POLineId; immutable source pin, classification/policy evidence; mutable combined `Sequence`/CAS and derived caches.
- `ReceivingAcceptanceEffect`: exact source state; typed existing GoodsReceiptLineId XOR ServiceAcceptanceId; AcceptanceRecordId remains that existing business record ID; LedgerSequence is the authoritative combined stream.
- `ReceivingLedger.AcceptanceHistoryVerified` is distinct from structural `Complete`. Legacy accepted rows or PO/PR accepted-counter mismatch blocks authoritative Balance/certification; physical held observation may still record truth.
- Core shared tenant session fence precedes snapshot; upstream PR locks → PO → sorted source states → receipt/service. Matching must use the same source-state locks before consumption, not a parallel accepted cache.

Use an `IAcceptedInvoiceSource` owner adapter over that actual history. Return existing acceptance Id, monotonic sequence-derived Version, issued revision/hash, acceptance-effect digest, accepted/returned quantity/net and currentness. Capture latest positive certification time, not receipt draft CreatedAt. Full admin reversal sets Reversed; full business return does not. Both reduce availability differently. Do not filter out a returned/reversed acceptance if prior history references it: pass explicit verified state so comparison blocks safely rather than losing original-debit provenance.

For TwoWay, no acceptance is required but exact issued commercial terms/current source and complete PO-level billing history still are. An unavailable P25 provider must not be replaced with synthetic Accepted rows.

## 3. Model/schema: extend existing records; three justified new tables

Proposed migration name **P26_InvoiceMatchingExecution** after root P25; numbering/schema generated only after its verified model is merged.

### Existing invoice and line extensions

- SupplierInvoice: `MappingRevision`, `CurrentMappingVersionId`, `LastMatchResultId`, **separate** `ActiveMatchClaimResultId`, `MatchState`, `MatchRevision`. Last comparison may be Blocked while an older conservative active claim remains held; a single ambiguous current-result pointer cannot model that.
- Keep original invoice fields/lines immutable after capture. Mapping changes may update only explicit source/current mapping projection columns. Header/line business original-value corrections require a later controlled version contract, not a generic PUT.
- SupplierInvoiceLine: `VerifiedUnitCode`, verifier PrincipalId/WorkerId/time, source document/version/reference, `IssuedVersionId`, mapping version reference. Existing PurchaseOrderLineId/GoodsReceiptLineId/ServiceAcceptanceId remain reused current mappings, with exact source constraints. Do not copy PO UOM by default.
- New intake exact contract validates quantity/rate four-place representability and policy-specific money only after a match policy is chosen. Preserve source eight-place line products if intake captures them; original intake and currency-rounded invoice totals must be explicitly reconciled against supplier document, never silently rounded on write. Reject unsupported excess precision with actionable error; do not silently reinterpret already stored legacy values as proof.

### `invoice_match_mapping_versions` (new)

One immutable version per existing invoice. Fields: scoped ID, InvoiceId, invoice original-business digest/version, integer MappingRevision, actor PrincipalId/WorkerId, verified time, schemaVersion, canonical complete mapping snapshot JSON+SHA and source document versions/hash. Payload schema has exactly one entry per current invoice line with line IDs, issued/PO/typed acceptance IDs, independently verified unit/evidence and no unsupported conversions. Up to200 lines; duplicates/missing/unknown lines reject. Current line FKs enforce projected identity; hydration verifies immutable snapshot and projections agree. JSON is an explicit versioned business snapshot, not an arbitrary custom-data store.

### `invoice_match_allocations` (new)

Append-only validated commercial claim/import facts: InvoiceId/LineId/MatchResultId (or explicit historical evidence owner), issued revision/PO/line, supplier/currency, typed goods/service acceptance IDs, Debit/Credit, OriginalDebitId, quantity/net/tax, exposure sequence, provenance mode/reference/watermark/hash and original rounding/policy reference. One supported normalized consumption row per invoice line; credit line has its own invoice/line and original debit FK. No header-only release.

Current Procurement claims are selected by invoice.ActiveMatchClaimResultId. Superseded/released results/allocations stay immutable; existing command receipt+audit pins exact old/new pointer, reason and safe owner proof. Historical Finance debits/credits remain explicit owner-import facts and are not dropped when a local invoice pointer changes. Reimport uses owner stable external IDs; no duplicate local/Finance representation of the same charge. MatchResult actor/task ownership is not a Finance balance.

### `invoice_match_exposure_states` (new)

One locked company/PO/issued revision resource. Mutable sequence/CAS, explicit coverage status/mode/owner, external coverage version/reference/digest/validity and immutable evidence reference. Allocation rows and versioned comparison snapshots retain the exact coverage checkpoint; updating current state never rewrites history. No Complete flag is accepted from public compare input.

**Coverage contract:** provider must prove all prior posted/pending/claimed debit/credit exposure for the source revision, across branches and channels, reconcile imported facts/local claims without duplication, and provide a current coherent watermark plus an owner concurrency/lease protocol. A static read of an external ledger cannot guarantee it stayed unchanged through commit. If using governed manual evidence, Finance must explicitly hold/own the source scope under a recorded approved procedure during reconciliation/claiming, with verified current evidence and authorized independent identities. A user checkbox, absent provider, empty local invoice table or FinancePostingRef=null is insufficient. Without this real contract, comparison records Blocked and creates no claim. Do not implement a fake Complete=true provider to make tests green.

### Extend existing result/exception, no duplicate masters

InvoiceMatchResult: invoice/mapping revisions, issued/submission/policy IDs+hashes, current actor WorkerId, command key/fingerprint, canonical input/evidence+digests, comparison schema, source/acceptance/exposure watermarks and per-line JSON. Retain legacy aggregate variance columns but make them nullable for unsupported cross-UOM aggregates; authoritative variance is per-line/type, never fabricated zero to fill required columns. No mutable result currentness flag; compute from snapshots plus current pointers, with an optional operational cached marker outside immutable result.

InvoiceException: match result/line/discrepancy identity, owner WorkerId, assignment revision, resolver result/ref. Use existing ownership/audit services. If no eligible owner resolves, state AssignmentRequired with explicit accountable AP queue policy owner; do not assign to an arbitrary username.

Reuse commercial command receipts (PO FK valid for supported route), add bounded distinct operation names InvoiceMap/InvoiceCompare/InvoiceExceptionAssign/InvoiceExceptionResolve/InvoiceClaimRelease and exact target/version/fingerprint. Extend recovery dispatch without changing PO commands. No new AP/payments/duplicate ledger.

### Required relational/data constraints

- All new scoped IDs/FKs bind same tenant/company; line belongs to invoice, mapped PO line belongs to exact PO, issued version matches submission/PO; acceptance belongs to same issued line/source state and typed kind. Reuse P25 composite alternate keys; do not rely solely on separately valid simple FKs.
- Unique invoice+MappingRevision, source exposure resource, company+operation+command key (existing receipt), owner external consumption identity and per-exposure sequence. Duplicate original supplier+normalized number+currency indexes stay company-wide.
- Allocation original debit self-FK includes source supplier/currency/issued/POline/acceptance identities where feasible; domain validates proportional credit and cumulative totals under locks. DB FK existence alone cannot prove cumulative credit limits.
- Existing immutable invoice amount/identity/lines, mapping versions, match results and allocations reject updates/deletes. Current pointers/state can change only through scoped expected-version application operations with retained command/audit evidence.
- Inputs18,4; accepted source net38,8 exact CLR representability; claim money is policy-rounded0–4 and persisted wide enough without rounding. No decimal→double→decimal intermediate. Existing money precision constraints must not silently coerce captured source product.
- Add nullable extensions for legacy rows, no fake mapping/unit/worker/policy/coverage backfill. Legacy incomplete invoices cannot become authoritative claims without controlled verification.
- Down must refuse used schema/claimed evidence and lossy narrowing; derive safe migration guards from the reviewed P24 pattern rather than automatically dropping history.

## 4. Application/infrastructure implementation files

1. **Already authored source correction:** InvoiceMatchExact.cs + accepted checks/subtractions in InvoiceMatchingFoundation.cs + regression tests. Verify first; do not fork a second comparison implementation in the API.
2. `Domain/InvoiceMatchingExecutionEvidence.cs`: three justified record types and explicit claim/mapping/coverage states using existing ProcurementRecord. Extend CommercialRecords only for existing fields/projections.
3. `Application/InvoiceMatchPolicyRules.cs`: strict INVOICE_MATCHING schema, reference-free syntax validation, explicit supported methods/rounding/limits; no default approval. API reference resolver validates real category/currency/policy ownership.
4. `Application/InvoiceMatchMapping.cs`: immutable complete mapping snapshot builder/verifier, original invoice digest versus operational Version, exact source IDs and independent invoice-unit document evidence. No I/O or invented authority.
5. `Application/InvoiceMatchExecutionContracts.cs`: exact-string commands/DTOs, result-currentness basis, claim lifecycle rules and stable error codes; pure helpers precompute expected mutations safely.
6. `Infrastructure/InvoiceMatching/InvoiceMatchSourceResolver.cs`: issued/submission/sourceEvidenceJson and P25 owner adapter, current worker/scope, governed policy, document proof and complete duplicate/exposure provider. Interfaces explicitly return Known/Unknown with owner/version evidence; only real adapters can produce known proof.
7. `Infrastructure/InvoiceMatching/InvoiceMatchExecutionService.cs`: one coordinator transaction/fence; load/lock/revalidate, invoke foundation, persist result/claim/exceptions/pointers/audit/receipt/outbox atomically. Current scope checked before any historical response.
8. `Infrastructure/Data/ProcurementInvoiceMatchingMapping.cs`: partial context mapping/Added/final immutability guards and precision/composite constraints. Preserve current PO/P25 mappings.
9. Host `ProcurementInvoiceMatchingRoutes.cs` plus focused read/mapping/exception partials: implement P26 endpoints; extend existing intake/doc access for precision/selected context compatibility; Core permission catalogue/DI/meta/options/schema readiness. No separate backend service/approval engine.
10. Tests: focused pure/model/mapped API classes plus actual PG probe mode for invoice matching; reuse synthetic real PO→receiving fixtures and restricted host principal patterns.

## 5. Atomicity, lock ordering and freshness

Reuse Core tenant session shared fence **before BEGIN/snapshot**. All matching writers follow existing sorted PR locks → PO → sorted P25 source states → PO/issued exposure state → invoice(s) → mapping/exception rows. TwoWay still locks PO/exposure state even without an acceptance. Discover candidate lock IDs without trusting authority, then re-read/check current mapping/PO/source after locks. A concurrent mapping change must retry/conflict rather than cause locks on an obsolete acceptance. Cross-invoice release/import locks all affected invoice IDs in sorted order.

P25 negative correction/return writers must lock the same source/exposure resources before computing complete consumption. Positive receiving changes sequence and therefore currentness; existing conservative claims remain held. Do not create a second disconnected available-quantity cache. PO commercial amendment path must invalidate or explicitly carry claims before replacing issued authority; unsupported carryover blocks.

Full exposure projection includes active claims from other invoices and validated owner imports, preserving original debit/credit links. For replacement, prove current invoice claim unbound/unattempted and release safe; exclude precisely that old claim from proposed self evaluation, then atomically switch pointer to new passed result. If new comparison blocks, retain old conservative claim unless a separate release command has valid proof. A different actor/current revoked permission cannot retrieve a receipt just because command key matches.

Do not call external Finance while holding database locks and claim external atomicity. Acquire owner coverage/lease with its explicit protocol first; within DB transaction verify it remains valid for pinned source/attempt, then reconcile release after commit with idempotent owner messages if required. Unsupported/expired lease or unknown owner state blocks. No AP posting is performed in this milestone.

## 6. Invalidation and exceptions

Result evidence retains source issued/submission hashes, mapping revision/original digest, P25 source sequence+effect digest, policy ID/version/hash, duplicate watermark and complete exposure checkpoint. Detail computes currentness from authoritative values. Events may set ReviewRequired for convenience but direct command guards always recompute. Do not automatically rerun with today's policy/rounding against old credits; incompatible policy histories block for reconciliation.

Blockers create distinct stable discrepancy keys on existing InvoiceException. Assignment requires same-scope active canonical worker with relevant resolving capability and configured responsibility; reassignment/resolve uses expected exception version and reason. Only a fresh result proving absence or authorized cancellation resolves a discrepancy. No “waive” path, self-approval shortcut or comment toggles comparison outcome. Separate Core exception approvals/limits can be added later via reviewed contracts.

## 7. Exact API/UI plan

Reuse existing LiveInvoices list/intake/detail/document panel. New frontend API adapter consumes P26 exact-string match-context/mapping/compare/history/exception routes. Add source-selection and result panels in place; no shell/menu/table reimplementation. Current intake remains available; use string fields for exact new contracts, retain legacy response compatibility with additive exact companions where needed.

Server supplies permitted source options, current remaining capacity, independent-unit verification state, policy/coverage blockers and conservative action candidates. Clients send IDs, document refs, expected versions and command key; never Complete/current-and-granted/CanPostToFinance. Retain original payload/key after timeout; recovery404 is inconclusive. ComparisonPassed label never enables invoice Approve/Pay. FinanceHandoff and invoiceValidation capability flags remain false until their distinct contracts and actual gates exist; expose comparison readiness granularly, off by default.

## 8. Meaningful verification matrix

| Gate | Required evidence |
|---|---|
| Exact foundation | Existing midpoint/percent/credit/canonical/max-product suite plus c97a1ad eight-place accepted ceilings and hard returned-value failure; no adapter-rounding workaround |
| Mapping/model | Original input unchanged, all lines covered exactly once, unit proof independent, scope/source/typed acceptance FKs, immutable mapping/result/allocation, command key/owner-import uniqueness |
| Actual mapped HTTP | Save mapping→compare→claim, blocked unknown coverage/no allocation, missing/forged unit/policy/acceptance, selected branch and current principal/worker denial, expected-version conflicts, exception assignment and resolving result requirements |
| PostgreSQL exactness | 1e-8 accepted net, rounded invoice money, maximum valid inputs, extra digits rejection, canonical jsonb/numeric/timestamp rehydration, no silently coerced original intake; full8dp remains after read |
| PostgreSQL concurrency | Competing invoices against same acceptance, duplicate command and import IDs, rematch vs receiving correction, mapping change during queued compare, claim release vs unknown/bound AP status, branch writer fence both orderings, multi-line fail leaves no partial effects |
| Historical credits | Original debit same source/kind/currency, complete imports+local dedupe, orphan/cross-source/over-credit denial, cumulative proportional net+tax midpoint and value-only remainder rules; unsupported policy/revision carryover never ignored |
| Replay/recovery | Same key exact result/no extra counters or outbox, changed payload conflicts, current revoked grant/branch/worker denies saved receipt, retained historical evidence not new authority |
| Source currentness | P25 acceptance/correction sequence, issued/policy/mapping/exposure change makes old result review-required, preserved conservative claim, stale AP progression blocked |
| Real UI | Existing Bills page mapping/two-/three-way/partial/blocked/exception flows against actual API; reload, denied/empty/loading/error/uncertain recovery, exact values and no fake Posted/Paid |
| Release | P0–P26 apply twice/no pending model; unused Up→Down→Up, used Down denial, runtime least privilege, independent review/UAT/policy approvals, monitoring/rollback evidence |

All fixtures are synthetic but must exercise real mapped providers/constraints. No fake Complete=true/current-and-granted providers qualify as operational positive proof. If the Finance owner adapter is not ready, verify blocked real behavior and leave positive execution capability disabled; report this remaining dependency explicitly.

## 9. Completion boundary and later milestones

P26 completes a controlled matching/claim/exception slice only after its real source/coverage/transaction/UI gates pass. It does not close overall Procurement or AP readiness. Later: approved invoice/exception authority via Core, idempotent Finance AP payload/acceptance/rejection/reconciliation, linked supplier credit execution, owner-confirmed receipt corrections/returns and payment-status visibility. No payment initiation or Finance ledger duplication.

**Worktree evidence:** source inspection and authored source/docs only; no SDK/build/test/DB/browser job run by this task. Parent owns current combined release gates and adoption of c97a1ad/P26.
