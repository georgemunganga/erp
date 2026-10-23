# PROC-12 manual payment foundation handoff

Status: source-only foundation. No route, mapping, migration, UI activation, payment execution, AP posting, or Finance outcome is registered by this change.

## Implemented in source

- A Procurement-owned manual request, append-only transition record and actor-scoped command-receipt shape, separate from `FinancePaymentStatusRef`.
- Pure transitions for create, revise, submit, assigned approval/return/rejection, external payment recording, independent confirmation, cancellation and dispute.
- Current Core principal/action/company/branch checks, current assignment checks at assignment and action time, exact amount and invoice/currency bindings, optimistic version checks and mandatory external reference/date/method.
- Pure rule tests for the successful system-only account path and critical denials. These tests have **not been run** in this source-only preparation.

## Required before API or runtime activation

1. Map the new records with additive EF migration and database constraints, including unique request number if added, positive exact `numeric(18,4)` amount, valid state, scoped foreign keys, unique `(tenant, company, actor principal, command key)` receipt, and append-only history. Generate and review the migration/snapshot in the owning release sequence; do not hand-edit an already published migration.
2. Resolve `ManualPaymentActor` and `ManualPaymentAssignee` from current Core accounts, current effective action grants, company and authorized branch at every command. Never accept the projections from request JSON. Implement scoped list/detail/assignment lookups and reject a missing or All-branches write selection.
3. In one serializable or equivalent locked transaction, verify the original invoice, supplier, branch, currency and payment eligibility, then calculate `RemainingExcludingThisRequest` from all competing manual requests and imported Finance settlements. Lock a stable invoice row before the aggregate read and write; retry serialization conflicts safely, not by creating a second payment. Define credit/dispute effects in the owner policy before calculating capacity. This adapter is absent from the pure foundation.
4. Add versioned HTTP commands and a single atomic transaction for request update, transition history, shared Core audit, command receipt and notification outbox. Reuse Core workflow/assignment and audit services where they own those capabilities; this pure state guard is not a replacement workflow engine. CAS must be enforced by the database update predicate. Return the exact recorded JSON response for matching actor-scoped idempotency keys; reject changed payloads. Unknown client outcomes use GET acknowledgement only.
5. Add attachment linking only when the real shared document service can verify record ownership and purpose. The attachment remains optional.
6. Add mapped PostgreSQL/API concurrency, scope, receipt and migration tests, plus browser tests against the existing Payments presentation. Keep the manual label distinct from Finance-synced outcomes. Publish only after the release gates pass.

No bank rail, supplier ledger, AP posting, imported Finance status, refund or reconciliation is implied by `ConfirmedManually`.
