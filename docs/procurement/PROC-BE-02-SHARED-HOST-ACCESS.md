# PROC-BE-02 — Shared ERP host and Procurement access scope

**Document type:** Technical child feature and implementation record

**Status:** Shared-host composition and scope resolution implemented in source; initial named action policies added for draft CRUD; production deployment and tenant-configurable permissions remain open

**Prepared:** 4 October 2026

**Hierarchy:** ERP → Procurement → Platform and Configuration → Shared Host Access → Resolve Procurement Scope

## 1. Feature Identification

`PROC-BE-02` is P0.5 of [PROC-BE-00](PROC-BE-00-BACKEND-FOUNDATION-MILESTONES.md). It composes Procurement in the existing .NET ERP host while retaining an independent `procurement` schema and migration history.

## 2. Purpose and Business Outcome

An authenticated employee can enter Procurement under the same login and company/branch switcher as HRMS. Procurement persistence receives a verified tenant, legal entity and actor rather than trusting browser fields.

## 3. Scope and Exclusions

In scope: conditional module registration, existing HRMS local/OIDC authentication, shared-host metadata route, strict header validation, branch confinement, database readiness and tests. Initial named action policies and active-worker checks for writes are recorded in [PROC-BE-03](PROC-BE-03-FIRST-CRUD-OPERATIONS.md). Excluded here: live rollout, supplier portal identity, tenant-configurable permission grants and approved delegated-authority rules.

## 4. Parent and Related Features

Parent: Procurement Foundation. Related: HRMS identity/organization shell, `PROC-01` Vendor CRUD, `PROC-02` requests, shared workflow and Finance. This host reads HRMS organization data to resolve a shared scope; Procurement domain code accesses it only through `IProcurementScope` and does not query `hrm` tables.

## 5. Actors and Participants

ERP user, HRMS identity service, organization scope resolver, Procurement route, Procurement database and operator. Supplier users require a separate portal access contract later.

## 6. Entry Points and Triggers

`/api/procurement/v1/meta` is registered only when `ConnectionStrings:Procurement` is configured. The existing `hrm_session` cookie or configured OIDC bearer token authenticates the user. The browser may send `X-Shell-Entity` and `X-Shell-Location` as selections, not authority.

## 7. Preconditions

An authenticated subject with a tenant claim and permitted workforce role exists; the subject maps to a GUID workforce user; the selected legal entity exists under the tenant; a confined user's branch assignment is valid. The Procurement database has been migrated by its dedicated migration command.

## 8. Workflow

Authenticate → check `procurement-access` admission policy → parse headers → resolve tenant legal entities and branch assignments → reject invalid or unauthorized selection → set immutable request scope → execute Procurement handler. If no header is supplied, a confined user defaults to an assigned branch; an unconfined user defaults to the tenant's primary legal entity.

## 9. Status / State Transitions

Request scope is `Unresolved → Resolved` once per request. Invalid, mismatched or forbidden selection ends in a 400/403 response. No business record changes state in this milestone.

## 10. Permissions and Data Scope

`procurement-access` admits the configured employee/manager/procurement roles for the metadata route. A valid login alone does not authorize business mutations. Scope includes tenant, legal entity, work location/org unit if selected, confinement flag, subject and correlation ID. A confined user's selected work location must be one of their assignments; selected company must match that branch. A non-GUID OIDC subject is denied until it has a workforce mapping. Vendor CRUD will require separate create/read/update/approve permissions and record/field scope.

## 11. Business Rules

Never accept tenant from a header. Never resolve a company from another tenant. An invalid branch or company header is rejected rather than ignored. Company and branch must agree. A confined account cannot use an unassigned branch. Ordinary HRMS startup does not run Procurement migrations.

## 12. Data Fields and Ownership

Claims: subject and tenant from identity. Selection: entity/location headers from ERP shell. Resolved scope: tenant ID, legal entity ID, work location ID or org unit ID, confinement and correlation. HRMS/Organization owns users, legal entities, work locations and assignments; Procurement owns its business records and schema.

