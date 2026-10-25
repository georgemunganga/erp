# PROC-06 P36 human recommendation Draft

**Status:** integrated source only; tests authored, not executed. P32 and P35
have genuine migration baselines. P36 now has genuine EF metadata but no physical
readiness capture. Its separate option defaults false and admission remains
unavailable. No award or purchase-order authority is introduced.

## 1. Business purpose

Record a human-authored internal proposal after a sealed round has been
independently opened. No algorithm selects a winner or computes a ranking.

## 2. Module owner

Procurement owns recommendation records. Core owns current accounts, roles,
branches, optional worker links, document retention and approval workflows.

## 3. Existing workflow

Reuse P32 retained rounds, prepared plans, opened offers and purpose readers.
P30 manual quotation Draft bytes cannot become sealed offers by adoption.

## 4. Trigger and scope

A current named opened-offer reader explicitly granted
`procurement-sourcing-recommend-draft` may create one own Draft per round.
No employee record is required. The new action has empty bootstrap grants.

## 5. Inputs

POST `/commercial-rounds/{roundId}/recommendation-drafts` accepts `draftId`,
`expectedRevision`, `expectedSnapshotSha256` and up to 500 proposed `lines`.
Each selection supplies offer/line IDs, quantity, proposed value and rationale.
Supplier and evidence hashes are derived from retained records.

## 6. Source ownership

Reload approved round, prepared evaluation definition, closing seal, original
opening receipt and exact sealed offer set. Full current Core and branch checks
precede discovery/counts and writes. Existing document holds remain authoritative.

## 7. Permissions

Both Draft and opened-offer read actions are required. Current named-reader
membership and retained/current opener independence are checked through the
existing P32 purpose boundary. No new default role or grant is created.

## 8. Identity evidence

The stable account principal owns the Draft. Original maker alias and each
revision actor alias are retained in the snapshot; aliases remain optional and
are not a substitute for current Core authority or a new employee prerequisite.

## 9. States

Only Draft exists. There is no Submit, Publish, Approved, Awarded or PO-ready
state. Existing RFQ commercial decisions remain distinct from this proposal.

## 10. Create

Create requires `draftId:null`, revision zero and null predecessor SHA. The
server allocates IDs and appends header, revision, lines and acknowledgement.

## 11. Revise and removal

Revise requires own ID and exact previous revision/SHA. Verify retained history
before extending it. Replace the current proposal in a new immutable revision;
omitted lines, including an empty replacement, remove proposals without deleting
history. Header cancel/retire is outside this bounded Draft contract.

## 12. Quantity and value rules

Positive exact 18,4 quantities cannot exceed an offer or aggregate source line.
Positive exact 38,8 proposed values use the round currency. No rounding, FX rate,
verified quoted pricing, budget authority, computed saving or guessed score is
inferred. A rationale is mandatory; assessment coverage is never automatic pass.

## 13. Persistence

Four additive families: head, revision, revision line and original receipt.
History is append-only. Only the head cursor and ordinary update stamps may
change, paired with exactly one new revision in the same save.

## 14. Parent relationships

Scoped composite keys bind round, prepared plan, seal, opening, offer, supplier
and sourcing line. Revision and receipt carry the exact unsealing owner. Keys
exclude mutable CAS cursors. Optional predecessor tuples explicitly reject NULL
SHA on later revisions. Genuine DDL must confirm NOT NULL on every required key.

## 15. Transactions

Reuse the owning serializable Procurement/Core transaction and existing sorted
source locks. No detached caller projection grants authority. The same save
requires exact receipt, audit and metadata-only outbox event.

## 16. Concurrency

Revision number and previous SHA/ID must match the stored head cursor. Maximum
100 revisions. Same command key with changed request or owner conflicts.

## 17. Exact evidence

Store full replacement snapshot and its SHA, including original source pins,
principal/optional alias and database-normalized time. Verify all historical
snapshots, line sets and original acknowledgements on recovery/read/revision.

## 18. Privacy and reads

