# PROC-02 Purchase Requisition and Line Routing

**Document type:** Child feature specification<br>
**Status:** Draft for business and product review<br>
**Version:** 0.1<br>
**Prepared:** 3 October 2026<br>
**Hierarchy:** ERP → Procurement → Employee Requests and Guided Buying → Purchase Requisition and Line Routing → PROC-02 operations<br>
**Module source:** [PROC-00](PROC-00-PROCUREMENT-MODULE-ARCHITECTURE-AND-CAPABILITY-MAP.md)<br>
**Framework:** [ERP 28-section standard](../hrm/feature-specifications/ERP_Feature_Specification_Framework_Enterprise_Integration_Updated.docx)

**Identity correction — 22 October 2026:** Procurement request access belongs to an active Core
system user with assigned module/action permissions and authorized company/branch scope. An HRMS
employee link and job designation are optional metadata. This supersedes earlier employee-only
wording. [Principal ownership contract](release/PROC-02-CORE-PRINCIPAL-OWNERSHIP-20261022.md)
defines the current source change, retained legacy ownership and release gates. It does not claim
that the new migration or runtime verification has completed.

A purchase requisition (PR) records an internal need and the authority to procure it. **The PR header is a summary; every line has its own approval, route, allocation and balance.** One approved PR can therefore send laptops to an RFQ, printers to a direct PO, and software to a contract release without pretending they share one lifecycle state. This is a configurable draft, not an approved purchasing policy.

## 1. Feature Identification

**Feature ID:** PROC-02. **Parent:** Employee Requests and Guided Buying. **Child:** Purchase Requisition and Line Routing. **Priority:** foundation. **Target:** documentation/build Phase 1 for non-catalog requests and governed line routing; later catalog and sourcing/PO routes depend on their own approved feature specifications. **Lifecycle:** draft. **Business owner:** Procurement process owner, to name. **Data owner:** Procurement for PR and routing; Finance for budget and reservation. **Product and technical owners:** to assign. **Review date:** to agree.

Operation IDs are `PROC-02-O1` create/save, `O2` submit, `O3` return/revise/resubmit, `O4` decide per-line route, `O5` allocate/convert line demand, `O6` defer/cancel/close, and `O7` reconcile balances and downstream outcomes. They are detailed in section 8.

## 2. Purpose and Business Outcome

An authorized system user needs a clear way to request goods, services, assets or subscriptions before a supplier commitment exists. Procurement needs a trustworthy record of approved demand and of how each line was handled. The outcome is a request with valid coding, authority, budget evidence, route and remaining balance; no unapproved or over-allocated line can create a PO. Measures include time to approval, approved-but-unprocessed line age, route mix, duplicate requests, conversion accuracy and cancelled/unused demand.

## 3. Scope and Exclusions

**Included:** guided purpose selection, non-catalog PR header and lines, drafts, attachment evidence, validation, policy/budget request, approval handoff, return and resubmission, line-level route decision, allocation to future sourcing/order/contract workflows, partial ordering, defer/cancel/close, balance reconciliation and requester tracking. A catalog item reference may be captured when an approved catalog exists, but catalog administration is a separate child feature. One PR may contain different line types and routes where policy permits.

**Excluded from this child:** executing approvals or maintaining the approval engine; owning Finance budget balances or reservations; publishing an RFQ; selecting an award; issuing a PO; receiving goods; posting invoices or payments. Those are related features that consume the approved line record. An authorized direct-buy exception may bypass a PR only if a separately documented policy and workflow explicitly permit it; this document does not create that exception.

Phase 1 may record an intended line route and an approved-demand balance. **Actual conversion is enabled only when the destination feature and its reconciliation contract are implemented and approved.** A request must not claim to be ordered or sourced merely because a route was selected.

Launch entities, currencies, purchase types, on-behalf-of requests and partial line approval are decisions in section 28. HRMS recruitment requisitions remain separate records, number series and routes.

## 4. Parent and Related Features

