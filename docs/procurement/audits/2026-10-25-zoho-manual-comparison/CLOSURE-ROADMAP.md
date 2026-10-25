# Proposed closure milestones

This closes the audited selected business workflows without rebuilding the existing UI or duplicating ERP services. It is a proposed engineering order; it does not approve spending limits, change existing grants or enable held capabilities. M8 and M10 depend on the product launch scope.

| Milestone | Outcome | Owner | Findings |
| --- | --- | --- | --- |
| M0 — Repair baseline visibility and Core administration | Publish current source/private/live manifest, ship reviewed hierarchical settings routes, expose all appropriate role actions, test effective role readback and explicit company/branch scope. | ERP Core + release engineering | PAY-18, CORE-02, CORE-03, CORE-04, CORE-13, AUD-01, AUD-02, AUD-03, AUD-04 |
| M1 — Accept shared document scanning | Complete genuine Core scanner verification and scoped retained evidence use; malicious/stale/unavailable scans deny safely without invented Clean results. | Core Documents | BE-04, BE-13, PAY-02, PAY-13 |
| M2 — Close master-data and reusable data-transfer gaps | Verify complete supplier/item/contact/site fields and lifecycle, catalog eligibility and shared dry-run/import/export/duplicate workflows. Preserve the supplied forms. | Procurement master data | BE-01, BE-02, BE-17, BE-18 |
| M3 — Complete request approval and demand routing | Accept system-user requests, current authority/delegation, explicit funding evidence and residual-safe partial/cross-request RFQ/PO conversion. Finance owns the budget master. | Procurement buying + Core approvals + Finance interface | BE-03, BE-09, PAY-10, CORE-05, CORE-11 |
| M4 — Complete competitive sourcing through an orderable award | Accept round/bid controls, current original documents, evaluator evidence, governed split awards, cross-round held quantity and award-to-PO source adoption. | Procurement sourcing | BE-11, BE-12 |
| M5 — Complete order fulfilment and corrections | Accept issue/dispatch evidence, acknowledgement, controlled changes and partial cancellation, goods/service/inspection/returns and independent receipt/invoice progress. | Procurement purchasing + receiving | BE-06, BE-07, BE-10 |
| M6 — Complete procurement invoices and manual settlement workflows | Accept PO/receipt-to-invoice, matching/exceptions, credit application/refund, manual allocation/advances/excess and authoritative batch recording. Keep bank/AP execution with Finance. | Procurement payables + Finance interface | BE-08, PAY-01, PAY-03, PAY-04, PAY-05, PAY-06, PAY-07, PAY-08, PAY-12 |
| M7 — Complete operational controls and repeat purchasing | Finish applicable contract activation/utilisation, recurrence execution, useful templates/notifications, comments and reconciled reporting. Reuse Core services. | Procurement + Core shared services | BE-05, BE-15, BE-19, PAY-09, PAY-11, PAY-14, PAY-15, PAY-16, PAY-17, CORE-01, CORE-08, CORE-09 |
| M8 — Deliver supplier self-service if included in launch scope | Accept external supplier invitation/login, own-record access, bids, acknowledgements, invoice/credit submission and truthful payment visibility. No internal-user shortcut. | Core external identity + Procurement portal | BE-14, BE-16, CORE-12 |
| M9 — Accept and release a coherent ERP module | Run current-source normal-session end-to-end UAT, negative scope/SoD/retry/concurrency cases, migration/restore/rollback and monitored rollout; publish exact live operations. | QA + security + operations + product | AUD-05 |
| M10 — Evaluate optional platform extensions | Decide future generic custom modules, executable custom functions, metadata designers and self-service developer tooling separately; keep selected features in a governed backlog. | ERP platform product owner | CORE-06, CORE-07, CORE-10 |

## Definition of closure

For every selected operation: retain the supplied screen and shared ERP controls; identify current principal/action/company/branch scope; enforce state/version/source/capacity rules on the server; persist owned records, audit and outbox atomically where required; test success, denied, stale, duplicate/retry and failure/recovery; verify a real session against the same API/schema/UI build; then record the deployed capability and support/rollback evidence. An existing field, endpoint, screen or passing unrelated test does not close a workflow.

## Ownership stays explicit

Core owns identity, roles/data scope, organisation, shared approvals, documents, audit and common presentation. HRM owns workforce records; an employee link is optional for permitted Procurement actions. Procurement owns its commercial records and manual-payment evidence. Finance owns budget balances, AP/GL, bank execution and reconciliation; Inventory and Assets own stock/asset ledgers. Bounded application/domain/infrastructure projects can remain in the shared host. Renaming the historical HRM host is not a prerequisite for modularity.

## Preserve the original interface

Reuse current editors, tabs, lists, empty states, shared buttons and shell. Missing integration belongs behind the existing controls. Do not replace a rich supplied screen with a simplified parallel UI, and do not show a local preview action as a saved ERP transaction.
