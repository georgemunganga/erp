# PROC-PAY-01 — Manual payment workflow

Status: implementation in progress; not deployed. Supersedes the earlier assumption that absence of Finance blocks all payment recording. Standard: GW-ENG-STD-001, Tier 3. Module → Payables → Manual Payment → operation. No new bank execution, ledger or authentication service.

The historical filename is retained for existing links. The canonical feature ID is `PROC-PAY-01`; `PROC-12` remains the supplier-portal feature in the module map. Domain/storage/Core-clock implementation does not mean a live payment API or completed UI integration.

## 1. Feature Identification
PROC-PAY-01; Procurement manual payment requests, assigned review and external-payment recording.
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
Exact positive amount, at most four fractional places, same supplier/invoice/currency, one approved amount per request. Creator cannot approve own request; assigned recorder cannot confirm own payment. A creator cannot be assigned as approver and recorder cannot be assigned as confirmer. Approval alone creates no payment. Record must equal approved amount; smaller installments use separate requests. The current Core company business date bounds the entered payment date; UTC server timestamps record approval and recording actions. Every mutation requires current CAS version; denied actions produce no payment mutation.

Registered capacity reserves submitted, approved, recorded, confirmed and disputed manual requests. Draft, rejected and cancelled requests do not reserve money. Noncancelled captured credit claims conservatively reserve capacity without claiming that Finance posted/applied a credit. Imported settlements are included; mixed manual execution and imported outcomes require explicit reconciliation until their owning link exists. Never clamp an inconsistent negative balance to zero, round away a discrepancy or assume an empty Finance projection proves no external payment.

Capacity exclusions are bound to the exact updated payment request and are forbidden for creation. Credit capture/correction uses the same calculation under the same invoice lock and serializable transaction. Any retained manual request seals the original invoice against correction/cancellation, including cancelled/rejected payment history; releasing capacity does not erase historical source facts. PostgreSQL microsecond normalization applies to immutable timestamp comparisons, while a real microsecond change remains forbidden.
## 12. Data Fields & Ownership
Procurement owns request ID, optional title (200 characters), tenant/company/branch, invoice/supplier refs, currency/amount, creator and assigned Core principals, status/version, approval decision, actual recorded date/method/reference, confirmation/dispute reason and audit. Core owns account/grants/branch. Finance owns posting, bank reconciliation and imported outcomes. No raw banking credentials.
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
Core company currency/default branch and trusted company business date; module-specific manual-payment enablement and assignment rules. ISO dates/exact decimal storage, localized display. No hard-coded Lusaka ID, time zone, currency or spend threshold.
## 22. Non-Functional Requirements
Atomic transitions, concurrency control, scoped pagination, actionable errors, accessible shared controls and retained recovery receipts. Never rely on frontend permission hiding.
## 23. Postconditions
Accepted transition advances version once and retains original history. A manual confirmation remains labelled manual. Invoice Finance-paid/posting fields are not changed.
## 24. Failure & Recovery
Rollback all business effects on validation/transaction failure. Unknown response is reconciled by receipt GET; no new payment/key or automatic POST. Migration Down veto must preserve any retained payment/audit/receipt evidence. No deletion of existing rehearsals or original migration artifacts.

Once manual-payment data exists, application rollback must retain the payment-aware credit/invoice guards or disable affected writes. An old P24 binary must not receive write traffic over retained payment records. Before first activation, upgrade every writer and drain transactions begun before P27; table availability alone is not workflow readiness.
## 25. Acceptance Criteria
A system-only user completes a properly assigned manual request through independent confirmation; absent Finance and attachments do not block it. Self-approval, self-confirmation, unauthorized branch/account/action, stale version and overpayment are denied. Reload reproduces actual persisted values and history. No AP, payment rail or Finance-synced row is fabricated.
## 26. Test Scenarios
Actual domain rule tests; mapped authorization/API and owning-store tests; genuine migration and PostgreSQL constraints/concurrency/receipt tests; actual Playwright create/edit/submit/approve/record/confirm/reload, empty/denied/conflict and interrupted-response recovery. Mocked browser tests are labelled separately.
## 27. Ownership / Dependencies
Procurement owns manual business decisions; Core provides real identity/access/workflow/audit infrastructure. Future Finance adapter owns reconciliation. Preserve P24 published baseline; no P25 dependency is required for this slice.
## 28. Open Decisions / Assumptions
User authorizes manual recording without Finance integration or mandatory file evidence. Current implementation must keep incomplete capabilities explicit; no claim of complete module or deployed workflow until schema/API/UI/browser gates pass. Assignment UI must consume current Core user capabilities; no hard-coded identities. Initial payment rail remains external/manual.

