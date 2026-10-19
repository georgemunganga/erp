# PROC-CFG-01 Procurement Configuration and Policy

**Document type:** Child feature specification<br>
**Status:** Draft for business and product review<br>
**Version:** 0.1<br>
**Prepared:** 3 October 2026<br>
**Hierarchy:** ERP → Procurement → Procurement Platform and Configuration → Procurement Configuration and Policy → PROC-CFG-01 operations<br>
**Module source:** [PROC-00](PROC-00-PROCUREMENT-MODULE-ARCHITECTURE-AND-CAPABILITY-MAP.md)<br>
**Framework:** [ERP 28-section standard](../hrm/feature-specifications/ERP_Feature_Specification_Framework_Enterprise_Integration_Updated.docx)

This feature defines the Procurement-owned settings that other procurement workflows consume. It uses shared ERP identity, organization, workflow, document, notification and audit capabilities where those are available; it does not create another copy of their master data. Values in this document are candidate configuration fields, not approved company policy.

**Implementation note, 19 October 2026:** Tenant/entity-scoped category and buying-method list/detail/create/update APIs and live Settings UI are deployed. Category activation requires an independent reviewer and recorded reason. The next source increment adds the same review, version and audit control for buying methods; a linked policy must already be Published and currently effective before its method can activate. A method without a linked policy can be activated as a reference only; that does not grant purchase authority or set a sourcing threshold. Policy authoring, simulation, publication and effective-rule resolution are still unimplemented.

## 1. Feature Identification

**Feature ID:** PROC-CFG-01. **Parent:** Procurement Platform and Configuration. **Child:** Procurement Configuration and Policy. **Priority:** foundation prerequisite for supplier and purchase request workflows. **Lifecycle:** draft. **Business owner:** Procurement governance owner, to name. **Data owner:** Procurement for policy definitions and published versions. **Product and technical owners:** to assign. **Target:** documentation/build Phase 1. **Review date:** to agree.

Operations: `PROC-CFG-01-O1` draft policy; `O2` validate and simulate; `O3` approve and publish; `O4` resolve effective policy for a transaction; `O5` supersede or retire; `O6` monitor expiry and configuration gaps; `O7` reconcile shared reference changes.

## 2. Purpose and Business Outcome

Procurement needs a controlled way to change purchasing methods, approvals, sourcing and evidence requirements without changing code or silently rewriting historical decisions. The outcome is one unambiguous effective rule set for each transaction and scope, with a reviewer, version, effective period and explanation. Users can see why a request was blocked or routed. Success measures include policy coverage, conflicting-rule prevention, expired or missing configuration, time to publish an approved change, and decisions that can be reproduced from their pinned policy version.

## 3. Scope and Exclusions

**Included:** procurement preferences, methods, categories, item/service categories, purchase-type restrictions, quote and sourcing requirements, approval matrix definitions, thresholds and materiality, numbering series, business calendars and SLA rules, document/evidence requirements, reminder rules, supplier-portal preferences, controlled transaction options, delivery/payment terms references, evaluation templates and procurement document templates. Rules are scoped and effective dated, previewed, approved and published as immutable versions.

**Excluded:** owning worker and organization records; defining currencies, FX rates, tax calculations, accounting codes or Finance budgets; executing generic workflow tasks; building arbitrary custom modules or executable custom functions; implementing a general ERP metadata engine. Procurement may consume shared currency, tax, coding, role, workflow, notification and document references. Country-specific legal or tax decisions require business/Finance/legal approval outside this document.

The initial launch can configure only the entities, transaction types and methods approved for rollout. A policy record for a future capability does not enable that capability until its feature is built and released.

## 4. Parent and Related Features

