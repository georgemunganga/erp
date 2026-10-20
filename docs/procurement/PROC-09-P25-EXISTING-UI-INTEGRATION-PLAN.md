# PROC-09 — Existing receiving UI integration plan

**Status:** Source audit and proposed adapter plan; no frontend implementation or runtime verification in this document.  
**Backend checkpoint:** `40c4c5a` plus review correction `e2049ab`.  
**Frontend audited:** `codex/procurement-final-ui-integration`, checkpoint `613ae83`.  
**Scope:** Reuse the existing Purchase Receives screens and ERP components for the bounded P25 goods/service slice.  
**Specification:** [28-section receiving adapter](PROC-09-P02-RECEIVING-PERSISTENCE-API-ADAPTER.md), [P25 implementation plan](PROC-09-P25-IMPLEMENTATION-PLAN.md).

## 1. Existing screens and reusable components

The imported screens already supply the intended navigation, shell, simple headings,
list layout, editor layout and detail presentation. Retain those layouts and the shared
ERP branding, typography, navigation, buttons, floating selectors, switches and tables.
The current Purchase Receives implementation is browser-local demonstration behaviour;
its data operations cannot become the live receiving adapter unchanged.

| Existing source | Reuse | Replace or connect for live P25 |
|---|---|---|
| `reference-ui/src/app/App.tsx` module routes | Existing `/purchase-receives` list/new/detail navigation | Dispatch live mode to a receiving adapter only after capability/acceptance gates; retain local reference mode separately |
| `components/layout/AppShell.tsx` | Purchase Receives sidebar and existing shortcut, company/branch selection | No new shell or navigation hierarchy |
| `features/RecordList.tsx` | StandardDataTable, list tools, active filter chips, simple title and empty-state composition | Server-owned receipt/service worklist, bounded page/count; local sort/filter/delete/export must not imply server operations |
| `features/TransactionEditor.tsx` | Source-first form region, line table, notes, sticky actions | Typed receipt/service drafts, exact IDs/decimal strings, immutable source description, separate record/inspect/accept actions |
| `components/data/LineEditor.tsx` | Table spacing, cell/input primitives, validation placement | Receiving exact-string variant; live rows keyed by PO-line ID, not item name; no catalog substitution or arbitrary account choice |
| `features/RecordDetail.tsx` | Header, overview/line sections, document/evidence region, contextual actions | Durable detail/version/source binding and controlled actions; remove local generic status mutation/delete from live path |
| Existing `LiveOrders` and platform API clients | Scoped request transport, uncertainty/recovery pattern, actor/context changes | Follow the established client structure; do not introduce another identity, company or permissions service |
| Existing `features/Settings.tsx` receiving preferences | Familiar settings presentation | Governed RECEIVING policy review/publication must be wired through existing policy editor/API; local switches never authorize acceptance |

The vendor transaction tab currently reads local Purchase Receives records. It requires a
later scoped vendor-history read contract; it must not display local counts as ERP history.

## 2. Field mapping and deliberate changes

| Existing UI field | Authoritative mapping / expected behaviour |
|---|---|
| Source Purchase Order | `purchaseOrderId` + `issuedVersionId`, selected from `/receiving/sources`; deep-link source is an initial selection hint, revalidated by server |
| Vendor | Derived from selected issued obligation. No live supplier substitution in a receipt; P25 picker currently lacks a supplier label projection, so add a scoped retained label read field before relying on this region |
| Purchase Receive number | Generated server `number`, read-only after save. Do not map editable local reference to server identity |
| Transaction title | No P25 title field. Omit from live draft or agree a specific field contract; do not silently combine it with notes |
| Date | `receivedAt` with explicit time and timezone. Current date-only value is insufficient for chronology; use existing date/time primitive and preserve milliseconds when reusing saved evidence |
| Receiving location | Explicit `siteId`, first slice equal to the issued PO branch/Core work location. Current generic receive editor has no location field; show the verified source location and avoid inventing a warehouse hierarchy |
| Item details / Unit / Account | Retained PO-line description/UOM and source coding, read-only; no item-name matching. P25 source projection exposes description/UOM but not full coding, which should be added only if needed for display |
| Received Qty | Exact decimal string, maximum four fractional digits and bounded precision; never JavaScript `Number` or step 0.01 for canonical value |
| Initial rejection | Exact string `rejectedQuantity` in draft, no greater than physical received; retained separately as `initiallyRejectedQuantity` after recording |
| Receiving Notes | `notes` |
| Delivery note | `deliveryNoteRef`, distinct from generated receipt number and structured evidence reference |
| Attach Files | Current UI stores filenames only. P25 has approved structured manual evidence references, not a receiving file-upload API. Do not claim a file is uploaded; connect Core document upload/access/versioning later |
| Service fields | Existing line/editor primitives plus explicit deliverable reference, service period and quantity; amount derived server-side from sealed net price, no client-created acceptance value |

