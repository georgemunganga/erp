# Kuubu public-launch page checklist

Audit owner: Codex with Greenwebb. Date: 20 November 2026. Release decision: **HOLD**.

Read this with [the workflow and release gates](PUBLIC-LAUNCH-READINESS-20261120.md). A page opening is a smoke observation, not proof of create/edit/archive, persistence, permission denial or downstream effects. Redirect aliases remain in this inventory. All actions default to NOT TESTED until supported by retained evidence.

| ID | Page / route | Workflow | Page observation | Lifecycle / scope |
| --- | --- | --- | --- | --- |
| RP-001 | `/accept-invitation` | Navigation or supporting workflow | NOT TESTED | NOT_TESTED / NOT TESTED |
| RP-002 | `/core/branch-access → /core/workspace/settings/branch-access` | Core foundations | BLOCKED: company chooser instead of requested page | NOT_TESTED / NOT TESTED |
| RP-003 | `/core/branding → /core/workspace/settings/branding` | Core foundations | BLOCKED: company chooser instead of requested page | NOT_TESTED / NOT TESTED |
| RP-004 | `/core/` | Core foundations | BLOCKED: company chooser instead of requested page | NOT_TESTED / NOT TESTED |
| RP-005 | `/core/organization → /core/workspace/settings/organization` | Core foundations | BLOCKED: company chooser instead of requested page | NOT_TESTED / NOT TESTED |
| RP-006 | `/core/roles → /core/workspace/settings/roles` | Core foundations | BLOCKED: company chooser instead of requested page | NOT_TESTED / NOT TESTED |
| RP-007 | `/core/settings/$slug → /core/workspace/settings/$slug` | Core foundations | REQUIRES RECORD/CONTEXT | NOT_TESTED / NOT TESTED |
| RP-008 | `/core/users/$id → /core/workspace/settings/users/$id` | Core foundations | REQUIRES RECORD/CONTEXT | NOT_TESTED / NOT TESTED |
| RP-009 | `/core/users` | Core foundations | BLOCKED: company chooser instead of requested page | NOT_TESTED / NOT TESTED |
| RP-010 | `/core/workspace/settings/$slug` | Core foundations | REQUIRES RECORD/CONTEXT | NOT_TESTED / NOT TESTED |
| RP-011 | `/core/workspace/settings` | Core foundations | BLOCKED: company chooser instead of requested page | NOT_TESTED / NOT TESTED |
| RP-012 | `/core/workspace/settings/users/$id` | Core foundations | REQUIRES RECORD/CONTEXT | NOT_TESTED / NOT TESTED |
| RP-013 | `/hrm/analytics` | Reporting | OBSERVED: page rendered; actions unqualified | NOT_TESTED / NOT TESTED |
| RP-014 | `/hrm/approvals` | Approvals | OBSERVED: page rendered; actions unqualified | NOT_TESTED / NOT TESTED |
| RP-015 | `/hrm/attendance/$id` | Attendance and work inputs | REQUIRES RECORD/CONTEXT | NOT_TESTED / NOT TESTED |
| RP-016 | `/hrm/attendance/clock` | Attendance and work inputs | OBSERVED: page rendered; actions unqualified | NOT_TESTED / NOT TESTED |
| RP-017 | `/hrm/attendance/` | Attendance and work inputs | OBSERVED: page rendered; actions unqualified | NOT_TESTED / NOT TESTED |
| RP-018 | `/hrm/attendance/new` | Attendance and work inputs | RETEST: workspace-session HTTP429/recovery | NOT_TESTED / NOT TESTED |
| RP-019 | `/hrm/benefits` | Variable pay and benefits | OBSERVED: page rendered; actions unqualified | NOT_TESTED / NOT TESTED |
| RP-020 | `/hrm/configuration/branch-access → /core/workspace/settings/branch-access` | HR/payroll configuration | RETEST: captured while access check pending | NOT_TESTED / NOT TESTED |
| RP-021 | `/hrm/configuration/branding → /core/workspace/settings/branding` | HR/payroll configuration | BLOCKED: company chooser instead of requested page | NOT_TESTED / NOT TESTED |
| RP-022 | `/hrm/configuration/business → /hrm/workspace/settings/business` | HR/payroll configuration | OBSERVED: page rendered; actions unqualified | NOT_TESTED / NOT TESTED |
| RP-023 | `/hrm/configuration/compliance → /hrm/workspace/settings/compliance` | HR/payroll configuration | RETEST: captured while access check pending | NOT_TESTED / NOT TESTED |
| RP-024 | `/hrm/configuration/contract-types → /hrm/workspace/settings/contract-types` | HR/payroll configuration | RETEST: captured while access check pending | NOT_TESTED / NOT TESTED |
| RP-025 | `/hrm/configuration/go-live → /hrm/workspace/settings/go-live` | HR/payroll configuration | RETEST: workspace-session HTTP429/recovery | NOT_TESTED / NOT TESTED |
| RP-026 | `/hrm/configuration/holidays → /hrm/workspace/settings/holidays` | HR/payroll configuration | RETEST: workspace-session HTTP429/recovery | NOT_TESTED / NOT TESTED |
| RP-027 | `/hrm/configuration/ → /hrm/workspace/settings` | HR/payroll configuration | RETEST: workspace-session HTTP429/recovery | NOT_TESTED / NOT TESTED |
| RP-028 | `/hrm/configuration/integrations → /hrm/workspace/settings/integrations` | HR/payroll configuration | RETEST: workspace-session HTTP429/recovery | NOT_TESTED / NOT TESTED |
| RP-029 | `/hrm/configuration/leave-periods → /hrm/workspace/settings/leave-periods` | HR/payroll configuration | RETEST: workspace-session HTTP429/recovery | NOT_TESTED / NOT TESTED |
| RP-030 | `/hrm/configuration/leave-types → /hrm/workspace/settings/leave-types` | HR/payroll configuration | RETEST: workspace-session HTTP429/recovery | NOT_TESTED / NOT TESTED |
| RP-031 | `/hrm/configuration/organisation → /core/workspace/settings/organization` | HR/payroll configuration | BLOCKED: company chooser instead of requested page | NOT_TESTED / NOT TESTED |
| RP-032 | `/hrm/configuration/payroll → /hrm/workspace/settings/payroll` | HR/payroll configuration | RETEST: captured while data pending | PARTIAL: salary-component create/read/edit/archive tested; other tabs NOT TESTED / NOT TESTED |
| RP-033 | `/hrm/configuration/process → /hrm/workspace/settings/process` | HR/payroll configuration | OBSERVED: page rendered; actions unqualified | NOT_TESTED / NOT TESTED |
| RP-034 | `/hrm/configuration/roles → /core/workspace/settings/roles` | HR/payroll configuration | BLOCKED: company chooser instead of requested page | NOT_TESTED / NOT TESTED |
| RP-035 | `/hrm/configuration/technical → /hrm/workspace/settings/technical` | HR/payroll configuration | OBSERVED: page rendered; actions unqualified | NOT_TESTED / NOT TESTED |
| RP-036 | `/hrm/configuration/users/$id → /core/workspace/settings/users/$id` | HR/payroll configuration | REQUIRES RECORD/CONTEXT | NOT_TESTED / NOT TESTED |
| RP-037 | `/hrm/configuration/users` | HR/payroll configuration | BLOCKED: company chooser instead of requested page | NOT_TESTED / NOT TESTED |
| RP-038 | `/hrm/data/import-export` | Import/export alternative | OBSERVED: page rendered; actions unqualified | NOT_TESTED / NOT TESTED |
| RP-039 | `/hrm/employees/$id/edit` | Employee setup | REQUIRES RECORD/CONTEXT | NOT_TESTED / NOT TESTED |
| RP-040 | `/hrm/employees/$id` | Employee setup | REQUIRES RECORD/CONTEXT | NOT_TESTED / NOT TESTED |
| RP-041 | `/hrm/employees/import` | Employee setup | OBSERVED: page rendered; actions unqualified | NOT_TESTED / NOT TESTED |
| RP-042 | `/hrm/employees/` | Employee setup | OBSERVED: page rendered; actions unqualified | NOT_TESTED / NOT TESTED |
| RP-043 | `/hrm/employees/new` | Employee setup | RETEST: captured while data pending | NOT_TESTED / NOT TESTED |
| RP-044 | `/hrm/experience/announcements` | Navigation or supporting workflow | EXCLUDED: release-disabled screen observed | NOT_TESTED / NOT TESTED |
| RP-045 | `/hrm/experience/knowledge` | Navigation or supporting workflow | EXCLUDED: release-disabled screen observed | NOT_TESTED / NOT TESTED |
| RP-046 | `/hrm/experience/letters` | Navigation or supporting workflow | OBSERVED: page rendered; actions unqualified | NOT_TESTED / NOT TESTED |
| RP-047 | `/hrm/help` | Navigation or supporting workflow | OBSERVED: page rendered; actions unqualified | NOT_TESTED / NOT TESTED |
| RP-048 | `/hrm/import` | Navigation or supporting workflow | RETEST: workspace-session HTTP429/recovery | NOT_TESTED / NOT TESTED |
| RP-049 | `/hrm/` | Navigation or supporting workflow | OBSERVED: page rendered; actions unqualified | NOT_TESTED / NOT TESTED |
| RP-050 | `/hrm/leave/$id` | Leave | REQUIRES RECORD/CONTEXT | NOT_TESTED / NOT TESTED |
| RP-051 | `/hrm/leave/allocations` | Leave | RETEST: workspace-session HTTP429/recovery | NOT_TESTED / NOT TESTED |
| RP-052 | `/hrm/leave/approvals` | Leave | RETEST: workspace-session HTTP429/recovery | NOT_TESTED / NOT TESTED |
| RP-053 | `/hrm/leave/control` | Leave | NOT TESTED | NOT_TESTED / NOT TESTED |
| RP-054 | `/hrm/leave/` | Leave | NOT TESTED | NOT_TESTED / NOT TESTED |
| RP-055 | `/hrm/leave/new` | Leave | NOT TESTED | NOT_TESTED / NOT TESTED |
| RP-056 | `/hrm/leave/recorded` | Leave | NOT TESTED | NOT_TESTED / NOT TESTED |
| RP-057 | `/hrm/lifecycle/alumni` | Navigation or supporting workflow | NOT TESTED | NOT_TESTED / NOT TESTED |
| RP-058 | `/hrm/lifecycle/assets` | Navigation or supporting workflow | NOT TESTED | NOT_TESTED / NOT TESTED |
| RP-059 | `/hrm/lifecycle/` | Navigation or supporting workflow | NOT TESTED | NOT_TESTED / NOT TESTED |
| RP-060 | `/hrm/lifecycle/journeys` | Navigation or supporting workflow | NOT TESTED | NOT_TESTED / NOT TESTED |
| RP-061 | `/hrm/lifecycle/mobility` | Navigation or supporting workflow | NOT TESTED | NOT_TESTED / NOT TESTED |
| RP-062 | `/hrm/lifecycle/movements/new` | Navigation or supporting workflow | NOT TESTED | NOT_TESTED / NOT TESTED |
| RP-063 | `/hrm/lifecycle/movements` | Navigation or supporting workflow | NOT TESTED | NOT_TESTED / NOT TESTED |
| RP-064 | `/hrm/lifecycle/offboarding/$id` | Navigation or supporting workflow | REQUIRES RECORD/CONTEXT | NOT_TESTED / NOT TESTED |
| RP-065 | `/hrm/lifecycle/offboarding` | Navigation or supporting workflow | NOT TESTED | NOT_TESTED / NOT TESTED |
| RP-066 | `/hrm/lifecycle/onboarding/$id` | Navigation or supporting workflow | REQUIRES RECORD/CONTEXT | NOT_TESTED / NOT TESTED |
| RP-067 | `/hrm/lifecycle/onboarding` | Navigation or supporting workflow | OBSERVED: page rendered; actions unqualified | NOT_TESTED / NOT TESTED |
| RP-068 | `/hrm/my-documents` | Navigation or supporting workflow | OBSERVED: page rendered; actions unqualified | NOT_TESTED / NOT TESTED |
| RP-069 | `/hrm/my-offboarding` | Navigation or supporting workflow | NOT TESTED | NOT_TESTED / NOT TESTED |
| RP-070 | `/hrm/my-performance` | Navigation or supporting workflow | NOT TESTED | NOT_TESTED / NOT TESTED |
| RP-071 | `/hrm/my-preferences` | Navigation or supporting workflow | NOT TESTED | NOT_TESTED / NOT TESTED |
| RP-072 | `/hrm/my-profile` | Navigation or supporting workflow | NOT TESTED | NOT_TESTED / NOT TESTED |
| RP-073 | `/hrm/offboarding/$id` | Navigation or supporting workflow | REQUIRES RECORD/CONTEXT | NOT_TESTED / NOT TESTED |
| RP-074 | `/hrm/offboarding/` | Navigation or supporting workflow | OBSERVED: page rendered; actions unqualified | NOT_TESTED / NOT TESTED |
| RP-075 | `/hrm/org-chart` | Employee setup | OBSERVED: page rendered; actions unqualified | NOT_TESTED / NOT TESTED |
| RP-076 | `/hrm/pay/compensation` | Pay assignment | OBSERVED: page rendered; actions unqualified | PARTIAL: component candidate/archive propagation passed; pay-profile edit date FAIL PL-004 / NOT TESTED |
| RP-077 | `/hrm/payroll/exceptions` | Payroll and closeout | OBSERVED: page rendered; actions unqualified | NOT_TESTED / NOT TESTED |
| RP-078 | `/hrm/payroll/` | Payroll and closeout | OBSERVED: page rendered; actions unqualified | NOT_TESTED / NOT TESTED |
| RP-079 | `/hrm/payroll/queue` | Payroll and closeout | OBSERVED: page rendered; actions unqualified | NOT_TESTED / NOT TESTED |
| RP-080 | `/hrm/payroll/runs/$id/edit` | Payroll and closeout | REQUIRES RECORD/CONTEXT | NOT_TESTED / NOT TESTED |
| RP-081 | `/hrm/payroll/runs/$id` | Payroll and closeout | REQUIRES RECORD/CONTEXT | NOT_TESTED / NOT TESTED |
| RP-082 | `/hrm/payroll/runs/new` | Payroll and closeout | NOT TESTED | NOT_TESTED / NOT TESTED |
| RP-083 | `/hrm/payroll/runs` | Payroll and closeout | OBSERVED: page rendered; actions unqualified | NOT_TESTED / NOT TESTED |
| RP-084 | `/hrm/payroll/salary-advances` | Payroll and closeout | OBSERVED: page rendered; actions unqualified | NOT_TESTED / NOT TESTED |
| RP-085 | `/hrm/payslips/$id` | Payroll and closeout | REQUIRES RECORD/CONTEXT | NOT_TESTED / NOT TESTED |
| RP-086 | `/hrm/payslips/` | Payroll and closeout | OBSERVED: page rendered; actions unqualified | NOT_TESTED / NOT TESTED |
| RP-087 | `/hrm/people/data-quality` | Employee setup | OBSERVED: page rendered; actions unqualified | NOT_TESTED / NOT TESTED |
| RP-088 | `/hrm/people/documents` | Employee setup | OBSERVED: page rendered; actions unqualified | NOT_TESTED / NOT TESTED |
| RP-089 | `/hrm/people/master-data` | Employee setup | OBSERVED: page rendered; actions unqualified | NOT_TESTED / NOT TESTED |
| RP-090 | `/hrm/people/org` | Employee setup | OBSERVED: page rendered; actions unqualified | NOT_TESTED / NOT TESTED |
| RP-091 | `/hrm/people/positions/$id` | Employee setup | REQUIRES RECORD/CONTEXT | NOT_TESTED / NOT TESTED |
| RP-092 | `/hrm/people/positions` | Employee setup | OBSERVED: page rendered; actions unqualified | NOT_TESTED / NOT TESTED |
| RP-093 | `/hrm/people/privacy` | Employee setup | OBSERVED: page rendered; actions unqualified | NOT_TESTED / NOT TESTED |
| RP-094 | `/hrm/performance/$id` | Talent and recruitment | REQUIRES RECORD/CONTEXT | NOT_TESTED / NOT TESTED |
| RP-095 | `/hrm/performance/` | Talent and recruitment | NOT TESTED | NOT_TESTED / NOT TESTED |
| RP-096 | `/hrm/recruitment/candidates/$id` | Talent and recruitment | REQUIRES RECORD/CONTEXT | NOT_TESTED / NOT TESTED |
| RP-097 | `/hrm/recruitment/candidates` | Talent and recruitment | NOT TESTED | NOT_TESTED / NOT TESTED |
| RP-098 | `/hrm/recruitment/` | Talent and recruitment | NOT TESTED | NOT_TESTED / NOT TESTED |
| RP-099 | `/hrm/recruitment/offers` | Talent and recruitment | NOT TESTED | NOT_TESTED / NOT TESTED |
| RP-100 | `/hrm/recruitment/operations` | Talent and recruitment | NOT TESTED | NOT_TESTED / NOT TESTED |
| RP-101 | `/hrm/recruitment/requisitions/new` | Talent and recruitment | NOT TESTED | NOT_TESTED / NOT TESTED |
| RP-102 | `/hrm/recruitment/requisitions` | Talent and recruitment | NOT TESTED | NOT_TESTED / NOT TESTED |
| RP-103 | `/hrm/recruitment/vacancies` | Talent and recruitment | NOT TESTED | NOT_TESTED / NOT TESTED |
| RP-104 | `/hrm/relations/cases/$id` | Navigation or supporting workflow | REQUIRES RECORD/CONTEXT | NOT_TESTED / NOT TESTED |
| RP-105 | `/hrm/relations/cases` | Navigation or supporting workflow | OBSERVED: page rendered; actions unqualified | NOT_TESTED / NOT TESTED |
| RP-106 | `/hrm/relations/discipline` | Navigation or supporting workflow | NOT TESTED | NOT_TESTED / NOT TESTED |
| RP-107 | `/hrm/relations/ethics` | Navigation or supporting workflow | NOT TESTED | NOT_TESTED / NOT TESTED |
| RP-108 | `/hrm/relations/` | Navigation or supporting workflow | NOT TESTED | NOT_TESTED / NOT TESTED |
| RP-109 | `/hrm/relations/labour` | Navigation or supporting workflow | NOT TESTED | NOT_TESTED / NOT TESTED |
| RP-110 | `/hrm/relations/operations` | Navigation or supporting workflow | OBSERVED: page rendered; actions unqualified | NOT_TESTED / NOT TESTED |
| RP-111 | `/hrm/relations/protected-disclosures` | Navigation or supporting workflow | OBSERVED: page rendered; actions unqualified | NOT_TESTED / NOT TESTED |
| RP-112 | `/hrm/relations/safety` | Navigation or supporting workflow | NOT TESTED | NOT_TESTED / NOT TESTED |
| RP-113 | `/hrm/reporting` | Reporting | OBSERVED: page rendered; actions unqualified | NOT_TESTED / NOT TESTED |
| RP-114 | `/hrm/reports/builder` | Reporting | NOT TESTED | NOT_TESTED / NOT TESTED |
| RP-115 | `/hrm/reports/` | Reporting | NOT TESTED | NOT_TESTED / NOT TESTED |
| RP-116 | `/hrm/requests/$id` | Navigation or supporting workflow | REQUIRES RECORD/CONTEXT | NOT_TESTED / NOT TESTED |
| RP-117 | `/hrm/requests/` | Navigation or supporting workflow | OBSERVED: page rendered; actions unqualified | NOT_TESTED / NOT TESTED |
| RP-118 | `/hrm/requests/new` | Navigation or supporting workflow | OBSERVED: page rendered; actions unqualified | NOT_TESTED / NOT TESTED |
| RP-119 | `/hrm/self-service` | Navigation or supporting workflow | OBSERVED: page rendered; actions unqualified | NOT_TESTED / NOT TESTED |
| RP-120 | `/hrm/setup` | HR/payroll configuration | NOT TESTED | NOT_TESTED / NOT TESTED |
| RP-121 | `/hrm/talent/feedback` | Talent and recruitment | NOT TESTED | NOT_TESTED / NOT TESTED |
| RP-122 | `/hrm/talent/goals` | Talent and recruitment | NOT TESTED | NOT_TESTED / NOT TESTED |
| RP-123 | `/hrm/talent/` | Talent and recruitment | NOT TESTED | NOT_TESTED / NOT TESTED |
| RP-124 | `/hrm/talent/learning` | Talent and recruitment | NOT TESTED | NOT_TESTED / NOT TESTED |
| RP-125 | `/hrm/talent/reviews/$id` | Talent and recruitment | REQUIRES RECORD/CONTEXT | NOT_TESTED / NOT TESTED |
| RP-126 | `/hrm/talent/reviews` | Talent and recruitment | NOT TESTED | NOT_TESTED / NOT TESTED |
| RP-127 | `/hrm/talent/succession` | Talent and recruitment | NOT TESTED | NOT_TESTED / NOT TESTED |
| RP-128 | `/hrm/time/attendance/bulk` | Attendance and work inputs | NOT TESTED | NOT_TESTED / NOT TESTED |
| RP-129 | `/hrm/time/attendance/import` | Attendance and work inputs | NOT TESTED | NOT_TESTED / NOT TESTED |
| RP-130 | `/hrm/time/expenses/` | Attendance and work inputs | NOT TESTED | NOT_TESTED / NOT TESTED |
| RP-131 | `/hrm/time/expenses/new` | Attendance and work inputs | NOT TESTED | NOT_TESTED / NOT TESTED |
| RP-132 | `/hrm/time/operations` | Attendance and work inputs | NOT TESTED | NOT_TESTED / NOT TESTED |
| RP-133 | `/hrm/time/schedules` | Attendance and work inputs | NOT TESTED | NOT_TESTED / NOT TESTED |
| RP-134 | `/hrm/time/timesheets` | Attendance and work inputs | NOT TESTED | NOT_TESTED / NOT TESTED |
| RP-135 | `/hrm/time/toil` | Attendance and work inputs | NOT TESTED | NOT_TESTED / NOT TESTED |
| RP-136 | `/hrm/time/travel` | Attendance and work inputs | NOT TESTED | NOT_TESTED / NOT TESTED |
| RP-137 | `/hrm/time/utilisation` | Attendance and work inputs | NOT TESTED | NOT_TESTED / NOT TESTED |
| RP-138 | `/hrm/workspace/settings/$slug` | HR/payroll configuration | REQUIRES RECORD/CONTEXT | NOT_TESTED / NOT TESTED |
| RP-139 | `/hrm/workspace/settings` | HR/payroll configuration | NOT TESTED | NOT_TESTED / NOT TESTED |
| RP-140 | `/` | Navigation or supporting workflow | NOT TESTED | NOT_TESTED / NOT TESTED |
| RP-141 | `/setup` | HR/payroll configuration | NOT TESTED | NOT_TESTED / NOT TESTED |
| RP-142 | `/sign-in` | Navigation or supporting workflow | NOT TESTED | NOT_TESTED / NOT TESTED |
| RP-143 | `/speak-up` | Navigation or supporting workflow | NOT TESTED | NOT_TESTED / NOT TESTED |
| RP-144 | `/team` | Navigation or supporting workflow | NOT TESTED | NOT_TESTED / NOT TESTED |
| RP-145 | `/workspace-access` | Navigation or supporting workflow | NOT TESTED | NOT_TESTED / NOT TESTED |
| RP-146 | `/hrm/workspace/settings/business` | HR/payroll configuration | NOT TESTED | NOT_TESTED / NOT TESTED |
| RP-147 | `/hrm/workspace/settings/holidays` | HR/payroll configuration | NOT TESTED | NOT_TESTED / NOT TESTED |
| RP-148 | `/hrm/workspace/settings/payroll` | HR/payroll configuration | NOT TESTED | PARTIAL: salary-component create/read/edit/archive tested; other tabs NOT TESTED / NOT TESTED |
| RP-149 | `/hrm/workspace/settings/leave-types` | HR/payroll configuration | NOT TESTED | NOT_TESTED / NOT TESTED |
| RP-150 | `/hrm/workspace/settings/contract-types` | HR/payroll configuration | NOT TESTED | NOT_TESTED / NOT TESTED |
| RP-151 | `/hrm/workspace/settings/leave-periods` | HR/payroll configuration | NOT TESTED | NOT_TESTED / NOT TESTED |
| RP-152 | `/hrm/workspace/settings/process` | HR/payroll configuration | NOT TESTED | NOT_TESTED / NOT TESTED |
| RP-153 | `/hrm/workspace/settings/compliance` | HR/payroll configuration | NOT TESTED | NOT_TESTED / NOT TESTED |
| RP-154 | `/hrm/workspace/settings/integrations` | HR/payroll configuration | NOT TESTED | NOT_TESTED / NOT TESTED |
| RP-155 | `/hrm/workspace/settings/go-live` | HR/payroll configuration | NOT TESTED | NOT_TESTED / NOT TESTED |
| RP-156 | `/hrm/workspace/settings/technical` | HR/payroll configuration | NOT TESTED | NOT_TESTED / NOT TESTED |
| RP-157 | `/core/workspace/settings/organization` | Core foundations | NOT TESTED | NOT_TESTED / NOT TESTED |
| RP-158 | `/core/workspace/settings/branding` | Core foundations | NOT TESTED | NOT_TESTED / NOT TESTED |
| RP-159 | `/core/workspace/settings/users` | Core foundations | NOT TESTED | NOT_TESTED / NOT TESTED |
| RP-160 | `/core/workspace/settings/roles` | Core foundations | NOT TESTED | NOT_TESTED / NOT TESTED |
| RP-161 | `/core/workspace/settings/branch-access` | Core foundations | NOT TESTED | NOT_TESTED / NOT TESTED |
| RP-162 | `/core/workspace/settings/workflows` | Core foundations | NOT TESTED | NOT_TESTED / NOT TESTED |
| RP-163 | `/core/workspace/settings/currencies` | Core foundations | NOT TESTED | NOT_TESTED / NOT TESTED |
| RP-164 | `/core/workspace/settings/reporting-tags` | Core foundations | NOT TESTED | NOT_TESTED / NOT TESTED |
| RP-165 | `/core/workspace/settings/general` | Core foundations | NOT TESTED | NOT_TESTED / NOT TESTED |
| RP-166 | `/core/workspace/settings/transaction-number-series` | Core foundations | NOT TESTED | NOT_TESTED / NOT TESTED |
| RP-167 | `/core/workspace/settings/pdf-templates` | Core foundations | NOT TESTED | NOT_TESTED / NOT TESTED |
| RP-168 | `/core/workspace/settings/email-notifications` | Core foundations | NOT TESTED | NOT_TESTED / NOT TESTED |
| RP-169 | `/core/workspace/settings/web-tabs` | Core foundations | NOT TESTED | NOT_TESTED / NOT TESTED |

