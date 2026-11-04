# PROC-PR-02 — Core approver bindings and requester routes

Status: implemented in source; issue192 M2. Version1. Release and company UAT remain separate gates. No live activation.

## 1. Feature Identification
Procurement → Purchase Requests → Approval Routing → Select ordered Core approvers. Core owns accounts, roles, scope and cases; Procurement owns buying policy.
## 2. Purpose & Business Outcome
An employee or system user can request purchases. Policy selects supervisor then executive, or executive-only for an authorised supervisor requester, without deriving access from employment or job titles.
## 3. Scope & Exclusions
Add PR rule schema2 alongside unchanged schema1. Schema2 filters the existing permission/finite-authority assignee strategy by one Core principal or active Core role. No replacement engine, generic executable rules, HR creation, bank execution or automatic approval.
## 4. Parent & Related Features
CORE-WF-01 directory; existing policy draft/review/publication, principal approval execution, authority grants and payment review. Department leadership remains separate governance data, not an implicit grant.
## 5. Actors & Participants
Policy editor/reviewer, Core account requester, named or role-bound approver, buyer, auditor. No employee requirement for Core bindings.
## 6. Entry Points & Triggers
Existing Buying Rules editor reached from Core directory; existing submit, evaluation, readiness, case opening and decisions.
## 7. Preconditions
Active scoped Core identities, versioned published policy, approved finite authority, current module permissions and required Finance funding evidence. Nothing here manufactures those conditions.
## 8. Workflow
Choose rule conditions, including requester Core roles, company departments and branches; configure ordered stages with explicit user or role filters; validate, independently review and publish through existing controls. Submission captures server-proven active requester roles and pins decisions atomically with the submitted version. Opening/deciding uses existing Core engine and rechecks current selected approvers.
## 9. Status / State Transitions
Existing draft→review→published policy and submitted→ordered approval case lifecycles stay intact. A higher-priority supervisor-requester rule may contain executive-only steps; an unrestricted fallback retains supervisor/executive. No implicit self-stage skipping.
## 10. Permissions & Data Scope
Existing policy permissions and company scope; active Core role assignments; current approval action, branch reach and finite authority. No department permission is inferred from an HR manager pointer. Wrong-company/inactive/locked principals cannot act.
## 11. Business Rules
Schema1 serialization/behaviour remains unchanged. Schema2 is PR-only, requires explicit binding on every stage, rejects unsupported BudgetOwner and HR-only strategies. Binding narrows the existing eligibility strategy rather than changing stored assignee kind. One with multiple eligible members is ambiguous; do not silently pick or use All to simulate one authorised executive. Multiple requester roles may coexist, so distinct role filters can overlap and must not bypass equal-priority ambiguity checks.
## 12. Data Fields & Ownership
Policy JSON2: when.requesterRoleKeys, optional when.departmentIds and existing when.branchIds; stage.binding {kind: CoreUser|CoreRole, key: canonical principal ID|Core role key}. Departments are referenced from existing Core organisation units, not copied into Procurement. Existing assignee=ProcurementManager denotes permission/financial-authority eligibility, not a job title. Pinned decision2 contains binding plus server-captured requester identity/roles; existing immutable policy/evaluation tables own storage. No relational columns change.
## 13. Screens & Data Views
Preserve existing Buying Rules table/form; add ordered Core-user/role stage controls and requester-role matching. Legacy policies must round-trip without invented defaults or dropped fields. Core directory links to the same editor.
## 14. User Experience / Human Behaviour
Use simple labels: Who approves, Person, Role, Add step, Move up/down. Show unavailable/ambiguous assignments; validation never claims authority. Preserve draft values on failures.
## 15. Notifications & Communications
Existing Core task notifications and procurement audit/events; configuration preview sends none.
## 16. Documents & Attachments
Existing policy supporting evidence and submission documents; no new upload mechanism.
## 17. Exceptions & Edge Cases
Inactive/changed approver, removed role, multiple role members, self/known-person aliases, empty role, changed branch, mixed-line routes, absent policy and role snapshot. Missing schema2 snapshot on a historical submission requires controlled return/resubmit; never reconstruct historic roles from today's account.
## 18. Integrations / APIs / Events / Sandbox
Existing versioned policy endpoints and principal execution. Preview role inputs are samples only; command submission takes roles only from the owning Core request proof, never client headers/body. Existing Core cases/events remain authoritative.
## 19. Audit / Security / Privacy
Retain policy version, rule result, ordered binding and requester role snapshot with submitted version. Current approver roles/permissions/authority revalidated on decision. Submission timestamps use the existing PostgreSQL microsecond-normalisation helper. Never copy payroll/personal identifiers. Keep negative authorization tests and independent financial release review.
## 20. Reports / Dashboards / Metrics
Existing approval history identifies each stage and actor; no new unsupported savings/payment metrics.
## 21. Configuration & Localization
Company currency/reference data unchanged; Core identifiers stable, labels localizable. No thresholds or real approver identities seeded.
## 22. Non-Functional Requirements
Bounded JSON≤64KB, rules≤100, steps≤20, safe exact identifiers and deterministic decisions. Accessible existing shared selects/buttons.
## 23. Postconditions
A matched schema2 decision pins explicit route and requester role evidence, without approving or committing spend. Current Core cases preserve maker/checker controls.
## 24. Failure & Recovery
Reject malformed/ambiguous bindings; atomic existing persistence/CAS and retry controls apply. Re-evaluation returns retained decision for the same submitted version. No fallback to unfiltered eligible pools when a binding is missing or invalid.
## 25. Acceptance Criteria
Named user and Core role routes execute without HR links; ordinary requester retains supervisor/executive; supervisor requester selects authorised executive-only rule; self-approval, wrong-scope, revoked role/authority and ambiguous role membership block. Old policy/cases unchanged; original UI round-trips each supported schema.
## 26. Test Scenarios
Schema1 regression/byte compatibility; schema2 syntax/bounds; role-condition matching/overlap; binding round-trip and mixed lines; server-only snapshot and late-evaluation denial; current membership/permission/authority; UI selection and denied/recovery states; real-cookie/native company UAT separately.
## 27. Ownership / Dependencies
Core identity/active roles/shared approval engine; Procurement versioned rule and finite-authority adapters; existing original UI. No new distributed service or duplicate account/directory table.
## 28. Open Decisions / Assumptions
Actual supervisor/executive/Accounts identities and limits require business confirmation before activation. Organisation-master department-head governance projection and general PO advances remain separately tracked; per-department transaction routing is supported by explicit configured rules, while explicit person/role routes do not claim to implement those features. A schema2 source pass alone is not company UAT or release evidence.
