# Original invoice unit review — P22 bounded persistence and execution plan

## 1. Reference and evidence status

PROC-11-P22: persisted original invoice-unit review. New source planning only, based on reviewed held pure checkpoint eb9e58a and next published combined baseline (pending root commit). No schema/model/API/runtime proof yet. Canonical P21 must not be rewritten or relabelled.

## 2. User outcome

An invoice mapper requests an independent review of the exact original document lines/units; named reviewers inspect the pinned documents and approve/return/reject using Core approvals. Successful independent review supplies retained unit evidence to a currently authorized matcher. Comparison, invoice acceptance, AP and payment remain different outcomes.

## 3. Current deficiency

SupplierInvoiceLine stores quantity/price/tax, not a verified original unit. InvoiceMappingLine.InvoiceUnitCode/document-line reference is a proposal. Exact18,4 transport and sealed PO UnitCode prove their own records, not what the supplier document says. Production IInvoiceMappingEvidenceReviewer and historical exposure owner are both currently Unavailable.

## 4. Owned modules and reuse

Procurement owns original invoice/mapping/review source/outcome and review policy semantics. Reuse Core identity/branch/action catalog, ApprovalEngine/ApprovalDbContext and existing enlisted coordinator, CoreDocs availability/scans/version/hash/access, existing PolicyVersion/operational worker identity. No duplicate approval/document/identity master, Finance balances or AP ledger.

## 5. Scope and prerequisites

All operations bind tenant/company and immutable original branch plus explicit selected branch, current allowed branches and canonical active worker. Mapping+original invoice version/digest, issued PO source, matching policy, review policy and pinned owner-authorized Available documents must be proven. Incomplete source/doc/policy/worker data blocks new effects.

## 6. Policy configuration dependency

Add INVOICE_UNIT_REVIEW as a third governed operational PolicyVersion key using existing strict InvoiceUnitReviewPolicyRules parser: explicit named One/All stage graph, no guessed reviewers/quorum/threshold. Extend existing operational worker identity key allowlists, lifecycle dispatch/validation and P22 CHECK/readiness manifest. Existing generic policy mutation must never become an ungoverned backdoor for this key. Draft CRUD can hold unconfigured rules; review/publication require strict completeness and independent original-author controls.

## 7. Permissions and defaults

Register exact existing pure contract actions procurement-invoice-unit-review-request/read and procurement-invoice-unit-review with empty bootstrap grants in the existing Core action catalog. Requester additionally needs existing invoice read/mapping ownership; reviewer receives purpose-only case access, not broad invoice/PO/document read. Execution option defaults false; no policy/grant/provider availability inferred from marker or erp_admin alone.

## 8. Source identity and maker

The opener must be the actual current immutable mapping maker principal AND original worker; current mapping pointer/revision and original invoice digest must match. Historical maker offboarding must not invalidate already retained terminal evidence. Current acting accounts and their own branch grants remain required for each new action/recovery.

## 9. Opening contract

POST /invoices/{id}/unit-reviews with original UUID commandKey, expectedInvoiceVersion, expectedMappingRevision and optional reason. Server chooses/loads one current published matching policy and one independently governed review policy, actual document hashes and immutable issued proof. No caller Complete/Verified/currentAuthority/documentSHA/reviewerWorker/caseID flags are accepted.

## 10. Core execution contract

Create SourceId=ownedReviewId/sourceVersion1 Core case with sourceType invoice_unit_review in the same physical PostgreSQL transaction. Use held PrepareOpen and exact deterministic Core graph verification; persist canonical worker pins per actual generated assignment. Invoice→Core case is the only new-effect lock order. Issued source is read-only immutable proof; never acquire upstream PR/PO/receiving writer locks after the invoice lock.

## 11. Decision contract

POST /unit-reviews/{reviewId}/assignments/{assignmentId}/decisions with UUID commandKey, expected invoice/case/assignment versions, Approve/Return/Reject and reason where required. Current canonical actor must match pinned assignment worker and actual current Core assignment plus its own action/branch grants; alias accounts cannot satisfy maker/checker independence. Delegate votes/quorum/advancement to Core ApprovalEngine.

## 12. Terminal contract

Seal exactly one immutable outcome from actual owned Core case/decisions, original source/policies/graph/documents using held InvoiceUnitReviewTerminal. Only Approved becomes schema2 Verified evidence. Returned/Rejected retain explicit nonverification; actual Core CompletionKey stays null for these. Never rewrite mapping status/hash or fabricate a v1 review envelope tied to the current matcher.

## 13. Four owned record proposal

InvoiceUnitReviewSource: scope/invoice/mapping/issued/policy/source versions, original/mapping/source digests, sourceJson and command identity. InvoiceUnitReviewAssignmentEvidence: scoped source+actual Core assignment/principal/worker pins. InvoiceUnitReviewOutcome: one per source, actual final decision/state/time/evidence JSON+SHA. InvoiceUnitReviewCommandReceipt: scoped actor/action/key/request fingerprint and original committed response for open/decision recovery. Reuse invoice pointers only if justified by actual query need; immutable mapping/original records remain unchanged.

