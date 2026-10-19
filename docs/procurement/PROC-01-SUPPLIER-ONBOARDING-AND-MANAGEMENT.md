# PROC-01 Supplier Onboarding and Management

**Document type:** Child feature specification<br>
**Status:** Draft for business and product review<br>
**Version:** 0.1<br>
**Prepared:** 3 October 2026<br>
**Hierarchy:** ERP → Procurement → Supplier Management → Supplier Onboarding and Management → PROC-01 operations<br>
**Module source:** [PROC-00](PROC-00-PROCUREMENT-MODULE-ARCHITECTURE-AND-CAPABILITY-MAP.md)<br>
**Framework:** [ERP 28-section standard](../hrm/feature-specifications/ERP_Feature_Specification_Framework_Enterprise_Integration_Updated.docx)

This feature establishes a proposed supplier profile, verifies it, decides whether it is orderable, and controls later changes or suspension. It defines business behaviour before implementation. Required documents, screening methods, authority, expiry periods and launch entities remain open decisions; examples are not approved policy.

**Implementation progress, 19 October 2026:** Draft intake, independent approval, qualification review and a controlled active-vendor hold are implemented. A hold requires a separate Procurement manager, current version, reason and acknowledgment of open orders; the reviewer can inspect an access-controlled list of open order references. The hold sets the supplier non-orderable and retains decision history. A separate reviewer can reactivate a held vendor after recording resolution and checking current category, contact, qualification and payment-verification status. Automated qualification expiry and the authoritative Finance remittance-verification interface remain open. This progress does not claim the full PROC-01 workflow is complete.

## 1. Feature Identification

**Feature ID:** PROC-01. **Parent:** Supplier Management. **Child:** Supplier Onboarding and Management. **Priority:** foundation. **Target:** documentation/build Phase 1. **Lifecycle:** draft. **Business owner:** Procurement supplier management lead, to name. **Data owner:** Procurement for supplier commercial profile; Finance for accounting account and verified remittance instructions. **Product and technical owners:** to assign. **Review date:** to agree.

Operation/scenario IDs: `PROC-01-O1` propose supplier; `O2` resolve duplicate; `O3` verify and approve; `O4` change supplier; `O5` suspend/reactivate; `O6` expire qualification or deactivate. These IDs are stable within this draft; the detailed contracts appear in section 8.

## 2. Purpose and Business Outcome

Employees and buyers need an eligible supplier they can identify reliably before a PO is issued. A supplier steward needs evidence of legal identity, relevant qualifications and independent review of sensitive payment instructions. The business outcome is an orderable supplier reference with a clear decision history. Measures include onboarding time, duplicate resolution, expired qualification backlog, orders blocked for ineligible suppliers, and sensitive-change verification completion.

## 3. Scope and Exclusions

**Phase 1 included:** internal supplier request, legal and contact profile, supplier sites, categories, evidence checklist, duplicate review, configured verification, approval/return/reject, activation, controlled material change, suspension and deactivation, basic classification, and Finance payment-verification reference. **Deferred to separately documented children:** supplier self-registration and portal, automated screening, performance scorecards, advanced risk scoring, RFx participation, payment statement, and supplier-facing collaboration. An offline supplier may still be contacted through a controlled internal workflow. Procurement does not post supplier AP accounts or store payment credentials in general supplier views.

Coverage is tenant and legal-entity scoped. Whether one legal supplier can be shared across entities, and whether a group-level global supplier ID exists, require a business and data-ownership decision.

## 4. Parent and Related Features

Supplier Management is the parent in [PROC-00](PROC-00-PROCUREMENT-MODULE-ARCHITECTURE-AND-CAPABILITY-MAP.md). Procurement configuration provides required fields, categories, diligence tiers and approval policy. Workforce identity and shared organization provide actor and entity scope. Finance verifies remittance information and maps the supplier to AP. Purchasing needs Active/Approved status; sourcing may invite Proposed suppliers only if policy explicitly allows it. Contracts and invoices retain historical supplier version references.

## 5. Actors and Participants

| Actor | Participation |
|---|---|
| Initiator | Employee or buyer proposes supplier and business reason |
| Supplier steward | Completes profile, resolves duplicate and evidence gaps, proposes activation/change |
| Procurement approver | Makes eligibility decision within delegated authority, distinct from initiator where policy requires |
| Compliance reviewer | Reviews configured qualification, ownership or risk evidence |
| Finance verifier | Independently verifies remittance instructions and returns restricted verification reference |
| Supplier contact | Supplies documents through approved channel; no internal ERP login in Phase 1 |
| System/operations | Duplicate check, expiry reminders, Finance handoff, retry/reconciliation |
| Auditor | Reads permitted history and evidence; cannot change records |

