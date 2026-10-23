# PROC-12 — Manual payment workflow

Status: implementation in progress; not deployed. Supersedes the earlier assumption that absence of Finance blocks all payment recording. Standard: GW-ENG-STD-001, Tier 3. Module → Payables → Manual Payment → operation. No new bank execution, ledger or authentication service.

## 1. Feature Identification
PROC-12; Procurement manual payment requests, assigned review and external-payment recording.
## 2. Purpose & Business Outcome
An authorized Core user can request payment; assigned users approve, record an actual payment made outside the ERP, and independently confirm the record before Finance integration exists.
## 3. Scope & Exclusions
Own manual records, versioned drafts, assignments, decisions, recorded references and correction history. No bank transfer, payment credentials, AP posting or bank reconciliation. Attachments are optional. Existing FinancePaymentStatusRef remains exclusively Finance-synced evidence.
## 4. Parent & Related Features
Payables; invoices; Core identity, scoped permissions, workflow and audit. Reuse existing payment screens/components rather than introducing another frontend.
## 5. Actors & Participants
Requester, assigned approver, assigned payment recorder, assigned confirmer. These are Core account/action assignments, not employment designations. One person can have multiple duties subject to maker/checker restrictions.
## 6. Entry Points & Triggers
Payments/New Payment or a bill's request-payment action; assigned approval and confirmation inbox tasks.
## 7. Preconditions
Current account, company, authorized branch and action; real visible same-company supplier/invoice; exact currency and amount. Assignment accounts must be active and authorized at assignment and execution. No employee requirement. API must validate original invoice eligibility and aggregate outstanding amount, including other manual requests and imported Finance settlements, under a database lock.
## 8. Workflow
Draft → Pending Approval → Approved → Payment Recorded → Confirmed Manually. Return sends pending approval to Draft; reject/cancel terminate unexecuted requests. The recorder enters payment date, method and external reference after actual payment. Confirmer checks details without requiring an uploaded attachment. Confirmation is manual evidence, not a Finance posting.
## 9. Status / State Transitions
Draft, PendingApproval, Approved, PaymentRecorded, ConfirmedManually, Rejected, Cancelled, Disputed. PaymentRecorded can be disputed; disputed/confirmed history is retained, never deleted or silently overwritten. Refunds/corrections require a separately linked controlled record; disputing never claims money returned.
## 10. Permissions & Data Scope
Core actions `procurement-payment-read/create/edit/submit/approve/record/confirm/cancel` (each suffix forms a separate hyphenated action ID). Company and allowed branch constraints apply to list/detail/mutation/search/export and every assignment. Header All branches is a read context, not permission. Transaction branch is required. Initial branch defaults from authorized Core settings; never manufacture access.
## 11. Business Rules
Exact positive amount, at most four fractional places, same supplier/invoice/currency, one approved amount per request. Creator cannot approve own request; assigned recorder cannot confirm own payment. A creator cannot be assigned as approver and recorder cannot be assigned as confirmer. Approval alone creates no payment. Record must equal approved amount; smaller installments use separate requests. Current server date bounds payment date. Every mutation requires current CAS version; denied actions produce no payment mutation.
## 12. Data Fields & Ownership
Procurement owns request ID, tenant/company/branch, invoice/supplier refs, currency/amount, creator and assigned Core principals, status/version, approval decision, actual recorded date/method/reference, confirmation/dispute reason and audit. Core owns account/grants/branch. Finance owns posting, bank reconciliation and imported outcomes. No raw banking credentials.
## 13. Screens & Data Views
Keep Payments and Batch Payments navigation. Reuse existing list, empty state, detail and editor. Display manual records as Manual; display imported outcomes as Finance-synced. Draft, approval, record and confirm actions depend on server action flags. Unavailable batch bank execution must not disable individual manual recording.
## 14. User Experience / Human Behaviour
Plain labels: Request payment, Send for approval, Record payment, Confirm payment, Report a problem. Explain Record payment saves details of a payment already made; it does not send money. Preserve inputs after validation/conflict. Show a company/branch summary before save.
## 15. Notifications & Communications
Assigned approval, approved-to-record, recorded-awaiting-confirmation, returned/rejected/disputed. Use Core notification/outbox delivery, not notification success as transaction truth.
## 16. Documents & Attachments
Optional shared document reference. No fake upload or requirement for unavailable attachment integration. Reference/date/method and reviewer decisions remain required; document access remains Core-controlled.
## 17. Exceptions & Edge Cases
Permission/account revocation, branch reassignment, stale version, missing invoice, duplicate command, ambiguous response, invoice dispute/credit, simultaneous payments, future date, cross-currency and partial installment. No implicit reassignment, fallback create or automatic retry.
## 18. Integrations / APIs / Events / Sandbox
Planned /api/procurement/v1/manual-payments list/detail/create/revise and explicit submit/approve/return/reject/record/confirm/dispute/cancel commands. Unique actor-scoped command receipt and acknowledgement lookup bind original JSON response to request/version. Same transaction persists record, audit, receipt and outbox. Future Finance imports reconcile by original manual-payment ID, never create a second bank payment. Synthetic private sandbox only until reviewed acceptance.
## 19. Audit / Security / Privacy
Append-only decisions and original values; server principal/time/scope, delegation if used and correlation ID. No physical deletion, anonymous action, employee prerequisite, broad admin bypass or browser-local success. Restrict references and optional documents to authorized participants/scopes.
## 20. Reports / Dashboards / Metrics
Requested/approved/recorded/confirmed totals by branch, supplier, currency and period; pending tasks and age; manual and Finance totals must not double count matched outcomes. No estimated ledger balance.
## 21. Configuration & Localization
Core company currency/default branch; module-specific manual-payment enablement and assignment rules. ISO dates/exact decimal storage, localized display. No hard-coded Lusaka ID, currency or spend threshold.
## 22. Non-Functional Requirements
Atomic transitions, concurrency control, scoped pagination, actionable errors, accessible shared controls and retained recovery receipts. Never rely on frontend permission hiding.
## 23. Postconditions
Accepted transition advances version once and retains original history. A manual confirmation remains labelled manual. Invoice Finance-paid/posting fields are not changed.
## 24. Failure & Recovery
Rollback all business effects on validation/transaction failure. Unknown response is reconciled by receipt GET; no new payment/key or automatic POST. Migration Down veto must preserve any retained payment/audit/receipt evidence. No deletion of existing rehearsals or original migration artifacts.
## 25. Acceptance Criteria
A system-only user completes a properly assigned manual request through independent confirmation; absent Finance and attachments do not block it. Self-approval, self-confirmation, unauthorized branch/account/action, stale version and overpayment are denied. Reload reproduces actual persisted values and history. No AP, payment rail or Finance-synced row is fabricated.
## 26. Test Scenarios
Actual domain rule tests; mapped authorization/API and owning-store tests; genuine migration and PostgreSQL constraints/concurrency/receipt tests; actual Playwright create/edit/submit/approve/record/confirm/reload, empty/denied/conflict and interrupted-response recovery. Mocked browser tests are labelled separately.
## 27. Ownership / Dependencies
Procurement owns manual business decisions; Core provides real identity/access/workflow/audit infrastructure. Future Finance adapter owns reconciliation. Preserve P24 published baseline; no P25 dependency is required for this slice.
## 28. Open Decisions / Assumptions
User authorizes manual recording without Finance integration or mandatory file evidence. Current implementation must keep incomplete capabilities explicit; no claim of complete module or deployed workflow until schema/API/UI/browser gates pass. Assignment UI must consume current Core user capabilities; no hard-coded identities. Initial payment rail remains external/manual.
