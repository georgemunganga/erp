# PROC-05 Procurement Planning

**Status:** Draft for business review · **Version:** 0.1 · **Hierarchy:** Procurement → Procurement Planning → Annual and Period Procurement Plan → Operations · **Framework:** [ERP 28-section standard](../hrm/feature-specifications/ERP_Feature_Specification_Framework_Enterprise_Integration_Updated.docx) · **Module:** [PROC-00](PROC-00-PROCUREMENT-MODULE-ARCHITECTURE-AND-CAPABILITY-MAP.md)

**Implementation note, 18 October 2026:** Own-plan Draft list/detail/create/revise/cancel routes are implemented in source at `/api/procurement/v1/plans`; the additive P8 migration was applied to disposable PostgreSQL. They create forecasts only. Submission, review, approval, activation, utilization and Finance budget confirmation remain blocked until the decisions below and their integration contracts are approved. The existing planning UI still uses demonstration records.

## 1. Feature Identification

`PROC-05`, advanced phase. Business owner: Procurement planning lead; Finance funding owner; product/data/technical owners to name. Operations: `O1` create plan, `O2` consolidate/review, `O3` approve/activate, `O4` revise, `O5` link PR/track utilization, `O6` close period.

## 2. Purpose and Business Outcome

Departments forecast purchases so Procurement can schedule sourcing and Finance can see expected demand. A plan is a forecast and governance record, not an automatic budget reservation or permission to issue a PO.

## 3. Scope and Exclusions

Includes financial-year/period plans, departmental requirements, expected amount/date/method/funding source, review, revision, PR linkage and utilization. Finance owns budgets and project funding. Demand prediction, automated supplier sourcing and capital authorization are separate.

## 4. Parent and Related Features

Parent Procurement Planning; uses organization/cost-centre/project refs, Finance budget periods, PROC-04 catalog/category and PROC-02 PR. Sourcing uses approved future demand; reports compare plan and actual.

## 5. Actors and Participants

Department planner, department head, Procurement planning reviewer, Finance budget owner, project owner, requester, auditor and scheduled reminder service.

## 6. Entry Points and Triggers

Annual planning window, new entity/project, budget cycle, approved plan change, PR referencing a plan line, period-end review or contract expiry forecast.

## 7. Preconditions

Valid entity, financial period, department, funding reference, currency and plan policy; planner has scope. Finance budget existence is checked separately and never inferred from plan approval.

## 8. Workflow

`O1`: department drafts requirements with quantity, category, date, estimate, method and source. `O2`: Procurement consolidates similar demand, returns gaps and records Finance funding review. `O3`: assigned approvers activate version. `O4`: material change creates revision with delta and reapproval. `O5`: PR links exact plan line/version and actual order/receipt progress derives utilization. `O6`: owner closes period and carries forward only through a new plan/version.

## 9. Status and State Transitions

Draft → Submitted → Reviewed → Approved → Active → Revised → Closed; Returned/Rejected/Cancelled as alternate paths. Actor, evidence, version, effective period, SLA and escalation are explicit. Revised plan preserves original; period close does not cancel existing PRs.

## 10. Permissions and Data Scope

`procurement.plan.edit` for assigned department, `.review` for Procurement, `.approve` for authorized head/Finance where configured, `.read` by scoped entity/project. Planner cannot approve own plan where segregation applies; sensitive funding lines have field scope.

## 11. Business Rules

`BR-PROC-PLN-001` active plan has approved period/entity scope; `002` plan approval is not budget reservation; `003` material revisions retain prior version and reason; `004` linked PR cannot consume more planned quantity/value than policy permits without exception; `005` actual and commitment metrics come from source modules.

## 12. Data Fields and Ownership

Procurement owns plan ID/version, year/period, entity/department, requirement lines, category, quantity/UOM, expected date/method, estimate/currency, funding/project refs, approval and utilization links. Finance owns budget and funding truth; HRMS/Organization owns department; PR/PO own transaction facts.

## 13. Screens and Data Views

Department plan editor, consolidated calendar, review/approval queue, version comparison, linked PR/PO utilization, period close and variance views. Filters respect entity/department scope.

## 14. User Experience and Human Behaviour

Explain that estimates may change and that plan approval does not authorize buying. Show duplicate-looking requirements for consolidation without forcing unrelated departments to share budget. Highlight unfunded and overdue planned needs.

## 15. Notifications and Communications

Planning window opens, submission/review due, return/approval, forecast date approaching, material variance and period close. Notify assigned owner through permitted channels and links.

## 16. Documents and Attachments

Business case, funding letter, forecast sheet and plan approval evidence are scanned, versioned, scoped and retained. PR links the relevant plan evidence rather than copying uncontrolled files.

## 17. Exceptions and Edge Cases

Financial-year change, multi-year contract, cross-department purchase, partial funding, project cancellation, currency estimate change, late PR outside plan, duplicate plan lines and period close with open transactions.

## 18. Integrations, APIs, Events and Sandbox

Consume Finance period/budget and Organization/Project refs; publish plan approved/revised/closed facts after commit. PR link contract carries plan line/version and tenant/entity; downstream actuals are idempotent projections. Sandbox tests synthetic budget period changes and late actuals.

## 19. Audit, Security and Privacy

Append line changes, consolidation, approval, revision and close with actor/reason; restrict funding and project fields. No rewrite of published plan version; exports logged.

## 20. Reports, Dashboards and Metrics

Forecast by category/month/entity, plan-to-PR conversion, planned versus ordered/actual, late demand, funding gaps and sourcing pipeline. Define currency conversion, as-of time and denominator.

## 21. Configuration and Localization

Financial calendars, planning windows, required line fields, materiality, approval path, carry-forward rules, currency and local date are effective-dated by entity; Finance remains budget owner.

## 22. Non-Functional Requirements

Bulk plan entry/import is validated and idempotent if enabled; consolidation and reporting scale to planned volume. Accessible spreadsheet-like entry and version comparison are required. Targets await estimates.

## 23. Postconditions

Active plan version has approved lines and funding status reference; linked PRs show source plan version; closure preserves open transaction history and variance.

## 24. Failure and Recovery

Finance reference outage leaves funding Unverified; plan may remain reviewable but cannot claim confirmed budget. Failed actual sync retries by source ID/version; inconsistent utilization enters reconciliation queue.

## 25. Acceptance Criteria

Given valid department plan, review and approval create immutable Active version. Given material revision, prior version and delta remain visible. Given linked PR, plan utilization updates without creating a Finance reservation. Given unauthorized department, view/edit is denied.

## 26. Test Scenarios

Test annual submission, consolidation, return/revision, cross-entity access, multi-currency estimate, period rollover, duplicate actual event, project cancellation, budget outage and plan close with open PR.

## 27. Ownership and Dependencies

Procurement planning owner governs forecast; Finance owns budget/period; departments own demand; Organization/Project owners supply refs. Depends on PROC-CFG-01 and PROC-02 integration; sourcing/reporting consume plan.

## 28. Open Decisions and Assumptions

Confirm plan horizon, mandatory categories, consolidation authority, relation to Finance budget, plan-line overrun policy, multi-year handling, import needs and launch timing. Owners: Procurement planning, Finance and departments; resolve before build.
