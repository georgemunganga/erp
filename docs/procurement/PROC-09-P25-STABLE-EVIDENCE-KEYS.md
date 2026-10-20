# P25 correction — stable EF identities and exact database fact ownership

**Status:** Source-only correction candidate, not executed or deployed. Preserves reviewed P25 migration; coordinator generates a new additive correction migration.

## Proven failure and intended contract

The retained actual HTTP probe `20261020_141959` passed source approval, PO funding/approval/issue/delivery and receiving-policy publication, then RecordGoods returned generic409 with no accepted effects. Read-only inspection found the persisted draft already held nonempty SourceStateBindingId/PhysicalBindingId although actual state/observation references were null. EF treats properties in principal alternate keys as immutable/generated key values; RecordGoods assigns the actual state/fact identity and EF rejects changing those keys before PostgreSQL receives the update. Service certification has the analogous mutable SourceStateBindingId principal key. This is an ORM lifecycle mismatch, not authority to relax exact source proof.

## Model correction

No table or column is added. Existing source, receipt/service/inspection, worker, quantity, scope, chronology, immutable evidence and SaveChanges guards stay in place.

- EF inspection→goods, effect→goods and effect→service relationships target the existing stable `(TenantId, LegalEntityId, Id)` identity keys.
- The exact previous goods physical tuple `(tenant_id,legal_entity_id,id,source_state_binding_id,physical_binding_id)` becomes ordinary unique index `ux_receiving_goods_physical_binding`.
- The exact previous service tuple `(tenant_id,legal_entity_id,id,source_state_binding_id)` becomes ordinary unique index `ux_receiving_service_state_binding`.
- All seven nonnullable binding GUID properties across header/line/inspection/service explicitly use `ValueGeneratedNever`. Zero remains missing legacy/draft evidence, never a fabricated identity. Immutable inspection composite evidence remains an EF principal key because it is set completely on insertion and never updated.
- Three explicit database FKs preserve exactly the prior ownership tuples. `Infrastructure/Data/ReceivingOwnershipConstraintSql.cs` provides their reviewed names/columns/install SQL. They are `DEFERRABLE INITIALLY DEFERRED`, with `NO ACTION` deletion. The stable scoped EF FKs remain immediate/Restrict; application physical deletion remains denied. Deferral accommodates service-update/effect-insert ordering inside the same transaction; it does not allow an invalid committed graph.

## Migration and maintenance contract

Root generates an additive correction after the immutable reviewed P25 migration. Up drops only the three affected old relationship FKs and their mutable goods/service alternate-key constraints, installs the ordinary unique indexes and stable scoped EF relationships, and explicitly installs all three exact database FKs **after** their referenced indexes exist. It must not rewrite or infer draft/legacy bindings, drop business rows, reset states, alter evidence hashes or create acceptance. Review generated dependency ordering before application. Failed retained P25 disposable databases can be upgraded without replaying already consumed fixture commands.

Down must run the existing protected P25 use/configuration/extension/audit guard before dropping the overlay and recreating the unsafe previous ORM shape; configured/used receiving cannot be downgraded. Restoration/forward correction is the recovery path. Removing tables in a later protected P25 Down removes constraints naturally; future column/index changes must explicitly preserve this overlay.

**The custom ownership FKs intentionally do not appear in EF model metadata/snapshot.** EF model-drift success alone is insufficient. The shared manifest query now requires all three names, exact child/parent column order and tables, referenced immediate valid unique indexes, `condeferrable`, `condeferred`, `convalidated`, MATCH SIMPLE and NO ACTION update/delete. Receiving schema readiness runs this manifest after migration/model/column/privilege checks, failing closed if absent or altered. This is a documented physical-schema contract; future P26 migrations cannot silently omit or invalidate it. A plain EF EnsureCreated schema is not operationally equivalent to the reviewed migrated PostgreSQL schema.

## Regression and actual verification requirements

Authored model tests require mutable goods/service bindings outside EF keys, ValueGeneratedNever, stable scope relationships and unique-index metadata. Persisted/reloaded tracked draft tests save goods observation/state and service certification+positive effect, and retain immutable post-fact guards. These use InMemory to isolate EF tracking; they do not prove PostgreSQL FKs or real actor/policy provenance.

Actual probe source first checks the full migrated ownership manifest. Disposable rollback-only schema mutations remove one constraint, make it nondeferrable, replace it with a same-name scoped-only lookalike, or replace it NOT VALID; each must make the manifest false, then rollback restores readiness. It also checks each migrated custom FK exists, validated and initially deferred. It starts from real HTTP-produced physical inspection/goods acceptance/service certification, attempts a mismatching physical binding or another real same-company source state under disposable bootstrap owner only, and forces the **named** constraint IMMEDIATE. Each expects named23503, rolls back and compares the unchanged original row. Execute-then-rollback without forcing constraints would not test deferred enforcement. Existing full real PR→PO→receiving producer/consumer path remains the positive test and must pass restricted runtime plus concurrency/rollback/current-access tests. Root owns SDK, generated migration/source review, fresh and retained P25 upgrade, protected Down and browser gates; none were run by this source increment.