## 14. Stable keys and constraints

Use immutable scoped source/invoice/mapping/issued FKs and same-save append-only source/assignment/outcome/receipt guards, bounded explicit short names, null-safe checks and unique source/key/case/outcome/assignment constraints. Core case/assignment/decision IDs remain verified references, not cross-schema FKs or mutable CAS principal keys. Existing mapping→invoice+PO+issued composite ownership is reused, not weakened.

## 15. P22 migration plan

Generate only additive 4-table migration from authoritative canonical P21 snapshot after source tests. Alter existing operational identity policy-key CHECK to add the third key, preserving all old rows/keys; update expected actual PG manifest and require real P22 history/finite privileges. No canonical P20/P21 artifact edits, forged applied history or legacy evidence backfill. Precise table/index/FK names fixed before generation and reviewed after actual EF output.

## 16. P22 rollback plan

First lock all new review tables plus operational identity/policy/audit/outbox owners that can retain new review use. Veto Down on any new source/pin/outcome/receipt, INVOICE_UNIT_REVIEW policy/evidence or matching audit/outbox trace, including selectively removed primary rows. Then remove new constraints/tables and restore original two-key CHECK only on an unused schema. SQL55000 exact guard; apply twice, empty Down/Up and genuine used Down denial rehearsals.

## 17. Transactions and replay

Use existing shared session branch fence before transaction snapshot and existing same-connection serializable coordinator with bounded confirmed-abort retries. Lock invoice then Core case for new decisions. Durable receipts persist response after actual Save versions. Original-key GET recovery first rechecks current actor/action+both invoice/retained source scope; reports historical committed result, not new decision authority. No automatic POST retries on unknown outcomes.

## 18. Document purpose access

ProcurementInvoiceDocumentAccess currently requires broad invoice-read for nonupload. Add only a validated current same-review assignment/source/invoice/pinned doc+version+hash metadata/download path. Reuse Core owner access and route-local IDocumentRecords Find/List pin decorator; no upload/delete/retire/general list or unpinned version access. Scan/retirement revocation and exact version owner checks stay enforced. Do not accept client assignment IDs as proof.

## 19. Typed v2 matcher bridge

Load actual persisted source/pins/outcome/Core graph and matching/review policies in the existing matching enlisted transaction; verify retained evidence with InvoiceRetainedUnitReview and separately constructed current InvoiceUnitReviewConsumer. Extend trusted review result/projector with an explicit schema2 authority path sharing line projection; keep v1 unchanged and never silently upgrade. Current documents and matcher permission/scope remain checked; historical signer need not still be employed.

## 20. Invalidation and retained history

New original/mapping version/digest, document version/hash/availability change, issued source mismatch or changed policy identity must prevent fresh review/consumption. Keep prior immutable sources/outcomes/receipts visible as historical facts under current scope. Returned corrections create a new mapping/review source; do not overwrite old makers, pins or outcomes.

## 21. Privacy and reads

GET invoice unit-review readiness, own/mapped case detail, assigned tasks and original command recovery expose only current invoice/review fields, exact strings and safe blockers. Filter immutable source+current actor/branch before count/page. Assigned reviewer cannot enumerate other invoices/POs/source allocations or original Finance references. Metadata GET never runs compare, creates state or writes audit. Authorized document download retains the existing Core DownloadAuthorized audit after immutable content-hash verification; this is a document access fact, never unit verification.

## 22. Finance boundary

Unit review verifies the supplier document unit only. Existing IInvoiceHistoricalExposureOwner remains Unavailable until an authenticated complete Finance/AP owner checkpoint/cutover/lease/revocation interface is actually implemented. Local allocations, new PO creation, documents, payroll and PR funding do not imply zero prior AP consumption. Verified unit review alone must still produce Blocked comparison if exposure is unknown, and never APReady/payable approval.

## 23. Pure and model acceptance

Import reviewed held parser/retained/transition/terminal contracts with their tests and preserve all source-case binding/strictJSON/microsecond/chronology invariants. Add same-save source/assignment/outcome/receipt completeness, append-only, current/original digest mismatch, wrong scoped FK, missing provenance, added children to persisted source and otherwise-valid reused receipt/key/result denial cases. Verify all evidence fields before effects.

## 24. Mapped HTTP acceptance

Actual routes: open→assigned queue→vote→terminal→typed consume, Current CASs and replay; worker alias swap/otherbranch/foreigninvoice/policy/schema/doc denials; Returned/Rejected neverVerified; expired/offboard historical signer retained but current actor stillrequired; no broad invoice read for reviewer and no unpinned document/version visibility. Known store failures must not become200Unavailable or falseauthority. No fake readiness in production.

## 25. Actual PG and browser acceptance

