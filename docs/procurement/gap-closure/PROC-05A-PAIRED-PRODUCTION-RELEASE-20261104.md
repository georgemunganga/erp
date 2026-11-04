# Purchase Plan paired release — 4 November 2026

**Status: prepared; production deployment and live verification pending.** This is a bounded Plan release under issue169, not whole-module acceptance. Issue145 remains open. [Verified private evidence](PROC-05A-FINAL-PRIVATE-RELEASE-EVIDENCE-20261104.md) distinguishes the real browser, compiled tests, native checks and production read-only observation.

The executable candidate is backend `c93e8df4f82214b58273bf0c9be58d013aca3331`, image `sha256:8fa2f59813d6670e71281b3f37ef752e7ce042cc1c349e5d63b7967b257baa66`, and preserved UI product `3e2783b1f373857ba11681e73407c13ab09e30a8`. Published prerequisites are ERP main `44291abd6c5bf6f519f9e0c60b538e37096c37af` and UI main `ef42580fa53b5ef74c1b63d3f81d5302597f9bc1`, or reviewed descendants. Documentation merges do not require rebuilding identical application trees.

## Permanent asset publication rule

Cached clients still request earlier hashed assets. The release stages five independent new primary files and retains exact earlier cached assets through same-filesystem hardlinks. The protected `WRITE-ONCE-ASSET-ALIASES.private.json` registry records every original/alias path, content hash, size and retained mode. Existing cached assets retain their original `0644` modes; this is an operational immutability rule, not a claim of operating-system-enforced immutability. New primary files are `0444` and release directories are traversable `0755`.

Every original and new hashed asset path in that registry is permanently write-once. **Never write, truncate, chmod, chown, touch or republish a listed path in place.** Build and stage future changes in an exclusive, versioned release directory, verify hashes, then switch the routing alias atomically. An old release may be removed only under a separately reviewed retention decision; unlinking an old alias must not alter any retained alias's content. Do not run an in-place build, overwrite copy, or `rsync` into an existing registered release.

## Recovery and migration gates

The fresh online database and globals archive authenticated successfully and was actually restored into a separate PostgreSQL17.11 cluster. Its strict live comparison failed because two live tables differed from the earlier online archive. A separately reviewed supplemental result verifies the exact catalog/security/physical representation and 235 unchanged table hashes, records both differences explicitly, and preserves the original failure. It does not claim all237 live rows matched or that a later live snapshot was preserved.

Before production DDL, stop the exact API and publisher writers, verify no other ERP database clients, capture the full native237-table state, make a fresh encrypted/authenticated/fsynced database and globals backup, and verify the full native state still matches. This latest quiescent archive is a new recovery point; do not describe it as independently test-restored. The earlier actual restore verified the same dump/restore workflow and schema.

Apply the byte-exact genuine EF migration body and exactly eight new-object legacy revocations plus thirteen missing finite-role privileges in one serializable transaction. Verify all original237 tables' original-column rows, authentication records, existing roles/default privileges/functions, genuine migration history, new empty tables and the exact declared schema/ACL delta. No production policy or actor is seeded.

Unknown SQL outcomes require native reconciliation, not a retry. The old binary does not admit the post-Plan catalog, so switching only the image after DDL is not functional rollback. Prefer a reviewed forward fix; an empty-schema rollback must pass the genuine retention guards first. Never erase retained Plan/policy/approval evidence or restore an older database automatically.

## Activation and acceptance boundary

After the actual production finite-login provider checks pass, the only intended new feature setting is `Procurement:PlanReview:Enabled=true`. Preserve all unrelated held settings. The flag creates neither permissions nor a policy: sending a plan for approval still requires current Core authority, branch scope and a real effective published review policy. Ordinary Plan draft CRUD has its separate checks and does not depend on that review flag.

Before declaring the release deployed, verify the paired runtime/publication hashes, five primary and all retained compatibility assets, public route/auth behavior and anonymous browser renders with owned-process cleanup. Anonymous checks are not signed-in production business acceptance. Alternate Plan flows, remaining sourcing/award workflows, attachments and the wider issue145 backlog remain separate acceptance work.