## What every page must prove

- [ ] Its prerequisites, record owner and downstream consumers are identified.
- [ ] Create/read/update actions persist after refresh and reopen.
- [ ] Archive, close or effective-date retirement preserves references and prior payroll.
- [ ] Unsafe delete, invalid input and duplicate retries are rejected with usable feedback.
- [ ] Restricted users and another company cannot read or mutate its records.
- [ ] Empty/loading/error/reconnect states work without losing drafts or inventing success.
- [ ] Navigation, keyboard, narrow screens, labels, contrast and relevant downloads work.
- [ ] Audit records and owning database state agree with the UI result.

## Per-page workflow and dependency register

### RP-001 — `/accept-invitation`
- Prerequisites: Verified identity and explicit company/module access.
- Produces: Destination, reference or supporting record.
- Used by: Owning business workflow.
- Direct realApi references in route source: None in this route file; trace imported component, redirect or layout before concluding no backend.
- Source: `modules/hrm/frontend/module-connect/src/routes/accept-invitation.tsx`.

### RP-002 — `/core/branch-access`
- Prerequisites: Company ownership and verified membership.
- Produces: Organisation, roles and shared configuration.
- Used by: Every module; employee placement and access.
- Direct realApi references in route source: None in this route file; trace imported component, redirect or layout before concluding no backend.
- Source: `modules/hrm/frontend/module-connect/src/routes/core.branch-access.tsx`.