Parent is Procurement Platform and Configuration in [PROC-00](PROC-00-PROCUREMENT-MODULE-ARCHITECTURE-AND-CAPABILITY-MAP.md). [PROC-01 Supplier Onboarding and Management](PROC-01-SUPPLIER-ONBOARDING-AND-MANAGEMENT.md) consumes diligence and activation rules; [PROC-02 Purchase Requisition and Line Routing](PROC-02-PURCHASE-REQUISITION-AND-LINE-ROUTING.md) consumes request, routing, approval and budget-gate rules. Sourcing, Contracts, Purchasing, Receiving and Invoice Validation consume later rule families. Shared Workflow executes tasks using Procurement's transaction-specific policy; Finance owns budget/tax/accounting truth; Organization/Core or an agreed interim source owns entity/site/department references.

## 5. Actors and Participants

| Actor | Participation |
|---|---|
| Procurement policy administrator | Drafts and maintains procurement-specific configuration |
| Procurement governance approver | Reviews and authorizes publication; separate from author where policy requires |
| Finance policy steward | Approves budget/commitment, tax-reference and financial threshold semantics |
| Compliance/legal reviewer | Reviews regulated sourcing, evidence and supplier rules where applicable |
| Organization/workflow/platform steward | Supplies shared reference and execution contracts |
| Transaction user or approver | Sees applicable rule explanation; cannot edit policy through transaction screen |
| Integration operator | Monitors reference sync and policy publication delivery |
| Auditor | Reads published versions, changes, simulations, approvals and usage |

## 6. Entry Points and Triggers

Configuration centre; change request following approved governance decision; annual policy review; new legal entity/category/method; shared currency or organization reference change; rule expiry; failed transaction policy resolution; newly released procurement capability; import of approved policy values if a controlled import is later specified. Publishing must not occur merely because an administrator saved a form.

## 7. Preconditions

Tenant and scoped legal entities exist; the author has configuration permission; a valid policy family and transaction type are selected; shared references can be resolved; publication approvers and effective-time rules are configured. A proposed policy has a clear precedence and cannot overlap ambiguously with published policy. Where a Finance-owned budget or tax reference is required, Finance must approve the contract and value before publication. Unavailable shared references block publication rather than being replaced with unverified free text.

## 8. Workflow

| Scenario | Actor and normal path | Alternate or failure path |
|---|---|---|
| PROC-CFG-01-O1 Draft | Admin selects tenant/entity, transaction type, category and rule family → enters values, effective period, reason and supporting decision → saves Draft | Invalid reference or unauthorized entity stays Draft with field errors |
| PROC-CFG-01-O2 Validate/simulate | System checks required fields, overlap, gaps, precedence, circular approval, invalid thresholds and unpublished dependencies → admin runs synthetic transaction examples → records results | Failed validation blocks review; ambiguous scope must be corrected, not resolved arbitrarily at runtime |
| PROC-CFG-01-O3 Approve/publish | Admin submits immutable candidate → independent authorized reviewer approves → system publishes at effective time and records version, approver and prior policy | Return/reject with reason; approval conflict or Finance sign-off missing blocks publish |
| PROC-CFG-01-O4 Resolve | Transaction supplies tenant/entity, transaction type, category, amount/currency, date and other policy inputs → system selects exactly one applicable version or a defined composable set → records explanation and version on transaction | No match, multiple matches or stale critical reference fails closed and creates configuration task |
| PROC-CFG-01-O5 Supersede/retire | Admin drafts successor or proposes retirement → impact preview lists affected future transactions → reviewer authorizes effective change → old version stays available to historical decisions | Cannot erase an active version if no successor covers required launch scope, unless capability is explicitly disabled |
| PROC-CFG-01-O6 Monitor | Scheduled check finds impending expiry, missing coverage, orphan number sequence or disabled reference → owner receives task → corrects through normal versioning | Reminder failure enters delivery queue; no automatic policy approval |
| PROC-CFG-01-O7 Reconcile reference | Shared source changes currency, entity, cost-centre, workflow role or tax reference → system identifies policies using old ID → steward resolves before effective use | Unknown/deactivated reference blocks affected publication or transaction, with visible owner |

