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

## Catalog lookup follow-up — UI PR117

The deployed backend contract separates approved catalog lookup (`procurement-item-read`, GET catalog) from item-master stewardship (`procurement-item-manage`, item listing/detail and CRUD). The request-only role initially lacked catalog lookup. UI main `1c1f984c78545dfe006def035518b17449f56afe` aligns Items page entry with stewardship, prevents hidden Home catalog metrics from making unused reads and shows manual-entry guidance instead of making a forbidden lookup when catalog read is absent. The original request/catalog selection interface remains shared.

Only the existing catalog-read permission was added to Front Desk through the Core role API, making its effective actions module access, request read/create/edit/submit and catalog read. No item/vendor/order maintenance, approval, payment or configuration action was added. Existing account, employee and organisation linkage remain unchanged. No backend/schema, supplier/item activation or catalog publication change was made.

TypeScript, production build and7 matrix checks passed.53 distinct browser cases have passing evidence:52/53 in the main run and the older pending-workflow fixture was corrected to include its required approval-read grant before its targeted rerun passed. Tests include approved-price selection/reference persistence, no unauthorized lookup for manual-only users, request-only menus/direct denials and Home/request regressions. Actual restricted-account verification passed9 read-only checks: scoped catalog API/browser search200, continued item/vendor/order403s, unchanged restricted navigation and manual request entry. Available approved catalog entries are0; that is a truthful empty result, distinct from a permission error. No procurement transactions were written; owned test sessions logged out (native active0). All12 new/retained public files match their publication hashes.

Publication: `/home/amizo/procurement-ui/releases/catalog-access-1c1f984c-20261111_run01`. The prior permission-aware publication and every asset URL are retained; private rollback configuration selects prior HTML while keeping the combined asset pool. Evidence: `/home/amizo/erp-backups/request-catalog-access-20261111_run01`. The Core-role delta is a deliberate configuration change separate from the earlier UI-only release. Company approval setup, approved catalog population and full-module UAT remain separate work.
