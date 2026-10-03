# Complete Procurement UI prompt pack for Lovable

**Status:** Draft UI implementation handoff<br>
**Prepared:** 3 October 2026<br>
**Source:** [PROC-00](PROC-00-PROCUREMENT-MODULE-ARCHITECTURE-AND-CAPABILITY-MAP.md), [navigation prompts](PROC-UX-01-NAVIGATION-AND-LOVABLE-PROMPTS.md), and the [child feature specifications](README.md)

This pack covers the whole planned Procurement UI. It is a sequence of copyable prompts for a designer or Lovable. The feature documents remain the business source of truth. Their section 28 decisions are open; this pack must not be treated as approval of thresholds, legal terms, tax logic, or backend contracts. Build the interface around configurable rules and clearly labelled demonstration data until those decisions are settled.

## How to use this pack

1. Give Lovable this document, the [capability map](PROC-00-PROCUREMENT-MODULE-ARCHITECTURE-AND-CAPABILITY-MAP.md), the [navigation document](PROC-UX-01-NAVIGATION-AND-LOVABLE-PROMPTS.md), and the relevant child specification for each prompt.
2. Apply Prompt 00 first. Apply Prompts 01–18 in order, keeping the same shell, components, status language, and mock data. Prompt 19 is the final integration pass.
3. Ask Lovable to show changed routes, screens, and working interactions after each prompt. Review those before sending the next prompt.
4. Use a typed data-access boundary and realistic local demo data if the Procurement API does not exist. Show a visible **Demo data** label. Do not imply a payment, budget reservation, supplier verification, tax validation, bid submission, or notification was executed externally when only local UI state changed.
5. Make all demo transitions obey the same basic state and permission rules as the feature documents. A button is not complete merely because it changes a label.
6. For the current `module-connect` implementation, run the corrective prompt in [PROC-UX-03](PROC-UX-03-LOVABLE-IMPLEMENTATION-REVIEW.md) before Prompt 09. Require route, working-action, cross-screen state, refresh, permission, negative-path, and remaining-work evidence before checking off each later milestone.

## Shared UI contract for every prompt

- Keep Procurement inside the existing ERP shell and brand. Reuse its spacing, typography, controls, navigation, search, organization context, notifications, responsive rules, and accessible components where possible.
- Use the role-aware menu in [PROC-UX-01](PROC-UX-01-NAVIGATION-AND-LOVABLE-PROMPTS.md). Internal employee/buyer/admin views and supplier portal are distinct experiences. Menus have at most one sublink level.
- Use stable detail URLs and list filters in URLs. Each detail has a title/reference, status, owner, entity, next action, related records, documents, and timeline. Split dense detail into tabs or sections; keep the primary action visible.
- Use status labels consistently: **requested**, **approved**, **ordered**, **accepted**, **invoiced**, **handed to AP**, **posted**, and **paid** are different facts. Receipt progress and invoice progress on a PO are separate.
- Every transition shows who may perform it, required evidence, confirmation where consequential, result, and recovery if it fails. Reject, return, cancel, override, amend, and reverse require a reason.
- Show loading, empty, no access, validation error, integration pending, integration failed, stale data, and success states. Provide a route back to the work queue. Never silently discard form input.
- Enforce role, tenant, entity, supplier, record, and field scope in demo views. UI hiding is only a preview of authorization; a real implementation must enforce it in the API and search.
- Keep demo amounts and threshold values labelled **illustrative**. Avoid hard-coded claims about policy or actual payment. Show currency and conversion basis when values from different currencies are compared.
- Use plain, task-focused copy. Show the next action and why a record is blocked. Avoid exposing internal IDs unless useful for audit/support.
- Meet keyboard, focus, contrast, screen reader, reduced-motion, small-screen, and touch-target needs. Tables must have a useful mobile presentation.

## Route and coverage map

Routes below are proposed UI paths, not a final API contract. Each prompt may add more detail routes within its prefix.