Each operation records actor, tenant/entity scope, effective date, old/new version, reason, approval, validation output, correlation and impacted rule family. A transaction decision uses the policy version effective for its defined decision instant; the instant itself must be agreed by rule family.

## 9. Status and State Transitions

| From → To | Permitted actor and conditions | Required data | Automation, SLA and recovery |
|---|---|---|---|
| Draft → Pending Review | Policy admin; validation passes | Change reason, scope, effective period, simulation and evidence | Assign reviewer; review clock starts; invalid draft stays Draft |
| Pending Review → Returned or Rejected | Assigned reviewer; reason required | Specific correction or rejection reason | Notify author; Returned creates editable successor draft, original review snapshot retained |
| Pending Review → Approved for Publication | Independent authorized approver; Finance/compliance sign-offs complete where required | Approval decision and policy version | Schedule publication; no self-approval if policy prohibits |
| Approved for Publication → Published | System at authorized effective instant | Non-overlap and shared references still valid | Atomically activate; publish event after commit; failure leaves visible Pending Publication |
| Published → Superseded | System when approved successor takes effect | Successor ID and effective time | Historic transactions remain pinned; queued work reviewed for applicable version |
| Published → Retired | Authorized governance action; capability disabled or successor coverage accepted | Impact review and reason | Future resolution stops; historical reads preserved |

Published, Superseded and Retired policy versions are immutable. A correction is a new version with traceable supersession. Draft deletion may be allowed only before any review or use and must still leave an administrative audit event. SLA duration, business calendar, pause condition, warning and escalation are configured; escalation does not publish a policy.

## 10. Permissions and Data Scope

| Action | Permission example | Scope and separation |
|---|---|---|
| Read applicable policy | `procurement.policy.read` | Effective policy explanation for authorized transaction; sensitive internal thresholds may be masked by role |
| Draft/edit | `procurement.policy.manage` | Assigned tenant/entity/rule family; Draft only |
| Simulate | `procurement.policy.simulate` | Authorized policy scope and synthetic or permitted transaction data |
| Review/approve | `procurement.policy.approve` | Assigned effective authority; independent of author where policy requires |
| Publish/retire | `procurement.policy.publish` | Authorized entity/rule family; only approved candidate |
| Audit/export | `procurement.policy.audit.read` | Approved tenant/entity and field scope; export logged |

Permission and data scope are separate. An administrator for one legal entity cannot publish a group-wide policy. Finance can approve financial semantics without gaining permission to alter unrelated supplier or sourcing rules. Workflow service accounts can resolve published policy but cannot edit it. UI hiding does not replace server authorization. Delegated approval cannot exceed the delegator's scope.

## 11. Business Rules

| ID | Candidate rule |
|---|---|
| BR-PROC-CFG-001 | For a given required scope and decision instant, policy resolution returns exactly one deterministic applicable rule set or fails with a named gap/conflict. |
| BR-PROC-CFG-002 | A Published version is immutable and remains retrievable for historic transaction replay. |
| BR-PROC-CFG-003 | Effective periods and precedence cannot overlap ambiguously; a narrower rule overrides or composes with broader rules only under an explicit precedence model. |
| BR-PROC-CFG-004 | Numeric thresholds, tolerances and approval limits are business configuration with currency, tax-inclusive basis and effective period; no values are hardcoded from examples. |
| BR-PROC-CFG-005 | A financial or tax rule cannot be published without Finance-owned reference validation and required sign-off. |
| BR-PROC-CFG-006 | Policy change affects future defined decision points; it does not silently reapprove, revoke or recalculate a completed decision. |
| BR-PROC-CFG-007 | A transaction records the policy ID/version, decision inputs and output sufficient to explain and reproduce its route. |
| BR-PROC-CFG-008 | Disabling a method or category cannot delete open transactions; each receives an explicit grandfather, re-evaluate, hold or cancel decision under approved policy. |
| BR-PROC-CFG-009 | Rule publication and transaction policy resolution are tenant and entity scoped; cross-tenant fallback is prohibited. |
| BR-PROC-CFG-010 | A configured approval strategy cannot grant self-approval or exceed delegated authority contrary to the adopted segregation policy. |