Parent is Employee Requests and Guided Buying in [PROC-00](PROC-00-PROCUREMENT-MODULE-ARCHITECTURE-AND-CAPABILITY-MAP.md). Procurement configuration provides categories, methods, policy and numbering. Core provides active system users and access grants. HRMS optionally provides genuine employee and reporting-line references; Organization/Core or its agreed interim source provides entity, department, site and cost centre. Finance provides budget decision and reservation reference. Shared Workflow executes approvals. Supplier Management (`PROC-01`) provides supplier eligibility if a preference or route uses one. Sourcing, Contracts and Purchasing consume approved line quantities; Receiving and Invoice Validation return downstream progress. Annual Procurement Planning may supply an optional plan reference when documented.

## 5. Actors and Participants

| Actor | Participation |
|---|---|
| Requester | Creates, corrects, submits and tracks own PR; may withdraw eligible demand |
| On-behalf-of creator | Creates for another employee only under configured authority; both identities retained |
| Line manager/business owner | Reviews need through the shared approval workflow |
| Budget owner/Finance | Confirms coding and authoritative budget result or exception |
| Procurement buyer | Reviews approved lines and chooses permitted procurement route |
| Sourcing/contract/PO owner | Consumes allocated line demand and returns source references and progress |
| Workflow and integration services | Evaluate policy, assign tasks, synchronize outcomes and reconcile balances |
| Auditor | Reviews permitted request, decision, conversion and history evidence |

## 6. Entry Points and Triggers

Employee Self-Service **Create Request**; My Requests draft/resume/duplicate; authorized on-behalf-of action; returned-request correction; scheduled expiry or aging escalation; downstream conversion or cancellation callback; optional approved catalog selection; optional annual-plan demand. API entry and bulk import require separate authentication, idempotency, actor attribution and scope design before enablement.

## 7. Preconditions

An authenticated, active Core system user has assigned Procurement module/action permissions and a permitted tenant/entity and organization scope. Employee linkage or job designation is not a prerequisite for ordinary request CRUD. Effective procurement policy, category, unit, location and coding references are available. A request cannot submit with missing required fields or an unknown currency. Budget source and approval route must be available or the request enters a visible Pending/Blocked state under an approved controlled manual procedure; no outage is treated as budget approval. Downstream conversion additionally requires a current approved PR version, approved unconsumed line balance, permitted route, eligible supplier where relevant, and valid funding evidence.

## 8. Workflow

| Scenario | Actor and steps | Alternate path, error and output |
|---|---|---|
| PROC-02-O1 Create/save | Requester chooses goods/service/asset/subscription → searches approved catalog/contract if available → adds non-catalog or catalog lines → enters entity, department, location, need date, coding, estimate, currency, reason and evidence → saves Draft | Invalid reference stays Draft with field error; duplicate-looking request warns with related links, subject to scope |
| PROC-02-O2 Submit | Requester reviews totals and policy results → system fixes request version → asks Finance for budget decision → hands version to approval workflow → records Submitted and task owners | Hard validation/policy failure blocks; Finance unavailable leaves Pending Budget Decision; retry does not create duplicate version/tasks |
| PROC-02-O3 Return/revise/resubmit | Approver returns with reason → requester edits new draft version → policy, coding, budget and approval route rerun → resubmits | Rejected item cannot be silently edited into approval; prior submitted version and decisions remain readable |
| PROC-02-O4 Decide line route | Buyer reviews each approved line, catalog/contract availability and sourcing policy → assigns Direct PO, RFQ, Tender, Contract/Framework Release, Defer or Cancel with reason | Unsupported route, missing supplier eligibility or policy conflict blocks allocation; buyer cannot change approved commercial scope without reapproval |
| PROC-02-O5 Allocate/convert | Buyer selects one or more approved lines/quantities across PRs → system atomically reserves route allocation → creates downstream draft with source line/version/quantity → confirms conversion reference | If downstream creation fails, allocation is released or marked Pending Reconciliation; no second route consumes same balance |
| PROC-02-O6 Defer/cancel/close | Authorized actor defers with review date, withdraws eligible demand, cancels unused line balance or closes after reconciliation → Finance releases related reservation if one exists | Already ordered/accepted balance is handled by PO/receipt reversal, not erased from PR; release failure stays visible |
| PROC-02-O7 Reconcile progress | Downstream modules return RFQ/award/PO/receipt/closure facts → PR updates derived line balances and header summary → buyer resolves orphan or failed allocations | Duplicate/out-of-order facts are idempotent or quarantined; failed handoff appears in operations queue |

