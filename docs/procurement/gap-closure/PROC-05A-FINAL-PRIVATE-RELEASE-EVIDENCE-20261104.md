# PROC-05A final private release evidence — 4 November 2026

Issue169 / draft PR171 implements the [28-section governed Plan feature](../features/PROC-05A-GOVERNED-PLAN-REVIEW.md). This record supersedes the pending post-primary owner/retention gates in the [3 November checkpoint](PROC-05A-PRIVATE-INTEGRATION-CHECKPOINT-20261103.md). It records actual Root-run private verification and production read-only compatibility observation. It does not claim paired deployment, production migration/permissions/business activation, alternate-flow UAT or complete Procurement acceptance. Master issue145 remains **NOT READY / open**.

## Accepted source and executable successor

Published main556fbee2 is integrated. Executable source is `c93e8df4f82214b58273bf0c9be58d013aca3331`, backend tree `adbcb5c396deba114fe57659e04349dc49e129c7`. Relative to the earlier tested6e85 product, the only application change permits three established **optional** Core reads: `hrm.org_units.effective_from`, `hrm.org_units.effective_to`, `hrm.workers.manager_id`. Existing principal approval already declares these properties, department validation uses effective dates, and manager resolution queries ManagerId. No required projection grows; password/payroll/private/unknown reads remain denied. The successor does not revoke legitimate existing production reads or broaden the guard to entire tables.

Root's actual successor SDK passed **551 Host cases, zero failures/skips**, retaining every prior548 test identity and adding three actual-model/optional-boundary cases. The actual2004 Procurement-case result is exactly bound and **reused, not rerun**; it includes both migration safety cases. Model-no-pending, raw-identical designer/snapshot and unchanged B1 SQL passed. Procurement/Core source and all migrations remain unchanged from6e85; no new scaffold was performed. Publication has150 files. The previous6e85/548 publication and all original failure logs remain retained.

The current emitted SQL is byte-identical to6e85 for all four files: Plan Up `68449de7b9a2ad2cb1fb27e249c1b3cd9052e8bcdd4cebf2c910201d879255cf`; guarded Plan Down `37acfb16d3c5800cef1d8097d128ae954aae7e220197e1f1e2487c8927628525`; B1 Up `6f198f59b63059c071eb61ef67bc206dbf4f548101dd7f723173a21ebcead6c9`; B1 Down `71bf93bab78b1837dc32910d81ee996a2e1350eb6266294149db40a058ef6530`. Genuine migration remains `20261103211606_ProcurementPlanGovernedReview`.

## Actual candidate canary and production compatibility

The successor publication's real finite direct-LOGIN private canary passed on both direct and true-restored239-table databases. Its six intended positive observations are ordinary Plan structural READ, review READ, Plan technical RECORD readiness, operational policy identity READ, B1 READ and B1 barrier. Both native states stayed exact. It reused the previously compiled6e85 probe against the actual newly loaded c93 Host module (MVID `78a14c0b-2b93-402a-bd4a-42711c42e8b0`), with a source-compatibility proof; the probe was not recompiled. No Host startup, authentication, business command or application authority is inferred from metadata readiness. Invoice-unit/owner-baseline/manual-payment provider values were observational false under this Plan purpose role; they are not newly accepted workflows or hidden test failures.

The three-read compatibility defect was then observed directly against the existing production finite LOGIN role in a read-only metadata transaction: old compiled guard **false**, new compiled guard **true**. Actual old/new emitted SQL differs by precisely the three optional reads; minimum required columns remain unchanged. There was no SET ROLE or owner substitution, no DML/business values, no grants/roles/memberships/schema/environment changes, and production container configuration/database/table count/migration state stayed unchanged. This confirms the current production role's guard compatibility **before DDL**; it is not production Plan schema/provider/business acceptance.

The explicit private role additions remain execution20 plus two shared-policy provenance SELECT dependencies, and ordinary Record7 (six plan/line CRUD edges plus submission SELECT for returned-round cancellation). No DELETE or sealed-content UPDATE authority is added. Existing owner-role admission denials remain retained in their earlier separate cohort; the current finite canary must not be described as another owner-denial run.

## Original UI primary flow, owner proof and retained Down veto

The original imported UI product `3e2783b1f373857ba11681e73407c13ab09e30a8`, with26 focused UI checks and a new live build, was tested against backend6e85 in the actual normal-cookie primary browser cohort: **Create201 → Submit201 → Review200 → Final200**. Reviewer access to maker-owner detail denied403; historical submitted content was read-only and the current parent Plan was Active. The normal shared API policy Create→SubmitReview→Publish had already passed under independent workerless current Core actors; no business policy was SQL-seeded. UI/harness successors and cleanup receipts are tracked by paired UI PR103. This browser cohort was **not rerun on c93**; c93 changes only the optional guard reads and has separate551/private-native/production-metadata evidence.

