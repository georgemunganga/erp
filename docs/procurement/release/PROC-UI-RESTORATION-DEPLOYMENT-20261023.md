# Procurement restored UI — public deployment

23 October 2026. The restored Bills directory, bill create/edit form, bill detail tabs and Inbox are published at https://erp.amizopower.co.zm/procurement/workspace/. This is a frontend release, not acceptance of the entire Procurement module.

## Source and scope

- Merged UI main: `5af64676a2ce2ef64c6e874521f442841fdc130d`, PR #57, reviewed head `416b8ad516ffd77526fd51379da23254efc386e7`.
- Tested application/config source: `ff0c3483d293f82d9dc1f66b141778f2c6310dc4`. Only AGENTS/restoration documentation differs from this source to merged main.
- Keep the ERP-adapted imported layout baseline `d104f596183e7a012a04578a679aac795e1489d4`, shared ERP controls, branding, scope, exact-decimal handling and recovery. No raw theme replacement or demo-store fallback.
- Published at the existing Nginx alias `/home/amizo/procurement-ui/reference-ui/dist`. JS `index-p0rAt06L.js`; CSS `index-SeJ_DlMz.css`.
- Backend, database, identities, grants and feature flags are unchanged. P25 and missing Finance integrations are not activated by this deployment.

## Actual deployment and verification

The five candidate artifacts were checked against the retained staged-build manifest. The previous static directory was archived and every archived file hash verified. New dependencies were published first and index replaced atomically last; old hashed assets remain for open browser tabs. The first immediate aggregate public-hash assertion failed without capturing which response differed. The script did not republish or roll back; subsequent explicit per-file verification matched all five accepted bodies. That initial attempt and phase receipt remain retained.

The deployed public assets then passed all 23 original-layout Playwright cases, one worker, zero retries/failures/skips/flaky results. These tests intercept business HTTP with synthetic fixtures: they prove the deployed presentation and client behaviors, not real authenticated database CRUD. Prior staged TypeScript/live build and two focused units passed separately. Native backend live/ready checks remained healthy, and unauthenticated Procurement context returned 401.

Private evidence: `/home/amizo/erp-backups/procurement-ui-restoration-deploy-20261023/`. Acceptance SHA256 `e4877e886088a60892687f589c1198ec05774493b5df343be27c96720509bf62`. Public asset bodies, browser JSON/log/artifacts, pre-release inventory, verified backup and original phase record are retained. This release adds no financial or operational test records.

## Completed branch cleanup

Deleted 13 remote UI branches whose tips exactly matched their merged PR heads and were ancestors of published UI main. No open PR used those heads. Deleted three matching, unattached local branches using normal `git branch -d`; checked-out worktree branches remain. All source commits survive in main. The receipt records every branch, exact tip, merged PR, main and result. No unpublished, advanced or unmerged branch was removed; no worktree, backup, database or test evidence was deleted.

## Remaining integration work

Payments and batch workflows need persistent server adapters behind their retained original screens. Finance is not built; implement assigned manual approval, actual external-payment recording and independent confirmation without pretending to execute a bank transfer or post AP. Other pages still need individual layout parity, permission/data-scope, real API/PostgreSQL and recovery acceptance. Missing terms/notes, pre-save files, Inbox intake, collaboration/history and full-dataset operations remain visible integration gaps. See the restoration register; do not simplify or replace the UI to conceal them.
