# PROC-08/P33 Principal PO Draft Revisions and Issued Amendments

**Status:** Source design for review; no endpoint, migration, permission, or capability is enabled by this document. **Date:** 23 October 2026. **Parent:** [PROC-08](PROC-08-PURCHASE-ORDERS-AND-AMENDMENTS.md).

## 1. Feature Identification

Procurement → Purchasing → Principal Purchase Order → Revise internal draft / propose issued amendment. This extends the P30 direct, source controlled PO and preserves the worker PO flow.

## 2. Purpose and Business Outcome

Let a Core authorized buyer correct a linked PO without losing its original approved demand, reservation, independent approval, issued version, or audit chain. Every changed commercial term has an exact before and after record.

## 3. Scope and Exclusions

Stage A is an **unfunded Draft** edit. Stage B is an **issued amendment proposal and independent approval**; it cannot become a normal in-place update. Neither stage executes a Finance transfer, dispatches to a supplier, changes Inventory, or treats PR approval as PO approval.

## 4. Parent and Related Features

Consumes P25 approved demand and Finance checker evidence, P30 original direct-source bindings, supplier eligibility, catalog commercial rules, PO policy and Core case authority. Receiving and invoice validation consume immutable issued versions.

## 5. Actors and Participants

Original buyer, current independent Finance recorder, policy assigned PO approver, issue actor, supplier acknowledgment recorder, auditor. A worker record is optional for the principal flow; a known worker alias remains a denial-only separation check.

## 6. Entry Points and Triggers

The existing five-tab PO editor opens an owned draft by ID. An issued PO exposes a separate “Propose amendment” action when actual authority and the amendment capability are available. A supplier's requested change is evidence, never direct mutation.

## 7. Preconditions

For Draft edit: status Draft, revision 0, no P30 funding or submission, exact current version, current buyer/edit action and branch, verified original source pin and complete active allocation/binding graph. For issued amendment: exact current issued revision, fulfillment and invoice quantities, valid source/funding and policy/authority route, no unresolved prior amendment.

## 8. Workflow

Draft: read current verified record → propose full draft and reason → lock PRs in stable order and PO → recheck current source/supplier/catalog and old graph → freeze the verified original commercial/source state if this is the first edit → compute exact delta → resize existing allocations atomically → append full proposed revision, receipt, audit and outbox → return current version. Issued: propose numbered delta against immutable issued version → independent financial adjustment and policy approval → freeze a new issued version → separately record manual supplier outcome.

## 9. Status and State Transitions

Stage A remains Draft with monotonically numbered draft revisions; cancellation releases the current reserved amount while preserving all revisions. Stage B: Issued → Change Pending → Amendment Approval → Issue Pending → Issued revision N+1, or Returned/Rejected with old issued revision still authoritative. Receipt and invoice progress remain separate.

## 10. Permissions and Data Scope

Draft edit requires current `procurement-po-edit`, original buyer, company and actual Core branch. Amendment proposal and approval need distinct, currently authorized Core actors; existing PO approval grants do not gain amount or amendment authority by implication. All counts, reads, receipt replay and related lists obey the same record scope.

## 11. Business Rules

`P33-001` original P30 source SHA never changes. `002` revision/receipt history is append only. `003` every source quantity delta preserves the PR aggregate and allocation ledger. `004` increased amount requires current funding and configured PO authority; no implicit tolerance. `005` a material issued change is independently approved and reissued. `006` already accepted/invoiced quantity cannot be reduced below its actual floor. `007` unconfirmed Finance release or adjustment blocks final transition. `008` no existing issued version is overwritten.

## 12. Data Fields and Ownership

Procurement owns revision ID/number, parent PO, original source SHA, full before/after commercial snapshots and digests, line/source/allocation references, exact amounts, reason, author, time, status and Core case links. Core owns identity and approval decisions. Finance owns actual commitment; Procurement stores a verified external reference and state only.

## 13. Screens and Data Views

Reuse the imported PO editor sections and five-tab detail. Show an exact revision comparison, current approved version, source line allocations and pending amendment. Missing backend actions stay visibly unavailable, without routing schema-2 records into worker or unlinked draft CRUD.

## 14. User Experience and Human Behaviour

Display the changed fields and amount/quantity delta before save. Explain when an external Finance adjustment or new approval is required. An unknown write shows the original command key and read-only recovery instead of offering a second save.

## 15. Notifications and Communications

Shared workflow may notify assigned internal approvers after an actual case opens. Supplier notification is recorded only after a real dispatch integration; a manual observation must be labeled as such.

## 16. Documents and Attachments

Issued version references an immutable approved document and checksum. An amendment document must be a new version. A plain unverified attachment ID is not evidence of supplier consent or Finance action.

## 17. Exceptions and Edge Cases