Root's strict owner reconciliation subsequently passed the exact accepted Plan/submission/Core graph and all original rows. The cohort has13 domain rows,14 Procurement audit rows and3 outbox rows, with exact receipt response bytes. Original business/authentication rows were preserved, the three pre-UI sessions were only revoked, and the earlier failed maker cohort remained preserved. Cohort cookies were inactive after logout and the owned runtime cgroup was empty. This is bounded primary-flow owner acceptance, not whole-module UAT.

Root then executed the unchanged genuine guarded Down body in a rollback-only private test. Its precondition correctly distinguishes parent Plan **Active** from round **Approved**; the original incorrect precondition script is preserved separately. Retained accepted records caused SQLSTATE **55000** with the exact guard message before destructive operations. Six finite-role negatives each denied with **42501**: immutable snapshot UPDATE, pinned policy hash UPDATE, submission DELETE, receipt DELETE, Core account UPDATE and Core decision DELETE. Each statement had WHEREfalse and its own rollback-only transaction; SET LOCAL ROLE was confined to this native owner-controlled negative probe, never API transport. No role attribute/grant/membership changes were made. All three full native states (protected source237, direct239, restored239) were exactly equal before/after. No accepted plan/round/history was deleted and no committed Down is claimed. Earlier actual empty Down/Up rollback evidence remains a distinct result.

## Delta image capture — no runtime or cutover

Root's reviewed delta image build captured sourcec93 and verified all150 publication bytes from a never-started verifier. Image ID: `sha256:8fa2f59813d6670e71281b3f37ef752e7ce042cc1c349e5d63b7967b257baa66`. Changed publication bytes total22,618,172; image growth5,353,820 bytes. Production configuration/routing stayed unchanged; there was no SQL, API startup or UI staging. Image and context remain retained. Documentation commits after c93 do not change its backend tree or executable source binding.

## Evidence bindings

Paths below are private evidence directory-relative locators beneath the retained backup root; documents include no credentials, cookies, actor/business rows or secret values.

| Artifact | SHA256 |
| --- | --- |
| `procurement-plan-core-projection-compat-sdk-20261103_run01/COMPLETED.private.json` | `649d1f1079b0f39d766cd95535bdd660be51cb4c10ee6c9aafaf7ff2bc64e9ae` |
| Same SDK directory, `committed-source-binding.private.json` | `3bf12df82cd85f7f00dcac6a13fdec660c44910b0b247cf69e90fbb2e247de35` |
| Same SDK directory, `published-api-manifest.private.json` | `6ff57b4c5cbbcee999b12706e7f0ba41dc05712948ce2a471c4e48ab7829ef76` |
| `procurement-plan-core-projection-private-canary-prep-20261103_run01/run-finite-c93/COMPLETED.private.json` | `6914aff7772c94b3680c8024cbc0b6b076fa66c813dc5f7ca432ab4b0283363d` |
| `procurement-plan-core-guard-production-canary-prep-20261103_run01/emission/COMPLETED.private.json` | `69d265eaedfc77315b8ee20ce32b51a900bdd257bd90ab04f8696eaebb701efe` |
| Same production-canary directory, `production-observation/COMPLETED.private.json` | `84b524caa4d5ff56336fcd81226f1c59b0ca6c69f1d2b387dac3bb5bb33ad5ac` |
| `procurement-plan-ui-owner-prep-20261103_run01/execution-owner-r3/COMPLETED.private.json` | `dc5d6c2d9b89d15dd0f69de9f990cc40a9f6456f48ef732e84bb5d7216ecb079` |
| `procurement-plan-post-primary-native-acceptance-prep-20261103_run01/execution-r2/COMPLETED.private.json` | `9a5549dc41a5f049de877111c191fd1aeee8e1b62a9246eca42df045d3f2d2d7` |
| `procurement-plan-delta-image-prep-20261104_run01/execution/COMPLETED.private.json` | `45cce18d70e4fd428e9b89f2ef8523f72ae26821ba2be8afbcc8044a1dba55c8` |

## Remaining release and module scope

Root review and paired production rollout remain pending. Current verified backup/restore, reviewed production migration/finite role reconciliation, paired image/UI publication/routing checks, switch/configuration review and signed-in production smoke are release gates; the image build and production read-only guard test do not satisfy them. Return/edit/resubmit, rejection/withdrawal, live current-authority revocation, uncertain-command recovery, concurrency and full branch/tenant cohorts are not inferred from this one primary browser flow. Shared delegation, period authority, attachments/SLA/reminder extensions and Plan→PR retain their scoped decisions. Formal bids/evaluation/award/award→PO and the other issue145 gaps remain separate. No issue145 completion or feature-wide production activation is claimed.