Finite private runtime role: existing Core identity SELECT only, approval execution only required approvals DML; new evidence/receipts/audit/outbox append-only and existing policy writes. Use real CoreDocs owner lifecycle/private blob with known-bytes-only testscanner explicitly test-only. Prove4FK/check/unique denials, same-tx rollback/no partial Core vote, concurrent quorum/replay, normalized jsonb/time hashes and guarded Down. Browser extends existing review/detail panels, real HTTP+independent owner reload; no mock Procurement responses or fabricated Finance owner.

## 26. Readiness and operations

Actual owning-context/readiness checks exact generated P22 columns/constraints/indexes/history and finite privileges including no columnUPDATE/DELETE/TRUNCATE/ownership bypass on append tables; reuse existing inspection helpers. Keep option false/unavailable until migration and real role proof; observe source/case/outcome linkage and safe correlation errors without exposing document content or credentials.

## 27. Implementation sequence

A: frozen pure contracts+28section spec and explicit third-key policy integration/action catalog. B: four records/mapping/Save guards+tests, parent-generated P22/actual constraints/rehearsal/readiness. C: actual open/queue/decision/read/recovery+document purpose guard+typed v2 provider and unavailable-exposure comparison tests. D: existing UI case/reviewer panel adapter+real browser owner proof. Separate coherent Git commits and independent source review per slice; parent serial gates, no parallel SDK jobs.

## 28. Decision and remaining work

Plan ready for parent technical review, not implementation completion. Need adopt exact baseline once combined source is committed; review required policy lifecycle extension and v2 result/projector API shape before broad code. No user policy defaults requested/invented. New source may be developed default-off while named policies/grants remain unconfigured. Finance/AP completeness is an explicit separate owning-system dependency, not a reason to omit this unit-review workflow.


## Isolated source increment A — persistence prerequisite (unexecuted)

Based on published combined `9445dc1`; imported the four reviewed pure contracts/four test files from held `eb9e58a`. Added four immutable review records, literal text source/issued/outcome/command JSON, strict storage rehydration and actual same-transaction Core graph persistence guards. The storage guard requires a current worker and genuine Npgsql enlisted transaction; it does not install a fake InMemory authority provider. Existing coordinator now carries its already-authenticated WorkerId into its private transaction scope rather than dropping it. Source opening verifies actual scoped mapping/current original/PO issued record and Core approval graph; outcomes re-run terminal sealing using retained pins. These are source-only prerequisites: no routes, DI provider, feature activation, policy adoption, P22 migration or test execution yet. Subsequent increments must add third-key governed policy/schema compatibility, actual case routes/purpose documents, typed v2 consumption and real HTTP/PG proof before any completion claim.


### Increment A source review corrections (unexecuted)

Distinct review found two missing persistence correlations in `499660f`; corrected source now calls the shared owning-evidence verifier against actual review/matching policies and actual retained operational publication proofs before opening source persistence. Typed strict request/response receipts bind actual Core assignment/action/expected versions/decision/state/outcome. A terminal outcome requires its same-save final-decision receipt; pending votes require no outcome. The guard additionally checks actual Core case/vote row `xmin` against the enlisted transaction ID, so an older orphan vote/case cannot be adopted as a newly committed command merely by rehashing request JSON. This check is deliberately PostgreSQL-specific and must receive actual PG positive/negative proof; pure DTO tests only emulate documented EF version increments and do not claim persistence execution. Original unit or Finance ownership is not inferred from these correlations.


### Source review correction: Core insert provenance (unexecuted)

The initial xmin/top-level-transaction approach was retracted before execution: Core columns are quoted, EF savepoints can use subtransaction xmin, and an updated old row is not an INSERT. Source now binds the coordinator's actual shared physical connection/transaction Core context internally before any work, records Added case/vote IDs only following successful EF SavedChanges, and still reloads the persisted Core graph in the same transaction. Pending insert observations clear on failed SaveChanges. This is owning-adapter provenance, not an HTTP-provided completeness flag or a claim of executed PostgreSQL proof. Private actual positive, rollback and old-orphan adoption regressions remain required.


### Source checkpoint — composed adapter (unexecuted)

The isolated P22 adapter now composes opening, current assigned queue, decisions, immutable outcomes, actor-owned historical command recovery, purpose-only pinned Core document reads/downloads, and an explicit typed-v2 comparison consumer. New policy/actions are default off with empty bootstrap grants. Exact P22 migration/CHECK manifests intentionally remain uncaptured and readiness false pending central generation and genuine PostgreSQL verification. Existing P21 migration files remain unchanged. The existing Finance history owner remains unavailable.

Actual Core INSERT provenance uses coordinator-owned EF Added/Saved events tied to the exact physical connection and original transaction object, not PostgreSQL xmin. The predicate is still subject to positive Save/savepoint, rollback/rebind and updated-orphan denial verification. No source checkpoint claims compilation, migration, runtime workflow completion or deployment.
