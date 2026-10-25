# Core settings frontend release candidate: source verification

Source `7a1c63b613500ea61837dfdc47240860c5313e6d` is an isolated frontend release candidate based on the live sign-in/Accounting entry source `716b7605ef574f20dc07490cb6401735211eb495`. It restores `/core/workspace/settings` and its connected branding, users, organization, branch access and roles pages, with redirects from the earlier Core URLs. The sign-in, ERP module chooser and Accounting entry product files are byte-identical to that live baseline. No frontend or API deployment has occurred.

## Checks

- Vite/Nitro production build passed on source `02b297d8120aa09ae9b812ea2a1bb91eed3f6193`; the subsequent two commits change only browser test heading assertions.
- Mocked Chromium checks passed: 8/8 role catalog cases, 4/4 Core settings/legacy/access cases, and 5/5 sign-in/Accounting entry cases. The sign-in total consists of four initial passes and one corrected single-case rerun. Browser API reads and writes were intercepted by local fixtures; no production authentication or business operation was tested.
- The first Core route run retained one failed assertion expecting `Core settings`; the page correctly rendered `ERP Core settings`. The first sign-in run retained one failed assertion expecting `Organization & Settings`. Both test assertions were corrected. The original failed logs and traces remain in private evidence.
- Full TypeScript checking exited 2 with 23 errors in unchanged live-baseline HRM files. A scoped diagnostic found zero errors in changed release files. **Full TypeScript checking has not passed.** No unrelated HRM code was copied into this release to clear those baseline errors.

Private evidence: `/home/amizo/erp-backups/core-settings-live-release-ui-validation-20261025_run01/result.private.json`, SHA-256 `eb7f21307d3f0f6eba039abca5d54040263f30344ed440f007f640a3db6e4c9b`. That receipt pins the build, TypeScript, initial failed browser runs, corrected reruns and offline sign-in fixture hashes without credentials or personal data.

## Release dependency

The currently deployed P24 API does not expose the new admin-only `GET /api/core/v1/role-permissions` catalog. The Procurement role editor deliberately closes permission editing if that catalog is missing or invalid; it does not fall back to the former 21-item static list. The paired backend catalog and authorization-parity source must pass its own validation and be deployed before this frontend can offer Procurement grant editing. This note records source and mocked-browser verification only; it does not claim live API compatibility, role-grant acceptance or release readiness.
