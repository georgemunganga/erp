# M3 advance-payment source checkpoint

Scope: pure Procurement calculation contracts only. This is not a completed PO advance workflow, database migration, API/UI integration, production release or company UAT.

`ManualPaymentOrderAdvanceCapacity` enforces explicit whole-order authority/currency and percentage terms, complete current commercial-version claims, reserved/disputed money, exact exclusions and unknown-settlement denial. The whole approved PO total may have eight decimals; percentage ceilings may have fourteen, preserved exactly. Individual existing payment amounts remain positive numeric(18,4). No hidden rounding or invented default advance percentage.

`ManualPaymentAdvanceAllocationCapacity` checks independently confirmed advances against actual same-order/company/branch/supplier/currency invoices. It limits allocations by both remaining advance and invoice capacity, including other advances applied to that bill; duplicate, inconsistent and incomplete intersections fail. Reversed projections retain evidence and release only their explicit allocation. Closed orders can reconcile already confirmed money; they cannot start new advances. These are trusted owning-store projections, not new editable records, a ledger, authorization or proof of complete storage reads.

## Executed verification

On the accepted c93+M2 source/dependency baseline, changed Application/tests compiled afresh; **2,065 Procurement tests passed**,0failed/0skipped, including37 new advance/allocation cases. This is not a fresh full B1c/main or Host-suite run. Earlier2058 and2063 successful checkpoints are retained separately before the final review additions. Source hashes exactly matched the final test inputs. SDK10.0.400, network-none, package cache read-only; unchanged dependency provenance remains in the accepted Core-routing release. Test/source verification index is private at `/home/amizo/erp-backups/procurement-module-completion-20261104_run01/advance-capacity-r3/verification-index.json`.

Tests cover source/supplier/company/branch/currency/version mismatch, whole-order splitting, exact percentage/four- and eight-decimal boundaries, disputes, missing confirmations, incomplete/duplicate allocation history, repeated identity across different histories, released states, unknown settlement review and amount-limit bypass. No new user-facing code or registered route changed, so no new Playwright/native-flow claim is made.

## Required next integration

1. Typed persisted invoice-versus-PO source without overloading an invoice ID; preserve existing records/FKs/snapshots/replay contracts.
2. Genuine migration, finite-role/readiness and retention/recovery acceptance. Changes to existing manual payment/catalog guards must be captured from actual schema, never guessed.
3. Reuse existing Core approval/record/independent-confirm path, add durable allocation/reversal and idempotency. Serializable aggregate reads/locks must cover both sources and prevent invoice double payment; no import or direct SQL route may bypass the invariant.
4. Extend the original payment/invoice forms and history, exact decimal transport, scoped sources and denied/recovery states. Actual API/native and preserved-screen browser tests are required before activation.
5. Company policy/users/limits and actual signed-in business acceptance. Original invoice-backed workflow remains separately accepted; no financial flags or production records changed in this checkpoint.

The28-section contract is [PROC-PAY-03](../features/PROC-PAY-03-ORDER-ADVANCES.md). Issue196 and overall145 stay open. Core owns identity/approvals; Finance remains the future owner of AP, bank execution and accounting. No employee prerequisite, Finance duplication or replacement UI is added.
