# P25 populated legacy restore fixture and preservation assertions

**Status:** Synthetic SQL source authored/reviewed against P24 model `e752ed1` and generated
P25 migration `20261020133820_P25PositiveReceivingExecution` at `e08b5a6`. No SQL, database,
SDK, migration, build or browser execution by this task. Root reviews/runs only on a private
restored P24 copy such as `procurement_p25_restore_20261020_133953`.

## Fixture boundaries

[01-seed-p24.sql](sql/p25-legacy-rehearsal/01-seed-p24.sql) creates eleven synthetic rows
using **only old P24 columns**: one supplier, one PR/two PR lines, one PO/two PO lines,
one goods receipt/line/inspection and one service acceptance. All old Procurement tenant/entity
composite FKs point to the same synthetic scope and existing fixture parents. Required scalar
columns were checked against the P24 model snapshot; nullable source/classification references
are left unknown. No Core company/identity/location, financial posting, approval, current
receiving policy, issue version, source state or acceptance effect is fabricated.

The synthetic tenant is `p25-legacy-rehearsal`; entity ends in `...0100`. IDs end in0001–0011
and are fixed for reproducibility. Worker/site IDs are retained legacy references, **not new
Core records or authenticated authority**. Supplier is non-orderable and PO uses a retained
legacy status/revision0 with no submission/case/issued proof. Old Approved/Accepted labels
are deliberately not converted to new execution authorization.

| Retained record | Meaningful old data |
|---|---|
| Goods PR/PO line | Ordered12, accepted7.2500, price125.1250; existing PO invoiced3.5000 |
| Goods receipt line | Received10.5000, accepted7.2500, rejected1.2500, original batch/serial/rejection text |
| Inspection | Inspected10.5000, defect1.2500, original inspector/checklist/notes/date |
| Service PR/PO line | Ordered8, accepted3.1250, price88.8888; existing PO invoiced1.1250 |
| Service acceptance | Quantity3.1250, amount277.7775, Accepted status, original period/certifiedAt and legacy Reason longer than1000 characters |
| All records | Nonempty status/owner references, stable versions/timestamps and Unicode/punctuation in selected text |

Values are synthetic preservation inputs, not reconstructed truth or approved new purchases.
There is no claim that these rows satisfy the live v1 workflow; preserving them without
inventing proof is exactly the upgrade/recovery requirement.

## Ordered private rehearsal

1. Root independently confirms the target is a disposable restored **P24, not P25** database,
   owner/bootstrap connection is private, and normal writers are stopped. Scripts refuse
   databases without the `procurement_p25_restore_` prefix. Seed additionally checks actual
   P24 migration history, rejects any P25 history and refuses an existing fixture tenant.
2. Run01 once with `psql -X` / ON_ERROR_STOP. Do not add ON CONFLICT, remove guards, delete
   existing data or run on production. Its inserts are one transaction; old FKs still apply.
3. Capture [02-capture-old-shape.sql](sql/p25-legacy-rehearsal/02-capture-old-shape.sql) with
   quiet/tuples-only output to a mode0600 private baseline file. It returns one JSON object
   containing every original column of all eleven rows, ordered by table/ID. Keep it outside Git.
4. Apply the root-reviewed idempotent P25 Up script twice. Do not seed P25 authority to make
   legacy rows pass. Run [03-assert-after-up-unverified.sql](sql/p25-legacy-rehearsal/03-assert-after-up-unverified.sql).
5. Bind the captured JSON text to the psql variable `legacy_baseline` and run
   [04-assert-old-shape-preserved.sql](sql/p25-legacy-rehearsal/04-assert-old-shape-preserved.sql).
   Binding must be an ordinary quoted psql variable/value, never executable SQL interpolation.
   It compares the whole old row shape as PostgreSQL jsonb in a read-only transaction and raises
   on change. Numeric277.7775 versus277.77750000 compares by exact JSONB numeric value: do not
   pass financial baselines through JS/jq/Python floating-point conversion or use byte equality
   of PostgreSQL's widened numeric rendering as a false data-loss signal.
6. On the still-unused P25 extension, run the centrally reviewed protected Down. Repeat04
   against the restored P24 shape, then reapply Up twice and repeat03+04. Capturing a new
   baseline after failure would conceal data loss; keep the original step3 snapshot.
7. Separately root runs the existing used/configured/precision/audit Down-denial matrix.
   Do not introduce new receiving config/effects into this unused-legacy roundtrip copy;
   protected Down is correctly forbidden after any such use.

No runnable connection string, production credential, backup or actual customer row belongs
in these scripts. Root may use its existing private service/credential mechanism; do not print
credentials or paste the baseline into an issue. The baseline contains only synthetic data,
but it still follows the same protected artifact handling as other migration evidence.

## What the assertions prove when executed

03 checks there is exactly one of each populated legacy receiving record; all new execution
versions remain0; worker/issued/submission/source/policy/effect/physical identities remain
null/Guid.Empty; initial rejection/qualified/current accepted extension caches remain0 rather
than copying old values; service Notes remains null rather than copying legacy Reason; and
no receiving state/effect/policy is created in the synthetic scope. Original nonzero goods,
inspection and service values/status/date/reason are checked explicitly.

04 strips **only** the columns actually added by generated P25 from each table's JSON row
shape. Before P25 those keys simply do not exist; after P25 it removes them for a like-for-like
comparison. Every original ID, tenant/entity, reference, numeric value, version, timestamp,
status, nullable value and full text is compared. It requires the captured original baseline
and fails rather than resets data. The script is usable before Up, after Up, after Down and
following re-Up. Neither assertion writes business records or creates a helper table/function.

The fixture provides no HTTP authorization, live matching eligibility or complete legacy
reconciliation proof. New controlled receiving must still block unverifiable inherited
acceptance, and branch-scoped legacy reads remain unavailable. Actual migration history,
schema readiness/model-drift, new FK/check enforcement and restricted-role workflows are
separate root-owned gates. Record actual results only after those jobs run.
