# PROC-07A — Recurring Purchase Plans

**Status:** Draft for review · **Version:** 0.1 · **Hierarchy:** Procurement → Contracts and recurring commitments → Recurring purchase plan → Create, edit, view, close · **Parent:** [PROC-07](PROC-07-CONTRACTS-FRAMEWORKS-AND-RECURRING-COMMITMENTS.md)

## 1. Feature Identification

`PROC-07A`, bounded draft milestone. Procurement owns a repeat-purchase schedule; Finance owns recurring payables and payments.

## 2. Purpose and Business Outcome

Record who expects to buy from which supplier, how often, when, and for what estimated amount. This improves visibility without booking a liability.

## 3. Scope and Exclusions

Create, list, read, edit and close own schedule. No automatic invoice, PO, approval, reservation, accrual, payment, reminder or contract drawdown.

## 4. Parent and Related Features

Related to supplier records and optionally an existing same-supplier contract. It does not change either source record.

## 5. Actors and Participants

Authenticated buyer/manager with a resolved active ERP worker may create and edit; own-record readers may view; auditors use shared audit access.

## 6. Entry Points and Triggers

Manual create from the recurring purchases list; later manual edit or close. A due date alone never starts a financial transaction.

## 7. Preconditions

Valid tenant, selected legal entity, authorized branch, action permission, mapped worker, eligible supplier, and optional own contract for that supplier. A department-only context has no recurring schedule scope in this milestone.

## 8. Workflow

Choose supplier → enter title, frequency, first/next/optional last cycle, expected exact amount and currency → save draft. Open it to revise or close with reason. Each write returns authoritative saved data.

## 9. Status and State Transitions

Create → `Draft`; edit retains `Draft`; close → `Closed`. A closed plan cannot be changed or reopened by this API. Each write uses the current version.

## 10. Permissions and Data Scope

Actions: `procurement-recurring-read/create/edit`. Reads and writes are restricted to the authenticated subject's own worker-owned records in the selected tenant/company and branch. A confined account without a resolved branch gets no records and cannot create. The server derives owner worker/subject and branch; the client cannot assign them.

## 11. Business Rules

Frequency is Weekly, Monthly, Quarterly or Yearly. First cycle ≤ next cycle ≤ optional last cycle. Amount is positive `numeric(18,4)` and cannot be rounded from a browser number. Supplier must be own draft or approved active and orderable. Optional contract must be the owner's non-cancelled record for the same supplier. Once any recurring plan references a contract, including a closed plan, the contract draft cannot change supplier. Link does not authorize contract purchasing.

## 12. Data Fields and Ownership

Procurement owns ID, number, title, supplier/contract references, owner worker/subject, work location, frequency, first/next/last cycle dates, expected amount/currency, status, closure reason/date, timestamps and version. HRMS/Core own employee and organization; Supplier/Contract records remain separately owned by Procurement; Finance owns budgets/AP/payment.

## 13. Screens and Data Views

Recurring purchases list with pagination and real total; create/edit form; read detail with owner, supplier, contract, schedule and closure history. Link text says “Recurring purchases” even though the historical route is `/recurring-bills`.

## 14. User Experience and Human Behaviour

Plain labels describe expected purchases. The screen explicitly says it creates no bill or payment. Show a true empty state only after a successful empty response. Keep entered values when a save conflicts or its result is uncertain.

## 15. Notifications and Communications

No automatic notifications in this bounded milestone. Later reminders need policy, recipients and idempotency before implementation.

## 16. Documents and Attachments

No attachments in this milestone. Link relevant evidence through the separately governed document service when defined.

## 17. Exceptions and Edge Cases

Unavailable supplier, mismatched contract, attempted supplier reparenting of a referenced contract, invalid or missing draft, date or precision, closed record, stale version, lost network acknowledgement, worker rebinding or offboarding, company/branch switch, and cross-owner URL access all fail closed.

## 18. Integrations, APIs, Events and Sandbox

`GET/POST /api/procurement/v1/recurring-purchases`, `GET/PUT /{id}`, `POST /{id}/close`. Consume Core scope/currency and Procurement supplier/contract references. No financial business event or AP command is emitted. Recurring create/update and contract draft update use serializable relational transactions for the supplier/reference invariant; a PostgreSQL serialization conflict returns 409. Mapped HTTP tests cover the sequential contract reparenting failure. PostgreSQL migration, relational concurrency and constraint tests remain a P23 gate after P22.

## 19. Audit, Security and Privacy

Shared immutable audit captures create, edit and close with actor, before/after snapshot and correlation. Entity/branch/owner filters apply before read/write. No physical delete.

## 20. Reports, Dashboards and Metrics

List count is authoritative total from scoped API. Forecast totals or next-due indicators need a defined aggregation and currency conversion rule; no fake spend metrics.

## 21. Configuration and Localization

Default currency comes from selected Core company at creation, then persists on the plan. Dates are ISO calendar dates; client display may localize separately. Frequency choices are currently fixed, not policy approval thresholds.

## 22. Non-Functional Requirements

Use exact amount strings, paginated reads, current-version CAS, scoped headers, abortable reads, error visibility and reconciliation after uncertain writes. No background scheduler is required.

## 23. Postconditions

Successful creation returns one saved draft. Edit increments version. Close preserves the record and closure reason; no invoice/PO/payment record is created.

## 24. Failure and Recovery

Failed GET never creates a replacement. A 409 or unknown write result blocks repeat submission; user reviews the saved record/list through GET before a new action. Scope changes discard old read results and prevent late write navigation.

## 25. Acceptance Criteria

An authorized mapped worker can create, edit and close an own schedule with exact amount and audit. A reader cannot write. Another owner/branch/entity cannot read or change it. Stale writes fail. The operation creates no payable or payment.

## 26. Test Scenarios

Mapped create/edit/close, other-owner 404, missing action 403, stale 409, amount/date validation, exact large monetary value, failed detail read, uncertain create, branch switch, and no Finance side effect. PostgreSQL relational constraints still require P23 verification.

## 27. Ownership and Dependencies

Procurement product owns this record and API; Core owns worker resolution, company/branch scope and currency source; Finance owns actual recurring bills. P23 migration follows P22 schema work.

## 28. Open Decisions and Assumptions

Decide whether future schedules need approval, planned versus active states, reminders, employee reassignment, contract coverage proof, budget forecasting and Finance recurring payable hand-off. None is implied by this draft CRUD milestone.
