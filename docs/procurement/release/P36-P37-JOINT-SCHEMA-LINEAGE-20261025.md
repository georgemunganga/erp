# P36/P37 joint schema lineage

This source branch merges the published P36 readiness source (`fb082225`) and the independently prepared P37 source (`d8bcb879`) onto root `33ab68cc`. Its EF model snapshot is byte-for-byte the genuine P36 snapshot from `fb082225` (SHA-256 `5782d8bab4c61193e6695e0e7e7586b11e1c5eb0bdb4e7f655c94bac039380c9`).

The two P37 migration files generated independently from the pre-P36 snapshot were removed from this joint tree. Their original commits and private raw generation evidence remain intact. P37 model, ownership SQL, rollback guard, and service source remain present. The joint tree intentionally has no P37 migration class yet; tests that reference that class cannot compile until a genuine P37 migration is generated against the P36 snapshot.

The next schema step is a fresh, official offline EF generation on this exact joint source tree. It must produce only a new P37 migration body, Designer, and snapshot. Review the raw generated triplet before adding authored ownership/rollback operations to the migration body. Do not edit the Designer or snapshot by hand. Then run model/host tests and a separate private PostgreSQL Up/Down/Up and retained-data veto rehearsal. Independent P37 migration proof does not establish this joint migration's correctness. Neither P36 nor P37 is activated or deployed by this merge.
