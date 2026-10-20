# P26 independent source review

**Reviewed source:** `ddd823665167919b713c56c75cfcaf345902279e`  
**Review date:** 20 October 2026  
**Method:** Read-only source inspection; no SDK, database, browser or production operations.  
**Status:** Integration prerequisites identified; this is not evidence of a deployed vulnerability or completed operational matching.

The reviewed increment has no activated matching API or production owner/reviewer implementation. Both unavailable implementations correctly refuse complete/verified authority. The following guards must be enforced in reusable evidence helpers before API integration; promises that a future caller will validate them do not close these findings.

Paths below are relative to `backend/procurement-api/src/Mightyfin.Erp.Procurement.Application/` at the reviewed commit.

## 1. Verified mapping review does not bind the evidence it certifies

**Location:** `InvoiceMappingEvidence.cs:64–68`; review contract in `InvoiceMatchingExecutionContracts.cs:11`.

`Project` accepts `Verified=true`, state `Verified`, evidence other than the exact string `{}`, and the expected line IDs/unit strings. Checking a freshly calculated SHA256 string's length equals64 does not verify any evidence content or provenance. The review carries no mapping digest/revision, original invoice digest, document version/hash, current actor/worker, site or governing policy binding.

Consequently, an earlier review with the same invoice line IDs and units can be reused after changing the proposed PO/acceptance/document mapping, or after changing original invoice data and generating a new mapping. `Verify` authenticates the new mapping against its new original digest, but does not authenticate the old review against that mapping. A nonempty arbitrary evidence string also satisfies the current evidence-content check.

**Required correction:** A typed review checkpoint must pin the exact mapping/original/document evidence and governed identity/scope/policy context. The helper must validate those pins before returning verified units. A proposal or client-provided boolean cannot establish verification.

**Regression cases:** Reuse a valid review after changing mapping digest, original amount, document version, reviewer identity or policy; every mismatch must block. Unavailable/malformed review cannot produce a verified unit.

## 2. Qualified claims can combine a prior comparison with different supplied input

**Location:** `InvoiceMatchExecutionEvidence.cs:31–39`, metadata projection at `:43–59`.

The helper checks result fingerprint/evidence equal the supplied comparison, but does not establish that the comparison belongs to the current `input`. It checks invoice ID/version, order/revision IDs and policy ID; changed consumption, policy contents, invoice line metadata/unit or other same-ID inputs are not canonically compared. Amounts come from the old comparison while some allocation identities/units come from the supplied input.

The result itself is also not required to match current tenant/company, `InvoiceVersion`, `InvoiceOriginalSha256`, or the exact current mapping version. A merely non-null `MappingVersionId` is insufficient. Old evidence or an unrelated result envelope must not be turned into fresh claims.

**Required correction:** Recompute and compare the canonical evaluator input/evidence (or use an equivalent checked evaluator-owned result bound to that input), and validate the complete immutable result/mapping/original/version/scope pins within the helper. Preserve these checks for every claim, not just one route.

**Regression cases:** Keep a valid comparison/result and change historical consumption, policy contents, line unit/source or mapping pointer; no claims may be produced. Independently alter each result version/digest/scope field and assert denial.

## 3. Historical owner checkpoint does not bind its allocation payload

**Location:** `InvoiceMatchExecutionEvidence.cs:8–22`; owner/exposure contracts in `InvoiceMatchingExecutionContracts.cs:15–28`.

`OwnerCovers` checks complete/state, freshness, policy field, JSON digest and selected JSON identities/watermark. It does not bind the returned `owner.Allocations` to that evidence digest, nor validate that the comparison's `input.Consumption` is the exact authenticated/reconciled checkpoint. The JSON also omits explicit branch/coverage-scope and policy digest checks; these distinctions must be defined rather than inferred. A verified empty checkpoint envelope can currently accompany a different allocation list without invalidating `OwnerCovers`.

