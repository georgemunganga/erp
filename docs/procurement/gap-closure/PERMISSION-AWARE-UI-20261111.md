# Permission-aware Procurement UI — deployed 11 November 2026

The live workspace showed the entire Procurement menu and Home sections to a request-only Core role. Server grants were correct, but presentation and some page-entry checks did not reflect them. [UI PR115](https://github.com/georgemunganga/module-connect/pull/115) closes UI issue114. UI main `91d165f1cf2f8d7af578ebffd5ef8adbba0ad39f` is deployed; Core frontend code remains `52bdb5a0c36778a2f082e8e4085d65cbff25df7e`. API image, schema, business flags, role grants, company policies and business records are unchanged.

## Result and ownership

The existing scoped server permissions drive a shared UI page-entry contract. Sidebar groups, quick-create actions, workflow links, account/settings links and Home cards/panels hide ungranted destinations. A request-only role sees Home, My Requests and its request action. Forbidden direct URLs are denied before branch prompts or domain-data components mount; the shared shell remains available for recovery. Authorized users retain the imported layout and shared ERP controls. Capability availability messages remain separate from authority.

Existing narrow workflows are preserved: opening approval work without assigned-queue reading, assigned PO detail without broad PO listing, and receiving inspection/acceptance/certification queues/tasks without general record viewing. API guards continue to enforce individual action, assignment, state, tenant/company/branch and owner scope. This is not a second role engine or a permission grant. See UI `reference-ui/PERMISSION_AWARE_WORKSPACE.md` and its shared page-access matrix.

## Verification and limits

Final TypeScript and live-mode production build passed. Six permission-matrix unit cases passed. The full browser regression initially passed97/98; a normal receiving detail-purpose case exposed a route guard mismatch. After correction, all9 receiving cases passed. Request-only/menu/URL cases were rerun18/18 after reverting a test-only invalid mobile click coordinate. Across the regression and focused reruns, all98 distinct case identities have passing evidence, with earlier failures/interrupted runs retained. This is not a claim that a fresh single98-case run was repeated after the last focused correction.

Actual read-only Chromium verification passed26 checks using restricted and broader signed-in accounts: desktop/mobile menu and Home filtering, quick-create/account links,17 forbidden direct routes without domain-data requests or branch prompts, continued guided request entry, exact request-only effective permissions, server403s for vendors/items/orders, and retained broader navigation/Home panels. There were no page errors or business writes. Both verification sessions logged out; native active count for the owned user-agent was0. Recipient/human sessions were preserved. Fixture persistence and read-only observation do not replace company business UAT.

All11 current/retained Procurement public file hashes matched, and the publication manifest was unchanged after rollout. Backend readiness remained healthy. Private evidence: `/home/amizo/erp-backups/procurement-permission-ui-20261111_run01` (source hashes, manifests, exact configurations, logs, browser case identities and private screenshots). No credentials/customer records belong in Git.

## Publication and rollback

Publication: `/home/amizo/procurement-ui/releases/permissions-91d165f1-20261111_run01`. It contains the new build and independent copies of the prior active assets; the older historical fallback and Core asset root remain intact. Existing publications were not mutated.

The first immediate post-reload index check observed the old page and rolled back the HTML alias while retaining new asset URLs. A qualified continuation verified the complete unchanged publication, activated only its alias and retried the index check through Nginx's graceful handover; the exact new index was observed. No restaging or backend deployment occurred. For rollback use the private `nginx-rollback-keep-assets.conf`, validate Nginx and reload: it selects prior HTML while preserving every published asset URL. Initial/continuation scripts and observations remain in evidence.

Company approval-policy configuration, Finance integration and wider full-module UAT holds remain separate outstanding work. No whole-module completion is asserted by this UI release.
