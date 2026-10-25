# Settings & Reports workflow depth

**Scope:** local, deterministic Books workflow definitions for organization settings, templates, Documents, Reports, automation, and integration setup simulation. The renderer supplies review, back, cancel, and completion controls. No workflow sends mail, creates credentials, starts a schedule, calls a provider, or changes a tenant.

| Flow ID | Steps | Validation | Persisted / linked result | Evidence |
|---|---|---|---|---|
| `settings-organization` | Organization details; address and locale | Required organization name/email, supported country/currency/time zone | Organization profile artifact in `settings` | local-concept |
| `settings-branding` | Brand assets; document presentation | Required colors and placement; optional local attachment/footer | Branding configuration artifact in `settings` | local-concept |
| `settings-user-access` | User identity; access and notifications | Required name/email; role and local status selected | User access entry in `settings`; no invitation | local-concept |
| `settings-role-access` | Role details; permissions | Required role name/description and permission selection | Role permission entry in `settings` | local-concept |
| `settings-general-preferences` | Workspace preferences; transaction presentation | Work-week, tax mode, rounding, and display choices selected | General preference artifact in `settings` | tenant-pattern (settings → General entry structure observed) |
| `settings-number-series` | Series selection; sequence rules | Series/prefix and positive next number required | Number-series configuration in `settings` | local-concept |
| `settings-terms` | Term details; availability | Required name and non-negative due days | Payment-term artifact in `settings` | local-concept |
| `settings-reminders` | Reminder stages; reminder content | Required stage days and preview subject/message | Reminder configuration in `settings`; no send or schedule | local-concept |
| `create-template` | Template identity; template content | Required name/type/layout and visible sections | New template artifact in `templates` | tenant-pattern (template index New affordance observed) |
| `edit-template` | Choose template; update presentation | Required `templates` record selection and visible sections | Selected template artifact edited in `templates` | tenant-pattern (template index Edit affordance observed) |
| `set-default-template` | Template family; default selection | Required template record and confirmation checkbox | `templates` artifact-status action `set-default` | tenant-pattern (default marker observed in template index) |
| `create-folder` | Folder details; folder notes | Required folder name/type; optional same-collection parent folder | New folder artifact in `folders` | tenant-pattern (Documents Create New Folder entry observed) |
| `upload-document` | Document file; organization | Required attachment/name/type; optional same-index folder | New document artifact in `documents`; no external upload | tenant-pattern (Documents index and folder navigation observed) |
| `process-receipt` | Receipt selection; review extracted values | Required document record, destination, date, and non-negative amount | Local document-process link to an `expenses` draft; no OCR/posting | local-concept (receipt inbox was observed, processing was unverified) |
| `save-report-filter` | Report definition; filters | Required name/report/basis and date range | Saved report artifact in `reports` with local filters | local-concept (report catalog/filter entry observed; saving was unverified) |
| `share-report` | Report selection; access level | Required saved-report record, fictional email, and access level | Share entry in `reportShares`; no email or permission grant | local-concept |
| `schedule-report` | Schedule target; timing | Required report/email/frequency/day | Schedule metadata in `reportSchedules`; no background job or delivery | local-concept |
| `create-automation-rule` | Rule trigger; criteria | Required name/module/event/criteria; plain-language criteria only | Rule artifact in `automationRules` | tenant-pattern (Workflow Rules New/filter entry structure observed) |
| `create-automation-action` | Rule and action; action details | Required same-collection rule, action, title/message, and local target | Action artifact in `automationActions` | tenant-pattern (Automation action categories/sidebar observed; creation unverified) |
| `test-automation-rule` | Rule and sample record; expected outcome | Required same-collection rule, sample name/status/amount, expected outcome | Local `automation-test` preview; no custom code/network/action execution | local-concept |
| `view-automation-log` | Log selection; review note | Required same-collection rule, outcome, and details | Review artifact in `automationLogs` | tenant-pattern (View Logs entry observed; log mutation unverified) |
| `setup-integration` | Integration choice; scope and safeguards | Required provider/name/mode/scopes; disabled by default | Disabled simulation artifact in `integrations`; no credentials/auth/provider call | local-concept |

## Contract notes

- Every flow belongs to the `Settings & Reports` domain and uses only `FlowDefinition`, `FlowStep`, `FlowField`, and `FlowEffect` values from `src/workflows/contracts.ts`.
- Collection-backed settings, templates, Documents, folders, reports, shares, schedules, automation, logs, and integrations use `artifact` or `artifact-status` effects. Template edits select a `record` from the same `templates` collection; automation tests select a rule from `automationRules` and provide sample fields.
- The receipt workflow uses `document-process` with a document record and `expenses` target. The `expenses` module is the only typed module used because it is the destination draft represented by the local process.
- Evidence is marked `tenant-pattern` only where the supplied evidence showed the relevant index/entry affordance or settings structure. Specific unverified mutations (report sharing/scheduling, receipt processing, integration setup, credentials, and rule execution) are marked `local-concept`.
