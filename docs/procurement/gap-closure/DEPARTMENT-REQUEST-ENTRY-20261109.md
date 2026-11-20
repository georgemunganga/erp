# Department material and service request entry

Change: ERP issue204 and UI issue112. This is a presentation improvement to the existing Procurement purchase-request workflow, not a second material-request record or approval engine.

## Behaviour and boundaries

The ERP module chooser and HRM Home reuse one permission-aware shortcut. It checks the actual current principal/company context, Procurement access/read/create permissions and the server draft capability. Core identity and organisation stay authoritative; no employee linkage is required by the shortcut. Server APIs remain authoritative for every action and record scope.

Procurement Home and My Requests open `/procurement/workspace/requests/new?entry=department`. The existing editor explains the internal need → draft → send for review flow. Optional cost centre, project, funding and approver notes remain available under More request details. Department entry starts with an empty estimate so an unknown cost is not silently saved as zero. Existing draft serialization, concurrency handling, branch selection, submit and approval routes are unchanged. Supplier sourcing and purchase orders happen later.

This bounded change does not add on-behalf-of requests, automatic department assignment, stock issues/transfers, file upload or unknown-price persistence. Current request APIs require a valid estimate. Uploaded item masters are not an approved priced catalog. Company workflow policy and designated approvers must be configured before a real approval demonstration.

## Verification

Both frontend TypeScript checks and production builds passed. Two Core display-authority tests passed. Three Core Playwright cases verify a workerless requester, mobile/desktop layout and missing create permission. Four guided-entry cases verify estimate validation, one existing request POST with exact decimal serialization, optional details, denied permission and mobile/desktop layout. Ten existing Procurement Home cases passed. These browser cases use API fixtures; they are not production business UAT. All-branch regression and actual read-only deployed walkthrough receipts are recorded separately in the release evidence.

Private execution evidence: `/home/amizo/erp-backups/material-request-entry-20261109_run01`. No customer records or credentials belong in this document or Git. Deployment is frontend-only; no schema, production business records, approval policy or role changes are authorized by this feature.

## Deployed receipt

ERP PR205 merged as `619c241e1dcf6f9f2b4b376c06f7ae05c1efcbeb`; UI PR113 merged as `657d9acb7c03f47c224ae287b901ee8d303d0ab8`. Both frontends are deployed from these clean, remote-current sources into exclusive publications. The original older checkout has substantial unrelated uncommitted/divergent work and was preserved rather than overwritten or rebased. Unrelated remote Kuubu branches remain separate.

Six all-branch Playwright regression cases passed, bringing the focused fixture browser checks to23. An actual owner-cookie, read-only Chromium walkthrough passed8 checks across ERP Home, explicit Lusaka branch choice, guided request form/optional fields,1440/390 widths, HRM Home, Procurement Home and My Requests. No page errors or business writes occurred. The owned verification session was logged out; no recipient sessions were revoked. All1243 current/retained public asset URLs matched their publication hashes; the Procurement index matched exactly. Backend readiness remains healthy; API image/schema/configuration are unchanged by this frontend rollout.

The first live resize assertion sampled the sidebar transition before settling; its failure receipt remains preserved. A bounded polling assertion against the settled viewport passed. Wide line tables retain their own horizontal scroll. An earlier fixture assertion was corrected to expect the existing exact decimal string, and one browser-cache-path invocation was rerun with the installed browser path. These failures are not hidden or counted as passes.

Core publication: `/home/amizo/erp-material-request-release-619c241e-20261109`; Procurement publication: `/home/amizo/procurement-ui/releases/material-request-657d9acb-20261109`. The new Core systemd50 override selects the exclusive publication. Nginx retains old asset contents alongside new ones. Rollback removes only the50 override, restores the prior HTML alias while retaining the new asset pools, validates Nginx and restarts/reloads services. Private manifests, exact configuration candidates/rollback, source pins, logs and signed-in screenshots are in the evidence directory above. No claim of whole-module completion or company-policy UAT is made.

## Custom Core role entry correction (issue207)

ERP admission and the Procurement module card also accept a current-principal/default-company `procurement-access` response from the existing server context endpoint. A custom role name by itself grants nothing; failed, mismatched and malformed responses fail closed. Cookie/OIDC authentication is reused, requests are aborted on identity/company/session changes, and response authority is keyed to that context. HRM and Core administration gates remain independent. This fixes custom request-only roles without assigning broader built-in roles. Server APIs still enforce their individual permissions and own-record scope.

Core TypeScript and a fresh production build passed. Three authority unit tests and five focused fixture Playwright cases passed, including custom-role admission, custom-role forbidden response, current requester and mobile layouts. No recipient password is set or tested by these fixtures; actual recipient password setup remains their action. Private operational receipts and rollout checks are retained under `/home/amizo/erp-backups/frontdesk-request-only-20261109_run01`.
