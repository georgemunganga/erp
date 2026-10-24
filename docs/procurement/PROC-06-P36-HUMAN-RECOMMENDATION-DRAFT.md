# PROC-06 P36 human recommendation Draft

**Status:** source design and pure validation only. The P32 migration, runtime
readiness, and private proof are prerequisites. This file does not activate a
recommendation, award, supplier message, purchase order, or Finance posting.

## 1–4. Purpose, owner, scope and trigger

Procurement owns an internal, human-authored proposal after an independently
opened P32 commercial round. A current Core account with the explicit
`procurement-sourcing-recommend-draft` action and current company/branch reach
may propose suppliers and quantities per sourcing line. The action has an empty
bootstrap grant. The actor must also be an authorized named opened-offer reader.
The draft does not rank offers or confer award authority.

## 5–8. Inputs, retained source and authority

The server reloads the exact approved round, closing seal, opening receipt,
opened offer set, original offer snapshots and prepared evaluation definition
under the existing P32 source and Core scope fences. It pins round/opening/
offer-set/evaluation SHA values in the draft header. Each proposed line pins
the selected immutable offer SHA, supplier and sourcing line; the client
cannot supply a supplier or source digest as authority. Assessment coverage
may be described as incomplete and is never converted into a pass or score.

## 9–12. State, permissions, rules and data

Only `Draft` exists. Create and full-replacement Revise append immutable
revisions and immutable line sets. Omitting a proposed line in a new revision
removes it from the current proposal while preserving history. CAS requires
the exact prior revision and SHA. The maker owns changes; current Core action,
company, branch and retained alias checks are required each time. Quantities
are positive exact 18,4 text and cannot exceed the selected offer line or the
source line in aggregate. Proposed positive values are exact 38,8 text in the
round currency; they are human proposals, not verified quote pricing, budget,
Finance authority or computed savings. Every line carries a nonempty rationale.

Legacy `BidEvaluation` and `SourcingAward` cannot hold the opening, offer-set,
plan, line and command evidence without false ownership. Four additive typed
families are proposed: draft head, full immutable revision, immutable revision
lines and original actor-owned command receipt. The model uses exact scoped
owner keys, explicit short constraint names, append-only guards and first
retained-row Down veto in a later genuine P36 migration. No hand-edited EF
snapshot or published migration is permitted.

## 13–24. Presentation, audit, integration and recovery

The existing RFQ five-tab detail and opened-offer purpose reader remain the
presentation basis. The API is a new purpose route under `commercial-rounds`
for Create, Revise, own list/detail/history and original UUID-key GET recovery.
The response is an actor-owned acknowledgement without competitor bodies.
Writes use the existing serializable Procurement/Core transaction, scoped
current actor, CAS, exact request fingerprint, same-save audit and a metadata-
only outbox event. A transport-uncertain command is resolved through its GET
receipt; there is no guessed second POST. Reads filter authorized tenant,
company and branch before count/page. A draft never mutates PR allocations or
supplier/PO/Finance rows.

## 25–28. Acceptance, holds and open decisions

Source tests must cover split quantities, decimal maximums, wrong source or
currency, stale CAS, wrong actor/branch, same-key changed body, no allocation
effects and retained history. Runtime acceptance later needs genuine additive
migration Up/Down/Up, physical finite-role readiness, normal Core accounts,
current alias revocation, actual opened-source and receipt proof, GET recovery
after lost response, and owner reload. Submit, recommendation publication,
ranking, mandatory-fail interpretation, tie breaking, award approval and PO
conversion require separate adopted business policy and owner contracts.