## 6. Entry Points and Triggers

New supplier request from buyer or employee; authorized internal creation; existing supplier material-change request; qualification/document expiry; periodic review; Finance verification result; PO attempt against ineligible supplier; suspension or reactivation decision. Bulk import and external API creation are excluded until separately specified with duplicate and scope controls.

## 7. Preconditions

Initiator is active and authenticated; tenant/entity and supplier category are valid; effective policy defines required evidence and approvers; a proposed supplier can be identified without overwriting an existing record. Activation additionally requires mandatory fields, duplicate disposition, diligence completion, independent sensitive-detail verification where applicable, and no active hard block. If Finance verification is unavailable, the supplier remains non-orderable or follows an explicitly approved interim procedure; outage never implies verification.

## 8. Workflow

| Scenario | Normal path | Alternate or exception path |
|---|---|---|
| PROC-01-O1 Propose | Initiator enters identity, entity/site, category and reason → saves Draft → submits immutable application version | Missing evidence returns field-level errors; inactive actor denied |
| PROC-01-O2 Resolve duplicate | System flags exact/likely match → steward reviews IDs and evidence → links as existing or documents distinct supplier | No automatic merge; unresolved match holds activation |
| PROC-01-O3 Verify and approve | Steward completes checklist → Compliance reviews when policy requires → Finance verifies restricted remittance reference → approver approves → supplier becomes Active | Return for information, conditional approval with expiry if allowed, or reject with reason |
| PROC-01-O4 Change | Steward proposes new version → system classifies material fields → relevant reviewers verify → approver publishes version | Prior Active version remains authoritative until approval; blocked sensitive change never becomes visible as verified |
| PROC-01-O5 Suspend/reactivate | Authorized owner records reason/effective time → new PO issue blocked → open obligations reviewed → reactivation follows new eligibility review | Urgent block can take immediate effect but requires documented review and notification |
| PROC-01-O6 Expire/deactivate | Scheduled rule alerts owner before expiry → owner renews evidence or status becomes Expired/Inactive per policy → ordering blocked | Late evidence restores only after review; existing POs/invoices remain accessible |

Every path records actor, source, time, version, reason, required document references, tasks and notifications. No path deletes an approved supplier or rewrites a historical PO snapshot.

## 9. Status and State Transitions

The **application status** and **orderability status** are distinct. The application can be under review while a previously approved supplier version remains orderable, unless a hard suspension applies.

| From → To | Actor and condition | Required data/documents | Automation, SLA and failure |
|---|---|---|---|
| Draft → Submitted | Initiator; required identity and category fields present | Business reason, profile and required first-stage evidence | Duplicate check/task; configurable submission SLA starts; invalid fields block |
| Submitted → Duplicate Review | System; possible match found | Match candidates and reason | Steward task; no automatic merge; stale check can be rerun |
| Submitted/Duplicate Review → Due Diligence | Steward; duplicate resolved | Disposition and evidence checklist | Reviewer tasks; missing evidence returns for information |
| Due Diligence → Pending Approval | Steward; configured checks complete | Review outcomes, Finance verification reference if needed | Approver task and SLA; verification outage stays pending |
| Pending Approval → Active | Authorized approver; no hard block, no self-approval conflict | Decision, policy version and effective date | Orderable projection and `SupplierActivated`; retry idempotently |
| Any review state → More Information Required or Rejected | Reviewer/approver; reason mandatory | Reason and specific missing/failed item | Notify initiator; resubmission creates new version |
| Active → Change Pending → Active | Steward then approver; material change reverified | Field delta, prior version, evidence and decision | Publish new version; previous version retained |
| Active/Conditional → Suspended | Authorized owner; reason and effective time | Impact on open POs/invoices | Block new orders; notify buyer/Finance; operator reconciles failed event |
| Active/Conditional → Expired/Inactive | Scheduler or authorized owner under approved policy | Expiry evidence or deactivation reason | Block new orders, retain history |
| Suspended/Expired/Inactive → Due Diligence → Active | Steward and approver; fresh checks pass | New evidence and independent verification as required | No direct status flip to Active |

