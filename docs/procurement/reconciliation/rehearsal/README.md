# Private canonical migration rehearsal

This helper is limited to the named private container `procurement-approval-rehearsal-postgres` and approved retained actual CRUD P20 database `procurement_crud_http_20261022_050209`. Before any DDL it verifies Docker reports the named container running with NetworkMode none. It never modifies/drops that source or existing databases. It creates new `proc_approval_http_*` databases, retains all failures, and performs no HTTP/production/role-grant operation. Do not run alongside another private database or SDK gate without parent coordination.

## Inputs and source freeze

Use compiled EF scripts from this exact source epoch: full idempotent `0 -> 20261022052824_P21CanonicalGovernedProcurementFoundation`, exact released `0 -> P19`, released `P19 -> 20261022043718_P20InvoiceDraftMaintenance`, and protected `P21 -> P20` Down. Do not use earlier pre-audit SQL outputs or held history IDs. The current migration must remain the independently reviewed frozen literal; preserve SHA256, commit, tool log and commands in protected evidence. Owner access here proves DDL only, never the application's finite runtime permissions.

```bash
python3 docs/procurement/reconciliation/rehearsal/rehearse.py \
  --prefix proc_approval_http_canon_YYYYMMDD_HHMM \
  --up-sql /protected/final/canonical-up.sql \
  --p19-sql /protected/final/released-p19.sql \
  --p20-sql /protected/final/released-p20.sql \
  --down-sql /protected/final/canonical-down-unused-only.sql \
  --source-root /home/amizo/procurement-governed-crud-reconciliation \
  --output /protected/new-unique-canonical-rehearsal
```

Choose a shorter prefix if needed: the suffixes and PostgreSQL 63-character identifier limit are enforced. Existing evidence paths and database names are never reused. Python syntax/`--help` verification is separate from executing the helper.

## Proof boundaries

- Fresh full Up twice and unused Down/Up must preserve exact schema/data/history.
- Generated **P19 plus explicitly synthetic unverified** supplier/invoice/line/result/exception/PO/physical receipt/inspection/service rows crosses released P20 then new P21. Original exact decimal quantities, values, text/status/metadata remain unchanged; no issue/approval/acceptance authority is backfilled. P19 proof is not a claim that an actual production P19 database was inspected.
- A clone of the actual retained P20 CRUD database separately proves original bill/credit/catalog/request/order/audit data and old history preservation, Up twice and unused Down/Up. If an existing RECEIVING policy protects this baseline, verify its exact no-effect Down veto instead; never delete configuration/evidence to force a downgrade.
- All nine DB-only ownership FKs must satisfy their original exact table/column/index/deferrability/validation manifests. Each missing-FK tamper occurs in a rollback-only transaction and must make its owning manifest false; snapshot equality proves restoration.
- All 39 named CHECKs (34 new P21 plus the exact five preserved from released844190e P20) must exist validated. Their actual PostgreSQL expressions are captured for separate exact source/reader comparison, rather than pretending a name is full semantic proof.
- Separate owner-only synthetic clones prove the named used-receipt, sourcing-identity audit-only, outbox-only, partial checker audit, unknown variance, allocation/service/tax precision Down vetoes. These are not successful workflow producers or original authority evidence. Failed Down must preserve complete data/schema/history.
- Source snapshots batch every table in one query per database and compare decimals exactly using `Decimal`, so an intentional numeric widening may change display scale without allowing any value rounding. Original text and stored JSON strings compare exactly.

The helper neither exercises genuine current-worker/Finance/approval/receiving API authorization nor enables options, grants, supplier status, publication, matching or AP posting. Full host compilation, mapped HTTP, finite runtime and actual workflow gates remain separate. Root must review any concrete failure, preserve its log/source epoch, and use a new prefix on rerun. Do not delete used data, alter checks, insert held migration-history IDs or broaden runtime grants to make it pass.