## 13. Screens and Data Views

The existing ERP shell supplies company and branch selectors. The metadata response echoes the resolved scope for developer/UI verification. After the draft CRUD routes were added in source, it reports `crudEnabled=true`; this reports host capability, not production deployment status.

## 14. User Experience / Human Behaviour

Invalid selections return plain messages such as “Choose a valid company” or “You cannot use that branch.” The UI should refresh its selector if an assignment changes. Switching company must not silently retain a branch from another company.

## 15. Notifications and Communications

No end-user notification is sent. Scope denials and database readiness failures should be visible in operational logs/monitoring without disclosing another company's records.

## 16. Documents and Attachments

None in this milestone. Future document reads must use the same resolved Procurement scope and field permissions.

## 17. Exceptions and Edge Cases

Missing tenant, unknown company, malformed GUID, stale branch assignment, mismatched company/branch, non-GUID subject and unavailable Procurement database fail closed. The Development-only synthetic identity may use the local development environment; it is not a production exception.

## 18. Integrations / APIs / Events / Sandbox

The HRMS API project references Procurement's Application and Infrastructure projects. It registers `ProcurementDbContext` with its separate connection and migration history. No cross-schema foreign key or event is introduced. Sandbox tests use synthetic tenant, company, branch and user assignments.

## 19. Audit / Security / Privacy

The existing ERP authentication and request ID pipeline applies. Scope selection is validated server-side. Future mutations still require Procurement audit and action-specific authorization. Employee or payroll private fields are not copied into the Procurement scope.

## 20. Reports / Dashboards / Metrics

No business metric. Operations can monitor Procurement database readiness, 400 invalid-scope and 403 forbidden responses by route and correlation ID.

## 21. Configuration and Localization

`ConnectionStrings:Procurement` opts the host into the module. Authentication mode remains the ERP host setting. Role-to-permission mapping, displayed company labels and localized error messages are future configuration tasks.

## 22. Non-Functional Requirements

One production process can serve both HRMS and Procurement routes. Database schemas and migrations remain independently owned. Scope resolution is request-scoped and asynchronous. Startup without Procurement configuration preserves existing HRMS operation.

## 23. Postconditions

An authorized metadata request reports a verified tenant, entity and optional branch. Procurement `DbContext` receives the same scope for later CRUD handlers. No live schema or business row is changed by this integration.

## 24. Failure and Recovery

Invalid selection returns 400/403; database outage makes readiness unhealthy. An operator can remove the optional Procurement connection to restore HRMS-only mode. Apply/retry Procurement migrations through the separate migration job, not HRMS startup.

## 25. Acceptance Criteria

The HRMS API builds with the module reference; a confined user cannot select an unassigned branch or another company; default scope is an assigned branch; mismatched headers are denied; Procurement and HRMS projects retain separate migrations. Draft CRUD routes are separately recorded in [PROC-BE-03](PROC-BE-03-FIRST-CRUD-OPERATIONS.md).

## 26. Test Scenarios

Automated middleware tests cover branch/company denial, assigned-branch default and mismatched headers. Procurement's five persistence tests still pass. Before production rollout, run an end-to-end local-session and OIDC check with the actual organization switcher, plus denied role, stale assignment and database outage cases.

## 27. Ownership / Dependencies

ERP host/Identity owns authentication and role claims; Organization/HRMS owns legal entities and branch assignments; Procurement owns its scope contract, route authorization and database. Deployment owns the optional connection, migration ordering and least-privilege credentials.

## 28. Open Decisions / Assumptions

Decide tenant-configurable Procurement grants and record scopes; how OIDC subjects map to workforce GUIDs if they are not GUIDs; how org-unit confinement maps to work-location assignments; and the production connection/credential deployment. Draft vendor routes have initial action and data-scope checks; activation remains blocked.