Every scenario records actor, effective policy, source version, reason where required, timestamp, related documents and correlation. The buyer's route decision is distinct from the requester's need and from the approver's authority.

**Correction implementation checkpoint:** A buyer or procurement manager with `procurement-request-return` may return a Submitted PR for changes with a reason and current version, provided no approval instance or confirmed Finance reservation is active. The requester can revise and resubmit; the earlier submitted contents and return reason remain in append-only audit evidence. Resubmission creates a new request version and buying-rule check; the previous Finance check is stale and must be redone. The return route is a correction control, not an approval or a Finance reservation release. Return and withdrawal of a reserved submitted request are blocked until Finance releases the reservation through its own authorized process.

## 9. Status and State Transitions

**Model:** a PR has a submission/approval status; each line also has route and fulfillment dimensions. Do not encode all three dimensions in one mutable string. The header is derived from its lines plus the current approval instance.

| Dimension | Candidate states | Key transition owner and prohibition |
|---|---|---|
| PR approval | Draft → Submitted → Pending Budget/Approval → Approved; Returned → Draft revision → Submitted; Rejected, Withdrawn, Cancelled | Requester submits/revises; assigned approvers decide; no Approved without all required decisions; submitted version immutable |
| Line approval | Requested → Pending Approval → Approved or Rejected/Returned; optionally Partially Approved | Shared workflow decides; partial approval disabled until business policy approves it |
| Line route | Unrouted → Pending Sourcing / Direct PO / Contract Release / Deferred / Cancelled | Buyer assigns after approval; no route allocation to cancelled/rejected/unapproved balance |
| Sourcing allocation | Unallocated → Allocated to RFQ/Tender → Awarded or Released | Sourcing service confirms; RFQ allocation is not an order/commitment |
| Order progress | Unordered → Partially Ordered → Fully Ordered | Purchasing returns issued/approved source quantities; cannot exceed approved unconsumed balance |
| Fulfillment progress | Not Received → Partially Accepted → Fulfilled; returns/reversals adjust accepted balance | Receiving facts update derived progress; invoice or payment does not imply receipt |
| Closure | Open → Closed, Cancelled or Expired where policy permits | Authorized owner after line/Finance reconciliation; no silent deletion |

| Transition example | Permitted actor and scope | Conditions and evidence | Automation, SLA and recovery |
|---|---|---|---|
| Draft → Submitted | Requester or authorized on-behalf-of creator in entity | Required fields/evidence, active authorized Core user, current policy | Budget and approval tasks; submission clock starts; retry returns same version |
| Submitted → Returned | Assigned approver in scope | Reason and requested correction | Notify requester; task clock pauses per policy; prior version locked |
| Approved line → RFQ allocation | Buyer in entity/category | Approved available quantity, sourcing method, evidence | Create RFQ draft; if creation fails release or reconcile allocation |
| Approved line → PO allocation | Buyer in entity/category | Approved available quantity, eligible supplier, funding | Create PO draft; no issued commitment until PO workflow completes |
| Any unused line → Cancelled | Requester/owner within allowed state | Reason, no unhandled downstream balance | Finance release request; unresolved release stays visible |

State SLA definitions must include business calendar, start/pause/resume, target, warning, breach and escalation owner. Candidate clocks: pending budget, pending approval, approved-unrouted and allocated-without-downstream-reference. Numeric targets are not set here; escalation never grants approval or creates a PO automatically.

## 10. Permissions and Data Scope

