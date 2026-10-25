# Zoho Books UI Reference & Prototype

A separate, unpublished React + TypeScript + Tailwind project for the user-selected Zoho Books Global organization. It is distinct from Zoho Procurement and does not change the Procurement project or repositories.

## Current state

The frontend now has 124 detailed, multi-step workflow paths across Sales (28), Purchases (27), Banking (16), Accountant (14), Items & Projects (17), and Settings & Reports (22). They share typed domain definitions, field renderers, a step/review workbench, a validated local workflow engine, a persistent browser-local store, linked record/artifact details, histories and completion receipts. The registry-generated coverage inventory is in `docs/workflow-depth/workflow-inventory.md`; engine cases/results are in `tests/fixtures/workflow-cases.json` and `docs/workflow-depth/engine-test-results.json`.

A workflow can be opened from its module list, an existing record's contextual actions, or the searchable Workflow Library. Multi-step input, allocations, item/journal rows, CSV mapping, validation, review, back/cancel, and next actions are represented as usable controls rather than explanation-only dialog placeholders. Reports filter actual fictional workspace data; document amounts use a shared line/discount/tax/shipping formula. Approvals, payment records, journal posting, bank matching, automation, and integration flows change only synthetic local state/artifacts and histories.

## Test and run commands

`pnpm test:engine` executes every registered effect and targeted invalid-payment, mismatched-selection, premature-conversion, unbalanced-journal, non-executable automation criteria, receipt-routing, import-default, and amount-calculation checks. `pnpm test` runs real-browser positive completion through all 124 workflows plus invalid-input/cancel preservation, mobile rendering/navigation, direct route health, console errors, and outbound-request detection. `pnpm build` runs strict TypeScript checking and produces the Vite bundle. `pnpm dev` serves the development preview on the project runtime.

## Tenant evidence and safety

The user confirmed the Books dashboard was ready before authenticated observation. Tenant inspection and the fictional local prototype are kept separate: no real Zoho Books record was created, edited, deleted, sent, exported, imported, paid, approved or otherwise changed. The 124 registry flows and passing prototype tests do **not** prove that all corresponding steps were visited or finished in the selected Books tenant. See `docs/verified-screen-patterns.md` and `docs/reference-manifest.json` for the partial tenant screen inventory and remaining unverified paths.

Rendered browser screenshots appeared in task results, but My Browser returned no local screenshot files; one Workflow Rules screenshot upload explicitly failed. `reference-private/` remains ignored and empty of raw image captures. A complete private raw-screenshot archive is therefore **not** claimed.

## Scope and delivery boundary

The app has no backend, database, authentication, Zoho API connection or real accounting operation. It uses fictional seed records stored under project-specific browser `localStorage` keys only. External email, portals, file import into Zoho, payment transfers, bank connections, approvals, schedules, integrations and ledger postings are never executed against outside services. CSV export can produce a local file from fictional report rows only. The prototype has not been published; Webdev server/database capabilities and auto-publish remain off.

`plan.md` documents implementation and design; `TODO.md` tracks remaining outcomes; `docs/books-coverage-plan.md` and `docs/domains/` contain public-document candidate research rather than tenant proof. The old unused screen modules that contained explanation-only placeholders have been removed; active routes lead to the shared workflow engine and data-backed reports. Raw tenant references, screenshots and secrets are excluded from application source and Git.

The Books project's managed repository remains canonical. `Greenwebb/zoho-books-ui-reference` is a separate private backup only. Neither repository includes raw tenant screenshots.
