# Procurement saved-branch hand-off hotfix

## Incident and cause

The user reported `Choose a valid ERP branch before continuing.` after the
P22 release. The shared HRM shell accepts an older saved branch **name** in
`erp.shell.state.v1`. Procurement's transaction adapters correctly require a
branch UUID. Its readiness request previously omitted the invalid label,
allowing the initial gate to pass before list requests failed locally.

The earlier PostgreSQL/API/browser release evidence used synthetic outer
authentication and shell selection. It did not establish that the user's
actual signed-in browser and inherited shell state worked. That acceptance
gap is retained explicitly; the earlier counts are not live-account proof.

## Boundaries of the correction

- Resolve only a unique, exact branch-name match from the current authorized
  Core locations in the explicitly selected company.
- Persist its actual ID before Procurement readiness or transaction screens
  run. Retain unrelated shell preferences.
- Block unknown, ambiguous, malformed, unavailable, or unsavable selections.
  Keep the existing header's company/branch picker usable for explicit recovery.
- Never infer a different company, pick the first branch, clear the branch to
  widen scope, or change server permissions.
- A bounded, server-verified current-context display is available when Core
  administration lists are forbidden; it does not grant organization browsing.
- Preserve authentication/scope invalidation, stale-response rejection, and
  all existing backend authorization.

## Actual live reproduction

One login with the user's previously authorized credentials succeeded. No
password reset, role grant, employee link, or policy change was made.
The actual Core company/location/shell endpoints returned HTTP 200.
An unmocked Playwright browser with this real session and a real branch name
saved in shell storage reproduced the user's exact error before the fix.
The same real session could read Procurement context, vendors and requests
with the valid UUID headers (HTTP 200), establishing the client hand-off fault.

Protected operational evidence is retained under
`/home/amizo/erp-backups/procurement-live-branch-hotfix-20261022`.
Credentials, cookies, account data, and screenshots are not committed.

## Verification and deployment

UI PR [#51](https://github.com/georgemunganga/module-connect/pull/51) was
normally merged as `8b980800a4a65501f98cc38d927fd51ee33b611f`. Application
source is `fe8c5b351abf3b8889dcff7999ef73aeec60fbf6`; final test-only source
is `4c19a295551107f64e3c461fd2123802c38e1c3a`. Their application source is
byte-identical. No backend, schema, account, role or policy changed.

- 67 targeted unit tests passed across four files.
- Node 22.23.3 TypeScript and live production build passed. An earlier
  unsupported-Node build is retained but is not accepted release evidence.
- All six focused Playwright cases passed together: one worker, zero
  retries, skips or flaky cases. The first attempt retained five passes and
  one whole-test timeout during cold Vite loading. Its trace established
  that the first navigation consumed about 25 seconds of a 30-second
  multi-navigation test. Only that test's overall limit was increased to
  90 seconds; its assertions were preserved. The final positive case took
  33.9 seconds and the complete run took 69.5 seconds.
- The exact five-file frozen build was deployed to the existing static
  alias, with assets copied first and index replaced atomically last.
  Existing assets were retained, and the previous static tree was backed up.
- Public HTML, JavaScript and CSS bytes matched the frozen build exactly.
- An actual unmocked live Playwright browser using the user's real session
  passed saved-name migration, Vendors and My Requests HTTP 200 with the
  canonical branch header, reload, and both new-record forms. No browser
  errors occurred. This was a read/form acceptance check: no live business
  records were created or changed, and it is not complete CRUD/UAT proof.

The immediate reported branch incident is resolved in the deployed build.
The full Procurement operational and Finance milestones remain incomplete.

Frozen application assets: `index-CUxR6Tlo.js`, `index-B_T57PCm.css`.
Frozen tree hash:
`c2e8dd09b1c28ea2e488973662bf4c09f1b87e55e76885720cca22f017c9cebf`.
Protected evidence includes `deployment.private.json`,
`live-after.private.json`, frozen manifest, actual live screenshots,
final unit/build/browser logs, and the retained first timeout trace.