Conditional approval, blacklist, and appeal are policy decisions. If enabled, each needs a separate transition and exact purchase restrictions. SLA targets, business calendar, pause reasons, warning and escalation owner are configuration; escalation never approves automatically. Submitted, approved and rejected versions cannot be overwritten or erased by ordinary users.

## 10. Permissions and Data Scope

| Action | Permission example | Required record and field scope |
|---|---|---|
| Propose | `procurement.supplier.request.create` | Authenticated active worker; own tenant and permitted entity |
| Read basic profile | `procurement.supplier.read` | Assigned entity/category or other approved scope; exclude bank data and restricted diligence |
| Edit Draft/Change Pending | `procurement.supplier.edit` | Assigned supplier/tenant/entity; only allowed fields and status |
| Resolve duplicate | `procurement.supplier.duplicate.resolve` | Steward scope; decision reason required |
| Verify/approve | `procurement.supplier.verify` / `.approve` | Assigned task, effective authority, no prohibited creator/approver conflict |
| Suspend/reactivate | `procurement.supplier.suspend` / `.reactivate` | Explicit authority and affected entity scope |
| View remittance verification | Restricted Finance permission | Verification status/reference only for general Procurement roles; actual instructions in Finance-controlled channel |
| Audit/export | `procurement.supplier.audit.read` | Approved entity and field scope; export logged |

Role names are defaults; authorization combines permission, tenant, entity, assigned record, field class and state on the server. A future supplier portal role is separately supplier-scoped and never inherits an internal buyer role. Delegates cannot exceed the delegator's authority.

## 11. Business Rules

| ID | Candidate rule |
|---|---|
| BR-PROC-SUP-001 | A new PO may be issued only to an Active approved supplier, unless a separately authorized emergency exception applies. |
| BR-PROC-SUP-002 | Exact or likely duplicate identity requires steward disposition before activation; no automatic merge. |
| BR-PROC-SUP-003 | A material change to legal identity, tax profile, remittance reference, ownership or risk class starts a new version and relevant revalidation. |
| BR-PROC-SUP-004 | Supplier creator cannot be sole final activator under the approved segregation policy. |
| BR-PROC-SUP-005 | Finance verifies payment-instruction changes through an independent channel before new instructions become usable. |
| BR-PROC-SUP-006 | Suspension, expiry and deactivation stop new issue but preserve existing obligations and historic references. |
| BR-PROC-SUP-007 | Eligibility is checked again at PO issue, not only when the PO draft is created. |
| BR-PROC-SUP-008 | A completed supplier decision retains policy version, evidence version, actor and reason. |

Required identifiers, diligence criteria, country-specific checks and time limits are configured after business approval. No external screening result should automatically blacklist a supplier without a defined review path.

## 12. Data Fields and Ownership

| Field group | Key fields | Source and classification |
|---|---|---|
| Identity | Supplier ID, legal/trading name, registration and tax references, country, entity relationship | Procurement profile; tax/registration data restricted by policy |
| Sites and contacts | Address, service site, contact name/work channel, delivery and correspondence details | Procurement; personal contact data access controlled |
| Classification | Category, goods/service scope, local/international, manufacturer/distributor/contractor, preferred/strategic flags | Procurement; effective dated and reviewed |
| Qualification | Certificate/license type, issuer, number/reference, issue/expiry, evidence version, review result | Procurement evidence; configured by risk/category/country |
| Eligibility | Application status, orderable status, risk tier, hold/suspension reason, approver, effective time, policy version | Procurement system of record |
| Finance relationship | AP supplier account reference, payment-verification status/token and callback ID | Finance system of record; Procurement stores minimum reference |
| History | Profile version, changed fields, actor, source, decision, reason, evidence and correlation ID | Procurement audit/event record; shared audit service may retain immutable copy |

Every record is tenant scoped; legal-entity relationship and site scope are explicit. Store stable IDs and normalized values, not names as cross-module keys. Never copy full bank credentials into general Procurement tables, events, search or reports.

## 13. Screens and Data Views

Supplier request form; My supplier requests; steward intake/duplicate queue; diligence checklist; approval detail; active supplier search and selector; supplier profile with current version and history; qualification expiry queue; suspension impact list; Finance verification state; restricted audit/export view. List views show status, owner, next action, age and scope. Supplier selectors exclude non-orderable records unless a permitted exception route is shown explicitly.

