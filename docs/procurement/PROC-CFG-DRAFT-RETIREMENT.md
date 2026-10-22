# PROC-CFG — remove unused reference drafts

Status: implemented source; verification in progress. 22 October 2026. GW-ENG-STD-001 Tier 3.

## 1. Feature Identification

Procurement → Reference settings → Categories / Buying methods → Remove unused draft. Release-base extension to existing reference CRUD.

## 2. Purpose & Business Outcome

Users can remove unwanted drafts while retaining their identifiers and audit history.

## 3. Scope & Exclusions

Draft retirement only. Active settings need a separately controlled change; no hard deletion or inferred approval.

## 4. Parent & Related Features

PROC-CFG-01 configuration; existing category/method create, edit, activate and list/detail.

## 5. Actors & Participants

Current authenticated company-scoped actor with procurement-configuration-edit. Existing ERP scope middleware remains authoritative.

## 6. Entry Points & Triggers

POST /api/procurement/v1/configuration/{categories|methods}/{id}/retire; existing settings table Remove action.

## 7. Preconditions

Visible record, Draft state, current version, nonblank reason ≤1,000 characters. Category has no unretired children or catalog/request/plan references.

## 8. Workflow

Open settings → select unused draft → Remove → enter reason → confirm → API persists retirement and audit → refresh list.

## 9. Status / State Transitions

Draft → Retired. Retired remains readable; existing edit/activate guards deny further changes.

## 10. Permissions & Data Scope

Existing configuration-edit permission and tenant/company query filters. Read-only administrator mode offers no Remove action.

## 11. Business Rules

No stale-version removal, blank reason, active-setting removal or parent-category retirement under a child. No record or history is physically removed.

## 12. Data Fields & Ownership

Existing Status and Version; append-only DraftRetired audit reason/actor/from/to/version. Procurement owns these settings; no Core master duplication.

## 13. Screens & Data Views

Reuse LiveConfiguration, ERP buttons/textarea/panel/table and status badge; keep title on page background.

## 14. User Experience / Human Behaviour

Plain Remove label, explicit history-preservation explanation, Keep draft recovery, disabled blank/busy confirmation; failures keep the reason.

## 15. Notifications & Communications

Inline confirmation via refreshed persisted status; no new external notification service.

## 16. Documents & Attachments

N/A; this transition changes no document ownership.

## 17. Exceptions & Edge Cases

Missing record404; denied action403; locked/stale/in-use409; invalid reason422. Category/method codes remain reserved to preserve references.

## 18. Integrations / APIs / Events / Sandbox

Reuse owning ProcurementDbContext, existing authentication and shared shell scope. No Finance/stock/payment event.

## 19. Audit / Security / Privacy

Record authorized actor, reason and transition; retain normal automatic audit and optimistic concurrency. No personal/bank data added.

## 20. Reports / Dashboards / Metrics

Existing list/detail includes Retired state; existing Active-only purchasing selectors do not offer it.

## 21. Configuration & Localization

No new approval thresholds, currencies, taxes or seeds. Existing company settings apply.

## 22. Non-Functional Requirements

Bounded indexed owner queries and atomic SaveChanges. Existing concurrent reference-writer interactions need complete PostgreSQL release regression; no immediate cross-writer serialization guarantee is claimed.

## 23. Postconditions

Record retained as Retired with higher version and audit. Failed validation leaves state/version unchanged.

## 24. Failure & Recovery

Preserve form/reason on failure. Refresh and inspect status after unknown response before retry. No automatic command retry.

## 25. Acceptance Criteria

Both settings types can be retired and reopened; drafts with dependencies, stale versions, missing permission or foreign company are denied.

## 26. Test Scenarios

Three mapped HTTP cases cover version/permission/reason/scope/history and child prevention; three mocked browser cases cover both kinds, refresh, and stale-removal reason retention. Execution results recorded separately.

## 27. Ownership / Dependencies

Coordinator owns API/UI and release verification; Core provides identity/company scope. No schema migration required for this feature.

## 28. Open Decisions / Assumptions

Active-setting deactivation/replacement is separate. Full actual PostgreSQL/browser release acceptance remains required before deployment.
