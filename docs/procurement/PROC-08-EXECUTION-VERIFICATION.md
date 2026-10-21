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
The original database and `runtime-orders-first.log` are retained.

The successor also integrates exact numeric-lexeme input checks for PO lines and Finance
amounts (`ae60ea5`), preventing silent decimal or database rounding before writes. Its
400/400 domain tests and 95/95 Procurement shared-host tests passed with zero failures
or skips. The PostgreSQL probe build succeeded with zero warnings/errors; the preceding
shared-host rebuild retained pre-existing nullable/analyzer warnings. An initial test
launcher omitted its read-only NuGet package mount and failed to resolve cached assemblies;
the corrected launch passed, with the initial log preserved. This was an environment
failure, not a passed source gate.

The second real database run passed submission, approval and issuing, then exposed another
fixture defect: its grant-revocation assertion changed a buyer role while the acting account
was still an explicit ERP administrator. `9bc3f7f` changes that scenario to the actual
non-administrator buyer role and checks permitted → revoked/403 → restored/permitted.
Production privilege rules were unchanged; `runtime-orders-exact-retry.log` and that
consumed database are retained.

The fresh restricted-runtime retry at `9bc3f7f` passed the complete actual mapped HTTP /
PostgreSQL PO workflow (exit 0): independent funding, Core decisions, supplier denial,
sealed issue and actual delivery; source conservation, branch writer/reader races and
grant revocation; commitment ownership and cancellation/release; exact decimal-string
round-trips, unrepresentable amounts returning 422 without partial effects, invalid raw
lexemes returning 400 without persisted effects, and exact 1E-08 Finance confirmation /
release. Revoked finite authority also denied exact decision and issue replay. Protected
`runtime-orders-checkpoint.json` records source, database, runtime role, DLL/log hashes
and each passing checkpoint. No production database was involved.

The UI branch `codex/procurement-final-ui-integration` at `1a023ac` records passing
checkpoints for 75 unique mocked browser scenarios. This was not one uninterrupted 75-case
run: all first failures are retained, test selector/copy defects were corrected, and the
activation-conflict message defect required an application fix. The 14 PR cases retain
their earlier source-identical proof; 61 remaining cases passed against frozen corrected
assets (`index-D-b3C2fm.js`, SHA-256
`714a19b5a7c25ef03d9170af68121e6544250731826593a601ad93525f146e83`).
The UI repository's `docs/PROCUREMENT-FINAL-UI-BROWSER-EVIDENCE.md` contains case provenance.

The actual API PO browser gate subsequently passed: one case, 2.1 minutes, against a fresh
restricted PostgreSQL sandbox using backend fixture `f9fca21` and unchanged application
assets/test source `1a023ac`. It exercised six UI commands, current-purpose reads, exhausted
source checks and exact delivery retry through actual business responses. The first attempt
correctly rejected submission after the fixture replaced the named policy role; the fix
preserved that role and restricted its actions to PO approval only, without PO/PR read or
PR approval permissions. No production or UI authorization rule changed.

Graceful sandbox shutdown then passed the owner-side persisted check: exactly one allocation,
Finance confirmation, submission, independent Core decision, issued version and delivery
confirmation; exact 100 ZMW, ordered quantity 1 and allocated quantity 0. Protected
`actual-po-browser-checkpoint.json` records source, manifest/log hashes and proof. Passing
inline traffic attachment was not materialized by the list reporter; terminal/assertion
results and persisted proof remain available, and the first failure trace was retained.
Future fixture tests now explicitly write owner-private traffic files. UI evidence is pushed
in `5ccf22c`; later test-only capture changes do not change these application asset hashes.

Actual settings browser acceptance also passed: one case, 56.4 seconds, backend `f9fca21`,
unchanged application assets and test-only selector/capture source `2729876`. Create (201),
submit (200) and independent Finance activation (200) preserved the exact 18,4 amount;
the scoped reader remained unable to revoke and its denied request did not change the grant.
The first status-selector failure and its consumed fixture are retained. Passing traffic was
materialized privately (SHA-256 `4ba97a06fe844716792508edb52d93169897500e4b093ea7b2b76535c701cd34`);
`actual-settings-browser-checkpoint.json` records the fresh fixture and manifest evidence.
UI evidence is pushed in `3622b78`.

The source-aligned release image and production release remain separate gates.
No complete Procurement module or production workflow activation is
claimed by this checkpoint. All supplier/Finance/delivery evidence here is synthetic; no
external supplier email or payment was executed.
