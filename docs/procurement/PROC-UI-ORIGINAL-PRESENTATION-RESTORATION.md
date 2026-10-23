# Procurement original UI restoration

Date: 23 October 2026. User correction: keep imported Greenwebb presentation and interactions, ERP branding and shared controls; connect backend through adapters. Do not replace the supplied screens with smaller parallel interfaces.

## Reviewed source

Original repository: https://github.com/Greenwebb/zoho-procurement-ui, actual fetched HEAD `6f0c305d96edeed6b3a062dbfdbb9521f8d1eebd`. Published integration baseline: module-connect main `30e17e9aad1bf86d3c94086b42fca590c9b0f05f`. Source comparison is not a claim that public production pixels were checked in this audit.

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

Bills list restoration is assigned to an isolated UI task based on published main. Remaining pages are not yet claimed restored. Manual payment specification remains in a separate branch and does not authorize replacing original Payments UI.
