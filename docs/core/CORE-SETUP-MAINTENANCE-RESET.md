# CORE-SETUP — Safe Web Reset Admission

**Status:** Source change authored; tests and runtime verification pending. Controlled maintenance reset is not implemented. This child specification follows [GW-ENG-STD-001](../engineering/GW-ENG-STD-001.md).

## 1. Feature Identification

Core/HRM setup web reset admission, version 1. The retained `POST /api/hrm/setup/reset` and versioned HRM equivalent refuse ordinary application reset.

## 2. Purpose and Business Outcome

Prevent an administrator's web request or an internal service caller from globally deleting tenant, payroll, audit and history records.

## 3. Scope and Exclusions

This change is a fail-closed service guard and structured API error. It does not implement a maintenance reset, tenant deletion, recovery tool, schema migration or new role.

## 4. Parent and Related Features

The first-time setup wizard and Core identity remain available. The broader ERP data-retention, tenant-isolation and audit contracts govern any future maintenance design.

## 5. Actors and Participants

An authenticated `hr_admin` may use ordinary setup steps. No web principal, including an unconfined administrator, is authorized to wipe the database.

## 6. Entry Points and Triggers

The existing reset endpoint still parses the explicit `RESET` confirmation and calls `ISetupService.ResetAsync`. Any other caller of `ResetAsync` is also refused.

## 7. Preconditions

Existing route authentication, role, branch-confinement and confirmation checks remain in order. Passing them is not maintenance authorization.

## 8. Workflow

The service throws `DomainException("setup-maintenance-required", …)` before `ISetupRepository` is called. The application error handler maps it to HTTP 503. No wipe, seed or setup-state transition runs.

## 9. Status and State Transitions

No data state changes. Pending setup stays Pending; completed setup stays Complete; historical audit and data rows remain retained.

## 10. Permissions and Data Scope

`hr_admin` plus unconfined scope and exact confirmation remain necessary for reaching the service. They are insufficient for reset. No tenant-wide permission is interpreted as authority to affect another tenant.

## 11. Business Rules

An ordinary web reset always fails. The refusal must precede every repository read or write in the reset path. A future controlled reset requires a separate approved design with explicit target, retention, backup, authorization and audit rules.

## 12. Data Fields and Ownership

No new fields. Existing `SetupState`, `SetupStepRecord`, tenant data and audit remain under their existing owners. The global `WipeAllDataAsync` implementation remains unexposed by this service.

## 13. Screens and Data Views

Retain the existing setup wizard layout and route. The UI receives a structured maintenance-required error on a confirmed reset; it must not represent this as successful restart.

## 14. User Experience and Human Behaviour

The error directs the user to an authorized maintenance operator. The user may continue safe setup viewing/editing according to existing permissions; no promise of an available maintenance workflow is made.

## 15. Notifications and Communications

No reset notification is emitted. Operational escalation is outside the current application contract.

## 16. Documents and Attachments

Existing documents and attachment references remain unchanged. No archive, export or backup is silently created.

## 17. Exceptions and Edge Cases

Anonymous/non-admin and confined admin retain their existing denials; missing or wrong-shaped confirmation returns HTTP 400. Correctly confirmed unconfined admin receives maintenance-required. Repeated calls have no data effect.

## 18. Integrations, APIs, Events and Sandbox

The existing POST route returns structured code `setup-maintenance-required` and HTTP 503 after its normal gates. Non-object JSON or a non-string/missing confirmation returns `reset-not-confirmed` and HTTP 400. No repository call, event, outbox message, external reset or command-line operation is part of this feature.

## 19. Audit, Security and Privacy

Audit/history rows stay intact. The ordinary endpoint neither enumerates tenants nor accepts a target tenant. Future maintenance must preserve evidence and use a separate reviewed operator boundary.

## 20. Reports, Dashboards and Metrics

No reporting data changes. A maintenance-required response can be counted as an operational refusal; it is never a completed-reset metric.

## 21. Configuration and Localization

No flag can enable application reset. The error text may be localized without changing its code or fail-closed behavior.

## 22. Non-Functional Requirements

The refusal is deterministic, independent of database availability, and occurs before any reset persistence operation. It must not depend on the state or contents of other tenants.

## 23. Postconditions

The original setup status, completed-step records, HR data and audit history are unchanged. No pending state is seeded.

## 24. Failure and Recovery Behaviour

The client can show the maintenance-required message. Retrying the same request remains denied. Existing setup completion and safe step-edit semantics are unaffected.

## 25. Acceptance Criteria

A confirmed unconfined administrator receives 503/code `setup-maintenance-required`; an internal direct service call receives the same DomainException. Both cause zero repository calls. Finished setup and an audit row survive; existing role/scope/confirmation denials remain.

## 26. Test Scenarios

Run mapped service no-repository-call tests, SQLite completed-setup/audit preservation, and TestServer confirmed admin and ordinary denials using the same domain-code/status resolver as production. The isolated TestServer does not boot `Program.cs`; a separate private real-cookie/database no-effects check remains required before deployment. Source-authored tests are not runtime acceptance until executed.

## 27. Ownership and Dependencies

Core/HRM application owns the guard; API owns error mapping; QA owns no-effects evidence. Operations/security/data owners must design any separate maintenance procedure.

## 28. Open Decisions and Assumptions

Controlled maintenance reset is deferred and unimplemented. Its scope, backups, retention/legal-hold protection, tenant selection, approval, audit and restoration procedure require explicit owner approval before any implementation. No existing web permission grants it.