| Prompt | Main area | Proposed route prefix | Source |
|---|---|---|---|
| 00–02 | Shell, Home, guided buying | `/procurement`, `/procurement/buying` | PROC-00, PROC-UX-01, PROC-02 |
| 03 | Items and catalog | `/procurement/catalog` | PROC-04 |
| 04 | Suppliers | `/procurement/suppliers` | PROC-01, PROC-13 |
| 05 | Plans | `/procurement/plans` | PROC-05 |
| 06 | Requisitions and line routing | `/procurement/requests` | PROC-02 |
| 07 | Approvals and budget | `/tasks`, `/procurement/requests/:id` | PROC-03 |
| 08 | Sourcing and bids | `/procurement/sourcing` | PROC-06 |
| 09 | Evaluation and award | `/procurement/sourcing/:id` | PROC-06 |
| 10 | Contracts and recurring commitments | `/procurement/contracts` | PROC-07 |
| 11 | Purchase orders | `/procurement/orders` | PROC-08 |
| 12 | Goods/service receiving and returns | `/procurement/receiving` | PROC-09 |
| 13 | Invoice intake and credits | `/procurement/invoices` | PROC-10 |
| 14 | Match, AP handoff, payment status | `/procurement/invoices/:id` | PROC-11 |
| 15 | Supplier portal | `/supplier` | PROC-12 |
| 16 | Reporting, collaboration and audit | `/procurement/reports` | PROC-14, PROC-13 |
| 17 | Configuration and policies | `/procurement/configuration` | PROC-CFG-01 |
| 18–19 | Shared edge states and final journey | All routes | All specifications |

## Prompt 00 — Establish the Procurement UI foundation

```text
We are adding Procurement to an existing ERP with HRMS. Inspect the current app's shell, visual tokens, routing, and reusable controls before making screens. Create a Procurement UI foundation that fits the existing product. Use the attached PROC-00 capability map and PROC-UX-01 navigation specification.

Create role presets for Employee, Manager/Approver, Buyer, Supplier Steward, Receiver, AP Processor, Procurement Admin, Auditor, and Supplier User. Use one internal ERP shell and a distinct supplier portal shell. Add a visible Demo data indicator and typed mock service boundary because the Procurement backend is not yet implemented in this repo. Organize realistic demo records for one end-to-end purchase and its exception variants; keep all screens consistent with those same records.

Build reusable list, detail, status badge, activity timeline, document, money, entity context, task, exception, and confirmation patterns. Each meaningful control must navigate, filter, edit local demo state, or explain why it is disabled. Do not produce static click-through images. No third-level sidebar menus. Do not alter existing HRMS flows.

Show the new route list, shared components, demo role switcher, and one example list/detail pair before continuing.
```

## Prompt 01 — Role-aware navigation and home

```text
Implement PROC-UX-01. Employee sidebar: Home, Catalog when enabled, My Requests, My Orders & Deliveries, and Approvals only when assigned. Procurement operations sidebar: Home; Buying; Suppliers; Sourcing; Contracts; Purchasing; Invoices; Reports; Configuration for admins. Supplier portal gets its own scoped navigation.

Build Home around the user's next action. Employee sees Create Request, recent requests, delivery/approval tasks, and clear status. Buyer sees unprocessed approved request lines, sourcing deadlines, POs awaiting issue, deliveries, invoice exceptions, and supplier onboarding. Approver sees pending decisions and due times. Auditor sees read-only search and activity. Do not sum mixed currencies. Each card links to a filtered work queue.

Use the shared ERP top bar for entity switcher, global search, Tasks & Approvals with count, notifications, help, and profile. Make collapsed/mobile navigation usable with keyboard and screen reader. Show a no-permission state and an empty first-use state.
```

## Prompt 02 — Guided buying entry

```text
Build the employee Create Request experience from PROC-02 and PROC-04. Start with a simple question: Goods, Service, Asset, or Subscription. Let the employee search eligible catalog items and existing contract offers first, then use a policy-controlled non-catalog path. Explain required fields as they appear. Keep a persistent request summary showing lines, quantities, estimated totals, currency, needed-by date, and delivery destination.

Support save draft, resume, duplicate, remove line, and attach evidence. Ask for purpose and accounting context at the right point, not all at once. If an item is restricted, a contract is required, or a supplier is blocked, show the reason and the next permitted route. Show a review step before submission with approval and budget expectations; never claim funding is reserved unless Finance confirms it.

Use the same draft record that Prompt 06 will display. Include catalog, non-catalog, partial draft, validation error, and unavailable budget-service examples.
```