The hierarchy of global/tenant/entity/category rules and whether multiple policies compose are open decisions. Until resolved, implementation must not invent a precedence order.

## 12. Data Fields and Ownership

| Record or field group | Minimum fields | Owner and classification |
|---|---|---|
| Policy package | ID, tenant, entity scope, rule family, transaction type, category/purchase type, version, state, effective start/end, author, approver, reason, predecessor | Procurement-owned controlled configuration |
| Procurement method | Code, permitted category/entity, sourcing route, evidence/quote requirement, exception path | Procurement-owned business policy |
| Threshold and approval rule | Amount, currency/basis, comparison operator, role/authority, sequence/parallel quorum, delegation/materiality, policy version | Procurement-owned rule; Finance signs off financial basis |
| Required evidence/template | Document type, required condition, classification, template/version, retention reference | Procurement requirement; shared document service stores files |
| SLA/reminder | State/transition, business calendar reference, target, pause rule, warning, escalation role/channel | Procurement rule; shared workflow/notification executes |
| Number series | Entity, transaction type, prefix/pattern, next allocation contract, effective period | Procurement policy; atomic number allocator ownership to decide |
| Shared reference | Currency/tax code, entity/site, role, cost centre, UOM, delivery/payment term ID and source version | Owning ERP module; Procurement stores stable reference and validation snapshot |
| Simulation and decision snapshot | Input facts, matched rules, output, warnings/blocks, actor, time, correlation and policy version | Procurement-owned evidence; protect real transaction data |

Tax rates, exchange rates, budget balances, GL accounts, user records and department master remain with their owners. Supplier banking data is not policy data. Use decimal amounts, ISO currency identifiers, explicit time zone and effective instant.

## 13. Screens and Data Views

Configuration landing page with rule-family coverage and pending changes; scoped policy list/filter; draft editor with structured fields; validation and simulation results; side-by-side version comparison; review/approval detail; published version timeline; effective-policy explanation from a transaction; missing coverage/expiry queue; shared-reference reconciliation; authorized export. Forms show scope and effective date prominently. Regular employees see rule outcomes relevant to their task, not the full administrative editor.

## 14. User Experience and Human Behaviour

Administrators may assume a saved threshold is live; the UI must distinguish Draft, Approved for Publication and Published, with effective time and affected entities. Show the consequences of a change using representative synthetic cases and a human-readable explanation. Avoid hidden precedence: reveal which rule wins and why. A reviewer should see before/after values and impacted workflows, not only raw JSON. Requesters and approvers need a plain-language reason for a block or extra approval. Accessibility and keyboard use apply to long policy forms and comparisons.

## 15. Notifications and Communications

Notify assigned reviewer on submission; author on return/reject/publish; Finance/compliance on required sign-off; affected workflow owners before policy effective date where necessary; operators on publication failure, expired required policy or reference gap. Reminder lead times and channels are configuration. In-app is baseline; email or other channels require policy. Messages link to authorized version detail and avoid including sensitive thresholds or transaction data in a broad notification.

## 16. Documents and Attachments

Policy decision memo, authority schedule, Finance sign-off, compliance review, template samples and simulation evidence may be attached. Published policy references exact attachment and template versions. Shared document service provides scanning, classification, access, retention and legal hold. Template replacement creates a new controlled version; issued historical PO or RFQ documents continue to reference the template version used. A missing required signed authority document blocks publication.

## 17. Exceptions and Edge Cases

