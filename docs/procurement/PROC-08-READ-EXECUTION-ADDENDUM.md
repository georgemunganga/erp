# PROC-08 — Execution Worklists, Detail and Command Recovery Addendum

**Status:** Source-authored proposal; not compiled, tested, enabled or deployed.
**Base:** `6225867`; additive to the initial commercial execution contract.
**Tracking:** [module-connect#5](https://github.com/georgemunganga/module-connect/issues/5).
**Hierarchy:** Procurement → Purchasing → Commercial Execution Visibility → Discover / Inspect / Recover.
**Related:** [initial commercial execution](PROC-08-P01-COMMERCIAL-EXECUTION-CONTRACT.md).

## 1. Feature Identification

`PROC-08-READ` supplies the smallest missing API surface for the existing LiveOrders UI. It adds scoped operational worklists, complete execution detail and historical command-outcome recovery. It does not expand buyer draft CRUD or the existing commercial-history endpoint.

## 2. Purpose & Business Outcome

Finance can discover an order and see the exact amount, revision and source reservation references required to confirm funding. A dispatch verifier can discover the sealed version and record a real delivery result. Buyers and assigned approvers can inspect the same execution detail without broadening ownership.

## 3. Scope & Exclusions

Includes source-controlled PO read projections and action-form candidates. Excludes payment/AP ledgers, new Finance balances, bank data, automatic approval, dispatch simulation, arbitrary staff access, runtime enablement and UI redesign. Standard draft CRUD remains buyer-only. Existing command guards remain authoritative.

## 4. Parent & Related Features

Consumes Procurement orders, source allocations, retained PR/PO identities, Finance evidence/resolutions, issued versions, delivery observations and commercial command receipts. Core owns authentication, current roles, branches, approval cases and decisions. Core's existing transaction/fence boundary governs current authority reads.

## 5. Actors & Participants

Buyer, assigned PO approver, Finance confirmer and dispatch verifier. A manager's generic PO-read action alone does not imply access to every order. An action grant is separate from tenant/company/branch scope and from maker/checker eligibility.

## 6. Entry Points & Triggers

| Read endpoint | Use |
|---|---|
| `GET /api/procurement/v1/orders/execution-worklist?view=buyer\|finance\|dispatch` | Discover work in the selected purpose; optional exact `status`, `page`, `pageSize` |
| `GET /api/procurement/v1/orders/{id}/execution-detail` | Inspect current order, evidence, assignment versions and action candidates |
| `GET /api/procurement/v1/orders/command-receipts/{operation}/{key}?orderId={optional}` | Recover the current actor's recorded historical result after an uncertain response |
| Existing `/orders/assigned-approvals` | Approver's active Core queue; remains the approval discovery route |

## 7. Preconditions

Commercial execution remains explicitly enabled/configured and schema-ready. The current account must resolve to an active canonical worker in this company. Current role actions and current branch assignments are read under the shared Core authorization fence, including selected branch context for organisation-wide users.

## 8. Workflow

1. Choose the buyer, Finance or dispatch worklist; retain the ERP-selected company/branch.
2. Server filters tenant/company, current branch context, source-controlled order population and purpose-specific visibility **before** total count and pagination.
3. Open execution detail; receive exact source IDs, current versions and decimal strings.
4. Open an action form only from a candidate whose `canStart` is true. `requiresCommandValidation` is always true: the command must recheck full authority, policy, source, supplier, Finance, version and amount.
5. Persist a pending client intent containing operation, path, actor/company context, original payload and one UUID idempotency key before submitting.
6. On uncertain response, read that exact operation/key receipt. If recorded, display its historical result plus current order status/version; refresh execution detail. Do not represent a historical receipt as new replay authorization.
7. A missing receipt is only absence at the read snapshot. The original request may still commit. Retain the original key and exact payload for retry; never issue a fresh key merely because a timeout or 404 occurred.

## 9. Status / State Transitions

Reads make no state transition. Worklists reflect current status; action candidates suggest forms only. Finance holds remain held until real resolution evidence exists, including expired confirmations. `IssuePending` remains different from verified `Issued`. Historical recovery returns `RecordedHistoricalOutcome` with `replayAuthorized: false`.

## 10. Permissions & Data Scope

All new endpoints require Procurement access, canonical identity and current tenant/company/branch scope. Detail uses these explicit read implications:

| Participant | Current action / resource condition |
|---|---|
| Buyer | `procurement-po-read`, own source-controlled order |
| Approver | `procurement-po-approve`, assigned to a Core PO case for that order; assignment needed for detail, active pending task needed for decision candidate |
| Finance | `procurement-po-finance-confirm`, Draft/Returned/Rejected source-controlled order, or a confirmation previously recorded by this same principal |
| Dispatch | `procurement-po-dispatch-confirm`, an issued version exists and order is IssuePending/Issued, or this same principal previously recorded its delivery evidence |

The Finance/dispatch command capability implies only this purpose-specific read. It does not grant global PO-read, ownership, another module's data or command eligibility. A conflicted worker may inspect a scoped purpose record; maker/checker action candidates remain blocked. Revoking the current purpose action removes this access unless another explicit permitted read relationship exists.

Receipt recovery additionally requires the exact operation's **current mutation permission**, the same actor principal that recorded the receipt, optional target match, and current target record scope. It does not substitute generic PO-read permission for the original operation.

## 11. Business Rules

- Filter resource/purpose scope before count/page; do not expose inaccessible counts or consume page slots then filter rows.
- New worklists contain only orders with active lines all linked to source PR line IDs. Legacy unsupported drafts retain their existing endpoints.
- Current canonical worker identities govern independence. Retained participant hashes support historical offboarding without resolving original actors through today's memberships.
- Source edits/cancellation remain blocked by any unreleased commitment; expiry is not release.
- Approval and issue action candidates never create or imply a Core approval or finite spending authority.
- All new quantity, unit rate, tax and money fields are decimal **strings**. JavaScript must retain string values; formatting must not use a Number roundtrip for mutation payloads.
- SQL `numeric(38,8)` is wider than `System.Decimal`. Shared `ExactDecimalArithmetic` rejects products/aggregates that cannot be represented exactly; the API must not silently round or claim all 38 digits fit. Each product must be representable; aggregate sums use the shared scaled-integer sum.

## 12. Data Fields & Ownership

No new tables. Procurement owns the derived projection and commercial evidence; Core owns identity and approval execution. Detail exposes order/line IDs and references, source reservation references, next commercial revision, Finance status/evidence/resolutions, submissions, cases/stages/assignments/decisions, issued versions and delivery observations. Bank credentials, bank details and unrelated identity records are not projected.

`ExactDecimalArithmetic.cs` is restored byte-for-byte from reviewed shared primitive commit `7b46e05`; no receiving source files are imported. This same helper is reused by the independently reviewed PO arithmetic increment.

## 13. Screens & Data Views

Reuse the existing LiveOrders list/detail layout. Finance and dispatch add existing-layout worklist views; assigned approvals reuse the existing queue. Detail sections may use tabs for Order, Funding, Approvals and Delivery. A funding form receives exact gross, currency, next revision, prior commitment/replacement reference and source reservation references directly from the read contract.

## 14. User Experience / Human Behaviour

Use plain labels: “Confirm funding”, “Send order”, “Confirm delivery”, “Release funding”, “Waiting for approval”. Explain known blockers. A `canStart` flag enables a form, not an unconditional operation. Show uncertain request recovery without telling users to click repeatedly or create another financial transaction.

## 15. Notifications & Communications

These reads send no messages or external communications. Existing command/outbox semantics remain unchanged. No network timeout becomes a supplier dispatch or payment notification.

## 16. Documents & Attachments

Expose authorized evidence references already owned by Procurement/Core. Resolving or downloading attachments continues through Core document authorization; a string reference in this projection does not grant download access.

## 17. Exceptions & Edge Cases

Missing policy/identity/retained evidence blocks action candidates. Not found/denied targets reveal no receipt or source details. Unknown receipt remains uncertain, not failed. Historical response versions can be older than the current order. Wrong operation casing, empty UUID, mismatched actor/target and current permission/scope revocation deny recovery.

## 18. Integrations / APIs / Events / Sandbox

New read contracts are additive and remain behind the existing commercial execution option/schema gate. Existing numeric draft DTOs and approved-source fields remain compatible; approved sources add `availableQuantityExact` and `approvedUnitPriceExact`. New execution detail's order line quantity/rate/tax/counter fields and totals are strings. Source-authored actual PostgreSQL HTTP probe cases send decimal-string quantity/rate/tax and an eight-decimal Finance gross; their execution is still required to prove binding/persistence.

## 19. Audit / Security / Privacy

No business fact, approval, Finance posting or audit transition is created by reading. Operational HTTP logging remains separate. Each request resolves current Core authority; frontend hiding is not authorization. Receipt response content is actor-owned historical business evidence, never a fresh approval. Read projections do not include bank fields or unrestricted commercial snapshot JSON.

## 20. Reports / Dashboards / Metrics

Worklist totals count only the selected purpose's scoped orders before pagination. These are operational backlog views, not spend totals or Finance ledger reconciliation. History can show prior confirmations/delivery outcomes without describing them as current balances.

## 21. Configuration & Localization

Uses existing company currency, approval policies, approved manual Finance policies and configured dispatch channels. No amounts, monetary thresholds or extra default grants are invented. Status/blocker/action codes are stable; frontend labels can be localized.

## 22. Non-Functional Requirements

Worklists have bounded pagination and perform database filtering/counts before presentation. Expensive evidence/authority hints are loaded only in detail, avoiding per-row deep loading in worklist pages. Detail uses a consistent transaction snapshot. Future performance targets follow measured scale; no unverified latency claim is made.

## 23. Postconditions

Only authorized derived data is returned. No state or hold changes. A recovered historical receipt describes an already committed operation, while current order status/version and replay authorization remain separate.

## 24. Failure & Recovery

Schema/feature unavailable returns unavailable; scope/identity failures deny; unknown target/receipt is not found. Preserve pending client intent through refresh when feasible. Recovery 404 is not a guarantee that an in-flight original command failed. Retry the exact original command/key and let its authoritative fingerprint/current guards resolve the outcome.

## 25. Acceptance Criteria

Default Finance with only its current purpose action can discover/read a scoped draft without global PO-read. An independent nonassigned dispatch verifier can discover the sealed version and configured channels. Other branches, tenants, unassigned staff and wrong-purpose actors cannot read these records/counts. Exact values survive browser transport and API/persistence. Historical recovery is actor/operation/key/target scoped, denies after current operation permission revocation and never claims replay authorization.

## 26. Test Scenarios

Authored pure tests cover current identity/tenant/company/selected-branch scope, purpose/assignment/ownership relationships, policy/hold/independence candidates, exact above-JS-precision values and unrepresentable product denial. Authored `--orders` HTTP probe cases cover Finance without broad read, unissued dispatch denial, dispatch discovery/detail, real string-bound mutation values/SQL roundtrip, wrong operation/actor/target recovery and revoked current operation permission. Root-controlled compile/test/actual PG/browser gates remain outstanding; no passing results are claimed.

## 27. Ownership / Dependencies

Procurement owns read projections and UI adaptation. Core owns current identity, roles/branch scope and approval execution. Finance owns commitment authority and resolution evidence. Depends on verified initial PO execution schema/runtime permissions, shared exact decimal primitive, actual mapped HTTP proof, existing LiveOrders UI integration and Playwright. No new migration is proposed here.

## 28. Open Decisions / Assumptions

Purpose-specific read implications are the explicit technical contract for this increment; a future separate audit/read role may require a new policy. UI action candidates remain conservative and nonauthoritative. Existing legacy numeric DTO behavior is unchanged. Actual decimal-string binding, restricted-runtime reads, error UX, performance and browser workflows must be verified before activation. No full workflow/deployment completion is claimed.