**Required correction:** Canonically bind coverage scope, policy, allocation/import identities and values, watermark and validity into authenticated owner evidence; verify the exact merged consumption checkpoint used for comparison. Include every relevant historical debit/credit and retained claim with explicit reconciliation/deduplication. Neither inactive local pointers nor an empty local table prove external history is complete. The owning adapter must also hold the documented concurrency/lease protocol through claim persistence.

**Regression cases:** Add/remove/change an allocation or credit under an unchanged checkpoint; alter watermark/policy/scope; substitute comparison consumption or omit a retained/historical debit. Each must block, including verified-zero history without actual owner coverage.

## 4. Malformed owner evidence escapes the unavailable result

**Location:** `InvoiceMatchExecutionEvidence.cs:14–22`.

Malformed JSON, absent keys or wrong field types throw from `JsonDocument.Parse`/`GetProperty`/typed reads instead of returning `false`. This remains financially fail-closed, but can convert an unavailable owner checkpoint into an unexpected server error rather than a durable blocked comparison with an actionable recovery reason.

**Required correction:** Validate bounded, typed owner evidence without uncaught parse/schema failures and return explicit unavailable/invalid evidence. Do not catch errors by inventing empty/complete history.

**Regression cases:** Invalid JSON, missing schema fields, wrong types and unsupported schema version produce a controlled blocked outcome with no allocation effects.

## Confirmed protections and review limits

- Default history owner returns `Complete=false`; default unit reviewer returns `Verified=false`. Missing integrations are not silently certified as zero history or verified units.
- Mapping retains original invoice values and hashes business data separately from operational version changes.
- The eight-decimal `BigInteger` matching arithmetic preserves exact accepted-value ceilings without a floating-point intermediate. The cumulative split-credit midpoint correction is present.
- The qualified-claim helper adds a hard accepted-quantity ceiling even where diagnostic policy tolerances permit variance.
- This increment has only negative/unavailable mapping-owner tests; the positive/mismatched claim and owner evidence regressions above remain necessary.
- EF constraints, transaction/lease ownership, historical reconciliation, API authorization, immutable persistence and real PostgreSQL/browser evidence require separate inspection and tests after source integration. No release or positive Finance/AP authority is established here.

## Source correction review — 4ee1286 and 1eb52f6

Reviewed both committed corrections by source inspection only. No SDK/test/database/browser job was run for this review.

- `4ee1286` adds typed review scope/identity/mapping/original/document envelopes and exact owner allocation/scope/policy/expiry hashes. Malformed or duplicate owner evidence returns false. Claim creation reconstructs complete owner/local history, checks result/current mapping/original/version/scope pins, recomputes the foundation evaluation and emits recomputed lines. Detached prior line objects cannot increase a new claim.
- The first correction still accepted a caller-supplied policy digest unrelated to the actual input policy. This residual was reported independently rather than treating equality between two claimed hashes as proof of the current policy.
- `1eb52f6` closes that residual by accepting the existing owned `PolicyVersion`, deriving a strict independently published matching projection, checking its actual `RulesJson` digest against review/owner/result bindings, and comparing its complete projection to the evaluated input. Document review additionally checks each version/hash against the expected immutable Core version hash projection.

**Source review disposition:** The four findings above and the residual policy/document binding issue are addressed at the pure-helper source level by `1eb52f6`; no further concrete blocker was found in that bounded review. Authored synthetic positive/negative tests still require execution. This does not certify the whole P26 persistence/API slice or enable live matching.

**Remaining release evidence:** Canonical identity and permission/data-scope checks; independent policy maker/checker ownership; actual Core document version authorization, scan status and hash lookup; a configured reconciled Finance history owner with a durable lease/concurrency protocol; all historical/local claim loading and immutable database constraints; actual transaction/retry/recovery tests; and real HTTP/PostgreSQL/browser verification. The shipped owner/reviewer defaults remain unavailable. Uncommitted EF/API work was outside this correction review.
