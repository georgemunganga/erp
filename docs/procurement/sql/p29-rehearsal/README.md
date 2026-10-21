# P29 sourcing policy identity DDL rehearsal

Status: source-authored helper, not executed. It reuses the accepted P28/Core P03
private clone/snapshot patterns; those helpers are unchanged. This is not an API,
identity, finite-role or production acceptance test.

## Inputs

- Fixed private container `procurement-approval-rehearsal-postgres`, PostgreSQL17.
- `--source-db`: normally closed genuine P28 governed RFQ browser/owner database,
  such as the parent-retained `proc_approval_http_p28_rfqbrowser_20261021_073507`.
  It must end at P28 history, contain no P29 table, and retain coupled governed
  cancellation/source/binding/released allocation and create/cancel receipt/audit
  facts. Do not create substitute positive rows.
- `--source-producer-log`: actual retained producer/independent-owner terminal log.
  This is retained as provenance by hash, not interpreted as cryptographic proof.
- `--migration-id 20261021084158_P29SourcingPolicyIdentity`: exact generated identity.
- `--baseline-sql`: independently reviewed zero-to-P28 SQL. It must establish the
  exact original P28 Procurement schema/history, including all manual ownership
  overlays; it must not seed operational or identity records.
- `--up-sql`: reviewed idempotent P29 SQL; either P28-to-P29 or full idempotent chain
  after the explicit fresh baseline. Original bytes are hashed; UTF8 BOM is removed
  only for execution, as in the earlier corrected helpers.
- `--down-sql`: reviewed transactional P29-to-P28 SQL. It retains policy_versions
  then identity-table AccessExclusive locks and exact SQLSTATE55000
  `SourcingPolicyIdentityRollbackWouldLoseEvidence` veto before dropping the table.
- `--prefix`: NEW bounded `proc_approval_http_*` prefix (maximum43 characters),
  distinct from sources; no derived clone may exist. Sources/final names <=63.
- `--output`: NEW private evidence directory. No overwrite or cleanup path.

Optional **together**: `--used-db` and `--used-producer-log` for a normally closed,
actual API-produced P29 identity source. Require complete original/submitted/
independently-published source-owned identity graph, chronology and policy timestamps,
not hand-inserted rows. The exact full P29 schema must match the reviewed upgraded
clone. The parent supplies actual workflow/owner evidence; SQL shape and file hashes
alone cannot prove which application produced data.

Without this genuine P29 used source, the helper deliberately reports **PARTIAL**
and `retainedDownVerified:false`, never full PASS or retained-evidence Down acceptance.
The actual used-evidence case must run later before P29 acceptance.

## Procedure

Snapshot complete original **Procurement-owned** column definitions, all-column row
values, all migration-history fields (including ProductVersion), raw constraint/index
definitions and schema-only pg_dump before cloning. Numeric evidence remains JSON
text rather than passing through Python floating point. Only pg_dump's random
restrict/unrestrict tokens are normalized. Original sources are never altered.

1. New template0 clone: reviewed zero-to-P28 baseline, then P29 Up twice. Require
   original schema/history shape, no fabricated baseline facts and only the one new
   identity table; every original column/type/default/nullability/constraint/index
   remains unchanged. Existing P25/P26/P28 manual FKs are included in these checks.
2. Two independent actual-used-P28 clones: Up twice; compare every original column
   value/hash/JSON/decimal and all prior history. Second Up must change nothing.
3. One unused-identity upgraded P28 clone: Down must restore the complete original
   P28 snapshot, then Up twice must restore the exact upgraded snapshot. Old policies,
   RFQ/source/Finance evidence, audit and history must not be rewritten/deleted.
4. If genuine P29 used source is supplied: clone it unchanged, invoke Down expecting
   the exact SQLSTATE55000 veto, then require zero schema/data/history changes.
5. Re-read original source snapshot(s) and require complete preservation. Retain
   every clone/log on success or failure; never drop databases or force clients.

The helper never inserts identity/operational evidence, deletes original history,
disables FK/CHECK/triggers, changes replication mode, or fabricates authority. It
rejects obvious unsafe script directives, but it is not an untrusted SQL sandbox:
review the complete SQL and hashes before scheduling execution. External writers
must stay stopped throughout copying; the helper checks closed sessions and never
terminates them. Bootstrap postgres is a private DDL tool role, not runtime proof.

## Evidence and limits

New output uses umask077/directory0700; numbered operation logs, full original and
upgraded snapshots, PostgreSQL version and SQL/producer-log hashes are retained.
Statement timeout60s and lock timeout10s bound commands. `p29-checks.json` and `.tsv`
retain actual validated PostgreSQL17 `pg_get_constraintdef(false)` and
`pg_get_expr(false)` for `ck_source_policy_identity`, without guessed normalization.

This proves logical Procurement DDL/data/history preservation only. It does not prove
whole ERP/Core/blob backup/restore, original-maker identity trust, current permissions,
co-located transaction readiness, query behavior, lifecycle/identity revocation fences,
HTTP or browser behavior, policy publication, RFQ publication or production activation.
Do not combine PARTIAL DDL evidence with unrelated green gates to imply release-ready.
No SDK, PostgreSQL, Docker, browser or helper execution occurred during authoring.