The current receive submit handler blocks received quantity above the local PO quantity
and matches rows by item name. That rule must be replaced: P25 preserves truthful physical
overdelivery and controls **acceptance** against qualified/remaining authorization.
Never truncate physical received quantity to make acceptance pass.

## 3. Page workflows and endpoint map

All paths below are beneath `/api/procurement/v1`. Mutation commands require a stable UUID
`Idempotency-Key`. The current source returns HTTP 200 for successful receiving mutations.
Use API identifiers and exact strings throughout; source versions are separate from receipt
or service record versions.

| Screen/action | Endpoint | Outcome shown |
|---|---|---|
| List goods receipts / service confirmations | `GET /receiving/worklist?view=receipts|services&state&page&pageSize` | Scoped `items,total,page,pageSize`; only backend-supported filters |
| Choose delivered issued lines | `GET /receiving/sources?page&pageSize` | Sealed order/line IDs, exact quantities/rate, classification, source revision, explicit blocker |
| Save goods draft | `POST /receipts/drafts` / `PUT /receipts/{id}/draft` | Draft saved, no accepted stock or match entitlement |
| Read goods detail | `GET /receipts/{id}` | Physical, initial/final rejection, inspection, held and accepted quantities separately |
| Cancel own unrecorded draft | `POST /receipts/{id}/cancel-draft` | Retained Cancelled record with reason; no hard delete |
| Record goods arrival | `POST /receipts/{id}/record` | Physical observation only; goods may remain held/need inspection |
| Record final inspection | `POST /receipts/{id}/lines/{lineId}/inspect` | Qualified quantity and derived inspection rejects; named inspector/current site rules |
| Accept goods | `POST /receipts/{id}/lines/{lineId}/accept` | Positive accepted effect, exact source/PR/PO counters; insufficient/unknown history blocks |
| Save/edit service draft | `POST /service-acceptances/drafts` / `PUT /service-acceptances/{id}/draft` | Pending Certification; intended quantity is not accepted quantity |
| Service detail / cancellation | `GET /service-acceptances/{id}` / `POST /service-acceptances/{id}/cancel-draft` | Durable period/deliverable/evidence and controlled draft cancellation |
| Confirm delivered service | `POST /service-acceptances/{id}/certify` | Named current independent owner, approved site and exact sealed net basis; server-derived accepted value |
| Recover uncertain command response | `GET /orders/command-receipts/{operation}/{key}` | Historical committed response for current authorized target/actor; `replayAuthorized=false` does not authorize another write |

Goods flow:

`Save draft → Record arrival → Inspect when required → Accept qualified quantity`

Service flow:

`Save draft with period/deliverable → Confirm service completion`

Plain labels may be “Record arrival”, “Inspect delivery”, “Accept goods” and “Confirm
service”. Preserve separate concepts even if a single detail page contains these actions.
“Recorded” must not be presented as accepted, invoiced, stocked or paid. Fully qualified
acceptance may complete a receipt even when some original delivery quantity was rejected.
Show original received, initial rejection, inspection rejection, accepted and held quantities.

## 4. Required read-contract completion before live UI

These are real backend gaps, not reasons to invent frontend authority:

1. **Current action candidates/blockers.** Receiving detail currently supplies no server-derived
   allowed actions. Add a scoped read model for draft ownership, current named inspector/service
   owner, site and policy conditions, source revision and relevant blockers. These are UI hints;
   mutation guards still decide. Do not infer authority from role labels or local preferences.
2. **Purpose-only queues/read access.** P25 currently requires `procurement-receipt-view` for
   list/detail. It does not yet expose assigned inspector/service tasks for users without broad
   receipt read. Decide the bounded purpose-scoped read contract rather than silently granting
   broad view permission. Existing actual fixture grants explicit scoped read; that is not a
   production default.
