# BE-01 P24 scoped CSV export backport

This source-only backport implements the bounded Vendor and managed Item CSV contract on the P24 API graph. Its governing 28-section contracts are `PROC-01-VENDOR-CSV-EXPORT.md` and `PROC-04-MANAGED-ITEM-CSV-EXPORT.md` at ERP commit `f32f90866fbb309e636f6611caa84194df33a3a7`. It carries no Procurement model, migration, Domain, Infrastructure, or database change.

The two GET routes reuse their existing list visibility and search/status predicates, use a stable default order across pages, and reject filters beyond `search` and `status`. Each route requires its prior read/manage action and a separately configured export action. The new actions have no bootstrap grants, including the legacy `erp_admin` seed. Explicit All is accepted only for company-master read paths and only within the caller's current Core company and branch assignments; writes and other module routes reject that header.

CSV is synchronous, capped at 10,000 matching rows, fixed to human-readable whitelisted fields, and contains a UTF-8 BOM with protected quoted cells. A durable metadata-only scoped audit precedes any successful attachment response. The routes expose no bank, tax, contact, price, ID-selection, or import capability. Existing HRM CSV quote behavior is unchanged.

**Status:** source authored; SDK compilation, HTTP negative tests, private normal-cookie export, finite database grants and independent audit reconciliation are pending separate acceptance. No production export capability is asserted by this document.
