# PROC-11 P22 original-unit review HTTP contract

Companion to the 28-section [persistence plan](PROC-11-P22-ORIGINAL-UNIT-REVIEW-PERSISTENCE-PLAN.md).
Backend source checkpoint: `37ac857`, branch `codex/procurement-invoice-unit-review-execution`.
**Source contract only:** uncompiled/unexecuted; central P22 migration, exact PG catalog capture,
finite runtime and actual browser proof pending. Execution option defaults false. No deployment claim.

## Availability and existing UI

Use existing Bills detail/review components; no new invoice/approval/document master or payment action.
Additive metadata `capabilities.invoiceOriginalUnitReview` describes configured review execution only.
It is true only with the explicit option+execution store, actual finite P22 schema and supported same-store
CoreDocs availability. Its current uncaptured schema implementation returns false. Missing capability means false;
configurationVersion, an ERP administrator role, matching enabled or a recorded unit string does not
establish available review execution. Hold mutation activation until this verified backend capability is explicitly true; use the current named action
and per-record readiness separately. Old backend 404/503 is unavailable, never success or an empty review history.
The existing manual invoice capture/edit/document/mapping UI keeps its existing contracts.

All routes have prefix `/api/procurement/v1`. Company and selected branch use existing shell headers.
Current canonical active worker and own Core action/branch scope are checked server-side. Clear loaded
review/document/command state synchronously on actor, authentication generation, company or branch
change; ignore superseded callbacks. An unmapped read-only administrator cannot perform unit review.
Versions are positive safe integers on the client; do not coerce unsafe values. No financial Number
conversion is involved in this API; retain existing exact string monetary clients.

## Invoice owner entry

`GET /invoices/{invoiceId}/unit-review-readiness`

Requires `procurement-invoice-unit-review-request` **and** `procurement-invoice-read`, the actual current
mapping maker principal/worker and current record scope for a positive request candidate. Returns:

```ts
{
  invoiceId: string; invoiceVersion: number; mappingRevision: number;
  existingReviewId: string | null; canRequestReview: boolean; blockers: string[];
  isUnitVerification: false; isInvoiceApproval: false; canPostToFinance: false;
}
```

This GET runs bounded pure opening validation only; it does not attach/persist a case, compare the
invoice or create a receiving/exposure state. Missing/unsupported schema or execution store returns503.
When retained schema is readable but execution is disabled, `canRequestReview=false` with
`OriginalUnitReviewExecutionDisabled`. Known changed inputs/policies/documents produce explicit
blockers; store failures must not become200 empty/ready. `existingReviewId` is the same mapping's
retained review; absence is not authority to invent a case.

`POST /invoices/{invoiceId}/unit-reviews`

Same named permissions and maker ownership; explicit UUID `Idempotency-Key`. Body:

```ts
{ invoiceVersion: number; mappingRevision: number; reason: string | null }
```

No policy/case/reviewer/worker/document SHA or Verified/Complete fields are accepted as authority.
Server selects actual published matching + independent review policies and pinned available original
versions. Returns200 **plain response below**, not the existing matching `{result:...}` wrapper.

## Purpose-only task and detail entry

`GET /invoice-unit-reviews/tasks?page=1&pageSize=20`

Requires `procurement-invoice-unit-review`. Page1..100000, size1..50. Returns:

```ts
{
  items: Array<{
    reviewId: string; invoiceId: string; mappingVersionId: string; mappingRevision: number;
    branchId: string | null; openedAt: string; canPostToFinance: false; isInvoiceApproval: false;
  }>;
  total: number; page: number; pageSize: number;
  availabilityMeaning: 'CurrentMappingAssignmentsOnly'; canPostToFinance: false;
}
```

Exact current own pending assignment + retained canonical worker, immutable branch/current invoice
branch equality and **current mapping** are filtered before count/page. No PO/other invoice data.
Picker/read does not hold quantity or create an approval case.

`GET /invoice-unit-reviews/{reviewId}`

Current original maker with request+invoice-read, a current canonical retained assigned reviewer with
review action, or explicit review-read+invoice-read may see scoped historical detail. Purpose reviewer
does not receive broad invoice or PO read grants. Response:

```ts
{
  reviewId: string; invoiceId: string; invoiceVersion: number;
  mappingVersionId: string; mappingRevision: number; coreCaseId: string; caseVersion: number;
  caseState: 'Pending' | 'Approved' | 'Returned' | 'Rejected';
  reviewState: 'Pending' | 'Verified' | 'Returned' | 'Rejected';
  currentMapping: boolean; originalDigestCurrent: boolean; blockers: string[];
  original: null | {
    invoiceNumber: string; invoiceDate: string; currencyCode: string;
    netAmount: string; taxAmount: string; grossAmount: string;
    lines: Array<{id: string;lineNumber: number;description: string;quantity: string;unitPrice: string;taxAmount: string}>;
  };
  assignments: Array<{assignmentId: string; assignmentVersion: number}>;
  originalDocumentLines: Array<{
    invoiceLineId: string; documentVersionId: string; documentSha256: string;
    documentLineReference: string; unitCode: string;
  }>;
  attestation: 'OriginalDocumentUnitsConfirmed';
  actionCandidates: Array<'Approve' | 'Return' | 'Reject'>;
  actionCandidateMeaning: 'CurrentAssignmentOnlyRevalidatedByCommand';
  isInvoiceApproval: false; canPostToFinance: false;
}
```

