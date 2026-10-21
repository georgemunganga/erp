# PROC-08 — Exact arithmetic source correction

Status: source-only, unverified; no migration or capability activation.

## Provenance and supported contract

`ExactDecimalArithmetic.cs` is byte-identical to the shared primitive introduced by receiving
review commit `7b46e05`, copied without an independent implementation. The receiving branch
remains frozen. Both increments can converge on this one Application primitive.

Scaled BigInteger multiplication and summation convert to existing System.Decimal fields
only when the exact result is representable. Nonzero digits are never discarded to fit the
coefficient/scale. `ExactDecimalNotRepresentable` blocks the operation. This does not claim
that every numeric(38,8) database value fits the application type or widen domain fields.

PO commercial totals now require exact individual line bases and cumulative base/gross totals.
Source approved line bases, proposed allocation values and quantity counter calculations use
the same primitive. Reserve/resize compute all fallible results before mutating request,
line or allocation fields. Order confirmation/release counters are similarly computed first;
Finance resolution plans all source counter results before changing any source aggregate.

Unrepresentable application results map to HTTP 422 `OrderAmountNotRepresentable` with
plain instructions to check quantities/prices/tax and contact an administrator if correct.
They do not return a generic stale-version refresh message. A source-authored actual mapped
PostgreSQL Finance command probe creates/approves a real finite-authority PR, then uses the
separate bootstrap fixture to rehearse a legacy/imported draft with a historically rounded
PO amount. Current PO draft input range limits prevent ordinary new requests reaching that
boundary; the probe does not bypass those limits through a production endpoint. It asserts
422 and unchanged source/order/commitment/evidence/receipt state. No probe execution is claimed.

Six source-authored PO regressions cover maximum four-decimal products, independently exact
lines with an unrepresentable aggregate, submit/revision preservation, source-estimate
rejection and reserve/resize no-mutation behavior. Existing restricted runtime, authority,
Finance and whole-command transaction tests still need the root's verification gate.

## Targeted PR reuse plan (not implemented by this increment)

The following existing EstimatedLineBase paths should use the same Multiply/Sum primitive in
one separately reviewed change, preserving existing scope, cancelled-line selection, basis,
policy pins, error contracts and historical evidence:

| Source | Existing calculation | Required narrow change |
|---|---|---|
| Application/PurchaseRequestSpendingAuthority.cs:75 | checked products + Sum; catches OverflowException only | Exact products/Sum; map representation failure to an explicit amount blocker before evaluating any grant |
| Application/PurchaseRequestApprovalPinning.cs:188 | Amount helper sums native products; callers catch overflow | Exact products/Sum and matching error handling for Pin/Verify; existing immutable evidence is never silently recomputed/backfilled |
| Application/RequestApprovalReadiness.cs:23 | Native products/Sum | Same exact calculation with read-only blocker, matching actual execution rather than a divergent preview |
| Api/ProcurementRequestRoutes.cs:248,468 | Submit estimate and draft validation accumulation | Exact products/Sum before state mutation, preserve existing configured amount/range limits and plain validation errors |
| Api/ProcurementPolicyEvaluation.cs:64 | Per-line policy amount | Exact Multiply before routing; no rounded amount crossing a threshold |
| Api/ProcurementBudgetRoutes.cs:117 | Finance check expected total | Exact products/Sum; never compare authoritative Finance against rounded demand |
| API queue projections: ProcurementBudgetRoutes.cs:40, ProcurementPolicyEvaluationRoutes.cs:45, ProcurementApprovalExecutionReads.cs:38 | EF/SQL aggregate products | Do not blindly invoke a non-translatable helper in EF. Resolve a bounded authorized page, compute its exact application amounts, and retain scope-safe counts/pagination; reconcile SQL numeric precision separately |

The PR input validator currently also limits estimated totals to the existing 18,4 amount
range. That reduces ordinary valid-CRUD exposure but does not make unchecked/legacy/imported
calculations intrinsically exact. New validation must preserve that range rather than use
the shared primitive to silently expand approved product limits. Add boundary, cancelled-line,
finite-authority, policy-pin, Finance-consistency and historical compatibility regressions.

The legacy PO draft numeric DTO at ProcurementOrderDraftRoutes.cs:228 also computes a native
product/Sum. Its owner is currently adding the exact execution read contract; coordinate the
read-path substitution rather than conflict-editing that API file in this source increment.