### RP-003 — `/core/branding`
- Prerequisites: Company ownership and verified membership.
- Produces: Organisation, roles and shared configuration.
- Used by: Every module; employee placement and access.
- Direct realApi references in route source: None in this route file; trace imported component, redirect or layout before concluding no backend.
- Source: `modules/hrm/frontend/module-connect/src/routes/core.branding.tsx`.

### RP-004 — `/core/`
- Prerequisites: Company ownership and verified membership.
- Produces: Organisation, roles and shared configuration.
- Used by: Every module; employee placement and access.
- Direct realApi references in route source: None in this route file; trace imported component, redirect or layout before concluding no backend.
- Source: `modules/hrm/frontend/module-connect/src/routes/core.index.tsx`.

### RP-005 — `/core/organization`
- Prerequisites: Company ownership and verified membership.
- Produces: Organisation, roles and shared configuration.
- Used by: Every module; employee placement and access.
- Direct realApi references in route source: None in this route file; trace imported component, redirect or layout before concluding no backend.
- Source: `modules/hrm/frontend/module-connect/src/routes/core.organization.tsx`.

### RP-006 — `/core/roles`
- Prerequisites: Company ownership and verified membership.
- Produces: Organisation, roles and shared configuration.
- Used by: Every module; employee placement and access.
- Direct realApi references in route source: None in this route file; trace imported component, redirect or layout before concluding no backend.
- Source: `modules/hrm/frontend/module-connect/src/routes/core.roles.tsx`.

### RP-007 — `/core/settings/$slug`
- Prerequisites: Company ownership and verified membership.
- Produces: Organisation, roles and shared configuration.
- Used by: Every module; employee placement and access.
- Direct realApi references in route source: None in this route file; trace imported component, redirect or layout before concluding no backend.
- Source: `modules/hrm/frontend/module-connect/src/routes/core.settings.$slug.tsx`.

### RP-008 — `/core/users/$id`
- Prerequisites: Company ownership and verified membership.
- Produces: Organisation, roles and shared configuration.
- Used by: Every module; employee placement and access.
- Direct realApi references in route source: None in this route file; trace imported component, redirect or layout before concluding no backend.
- Source: `modules/hrm/frontend/module-connect/src/routes/core.users.$id.tsx`.

### RP-009 — `/core/users`
- Prerequisites: Company ownership and verified membership.
- Produces: Organisation, roles and shared configuration.
- Used by: Every module; employee placement and access.
- Direct realApi references in route source: None in this route file; trace imported component, redirect or layout before concluding no backend.
- Source: `modules/hrm/frontend/module-connect/src/routes/core.users.tsx`.

### RP-010 — `/core/workspace/settings/$slug`
- Prerequisites: Company ownership and verified membership.
- Produces: Organisation, roles and shared configuration.
- Used by: Every module; employee placement and access.
- Direct realApi references in route source: None in this route file; trace imported component, redirect or layout before concluding no backend.
- Source: `modules/hrm/frontend/module-connect/src/routes/core.workspace.settings.$slug.tsx`.

### RP-011 — `/core/workspace/settings`
- Prerequisites: Company ownership and verified membership.
- Produces: Organisation, roles and shared configuration.
- Used by: Every module; employee placement and access.
- Direct realApi references in route source: None in this route file; trace imported component, redirect or layout before concluding no backend.
- Source: `modules/hrm/frontend/module-connect/src/routes/core.workspace.settings.tsx`.

### RP-012 — `/core/workspace/settings/users/$id`
- Prerequisites: Company ownership and verified membership.
- Produces: Organisation, roles and shared configuration.
- Used by: Every module; employee placement and access.
- Direct realApi references in route source: None in this route file; trace imported component, redirect or layout before concluding no backend.
- Source: `modules/hrm/frontend/module-connect/src/routes/core.workspace.settings.users.$id.tsx`.

### RP-013 — `/hrm/analytics`
- Prerequisites: Authorised company-scoped persisted records.
- Produces: Filtered report or download.
- Used by: Management decisions and audit/reconciliation.
- Direct realApi references in route source: None in this route file; trace imported component, redirect or layout before concluding no backend.
- Source: `modules/hrm/frontend/module-connect/src/routes/hrm.analytics.tsx`.

### RP-014 — `/hrm/approvals`
- Prerequisites: Submitted owned record and assigned decision authority.
- Produces: Authorised decision and audit.
- Used by: Owning leave/time/payroll/HR workflow.
- Direct realApi references in route source: experienceRequests, leaveRequests, timeCorrections, workflowQueue
- Source: `modules/hrm/frontend/module-connect/src/routes/hrm.approvals.tsx`.

### RP-015 — `/hrm/attendance/$id`
- Prerequisites: Linked employee, location, calendar and access.
- Produces: Check events, approved time and exceptions.
- Used by: Overtime, payroll inputs and management reporting.
- Direct realApi references in route source: None in this route file; trace imported component, redirect or layout before concluding no backend.
- Source: `modules/hrm/frontend/module-connect/src/routes/hrm.attendance.$id.tsx`.

### RP-016 — `/hrm/attendance/clock`
- Prerequisites: Linked employee, location, calendar and access.
- Produces: Check events, approved time and exceptions.
- Used by: Overtime, payroll inputs and management reporting.
- Direct realApi references in route source: clockMyselfIn, clockMyselfOut, myAttendanceToday
- Source: `modules/hrm/frontend/module-connect/src/routes/hrm.attendance.clock.tsx`.

### RP-017 — `/hrm/attendance/`
- Prerequisites: Linked employee, location, calendar and access.
- Produces: Check events, approved time and exceptions.
- Used by: Overtime, payroll inputs and management reporting.
- Direct realApi references in route source: timeCorrections
- Source: `modules/hrm/frontend/module-connect/src/routes/hrm.attendance.index.tsx`.

### RP-018 — `/hrm/attendance/new`
- Prerequisites: Linked employee, location, calendar and access.
- Produces: Check events, approved time and exceptions.
- Used by: Overtime, payroll inputs and management reporting.
- Direct realApi references in route source: createMyCorrection
- Source: `modules/hrm/frontend/module-connect/src/routes/hrm.attendance.new.tsx`.

### RP-019 — `/hrm/benefits`
- Prerequisites: Employee, policy, amounts, effective dates and approver.
- Produces: Approved claims, allowances or deductions.
- Used by: Payroll inputs and financial reconciliation.
- Direct realApi references in route source: benefitAllowances, benefitClaims, benefitTypes, createBenefitClaim, createBenefitType, decideBenefitClaim, deleteBenefitAllowance, deleteBenefitClaim, deleteBenefitType, employees, locations, orgUnits, payBenefitClaim, setBenefitAllowance, updateBenefitClaim, updateBenefitType
- Source: `modules/hrm/frontend/module-connect/src/routes/hrm.benefits.tsx`.

### RP-020 — `/hrm/configuration/branch-access`
- Prerequisites: Company owner or authorised HR administrator.
- Produces: Calendars, contracts, statutory rules and pay periods.
- Used by: Employee setup, attendance, leave and payroll.
- Direct realApi references in route source: assignBranchAccess, branchAccess, removeBranchAccess
- Source: `modules/hrm/frontend/module-connect/src/routes/hrm.configuration.branch-access.tsx`.

### RP-021 — `/hrm/configuration/branding`
- Prerequisites: Company owner or authorised HR administrator.
- Produces: Calendars, contracts, statutory rules and pay periods.
- Used by: Employee setup, attendance, leave and payroll.
- Direct realApi references in route source: branding, resetBranding, updateBranding
- Source: `modules/hrm/frontend/module-connect/src/routes/hrm.configuration.branding.tsx`.

### RP-022 — `/hrm/configuration/business`
- Prerequisites: Company owner or authorised HR administrator.
- Produces: Calendars, contracts, statutory rules and pay periods.
- Used by: Employee setup, attendance, leave and payroll.
- Direct realApi references in route source: closeJob, createJob, jobs, updateJob
- Source: `modules/hrm/frontend/module-connect/src/routes/hrm.configuration.business.tsx`.

### RP-023 — `/hrm/configuration/compliance`
- Prerequisites: Company owner or authorised HR administrator.
- Produces: Calendars, contracts, statutory rules and pay periods.
- Used by: Employee setup, attendance, leave and payroll.
- Direct realApi references in route source: exportSecurityAudit, placeLegalHold, recordComplianceEvidence, releaseLegalHold, securityDashboard
- Source: `modules/hrm/frontend/module-connect/src/routes/hrm.configuration.compliance.tsx`.

### RP-024 — `/hrm/configuration/contract-types`
- Prerequisites: Company owner or authorised HR administrator.
- Produces: Calendars, contracts, statutory rules and pay periods.
- Used by: Employee setup, attendance, leave and payroll.
- Direct realApi references in route source: contractTypes, createContractType, updateContractType
- Source: `modules/hrm/frontend/module-connect/src/routes/hrm.configuration.contract-types.tsx`.

