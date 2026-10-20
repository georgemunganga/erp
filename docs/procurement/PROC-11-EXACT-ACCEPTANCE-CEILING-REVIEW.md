# Invoice matcher exact accepted-value correction

**Status:** Source authored, not compiled/tested/activated by this increment. No schema/provider/API changes.
**Base:** `5278267`; scope: `InvoiceMatchExact`, accepted-net checks/subtraction in `InvoiceMatchingFoundation`, focused regression cases in its existing test class.

## Defect and contract

P25 produces exact eight-decimal pre-tax acceptance values. The matcher previously required accepted/returned values to satisfy invoice money scale (0–4), and its common integer representation used four decimal places. This rejected valid receiving evidence such as `0.00010001`; rounding that evidence in an adapter could instead invent service billing capacity.

Use common integer units of `10^8`. Quantity/unit-price input remains numeric(18,4); invoice/debit/credit net/tax remains explicitly policy-rounded at scale0–4. Only source accepted/returned net is allowed exact nonnegative eight-decimal CLR-representable values. This is not permission to accept a fifth-decimal invoice input or change tolerance policy.

- Decimal bits supply signed coefficient/scale directly; scaling uses BigInteger, not decimal multiplication. Extra trailing-zero representations normalize exactly; nonzero excess fractional digits fail closed.
- ProductMoney rounds a `10^16` product to the explicit policy scale, then returns `10^8` units.
- RatioMoney and MoneyInput use `10^(8-moneyScale)`; percentage tolerance uses denominator `100*10^8`. Canonical output trims eight fractional places, preserving existing four-decimal strings.
- Accepted and returned net are validated independently for exact eight-place representation. Service remaining net is `Units(accepted)-Units(returned)`; it is never a decimal subtraction/rounding intermediate.
- Existing service value ceiling remains hard regardless of quantity/price/total commercial tolerance. Comparison still grants no approval/AP/payment authority.

## Authored regressions and existing gates

New cases cover valid `0.00010001` accepted net versus policy-rounded `0.0001` invoice; a `0.00000002` return leaving `0.00009999` (the same invoice must fail service ceiling even with permissive commercial tolerance); unsupported ninth fractional digit; and a large exact source net whose multiplication by `10^8` in decimal would overflow. Existing midpoint/percent/cumulative linked-credit/max-product/canonical replay tests remain mandatory unchanged gates.

No SDK, test runner, PostgreSQL or browser was run in this source-only worktree. Root schedules full Procurement tests and subsequent P25/P26 real persistence/HTTP verification before adopting the correction. Numeric(38,8) database capacity still does not imply every38-digit value fits System.Decimal.
