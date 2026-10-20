# Purchase-order execution verification checkpoint

Status: implementation candidate; not production enabled.

## Verified baseline

- `6225867`: all 247 Procurement business tests passed. Includes initial controlled PO execution and Core branch mutation fence source.
- First mapped HTTP run: 30 failures / 1 pass. The fixture authentication identities omitted the mandatory `tenant` claim required by the existing Procurement authorization policies. This failure was retained in `artifacts/order-execution/first-gate.log` and its TRX.
- `049fc00`: fixture-only correction supplies that claim; all 31 targeted mapped HTTP cases passed. The production authorization policy was unchanged. Verification does not replace actual PostgreSQL execution tests.
- Isolated P19 backup restore: committed P20–P23 SQL applied twice successfully, five P23 tables verified, all pre-existing business table row counts preserved. The initial wrapper failed because concatenation retained embedded UTF-8 BOMs; the wrapper was corrected, source SQL was not changed, and the passing rerun was recorded. Protected manifests/logs are under `/home/amizo/erp-backups/procurement-p23-restored-upgrade-20261020/`. Production was not migrated.

## Combined source under verification

`92abe51` adds reviewed exact arithmetic, actionable precision failures, target-scope checks on conversion replay, and purpose-specific Finance/dispatch reads and recovery. All read, conversion replay and amount rollback probe calls were retained when combining changes. Exact arithmetic is shared rather than copied. New read projections use decimal strings. `actionCandidates` are form hints requiring command validation, not proof of approval or funding.

Separate-author source review found no new concrete defect in the additive read/recovery changes. This is not production acceptance or a completed runtime security gate.

## Remaining release gates

Combined tests and actual PostgreSQL probe compilation; separate P24 migration generation and review; model drift check; fresh and restored database apply-twice rehearsal; actual restricted-runtime PO HTTP checks including rollback/replay and Core branch concurrency; existing UI adapters with actual API Playwright; source-aligned host build; protected production role/configuration and migration release checks. Receiving, matching, AP hand-off and broader Procurement completion remain separate work.

## Combined candidate and P24 review

The `92abe51` combination passed 267/267 Procurement business tests, 31/31 targeted mapped HTTP cases, and actual PostgreSQL verification-tool compilation (zero warnings/errors). These results do not claim a completed database execution gate.

Settings `80ff610`, reviewed fulfilment domain `db1f033`, and browser fixture `f6342a6` were then integrated. Settings add no schema changes; receiving/matching remain pure domain/application foundations with no live API or activation. The complete source must pass its own combined gates.

P24 was scaffolded from the compiled PO model; EF mapping source was unchanged from `92abe51` at generation. A rebuilt model drift check is mandatory after all source integration. Its forward amount alteration widens `numeric(18,4)` to `numeric(38,8)` and does not discard decimal digits. Existing methods receive `Unconfigured`, requiring deliberate routing configuration.

The initial generation/build job was deliberately stopped (exit 137) for migration review, not because of an out-of-memory failure. Its log was retained. Generated Down was amended to lock affected tables, block removal after execution/configuration is used, and reject any amount that would lose digits or overflow `numeric(18,4)`. Only an unused, exactly representable schema may downgrade. Forward fixes or a separately reviewed protected recovery plan are required after use. Actual apply-twice, unused downgrade/upgrade, used downgrade denial and row-preservation checks remain pending until their recorded PostgreSQL runs.

## Integrated P24 checkpoint and real HTTP findings

At `5278267`, the combined source passed 375/375 Procurement domain tests and
91/91 Procurement shared-host tests. The PostgreSQL HTTP probe compiled with zero
warnings/errors. These supersede the earlier narrower source test counts.

The rebuilt integrated model has no pending migrations. Actual isolated PostgreSQL
rehearsals passed fresh apply-twice, unused Down/Up, and a restored P23 backup upgraded
twice with all previous business row counts and allocation amounts preserved. Rollback-only
DDL checks rejected sub-four-place allocation values, numeric(18,4) overflow and configured
execution routes; exact maximum and zero allocation values remained downgradeable.
The consumed restricted-runtime database also rejected the actual rollback-only Down
script with `P24RollbackRequiresEvidencePreservingRecovery`; its 21 migration entries,
one Finance evidence row and two commercial receipts remained present afterwards.
Protected evidence is in `/home/amizo/erp-backups/procurement-orders-postgres-20261020/`.
Production remains at P19; none of these migrations were applied there.

The first restricted-runtime `--orders` run passed Core branch fence cleanup/concurrency,
typed PR/PO settings, conversion/replay/source conservation, current target scope and
purpose-specific Finance reads. It then correctly rejected a synthetically published PO
policy whose stage code used a prohibited hyphen. The fixture now uses `PO_COMMERCIAL`
and validates its policy before seeding. Production policy validation was not relaxed.
The original database and `runtime-orders-first.log` are retained. A fresh database and
restricted role are provisioned for the retry.

The successor also integrates exact numeric-lexeme input checks for PO lines and Finance
amounts (`ae60ea5`), preventing silent decimal or database rounding before writes. Its
combined tests, probe build and actual database execution are pending. An initial test
launcher omitted its read-only NuGet package mount and failed to resolve cached assemblies;
the corrected launch is running, with the initial log preserved. This was an environment
failure, not a passed source gate.

Frontend verification and actual API browser execution remain separate gates. No complete
Procurement release or production workflow activation is claimed by this checkpoint.
