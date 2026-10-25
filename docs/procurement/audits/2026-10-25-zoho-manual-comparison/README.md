# Procurement and ERP Core audit against Zoho manuals

**Audit date:** 25 October 2026. **Status:** evidence-based gap assessment and proposed closure backlog; no new business policy approved. **Standard:** GW-ENG-STD-001, Tier 3.

Our ERP has real Procurement implementation, shared Core capabilities and several independently verified workflow slices. It does **not** yet provide the complete operating experience described across Zoho's Procurement manuals. The largest gaps sit between records: supplier participation, award-to-order conversion, invoice source/matching evidence, settlement allocations, recurrence, shared settings and coherent deployment. Existing screens should be connected and completed, preserving the supplied layout and shared ERP controls.

## What was reviewed

Starting at [Zoho Procurement resources](https://www.zoho.com/procurement/resources/), the audit followed the public [help navigation](https://www.zoho.com/procurement/help/getting-started/welcome/) and every discovered Procurement help link, then the linked [FAQ collection](https://www.zoho.com/procurement/kb/general/).

| Coverage | Result |
| --- | --- |
| Distinct canonical manual guides retrieved and reviewed | 164 |
| Discovered help-path URLs | 171: 165 successful responses, including one duplicate canonical alias; six unavailable or malformed links |
| Linked FAQ material | 41 pages: 33 answers and eight category indexes |
| Manual operation checklist | 349 entries; entries may overlap and are not distinct implemented-feature counts |
| Grouped findings | 55, including partial workflows, missing implementation, verification/release gaps, documentation drift and optional platform features |
| Field and transition crosswalk | 64 rows |
| Named settings controls | 42 rows |
| Additional payables field/control groups | Eight |
| Checked 28-section feature/slice documents | 25 have all required headings in order; this confirms structure, not accepted behaviour |

The comparison uses ERP source `c38202e57afff83abbcdcbb05f95a27d38a8be26`, Procurement UI source `c370275e9242f447cade47bec31c2fb42aa75637`, the separate live sign-in release, and named existing acceptance records. Unmerged held-quantity source and a passing test log without accepted reconciliation are not silently promoted into accepted capabilities.

This was a read-only source/manual/evidence audit with fresh anonymous public route probes. It did not perform new authenticated production CRUD, exhaustive field-by-field UAT, a penetration test or new database migrations. Historical private tests prove their recorded source and workflow scope, not today's entire live system. Forums, videos, marketing claims, other-product account help and private/unlinked screens are outside the traversed manual graph.

## Detailed deliverables

- [Manual-by-manual comparison](MANUAL-COMPARISON.md): all 171 help-path URLs and 41 FAQ pages, with expected actions, current scope and finding references.
- [Detailed gap register](GAP-REGISTER.md): 55 findings with current behaviour, missing work, source references, ownership and closure criteria.
- [Proposed closure roadmap](CLOSURE-ROADMAP.md): the order of work without rebuilding the UI or duplicating Core/Finance.
- [Field and transition crosswalk](field-transition-crosswalk.json), [settings controls](settings-control-crosswalk.json), and [payables controls](payables-field-crosswalk.json).
- [Manual comparison JSON](manual-comparison.json), [gap JSON](gap-register.json), [gap CSV](gap-register.csv), and [source inventory](source-inventory.json).
- [Public route observations](live-public-routes.json), [role action comparison](role-action-catalog-difference.json), and [specification structure check](spec-structure-check.json).

The JSON records retain source-line links, our feature-spec references and the original verification limits. Full external manuals and screenshots are kept out of the repository; the inventory records retrieval status and source hashes.

## Current implementation compared with the manuals

| Area | What our system currently establishes | Remaining difference |
| --- | --- | --- |
| Shared identity and access | ERP-wide sign-in is live. Core user identities, custom roles and Procurement actions exist; employee linkage is optional for principal workflows. | The role grid exposes only a subset of actions; current hierarchical settings routes are not deployed. MFA and the full account/organization lifecycle need explicit scope and acceptance. CORE-02/04/13. |
| Organization and branding | Connected organization readers, branch/department controls and branding editors exist in source; sign-in consumes public branding. | Legal-entity administration is not complete CRUD in the real UI; global defaults have placeholders. Branding/font changes need cross-module save/readback verification. CORE-03/11/13. |
| Suppliers | Internal supplier fields, contacts, sites, qualifications and governed state actions have typed routes and UI. | External onboarding/invite/correction, merge, broad data transfer and templates are not complete equivalents. A portal-request checkbox does not create supplier access. BE-01/16/17/18. |
| Items and catalog | Item drafts, exact prices and independently controlled catalog publication are represented in the real backend/adapters. | Import/clone/bulk operations, customizable columns/preferences and external punchout need their own contracts. Typed storage is not a generic customization engine. BE-01/02, CORE-07. |
| Purchase requests | Drafts, submission, policy/budget evidence, Core-principal review and bounded source conversion exist. | Recall/hold/reapproval variants, attachments and cross-request bulk conversion need complete operation-level evidence and UI connections. BE-03/04/09. |
| Budgets | Procurement records scoped external funding-check/reservation evidence. | Fiscal/category/period budget creation, consumption, warning slabs and actuals reporting are broader Finance-owned capabilities, not an existing Procurement budget ledger. PAY-10/11. |
| Sourcing and bids | Draft/publish/review groundwork and commercial-round preparation exist; human recommendation and offer-history source is bounded. | Genuine original-document offer submission remains scanner-dependent; supplier self-service, full bid-control parity, split award approval/publication and award-to-order are incomplete. BE-11/12/13/14/15. |
| Purchase orders | Direct-order draft/governance/revision paths and retained source controls exist. | Full sourcing-award adoption, all bulk approval presets, post-issue partial cancellation/reopening and complete bill conversion remain narrower than the manuals. BE-06/07/08/09. |
| Receiving and returns | Partial goods/service receiving and inspection have bounded private evidence; return-draft create/revise/cancel has real cookie/browser and owner evidence. | A return draft is not completed supplier dispatch, replacement, credit, stock movement or asset handoff. Transit/billing/import and full correction closure remain separate. BE-10 and the receiving crosswalk. |
| Bills and matching | Scoped invoice capture/correction, original-unit review and source/comparison groundwork exist. | Full PO/receipt-to-bill conversion, genuine-original positive matching/exception closure, intake/OCR and AP feedback are not a complete live flow. Procurement draft work must not wait merely because Finance is future. PAY-01/02/03/12, BE-08. |
| Manual payments | Payments are listed and have a controlled one-invoice manual request/review/record/confirm workflow with private proof. | Vendor advances, multi-invoice allocation, excess/unapplied amounts, compensating refunds and full batch management are missing workflows, not just missing bank connectors. PAY-04/05/08. |
| Supplier credits | Captured invoice-linked credit claims protect shared invoice/payment capacity. | Standalone/itemized credits, independent credit review, application/unapplication across bills, remaining balance and refund history are broader than captured credit reasons. PAY-06/07. |
| Contracts | Draft/rate editing and internal review have actual private requester/reviewer browser and owner acceptance. | Internal approval does not activate a signed contract. Amendments, ceilings, framework drawdown and renewal execution remain to implement. BE-19. |
| Recurring purchases | Frequency, start/end/next-cycle dates and expected amounts already persist as planning. | Scheduled child bill/order generation, idempotent catch-up, pause/resume and child/payment navigation do not follow from saved dates. PAY-09. |
| Reporting and collaboration | Scoped operational readers, loaded-record insights, transaction links, audit and outbox infrastructure exist. | Full-query monetary/aging/variance datasets, reconciled drilldown, report delivery, record comments/sharing and reusable document templates need completion. PAY-11/14/15/16, CORE-08/09. |
| Supplier portal | Internal UI can capture an access request; preview supplier screens are not external access. | Supplier authentication, invitations, own-record scope, bid/PO/invoice actions and payment/statement visibility need a real external workflow. CORE-12, BE-14/16. |
| Generic extensibility | Typed vendor attributes and specific policy editors already serve bounded needs. | General low-code modules, formula/layout designers, arbitrary executable functions and developer self-service are separate platform candidates. CORE-06/07/10. |

These rows deliberately separate source, bounded private verification and live availability. Consult the linked manual records for the expected individual actions and the exact scope of each claim.

## Findings that should be addressed first

### 1. Shared settings deployment does not match current source

Fresh public GET probes returned **404** for `/core/workspace/settings`, `/core/workspace/settings/branding` and `/hrm/workspace/settings`. Legacy `/core` and `/core/branding` returned 200. Source already contains the newer routes. The successful ERP sign-in release did not deploy the entire newer Core/HRM frontend. This is a release-alignment gap, not proof that the branding backend is absent. See CORE-13 and [route observations](live-public-routes.json).

### 2. Permission management is implemented but its UI catalog is incomplete

The backend declares **91 Procurement actions**, while the visible Procurement permission grid lists **21**. **70 actions have no selection control in that grid**, including newer approval, PO, receiving, payment, document-review and sourcing actions. No UI action is unknown to the backend. Reuse a single owned action-definition catalog; opening an editor must not grant anything, and saving unrelated role fields must preserve explicit retained actions. See CORE-04 and [the exact action difference](role-action-catalog-difference.json).

A second static concern is that role DTO fallback and effective authorization use different tests for legacy rows. Empty/unknown/HR-only permissions can potentially display bootstrap defaults while the authorizer grants none. This is a regression-test requirement, **not a confirmed affected production row or authorization bypass**. AUD-04 specifies the cases.

### 3. Competitive sourcing still cannot complete the full commercial chain

The [award manual](https://www.zoho.com/procurement/help/request-for-quotes/award-request-for-quotes/) includes comparison, split selection, review and publication. Our P38 foundation has genuine schema/rollback evidence, but mapped award insertion remains deliberately denied until authoritative retained source and capacity are available. No accepted end-to-end award-to-PO workflow follows from those tables. Preserve the existing UI and complete the trusted adapters; do not bypass the guard. BE-09/12/13.

### 4. Payables needs richer manual workflows as well as future Finance integration

The [payment guide](https://www.zoho.com/procurement/help/payments-made/record-payments-made/) describes vendor-level allocation and retained excess. Our bounded manual workflow is useful and real, but one invoice per request cannot represent advances, a payment split over several bills or a leftover amount applied later. Credit application/refund and server-owned batches need corresponding controlled records. Finance absence is not a reason to omit these Procurement-facing manual operations. Bank execution, AP posting and bank reconciliation remain Finance-owned. PAY-04 through PAY-08.

### 5. Several shared settings are directory entries rather than working editors

Core's generic settings fallback explicitly renders an unavailable editor. A navigation label for numbering, PDF templates, reporting tags or notifications does not establish persisted configuration. The [42-control crosswalk](settings-control-crosswalk.json) identifies connected readers/writers, partial operations, module-owned references, preview-only controls and placeholders. CORE-07/08/09/10/11.

### 6. Some documentation is structurally complete but historically stale

The master map still says the repository has no Procurement backend/frontend, and the initial System Definition still says production has no Procurement tables. Later source and release records contradict those as statements about today. Their original dates/history should remain, with an explicit current status linked above them. The 28-section headings are present in 25 checked files; open business decisions, missing operations and absent deployment evidence still need closure. AUD-01/02/05.

## Differences we should preserve or decide explicitly

Zoho is the workflow benchmark, not the authority for our data ownership or security policy.

- Keep one Core identity, permissions/data scope, organization, document, approval and audit foundation. A login account is not necessarily an employee. The historical HRM host/folder name does not by itself make bounded modules non-modular.
- Keep Procurement manual evidence distinct from Finance-posted liabilities or settled payments. Do not create a duplicate AP, stock or asset ledger merely to match a screen.
- Retain approved/posted history through revisions, cancellation and compensating records. Do not copy destructive transaction deletion or bulk overwrite as the default way to change supplier currency or addresses. The [vendor currency FAQ](https://www.zoho.com/procurement/kb/vendors/change-currency/) is a specific example requiring an ERP-safe alternative.
- Keep exact arithmetic and current source/version controls. Quantity display preferences must not silently change historical values or storage precision.
- Core currently interprets no branch assignments as organization-wide reach. Make intentionally wide versus unconfigured access explicit and test it; do not silently infer scope from employment. AUD-03.
- Zoho subscription seat billing, support operating hours and product-specific account settings are not automatically Procurement requirements. Generic custom modules and executable custom functions need a separate product decision.
- The [backup FAQ](https://www.zoho.com/procurement/kb/general/automate-data-backup/) describes a vendor product limitation. Our own Tier 3 backup/recovery requirements still apply.

## Recommended next step

Use [M0–M9](CLOSURE-ROADMAP.md) to close the current operational flows and their release evidence; M8 supplier self-service is conditional on launch scope. Keep M10 optional platform extensions separately visible. Start with Core settings/permission visibility and genuine shared document evidence, then complete request-to-award-to-order and invoice/manual settlement through the existing screens. Finish with one coherent normal-session release test across Core and Procurement, rather than treating separate source merges and private proofs as a complete deployed module.
