# MANUS-P35-BACKEND-VERIFICATION-06 — independent Procurement source verification

**Status:** Queued for an available Manus worker on separate non-production hardware. This is additive to `MANUS-CORE-BUILD-03`, `MANUS-CORE-DOCUMENT-SCANNER-04`, and `MANUS-P33-UI-VERIFICATION-05`; do not interrupt their workers, checkouts, caches, containers, or evidence. The coordinator reviews all results and controls acceptance.

## Exact starting points

| Source | Pinned reference |
| --- | --- |
| Instruction repository | Published `georgemunganga/erp` `main` at `43bc3d3ac09400c2741cf2a2ece5fb0804f07844` or a later reviewed published descendant |
| Source to verify | ERP branch `codex/p35-contract-review-source` at pushed commit `f3f1ec304cd2d70cd59182ae704f5d6f8bebb288` |
| Prerequisites in that source | Ordinary P32 fix `77889925925cfb9fb44c0e25d7b84ba51d7b04c5` merged through P33 `1e00bdb550f4885393aeaf3b8aee0e222cebaba2`, P34 `9e67878398795dd49b50e78050262b2836eb7a9f`, then P35; no separate cherry-pick or guessed ancestor |
| SDK | `backend/procurement-api/global.json` and `backend/hrm-api/global.json` specify `10.0.400`; record the actual installed SDK and pinned package cache/lock inputs |
| Feature contracts | `docs/procurement/PROC-06-P32-PRINCIPAL-COMMERCIAL-COMPLETION.md`, `PROC-08-P33-PRINCIPAL-PO-REVISIONS.md`, `PROC-07-CONTRACTS-FRAMEWORKS-AND-RECURRING-COMMITMENTS.md`, and `docs/procurement/release/P35-CONTRACT-INTERNAL-REVIEW-DELTA.md` on the pinned source branch |

The pinned P35 branch is **held source**, not published production behavior. P32, P33, and P35 lack their independently accepted genuine EF migrations and runtime readiness proof at assignment time. Do not infer that an existing P30/P31 deployment has these capabilities.

## Hardware and ownership

Use a separate host with at least **8 GiB RAM and 4 GiB free disk** before installing or building. Record OS, CPU, memory, disk, SDK/runtime, package cache or lockfile hash, and source commit before work. Use a fresh checkout and isolated build/artifact directories. Keep one heavy SDK test/build process at a time unless actual resource measurements justify more. The coordinator's constrained machine is running independent Procurement HTTP/database verification; do not run this task there.

Own only verification and focused fixes to compiler errors or test defects demonstrated on the pinned P35 source. Open a **draft ERP PR targeting `codex/p35-contract-review-source`**, with a progress file `docs/collaboration/manus/p35-backend-verification-06-progress.md` on that branch. Do not merge the PR. The coordinator independently reviews patches and merges them into the held source only when appropriate. Avoid changes to Core identity, permissions, policy authority, Finance, source allocation or business thresholds merely to make a test pass.

## Verification sequence

1. Read `AGENTS.md` and `docs/engineering/GW-ENG-STD-001.md`. Verify the remote branch and exact source SHA. Capture a source and package manifest, plus the initial clean working-tree state.
2. Build the existing Procurement infrastructure, application API/host and required Core/HRM dependencies with .NET SDK `10.0.400` using the existing project graph and immutable package inputs. Record exact commands, exit codes, elapsed time and diagnostics. Do not compile against cached assemblies from a different source tip.
3. Run the complete `backend/procurement-api/src/Mightyfin.Erp.Procurement.Tests/Mightyfin.Erp.Procurement.Tests.csproj` suite and the complete `backend/hrm-api/src/Mightyfin.Erp.Hrm.Tests/Mightyfin.Erp.Hrm.Tests.csproj` host suite. Record nonzero discovered/executed counts, passed/failed/skipped totals and test result hashes. A filter with zero tests is a failure of evidence, not a pass.
4. Exercise focused P32/P33/P35 negative tests from the actual source: sealed commercial offers must not leak before opening; incomplete assessments must not imply an award; Draft PO revision must preserve original approved source and reject funded/issued edits, stale CAS and missing receipts; contract review must enforce independent current Core actors, scoped access, immutable votes/receipts and no accidental activation. For the assigned-review worklist specifically, verify the projected `AssignmentId` is the actor's current pending Core assignment (never the submission ID), current company/branch/action and active stage filter rows **before** count and pagination, and unassigned, off-branch, completed or inactive assignments reveal neither rows nor counts. Include denied and cross-actor detail/decision cases; preserve the frozen terms and original command receipt checks. Use actual test names discovered in the source; record missing coverage as a gap rather than inventing test results.
5. Run or author narrowly scoped **in-process HTTP negative tests against the actual Program route map**, without a real database or fake readiness success. P35 `ProcurementContractReviewRoutes.MapRoutes` is deliberately not called in the pinned source, so its held mutation URLs must remain unavailable through the host; do not assert a mapped 503 where the route is absent. Confirm normal authentication and scope gates for mapped P32/P33 endpoints and that default-off options or absent schema readiness deny writes without side effects. Record exact HTTP statuses and response codes observed, plus the route-registration source line. These tests establish an unavailable/default-off boundary only; they are not live transaction proof.
6. If a compiler or test defect is found, preserve the first failed run and make a minimal source fix on the task branch. Add or repair the relevant test without weakening assertions, replacing typed records with mocks, bypassing admission/readiness, changing business limits, granting roles or dropping UI/API behavior. Repeat the affected check and then the complete suites. Send any larger product or model change to the coordinator for review before implementation.

## Explicit limits

- This task does **not** generate or edit EF migrations, designers, snapshots, schema, database grants, private/prod databases or deployment configuration. Do not present `HasPendingModelChanges == false`, live readiness, HTTP transaction success, or migration Up/Down as verified unless the coordinator later supplies and accepts the genuine schema evidence.
- Do not enable P32/P33/P35 feature flags or add default policies, monetary thresholds, fictitious reviewers, local employees, supplier dispatch, PO issue, AP posting or Finance payment execution.
- Source builds and tests may use existing in-memory or synthetic fixtures. Clearly label them as such. A passing unit/host suite is not an actual PostgreSQL/normal-cookie/prod release proof.
- Preserve existing failures and logs. Do not commit credentials, cookies, customer/supplier data, unrestricted traces or private database contents. Report source hashes and sanitized error categories only in the PR.

## Deliverables and review gate

Submit a draft ERP PR with the exact pinned base, hardware/package/source manifest, every command and exit code, discovered/pass/fail/skip counts, first failures and corrected reruns, source diffs, and protected evidence hashes/paths. State each unrun or blocked check explicitly. A separate coordinator review confirms that fixes preserve the P32/P33/P35 28-section contracts and existing P30/P31 behavior. This verification task is complete only when the source checks and negative evidence are reviewable; schema, real HTTP and production activation remain separate gates.
