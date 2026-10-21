# P25 correction — stable EF identities and exact database fact ownership

**Status:** Source-only correction candidate, not executed or deployed. Preserves reviewed P25 migration; coordinator generates a new additive correction migration.

## Proven failure and intended contract

The retained actual HTTP probe `20261020_141959` passed source approval, PO funding/approval/issue/delivery and receiving-policy publication, then RecordGoods returned generic409 with no accepted effects. Read-only inspection found the persisted draft already held nonempty SourceStateBindingId/PhysicalBindingId although actual state/observation references were null. EF treats properties in principal alternate keys as immutable/generated key values; RecordGoods assigns the actual state/fact identity and EF rejects changing those keys before PostgreSQL receives the update. Service certification has the analogous mutable SourceStateBindingId principal key. This is an ORM lifecycle mismatch, not authority to relax exact source proof.

## Model correction

No table or column is added. Existing source, receipt/service/inspection, worker, quantity, scope, chronology, immutable evidence and SaveChanges guards stay in place.

- EF inspection→goods, effect→goods and effect→service relationships target the existing stable `(TenantId, LegalEntityId, Id)` identity keys.
- The exact previous goods physical tuple `(tenant_id,legal_entity_id,id,source_state_binding_id,physical_binding_id)` becomes ordinary unique index `ux_receiving_goods_physical_binding`.
- The exact previous service tuple `(tenant_id,legal_entity_id,id,source_state_binding_id)` becomes ordinary unique index `ux_receiving_service_state_binding`.
- All eight nonnullable binding GUID properties across header/line/inspection/service explicitly use `ValueGeneratedNever`. Zero remains missing legacy/draft evidence, never a fabricated identity. Immutable inspection composite evidence remains an EF principal key because it is set completely on insertion and never updated.
- Three explicit database FKs preserve exactly the prior ownership tuples. `Infrastructure/Data/ReceivingOwnershipConstraintSql.cs` provides their reviewed names/columns/install SQL. They are `DEFERRABLE INITIALLY DEFERRED`, with `NO ACTION` deletion. The stable scoped EF FKs remain immediate/Restrict; application physical deletion remains denied. Deferral accommodates service-update/effect-insert ordering inside the same transaction; it does not allow an invalid committed graph.

## Migration and maintenance contract

Root generates an additive correction after the immutable reviewed P25 migration. Up drops only the three affected old relationship FKs and their mutable goods/service alternate-key constraints, installs the ordinary unique indexes and stable scoped EF relationships, and explicitly installs all three exact database FKs **after** their referenced indexes exist. It must not rewrite or infer draft/legacy bindings, drop business rows, reset states, alter evidence hashes or create acceptance. Review generated dependency ordering before application. Failed retained P25 disposable databases can be upgraded without replaying already consumed fixture commands.

Down must run the existing protected P25 use/configuration/extension/audit guard before dropping the overlay and recreating the unsafe previous ORM shape; configured/used receiving cannot be downgraded. Restoration/forward correction is the recovery path. Removing tables in a later protected P25 Down removes constraints naturally; future column/index changes must explicitly preserve this overlay.

**The custom ownership FKs intentionally do not appear in EF model metadata/snapshot.** EF model-drift success alone is insufficient. The shared manifest query now requires all three names, exact child/parent column order and tables, referenced immediate valid unique indexes, `condeferrable`, `condeferred`, `convalidated`, MATCH SIMPLE and NO ACTION update/delete. Receiving schema readiness runs this manifest after migration/model/column/privilege checks, failing closed if absent or altered. This is a documented physical-schema contract; future P26 migrations cannot silently omit or invalidate it. A plain EF EnsureCreated schema is not operationally equivalent to the reviewed migrated PostgreSQL schema.

## Regression and actual verification requirements

Authored model tests require mutable goods/service bindings outside EF keys, ValueGeneratedNever, stable scope relationships and unique-index metadata. Persisted/reloaded tracked draft tests save goods observation/state and service certification+positive effect, and retain immutable post-fact guards. These use InMemory to isolate EF tracking; they do not prove PostgreSQL FKs or real actor/policy provenance.

Actual probe source first checks the full migrated ownership manifest. Disposable rollback-only schema mutations remove one constraint, make it nondeferrable, replace it with a same-name scoped-only lookalike, or replace it NOT VALID; each must make the manifest false, then rollback restores readiness. It also checks each migrated custom FK exists, validated and initially deferred. It starts from real HTTP-produced physical inspection/goods acceptance/service certification, attempts a mismatching physical binding or another real same-company source state under disposable bootstrap owner only, and forces the **named** constraint IMMEDIATE. Each expects named23503, rolls back and compares the unchanged original row. Execute-then-rollback without forcing constraints would not test deferred enforcement. Existing full real PR→PO→receiving producer/consumer path remains the positive test and must pass restricted runtime plus concurrency/rollback/current-access tests. Root owns SDK, generated migration/source review, fresh and retained P25 upgrade, protected Down and browser gates; none were run by this source increment.

## Distinct generated-migration source review — 6c62ee8

A distinct source review examined `20261020144755_P25ReceivingStableEvidenceBindings.cs`, its generated designer and the updated Procurement model snapshot. No blocking source defect was found. This review used source/static text comparisons only; it did not execute SDK, SQL, database or browser jobs.

- Up drops the three dependent old composite FKs before removing the two mutable alternate-key constraints. Ordinary unique indexes contain the exact former principal tuples, with all identity/scope/binding columns in order. Stable scoped EF relationships remain. The existing simple inspection→goods FK was already present and therefore requires no duplicate AddForeignKey. The immutable inspection evidence composite key/FK remains unchanged.
- Three exact ownership FKs are appended only after their referenced unique indexes exist. Their frozen migration literal matches the reviewed `ReceivingOwnershipConstraintSql.Install` contract, including NO ACTION and `DEFERRABLE INITIALLY DEFERRED`; the historical migration does not execute a mutable current helper.
- Static operation inspection found no CreateTable, DropTable, AddColumn, DropColumn, AlterColumn, InsertData, UpdateData or DeleteData operation. No row backfill, financial precision change or inferred evidence is introduced.
- Down's first SQL operation locks the relevant tables and rejects receiving configuration, any execution extensions, states/effects, command receipts, controlled/generic typed audit and outbox evidence. Only after this guard does it drop the three custom constraints, then reverse indexes/stable FKs/alternate keys in dependency order. It does not narrow amounts; the original P25 Down retains its separate precision-loss guard.
- Generated designer and snapshot model bodies are identical under a direct normalized source comparison. The original P25 migration retains SHA-256 `0a361aa9db2ee0f8f1c9e1554411b1c9e05c11ee543b0036e5eb839424a75ad1`. The reviewed correction migration SHA-256 is `d150f45c56319ac559076b3ea4df6691b59cad0d988b64b9ae71c733c1a52d23`.

The coordinator reports the focused persistence/model tests passed11/11 after correcting a test-only missing CreatedBy actor; the first fixture failure is retained. The full combined domain/shared-host/probe build, EF drift/SQL generation, fresh/retained-P25 upgrade, complete legacy preservation, protected unused-only Down/Up, structural manifest tampering and actual receiving workflow remain coordinator verification gates. This source verdict is not a database migration pass, production approval or complete Procurement claim.
