# Core P03 document-use migration rehearsal

**Status: source-only helper, unexecuted.** No Python invocation, SDK build, SQL,
PostgreSQL, Docker or browser job has been run for this source increment. The helper
adapts the existing private P28 rehearsal patterns, without its malformed-row seeding
or constraint removal. Parent controls serialized execution and independent evidence.

## Inputs and ownership

Use the fixed private `procurement-approval-rehearsal-postgres` container and actual
PostgreSQL17 only. Both sources must be **normally closed** before cloning:

- `--source-db core_doc_rehearsal_base_<unique>`: exact Core P01/P02 history and
  genuine used documents/versions/clean scans/operation events from the existing
  default `CoreDocumentWorkflowProbe`. Parent may first run that tool on current P03
  with no use rows, then use the reviewed unused P03 Down to produce the used P02
  baseline. Retain that producer/Down evidence. This helper does not resume the probe
  or downgrade the original source.
- `--used-db core_doc_rehearsal_pins_<unique>`: exact Core P01/P02/P03 history and
  genuine completed use sets/pins from the opt-in owning probe. Counts, same-owner
  complete selection rows and matching clean scan/version hashes corroborate its
  retained facts. Only the actual producer logs establish provenance; SQL shape
  alone does not prove which API or application produced a record.
- `--prefix core_doc_rehearsal_p03_<unique>`: new bounded prefix, distinct from either
  source. Four suffixed database names (`fresh`, `used`, `unused`, `retained`) must
  not exist. Nothing is dropped, replaced, force-disconnected or reused.
- `--baseline-sql`: independently reviewed zero-to-P02 SQL containing
  `20261020060744_P01CoreDocumentFoundation` and
  `20261020063543_P02CoreDocumentUploadRecovery`, without P03.
- `--up-sql`: reviewed **idempotent** SQL containing
  `20261021080548_P03DocumentUseRetention`; a complete zero-to-P03 idempotent script
  or the idempotent P02-to-P03 script is suitable after the explicit baseline.
- `--down-sql`: reviewed transactional P03-to-P02 SQL retaining the AccessExclusive
  table locks and exact SQLSTATE55000 `CoreDocumentUseRollbackWouldLoseEvidence`
  stop. No `--no-transactions` script. Never disable FK/CHECK/trigger enforcement.
- `--output`: a NEW private evidence directory. Existing output is never overwritten.

All database names must use `core_doc_rehearsal_*`, bounded lowercase letters/digits/
underscores only. Prefix <=43 characters, final names <=63. No connection string,
password or remote server argument is accepted. Existing active source sessions cause
failure rather than termination. Root must retain control of these isolated sources
throughout the rehearsal. The helper uses bootstrap `postgres` inside the private
container solely for DDL rehearsal, not as runtime-role acceptance.

Example **for later scheduled execution**, with actual reviewed files substituted:

```bash
python3 docs/core/sql/p03-rehearsal/rehearse.py \
  --source-db core_doc_rehearsal_base_TIMESTAMP \
  --used-db core_doc_rehearsal_pins_TIMESTAMP \
  --prefix core_doc_rehearsal_p03_TIMESTAMP \
  --baseline-sql /private/P02-baseline.sql \
  --up-sql /private/P03-idempotent.sql \
  --down-sql /private/P03-to-P02.sql \
  --output /home/amizo/erp-backups/core-document-p03-TIMESTAMP
```

`TIMESTAMP` above is illustrative; replace it with lowercase/digit/underscore values.
No input SQL is generated or silently repaired. This is not an untrusted-SQL sandbox:
root must review the complete scripts and their hashes first. The helper rejects
obvious database-drop/forced-client/trigger-bypass directives but does not pretend
that textual checks can authenticate arbitrary SQL.

## Comparisons and cases

Before any clone, snapshot each source's complete Core document schema:

- ordered original column names, storage types, nullability, defaults, precision,
  lengths, datetime precision, identity/generation and collation;
- all rows projected over every original column, sorted deterministically without
  parsing financial values through floating point;
- every history row and column, including ProductVersion;
- all returned constraint definitions/validation and index definitions;
- schema-only `pg_dump` text, normalizing only random restrict/unrestrict tokens.

### Fresh baseline and idempotent Up

Create one new `template0` database. Apply reviewed zero-to-P02 SQL, require the same
P02 schema/history as the used source but no fabricated domain rows, then apply P03
Up twice. Require unchanged second application, exact P01/P02/P03 history, only the
two new tables, all original columns/types/defaults unchanged and all original
constraints/indexes retained.

### Used P02 upgrade

Create two independent closed-P02 clones. Up twice must preserve all original
column values, JSON/text/hash evidence and full original history while adding only
P03. Persist complete upgraded snapshots. One clone remains as upgraded evidence.

### Unused P03 rollback and re-upgrade

On the other upgraded P02 clone, run Down. Require exact complete P02 data/history/
schema/constraints/indexes equality with the original used P02 source. Run Up twice
again and require the exact original upgraded snapshot. Old documents/versions/
scans/events are never deleted or recoded to make rollback succeed.

### Genuine used P03 rollback denial

Clone the independent closed, genuinely used P03 source. First require the clone's
complete snapshot equals its source and that its full schema/column/constraint/index
definitions equal the reviewed upgraded P03 clone (no relaxed or missing constraints). Run Down expecting nonzero psql exit and exact
verbose SQLSTATE55000 guard. Require every row, column, history entry, constraint,
index and schema dump unchanged after denial. No pin/set/scan is inserted, altered,
removed, forged or relaxed by the helper. An incomplete browser/probe fixture cannot
be substituted with fabricated positive rows.

Finally, re-read **both original sources** and require their complete snapshots
unchanged. All clones and logs remain for inspection even on failure. There is no
cleanup/drop path.

## Evidence and limits

The helper uses umask077 and creates its new output0700; numbered docker-exec command
stdout/stderr logs and snapshot JSON files inherit owner-only permissions. SQL sets
statement timeout60s and lock timeout10s. Original SQL bytes are SHA256-hashed before
BOM-aware text execution; no hash silently describes a BOM-stripped replacement.

Retain `p03-checks.json` and `.tsv` with actual PostgreSQL17
`pg_get_constraintdef(oid,false)`, `pg_get_expr(conbin,conrelid,false)`, exact table/
constraint names and validation for `ck_doc_use_set_shape` and
`ck_doc_use_pin_shape`. These are returned owning PostgreSQL expressions, not guessed
source normalization. `postgresql-version.txt`, original/upgraded snapshots and the
terminal `result.json` connect SQL provenance, source/clone names and workflow/use
counts. A PASS file is written only after all comparisons succeed.

This proves logical **Core document schema/data/history DDL preservation** only.
It is not a physical backup or private-blob recovery test, whole-ERP schema proof,
current-purpose authorization, co-located transaction/provider readiness, lifecycle
race fence, finite runtime grants, publication, supplier delivery, Finance authority
or production enablement. Root retains earlier79/79 Core tests and other gate
checkpoints separately; helper source review or syntax checks are not migration
runtime evidence. No combined release-green claim is implied.