## Prompt 03 — Items and catalog

```text
Build PROC-04's Procurement Items and Catalog UI. Employee catalog: searchable cards/list for goods, services, asset candidates, consumables, and subscriptions; category, unit, specification, eligible entity/site, preferred supplier, indicative or contracted price, currency, lead time, validity, and availability. Add filters and an item detail that feeds the guided request.

Buyer/admin item workspace: create and maintain item references, classification, unit, tax reference, preferred supplier/price, contract link, eligibility, active dates, and visibility rules. Show who owns a stock/asset classification when another ERP module is authoritative. Version changes affecting price or eligibility. Allow a non-catalog request when policy permits, with a clear reason.

Include expired price, inactive item, cross-entity restriction, no results, and item with multiple supplier offers. Do not show stock-on-hand as Procurement-owned data.
```

## Prompt 04 — Supplier onboarding, profile, risk, and performance

```text
Build PROC-01 and PROC-13. Create a supplier queue with Proposed, Under Diligence, More Information Required, Conditional, Active, Suspended, Expired, and Inactive filters. Supplier profile tabs: Overview, Sites & Contacts, Categories, Qualifications, Risk & Performance, Documents, Related Orders, and Activity. Use a step-by-step onboarding form with company identity, legal/tax references, contacts, service categories, required documents, duplicate candidates, and review assignment.

Show separate decisions for Procurement approval and Finance verification of sensitive payment instructions. Mask bank/remittance details and show verification status/reference, not broad raw fields. Activation is blocked until required evidence and approvals exist. Changes to legal or sensitive fields create a new review version. Suspension requires reason, effective date, and affected open PO/invoice review.

Performance uses evidence-backed delivery, quality, responsiveness, and dispute trends with date range and source. Show insufficient-data state. Include duplicate candidate review, expiring certificate, rejected application, supplier suspension, and read-only auditor view.
```

## Prompt 05 — Procurement planning

```text
Build PROC-05's optional planning workspace, visible only when enabled. Let a department plan a financial period by requirement, category, estimated value/currency, expected date, funding source, method, owner, and notes. Show Draft, Submitted, Reviewed, Approved, Active, Revised, and Closed states with a versioned revision history.

Provide department and procurement review views, plan-line table, totals within one currency, filters, variance/linked requisition view, and a clear 'planning estimate, not a budget reservation' label. An approved plan may seed a request but cannot itself issue an order. Include missing budget link, changed estimate, cancelled requirement, and locked prior version.
```

## Prompt 06 — Requisition workspace and line routing

```text
Build the full PROC-02 requisition UI. Lists for My Requests and buyer Requests support status, entity, department, category, requester, date, owner, and exception filters. A request detail shows header summary and a line table with each line's approved, allocated, ordered, received/accepted, and remaining quantity/value. One request can route lines to direct PO, RFQ/tender, contract/framework, defer, or cancel.

Add save draft, submit, return for correction, withdraw, buyer review, assign route, convert approved line, partial order, and close actions with permitted-state checks. After submission, material edits use return/reopen/version; preserve the original. Provide related RFx/PO/receipt links and a chronological timeline. Header status is derived from line progress, not a substitute for it.

Include an unapproved-line conversion block, a line split across sourcing and direct purchase, insufficient balance, concurrent conversion warning, partially fulfilled request, and cancelled remainder. Show precise next owner and why each action is available.
```

## Prompt 07 — Tasks, approvals, budget decisions, and delegation

```text
Build PROC-03 in the shared Tasks & Approvals workspace and linked request/PO/supplier screens. Show assigned, delegated, overdue, completed, and escalated tasks. Decision detail shows transaction summary, policy version, decision stage, prior decisions, supporting documents, budget decision timestamp/source, and conflict warning. Actions: Approve, Reject, Return, Delegate when allowed; require reason for Reject/Return and a deliberate confirmation for consequential approvals.

Support sequential and parallel stages, pending approvers, quorum, effective-dated delegation, and SLA countdown/paused/breached views. A requester cannot finally approve their own request in normal demo policy. A delegate cannot exceed authority. A hard budget failure blocks approval; an authorized exception route shows named approver and evidence. Distinguish an advisory budget check from an actual Finance reservation/commitment reference.

Include manager change/reassignment, stale approval after material edit, approver outside entity, budget service unavailable, hard no-budget, and approved exception. Do not auto-approve on SLA breach.
```