### RP-025 — `/hrm/configuration/go-live`
- Prerequisites: Company owner or authorised HR administrator.
- Produces: Calendars, contracts, statutory rules and pay periods.
- Used by: Employee setup, attendance, leave and payroll.
- Direct realApi references in route source: goLiveReadiness, recordGoLiveEvidence, recordGoLiveSignoff
- Source: `modules/hrm/frontend/module-connect/src/routes/hrm.configuration.go-live.tsx`.

### RP-026 — `/hrm/configuration/holidays`
- Prerequisites: Company owner or authorised HR administrator.
- Produces: Calendars, contracts, statutory rules and pay periods.
- Used by: Employee setup, attendance, leave and payroll.
- Direct realApi references in route source: calendar, calendars, createHoliday, deleteHoliday, updateHoliday
- Source: `modules/hrm/frontend/module-connect/src/routes/hrm.configuration.holidays.tsx`.

### RP-027 — `/hrm/configuration/`
- Prerequisites: Company owner or authorised HR administrator.
- Produces: Calendars, contracts, statutory rules and pay periods.
- Used by: Employee setup, attendance, leave and payroll.
- Direct realApi references in route source: None in this route file; trace imported component, redirect or layout before concluding no backend.
- Source: `modules/hrm/frontend/module-connect/src/routes/hrm.configuration.index.tsx`.

### RP-028 — `/hrm/configuration/integrations`
- Prerequisites: Company owner or authorised HR administrator.
- Produces: Calendars, contracts, statutory rules and pay periods.
- Used by: Employee setup, attendance, leave and payroll.
- Direct realApi references in route source: createFinancePosting, createIdentitySync, createPaymentHandoff, createStatutoryHandoff, downloadIntegration, integrationDashboard, payrollPayGroupPeriods, payrollPayGroups, payrollRuns, reconcileIntegration, retryIntegration
- Source: `modules/hrm/frontend/module-connect/src/routes/hrm.configuration.integrations.tsx`.

### RP-029 — `/hrm/configuration/leave-periods`
- Prerequisites: Company owner or authorised HR administrator.
- Produces: Calendars, contracts, statutory rules and pay periods.
- Used by: Employee setup, attendance, leave and payroll.
- Direct realApi references in route source: runLeaveAccrual, timeOperationsHistory
- Source: `modules/hrm/frontend/module-connect/src/routes/hrm.configuration.leave-periods.tsx`.

### RP-030 — `/hrm/configuration/leave-types`
- Prerequisites: Company owner or authorised HR administrator.
- Produces: Calendars, contracts, statutory rules and pay periods.
- Used by: Employee setup, attendance, leave and payroll.
- Direct realApi references in route source: createLeaveType, leaveTypes, updateLeaveType
- Source: `modules/hrm/frontend/module-connect/src/routes/hrm.configuration.leave-types.tsx`.

### RP-031 — `/hrm/configuration/organisation`
- Prerequisites: Company owner or authorised HR administrator.
- Produces: Calendars, contracts, statutory rules and pay periods.
- Used by: Employee setup, attendance, leave and payroll.
- Direct realApi references in route source: closeOrgUnit, createLocation, employees, legalEntities, locations, orgUnits
- Source: `modules/hrm/frontend/module-connect/src/routes/hrm.configuration.organisation.tsx`.

### RP-032 — `/hrm/configuration/payroll`
- Prerequisites: Company owner or authorised HR administrator.
- Produces: Calendars, contracts, statutory rules and pay periods.
- Used by: Employee setup, attendance, leave and payroll.
- Direct realApi references in route source: createSalaryComponent, payGroupsFull, payrollComponents, payrollContributionRules, payrollStructures, payrollTaxSlabs, updateContributionRule, updatePayGroup, updateSalaryComponent, updateStructure, updateTaxSlab
- Source: `modules/hrm/frontend/module-connect/src/routes/hrm.configuration.payroll.tsx`.

### RP-033 — `/hrm/configuration/process`
- Prerequisites: Company owner or authorised HR administrator.
- Produces: Calendars, contracts, statutory rules and pay periods.
- Used by: Employee setup, attendance, leave and payroll.
- Direct realApi references in route source: None in this route file; trace imported component, redirect or layout before concluding no backend.
- Source: `modules/hrm/frontend/module-connect/src/routes/hrm.configuration.process.tsx`.

### RP-034 — `/hrm/configuration/roles`
- Prerequisites: Company owner or authorised HR administrator.
- Produces: Calendars, contracts, statutory rules and pay periods.
- Used by: Employee setup, attendance, leave and payroll.
- Direct realApi references in route source: createRole, roles, updateRole
- Source: `modules/hrm/frontend/module-connect/src/routes/hrm.configuration.roles.tsx`.

### RP-035 — `/hrm/configuration/technical`
- Prerequisites: Company owner or authorised HR administrator.
- Produces: Calendars, contracts, statutory rules and pay periods.
- Used by: Employee setup, attendance, leave and payroll.
- Direct realApi references in route source: notificationDeliveries, retryNotification
- Source: `modules/hrm/frontend/module-connect/src/routes/hrm.configuration.technical.tsx`.

### RP-036 — `/hrm/configuration/users/$id`
- Prerequisites: Company owner or authorised HR administrator.
- Produces: Calendars, contracts, statutory rules and pay periods.
- Used by: Employee setup, attendance, leave and payroll.
- Direct realApi references in route source: None in this route file; trace imported component, redirect or layout before concluding no backend.
- Source: `modules/hrm/frontend/module-connect/src/routes/hrm.configuration.users.$id.tsx`.

### RP-037 — `/hrm/configuration/users`
- Prerequisites: Company owner or authorised HR administrator.
- Produces: Calendars, contracts, statutory rules and pay periods.
- Used by: Employee setup, attendance, leave and payroll.
- Direct realApi references in route source: roles
- Source: `modules/hrm/frontend/module-connect/src/routes/hrm.configuration.users.tsx`.

### RP-038 — `/hrm/data/import-export`
- Prerequisites: Chosen entity, mapping and access.
- Produces: Validated batch or export.
- Used by: Same business records as manual setup; imports optional.
- Direct realApi references in route source: importSchemas
- Source: `modules/hrm/frontend/module-connect/src/routes/hrm.data.import-export.tsx`.

### RP-039 — `/hrm/employees/$id/edit`
- Prerequisites: Company, location, department and agreed employment terms.
- Produces: Employee and assignment records.
- Used by: Leave, attendance, compensation, payroll and reporting.
- Direct realApi references in route source: addBankDetails, addEducation, addEmergencyContact, addExternalWorkHistory, addInternalWorkHistory, contractTypes, createWorkerAssignment, deleteEmergencyContact, education, employees, externalWorkHistory, internalWorkHistory, locations, removeEducation, removeExternalWorkHistory, removeInternalWorkHistory, updateBankDetails, updateEducation, updateEmergencyContact, updateExternalWorkHistory, updateInternalWorkHistory, updateWorker, updateWorkerAssignment, worker, workerAssignments
- Source: `modules/hrm/frontend/module-connect/src/routes/hrm.employees.$id.edit.tsx`.

### RP-040 — `/hrm/employees/$id`
- Prerequisites: Company, location, department and agreed employment terms.
- Produces: Employee and assignment records.
- Used by: Leave, attendance, compensation, payroll and reporting.
- Direct realApi references in route source: attendanceHistory, benefitAllowances, benefitClaims, employees, leaveBalances, leaveRequests, overtime, payrollProfiles, payrollRunAudit, payrollRunLeaveBalances, payrollRunLeaveTaken, payrollRunLines, payrollRuns, payslipDownloadBlob, salaryAdvances, worker, workerAssignments, workerDocuments, workerPayslipPreview, workerPayslips
- Source: `modules/hrm/frontend/module-connect/src/routes/hrm.employees.$id.tsx`.

### RP-041 — `/hrm/employees/import`
- Prerequisites: Company, location, department and agreed employment terms.
- Produces: Employee and assignment records.
- Used by: Leave, attendance, compensation, payroll and reporting.
- Direct realApi references in route source: None in this route file; trace imported component, redirect or layout before concluding no backend.
- Source: `modules/hrm/frontend/module-connect/src/routes/hrm.employees.import.tsx`.

### RP-042 — `/hrm/employees/`
- Prerequisites: Company, location, department and agreed employment terms.
- Produces: Employee and assignment records.
- Used by: Leave, attendance, compensation, payroll and reporting.
- Direct realApi references in route source: archiveWorker, entityTree
- Source: `modules/hrm/frontend/module-connect/src/routes/hrm.employees.index.tsx`.

### RP-043 — `/hrm/employees/new`
- Prerequisites: Company, location, department and agreed employment terms.
- Produces: Employee and assignment records.
- Used by: Leave, attendance, compensation, payroll and reporting.
- Direct realApi references in route source: createWorker, createWorkerAssignment, employees, legalEntities, locations, orgUnits, setupStepData, workerAssignments
- Source: `modules/hrm/frontend/module-connect/src/routes/hrm.employees.new.tsx`.

### RP-044 — `/hrm/experience/announcements`
- Prerequisites: Verified identity and explicit company/module access.
- Produces: Destination, reference or supporting record.
- Used by: Owning business workflow.
- Direct realApi references in route source: None in this route file; trace imported component, redirect or layout before concluding no backend.
- Source: `modules/hrm/frontend/module-connect/src/routes/hrm.experience.announcements.tsx`.

### RP-045 — `/hrm/experience/knowledge`
- Prerequisites: Verified identity and explicit company/module access.
- Produces: Destination, reference or supporting record.
- Used by: Owning business workflow.
- Direct realApi references in route source: None in this route file; trace imported component, redirect or layout before concluding no backend.
- Source: `modules/hrm/frontend/module-connect/src/routes/hrm.experience.knowledge.tsx`.

### RP-046 — `/hrm/experience/letters`
- Prerequisites: Verified identity and explicit company/module access.
- Produces: Destination, reference or supporting record.
- Used by: Owning business workflow.
- Direct realApi references in route source: createMyLetter, downloadMyLetter, myLetters
- Source: `modules/hrm/frontend/module-connect/src/routes/hrm.experience.letters.tsx`.

### RP-047 — `/hrm/help`
- Prerequisites: Verified identity and explicit company/module access.
- Produces: Destination, reference or supporting record.
- Used by: Owning business workflow.
- Direct realApi references in route source: None in this route file; trace imported component, redirect or layout before concluding no backend.
- Source: `modules/hrm/frontend/module-connect/src/routes/hrm.help.tsx`.

### RP-048 — `/hrm/import`
- Prerequisites: Verified identity and explicit company/module access.
- Produces: Destination, reference or supporting record.
- Used by: Owning business workflow.
- Direct realApi references in route source: importExportBlob, importSchemas
- Source: `modules/hrm/frontend/module-connect/src/routes/hrm.import.tsx`.

### RP-049 — `/hrm/`
- Prerequisites: Verified identity and explicit company/module access.
- Produces: Destination, reference or supporting record.
- Used by: Owning business workflow.
- Direct realApi references in route source: employees, experienceRequests, leaveRequests, payrollRuns, relationsCases, timeCorrections, workflowQueue
- Source: `modules/hrm/frontend/module-connect/src/routes/hrm.index.tsx`.

### RP-050 — `/hrm/leave/$id`
- Prerequisites: Employee, leave policy/calendar and approver.
- Produces: Leave request, decision and balance movement.
- Used by: Attendance, unpaid-leave proration and employee self-service.
- Direct realApi references in route source: decideLeaveRequest
- Source: `modules/hrm/frontend/module-connect/src/routes/hrm.leave.$id.tsx`.

### RP-051 — `/hrm/leave/allocations`
- Prerequisites: Employee, leave policy/calendar and approver.
- Produces: Leave request, decision and balance movement.
- Used by: Attendance, unpaid-leave proration and employee self-service.
- Direct realApi references in route source: adjustLeaveBalance, employees, leaveBalances, leaveTypes
- Source: `modules/hrm/frontend/module-connect/src/routes/hrm.leave.allocations.tsx`.

### RP-052 — `/hrm/leave/approvals`
- Prerequisites: Employee, leave policy/calendar and approver.
- Produces: Leave request, decision and balance movement.
- Used by: Attendance, unpaid-leave proration and employee self-service.
- Direct realApi references in route source: decideLeaveRequest, leaveRequests
- Source: `modules/hrm/frontend/module-connect/src/routes/hrm.leave.approvals.tsx`.

