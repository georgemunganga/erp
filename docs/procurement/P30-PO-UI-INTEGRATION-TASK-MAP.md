# P30 principal purchase order UI integration task map

Status: source-only handoff. The principal direct-order and governed-order APIs remain disabled until the P30 owning migration, finite-role checks, PostgreSQL rehearsal, and actual HTTP/browser acceptance pass. The imported ERP presentation remains the design baseline.

## Entry points and existing presentation

`reference-ui/src/app/App.tsx` already routes `/purchase-orders`, `/purchase-orders/new`, `/purchase-orders/:id`, and `/purchase-orders/:id/edit` through `LiveOrders.tsx`. Preserve its `LiveRecordDirectory`, transaction editor frame, record detail header/tabs, shared form controls, and empty/loading/error states. Do not route principal records through `LegacyOrder*`, `LiveOrderSourceEditor`'s worker contract, or `LiveOrderExecution`'s worker contract. Existing worker orders stay available under their current capability.

Add a typed, separate `src/platform/procurement-principal-orders-api.ts` and principal adapter components under `reference-ui/src/features`. `reference-ui/src/lib/procurement-live.ts` and `ProcurementLiveGate.tsx` need optional, default-false `principalDirectOrderDrafts` and governed-order capabilities. A current action grant, current company/branch scope, and the corresponding capability must all be present before offering a command. Refresh and scope changes must invalidate detail, form, and command-recovery state; late responses cannot restore old-context data.

## Screens, actors, actions, and exact routes

| Existing presentation | Principal workflow | Backend route under `/api/procurement/v1/orders` |
| --- | --- | --- |
| `LiveOrders.tsx` directory/empty state | Buyer lists own scoped principal orders; expose exact status and gross text, never worker Finance or delivery labels | `GET /principal-direct-drafts` |
| `LiveOrderSourceEditor.tsx` layout and source picker | Buyer selects approved P25 direct lines, supplier, quantities, prices, and makes an internal draft; preserve all editor fields and exact decimal strings | `GET /approved-principal-direct-sources`; `POST /from-approved-principal-direct-sources` with `draft` and `sources[{draftLineIndex,purchaseRequestId,purchaseRequestLineId,expectedRequestVersion}]` |
| `LiveOrders.tsx` detail tabs | Buyer reads source pins, lines, Core approval history, funding and release history; actions follow actual state | `GET /{id}/principal-direct-draft` |
| Detail action | Buyer cancels an **unfunded** draft; this releases its PR allocation | `POST /{id}/principal-direct-cancel` with expected version, original source digest, reason |
| Finance queue/detail action | Independent recorder reads exact retained source reservations and attests an external manual commitment using exact amount/currency and evidence. This is not a Finance ledger entry or balance. | `GET /principal-finance-work`; `GET /{id}/principal-finance-detail`; `POST /{id}/principal-funding` |
| Detail action | Buyer submits after funding; Core opens an independent PO approval case | `POST /{id}/principal-submit` with expected order version |
| `LiveOrderApprovalQueue.tsx` and `ApprovalDecisionDialog` | Assigned Core approver finds only current assigned tasks, reviews the frozen commercial and funding evidence, then approves, returns or rejects with exact case, assignment and order versions | `GET /principal-assigned-reviews`; `GET /{id}/principal-review/{assignmentId}`; `POST /{id}/principal-decisions/{assignmentId}` |
| Detail action | Authorized issuer freezes the internal issued version after completed Core approval; **no supplier message is sent** | `POST /{id}/principal-issue` with expected order version |
| Detail action | Independent operator records the supplier's actual response to that issued version, channel, supplier/evidence reference and observed time | `POST /{id}/principal-acknowledgment` with issuedVersionId and Accepted/Rejected/Unknown |
| Detail action | Buyer requests cancellation of a funded Draft/Returned/Rejected order; status becomes ReleasePending and allocation remains reserved | `POST /{id}/principal-release-request` |
| Finance queue/detail action | Independent recorder confirms an externally completed manual release against the release request and exact original reservations; only the confirmed command releases allocation | `POST /{id}/principal-release-confirmation` |
| All command forms | On uncertain outcomes, lock repeated submission and retrieve the same actor's recorded response by operation/key under current scope and original action grant; never use a new POST to guess | `GET /principal-direct-command-receipts/{operation}/{key}` |

P30 command inputs and statuses are defined by `ProcurementPrincipalOrderDraftRoutes.cs`. Command responses and recovery are P30-specific; the worker client `src/platform/procurement-order-execution-api.ts` has different endpoints and receipt shapes. Keep `Idempotency-Key`, server CAS versions, and original evidence digest intact. Do not use JavaScript `Number` for quantities or amounts.

## Purpose-only reads added in source

1. `GET /{id}/principal-finance-detail` returns exact retained source reservations, amount, funding reference and release request after current `procurement-po-finance-confirm` and branch checks. The endpoint refuses changed original pins. Never derive these values from a worker order or broad PR read.
2. `GET /principal-assigned-reviews` joins the current Core assignment, retained P30 assignment/submission and scoped order before count/paging, then checks the bounded rows' retained snapshot hashes. `LiveOrderApprovalQueue.tsx` currently calls the worker `/assigned-approvals` endpoint and needs the principal adapter.

Both reads are source-only and require the same P30 schema, finite-role, real HTTP and browser gates before any UI availability claim.

## Presentation and verification boundaries

Reuse the original order directory/editor/detail and shared `Button`, `Field`, `Input`, `Select`, `Switch`, `Modal`, and `ApprovalDecisionDialog`. Wording must distinguish “External commitment recorded,” “Internal version authorized,” and “Supplier response recorded.” The existing `LiveOrderExecution.tsx` labels “Funds reserved” and “Authorise sending” would misstate P30 evidence. A supplier acknowledgment is not delivery or receipt. `observedAt` and `confirmedAt` require explicit human evidence and server validation; do not infer a company business date from the browser clock.

Acceptance should cover workerless Core buyer, independent Finance recorder/approver/issuer/acknowledgment actor, foreign branch/company denials, exact decimal transport, return/reject and ReleasePending visibility, real source allocation conservation, context-switch abort, unknown-command GET-only recovery, reader-only screens, unavailable capability, and no supplier-send/Finance-payment claims. Keep the principal adapter held until the backend prerequisites and actual runtime tests are complete.