| Action | Permission example | Data scope and segregation |
|---|---|---|
| Create or edit Draft | `procurement.pr.create` / `.edit` | Self or authorized on-behalf-of; permitted tenant/entity/department; only Draft/Returned version |
| Submit or withdraw | `procurement.pr.submit` / `.withdraw` | Own/assigned request; active authorized Core identity; state gate |
| Read | `procurement.pr.read` | Self, assigned approval, department, buyer entity/category or auditor scope; field restrictions apply |
| Approve/return/reject | `procurement.pr.approve` | Assigned task and delegated authority; requester cannot finally approve own need under normal policy |
| Decide route | `procurement.pr.route` | Buyer assigned entity/category; no authority to alter approved scope or Finance decision |
| Allocate/convert | `procurement.pr.convert` | Buyer and downstream feature permission; source balance and supplier checks server-side |
| Cancel/close | `procurement.pr.cancel` / `.close` | Requester on eligible own balance or designated buyer/owner; open obligations protected |
| Audit/export | `procurement.pr.audit.read` / `.export` | Approved entity and field scope; exports logged |

Permissions and record scope are independent. A manager with approval permission and Own Department scope cannot approve another department. Supplier portal users have no PR access by default. Search, links, attachments, bulk actions and APIs enforce the same boundaries.

## 11. Business Rules

| ID | Candidate invariant or configurable rule |
|---|---|
| BR-PROC-PR-001 | A submitted PR version is immutable; changes create a new version and rerun affected policy/budget/approval checks. |
| BR-PROC-PR-002 | A line needs valid type, description/item ref, unit, quantity, need date, entity, delivery location, coding, estimate/currency and reason according to category policy. |
| BR-PROC-PR-003 | A suspended/inactive Core account or revoked Procurement permission cannot submit a PR. Employee offboarding alone does not revoke an otherwise active authorized system user; historic principal/worker attribution remains unchanged. |
| BR-PROC-PR-004 | No unapproved, rejected, cancelled or expired line balance can create a sourcing/order allocation. |
| BR-PROC-PR-005 | The sum of live line allocations and ordered quantities cannot exceed approved quantity/value under the applicable conversion rule. An RFQ allocation cannot also be used for a direct PO. |
| BR-PROC-PR-006 | Material change to quantity, estimated value, coding, type or supplier preference as defined by policy requires re-evaluation and reapproval. |
| BR-PROC-PR-007 | A Finance check is not a reservation unless Finance returns a reservation reference; failed or stale hard-budget decision cannot be treated as Pass. |
| BR-PROC-PR-008 | Header status and totals are derived from line and approval records; they are not independently edited to force closure. |
| BR-PROC-PR-009 | Cancellation or closure preserves allocated/ordered/accepted history and requests release of unused Finance commitment where applicable. |
| BR-PROC-PR-010 | Direct or emergency purchase without PR is allowed only through a separately approved and documented exception method. |

Tax-inclusive threshold basis, price/value variance, partial approval and line splitting are configuration decisions, not constants in this document.

## 12. Data Fields and Ownership

| Record or field group | Key fields | Source and ownership |
|---|---|---|
| PR header | ID/number, tenant/entity, requester subject and worker ref, on-behalf-of actor, department/branch/site refs, cost centre/project/plan refs, purpose, delivery location, submitted version, approval state, created/updated time | Procurement owns transaction; HRMS/Organization own reference masters |
| PR line | Stable line ID, parent/version, purchase type, catalog/item ref or free-text specification, category, unit, requested/approved quantity, estimate and currency, need date, tax reference if known, supplier preference, source document refs | Procurement owns request; catalog/Finance own relevant references |
| Policy and budget snapshot | Policy ID/version, validation outcomes, Finance check ID/time/status, reservation ID/status if provided, currency-conversion source | Procurement stores response snapshot; Finance owns budget truth/reservation |
| Routing/allocation | Line ID/version, method, decision actor/reason/time, allocated quantity/value, route status, RFQ/contract/PO target ref, allocation version and idempotency key | Procurement owns source allocation; target module/workflow owns its document |
| Derived balances | Approved available, allocated to sourcing, ordered, accepted, cancelled/deferred, residual | Procurement derives from committed source and downstream facts; values carry as-of time |
| History and evidence | Transition, actor, reason, prior/new status, approval and document versions, correlation ID | Procurement business audit; shared platform may hold immutable audit copy |

