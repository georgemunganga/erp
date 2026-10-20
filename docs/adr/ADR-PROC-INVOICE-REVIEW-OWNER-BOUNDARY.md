# ADR — Separate original-unit review authority from matching and Finance history

Status: Proposed, 20 October 2026. Related specification: `docs/procurement/PROC-11-ORIGINAL-UNIT-REVIEW-AND-EXPOSURE-OWNER.md`. No implementation/activation accompanies this record.

## Context

P26 production uses unavailable original-unit review and complete-history owners. Core documents prove stored content/version/hash/scan status, not commercial line meaning. Existing Finance references and payroll integration operations do not own complete Procurement AP exposure. Current review envelope conflates historical reviewer with current comparison caller, so it cannot simply be reused for independent persisted review.

## Proposed decision

Implement a bounded governed Procurement original-document/unit review using the existing Core approval/document/access services. Retain independent reviewer authority and exact original/mapping/document/policy pins, separately check the current matcher identity and scope. Keep immutable proposal/review history; choose the smallest relational representation after review of existing mapping evidence fields. Do not copy a historic approval into a new caller's identity.

Keep the Finance exposure owner unavailable until an actual authenticated versioned complete-history/cutover/lease/revocation/reconciliation contract is supplied. Local absence, funding evidence and operator CSV reconciliation cannot replace completeness. A review outcome does not approve an invoice, create AP liability, qualify a capacity claim or execute payment.

## Alternatives and consequences

Auto-verifying submitted units/scan status would fabricate authority. Treating local empty claims as complete history risks duplicate consumption. Copying a payroll integration service duplicates the wrong domain and grants irrelevant access. Building a second AP ledger is outside Procurement ownership. Independent unit review adds a useful real capability now, while truthful blocked Finance status remains until its genuine owner exists. Source, security, model/migration and actual API/PG/UI gates remain mandatory before release.

## Step 2 representation and adapter decision — proposed source design

Inspected integration `3613559` and reviewed contract `cadaffb`. Choose a **separate immutable original-unit review source binding**, with Core as execution owner. Do not create a successor `InvoiceMatchMappingVersion` merely to attach verification.

Evidence behind the decision:

- `InvoiceMappingEvidence.Create` fixes mapping revision, complete original-line set, original SHA, proposal maker principal/worker and proposed document-line/unit references. `Verify` binds all of these to the original invoice and mapping digest.
- `ProcurementInvoiceMatchingMapping.GuardInvoiceMatchingExecution` rejects every mapping update/delete (`InvoiceMappingAppendOnly`). A successor would change revision/digest and its `Actor*` proposal maker, invalidating the exact v2 envelope or requiring the reviewer to masquerade as the original maker. Existing comparison/results/claims reference exact mapping identity.
- Core already owns unique `(tenant, company, sourceModule, sourceType, sourceId, sourceVersion)` cases, assignment CAS, quorum execution, append-only decisions and exact decision replay. Procurement should retain source/worker/policy evidence and terminal outcome, not copy Core state transitions.
- Existing matching `IInvoiceMappingEvidenceReviewer`/`InvoiceMappingEvidence.Project` accepts caller-bound schema-1 evidence. A retained schema-2 outcome must use a typed v2 reader/projection path; translating its signer into the current matcher would discard the separation established in `cadaffb`.

### Proposed records, with ownership

| Record | Minimal responsibility / constraints |
|---|---|
| `InvoiceUnitReviewSource` | Immutable review ID and source version 1; exact invoice/mapping/original/issued IDs and SHAs, retained branch, invoice version at opening, complete line/document-version/hash/reference/unit set, original mapping maker worker/principal, opening actor, matching and review policy IDs/version/rules SHAs, Core case ID, canonical bounded source JSON/SHA. A later attempt receives a new review ID; the rejected source is never overwritten. |
| `InvoiceUnitReviewAssignmentEvidence` | Immutable source/case/stage/assignment tuple, named principal and worker, exact published review-policy digest and stage definition. One row per actual Core assignment; no role-label identity, spending-limit substitution or copied user master. |
| `InvoiceUnitReviewOutcome` | At most one terminal result per source and Core case. Approved outcome contains independently verified schema-2 envelope, final Core decision ID, completion key/time and SHA. Rejected/Returned outcomes retain their actual decision/reason and never contain verified unit authority. No editable `Verified` boolean. |
| `InvoiceUnitReviewCommandReceipt` | Real review source target, operation, canonical actor+worker, UUID key, stable wire fingerprint, original HTTP status/response bytes. Scoped uniqueness by operation/key; changed body/actor denies replay. No fake PO FK or reuse of PO discovery permissions. |

No mutable pointer on the invoice is initially needed. Read outcomes by exact current mapping identity. Reject more than one eligible successful review as ambiguous rather than select an arbitrary latest timestamp. Permit at most one review attempt for a given mapping, matching-policy ID and review-policy ID (including rejected/returned attempts); corrected proposals or a genuinely new governed policy produce fresh pins. This avoids concurrent duplicate attempts and retrospective re-review under the same authority. Business acceptance of that bounded first-release retry rule remains a DoR item.

