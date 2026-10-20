# PROC-03 — Editable request policy-code boundary

Issue: [module-connect #32](https://github.com/georgemunganga/module-connect/issues/32).
Status: source implementation and regression scenarios authored; not executed or deployed by this change.

## 1. Feature Identification
PROC-03 policy-code draft boundary correction, additive to the existing requisition specification.
## 2. Purpose & Business Outcome
A catalog display kind such as `Goods` must become request policy code `GOODS` when the employee saves an editable request, rather than failing much later during policy evaluation/PO submission.
## 3. Scope & Exclusions
New requests and explicitly edited Draft/ReturnedForChanges input values only. No backfill, historical snapshot rewrite, policy relaxation, inferred synonyms, or trusted receiving classification.
## 4. Parent & Related Features
Purchase requisition draft CRUD, item catalog, buying-rule validation and preview.
## 5. Actors & Participants
Existing authenticated requester and permitted draft editor; no additional authority.
## 6. Entry Points & Triggers
POST `/api/procurement/v1/requests` and PUT `/api/procurement/v1/requests/{id}`.
## 7. Preconditions
Existing employee, scope, ownership, version, editable lifecycle and released-prior-case checks remain mandatory.
## 8. Workflow
Validate every input line before header/line mutation. Trim optional category/type; blank becomes null. Accept only ASCII letters, digits and underscore, then uppercase invariant. Apply validated canonical values on draft creation/edit.
## 9. Status / State Transitions
No new transitions. Submitted/approved records cannot use draft editing to change evidence.
## 10. Permissions & Data Scope
Existing endpoint permissions, creator/worker checks and organisation filters apply unchanged.
## 11. Business Rules
Category code maximum 100 characters; purchase type maximum 40, matching its existing database column. Both use the existing strict policy identifier grammar. No active-category requirement is added: a syntactically valid draft may still need policy review later. Unicode lookalikes, separators and embedded whitespace fail before saving.
## 12. Data Fields & Ownership
Procurement owns PR line CategoryCode/PurchaseType. Item.Kind remains its existing display discriminator; item/catalog records, publication version and retained catalog evidence are unchanged. Canonical type is a declared request policy input, not proof of receiving classification or authority.
## 13. Screens & Data Views
Existing request editor and response fields remain; no new UI. API returns persisted canonical codes.
## 14. User Experience / Human Behaviour
Mixed-case catalog/noncatalog input works. Invalid category/type receives a field-specific 422 validation message and draft remains unchanged.
## 15. Notifications & Communications
No new notifications.
## 16. Documents & Attachments
No document changes.
## 17. Exceptions & Edge Cases
Optional whitespace becomes null. Boundary is ASCII-only before case conversion. Category 101 and type 41 characters fail; valid underscore/digit codes retain meaning. Old malformed values are not silently repaired during submission or replay.
## 18. Integrations / APIs / Events / Sandbox
Response shape and existing event/audit behavior remain. Request catalog-reference checks currently do not equate Item.Kind with PurchaseType, so no catalog-evidence equality change is required. Existing saved/reference validation remains untouched.
## 19. Audit / Security / Privacy
Invalid create/edit must produce no persisted requests, lines, audit or outbox changes. Approved historical/source evidence remains immutable. Strict policy rule validation and preview share the same strict code predicate; only explicit draft inputs use normalization.
## 20. Reports / Dashboards / Metrics
Newly saved draft codes are canonical; no historical reporting rewrite.
## 21. Configuration & Localization
Identifier grammar is invariant, not locale dependent. No new setting or monetary policy.
## 22. Non-Functional Requirements
Small bounded per-line validation, existing maximum 100 request lines. No schema migration or new shared service.
## 23. Postconditions
Successful draft save/reload returns canonical codes or null; catalog display Kind remains unchanged.
## 24. Failure & Recovery
422 before mutation for any invalid line; edit version/header/removed lines remain unchanged. User corrects input and explicitly saves again.
## 25. Acceptance Criteria
Mixed-case catalog Goods and noncatalog Service persist GOODS/SERVICE; Asset/Subscription normalize likewise. Whitespace optional values become null; invalid characters and storage-bound violations cannot mutate data. Policy validation still rejects noncanonical historical scenario codes.
## 26. Test Scenarios
Authored mapped TestServer HTTP create/edit/reload theories, invalid multi-line create and header-changing edit denial with fresh-context request/line/audit/outbox counts and exact reload equality; existing full catalog publication/selection scenario now checks mixed-case kinds, canonical request response and unchanged item Kind. Pure helper tests cover ASCII/Unicode/blank/100-character boundary and strict predicate. These are source scenarios, not passing evidence; actual PostgreSQL/browser gates remain required before issue closure.
## 27. Ownership / Dependencies
Procurement Application owns shared policy identifier grammar; request transport boundary owns optional-input normalization and PR storage bounds. Core identity/organisation/audit infrastructure is reused.
## 28. Open Decisions / Assumptions
No new business-policy decisions. Existing approved/historical malformed codes require a separately authorized remediation decision if encountered; this change does not rewrite them. Root owns centralized build, complete regression, actual HTTP/PostgreSQL and browser validation. Issue #32 remains open until those gates pass.
