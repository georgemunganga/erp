# M6D pure invoice calculation checkpoint — 29 October 2026

Implemented `InvoiceCurrencyRoundedCalculation` as a pure application component. It reuses `InvoiceMatchExact` integer rounding and `ExactDecimalArithmetic`; it adds no currency master, project, endpoint, migration or change to legacy invoice creation.

The calculator consumes an explicit currency-reference projection, preserves quantity/rate and exact raw products, rounds each line net before summing, validates entered tax against the currency quantum, and returns line/header totals and signed rounding adjustments. Invalid references, modes, scales, duplicate line identities and out-of-range amounts fail. The projection alone does not establish current Core configuration or authorization; the future owning adapter must establish those before use.

Eleven new cases cover 1 × 5.1234 → 5.12 without changing the rate, eight-decimal raw products, both tie modes, per-line versus header rounding, scales 0/3/4, tax precision, invalid inputs and line/aggregate overflow. Independent source review found no defect in this bounded slice.

The full Procurement suite passed **1,603/1,603**, zero failures/skips, with analyzers enabled in the existing SDK image on one CPU. Before testing, all 1,195 source files matched accepted backend `0f656d63`; exactly the new calculator and test files were overlaid. Test execution used no network or database, changed no schema and left the three production container identities unchanged. This run does not republish the host or repeat its earlier 671-test acceptance.

Protected evidence SHA-256: `67a81ac6c240ea426fc5415e67c142d1361a4659730f1ae1257aabba179fcca8`. The adjacent JSON includes source and TRX pins without private runtime configuration.

**Remaining:** Core-owned persisted precision and editor/reader, atomic v2 capture/evidence, original-UI preview integration, qualified private matching/allocation acceptance and release verification. No existing invoice is recalculated. The historical Blocked control, post-history correction gap and production deployment remain separate.