### RP-053 — `/hrm/leave/control`
- Prerequisites: Employee, leave policy/calendar and approver.
- Produces: Leave request, decision and balance movement.
- Used by: Attendance, unpaid-leave proration and employee self-service.
- Direct realApi references in route source: encashments, leaveRequests, leaveTypes, timeOperationsHistory
- Source: `modules/hrm/frontend/module-connect/src/routes/hrm.leave.control.tsx`.

### RP-054 — `/hrm/leave/`
- Prerequisites: Employee, leave policy/calendar and approver.
- Produces: Leave request, decision and balance movement.
- Used by: Attendance, unpaid-leave proration and employee self-service.
- Direct realApi references in route source: None in this route file; trace imported component, redirect or layout before concluding no backend.
- Source: `modules/hrm/frontend/module-connect/src/routes/hrm.leave.index.tsx`.

### RP-055 — `/hrm/leave/new`
- Prerequisites: Employee, leave policy/calendar and approver.
- Produces: Leave request, decision and balance movement.
- Used by: Attendance, unpaid-leave proration and employee self-service.
- Direct realApi references in route source: createMyLeaveRequest, employees, leaveTypes
- Source: `modules/hrm/frontend/module-connect/src/routes/hrm.leave.new.tsx`.

### RP-056 — `/hrm/leave/recorded`
- Prerequisites: Employee, leave policy/calendar and approver.
- Produces: Leave request, decision and balance movement.
- Used by: Attendance, unpaid-leave proration and employee self-service.
- Direct realApi references in route source: leaveTypes
- Source: `modules/hrm/frontend/module-connect/src/routes/hrm.leave.recorded.tsx`.

### RP-057 — `/hrm/lifecycle/alumni`
- Prerequisites: Verified identity and explicit company/module access.
- Produces: Destination, reference or supporting record.
- Used by: Owning business workflow.
- Direct realApi references in route source: None in this route file; trace imported component, redirect or layout before concluding no backend.
- Source: `modules/hrm/frontend/module-connect/src/routes/hrm.lifecycle.alumni.tsx`.

### RP-058 — `/hrm/lifecycle/assets`
- Prerequisites: Verified identity and explicit company/module access.
- Produces: Destination, reference or supporting record.
- Used by: Owning business workflow.
- Direct realApi references in route source: None in this route file; trace imported component, redirect or layout before concluding no backend.
- Source: `modules/hrm/frontend/module-connect/src/routes/hrm.lifecycle.assets.tsx`.

### RP-059 — `/hrm/lifecycle/`
- Prerequisites: Verified identity and explicit company/module access.
- Produces: Destination, reference or supporting record.
- Used by: Owning business workflow.
- Direct realApi references in route source: None in this route file; trace imported component, redirect or layout before concluding no backend.
- Source: `modules/hrm/frontend/module-connect/src/routes/hrm.lifecycle.index.tsx`.

### RP-060 — `/hrm/lifecycle/journeys`
- Prerequisites: Verified identity and explicit company/module access.
- Produces: Destination, reference or supporting record.
- Used by: Owning business workflow.
- Direct realApi references in route source: None in this route file; trace imported component, redirect or layout before concluding no backend.
- Source: `modules/hrm/frontend/module-connect/src/routes/hrm.lifecycle.journeys.tsx`.

### RP-061 — `/hrm/lifecycle/mobility`
- Prerequisites: Verified identity and explicit company/module access.
- Produces: Destination, reference or supporting record.
- Used by: Owning business workflow.
- Direct realApi references in route source: None in this route file; trace imported component, redirect or layout before concluding no backend.
- Source: `modules/hrm/frontend/module-connect/src/routes/hrm.lifecycle.mobility.tsx`.

### RP-062 — `/hrm/lifecycle/movements/new`
- Prerequisites: Verified identity and explicit company/module access.
- Produces: Destination, reference or supporting record.
- Used by: Owning business workflow.
- Direct realApi references in route source: None in this route file; trace imported component, redirect or layout before concluding no backend.
- Source: `modules/hrm/frontend/module-connect/src/routes/hrm.lifecycle.movements.new.tsx`.

### RP-063 — `/hrm/lifecycle/movements`
- Prerequisites: Verified identity and explicit company/module access.
- Produces: Destination, reference or supporting record.
- Used by: Owning business workflow.
- Direct realApi references in route source: None in this route file; trace imported component, redirect or layout before concluding no backend.
- Source: `modules/hrm/frontend/module-connect/src/routes/hrm.lifecycle.movements.tsx`.

### RP-064 — `/hrm/lifecycle/offboarding/$id`
- Prerequisites: Verified identity and explicit company/module access.
- Produces: Destination, reference or supporting record.
- Used by: Owning business workflow.
- Direct realApi references in route source: offboardWorker
- Source: `modules/hrm/frontend/module-connect/src/routes/hrm.lifecycle.offboarding.$id.tsx`.

### RP-065 — `/hrm/lifecycle/offboarding`
- Prerequisites: Verified identity and explicit company/module access.
- Produces: Destination, reference or supporting record.
- Used by: Owning business workflow.
- Direct realApi references in route source: None in this route file; trace imported component, redirect or layout before concluding no backend.
- Source: `modules/hrm/frontend/module-connect/src/routes/hrm.lifecycle.offboarding.tsx`.

### RP-066 — `/hrm/lifecycle/onboarding/$id`
- Prerequisites: Verified identity and explicit company/module access.
- Produces: Destination, reference or supporting record.
- Used by: Owning business workflow.
- Direct realApi references in route source: onboardingPlan, worker
- Source: `modules/hrm/frontend/module-connect/src/routes/hrm.lifecycle.onboarding.$id.tsx`.

### RP-067 — `/hrm/lifecycle/onboarding`
- Prerequisites: Verified identity and explicit company/module access.
- Produces: Destination, reference or supporting record.
- Used by: Owning business workflow.
- Direct realApi references in route source: employees, onboardingPlan
- Source: `modules/hrm/frontend/module-connect/src/routes/hrm.lifecycle.onboarding.tsx`.

### RP-068 — `/hrm/my-documents`
- Prerequisites: Verified identity and explicit company/module access.
- Produces: Destination, reference or supporting record.
- Used by: Owning business workflow.
- Direct realApi references in route source: myDocuments, uploadMyDocument
- Source: `modules/hrm/frontend/module-connect/src/routes/hrm.my-documents.tsx`.

### RP-069 — `/hrm/my-offboarding`
- Prerequisites: Verified identity and explicit company/module access.
- Produces: Destination, reference or supporting record.
- Used by: Owning business workflow.
- Direct realApi references in route source: myOffboarding, submitResignation
- Source: `modules/hrm/frontend/module-connect/src/routes/hrm.my-offboarding.tsx`.

### RP-070 — `/hrm/my-performance`
- Prerequisites: Verified identity and explicit company/module access.
- Produces: Destination, reference or supporting record.
- Used by: Owning business workflow.
- Direct realApi references in route source: myPerformance, submitSelfAssessment
- Source: `modules/hrm/frontend/module-connect/src/routes/hrm.my-performance.tsx`.

### RP-071 — `/hrm/my-preferences`
- Prerequisites: Verified identity and explicit company/module access.
- Produces: Destination, reference or supporting record.
- Used by: Owning business workflow.
- Direct realApi references in route source: myPreferences, updateMyPreferences
- Source: `modules/hrm/frontend/module-connect/src/routes/hrm.my-preferences.tsx`.

### RP-072 — `/hrm/my-profile`
- Prerequisites: Verified identity and explicit company/module access.
- Produces: Destination, reference or supporting record.
- Used by: Owning business workflow.
- Direct realApi references in route source: None in this route file; trace imported component, redirect or layout before concluding no backend.
- Source: `modules/hrm/frontend/module-connect/src/routes/hrm.my-profile.tsx`.

### RP-073 — `/hrm/offboarding/$id`
- Prerequisites: Verified identity and explicit company/module access.
- Produces: Destination, reference or supporting record.
- Used by: Owning business workflow.
- Direct realApi references in route source: addChecklistItem, approveOffboarding, cancelOffboarding, completeChecklistItem, createExitInterview, markFinalPay, offboardingRequest, rejectOffboarding, updateExitInterview
- Source: `modules/hrm/frontend/module-connect/src/routes/hrm.offboarding.$id.tsx`.

### RP-074 — `/hrm/offboarding/`
- Prerequisites: Verified identity and explicit company/module access.
- Produces: Destination, reference or supporting record.
- Used by: Owning business workflow.
- Direct realApi references in route source: createOffboarding, offboardingRequests
- Source: `modules/hrm/frontend/module-connect/src/routes/hrm.offboarding.index.tsx`.

### RP-075 — `/hrm/org-chart`
- Prerequisites: Company, location, department and agreed employment terms.
- Produces: Employee and assignment records.
- Used by: Leave, attendance, compensation, payroll and reporting.
- Direct realApi references in route source: orgChart
- Source: `modules/hrm/frontend/module-connect/src/routes/hrm.org-chart.tsx`.

### RP-076 — `/hrm/pay/compensation`
- Prerequisites: Employee, salary components, pay group and effective date.
- Produces: Effective-dated basic pay and allowances.
- Used by: Payroll inclusion and calculation.
- Direct realApi references in route source: createPayrollProfile, employees, entityTree, locations, payrollComponents, payrollPayGroups, payrollProfiles, setOvertimePolicy, setPayBasis
- Source: `modules/hrm/frontend/module-connect/src/routes/hrm.pay.compensation.tsx`.

### RP-077 — `/hrm/payroll/exceptions`
- Prerequisites: Eligible employees, pay profiles, period and approved variable inputs.
- Produces: Calculated/reviewed payroll, released payslips and closeout evidence.
- Used by: Employee self-service, payment and accounting reports.
- Direct realApi references in route source: payrollExceptionDecision, payrollRunLines, payrollRuns
- Source: `modules/hrm/frontend/module-connect/src/routes/hrm.payroll.exceptions.tsx`.

### RP-078 — `/hrm/payroll/`
- Prerequisites: Eligible employees, pay profiles, period and approved variable inputs.
- Produces: Calculated/reviewed payroll, released payslips and closeout evidence.
- Used by: Employee self-service, payment and accounting reports.
- Direct realApi references in route source: payrollComponents, payrollContributionRules, payrollPayGroupPeriods, payrollPayGroups
- Source: `modules/hrm/frontend/module-connect/src/routes/hrm.payroll.index.tsx`.

### RP-079 — `/hrm/payroll/queue`
- Prerequisites: Eligible employees, pay profiles, period and approved variable inputs.
- Produces: Calculated/reviewed payroll, released payslips and closeout evidence.
- Used by: Employee self-service, payment and accounting reports.
- Direct realApi references in route source: payrollQueue, shell
- Source: `modules/hrm/frontend/module-connect/src/routes/hrm.payroll.queue.tsx`.

### RP-080 — `/hrm/payroll/runs/$id/edit`
- Prerequisites: Eligible employees, pay profiles, period and approved variable inputs.
- Produces: Calculated/reviewed payroll, released payslips and closeout evidence.
- Used by: Employee self-service, payment and accounting reports.
- Direct realApi references in route source: createExperienceRequest, payrollCorrection, payrollPayGroupPeriods, payrollPayGroups, payrollRun, payrollRunLines, updatePayrollRun
- Source: `modules/hrm/frontend/module-connect/src/routes/hrm.payroll.runs.$id.edit.tsx`.

### RP-081 — `/hrm/payroll/runs/$id`
- Prerequisites: Eligible employees, pay profiles, period and approved variable inputs.
- Produces: Calculated/reviewed payroll, released payslips and closeout evidence.
- Used by: Employee self-service, payment and accounting reports.
- Direct realApi references in route source: calculatePayrollRun, legalEntities, lockPayrollRun, payrollAuditExport, payrollCalculationReadiness, payrollGenerateAllPayslips, payrollPaymentApprove, payrollPaymentFile, payrollPaymentGenerate, payrollPaymentReadiness, payrollPaymentRelease, payrollReconcile, payrollRun, payrollRunApprove, payrollRunAudit, payrollRunCancel, payrollRunLines, payrollRunPayslips, payrollRunRelease, payrollRunReverse, payrollRunStatutoryReadiness, payslipDownloadBlob, submitPayrollRun
- Source: `modules/hrm/frontend/module-connect/src/routes/hrm.payroll.runs.$id.tsx`.

