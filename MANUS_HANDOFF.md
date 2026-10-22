# Manus ERP collaboration handoff

**Updated:** 22 October 2026 (UTC)
**Primary project:** https://github.com/georgemunganga/erp
**Milestone:** `MANUS-CORE-BUILD-03` — repair the shared ERP frontend TypeScript/build gate; vendor milestone accepted
**Owner:** Manus / `Greenwebb`; primary agent reviews and releases
**Status:** New independent Core frontend milestone [MANUS-CORE-BUILD-03](docs/collaboration/manus/MANUS-CORE-BUILD-03.md) is ready for pickup after its assignment merge. Vendor fallback accepted and merged in UI PR #47. Do not duplicate the editor. The coordinator is verifying the settings shell and remaining CRUD controls on a separate branch; do not start overlapping settings implementation. Publish any existing work/claim in a PR for reconciliation. No Manus implementation PR has been observed. See the fresh merged source baselines below; they are not a production release.

## Start from merged code

New assignments start from published `origin/main` in both existing repositories. The coordinator merges reviewed prerequisites before assigning a milestone. Do not start from an old task branch, a default branch missing the application, or an unpublished commit. Keep your existing work and port focused changes onto the new baseline; never reset or rewrite published history.

| Responsibility | Repository and source | Baseline |
| --- | --- | --- |
| ERP, backend contracts, schemas, standards and task coordination | `georgemunganga/erp`, `main` | Merged main `4c4c9ad702ff218b175d218716d5915925b6f6ea` or a published main descendant; includes the P23/P24 live release acceptance in PR #32 and current roadmap in PR #34. Exact deployed application/schema/public assets and scoped real-account acceptance are recorded in the release receipt below. |
| Existing Procurement UI and API clients | `georgemunganga/module-connect`, `main`; `reference-ui/` and its existing `src/platform/` clients | Merged main `ecf48db33a863bef4235e877f9d2dc699c07a1b4` or a published main descendant; includes P24 PR #54 and P25 request-approval UI compatibility/retry PR #55. Source `e00d475` passed 53 units, staged typecheck/build and 25 controlled browser cases. PR #55 is source preparation, not a production deployment; the accepted P24 bundle remains the public UI. |
| Design reference | `Greenwebb/zoho-procurement-ui`, reference `6f0c305` | Existing vendor layout/editor; reuse the imported implementation already in the UI repository |

The ERP already serves this Procurement UI. Its source remains in the existing UI repository. ERP's `modules/hrm/frontend/module-connect/` is a tracked HRM snapshot, not the Procurement source or a Git submodule. Do not duplicate the UI there, re-import the reference project, or create another frontend. Coordination and backend work belong in ERP; a fork-based UI PR is acceptable when upstream UI access is read-only.

**Current access requirement supersedes older workerless read-only instructions below:** an active Core account with current module/action permissions and company/branch/record scope may use Procurement whether or not it is linked to an employee. An employee designation or link never grants Procurement authority. Do not fabricate or request an employee link for general CRUD. Preserve known linked-worker maker/checker conflict checks as veto metadata; inactive HR status must not erase those conflicts. Old operational worker-pinned contracts remain gated until their versioned Core-principal replacements are verified.

Before work, fetch `origin/main` in both repositories, record the exact starting commits and verify they contain the minimum baselines above. Branch from those main tips. Read ERP `AGENTS.md`, `docs/engineering/GW-ENG-STD-001.md`, UI `AGENTS.md` and `reference-ui/UI_COMPONENT_STANDARD.md`.

Production now includes the reviewed P20–P24 CRUD schema and Core-principal application, with [P23/P24 actual live release acceptance](docs/procurement/release/PROC-OPS-P23-P24-LIVE-ACCEPTANCE-20261022.md). Real-account vendor and recurring-purchase UI journeys passed with independent owner/audit reconciliation. P23 document/workflow activation prerequisites remain outstanding. Do not deploy source branches, apply migrations or enable workflow/Finance flags to complete this UI task.

**Current coordinator milestone:** [P24 recurring planning private acceptance](docs/procurement/release/PROC-07A-P24-PUBLISHED-ACCEPTANCE-20261022.md) is complete and merged. The coordinator completed the [combined P23/P24 production release gates](docs/procurement/release/PROC-OPS-P23-P24-RELEASE-PLAN-20261022.md) and owns the unfinished P25 principal-based request approvals. P25 is a draft, default-off source milestone, not deployed. Manus retains `MANUS-CORE-BUILD-03`; do not duplicate recurring CRUD, invoice review, settings, backend migrations or deployment. Finance/AP and payments belong to the future ERP Finance module, which is not built; no substitute ledger or fake funding/payment success is authorized.

**Branch-scope regression fixed:** preserve PR #51's verified legacy-name-to-ID hand-off before readiness or business requests. Do not silently clear a stored branch, infer another company, choose the first match, or reintroduce a read-only gate that strands the branch picker. See [the incident and real-account verification](docs/procurement/release/PROC-UI-BRANCH-HANDOFF-20261022.md). The actual signed-in account can open Vendors/My Requests and their new forms; this narrow acceptance does not certify every workflow.

**Approval work ownership:** PR #55 preserves the existing UI, handles the separate optional principal capability and fixes in-session same-command recovery. It does not enable approval execution or prove real API/PostgreSQL workflow success. The coordinator owns the remaining P25 finite-role/runtime acceptance and durable recovery; do not duplicate these changes or enable Finance/approval flags.

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