Amounts use decimal values with explicit currency. The conversion rule must define whether quantity, value or both constrain a service/subscription line. The HRMS worker ID and organization/cost-centre references are stable IDs, not copied master names. Payroll/private HR data are excluded.

## 13. Screens and Data Views

Employee entry points: **My Requests**, Create Request, Drafts, Returned Requests and PR detail with each line's route, owner and next step. The create flow first asks purchase type, searches approved catalog/contract when available, then offers permitted non-catalog entry. Buyers have Approved Demand, Unrouted Lines, Pending Sourcing Allocations, Partial Conversion and Reconciliation queues. Approvers see only assigned decision summary and evidence. Detail views show header, line table, amount/currency, policy/budget source and time, approval history, allocations, downstream links, documents and activity timeline. Search, saved filters, export and bulk conversion are scope checked.

## 14. User Experience and Human Behaviour

Requesters should understand the difference between **requested**, **approved**, **sourcing started**, **ordered**, **delivered**, **invoiced** and **paid**. Never imply that an approved PR means a supplier has accepted an order. Show line-specific status because one request can progress unevenly. Duplicate-looking request warnings should help avoid repeat buying but not expose another team's confidential request. Explain policy and budget blocks in plain language with named resolution owner. Drafts should autosave, long forms should preserve progress, and return should highlight exact changes needed. Keyboard and mobile access are required. Requesters see next action and expected update rather than technical event logs.

## 15. Notifications and Communications

Submission notifies the assigned approver; return/rejection/approval notifies requester; approved unrouted lines notify buyer; route decision and PO/RFQ conversion update requester; aging or failed conversion notifies responsible operator. Recipients, business-hours SLA and escalation follow effective workflow configuration. In-app is baseline; email, SMS or WhatsApp require channel policy. Messages link to authorized detail and omit sensitive pricing or justification where recipient/channel lacks access. Repeated line events can be grouped into one actionable notification.

## 16. Documents and Attachments

Candidate documents include business case, specification, supplier quote/preference evidence, project approval, asset justification and budget exception evidence. Submission pins attachment versions and scan status to PR version; returned edits create new evidence versions without deleting earlier submitted evidence. Scope, classification, expiry if applicable, checksum, access log, retention and legal hold are required. A required but unscanned document cannot satisfy submission. Downstream RFQ/PO receives secure references or approved copies, not uncontrolled file duplication.

## 17. Exceptions and Edge Cases

Inactive Core user with draft; active system user without an employee; employee profile deactivated while its Core account remains active; manager or department changes while approval pending; multiple employment assignments; invalid or closed cost centre; no budget configured; stale budget response; estimated price changes before ordering; mixed currency; request line with zero/decimal quantity or service value; catalog item deactivated after draft; supplier suspended after route decision; one line split among two suppliers; RFQ allocation cancelled before award; PO draft creation succeeds but confirmation is lost; partial PO issued then remaining line cancelled; return after partial order; duplicate/out-of-order downstream event. Each case must either block, create a controlled exception with owner, or reconcile; none silently rewrites approved demand.

## 18. Integrations, APIs, Events and Sandbox

