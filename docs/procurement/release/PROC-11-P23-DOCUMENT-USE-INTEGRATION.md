# P23 original review — real Core document-use integration

**Source only; unbuilt and unexecuted.** Isolated from `54e8d9f`; no changes to any model, migration, Designer or snapshot. Existing P01/P02/P03 Core document migrations and genuine P23 Procurement migration remain owning artifacts. All earlier SDK/runtime proofs cover their recorded sources, not this increment.

## 1. Purpose
Retain the exact scanned supplier original while a genuine independent Core review uses it; a JSON hash or current Available label alone cannot prevent retirement.

## 2. Existing feature
Extends PROC-11, ADR-003 and the P23 original-unit review contract. Core remains owner of documents, scans, blobs, lifecycle leases and approval records.

## 3. Scope
Real upload and scan services remain `RecordDocumentService`, `PrivateDocumentStore` and `ClamdDocumentScanner`. No new scanner, bucket, metadata master, approval engine or Finance provider.

## 4. Configuration
Existing CoreDocuments Enabled/connection/private-directory/scanner-socket and unit-review options remain default off. Same physical database is mandatory for document metadata and the explicit finite ProcurementApprovalExecution connection. Different usernames are allowed; different stores fail closed.

## 5. Pre-BEGIN owner
Open derives the exact document owner from its invoice route ID and current tenant/company scope. It proves actual invoice/current actor inside the fenced transaction; an ID alone grants nothing.

## 6. Lock order
Existing Core branch → catalog → Core document owner shared/exclusive session lease → SERIALIZABLE → domain rows. Branch/catalog lock keys and normal coordinator behavior stay unchanged. The document context and lease use the actual same physical connection and live transaction.

## 7. Transaction
Typed coordinator overload owns contexts, retry, transaction and commit. Open saves use rows, actual Core case, Procurement source/assignment/receipt/audit atomically. Any exception rolls all three schemas back. No blob or scanner work occurs in its retry callback.

## 8. Source reference
Core use key and source record ID are the immutable review ID; source revision is 1, kind Review. Owner is procurement/supplier_invoice/the exact invoice ID. Publication is not inferred.

## 9. Selection
A bounded distinct version set is derived from immutable mapping lines and owning Core rows. Every document/version revision, hash, clean scan attempt/evidence and current availability is verified by the existing Core use store. Missing, foreign, changed or unscanned content is rejected.

## 10. Current authority
The module creates the purpose authorizer only after current Core action/record-scope validation. It can authorize only that source/reference/exact version set. It grants no generic download, upload, scan or retirement.

## 11. Persistent proof
The Procurement source Save guard independently reads the already-stored same-transaction Core use set through the Core-owned reader, verifying full set digest/ownership and original maker/hash/time binding. A direct source save without pins fails.

## 12. Historical read
Core adds a read-only retained-evidence operation on the caller's enlisted context. It cannot acquire uses, authorize a new lifecycle effect or assert current availability. The owning module separately authorizes current purpose, before lookup and for the exact returned set.

## 13. Decisions and consumers
New decisions, purpose document reads and retained unit consumption require existing exact retained pins. They never manufacture pins for an older source. Current document availability/hash and Core permissions remain separately required where already specified.

## 14. Recovery
Same command key returns the original typed receipt after current actor/scope checks. Open replay does not stage another pin set. No new schema or receipt-wire version. Unknown commits require GET recovery; no implicit network-error retry.

## 15. Retirement
A distinct empty-bootstrap action `procurement-invoice-document-retire` protects the new POST invoice/documents/documentId/retire route. Read/upload does not grant retirement. The route uses current Core actor and invoice scope, and an exclusive owner lease acquired before BEGIN.

## 16. Retirement boundary
Only unused Received invoice documents can retire: no current mapping, bound original reference or Finance posting. Expected document revision and a bounded nonblank reason are required. Core additionally rejects legal hold or any persisted/local use pin; no purge, pin release or blob deletion.

