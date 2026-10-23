# Procurement original UI restoration

Date: 23 October 2026. User correction: keep imported Greenwebb presentation and interactions, ERP branding and shared controls; connect backend through adapters. Do not replace the supplied screens with smaller parallel interfaces.

## Reviewed source

Original repository: https://github.com/Greenwebb/zoho-procurement-ui, actual fetched HEAD `6f0c305d96edeed6b3a062dbfdbb9521f8d1eebd`. ERP-adapted layout reference: `d104f596183e7a012a04578a679aac795e1489d4` (shared ERP controls and shell). Restoration began from module-connect main `30e17e9aad1bf86d3c94086b42fca590c9b0f05f`; reviewed PR #57 is now merged in published main `5af64676a2ce2ef64c6e874521f442841fdc130d`. The raw Greenwebb theme is a comparison reference, not the branding target. Source comparison is not a claim that public production pixels were checked in this audit.

## Confirmed regression

`reference-ui/src/app/App.tsx` selects parallel `Live*` views in live mode. API adapters are necessary; alternative reduced presentation is not. `LiveInvoiceList` uses a six-column table inside one Panel in place of original RecordList toolbar, ListTools, status/search/filter controls, selection, configurable table and detail drawer. `/inbox` also routes to this reduced bill list instead of PayablesInbox. Bill editors/details use LiveInvoiceEditor/Detail instead of the imported TransactionEditor/RecordDetail structures. Requests, RFQs and orders have similar route substitutions and need individual parity review. Some vendor/receiving presentation has already been restored; preserve those improvements rather than blindly reverting all files.

## Route restoration register

| Surface | Original presentation | Adapter to preserve | Acceptance |
| --- | --- | --- | --- |
| Bills list | RecordList, ListTools, StandardDataTable | LiveInvoiceList HTTP/scope/exact amounts | Original toolbar/list structure; scoped server rows, empty/loading/error/denied, pagination |
| Bill new/edit | TransactionEditor | LiveInvoiceEditor/edit validation/CAS | Original sections and line controls; no draft loss or forged field persistence |
| Bill detail | RecordDetail header/tabs/overview/lines/related/comments/history | LiveInvoiceDetail plus invoice documents/comparison/unit review | Original detail layout plus real supported controls; unsupported related/comment actions visible and explained |
| Inbox | PayablesInbox | Dedicated intake adapter | Inbox layout/intake channels preserved; not silently replaced by bill directory |
| Payments | Original RecordList/TransactionEditor/RecordDetail | New manual-payment workflow adapter | Assigned approval/record/confirm, no browser-local fake Paid state |
| Batch payments | BatchPayments/BatchPaymentEditor | Future persisted batch adapter | Original selection/review structure; no simulated bank send or local-only success |
| Requests/RFQs/PO | RecordList/RecordDetail/TransactionEditor or RfqWizard | Existing LiveRequests/LiveSourcing/LiveOrders | Per-control parity review before release; preserve scope/recovery/business guards |

## Implementation rules

1. Prefer shared imported presentation components with supplied rows, action flags and callbacks. Keep server state, authorization and network handling in adapters.
2. Preserve ERP logo/header/sidebar/fonts/buttons/tokens and original page organization; no copying original theme over shared branding.
3. Preserve proven original workflows and all existing real API features. Do not route live traffic back to AppProvider demo mutations to restore visuals.
4. Keep unsupported controls and sections discoverable with truthful disabled/reason states; absence of backend integration is not permission to remove the layout.
5. Filters, counts, selection and exports must state whether they cover loaded rows or the full scoped server dataset. Never imply server-wide filtering/export from one fetched page.
6. Keep scope remounts/abort guards, exact decimal values, optimistic versions, unknown-response recovery and server action authorization.
7. Compare desktop/mobile screenshots against original structures, then run actual Playwright flows against API/PostgreSQL. Mocked visual tests are not persistence proof. Do not certify all UI restored from one page.

## Current execution

UI [PR #57](https://github.com/georgemunganga/module-connect/pull/57) merged normally at `5af64676a2ce2ef64c6e874521f442841fdc130d`, with reviewed head `416b8ad516ffd77526fd51379da23254efc386e7`. Bills list, editor, detail and Inbox retain the ERP-adapted original structures and existing API adapters. The list scope is explicitly the current loaded page; unsupported actions remain visible with reasons. Document state survives detail tabs and refresh. Unknown write responses retain recovery locks.

Tested application/config source: `ff0c3483d293f82d9dc1f66b141778f2c6310dc4`. TypeScript/live production build and two focused units passed. All 23 controlled Playwright cases passed against the staged production bundle with one worker, zero retries, failures, skips or flaky results. Cases cover the retained form/detail/list structure, permissions, unknown responses, upload recovery, discard protection and desktop/phone overflow. Final source/assets remained unchanged during verification. Private result SHA256: `a6199da230ba9fd705cfa9a15707df60c39f3af1f796209a348a418a7069fb36` in `/home/amizo/erp-backups/procurement-imported-ui-restoration-20261023_run06/result.json`. Earlier failed/interrupted runs remain retained.

These HTTP-fixture browser tests verify client behavior and presentation, not fresh real API/PostgreSQL persistence or public deployment. The accepted static assets were subsequently deployed on 23 October; see [the public UI release record](release/PROC-UI-RESTORATION-DEPLOYMENT-20261023.md). This changes no backend/schema or feature flags. Other pages still need individual layout parity and integration acceptance.

Manual payment documentation/source foundation remains separate and unregistered. The future Finance module's absence must not remove Payments or Batch Payments presentation: implement assigned approval, recording of a real external payment, independent confirmation and future Finance reconciliation behind the original screens. Do not manufacture bank execution, AP posting, synced payment facts or local-only Paid state. Terms/notes capture, pre-save files, Inbox file/email/portal intake, related records/comments/history and full-dataset operations remain integration gaps where unavailable.