Consume current Core account, permissions and authorized organization scope, optional employee/manager projection, organization/location/coding references, catalog/contract eligibility when enabled, Finance budget check/reserve/release, and shared workflow decision results. Publish versioned `PurchaseRequisitionCreated`, `PurchaseRequisitionSubmitted`, `PurchaseRequisitionApproved`, `PurchaseRequisitionReturned`, `PurchaseRequisitionCancelled`, `PurchaseRequisitionLineRouted` and allocation/release facts only after commit. Sourcing/Contracts/Purchasing return target ID/version and quantity/value outcome; Receiving returns accepted progress. API resources should cover header, lines, submit, revise, cancel, route, allocate and reconcile; exact paths, schemas and auth claims are technical design tasks. Every contract includes tenant/entity, PR and line ID/version, actor/source, occurred time, correlation and idempotency keys. No other module reads Procurement tables directly. Sandbox uses synthetic workers, budgets, catalog items, suppliers and downstream responses; test retries, concurrent conversions, late events and permission failures before production promotion.

## 19. Audit, Security and Privacy

Append draft creation, submitted snapshot, changed fields, policy/budget result, approval reference, route decision, allocation, conversion, cancellation and reconciliation with actor, role, source, time, before/after, reason and policy version. No ordinary role edits completed events. Tenant/entity/department/assigned-record and field scope apply server-side, including attachments, search, events and exports. On-behalf-of action preserves both actor and subject. Encrypt data in transit and at rest; avoid copying payroll or unrelated personal data. Privileged exports and exception overrides are monitored.

## 20. Reports, Dashboards and Metrics

Open PRs by status and owner; approval turnaround; approved-but-unrouted line aging; routing mix by method; partially ordered and unconverted approved balances; request-to-PO cycle time; returned/rejected reasons; duplicate warnings; cancelled demand; budget failure/exception count; allocation reconciliation backlog. Define numerator, denominator, source states, period, business calendar, entity, currency conversion and report owner before publishing. Do not sum different currencies or count RFQ allocation as committed spend.

## 21. Configuration and Localization

Configure purchase type and categories, required fields and attachments, numbering, non-catalog permission, preferred/blocked supplier policy, quote/sourcing thresholds, tax-inclusive threshold basis, approval strategy, budget check/reservation point, partial approval and split rules, cancellation authority, SLA clocks, notification channels, units, date/number/currency display and effective country/entity policy. The item/catalog schema may have shared ownership with Inventory/Assets; this must be agreed before catalog build. No example monetary threshold is hardcoded.

## 22. Non-Functional Requirements

PR submission and line allocation are concurrency safe and idempotent. Search, list, export and API enforce scope at scale. Long forms recover from interruption; errors are actionable and accessible. Integration delivery and balance reconciliation are observable with correlation IDs. Define target request volume, peak concurrent approvers/converters, latency, availability, document size and recovery objectives after launch estimates are supplied. Downstream outages must not cause duplicate PR versions or over-allocation.

## 23. Postconditions

After submission, a fixed PR version, evidence and pending owner exist. After approval, each approved line has a policy and Finance result, an available balance and an assigned buyer or route task. After allocation, source line/version and target draft reference reconcile with exactly one live allocation per quantity/value slice. After order/receipt, progress is derived from acknowledged downstream facts. After cancellation or closure, unused demand and any Finance reservation release are reconciled; historical approval and allocation remain readable.

## 24. Failure and Recovery Behaviour

Validation failure leaves editable Draft; it does not create approval tasks. Finance outage leaves Pending Budget Decision or controlled manual review, never implicit Pass. Unknown conversion outcome is queried by idempotency key before retry; allocation may stay Pending Reconciliation until target confirmation. Downstream rejection releases a provisional allocation through a recorded compensation. Duplicate or out-of-order events are ignored or quarantined by source version. A failed Finance release is an open exception and closure cannot claim funds were released. Operators use retry/reconcile actions with reasons and audit, not direct table edits.

## 25. Acceptance Criteria