Next implementation milestone: register the payment action catalogue, add a publishable typed payment-review policy through existing policy administration, retain exact links between manual transitions and shared Core cases/assignments/decisions, retain the trusted Core date observation, and implement scoped/idempotent APIs. Then bind the existing restored Payments/Bills presentation and verify normal-cookie API/PostgreSQL/Playwright journeys. No arbitrary policy ID, reused purchase-request policy, employee requirement, replacement frontend or fabricated Finance outcome is permitted. UUIDs are truthful initial record references; transaction numbering must reuse the ERP numbering capability when provided. Multi-invoice allocations, advances, refunds and batch execution need their own controlled contracts before their existing UI actions become live.


Implementation checkpoint (23 October 2026): merged PR #43 is the verified storage/capacity/Core-clock foundation. Draft PR #44 adds the eight Core payment actions without ordinary bootstrap grants; strict schema-2 `MANUAL_PAYMENT_REVIEW` policy publication through the existing policy lifecycle; actual shared Core approval/confirmation cases and linked decisions; retained company-date observations; and genuine P28 schema generation. Published review policies require explicit positive exact currency limits, applied to the captured whole invoice so installments cannot evade the ceiling. Core maker cancellation uses the same controlled shared workflow and persisted decision history.

P28 source integrity guards require companion records on consequential transitions, exact source/Core linkage and publication lineage, append-only evidence, submitted-title immutability and retained-evidence downgrade veto. These supplement current server authorization; they do not make a database owner unable to alter Core records or authenticate a synthetic principal. The application must revalidate current Core authority, source pins and the trusted company clock. The private governance probe is distinct from the previously accepted P27 foundation probe; never relabel their source versions or claim a pure/synthetic store test proves HTTP authorization.

The original Payments layout stays the UI contract. Initial live scope is one invoice per request; Title and notes remain draft fields, while payment date/method/reference are entered only when recording an already-made external payment. Unsupported payment account, multi-bill, batch bank execution and filename-only upload controls cannot claim persistence. No new UI is needed to simplify those contracts.

### P28 finite Core column admission correction — 24 October 2026 (source only)

This bounded correction reuses the accepted P25 Core identity read projection rather
than requiring table-wide SELECT on seven HRM identity/organisation tables. It adds
only the fields used by payment presentation and the owning Core date provider:
`LocalUser.DisplayName` for assignee/history labels, `WorkLocation.Code/Name` for the
branch picker, and `LegalEntity.TimeZoneId` for the persisted company clock.
Canonical account/role/tenant/branch and optional worker-alias queries remain owned
by the existing Core helpers; optional employment does not become a prerequisite.

The owning HrmDbContext supplies model metadata on the existing execution connection;
it performs no identity SELECT, opens no second connection and creates no independent
transaction. Each mapped physical column is a separate required
`has_column_privilege(...,'SELECT')` AND term, executed on the original read-only
inspection transaction. Credential, email, salary and whole-table SELECT are not
required by this projection. The HRM migration-history SELECT remains separate.
No grants, role memberships, schema fingerprints, policies, flags or mutation guards
are changed. Existing no-HRM-writes (including column grants), no ownership/elevated
role assumptions and append-only Core/Procurement evidence checks still apply.

Authored acceptance: twelve owning-model/SQL-contract cases cover the complete
column-only projection, eight individual missing-column terms, exact reuse plus the
four justified added fields, unchanged write/owner/elevated-role denials, and missing
connection/default-off behavior. Existing mapped activation negatives remain intact.
These cases are authored, not run. They establish no actual PostgreSQL role admission.

Before release, the independent finite-role rehearsal must grant only the reviewed
columns (no table SELECT), demonstrate successful physical admission with every
other prerequisite satisfied, revoke one required column and observe refusal, then
separately prove HRM column UPDATE, table ownership and elevated SET membership
continue to refuse admission. Restore every injected fault inside the private
rehearsal and retain unchanged owner/ACL/data evidence. No production role change,
workflow activation, payment/Finance availability or runtime success is implied.

P30 schema compatibility is a separate exact fingerprint profile. The existing P28
fingerprint includes the entire operational identity CHECK; P30 commercial workflows
add `SOURCING_PUBLISH` to that owning constraint, so accepting only the old hash would
make payment governance unavailable on a valid P30 database. The source selector now
requires the actual P28 migration and selects a distinct P30 profile when migration
`20261023222748_P30PrincipalCommercialWorkflows` is present. It does not remove the
constraint, normalize SQL, relax any other columns/functions/triggers, or accept the
P28 fingerprint on a P30 history. Pending migrations/model checks remain unchanged.

The P30 expected hash is intentionally empty until root captures this exact
`FingerprintSql` on a stable genuinely migrated private P30 database and reviews the
protected raw catalog provenance. Consequently this source checkpoint stays closed
on P30, including after the column correction. Two additional authored cases prove
history-specific selection and the empty-capture hold; they are unrun. On actual
capture, replace the empty constant and its temporary hold assertion with the exact
captured hash and positive/tamper cases, retaining P28/P30 cross-profile denials.
