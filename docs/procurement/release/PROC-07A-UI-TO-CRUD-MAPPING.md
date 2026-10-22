# Recurring purchases: imported UI to bounded CRUD

Status: source audit and implementation plan, not runtime completion. Backend candidate starts at
integrated P23 `dbf5107`; earlier recurring source `c4f65f2` is reused. The existing 28-section
[PROC-07A](../PROC-07A-RECURRING-PURCHASE-PLANS.md) defines this planning-only slice.

## Visible surfaces and ownership

| Existing UI source | Current behavior | Integration treatment |
|---|---|---|
| `reference-ui/src/features/CatalogDetail.tsx` recurring-bills columns | Browser-local generic transaction rows: date, number, vendor, amount, frequency, status | Reuse shared listing/table/empty-state patterns with real own-record recurring API data. Label as Recurring purchases. |
| `reference-ui/src/features/TransactionEditor.tsx` recurring branch | Generic vendor, date, lines, taxes/discounts, notes, frequency, end date; browser-local draft/save | Existing prepared planning editor is `/home/amizo/procurement-recurring-ui` commit `cd3802b`. Align it with current Core-principal contract and reuse shared controls, rather than build another editor. |
| `reference-ui/src/features/RecordDetail.tsx` recurring branch | Generic amounts/payment/source and frequency/end metadata | Show authoritative saved schedule, exact estimated amount, supplier/optional contract and reasoned close. No derived payable balance. |
| `src/routes/procurement.payables.recurring.tsx` | Older contract-backed read view | Not a second source of record; canonical live workspace must use the recurring-purchases adapter. |

## Exact first-slice contract

| Input / saved field | Validation / source |
|---|---|
| `supplierId` | Required nonzero UUID; same company supplier, own draft or approved active/orderable. |
| `supplierContractId` | Optional UUID; same company, same supplier, original actor-owned, not cancelled. Reference is not release authority. |
| `title` | Required trimmed text, maximum 300 characters. |
| `frequency` | Explicit Weekly, Monthly, Quarterly or Yearly. No automatic schedule execution. |
| `firstCycleOn`, `nextCycleOn`, `lastCycleOn` | ISO calendar dates; required first/next, optional last; first <= next <= last when present. |
| `expectedAmount` | Positive exact decimal string, numeric(18,4), no rounding. Response supplies `exactExpectedAmount`. |
| `currencyCode` | Defaults from selected Core company on creation; preserved if omitted on edit; three ASCII letters, normalized uppercase. |
| `ownerPrincipalId`, `ownerWorkerId`, `workLocationId`, creator | Server-owned current Core principal, optional genuine HR projection and authorized scope. Client ownership cannot transfer records. |
| `number`, ID, timestamps, `version` | Server-owned; version1 at creation, exactly +1 for accepted edits including no-op, and close. |
| `status`, `closedReason`, `closedAt` | Draft on create; Closed only through current-version close with required reason <=1000. Closed is read-only. No physical delete. |

Actions: GET/POST `/api/procurement/v1/recurring-purchases`, GET/PUT `/{id}`, POST `/{id}/close`.
Permissions are current stored `procurement-recurring-read/create/edit`, plus module access. Server
filters tenant/company, original principal and selected authorized branch before operations.
Department-only scope is unsupported and remains closed rather than widened. Shared immutable
audit records create/edit/close in the same save. Referenced contracts cannot change supplier even
after plan closure; both writers use serializable transactions. No separate supplier, identity,
currency, documents, approval engine or accounting model is introduced.

## Limits requiring an owning contract

Generic line tax, discount, payable balances, payment terms, recurring bill generation, pause/resume
execution, approval and Finance handoff are not represented by this schedule DTO. Retain those
feature requirements for their owning milestones; never silently discard their entered values into
a planning save or present a local record as persisted. Finance owns liabilities, recurring payable
schedules, credits, posting and payment. This API sends no Finance command and reserves no budget.

Existing import/export controls may be reused only after an explicit recurring column map, exact
amount/date validation, per-row permission and error/reconciliation contract is implemented. No
backend bulk-import endpoint is currently supplied. Do not expose a fake working import action.

## Acceptance still pending

Serialize SDK gates after the active P23 job; run mapped/domain tests, genuine P24 migration and
provider constraints, used/unused rollback and contract-reparent race proof. Integrate the existing
UI with principal-owned journals and strict receipts, then run employee-free CRUD, cross-owner/
branch denial, unknown-write/reload, closed-history and no-Finance-effect Playwright checks against
the actual isolated API. No migration, build, browser, deployment or full feature success is claimed
by this source checkpoint.
