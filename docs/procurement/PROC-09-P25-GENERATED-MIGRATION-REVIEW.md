# P25 generated migration — distinct source review

**Review target:** Root-owned `20261020133820_P25PositiveReceivingExecution.cs`, its Designer
and `ProcurementDbContextModelSnapshot.cs`, after root incorporated the two explicit required
sequence predicates from `8a044e8` as `35e3780`. Reviewed read-only in the root worktree.
**Verdict:** No blocking generated-schema/source defect found in this bounded pass. This is
not permission to deploy or a claim that SQL, migration rollback or the live API has passed.
**Execution:** No SDK, build, database, browser, migration or production operation by reviewer.

## Reviewed artifact fingerprints

| File | SHA-256 at review |
|---|---|
| `20261020133820_P25PositiveReceivingExecution.cs` | `0a361aa9db2ee0f8f1c9e1554411b1c9e05c11ee543b0036e5eb839424a75ad1` |
| `20261020133820_P25PositiveReceivingExecution.Designer.cs` | `cc8a944eeafa6390cf3b60f1ed2dce36c2ed5a838dd744720e87a37fb2f5971d` |
| `ProcurementDbContextModelSnapshot.cs` | `d20269eedcbbd86662cd4e84dbf84d30cae6a9e124c332708dac6e62d64b8e77` |

A later artifact change requires a review of that diff; these fingerprints identify the
uncommitted generated files reviewed rather than imply a released migration commit.

## Additive Up and data preservation

- Exactly two new tables: `receiving_source_states`, then `receiving_acceptance_effects`.
  No duplicate goods-receipt, service, inspection, Inventory, Asset, AP or payment ledger.
- Forty-four additive columns across existing receipt header/line, inspection and service
  records match the reviewed model inventory. No existing column/table/FK/key is dropped by
  Up. Two prior nonunique FK-prefix indexes are rebuilt with expanded composite columns;
  no record is removed or relabelled.
- The only changed existing column type is service `accepted_amount`, from numeric(18,4)
  to numeric(38,8), an exact storage widening. New accepted-net/effect values use38,8;
  new quantities use18,4. This does not promise CLR support for arbitrary38-digit values.
- No raw data mutation, seed, guessed worker/policy identity, synthetic effect, legacy
  classification backfill or new success/Finance state occurs in Up.
- Legacy execution defaults remain0. New nonnullable binding sentinels default Guid.Empty;
  nullable issued/submission/state/worker/policy/effect pins remain nullable. New composite
  foreign keys skip genuinely absent legacy pins, rather than retrofitting NOT NULL IDs.
  Alternate keys include unique existing IDs and cannot collapse legacy zero sentinels.
- Goods and service Notes are nullable max2000. Existing service Reason is not changed or
  copied into Notes. Existing legacy amounts, quantity, reason, dates and source links stay.

## Keys, checks and creation order

- Existing principal alternate keys are added before the new state/effect tables need them.
  State binds company/tenant to exact PO line/PO, issued revision/submission, delivery/issued
  and retained classification policy. Typed effects bind exact goods observation or service
  record/state and, when supplied, inspection/source/physical identity.
- Existing table foreign keys are added after state creation; all use Restrict, without a
  cross-schema Core identity/approval FK or cascade deletion of final evidence.
- Scoped issued-line uniqueness, effect key and per-state effect sequence indexes are
  present. Physical/inspection uniqueness filters preserve legacy null rows. These do not
  replace the shared lock/CAS and complete combined-ledger verification in the application.
- The explicit `physical_sequence IS NOT NULL` and inspection `ledger_sequence IS NOT NULL`
  corrections occur exactly once in each generated file, matching the model. The legacy
  and unrecorded-draft branches remain permitted; PostgreSQL CHECK UNKNOWN no longer
  accepts missing sequences in those final-evidence branches.
- Designer and Snapshot model bodies are identical after whitespace normalization.
  Neither accidentally includes P26 invoice mapping/allocation tables.

## Protected Down

- Its first migration operation is the ordinary-transaction `migrationBuilder.Sql` guard;
  no `suppressTransaction` is present. Embedded SQL matches the reviewed candidate after
  comment/whitespace normalization. No destructive operation precedes it.
- Bounded ACCESS EXCLUSIVE locks cover new state/effects, affected existing records,
  configuration and command/audit/outbox evidence. Guard vetoes configured or used receiving,
  every nondefault extension (including service Notes on v0), typed generic audit evidence,
  and service values that cannot return exactly to numeric(18,4).
- Up-added column, explicit FK and alternate-key inventories are symmetric with Down drops;
  only the two new tables are dropped. Incoming existing-table FKs are removed before the
  effects table, then state table, followed by new principal keys/fields. Prior indexes are
  recreated and service amount narrows only after the guard succeeds.
- A used database requires an evidence-preserving forward fix or protected recovery; clearing
  evidence merely to pass Down is not permitted. No raw SQL was executed in this review.

## Remaining required gates

Root must rebuild after synchronized source/generated changes, verify no pending model drift,
review idempotent SQL, and rehearse P24→P25 Up twice, unchanged legacy0 data, unused Down→Up,
used/config/audit-only/Notes-only denial, exact precision/range denial, and concurrent lock
failure with complete rollback. Actual typed/scope FK denials and the new named23514 NULL
sequence probe must run against PostgreSQL with retained first failures. Restricted-runtime
receiving HTTP workflows and compatible frontend/browser gates are separate obligations.
No production activation or verification is inferred from the source review verdict.