## 14. User Experience and Human Behaviour

Explain why activation is blocked and what evidence is missing, without exposing confidential screening or bank details. Show possible duplicates before a requester invests effort in a new profile. Make Active, Conditional, Suspended and Expired visibly distinct. Before suspension, show linked open POs, receipts and invoices so an operator can arrange follow-up. A returned application should preserve inputs and name the reviewer request; a rejection should not appear as a transient technical error. Keyboard, mobile and accessible forms are required for internal users.

## 15. Notifications and Communications

On submission, notify assigned steward; on duplicate, verification or approval task, notify responsible reviewer; on return/rejection/activation, notify initiator; before qualification expiry, notify owner with configured lead time; on suspension, notify buyers with affected open transactions and Finance where payment relevance exists. In-app is the baseline; email/SMS/WhatsApp are configured channels, not defaults. External messages include only approved supplier-visible details. Every notification links to a scope-checked record; failed delivery is logged and retryable.

## 16. Documents and Attachments

Candidate evidence: registration, tax document, license/certification, ownership declaration, insurance, signed supplier form, remittance verification reference and correspondence. Each attachment records type, classification, owner, version, issue/expiry date if applicable, source, checksum, scan result and access scope. Approval snapshots refer to exact evidence versions. Originals and prior approved versions remain available to authorized auditors under retention/legal hold policy. An unscanned or failed document cannot satisfy a mandatory evidence gate.

## 17. Exceptions and Edge Cases

Same supplier under several trading names; one legal entity with multiple service sites; duplicate tax identifier with a documented legitimate reason; supplier name changes after PO issue; expired qualification with open order; suspended supplier with unpaid valid invoice; failed independent bank verification; Finance account mapping missing; supplier is foreign or lacks a local identifier; document authenticity disputed; conflicting concurrent profile changes; urgent emergency procurement. Each case has a named steward/Finance owner and a blocked or exception status, never a silent overwrite or fallback activation.

## 18. Integrations, APIs, Events and Sandbox

Procurement needs versioned identity/organization scope, Finance supplier-account and verification contracts, document storage/scanning, workflow tasks, notification and audit services. Proposed API resources are supplier request, profile/version, duplicate review, qualification, decision, suspension, and status; exact paths, HTTP schemas and authorization claims require technical design. Events after commit may include `SupplierProposed`, `SupplierActivated`, `SupplierChanged`, `SupplierSuspended` and `SupplierQualificationExpiring`. They carry tenant, entity, supplier ID/version, occurred time, policy version, correlation and idempotency keys, and minimal non-sensitive payload. No banking detail or confidential diligence result is broadcast. Consumers acknowledge or reconcile; replay is idempotent. Sandbox uses synthetic suppliers and verification tokens, including duplicate, late callback, suspension and outage cases, before production promotion.

## 19. Audit, Security and Privacy

Record proposal, field changes, duplicate disposition, evidence check, verification response, approval, suspension, privileged read/export and each status transition with actor, role, source, time, before/after, policy version and reason. Business history is append-only to normal users; a correction is a new version/event. Enforce tenant/entity/field scope server-side, encryption in transit and at rest, authenticated service callbacks, malware scanning, restricted Finance data, retention/legal hold and access monitoring. Consent or lawful processing basis for supplier contacts is a business/legal decision; this document does not prescribe one.

## 20. Reports, Dashboards and Metrics

Supplier applications by state/entity/category; median onboarding time from submitted to Active; duplicate-review backlog; qualification expiring within configured horizon; active/conditional/suspended counts; change-verification age; orders blocked for ineligible supplier; open obligations affected by suspension. Define numerator, denominator, time window, entity and report owner before use as a control KPI. Reports and exports obey field and record scope; no bank detail in broad analytics.

## 21. Configuration and Localization

Configure required profile fields and document types by country/category/risk; identifiers and validation formats; legal-entity sharing; duplicate matching; classification labels; review/expiry intervals; approval strategy and segregation; SLA business calendar; localized names, addresses, dates and currencies; notification templates and channels. Policies are versioned and effective dated. Zambia-specific requirements are configured only after local policy and legal review, not hardcoded from examples.

## 22. Non-Functional Requirements

Tenant and entity isolation applies to list, detail, search, attachment, API, event and export. Concurrent profile changes use version checks. Submission and callback retries are idempotent. Integration errors, stale verification and expiry jobs are observable with operator ownership. Documents are scanned before use. Availability, latency, storage, attachment size and recovery targets need pilot volume and service-level decisions; accessibility is required for all internal screens.

