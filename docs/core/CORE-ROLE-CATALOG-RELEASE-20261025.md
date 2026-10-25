# Core permissions and settings release — 25 October 2026

The Core role editor now reads the running server's Procurement action catalog. It preserves grants on name/status edits and no longer displays phantom Procurement grants for empty or unrelated legacy role rows. Core settings directory, branding, organization, users, roles and branch-access URLs now resolve under `/core/workspace/settings`. Existing Core URLs redirect to these pages. The supplied Procurement layouts and Accounting preview remain in place.

Production uses the compatible P24 backport `7a37b4b9` with its original **67** action keys, image `3a6ba254b9d6`. The development branch has 91 keys; its newer Procurement model/host has not been deployed. No role grants, database migrations, feature flags or Finance posting capabilities were added by this release. Genuine publication contained the same 150 paths; only the API/application DLLs and PDBs changed. Models, dependencies and other published bytes matched the accepted P24 publication.

## Verification

The development fix passed 15 focused tests, all 1,364 Host regressions, strict TypeScript, the frontend build and eight built-app browser cases. The production backport passed 10 focused tests. Its full Host run passed 1,006 cases and failed three source-layout checks; the exact Git API source was supplied beside the unchanged test assembly and those same three case IDs then passed. This is **1,009 cases covered across the original run and supplement**, not a relabelled single passing full run. Original failed attempts remain retained.

The compatible frontend built successfully. All 17 mocked Core/role/sign-in browser cases passed locally and again against deployed public assets. Twelve real anonymous HTTP probes and actual container checks confirmed settings routes, sign-in, Procurement and Accounting, catalog/admin denial, API health and publisher stability. Browser login/reset/role writes were intercepted; these checks changed no business records and do not replace real-account business UAT.

The compatible frontend retains 23 TypeScript errors in unchanged legacy HRM files; none are in release changes. Its full TypeScript check is not reported as passing. The canonical development source passes the full check. Keeping this release bounded avoids importing unrelated HRM behavior changes.

## Operations and recovery

The first paired cutover checked frontend readiness too early and automatically restored both original writers and the prior web override. A bounded readiness wait was added and independently reviewed; the second cutover succeeded. The old containers are retained for exact rollback, and nginx's Procurement/Accounting aliases remain unchanged.

The reviewed [cutover tool](../../tools/releases/core_role_catalog_p24_cutover.py) is archived in source control. It requires an exact private reviewed plan, genuine publication/image proof, original configuration hashes and frontend artifact inventory. It never logs or saves environment values. Operational package: `core-role-catalog-paired-cutover-prep-20261025_run02`; rollback uses its existing `rollback` mode and exact saved plan hash, not a newly generated plan or a blind Compose deployment. Do not replay the completed cutover.

An inactive build cache was archived losslessly after all 1,269 file hashes were verified; original logs and accepted parent publications remain intact. No ERP data, Frappe, dependencies or browser installation was removed.

[Machine-readable release receipt](CORE-ROLE-CATALOG-RELEASE-20261025.json) records source, image and evidence hashes. Real-account role CRUD/data-scope UAT, HRM module settings routes and the remaining Procurement findings stay on the gap roadmap.
