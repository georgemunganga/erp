# Department material and service request entry

Change: ERP issue204 and UI issue112. This is a presentation improvement to the existing Procurement purchase-request workflow, not a second material-request record or approval engine.

## Behaviour and boundaries

The ERP module chooser and HRM Home reuse one permission-aware shortcut. It checks the actual current principal/company context, Procurement access/read/create permissions and the server draft capability. Core identity and organisation stay authoritative; no employee linkage is required by the shortcut. Server APIs remain authoritative for every action and record scope.

Procurement Home and My Requests open `/procurement/workspace/requests/new?entry=department`. The existing editor explains the internal need → draft → send for review flow. Optional cost centre, project, funding and approver notes remain available under More request details. Department entry starts with an empty estimate so an unknown cost is not silently saved as zero. Existing draft serialization, concurrency handling, branch selection, submit and approval routes are unchanged. Supplier sourcing and purchase orders happen later.

This bounded change does not add on-behalf-of requests, automatic department assignment, stock issues/transfers, file upload or unknown-price persistence. Current request APIs require a valid estimate. Uploaded item masters are not an approved priced catalog. Company workflow policy and designated approvers must be configured before a real approval demonstration.

## Verification

Both frontend TypeScript checks and production builds passed. Two Core display-authority tests passed. Three Core Playwright cases verify a workerless requester, mobile/desktop layout and missing create permission. Four guided-entry cases verify estimate validation, one existing request POST with exact decimal serialization, optional details, denied permission and mobile/desktop layout. Ten existing Procurement Home cases passed. These browser cases use API fixtures; they are not production business UAT. All-branch regression and actual read-only deployed walkthrough receipts are recorded separately in the release evidence.

Private execution evidence: `/home/amizo/erp-backups/material-request-entry-20261109_run01`. No customer records or credentials belong in this document or Git. Deployment is frontend-only; no schema, production business records, approval policy or role changes are authorized by this feature.
