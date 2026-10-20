# Original-value precision guards — existing child-feature addendum

**Status:** Source-only, isolated from frozen P26 compilation at `2c9f301`. No SDK, tests,
Node/API/DB/browser process, model, migration or production change in this increment.

## Existing contract and ownership

This corrects input boundaries for existing Procurement item/catalog maintenance, purchase
requests, planning drafts, contract drafts and Finance budget evidence. Reuse their existing
28-section documents, routes, Core permissions/scope, catalog fences, workflows/audit and record
owners. No new buying/approval authority, table, ledger, setting or currency-rounding policy.

| Original input | Actual retained schema | Guard |
|---|---|---|
| Catalog price/min/max tier, item MOQ | numeric18,4 | Existing CommercialLineDecimalJsonConverter; nullable variant for optional values; typed range/precision validator before tracking. |
| Item purchase-unit conversion | numeric18,6 | Nullable exact codec with six-place limit and existing positive rule. |
| PR requested quantity/estimated rate | numeric18,4 | Existing exact commercial converter; current positive/range/total rules unchanged. |
| PR discount | numeric7,4 | Nullable exact codec; existing0–100 rule unchanged. |
| Plan estimated quantity and **declared** amount | numeric18,4 | Existing exact converter; no multiplication/reinterpretation of the user-declared amount. |
| Contract declared ceiling | numeric18,4 | Existing exact converter; retained scope/version/range rules. |
| Finance CheckedAmount | numeric18,4 | Existing exact converter; retained independent Finance/manual-evidence and full-request guards. |

Numbers and exact decimal strings are supported; null remains null for existing optional fields;
legacy numeric output stays numeric. Raw fifth-place/too-large or beyond-System.Decimal tokens
cannot first round into a different accepted decimal. This does not rewrite already rounded
historical data or guess original vendor terms. The separate computed allocation EstimatedValue
18,4-versus-product8 gap is deliberately not changed here and still needs its reviewed correction.

## Workflow, security and failure behavior

Affected write routes buffer a lossless JsonElement through the normal Minimal API body binder
**before** catalog endpoint fences acquire their database session leases. The shared
ProcurementOriginalValueInput converts that buffered JSON into the existing typed DTO using
its exact converters. This avoids streaming a slow request body while holding a catalog lock.
Original typed API descriptions are retained with Accepts metadata; URL/body shape, role grants,
Core scope and meaningful business guards remain unchanged.

Valid JSON with unrepresentable numbers returns validation-failed422 with a clear message,
before domain mutation or persistence. Catalog typed precision/range checks also return422;
other existing references/version/permission/status responses remain their established paths.
Malformed JSON/media type are still rejected by normal HTTP binding. No automatic POST retry,
record creation, audit/outbox effect or Finance activity follows a rejected original amount.
Existing catalog publish/withdraw fencing and independent approval rules remain intact.

## Authored acceptance and regression source

- Actual DTO deserialization tests cover price/tier/MOQ/conversion, raw29-digit PR/plan/contract/
  Finance amounts, nullable fields, true18,6 boundary, exact strings/numbers and numeric output.
- Mapped API tests cover original tiny quote/quantity values that formerly rounded, schema range,
  all create/update write families and Finance syntax validation using their finite existing
  roles. Full catalog/price/request/plan/contract/budget rows plus audit/outbox footprints must
  remain identical after rejection.
- Positive catalog API reads preserve six-place conversion and maximum18,4 price/tier fields;
  invalid price update must leave original value and version unchanged.
- Existing accepted5dp rejection/4dp arithmetic remains meaningful: no per-line currency rounding
  or widening of user-declared amounts is introduced. Compiled full relevant HTTP/domain and
  actual PostgreSQL round-trip gates are still required before deployment; InMemory cannot
  prove PostgreSQL coercion/locking behavior.

No passing test, release or full Procurement completion claim follows from source authorship.