### RP-082 — `/hrm/payroll/runs/new`
- Prerequisites: Eligible employees, pay profiles, period and approved variable inputs.
- Produces: Calculated/reviewed payroll, released payslips and closeout evidence.
- Used by: Employee self-service, payment and accounting reports.
- Direct realApi references in route source: createPayrollRun, employees, entityTree, legalEntities, payrollComponents, payrollContributionRules, payrollPayGroupPeriods, payrollPayGroups, payrollProfiles, payrollTaxSlabs
- Source: `modules/hrm/frontend/module-connect/src/routes/hrm.payroll.runs.new.tsx`.

### RP-083 — `/hrm/payroll/runs`
- Prerequisites: Eligible employees, pay profiles, period and approved variable inputs.
- Produces: Calculated/reviewed payroll, released payslips and closeout evidence.
- Used by: Employee self-service, payment and accounting reports.
- Direct realApi references in route source: locations, payrollRuns
- Source: `modules/hrm/frontend/module-connect/src/routes/hrm.payroll.runs.tsx`.

### RP-084 — `/hrm/payroll/salary-advances`
- Prerequisites: Eligible employees, pay profiles, period and approved variable inputs.
- Produces: Calculated/reviewed payroll, released payslips and closeout evidence.
- Used by: Employee self-service, payment and accounting reports.
- Direct realApi references in route source: cancelSalaryAdvance, createSalaryAdvance, employees, salaryAdvances, updateSalaryAdvance
- Source: `modules/hrm/frontend/module-connect/src/routes/hrm.payroll.salary-advances.tsx`.

### RP-085 — `/hrm/payslips/$id`
- Prerequisites: Eligible employees, pay profiles, period and approved variable inputs.
- Produces: Calculated/reviewed payroll, released payslips and closeout evidence.
- Used by: Employee self-service, payment and accounting reports.
- Direct realApi references in route source: myPayslipById, myPayslipDownloadBlob
- Source: `modules/hrm/frontend/module-connect/src/routes/hrm.payslips.$id.tsx`.

### RP-086 — `/hrm/payslips/`
- Prerequisites: Eligible employees, pay profiles, period and approved variable inputs.
- Produces: Calculated/reviewed payroll, released payslips and closeout evidence.
- Used by: Employee self-service, payment and accounting reports.
- Direct realApi references in route source: myPayslips
- Source: `modules/hrm/frontend/module-connect/src/routes/hrm.payslips.index.tsx`.

### RP-087 — `/hrm/people/data-quality`
- Prerequisites: Company, location, department and agreed employment terms.
- Produces: Employee and assignment records.
- Used by: Leave, attendance, compensation, payroll and reporting.
- Direct realApi references in route source: dqChecks
- Source: `modules/hrm/frontend/module-connect/src/routes/hrm.people.data-quality.tsx`.

### RP-088 — `/hrm/people/documents`
- Prerequisites: Company, location, department and agreed employment terms.
- Produces: Employee and assignment records.
- Used by: Leave, attendance, compensation, payroll and reporting.
- Direct realApi references in route source: downloadDocument, employees, uploadDocument
- Source: `modules/hrm/frontend/module-connect/src/routes/hrm.people.documents.tsx`.

### RP-089 — `/hrm/people/master-data`
- Prerequisites: Company, location, department and agreed employment terms.
- Produces: Employee and assignment records.
- Used by: Leave, attendance, compensation, payroll and reporting.
- Direct realApi references in route source: applyMasterDataBatch, dqChecks, employees, masterDataBatches, previewWorkerBulk, previewWorkerImport, reactivateWorker, rollbackMasterDataBatch
- Source: `modules/hrm/frontend/module-connect/src/routes/hrm.people.master-data.tsx`.

### RP-090 — `/hrm/people/org`
- Prerequisites: Company, location, department and agreed employment terms.
- Produces: Employee and assignment records.
- Used by: Leave, attendance, compensation, payroll and reporting.
- Direct realApi references in route source: createOrgUnit, entityTree
- Source: `modules/hrm/frontend/module-connect/src/routes/hrm.people.org.tsx`.

### RP-091 — `/hrm/people/positions/$id`
- Prerequisites: Company, location, department and agreed employment terms.
- Produces: Employee and assignment records.
- Used by: Leave, attendance, compensation, payroll and reporting.
- Direct realApi references in route source: None in this route file; trace imported component, redirect or layout before concluding no backend.
- Source: `modules/hrm/frontend/module-connect/src/routes/hrm.people.positions.$id.tsx`.

### RP-092 — `/hrm/people/positions`
- Prerequisites: Company, location, department and agreed employment terms.
- Produces: Employee and assignment records.
- Used by: Leave, attendance, compensation, payroll and reporting.
- Direct realApi references in route source: createJob, entityTree, jobs, orgUnits, updateJob
- Source: `modules/hrm/frontend/module-connect/src/routes/hrm.people.positions.tsx`.

### RP-093 — `/hrm/people/privacy`
- Prerequisites: Company, location, department and agreed employment terms.
- Produces: Employee and assignment records.
- Used by: Leave, attendance, compensation, payroll and reporting.
- Direct realApi references in route source: createRetentionRule, deleteRetentionRule, retentionRules, updateRetentionRule
- Source: `modules/hrm/frontend/module-connect/src/routes/hrm.people.privacy.tsx`.

### RP-094 — `/hrm/performance/$id`
- Prerequisites: Organisation, positions, participants and permissions.
- Produces: Goals, reviews, requisitions and hiring records.
- Used by: Employee lifecycle, organisational reporting and related workflows.
- Direct realApi references in route source: closePerformanceCycle, createPerformanceGoal, finalizeAssessment, performanceAssessments, performanceCycle, performanceCycleReport, performanceGoals, seedPerformanceAssessments, submitManagerAssessment, updatePerformanceCycle
- Source: `modules/hrm/frontend/module-connect/src/routes/hrm.performance.$id.tsx`.

### RP-095 — `/hrm/performance/`
- Prerequisites: Organisation, positions, participants and permissions.
- Produces: Goals, reviews, requisitions and hiring records.
- Used by: Employee lifecycle, organisational reporting and related workflows.
- Direct realApi references in route source: createPerformanceCycle, performanceCycles
- Source: `modules/hrm/frontend/module-connect/src/routes/hrm.performance.index.tsx`.

### RP-096 — `/hrm/recruitment/candidates/$id`
- Prerequisites: Organisation, positions, participants and permissions.
- Produces: Goals, reviews, requisitions and hiring records.
- Used by: Employee lifecycle, organisational reporting and related workflows.
- Direct realApi references in route source: advanceCandidate, recruitmentVacancies, vacancyCandidates
- Source: `modules/hrm/frontend/module-connect/src/routes/hrm.recruitment.candidates.$id.tsx`.

### RP-097 — `/hrm/recruitment/candidates`
- Prerequisites: Organisation, positions, participants and permissions.
- Produces: Goals, reviews, requisitions and hiring records.
- Used by: Employee lifecycle, organisational reporting and related workflows.
- Direct realApi references in route source: recruitmentVacancies, vacancyCandidates
- Source: `modules/hrm/frontend/module-connect/src/routes/hrm.recruitment.candidates.tsx`.

### RP-098 — `/hrm/recruitment/`
- Prerequisites: Organisation, positions, participants and permissions.
- Produces: Goals, reviews, requisitions and hiring records.
- Used by: Employee lifecycle, organisational reporting and related workflows.
- Direct realApi references in route source: None in this route file; trace imported component, redirect or layout before concluding no backend.
- Source: `modules/hrm/frontend/module-connect/src/routes/hrm.recruitment.index.tsx`.

### RP-099 — `/hrm/recruitment/offers`
- Prerequisites: Organisation, positions, participants and permissions.
- Produces: Goals, reviews, requisitions and hiring records.
- Used by: Employee lifecycle, organisational reporting and related workflows.
- Direct realApi references in route source: offerLetter, recruitmentOffers
- Source: `modules/hrm/frontend/module-connect/src/routes/hrm.recruitment.offers.tsx`.

### RP-100 — `/hrm/recruitment/operations`
- Prerequisites: Organisation, positions, participants and permissions.
- Produces: Goals, reviews, requisitions and hiring records.
- Used by: Employee lifecycle, organisational reporting and related workflows.
- Direct realApi references in route source: acceptOffer, activatePreboarding, advanceCandidate, approveOffer, createCandidate, createInterview, createOffer, createVacancy, decideInterview, issueOffer, orgTree, preboardingCases, publishVacancy, recruitmentOffers, recruitmentVacancies, updatePreboardingTask, uploadCandidateDocument
- Source: `modules/hrm/frontend/module-connect/src/routes/hrm.recruitment.operations.tsx`.

### RP-101 — `/hrm/recruitment/requisitions/new`
- Prerequisites: Organisation, positions, participants and permissions.
- Produces: Goals, reviews, requisitions and hiring records.
- Used by: Employee lifecycle, organisational reporting and related workflows.
- Direct realApi references in route source: createRequisition, entityTree, orgUnits
- Source: `modules/hrm/frontend/module-connect/src/routes/hrm.recruitment.requisitions.new.tsx`.

### RP-102 — `/hrm/recruitment/requisitions`
- Prerequisites: Organisation, positions, participants and permissions.
- Produces: Goals, reviews, requisitions and hiring records.
- Used by: Employee lifecycle, organisational reporting and related workflows.
- Direct realApi references in route source: approveRequisition, entityTree, requisitionDetail, requisitions, returnRequisition, submitRequisition
- Source: `modules/hrm/frontend/module-connect/src/routes/hrm.recruitment.requisitions.tsx`.

### RP-103 — `/hrm/recruitment/vacancies`
- Prerequisites: Organisation, positions, participants and permissions.
- Produces: Goals, reviews, requisitions and hiring records.
- Used by: Employee lifecycle, organisational reporting and related workflows.
- Direct realApi references in route source: entityTree, recruitmentVacancies
- Source: `modules/hrm/frontend/module-connect/src/routes/hrm.recruitment.vacancies.tsx`.

### RP-104 — `/hrm/relations/cases/$id`
- Prerequisites: Verified identity and explicit company/module access.
- Produces: Destination, reference or supporting record.
- Used by: Owning business workflow.
- Direct realApi references in route source: relationsCases
- Source: `modules/hrm/frontend/module-connect/src/routes/hrm.relations.cases.$id.tsx`.

### RP-105 — `/hrm/relations/cases`
- Prerequisites: Verified identity and explicit company/module access.
- Produces: Destination, reference or supporting record.
- Used by: Owning business workflow.
- Direct realApi references in route source: relationsCases
- Source: `modules/hrm/frontend/module-connect/src/routes/hrm.relations.cases.tsx`.

### RP-106 — `/hrm/relations/discipline`
- Prerequisites: Verified identity and explicit company/module access.
- Produces: Destination, reference or supporting record.
- Used by: Owning business workflow.
- Direct realApi references in route source: None in this route file; trace imported component, redirect or layout before concluding no backend.
- Source: `modules/hrm/frontend/module-connect/src/routes/hrm.relations.discipline.tsx`.

### RP-107 — `/hrm/relations/ethics`
- Prerequisites: Verified identity and explicit company/module access.
- Produces: Destination, reference or supporting record.
- Used by: Owning business workflow.
- Direct realApi references in route source: None in this route file; trace imported component, redirect or layout before concluding no backend.
- Source: `modules/hrm/frontend/module-connect/src/routes/hrm.relations.ethics.tsx`.

### RP-108 — `/hrm/relations/`
- Prerequisites: Verified identity and explicit company/module access.
- Produces: Destination, reference or supporting record.
- Used by: Owning business workflow.
- Direct realApi references in route source: None in this route file; trace imported component, redirect or layout before concluding no backend.
- Source: `modules/hrm/frontend/module-connect/src/routes/hrm.relations.index.tsx`.

### RP-109 — `/hrm/relations/labour`
- Prerequisites: Verified identity and explicit company/module access.
- Produces: Destination, reference or supporting record.
- Used by: Owning business workflow.
- Direct realApi references in route source: None in this route file; trace imported component, redirect or layout before concluding no backend.
- Source: `modules/hrm/frontend/module-connect/src/routes/hrm.relations.labour.tsx`.

### RP-110 — `/hrm/relations/operations`
- Prerequisites: Verified identity and explicit company/module access.
- Produces: Destination, reference or supporting record.
- Used by: Owning business workflow.
- Direct realApi references in route source: assignRelationsCase, createCase, createRelationsAction, declareRelationsAccess, downloadRelationsEvidence, relationsCase, relationsCases, transitionRelationsCase, updateRelationsAction, uploadRelationsEvidence
- Source: `modules/hrm/frontend/module-connect/src/routes/hrm.relations.operations.tsx`.

