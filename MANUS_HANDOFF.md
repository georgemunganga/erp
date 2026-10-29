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

M6B principal source mapping and matching are under integration. The generated
receiving-fact binding migration passed private Up with all original business
rows preserved. Review then identified a separate legacy-issued-version FK
on mapping, exposure and result records that rejects genuine principal issued
IDs. A discriminated, scoped dual-source schema correction is required before
source3 can be activated. Do not insert surrogate legacy issued versions or drop
ownership constraints to bypass this. No M6B production or full UAT claim exists.
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
