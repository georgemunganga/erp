# PROC-04 — Catalog classification at the request/commitment boundary

Tracking: [#32](https://github.com/georgemunganga/module-connect/issues/32), separate adjacent trust correction.
Status: source implementation/regressions authored; no build, database or browser verification, no deployment claim.
Base: root `fd1c7d0`; isolated branch `codex/procurement-catalog-classification-binding`.

## 1. Feature Identification
PROC-04 catalog request classification binding, additive contract for PROC-02 requisitions and PROC-M5 controlled source conversion.
## 2. Purpose & Business Outcome
An employee selecting a Goods catalog item cannot declare SERVICE or another category to select a different purchasing/approval/receiving policy path.
## 3. Scope & Exclusions
CatalogItemId-backed new/edited PR lines, PR submission and new source-controlled commercial actions. No alternative master, synonym inference, arbitrary requester override, legacy rewrite, inventory classification guess or financial ledger.
## 4. Parent & Related Features
PROC-04 governed catalog, PROC-02 request and policy evidence, approved source allocation and PO execution, P25 sealed receiving classification.
## 5. Actors & Participants
Item steward owns item Kind/CategoryCode; requester selects item; existing buyer, approver and Finance actors retain their current capabilities.
## 6. Entry Points & Triggers
Create/update PR; submit stored PR; buyer approved-source eligibility/conversion; source PO edit, independent funding confirmation, commercial submit and issue.
## 7. Preconditions
Existing scoped item must be Active and purchasable, and owner Kind/category must produce supported canonical policy codes. Request type retains its existing 40-character storage bound; category is bounded to 100.
## 8. Workflow
Editable input validation loads existing scoped item owners with the existing item reference query. Omitted/blank type/category fill from the owner. Explicit values normalize with ProcurementPolicyCode then must equal owned canonical values. Validate every line/reference before changing header/lines. At submit/new commitment, existing stored values must already equal those canonical owner codes; reject missing, mixed-case, mismatching or unverifiable evidence rather than repairing it.
## 9. Status / State Transitions
No new states. Rejected input remains a draft. Approved/source/issued snapshots stay unchanged. Normal permitted draft correction/resubmission is available under existing version/Finance-release rules.
## 10. Permissions & Data Scope
Existing request ownership, worker, permissions and query filters apply. Existing PO Core current actor/branch fence and shared serializable transaction apply. The helper does not accept a caller-supplied authority/override flag.
## 11. Business Rules
`BR-PROC-CAT-007`: catalog classification comes from its owned item, not the employee's policy hints. No governed requester-override policy was found in PROC-04 §§8/10/11/12/19, PROC-02 or configuration. Therefore conflicts deny by default. A noncatalog line remains the existing explicitly controlled path; it cannot retain a CatalogItemId and override that owner's classification. No synonyms map GOODS to SERVICE or SUBSCRIPTION to SERVICE.
## 12. Data Fields & Ownership
Procurement's existing CatalogItem.Kind/CategoryCode is current source authority for this catalog adapter. Its display casing stays unchanged. PR persists derived canonical codes. Existing item/catalog master, PR/policy/submission/issued JSON are reused; no new column/table or competing owner.
## 13. Screens & Data Views
Existing catalog fields are already read-only in LiveRequests. API enforces the same contract for direct callers. Buyer source list reports NeedsReview with OrderSourceCatalogClassificationUnverified when binding is unproved.
## 14. User Experience / Human Behaviour
Omitted fields are filled transparently; explicit conflicts show a clear catalog classification error. Historical reads remain available. Restoring genuine owner/source agreement permits a new explicit command; records are not silently rewritten to make it pass.
## 15. Notifications & Communications
Existing audit/outbox only on successful commands. No new notification service.
## 16. Documents & Attachments
No changes to document ownership or attachments.
## 17. Exceptions & Edge Cases
Blank/invalid owner fields, inactive/missing owner, foreign-entity reference and item reclassification after draft/approval fail closed for new authority. Item edit requires deactivation and withdraws published prices; current submission already rejects stale publication versions. A missing price reference does not waive item classification binding. Classification drift after an approved source requires review rather than editing its pin.
## 18. Integrations / APIs / Events / Sandbox
Existing DTO shape and API routes remain. One pure Application helper and one scoped API resolver are reused by request and PO command boundaries. PO guards run after current identity/receipt checks but before new commercial mutation. Actual historical command receipts acknowledge recorded results, not newly qualified catalog authority; existing authorization remains required. Cancel, Finance release, delivery acknowledgement and historical read do not add this mutable-owner guard, preserving safe cleanup of existing obligations.
## 19. Audit / Security / Privacy
Conflicting create/edit cannot write header/lines/audit/outbox. New commitment denial cannot leave allocation/case/evidence/receipt effects because it executes under the existing shared transaction. An old mismatched approved source remains visible but cannot be used to create/approve issue of a new commitment. Sealed receiving still uses retained issued evidence, not current mutable catalog labels.
## 20. Reports / Dashboards / Metrics
No retroactive reclassification or historical report rewrite.
## 21. Configuration & Localization
Strict invariant policy codes shared with existing rule validator; display casing and unit contract remain owned independently. No override configuration is invented.
## 22. Non-Functional Requirements
Batch scoped item lookup, at most existing 100 PR lines. No per-line owner network calls, schema migration or duplication of Core/Finance/Inventory services.
## 23. Postconditions
Saved catalog PR line matches owned canonical category/type; current owner display Kind remains unchanged. New source-controlled commercial effects require proven current binding.
## 24. Failure & Recovery
422 for invalid draft/submission, 409 OrderSourceCatalogClassificationUnverified for new PO commercial actions. Existing request correction and governed catalog maintenance provide recovery. Finance release and unfunded cancellation must remain usable despite classification drift; this correction does not erase history or simulate release.
## 25. Acceptance Criteria
Omitted catalog fields fill GOODS/owner category; matching mixed-case input works; SERVICE/other-category spoof fails before mutation. An inherited tampered draft cannot submit or create policy evidence. Genuine GOODS-only policy matches an owner-bound line. Approved original evidence remains unchanged when current catalog drift blocks conversion/funding/submit/issue.
## 26. Test Scenarios
Authored mapped TestServer catalog publication scenario now asserts spoofed create/edit no persistence (fresh-context request/line/audit/outbox counts and exact unchanged reload), omitted owner fill, GOODS-only actual policy match, inherited tampered stored draft submission denial and unchanged owner Kind. Pure tests cover conflicts, absent/invalid owner, synonyms and exact stored semantics. Disposable actual PostgreSQL --orders probe source creates a genuine item-backed PR through request/budget/Core approval, changes only synthetic current owner data, requires exact classification blocker and unchanged PO/allocation/evidence footprint for conversion, funding, submit and issue; restoring owner agreement positively executes each step. These are authored scenarios, not test results.
## 27. Ownership / Dependencies
Procurement Application owns binding invariant; API owns scoped owner resolution. Existing Core actors/scopes/fence/approval engine, Procurement pins/transactions and owned item records are reused. Root owns centralized builds, actual SQL/HTTP/browser gates and distinct review before merge/enablement.
## 28. Open Decisions / Assumptions
No authorized catalog classification override exists in the reviewed contract. If the business requires one, define a separately governed exception with independent authority and pinned evidence before supporting it. Existing historical mismatches are not backfilled. No completion, production activation or independent-review claim is made by this source checkpoint.
