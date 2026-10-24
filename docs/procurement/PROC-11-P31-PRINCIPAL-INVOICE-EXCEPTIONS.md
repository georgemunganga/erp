# PROC-11 P31 — Principal invoice discrepancy and correction

**Status (24 October 2026):** typed discrepancy and correction routes are source-authored behind default-off P31 readiness. The genuine thirteen-table migration is generated but not privately applied, captured or runtime verified. Positive comparison, AP posting and payment are not enabled. This is an additive schema-2 workflow; existing worker-era `InvoiceException` rows and keys remain unchanged.

**Source checkpoint:** Typed discrepancy/action records, exact owner FKs, a known-blocker Compare path with no claim, scoped list/action/recovery routes and correction remap/retry are authored on an isolated branch. They are not registered. The genuine additive EF migration, database deferred guards, finite privileges, mapped tests and actual PostgreSQL/normal-cookie proof remain required. An unavailable owner or a failed source read still returns unavailable and does not fabricate a discrepancy or zero capacity.

## 1. Feature Identification
Principal invoice discrepancy from a typed issued PO2/original bill comparison.
## 2. Purpose and Business Outcome
Give each known comparison discrepancy a durable owner, reason and correction trail so a failed comparison can be worked and retried without silently losing evidence.
## 3. Scope and Exclusions
Record typed Procurement issues and corrective actions. Exclude Finance posting, tax determination, bank payment, automatic credit ledger, stock changes and silent tolerance overrides.
## 4. Parent and Related Features
PROC-11 P31 mapping, original-unit Core review and comparison; PROC-07 issued PO2; PROC-09 receiving; Core documents/approval; Finance/AP remains external.
## 5. Actors and Participants
Current Core invoice matcher, assigned exception owner, original-unit reviewer where mapping changes, procurement auditor. Worker links are optional denial-only aliases.
## 6. Entry Points and Triggers
Known `PrincipalInvoiceComparisonRules.Evaluate` blocker, owner/duplicate/source change, reviewer Return/Reject, or a documented correction request.
## 7. Preconditions
Current tenant/company/branch/action, original invoice and typed mapping pins, genuine P31 schema/readiness and scoped physical source. Missing evidence is unknown, never an inferred zero or auto approval.
## 8. Workflow
Compare → durable blocked result and issue(s) without a claim → owner reviews original and source → choose corrected invoice, corrected mapping, PO amendment/receipt correction, supplier credit reference or justified resolution → revalidate under current policy and source → new review if mapping/original changed → recompare → close only issues proved resolved. Separate unknown store failures retain the command locator for GET-only recovery.
## 9. Status / State Transitions
Issue Open → InReview → AwaitingSupplier/Receiving/POCorrection → ReadyToRetry → Resolved or CancelledWithReason. Invoice typed state Reviewed → ComparisonBlocked → Mapped or Reviewed after controlled correction → Compared. Never transition directly from blocked to Compared without a new complete comparison.
## 10. Permissions and Data Scope
`procurement-invoice-read` and `procurement-invoice-match` for current scoped records; assigned owner and distinct correction actions are required for mutation. Filter tenant/company/authorized branch before count/page/detail. Do not grant general PO, supplier, Finance or document read to a purpose reviewer.
## 11. Business Rules
One issue row per deterministic code/original line/blocked comparison; no duplicate open issue on exact replay. Every resolution records actual evidence reference, reason, actor and time. A credit reference is commercial context, not a posted credit. A corrected mapping invalidates the old review; no reused unit attestation. Comparison claims require all issues current-resolved and complete locked owner history.
## 12. Data Fields and Ownership
Add typed `PrincipalInvoiceDiscrepancy`: tenant/company, invoice/mapping/issued source, original line, blocked comparison ID, deterministic issue key/code, owner Core principal and optional alias, state, due date, created/updated times. Add append-only `PrincipalInvoiceDiscrepancyAction`: issue, action, reason, evidence/reference, actor and timestamp, command key and exact source/version pins. Procurement owns these; Core owns account and document bytes; Finance owns AP/payment.
## 13. Screens and Data Views
Reuse Bills detail Overview review history and existing invoice exception presentation. Show issues with plain language, owner, next action and retained history. Assigned exception queue should use the existing Approvals/list shell, not a new demo page.
## 14. User Experience / Human Behaviour
Show the discrepancy and what must be corrected. Saving a note alone does not resolve. Expose uncertainty and original-key recovery; never show “matched” after a rejected or unknown command.
## 15. Notifications and Communications
Notify assigned owner and requester on creation, reassignment, correction needed and resolution through shared notification service; avoid document/bank contents in messages.
## 16. Documents and Attachments
Retain actual original document version/hash and purpose scope; corrections link new approved document versions instead of overwriting originals. Malware/retention remain Core owned.
## 17. Exceptions and Edge Cases
Unknown owner coverage, mixed legacy/P31 evidence, stale invoice, revoked branch/permission, changed PO/receipt, duplicate invoice, missing document, concurrent credit or claim, withdrawn supplier and changed policy all fail closed.
## 18. Integrations / APIs / Events / Sandbox
Typed list/detail/action routes and actor-owned command receipts, default off until P31 DDL/readiness/finite roles. Publish Procurement exception events only after owned transaction commits. No Finance posting or bank event.
## 19. Audit / Security / Privacy
Append-only issue/action history, exact actor and source pins, separate technical error log. No browser command payload or original document text stored in recovery markers.
## 20. Reports / Dashboards / Metrics
Open/aging by owner, category and code; retry and resolution time. Separate unresolved from unavailable. Never count blocked comparisons as approved spend or paid bills.
## 21. Configuration and Localization
Use existing published matching rules and Core company locale; no hard-coded thresholds or tolerance. Display exact currency amounts as strings.
## 22. Non-Functional Requirements
Serializable source lock, idempotency/CAS, branch filter before count/page, strict bounded pages, no broad data joins, retry after explicit GET recovery only.
## 23. Postconditions
Blocked attempt has a durable issue and zero new claims; resolved issue retains its full trail. Successful new comparison binds its own policy, original, source and owner evidence.
## 24. Failure and Recovery
Definite validation may be corrected. Network/409/5xx or malformed acknowledgement leaves the original command locked; GET the same actor/key receipt and inspect current typed state. No generated replacement command on refresh.
## 25. Acceptance Criteria
Known mismatch creates a scoped owned issue and no claim; correction preserves prior mapping/review/issue; independent original review repeats when needed; same-key replay has one effect; cross-branch/tenant/actor reads deny; no Finance status changes.
## 26. Test Scenarios
Positive mismatch→correction→new review→comparison; mismatch duplicate replay; changed owner/branch, missing source, stale version, changed document, insufficient credit history and concurrent claim; raw cross-owner FK/null/receipt tests plus actual PostgreSQL and normal-cookie UI proof.
## 27. Ownership / Dependencies
Procurement owns issue and commercial correction. Core owns identity, approval, original document bytes and retained document uses. Supplier credits and AP remain separate contracts. P30 readiness plus private P31 migration rehearsal, physical capture, finite privileges, genuine scanner and HTTP proof are prerequisites.
## 28. Open Decisions / Assumptions
Specify exception owner matrix/SLA and authorized correction methods per company, and whether a credit correction requires Finance confirmation. Until configured and proven, the UI presents a blocker and no release-ready claim.
