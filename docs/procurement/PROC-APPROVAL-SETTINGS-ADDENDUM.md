# Purchase request approval settings — implementation addendum

Status: source implementation in progress; verification and release pending. Standard: GW-ENG-STD-001, Tier 3.

## 1. Feature Identification
PROC-APPROVAL-SETTINGS: approval steps and finite named-person spending limits. Extends the existing purchase request approval blueprint.

## 2. Purpose & Business Outcome
An authorised administrator configures who may approve, how much, and within which organisation boundaries. Independent review activates financial authority.

## 3. Scope & Exclusions
Purchase request authority configuration and the existing policy editor. No payments, Finance balances, identity administration, new organisation masters, or automatic authority grants.

## 4. Parent & Related Features
Procurement → Configuration → Purchase Request Approvals → Steps / Approval Limits. Depends on Core identity, organisation, current permission grants, policy publication and approval execution.

## 5. Actors & Participants
Configuration owner, independently authorised reviewer, authorised revoker, read-only auditor, named authority holder. Account identity and worker identity both matter to independence.

## 6. Entry Points & Triggers
Existing `/procurement/workspace/settings/modules/purchase-requests/approvals` tab. No additional primary sidebar. The existing buying-rules route remains available.

## 7. Preconditions
Authenticated current canonical Core account, selected company/scope, relevant configured permission, schema readiness. Candidate people must have a unique active account, current company membership and configured approval permission; subsequent execution rechecks all eligibility.

## 8. Workflow
Create/edit draft → submit → independent review → activate or return. Published values are immutable. Changes require a new authority version and atomic supersession where an active predecessor exists. Revoke removes current authority under existing revocation permission policy.

## 9. Status / State Transitions
Draft → PendingReview → Active; PendingReview → Draft with return reason; Active → Revoked / Superseded. Published or terminal records cannot be edited or revived. The UI uses server-projected allowed actions; command endpoints repeat their own guards.

## 10. Permissions & Data Scope
`procurement-authority-read`, `-configure`, `-review`, `-revoke` remain separate grants. Reads and commands use current tenant/company/branch/department scope. A role does not confer a monetary limit. Named people lookup consumes Core records and does not confer HR administrator access.

## 11. Business Rules
Finite positive amount on submission, company currency only, fixed EstimatedLineBase amount basis, valid dates and explicit branch/department/category scope. Independent activation/return/supersession excludes the maker, owner, subject and submitter, including their alternative accounts for the same worker. Revocation keeps the existing permission semantics; no new independence policy is implied.

## 12. Data Fields & Ownership
Procurement owns grant reference/version, principal reference, maximum amount, dates, scope selections, categories, state, configuration owner and review evidence. Core owns identity/name/worker/company/branch/department; Procurement owns active procurement category references. No email, credentials or unrelated employee data in lookup responses.

## 13. Screens & Data Views
Reuse existing settings shell, page header, tabs, table, fields, selectors, switches and review modal. List/detail show person, currency, exact finite limit, scope, effective dates, version and state. Unknown policy JSON remains read-only rather than being rewritten by a partial editor.

## 14. User Experience / Human Behaviour
Use “Approval steps”, “Approval limits”, “Send for review”, “Activate limit”, “Return for changes”, “Replace active limit” and “Revoke limit”. Explain that a saved draft grants no authority. Clearly label the amount as before discounts, tax and delivery.

## 15. Notifications & Communications
Retain existing audit and workflow integration boundaries. This increment does not claim notification delivery. Future notifications consume committed transitions without determining transaction success.

## 16. Documents & Attachments
No new upload surface. Approval reasons and existing policy evidence remain available; documents continue through Core document services when separately required.

## 17. Exceptions & Edge Cases
Inactive, locked, ambiguous-login and foreign-scope people are excluded. Readable grant names may be unavailable after organisational changes; show an unavailable label with retained reference. Stale versions, changed authority family, expired dates and insufficient independence fail closed.

## 18. Integrations / APIs / Events / Sandbox
Authority routes remain `/api/procurement/v1/configuration/spending-authorities`. GET collection uses `page/pageSize/state`; GET detail and existing create/edit/submit/return/activate/revoke/supersede commands are retained. New bounded GET `/lookups/{people|branches|departments|categories}` accepts search, offset and limit (maximum 50); returns items and nullable nextOffset, not an invented eligible-total count. List/detail expose names and allowedActions; supersession includes predecessor ID/version. MaximumAmount is an exact decimal string on output and accepts decimal strings or legacy JSON numeric lexemes on input. No binary floating-point parsing. Invalid precision/range fails before persistence.

## 19. Audit / Security / Privacy
All consequential commands retain their existing transactional audit. Lookup names and identifiers are scoped, minimal and read-only. UI hiding never substitutes for server guards. No account creation, permission assignment or worker administration is added here.

## 20. Reports / Dashboards / Metrics
Paged configuration inventory and current state are operational views. Do not equate “Active” with applicable authority without effective-date, scope, category and action eligibility checks.

## 21. Configuration & Localization
Default currency comes from selected company. Numeric transport is invariant; display may localise without converting exact amount strings to JavaScript Number. Effective timestamps require explicit timezone handling. Global branding and components remain shared.

## 22. Non-Functional Requirements
Bounded lookups and pagination, cancellation of obsolete reads, no previous-record actions after denied navigation, accessible form labels, loading/empty/error/denied states. No production capability activation in this change.

## 23. Postconditions
Saved draft or authorised lifecycle transition is persisted and audited. Only successfully activated, currently applicable grants can contribute to subsequent approval eligibility.

## 24. Failure & Recovery
Refresh and compare after version conflict. Creation has no durable idempotency contract: after lost response show an unconfirmed result and reconcile the current list before manually retrying; never automatically repeat POST creation. Read retries do not infer command success.

## 25. Acceptance Criteria
Scoped users see names without HR administrator access; readers receive no mutation actions; maker/subject aliases cannot activate; limits round-trip `99999999999999.9999` exactly; excess four-place precision and overflow fail; legacy numeric callers remain accepted; no unknown policy fields are silently discarded.

## 26. Test Scenarios
Application JSON boundary tests cover string/numeric maximum, exponent form, precision/overflow and canonical round-trip. HTTP tests cover scoped names, bounded pages, omission of email/credentials, ambiguous login exclusion and current grants overriding stale claims. Existing domain, independent-worker and PostgreSQL transaction gates remain required. UI and real API browser evidence are separate release gates; none is claimed by source authorship.

## 27. Ownership / Dependencies
Procurement engineering owns the configuration adapter/domain contract. Core remains owner of referenced people and organisation dimensions. Product/engineering owners must approve any future change to revocation policy or authority delegation.

## 28. Open Decisions / Assumptions
Independent revocation is not currently required by the implemented policy. No exchange-rate authority, delegated authority, arbitrary scripts or alternate amount basis. Production activation, backend review, migration applicability, browser gates and operating approval remain with the release owner.