## Prompt 08 — Sourcing event and supplier responses

```text
Build PROC-06's sourcing workspace. Buyer creates an event from approved request lines and chooses permitted Direct, Single Source, Emergency, RFQ, RFP, or Tender method. The event editor covers requirements, items/quantities, deadlines and time zone, supplier eligibility, invitations, terms, response documents, and fixed evaluation criteria/weights. The publication preview shows exactly what suppliers will receive.

Create event queue, event detail, invitation tracker, Q&A, sealed response inbox, and a surrogate-bid capture form for an authorized buyer entering an offline response. Surrogate entry records supplier, source channel, received time, entered time, original evidence, and actor. Submitted bids are versioned and locked; the buyer cannot quietly edit them. Before the authorized opening, show receipt metadata without bid content.

Include draft, pending approval, published/open, closed, cancelled/reopened, no invitation delivered, late bid, portal outage, and no-response states. Changes after publication need a controlled amendment and supplier notification.
```

## Prompt 09 — Bid evaluation, comparison, and award

```text
Continue PROC-06 with controlled opening, compliance checklist, individual evaluator scoring, technical/commercial stages when configured, normalized comparison, recommendation, and award approval. Display declared conflicts and exclude unresolved conflicted scores. Show each supplier's bid version, currency, delivery terms, exclusions, non-price score, and total-cost assumptions. Make the calculation basis visible; do not call an estimate difference 'savings' without an approved method.

Allow full award, line-level partial award, no award, disqualification, and reroute of unawarded demand. Award recommendation records rationale, risks, quantities/value, and approvers; it creates only an approved award reference, not an issued PO. Supplier-facing outcome notices contain only authorized content.

Include no compliant bids, one bidder, evaluator withdrawal, changed supplier eligibility, currency mismatch, and award value above approved line balance. Keep competitor bids and scores hidden from supplier users.
```

## Prompt 10 — Contracts, frameworks, renewals, and recurring commitments

```text
Build PROC-07. Contract list and detail show supplier, entity, owner, category, effective/expiry dates, renewal notice, signed version, ceiling, currency, terms, rate schedule, obligations, related sourcing and POs, committed drawdown, and remaining capacity. Use a Draft → Review → Approved → Signed/Active → Expiring → Renewed/Expired/Terminated/Closed progression with controlled amendments.

Add framework agreement releases and a recurring commitment view for subscriptions/services with frequency, expected amount, owner, renewal, and contract link. Distinguish framework ceiling from blanket PO limit and recurring commitment from Finance-owned recurring bills. Show expiry reminders, utilization, over-ceiling block, pending signature, old signed version, and renewal decision. Sensitive contract documents follow explicit access.
```

## Prompt 11 — Purchase orders and amendments

```text
Build PROC-08. PO list/detail should show supplier, source PR/RFx/contract, entity, ship-to/bill-to, schedule, line prices, tax reference, terms, total, currency, approval, issue version, acknowledgment, receipt progress, invoice progress, and remaining commitment. Draft PO can be generated only from an approved eligible route in the demo.

Provide review/approve, issue, supplier response, change request, numbered revision with before/after comparison, reapproval when materiality rule says so, partial cancellation, blanket/standing PO release and ceiling, and closure. Record issue channel, delivery result, acknowledgment, and timestamp. Keep receipt and invoice progress as separate visual tracks.

Show supplier suspended, changed price/quantity, PO above source authority, duplicate issue retry, partial delivery, expired PO, and remaining balance on closure. An approved/issued PO is never edited in place.
```

## Prompt 12 — Goods receipt, service acceptance, inspection, and returns