Concurrent source reservation; changed method, supplier or catalog rate; inactive supplier; stale PO/PR/CAS; partial receipt/invoice; returned Core case; missing original revision; mismatched stored receipt; interrupted response; currency/UOM change; branch/account revocation; legacy worker PO accidentally targeted by a principal route.

## 18. Integrations, APIs, Events and Sandbox

Stage A needs dedicated principal edit and actor-owned `Edit` command receipt endpoints behind a distinct default-off capability. Stage B needs separate proposal/decision/issue endpoints and Finance adjustment evidence. Emit internal revision facts only after the owning transaction commits. No generic `PUT /orders/{id}` routing for schema 2.

## 19. Audit, Security and Privacy

Retain original Create fingerprint, first-touch original snapshot, each revision digest, actor, reason, source delta, command key and authorization evidence. Do not log supplier sensitive records, cookies, or request bodies. Database constraints/privileges must prevent an application role from rewriting prior revisions.

## 20. Reports, Dashboards and Metrics

Count draft edits, issued amendments, cycle time, approval returns and value deltas separately. A proposed amount is not committed spend; Finance's actual balance remains authoritative.

## 21. Configuration and Localization

Use effective PO policy and configured materiality, amount limit and currency rules. No hard-coded tolerance or exchange conversion. Store exact normalized decimals and UTC evidence; display company locale/zone separately.

## 22. Non-Functional Requirements

Serializable source/PO locking, bounded line count, exact decimal arithmetic, CAS and idempotency, append-only proof, scoped error messages and receipt recovery. Reads must verify retained original and current revision graphs without fetching cross-branch records into a page.

## 23. Postconditions

After Draft edit, one current draft revision and current allocations reconcile to PR balances; original source pin and earlier revisions are unchanged. After approved amendment, exactly one new issued version becomes current and the prior issued version remains readable.

## 24. Failure and Recovery

Any failed source, Finance, Core or database check rolls back all deltas and evidence. Unknown network results are recovered by the original actor/action/key under current scope. Missing first-touch origin proof or incomplete historical revision is a reconciliation error, never silently invented history.

## 25. Acceptance Criteria

An authorized workerless buyer can edit an unfunded principal Draft and reload its full revision history. A second client using stale version or key/payload mismatch cannot overwrite it. Increased source quantity cannot exceed approved demand or funding. An issued amendment cannot affect its original issued record before its own independent approval.

## 26. Test Scenarios

First-touch verified original capture; repeated edits; shrinking/growing reservations; exact decimal boundary; source tamper; supplier change; funded edit denial; stale CAS; same-key replay and unknown write; branch/alias revocation; receiving/invoice floor; approved/rejected amendment; no supplier send or Finance ledger mutation. Use mapped tests plus genuine PostgreSQL/normal-cookie proof before enabling.

## 27. Ownership and Dependencies

Procurement owns PO revision and reservation records. Core owns person, roles, branch and case. Finance owns commitment and adjustment. Receiving/Invoice own downstream facts. Root owns generated migration, finite privileges, readiness and release gates.

## 28. Open Decisions and Assumptions

Confirm who may propose an issued amendment, which changes are material, source increases allowed after issue, mandatory external Finance adjustment evidence and supplier reacknowledgment, and handling of active receipt/invoice commitments. Stage B cannot be activated before those policies and downstream version contracts exist.

## Existing source audit and implementation boundary

- Worker source-backed `PUT /orders/{id}/source-draft` already uses `PurchaseOrderSourceAllocation.ResizeReservation` and catalog/source checks. It is **not** a schema-2 route; generic `PUT /orders/{id}` explicitly rejects schema 2.
- P30 principal routes expose Create/list/detail/cancel and governed funding/approval/issue/acknowledgment, but no Edit or amendment endpoint. The principal editor currently displays a status message for an `/edit` ID rather than loading editable fields.
- `PrincipalOrderDraftPins.Digest` currently hashes mutable PO header and line values against the original source binding. Therefore an in-place line/header edit would invalidate every existing principal read, funding, issued and receiving verifier. The original SHA must remain immutable while a separate current revision becomes authoritative. P30 first creation did not retain a full commercial Create body; a first edit can capture the original state only after the present hash and all source/binding/allocation facts verify.
- `PrincipalOrderCommercialSnapshot.Capture` and the P30 governance/issued/receiving paths currently require commercial revision 1. `PurchaseOrderRevision` has before/after JSON but no principal source pins, command receipt, current Core decision or Finance adjustment binding. It cannot be treated as approved schema-2 amendment proof without a versioned contract.
- The bounded first source checkpoint is **unfunded Draft revisions only**. Issued amendments need an explicit new issued-version graph, Finance adjustment owner, downstream receiving/invoice compatibility and a separate approval cycle. Neither path is ready to register or enable.
