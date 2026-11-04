# Purchase Plan paired release — 4 November 2026

**Status: deployed with bounded technical verification; business acceptance remains open.** This is a bounded Plan release under issue169, not whole-module acceptance. Issue145 remains open. [Verified private evidence](PROC-05A-FINAL-PRIVATE-RELEASE-EVIDENCE-20261104.md) distinguishes the real browser, compiled tests, native checks and production read-only observation.

The executable candidate is backend `c93e8df4f82214b58273bf0c9be58d013aca3331`, image `sha256:8fa2f59813d6670e71281b3f37ef752e7ce042cc1c349e5d63b7967b257baa66`, and preserved UI product `3e2783b1f373857ba11681e73407c13ab09e30a8`. Published prerequisites are ERP main `44291abd6c5bf6f519f9e0c60b538e37096c37af` and UI main `ef42580fa53b5ef74c1b63d3f81d5302597f9bc1`, or reviewed descendants. Documentation merges do not require rebuilding identical application trees.

## Permanent asset publication rule

Cached clients still request earlier hashed assets. The release stages five independent new primary files and retains exact earlier cached assets through same-filesystem hardlinks. The protected `WRITE-ONCE-ASSET-ALIASES.private.json` registry records every original/alias path, content hash, size and retained mode. Existing cached assets retain their original `0644` modes; this is an operational immutability rule, not a claim of operating-system-enforced immutability. New primary files are `0444` and release directories are traversable `0755`.

Every original and new hashed asset path in that registry is permanently write-once. **Never write, truncate, chmod, chown, touch or republish a listed path in place.** Treat partial staged releases as write-once too; retain and investigate them rather than reusing their paths. Build and stage future changes in an exclusive, versioned release directory, verify hashes, then switch the routing alias atomically. An old release may be removed only under a separately reviewed retention decision; unlinking an old alias must not alter any retained alias's content. Do not run an in-place build, overwrite copy, or `rsync` into an existing registered release.

## Recovery and migration gates

The fresh online database and globals archive authenticated successfully and was actually restored into a separate PostgreSQL17.11 cluster. Its strict live comparison failed because two live tables differed from the earlier online archive. A separately reviewed supplemental result verifies the exact catalog/security/physical representation and 235 unchanged table hashes, records both differences explicitly, and preserves the original failure. It does not claim all237 live rows matched or that a later live snapshot was preserved.

Before production DDL, stop the exact API and publisher writers, verify no other ERP database clients, capture the full native237-table state, make a fresh encrypted/authenticated/fsynced database and globals backup, and verify the full native state still matches. This latest quiescent archive is a new recovery point; do not describe it as independently test-restored. The earlier actual restore verified the same dump/restore workflow and schema.

Apply the byte-exact genuine EF migration body and exactly eight new-object legacy revocations plus thirteen missing finite-role privileges in one serializable transaction. Verify all original237 tables' original-column rows, authentication records, existing roles/default privileges/functions, genuine migration history, new empty tables and the exact declared schema/ACL delta. No production policy or actor is seeded.

Unknown SQL outcomes require native reconciliation, not a retry. The old binary does not admit the post-Plan catalog, so switching only the image after DDL is not functional rollback. Prefer a reviewed forward fix; an empty-schema rollback must pass the genuine retention guards first. Never erase retained Plan/policy/approval evidence or restore an older database automatically.

## Activation and acceptance boundary

After the actual production finite-login provider checks pass, the only intended new feature setting is `Procurement:PlanReview:Enabled=true`. Preserve all unrelated held settings. The flag creates neither permissions nor a policy: sending a plan for approval still requires current Core authority, branch scope and a real effective published review policy. Ordinary Plan draft CRUD has its separate checks and does not depend on that review flag.

Before declaring the release deployed, verify the paired runtime/publication hashes, five primary and all retained compatibility assets, public route/auth behavior and anonymous browser renders with owned-process cleanup. Anonymous checks are not signed-in production business acceptance. Alternate Plan flows, remaining sourcing/award workflows, attachments and the wider issue145 backlog remain separate acceptance work.


