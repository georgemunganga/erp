# B1 retained schema admission after a real PostgreSQL restore

## Defect and bounded fix

The immutable pub29 B1 catalog query returns direct fingerprint `24e096b8ea495da24c7eab1ea2b0bd1fda9cc54754d2d2b5f9ca923cd743133b`. A genuine PostgreSQL 17.11 custom-format pg_dump/pg_restore of the private database returns `f7d9d1254a04586578b8853da7c5152c8f3eb9a6ec3b42c07fa81d2da10d0032`. The existing exact matcher therefore rejects an otherwise intact restored B1 schema, including the shared M4 barrier. No production failure or successful restored-provider acceptance is claimed by this source change.

An independent offline projection of the saved raw catalogs reproduced **both actual PostgreSQL hashes exactly**, using the exact pub29 query's six wanted tables, columns, constraints, index states, enabled triggers, parent key and invoker functions. The only differences are five named CHECK definition strings:

- `commercial_round_criteria.ck_round_criterion_v1`
- `commercial_round_participants.ck_round_participant_v1`
- `commercial_round_plan_receipts.ck_round_receipt_v1`
- `commercial_round_plan_revisions.ck_round_revision_v1`
- `commercial_round_plans.ck_round_plan_draft`

`commercial_round_enrollments` and every other projected value are identical. All five pairs differ only by six literal varchar-array-to-text-array cast distributions; criteria/participants also flatten the redundant parentheses around the first pair of AND operands. A bounded offline transformation verified each complete direct/restored pair exactly. It did not modify database definitions or normalize SQL in production.

`CommercialRoundRestoredCheckForms` maps only the five **complete observed restored definition strings**, each under its exact table, constraint name and CHECK type, to its original captured definition. The full B1 hash remains the original `24e096...` contract. Unknown spellings, changed literals/guards, owners, columns, constraint validation, indexes, disabled triggers, function security and parent-key changes remain part of the same full fingerprint and fail admission. No migration, schema, grant, flag, route, authority or data change is introduced. This fix is independent of the new Plan module and uses no Plan tables.

## Actual private evidence

Evidence directory: `procurement-plan-review-native-capture-prep-20261103_run01/execution-r4`. The complete owner capture retained all original 237-table data/catalog state, captured the additive Plan schema in two private databases, and saved raw restore representations before comparison. This evidence identifies the existing B1 restore defect; it does not activate Plan or accept its CHECK variants.

| Artifact | SHA-256 |
|---|---|
| `COMPLETED.private.json` | `5d0134c4f79a7fd683111034eb16be51f0d7941b4f4c9bf3d7b5ea088b2dbba0` |
| `direct-final239-RAW.private.json.gz` | `8e2390d7259bbd276093e0e7db9bc27155c1e09695fb003bf1000fce534d687f` |
| `restored239-RAW-BEFORE-COMPARISON.private.json.gz` | `b4ed76e47bbb2500a3f0e0b3fb6ffd50af9e7d9f0ce69a23408b3ac016e5a857` |
| `direct-b1-canonical.actual.private.json` | `fdb64637c4ef7ffade59733b603cd197ad1752e50327892bd3ccef3f51621aeb` |
| `restored-b1-canonical.actual.private.json` | `b07c267d1afc531b3ff6bd49666f908a7af6ec871e8411124ea84e90e4565117` |

The two committed test fixtures contain only the B1 catalog projection, including schema function definitions. They contain no business rows, credentials, sessions or cookies. Twenty authored Host cases exercise both captured hashes, exact known spelling/name matching, weaker CHECK rejection, complete-shape parity and unrelated security/catalog mutations. **These C# tests have not been run in this source-only hotfix worktree.** Only offline Python evidence comparison, exact bounded pair proof and git whitespace checks were performed.

## Release holds

Root must independently review the exact pairs and code, run the focused/new and affected Host tests, emit the actual new compiled fingerprint SQL, and prove it returns the original captured hash on both existing direct and true-restored native databases. Actual malformed-definition/disabled-trigger/finite-role checks remain required before shipping or claiming restored-provider acceptance. Preserve the original pub29 `f7d9...` failure and all existing native evidence. Keep feature flags and current permission/grant boundaries unchanged. No build, SQL execution, runtime, deployment or production change is authorized by this source document.


## Subsequent Root verification — 3 November 2026

The six backend/test files from hotfix `e6d87c5c` are byte-identical in the combined Plan candidate `6e85bdc8`. That actual candidate passed all 548 Host cases with zero failures/skips, including all 20 `ProcurementCommercialRoundRestoredShapeTests` cases. The source binding is `f970f4bd64c4f2795eea5b18e8bcb60988090add3a635b4eb0944ea29ed6dc8a`; the candidate's immutable publication manifest is `377ef9a50cdaef70af62ec9a27a7ea3f9cc6dd55b79f7d2807255d046c91c9e0`.

The actual newly compiled B1 catalog SQL returned the original `24e096...` fingerprint on both the direct and genuinely restored private databases. Completion receipt SHA-256: `0c705b38c511630fc8421087f077ccc9f75440c5426299fd99d584a8451252a2`. Full native captures proved all protected original237 and both239-table databases unchanged during that read-only observation.

Actual finite direct-LOGIN provider checks subsequently passed B1 read and its shared barrier on both databases, alongside Plan read/record and policy-identity admission. Completion SHA-256: `9f282cb6d45b65dc6f1cfcb895d710a9e7cb6a33f610ae8e2cd03b0186096597`. The owner-role denial cohort also passed; its results SHA-256 is `ab349c97fe142f286aa721e8e50372e3649a7253364fcc4277392865c7b4c70d`. This is private native evidence for the combined candidate; legacy-purpose providers were not relabeled as accepted under the Plan execution role.

These later results supersede the earlier not-run status for the 20 exact hotfix tests. They do not establish standalone hotfix build execution, signed-in workflow UAT, native malformed-CHECK/disabled-trigger mutation acceptance, deployment, or overall Procurement completion. Production remains on the previously recorded release; release holds above remain relevant wherever not explicitly satisfied by this evidence.
