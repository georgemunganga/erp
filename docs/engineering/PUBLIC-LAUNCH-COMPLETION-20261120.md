# Public-launch completion test report — 20 November 2026

Current outcome: the expanded automated and available live-ERP pass is complete. Full public-launch sign-off remains HOLD. A rendered page is not a completed CRUD workflow, and unavailable editors are not passed features.

## Deployed fixes and source

- API679b01c179ff7d9ae8d5559c3ffa50f2498d17d3: invariant attendance, leave, correction and roster date/time DTOs, preserving deployed authority. This overlays API12ca4a95 on d41303dd + ownerf081d5a3; it does not deploy unrelated current-branch backend work.
- Webcf64a06f3676064c12fa6e64af061484c5b52c13: employee Pay and statutory reads the actual effective payroll profile/group rather than an intentionally blank worker projection. Loading, unavailable permission, absent group and ambiguous profiles are distinct. A payroll review link to the release-disabled run editor is no longer offered. The editor itself remains excluded; no financial editing authority was enabled.
- API/web/proxy: kuubu-launch-completion01-api01 / kuubu-launch-completion02-web01 / kuubu-launch-completion02-proxy01;29009/29011. Configuration and backup25 live in /opt/kuubu-production/releases/launch-completion-20261120-web02. Prior containers/publications are retained, with replaced API/web stopped.
- Laravel source8e6c3a9:99 tracked app/config/routes/migration/lock files match the effective central-server release after line-ending normalization. The older root release.json is stale and was not treated as effective deployment authority. No Laravel or Perfex production code/config was changed in this pass.

## Executed checks

| Layer | Result | Scope |
|---|---|---|
| Full deployed-composite ERP suite |2891 passed,52 skipped,0 failed |2943 discovered cases; original aborted failing-format run retained. Final run took11m24s. |
| Additional actual PostgreSQL run |15 passed,0 skipped |Subset previously skipped: team-command races, invitation boundaries, module grants, session evidence and native owner/membership constraints. Runs in a separate PostgreSQL container, never production. These15 are not added to the discovered-test count. |
| HR-filtered suite |2568 passed,52 skipped |Diagnostic run excluding the large Procurement HTTP class; not additive to full-suite totals. |
| Frontend contracts |157 passed |Includes effective pay-group display, other-worker/future/ended profiles, ambiguous groups, import mapping and existing product/session/team contracts. |
| Frontend build/TypeScript |Passed |Final web build02; immutable web publication01. |
| Laravel unit/features |166 passed,1 skipped;1377 assertions |SQLite memory database, array mail/session/cache, provider HTTP fakes. Includes identity, customers, module subscriptions, billing and Lenco state handling; no real charge. |
| Database restore |Passed |Fresh dump restored to separate database;110 Core/HR tables, no row-count differences at comparison. |
| Restored application |Passed |Private29010 readiness200; anonymous mobile and HR401. Delivery/accrual workers disabled, protected keys mounted read-only, writable mounts isolated. Test container stopped. |
| Actual payroll PDF |Passed |Synthetic SQLite-memory create/lock/calculate/approve/release, then actual PayslipDocumentServiceImpl + installed WeasyPrint in current runtime image with network disabled. PDF signature/text and one-page rendered image inspected; gross30000/deductions10476/net19524. No production release, bank payment or statutory submission. |
| Production operations |Passed immediate checks |Both API hosts healthy200, backup25 execution success and timer active. Final root free2338.3MiB; future capacity/alerts remain an operational gate. |

The 15 native passes reduce the52 environment-dependent skips to37 not exercised by the additional run. Provider/browser/process and SMTP fixture tests remain explicit; a skip is not acceptance evidence. The Laravel skipped test is also not a pass.

## Live workflow observations

- The original66 route observations remain immutable. All81 originally unvisited static entries now have a new retained observation; some are intentional exclusion, planned editor or access/loading captures. This is147 static/alias route observations, not147 successful business workflows. Contextual employee/profile/payroll checks are retained separately; other record-specific flows remain unqualified.
- Contract type: create, reopen, edit notice period14/30 to14/21, archive. Designation: create, edit title, archive. Leave type: create entitlement3, edit4, archive. All were named QA records in the import trial; default masters and worker leave balances were unchanged. No test master was assigned.
- Existing imported employee: manually edited preferred name, reloaded and observed persistence. Contract, start date, location and salary remained intact. Profile tabs were inspected; saved pay group is now displayed correctly in the live Pay and statutory tab.
- Completed HR setup displays the next steps to add/import employees, set compensation and review first payroll. It does not pretend missing bank/statutory information is complete.
- Previously qualified manual and import first-calculation paths remain calculated/unpaid: one employee each, gross5000/deductions300/net4700. Final SQL evidence is hr-first-run-20261118-01/launchcompletionfinal03.private.json. No production approval/release/payment was performed.
- The earlier rapid-navigation cancellation500 logs remain retained. A normal session/read-only compensation path succeeded after the API upgrade; this is not a claim of all-network-condition resilience.

## What still prevents a blanket public-launch claim

1. The Kuubu website browser shows sign-in. George's existing ERP session remains usable, but it cannot substitute for a fresh website identity session. Fresh live account/subscription/team/company switching and payment-modal acceptance is pending interactive sign-in; the user replied continue, not a verified successful sign-in. No identity bypass, Google/email merge or impersonation was used.
2. Six Core editors are explicitly planned: reporting tags, general defaults, numbering, PDF templates, email notifications and web tabs. Core directory already labels them planned. Entity/location editing is read-only in the current organisation view; tested create/close paths do not qualify all mutations. These are implementation/scope gaps, not test passes.
3. Procurement-only Core entry has not been implemented/qualified by this HR-session provider. The commercial rule remains Core included with one valid module/trial; no Core fee was added, and no HR entitlement is granted to solve another module's entry.
4. Remaining record-specific CRUD, actor/browser and provider recovery combinations need their corresponding records/fixtures. Automated policy/native evidence does not certify every UI action. Real-money acquisition was not performed; mocked payment lifecycle is separate from merchant live acceptance.
5. Disk growth/alerts and retained-release capacity need an operating decision before broad public admission. Current restore/startup/backup checks passed, but2338MiB is finite headroom.

## Retained packet

Raw synthetic/private evidence is inspection/qualifications/public-launch-completion-20261120-01. Original failed and partial attempts are retained. Native connection attempts failed because the SSH key prohibits forwarding; the successful successor ran inside the server without changing SSH policy or exposing PostgreSQL. A first restored-app harness omitted its key directory; a fresh successor mounted it read-only. A first PDF harness omitted SQLite native dependencies; a fresh successor supplied them. No prior publication was rewritten to make a check pass.

The workflow gates in PUBLIC-LAUNCH-READINESS-20261120.md remain authoritative. This report does not publish a new ERP main baseline or certify Procurement, Vcron or on-premise upgrades.
