# Remaining Procurement implementation and acceptance

Current published prerequisites: ERP `ae32466e2959b79e62ae1d0a3b457c58110389ae` (including pure M3 foundation PR197) and UI `2b3357d2de3c383ea2926264abc701b1564a5895` (company-context/settings PR108/109), or reviewed descendants. Runtime API remains the separately qualified c93/M2 release,239 tables. Never deploy all of main while held B1c migration/readiness remains unresolved.

The complete module is **not finished**. This register separates code, technical release and company acceptance; completed draft CRUD does not complete an entire lifecycle.

| Milestone | Actual checkpoint | Remaining exit evidence |
| --- | --- | --- |
| Core identities/context and shared settings | Core-user/no-employee owner-account sign-in and real company context verified | Actual staff roles, scopes, department governance and independent authorities configured by the business |
| Supplier/item/request draft operations | Real owner-cookie vendor create/edit/reload/withdraw; item create/edit with native inactive-state proof; request create/edit/reload/withdraw and different-selected-branch404 verified cumulatively | Broader user/assigned-scope cases and actual business records/owner sign-off; retain synthetic test history |
| Core workflow directory and explicit PR routing | M1/M2 deployed; named Core user/role and requester-dependent source tested | Real supervisor/executive chain, finite limits, manual funding/sourcing policy, independent publication and actual signed-in approval chain |
| M3a PO advance and invoice allocation calculations | PR197 merged;2065 Procurement tests on c93+M2 plus37 new cases | Pure contracts are not registered payment APIs or persistence |
| M3b persisted typed payment sources | Next owned Manus milestone196 | Preserve invoice records/FKs/snapshots/replay; real PO references, immutable source versions, allocation/reversal, genuine generated migration, finite-role/readiness and recovery proof |
| M3c payment commands and capacity | Existing invoice-backed external recording/independent confirmation is separate | Reuse Core approval cases, current action/scope, exact monetary/source policy, aggregate locks/idempotency and complete cross-source evidence; no duplicate invoice/advance payment |
| M3d original payment/invoice UI | Imported screens retained | Wire PO source/remaining/allocation/history and failure/recovery controls to accepted APIs, exact decimal strings; no replacement layout |
| M3e native/browser/production qualification | Current source foundation has no native-flow claim | Real Core-cookie/SQL proof, original-UI Playwright, races, old-invoice compatibility, downgrade retention and independently reviewed paired rollout |
| Basic authorised buying | Bounded earlier private implementation evidence exists | Actual request→independent approval→linked PO→approval/issue→partial receipt/service acceptance→invoice→Accounts test with company configuration; not inferred from anonymous smoke |
| RFx, quotations, sealed bids/evaluation/award | Earlier quotation/round drafts and partial review are documented | Finish held B1c readiness, formal participation/deadline/opening, individual scoring, independent award and typed award-to-PO, including actual native/UI/recovery evidence |
| Contracts/frameworks/recurring/planning | Draft/planning capabilities have bounded prior evidence | Complete each documented lifecycle, ceilings/drawdowns/amendments/renewals, recurring scheduling and real approval/receipt/invoice links; do not mark drafts as full lifecycle |
| Receiving/inspection/returns/credits | Existing receipt/service and return/credit work has separate bounded evidence | Full partial/reject/reverse/replace/credit and retained-source/race acceptance; Inventory/Asset handoff events without duplicating their ledgers |
| Supplier portal and collaboration | Imported UI is not proof of external participation | Supplier-scoped identity/access, onboarding/quotation/PO/invoice participation, documents/comments and leakage/security tests |
| Shared documents/notifications/integrations | Core owns transport, scanning, notifications and event contracts | Actual configured adapters, delivery/retry/access/retention and required-document admission. Optional OCR/e-sign/screening are conditional, never fake dependencies or completed integrations |
| Reports, analytics, imports/exports | Live counts and bounded adapters exist | Requirement-level metric definitions/reconciliation/scoped exports, supported UI filters, failures and remaining reporting integration; no invented Finance balances |
| Release/operations/company handover | API healthy; UI108/109 deployed; actual bounded owner CRUD evidence retained | Durable disk headroom, tested backup/recovery, support ownership, complete scope/security/failure matrix and actual owner acceptance |

## Financial integration contract for Manus196

Use the existing shared Core engine and manual-payment record/decision/receipt/record/confirm workflow. Do not use a fake invoice or put a PO UUID into an invoice FK. Design the typed source migration and compatibility deliberately, preserving historic JSON bytes and command recovery. Generated schema must be verified, not handwritten as a fake scaffold or accepted with blank fingerprints.

The M3 allocation calculator expects invoice capacity **before the separately supplied advance allocations**. Keep explicit before/after projections in the adapter; never subtract the same allocation twice. Current pure claims are owning-store projections, not browser DTOs. Complete source/claim histories and correct lock ordering are mandatory. Current commercial-version mismatch fails for review; do not silently drop historical payments when an order is amended. Currency/defaults come from Core; actual company policy and authorities cannot be invented. An external payment record is neither a bank transfer nor an AP/GL posting.

Start with feature PROC-PAY-03 and merged PR197 source. Return a reviewable PR with schema/API/native/original-UI evidence. Root retains production/runtime/native credentials and deployment. No acknowledgment or active Manus work is inferred from this assignment.

## Business and operational inputs

Actual approvers/independent Accounts duties and approved limits/funding/sourcing policy are still required before activation. An asynchronous request for those inputs is pending. Finance is a future module; Procurement retains controlled manual evidence and durable integration contracts.

The root filesystem has about200MiB free after safe cache checks. Large existing caches mostly share files and were retained; only unshared disposable files were removed. Protected publications, native databases, customer records and rollback artifacts were not deleted. Further large migration/restore jobs need capacity or a separate reviewed retention decision; no destructive cleanup is inferred from a feature implementation request.

Issues145,190,192 and196 remain open until their actual acceptance conditions are met. No full-module completion or owner sign-off is claimed.