The same invoice original summary uses exact decimal strings and is omitted if its digest changed;
no source PO lines, other invoice allocations or Finance references are exposed. Changed originals
yield `InvoiceOriginalChanged` and no action candidates. Candidates prove current assignment/mapping only. They do not certify current policy/document
availability or guarantee command success. A newer mapping yields `InvoiceMappingSuperseded`, empty
assignments/candidates and historical detail. The old Core case is retained; no terminal outcome or
Core cancellation is fabricated.

## Pinned original documents

`GET /invoice-unit-reviews/{reviewId}/documents`

`GET /invoice-unit-reviews/{reviewId}/documents/{documentVersionId}/download`

Requires the exact original maker/read path or **current Pending case, active stage, Pending own
assignment and retained canonical worker**. Current mapping/original digest and exact available owner
version/hash must still match. No generic invoice document enumeration or broad invoice-read grant.
List returns `{reviewId,invoiceId,items:CoreDocumentView[],availabilityMeaning:
'PinnedOriginalDocumentVersionsOnly',isUnitVerification:false,canPostToFinance:false}`.
CoreDocumentView uses existing `documentId,versionId,versionNumber,fileName,mediaType,sizeBytes,sha256,
scanState,failureCode,canDownload`. Download returns the actual Core private byte stream after hash
verification and the existing `DownloadAuthorized` audit; it does not attest units. Never construct
arbitrary invoice/document URLs from manual IDs. Purpose reviewer cannot upload/scan/retire files.

## Explicit decision and original-key recovery

`POST /invoice-unit-reviews/{reviewId}/assignments/{assignmentId}/decisions`

Requires `procurement-invoice-unit-review`, exact named current assignment/worker, independence from
original mapping maker, current source/policies/documents, UUID `Idempotency-Key`. Body:

```ts
{
  invoiceVersion: number; caseVersion: number; assignmentVersion: number;
  action: 'Approve' | 'Return' | 'Reject'; reason: string | null;
  attestation: 'OriginalDocumentUnitsConfirmed' | null;
}
```

Approve requires **explicit human attestation** to the pinned original's declared units; do not precheck
or silently send the attestation without an explicit review action. Return/Reject require a reason.
Do not infer unit verification from PO units, mapped proposal, scan result or a document preview.
Core alone decides quorum/state. Approved yields retained schema2 Verified; intermediate votes stay
Pending; Returned/Rejected never verify units. No invoice status approval/AP effect is implied.

Open and decision success response200:

```ts
{
  schemaVersion: 1; invoiceId: string; reviewId: string; coreCaseId: string;
  commandKey: string; operation: 'Open' | 'Decision';
  invoiceVersion: number; caseVersion: number;
  caseState: 'Pending' | 'Approved' | 'Returned' | 'Rejected';
  reviewState: 'Pending' | 'Verified' | 'Returned' | 'Rejected';
  decisionId: string | null; outcomeId: string | null; completedAt: string;
  recordedHistoricalResult: true; isInvoiceApproval: false; canPostToFinance: false;
}
```

`GET /invoice-unit-reviews/{reviewId}/commands/{commandKey}` returns that original200 response only to
the same current canonical original command actor/worker with the current original operation action
(and invoice-read for Open) in current source+invoice scope. It does not rerun a vote or confer current
source eligibility. No operation query parameter; no matching command wrapper.

After ambiguous POST (network loss/5xx), preserve the exact original UUID/body/actor+scope, disable a
second POST and offer **GET-only** recovery. A recovery404 remains uncertain; it does not authorize a
new key or automatic repost. Actor/context changes clear access to the old uncertain operation. A
recorded result is historical acknowledgement, not current authorization or payment authority.

## Existing matching consumer boundary

Matching POST remains the existing `/{invoiceId}/matching/compare` contract. It loads persisted
schema2 original review + actual Core completion and separately validates the current matcher's own
action/worker/branch/document/policy evidence. It never fabricates caller-bound v1 review JSON.
Historical reviewer offboarding does not erase retained proof. The external Finance exposure owner is
still unavailable: valid units alone leave comparison Blocked with no claim/AP/payment effects.


## P23 source amendment — not yet built or enabled

The P22 filename above is historical. New original-unit review belongs to future genuine P23. Detail and newly recorded command receipts add canonical `makerPrincipalId` and retained `identitySchemaVersion: 3`. They do not derive the owner from the current employee. `evidenceSchemaVersion` is absent on pending receipts and null on pending detail; terminal Verified, Returned and Rejected responses carry actual terminal version 3. Returned/Rejected are never unit verification. The request/receipt envelope remains `schemaVersion: 1`; it is not source/evidence version 3. Existing separately retained legacy v2 review evidence preserves its original bytes/consumer semantics and is not relabelled as a new-table v3 source.

Every new stored receipt is source-bound; historical recovery/replay validates its key, request fingerprint, actor, invoice/review/Core-case references, source owner/version, original completion and non-financial flags. Historical acknowledgement may outlive a later invoice revision and does not authorize a new state change. Fresh commands still require current Core account/actions/scope and current CAS. Known optional Core-worker aliases only restrict maker/reviewer independence; they never supply active employment or permission.

Principal evidence v2 now emits an explicit durable `principalEvidenceVersion: 2`/`principalEvidenceKind` audit marker. Legacy v1 field-array audit bytes are unchanged. New comparison execution 2 publishes `supplier_invoice.compared.v2` with explicit execution/principal version; execution 1 retains its old v1 event and payload. Down guards must consider these actual retained producers even if a business row was selectively removed.

Readiness remains false until genuine P23 migration/catalog capture includes all four new review CHECKs, both altered mapping/execution CHECKs and the operational identity CHECK, plus actual finite-role/atomic Core Documents/Approvals evidence. No authority is granted by these additive metadata fields.
