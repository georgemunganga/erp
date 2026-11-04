# Owner CRUD and company-context verification — 4 November 2026

## Published source and runtime

ERP main `ae32466e2959b79e62ae1d0a3b457c58110389ae` includes PR197, product `e87fcfc741bf0b8115ed2f9d9cfd693237f53080`: pure PO advance and invoice allocation capacity contracts. These types have no registered API/persistence integration and are not deployed. The accepted c93/M2 verification source plus this foundation passed **2,065 Procurement tests**, including37 new cases. This is not a fresh full-main/B1c build or native M3 acceptance.

UI main `2b3357d2de3c383ea2926264abc701b1564a5895` includes PR108/109. The original imported presentation and shared ERP controls remain. Home supports company-wide context; branch-owned forms offer a permitted branch selector; new-request actions open the form; company Buying rules opens without a transaction branch. Backend authorization is unchanged.

Latest frontend publication: `/home/amizo/procurement-ui/releases/company-settings-7b63c385-20261104`. The separately qualified API remains image `sha256:3da35edf636057662ef30918399397e4a82fc928878fe23ec471a7f08b20293f`,239 tables. No financial flag, business policy, approver, limit, role or employee link was invented/activated. Do not deploy current main over production while held B1c readiness remains unresolved.

## Evidence and limits

- UI108:25 unique mocked-browser cases passed cumulatively across three runs. Earlier Home locator failures remain retained. UI109:11 Chromium cases passed. Both changes passed984 UI unit checks, TypeScript and live-mode production builds. These are separate from the signed-in production cases below.
- Real owner-cookie vendor create/edit/reload/withdraw succeeded. Item create/edit succeeded; its draft was already Inactive. The script incorrectly expected an active-only deactivation button; guarded cleanup and native SQL confirmed the revised inactive record. Do not label that entire script green or claim UI deactivation coverage.
- Real request create/edit/reload preserved four-decimal quantity/price. A separate request completed UI withdrawal. The first request cleanup used the API after an incorrect exact accessible-label assertion. Another attempt supplying eight decimals correctly failed client validation before POST; it was an unexpected script failure, not a planned passing negative test.
- Native SQL confirmed both synthetic requests Withdrawn, their original Core user attribution with no employee link, exact four-decimal values and zero procurement allocations. No request was submitted/approved or converted to a commercial commitment.
- A different permitted selected branch returned404 for the synthetic request. This does not prove another user's branch assignment or cross-tenant matrix.
- Actual Core workflow directory returned200 with five registrations. Core Approval workflows and company-wide Buying rules rendered; the unsaved editor loaded actual role/department/approver lookups200, with zero Procurement mutations and zero page errors. All-company preference was set using the verified Core company; no header-click acceptance is inferred.
- All owned test sessions were revoked and checked. The first probe used an incorrect logout route; exactly its one session was subsequently revoked under account/time/user-agent guards. Other sessions were preserved.
- After latest cutover,48 exact public product/compatibility files passed. Earlier asset URLs remain served from immutable publications. Unknown asset requests return404. The backend, Core frontend and business flags were not changed by these UI releases.

Private evidence is retained in `erp-backups/procurement-module-completion-20261104_run01`, `procurement-company-context-release-20261104_run01` and `procurement-company-settings-release-20261104_run01` beneath `/home/amizo`. Never publish their account/session/transaction payloads. The latest `verification-index.json` binds release files, public checks, cutover, actual settings browser result and native zero-active-session result.

For rollback use the latest release packet's `nginx-rollback-with-assets.conf`: it restores the previous HTML entry while retaining all published assets. Existing releases are write-once. Do not restore an older alias configuration that breaks newly published asset URLs.

## Remaining acceptance

This is bounded cumulative owner draft-CRUD and context proof, not full-module completion or business-owner UAT sign-off. Actual staff authority, approved limits/funding/sourcing policy and independent supervisor/executive/Accounts flows remain required. M3 still needs typed source persistence, genuine migration/readiness, command/API integration, allocation/reversal, preserved payment UI and native/browser/recovery acceptance. The complete [remaining register](REMAINING-MODULE-ROADMAP-20261104.md) retains the other lifecycle gaps.

The root disk has approximately200MiB free. Safe cache checks did not reclaim the apparent size of hardlinked packages. Protected database, publication and rollback evidence was retained; large restore/migration jobs require capacity or an explicit retention decision. No blanket prune was performed.