Own list/detail/history only, after current purpose and branch authorization.
No competitor bodies or another evaluator's assessment enter receipt responses.
Tenant/company/branch/owner predicates precede count/page.

## 19. Recovery

GET `/commercial-rounds/{roundId}/recommendation-commands/{key}` returns the
original actor-owned acknowledgement only after the same current named-reader,
retained-opening and alias-purpose admission used for new writes, then verifying
stored history. POST replay uses that same admission before receipt lookup. Transport
uncertainty requires GET reconciliation, never a guessed second POST.

## 20. Audit and integration

`RecordRecommendationDraft` and `sourcing.recommendation_draft_recorded.v1`
are paired with the original receipt. Event payload contains metadata only.
This command never changes allocations, supplier, stock, asset, PO or Finance.

## 21. Presentation

Preserve the imported RFQ detail/tabs and existing ERP controls. No UI source is
changed in this integration; a future adapter must be capability gated.

## 22. Configuration and readiness

`RecommendationDraftEnabled` defaults false. The scoped four-family provider is
registered and checks the owning serializable transaction, configured finite
Execution database, P32 opened-offer read readiness, exact P36 model and physical
catalog, and only the P36 Draft write privileges. The head needs UPDATE on its
three revision cursor and three audit columns; immutable head columns, evidence
families, audit and outbox reject UPDATE. Its physical CHECK/FK/parent
capture remains deliberately empty until independent PostgreSQL capture, so it
returns unavailable even if the flag is set. Current Core purpose, branch,
named-reader and retained offer checks remain in the command/query routes.
There is no accepted host activation or metadata capability yet.

## 23. Migration and compatibility

The genuine migration adds four families and three stable P32 parent keys.
The registered readiness provider requires their exact captured PostgreSQL
definitions and the applied P36 migration; model names alone never suffice.
Preserve the generated Designer and snapshot bytes. Do not invent captured
CHECK or FK text, or ignore unexpected parent keys.

## 24. Rollback

Authored `P36RecommendationDraftGuards.BeforeDownSql` locks all four families
and vetoes any retained row with SQLSTATE 55000. It is installed first in the
genuine Down; an executed rollback proof remains pending.

## 25. Source verification

Authored tests cover split quantities, bounds/currency, null elements, immutable
history, receipt/opening ownership, NULL-safe predecessor CHECK, orphan event,
source/actor reparenting and short model keys. Static source inspection only;
no SDK, database, browser or Node jobs were run for this integration.

## 26. Physical acceptance

Required: genuine EF Up/Down/Up, four-family CHECK/FK/null/append-only negatives,
exact physical capture, finite grants and full old data/catalog/ACL preservation.
Use physically valid retained dependency closures for used-Down veto; no synthetic
business approval claims. No generated migration is included here.

## 27. Runtime acceptance

Required: current Core accounts with optional employee, named reader/grant and
alias revocation, actual retained opened offers, create/revise/stale CAS,
original-key replay/recovery, exact audits/outbox and independent owner reload.
Missing capability remains unavailable until accepted.

## 28. Open business decisions

Recommendation submission/publication, assessment completion, mandatory-fail
interpretation, ties, independent award approval, supplier notification and PO
conversion need separately adopted policy and owner contracts. This Draft is
not an executable PO source. Production remains unchanged.


## Genuine generation checkpoint (2026-10-25)

Official generation02 from `9b5e0dcb` passed after failed01's two Application
syntax tokens were corrected. Genuine `20261025014631_P36HumanRecommendationDraft`
adds four tables, ten scoped FKs, four CHECKs, fourteen indexes and exactly
three stable parent alternate keys. Raw Designer and snapshot are installed
byte-identically. A separate authored change invokes the existing four-family
retention veto as the first Down operation. Original raw output and failed01
evidence remain untouched. SDK/model/SQL validation, physical roundtrip/capture,
P32 parent-key readiness compatibility, finite grants and runtime are pending.
The capability stays default off and this Draft is never award/PO authority.