## 17. Managed writer
Existing `EfDocumentRetirement` now exposes its same guarded transition inside an already-enlisted owner transaction. Its standalone method delegates to that transition; no duplicate lifecycle rules. The caller owns commit, never an independent nested transaction.

## 18. Readiness
Actual execution-role Core document readiness checks P03 applied/no pending/model drift, mapped columns/keys/FKs/unique indexes and exact validated two CHECK definitions from retained PG17 capture. It checks actual current connection privileges and ownership; no fake default-ready provider.

## 19. Evidence provenance
Exact Core P03 CHECK constants come from `core-document-p03-migration-20261021_0812/p03-checks.json`; its SHA is recorded alongside code. These source pins require renewed actual finite-role and schema-negative tests before operational activation. P23's existing exact manifest must also be captured separately.

## 20. Finite execution role
Create a separately reviewed workflow role, never broaden existing procurement_api silently. Read actual Core identity/organisation facts; write only the existing Procurement/Core approval contracts. Core documents: SELECT on six owning tables/history, INSERT use_sets/use_pins; no UPDATE on use/scan/event evidence, DELETE/TRUNCATE, owners, schema CREATE, memberships to elevated roles or identity DML.

## 21. Optional retirement privilege
Only an explicitly configured retirement actor uses the endpoint. The finite database role additionally needs UPDATE on the five mapped document retirement/CAS columns and INSERT record_events, not broad UPDATE on all Core data. If absent, readiness refuses retirement while review opening can remain supported.

## 22. Upload role
Existing CoreDocuments connection owns upload registration and scan-state transitions. Its finite role needs bounded document/version INSERT, exact scan-state column UPDATE and scan/event INSERT. It is not the approval execution role by default. Blob directory and ClamD socket are separate private mounts; no app schema creation.

## 23. Deployment order
Independently rehearse Core P01→P03 migration chain with owner credentials; preserve current data/history/ACL and captured scan evidence. Review finite role provisioning, actual blob/socket configuration and current Core grants. Publish this full source and gate it before flags activate. No production grants, migrations, flags or scanner were changed by this commit.

## 24. Unit coverage authored
Workerless principal exact-use authorization; wrong company/actor/owner/source/revision/kind/version/duplicate/empty set denial; no download-through-pin authority; separate retirement action with no bootstrap grants; original/mapped/posted/cancelled invoice retirement denial. These structural tests are not actual Core grant proof.

## 25. Actual HTTP/PG coverage authored
Existing `--unit-reviews` probe now independently reloads exact use set after real HTTP Open/replay. Persistence provenance saves Core/document/Procurement owners then rolls back and fingerprints every document/use/scan/event row too. Historical orphan adoption stages otherwise-valid pins so failure cannot be masked by missing retention.

## 26. Race coverage authored
Private storage-only test stages actual document-use rows under a shared session fence. Exclusive managed retirement must not enter its callback until that transaction rolls back; it then performs the actual transition and deliberately rolls back too. Full footprint must equal baseline. This proves protocol/storage, not production module permission. Committed retained-pin denial and public finite-role cases remain required acceptance.

## 27. Required gates
Build Core Documents/Procurement/API/tool; focused and full impacted tests; unchanged model/137 migration artifact proof; actual finite-role upload→real ClamD→Available→mapping→Open/Core case/pins→purpose download→independent decision. Test wrong store, evidence-column UPDATE/ownership, weakened FK/CHECK, revoked/current branch, rollback and committed pin/retirement denial. Test the race with bounded timeouts on private PostgreSQL. Production scanner and Finance exposure owner cannot be replaced by synthetic fixtures.

## 28. Limits
No Finance history, complete exposure, invoice approval, payable/APReady, posting, payment or sourcing publication is provided. Unknown exposure stays Blocked after original-unit verification. Source is reviewable but has no build/runtime/deployment claim; all flags/actions remain explicitly controlled.