1. **Given** an active authorized Core user (with or without an employee) and valid references, **when** they submit a complete PR, **then** one immutable submitted version and the configured budget/approval tasks exist after any retry.
2. **Given** an inactive Core account, revoked action permission or invalid entity/coding, **when** submission is attempted, **then** it fails with a scoped, actionable error and no approval task.
3. **Given** a hard policy or Finance budget failure, **when** approval or conversion is attempted, **then** progression is blocked until the designated exception is approved and recorded.
4. **Given** a returned PR, **when** the requester edits and resubmits, **then** prior version, reason and decisions remain visible and policy/budget/approval are reevaluated.
5. **Given** one PR with three approved lines, **when** the buyer routes them respectively to RFQ, direct PO and contract release, **then** each line shows its own target and balance while the header shows derived overall progress.
6. **Given** concurrent conversions of the same line, **when** they compete for available quantity/value, **then** at most the approved balance is live-allocated or ordered.
7. **Given** failed downstream creation after allocation, **when** retry or reconciliation runs, **then** the source and target agree and no duplicate RFQ/PO is created.
8. **Given** a partly ordered PR, **when** unused balance is cancelled, **then** issued order history remains, only valid remainder is cancelled, and Finance release state is visible.
9. **Given** a user outside the permitted tenant/entity/department scope, **when** they search, open, export or access a PR attachment, **then** the action is denied without leaking line details.
10. **Given** RFQ allocation but no award/PO, **when** spend reports run, **then** they do not label the allocation as ordered or paid spend.

## 26. Test Scenarios

**Positive:** create/save/resume, goods/service/asset/subscription lines, submit/approve, return/resubmit, route mixed lines, partial direct order, RFQ/contract allocation, close remaining balance. **Negative:** missing fields, stale organization/cost centre, inactive Core account, revoked permission, hard budget failure, unapproved line conversion, wrong route, supplier ineligible, insufficient balance, self-approval conflict. **Boundary/concurrency:** zero/decimal quantity, threshold edge, currency conversion, one line split, two concurrent conversions, cancellation after partial order, catalog deactivation. **Security:** self/team/department/entity/tenant scope, field masking, attachment and export access, on-behalf-of attribution. **Integration/recovery:** Finance outage/stale result, duplicated submit, lost target confirmation, duplicate/out-of-order downstream event, reservation-release failure. **Load/accessibility:** peak list/form and conversion targets once volumes are agreed; keyboard and mobile completion.

## 27. Ownership and Dependencies

Procurement process owner approves PR purpose, route policy and cancellation rules. Procurement data owner owns PR and source allocation. Finance owner defines budget response, reservation/release and coding; HRMS/Organization owners provide workforce and entity references; Workflow owner executes approvals; Sourcing, Contracts and Purchasing owners define conversion acknowledgements. Platform owners provide identity, documents, notifications, audit and search. Product and technical owners must be named. `PROC-CFG-01` and shared reference contracts are prerequisites; `PROC-01` supplies supplier eligibility for relevant routes. Later feature documents must respect PR line ID/version and balance semantics.

## 28. Open Decisions and Assumptions

| Decision | Proposed owner | Needed before |
|---|---|---|
| Launch entities/countries, currencies and initial purchase types | Procurement business owner and Finance | Field and policy sign-off |
| Which organization/cost-centre source is authoritative while HRMS holds current organization records | ERP architecture and data owners | Reference contract |
| Catalog timing, item master owner and non-catalog restrictions | Procurement, Inventory and Assets | Guided buying design |
| Partial line approval, line splitting and quantity-versus-value balance for services | Procurement governance | State and allocation implementation |
| Direct/emergency buying without PR, quote thresholds and contract-first rules | Procurement governance | Policy configuration |
| Budget check versus reservation point, hard-failure path and controlled manual evidence | Finance budget owner | Submit/approval contract |
| Approval strategy, self-approval compensating policy, SLA calendar and targets | Workflow and Procurement owners | Task configuration |
| Conversion target API/event acknowledgement and unknown-outcome reconciliation | Sourcing, Contracts, Purchasing and technical owners | Integration design |
| Cancellation/closure authority and commitment-release rule | Procurement and Finance | State transition sign-off |
| Number series, data retention and launch performance volumes | Product, records and operations owners | Migration and release planning |

This feature is ready for stakeholder review, not yet approved for implementation. Section 28 must be resolved, state/route policy confirmed, and the Finance and downstream conversion contracts agreed before build.
