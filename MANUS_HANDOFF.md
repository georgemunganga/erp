# Kuubu release qualification — 6 November 2026

This branch is the maintained qualified runtime line, not a per-customer fork or
permission to deploy wholesale ERP main. Inspected published main remains
`7b7e3fc70b2d4866bb24e165a9646d598b589a8e`; existing ERP UI pin remains
`2b3357d2de3c383ea2926264abc701b1564a5895`, unchanged by this work.

Running Kuubu-host qualification image408da398 has57 genuine Core/HRM migrations,
zero users/sessions, loopback-only API, native restricted-role denials and actual
backup/independent restore proof for all98 HRM tables. See
`deployment/kuubu/QUALIFICATION-20261106.md`. Historical ERP production elsewhere
was not accessed or changed.

The session tenant repair from stacked draftPR200 is backported as exact image
source `1f54ff046cf69b7b4fb8604da9ad9bf6bc7b2f63`. Identical reviewed application
source passed34 focused,10 genuine-PostgreSQL and2,000 full main-line HRM tests.
Its own Docker gate is running as `kuubu-runtime-image-build03.service`; no image
success or deployment follows until actual terminal receipts and runtime checks.
Read `docs/engineering/CORE-SESSION-TENANT-20261106.md` for provenance and limits.

Root retains integration/release ownership. No collaborator task/acknowledgment
is inferred. All B1c/financial holds remain. Explicit Laravel-to-Core admission,
workspace/team mappings, operational module ACLs, unrelated-tenant acceptance,
off-host recovery and launch review remain required before customer routing.

# Plan review source integration — 3 November 2026

