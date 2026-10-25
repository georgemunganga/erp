# Detailed gap register

These are 55 grouped findings: implementation gaps, narrower workflows, verification/release gaps, documentation drift and optional platform parity. They are not 55 confirmed production defects. Preserve supplied screens and reuse the owning Core services. Priority is conditional on the chosen launch scope.

See [machine-readable register](gap-register.json) and [closure milestones](CLOSURE-ROADMAP.md).

## BE-01 bulk/import/export/clone/delete/custom views

**Priority:** P2 · **Owner:** Procurement UI/API · **Milestone:** M2

**Scope:** selected operational launch workflows

**Documented expectation:** Broad ERP feature proposals exist; full operation-level parity is not accepted. See erpSpecPaths and per-manual documentation status.

**Current implementation:** Supplier/item/request/order/receive draft readers and scoped single-record commands exist; a reusable rendered toolbar is not a server-backed batch/import/export.

**Gap:** No named import/clone/bulk mutation/export/custom-view routes in inspected item, PR, PO, receive and vendor route maps; preserved ERP records prefer cancellation or retirement over hard delete.

**Acceptance:** Typed scoped import/export, stable field mapping, limited batch commands and private end-to-end tests; irreversible deletion policy decided explicitly.

**Source evidence:** [backend/hrm-api/src/Mightyfin.Erp.Hrm.Api/ProcurementItemEndpoints.cs:18](https://github.com/georgemunganga/erp/blob/c38202e57afff83abbcdcbb05f95a27d38a8be26/backend/hrm-api/src/Mightyfin.Erp.Hrm.Api/ProcurementItemEndpoints.cs#L18); [backend/hrm-api/src/Mightyfin.Erp.Hrm.Api/ProcurementItemEndpoints.cs:168](https://github.com/georgemunganga/erp/blob/c38202e57afff83abbcdcbb05f95a27d38a8be26/backend/hrm-api/src/Mightyfin.Erp.Hrm.Api/ProcurementItemEndpoints.cs#L168); [backend/hrm-api/src/Mightyfin.Erp.Hrm.Api/ProcurementItemEndpoints.cs:267](https://github.com/georgemunganga/erp/blob/c38202e57afff83abbcdcbb05f95a27d38a8be26/backend/hrm-api/src/Mightyfin.Erp.Hrm.Api/ProcurementItemEndpoints.cs#L267)

**ERP specifications:** [PROC-01-SUPPLIER-ONBOARDING-AND-MANAGEMENT.md](https://github.com/georgemunganga/erp/blob/c38202e57afff83abbcdcbb05f95a27d38a8be26/docs/procurement/PROC-01-SUPPLIER-ONBOARDING-AND-MANAGEMENT.md), [PROC-02-PURCHASE-REQUISITION-AND-LINE-ROUTING.md](https://github.com/georgemunganga/erp/blob/c38202e57afff83abbcdcbb05f95a27d38a8be26/docs/procurement/PROC-02-PURCHASE-REQUISITION-AND-LINE-ROUTING.md), [PROC-04-PROCUREMENT-ITEMS-AND-CATALOG.md](https://github.com/georgemunganga/erp/blob/c38202e57afff83abbcdcbb05f95a27d38a8be26/docs/procurement/PROC-04-PROCUREMENT-ITEMS-AND-CATALOG.md), [PROC-08-PURCHASE-ORDERS-AND-AMENDMENTS.md](https://github.com/georgemunganga/erp/blob/c38202e57afff83abbcdcbb05f95a27d38a8be26/docs/procurement/PROC-08-PURCHASE-ORDERS-AND-AMENDMENTS.md), [PROC-09-RECEIVING-INSPECTION-AND-RETURNS.md](https://github.com/georgemunganga/erp/blob/c38202e57afff83abbcdcbb05f95a27d38a8be26/docs/procurement/PROC-09-RECEIVING-INSPECTION-AND-RETURNS.md)

**Zoho references:** [Guide 1](https://www.zoho.com/procurement/help/items/create-items/), [Guide 2](https://www.zoho.com/procurement/help/items/manage-items/), [Guide 3](https://www.zoho.com/procurement/help/items/other-actions-for-items/), [Guide 4](https://www.zoho.com/procurement/help/items/overview/), [Guide 5](https://www.zoho.com/procurement/help/my-requests/manage-purchase-requests/), [Guide 6](https://www.zoho.com/procurement/help/my-requests/other-actions/), [Guide 7](https://www.zoho.com/procurement/help/purchase-orders/create-purchase-orders/), [Guide 8](https://www.zoho.com/procurement/help/purchase-orders/manage-purchase-orders/), [Guide 9](https://www.zoho.com/procurement/help/purchase-orders/other-actions-for-purchase-orders/), [Guide 10](https://www.zoho.com/procurement/help/purchase-orders/share-purchase-orders/), [Guide 11](https://www.zoho.com/procurement/help/purchase-receives/create-purchase-receives/), [Guide 12](https://www.zoho.com/procurement/help/purchase-receives/other-actions-for-purchase-receives/), [Guide 13](https://www.zoho.com/procurement/help/purchase-requests/view-all-purchase-requests/), [Guide 14](https://www.zoho.com/procurement/help/vendors/add-vendors/), [Guide 15](https://www.zoho.com/procurement/help/vendors/manage-vendors/), [Guide 16](https://www.zoho.com/procurement/help/vendors/other-actions-for-vendors/)

## BE-02 module preferences and user-configurable fields

**Priority:** P2 · **Owner:** Procurement policy/Core · **Milestone:** M2

**Scope:** selected operational launch workflows

**Documented expectation:** Broad ERP feature proposals exist; full operation-level parity is not accepted. See erpSpecPaths and per-manual documentation status.

**Current implementation:** Typed fields, fixed exact-amount codecs, category/method policy versions and selected module editors exist; this is narrower than arbitrary metadata/preferences.

**Gap:** Manual quantity precision, duplicate names, numbering, arbitrary status, custom fields/rules/actions and template preferences are broader than typed ERP policy/version records.

**Acceptance:** Published typed policies with owner approval, schema/validation/migration and UI evidence for chosen preferences.

**Source evidence:** [backend/hrm-api/src/Mightyfin.Erp.Hrm.Api/ProcurementItemEndpoints.cs:18](https://github.com/georgemunganga/erp/blob/c38202e57afff83abbcdcbb05f95a27d38a8be26/backend/hrm-api/src/Mightyfin.Erp.Hrm.Api/ProcurementItemEndpoints.cs#L18); [backend/hrm-api/src/Mightyfin.Erp.Hrm.Api/ProcurementItemEndpoints.cs:168](https://github.com/georgemunganga/erp/blob/c38202e57afff83abbcdcbb05f95a27d38a8be26/backend/hrm-api/src/Mightyfin.Erp.Hrm.Api/ProcurementItemEndpoints.cs#L168); [backend/hrm-api/src/Mightyfin.Erp.Hrm.Api/ProcurementItemEndpoints.cs:267](https://github.com/georgemunganga/erp/blob/c38202e57afff83abbcdcbb05f95a27d38a8be26/backend/hrm-api/src/Mightyfin.Erp.Hrm.Api/ProcurementItemEndpoints.cs#L267)

**ERP specifications:** [PROC-01-SUPPLIER-ONBOARDING-AND-MANAGEMENT.md](https://github.com/georgemunganga/erp/blob/c38202e57afff83abbcdcbb05f95a27d38a8be26/docs/procurement/PROC-01-SUPPLIER-ONBOARDING-AND-MANAGEMENT.md), [PROC-02-PURCHASE-REQUISITION-AND-LINE-ROUTING.md](https://github.com/georgemunganga/erp/blob/c38202e57afff83abbcdcbb05f95a27d38a8be26/docs/procurement/PROC-02-PURCHASE-REQUISITION-AND-LINE-ROUTING.md), [PROC-04-PROCUREMENT-ITEMS-AND-CATALOG.md](https://github.com/georgemunganga/erp/blob/c38202e57afff83abbcdcbb05f95a27d38a8be26/docs/procurement/PROC-04-PROCUREMENT-ITEMS-AND-CATALOG.md), [PROC-06-STRATEGIC-SOURCING-AND-AWARD.md](https://github.com/georgemunganga/erp/blob/c38202e57afff83abbcdcbb05f95a27d38a8be26/docs/procurement/PROC-06-STRATEGIC-SOURCING-AND-AWARD.md), [PROC-08-PURCHASE-ORDERS-AND-AMENDMENTS.md](https://github.com/georgemunganga/erp/blob/c38202e57afff83abbcdcbb05f95a27d38a8be26/docs/procurement/PROC-08-PURCHASE-ORDERS-AND-AMENDMENTS.md)

**Zoho references:** [Guide 1](https://www.zoho.com/procurement/help/items/create-items/), [Guide 2](https://www.zoho.com/procurement/help/items/overview/), [Guide 3](https://www.zoho.com/procurement/help/items/preferences/), [Guide 4](https://www.zoho.com/procurement/help/purchase-orders/preferences/), [Guide 5](https://www.zoho.com/procurement/help/purchase-receives/preferences/), [Guide 6](https://www.zoho.com/procurement/help/purchase-requests/purchase-request-preferences/), [Guide 7](https://www.zoho.com/procurement/help/request-for-quotes/preferences/), [Guide 8](https://www.zoho.com/procurement/help/request-for-quotes/preferences//), [Guide 9](https://www.zoho.com/procurement/help/vendors/preferences/)

## BE-03 PR recall/forward/hold/processed/reapprove

**Priority:** P1 · **Owner:** Core approval + Procurement · **Milestone:** M3

**Scope:** selected operational launch workflows

**Documented expectation:** Broad ERP feature proposals exist; full operation-level parity is not accepted. See erpSpecPaths and per-manual documentation status.

**Current implementation:** Request submit and shared Core review/decisions exist in source; version/source locks intentionally constrain recall and later modification.

**Gap:** Current Core-case transitions and source locks do not prove every manual status reversal or submitter-selected approver.

**Acceptance:** Source and Core history tests for each proposed transition, current authority and retained effects.

**Source evidence:** [backend/hrm-api/src/Mightyfin.Erp.Hrm.Api/ProcurementApprovalExecutionRoutes.cs:24](https://github.com/georgemunganga/erp/blob/c38202e57afff83abbcdcbb05f95a27d38a8be26/backend/hrm-api/src/Mightyfin.Erp.Hrm.Api/ProcurementApprovalExecutionRoutes.cs#L24); [backend/hrm-api/src/Mightyfin.Erp.Hrm.Api/ProcurementApprovalExecutionRoutes.cs:42](https://github.com/georgemunganga/erp/blob/c38202e57afff83abbcdcbb05f95a27d38a8be26/backend/hrm-api/src/Mightyfin.Erp.Hrm.Api/ProcurementApprovalExecutionRoutes.cs#L42)

**ERP specifications:** [PROC-03-APPROVAL-AND-BUDGET-CONTROL.md](https://github.com/georgemunganga/erp/blob/c38202e57afff83abbcdcbb05f95a27d38a8be26/docs/procurement/PROC-03-APPROVAL-AND-BUDGET-CONTROL.md)

**Zoho references:** [Guide 1](https://www.zoho.com/procurement/help/my-requests/approvals-for-my-requests/), [Guide 2](https://www.zoho.com/procurement/help/my-requests/manage-purchase-requests/), [Guide 3](https://www.zoho.com/procurement/help/my-requests/overview/), [Guide 4](https://www.zoho.com/procurement/help/purchase-requests/manage-purchase-requests/), [Guide 5](https://www.zoho.com/procurement/help/purchase-requests/purchase-request-preferences/)

## BE-04 PR attachments and scanner truth

**Priority:** P1 · **Owner:** Core Documents + Procurement · **Milestone:** M1

**Scope:** selected operational launch workflows

**Documented expectation:** Broad ERP feature proposals exist; full operation-level parity is not accepted. See erpSpecPaths and per-manual documentation status.

**Current implementation:** Request lines and associated data have typed routes; generic document storage alone is not a request-specific upload/view/retirement workflow.

**Gap:** PR typed source has fields/lines; the manual’s upload, preview and delete interaction is not established by PR routes.

**Acceptance:** Real scanner Clean/Available and authorized document-use API plus before/after owner proof.

**Source evidence:** [backend/hrm-api/src/Mightyfin.Erp.Hrm.Api/ProcurementRequestRoutes.cs:20](https://github.com/georgemunganga/erp/blob/c38202e57afff83abbcdcbb05f95a27d38a8be26/backend/hrm-api/src/Mightyfin.Erp.Hrm.Api/ProcurementRequestRoutes.cs#L20); [backend/hrm-api/src/Mightyfin.Erp.Hrm.Api/ProcurementRequestRoutes.cs:35](https://github.com/georgemunganga/erp/blob/c38202e57afff83abbcdcbb05f95a27d38a8be26/backend/hrm-api/src/Mightyfin.Erp.Hrm.Api/ProcurementRequestRoutes.cs#L35); [backend/hrm-api/src/Mightyfin.Erp.Hrm.Api/ProcurementRequestRoutes.cs:65](https://github.com/georgemunganga/erp/blob/c38202e57afff83abbcdcbb05f95a27d38a8be26/backend/hrm-api/src/Mightyfin.Erp.Hrm.Api/ProcurementRequestRoutes.cs#L65)

**ERP specifications:** [PROC-02-PURCHASE-REQUISITION-AND-LINE-ROUTING.md](https://github.com/georgemunganga/erp/blob/c38202e57afff83abbcdcbb05f95a27d38a8be26/docs/procurement/PROC-02-PURCHASE-REQUISITION-AND-LINE-ROUTING.md)

**Zoho references:** [Guide 1](https://www.zoho.com/procurement/help/my-requests/create-purchase-requests/)

## BE-05 PDF/print/share/comments/notifications

**Priority:** P2 · **Owner:** Procurement + Core documents · **Milestone:** M7

**Scope:** selected operational launch workflows

**Documented expectation:** Broad ERP feature proposals exist; full operation-level parity is not accepted. See erpSpecPaths and per-manual documentation status.

**Current implementation:** Transaction audit and outbox records exist; contextual comments, business document rendering and delivery require separate adapters.

**Gap:** Inspected module routes lack named PDF/export/comment/email operations and retained template/PII rules for the manual flows.

**Acceptance:** Scoped rendered artifact or notification, permissions, no-leak tests, retained audit and delivery receipts.

**Source evidence:** [backend/hrm-api/src/Mightyfin.Erp.Hrm.Api/ProcurementOrderDraftRoutes.cs:15](https://github.com/georgemunganga/erp/blob/c38202e57afff83abbcdcbb05f95a27d38a8be26/backend/hrm-api/src/Mightyfin.Erp.Hrm.Api/ProcurementOrderDraftRoutes.cs#L15); [backend/hrm-api/src/Mightyfin.Erp.Hrm.Api/ProcurementPrincipalOrderDraftRoutes.cs:43](https://github.com/georgemunganga/erp/blob/c38202e57afff83abbcdcbb05f95a27d38a8be26/backend/hrm-api/src/Mightyfin.Erp.Hrm.Api/ProcurementPrincipalOrderDraftRoutes.cs#L43); [backend/hrm-api/src/Mightyfin.Erp.Hrm.Api/ProcurementPrincipalOrderDraftRoutes.cs:86](https://github.com/georgemunganga/erp/blob/c38202e57afff83abbcdcbb05f95a27d38a8be26/backend/hrm-api/src/Mightyfin.Erp.Hrm.Api/ProcurementPrincipalOrderDraftRoutes.cs#L86)

**ERP specifications:** [PROC-08-PURCHASE-ORDERS-AND-AMENDMENTS.md](https://github.com/georgemunganga/erp/blob/c38202e57afff83abbcdcbb05f95a27d38a8be26/docs/procurement/PROC-08-PURCHASE-ORDERS-AND-AMENDMENTS.md)

**Zoho references:** [Guide 1](https://www.zoho.com/procurement/help/my-requests/download-and-print-pr/), [Guide 2](https://www.zoho.com/procurement/help/my-requests/overview/), [Guide 3](https://www.zoho.com/procurement/help/purchase-orders/other-actions-for-purchase-orders/), [Guide 4](https://www.zoho.com/procurement/help/purchase-orders/share-purchase-orders/), [Guide 5](https://www.zoho.com/procurement/help/purchase-receives/other-actions-for-purchase-receives/), [Guide 6](https://www.zoho.com/procurement/help/request-for-quotes/other-actions-for-request-for-quotes/), [Guide 7](https://www.zoho.com/procurement/help/request-for-quotes/share-request-for-quotes/), [Guide 8](https://www.zoho.com/procurement/help/vendors/other-actions-for-vendors/)

## BE-06 bulk PO approval/configurable presets

**Priority:** P2 · **Owner:** Core approval + Procurement · **Milestone:** M5

**Scope:** selected operational launch workflows

**Documented expectation:** Broad ERP feature proposals exist; full operation-level parity is not accepted. See erpSpecPaths and per-manual documentation status.

**Current implementation:** Direct-order review uses current authority and shared Core cases; that does not implement every bulk or selectable approval preset.

**Gap:** Principal PO Core review exists; bulk submit/decision, arbitrary Simple/Multi-Level/Custom presets and email notification are not established.

**Acceptance:** Bounded batch atomicity and policy-stage authority tests; notification owner proof if enabled.

**Source evidence:** [backend/hrm-api/src/Mightyfin.Erp.Hrm.Api/ProcurementOrderDraftRoutes.cs:15](https://github.com/georgemunganga/erp/blob/c38202e57afff83abbcdcbb05f95a27d38a8be26/backend/hrm-api/src/Mightyfin.Erp.Hrm.Api/ProcurementOrderDraftRoutes.cs#L15); [backend/hrm-api/src/Mightyfin.Erp.Hrm.Api/ProcurementPrincipalOrderDraftRoutes.cs:43](https://github.com/georgemunganga/erp/blob/c38202e57afff83abbcdcbb05f95a27d38a8be26/backend/hrm-api/src/Mightyfin.Erp.Hrm.Api/ProcurementPrincipalOrderDraftRoutes.cs#L43); [backend/hrm-api/src/Mightyfin.Erp.Hrm.Api/ProcurementPrincipalOrderDraftRoutes.cs:86](https://github.com/georgemunganga/erp/blob/c38202e57afff83abbcdcbb05f95a27d38a8be26/backend/hrm-api/src/Mightyfin.Erp.Hrm.Api/ProcurementPrincipalOrderDraftRoutes.cs#L86)

**ERP specifications:** [PROC-08-PURCHASE-ORDERS-AND-AMENDMENTS.md](https://github.com/georgemunganga/erp/blob/c38202e57afff83abbcdcbb05f95a27d38a8be26/docs/procurement/PROC-08-PURCHASE-ORDERS-AND-AMENDMENTS.md)

**Zoho references:** [Guide 1](https://www.zoho.com/procurement/help/purchase-orders/approvals-for-purchase-orders/)

## BE-07 partial PO line cancellation/reopen

**Priority:** P1 · **Owner:** Procurement + Finance · **Milestone:** M5

**Scope:** selected operational launch workflows

**Documented expectation:** Broad ERP feature proposals exist; full operation-level parity is not accepted. See erpSpecPaths and per-manual documentation status.

**Current implementation:** Controlled direct-order edits, cancellation and source release exist; an issued order needs separately accepted partial cancellation/reopening rules.

**Gap:** Current direct edit/cancel/release commands do not establish post-issue unbilled line cancellation or reopening with downstream receipt/bill conservation.

**Acceptance:** Source/receipt/bill capacity and history proof including concurrent partials.

**Source evidence:** [backend/hrm-api/src/Mightyfin.Erp.Hrm.Api/ProcurementOrderDraftRoutes.cs:15](https://github.com/georgemunganga/erp/blob/c38202e57afff83abbcdcbb05f95a27d38a8be26/backend/hrm-api/src/Mightyfin.Erp.Hrm.Api/ProcurementOrderDraftRoutes.cs#L15); [backend/hrm-api/src/Mightyfin.Erp.Hrm.Api/ProcurementPrincipalOrderDraftRoutes.cs:43](https://github.com/georgemunganga/erp/blob/c38202e57afff83abbcdcbb05f95a27d38a8be26/backend/hrm-api/src/Mightyfin.Erp.Hrm.Api/ProcurementPrincipalOrderDraftRoutes.cs#L43); [backend/hrm-api/src/Mightyfin.Erp.Hrm.Api/ProcurementPrincipalOrderDraftRoutes.cs:86](https://github.com/georgemunganga/erp/blob/c38202e57afff83abbcdcbb05f95a27d38a8be26/backend/hrm-api/src/Mightyfin.Erp.Hrm.Api/ProcurementPrincipalOrderDraftRoutes.cs#L86)

**ERP specifications:** [PROC-08-PURCHASE-ORDERS-AND-AMENDMENTS.md](https://github.com/georgemunganga/erp/blob/c38202e57afff83abbcdcbb05f95a27d38a8be26/docs/procurement/PROC-08-PURCHASE-ORDERS-AND-AMENDMENTS.md)

**Zoho references:** [Guide 1](https://www.zoho.com/procurement/help/purchase-orders/cancel-purchase-orders/), [Guide 2](https://www.zoho.com/procurement/help/purchase-orders/overview/)

## BE-08 PO-to-bill and bill-controlled lifecycle

**Priority:** P1 · **Owner:** Procurement invoice intake/source; Finance owns later AP posting · **Milestone:** M6

**Scope:** selected operational launch workflows

**Documented expectation:** Broad ERP feature proposals exist; full operation-level parity is not accepted. See erpSpecPaths and per-manual documentation status.

**Current implementation:** Procurement invoice intake and retained source/comparison groundwork exist. A one-click eligible order/receive-to-invoice flow is not fully integrated; Finance AP posting is a separate boundary.

**Gap:** A direct PO→typed Procurement invoice Draft/link/source/capture action and Billed/Partially Billed receive projection are not established. P31 invoice source/comparison exists but genuine scanned invoice evidence remains gated; later Finance AP posting is a separate owner, not an excuse to omit Procurement Draft CRUD.

**Acceptance:** Producer-backed PO→invoice Draft/source link and scan-backed invoice evidence with exact line/quantity reconciliation; then separate Finance posting acceptance.

**Source evidence:** [backend/hrm-api/src/Mightyfin.Erp.Hrm.Api/ProcurementOrderDraftRoutes.cs:15](https://github.com/georgemunganga/erp/blob/c38202e57afff83abbcdcbb05f95a27d38a8be26/backend/hrm-api/src/Mightyfin.Erp.Hrm.Api/ProcurementOrderDraftRoutes.cs#L15); [backend/hrm-api/src/Mightyfin.Erp.Hrm.Api/ProcurementPrincipalOrderDraftRoutes.cs:43](https://github.com/georgemunganga/erp/blob/c38202e57afff83abbcdcbb05f95a27d38a8be26/backend/hrm-api/src/Mightyfin.Erp.Hrm.Api/ProcurementPrincipalOrderDraftRoutes.cs#L43); [backend/hrm-api/src/Mightyfin.Erp.Hrm.Api/ProcurementPrincipalOrderDraftRoutes.cs:86](https://github.com/georgemunganga/erp/blob/c38202e57afff83abbcdcbb05f95a27d38a8be26/backend/hrm-api/src/Mightyfin.Erp.Hrm.Api/ProcurementPrincipalOrderDraftRoutes.cs#L86)

**ERP specifications:** [PROC-08-PURCHASE-ORDERS-AND-AMENDMENTS.md](https://github.com/georgemunganga/erp/blob/c38202e57afff83abbcdcbb05f95a27d38a8be26/docs/procurement/PROC-08-PURCHASE-ORDERS-AND-AMENDMENTS.md)

**Zoho references:** [Guide 1](https://www.zoho.com/procurement/help/purchase-orders/convert-purchase-orders/), [Guide 2](https://www.zoho.com/procurement/help/purchase-orders/overview/), [Guide 3](https://www.zoho.com/procurement/help/purchase-orders/preferences/), [Guide 4](https://www.zoho.com/procurement/help/purchase-receives/overview/)

## BE-09 awarded RFQ/PR line to PO conversion

**Priority:** P1 · **Owner:** Procurement · **Milestone:** M3

**Scope:** selected operational launch workflows

**Documented expectation:** Broad ERP feature proposals exist; full operation-level parity is not accepted. See erpSpecPaths and per-manual documentation status.

**Current implementation:** Approved demand can feed bounded direct-order and sourcing source workflows; source quantities are controlled. Full cross-request bulk selection and award-to-order adoption remain unproven.

**Gap:** Approved PR source conversion is bounded; P38 award-to-PO adapter is absent and manual cross-request bulk selection is unproven.

**Acceptance:** Line/source/quantity conservation and original-key recovery after an approved P38 outcome; private owner and browser proof.

**Source evidence:** [backend/hrm-api/src/Mightyfin.Erp.Hrm.Api/ProcurementOrderDraftRoutes.cs:15](https://github.com/georgemunganga/erp/blob/c38202e57afff83abbcdcbb05f95a27d38a8be26/backend/hrm-api/src/Mightyfin.Erp.Hrm.Api/ProcurementOrderDraftRoutes.cs#L15); [backend/hrm-api/src/Mightyfin.Erp.Hrm.Api/ProcurementPrincipalOrderDraftRoutes.cs:43](https://github.com/georgemunganga/erp/blob/c38202e57afff83abbcdcbb05f95a27d38a8be26/backend/hrm-api/src/Mightyfin.Erp.Hrm.Api/ProcurementPrincipalOrderDraftRoutes.cs#L43); [backend/hrm-api/src/Mightyfin.Erp.Hrm.Api/ProcurementPrincipalOrderDraftRoutes.cs:86](https://github.com/georgemunganga/erp/blob/c38202e57afff83abbcdcbb05f95a27d38a8be26/backend/hrm-api/src/Mightyfin.Erp.Hrm.Api/ProcurementPrincipalOrderDraftRoutes.cs#L86)

**ERP specifications:** [PROC-08-PURCHASE-ORDERS-AND-AMENDMENTS.md](https://github.com/georgemunganga/erp/blob/c38202e57afff83abbcdcbb05f95a27d38a8be26/docs/procurement/PROC-08-PURCHASE-ORDERS-AND-AMENDMENTS.md)

**Zoho references:** [Guide 1](https://www.zoho.com/procurement/help/purchase-orders/create-purchase-orders/), [Guide 2](https://www.zoho.com/procurement/help/purchase-requests/convert-requested-items/)

## BE-10 receive transit/bill lifecycle and import

**Priority:** P1 · **Owner:** Procurement + Inventory + Finance · **Milestone:** M5

**Scope:** selected operational launch workflows

**Documented expectation:** Broad ERP feature proposals exist; full operation-level parity is not accepted. See erpSpecPaths and per-manual documentation status.

**Current implementation:** Principal goods/service receiving, inspection and partial acceptance have bounded private evidence; transit/billing/import are distinct operations.

**Gap:** Received/recorded/inspected/accepted paths are private proven, but transit toggle, Billed/Partially Billed and CSV receive import are not established.

**Acceptance:** Typed status transitions and external stock/AP ownership with exact partial quantity proof.

**Source evidence:** [backend/hrm-api/src/Mightyfin.Erp.Hrm.Api/ProcurementReceivingRoutes.cs:32](https://github.com/georgemunganga/erp/blob/c38202e57afff83abbcdcbb05f95a27d38a8be26/backend/hrm-api/src/Mightyfin.Erp.Hrm.Api/ProcurementReceivingRoutes.cs#L32); [backend/hrm-api/src/Mightyfin.Erp.Hrm.Api/ProcurementPrincipalReceivingRoutes.cs:23](https://github.com/georgemunganga/erp/blob/c38202e57afff83abbcdcbb05f95a27d38a8be26/backend/hrm-api/src/Mightyfin.Erp.Hrm.Api/ProcurementPrincipalReceivingRoutes.cs#L23)

**ERP specifications:** [PROC-09-RECEIVING-INSPECTION-AND-RETURNS.md](https://github.com/georgemunganga/erp/blob/c38202e57afff83abbcdcbb05f95a27d38a8be26/docs/procurement/PROC-09-RECEIVING-INSPECTION-AND-RETURNS.md), [PROC-09-PRINCIPAL-RECEIVING-SUCCESSOR.md](https://github.com/georgemunganga/erp/blob/c38202e57afff83abbcdcbb05f95a27d38a8be26/docs/procurement/PROC-09-PRINCIPAL-RECEIVING-SUCCESSOR.md)

**Zoho references:** [Guide 1](https://www.zoho.com/procurement/help/purchase-receives/create-purchase-receives/), [Guide 2](https://www.zoho.com/procurement/help/purchase-receives/manage-purchase-receives/), [Guide 3](https://www.zoho.com/procurement/help/purchase-receives/overview/)

## BE-11 RFQ recall/resubmit/policy variants

**Priority:** P2 · **Owner:** Core approval + Procurement · **Milestone:** M4

**Scope:** selected operational launch workflows

**Documented expectation:** Broad ERP feature proposals exist; full operation-level parity is not accepted. See erpSpecPaths and per-manual documentation status.

**Current implementation:** RFQ draft, submission and shared publication review exist; not every recall, published-deadline extension or selectable review variant has equivalent acceptance.

**Gap:** Core publication review exists but manual-style recall/reapprove and user-selected approver variants are not established.

**Acceptance:** Retained source/case cancellation and new-case tests with no invitation leakage.

**Source evidence:** [backend/hrm-api/src/Mightyfin.Erp.Hrm.Api/ProcurementSourcingExecutionRoutes.cs:30](https://github.com/georgemunganga/erp/blob/c38202e57afff83abbcdcbb05f95a27d38a8be26/backend/hrm-api/src/Mightyfin.Erp.Hrm.Api/ProcurementSourcingExecutionRoutes.cs#L30); [backend/hrm-api/src/Mightyfin.Erp.Hrm.Api/ProcurementPrincipalSourcingPublicationRoutes.cs:25](https://github.com/georgemunganga/erp/blob/c38202e57afff83abbcdcbb05f95a27d38a8be26/backend/hrm-api/src/Mightyfin.Erp.Hrm.Api/ProcurementPrincipalSourcingPublicationRoutes.cs#L25); [backend/hrm-api/src/Mightyfin.Erp.Hrm.Api/ProcurementRfqCommercialRoundRoutes.cs:10](https://github.com/georgemunganga/erp/blob/c38202e57afff83abbcdcbb05f95a27d38a8be26/backend/hrm-api/src/Mightyfin.Erp.Hrm.Api/ProcurementRfqCommercialRoundRoutes.cs#L10)

**ERP specifications:** [PROC-06-STRATEGIC-SOURCING-AND-AWARD.md](https://github.com/georgemunganga/erp/blob/c38202e57afff83abbcdcbb05f95a27d38a8be26/docs/procurement/PROC-06-STRATEGIC-SOURCING-AND-AWARD.md), [PROC-06-P32-PRINCIPAL-COMMERCIAL-COMPLETION.md](https://github.com/georgemunganga/erp/blob/c38202e57afff83abbcdcbb05f95a27d38a8be26/docs/procurement/PROC-06-P32-PRINCIPAL-COMMERCIAL-COMPLETION.md)

**Zoho references:** [Guide 1](https://www.zoho.com/procurement/help/request-for-quotes/approvals-for-request-for-quotes/)

## BE-12 RFQ supplier award

**Priority:** P0 · **Owner:** Procurement + Core approval · **Milestone:** M4

**Scope:** selected operational launch workflows

**Documented expectation:** DOCUMENTED_SOURCE_FOUNDATION_UNAVAILABLE

**Current implementation:** P38 award foundation has genuine schema and rollback evidence, while insertion and the full commercial award workflow deliberately remain unavailable.

**Gap:** P38 has genuine source/model/schema and private effect-veto proof but mapped award inserts deny; no live award draft/submit/approve/publish or PO output.

**Acceptance:** Retained P32/P36 source and residual-held-capacity loader, Core policy/role grants, populated veto, native/owner/UI proof.

**Source evidence:** [backend/hrm-api/src/Mightyfin.Erp.Hrm.Api/ProcurementHostIntegration.cs:143](https://github.com/georgemunganga/erp/blob/c38202e57afff83abbcdcbb05f95a27d38a8be26/backend/hrm-api/src/Mightyfin.Erp.Hrm.Api/ProcurementHostIntegration.cs#L143); [docs/procurement/release/P38-FOUNDATION-SCHEMA-ACCEPTANCE-20261025.json:1](https://github.com/georgemunganga/erp/blob/c38202e57afff83abbcdcbb05f95a27d38a8be26/docs/procurement/release/P38-FOUNDATION-SCHEMA-ACCEPTANCE-20261025.json#L1); [backend/procurement-api/src/Mightyfin.Erp.Procurement.Infrastructure/Data/ProcurementRfqAwardReviewMapping.cs:202](https://github.com/georgemunganga/erp/blob/c38202e57afff83abbcdcbb05f95a27d38a8be26/backend/procurement-api/src/Mightyfin.Erp.Procurement.Infrastructure/Data/ProcurementRfqAwardReviewMapping.cs#L202)

**ERP specifications:** [PROC-06-P38-PRINCIPAL-SUPPLIER-AWARD.md](https://github.com/georgemunganga/erp/blob/c38202e57afff83abbcdcbb05f95a27d38a8be26/docs/procurement/PROC-06-P38-PRINCIPAL-SUPPLIER-AWARD.md)

**Zoho references:** [Guide 1](https://www.zoho.com/procurement/help/request-for-quotes/award-request-for-quotes/), [Guide 2](https://www.zoho.com/procurement/help/request-for-quotes/manage-request-for-quotes/), [Guide 3](https://www.zoho.com/procurement/help/request-for-quotes/overview/)

## BE-13 supplier offer original evidence

**Priority:** P0 · **Owner:** Core Documents + Procurement · **Milestone:** M1

**Scope:** selected operational launch workflows

**Documented expectation:** Broad ERP feature proposals exist; full operation-level parity is not accepted. See erpSpecPaths and per-manual documentation status.

**Current implementation:** P32 commercial Draft/Prepare has bounded private acceptance; original-document offer submission is gated on genuine available scanned evidence.

**Gap:** Zero-document commercial prepare is accepted, but SubmitOffer requires real Available original document; scanner C04 remains external.

**Acceptance:** Actual Clean scan and sealed original document-use/submission/reload owner proof.

**Source evidence:** [backend/hrm-api/src/Mightyfin.Erp.Hrm.Api/ProcurementRfqCommercialRoundRoutes.cs:19](https://github.com/georgemunganga/erp/blob/c38202e57afff83abbcdcbb05f95a27d38a8be26/backend/hrm-api/src/Mightyfin.Erp.Hrm.Api/ProcurementRfqCommercialRoundRoutes.cs#L19); [backend/hrm-api/src/Mightyfin.Erp.Hrm.Api/ProcurementRfqCommercialRoundOffer.cs:137](https://github.com/georgemunganga/erp/blob/c38202e57afff83abbcdcbb05f95a27d38a8be26/backend/hrm-api/src/Mightyfin.Erp.Hrm.Api/ProcurementRfqCommercialRoundOffer.cs#L137)

**ERP specifications:** [PROC-06-P32-PRINCIPAL-COMMERCIAL-COMPLETION.md](https://github.com/georgemunganga/erp/blob/c38202e57afff83abbcdcbb05f95a27d38a8be26/docs/procurement/PROC-06-P32-PRINCIPAL-COMMERCIAL-COMPLETION.md)

**Zoho references:** [Guide 1](https://www.zoho.com/procurement/help/request-for-quotes/create-request-for-quotes/), [Guide 2](https://www.zoho.com/procurement/help/request-for-quotes/surrogate-bids/)

## BE-14 external supplier bidding/portal

**Priority:** P1 · **Owner:** Supplier identity + Procurement · **Milestone:** M8

**Scope:** supplier-facing launch; supplier banking remains Finance-owned

**Documented expectation:** Broad ERP feature proposals exist; full operation-level parity is not accepted. See erpSpecPaths and per-manual documentation status.

**Current implementation:** Internal buyer/surrogate and publication groundwork exists; it does not authenticate an external supplier or prove supplier portal bidding.

**Gap:** Publication and internal surrogate quote are bounded; no supplier portal account, invitation acceptance, authentic supplier bid/award notification lifecycle.

**Acceptance:** Distinct supplier auth/session/branch boundary, original bid/document authority, sealed access and browser owner proof.

**Source evidence:** [backend/hrm-api/src/Mightyfin.Erp.Hrm.Api/ProcurementSourcingExecutionRoutes.cs:30](https://github.com/georgemunganga/erp/blob/c38202e57afff83abbcdcbb05f95a27d38a8be26/backend/hrm-api/src/Mightyfin.Erp.Hrm.Api/ProcurementSourcingExecutionRoutes.cs#L30); [backend/hrm-api/src/Mightyfin.Erp.Hrm.Api/ProcurementPrincipalSourcingPublicationRoutes.cs:25](https://github.com/georgemunganga/erp/blob/c38202e57afff83abbcdcbb05f95a27d38a8be26/backend/hrm-api/src/Mightyfin.Erp.Hrm.Api/ProcurementPrincipalSourcingPublicationRoutes.cs#L25); [backend/hrm-api/src/Mightyfin.Erp.Hrm.Api/ProcurementRfqCommercialRoundRoutes.cs:10](https://github.com/georgemunganga/erp/blob/c38202e57afff83abbcdcbb05f95a27d38a8be26/backend/hrm-api/src/Mightyfin.Erp.Hrm.Api/ProcurementRfqCommercialRoundRoutes.cs#L10)

**ERP specifications:** [PROC-06-STRATEGIC-SOURCING-AND-AWARD.md](https://github.com/georgemunganga/erp/blob/c38202e57afff83abbcdcbb05f95a27d38a8be26/docs/procurement/PROC-06-STRATEGIC-SOURCING-AND-AWARD.md), [PROC-06-P32-PRINCIPAL-COMMERCIAL-COMPLETION.md](https://github.com/georgemunganga/erp/blob/c38202e57afff83abbcdcbb05f95a27d38a8be26/docs/procurement/PROC-06-P32-PRINCIPAL-COMMERCIAL-COMPLETION.md)

**Zoho references:** [Guide 1](https://www.zoho.com/procurement/help/request-for-quotes/create-request-for-quotes/), [Guide 2](https://www.zoho.com/procurement/help/request-for-quotes/manage-request-for-quotes/), [Guide 3](https://www.zoho.com/procurement/help/request-for-quotes/overview/), [Guide 4](https://www.zoho.com/procurement/help/request-for-quotes/preferences/), [Guide 5](https://www.zoho.com/procurement/help/request-for-quotes/preferences//), [Guide 6](https://www.zoho.com/procurement/help/request-for-quotes/publish-request-for-quotes/), [Guide 7](https://www.zoho.com/procurement/help/request-for-quotes/surrogate-bids/)

## BE-15 RFQ invite/close/award notifications

**Priority:** P2 · **Owner:** Outbox/communications · **Milestone:** M7

**Scope:** selected operational launch workflows

**Documented expectation:** Broad ERP feature proposals exist; full operation-level parity is not accepted. See erpSpecPaths and per-manual documentation status.

**Current implementation:** Stored invitation/manual-contact/outbox facts exist; actual supplier message delivery and reminder/outcome preferences need their own proof.

**Gap:** Internal manual-contact/outbox fact is not proof of vendor email delivery, reminders, expiry or award notification preferences.

**Acceptance:** Delivery consumer, templates, idempotent delivery receipts and negative privacy tests.

**Source evidence:** [backend/hrm-api/src/Mightyfin.Erp.Hrm.Api/ProcurementSourcingExecutionRoutes.cs:30](https://github.com/georgemunganga/erp/blob/c38202e57afff83abbcdcbb05f95a27d38a8be26/backend/hrm-api/src/Mightyfin.Erp.Hrm.Api/ProcurementSourcingExecutionRoutes.cs#L30); [backend/hrm-api/src/Mightyfin.Erp.Hrm.Api/ProcurementPrincipalSourcingPublicationRoutes.cs:25](https://github.com/georgemunganga/erp/blob/c38202e57afff83abbcdcbb05f95a27d38a8be26/backend/hrm-api/src/Mightyfin.Erp.Hrm.Api/ProcurementPrincipalSourcingPublicationRoutes.cs#L25); [backend/hrm-api/src/Mightyfin.Erp.Hrm.Api/ProcurementRfqCommercialRoundRoutes.cs:10](https://github.com/georgemunganga/erp/blob/c38202e57afff83abbcdcbb05f95a27d38a8be26/backend/hrm-api/src/Mightyfin.Erp.Hrm.Api/ProcurementRfqCommercialRoundRoutes.cs#L10)

**ERP specifications:** [PROC-06-STRATEGIC-SOURCING-AND-AWARD.md](https://github.com/georgemunganga/erp/blob/c38202e57afff83abbcdcbb05f95a27d38a8be26/docs/procurement/PROC-06-STRATEGIC-SOURCING-AND-AWARD.md), [PROC-06-P32-PRINCIPAL-COMMERCIAL-COMPLETION.md](https://github.com/georgemunganga/erp/blob/c38202e57afff83abbcdcbb05f95a27d38a8be26/docs/procurement/PROC-06-P32-PRINCIPAL-COMMERCIAL-COMPLETION.md)

**Zoho references:** [Guide 1](https://www.zoho.com/procurement/help/request-for-quotes/other-actions-for-request-for-quotes/), [Guide 2](https://www.zoho.com/procurement/help/request-for-quotes/publish-request-for-quotes/)

## BE-16 vendor self-onboarding/bank details

**Priority:** P1 · **Owner:** Supplier portal + Finance identity · **Milestone:** M8

**Scope:** supplier-facing launch; supplier banking remains Finance-owned

**Documented expectation:** Broad ERP feature proposals exist; full operation-level parity is not accepted. See erpSpecPaths and per-manual documentation status.

**Current implementation:** Internal vendor profile/contact/site/qualification CRUD and governed state actions exist; supplier self-service and Finance-verified remittance are separate.

**Gap:** Internal vendor CRUD exists; no invite/sign-up/self-entered tax/bank workflow. Finance retains banking ownership.

**Acceptance:** External supplier identity, verified invite and document/bank handoff with privacy and SoD proof.

**Source evidence:** [backend/hrm-api/src/Mightyfin.Erp.Hrm.Api/ProcurementVendorRoutes.cs:13](https://github.com/georgemunganga/erp/blob/c38202e57afff83abbcdcbb05f95a27d38a8be26/backend/hrm-api/src/Mightyfin.Erp.Hrm.Api/ProcurementVendorRoutes.cs#L13); [backend/hrm-api/src/Mightyfin.Erp.Hrm.Api/ProcurementVendorRoutes.cs:18](https://github.com/georgemunganga/erp/blob/c38202e57afff83abbcdcbb05f95a27d38a8be26/backend/hrm-api/src/Mightyfin.Erp.Hrm.Api/ProcurementVendorRoutes.cs#L18); [backend/hrm-api/src/Mightyfin.Erp.Hrm.Api/ProcurementVendorRoutes.cs:79](https://github.com/georgemunganga/erp/blob/c38202e57afff83abbcdcbb05f95a27d38a8be26/backend/hrm-api/src/Mightyfin.Erp.Hrm.Api/ProcurementVendorRoutes.cs#L79)

**ERP specifications:** [PROC-01-SUPPLIER-ONBOARDING-AND-MANAGEMENT.md](https://github.com/georgemunganga/erp/blob/c38202e57afff83abbcdcbb05f95a27d38a8be26/docs/procurement/PROC-01-SUPPLIER-ONBOARDING-AND-MANAGEMENT.md)

**Zoho references:** [Guide 1](https://www.zoho.com/procurement/help/vendors/add-vendors/), [Guide 2](https://www.zoho.com/procurement/help/vendors/overview/), [Guide 3](https://www.zoho.com/procurement/help/vendors/vendor-onboarding/)

## BE-17 configurable vendor approval/portal resubmit

**Priority:** P2 · **Owner:** Core policy + Procurement · **Milestone:** M2

**Scope:** selected operational launch workflows

**Documented expectation:** Broad ERP feature proposals exist; full operation-level parity is not accepted. See erpSpecPaths and per-manual documentation status.

**Current implementation:** Internal supplier submission and reviewer decisions exist; selectable approval variants and portal correction are not full equivalents.

**Gap:** Internal vendor submit/approve/reject is present, but manual approval presets, portal resubmission and notifications are not demonstrated.

**Acceptance:** Versioned policy/current assignments and retained resubmission evidence in a private cohort.

**Source evidence:** [backend/hrm-api/src/Mightyfin.Erp.Hrm.Api/ProcurementVendorRoutes.cs:13](https://github.com/georgemunganga/erp/blob/c38202e57afff83abbcdcbb05f95a27d38a8be26/backend/hrm-api/src/Mightyfin.Erp.Hrm.Api/ProcurementVendorRoutes.cs#L13); [backend/hrm-api/src/Mightyfin.Erp.Hrm.Api/ProcurementVendorRoutes.cs:18](https://github.com/georgemunganga/erp/blob/c38202e57afff83abbcdcbb05f95a27d38a8be26/backend/hrm-api/src/Mightyfin.Erp.Hrm.Api/ProcurementVendorRoutes.cs#L18); [backend/hrm-api/src/Mightyfin.Erp.Hrm.Api/ProcurementVendorRoutes.cs:79](https://github.com/georgemunganga/erp/blob/c38202e57afff83abbcdcbb05f95a27d38a8be26/backend/hrm-api/src/Mightyfin.Erp.Hrm.Api/ProcurementVendorRoutes.cs#L79)

**ERP specifications:** [PROC-01-SUPPLIER-ONBOARDING-AND-MANAGEMENT.md](https://github.com/georgemunganga/erp/blob/c38202e57afff83abbcdcbb05f95a27d38a8be26/docs/procurement/PROC-01-SUPPLIER-ONBOARDING-AND-MANAGEMENT.md)

**Zoho references:** [Guide 1](https://www.zoho.com/procurement/help/vendors/approvals-for-vendors/), [Guide 2](https://www.zoho.com/procurement/help/vendors/vendor-onboarding/)

## BE-18 vendor merge and transaction reparenting

**Priority:** P1 · **Owner:** Procurement master + all consumers · **Milestone:** M2

**Scope:** selected operational launch workflows

**Documented expectation:** Broad ERP feature proposals exist; full operation-level parity is not accepted. See erpSpecPaths and per-manual documentation status.

**Current implementation:** Supplier identities are referenced by retained commercial history. No governed merge/reparent operation was established.

**Gap:** No merge/reparent route; supplier IDs are retained source references across orders, quotes, receipts and contracts.

**Acceptance:** Referential closure, retained history and one transaction across all affected consumers; no silent ID rewrite.

**Source evidence:** [backend/hrm-api/src/Mightyfin.Erp.Hrm.Api/ProcurementVendorRoutes.cs:13](https://github.com/georgemunganga/erp/blob/c38202e57afff83abbcdcbb05f95a27d38a8be26/backend/hrm-api/src/Mightyfin.Erp.Hrm.Api/ProcurementVendorRoutes.cs#L13); [backend/hrm-api/src/Mightyfin.Erp.Hrm.Api/ProcurementVendorRoutes.cs:18](https://github.com/georgemunganga/erp/blob/c38202e57afff83abbcdcbb05f95a27d38a8be26/backend/hrm-api/src/Mightyfin.Erp.Hrm.Api/ProcurementVendorRoutes.cs#L18); [backend/hrm-api/src/Mightyfin.Erp.Hrm.Api/ProcurementVendorRoutes.cs:79](https://github.com/georgemunganga/erp/blob/c38202e57afff83abbcdcbb05f95a27d38a8be26/backend/hrm-api/src/Mightyfin.Erp.Hrm.Api/ProcurementVendorRoutes.cs#L79)

**ERP specifications:** [PROC-01-SUPPLIER-ONBOARDING-AND-MANAGEMENT.md](https://github.com/georgemunganga/erp/blob/c38202e57afff83abbcdcbb05f95a27d38a8be26/docs/procurement/PROC-01-SUPPLIER-ONBOARDING-AND-MANAGEMENT.md)

**Zoho references:** [Guide 1](https://www.zoho.com/procurement/help/vendors/other-actions-for-vendors/), [Guide 2](https://www.zoho.com/procurement/help/vendors/overview/)

## BE-19 contract activation/framework utilisation

**Priority:** P1 · **Owner:** Procurement + Core + Finance · **Milestone:** M7

**Scope:** selected operational launch workflows

**Documented expectation:** DOCUMENTED_AS_MISSING

**Current implementation:** Contract draft/rate editing and internal review have real private browser/owner evidence; internal approval is not signature, activation or framework consumption.

**Gap:** Draft/rates and opt-in internal Core review have private proof, but signature/Active, amendments, framework releases and utilisation are absent.

**Acceptance:** Explicit owner policy, signed/active transition, finite grants, exact ceiling/PO consumption and normal-cookie owner proof.

**Source evidence:** [backend/hrm-api/src/Mightyfin.Erp.Hrm.Api/ProcurementContractDraftRoutes.cs:16](https://github.com/georgemunganga/erp/blob/c38202e57afff83abbcdcbb05f95a27d38a8be26/backend/hrm-api/src/Mightyfin.Erp.Hrm.Api/ProcurementContractDraftRoutes.cs#L16); [backend/hrm-api/src/Mightyfin.Erp.Hrm.Api/ProcurementContractReviewRoutes.cs:27](https://github.com/georgemunganga/erp/blob/c38202e57afff83abbcdcbb05f95a27d38a8be26/backend/hrm-api/src/Mightyfin.Erp.Hrm.Api/ProcurementContractReviewRoutes.cs#L27); [backend/hrm-api/src/Mightyfin.Erp.Hrm.Api/ProcurementHostIntegration.cs:186](https://github.com/georgemunganga/erp/blob/c38202e57afff83abbcdcbb05f95a27d38a8be26/backend/hrm-api/src/Mightyfin.Erp.Hrm.Api/ProcurementHostIntegration.cs#L186)

**ERP specifications:** [PROC-07-CONTRACTS-FRAMEWORKS-AND-RECURRING-COMMITMENTS.md](https://github.com/georgemunganga/erp/blob/c38202e57afff83abbcdcbb05f95a27d38a8be26/docs/procurement/PROC-07-CONTRACTS-FRAMEWORKS-AND-RECURRING-COMMITMENTS.md)

**Zoho references:** 

## PAY-01 Complete bill capture and editing operations

**Priority:** P1 · **Owner:** Procurement; Core numbering/import platform · **Milestone:** M6

**Scope:** selected operational launch workflows

**Documented expectation:** Partial PROC-10 specification; narrower current intake contract.

**Current implementation:** Scoped single-bill list/detail/create/revise/cancel; supplier, optional PO, dates, currency and exact lines.

**Gap:** Automatic numbering contract, clone/import/bulk capture, reusable payment terms/AP account classification and full Zoho bill lifecycle are not all exposed.

**Acceptance:** Create/revise/reload a bill in the existing editor; verify duplicate/stale/denied paths, approved numbering and selected clone/import/terms operations without requiring Finance posting.

**Source evidence:** [backend/hrm-api/src/Mightyfin.Erp.Hrm.Api/ProcurementInvoiceIntakeRoutes.cs:29](https://github.com/georgemunganga/erp/blob/c38202e57afff83abbcdcbb05f95a27d38a8be26/backend/hrm-api/src/Mightyfin.Erp.Hrm.Api/ProcurementInvoiceIntakeRoutes.cs#L29); [backend/hrm-api/src/Mightyfin.Erp.Hrm.Api/ProcurementInvoiceIntakeRoutes.cs:129](https://github.com/georgemunganga/erp/blob/c38202e57afff83abbcdcbb05f95a27d38a8be26/backend/hrm-api/src/Mightyfin.Erp.Hrm.Api/ProcurementInvoiceIntakeRoutes.cs#L129); [reference-ui/src/features/LiveInvoices.tsx:530](https://github.com/georgemunganga/module-connect/blob/c370275e9242f447cade47bec31c2fb42aa75637/reference-ui/src/features/LiveInvoices.tsx#L530)

**ERP specifications:** 

**Zoho references:** [Guide 1](https://www.zoho.com/procurement/help/bills/create-bills/), [Guide 2](https://www.zoho.com/procurement/help/bills/manage-bills/), [Guide 3](https://www.zoho.com/procurement/help/bills/overview/)

## PAY-02 Positive retained invoice review and PO/receipt matching

**Priority:** P0 · **Owner:** Procurement invoice workflow; Core Documents/scanner owner · **Milestone:** M1

**Scope:** selected operational launch workflows

**Documented expectation:** Detailed P31 source/exception specs and C04 dependency note.

**Current implementation:** Typed mapping, independent shared Core unit review, compare, discrepancy and correction/remap source; physical schema captured.

**Gap:** Genuine scanned originals plus finite-role positive end-to-end approval/comparison/claims cohort and current UI/API acceptance. No OCR inference.

**Acceptance:** Use genuine currently available originals; create and compare partial PO/receipt claims, resolve exceptions and verify independent review, denied access, stale source and duplicate/retry behavior.

**Source evidence:** [backend/hrm-api/src/Mightyfin.Erp.Hrm.Api/ProcurementPrincipalInvoiceMappingRoutes.cs:33](https://github.com/georgemunganga/erp/blob/c38202e57afff83abbcdcbb05f95a27d38a8be26/backend/hrm-api/src/Mightyfin.Erp.Hrm.Api/ProcurementPrincipalInvoiceMappingRoutes.cs#L33); [backend/hrm-api/src/Mightyfin.Erp.Hrm.Api/ProcurementPrincipalInvoiceStatusRoutes.cs:88](https://github.com/georgemunganga/erp/blob/c38202e57afff83abbcdcbb05f95a27d38a8be26/backend/hrm-api/src/Mightyfin.Erp.Hrm.Api/ProcurementPrincipalInvoiceStatusRoutes.cs#L88); [backend/hrm-api/src/Mightyfin.Erp.Hrm.Api/ProcurementHostIntegration.cs:363](https://github.com/georgemunganga/erp/blob/c38202e57afff83abbcdcbb05f95a27d38a8be26/backend/hrm-api/src/Mightyfin.Erp.Hrm.Api/ProcurementHostIntegration.cs#L363); [backend/hrm-api/src/Mightyfin.Erp.Hrm.Api/ProcurementInvoiceDocumentRoutes.cs:81](https://github.com/georgemunganga/erp/blob/c38202e57afff83abbcdcbb05f95a27d38a8be26/backend/hrm-api/src/Mightyfin.Erp.Hrm.Api/ProcurementInvoiceDocumentRoutes.cs#L81); [reference-ui/src/features/InvoiceDocuments.tsx:37](https://github.com/georgemunganga/module-connect/blob/c370275e9242f447cade47bec31c2fb42aa75637/reference-ui/src/features/InvoiceDocuments.tsx#L37); [backend/core-api/src/Mightyfin.Erp.Core.Documents.Domain/DocumentUsePin.cs:65](https://github.com/georgemunganga/erp/blob/c38202e57afff83abbcdcbb05f95a27d38a8be26/backend/core-api/src/Mightyfin.Erp.Core.Documents.Domain/DocumentUsePin.cs#L65); [docs/procurement/release/P31-P32-C04-DOCUMENT-DEPENDENCY-20261025.md:12](https://github.com/georgemunganga/erp/blob/c38202e57afff83abbcdcbb05f95a27d38a8be26/docs/procurement/release/P31-P32-C04-DOCUMENT-DEPENDENCY-20261025.md#L12)

**ERP specifications:** 

**Zoho references:** [Guide 1](https://www.zoho.com/procurement/help/bills/approvals-for-bills/), [Guide 2](https://www.zoho.com/procurement/help/bills/bill-reconciliation/), [Guide 3](https://www.zoho.com/procurement/help/bills/overview/), [Guide 4](https://www.zoho.com/procurement/help/bills/preferences/), [Guide 5](https://www.zoho.com/procurement/help/bills/purchase-order-matching/)

## PAY-03 Authoritative payable balances, paid/overdue/partial states and posting

**Priority:** P1 · **Owner:** Finance owns ledger; Procurement owns clear payable projections · **Milestone:** M6

**Scope:** selected operational launch workflows

**Documented expectation:** Explicit Finance handoff boundary.

**Current implementation:** Intake and manual evidence are separate; metadata invoiceQualifiedClaims, invoiceValidation and financeHandoff are false.

**Gap:** Owned AP posting/settlement reconciliation and trusted unpaid/paid balance projection; draft statuses cannot substitute.

**Acceptance:** Verify an owned Finance adapter or explicit unavailable state, reconcile callbacks idempotently and show exact partial/paid/overdue projections without assigning ledger truth to manual evidence.

**Source evidence:** [backend/hrm-api/src/Mightyfin.Erp.Hrm.Api/ProcurementHostIntegration.cs:350](https://github.com/georgemunganga/erp/blob/c38202e57afff83abbcdcbb05f95a27d38a8be26/backend/hrm-api/src/Mightyfin.Erp.Hrm.Api/ProcurementHostIntegration.cs#L350); [backend/hrm-api/src/Mightyfin.Erp.Hrm.Api/ProcurementHostIntegration.cs:364](https://github.com/georgemunganga/erp/blob/c38202e57afff83abbcdcbb05f95a27d38a8be26/backend/hrm-api/src/Mightyfin.Erp.Hrm.Api/ProcurementHostIntegration.cs#L364); [backend/hrm-api/src/Mightyfin.Erp.Hrm.Api/ProcurementManualPaymentRoutes.cs:24](https://github.com/georgemunganga/erp/blob/c38202e57afff83abbcdcbb05f95a27d38a8be26/backend/hrm-api/src/Mightyfin.Erp.Hrm.Api/ProcurementManualPaymentRoutes.cs#L24); [backend/hrm-api/src/Mightyfin.Erp.Hrm.Api/ProcurementManualPaymentCommandRoutes.cs:23](https://github.com/georgemunganga/erp/blob/c38202e57afff83abbcdcbb05f95a27d38a8be26/backend/hrm-api/src/Mightyfin.Erp.Hrm.Api/ProcurementManualPaymentCommandRoutes.cs#L23); [backend/hrm-api/src/Mightyfin.Erp.Hrm.Api/ProcurementManualPaymentCommandRoutes.cs:46](https://github.com/georgemunganga/erp/blob/c38202e57afff83abbcdcbb05f95a27d38a8be26/backend/hrm-api/src/Mightyfin.Erp.Hrm.Api/ProcurementManualPaymentCommandRoutes.cs#L46); [reference-ui/src/features/LivePayments.tsx:312](https://github.com/georgemunganga/module-connect/blob/c370275e9242f447cade47bec31c2fb42aa75637/reference-ui/src/features/LivePayments.tsx#L312)

**ERP specifications:** 

**Zoho references:** [Guide 1](https://www.zoho.com/procurement/help/analytics/payables/), [Guide 2](https://www.zoho.com/procurement/help/batch-payments/record-payments-for-batch-payments/), [Guide 3](https://www.zoho.com/procurement/help/bills/bill-reconciliation/), [Guide 4](https://www.zoho.com/procurement/help/bills/overview/), [Guide 5](https://www.zoho.com/procurement/help/payments-made/overview/)

## PAY-04 Vendor-level payments, multi-bill allocation, advances and excess/unapplied amounts

**Priority:** P1 · **Owner:** Procurement manual payables · **Milestone:** M6

**Scope:** selected operational launch workflows

**Documented expectation:** Explicitly deferred in PROC-12 section 28/addenda.

**Current implementation:** Real one-invoice payment request, independent approval, external recording, confirmation, list/detail and original-key recovery.

**Gap:** Vendor payment before invoice, split across bills, unapplied/excess balance, later allocation/unallocation and advance history. These are missing manual workflows, not merely a missing bank connector.

**Acceptance:** Persist a supplier advance or a payment split across multiple invoices, conserve used/excess totals, apply later without double consumption and deny cross-supplier/currency/branch allocation.

**Source evidence:** [backend/hrm-api/src/Mightyfin.Erp.Hrm.Api/ProcurementManualPaymentRoutes.cs:24](https://github.com/georgemunganga/erp/blob/c38202e57afff83abbcdcbb05f95a27d38a8be26/backend/hrm-api/src/Mightyfin.Erp.Hrm.Api/ProcurementManualPaymentRoutes.cs#L24); [backend/hrm-api/src/Mightyfin.Erp.Hrm.Api/ProcurementManualPaymentCommandRoutes.cs:23](https://github.com/georgemunganga/erp/blob/c38202e57afff83abbcdcbb05f95a27d38a8be26/backend/hrm-api/src/Mightyfin.Erp.Hrm.Api/ProcurementManualPaymentCommandRoutes.cs#L23); [backend/hrm-api/src/Mightyfin.Erp.Hrm.Api/ProcurementManualPaymentCommandRoutes.cs:46](https://github.com/georgemunganga/erp/blob/c38202e57afff83abbcdcbb05f95a27d38a8be26/backend/hrm-api/src/Mightyfin.Erp.Hrm.Api/ProcurementManualPaymentCommandRoutes.cs#L46); [reference-ui/src/features/LivePayments.tsx:312](https://github.com/georgemunganga/module-connect/blob/c370275e9242f447cade47bec31c2fb42aa75637/reference-ui/src/features/LivePayments.tsx#L312)

**ERP specifications:** 

**Zoho references:** [Guide 1](https://www.zoho.com/procurement/help/bills/record-payments-for-bills/), [Guide 2](https://www.zoho.com/procurement/help/payments-made/overview/), [Guide 3](https://www.zoho.com/procurement/help/payments-made/record-payments-made/), [Guide 4](https://www.zoho.com/procurement/help/recurring-bills/record-payments-for-child-bills/)

## PAY-05 Manual refund, reversal and historical correction

**Priority:** P1 · **Owner:** Procurement manual payables; Finance reconciliation adapter · **Milestone:** M6

**Scope:** selected operational launch workflows

**Documented expectation:** Manual refunds/advances deferred; no bank execution claimed.

**Current implementation:** Unexecuted cancellation, return, rejection and recorded-payment dispute exist.

**Gap:** Refund cash-in records, confirmed-payment reversal/void-to-draft with retained compensating history, edit/refund allocation and payable reconciliation.

**Acceptance:** Record an authorized refund or compensating correction linked to the original; retain prior evidence, recompute allocation capacity and prove replay/concurrent correction safety.

**Source evidence:** [backend/hrm-api/src/Mightyfin.Erp.Hrm.Api/ProcurementManualPaymentRoutes.cs:24](https://github.com/georgemunganga/erp/blob/c38202e57afff83abbcdcbb05f95a27d38a8be26/backend/hrm-api/src/Mightyfin.Erp.Hrm.Api/ProcurementManualPaymentRoutes.cs#L24); [backend/hrm-api/src/Mightyfin.Erp.Hrm.Api/ProcurementManualPaymentCommandRoutes.cs:23](https://github.com/georgemunganga/erp/blob/c38202e57afff83abbcdcbb05f95a27d38a8be26/backend/hrm-api/src/Mightyfin.Erp.Hrm.Api/ProcurementManualPaymentCommandRoutes.cs#L23); [backend/hrm-api/src/Mightyfin.Erp.Hrm.Api/ProcurementManualPaymentCommandRoutes.cs:46](https://github.com/georgemunganga/erp/blob/c38202e57afff83abbcdcbb05f95a27d38a8be26/backend/hrm-api/src/Mightyfin.Erp.Hrm.Api/ProcurementManualPaymentCommandRoutes.cs#L46); [reference-ui/src/features/LivePayments.tsx:312](https://github.com/georgemunganga/module-connect/blob/c370275e9242f447cade47bec31c2fb42aa75637/reference-ui/src/features/LivePayments.tsx#L312); [backend/hrm-api/src/Mightyfin.Erp.Hrm.Api/ProcurementCreditIntakeRoutes.cs:13](https://github.com/georgemunganga/erp/blob/c38202e57afff83abbcdcbb05f95a27d38a8be26/backend/hrm-api/src/Mightyfin.Erp.Hrm.Api/ProcurementCreditIntakeRoutes.cs#L13); [backend/hrm-api/src/Mightyfin.Erp.Hrm.Api/ProcurementCreditIntakeRoutes.cs:37](https://github.com/georgemunganga/erp/blob/c38202e57afff83abbcdcbb05f95a27d38a8be26/backend/hrm-api/src/Mightyfin.Erp.Hrm.Api/ProcurementCreditIntakeRoutes.cs#L37); [backend/hrm-api/src/Mightyfin.Erp.Hrm.Api/ProcurementCreditIntakeRoutes.cs:211](https://github.com/georgemunganga/erp/blob/c38202e57afff83abbcdcbb05f95a27d38a8be26/backend/hrm-api/src/Mightyfin.Erp.Hrm.Api/ProcurementCreditIntakeRoutes.cs#L211); [src/platform/procurement-vendor-credits-api.ts:3](https://github.com/georgemunganga/module-connect/blob/c370275e9242f447cade47bec31c2fb42aa75637/src/platform/procurement-vendor-credits-api.ts#L3)

**ERP specifications:** 

**Zoho references:** [Guide 1](https://www.zoho.com/procurement/help/bills/record-payments-for-bills/), [Guide 2](https://www.zoho.com/procurement/help/payments-made/manage-payments-made/), [Guide 3](https://www.zoho.com/procurement/help/vendor-credits/refunds-for-vendor-credits/)

## PAY-06 Standalone and line-item vendor credit approval lifecycle

**Priority:** P1 · **Owner:** Procurement credit workflow · **Milestone:** M6

**Scope:** selected operational launch workflows

**Documented expectation:** PROC-10 credit Draft CRUD intentionally bounded.

**Current implementation:** Invoice-linked positive amount/reason credit Draft create/revise/cancel; same bill/vendor scope and conservative aggregate cap.

**Gap:** Standalone vendor credit, credit item/tax rows, clone/import, shared Core credit review and Open/Closed/Void lifecycle.

**Acceptance:** Create/edit an eligible standalone or itemized credit, validate amount/tax/source, run independent review and retain state/version history with correct cancellation boundaries.

**Source evidence:** [backend/hrm-api/src/Mightyfin.Erp.Hrm.Api/ProcurementCreditIntakeRoutes.cs:13](https://github.com/georgemunganga/erp/blob/c38202e57afff83abbcdcbb05f95a27d38a8be26/backend/hrm-api/src/Mightyfin.Erp.Hrm.Api/ProcurementCreditIntakeRoutes.cs#L13); [backend/hrm-api/src/Mightyfin.Erp.Hrm.Api/ProcurementCreditIntakeRoutes.cs:37](https://github.com/georgemunganga/erp/blob/c38202e57afff83abbcdcbb05f95a27d38a8be26/backend/hrm-api/src/Mightyfin.Erp.Hrm.Api/ProcurementCreditIntakeRoutes.cs#L37); [backend/hrm-api/src/Mightyfin.Erp.Hrm.Api/ProcurementCreditIntakeRoutes.cs:211](https://github.com/georgemunganga/erp/blob/c38202e57afff83abbcdcbb05f95a27d38a8be26/backend/hrm-api/src/Mightyfin.Erp.Hrm.Api/ProcurementCreditIntakeRoutes.cs#L211); [src/platform/procurement-vendor-credits-api.ts:3](https://github.com/georgemunganga/module-connect/blob/c370275e9242f447cade47bec31c2fb42aa75637/src/platform/procurement-vendor-credits-api.ts#L3)

**ERP specifications:** 

**Zoho references:** [Guide 1](https://www.zoho.com/procurement/help/vendor-credits/approvals-for-vendor-credits/), [Guide 2](https://www.zoho.com/procurement/help/vendor-credits/create-vendor-credits/), [Guide 3](https://www.zoho.com/procurement/help/vendor-credits/manage-vendor-credits/), [Guide 4](https://www.zoho.com/procurement/help/vendor-credits/overview/), [Guide 5](https://www.zoho.com/procurement/help/vendor-credits/vendor-credit-preferences/)

## PAY-07 Apply credits and record credit refunds

**Priority:** P1 · **Owner:** Procurement manual credit allocation; Finance reconciliation · **Milestone:** M6

**Scope:** selected operational launch workflows

**Documented expectation:** Future application/Finance adjustments described, current Draft boundary explicit.

**Current implementation:** Captured noncancelled credit conservatively reserves invoice payment capacity; it does not assert Finance application.

**Gap:** Partial credit application to one/multiple bills, remaining credit, unapply/reallocation and full/partial cash refund history/import/edit/delete semantics.

**Acceptance:** Partially apply/unapply a credit and record a partial refund with immutable receipts; remaining value reconciles under concurrent invoice/payment/credit claims.

**Source evidence:** [backend/hrm-api/src/Mightyfin.Erp.Hrm.Api/ProcurementCreditIntakeRoutes.cs:13](https://github.com/georgemunganga/erp/blob/c38202e57afff83abbcdcbb05f95a27d38a8be26/backend/hrm-api/src/Mightyfin.Erp.Hrm.Api/ProcurementCreditIntakeRoutes.cs#L13); [backend/hrm-api/src/Mightyfin.Erp.Hrm.Api/ProcurementCreditIntakeRoutes.cs:37](https://github.com/georgemunganga/erp/blob/c38202e57afff83abbcdcbb05f95a27d38a8be26/backend/hrm-api/src/Mightyfin.Erp.Hrm.Api/ProcurementCreditIntakeRoutes.cs#L37); [backend/hrm-api/src/Mightyfin.Erp.Hrm.Api/ProcurementCreditIntakeRoutes.cs:211](https://github.com/georgemunganga/erp/blob/c38202e57afff83abbcdcbb05f95a27d38a8be26/backend/hrm-api/src/Mightyfin.Erp.Hrm.Api/ProcurementCreditIntakeRoutes.cs#L211); [src/platform/procurement-vendor-credits-api.ts:3](https://github.com/georgemunganga/module-connect/blob/c370275e9242f447cade47bec31c2fb42aa75637/src/platform/procurement-vendor-credits-api.ts#L3); [backend/hrm-api/src/Mightyfin.Erp.Hrm.Api/ProcurementManualPaymentRoutes.cs:24](https://github.com/georgemunganga/erp/blob/c38202e57afff83abbcdcbb05f95a27d38a8be26/backend/hrm-api/src/Mightyfin.Erp.Hrm.Api/ProcurementManualPaymentRoutes.cs#L24); [backend/hrm-api/src/Mightyfin.Erp.Hrm.Api/ProcurementManualPaymentCommandRoutes.cs:23](https://github.com/georgemunganga/erp/blob/c38202e57afff83abbcdcbb05f95a27d38a8be26/backend/hrm-api/src/Mightyfin.Erp.Hrm.Api/ProcurementManualPaymentCommandRoutes.cs#L23); [backend/hrm-api/src/Mightyfin.Erp.Hrm.Api/ProcurementManualPaymentCommandRoutes.cs:46](https://github.com/georgemunganga/erp/blob/c38202e57afff83abbcdcbb05f95a27d38a8be26/backend/hrm-api/src/Mightyfin.Erp.Hrm.Api/ProcurementManualPaymentCommandRoutes.cs#L46); [reference-ui/src/features/LivePayments.tsx:312](https://github.com/georgemunganga/module-connect/blob/c370275e9242f447cade47bec31c2fb42aa75637/reference-ui/src/features/LivePayments.tsx#L312)

**ERP specifications:** 

**Zoho references:** [Guide 1](https://www.zoho.com/procurement/help/bills/record-payments-for-bills/), [Guide 2](https://www.zoho.com/procurement/help/vendor-credits/create-vendor-credits/), [Guide 3](https://www.zoho.com/procurement/help/vendor-credits/manage-vendor-credits/), [Guide 4](https://www.zoho.com/procurement/help/vendor-credits/overview/), [Guide 5](https://www.zoho.com/procurement/help/vendor-credits/refunds-for-vendor-credits/), [Guide 6](https://www.zoho.com/procurement/help/vendor-credits/share-vendor-credits/)

## PAY-08 Authoritative batch payment management

**Priority:** P1 · **Owner:** Procurement batch workflow; Finance/bank adapter only for execution · **Milestone:** M6

**Scope:** selected operational launch workflows

**Documented expectation:** PROC-12 explicitly defers batch contracts.

**Current implementation:** Imported Batch Payments UI uses local storage; live admission has no batch capability and payment adapter declares it unavailable.

**Gap:** Server-owned batch draft lines, independent approval, cancel/delete boundaries, bank-file export, per-payment processed/failed outcomes and retained original-key recovery.

**Acceptance:** Persist a batch and its lines, approve independently, record per-line manual outcomes and recover uncertain acknowledgements; do not imply bank execution or bypass individual capacity guards.

**Source evidence:** [reference-ui/src/features/BatchPayments.tsx:26](https://github.com/georgemunganga/module-connect/blob/c370275e9242f447cade47bec31c2fb42aa75637/reference-ui/src/features/BatchPayments.tsx#L26); [reference-ui/src/features/BatchPayments.tsx:60](https://github.com/georgemunganga/module-connect/blob/c370275e9242f447cade47bec31c2fb42aa75637/reference-ui/src/features/BatchPayments.tsx#L60); [reference-ui/src/lib/ProcurementLiveGate.tsx:91](https://github.com/georgemunganga/module-connect/blob/c370275e9242f447cade47bec31c2fb42aa75637/reference-ui/src/lib/ProcurementLiveGate.tsx#L91); [src/platform/procurement-manual-payments-api.ts:442](https://github.com/georgemunganga/module-connect/blob/c370275e9242f447cade47bec31c2fb42aa75637/src/platform/procurement-manual-payments-api.ts#L442)

**ERP specifications:** 

**Zoho references:** [Guide 1](https://www.zoho.com/procurement/help/batch-payments/approvals-for-batch-payments/), [Guide 2](https://www.zoho.com/procurement/help/batch-payments/create-batch-payments/), [Guide 3](https://www.zoho.com/procurement/help/batch-payments/manage-batch-payments/), [Guide 4](https://www.zoho.com/procurement/help/batch-payments/other-actions-for-batch-payments/), [Guide 5](https://www.zoho.com/procurement/help/batch-payments/overview/), [Guide 6](https://www.zoho.com/procurement/help/batch-payments/preferences/), [Guide 7](https://www.zoho.com/procurement/help/batch-payments/record-payments-for-batch-payments/)

## PAY-09 Recurring bill profiles and child generation

**Priority:** P1 · **Owner:** Procurement recurrence execution; Core scheduling infrastructure · **Milestone:** M7

**Scope:** selected operational launch workflows

**Documented expectation:** Recurring PURCHASE planning scope explicitly excludes scheduler/bills.

**Current implementation:** Vendor-backed planning records support create/edit/close and are actually live P24.

**Gap:** Executable recurring-invoice templates, scheduled child bill creation, idempotent generation, stop/resume, child navigation and child-payment linkage. Planning Frequency/FirstCycleOn/LastCycleOn/NextCycleOn already exist; their presence is not execution.

**Acceptance:** Generate each due child exactly once using Core business time, preserve template/source versions, test catch-up and stop/resume and show generated records and payment links.

**Source evidence:** [backend/hrm-api/src/Mightyfin.Erp.Hrm.Api/ProcurementRecurringPurchaseRoutes.cs:16](https://github.com/georgemunganga/erp/blob/c38202e57afff83abbcdcbb05f95a27d38a8be26/backend/hrm-api/src/Mightyfin.Erp.Hrm.Api/ProcurementRecurringPurchaseRoutes.cs#L16); [backend/hrm-api/src/Mightyfin.Erp.Hrm.Api/ProcurementHostIntegration.cs:309](https://github.com/georgemunganga/erp/blob/c38202e57afff83abbcdcbb05f95a27d38a8be26/backend/hrm-api/src/Mightyfin.Erp.Hrm.Api/ProcurementHostIntegration.cs#L309); [docs/procurement/PROC-07A-RECURRING-PURCHASE-PLANS.md:1](https://github.com/georgemunganga/erp/blob/c38202e57afff83abbcdcbb05f95a27d38a8be26/docs/procurement/PROC-07A-RECURRING-PURCHASE-PLANS.md#L1); [backend/hrm-api/src/Mightyfin.Erp.Hrm.Api/ProcurementInvoiceIntakeRoutes.cs:29](https://github.com/georgemunganga/erp/blob/c38202e57afff83abbcdcbb05f95a27d38a8be26/backend/hrm-api/src/Mightyfin.Erp.Hrm.Api/ProcurementInvoiceIntakeRoutes.cs#L29); [backend/hrm-api/src/Mightyfin.Erp.Hrm.Api/ProcurementInvoiceIntakeRoutes.cs:129](https://github.com/georgemunganga/erp/blob/c38202e57afff83abbcdcbb05f95a27d38a8be26/backend/hrm-api/src/Mightyfin.Erp.Hrm.Api/ProcurementInvoiceIntakeRoutes.cs#L129); [reference-ui/src/features/LiveInvoices.tsx:530](https://github.com/georgemunganga/module-connect/blob/c370275e9242f447cade47bec31c2fb42aa75637/reference-ui/src/features/LiveInvoices.tsx#L530)

**ERP specifications:** 

**Zoho references:** [Guide 1](https://www.zoho.com/procurement/help/bills/create-bills/), [Guide 2](https://www.zoho.com/procurement/help/recurring-bills/create-recurring-bills/), [Guide 3](https://www.zoho.com/procurement/help/recurring-bills/manage-recurring-bills/), [Guide 4](https://www.zoho.com/procurement/help/recurring-bills/other-actions-for-recurring-bills/), [Guide 5](https://www.zoho.com/procurement/help/recurring-bills/overview/), [Guide 6](https://www.zoho.com/procurement/help/recurring-bills/preferences/), [Guide 7](https://www.zoho.com/procurement/help/recurring-bills/record-payments-for-child-bills/)

## PAY-10 Budget creation, controls and maintenance

**Priority:** P1 · **Owner:** Finance budget authority; Procurement budget consumption UI · **Milestone:** M3

**Scope:** selected operational launch workflows

**Documented expectation:** PROC-FIN-01 and PROC-03 describe evidence and approval boundary.

**Current implementation:** Scoped Finance check queue/status/history and evidence-backed outcomes; not a budget master.

**Gap:** Fiscal period/category/tag/project budget grids, edit/delete/clone, prior-actual prefill/autofill, threshold notifications and authoritative consumption/approval blocks.

**Acceptance:** Consume the owning Finance fiscal/period budget contract; test configured advisory/hard-block/exception behavior and reservations without creating a second balance ledger.

**Source evidence:** [backend/hrm-api/src/Mightyfin.Erp.Hrm.Api/ProcurementBudgetRoutes.cs:34](https://github.com/georgemunganga/erp/blob/c38202e57afff83abbcdcbb05f95a27d38a8be26/backend/hrm-api/src/Mightyfin.Erp.Hrm.Api/ProcurementBudgetRoutes.cs#L34); [backend/hrm-api/src/Mightyfin.Erp.Hrm.Api/ProcurementBudgetRoutes.cs:188](https://github.com/georgemunganga/erp/blob/c38202e57afff83abbcdcbb05f95a27d38a8be26/backend/hrm-api/src/Mightyfin.Erp.Hrm.Api/ProcurementBudgetRoutes.cs#L188); [reference-ui/src/features/LiveBudgets.tsx:14](https://github.com/georgemunganga/module-connect/blob/c370275e9242f447cade47bec31c2fb42aa75637/reference-ui/src/features/LiveBudgets.tsx#L14); [docs/procurement/PROC-FIN-01-BUDGET-RESERVATION-CONTRACT.md:1](https://github.com/georgemunganga/erp/blob/c38202e57afff83abbcdcbb05f95a27d38a8be26/docs/procurement/PROC-FIN-01-BUDGET-RESERVATION-CONTRACT.md#L1)

**ERP specifications:** 

**Zoho references:** [Guide 1](https://www.zoho.com/procurement/help/budgets/create-budgets/), [Guide 2](https://www.zoho.com/procurement/help/budgets/manage-budgets/), [Guide 3](https://www.zoho.com/procurement/help/budgets/overview/)

## PAY-11 Budget versus actuals reports

**Priority:** P1 · **Owner:** Finance reporting owner; Procurement report presentation · **Milestone:** M7

**Scope:** selected operational launch workflows

**Documented expectation:** Planned analytics and Finance contract, not delivered report.

**Current implementation:** Budget evidence history only.

**Gap:** Period/category aggregation, actual consumption/variance, drilldown, filter semantics, PDF/XLS and print.

**Acceptance:** Reconcile period/category actuals and commitments to owned sources; test as-of dates, FX basis, scope, drilldown and complete export against the same filters.

**Source evidence:** [backend/hrm-api/src/Mightyfin.Erp.Hrm.Api/ProcurementBudgetRoutes.cs:34](https://github.com/georgemunganga/erp/blob/c38202e57afff83abbcdcbb05f95a27d38a8be26/backend/hrm-api/src/Mightyfin.Erp.Hrm.Api/ProcurementBudgetRoutes.cs#L34); [backend/hrm-api/src/Mightyfin.Erp.Hrm.Api/ProcurementBudgetRoutes.cs:188](https://github.com/georgemunganga/erp/blob/c38202e57afff83abbcdcbb05f95a27d38a8be26/backend/hrm-api/src/Mightyfin.Erp.Hrm.Api/ProcurementBudgetRoutes.cs#L188); [reference-ui/src/features/LiveBudgets.tsx:14](https://github.com/georgemunganga/module-connect/blob/c370275e9242f447cade47bec31c2fb42aa75637/reference-ui/src/features/LiveBudgets.tsx#L14); [docs/procurement/PROC-FIN-01-BUDGET-RESERVATION-CONTRACT.md:1](https://github.com/georgemunganga/erp/blob/c38202e57afff83abbcdcbb05f95a27d38a8be26/docs/procurement/PROC-FIN-01-BUDGET-RESERVATION-CONTRACT.md#L1); [reference-ui/src/features/LiveOverview.tsx:72](https://github.com/georgemunganga/module-connect/blob/c370275e9242f447cade47bec31c2fb42aa75637/reference-ui/src/features/LiveOverview.tsx#L72); [reference-ui/src/features/Insights.tsx:414](https://github.com/georgemunganga/module-connect/blob/c370275e9242f447cade47bec31c2fb42aa75637/reference-ui/src/features/Insights.tsx#L414); [reference-ui/src/features/Insights.tsx:419](https://github.com/georgemunganga/module-connect/blob/c370275e9242f447cade47bec31c2fb42aa75637/reference-ui/src/features/Insights.tsx#L419); [reference-ui/src/features/Insights.tsx:528](https://github.com/georgemunganga/module-connect/blob/c370275e9242f447cade47bec31c2fb42aa75637/reference-ui/src/features/Insights.tsx#L528)

**ERP specifications:** 

**Zoho references:** [Guide 1](https://www.zoho.com/procurement/help/budgets/budget-vs-actuals-report/), [Guide 2](https://www.zoho.com/procurement/help/budgets/overview/), [Guide 3](https://www.zoho.com/procurement/help/budgets/share-budgets/)

## PAY-12 File/email ingestion and OCR-to-bill conversion

**Priority:** P1 · **Owner:** Core Documents scanner owner; Procurement intake/OCR adapter · **Milestone:** M6

**Scope:** selected operational launch workflows

**Documented expectation:** PROC-10 plus C04 dependency documented separately.

**Current implementation:** Manual Inbox lists saved bills; saved bill documents have Core upload/retry/download source and exact Clean-use pins.

**Gap:** Inbox file/email ingestion, OCR extraction, processed queue, PO line matching and convert-to-bill corrections. Antivirus Clean does not extract invoice fields.

**Acceptance:** Receive a real file/email into an owned queue, scan it and validate any extraction; correct values and convert idempotently to a bill without treating OCR or Clean as business approval.

**Source evidence:** [reference-ui/src/features/LiveInbox.tsx:9](https://github.com/georgemunganga/module-connect/blob/c370275e9242f447cade47bec31c2fb42aa75637/reference-ui/src/features/LiveInbox.tsx#L9); [reference-ui/src/features/LiveInbox.tsx:50](https://github.com/georgemunganga/module-connect/blob/c370275e9242f447cade47bec31c2fb42aa75637/reference-ui/src/features/LiveInbox.tsx#L50); [backend/hrm-api/src/Mightyfin.Erp.Hrm.Api/ProcurementInvoiceIntakeRoutes.cs:31](https://github.com/georgemunganga/erp/blob/c38202e57afff83abbcdcbb05f95a27d38a8be26/backend/hrm-api/src/Mightyfin.Erp.Hrm.Api/ProcurementInvoiceIntakeRoutes.cs#L31); [backend/hrm-api/src/Mightyfin.Erp.Hrm.Api/ProcurementInvoiceDocumentRoutes.cs:81](https://github.com/georgemunganga/erp/blob/c38202e57afff83abbcdcbb05f95a27d38a8be26/backend/hrm-api/src/Mightyfin.Erp.Hrm.Api/ProcurementInvoiceDocumentRoutes.cs#L81); [reference-ui/src/features/InvoiceDocuments.tsx:37](https://github.com/georgemunganga/module-connect/blob/c370275e9242f447cade47bec31c2fb42aa75637/reference-ui/src/features/InvoiceDocuments.tsx#L37); [backend/core-api/src/Mightyfin.Erp.Core.Documents.Domain/DocumentUsePin.cs:65](https://github.com/georgemunganga/erp/blob/c38202e57afff83abbcdcbb05f95a27d38a8be26/backend/core-api/src/Mightyfin.Erp.Core.Documents.Domain/DocumentUsePin.cs#L65); [docs/procurement/release/P31-P32-C04-DOCUMENT-DEPENDENCY-20261025.md:12](https://github.com/georgemunganga/erp/blob/c38202e57afff83abbcdcbb05f95a27d38a8be26/docs/procurement/release/P31-P32-C04-DOCUMENT-DEPENDENCY-20261025.md#L12)

**ERP specifications:** 

**Zoho references:** [Guide 1](https://www.zoho.com/procurement/help/bills/purchase-order-matching/)

## PAY-13 Attachments across credits/payments and cloud sources

**Priority:** P2 · **Owner:** Core Documents/connectors; Procurement record owners · **Milestone:** M1

**Scope:** selected operational launch workflows

**Documented expectation:** Shared document/reference boundary documented.

**Current implementation:** Invoice original-document routes exist; manual payment optional evidence reference exists and upload remains disabled honestly.

**Gap:** Actual payment/credit document owner adapters, permitted cloud import, upload validation and purpose download coverage for those records.

**Acceptance:** Attach/retrieve/retire actual payment or credit evidence under owner-purpose permissions; reject malicious files, expired links and unauthorized access; uploads remain optional where policy permits.

**Source evidence:** [backend/hrm-api/src/Mightyfin.Erp.Hrm.Api/ProcurementInvoiceDocumentRoutes.cs:81](https://github.com/georgemunganga/erp/blob/c38202e57afff83abbcdcbb05f95a27d38a8be26/backend/hrm-api/src/Mightyfin.Erp.Hrm.Api/ProcurementInvoiceDocumentRoutes.cs#L81); [reference-ui/src/features/InvoiceDocuments.tsx:37](https://github.com/georgemunganga/module-connect/blob/c370275e9242f447cade47bec31c2fb42aa75637/reference-ui/src/features/InvoiceDocuments.tsx#L37); [backend/core-api/src/Mightyfin.Erp.Core.Documents.Domain/DocumentUsePin.cs:65](https://github.com/georgemunganga/erp/blob/c38202e57afff83abbcdcbb05f95a27d38a8be26/backend/core-api/src/Mightyfin.Erp.Core.Documents.Domain/DocumentUsePin.cs#L65); [docs/procurement/release/P31-P32-C04-DOCUMENT-DEPENDENCY-20261025.md:12](https://github.com/georgemunganga/erp/blob/c38202e57afff83abbcdcbb05f95a27d38a8be26/docs/procurement/release/P31-P32-C04-DOCUMENT-DEPENDENCY-20261025.md#L12); [backend/hrm-api/src/Mightyfin.Erp.Hrm.Api/ProcurementManualPaymentRoutes.cs:24](https://github.com/georgemunganga/erp/blob/c38202e57afff83abbcdcbb05f95a27d38a8be26/backend/hrm-api/src/Mightyfin.Erp.Hrm.Api/ProcurementManualPaymentRoutes.cs#L24); [backend/hrm-api/src/Mightyfin.Erp.Hrm.Api/ProcurementManualPaymentCommandRoutes.cs:23](https://github.com/georgemunganga/erp/blob/c38202e57afff83abbcdcbb05f95a27d38a8be26/backend/hrm-api/src/Mightyfin.Erp.Hrm.Api/ProcurementManualPaymentCommandRoutes.cs#L23); [backend/hrm-api/src/Mightyfin.Erp.Hrm.Api/ProcurementManualPaymentCommandRoutes.cs:46](https://github.com/georgemunganga/erp/blob/c38202e57afff83abbcdcbb05f95a27d38a8be26/backend/hrm-api/src/Mightyfin.Erp.Hrm.Api/ProcurementManualPaymentCommandRoutes.cs#L46); [reference-ui/src/features/LivePayments.tsx:312](https://github.com/georgemunganga/module-connect/blob/c370275e9242f447cade47bec31c2fb42aa75637/reference-ui/src/features/LivePayments.tsx#L312); [backend/hrm-api/src/Mightyfin.Erp.Hrm.Api/ProcurementCreditIntakeRoutes.cs:13](https://github.com/georgemunganga/erp/blob/c38202e57afff83abbcdcbb05f95a27d38a8be26/backend/hrm-api/src/Mightyfin.Erp.Hrm.Api/ProcurementCreditIntakeRoutes.cs#L13); [backend/hrm-api/src/Mightyfin.Erp.Hrm.Api/ProcurementCreditIntakeRoutes.cs:37](https://github.com/georgemunganga/erp/blob/c38202e57afff83abbcdcbb05f95a27d38a8be26/backend/hrm-api/src/Mightyfin.Erp.Hrm.Api/ProcurementCreditIntakeRoutes.cs#L37); [backend/hrm-api/src/Mightyfin.Erp.Hrm.Api/ProcurementCreditIntakeRoutes.cs:211](https://github.com/georgemunganga/erp/blob/c38202e57afff83abbcdcbb05f95a27d38a8be26/backend/hrm-api/src/Mightyfin.Erp.Hrm.Api/ProcurementCreditIntakeRoutes.cs#L211); [src/platform/procurement-vendor-credits-api.ts:3](https://github.com/georgemunganga/module-connect/blob/c370275e9242f447cade47bec31c2fb42aa75637/src/platform/procurement-vendor-credits-api.ts#L3)

**ERP specifications:** 

**Zoho references:** [Guide 1](https://www.zoho.com/procurement/help/bills/other-actions-for-bills/), [Guide 2](https://www.zoho.com/procurement/help/payments-made/other-actions-for-payments-made/), [Guide 3](https://www.zoho.com/procurement/help/vendor-credits/other-actions-for-vendor-credits/)

## PAY-14 Record discussion, mentions and activity reports

**Priority:** P2 · **Owner:** Procurement collaboration; Core audit/notification services · **Milestone:** M7

**Scope:** selected operational launch workflows

**Documented expectation:** Detailed PROC-14 draft business specification.

**Current implementation:** Immutable business audit exists; generic demo comments remain browser metadata.

**Gap:** Server-owned record comments/audiences/history, rich content, authorized mentions, notification delivery, scoped audit/activity and system-mail report.

**Acceptance:** Persist internal/supplier-visible comments with explicit audience and mention policy; verify scoped timeline/audit views and observable notification retries without exposing hidden content.

**Source evidence:** [reference-ui/src/features/RecordDetail.tsx:224](https://github.com/georgemunganga/module-connect/blob/c370275e9242f447cade47bec31c2fb42aa75637/reference-ui/src/features/RecordDetail.tsx#L224); [reference-ui/src/features/RecordDetail.tsx:644](https://github.com/georgemunganga/module-connect/blob/c370275e9242f447cade47bec31c2fb42aa75637/reference-ui/src/features/RecordDetail.tsx#L644); [docs/procurement/PROC-14-ANALYTICS-CONTROLS-AND-RECORD-COLLABORATION.md:11](https://github.com/georgemunganga/erp/blob/c38202e57afff83abbcdcbb05f95a27d38a8be26/docs/procurement/PROC-14-ANALYTICS-CONTROLS-AND-RECORD-COLLABORATION.md#L11); [reference-ui/src/features/LiveOverview.tsx:72](https://github.com/georgemunganga/module-connect/blob/c370275e9242f447cade47bec31c2fb42aa75637/reference-ui/src/features/LiveOverview.tsx#L72); [reference-ui/src/features/Insights.tsx:414](https://github.com/georgemunganga/module-connect/blob/c370275e9242f447cade47bec31c2fb42aa75637/reference-ui/src/features/Insights.tsx#L414); [reference-ui/src/features/Insights.tsx:419](https://github.com/georgemunganga/module-connect/blob/c370275e9242f447cade47bec31c2fb42aa75637/reference-ui/src/features/Insights.tsx#L419); [reference-ui/src/features/Insights.tsx:528](https://github.com/georgemunganga/module-connect/blob/c370275e9242f447cade47bec31c2fb42aa75637/reference-ui/src/features/Insights.tsx#L528)

**ERP specifications:** 

**Zoho references:** [Guide 1](https://www.zoho.com/procurement/help/analytics/activity/), [Guide 2](https://www.zoho.com/procurement/help/bills/other-actions-for-bills/), [Guide 3](https://www.zoho.com/procurement/help/vendor-credits/other-actions-for-vendor-credits/)

## PAY-15 Payables and purchasing report datasets with drilldown

**Priority:** P1 · **Owner:** Procurement reporting; Finance authoritative monetary datasets · **Milestone:** M7

**Scope:** selected operational launch workflows

**Documented expectation:** PROC-14 specifies governed source/currency/as-of requirements.

**Current implementation:** Live Overview/Analytics show authorized currently loaded record counts and links; amounts/export explicitly unavailable.

**Gap:** AP aging, vendor balances, payable/credit/payment totals, purchases-by-item, approval-duration metrics and full-query drilldown/aggregation; local page counts are incomplete reports.

**Acceptance:** Compute required full-query report datasets with documented metric/FX/time definitions, access scope and consistent drilldown; test against known persisted totals rather than page-local counts.

**Source evidence:** [reference-ui/src/features/LiveOverview.tsx:72](https://github.com/georgemunganga/module-connect/blob/c370275e9242f447cade47bec31c2fb42aa75637/reference-ui/src/features/LiveOverview.tsx#L72); [reference-ui/src/features/Insights.tsx:414](https://github.com/georgemunganga/module-connect/blob/c370275e9242f447cade47bec31c2fb42aa75637/reference-ui/src/features/Insights.tsx#L414); [reference-ui/src/features/Insights.tsx:419](https://github.com/georgemunganga/module-connect/blob/c370275e9242f447cade47bec31c2fb42aa75637/reference-ui/src/features/Insights.tsx#L419); [reference-ui/src/features/Insights.tsx:528](https://github.com/georgemunganga/module-connect/blob/c370275e9242f447cade47bec31c2fb42aa75637/reference-ui/src/features/Insights.tsx#L528)

**ERP specifications:** 

**Zoho references:** [Guide 1](https://www.zoho.com/procurement/help/analytics/activity/), [Guide 2](https://www.zoho.com/procurement/help/analytics/manage-analytic-reports/), [Guide 3](https://www.zoho.com/procurement/help/analytics/overview/), [Guide 4](https://www.zoho.com/procurement/help/analytics/payables/), [Guide 5](https://www.zoho.com/procurement/help/analytics/purchases/)

## PAY-16 Report and transaction sharing

**Priority:** P2 · **Owner:** Shared ERP reporting/export/notifications; Procurement templates · **Milestone:** M7

**Scope:** selected operational launch workflows

**Documented expectation:** PROC-14 describes exports, schedules and sharing.

**Current implementation:** Some transaction tables can export loaded rows; live Analytics export disabled.

**Gap:** Governed full-period/current-view export, report columns/grouping/multi-criteria, PDF/print, templates/password/PII options, email delivery and scheduled reports.

**Acceptance:** Export/share the exact permitted query with correct columns, PII and recipient scope; verify document rendering, delivery status and scheduled retry/deduplication.

**Source evidence:** [reference-ui/src/features/LiveOverview.tsx:72](https://github.com/georgemunganga/module-connect/blob/c370275e9242f447cade47bec31c2fb42aa75637/reference-ui/src/features/LiveOverview.tsx#L72); [reference-ui/src/features/Insights.tsx:414](https://github.com/georgemunganga/module-connect/blob/c370275e9242f447cade47bec31c2fb42aa75637/reference-ui/src/features/Insights.tsx#L414); [reference-ui/src/features/Insights.tsx:419](https://github.com/georgemunganga/module-connect/blob/c370275e9242f447cade47bec31c2fb42aa75637/reference-ui/src/features/Insights.tsx#L419); [reference-ui/src/features/Insights.tsx:528](https://github.com/georgemunganga/module-connect/blob/c370275e9242f447cade47bec31c2fb42aa75637/reference-ui/src/features/Insights.tsx#L528)

**ERP specifications:** 

**Zoho references:** [Guide 1](https://www.zoho.com/procurement/help/analytics/manage-analytic-reports/), [Guide 2](https://www.zoho.com/procurement/help/analytics/overview/), [Guide 3](https://www.zoho.com/procurement/help/batch-payments/other-actions-for-batch-payments/), [Guide 4](https://www.zoho.com/procurement/help/bills/other-actions-for-bills/), [Guide 5](https://www.zoho.com/procurement/help/bills/record-payments-for-bills/), [Guide 6](https://www.zoho.com/procurement/help/bills/share-bills/), [Guide 7](https://www.zoho.com/procurement/help/budgets/budget-vs-actuals-report/), [Guide 8](https://www.zoho.com/procurement/help/budgets/share-budgets/), [Guide 9](https://www.zoho.com/procurement/help/payments-made/other-actions-for-payments-made/), [Guide 10](https://www.zoho.com/procurement/help/payments-made/overview/), [Guide 11](https://www.zoho.com/procurement/help/payments-made/share-payments-made/), [Guide 12](https://www.zoho.com/procurement/help/recurring-bills/other-actions-for-recurring-bills/), [Guide 13](https://www.zoho.com/procurement/help/vendor-credits/other-actions-for-vendor-credits/), [Guide 14](https://www.zoho.com/procurement/help/vendor-credits/share-vendor-credits/)

## PAY-17 Entity preferences and safe bulk operations

**Priority:** P2 · **Owner:** Procurement feature configuration; Core policy infrastructure · **Milestone:** M7

**Scope:** selected operational launch workflows

**Documented expectation:** Some typed approval policies exist; full Zoho-style preferences not documented as complete.

**Current implementation:** Feature-specific fixed DTOs, strict field validation, scope checks and selected single-record commands.

**Gap:** Custom fields, configurable bill/credit/payment validation, custom buttons/links/related lists, saved custom views and import/bulk edit/delete/submit where manuals specify them.

**Acceptance:** Persist selected module preferences and saved views through shared metadata/rules; test bulk commands for per-record scope, partial failures, CAS and controlled cancellation.

**Source evidence:** [backend/hrm-api/src/Mightyfin.Erp.Hrm.Api/ProcurementInvoiceIntakeRoutes.cs:29](https://github.com/georgemunganga/erp/blob/c38202e57afff83abbcdcbb05f95a27d38a8be26/backend/hrm-api/src/Mightyfin.Erp.Hrm.Api/ProcurementInvoiceIntakeRoutes.cs#L29); [backend/hrm-api/src/Mightyfin.Erp.Hrm.Api/ProcurementInvoiceIntakeRoutes.cs:129](https://github.com/georgemunganga/erp/blob/c38202e57afff83abbcdcbb05f95a27d38a8be26/backend/hrm-api/src/Mightyfin.Erp.Hrm.Api/ProcurementInvoiceIntakeRoutes.cs#L129); [reference-ui/src/features/LiveInvoices.tsx:530](https://github.com/georgemunganga/module-connect/blob/c370275e9242f447cade47bec31c2fb42aa75637/reference-ui/src/features/LiveInvoices.tsx#L530); [backend/hrm-api/src/Mightyfin.Erp.Hrm.Api/ProcurementManualPaymentRoutes.cs:24](https://github.com/georgemunganga/erp/blob/c38202e57afff83abbcdcbb05f95a27d38a8be26/backend/hrm-api/src/Mightyfin.Erp.Hrm.Api/ProcurementManualPaymentRoutes.cs#L24); [backend/hrm-api/src/Mightyfin.Erp.Hrm.Api/ProcurementManualPaymentCommandRoutes.cs:23](https://github.com/georgemunganga/erp/blob/c38202e57afff83abbcdcbb05f95a27d38a8be26/backend/hrm-api/src/Mightyfin.Erp.Hrm.Api/ProcurementManualPaymentCommandRoutes.cs#L23); [backend/hrm-api/src/Mightyfin.Erp.Hrm.Api/ProcurementManualPaymentCommandRoutes.cs:46](https://github.com/georgemunganga/erp/blob/c38202e57afff83abbcdcbb05f95a27d38a8be26/backend/hrm-api/src/Mightyfin.Erp.Hrm.Api/ProcurementManualPaymentCommandRoutes.cs#L46); [reference-ui/src/features/LivePayments.tsx:312](https://github.com/georgemunganga/module-connect/blob/c370275e9242f447cade47bec31c2fb42aa75637/reference-ui/src/features/LivePayments.tsx#L312); [backend/hrm-api/src/Mightyfin.Erp.Hrm.Api/ProcurementCreditIntakeRoutes.cs:13](https://github.com/georgemunganga/erp/blob/c38202e57afff83abbcdcbb05f95a27d38a8be26/backend/hrm-api/src/Mightyfin.Erp.Hrm.Api/ProcurementCreditIntakeRoutes.cs#L13); [backend/hrm-api/src/Mightyfin.Erp.Hrm.Api/ProcurementCreditIntakeRoutes.cs:37](https://github.com/georgemunganga/erp/blob/c38202e57afff83abbcdcbb05f95a27d38a8be26/backend/hrm-api/src/Mightyfin.Erp.Hrm.Api/ProcurementCreditIntakeRoutes.cs#L37); [backend/hrm-api/src/Mightyfin.Erp.Hrm.Api/ProcurementCreditIntakeRoutes.cs:211](https://github.com/georgemunganga/erp/blob/c38202e57afff83abbcdcbb05f95a27d38a8be26/backend/hrm-api/src/Mightyfin.Erp.Hrm.Api/ProcurementCreditIntakeRoutes.cs#L211); [src/platform/procurement-vendor-credits-api.ts:3](https://github.com/georgemunganga/module-connect/blob/c370275e9242f447cade47bec31c2fb42aa75637/src/platform/procurement-vendor-credits-api.ts#L3)

**ERP specifications:** 

**Zoho references:** [Guide 1](https://www.zoho.com/procurement/help/batch-payments/approvals-for-batch-payments/), [Guide 2](https://www.zoho.com/procurement/help/batch-payments/preferences/), [Guide 3](https://www.zoho.com/procurement/help/bills/approvals-for-bills/), [Guide 4](https://www.zoho.com/procurement/help/bills/create-bills/), [Guide 5](https://www.zoho.com/procurement/help/bills/manage-bills/), [Guide 6](https://www.zoho.com/procurement/help/bills/other-actions-for-bills/), [Guide 7](https://www.zoho.com/procurement/help/bills/preferences/), [Guide 8](https://www.zoho.com/procurement/help/payments-made/approvals-for-payments-made/), [Guide 9](https://www.zoho.com/procurement/help/payments-made/manage-payments-made/), [Guide 10](https://www.zoho.com/procurement/help/payments-made/other-actions-for-payments-made/), [Guide 11](https://www.zoho.com/procurement/help/payments-made/preferences/), [Guide 12](https://www.zoho.com/procurement/help/payments-made/record-payments-made/), [Guide 13](https://www.zoho.com/procurement/help/recurring-bills/create-recurring-bills/), [Guide 14](https://www.zoho.com/procurement/help/recurring-bills/manage-recurring-bills/), [Guide 15](https://www.zoho.com/procurement/help/recurring-bills/other-actions-for-recurring-bills/), [Guide 16](https://www.zoho.com/procurement/help/recurring-bills/preferences/), [Guide 17](https://www.zoho.com/procurement/help/vendor-credits/approvals-for-vendor-credits/), [Guide 18](https://www.zoho.com/procurement/help/vendor-credits/create-vendor-credits/), [Guide 19](https://www.zoho.com/procurement/help/vendor-credits/other-actions-for-vendor-credits/), [Guide 20](https://www.zoho.com/procurement/help/vendor-credits/vendor-credit-preferences/)

## PAY-18 Accurate current implementation/acceptance status

**Priority:** P2 · **Owner:** Procurement documentation and release ownership · **Milestone:** M0

**Scope:** selected operational launch workflows

**Documented expectation:** Specs and receipts exist but main PROC-12 still starts implementation-in-progress and contains earlier planned wording.

**Current implementation:** PAY-02 explicitly accepts payment HTTP/browser privately; P24 live receipt covers vendor CRUD and recurring planning only.

**Gap:** One current operation-level release matrix linking later payment proof, held P31 positive gates and exact live scope; no foundation-only citation as complete payment proof.

**Acceptance:** Publish a dated operation-by-operation source/private/live matrix tied to actual builds and UAT; preserve earlier foundation-only and failed records as historical evidence.

**Source evidence:** [backend/hrm-api/src/Mightyfin.Erp.Hrm.Api/ProcurementManualPaymentRoutes.cs:24](https://github.com/georgemunganga/erp/blob/c38202e57afff83abbcdcbb05f95a27d38a8be26/backend/hrm-api/src/Mightyfin.Erp.Hrm.Api/ProcurementManualPaymentRoutes.cs#L24); [backend/hrm-api/src/Mightyfin.Erp.Hrm.Api/ProcurementManualPaymentCommandRoutes.cs:23](https://github.com/georgemunganga/erp/blob/c38202e57afff83abbcdcbb05f95a27d38a8be26/backend/hrm-api/src/Mightyfin.Erp.Hrm.Api/ProcurementManualPaymentCommandRoutes.cs#L23); [backend/hrm-api/src/Mightyfin.Erp.Hrm.Api/ProcurementManualPaymentCommandRoutes.cs:46](https://github.com/georgemunganga/erp/blob/c38202e57afff83abbcdcbb05f95a27d38a8be26/backend/hrm-api/src/Mightyfin.Erp.Hrm.Api/ProcurementManualPaymentCommandRoutes.cs#L46); [reference-ui/src/features/LivePayments.tsx:312](https://github.com/georgemunganga/module-connect/blob/c370275e9242f447cade47bec31c2fb42aa75637/reference-ui/src/features/LivePayments.tsx#L312); [backend/hrm-api/src/Mightyfin.Erp.Hrm.Api/ProcurementHostIntegration.cs:350](https://github.com/georgemunganga/erp/blob/c38202e57afff83abbcdcbb05f95a27d38a8be26/backend/hrm-api/src/Mightyfin.Erp.Hrm.Api/ProcurementHostIntegration.cs#L350); [backend/hrm-api/src/Mightyfin.Erp.Hrm.Api/ProcurementHostIntegration.cs:364](https://github.com/georgemunganga/erp/blob/c38202e57afff83abbcdcbb05f95a27d38a8be26/backend/hrm-api/src/Mightyfin.Erp.Hrm.Api/ProcurementHostIntegration.cs#L364)

**ERP specifications:** 

**Zoho references:** [Guide 1](https://www.zoho.com/procurement/help/bills/approvals-for-bills/), [Guide 2](https://www.zoho.com/procurement/help/bills/bill-reconciliation/), [Guide 3](https://www.zoho.com/procurement/help/bills/create-bills/), [Guide 4](https://www.zoho.com/procurement/help/bills/manage-bills/), [Guide 5](https://www.zoho.com/procurement/help/bills/other-actions-for-bills/), [Guide 6](https://www.zoho.com/procurement/help/bills/overview/), [Guide 7](https://www.zoho.com/procurement/help/bills/preferences/), [Guide 8](https://www.zoho.com/procurement/help/bills/purchase-order-matching/), [Guide 9](https://www.zoho.com/procurement/help/bills/record-payments-for-bills/), [Guide 10](https://www.zoho.com/procurement/help/bills/share-bills/), [Guide 11](https://www.zoho.com/procurement/help/payments-made/approvals-for-payments-made/), [Guide 12](https://www.zoho.com/procurement/help/payments-made/manage-payments-made/), [Guide 13](https://www.zoho.com/procurement/help/payments-made/other-actions-for-payments-made/), [Guide 14](https://www.zoho.com/procurement/help/payments-made/overview/), [Guide 15](https://www.zoho.com/procurement/help/payments-made/preferences/), [Guide 16](https://www.zoho.com/procurement/help/payments-made/record-payments-made/), [Guide 17](https://www.zoho.com/procurement/help/payments-made/share-payments-made/)

## CORE-01 Role-specific home, quick request and defined dashboard metrics

**Priority:** P2 · **Owner:** Procurement UI + reporting · **Milestone:** M7

**Scope:** selected operational launch workflows

**Documented expectation:** Zoho My Home and Dashboard manuals describe actionable home panels and spend/payables metrics.

**Current implementation:** A live overview route and readiness gate exist.

**Gap:** No matching metric-definition, data-source or My Home positive proof in this audit.

**Acceptance:** Trace each displayed metric to scoped persisted source and test role-specific quick-create and approvals.

**Source evidence:** [reference-ui/src/app/App.tsx:107](https://github.com/georgemunganga/module-connect/blob/c370275e9242f447cade47bec31c2fb42aa75637/reference-ui/src/app/App.tsx#L107); [reference-ui/src/lib/ProcurementLiveGate.tsx:24](https://github.com/georgemunganga/module-connect/blob/c370275e9242f447cade47bec31c2fb42aa75637/reference-ui/src/lib/ProcurementLiveGate.tsx#L24)

**ERP specifications:** 

**Zoho references:** [Guide 1](https://www.zoho.com/procurement/help/getting-started/navigating-zoho-procurement/), [Guide 2](https://www.zoho.com/procurement/help/getting-started/welcome/), [Guide 3](https://www.zoho.com/procurement/help/home/dashboard/), [Guide 4](https://www.zoho.com/procurement/help/home/my-home/)

## CORE-02 Organization/account switching and self-service MFA

**Priority:** P1 · **Owner:** ERP Core identity · **Milestone:** M0

**Scope:** selected operational launch workflows

**Documented expectation:** Zoho manuals include account email management, MFA, create/join/switch/default/delete organizations.

**Current implementation:** ERP local sign-in, session guard and Core organization UI exist; public sign-in presentation has five passed browser cases.

**Gap:** No Zoho-style MFA, external account lifecycle, org switch/default/delete or real public credential-flow proof established here.

**Acceptance:** Publish explicit Core identity/organization contracts and real authorized tests for supported operations; list unsupported controls honestly.

**Source evidence:** [modules/hrm/frontend/module-connect/src/platform/components/AuthGate.tsx:24](https://github.com/georgemunganga/erp/blob/c38202e57afff83abbcdcbb05f95a27d38a8be26/modules/hrm/frontend/module-connect/src/platform/components/AuthGate.tsx#L24); [backend/hrm-api/src/Mightyfin.Erp.Hrm.Api/LocalIdentity.cs:118](https://github.com/georgemunganga/erp/blob/c38202e57afff83abbcdcbb05f95a27d38a8be26/backend/hrm-api/src/Mightyfin.Erp.Hrm.Api/LocalIdentity.cs#L118); [docs/releases/ERP-SIGNIN-RESTORATION-20261025-ACCEPTANCE.json:1](https://github.com/georgemunganga/erp/blob/4fdaa711c102e8fc45c8d85e7041bead11554a99/docs/releases/ERP-SIGNIN-RESTORATION-20261025-ACCEPTANCE.json#L1)

**ERP specifications:** 

**Zoho references:** [Guide 1](https://www.zoho.com/procurement/help/manage-organization-and-account/manage-organization/), [Guide 2](https://www.zoho.com/procurement/help/manage-organization-and-account/manage-zoho-account/)

## CORE-03 Edit and publish company identity/appearance

**Priority:** P2 · **Owner:** ERP Core branding · **Milestone:** M0

**Scope:** selected operational launch workflows

**Documented expectation:** Zoho branding/profile manuals cover logo, colors, address/contact/regional preferences.

**Current implementation:** Core BrandingPage calls GET/PUT/reset; public branding GET feeds sign-in and was exercised in public UI presentation tests.

**Gap:** The accepted sign-in receipt does not verify live admin PUT/reset or complete organization profile parity.

**Acceptance:** Verify authorized admin edit/reset, nonadmin denial, public readback and scoped organization fields.

**Source evidence:** [modules/hrm/frontend/module-connect/src/routes/hrm.configuration.branding.tsx:157](https://github.com/georgemunganga/erp/blob/c38202e57afff83abbcdcbb05f95a27d38a8be26/modules/hrm/frontend/module-connect/src/routes/hrm.configuration.branding.tsx#L157); [backend/hrm-api/src/Mightyfin.Erp.Hrm.Api/ApiRoutesClean.cs:1373](https://github.com/georgemunganga/erp/blob/c38202e57afff83abbcdcbb05f95a27d38a8be26/backend/hrm-api/src/Mightyfin.Erp.Hrm.Api/ApiRoutesClean.cs#L1373); [docs/releases/ERP-SIGNIN-RESTORATION-20261025-ACCEPTANCE.json:1](https://github.com/georgemunganga/erp/blob/4fdaa711c102e8fc45c8d85e7041bead11554a99/docs/releases/ERP-SIGNIN-RESTORATION-20261025-ACCEPTANCE.json#L1)

**ERP specifications:** 

**Zoho references:** [Guide 1](https://www.zoho.com/procurement/help/settings/organization/branding/), [Guide 2](https://www.zoho.com/procurement/help/settings/organization/profile/)

## CORE-04 Complete and verify existing custom Procurement role administration

**Priority:** P0 · **Owner:** ERP Core permissions · **Milestone:** M0

**Scope:** selected operational launch workflows

**Documented expectation:** Zoho users/roles/departments manuals support invite, custom per-module permissions, assignment and lifecycle.

**Current implementation:** The Core Roles screen already has a Procurement permission grid for access, vendors, items, requests, plans, configuration, contracts and sourcing. Its create/update actions call real APIs; backend role CRUD persists assignments and LocalIdentity expands configured permissions.

**Gap:** The directory text saying “HRM permissions today” is stale. Remaining gap is coverage of newer action families, scoped role-administration UX and deployed positive/negative verification; no absent-designer claim. Branch assignment absence can mean organization-wide scope by policy and is not itself a defect.

**Acceptance:** Inventory current permission options against every supported action, verify create/update/readback and denied role/branch cases with real accounts, then correct stale directory copy after reviewed evidence. Add DTO/effective-authorization parity tests for exact seed, empty, HR-only, unknown and explicit retained actions; preserve valid stored actions on edit.

**Source evidence:** [modules/hrm/frontend/module-connect/src/routes/hrm.configuration.roles.tsx:44](https://github.com/georgemunganga/erp/blob/c38202e57afff83abbcdcbb05f95a27d38a8be26/modules/hrm/frontend/module-connect/src/routes/hrm.configuration.roles.tsx#L44); [modules/hrm/frontend/module-connect/src/routes/hrm.configuration.roles.tsx:116](https://github.com/georgemunganga/erp/blob/c38202e57afff83abbcdcbb05f95a27d38a8be26/modules/hrm/frontend/module-connect/src/routes/hrm.configuration.roles.tsx#L116); [modules/hrm/frontend/module-connect/src/routes/hrm.configuration.roles.tsx:144](https://github.com/georgemunganga/erp/blob/c38202e57afff83abbcdcbb05f95a27d38a8be26/modules/hrm/frontend/module-connect/src/routes/hrm.configuration.roles.tsx#L144); [backend/hrm-api/src/Mightyfin.Erp.Hrm.Api/ApiRoutesClean.cs:1512](https://github.com/georgemunganga/erp/blob/c38202e57afff83abbcdcbb05f95a27d38a8be26/backend/hrm-api/src/Mightyfin.Erp.Hrm.Api/ApiRoutesClean.cs#L1512); [backend/hrm-api/src/Mightyfin.Erp.Hrm.Api/LocalIdentity.cs:169](https://github.com/georgemunganga/erp/blob/c38202e57afff83abbcdcbb05f95a27d38a8be26/backend/hrm-api/src/Mightyfin.Erp.Hrm.Api/LocalIdentity.cs#L169); [modules/hrm/frontend/module-connect/src/routes/hrm.configuration.roles.tsx:142](https://github.com/georgemunganga/erp/blob/c38202e57afff83abbcdcbb05f95a27d38a8be26/modules/hrm/frontend/module-connect/src/routes/hrm.configuration.roles.tsx#L142); [modules/hrm/frontend/module-connect/src/modules/core/settings.ts:30](https://github.com/georgemunganga/erp/blob/c38202e57afff83abbcdcbb05f95a27d38a8be26/modules/hrm/frontend/module-connect/src/modules/core/settings.ts#L30); [backend/hrm-api/src/Mightyfin.Erp.Hrm.Application/ConfigAndExtras/JobsAdminServices.cs:151](https://github.com/georgemunganga/erp/blob/c38202e57afff83abbcdcbb05f95a27d38a8be26/backend/hrm-api/src/Mightyfin.Erp.Hrm.Application/ConfigAndExtras/JobsAdminServices.cs#L151); [backend/hrm-api/src/Mightyfin.Erp.Hrm.Api/ProcurementPermissions.cs:40](https://github.com/georgemunganga/erp/blob/c38202e57afff83abbcdcbb05f95a27d38a8be26/backend/hrm-api/src/Mightyfin.Erp.Hrm.Api/ProcurementPermissions.cs#L40)

**ERP specifications:** 

**Zoho references:** [Guide 1](https://www.zoho.com/procurement/help/settings/users-and-roles/departments/), [Guide 2](https://www.zoho.com/procurement/help/settings/users-and-roles/roles/), [Guide 3](https://www.zoho.com/procurement/help/settings/users-and-roles/users/)

## CORE-05 Select simple, hierarchical, multi-level, custom or no-approval strategy

**Priority:** P1 · **Owner:** Procurement approvals + Core shared engine · **Milestone:** M3

**Scope:** selected operational launch workflows

**Documented expectation:** Zoho manuals describe configurable approval strategies and notification preferences.

**Current implementation:** Procurement live settings mounts feature-specific approval editors and uses Core approvals.

**Gap:** No general strategy switch or feature-complete equivalence; some modules remain capability held.

**Acceptance:** Inventory actual per-module policy configuration and verify maker/checker, SoD, replay, branch and no-approval cases with real Core evidence.

**Source evidence:** [reference-ui/src/features/LiveSettingsShell.tsx:89](https://github.com/georgemunganga/module-connect/blob/c370275e9242f447cade47bec31c2fb42aa75637/reference-ui/src/features/LiveSettingsShell.tsx#L89); [reference-ui/src/features/LiveApprovalSettings.tsx:1](https://github.com/georgemunganga/module-connect/blob/c370275e9242f447cade47bec31c2fb42aa75637/reference-ui/src/features/LiveApprovalSettings.tsx#L1)

**ERP specifications:** 

**Zoho references:** [Guide 1](https://www.zoho.com/procurement/help/admin/approvals/no-approval/), [Guide 2](https://www.zoho.com/procurement/help/settings/approvals/custom-approval/), [Guide 3](https://www.zoho.com/procurement/help/settings/approvals/hierarchical-approval/), [Guide 4](https://www.zoho.com/procurement/help/settings/approvals/multi-level-approval/), [Guide 5](https://www.zoho.com/procurement/help/settings/approvals/overview/), [Guide 6](https://www.zoho.com/procurement/help/settings/approvals/simple-approval/)

## CORE-06 Create custom modules, records and blueprints

**Priority:** P3 · **Owner:** ERP Core extensibility · **Milestone:** M10

**Scope:** separate product decision; not automatically a launch blocker

**Documented expectation:** Zoho custom module manuals cover typed forms, records, field access, blueprints with state/transition/SLA and portal sharing.

**Current implementation:** No generic custom-module editor/engine was found in live Core settings; existing feature-specific workflows are separate.

**Gap:** Generic module/blueprint behavior is an optional future extension, not a blocker for existing governed Procurement feature launch. No generic designer or engine is claimed.

**Acceptance:** Define module ownership and safety boundaries; implement and verify one bounded real module before exposing designer.

**Source evidence:** [modules/hrm/frontend/module-connect/src/modules/core/settings.ts:14](https://github.com/georgemunganga/erp/blob/c38202e57afff83abbcdcbb05f95a27d38a8be26/modules/hrm/frontend/module-connect/src/modules/core/settings.ts#L14); [modules/hrm/frontend/module-connect/src/routes/core.workspace.settings.$slug.tsx:26](https://github.com/georgemunganga/erp/blob/c38202e57afff83abbcdcbb05f95a27d38a8be26/modules/hrm/frontend/module-connect/src/routes/core.workspace.settings.$slug.tsx#L26)

**ERP specifications:** 

**Zoho references:** [Guide 1](https://www.zoho.com/procurement/help/custom-modules/blueprints/), [Guide 2](https://www.zoho.com/procurement/help/custom-modules/create-custom-modules/), [Guide 3](https://www.zoho.com/procurement/help/custom-modules/custom-modules-in-vendor-portal/), [Guide 4](https://www.zoho.com/procurement/help/custom-modules/manage-custom-module-records/), [Guide 5](https://www.zoho.com/procurement/help/custom-modules/manage-custom-modules/), [Guide 6](https://www.zoho.com/procurement/help/custom-modules/overview/), [Guide 7](https://www.zoho.com/procurement/help/custom-modules/preferences/), [Guide 8](https://www.zoho.com/procurement/help/customization/custom-field-data-types/), [Guide 9](https://www.zoho.com/procurement/help/vendor-portal/custom-modules/)

## CORE-07 Define typed custom fields, validation, layout and related lists

**Priority:** P3 · **Owner:** ERP Core extensibility · **Milestone:** M10

**Scope:** separate product decision; not automatically a launch blocker

**Documented expectation:** Zoho customization manuals cover field types, formulas/lookups, layout, rules, buttons and related lists.

**Current implementation:** Vendor editor stores narrow key/value CustomField and ReportingTag attributes with backend validation.

**Gap:** Narrow vendor attributes are not a general field-definition, formula, layout, validation-rule or cross-module customization system.

**Acceptance:** Retain typed vendor attributes; separately design schema/version/permission semantics and verify persisted definitions and reads.

**Source evidence:** [reference-ui/src/features/VendorEditorForm.tsx:69](https://github.com/georgemunganga/module-connect/blob/c370275e9242f447cade47bec31c2fb42aa75637/reference-ui/src/features/VendorEditorForm.tsx#L69); [backend/hrm-api/src/Mightyfin.Erp.Hrm.Api/ProcurementVendorRoutes.cs:852](https://github.com/georgemunganga/erp/blob/c38202e57afff83abbcdcbb05f95a27d38a8be26/backend/hrm-api/src/Mightyfin.Erp.Hrm.Api/ProcurementVendorRoutes.cs#L852)

**ERP specifications:** 

**Zoho references:** [Guide 1](https://www.zoho.com/procurement/help/settings/customization/custom-buttons/), [Guide 2](https://www.zoho.com/procurement/help/settings/customization/custom-fields/), [Guide 3](https://www.zoho.com/procurement/help/settings/customization/formula-data-type/), [Guide 4](https://www.zoho.com/procurement/help/settings/customization/lookup-data-type/), [Guide 5](https://www.zoho.com/procurement/help/settings/customization/page-layouts/), [Guide 6](https://www.zoho.com/procurement/help/settings/customization/related-lists/), [Guide 7](https://www.zoho.com/procurement/help/settings/customization/validation-rules/)

## CORE-08 Manage numbering, reporting tags, PDFs, notifications and web tabs

**Priority:** P2 · **Owner:** ERP Core shared defaults · **Milestone:** M7

**Scope:** selected operational launch workflows

**Documented expectation:** Zoho manuals provide editable numbering, tags, PDF templates, notifications and links.

**Current implementation:** Core lists these entries in the directory.

**Gap:** The slug route renders Editor coming soon and cannot save; vendor tags are narrow attributes.

**Acceptance:** Expose only genuinely persisted editors with owner policy and preview/readback; keep absent controls explicitly unavailable.

**Source evidence:** [modules/hrm/frontend/module-connect/src/modules/core/settings.ts:14](https://github.com/georgemunganga/erp/blob/c38202e57afff83abbcdcbb05f95a27d38a8be26/modules/hrm/frontend/module-connect/src/modules/core/settings.ts#L14); [modules/hrm/frontend/module-connect/src/routes/core.workspace.settings.$slug.tsx:26](https://github.com/georgemunganga/erp/blob/c38202e57afff83abbcdcbb05f95a27d38a8be26/modules/hrm/frontend/module-connect/src/routes/core.workspace.settings.$slug.tsx#L26)

**ERP specifications:** 

**Zoho references:** [Guide 1](https://www.zoho.com/procurement/help/settings/customization/pdf-templates/), [Guide 2](https://www.zoho.com/procurement/help/settings/customization/reporting-tags/), [Guide 3](https://www.zoho.com/procurement/help/settings/customization/transaction-number-series/), [Guide 4](https://www.zoho.com/procurement/help/settings/customization/web-tabs/)

## CORE-09 Create and execute workflow rules, alerts, field updates, webhooks and schedules

**Priority:** P1 · **Owner:** ERP Core automation · **Milestone:** M7

**Scope:** selected operational launch workflows

**Documented expectation:** Zoho manuals include trigger conditions, actions, audit/logs and scheduled execution.

**Current implementation:** Procurement preview settings and legacy HRM mock process page display sample controls; live shell does not mount a general automation editor.

**Gap:** No persisted general rule/action scheduler or safe integration authorization was found in this UI slice.

**Acceptance:** Specify bounded event source, action safety, idempotency and scoped logs; prove execution and denial before enabling UI.

**Source evidence:** [reference-ui/src/features/Settings.tsx:1438](https://github.com/georgemunganga/module-connect/blob/c370275e9242f447cade47bec31c2fb42aa75637/reference-ui/src/features/Settings.tsx#L1438); [reference-ui/src/features/LiveSettingsShell.tsx:102](https://github.com/georgemunganga/module-connect/blob/c370275e9242f447cade47bec31c2fb42aa75637/reference-ui/src/features/LiveSettingsShell.tsx#L102); [modules/hrm/frontend/module-connect/src/routes/hrm.configuration.process.tsx:8](https://github.com/georgemunganga/erp/blob/c38202e57afff83abbcdcbb05f95a27d38a8be26/modules/hrm/frontend/module-connect/src/routes/hrm.configuration.process.tsx#L8)

**ERP specifications:** 

**Zoho references:** [Guide 1](https://www.zoho.com/procurement/help/settings/automation/custom-functions/), [Guide 2](https://www.zoho.com/procurement/help/settings/automation/email-alerts/), [Guide 3](https://www.zoho.com/procurement/help/settings/automation/field-updates/), [Guide 4](https://www.zoho.com/procurement/help/settings/automation/in-app-notifications/), [Guide 5](https://www.zoho.com/procurement/help/settings/automation/schedules/), [Guide 6](https://www.zoho.com/procurement/help/settings/automation/webhooks/), [Guide 7](https://www.zoho.com/procurement/help/settings/automation/workflow-rules/), [Guide 8](https://www.zoho.com/procurement/help/settings/customization/email-notifications/), [Guide 9](https://www.zoho.com/procurement/help/settings/setup-and-configurations/reminders/)

## CORE-10 Administer API usage, connections, incoming webhooks, signals, backups and forms

**Priority:** P3 · **Owner:** ERP Core integrations · **Milestone:** M10

**Scope:** separate product decision; not automatically a launch blocker

**Documented expectation:** Zoho developer-data manuals define credentials, logs, quotas, webhooks, connections, backup and forms operations.

**Current implementation:** No corresponding live shared settings routes were found in audited Core directory.

**Gap:** These admin operations are absent from visible connected settings; production backup practices alone do not establish self-service UI parity.

**Acceptance:** Separate security-sensitive integration scope from backup operations; test tenant isolation, credentials, audit and recovery.

**Source evidence:** [modules/hrm/frontend/module-connect/src/modules/core/settings.ts:43](https://github.com/georgemunganga/erp/blob/c38202e57afff83abbcdcbb05f95a27d38a8be26/modules/hrm/frontend/module-connect/src/modules/core/settings.ts#L43); [modules/hrm/frontend/module-connect/src/routes/core.workspace.settings.$slug.tsx:32](https://github.com/georgemunganga/erp/blob/c38202e57afff83abbcdcbb05f95a27d38a8be26/modules/hrm/frontend/module-connect/src/routes/core.workspace.settings.$slug.tsx#L32)

**ERP specifications:** 

**Zoho references:** [Guide 1](https://www.zoho.com/procurement/help/settings/developer-data/api-usage/), [Guide 2](https://www.zoho.com/procurement/help/settings/developer-data/backups/), [Guide 3](https://www.zoho.com/procurement/help/settings/developer-data/connections/), [Guide 4](https://www.zoho.com/procurement/help/settings/developer-data/incoming-webhooks/), [Guide 5](https://www.zoho.com/procurement/help/settings/developer-data/signals/), [Guide 6](https://www.zoho.com/procurement/help/settings/developer-data/web-forms/)

## CORE-11 Manage taxes, currency/rates, accounting references, customers/projects and general defaults

**Priority:** P2 · **Owner:** ERP Core + Finance · **Milestone:** M3

**Scope:** selected operational launch workflows

**Documented expectation:** Zoho settings manuals expose tax/TDS, currencies, finance accounts, customers/projects and module defaults.

**Current implementation:** Live Procurement redirects Finance-owned settings; Core directory lists currencies/general as planned.

**Gap:** No integrated editable ERP equivalent in this settings slice; absence does not establish lack of Finance backend.

**Acceptance:** Owner-by-owner source inventory and positive UI/API checks before connecting settings entries.

**Source evidence:** [reference-ui/src/features/LiveSettingsShell.tsx:106](https://github.com/georgemunganga/module-connect/blob/c370275e9242f447cade47bec31c2fb42aa75637/reference-ui/src/features/LiveSettingsShell.tsx#L106); [modules/hrm/frontend/module-connect/src/modules/core/settings.ts:40](https://github.com/georgemunganga/erp/blob/c38202e57afff83abbcdcbb05f95a27d38a8be26/modules/hrm/frontend/module-connect/src/modules/core/settings.ts#L40)

**ERP specifications:** 

**Zoho references:** [Guide 1](https://www.zoho.com/procurement/help/settings/module-settings/chart-of-accounts/), [Guide 2](https://www.zoho.com/procurement/help/settings/module-settings/customers/), [Guide 3](https://www.zoho.com/procurement/help/settings/module-settings/projects/), [Guide 4](https://www.zoho.com/procurement/help/settings/setup-and-configurations/currencies/), [Guide 5](https://www.zoho.com/procurement/help/settings/setup-and-configurations/general/), [Guide 6](https://www.zoho.com/procurement/help/settings/taxes-and-compliance/tax-settings/), [Guide 7](https://www.zoho.com/procurement/help/settings/taxes-and-compliance/taxes/), [Guide 8](https://www.zoho.com/procurement/help/settings/taxes-and-compliance/tds-rates/)

## CORE-12 Supplier self-service portal for invite, bids, orders, invoices, payments and statements

**Priority:** P1 · **Owner:** Supplier identity + Procurement · **Milestone:** M8

**Scope:** supplier-facing launch; supplier banking remains Finance-owned

**Documented expectation:** Zoho portal manuals describe external signup and supplier transaction actions.

**Current implementation:** Vendor editor can request portal access, and outer preview has a demo supplier screen; live settings says no portal editor.

**Gap:** External supplier login and transaction authority remain outside the current held capability. This becomes critical only if full supplier self-service is part of the launch scope; it is not a discovered security vulnerability.

**Acceptance:** Define finite external identity and supplier scope, verify actual invitation/login, bid, PO response and no unauthorized access before exposing portal.

**Source evidence:** [reference-ui/src/features/LiveSettingsShell.tsx:105](https://github.com/georgemunganga/module-connect/blob/c370275e9242f447cade47bec31c2fb42aa75637/reference-ui/src/features/LiveSettingsShell.tsx#L105); [reference-ui/src/data/settings.ts:165](https://github.com/georgemunganga/module-connect/blob/c370275e9242f447cade47bec31c2fb42aa75637/reference-ui/src/data/settings.ts#L165); [reference-ui/src/features/VendorEditorForm.tsx:55](https://github.com/georgemunganga/module-connect/blob/c370275e9242f447cade47bec31c2fb42aa75637/reference-ui/src/features/VendorEditorForm.tsx#L55); [src/modules/procurement/payables/other-screens.tsx:220](https://github.com/georgemunganga/module-connect/blob/c370275e9242f447cade47bec31c2fb42aa75637/src/modules/procurement/payables/other-screens.tsx#L220); [docs/procurement/release/PROC-MODULE-COVERAGE-20261025.json:889](https://github.com/georgemunganga/erp/blob/c38202e57afff83abbcdcbb05f95a27d38a8be26/docs/procurement/release/PROC-MODULE-COVERAGE-20261025.json#L889)

**ERP specifications:** 

**Zoho references:** [Guide 1](https://www.zoho.com/procurement/help/settings/setup-and-configurations/vendor-portal-preferences/), [Guide 2](https://www.zoho.com/procurement/help/vendor-portal/home/), [Guide 3](https://www.zoho.com/procurement/help/vendor-portal/invoices/), [Guide 4](https://www.zoho.com/procurement/help/vendor-portal/other-actions/), [Guide 5](https://www.zoho.com/procurement/help/vendor-portal/overview/), [Guide 6](https://www.zoho.com/procurement/help/vendor-portal/payments-received/), [Guide 7](https://www.zoho.com/procurement/help/vendor-portal/purchase-orders/), [Guide 8](https://www.zoho.com/procurement/help/vendor-portal/request-for-quotes/), [Guide 9](https://www.zoho.com/procurement/help/vendor-portal/sign-up/), [Guide 10](https://www.zoho.com/procurement/help/vendor-portal/statements/)

## CORE-13 Reach shared Core and HRM settings from published URLs

**Priority:** P0 · **Owner:** ERP frontend release · **Milestone:** M0

**Scope:** selected operational launch workflows

**Documented expectation:** Zoho settings manuals assume navigable organization and account administration.

**Current implementation:** Current source has /core/workspace/settings and nested editors; public route probe returns 404 for these and /hrm/workspace/settings, while /core and /core/branding return 200.

**Gap:** The deployed release does not match the current hierarchical settings routes. HTTP 200 on legacy pages does not prove current Core editor or permission behavior.

**Acceptance:** Ship reviewed route registration and test live public navigation/authorized editor read/write and unauthorized denial, preserving sign-in behavior.

**Source evidence:** [modules/hrm/frontend/module-connect/src/routes/core.workspace.settings.$slug.tsx:20](https://github.com/georgemunganga/erp/blob/c38202e57afff83abbcdcbb05f95a27d38a8be26/modules/hrm/frontend/module-connect/src/routes/core.workspace.settings.$slug.tsx#L20)

**ERP specifications:** 

**Zoho references:** [Guide 1](https://www.zoho.com/procurement/help/manage-organization-and-account/manage-organization/), [Guide 2](https://www.zoho.com/procurement/help/manage-organization-and-account/manage-zoho-account/), [Guide 3](https://www.zoho.com/procurement/help/settings/organization/branding/), [Guide 4](https://www.zoho.com/procurement/help/settings/organization/profile/), [Guide 5](https://www.zoho.com/procurement/help/settings/users-and-roles/departments/), [Guide 6](https://www.zoho.com/procurement/help/settings/users-and-roles/roles/), [Guide 7](https://www.zoho.com/procurement/help/settings/users-and-roles/users/)

## AUD-01 Publish a current implementation baseline beside historical design drafts

**Priority:** P0 · **Owner:** ERP documentation owner · **Milestone:** M0

**Scope:** selected operational launch workflows

**Documented expectation:** PROC-00 §2 says no Procurement backend/frontend; ERP System Definition says production has no Procurement tables. Those are dated original baselines, not the current implemented position.

**Current implementation:** Bounded Procurement projects, routes and later production/private acceptance records exist. The active roadmap correctly retains a dated chronology.

**Gap:** A reader-facing current source/private/live status above the stale present-tense claims and the stale HRM-only role description.

**Acceptance:** Date-stamp retained historical claims; connect each current claim to a versioned product/UI build and operation-specific acceptance, without relabelling earlier failures.

**Source evidence:** [docs/procurement/PROC-00-PROCUREMENT-MODULE-ARCHITECTURE-AND-CAPABILITY-MAP.md:25](https://github.com/georgemunganga/erp/blob/c38202e57afff83abbcdcbb05f95a27d38a8be26/docs/procurement/PROC-00-PROCUREMENT-MODULE-ARCHITECTURE-AND-CAPABILITY-MAP.md#L25); [docs/engineering/ERP-SYSTEM-DEFINITION.md:48](https://github.com/georgemunganga/erp/blob/c38202e57afff83abbcdcbb05f95a27d38a8be26/docs/engineering/ERP-SYSTEM-DEFINITION.md#L48); [docs/procurement/release/PROC-OPS-P23-P24-LIVE-ACCEPTANCE-20261022.md:3](https://github.com/georgemunganga/erp/blob/c38202e57afff83abbcdcbb05f95a27d38a8be26/docs/procurement/release/PROC-OPS-P23-P24-LIVE-ACCEPTANCE-20261022.md#L3); [modules/hrm/frontend/module-connect/src/modules/core/settings.ts:30](https://github.com/georgemunganga/erp/blob/c38202e57afff83abbcdcbb05f95a27d38a8be26/modules/hrm/frontend/module-connect/src/modules/core/settings.ts#L30)

**ERP specifications:** 

**Zoho references:** [Guide 1](https://www.zoho.com/procurement/help/getting-started/welcome/)

## AUD-02 Tie the 28-section designs to individual accepted operations

**Priority:** P1 · **Owner:** Product + engineering + QA · **Milestone:** M0

**Scope:** selected operational launch workflows

**Documented expectation:** Twenty-five checked child/slice files have all 28 required numbered headings. That verifies structure, not completeness of their business decisions or implementation.

**Current implementation:** Named release receipts verify particular private or production slices; draft CRUD, internal review and source schemas have deliberately different scopes.

**Gap:** One accepted operation register spanning screen, API, scope, state guard, owned data/effects, failure recovery and deployed evidence for each selected launch journey.

**Acceptance:** For each in-scope operation record its exact entry/state/actor/data scope/source version/effects plus allowed, denied, stale and retry tests; do not count passing heading checks or unrelated tests as workflow acceptance.

**Source evidence:** [docs/procurement/PROC-00-PROCUREMENT-MODULE-ARCHITECTURE-AND-CAPABILITY-MAP.md:39](https://github.com/georgemunganga/erp/blob/c38202e57afff83abbcdcbb05f95a27d38a8be26/docs/procurement/PROC-00-PROCUREMENT-MODULE-ARCHITECTURE-AND-CAPABILITY-MAP.md#L39); [docs/procurement/release/PROC-MODULE-COVERAGE-20261025.json:11](https://github.com/georgemunganga/erp/blob/c38202e57afff83abbcdcbb05f95a27d38a8be26/docs/procurement/release/PROC-MODULE-COVERAGE-20261025.json#L11); [docs/procurement/PROC-WORKFLOW-REFERENCE-TRACE.md:22](https://github.com/georgemunganga/erp/blob/c38202e57afff83abbcdcbb05f95a27d38a8be26/docs/procurement/PROC-WORKFLOW-REFERENCE-TRACE.md#L22)

**ERP specifications:** 

**Zoho references:** [Guide 1](https://www.zoho.com/procurement/help/purchase-requests/convert-requested-items/), [Guide 2](https://www.zoho.com/procurement/help/request-for-quotes/award-request-for-quotes/)

## AUD-03 Make organisation-wide versus unassigned branch access an explicit policy

**Priority:** P0 · **Owner:** ERP Core data-scope owner · **Milestone:** M0

**Scope:** selected operational launch workflows

**Documented expectation:** User requirements distinguish company-wide viewing, branch transaction context and role/action permission. Existing Core code intentionally treats no branch assignments as tenant-wide reach.

**Current implementation:** Procurement rechecks current Core user/actions and selected company/branch; a Worker record is optional. Assigned branches constrain users.

**Gap:** A visible, approved distinction between intentionally organisation-wide and not-yet-configured access, with clear user-administration language. This is a policy/control review, not a demonstrated cross-tenant exploit.

**Acceptance:** Verify explicit organisation-wide, single/multiple branch, unconfigured user and revoked grant cases across reads, creates, assignee pickers and exports; never infer a company grant from an employee home.

**Source evidence:** [backend/hrm-api/src/Mightyfin.Erp.Hrm.Api/ProcurementHostIntegration.cs:650](https://github.com/georgemunganga/erp/blob/c38202e57afff83abbcdcbb05f95a27d38a8be26/backend/hrm-api/src/Mightyfin.Erp.Hrm.Api/ProcurementHostIntegration.cs#L650); [backend/hrm-api/src/Mightyfin.Erp.Hrm.Api/ProcurementPrincipalPeople.cs:18](https://github.com/georgemunganga/erp/blob/c38202e57afff83abbcdcbb05f95a27d38a8be26/backend/hrm-api/src/Mightyfin.Erp.Hrm.Api/ProcurementPrincipalPeople.cs#L18); [backend/hrm-api/src/Mightyfin.Erp.Hrm.Api/ProcurementHostIntegration.cs:672](https://github.com/georgemunganga/erp/blob/c38202e57afff83abbcdcbb05f95a27d38a8be26/backend/hrm-api/src/Mightyfin.Erp.Hrm.Api/ProcurementHostIntegration.cs#L672)

**ERP specifications:** 

**Zoho references:** [Guide 1](https://www.zoho.com/procurement/help/settings/users-and-roles/roles/), [Guide 2](https://www.zoho.com/procurement/help/settings/users-and-roles/users/)

## AUD-04 Display the same effective permissions that the authorizer enforces

**Priority:** P2 · **Owner:** Core role administration + authorization · **Milestone:** M0

**Scope:** selected operational launch workflows

**Documented expectation:** Custom Procurement roles and real role create/update operations already exist. The actual action catalog is broader than the rendered grid.

**Current implementation:** Role DTO projection can display bootstrap fallback when no stored known Procurement action exists; the authorizer allows that fallback only for the exact intentional legacy seed.

**Gap:** Regression evidence for effective/displayed permission equivalence on empty, unknown, HR-only and exact-legacy rows, plus preservation of newer actions not visible in the grid. This is a static code-path mismatch; no affected production row is established.

**Acceptance:** For each legacy/custom role case compare DTO/selected UI values with ResolveConfiguredActionsAsync; editing unrelated role fields must not manufacture or drop effective grants.

**Source evidence:** [backend/hrm-api/src/Mightyfin.Erp.Hrm.Application/ConfigAndExtras/JobsAdminServices.cs:151](https://github.com/georgemunganga/erp/blob/c38202e57afff83abbcdcbb05f95a27d38a8be26/backend/hrm-api/src/Mightyfin.Erp.Hrm.Application/ConfigAndExtras/JobsAdminServices.cs#L151); [backend/hrm-api/src/Mightyfin.Erp.Hrm.Api/ProcurementPermissions.cs:40](https://github.com/georgemunganga/erp/blob/c38202e57afff83abbcdcbb05f95a27d38a8be26/backend/hrm-api/src/Mightyfin.Erp.Hrm.Api/ProcurementPermissions.cs#L40); [modules/hrm/frontend/module-connect/src/routes/hrm.configuration.roles.tsx:44](https://github.com/georgemunganga/erp/blob/c38202e57afff83abbcdcbb05f95a27d38a8be26/modules/hrm/frontend/module-connect/src/routes/hrm.configuration.roles.tsx#L44)

**ERP specifications:** 

**Zoho references:** [Guide 1](https://www.zoho.com/procurement/help/settings/users-and-roles/roles/)

## AUD-05 Accept a coherent Core/HRM/Procurement release rather than infer it from separate proofs

**Priority:** P0 · **Owner:** Release engineering + QA · **Milestone:** M9

**Scope:** selected operational launch workflows

**Documented expectation:** Current receipts distinguish source, private schema/runtime, browser and production acceptance; the newly restored shared login is a separate frontend-only release.

**Current implementation:** Public login is ERP-wide; public hierarchical Core/HRM settings paths return404. Later Procurement features have private proofs while the deployed API remains at the earlier accepted baseline.

**Gap:** A compatible release manifest and actual normal-session journey through the chosen launch operations with the same API/schema/UI/permission configuration. Private passing tests do not imply live availability.

**Acceptance:** Pin source/build/schema/config and current authority; migrate and restore-test on staging, complete positive/negative real-session UAT and post-release smoke checks, then publish the precise live capability matrix.

**Source evidence:** [docs/procurement/release/PROC-MODULE-COVERAGE-20261025.json:15](https://github.com/georgemunganga/erp/blob/c38202e57afff83abbcdcbb05f95a27d38a8be26/docs/procurement/release/PROC-MODULE-COVERAGE-20261025.json#L15); [docs/releases/ERP-SIGNIN-RESTORATION-20261025-ACCEPTANCE.json:1](https://github.com/georgemunganga/erp/blob/c38202e57afff83abbcdcbb05f95a27d38a8be26/docs/releases/ERP-SIGNIN-RESTORATION-20261025-ACCEPTANCE.json#L1); [docs/procurement/release/P35-NORMAL-COOKIE03-ACCEPTANCE-20261025.json:1](https://github.com/georgemunganga/erp/blob/c38202e57afff83abbcdcbb05f95a27d38a8be26/docs/procurement/release/P35-NORMAL-COOKIE03-ACCEPTANCE-20261025.json#L1); [docs/procurement/release/P38-FOUNDATION-SCHEMA-ACCEPTANCE-20261025.json:1](https://github.com/georgemunganga/erp/blob/c38202e57afff83abbcdcbb05f95a27d38a8be26/docs/procurement/release/P38-FOUNDATION-SCHEMA-ACCEPTANCE-20261025.json#L1)

**ERP specifications:** 

**Zoho references:** [Guide 1](https://www.zoho.com/procurement/help/getting-started/navigating-zoho-procurement/)