Overlapping entity/category policies; effective-date gap at midnight or across time zones; threshold in one currency with transaction in another; missing FX source; newly created department or category without policy; retired role assigned as approver; circular approval route; policy changes after PR submission but before PO issue; number-series collision; disabling a method with open RFQs; tax reference withdrawn; two admins publish simultaneously; rollback after erroneous publication; future policy approved before prerequisite feature deploys. Each receives a deterministic block, hold or review task; none silently selects an arbitrary default.

## 18. Integrations, APIs, Events and Sandbox

Procurement consumes versioned shared references for identity/organization, currencies/FX, tax codes, cost centres, workflow roles, documents and notification channels. Proposed domain APIs cover draft, validate, simulate, submit for review, approve, publish, resolve at decision time, compare versions and retire; exact routes and schemas follow technical design. Publish `ProcurementPolicyPublished`, `ProcurementPolicySuperseded` and `ProcurementPolicyRetired` only after commit. Events include tenant, entity scope, rule family, version, effective instant, source, correlation and idempotency key; no sensitive policy document is broadcast. Transaction services resolve through a defined interface, cache only with safe invalidation/version semantics, and record the chosen version. Sandbox uses synthetic entities, currencies, roles and transactions, including overlap, edge-time, FX-unavailable, duplicate publication and replay cases. No cross-module table access.

## 19. Audit, Security and Privacy

Append draft changes, validation, simulation, review, sign-off, publication, retirement, privileged resolve/export and failed authorization with actor, role, source, timestamp, before/after, reason and correlation. A published version is read-only to normal and admin application roles. Enforce tenant/entity/rule-family scope and two-person control where adopted. Policy simulations with real transaction data inherit that record's privacy scope. Encrypt in transit/at rest, restrict secrets and monitor unusual mass changes. Retention is set by records policy, not inferred from benchmark software.

## 20. Reports, Dashboards and Metrics

Configuration coverage by required entity/transaction/category; ambiguous/failed resolution count; future expiry; drafts awaiting review; publication turnaround; unused rules; policy version usage; transactions blocked by missing rule; failed reference sync; override/exception frequency. A control KPI needs a defined denominator, as-of time, owner and explanation of whether a rule was required for the relevant capability. No dashboard should imply that a rule is enforced merely because it is configured; feature availability and execution status are separate.

## 21. Configuration and Localization

This feature itself configures policy scope and effective dates, but shared locale and currency master remain external. Support local business calendar, time zone, language/labels, number and date display, currency-specific amount precision, entity-specific document template and effective policy. Incoterms, delivery and payment term codes are references where applicable, not free-text legal conclusions. Country packs or tax codes are versioned by their owning module; Procurement only stores reference IDs and displays approved meaning. No Zambia-only logic is embedded in code.

## 22. Non-Functional Requirements

Publication is atomic; two concurrent publications cannot both become effective for the same scope. Policy resolution is deterministic, explainable, tenant safe and observable. Transaction capture must record enough inputs/version for replay. Cached policy must not lag a critical suspension or publication beyond an agreed target; cache invalidation or version check is required. Admin forms and simulations are accessible and responsive. Set latency, availability, peak number of rules, audit retention, recovery objective and maximum propagation delay after pilot size and operational needs are known.

## 23. Postconditions

After publication, exactly the approved version is effective at its scheduled instant, historical versions remain readable, dependent transactions can resolve it, and publication delivery is monitored. After supersession/retirement, prior transaction decisions still refer to their original version. After a failed publication, the earlier effective policy remains authoritative and a named operator has a reconciliation task.

## 24. Failure and Recovery Behaviour

Invalid validation or simulation does not mutate Published policy. Approval and publication use idempotent commands. An unknown publication outcome is read back by policy ID/version before retry. Shared-reference outage blocks affected new policy publication or transaction resolution according to severity, with explicit operator queue; it never creates a made-up tax, currency or cost-centre value. A failed policy event is retried with the same version/key. Emergency rollback is a new approved superseding version or controlled feature hold, not deletion or in-place edit. Operators record intervention and verify affected transactions.