## Actual deployment and verification

The final API is `4b3d2136f512bce2b09b0b6442f3a69fe2f70a195629c0ad717f0e43dd032c0c`, image8fa2 above, with the existing HRM publisher retained/running. UI release is `plan-review-3e2783b1-20261104`; Nginx configuration SHA256 is `ce44af2ac8aadca4b298ab1c12589090126519adcfa35567d40d88b658d65685`. Old API and UI are retained. Actual health is healthy. Only the Plan review setting changed; all unrelated Env/HostConfig settings were preserved.

The genuine Up/security transaction passed once: schema237→239, two new empty tables, eight new-object legacy rights removed and thirteen missing finite-role privileges added. All original237 original-column business/authentication rows, roles, memberships, default privileges and existing functions were preserved. Five actual production-purpose checks passed through independent execution and ordinary Record LOGIN connections, all forced read-only; full B1 READ remained false/held, with no extra B1 grants. Observed invoice/unit/owner/manual readiness does not activate those workflows.

The first paired cutover reached healthy API/auth/all150-file verification, then failed its immediate exact public HTML check. Cleanup restored the old UI alias and stopped the candidate/publisher; the old API remained stopped after schema Up. Original failure is retained. Root's stopped-state reconciliation found the complete239 native state unchanged. The reviewed continuation restarted the **same** candidate, accounting exactly for Docker's image-default plus payload label merge, and polled fresh public HTML. It observed exact new HTML SHA256 `3897490b9a6a183efb68aad9cdb97a3a630520fd567ff2eb2bc76e9a6cd17719` on its first poll. No first-attempt success or definitive original mismatch cause is claimed. No migration, grants or container creation were replayed.

Public verification then passed44 unique files (five primary plus40 compatibility assets with one CSS overlap), exact status/hash/size,22 route/auth checks, and7 real anonymous Playwright renders across sign-in, HRM, Core and Procurement. Page errors and blocked mutations were zero. The exact owned browser service was stopped and its cgroup verified empty. The HTTP harness's first offline attempt rejected differing metadata shapes before any HTTP; its successor projects only the declared hash/size fields and retains every individual byte/status check. This is not signed-in production CRUD/UAT acceptance.

| Protected receipt (relative to backup root) | SHA256 |
| --- | --- |
| `procurement-plan-production-rollout-20261104_run01/schema-execution/COMPLETED.private.json` | `2c94063cfc80121f61771956028fb15d8e660d8a76ccbd5fb0090ecee5f68112` |
| `procurement-plan-production-purpose-canary-prep-20261104_run02/post-up-readonly-r1/COMPLETED.private.json` | `0f4e4905175b9e9cd8749c6e9c17f259e2594325e8ed66e03415fa4ee26609d1` |
| `procurement-plan-production-rollout-20261104_run01/same-candidate-resume-r1/COMPLETED.private.json` | `39f129ed98cf1519d642e6ec3cfbc1ac7f001669ce97de97bb5d9d45354cdeb6` |
| `procurement-plan-production-rollout-20261104_run01/public-http-prep/execution-resume-r2/PUBLIC_HTTP_COMPLETE.private.json` | `f8d1889523b80c8768a3cea9fb2a53329f415d8af1f18753b3f82c3b868b4ad6` |
| `procurement-plan-paired-release-prep-20261103_run01/public-smoke-source/execution/COMPLETE.private.json` | `e84e9e4ab9ac90d9b0860a54da471941c0790238568721e0b05eb64a2dce378f` |

The latest quiescent encrypted archive was authenticated and native-stability checked, not independently test-restored. The earlier online archive has its actual isolated restore and explicitly narrower supplemental comparison. The owned private test PostgreSQL instance was stopped with data/configuration/volumes retained for public-browser resource admission; production PostgreSQL remains running. Archived system-journal/cache cleanup did not delete ERP audit data, business records, source, recovery archives or current browser tools.