```text
Build PROC-09. Receiving queue starts from issued POs. Goods receipt form shows ordered, previously accepted, remaining, delivered, inspected, accepted, rejected, and returned quantities per line; receiving site, delivery note, date, lot/serial when relevant, photos/evidence, receiver, and inspection outcome. Allow multiple partial receipts and a receipt document view.

Service acceptance is a separate path: service period/milestone, deliverable, accepted value or quantity, responsible owner, completion evidence, and acceptance/rejection. Add inspection checklist, conditionally accepted outcome when policy permits, return request, replacement/credit reference, and reversal with reason. Reversal preserves the original receipt.

Show over-delivery block/tolerance, damaged goods, wrong item, partial acceptance, missing evidence, service dispute, prior receipt reversal, and Inventory/Asset handoff status. Procurement does not show a self-maintained stock balance or asset register.
```

## Prompt 13 — Invoice intake and supplier adjustments

```text
Build PROC-10's invoice inbox. Intake sources: manual entry, supplier portal, email/API placeholder, and document upload. Show source, supplier/entity, invoice number/date, due date, currency, tax totals, PO/contract reference, lines, attachment, version, and intake owner. If OCR is shown, label extracted fields as unconfirmed until reviewed.

Create validation summary for required fields, duplicate key, supplier eligibility, totals, tax reference, and PO lookup. Provide supplier correction request and immutable original/replacement version history. Add commercial credit-note and refund-reference views linked to original invoice/return; Finance owns posting and cash application.

Include duplicate invoice, unreadable upload, unknown supplier, invoice before receipt, wrong entity, currency discrepancy, and credit for cancelled service. Local demo save must not claim AP posting.
```

## Prompt 14 — Match workbench, exceptions, AP handoff, payment visibility

```text
Build PROC-11's match workbench. Place invoice, PO, and accepted receipt/service evidence side by side at line level, with comparable quantities, prices, tax, total, source versions, configured tolerance, difference, and exact discrepancy reason. Use two-way match only for permitted cases and three-way match where acceptance is required. Rejected or reversed receipts are excluded.

An exception gets type, owner, due date, evidence, and resolution history. Resolution paths: corrected invoice, PO revision, receipt correction, supplier credit, or authorized variance approval. Rerun the match after source changes. Matched and approved invoice moves to Handoff Pending, Handed to AP, AP Rejected, Posted, Partially Paid, or Paid only as Finance confirms each fact. Show idempotent handoff key, attempt/acknowledgment log, and stale/pending integration state to authorized staff.

Include partial receipt against full invoice, price and tax variance, hard duplicate, Finance outage/unknown send outcome, AP rejection, conflicting payment callback, and credit application. There is no Pay button in Procurement.
```

## Prompt 15 — Supplier portal end to end

```text
Build PROC-12 as a separate supplier-scoped portal. Supplier Home shows pending registration evidence, RFx invitations/deadlines, PO acknowledgments, delivery updates, invoice corrections, and confirmed payment status. Include profile/document submission, own opportunity/bid view, quotation submission receipt, PO accept/reject/change request, delivery notice, invoice/credit submission, own transaction messages, and document expiry notices.

Keep internal risk notes, competitor bids, evaluation scores, accounting coding, raw bank details, and other suppliers' records out of supplier views. Show clear submitted/version-locked states and source time for bids. Payment status must say when Finance last confirmed it. Include expired invitation, upload failure, duplicate submission retry, suspended supplier, and supplier user without access to this supplier.
```

## Prompt 16 — Reports, record collaboration, audit, and controls

```text
Build PROC-13/14 operational reporting and record collaboration. Reports: request backlog/turnaround, sourcing coverage and cycle time, supplier delivery/quality, PO commitments and overdue deliveries, contract utilization/expiry, invoice first-pass match and exception aging, budget/commitment vs actual when Finance data exists, and emergency/sole-source activity. Every metric detail shows definition, period, entity, currency basis, source, and last refresh. Use saved filters and authorized export; do not sum mixed currencies silently.

On each business record provide an activity timeline with actor, role, event, old/new state, reason, document/version, policy version, and integration result. Add internal comments, mentions, followers, and explicitly supplier-visible messages. Distinguish audiences before send. Auditor gets read-only reconstruction from request through award, order, receipt, invoice, AP and payment confirmation.

Show empty/insufficient-data charts, stale Finance status, filtered scope, export permission denial, and a failed integration requiring operator attention.
```

