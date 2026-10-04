# Procurement documentation

[PROC-00 Procurement Module Architecture and Capability Map](PROC-00-PROCUREMENT-MODULE-ARCHITECTURE-AND-CAPABILITY-MAP.md) is the current draft master document. It defines the module boundary, 12 parent capability groups, line-level request routing, shared ERP dependencies, data ownership, and documentation/build sequence.

[PROC-UX-01 Procurement Navigation and Lovable Prompts](PROC-UX-01-NAVIGATION-AND-LOVABLE-PROMPTS.md) translates the capability map into role-aware side menus, sublinks, and copyable prompts for the UI designer.

[PROC-UX-02 Complete Procurement UI Prompt Pack](PROC-UX-02-COMPLETE-LOVABLE-UI-PROMPT-PACK.md) provides a sequenced Lovable handoff for the entire internal and supplier-facing UI, including role views, exception states, and an end-to-end journey.

[PROC-UX-03 Lovable Implementation Review](PROC-UX-03-LOVABLE-IMPLEMENTATION-REVIEW.md) compares the prompt pack with the current `module-connect` Procurement UI and provides a corrective prompt to run before Prompt 09.

[PROC-UX-04 Procurement Settings and Empty Record States](PROC-UX-04-SETTINGS-AND-EMPTY-RECORD-STATES.md) defines the configuration directory, Bill approval previews, and working CRUD-oriented empty states for first-use pages.

[PROC-UX-05 New Vendor Form](PROC-UX-05-NEW-VENDOR-FORM.md) specifies the tabbed onboarding form, validation, document limits, and review status used by the frontend.

[PROC-UX-06 Plain Language for Procurement](PROC-UX-06-PLAIN-LANGUAGE.md) gives the UI designer preferred labels, writing rules, and checks for keeping workflows understandable without changing their controls.

[PROC-UX-07 Simple Procurement Screens](PROC-UX-07-SIMPLE-SCREENS.md) turns the supplied Zoho screenshots into layout and interaction guidance for record pages, forms, and lists.

[PROC-UX-08 Shared ERP Shell and Procurement Navigation](PROC-UX-08-SHARED-ERP-SHELL.md) records which header, menu, and button patterns are reused from HRM, what stays module-specific, and the current boundary between the two frontend deployments.

[PROC-UX-09 Shared Data Transfer](PROC-UX-09-SHARED-DATA-TRANSFER.md) defines the reused HRM import/export flow for Procurement vendors and items, its demo behavior, and the server contract needed for production data.

Every current child draft uses the ERP's mandatory **Module → Parent Feature → Child Feature → Operation or Scenario** hierarchy and [28-section framework](../hrm/feature-specifications/ERP_Feature_Specification_Framework_Enterprise_Integration_Updated.docx). The full catalogue is:

| Phase | Child feature specification |
|---|---|
| Foundation | [PROC-CFG-01 Procurement Configuration and Policy](PROC-CFG-01-PROCUREMENT-CONFIGURATION-AND-POLICY.md) |
| Foundation | [PROC-01 Supplier Onboarding and Management](PROC-01-SUPPLIER-ONBOARDING-AND-MANAGEMENT.md) |
| Foundation | [PROC-02 Purchase Requisition and Line Routing](PROC-02-PURCHASE-REQUISITION-AND-LINE-ROUTING.md) |
| Foundation | [PROC-03 Approval and Budget Control](PROC-03-APPROVAL-AND-BUDGET-CONTROL.md) |
| Purchasing | [PROC-06 Strategic Sourcing and Award](PROC-06-STRATEGIC-SOURCING-AND-AWARD.md) |
| Purchasing | [PROC-08 Purchase Orders and Amendments](PROC-08-PURCHASE-ORDERS-AND-AMENDMENTS.md) |
| Fulfillment | [PROC-09 Receiving, Inspection and Returns](PROC-09-RECEIVING-INSPECTION-AND-RETURNS.md) |
| Procure-to-pay | [PROC-10 Invoice Intake and Supplier Adjustments](PROC-10-INVOICE-INTAKE-AND-SUPPLIER-ADJUSTMENTS.md) |
| Procure-to-pay | [PROC-11 Invoice Match, AP Handoff and Payment Visibility](PROC-11-INVOICE-MATCH-AP-HANDOFF-AND-PAYMENT-VISIBILITY.md) |
| Advanced | [PROC-04 Procurement Items and Catalog](PROC-04-PROCUREMENT-ITEMS-AND-CATALOG.md) |
| Advanced | [PROC-05 Procurement Planning](PROC-05-PROCUREMENT-PLANNING.md) |
| Advanced | [PROC-07 Contracts, Frameworks and Recurring Commitments](PROC-07-CONTRACTS-FRAMEWORKS-AND-RECURRING-COMMITMENTS.md) |
| Advanced | [PROC-12 Supplier Portal](PROC-12-SUPPLIER-PORTAL.md) |
| Advanced | [PROC-13 Supplier Performance and Risk](PROC-13-SUPPLIER-PERFORMANCE-AND-RISK.md) |
| Cross-cutting | [PROC-14 Analytics, Controls and Record Collaboration](PROC-14-ANALYTICS-CONTROLS-AND-RECORD-COLLABORATION.md) |

The catalogue covers the current [PROC-00](PROC-00-PROCUREMENT-MODULE-ARCHITECTURE-AND-CAPABILITY-MAP.md) capability map as **draft documentation**. Business policy values, named owners, technical contracts and release scope in each section 28 still need approval before implementation.

The [coverage and decision register](COVERAGE-AND-DECISION-REGISTER.md) maps the supplied module outline and later capability findings to these documents and collects the cross-module decisions for stakeholder review.

The [earlier workflow blueprint](archive/ENTERPRISE-WORKFLOW-BLUEPRINT.md) and [provisional seven-feature draft](archive/PHASE-1-FEATURE-SPECIFICATIONS.md) are archived because their phase plan and identifiers conflict with PROC-00. They remain source material for the new child documents, not current implementation contracts.