Issue [169](https://github.com/georgemunganga/erp/issues/169), draft [ERP PR171](https://github.com/georgemunganga/erp/pull/171), now includes actual published ERP main `f064f1a343e9897f4234bed7b6fd9586aebcded3` (accepted PR176 and B1 release PR177) through merge `d2c24ae4ea8f156406ec8ab23d94a992fc2c3921`, retaining the earlier B1 baseline `dc6a082db74f9740104e2295988bc6f1f54027d8`. Published UI main is `9b5d02dcf971900289793f56e91895dd3747fd80`; Plan UI PR103 has separate ownership. The single B1 genuine migration `20261103163108_M7b1CommercialRoundDraftPlans`, designer and snapshot remain byte-identical to ERP main. The host All-reader merge retains both Plan review and B1 round-plan routes.

Source review additionally sealed each submitted line's version in the immutable snapshot, alongside its ID and exact four-place decimal values. A regression case covers a version change with unchanged values; it has not been executed. [The source readiness record](docs/procurement/gap-closure/PROC-05A-POST-B1-SOURCE-READINESS-20261103.md) distinguishes source checks from runtime evidence and records remaining schema and contract gates.

Prior integrated backend source candidate is `c0dc4198` (R8 security source `72601ac3813c68c24278847eb6f094039e20a9c1` cherry-picked onto genuine adoption `a4f31f7c669eb7f8040990d14007d6146b71a703`). A separate author-peer source review verified the extracted shared Core projection implementation body byte-identical to the former B1 guard, with unchanged minimum-read grants and existing B1 facade; actual SQL golden/runtime verification remains pending. Plan READ/WRITE now reject effective extra HRM SELECT and sealed submission UPDATE; WRITE admits exactly five terminal/concurrency columns. Four InMemory terminal-save cases are included in14 new Host cases, all unexecuted; two migration safety cases are also unexecuted. Actual2002 Procurement passes belong to the earlier raw generation run, followed by retained Host CS0833 failure. No post-fix Host/model/SQL/native success is claimed. Warm source and SDK remain Root-owned.

Root's later actual verification of `400441d1` passed2004 Procurement cases, then Host477 yielded437pass/40fail (noOOM). The retained TRX identifies38 endpoint-construction failures from implicit Plan GET body inference in the minimal legacy fixture, plus2 stale assertions confusing safe Core Approval ownership metadata with business data privileges. Source correction explicitly marks all mapped Plan-handler DI parameters FromServices, keeps real registration/readiness and transport DTO/query bindings, and narrows those assertions while preserving the exact ownership guard. The added minimal-host endpoint-construction regression and source corrections remain unexecuted; Root owns subsequent Host/SQL/native verification. No warm source or SDK operation occurred for these fixes.

Actual follow-up source `bd4ace99` now passed478 Host/0fail/0skip with exact2004 Procurement reuse, model parity and SQL emission; bindingSHA `5141705b79f050ccc2601617adc398c7cd3a1ce218c06c83aaff2b01ccbd3d7b` is retained under the HTTP verification packet. No database/publication/runtime acceptance follows. A later source-only Detail correction ties Withdraw/assignment hints to the requested currently pinned Pending round and live case/stage/assignment, suppressing actions on historical rounds and Company All detail; six authored regression cases are unexecuted. Actual native capture is still required before manifest adoption.

Actual native r4 direct/true-restore observations now populate the Plan migration/two-check manifest and one immutable shared CHECK capture. Both exact variants are accepted by applied-migration-qualified generic operational identity, unit-review, invoice-owner-baseline and payment selectors. Actual payment canonical query hash `2a1a42eccb1379a8039da14a59458440a286f8967db5901abe3f144e532fe759` is evidence-bound; old pre-Plan expectations and other catalog predicates stay exact. Twelve new selector/payment-query cases are unexecuted; six Detail action cases remain unexecuted too. Source adoption does not establish finite-role/API/real-cookie workflow or UI acceptance. [Exact capture/payment provenance and verification bounds](docs/procurement/gap-closure/PROC-05A-POST-B1-SOURCE-READINESS-20261103.md#actual-direct-and-logical-restore-check-adoption--native-r4).

Integrated source now includes actual capture adoption `190dac6038e0f249074832959cfe0228cc21c454` and independently authored B1 restore hotfix `e6d87c5ce3882c5240abfa3355969a64554896dd` (held PR179), cherry-picked as `81778f06d1d4ce32971ec0dc011870f3000213ca`. The B1 fix maps only five exact observed named restore CHECK definitions inside the original complete catalog fingerprint; its expected hash, schema/privileges/flags/migrations and unrelated predicates remain unchanged. No direct main merge occurred. Next Root SDK candidate must verify twelve new Plan capture/payment-query cases, six historical action cases and twenty B1 restore cases; none is covered by the prior478 result. Actual direct/restored provider SQL and finite-role/native compatibility still require integration evidence.

Before the next SDK overlay, the missing Plan branch in shared CheckCompanyRules was corrected: strict schema2/current Core company/assignee action/branch/alias checks now cover Create/Update/SubmitReview/Publish without inventing policy currency fields or employee requirements. New purpose-specific config picker `/configuration/policies/lookups/plan-reviewers` supports exact eligible-total pagination and same-scope bounded principalIds hydration; sourcing-publication permissions are not reused. Fourteen handler/lookup regressions are unexecuted, and the actual Npgsql shared policy API/native-cookie lifecycle remains a required acceptance gate. No direct database policy seed is evidence. UI ownership receives the exact contract through Root.

Peer R8 confirmed Plan policy schema2 is entity-global: current organization-wide Core configuration actors and named Review/Final people are required, independently of shell branch/department. New picker has the same global eligibility boundary and no extra Core grant. Raw policy All retains company context for organization-wide users; branch-confined All remains400 before fallback. Transaction All guards stay unchanged. Twelve further cases are unexecuted (five middleware, seven handler/lookup); Plan policy class totals21 cases. Branch-specific policy conditions are unavailable and must be explained by the existing UI.

R8's cleanup-path audit additionally closed Return/Retire and retirement-hint global-scope bypasses. A shared current Core global configuration-actor gate now covers all Plan policy mutations; cleanup calls it without currency/rules/assignee validation while preserving original identity/SoD/readiness/unused-draft checks. Six more source tests are unexecuted (policy class27): branch/action revocation with unchanged records/audit/outbox and hintfalse, plus correction with unavailable currency/assignee. R8's full source audit must clear before Root's final SDK freeze.

Pinned-policy source correction now separates current submission eligibility from retained round provenance: existing rounds retain their exact Published/Superseded policy after replacement/expiry through shared historical proof; current Core authority and explicit denial states remain enforced. Added regressions remain unexecuted. Attachment requirements, delivered notices and SLA/reminders are explicitly unavailable in the initial strict stage-only policy schema pending adopted shared-service contracts.

Root owns all SDK/scaffolding/native/finite-role/private runtime and production work. No SDK, migration generation/application, database, Docker or deployment action was run for this integration. Actual genuine migration `20261103211606_ProcurementPlanGovernedReview` is now adopted with raw-identical designer/snapshot and a reviewed Down retention prefix. It covers two review tables, nullable plan branch/index and shared operational-policy identity CHECK expansion. Root's actual2002 Procurement cases passed; Host compilation then failed CS0833. The explicit audit member-name fix and new migration safety cases remain unexecuted. [Exact raw/final provenance and remaining gates](docs/procurement/gap-closure/PROC-05A-GENUINE-MIGRATION-PROVENANCE-20261103.md). Actual direct/restore manifests are now adopted in source; integrated provider/finite-role/workflow verification remains required before runtime admission. Do not publish this branch as a schema-complete release or copy mappings into the snapshot by hand. No business assignees, quorum, policy publication or feature switch is activated. The actual post-Plan shared CHECK fingerprint and migration-specific selector must ship with it; the existing owner-baseline fingerprint would otherwise deny all operational-policy admission. [The inert generation/native plan](docs/procurement/gap-closure/PROC-05A-GENUINE-GENERATION-AND-NATIVE-PLAN-20261103.md) records verified accepted PR176 ancestry and meaningful compatibility/retention negatives. B1 technical release is complete; Root retains allocation of the next Plan SDK/resource slot. Concrete inert packet `/home/amizo/erp-backups/procurement-plan-review-generation-sdk-20261103_run01` binds this backend source to accepted warm6ec, an exact27-file overlay, archive-aware23-current/29-historical-publication guards, mandatory offline restore, bounded launch argv and genuine EF generation script. Only syntax parsing ran; no overlay/SDK/container/DB step ran. Root must claim169 and allocate/review its slot before execution.

---

# Current production and collaborator boundary — 3 November 2026, 20:56 UTC

The B1 paired technical release is now deployed and verified. Published ERP prerequisite is main `950856f7e01e3e2d7c628ddbf67dbac846644803` (merged PR176) or its reviewed descendant; UI main `9b5d02dcf971900289793f56e91895dd3747fd80` remains published. Deployed products are backend `6ec41f938409f9400bd627f727c0161af0043479` / pub29 and UI `26d4ef1924b508a4ca33a184ec21500691003b3f`, image `97bd9e85…`. See the [actual release record](docs/procurement/gap-closure/M7B1-PAIRED-PRODUCTION-RELEASE-20261103.md).

Actual429 Host passes, separately bound1970 baseline reuse, genuine restore/Up/Down/Up, exact production24-revoke/22+3 grants, compiled read-only provider acceptance,150 live runtime hashes,16 public route/auth checks,5 primary+38 compatibility UI assets and7 anonymous Playwright renders passed. The interrupted pre-record3 step and reconciled single-grant continuation remain explicit. No signed-in production CRUD or whole-module UAT is claimed; issue145 stays **NOT READY**. All processing flags remain off and current Env/HostConfig is preserved.

Next candidate: issue169 / draft ERP PR171 `codex/procurement-plan-approval-20261103`, published source `a2e39e595347fb328283756eab13264e330c4d0f` (includes accepted main950856f7), paired draft UI PR103 source `1cb1bf4d3cc357893387c5ed5066dca0d9193788`. Re-read issue169 ownership before scaffolding; only one genuine migration is allowed. Its current migration directory still equals main:2 review tables, nullable Plan branch/index and the changed existing operational-policy identity CHECK still need actual generation/capture/compatibility verification. The UI's173 focused source tests do not replace that backend/native evidence. Root owns runtime/SDK coordination; the independent Core scanner task153 remains separate. Do not reuse deleted PR165/104 remote branches or stale tips from historical entries below.

The idle private test database is stopped. Generated warm SDK obj caches were safely cleared after proof that all tracked source and10 runtime test assemblies remained exact; run offline restore before the next build. No source/bin/publication/database/release evidence was removed. Earlier entries below are retained as dated history and do not override this current status.

---

# Superseded prerequisite checkpoint — 3 November 2026

Fetch ERP main `dc6a082db74f9740104e2295988bc6f1f54027d8` (including merged PR165) and Procurement UI main `9b5d02dcf971900289793f56e91895dd3747fd80` (including merged PR104), or reviewed descendants. These are published source prerequisites; the deployed pair remains the separately recorded manual-payment release until the B1 technical rollout completes. Backend product `53ed4d5486173580660cb3ea571c3703f227c85a` and preserved UI product `567e318105687b655a886e79c07eebd2b6bf4563` are now deployed. The final backend 361/UI 172 checks and private run03 six-form/native/security/owner evidence remain distinct as recorded in the [current paired release](docs/procurement/gap-closure/MANUAL-PAYMENT-PAIRED-TECHNICAL-RELEASE-20261103.md).

Root completed cutover run02 and public/browser verification at 16:23 UTC: API `be45e03038a596a7e52ee5deab74a0300493eb34849fe33cc79d99aa687577d3`, image `sha256:eb2077c982a7e24722c1afeb1a3206b9c5dfea2e67af4f984089800bc24400aa`, UI alias `payment-write-567e3181-20261103/`. All 150 API files, five UI / 37 compatibility assets, 15 public route/auth checks and seven anonymous browser renders with zero page errors passed. Attempt01 caught UI 500 and fully rolled back; Root repaired only staged-root permissions 0700→0755 with all 42 content entries unchanged, then separately approved the successful retry. All containers/releases and first-attempt receipts remain retained.

PR167's exact 11 narrow Procurement rights passed full ACL-delta verification and the actual final production full-governance provider returned true in a forced-read-only canary. No Core identity/Finance write or broad HR grant was added; all application Env/HostConfig and PG/publisher configuration are preserved. **Production payment-policy counts are zero and `Procurement:ManualPayments:Enabled=false` remains unchanged.** Configure an actual independently reviewed payment policy and current scoped Core duties using the existing forms before a separate activation/signed-in production acceptance review; do not invent fixture policy values or implement another payment engine. All 17 synthetic cohort sessions are revoked/original 192 preserved; private policy API is stopped. No Finance execution, verified attachment or signed-in production CRUD/UAT claim is made.

**Issue145 remains NOT READY.** Sourcing PR165 / issue162 now has genuine EF migration `20261103163108_M7b1CommercialRoundDraftPlans`, the actual 1,970 Procurement test result, and latest source `0f9bbc0bc9a2c236b40613d46538751323b78eaf` / publication28 with **411 Host checks passing**. The initial compilation/OOM failures remain retained. Isolated native acceptance passed actual workerless Core-cookie Create/Revise/Cancel, three-version history, original actor/key recovery, cross-actor key privacy, same-cookie current READ withdrawal and branch reassignment denials, strict row/audit/outbox reconciliation, and exact Core fixture restoration. Credential/payroll SELECT additions denied READ/RECORD; missing referenced-table-owner schema USAGE denied RECORD while preserving READ. See [bounded B1 evidence](docs/procurement/gap-closure/M7B1-PRIVATE-SQL-ROLE-ACCEPTANCE-20261103.md).

The preserved imported UI source `8dca4943bbe5b5ea4993a7eecb0e7a40fecb53c1` passed its actual offline production build; no replacement UI was created. A separate fresh original231-source UI clone passed genuine upgrade to237 tables, all six new tables empty and expected ownership/schema access, while preserving the source and earlier Cancelled native graph. The first actual UI run saved version1 and recovered its original201 response, then found an All-branch rendering bug. Fixed UI product `26d4ef1924b508a4ca33a184ec21500691003b3f` passed37 focused checks/typecheck/build and a narrow actual Playwright continuation on that retained record: All reads, C revision/cancellation, immutable history and key privacy, zero page errors. Cumulative native owner proof `1785d682…` and exact Core role cleanup passed; the failed run remains retained. Populated Down guard and rollback-only minimal M4 publication-barrier tests passed with unchanged rows/ACLs. Paired release remains open, with production round flags off. Both private APIs/proxies are stopped; all six native and three UI cohort sessions are revoked, and their existing Core roles restored exactly. Scanner153 remains external/no verified attachments. Business-owner sign-off and wider UAT remain open; hosted CI stays billing-blocked. Do not merge held P32/P36/P38 or overlap Root's release/runtime/SDK ownership.

**Assigned independent Manus build/scaffold pickup:** [issue170](https://github.com/georgemunganga/erp/issues/170), [MANUS-PROC-M7B1-SCAFFOLD-05](docs/collaboration/manus/MANUS-PROC-M7B1-SCAFFOLD-05.md), on the user-authorized separate computer. Fetch PR165 branch `codex/m7b1-round-plan-drafts-20261103` current head into Manus's own branch, never push Root165 directly. Original source-only pin is `2188f1bff287c7f293e77cd7e711d0cc63284352` / backend tree `53b6de1d91ed4743bd82a01f2f7788043c7b4d1a`. Root run03 generated `20261103163108_M7b1CommercialRoundDraftPlans` and passed all 1970 Procurement tests; Host compilation failed before its tests. The reviewed genuine migration/snapshot and callback name fix are published on PR165 for independent verification; subsequent Root publication28 passed 411 Host checks and bounded native acceptance. Verify that existing migration; do not generate a duplicate. Browser and release gates remain open. See [portable provenance](docs/procurement/gap-closure/M7B1-GENUINE-MIGRATION-PROVENANCE-20261103.md). If the genuine migration is published before pickup, verify that existing migration/tests rather than adding a second; if still source-only 2188, independent genuine scaffolding is allowed. Reproduce SDK 10.0.400/EF 10.0.11, preserve raw/final provenance, run 1970 Procurement/377 exact focused Host cases with analyzers and return no-pending-model/SQL proof. No worker acknowledgment is inferred; only **one** genuine migration will be selected/reviewed to merge, never automatic parallel integration. Root retains SQL/native/finite-role/activation; no production/private fixtures/secrets/UI/held-branch changes assigned. Scanner153 remains independent.

Earlier dated checkpoints below are retained; this entry supersedes their current release status.

---

# B1a/B1b commercial-round draft implementation — genuine-generation checkpoint, 3 November 2026

Issue [162](https://github.com/georgemunganga/erp/issues/162) starts from **published ERP main `3a8436c5cf9639bcb0e5a99018f4386d8c03dbf6`**, with published UI main `5dd51dff2ff74087d3f72682025ded071edd4782` unchanged. Implementation ownership: Root coordinates release/SDK/native acceptance; m6d owns isolated source branch `codex/m7b1-round-plan-drafts-20261103`; R8 provides independent source review. The branch now includes published main `7b3be5414f74bb754e3721c59a1861efffd9061f`; collaborators must fetch the current PR165 branch head, retain its single genuine migration and avoid generating a second one.

The draft source adds typed incomplete-plan enrollment/revisions/criteria/team/actor-command receipts, scoped Core-principal draft/read/recovery routes, current EF mappings and guarded staging, default-off READ/RECORD options, concrete finite-schema admission, same-release M4 publication/source-cancel barriers, and proposed database trigger/retention SQL helpers. Submit/review/publication are intentionally unavailable. No held migration/snapshot was merged; no master, employee prerequisite or business-policy approval was invented. See the existing [28-section specification](docs/procurement/features/PROC-06-M7B1-COMMERCIAL-ROUND-PLAN.md#b1ab1b-implementation-checkpoint--3-november-2026-source-only).

**Evidence and release holds:** source/diff review only; 43 new Procurement cases have passed within actual 1970/0/0 suite after genuine EF generation. The 16 new Host cases are pending because Host compilation caught a callback parameter/discard name collision; its four-line fix is included but untested. No database/grant change, API, UI, production deployment or native/UAT acceptance occurred for B1. Genuine current-model migration/snapshot bytes are retained with [portable provenance](docs/procurement/gap-closure/M7B1-GENUINE-MIGRATION-PROVENANCE-20261103.md); exact captured migration/trigger/function/index/owner fingerprint, finite-role rollout and disabled-trigger/partial-install/rollback negatives remain required before enrollment can activate. Capture constants are unset and both feature options default off. SECURITY INVOKER barrier grants expose only scope/key/state/revision/operation columns to M4; preserve those grants on binary rollback. An old binary may fail exact schema readiness and must leave affected M4 writes disabled until a compatible forward release. Do not strip triggers to restore a stale fingerprint. Root retains unrelated payment/scanner work. Historical checkpoints below remain evidence, not current release claims.


---

# M7B1 adopted engineering baseline — 3 November 2026

Documentation-only draft against published ERP main `b42aa7355d196b5c3c678721890b6f1479851b63` and UI main `5dd51dff2ff74087d3f72682025ded071edd4782`: [PROC-06-M7B1 commercial round plan](docs/procurement/features/PROC-06-M7B1-COMMERCIAL-ROUND-PLAN.md), linked to [issue145](https://github.com/georgemunganga/erp/issues/145). Root and independent m6d review have adopted this 28-section engineering baseline for implementation under the user’s existing full-module authorization. Production business-policy settings remain unapproved; no implementation, runtime acceptance or activation is claimed. It uses the actual current principal capture2 / quote-evidence schema2 / commercial snapshot3 and existing `SupplierBid.Version` pin; no separate BidVersion entity or held branch merge is introduced.

The proposed first slice covers internal plan/enrollment/review/publication only for never-published, untouched principal RFQ Drafts. Existing published M7A quotations cannot be retrospectively sealed or enrolled. The existing RFQ publication paths must retain a barrier for enrolled events until formal submission and every confidentiality surface are accepted. All policy weights/thresholds, required evidence, disclosure and restart decisions remain explicit adoption inputs; no approved defaults or migration/route/UI implementation is claimed. See sections10/11/25/28 for the confidentiality matrix, publication guards, acceptance and blocking decisions.

Root retains payment issue158/PR159 SDK/native/write-policy acceptance and deployment ownership; independent scanner task153 is unchanged. After this reviewed PR merges, start the next B1 build from published main. The round handler must use round-specific current action/company/branch access that preserves the original RFQ/source maker; do not reuse or globally relax M4 PrincipalOwned’s maker==actor checks. Shared lock order is sorted PR requests → event → enrollment/plan → Core rows. The first enrollment write, including draft CRUD, must ship the existing RFQ publication barrier in the same release, never deferred from persistence to later HTTP work. Revision children have distinct physical row IDs and stable logical IDs, unique per plan/revision/logical ID. Every command pins its exact immutable result revision; publication references the unchanged submitted snapshot/hash. Explicitly Cancel the plan first, then use only the unchanged authorized M4 source-cancel path when no active review/Published plan remains; retain enrollment/history and let only M4 release the PR holds. Do not relabel events/sources or globally block legitimate other RFQ creation. Engineering may implement configuration/schema/adapters/draft CRUD under the existing authorization with these adopted constraints. Unresolved production business policy blocks Submit/Publish, not design or draft CRUD implementation. Do not merge held `98609167`/P32/P36/P38, touch active runtime/DB/UI, or treat this documentation branch as a production release. Historical checkpoints below remain retained.


## Next independent Manus milestone — MANUS-PLAN-06

**Repository:** `georgemunganga/erp`; track [issue169](https://github.com/georgemunganga/erp/issues/169) and [draft PR171](https://github.com/georgemunganga/erp/pull/171). This is a backend implementation/verification milestone. Procurement already has the UI; do not reimport or replace it. Root retains production, shared private databases, runtime resources, release and final integration ownership.

**Published prerequisites:** ERP main `dc6a082db74f9740104e2295988bc6f1f54027d8` and UI main `9b5d02dcf971900289793f56e91895dd3747fd80`. The work input is the published Plan feature branch `codex/procurement-plan-approval-20261103`, source `a7bc1371d06edb7b39d9b29415e0da81bfc847bb`, on top of that ERP main. Plan UI PR103 source `1cb1bf4d3cc357893387c5ed5066dca0d9193788` retains the existing screens; runtime tree `78b0b728` passed TypeScript and173 focused tests. A frontend checkout is unnecessary for the initial backend task; use the ERP feature/API documentation to preserve its contract.

**Claim and coordination:** comment on issue169 before generating a migration, with your branch and input SHA. Re-fetch issue169/PR171 immediately before scaffolding. If Root or another owner has already published a genuine Plan migration, verify that migration instead of generating another. Source review of the latest policy-pinning change is being recorded under issue169; do independent design/code verification while that review completes. Do not push directly to Root's branch, reuse server-private evidence or invent successful checks.

**Required work:**

1. Read AGENTS.md, GW-ENG-STD-001, the adopted Plan feature specification and `docs/procurement/gap-closure/PROC-05A-POST-B1-SOURCE-READINESS-20261103.md`. Preserve current Core identities, employee-optional access, company/branch scope, exact decimal values, immutable policy/line-version pins, shared Core approval engine and original-key receipts.
2. Compile the actual source using its pinned SDK/packages. Fix concrete compilation, authorization, state-machine and contract failures on your branch, with focused meaningful tests. Do not upgrade dependencies or replace shared services merely to make the build pass.
3. From the actual post-B1 model, generate the genuine EF migration/designer/snapshot for the two Plan review tables, nullable Plan branch/index changes, and the changed existing `ck_operational_policy_identity` CHECK adding `PROCUREMENT_PLAN_REVIEW`. Preserve all existing B1 migration bytes. Do not handwrite a snapshot, copy a held migration or create duplicate schema history. Record source, SDK/EF versions, command outputs and exact artifact hashes.
4. Run no-pending-model checks and actual generated Up/guarded Down SQL checks. Apply them only to an isolated non-production PostgreSQL fixture. Capture actual schema constraints, ownership, indexes/triggers and the canonical readiness fingerprint before filling the currently empty Plan readiness manifest. Update shared `ProcurementOperationalPolicyIdentitySchemaReadiness` only from that actual new CHECK capture; keeping its old owner-baseline capture would deny every affected policy workflow. Prove retained records/audit cannot be destroyed by downgrade; no trigger disabling or catalog edits.
5. Test current permission/branch/tenant denial, workerless Core accounts, All-scope read/no-write, source line-version consistency, maker/checker and current authority, sequential review/final decision, return/rejection/withdrawal, delegated/expired authority, pinned policy after ordinary replacement, original-key exact response recovery and retries/concurrency. A new Open/Submit still needs an eligible current policy; revoked/retired/unknown policies do not gain authority. Use current services, not test actor headers or fake approved outcomes.
6. Run the relevant complete Procurement and Host cohorts on your machine and identify stable baseline/new cases separately. Where feasible, complete normal-cookie API/native persistence acceptance using synthetic users on your own isolated fixture; preserve failures, owned audit/outbox effects and cleanup. Do not use or request production credentials or customer records.
7. Push a focused draft PR and progress entry on issue169 with exact source, migrations, tests run versus unrun, failure/recovery evidence and remaining limitations. Include operating/rollback and permission/schema contract updates. Do not mark whole-module UAT or production release complete.

**Boundary:** files, SLA delivery and reminder policies currently unsupported by this Plan policy schema remain explicitly documented; do not fabricate a scanner, attachments, notifications or Finance integration. Improve them only through an adopted shared-service contract. Root will review and integrate your PR with the existing UI, run paired browser acceptance, and deploy separately. Unclaimed work may advance under Root; always re-read current ownership and published migration state before adding schema.

---

# Current integration ownership — 3 November 2026

Published prerequisites: ERP main `9d10180cd09f8dcfd2be1ad4344219a70e54993a` and Procurement UI main `5dd51dff2ff74087d3f72682025ded071edd4782`, or reviewed current main descendants. ERP PR152 and UI PR99/100 are merged and their code/static releases are deployed. Fetch both main branches before starting; quotation write switches remain off. See [the current production record](docs/procurement/gap-closure/M7A-PRODUCTION-WORKFLOW-RELEASE-20261103.md).

**M7A issue148 is now owned by Root through ERP PR152 and UI PR99; do not start overlapping quotation UI edits.** The complete original-screen draft workflow has private API/UI/native-owner evidence, including current Core identity, exact terms, scoped reads, revision/cancellation and original-key recovery. Retained B/C Company All browser reads, test cleanup and paired code rollout passed. See [the current acceptance record](docs/procurement/gap-closure/M7A-NATIVE-DRAFT-WORKFLOW-ACCEPTANCE-20261103.md). Updated main baselines and the next collaborator milestone will be published after prerequisite merges; the independent Core scanner task remains available on separate non-production hardware as documented below.

Root retains positive database-backed quotation workflow verification, schema/finite-role operations, full UAT closeout and paired production deployment. New quotation record/capture switches default off independently from RFQ publishing. Existing independent document-scanner ownership is unchanged; quotation nonempty document transport is not available yet.

Evidence: typed persistence passed 1,927 Procurement tests and 167 focused Host cases; the final API integration passed 218 focused Host cases. Genuine isolated migration/rollback guards, exact ACL changes and real enlisted READ/RECORD admission passed; dedicated restore instances stopped with original data retained. The preserved RFQ UI passed ten adapter cases, TypeScript checking and 22 transport-mocked Playwright cases. These are bounded results, not complete quotation CRUD or whole-module UAT acceptance. See [the owning contract](docs/procurement/gap-closure/M7A-OWNING-HTTP-CONTRACT-DRAFT-20261030.md) and [whole-module gaps](docs/procurement/gap-closure/PROCUREMENT-UAT-AND-GAPS-20261027.md).

The fresh-backup paired API/schema/UI rollout is complete; see [the production release record](docs/procurement/gap-closure/M7-PRODUCTION-TECHNICAL-RELEASE-20261103.md). Signed-in production checks and the remaining bid/evaluation/award/returns/contracts/payment/portal acceptance remain open under [issue 145](https://github.com/georgemunganga/erp/issues/145). GitHub Actions is account-billing blocked; local/private test evidence is separate. Historical entries below are retained and superseded for current ownership by this section.

**Payment retained READ technical release — issue155 / ERP PR156+157 / UI PR101:** backend `b61ddf73` and preserved UI `ed044333` are deployed. Source passed 253 focused Host cases; UI passed 172 unit and 31 mocked Playwright cases plus actual normal-cookie history/client-key recovery. Three specifically reviewed Core column SELECT grants were necessary; the actual production read-only provider then passed. Public route/auth, five primary plus 36 compatibility assets, all 150 live API file hashes, and seven anonymous browser renders passed. Native session cleanup preserved all other sessions/business/source records. See [the current UAT release record](docs/procurement/gap-closure/PROCUREMENT-UAT-AND-GAPS-20261027.md#payment-retained-read-technical-release--3-november-2026). Processing stays off. This is not complete payment write or whole-module acceptance.

**Next coordinator milestone:** [issue158](https://github.com/georgemunganga/erp/issues/158), existing manual-payment write/policy/UI acceptance. Draft PR159 narrows full-governance Core reads without weakening write controls; it is unverified source, not an adopted main prerequisite. Root owns its SDK/finite-role/private multi-actor workflow and later activation decision. M7B formal rounds need their own adopted child specification and confidentiality/document controls. No competing workflow/UI or default business grants/thresholds are authorized.

**Independent Manus pickup:** [issue153](https://github.com/georgemunganga/erp/issues/153), [Core scanner C04](docs/collaboration/manus/MANUS-CORE-DOCUMENT-SCANNER-04.md), on separate adequate non-production hardware. Claim with its focused draft PR and progress file. No frontend checkout is needed. Root retains payment READ/schema/API/UI integration and document HTTP/workflow activation; do not overlap those files.

---

# Current integration ownership — 30 October 2026

Published code prerequisites: ERP main `a3381969581c613e2e583036d3d3e8bf176440f2` and Procurement UI main `06347d3029361533962b972556ab1d1952d68602` (`georgemunganga/module-connect`). These contain the complete merged M6 source cohort: ERP PRs #135–#139 and UI PRs #93–#96. Start from these published main commits or their descendants. Before starting collaborator work, fetch both current main branches and verify they include the complete cohort; do not start from an old deployment SHA or a held experiment. Tested backend `f1e5102e` and UI `9c56b492` identify the retained candidate publications. A source merge does not activate a production workflow.

The existing correction form completed a real save and original-key recovery after a deliberately lost response. Its later harness-origin failure remains retained. A separate flags-off original-UI continuation passed both invoices’ Overview/History and saved-result recovery: seven direct reads, six screenshots, no page errors/business writes, and logout. Independent native reconciliation preserved every business record and admitted only its exact authentication cohort. The single temporary vendor permission and 62 finite SQL grants are restored with current data/history retained; only two documented PostgreSQL owner-default ACL storage representations differ from their initial NULL representation. See the [M6E record](docs/procurement/gap-closure/M6E-CONTROLLED-INVOICE-REPLACEMENT-20261030.md).

Root owns fresh production backup/restore, the Core-first plus seven-Procurement migration rehearsal, paired rollout and signed-in verification. Production deployment and [whole-module UAT](docs/procurement/gap-closure/PROCUREMENT-UAT-AND-GAPS-20261027.md) remain open. M7 quotation validation/evidence contracts are isolated source groundwork, not deployed quotation CRUD; complete supplier-offer fields, schema, API and original-UI integration remain required. No new Manus assignment is issued here. Existing independent document-scanner ownership is unchanged. The dated sections below retain historical evidence and are superseded for current ownership by this section.

---

# M5 technical release and current prerequisites — 28 October 2026

Published ERP main: `1c6a81d772c654e7a58254845d116e2cc3b0d7bd` (through PR133).
Published Procurement UI main: `47cac6a13a24e9db113383f8a9cf5b319494d526`
(PR92, `georgemunganga/module-connect`). Use current main descendants.
These include M5b receiving and M5c company-local service-date source fixes.

M6A invoice-history Open and independent review passed a bounded private
real-cookie/native owner checkpoint on backend source `6287bd22`, now published through PR133:
[scope and exact evidence](docs/procurement/gap-closure/M6A-INVOICE-HISTORY-PRIVATE-ACCEPTANCE-20261028.md).
Published ERP main is `1c6a81d7`; PR #133 is merged, and this checkpoint
does not activate M6 or prove qualified matching, Finance posting or whole UAT.

M6B principal invoice mapping remains under Root integration in ERP draft PR135
and UI draft PR93; these are not published prerequisites for new Manus work.
The genuine receiving and dual-issued-parent migrations passed private Up,
guarded Down/Up and retention veto checks. Source `7f44d5f3` passed 1,559
Procurement and 304 focused Host tests. A real-cookie private journey created
an invoice, saved source3 links to a genuine principal receipt, recovered the
original command, denied another actor's key and Company All writes, and
required independent original-unit review before comparison. Native persistence
reconciliation passed. The original UI then passed a second mapping, lost-response
original-key recovery and review-required denial; the wording successor and
flag-off history reads also passed real Playwright. Exact temporary roles/grants
were restored and both private runtime stacks stopped, preserving the legitimate
test records. See [M6B bounded acceptance and receipt pins](docs/procurement/gap-closure/M6B-PRINCIPAL-INVOICE-LINKS-PRIVATE-ACCEPTANCE-20261028.md).
No M6B production activation, qualified matching, Finance posting or full-module
UAT is claimed. This is the bounded M6B record; later M6C manual-review and
Blocked-comparison acceptance is recorded in
[M6C's private checkpoint](docs/procurement/gap-closure/M6C-PRIVATE-WORKFLOW-CHECKPOINT-20261029.md),
and the separate M6D qualified comparison and feature-off closeout is recorded in
[M6D's checkpoint](docs/procurement/gap-closure/M6D-PRIVATE-CURRENCY-CAPTURE-CHECKPOINT-20261030.md).
Those draft integrations are not published Manus prerequisites. Preserve real
principal parent constraints; never insert surrogate legacy versions or fake
document evidence.
GitHub Actions currently cannot start because its account has a billing lock;
local pinned SDK/native evidence must remain explicit, never reported as CI.

M5d accepted-Goods return-request CRUD/history now has private normal-cookie
API and independently replayed native owner acceptance. It preserves the
original receiving UI and ERP components. Writes-off history/recovery passed;
all cohort sessions are revoked, temporary role rows restored and private
API/proxy services stopped. See [M5d scope and evidence](docs/procurement/gap-closure/M5D-GOODS-RETURN-PROPOSALS-20261028.md).
Do not duplicate M5b/M5c/M5d or resume held P37 migrations.

Production M5 technical rollout and observed restore-readiness fix are complete:
product `6087f211`, UI `47cac6a1`,
220 tables, independently preserved original203 rows/sequences, finite grants,
read-only canary, public route/asset checks and three Playwright renders passed.
Existing business flags are preserved; new return writes remain off. See the
[release record](docs/procurement/gap-closure/M5-PRODUCTION-TECHNICAL-RELEASE-20261028.md).
The [API-only recovery fix](docs/procurement/gap-closure/M5-RESTORE-READINESS-PRODUCTION-20261028.md)
preserves schema, settings, permissions, publisher and UI. Authenticated
production CRUD is not claimed.
Whole-module UAT is open: actual return approval/dispatch/reversal, qualified
invoice source/consumption ownership, and broader scope/concurrency tests remain.
The original Service UI completed one bounded private real-cookie journey with
owner reconciliation; see the
[Service UI acceptance](docs/procurement/gap-closure/M5-SERVICE-ORIGINAL-UI-PRIVATE-ACCEPTANCE-20261028.md).
The earlier private Service API journey has scoped owner and Core UTC-date
acceptance; see the
[Service checkpoint](docs/procurement/gap-closure/M5-SERVICE-RESTORE-READINESS-PRIVATE-CHECKPOINT-20261028.md).
Neither establishes production Service activation or non-UTC/multibranch UAT.
Return requests alone change no accepted,
Inventory, credit or Finance balance. Core retains shared identity/roles/scope,
organisation/calendar/branding ownership.

Root owns release follow-up. Active agent work covers the Service restore-readiness
release review and M6 principal invoice matching specification/implementation. No
new Manus implementation assignment is made by these agent scopes. Existing
independent C04 ownership is unchanged.

The checkpoints below are historical. Their invitations and baseline claims are
superseded by this active ownership section.

---

# Published Procurement UAT checkpoint — 27 October 2026

Audited ERP backend baseline: `fcdafac34351b3e0d312b4e49ff949dc2d1c0ede`
(product `20985344252b0b394939cfceb87152d49f39e2e2`).
UI PR89 is merged: accepted UI main
`f1e237713d5f803212ec2ae72773be842ab0e3b6`, product `720b108`, final tests
`ba25a24` and documentation-only successor `f03a532`. Start from current
published main descendants; no UI rebuild or duplicate import is assigned.

The [UAT report](docs/procurement/gap-closure/PROCUREMENT-UAT-AND-GAPS-20261027.md)
and [exact checkpoint](docs/procurement/gap-closure/PROCUREMENT-UAT-CHECKPOINT-20261027.json)
record 587 UI tests and a build, 444 mocked browser scenarios (full443/444 then20/20
affected-file rerun), two fixed navigation races, and real-cookie manual-payment
API/native-owner acceptance. Four Core users without employees completed the
payment flow; independent confirmation is not bank/Finance execution. Whole
Procurement UAT is NOT signed off; authenticated production CRUD was not tested
in this audit. Hosted CI is not represented as a pass.

Root owns M5a release readiness and invoice/payment follow-up. Manus may claim
[M5b receiving/inspection/returns](docs/collaboration/manus/MANUS-M5B-PRINCIPAL-RECEIVING-08.md)
from these merged prerequisites/current main. Connect Core users and M5 PO
issued evidence through the original receiving UI. Preserve permission/scope,
independent approvals, quantity conservation, reversal history and module
ownership. C04 scanner work remains independent. Do not resume old held branches
or infer supplier delivery, Finance posting or inventory ledger acceptance.

Production remains M4 with existing flags; this audit changed no production
role, policy, schema, flag, container or UI alias. Current-row migration preflight,
fresh recoverable backup/restore, finite Record+Execution grants and paired
rollout/smoke checks remain required for M5a. Test-profile grants and the synthetic
ZMW100 review policy are not production configuration.

---

# Published M5a prerequisites and receiving handoff — 27 October 2026

This checkpoint supersedes older assignment/baseline paragraphs below.
ERP M5a PR119 is merged in accepted main
`58dedebf9761d20c1dd0490b1b1dd6532c9d8454`; UI PR87 is merged in accepted main
`c33b435efe041bb97c7b831be93cd9d8aca05d28`. Start assigned work from these
published prerequisites or current main descendants. Application trees match
tested products `20985344` and `79a19df` respectively.

[Bounded M5a evidence](docs/procurement/gap-closure/M5A-PRIVATE-UI-VERIFICATION-20261027.json)
records 94 latest focused Host checks, 51 focused UI checks/build, ten genuine UI
form actions with reload/GET recovery, ten scoped/stale denials, five actual All
queues with writes off, and overlapping Submit/Issue with one Core case/issued
version. Native checks reconciled original rows, schema and normal logout.
This is source acceptance, not production deployment or whole Procurement UAT.
Hosted CI did not start because GitHub reports an account billing lock; local
results are recorded separately, never presented as a hosted pass.

Root owns M5a production preflight/release and M6 invoice/payment contract auditing.
Manus may claim the full
[M5b receiving/inspection/returns milestone 08](docs/collaboration/manus/MANUS-M5B-PRINCIPAL-RECEIVING-08.md)
from these published prerequisites. Preserve the imported UI and shared services;
Core users do not require employee links. Publish ERP coordination and linked UI
PRs. The independent C04 scanner scope remains; record its state/handoff if active
before claiming another implementation stream. Older queued P35/vendor tasks
below are historical instructions, not permission to resume stale branches.

Production remains the healthy M4 technical release with existing flags. M5a
release needs current-row migration preflight, fresh recoverable backup/restore,
finite grants and paired rollout/smoke checks. Do not enable production workflows,
claim external Finance/supplier delivery or merge held branch ancestry.

---

## Retained history

# Procurement collaboration checkpoint — 27 October 2026

Verified published ERP main: `9f2591cc3bd5b322e73cd44ea080795370b7352c`.
Verified published UI main: `46782ccdb502d8be34e5abb7e25ff659cd325582`.
The ERP main update after the M4 deployment is documentation only. These are
the shared starting points; the following draft branches are not merged release
baselines and must not be deployed as accepted milestones.

Root owns M5a backend and private verification in
[ERP draft PR #119](https://github.com/georgemunganga/erp/pull/119).
The existing UI integration is in
[UI draft PR #87](https://github.com/georgemunganga/module-connect/pull/87).
Preserve the imported Procurement layout and shared ERP components. Do not
rewrite the PO screens or introduce another master, ledger or approval engine.

Current verified backend product revision is
`20985344252b0b394939cfceb87152d49f39e2e2`; UI build revision is
`79a19df28b1d3e95d4ab8798c7f27cd1ff53f9d0`. Both are published draft branch
commits, not merged release baselines. The latest backend passed 94 focused checks,
no pending model changes and the genuine API publication. Earlier private
same-row PO and four release workflows are reconciled. The UI passed type
checking/build and 51 focused checks; the fresh uninterrupted ten-command
five-role form journey passed on backend product `2bebdf70` and that UI build.
Latest backend `20985344` additionally passed five All-queue UI reads with writes
off and original-key recovery, plus overlapping Submit/Issue requests with one
Core case and one issued version. Earlier ten denial checks passed on `2bebdf70`.
All results have separate native owner reconciliation. Production release and
broader Procurement UAT remain open.
Exact source-specific evidence, preserved failures and release limits are in
[the M5a implementation record](docs/procurement/M5A-PRINCIPAL-ORDER-GOVERNANCE-IMPLEMENTATION.md).

Do not overlap Root's active M5a files, change test credentials/grants, or enable
production operational flags. The existing independent Manus scanner assignment remains in place. No new collaborator milestone is assigned from
these drafts. Publish reviewed prerequisite merges and update this checkpoint
before assigning work that depends on them.

The bounded form-driven, return/reject/release, stale-command, scoped-queue and
overlapping-command results are recorded above. Release still needs current-row
canonical migration preflight, a fresh recoverable production backup/restore,
finite grants, paired rollout and post-deploy smoke checks. Broader security
matrices and whole Procurement business UAT are not declared complete.
Receiving/returns, amendments and later principal-identity procure-to-pay work
remain separate milestones. Finance owns its eventual accounting/payment
adapter; the private manual evidence here does not implement that adapter.

---

## Earlier retained checkpoints

# Current deployed and published baseline — M4 technical release

Use ERP main `63360f128911b0cd66eb4e44e0f79fabcd1a2345` and UI main
`46782ccdb502d8be34e5abb7e25ff659cd325582` or descendants for new assigned work.
The matched release is deployed: tested API product `cca4772473b2a87411b3ca59a0a766fbd84187de`,
UI `b51216eaf4af07c3985faef9e8b51145c4bbf92f`, image `b6dc4e822fd7383e13c186e2bac4d0974dde4fb6922b53e20bf1e5be525f6fff`,
schema203/histories56/29/1. [Deployment evidence](docs/procurement/release/M4-PRODUCTION-DEPLOYMENT-20261026.json)
records actual fresh backup/restore, atomic migration/finite grants, readonly
canary, live review and anonymous public Playwright. Core/HRM3300 and Accounting
are preserved. Current flags remain unchanged; no operational M4 activation or
whole Procurement UAT completion is claimed.

Coordinator owns M5a PR119, receiving, sourcing follow-ons and remaining UAT;
its eight-table migration and Down safety are under review, not deployed.
Do not duplicate these or merge held mixed migrations/UI ancestry. Manus retains
the independent Core document-scanner assignment; no overlapping new assignment.
The imported UI and shared ERP identity, settings, controls and ownership stay
in place. Nine merged remote branches were retired across the cleanup passes; their
commits/local worktrees and all unique/advanced work remain. Historical baseline
paragraphs below are superseded by this section.

---

# Latest UI and UAT checkpoint

ERP prerequisite main is `de449645a5cf4b46da1024c821252ac46b625103`; UI main `dfed2dd1954e8dc735a3bd8ff03449d4b00e6911` includes deployed anonymous-access recovery PR85. Current backend is product `237a936b25690f780cd09ca6ab3f90b1a05cc009` / image `c233ffd9e76f7579f52bfb03259985a7abc6691c94d05b9633773b2389f3aa0a` after the [verified expiry-guard image-only release](docs/procurement/release/CORE-ACCOUNT-LINK-EXPIRY-DEPLOYMENT-20261026.json); current UI product is `9b6ab3442aa1583a339dcfed311adad393a29706`. Fresh M3b and finite-Core current-mainline M2a private browser/owner/preservation checks passed; [current status](docs/procurement/gap-closure/CURRENT-EXECUTION-STATUS-20261026.md) distinguishes that from production activation.

Production imports and finite maintenance are now enabled; [activation evidence](docs/procurement/release/M2A-PRODUCTION-IMPORT-ACTIVATION-20261026.json) records healthy checks and unchanged role permissions/schema. No production import transaction was written. The tested account lacks explicit import permission and correctly receives 403; Core role assignment remains separate.

Coordinator owns M4 backend PR114 and original-layout UI PR86, their paired production release, plus [draft M5a PR119](https://github.com/georgemunganga/erp/pull/119) at published source `e57de3c786e65e60da28285483ed9de9a8a942dd`. M4 internal RFQ publication has [bounded private real-cookie acceptance](docs/procurement/gap-closure/M4-INTERNAL-RFQ-PUBLICATION-ACCEPTANCE-20261026.json), while supplier delivery, bidding, evaluation and awards remain open. M5a reuses existing PO records and shared Core approval; migration, readiness, UI and runtime acceptance remain open. Its source checkpoint is not an assignment or an accepted prerequisite. M4 remains disabled in production. Do not duplicate those or merge held mixed migrations/UI ancestry. Manus retains the independent scanner task. No new overlapping collaborator assignment, Finance integration or whole-module UAT completion is claimed.

**M4 private acceptance:** the earlier Open/lost-response recovery passed, but its Decide and one same-key diagnostic returned 409 without committed effects. The repeated-save shortlist invariant was corrected in `cca4772473b2a87411b3ca59a0a766fbd84187de`. A fresh real-cookie PR/Finance/Core-approved RFQ then passed one Open with original-key recovery and one independent internal Approve; owner checks linked the Core decision, two publication receipts, one unsent manual invitation, audit and outbox. The expired old Pending case was separately returned through the real UI with its original Open receipt and Finance snapshot unchanged. The positive graph stayed exact after that Return, 197 outside-fixture tables and all histories matched, and all synthetic sessions were revoked. The post-quiescence strict original comparison retained all 359 old IDs: 358 rows including five POs were exact; one old policy row had an authorized supersession, documented without concealing the strict failure. [Exact evidence and limits](docs/procurement/gap-closure/M4-INTERNAL-RFQ-PUBLICATION-ACCEPTANCE-20261026.json) exclude supplier delivery, external Finance verification and production M4 activation.

**Cleanup:** three completed compiled-only archives (299,087,290 bytes) were removed after member, active-process and mount checks. Raw source, test results, databases, backups and rollback products remain. Merged remote branches for PR117 and PR118 were deleted only after exact-tip/main-ancestry/open-PR checks; local worktrees and commits remain. [Cleanup record](docs/procurement/release/UAT-CLEANUP-CHECKPOINT-20261026.json) pins the private journals.

---

# Current deployed baseline — 26 October 2026

Published prerequisites are ERP main `c8eac4c54b009bbafe3cf916992abdbcf3562a61` and UI main `7f75d327a2bb8eb399f48b370da0cc53a0a831b9`. The paired technical release is live: product `d35ecc090b0de0abcab1e38be09fd25c52a1d0aa`, UI `e032805303e23e1658d5a66808d9b0d6f1ce991c`, image `c9ea9f7bd48c112bf20cc5cbb6f20d48850075942dfa37f366af9c41f352464b`, schema histories HRM56/Procurement28/CoreApproval1 and finite runtime accounts. [Current status and remaining gates](docs/procurement/gap-closure/CURRENT-EXECUTION-STATUS-20261026.md) supersede historical no-deployment paragraphs below.

Coordinator owns remaining UAT, gated capability activation, SMTP/expiry investigation and anonymous Procurement sign-in recovery. Manus retains the independent Core scanner assignment; no overlapping implementation is assigned. Keep original layouts/shared ERP controls and Core-owned identity/settings. At the initial technical cutover these capabilities were off; imports and finite maintenance are now enabled as recorded above. Operational approvals, manual payments and principal direct-order execution remain off until their affected release acceptance. No business-UAT sign-off or Finance integration is claimed.

---

**M3a source accepted — 26 October 2026:** Start assigned backend work from published ERP main `35045b7e51efca2e756dd678b9de6a62477b03bc` (PR108) or a descendant; UI main remains `704ac9c642d00e3672cd81e7c1df2d9cb50be160`. Fresh 14 focused / 1,295 Host checks, three real-cookie browser phases, independent owner/ACL/original-row verification and session cleanup passed. [Bounded acceptance and retained failures](docs/procurement/gap-closure/M3A-REQUEST-APPROVAL-ACCEPTANCE-20261026.json) distinguish API actions from actual UI, trust/owner-backed private tests from production, and manual Finance references from accounting. Coordinator continues principal approved-line/PO conversion and remaining M0/release work; Manus retains scanner task04. No production deployment or activation occurred; do not duplicate coordinator work or merge held UI ancestry.

**Publisher source accepted — 26 October 2026:** ERP main `27be07e0a97d61ea8fe57d4a15c3de527cbcfd43` includes merged PR106; UI main remains `704ac9c642d00e3672cd81e7c1df2d9cb50be160`. Start new assigned backend work from this main or a descendant. Fresh 28 focused / 1,293 Host tests and finite-role private two-tenant Store + genuine publisher acceptance passed: four rows, eight correctly tenant-attributed audits, unchanged original records/queue, clean stop. [Exact evidence and limits](docs/procurement/gap-closure/M0-CROSS-TENANT-PUBLISHER-VERIFICATION-20261026.json) distinguish acknowledgement from external delivery and production deployment. Coordinator continues M3a request/approval acceptance and remaining M0/release gates; Manus retains independent scanner task04. Production is unchanged; do not enable held workflows or duplicate coordinator work.

**Published UI baseline — 26 October 2026:** UI PR83 is merged in `georgemunganga/module-connect` main `704ac9c642d00e3672cd81e7c1df2d9cb50be160`. The narrow import/export/Core-scope port preserves the existing UI and excludes 139 held files. Clean committed validation passed TypeScript, 99 units, a fresh live build and 23 mocked browser tests; [evidence and remaining real-API gate](docs/procurement/gap-closure/M2A-UI-MAINLINE-VERIFICATION-20261026.json) are explicit. ERP main `788c944a0acb4eada5246bdd7a5793e78d21689d` includes M2a source acceptance. Start new assigned work from these published mains or descendants. Coordinator now owns the shared nonempty publisher tenant/audit fix and remaining integration/release work; Manus Core scanner task04 remains independent. Production remains unchanged. Earlier UI main references are historical.

**M2a source accepted — 26 October 2026:** ERP PR100 is merged in main `ff255065ab3240d7949315aafd62395db3d03838`; exact product source `88c561554b60f313881248e5cd581f6856e982d8` passed fresh builds, 25 focused / 1,291 Host tests, 12 real-cookie API cases, four real browser phases and independent owner/finite-role/lifecycle review. All 18 sessions are revoked and original data is preserved. Imports remain default off; production is unchanged. See [acceptance evidence](docs/procurement/gap-closure/M2A-NATIVE-IMPORT-ACCEPTANCE-20261026.json) and [remaining milestones](docs/procurement/gap-closure/CURRENT-EXECUTION-STATUS-20261026.md). Coordinator owns the separate narrow UI mainline port and release gates. Manus retains Core scanner task04; do not duplicate imports, merge held UI ancestry, or activate held workflows. Start new assigned backend work from this published main or a descendant. Earlier pending import/build paragraphs below are historical.

**Native import checkpoint — 26 October 2026:** Vendor preview/save/byte-identical replay passed real private Core-cookie HTTP and owner reconciliation. Item preview remains blocked: the diagnostic cohort located HTTP 503 at the database-boundary check while the catalog connection is open. Diagnostic source `cddd1525342a3ad840e84e568afb1ba96657df3b` passed 22 focused / 1,288 Host tests and genuine API/Tool builds. The unchanged Procurement/Approval results are carried from the earlier build, not rerun. No real import browser journey or production activation is accepted. The coordinator owns the fix; Manus should retain the independent scanner assignment. See the [current execution checkpoint](docs/procurement/gap-closure/CURRENT-EXECUTION-STATUS-20261026.md) for exact scope and remaining gates. Historical pending-build statements below are superseded by this checkpoint.

**Private schema update — 26 October 2026:** [M2a migration rehearsal](docs/procurement/gap-closure/M2A-PRIVATE-MIGRATION-VERIFICATION-20261026.json) passed genuine Up/empty Down/Up and 15 rollback vetoes with independently verified original-data/history/access preservation. This was on the isolated restore only. Runtime roles, real imports and production activation remain pending; coordinator ownership is unchanged.

# Current execution checkpoint — 26 October 2026

**This section supersedes historical status and baseline instructions below.** See [current baselines, exact test evidence, owners and remaining milestones](docs/procurement/gap-closure/CURRENT-EXECUTION-STATUS-20261026.md). ERP code baseline is published main `8eb2e305e66dba2f38763a2b029957ddec108b69`; UI main remains `403bfdac505d593921a5bab1ba3388896205325f`. Main PR99 CSV compilation is verified (14 focused passes), not pending.

Coordinator owns default-off M2a imports in ERP draft PR100 and UI draft PR82. Local verification passed 1,282 Host / 1,414 Procurement / 30 Approval tests, 68 focused tests, and six mocked UI browser cases. Private migration acceptance is recorded above; finite-role runtime and real-cookie browser acceptance remain pending. Production is unchanged. Manus retains Core scanner task04 and should not duplicate M2a or activate held workflows. Start new assigned work from published main and record exact baselines; do not merge held branch ancestry wholesale.

---

# Manus ERP collaboration handoff

**Core03 source verification completed (24 October):** The coordinator repaired and verified the shared frontend in ERP PR #74. TypeScript, the production client/SSR build, eleven role cases and seven isolated mocked browser cases passed. See [acceptance evidence](docs/collaboration/manus/CORE03-ACCEPTANCE-20261024.json). Manus should continue its other assigned work; Core03 is no longer available for pickup. No deployment or real API acceptance is claimed.

**Queued independent UI verification (24 October):** [MANUS-P35-UI-VERIFICATION-07](docs/collaboration/manus/MANUS-P35-UI-VERIFICATION-07.md) checks the existing Procurement UI draft [PR #71](https://github.com/georgemunganga/module-connect/pull/71) at exact commit `ae3694740288c5442d8f60863a68fa598f322f3f`. It covers the P30/P31/P33/P34/P35 frontend stack, including restored layouts, exact amounts, scoped access and unknown-write recovery. It is additive and queued for an available worker on separate hardware; it does not interrupt Core build, scanner, P33 UI or P35 backend tasks. The UI and P35 backend are held source, with capabilities default off until separately accepted runtime/schema gates. No mocked browser result from Task 07 can establish real API or production acceptance.

**Ready independent hardware task (24 October):** [MANUS-CORE-DOCUMENT-SCANNER-04](docs/collaboration/manus/MANUS-CORE-DOCUMENT-SCANNER-04.md) is assigned for Manus pickup on adequate non-production hardware, in a separate branch/checkout from `MANUS-CORE-BUILD-03`. It verifies the existing Core scanner and permits focused protocol fixes supported by real results. It does not replace the frontend assignment, require held Procurement branches, activate uploads or authorize production changes. Claim it in a focused draft PR and its own progress file; the coordinator reviews and releases.

**Accepted source checkpoint (23 October):** ERP PR #48 and UI PR #64 are merged on top of the payment, Core role and principal-approval milestones. Core users without employee links passed the RFQ draft create/read/replay/cancel workflow:65 actual private API requests,11 PostgreSQL constraint cases, a normal local-cookie Playwright create/reload/cancel/reload journey, independent retained-owner verification and both genuine downgrade vetoes. Failed attempts and the reviewed verifier capture compatibility are preserved in [P29 acceptance](docs/procurement/release/P29-ACCEPTANCE-20261023.json). This is source acceptance, not production deployment or whole-module completion. Production remains P24. P30 PO governance, RFQ publication/quotation drafts and receiving are coordinator-owned, still disabled pending combined schema/runtime/browser verification; they are not a new Manus assignment.

**Updated:** 24 October 2026 (UTC)
**Primary project:** https://github.com/georgemunganga/erp
**Milestone:** `MANUS-CORE-BUILD-03` — repair the shared ERP frontend TypeScript/build gate; vendor milestone accepted
**Owner:** Coordinator has claimed Core03 on 24 October 2026; Manus should not duplicate it. Separate scanner work remains independent.
**Status:** [MANUS-CORE-BUILD-03](docs/collaboration/manus/MANUS-CORE-BUILD-03.md) is claimed from published ERP main `ef67a5b3ef6712cdfdb917d43c6142027ec43722`; [progress](docs/collaboration/manus/CORE03-COORDINATOR-PROGRESS-20261024.md) records source and verification separately. The shared frontend TypeScript/build gate remains unverified. Vendor fallback was accepted and merged in UI PR #47. The coordinator owns settings shell and remaining Procurement CRUD controls separately; do not start overlapping work.

**Payment integration checkpoint:** ERP PR #44 and UI PR #62 are merged. Private payment and policy browser journeys and owner reconciliation passed. Production is still P24; migration, finite roles, feature configuration, deployment and UAT are separate release gates. P25 principal approval source is now merged; the coordinator is implementing the separate P30 publication/PO/receiving successors and production release prerequisites. Do not duplicate it. Existing Manus Core build assignment remains unchanged.

## Start from merged code

New assignments start from published `origin/main` in both existing repositories. The coordinator merges reviewed prerequisites before assigning a milestone. Do not start from an old task branch, a default branch missing the application, or an unpublished commit. Keep your existing work and port focused changes onto the new baseline; never reset or rewrite published history.

| Responsibility | Repository and source | Baseline |
| --- | --- | --- |
| ERP, backend contracts, schemas, standards and task coordination | `georgemunganga/erp`, `main` | Published main `298a857b0eaa88fdc3480bebcde2e1b8defece84` or a published main descendant; includes P23/P24 live acceptance and reviewed manual-payment foundations PRs #42/#43 (storage, shared invoice/credit capacity and Core company clock). Includes PR #44 Core workspace choices/governed manual payments, PR #46 Core role revocation hardening and PR #45 versioned Core-user request approval with retained-worker compatibility. Adds PR #48 verified principal RFQ draft persistence and genuine corrective migration. Private evidence is recorded; production release is pending. |
| Existing Procurement UI and API clients | `georgemunganga/module-connect`, `main`; `reference-ui/` and its existing `src/platform/` clients | Published main `403bfdac505d593921a5bab1ba3388896205325f` or a published main descendant. Includes the subsequent original-layout restorations through PRs #60/#61 and payment/Core selector integration PR #62 and exact-budget/approval integration PR #63 and verified RFQ draft adapter PR #64; this is the current public UI baseline. Preserve its shared ERP shell, branding, controls, real API adapters and recovery behavior. Restored presentation does not prove every workflow is connected. |
| Design reference | `Greenwebb/zoho-procurement-ui`, reference `6f0c305` | Existing vendor layout/editor; reuse the imported implementation already in the UI repository |

The ERP already serves this Procurement UI. Its source remains in the existing UI repository. ERP's `modules/hrm/frontend/module-connect/` is a tracked HRM snapshot, not the Procurement source or a Git submodule. Do not duplicate the UI there, re-import the reference project, or create another frontend. Coordination and backend work belong in ERP; a fork-based UI PR is acceptable when upstream UI access is read-only.

**Current access requirement supersedes older workerless read-only instructions below:** an active Core account with current module/action permissions and company/branch/record scope may use Procurement whether or not it is linked to an employee. An employee designation or link never grants Procurement authority. Do not fabricate or request an employee link for general CRUD. Preserve known linked-worker maker/checker conflict checks as veto metadata; inactive HR status must not erase those conflicts. Old operational worker-pinned contracts remain gated until their versioned Core-principal replacements are verified.

Before work, fetch `origin/main` in both repositories, record the exact starting commits and verify they contain the minimum baselines above. Branch from those main tips. Read ERP `AGENTS.md`, `docs/engineering/GW-ENG-STD-001.md`, UI `AGENTS.md` and `reference-ui/UI_COMPONENT_STANDARD.md`.

Production now includes the reviewed P20–P24 CRUD schema and Core-principal application, with [P23/P24 actual live release acceptance](docs/procurement/release/PROC-OPS-P23-P24-LIVE-ACCEPTANCE-20261022.md). Real-account vendor and recurring-purchase UI journeys passed with independent owner/audit reconciliation. P23 document/workflow activation prerequisites remain outstanding. Do not deploy source branches, apply migrations or enable workflow/Finance flags to complete this UI task.

**Current coordinator milestone:** [P24 recurring planning private acceptance](docs/procurement/release/PROC-07A-P24-PUBLISHED-ACCEPTANCE-20261022.md) is complete and merged. The coordinator completed the [combined P23/P24 production release gates](docs/procurement/release/PROC-OPS-P23-P24-RELEASE-PLAN-20261022.md) and owns the unfinished P25 principal-based request approvals. P25 is a draft, default-off source milestone, not deployed. Manus retains `MANUS-CORE-BUILD-03`; do not duplicate recurring CRUD, invoice review, settings, backend migrations or deployment. Finance/AP and payments belong to the future ERP Finance module, which is not built; no substitute ledger or fake funding/payment success is authorized.

**Branch-scope regression fixed:** preserve PR #51's verified legacy-name-to-ID hand-off before readiness or business requests. Do not silently clear a stored branch, infer another company, choose the first match, or reintroduce a read-only gate that strands the branch picker. See [the incident and real-account verification](docs/procurement/release/PROC-UI-BRANCH-HANDOFF-20261022.md). The actual signed-in account can open Vendors/My Requests and their new forms; this narrow acceptance does not certify every workflow.

**Approval work ownership:** PR #55 preserves the existing UI, handles the separate optional principal capability and fixes in-session same-command recovery. It does not enable approval execution or prove real API/PostgreSQL workflow success. PR #56 adds scoped locator-only Open/Decide recovery through explicit actor-owned GET acknowledgements after reload; no payloads/reasons/credentials or automatic new POSTs are stored. The coordinator owns remaining P25 runtime/authorization/real-identity/browser/release gates; do not duplicate this work or enable Finance/approval flags.

## Accepted coordination and remaining work

The claim/status contributions from ERP PR #9 are retained with their original author. That PR has no vendor implementation. Its historical missing-SHA observation and old branch references do not govern new work. The coordinator has confirmed that `445a5cb` is published; the new baseline table supersedes the old instructions.

The user correctly identified a layout regression: live routing used a separate simplified vendor page while the imported directory, sticky toolbar and detail tabs remained in `CatalogDetail.tsx`. The coordinator has restored the directory, compact header, split overview and URL tabs in the reviewed UI baseline. Preserve that work, the real API adapter and the explicit administrator read-only controls.

## Historical vendor milestone — accepted, not the active assignment

The following vendor scope and verification instructions are retained for traceability. The active `MANUS-CORE-BUILD-03` file defines Manus’s current owned files and acceptance; these older sections do not assign new vendor or settings work.

### Vendor owned files

- `reference-ui/src/features/LiveVendors.tsx`: focus on editor/form integration and concrete save/context safety; preserve the merged detail layout.
- Existing vendor form/presentation components in `Catalogs.tsx` or an extracted shared component, only where required to reuse the original editor.
- `src/platform/procurement-vendors-api.ts` and vendor-specific helpers/tests.
- ERP `docs/collaboration/manus/vendor-contract-verification.md` and progress log.

The new baseline includes vendor attribute update persistence: keyed CustomField/ReportingTag patches preserve omitted keys and retain explicitly cleared rows. Use this real contract; no workaround storage is needed. The new invoice P20 migration is coordinator-owned and must not be applied by Manus. Credits/catalog follow-up work is separate and not part of this baseline.

Backend supplier routes/contracts are read-only during this milestone. Report precise gaps for a separate owned backend task. Do not change migrations, identity, Core settings, global styles/navigation, shared controls, Finance, approvals, sourcing or other modules. Do not overwrite coordinator work or manufacture endpoints.

## Full milestone: vendor editor and supported workflows

Follow the ordered [V1–V6 checkpoints and CRUD/workflow register](docs/collaboration/manus/MANUS-PROC-VENDOR-01.md). Complete all checkpoints and report unsupported contracts as gaps, not implemented features.

Reuse [PROC-01](docs/procurement/PROC-01-SUPPLIER-ONBOARDING-AND-MANAGEMENT.md), [the vendor form specification](docs/procurement/PROC-UX-05-NEW-VENDOR-FORM.md), and their existing 28-section feature contracts.

1. **Preserve the supplied form.** Use the original primary-contact/company/display-name/contact header and the seven tabs: Other Details, Address, Contact Persons, Bank Details, Custom Fields, Reporting Tags and Remarks. Reuse the existing imported form with ERP controls, typography and branding. Do not replace it with a shorter new form.
2. **Map every field.** Record the source UI field, real DTO/API field, owner, validation, read/write support and persistence evidence. Use actual supplier contacts, sites, qualifications and allowed attributes. Keep Finance-owned bank/payment information separate; never store raw bank details in browser storage or invent Finance balances. Unsupported uploads, portal activation, email or other functions remain explicitly unavailable until their owning contract exists.
3. **Complete supported CRUD.** Empty/list/search/filter, create, read, edit and versioned withdrawal; supported contact/site/qualification add/edit/remove; submit/return/reject/approve/hold/reactivate only when permitted by the server. Preserve the existing directory and detail tabs. Restore the original New Transaction/More toolbar containers with permission-aware supported actions and clear unavailable states for missing contracts; never fake email, payments, cloning or deletion. No hard deletion of approved records or browser-local success fallback.
4. **Respect ownership and scope.** Currency defaults from Core company settings. Use stable company/vendor identifiers and current Core permissions/action flags, including authorized users without employee links. No fabricated employee, blanket administrator approval, supplier activation or policy default.
5. **Save safely.** An edit cannot fall back to create when its record/version has not loaded. Preserve unowned fields; do not send blank placeholders that erase data. Handle validation and optimistic-version conflicts without losing entered values.
6. **Prevent stale context.** Abort or ignore old requests after company, account or vendor changes. A delayed response or mutation must never populate or write another context. Do not reuse another record's ID/version.
7. **Handle uncertain outcomes.** Disable duplicate saves. If the server has no idempotency/recovery contract, a lost response is unconfirmed: do not automatically repeat the mutation or treat a same-name vendor as proof of persistence. Offer an explicit read-only reconciliation path and record the backend gap.
8. **Keep simple interactions.** Existing shared controls, floating selects, toggle switches, accessible tabs, centered empty states and the shared table. Import appears only when a real supported path exists. Loading, empty, validation, denied, conflict, unavailable and recovery states must work.

## Verification and deliverables

- TypeScript, focused units, unchanged-lockfile live-mode build and Playwright: one worker, no retries, preserved failed traces.
- Browser cases: original editor tabs/fields and detail layout at desktop/mobile; create/refresh/edit; edit-load failure cannot create; denied/read-only paths; validation preserves values; stale version; delayed company/account switch; duplicate-click/lost response; supported contacts/sites/qualification flows.
- Test only synthetic fixtures or an explicitly assigned disposable backend. No production credentials, production test writes, migrations or real personal/banking data in evidence.
- Distinguish mocked HTTP UI tests from actual API/PostgreSQL persistence. An HTTP200, screenshot or local-storage entry alone does not prove persistence.
- ERP contract matrix, workflow checklist, exact commands/commits/results, synthetic screenshots, API gaps and recovery behavior. Update [STATUS.md](docs/collaboration/manus/STATUS.md).
- Open a new draft ERP verification PR against `main` and link the actual UI PR/fork PR against the UI `main`. Include `MANUS-PROC-VENDOR-01` in both titles. Do not carry the old development branch's unrelated ancestry into a stable PR.
- Mark ready only after the supported milestone passes its acceptance cases. List unsupported owning-module contracts honestly; do not claim the entire Procurement module is complete.

## Merge and release ownership

Manus submits focused commits and PR evidence. The primary agent independently reviews and verifies the changes, merges reviewed prerequisites, then publishes the next task's exact main baselines. No new task is assigned against stale or unpublished code.

Do not merge, deploy, change production identities/data/permissions, apply production migrations, enable capabilities, force-push or rewrite history. Preserve existing work. ERP issues are disabled; use the handoff, milestone log and linked PRs rather than changing repository settings.

## Queued milestone after vendor acceptance

`MANUS-PROC-SETTINGS-02`: restore the imported Procurement settings index and navigation shell; maintain the original simple layout and module-specific groups. Audit each child against a real owning API. Global organisation, branding, locations, users and roles stay in Core. Do not mount browser-preview save handlers as live configuration. This is queued, not assigned concurrently with the vendor editor. The coordinator publishes fresh merged ERP/UI baseline SHAs after vendor acceptance.

## 24-hour coordination update — 22 October 2026

The new merged source includes exact catalog price edits, persisted invoice-linked credit drafts and safe editor recovery. It passed10 actual browser flows (real API/private PostgreSQL),152 API assertions/90requests,84 backend cases,42 units and111 mocked browser cases across48+63 batches on one application bundle. It is not yet production-deployed. The coordinator is actively verifying the original full seven-tab vendor layout with actual child persistence; this is fallback integration, not acceptance of an unseen Manus implementation. Receiving/approval source reconciliation and forward migration remain separate held work. Preserve all existing contributions; do not reset branches or copy a second frontend.

## Accepted vendor milestone — 22 October 2026

UI PR #47 merged normally at `5f88d54917525c6642d05e94409068f37212b5fa`; reviewed final feature head `26142f51d809efd3a5ec6364bfffa689e5b8e3c2`. Final source passed24 mocked browser cases and2 actual API/private-PostgreSQL browser cases, one worker/no retries, plus10 focused units, TypeScript and production build. Independent review found no remaining blocker in the reviewed saved-header reconciliation path. The final actual bundle was `index-BhacWz3_.js`; traces and source/asset manifests are retained privately. This is acceptance of supported vendor CRUD, not unsupported bank/portal/document contracts or production deployment.

**Collaboration reservation:** the coordinator already has a module-only settings shell, Home/employee catalog, permission/context invalidation and uncertain-save guards under verification. Do not implement those in parallel. Manus may review the fresh merged vendor form and return concrete layout/interaction defects in a focused PR or verification report, with no duplicate frontend or backend changes. Any new implementation milestone will name its owned files and a newly merged baseline.

## Current independent assignment

Manus owns [MANUS-CORE-BUILD-03](docs/collaboration/manus/MANUS-CORE-BUILD-03.md), a complete repair/verification milestone for inherited shared ERP frontend TypeScript failures. Start from the latest merged ERP main with the minimum baseline above, not the coordinator’s unmerged branches. Procurement implementation continues independently; the existing vendor and settings assignments must not be duplicated.

**Finance integration boundary:** The user confirmed AP and payments belong to the future Finance module, which is not built. Procurement may validate its commercial invoice records and prepare versioned hand-off contracts. No fake posting/payment success or substitute ledger is authorized. P25 private tests use explicitly synthetic manual evidence and do not establish external Finance authority.

## 23 October public UI release and source preservation

Bills list/editor/detail and Inbox restoration PR #57 is deployed. See [the release evidence](docs/procurement/release/PROC-UI-RESTORATION-DEPLOYMENT-20261023.md) and [original-layout integration register](docs/procurement/PROC-UI-ORIGINAL-PRESENTATION-RESTORATION.md). All 23 deployed-asset browser cases passed with mocked business HTTP; this is not actual database acceptance for every workflow. Backend/schema/flags are unchanged. Use the ERP-adapted imported layout reference `d104f596183e7a012a04578a679aac795e1489d4` and keep current shared branding, shell, API and recovery behavior. No reduced parallel forms or local demo mutations.

Thirteen fully merged remote UI branches were deleted after exact-tip/main-ancestry/open-PR checks, along with three unattached local branches. Fetch published main for new work. Unmerged and advanced branches, checked-out worktrees, evidence and backups remain. Manus retains `MANUS-CORE-BUILD-03`; the coordinator owns remaining Procurement integrations, including manual payments behind retained original screens. Finance absence must not remove Payments UI, but no bank execution, AP posting or fake Finance status is authorized.

## 23 October manual-payment foundation checkpoint

The coordinator owns `PROC-PAY-01`; [ERP PR #43](https://github.com/georgemunganga/erp/pull/43) contains mapped manual records, append-only receipts/history, P27 guards, shared invoice/credit capacity and the existing Core company clock. [The source acceptance receipt](docs/procurement/release/PROC-PAY-01-FOUNDATION-ACCEPTANCE-20261023.json) records1,319 Procurement tests,321 focused Core/API tests,59 real PostgreSQL checks, retained/fresh migrations and actual Program compatibility. Prior failures remain preserved; no hosted-CI, actual payment API/browser, finite-role or production activation is claimed.

The next coordinator milestone is typed payment policy and exact shared Core workflow linkage, then the current-authority/idempotent API and existing restored Payments UI binding. Do not invent a policy ID, require an employee link, copy a second workflow engine/frontend, manufacture Finance Paid, or enable demo mutations. Manus retains `MANUS-CORE-BUILD-03`; no overlapping payment implementation is assigned by this checkpoint. Start any later assignment only after the prerequisite merge and fresh published main baselines.

After payment history exists, rollback must retain the new invoice/credit guards or disable those writes. Old P24 application code must stay off write traffic over retained manual payment data.

## Payment governance implementation checkpoint — 23 October 2026

The coordinator owns draft [PROC-PAY-02 / PR #44](https://github.com/georgemunganga/erp/pull/44), based on merged ERP `9693296432a698c06b4bfc724ff394405b75cfaf`. It adds typed review policies, actual shared Core case/decision links, company-date observation evidence and P28; the existing Payments editor title is retained. Unregistered scoped payment reads and actor-owned command recovery are source work, not a live API. Payment command integration, private PostgreSQL/finite-role API acceptance and the original UI/Playwright payment journey remain coordinator-owned. Do not implement a competing Payments UI, add employee prerequisites, enable demo mutation or assign policy/grants by default. Manus retains `MANUS-CORE-BUILD-03` from the published main baselines above.

The P28 migration is not applied to production. Keep its schema checks and the existing operational-policy readiness pins coordinated; P28 extends the shared policy identity CHECK and old rendered P23 pins cannot silently certify it. No Finance/AP dependency is introduced for manual external-payment recording. No bank execution or fabricated Finance Paid result is authorized. GitHub issues are disabled; PR #44 and the versioned feature/handoff/verification records provide change trace. Hosted GitHub CI remains blocked by account billing; local evidence is recorded separately.

## Queued P35 backend verification — 24 October 2026

[MANUS-P35-BACKEND-VERIFICATION-06](docs/collaboration/manus/MANUS-P35-BACKEND-VERIFICATION-06.md) is a separate, queued source-verification assignment pinned to ERP branch `codex/p35-contract-review-source` at `f5cee78d57df4a0d22089b9a7b94fee844f27233`. It must not interrupt the active Core build, scanner or P33 UI workstreams. P32/P33/P35 remain held and default off pending genuine schema, runtime and owner evidence; the task's SDK and test results must be recorded before any further activation claim.


## Coordinator checkpoint — 29 October 2026

No new Manus milestone is assigned by this checkpoint. Published main baselines remain ERP `1c6a81d772c654e7a58254845d116e2cc3b0d7bd` and UI `47cac6a13a24e9db113383f8a9cf5b319494d526`; refresh and verify them before any subsequent assignment.

Coordinator-owned draft [ERP PR #136](https://github.com/georgemunganga/erp/pull/136) and [UI PR #94](https://github.com/georgemunganga/module-connect/pull/94) retain the original imported invoice UI and manual Core unit-review work. Backend `0f656d63` passes 1,592 Procurement and 671 host tests plus a focused regression. Original-UI review/recovery and native retained history passed; the recorded comparison is legitimately Blocked with no qualified claim. See the [current private workflow checkpoint](docs/procurement/gap-closure/M6C-PRIVATE-WORKFLOW-CHECKPOINT-20261029.md) for evidence and release limits.

Currency-aware invoice calculation and controlled correction after processing history remain coordinator-owned gaps. The Core precision and M6D documents are proposed contracts; do not implement a competing currency master, replace the imported UI, rewrite historical invoices, enable production capabilities or claim Finance integration from these private tests. New work must consume published, reviewed prerequisites; these draft heads are not published main baselines.


## M6D active continuation — 30 October 2026

Published main baselines remain ERP `1c6a81d772c654e7a58254845d116e2cc3b0d7bd` and UI `47cac6a13a24e9db113383f8a9cf5b319494d526`. Draft ERP PR137 and UI PR95 contain Core currency ownership and new invoice calculation/recovery; draft source `72537cd5` / UI `72c3882e` includes the source-picker correction. [Actual capture evidence and retained failures](docs/procurement/gap-closure/M6D-PRIVATE-CURRENCY-CAPTURE-CHECKPOINT-20261030.md) distinguish one saved invoice and original-key recovery from uncompleted matching. The coordinator continues actual mapping, independent unit review, qualified comparison, private fixture restoration and reviewed release. No competing currency master, employee prerequisite, policy weakening or replacement invoice UI is authorized. These draft heads are not published prerequisites for a new external assignment.

### B1 actual capture adoption checkpoint — 3 November 2026

PR165 / issue162 now has genuine migration `20261103163108_M7b1CommercialRoundDraftPlans`, actual1970 Procurement +377 Host/model/SQL evidence (`63f5f9b7…`), and private phase1 Up/capture (`384660f8…`). The new source adopts the full observed query together with migration ID and canonical hash `24e096b8…`; old M7A accepted shapes remain unchanged. Latest four new Host contracts and one named test supersession require source-bound381-case verification before the single captured publication26. No B1 flags, users, finite-role grants, API workflow, UI or production activation is claimed. Root separately reviews the genuine empty Down/Up packet; cookie CRUD and barrier/finite-role/retained Down acceptance follow later. Keep the original generator failures and raw/final source hashes in the portable migration-provenance document.

### B1 UI-facing backend follow-up

The source-only successor adds current source version/status, shared verified method/line projection, per-record actions, round-only provisional-source discovery, minimal event-scoped Core people paging and independent M4 enrollment/cancellation barrier hints. No original M4 maker gate is relaxed. It also enforces the reviewed B1-only Core SELECT safe union while retaining the same required Core minimum. Root-authored Core-read guard tests and source DTO/action/projection tests await the next source-bound Host run/publication27; pub26/schema/finite-role proofs remain separate actual evidence. Existing round command bodies, genuine migration, canonical schema hash and owning Procurement/Core source remain unchanged.

### M7A restored physical-shape source correction — 3 November 2026

Release issue [175](https://github.com/georgemunganga/erp/issues/175) holds the paired technical deployment pending fresh restore readiness. This focused draft starts from published ERP main `c2da0b6f59e8472d8af3fb6dc464485d7d2b6b34`; UI `26d4` remains unchanged and unmerged Plan PR171 is excluded. The source adds one exact complete seven-table quotation contract derived from saved authenticated production-backup restore metadata. Its four M7A CHECK definitions differ from `Json`; relative to `ProductionJson`, the restored M4 decision definition is a fifth difference. Existing contracts, catalog projection, model, migrations and privilege/owner denials remain unchanged. Both the digest matcher and database JSON equality admit the same third full contract; unknown changes and mixed CHECK variants remain denied.

The schema-only fixture is a projection of saved actual metadata, **not yet an actual invocation of the frozen seven-table SQL or compiled provider**. Source audit SHA-256 is `93fad9d7a623cde40ae6a8a8fd07782fddaf04354db6cb7930a9b9e36e28d62e`; the private projected-observation file SHA-256 is `563d562707a02a2d0f97dfd6e3988d8248c8fce204b9bd4650a0df5293859c02`. Root owns SDK verification, compiled old-negative/new-positive proof, actual SQL capture in the next isolated rehearsal, runtime/private PostgreSQL work and release approval. New regressions cover all three contracts, old-contract rejection of the observation, exact SQL alternatives, constraint/omission/scope/index changes, mixed variants and unchanged owner/privileged-role denials. Tests are authored but unexecuted at this source checkpoint; no deployment or activation is claimed.


### PROC-05A actual private primary checkpoint — 3 November 2026

Plan PR171/issue169 now includes published main556fbee2; only the B1 restore evidence document conflicted and current main evidence is retained. Tested backend6e85/tree `c4d0703b357ba2674cd4baffd1e90e80a069ef7b` is unchanged. Root actual548 Host / exact2004 Procurement reuse, model/unchanged B1SQL, dual-native finite-provider and owner-denial evidence, private execution20+2 and ordinaryRecord7, real shared policy Create/Submit/Publish, and UI3e/26 focused checks/new build/four-command browser primary path are documented in the [current checkpoint](docs/procurement/gap-closure/PROC-05A-PRIVATE-INTEGRATION-CHECKPOINT-20261103.md). The browser observed Create201/Submit201/Review200/Final200, reviewer-owner403, historical read-only/currentActive, revoked cohort cookies and empty owned cgroup. Strict post-primary native owner reconciliation, retained Down55000/privilege negatives and production release remain Root-owned/pending. Master145 stays NOT READY; no merge/deploy or production acceptance follows from this documentation update.