## Prompt 17 — Procurement configuration and policy

```text
Build PROC-CFG-01 as the single Procurement Configuration entry, visible only to admins. Internal sections: purchasing preferences, methods/categories, thresholds and approval strategies, budget/tolerance rules, numbering, calendars/SLAs, supplier rules, document templates, portal preferences, validation, notifications, and integrations/status. Reuse shared ERP controls for roles, currencies, tax references, workflow execution, documents, and audit; link to those settings instead of duplicating them.

Make configuration versioned and effective-dated. Show draft, validation, review, publish, future-effective, retired, and rollback/replacement history where relevant. Include a policy simulator: select entity, category, amount/currency, supplier/risk, and route; show which rule version would apply and expected approval path without changing a transaction. Any numeric examples are illustrative.

Show conflicting rules, gaps, effective-date collision, missing authority, invalid tolerance, pending approval on policy change, and read-only view for non-admins.
```

## Prompt 18 — Responsive, accessibility, and recovery pass

```text
Audit every Procurement screen built so far. At desktop, tablet, and phone widths, verify navigation, dense line tables, comparison workbench, forms, document previews, and approval dialogs. Preserve a readable mobile alternative to every data table. Support keyboard-only operation, visible focus, labels/error association, accessible statuses, sufficient contrast, reduced motion, and non-color-only variance indicators.

Check loading, empty, validation error, no permission, expired session, network loss, upload failure, integration pending, integration rejected, concurrent edit, stale version, and retry states. Preserve unsaved drafts across recoverable interruptions. For irreversible or consequential transitions, confirm the exact record/version and show the result or failure clearly.

Return a route-by-route issue list, fix the issues, and identify any interactions still simulated because they require backend contracts.
```

## Prompt 19 — Final connected journey and handoff

```text
Run a complete demo journey using one consistent set of linked records:

Employee creates a request with three lines: a catalog good, a non-catalog service, and a contract-covered subscription. Manager and budget owner review it. Buyer routes one line to RFQ, one to direct PO under illustrative policy, and one to contract release. Supplier responds to RFQ; evaluators score; approver awards; buyer creates and issues PO versions. Supplier acknowledges. Receiver accepts goods partly, rejects damaged units, and service owner certifies a milestone. Supplier invoices the accepted quantities. Matcher resolves one discrepancy and hands the validated invoice to Finance demo adapter. Finance demo status later reports Posted and Paid. Close the remaining balance with reasons and show the full audit timeline.

Then demonstrate negative paths: self-approval blocked, hard budget failure, suspended supplier, sealed bid protected, award above approved balance blocked, issued PO revision/reapproval, over-receipt blocked, duplicate invoice, AP rejection, supplier tenant isolation, and stale payment status. Every route must remain reachable from role-appropriate navigation, and the same record totals/statuses must agree across screens.

Provide a final deliverable list: routes, reusable components, role visibility matrix, mock data/service boundary, known unimplemented integrations, responsive/accessibility findings, and explicit mapping from each PROC-CFG-01/PROC-01–PROC-14 feature document to its screens. Fix missing screens or dead-end actions before calling the UI complete.
```

## Final UI acceptance checklist

- All 15 current child feature drafts have an internal or supplier-facing UI surface, or an explicit reason the capability is shared ERP infrastructure rather than a Procurement screen.
- Employee, manager, buyer, supplier steward, receiver, AP processor, admin, auditor, and supplier roles have coherent starting points and only authorized actions.
- One request can split into multiple routes by line, and every downstream record links back to its source.
- Approved/issued/submitted records are versioned or reversed, never silently overwritten.
- Supplier portal cannot reveal another supplier or internal evaluation information.
- Every important transition has visible preconditions, error feedback, next owner, and history.
- Payment and budget states are displayed as externally confirmed facts, with freshness and failure states.
- Desktop and mobile journeys, keyboard access, and empty/error/recovery states are demonstrated.
- The final handoff clearly separates functioning local demo interactions from backend-dependent operations.