### RP-111 — `/hrm/relations/protected-disclosures`
- Prerequisites: Verified identity and explicit company/module access.
- Produces: Destination, reference or supporting record.
- Used by: Owning business workflow.
- Direct realApi references in route source: protectedDisclosure, protectedDisclosures, transitionProtectedDisclosure
- Source: `modules/hrm/frontend/module-connect/src/routes/hrm.relations.protected-disclosures.tsx`.

### RP-112 — `/hrm/relations/safety`
- Prerequisites: Verified identity and explicit company/module access.
- Produces: Destination, reference or supporting record.
- Used by: Owning business workflow.
- Direct realApi references in route source: None in this route file; trace imported component, redirect or layout before concluding no backend.
- Source: `modules/hrm/frontend/module-connect/src/routes/hrm.relations.safety.tsx`.

### RP-113 — `/hrm/reporting`
- Prerequisites: Authorised company-scoped persisted records.
- Produces: Filtered report or download.
- Used by: Management decisions and audit/reconciliation.
- Direct realApi references in route source: employees, reportingLines, updateReportingLines
- Source: `modules/hrm/frontend/module-connect/src/routes/hrm.reporting.tsx`.

### RP-114 — `/hrm/reports/builder`
- Prerequisites: Authorised company-scoped persisted records.
- Produces: Filtered report or download.
- Used by: Management decisions and audit/reconciliation.
- Direct realApi references in route source: None in this route file; trace imported component, redirect or layout before concluding no backend.
- Source: `modules/hrm/frontend/module-connect/src/routes/hrm.reports.builder.tsx`.

### RP-115 — `/hrm/reports/`
- Prerequisites: Authorised company-scoped persisted records.
- Produces: Filtered report or download.
- Used by: Management decisions and audit/reconciliation.
- Direct realApi references in route source: downloadManagementReport, managementReports, payrollPayGroupPeriods, payrollPayGroups, payrollRuns, statutoryPreview
- Source: `modules/hrm/frontend/module-connect/src/routes/hrm.reports.index.tsx`.

### RP-116 — `/hrm/requests/$id`
- Prerequisites: Verified identity and explicit company/module access.
- Produces: Destination, reference or supporting record.
- Used by: Owning business workflow.
- Direct realApi references in route source: addMyRequestMessage, addRequestMessage, experienceRequests, myRequest, resolveRequest
- Source: `modules/hrm/frontend/module-connect/src/routes/hrm.requests.$id.tsx`.

### RP-117 — `/hrm/requests/`
- Prerequisites: Verified identity and explicit company/module access.
- Produces: Destination, reference or supporting record.
- Used by: Owning business workflow.
- Direct realApi references in route source: myRequests
- Source: `modules/hrm/frontend/module-connect/src/routes/hrm.requests.index.tsx`.

### RP-118 — `/hrm/requests/new`
- Prerequisites: Verified identity and explicit company/module access.
- Produces: Destination, reference or supporting record.
- Used by: Owning business workflow.
- Direct realApi references in route source: createMyRequest
- Source: `modules/hrm/frontend/module-connect/src/routes/hrm.requests.new.tsx`.

### RP-119 — `/hrm/self-service`
- Prerequisites: Verified identity and explicit company/module access.
- Produces: Destination, reference or supporting record.
- Used by: Owning business workflow.
- Direct realApi references in route source: clockMyselfIn, clockMyselfOut, markAllMyNotificationsRead, myDashboard, myDocuments, myLetters, myNotifications, myPayslips, myRequests
- Source: `modules/hrm/frontend/module-connect/src/routes/hrm.self-service.tsx`.

### RP-120 — `/hrm/setup`
- Prerequisites: Company owner or authorised HR administrator.
- Produces: Calendars, contracts, statutory rules and pay periods.
- Used by: Employee setup, attendance, leave and payroll.
- Direct realApi references in route source: None in this route file; trace imported component, redirect or layout before concluding no backend.
- Source: `modules/hrm/frontend/module-connect/src/routes/hrm.setup.tsx`.

### RP-121 — `/hrm/talent/feedback`
- Prerequisites: Organisation, positions, participants and permissions.
- Produces: Goals, reviews, requisitions and hiring records.
- Used by: Employee lifecycle, organisational reporting and related workflows.
- Direct realApi references in route source: None in this route file; trace imported component, redirect or layout before concluding no backend.
- Source: `modules/hrm/frontend/module-connect/src/routes/hrm.talent.feedback.tsx`.

### RP-122 — `/hrm/talent/goals`
- Prerequisites: Organisation, positions, participants and permissions.
- Produces: Goals, reviews, requisitions and hiring records.
- Used by: Employee lifecycle, organisational reporting and related workflows.
- Direct realApi references in route source: None in this route file; trace imported component, redirect or layout before concluding no backend.
- Source: `modules/hrm/frontend/module-connect/src/routes/hrm.talent.goals.tsx`.

### RP-123 — `/hrm/talent/`
- Prerequisites: Organisation, positions, participants and permissions.
- Produces: Goals, reviews, requisitions and hiring records.
- Used by: Employee lifecycle, organisational reporting and related workflows.
- Direct realApi references in route source: None in this route file; trace imported component, redirect or layout before concluding no backend.
- Source: `modules/hrm/frontend/module-connect/src/routes/hrm.talent.index.tsx`.

### RP-124 — `/hrm/talent/learning`
- Prerequisites: Organisation, positions, participants and permissions.
- Produces: Goals, reviews, requisitions and hiring records.
- Used by: Employee lifecycle, organisational reporting and related workflows.
- Direct realApi references in route source: None in this route file; trace imported component, redirect or layout before concluding no backend.
- Source: `modules/hrm/frontend/module-connect/src/routes/hrm.talent.learning.tsx`.

### RP-125 — `/hrm/talent/reviews/$id`
- Prerequisites: Organisation, positions, participants and permissions.
- Produces: Goals, reviews, requisitions and hiring records.
- Used by: Employee lifecycle, organisational reporting and related workflows.
- Direct realApi references in route source: None in this route file; trace imported component, redirect or layout before concluding no backend.
- Source: `modules/hrm/frontend/module-connect/src/routes/hrm.talent.reviews.$id.tsx`.

### RP-126 — `/hrm/talent/reviews`
- Prerequisites: Organisation, positions, participants and permissions.
- Produces: Goals, reviews, requisitions and hiring records.
- Used by: Employee lifecycle, organisational reporting and related workflows.
- Direct realApi references in route source: None in this route file; trace imported component, redirect or layout before concluding no backend.
- Source: `modules/hrm/frontend/module-connect/src/routes/hrm.talent.reviews.tsx`.

### RP-127 — `/hrm/talent/succession`
- Prerequisites: Organisation, positions, participants and permissions.
- Produces: Goals, reviews, requisitions and hiring records.
- Used by: Employee lifecycle, organisational reporting and related workflows.
- Direct realApi references in route source: None in this route file; trace imported component, redirect or layout before concluding no backend.
- Source: `modules/hrm/frontend/module-connect/src/routes/hrm.talent.succession.tsx`.

### RP-128 — `/hrm/time/attendance/bulk`
- Prerequisites: Linked employee, location, calendar and access.
- Produces: Check events, approved time and exceptions.
- Used by: Overtime, payroll inputs and management reporting.
- Direct realApi references in route source: None in this route file; trace imported component, redirect or layout before concluding no backend.
- Source: `modules/hrm/frontend/module-connect/src/routes/hrm.time.attendance.bulk.tsx`.

### RP-129 — `/hrm/time/attendance/import`
- Prerequisites: Linked employee, location, calendar and access.
- Produces: Check events, approved time and exceptions.
- Used by: Overtime, payroll inputs and management reporting.
- Direct realApi references in route source: importOvertime, timeOperationsHistory
- Source: `modules/hrm/frontend/module-connect/src/routes/hrm.time.attendance.import.tsx`.

### RP-130 — `/hrm/time/expenses/`
- Prerequisites: Linked employee, location, calendar and access.
- Produces: Check events, approved time and exceptions.
- Used by: Overtime, payroll inputs and management reporting.
- Direct realApi references in route source: None in this route file; trace imported component, redirect or layout before concluding no backend.
- Source: `modules/hrm/frontend/module-connect/src/routes/hrm.time.expenses.index.tsx`.

### RP-131 — `/hrm/time/expenses/new`
- Prerequisites: Linked employee, location, calendar and access.
- Produces: Check events, approved time and exceptions.
- Used by: Overtime, payroll inputs and management reporting.
- Direct realApi references in route source: None in this route file; trace imported component, redirect or layout before concluding no backend.
- Source: `modules/hrm/frontend/module-connect/src/routes/hrm.time.expenses.new.tsx`.

### RP-132 — `/hrm/time/operations`
- Prerequisites: Linked employee, location, calendar and access.
- Produces: Check events, approved time and exceptions.
- Used by: Overtime, payroll inputs and management reporting.
- Direct realApi references in route source: decideOvertime, overtime
- Source: `modules/hrm/frontend/module-connect/src/routes/hrm.time.operations.tsx`.

### RP-133 — `/hrm/time/schedules`
- Prerequisites: Linked employee, location, calendar and access.
- Produces: Check events, approved time and exceptions.
- Used by: Overtime, payroll inputs and management reporting.
- Direct realApi references in route source: assignShift, closeShift, createShift, employees, roster, shifts, updateShift
- Source: `modules/hrm/frontend/module-connect/src/routes/hrm.time.schedules.tsx`.

### RP-134 — `/hrm/time/timesheets`
- Prerequisites: Linked employee, location, calendar and access.
- Produces: Check events, approved time and exceptions.
- Used by: Overtime, payroll inputs and management reporting.
- Direct realApi references in route source: attendanceSummary, decideOvertime
- Source: `modules/hrm/frontend/module-connect/src/routes/hrm.time.timesheets.tsx`.

### RP-135 — `/hrm/time/toil`
- Prerequisites: Linked employee, location, calendar and access.
- Produces: Check events, approved time and exceptions.
- Used by: Overtime, payroll inputs and management reporting.
- Direct realApi references in route source: createEncashment, decideEncashment, employees, encashmentRate, encashments, leaveTypes
- Source: `modules/hrm/frontend/module-connect/src/routes/hrm.time.toil.tsx`.

### RP-136 — `/hrm/time/travel`
- Prerequisites: Linked employee, location, calendar and access.
- Produces: Check events, approved time and exceptions.
- Used by: Overtime, payroll inputs and management reporting.
- Direct realApi references in route source: None in this route file; trace imported component, redirect or layout before concluding no backend.
- Source: `modules/hrm/frontend/module-connect/src/routes/hrm.time.travel.tsx`.

### RP-137 — `/hrm/time/utilisation`
- Prerequisites: Linked employee, location, calendar and access.
- Produces: Check events, approved time and exceptions.
- Used by: Overtime, payroll inputs and management reporting.
- Direct realApi references in route source: None in this route file; trace imported component, redirect or layout before concluding no backend.
- Source: `modules/hrm/frontend/module-connect/src/routes/hrm.time.utilisation.tsx`.

### RP-138 — `/hrm/workspace/settings/$slug`
- Prerequisites: Company owner or authorised HR administrator.
- Produces: Calendars, contracts, statutory rules and pay periods.
- Used by: Employee setup, attendance, leave and payroll.
- Direct realApi references in route source: None in this route file; trace imported component, redirect or layout before concluding no backend.
- Source: `modules/hrm/frontend/module-connect/src/routes/hrm.workspace.settings.$slug.tsx`.

### RP-139 — `/hrm/workspace/settings`
- Prerequisites: Company owner or authorised HR administrator.
- Produces: Calendars, contracts, statutory rules and pay periods.
- Used by: Employee setup, attendance, leave and payroll.
- Direct realApi references in route source: None in this route file; trace imported component, redirect or layout before concluding no backend.
- Source: `modules/hrm/frontend/module-connect/src/routes/hrm.workspace.settings.tsx`.

### RP-140 — `/`
- Prerequisites: Verified identity and explicit company/module access.
- Produces: Destination, reference or supporting record.
- Used by: Owning business workflow.
- Direct realApi references in route source: None in this route file; trace imported component, redirect or layout before concluding no backend.
- Source: `modules/hrm/frontend/module-connect/src/routes/index.tsx`.

### RP-141 — `/setup`
- Prerequisites: Company owner or authorised HR administrator.
- Produces: Calendars, contracts, statutory rules and pay periods.
- Used by: Employee setup, attendance, leave and payroll.
- Direct realApi references in route source: None in this route file; trace imported component, redirect or layout before concluding no backend.
- Source: `modules/hrm/frontend/module-connect/src/routes/setup.tsx`.