## 25. Acceptance Criteria

1. **Given** overlapping policy scopes without explicit precedence, **when** publication is attempted, **then** it fails with the conflicting versions and no new effective rule.
2. **Given** a valid Draft, **when** its author attempts sole final approval where two-person control applies, **then** the server denies publication.
3. **Given** an approved future-effective version, **when** its start instant arrives, **then** new decisions use it and earlier decisions still resolve to their pinned old version.
4. **Given** a missing Finance-owned reference or sign-off, **when** a financial rule is submitted, **then** publication is blocked with a named owner and reason.
5. **Given** an entity/category transaction, **when** policy is resolved, **then** the response includes matched version, inputs, outcome and explanation or a deterministic gap/conflict error.
6. **Given** a published policy, **when** an admin edits it, **then** the edit creates a draft successor rather than mutating the published version.
7. **Given** an unpublished or retired method, **when** a buyer tries to use it, **then** the transaction is blocked or routed to the explicitly configured transition policy.
8. **Given** a publication event replay or API retry, **when** it is processed, **then** one version becomes effective and historical audit has no duplicate business decision.
9. **Given** a user outside tenant/entity/rule-family scope, **when** they read, simulate, export or publish policy, **then** the action is denied without leaking restricted fields.

## 26. Test Scenarios

Positive: draft, simulate, approve, future publish, resolve and supersede per entity/category. Negative: overlap, gap, invalid threshold, missing Finance sign-off, self-approval, retired shared reference and unpublished method. Boundary: exact effective instant, time zone and daylight-saving change, currency conversion absence, amount at threshold, empty category, newly added entity. Concurrency/recovery: two publishers, duplicate command, unknown commit result, cache invalidation, failed event and rollback-by-successor. Security: tenant/entity/rule-family and sensitive-threshold access, simulation with restricted transaction. Load/accessibility: configured rule count and resolution latency once pilot targets are agreed; keyboard and screen-reader review of version comparison.

## 27. Ownership and Dependencies

Procurement governance owner approves method, category, approval and evidence policy. Finance owner approves budget/tax/accounting references and amount semantics. Compliance/legal owners review rules within their remit. Organization, Identity, Workflow, Document, Notification and Audit owners provide shared contracts. ERP technical owner decides backend runtime and migration path. This feature is a prerequisite for [PROC-01](PROC-01-SUPPLIER-ONBOARDING-AND-MANAGEMENT.md), [PROC-02](PROC-02-PURCHASE-REQUISITION-AND-LINE-ROUTING.md) and later sourcing/order/receipt/invoice features. Named people and operational service owners must be assigned.

## 28. Open Decisions and Assumptions

| Decision | Proposed owner | Needed before |
|---|---|---|
| Policy precedence and composition across tenant, entity, category and transaction type | Procurement governance and ERP architecture | Rule schema and resolver |
| Decision instant and whether an in-flight transaction is re-evaluated after policy change | Procurement and Finance | State/approval contracts |
| First launch entities, countries, methods and rule families | Product and Procurement | Configuration seed and release |
| Approval authority, two-person control and delegated scope | Governance and Identity/Workflow | Publication flow |
| Finance-owned budget, tax, currency, FX and cost-centre reference contracts | Finance and shared-platform owners | Financial rule validation |
| Number-series allocator ownership and collision/reversal semantics | ERP architecture and Procurement | Transaction numbering |
| SLA calendars/targets, reminder channels and escalation | Procurement operations and platform | Workflow configuration |
| Custom fields/conditional validation availability and safe limits | ERP platform and Product | UI/configuration design |
| Retention, legal hold, document templates and external communications | Records, Legal and Procurement | Evidence/template design |
| Procurement runtime, migration owner, deployment and policy-cache strategy | ERP technical owner | Implementation plan |

This is a complete draft structure for review. The business must approve values and owners, and the technical owners must agree shared contracts before this feature becomes an implementation contract.
