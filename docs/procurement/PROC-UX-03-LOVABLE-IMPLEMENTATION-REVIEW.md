# Lovable Procurement UI implementation review

**Status:** Review of external UI repository<br>
**Reviewed:** 3 October 2026<br>
**Repository:** [georgemunganga/module-connect](https://github.com/georgemunganga/module-connect) at `17f7834a6c84d198f01739efb627c920a8b0e683`<br>
**Compared with:** [complete UI prompt pack](PROC-UX-02-COMPLETE-LOVABLE-UI-PROMPT-PACK.md)

This is a source review of the React/Lovable repository, not a visual browser test or backend audit. The repository describes itself as frontend-only with mock data. Its [roadmap](https://github.com/georgemunganga/module-connect/blob/17f7834a6c84d198f01739efb627c920a8b0e683/roadmap.md) checks off Prompts 00–08 and leaves 09–19 open. Those checkmarks indicate screens/scaffolds exist, not that every prompt acceptance condition has been met.

## Assessment

The prompt pack is a useful full-lifecycle sequence and the implemented UI follows it recognizably: shared shell, role-specific Home, guided request, catalog, supplier onboarding, planning, line-level requisition, approvals, and sealed sourcing responses are present. The repo correctly labels local demo behavior and separates some Finance-owned states. Continue using the pack, with a stabilization gate before Prompt 09 and a stricter completion review after every later prompt.

| Prompt area | Observed in repository | Review status |
|---|---|---|
| 00 Foundation | Shared shell, demo badge, typed mock readers, reusable views | Good start; mock mutations are not yet one shared data boundary |
| 01 Navigation/Home | Role-specific Home and rail; future destinations have explicit empty states | Partial: operations rail is flat, not the proposed expandable sublinks; several Home cards lead to placeholders |
| 02 Guided buying | Goods/service/asset/subscription flow, catalog-first, local draft | Partial: submission displays a reference but does not add it to My Requests |
| 03 Catalog | Search, filters, item detail, offer and expiry cues, manager view | Partial: item management is largely read-only; item eligibility does not consistently follow selected entity context |
| 04 Suppliers | Queue, onboarding form, profile, evidence, Finance-verification status, suspension demo | Partial: form/result and profile actions are page-local; some displayed actions have no behavior |
| 05 Planning | Plan views, filters, versions and estimates | Partial: Submit plan/Mark reviewed buttons have no handler, so the workflow is not complete |
| 06 Requisitions | Line table, routing, derived progress, source links and conversion demo | Needs correction: route/quantity semantics and cross-screen state can mislead downstream workflows |
| 07 Approvals | Task queue, stages, budget kinds, blockers, decision UI | Partial: task decision is page-local; request-detail approval callback is a no-op |
| 08 Sourcing | Event queue/create/detail, sealed bids, surrogate entry and amendments | Partial: actions are local; supplier notice and resend are not connected; evaluation/award belongs to Prompt 09 |
| 09–19 | Roadmap open; contracts, purchasing, invoices, reports use milestone placeholders | Not implemented yet |

## Corrections needed before Prompt 09

1. **One coherent demo transaction store.** [Request submission](https://github.com/georgemunganga/module-connect/blob/17f7834a6c84d198f01739efb627c920a8b0e683/src/routes/procurement.requests.new.tsx) generates a success reference, then My Requests still reads a fixed array. [Approval decisions](https://github.com/georgemunganga/module-connect/blob/17f7834a6c84d198f01739efb627c920a8b0e683/src/routes/procurement.approvals_%24id.tsx) and [request changes](https://github.com/georgemunganga/module-connect/blob/17f7834a6c84d198f01739efb627c920a8b0e683/src/routes/procurement.requests.%24id.tsx) use local component state. A user must see the same status, amount, owner and timeline after navigation and refresh. Use a typed mock repository with persisted demo state and operation methods; do not duplicate records in disconnected arrays.
2. **Route and record scope.** The [rail](https://github.com/georgemunganga/module-connect/blob/17f7834a6c84d198f01739efb627c920a8b0e683/src/modules/procurement/nav.ts) filters links, but direct supplier and sourcing routes still render records by ID without the same role/entity/supplier checks. The [sourcing detail](https://github.com/georgemunganga/module-connect/blob/17f7834a6c84d198f01739efb627c920a8b0e683/src/routes/procurement.sourcing_%24id.tsx) can show opened bid values to a role that reaches its URL. In this demo, guard route, record, field, search and page metadata consistently. A production API must enforce those permissions again.
3. **Correct line-level quantities and stages.** In the [request detail](https://github.com/georgemunganga/module-connect/blob/17f7834a6c84d198f01739efb627c920a8b0e683/src/routes/procurement.requests.%24id.tsx), changing route can allocate a line before approval; the Convert to PO button remains available for RFQ/contract routes; and creating a draft PO increments `orderedQty`. Draft allocation, issued order, and accepted receipt need distinct balances and source links. [Header progress](https://github.com/georgemunganga/module-connect/blob/17f7834a6c84d198f01739efb627c920a8b0e683/src/modules/procurement/requisition.ts) also uses the first line's remaining quantity when deciding Fulfilled; derive it across all lines.
4. **Finish or relabel marked milestones.** [Planning](https://github.com/georgemunganga/module-connect/blob/17f7834a6c84d198f01739efb627c920a8b0e683/src/routes/procurement.planning.tsx) has buttons with no action. [Configuration](https://github.com/georgemunganga/module-connect/blob/17f7834a6c84d198f01739efb627c920a8b0e683/src/routes/procurement.configuration.tsx) displays clickable setup cards without destinations. Keep the roadmap honest: label a milestone “UI preview” or “partial” until every primary action either works within the demo or is explicitly disabled with a reason.
5. **Keep future links truthful.** [Milestone placeholders](https://github.com/georgemunganga/module-connect/blob/17f7834a6c84d198f01739efb627c920a8b0e683/src/routes/procurement.area.%24area.tsx) clearly say they are not set up, which is good. Home cards should not imply that a PO, receipt, invoice, or report work queue is active until the relevant screen exists. Link to the placeholder only with a visible “Coming later” cue, or hide the card from action queues.

The source data is illustrative and contains separate representations of the same PR in the request array and line metadata. Reconcile totals, descriptions and quantities in one store before Prompt 19; that final journey cannot pass while source records disagree.

## Copyable corrective prompt for Lovable — run before Prompt 09

```text
Continue the existing module-connect Procurement UI. Before adding bid evaluation/award, stabilize the work already marked as Prompts 00–08. Read roadmap.md and the current Procurement routes and mock modules. Keep the existing visual design and shared ERP shell. Do not rebuild HRMS or replace the Procurement screens.

Create one typed Procurement demo repository/service boundary for requests, request lines, approvals, suppliers, catalog and sourcing. Persist demo changes across route navigation and refresh (local storage is acceptable for demo data), with a reset-demo control. Make submitted requests appear in My Requests and their detail. Make an approval decision update its task, request state/line approval, owner, next action, and timeline consistently. Make supplier and sourcing transitions visible in their queues and details. Do not claim external notifications, budget reservation, supplier verification or AP actions occurred.

Enforce demo role, entity, supplier, record and field scope on direct URLs and search, as well as navigation. A supplier role cannot enter internal Procurement routes; an employee cannot open sealed bids or supplier banking details; an auditor is read-only; an actor outside the record entity gets a neutral restricted state. Do not put restricted names or bid details in page metadata. Test by pasting direct URLs while each demo role is selected.

Repair requisition-line accounting: requested, approved, allocated to sourcing or PO draft, ordered by issued PO, accepted by receipt, cancelled, and remaining must be separate quantities. Changing route is allowed only for approved available quantity. RFQ route creates an RFQ allocation, contract route creates a contract release, and direct route may create a draft PO. A draft PO does not increment ordered quantity; only an issued PO does. Prevent over-allocation and derive header progress from every line, not the first line. Keep related record links real within the demo or clearly labelled pending.

Complete or relabel partial Prompt 00–08 work. Make Planning Submit/Review actions work with versioned state or mark them preview-only. Make catalog management and supplier review actions work or disable with an explanation. Replace no-op approval actions. Use one-level sublinks for Buying, Suppliers and Sourcing as the module grows; hide unavailable destinations or label them clearly as coming later. Update Home cards so they link to an implemented queue or visibly preview a future milestone.

Before calling this stabilization done, demonstrate: create request → appears in list/detail → approve → route an approved line to RFQ → open linked sourcing event → refresh → all records still agree. Also demonstrate self-approval blocked, direct URL access blocked for an unauthorized role, sealed bid hidden before opening, supplier banking masked, and a multi-line request that cannot be marked fulfilled until every line is settled. Provide a table of the previously checked milestones 00–08 with pass/partial status and any remaining demo limitations. Only then proceed to Prompt 09.
```

## Prompt-pack improvement

After each subsequent prompt, require a **completion evidence** response: route(s) added, working actions, data updates across screens and refresh, role/field restrictions, negative path demonstrated, placeholder status, and a short list of unfinished work. Mark a roadmap checkbox complete only when those checks pass. This makes the original 20-prompt pack usable without mistaking a polished screen for a finished workflow.