All records use stable tenant/company/ID identity, restricted deletion and append-only guards; normalized scoped FKs bind source→mapping/invoice/issued source and assignment/outcome→source/case. If Core-schema FKs cannot be represented without cross-owner migration coupling, retain explicit transaction-time exact tuple verification and centrally reviewed literal scoped constraints; do not pretend a JSON hash supplies provenance. New alternate keys on existing tables require explicit short physical names and Npgsql verification; mutable statuses, completion fields and financial/source counters must not become EF keys. Do not edit retained P24–P26 migrations or snapshots. Future Down must lock affected tables and veto any review source, assignment, outcome, receipt, corresponding audit/outbox or Core source case before dropping evidence.

### Existing transaction adapter, extended by composition

Use `ProcurementApprovalTransaction.ExecuteAsync` unchanged: fresh owned contexts, existing Core branch and catalog session fences before SERIALIZABLE snapshot, one physical connection, whole-command retry only for confirmed 40001/40P01 aborts. Inside the callback enlist canonical Core identity with `ProcurementApprovalExecutionLocks.CoreAsync` and Core document metadata with `UseTransactionAsync` on the same connection. Require `InvoiceDocumentStoreBinding.SamePhysicalDatabase`; a separate store remains unavailable until its real authenticated reader protocol exists.

Proposed order within this adapter: current actor/action/scope → invoice row lock → owned source/mapping/issued checks → original-key receipt recovery → expected versions/current source → policies/assignments/document hashes → Core engine and Procurement evidence writes → final versions/receipt → one commit. On decisions lock invoice before case; every writer must use the same order. Opening and mapping mutation both lock the invoice, so remapping cannot race an accepted decision unnoticed. Source-read-only history and recovery must not trigger a new case or comparison. No blob upload, scanning, email, document interpretation network call or Finance call occurs inside this transaction.

### Separate policy owner, not a financial threshold

Procurement owns review meaning and assignment policy under the existing independently governed `PolicyVersion`, key `INVOICE_UNIT_REVIEW`. Core owns execution and canonical identities. The actual policy parser/publication adapter/resolver **does not exist**: current `CheckCompanyRules` supports PurchaseRequest/PurchaseOrder/Receiving/InvoiceMatching only, and the PR resolver explicitly requires purchase-request route/manager/spending authority semantics. Reusing it unchanged would invent review authority.

The proposed initial document explicitly lists named canonical principals per stage with `One` or `All`, no implicit approver, amount threshold, budget owner, hierarchy fallback, quorum default or currency rounding. Resolve each principal from current Core company membership and configured review action; pin the actual worker and reject duplicate worker aliases and proposal-maker self-review. `One` maps to Core `Any`/required1, `All` to Core `All`/all named workers. Unknown stages/modes/department-scoped rules fail closed. Policies must follow existing configuration edit→independent review/publish, including worker independence and current scope. Adoption of named reviewers and desired stages remains business-owned, not seeded by engineering.

Initially only the original mapping maker may request review with explicit request permission, preventing an unpinned separate opener from becoming their own approver. Broader delegated opening needs an explicit policy and retained opener-independence rule; it is not inferred from administrator status. Historical signer accounts need not remain active to read proof. Current decision actors and current comparison consumers must remain canonical, permitted and in scope. Both matching and review policy publication/current-effective requirements from `cadaffb` remain unchanged; no automatic grace for withdrawn policy is introduced.

### Purpose-limited access and typed consumption

Register empty-bootstrap action IDs `procurement-invoice-unit-review-request`, `procurement-invoice-unit-review` (matches reviewed v2 contract), and `procurement-invoice-unit-review-read`. A reviewer obtains assigned-source detail and exact mapped document-version downloads, without invoice-read, PO-read, vendor/bank/Finance directory grants. The action catalogue currently lacks these actions; registering them and role-management tests is a prerequisite, not evidence that a role label grants access.

Do not relax `ProcurementInvoiceDocumentAccess`, whose read/download currently requires broad invoice-read. Add purpose routes under the review ID, constructing the existing Core `RecordDocumentService` with a narrowly scoped `IDocumentOwnerAccess` adapter and existing blob/record/scanner/limits services. That adapter allows only Read/Download for the actual owned invoice and the source's pinned document-version allowlist, current assigned worker/action/branch, nonretired owner, Available scan state and exact retained SHA. A broad owner list must never leak unpinned versions; return the source-pinned metadata projection and use Core download for each allowed version. No Upload/RetryScan through this purpose adapter. Never return StorageKey or bank/Finance fields.

For a new comparison, an owner-authenticated, same-transaction `InvoiceRetainedUnitReview` reader loads actual source/outcome/Core case+decisions/worker pins/policies/doc metadata and supplies typed authority to `InvoiceRetainedUnitReview.Verify`. A new typed projection consumes those verified units for the exact original mapping. The schema-1 producer remains unavailable; no v2→v1 actor/time rewriting. Missing/ambiguous/stale proof yields a plain review-unavailable blocker. Exposure owner remains unavailable and AP handoff false regardless of review success.
