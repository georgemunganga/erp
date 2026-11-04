# M2 — explicit Core purchase-request approval routing

Product source: `f62a35a902a54fcefbbc67fcd2f3c3cdefe2194c`. Procurement UI: `12633f91733f5eae355d1377d6ba9194461910a8` in `georgemunganga/module-connect`. This is implementation and local verification evidence, not whole-module acceptance or a production activation receipt.

## Behaviour and boundaries

Core remains owner of users, roles, organisation and access. Version 2 PR rules narrow the existing permission/finite-authority approver pool to named Core users or current Core roles. Requester-role and department/branch conditions support separate employee and supervisor routes. No employee link is required. A binding never grants permission, branch access or spending authority. Self-approval, ambiguous One routes and stale membership remain blocked. Review/publication require independent actors and organisation-wide configuration scope. Existing version 1 rules remain compatible; no migration or new workflow engine is introduced.

The imported Buying rules form is retained, using ERP controls. New PR drafts support ordered person/role stages, One/All, reordering, requester roles, departments and existing ERP branches. Incomplete bindings fail before save. Unsupported legacy budget-owner routing cannot be newly selected. Exact decimal thresholds survive read/write without JavaScript rounding. Saved unavailable choices remain visible for correction.

## Executed checks

- Backend SDK 10.0.400: all **2,033 Procurement tests** and **49 focused Host tests** passed in final run `evidence-r7`. Includes mapped configuration access and independent policy publication by Core users without employees, branch-limited publication denial, role revocation and trusted submission snapshot tests. PostgreSQL timestamp precision is normalized before storing the role snapshot.
- UI: **984 unit tests in 90 files**, final release TypeScript and live-mode Vite production build passed. Four new Chromium cases passed, including stage reordering, named user/role, branch/department and incomplete-route guards. Seventeen existing workflow/write-guard cases also passed. These are **21 unique cases across runs**, not one 21-green run. Browser API responses were mocked.
- Earlier failed runs remain retained: serialization of a computed property (fixed), over-restricted test actor (fixture corrected and denial test added), duplicate Add draft selector (test corrected), a concurrent typecheck timeout, and an invalid Vite `--root` command (corrected build passed). They are not relabelled as passing evidence.
- Build qualification is incremental: changed projects compiled afresh; unchanged source-qualified dependency artifacts reused. This is not a clean dependency rebuild or native PostgreSQL workflow proof.

Private evidence: `/home/amizo/erp-backups/procurement-core-routing-m2-20261104_run01`. Contains all backend runs/TRXs, dependency provenance, exact source hashes and UI logs. No cookies, credentials or customer records are published here.

## Release and remaining work

Production initially remains on `c93e8df4f82214b58273bf0c9be58d013aca3331`, 239-table accepted schema. A separately tested c93 backport is being prepared so M2 does not deploy held advanced-sourcing/B1c schema work. Product and evidence source merges do not themselves prove deployment. Do not enable approvals or payments by changing flags alone.

Still required for owner launch: actual company policy/finite authority configuration; native and signed-in company acceptance of supervisor/executive routing; department-governance assignment distinct from HR worker pointers; payment timing/general PO-advance gap resolution; and full critical purchase-to-payment acceptance. Existing invoice-backed external payment recording and independent Accounts confirmation remain separate from bank execution and the future Finance ledger. Issues 192, 190 and 145 remain open. No actual approvers, limits, financial attestation or user grants were invented.
