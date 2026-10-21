# Manus ERP collaboration handoff

**Updated:** 21 October 2026 (UTC)  
**Primary project and coordination repository:** https://github.com/georgemunganga/erp  
**Coordinator:** primary Codex agent  
**Worker:** Manus on a separate computer/GitHub account  
**Assigned milestone:** `MANUS-PROC-VENDOR-01` — connect and verify the existing vendor UI and ERP contracts  
**Log:** [docs/collaboration/manus/STATUS.md](docs/collaboration/manus/STATUS.md)

## Start here

The user wants collaboration managed in this ERP repository. Read the latest version of this file on `origin/main` before working. This is the authoritative task file; the earlier handoff in the UI repository is superseded.

**The ERP already has the Procurement UI. Do not rebuild, re-import, or make a duplicate copy.** Its currently deployed source is in a separate existing repository, as described below. Work with that implementation. Repository consolidation is not part of this milestone.

## Verified repository and deployment map

| Responsibility | Existing source / location | How to use it |
| --- | --- | --- |
| ERP/backend, Core and Procurement contracts, schemas and engineering docs | This `georgemunganga/erp` repository | Primary work, coordination and contract source |
| Tracked HRM frontend | `modules/hrm/frontend/module-connect/` in this repository | Reuse its established shared-component/Core contracts; do not assume it contains the separate Procurement pages |
| Existing Procurement frontend | `georgemunganga/module-connect`, particularly `reference-ui/` and its required `src/platform/` clients | Secondary source dependency for the actual existing vendor screens; edit there only where necessary and link its PR to the ERP task PR |
| Current live `/procurement/workspace/` | Verified server mapping serves the existing `procurement-ui/reference-ui/dist` build | Explains why the UI is visible in ERP even though its Procurement source is tracked separately; not permission to write production |

The coordinator verified this map against current repository trees and the live server configuration on 21 October 2026. Current default-branch code and active implementation branches differ; use the implementation baselines below. Do not overwrite the working branch with an older/default application snapshot or recent unrelated branding changes.

## Baselines and claims

1. Read this repository's `AGENTS.md` and `docs/engineering/GW-ENG-STD-001.md`. Procurement is Tier3. Preserve the existing 28-section feature contracts.
2. ERP work starts from `codex/procurement-rfq-finance-identity-integration`, checkpoint `445a5cb` or a coordinator-agreed descendant. Create `manus/procurement-vendor-integration` or another clearly named branch.
3. Read [the milestone log](docs/collaboration/manus/STATUS.md) and check existing draft PRs for an active claim. This ERP repo currently has GitHub issues disabled: use this file, your draft PR and PR comments. Do not enable repository features or change account settings.
4. Your first commit should add your claim to `docs/collaboration/manus/STATUS.md` on your branch: GitHub handle, branch, start commit and owned files. Open a **draft ERP PR against `codex/procurement-rfq-finance-identity-integration`** with milestone ID `MANUS-PROC-VENDOR-01` in the title. That PR is the coordination thread, even if initial work is only the contract audit/status.
5. The implementation branch may predate this default-branch handoff. Copy only these coordination documents when needed; do not merge unrelated main code to obtain them.
6. If UI edits are needed, use the existing frontend baseline specified below, open its separate draft PR, and link both PRs. Do not create a second Procurement frontend inside ERP as a shortcut. If no frontend change is needed, verify the actual existing integration and report evidence.

## Ownership and workflow

| Milestone | Owner | State | Boundaries |
| --- | --- | --- | --- |
| `MANUS-PROC-VENDOR-01` | Manus, claim through draft ERP PR | Ready for claim | Vendor UI/API contract integration, save/context safety, focused tests and evidence |
| Sourcing policy/P29/approval configuration, shared Core documents and transaction work | Primary agent and existing backend agents | In progress | Reserved: no edits to these files by Manus |
| Further Manus tasks | Unassigned | Await assignment | Do not start another module based on assumptions |

ERP deliverables include `docs/collaboration/manus/vendor-contract-verification.md`: map each existing UI action to actual endpoint, permission/scope, required/version fields, transition, persistence owner, audit and failure/recovery behavior. Link existing feature specifications instead of duplicating them. Record backend gaps precisely. Keep backend supplier changes separate and ask in your PR before editing a shared file; the coordinator can assign a bounded follow-up. Do not change migrations/schema, Finance, identity, Core settings, shared components, approvals, RFQ or global branding during this milestone.

