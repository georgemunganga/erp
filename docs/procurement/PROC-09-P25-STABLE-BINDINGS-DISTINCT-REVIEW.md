# P25 stable evidence bindings — distinct source review

Reviewed source: `ad844ab` in `codex/procurement-receiving-stable-keys`, base `28942ee`.
Reviewer: separate agent from correction author. Scope: source only; no SDK, database, migration generation or browser execution.

## Source verdict

No blocking issue found in this bounded mapping/constraint/readiness/probe correction.
This is not evidence that its future migration applies safely or that real receiving passes.

## Checked ownership and lifecycle invariants

- Inspection→goods, acceptance effect→goods and effect→service now target stable scoped EF
  IDs. Tenant/legal-entity scope remains part of each EF relationship; no global-ID-only
  reference, cascade deletion or additional master/ledger is introduced.
- Mutable goods source/physical binding and service source binding are no longer EF identity
  keys. Their previous full tuples remain ordinary unique indexes, suitable for referenced
  PostgreSQL ownership constraints without EF treating transitions as primary identity changes.
- Eight GUID binding properties explicitly use ValueGeneratedNever: header one, goods three,
  inspection two and service two. Guid.Empty is retained draft/legacy missing evidence rather
  than a generated authoritative fact. Initial author documentation's seven-count is cosmetic;
  the actual model configures all eight.
- The existing full immutable inspection principal tuple and effect→inspection relationship
  remain unchanged. All source-state checks, sequence/CAS, effect/counter correspondence,
  worker/source pins and post-observation/certification immutability guards are unchanged.
- The SQL overlay retains all three exact prior business ownership tuples: inspection and
  goods effects must reference the same goods line/source-state/physical observation; service
  effects must reference the same service/state. DEFERRABLE INITIALLY DEFERRED with NO ACTION
  allows valid same-transaction update/insert ordering without accepting an invalid commit.
  Stable scoped EF FKs remain immediate and Restrict. No row/evidence rewrite is specified.

## Readiness and maintenance contract

Custom FKs are intentionally outside EF's model/snapshot. Ordinary EF drift success cannot
prove their presence. The receiving readiness manifest therefore checks exact constraint
names, child/parent tables and ordered columns, FK type, validation, initial deferral,
NO ACTION update/delete, MATCH SIMPLE and valid immediate unique supporting indexes.
A database with only original P25 columns/relationships fails this corrected readiness gate.
Future P26 or other migrations must preserve/reinstall this explicit overlay; this requirement
is documented rather than hidden behind EnsureCreated equivalence.

## Meaningful authored verification

Persisted/reloaded goods and service draft tests exercise the formerly invalid EF transition
and assert unchanged zero draft sentinels and post-fact immutability. Their InMemory provider
isolates tracking behavior; it does not prove SQL FKs or authenticated policy provenance.

Actual PostgreSQL probe source starts from mapped HTTP-produced rows. It changes only the
relevant binding fields so other scoped relationships remain valid, then explicitly forces
**the named** deferred FK IMMEDIATE and expects named 23503. It compares the original row after
rollback. A same-company real other state supplies the service mismatch, avoiding an unrelated
missing-state FK failure. The goods-effect case clears optional inspection linkage so the
intended exact goods relationship must reject it. Manifest negative cases mutate/drop/replace
constraints in rolled-back DDL and require false readiness, followed by restored readiness.
These are authored checks, not passing runtime results.

## Remaining central gates

1. Generate one additive correction migration after immutable existing P25; do not rewrite P25.
2. Review drop/create ordering: dependent old FKs before old alternate keys, ordinary referenced
   unique indexes before custom exact constraints, correct stable scoped EF keys and no business
   data/table/column drops. Install the reviewed overlay in the same migration transaction.
3. Preserve guarded Down: used/configured receiving must not downgrade into the broken ORM
   shape. Review both unused rollback and used denial behavior in a disposable database.
4. Build/test model and shared host, check drift, apply SQL on fresh and retained P25 databases,
   execute manifest/ownership negatives and full restricted-runtime actual receiving workflow.
5. Complete concurrency/replay/access/browser checks before activation. This review author
   neither generated nor ran those gates and makes no deployment/completion claim.
