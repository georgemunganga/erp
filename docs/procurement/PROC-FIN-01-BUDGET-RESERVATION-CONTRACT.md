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