Use focused commits referencing the milestone. Log exact tests, tested commit, failures/traces and mock-versus-actual boundaries. Preserve failed evidence. An HTTP200 or screenshot alone is not proof of persistence. No production credentials, real personal/banking data, secrets or environment files in commits, issues, screenshots or logs.

The primary agent reviews code before executing untrusted changes, runs relevant integration/Playwright gates, resolves overlaps and records acceptance/merge. **Do not merge, deploy, alter production data, change permissions, run production migrations, activate missing capabilities, force push or rewrite published history.** After this milestone is PR-ready, wait for review or an explicitly assigned next milestone.

## Detailed vendor assignment

## Assignment: complete and verify the live Procurement vendor UI

Work independently while the primary agent completes sourcing backend integration. Coordinate in this ERP repository and open an ERP draft PR immediately for your claim/status/contract evidence. Existing Procurement UI source changes, if needed, belong in its current UI repository and must be linked from the ERP PR. Do not merge or deploy.

### Existing UI source and branch

- Existing Procurement frontend source (secondary code dependency): https://github.com/georgemunganga/module-connect
- Start from `codex/procurement-sourcing-policy-ui`, checkpoint `eaca0d1` or its descendant. Create your own branch, for example `manus/procurement-vendor-integration`.
- Backend contracts, read only: https://github.com/georgemunganga/erp, branch `codex/procurement-rfq-finance-identity-integration`, checkpoint `315380e` or its descendant. Inspect actual Procurement vendor routes and DTOs before wiring fields. Report missing backend contracts in the PR; do not manufacture success or invent endpoints.
- Read applicable AGENTS.md and the existing UI component standard. Preserve existing user work.

### Owned files and boundaries

Own `reference-ui/src/features/LiveVendors.tsx`, vendor-specific helpers/tests, and `src/platform/procurement-vendors-api.ts` where needed. You may reuse existing vendor form components. Do not edit sourcing policies, reviewer selection, RFQ, budgets, shared navigation/branding, Core settings, authentication, global components, database migrations or backend business rules. List any necessary shared change separately for review.

### Required work

1. Audit the existing live vendor list, create, edit, detail, contacts, addresses/sites, qualification and approval/suspension flows against actual backend contracts. Finish available integrations and preserve existing functions. Reuse the imported vendor form layout/tabs and ERP components; do not replace this with a new design or remove fields.
2. Keep simple language, established font/spacing/buttons, floating selects and boolean toggles. Use the existing shared table and centered no-data state. An empty vendor list must offer Add vendor when authorized; only offer Import if a genuine supported import path exists.
3. Fix concrete save safety: edit mode must never fall back to `createVendorDraft` when its version/detail is unavailable. Block save until the exact current vendor/version has loaded. Load or permission failure must not turn an edit into a create.
4. Prevent old detail/list/contact responses or pending mutations from populating another company or account context. Cancel requests and guard stale completions. Preserve form values on an ordinary validation/authorization failure. Never send another company's ID/version/fields in a write.
5. Default currency from organization settings. Preserve fields the editor does not own; do not send empty placeholders that erase existing data. Match real DTOs, lengths, required fields and optimistic versions. Do not put banking secrets in logs, screenshots or browser storage.
6. Handle a lost response honestly. If no backend idempotency/recovery contract exists, do not invent one or automatically repeat a create/update. Explain the unconfirmed result, block duplicate saves, and provide an explicit read-only reconciliation path. A same-name vendor is not proof of the original write.
7. Respect actual server-provided allowed actions and permissions. A portal request does not activate supplier login. Procurement must not approve bank details or execute payments. Do not label mocked/unsupported functions as connected.

### Verification and PR evidence

- Run TypeScript, relevant unit tests and the live-mode production build with the existing package manager and unchanged lockfile unless a justified dependency change is required.
- Add meaningful Playwright coverage on the existing live vendor routes: empty/create/refresh/edit round trip; a load failure cannot create; validation preserves values; stale-version conflict; denied action; company switch during a delayed response; double-click/lost response produces no second mutation; contact/site flows where supported.
- Separate mocked HTTP tests from actual backend tests explicitly. If actual backend access is unavailable, provide precise mocked contract evidence and mark actual persistence/integration unverified. No production credentials or production test writes.
- Use one browser worker and no retries for acceptance evidence; keep failed traces. Report exact commands, source commit, passed/failed counts and limitations. Do not claim the entire Procurement module is complete.
- Keep commits focused and reference `MANUS-PROC-VENDOR-01` and your ERP draft PR. Link any separate existing-UI PR. Include a short workflow checklist, screenshots using synthetic data, and an inventory of remaining API gaps. The primary agent will independently review and test before merge.
