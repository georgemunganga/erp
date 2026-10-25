# P24 branch-scope compatibility finding

Status: inherited behavior recorded; broader operational All-branch support remains open.

## M0 request/plan/contract list slice deployed; broader C01 remains open

The [M0 private proof](../procurement/release/P24-M0-COMPANY-REQUEST-SCOPE-PRIVATE-VERIFICATION-20261025.md) records source `8fe23143` / UI `bc871e75`, 105 focused/1,070 Host tests, 85 UI units, 16 mocked browser cases and actual normal-cookie PostgreSQL HTTP plus two browser cases with independent owner reconciliation. All own-request reads use current permitted A+B before count/page; same-cookie B revocation removes B, selected-A CRUD remains valid, and explicit-All request writes deny without effects. Plans/contracts remain company/owner scoped. The [bounded M0 release](../procurement/release/P24-M0-COMPANY-REQUEST-SCOPE-RELEASE-20261025.md) is deployed on image `ceb4cbeb80fc`, with the API/publisher running, zero restarts, healthy API and exact public five-file UI verification. The e833 baseline described below is its retained historical predecessor; real production-user UAT remains unperformed. Orders/receipts/invoices/dashboard aggregation and whole C01 acceptance stay open.

## First-use organisation choice: bounded compatibility deployed

[PR93](https://github.com/georgemunganga/erp/pull/93) at `b3e190baedef2260bd270d5155b52b0da7ea3c81` backports the existing Core workspace context, named shared-session authorization and validate-only Core startup. The [P24 private acceptance](../procurement/release/P24-SCOPED-CSV-PRIVATE-VERIFICATION-20261025.md) records one actual browser with empty storage, current Core company/branch choices, server headquarters selection, Vendor access and logout, with identical owner snapshots and no export audit. This closes the bounded first-use/session compatibility issue in source and private verification. The [bounded P24 release](../procurement/release/P24-SCOPED-CSV-RELEASE-20261025.md) is now deployed on image `e8330656f4e9`, with healthy API, running publisher and anonymous Core/export 401 checks. Real production-account UAT remains unperformed; this does not close C01 operational All-branch aggregation.

### Preserved deployed-baseline finding before the backport

The Procurement live shell requests `GET /api/core/v1/workspace-context` for authorised company and branch choices. The pre-backport P24 host does not map that Core endpoint, so the shell falls back to HRM administrator company/location reads and the HRM shell. A workerless account with only Procurement grants cannot use those HRM administrator reads to populate a first-use chooser. With no previously saved canonical company and branch IDs, the live Procurement gate displays **Choose your branch** and does not load Vendor or Item records.

For an account with previously saved canonical company and branch IDs, the shell can proceed through the P24 fallback. The separate `GET /api/procurement/v1/context` and `/meta` requests must still verify the current Core principal, `procurement-access`, company, permitted branch, schema and Vendor/Item capability; each list and export retains its own server permission and scope checks. Browser storage alone grants nothing. This saved-selection path is the bounded CSV export acceptance case, not acceptance of first-use organisation selection. The browser proof should distinguish the two cases and record actual responses rather than infer support from source.

The Core-owned compatibility step, now implemented and privately verified in the source above, is to provide current, authorised company/branch choices and organisation defaults to Procurement-only accounts through the existing Core membership and scope rules. It must remain default-deny for unavailable or revoked choices. It must not give Procurement users HRM administrator roles, infer an employee home location, or invent a branch. This first-use chooser gap is separate from the C01 operational All-branch limitation below and from dashboard gap CORE-01.

## Preserved pre-M0 operational baseline

Before M0, the P24 Procurement middleware resolved a concrete assigned/default branch when no branch identifier was supplied. It previously ignored `X-Shell-Branch-Scope: all`. The preceding bounded CSV backport supported true All for the reviewed company-master reads, including Vendor/Item exports, while preserving the previous concrete-branch behavior for other operational routes. This avoids introducing new failures into existing CRUD or expanding a caller's data authority. That earlier CSV release did not establish company-wide request, order, receipt, invoice or dashboard aggregation. M0 subsequently supplies the bounded own-request/plan/contract behavior above; other operational aggregation remains open.

This is organisation/data-scope capability **C01** in GW-ENG-STD-001, rather than a new procurement master or approval engine. Core remains owner of current company/branch assignments. Track dashboard acceptance under CORE-01 and organization-context acceptance under CORE-02 in the existing Procurement gap plan; its 55-finding baseline is unchanged.

For the remaining operational resources, the next scope feature specification must define which records belong to a branch or to the company, then filter All reads by the current permitted branch intersection. A company-owned Vendor or Item can legitimately be shared across authorized branches; a branch-owned order must not expose an unassigned branch's records. Creating a branch-owned transaction must still resolve an authorized branch. Employee home locations do not replace Core account assignments.

The UI must display the scope actually acknowledged by the server. A concrete-branch result must not be presented as a complete company-wide operational result. Use the existing branch selector and scope feedback components when a branch is required. This requires its own UI/API contract and verification before broader All support is claimed.

Acceptance must include authorized branches A+B, denied branch C, another company and another tenant; the same checks must cover lists, detail URLs, exports, aggregates and drilldowns. Role changes and branch reassignment must take effect for existing sessions. Existing selected-branch CRUD, company-owned master CRUD, audit history and the HRM module must retain their behavior.

The CSV backport is tracked separately in [ERP PR93](https://github.com/georgemunganga/erp/pull/93). Its private acceptance and deployment gates must be completed independently of the newer-model export proof.