## 23. Postconditions

After approval, the supplier has a current immutable approved version, orderable status, required evidence and a traceable decision. After a material change, the prior approved version remains historical and the newly approved version becomes current only after required verification. After suspension/expiry, new PO issue is blocked while existing POs and invoices keep resolvable supplier references and assigned follow-up.

## 24. Failure and Recovery Behaviour

Invalid submission leaves Draft with field errors, not a partial application. Failed Finance verification leaves Pending; retry uses stable request key and reconciles unknown outcome before resending. Duplicate approval command returns the prior result. Failed activation event is queued with visible delivery status and replay; the local supplier decision is not reissued. If PO issue races with suspension, recheck eligibility at issue transaction and deny or compensate according to the adopted cross-module contract. Operator intervention records reason and does not edit audit rows directly.

## 25. Acceptance Criteria

1. **Given** a valid internal supplier request, **when** an active permitted user submits it, **then** one immutable application version and steward task exist, even after retry.
2. **Given** an exact or likely duplicate, **when** activation is attempted, **then** it is blocked until a steward records a disposition with evidence.
3. **Given** required evidence or Finance verification is missing, **when** an approver tries to activate, **then** the server denies the transition and names the missing gate.
4. **Given** a creator/approver conflict, **when** the creator tries final activation, **then** the server denies it under the configured segregation policy.
5. **Given** a material supplier or remittance change, **when** review is pending, **then** the prior approved version remains authoritative and the unverified detail is not orderable.
6. **Given** a suspended or expired supplier, **when** a buyer tries to issue a new PO, **then** issue is blocked while historical transactions remain readable.
7. **Given** another tenant, entity or supplier scope, **when** a user searches, opens, exports or fetches evidence, **then** unauthorized data and fields are absent or denied.
8. **Given** duplicated or delayed Finance callbacks, **when** they are processed, **then** one verified state is applied, conflicts are quarantined, and the audit identifies the source.

## 26. Test Scenarios

Positive: create, return, resubmit and activate; change non-material and material fields; suspend and reactivate after new review. Negative/boundary: missing ID/document, duplicate legal number with same/different name, certificate expiry boundary, entity sharing, self-approval, unauthorized bank field, foreign supplier without local ID, activation on Finance outage. Concurrency/recovery: two stewards edit same version, duplicate submit/approve, late verification callback, event replay, PO issue racing suspension. Security: tenant/entity/field and attachment isolation, export logging, supplier-portal identity denied internal API. Load/operational: supplier import/search volume and notification queue targets once pilot figures are known.

## 27. Ownership and Dependencies

Business owner: supplier management lead. Procurement data steward owns profile quality and duplicate disposition. Finance data owner owns AP account mapping and remittance verification. Compliance owner sets applicable diligence criteria. Platform owners provide identity, workflow, documents, audit and notification contracts. Engineering owner implements the adopted Procurement host and migration path. Dependencies: `PROC-CFG-01` policy, shared organization/workforce reference, Finance verification interface, document service and PO supplier-eligibility check. Named people and service-level owners must be assigned before implementation.

## 28. Open Decisions and Assumptions

| Decision | Proposed decision owner | Needed before |
|---|---|---|
| Launch entities/countries, shared supplier across entities, mandatory IDs | Procurement and organization data owners | Schema/API contract |
| Diligence tiers, required documents, conditional/blacklist statuses and review periods | Procurement, compliance and legal | State and policy approval |
| Supplier creator/approver separation and emergency exception authority | Procurement governance owner | Workflow implementation |
| Banking data owner, secure channel, independent verification and Finance callback | Finance and security owners | Activation integration |
| Duplicate matching and human disposition policy | Supplier data steward | Search and validation |
| Tax and sanctions services, if any, and manual fallback | Compliance and Finance owners | Verification design |
| SLA targets, business calendar, reminders and retention | Procurement operations and records owners | Configuration and go-live |
| Procurement runtime and shared platform service availability | ERP technical owner | Implementation plan |

Until these are decided, this document is a complete **draft structure** rather than an approved build contract. The related [PROC-02 Purchase Requisition and Line Routing](PROC-02-PURCHASE-REQUISITION-AND-LINE-ROUTING.md) draft defines how approved demand can use an orderable supplier.
