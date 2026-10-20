# Procurement ↔ Finance budget reservation contract

**Status:** Draft integration contract; no Finance adapter is active  
**Owner:** Finance and Procurement engineering  
**Related:** PROC-03, ADR-002, module-connect issue #22

## Boundary

Finance owns budget balances, cost-centre budget owners, reservations, releases and actual expense. Procurement owns the purchase request and the decision to continue its buying workflow. A manually recorded Finance reference in Procurement is evidence of a review, not a verified reservation in the Finance ledger.

## Required Finance response

An authenticated Finance response must identify the tenant, legal entity, purchase request ID and submitted version, cost centre or project, amount basis and currency, outcome, reservation reference and status, budget owner subject ID, Finance decision ID, effective time and expiry or release conditions. Procurement rejects a response whose scope, version, amount or currency differs from the current request. It never derives the budget owner from a department manager.

## Commands and reconciliation

1. `CheckAndReserve` uses a stable key for one submitted request version. Repeating the key returns the same decision without a second commitment.
2. `GetReservation` verifies that an existing reservation is still active before opening or completing an approval case or issuing a PO.
3. `ReleaseReservation` uses a stable key and reason when demand is withdrawn, cancelled, returned or closed. Finance confirms the released amount and reference.
4. A reconciliation job compares open Procurement requests and POs with Finance reservations, flags missing, duplicate, stale or excess commitments, and does not silently repair differences.

Network retries must be bounded and observable. A timeout leaves Procurement in a pending state until Finance can confirm the result. Finance owns its posting and reservation transaction; Procurement stores an immutable response snapshot and correlation ID. Any later response is a new version of evidence.

## Activation gate

The current `budget-checks` API records a designated Finance user's external evidence. It remains useful for controlled preparation, but does not by itself enable binding approval cases or PO issue. Activation requires a verified Finance adapter or a separately approved manual control with independent confirmation, retained evidence and reconciliation. The Core approval adapter must recheck the current reservation and budget owner at the decision boundary.

## Worker independence correction — source increment, 20 October 2026

**Status:** authored for separate review and scheduled mapped/API testing; not an activation or production claim. No schema changes.

The recording account and the request creator account being different is insufficient. Both accounts may belong to one Core worker. Budget recording (including original-reference replay) and current approval Finance eligibility now resolve the verifier through the existing active canonical Core account/workforce mapping and deny a verifier whose worker matches the retained requester or maker. Current permissions, manual-evidence policy, freshness, request versions, amounts and reservation checks remain required. This does not assert an external Finance reservation-owner API exists.

**Retained evidence and scope:** ERP request creation fixes `RequesterWorkerId` to its actual maker worker; no on-behalf field is accepted by current create/update inputs. For unopened ERP requests, that retained requester excludes the original maker even after the creator login becomes inactive. A currently resolvable creator worker is checked additionally. Legacy/import/on-behalf records without separate retained maker evidence fail closed; no new worker identity is inferred or backfilled. For an opened request, the owning current case/submission must match tenant, company, request, submitted version, maker account and requester, with a valid schema-2 worker identity digest. Its retained maker is checked without requiring that historical account to remain active. The digest is a binding check on the owned record, not an authorization token.

**Atomicity and failure:** relational record writes read canonical identity through Core enlisted on the same existing request transaction/connection. Denial occurs before recording a Finance snapshot, audit, outbox or request mutation. Existing request-lock/CAS and reservation checks remain. No fake worker, new identity table, employee master, migration, reservation or Finance ledger write is introduced. The budget queue is still a discovery view; command permission and current worker independence are authoritative.

**Authored verification:** two active accounts for one worker cannot record funding; no-effect footprint includes request/lines, snapshots, audits and outbox. A historical alias snapshot cannot satisfy current Finance eligibility. A distinct active Finance worker may record after creator login deactivation. Rebinding that Finance account to the requester subsequently denies receipt replay and current eligibility. Inactive verifier, unsupported legacy/on-behalf provenance, wrong retained request/case/version/company, changed worker hash and schema-1 evidence are denied. A separate retained maker and requester both exclude the reviewer, while an independent worker passes without a current historical maker login.

**Pending gates:** distinct source review; focused `ProcurementFinanceWorkerIndependenceTests` and `ProcurementRequestHttpTests` (then relevant approval regressions); actual Npgsql mapped/runtime regression under restricted role before enabling workflow. In-memory fixtures and pure evidence checks are not PostgreSQL transaction or external Finance proof.

## Remaining retained-checker identity gap

The current worker-independence correction resolves the Finance recording subject's **current** Core worker; `BudgetCheckSnapshot` does not retain that worker at capture. A later account-to-worker reassignment must not be presented as proof of the historic checker. The separate [PROC-FIN-01-P02 retained checker identity contract](PROC-FIN-01-P02-RETAINED-FINANCE-CHECKER-IDENTITY.md) defines the bounded next schema, serializer compatibility, legacy-positive-use denial and testing increment. It is documentation only; no migration, immutable worker backfill, historic checker proof or external Finance completeness has been implemented by that plan.