3. **Verified capacity/history projection.** Source picker reports `RequiresCommandVerification`;
   it is not authoritative remaining acceptance. Supply checked source/qualified/remaining
   capacity for actionable previews under the same ledger proof. Do not subtract browser rows
   or interpret missing facts as zero. Historical malformed/legacy acceptance must be visible
   as needing reconciliation, not a fresh empty ledger.
4. **Source labels/location.** Reuse current Core company/branch lookup where authorized; add
   narrow retained supplier/order/source labels as needed. No duplicate supplier/Core masters.
5. **Evidence history.** Detail exposes inspection/certificate IDs and facts but no complete
   accepted-effect timeline. Add purpose-scoped immutable effect history for decision review;
   retain references instead of creating another audit store.
6. **Documents/import/export/search.** No receipt document upload, server export/import, bulk
   actions or rich date/vendor/site search exist in this slice. Keep those actions unavailable
   with a useful explanation until their real contracts exist.

The existing PO “Create receive” action must use delivered issued source IDs, not local
record IDs alone. The receive “Create bill” link may later prefill intake context; it cannot
claim matching or AP eligibility. P25 explicitly returns `matchingAuthorityAvailable=false`.
Returns, reversals, replacements and negative acceptance remain unavailable pending a
trusted consumption/owner adapter. Inventory/Assets retain their own registers and balances.

## 5. UI states, permissions and failure recovery

- **Loading:** reuse the global shell/preloader and scoped table/form loading states. Abort
  stale requests when company/branch changes; clear selected source/revisions and isolate
  unsaved drafts by scope. Never carry another company's cached row into a new command.
- **Empty:** centered receiving icon, plain title (“No deliveries recorded yet”), short help,
  and Add action only when current draft capability/action permits. No Imports button until
  a real import API exists. An empty list is not an error or proof of zero historical acceptance.
- **Missing setup:** show the actual blocker and a permission-aware link to existing Procurement
  policy configuration; do not offer a switch that fabricates published classification.
- **Denied/not found (403/404):** plain scoped access message, no foreign record identifiers or
  stale details. A former actor must not recover a response after current permission/scope loss.
- **Validation (400/422):** keep entered exact text and field help. Invalid quantity/UUID or
  nonrepresentable decimal has no successful effect; do not round to make it fit.
- **Conflict (409):** reload record and source revision, explain changed evidence/access/capacity;
  require explicit review before a new intent. Never automatically repost changed payload.
- **Unavailable (503):** keep draft, show retry/help and no success status. A hidden route or
  visible UI button does not establish backend readiness.
- **Unknown write outcome:** retain the original UUID, command fingerprint/payload and scope;
  recover via the existing saved receipt path. Do not silently allocate a new UUID and repeat
  acceptance. Historical response and present action eligibility are distinct.
- **Success:** use the returned durable version and exact values; re-fetch dependent source/
  receipt views. Audit/outbox persistence does not imply notification delivery or owner posting.

Use shared switches for booleans, floating dropdowns for scoped choices and simple unboxed
page headings. Quantity formatting may be localized for display, while typed API quantities
remain exact invariant strings. Accessibility must cover labels, keyboard actions, focus,
validation announcements and narrow screens without losing source/evidence context.

## 6. Bounded implementation and acceptance order

1. Centralize backend source/model/HTTP/PostgreSQL verification and P25 migration/recovery
   review. Keep default-off flags and legacy execution-version compatibility.
2. Complete the narrow read/action/capacity contracts above, preserving current permissions
   and Core branch fence. Verify no purpose-only access broadening or data leakage.
3. Wire existing list/editor/detail using a typed platform client and existing primitives;
   no navigation/shell redesign. Integrate goods draft/record first, then inspection/accept,
   then service draft/certification. Do not replace unrelated module numeric editors.
4. Add focused UI tests for exact strings, overdelivery truth, withheld acceptance, denied actions,
   legacy unverified records, current context switches and lost-response recovery.
5. Run actual browser → real mapped backend → disposable restricted Postgres flows for goods
   and service with independent synthetic actors. Verify accepted effect/counters/audit/outbox
   and rollback; negative/replay/current-site cases remain mandatory. Mocked UI tests alone
   do not prove integration.
6. Enable the existing live receiving route only after these gates and authorized deployment.
   The old broad `receiving=false` remains until a compatible adapter is verified; granular
   capabilities do not silently permit local demonstration data in live mode.

**Evidence:** Source inspection only. No UI files, runtime configuration, databases or deployed
assets changed for this plan. No tests or browser/server/build jobs were run by this task.