### RP-142 — `/sign-in`
- Prerequisites: Verified identity and explicit company/module access.
- Produces: Destination, reference or supporting record.
- Used by: Owning business workflow.
- Direct realApi references in route source: None in this route file; trace imported component, redirect or layout before concluding no backend.
- Source: `modules/hrm/frontend/module-connect/src/routes/sign-in.tsx`.

### RP-143 — `/speak-up`
- Prerequisites: Verified identity and explicit company/module access.
- Produces: Destination, reference or supporting record.
- Used by: Owning business workflow.
- Direct realApi references in route source: speakUp, speakUpStatus
- Source: `modules/hrm/frontend/module-connect/src/routes/speak-up.tsx`.

### RP-144 — `/team`
- Prerequisites: Verified identity and explicit company/module access.
- Produces: Destination, reference or supporting record.
- Used by: Owning business workflow.
- Direct realApi references in route source: None in this route file; trace imported component, redirect or layout before concluding no backend.
- Source: `modules/hrm/frontend/module-connect/src/routes/team.tsx`.

### RP-145 — `/workspace-access`
- Prerequisites: Verified identity and explicit company/module access.
- Produces: Destination, reference or supporting record.
- Used by: Owning business workflow.
- Direct realApi references in route source: None in this route file; trace imported component, redirect or layout before concluding no backend.
- Source: `modules/hrm/frontend/module-connect/src/routes/workspace-access.tsx`.

### RP-146 — `/hrm/workspace/settings/business`
- Prerequisites: Company owner or authorised HR administrator.
- Produces: Calendars, contracts, statutory rules and pay periods.
- Used by: Employee setup, attendance, leave and payroll.
- Direct realApi references in route source: None in this route file; trace imported component, redirect or layout before concluding no backend.
- Source: `modules/hrm/frontend/module-connect/src/routes/hrm.workspace.settings.$slug.tsx`.

### RP-147 — `/hrm/workspace/settings/holidays`
- Prerequisites: Company owner or authorised HR administrator.
- Produces: Calendars, contracts, statutory rules and pay periods.
- Used by: Employee setup, attendance, leave and payroll.
- Direct realApi references in route source: None in this route file; trace imported component, redirect or layout before concluding no backend.
- Source: `modules/hrm/frontend/module-connect/src/routes/hrm.workspace.settings.$slug.tsx`.

### RP-148 — `/hrm/workspace/settings/payroll`
- Prerequisites: Company owner or authorised HR administrator.
- Produces: Calendars, contracts, statutory rules and pay periods.
- Used by: Employee setup, attendance, leave and payroll.
- Direct realApi references in route source: None in this route file; trace imported component, redirect or layout before concluding no backend.
- Source: `modules/hrm/frontend/module-connect/src/routes/hrm.workspace.settings.$slug.tsx`.

### RP-149 — `/hrm/workspace/settings/leave-types`
- Prerequisites: Company owner or authorised HR administrator.
- Produces: Calendars, contracts, statutory rules and pay periods.
- Used by: Employee setup, attendance, leave and payroll.
- Direct realApi references in route source: None in this route file; trace imported component, redirect or layout before concluding no backend.
- Source: `modules/hrm/frontend/module-connect/src/routes/hrm.workspace.settings.$slug.tsx`.

### RP-150 — `/hrm/workspace/settings/contract-types`
- Prerequisites: Company owner or authorised HR administrator.
- Produces: Calendars, contracts, statutory rules and pay periods.
- Used by: Employee setup, attendance, leave and payroll.
- Direct realApi references in route source: None in this route file; trace imported component, redirect or layout before concluding no backend.
- Source: `modules/hrm/frontend/module-connect/src/routes/hrm.workspace.settings.$slug.tsx`.

### RP-151 — `/hrm/workspace/settings/leave-periods`
- Prerequisites: Company owner or authorised HR administrator.
- Produces: Calendars, contracts, statutory rules and pay periods.
- Used by: Employee setup, attendance, leave and payroll.
- Direct realApi references in route source: None in this route file; trace imported component, redirect or layout before concluding no backend.
- Source: `modules/hrm/frontend/module-connect/src/routes/hrm.workspace.settings.$slug.tsx`.

### RP-152 — `/hrm/workspace/settings/process`
- Prerequisites: Company owner or authorised HR administrator.
- Produces: Calendars, contracts, statutory rules and pay periods.
- Used by: Employee setup, attendance, leave and payroll.
- Direct realApi references in route source: None in this route file; trace imported component, redirect or layout before concluding no backend.
- Source: `modules/hrm/frontend/module-connect/src/routes/hrm.workspace.settings.$slug.tsx`.

### RP-153 — `/hrm/workspace/settings/compliance`
- Prerequisites: Company owner or authorised HR administrator.
- Produces: Calendars, contracts, statutory rules and pay periods.
- Used by: Employee setup, attendance, leave and payroll.
- Direct realApi references in route source: None in this route file; trace imported component, redirect or layout before concluding no backend.
- Source: `modules/hrm/frontend/module-connect/src/routes/hrm.workspace.settings.$slug.tsx`.

### RP-154 — `/hrm/workspace/settings/integrations`
- Prerequisites: Company owner or authorised HR administrator.
- Produces: Calendars, contracts, statutory rules and pay periods.
- Used by: Employee setup, attendance, leave and payroll.
- Direct realApi references in route source: None in this route file; trace imported component, redirect or layout before concluding no backend.
- Source: `modules/hrm/frontend/module-connect/src/routes/hrm.workspace.settings.$slug.tsx`.

### RP-155 — `/hrm/workspace/settings/go-live`
- Prerequisites: Company owner or authorised HR administrator.
- Produces: Calendars, contracts, statutory rules and pay periods.
- Used by: Employee setup, attendance, leave and payroll.
- Direct realApi references in route source: None in this route file; trace imported component, redirect or layout before concluding no backend.
- Source: `modules/hrm/frontend/module-connect/src/routes/hrm.workspace.settings.$slug.tsx`.

### RP-156 — `/hrm/workspace/settings/technical`
- Prerequisites: Company owner or authorised HR administrator.
- Produces: Calendars, contracts, statutory rules and pay periods.
- Used by: Employee setup, attendance, leave and payroll.
- Direct realApi references in route source: None in this route file; trace imported component, redirect or layout before concluding no backend.
- Source: `modules/hrm/frontend/module-connect/src/routes/hrm.workspace.settings.$slug.tsx`.

### RP-157 — `/core/workspace/settings/organization`
- Prerequisites: Company ownership and verified membership.
- Produces: Organisation, roles and shared configuration.
- Used by: Every module; employee placement and access.
- Direct realApi references in route source: None in this route file; trace imported component, redirect or layout before concluding no backend.
- Source: `modules/hrm/frontend/module-connect/src/routes/core.workspace.settings.$slug.tsx`.

### RP-158 — `/core/workspace/settings/branding`
- Prerequisites: Company ownership and verified membership.
- Produces: Organisation, roles and shared configuration.
- Used by: Every module; employee placement and access.
- Direct realApi references in route source: None in this route file; trace imported component, redirect or layout before concluding no backend.
- Source: `modules/hrm/frontend/module-connect/src/routes/core.workspace.settings.$slug.tsx`.

### RP-159 — `/core/workspace/settings/users`
- Prerequisites: Company ownership and verified membership.
- Produces: Organisation, roles and shared configuration.
- Used by: Every module; employee placement and access.
- Direct realApi references in route source: None in this route file; trace imported component, redirect or layout before concluding no backend.
- Source: `modules/hrm/frontend/module-connect/src/routes/core.workspace.settings.$slug.tsx`.

### RP-160 — `/core/workspace/settings/roles`
- Prerequisites: Company ownership and verified membership.
- Produces: Organisation, roles and shared configuration.
- Used by: Every module; employee placement and access.
- Direct realApi references in route source: None in this route file; trace imported component, redirect or layout before concluding no backend.
- Source: `modules/hrm/frontend/module-connect/src/routes/core.workspace.settings.$slug.tsx`.

### RP-161 — `/core/workspace/settings/branch-access`
- Prerequisites: Company ownership and verified membership.
- Produces: Organisation, roles and shared configuration.
- Used by: Every module; employee placement and access.
- Direct realApi references in route source: None in this route file; trace imported component, redirect or layout before concluding no backend.
- Source: `modules/hrm/frontend/module-connect/src/routes/core.workspace.settings.$slug.tsx`.

### RP-162 — `/core/workspace/settings/workflows`
- Prerequisites: Company ownership and verified membership.
- Produces: Organisation, roles and shared configuration.
- Used by: Every module; employee placement and access.
- Direct realApi references in route source: None in this route file; trace imported component, redirect or layout before concluding no backend.
- Source: `modules/hrm/frontend/module-connect/src/routes/core.workspace.settings.$slug.tsx`.

### RP-163 — `/core/workspace/settings/currencies`
- Prerequisites: Company ownership and verified membership.
- Produces: Organisation, roles and shared configuration.
- Used by: Every module; employee placement and access.
- Direct realApi references in route source: None in this route file; trace imported component, redirect or layout before concluding no backend.
- Source: `modules/hrm/frontend/module-connect/src/routes/core.workspace.settings.$slug.tsx`.

### RP-164 — `/core/workspace/settings/reporting-tags`
- Prerequisites: Company ownership and verified membership.
- Produces: Organisation, roles and shared configuration.
- Used by: Every module; employee placement and access.
- Direct realApi references in route source: None in this route file; trace imported component, redirect or layout before concluding no backend.
- Source: `modules/hrm/frontend/module-connect/src/routes/core.workspace.settings.$slug.tsx`.

### RP-165 — `/core/workspace/settings/general`
- Prerequisites: Company ownership and verified membership.
- Produces: Organisation, roles and shared configuration.
- Used by: Every module; employee placement and access.
- Direct realApi references in route source: None in this route file; trace imported component, redirect or layout before concluding no backend.
- Source: `modules/hrm/frontend/module-connect/src/routes/core.workspace.settings.$slug.tsx`.

### RP-166 — `/core/workspace/settings/transaction-number-series`
- Prerequisites: Company ownership and verified membership.
- Produces: Organisation, roles and shared configuration.
- Used by: Every module; employee placement and access.
- Direct realApi references in route source: None in this route file; trace imported component, redirect or layout before concluding no backend.
- Source: `modules/hrm/frontend/module-connect/src/routes/core.workspace.settings.$slug.tsx`.

### RP-167 — `/core/workspace/settings/pdf-templates`
- Prerequisites: Company ownership and verified membership.
- Produces: Organisation, roles and shared configuration.
- Used by: Every module; employee placement and access.
- Direct realApi references in route source: None in this route file; trace imported component, redirect or layout before concluding no backend.
- Source: `modules/hrm/frontend/module-connect/src/routes/core.workspace.settings.$slug.tsx`.

### RP-168 — `/core/workspace/settings/email-notifications`
- Prerequisites: Company ownership and verified membership.
- Produces: Organisation, roles and shared configuration.
- Used by: Every module; employee placement and access.
- Direct realApi references in route source: None in this route file; trace imported component, redirect or layout before concluding no backend.
- Source: `modules/hrm/frontend/module-connect/src/routes/core.workspace.settings.$slug.tsx`.

### RP-169 — `/core/workspace/settings/web-tabs`
- Prerequisites: Company ownership and verified membership.
- Produces: Organisation, roles and shared configuration.
- Used by: Every module; employee placement and access.
- Direct realApi references in route source: None in this route file; trace imported component, redirect or layout before concluding no backend.
- Source: `modules/hrm/frontend/module-connect/src/routes/core.workspace.settings.$slug.tsx`.



## Targeted deployed correction retests — 20 November 2026

API12ca4a95/webffeb04a6 are live and pushed. Owner HR Core organisation/central Team entry, live company labels/date, ISO compensation same-value save/reopen and readable payroll run passed.16 reload/navigation observations had no reconnect; sampled session log31 HTTP200/no429 plus one cancellation500. Rapid-navigation cancellation errors are retained. Immediate disk headroom3233.4MiB; both health hosts200 and backup timeractive. Original route counts/untested cases stay unchanged; Core entity/location edit CRUD, Procurement-only Core, negative actors, full compensation history, closeout and restore/growth are not qualified. See [exact deployment evidence and remaining gates](PUBLIC-LAUNCH-READINESS-20261120.md). No blanket launch acceptance.
