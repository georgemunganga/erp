# P31 alternate-key names — generated correction and authored transport

Status: source only; post-generation build, PostgreSQL and runtime acceptance pending.

The successful genuine EF command generated `20261024055739_P31PrincipalInvoiceConstraintNames`, its Designer, and the owning snapshot from source `7e01c2ba`. Generation evidence is protected under `procurement-p31-key-names-generation-20261024_run02`. The preceding 768 MiB compiler attempt failed with exit137; it is retained and was not a migration result. The successful serial SDK build used the original1500 MiB allowance and changed no database.

The three explicit model names are `ak_pi_issued_order`, `ak_pi_receiving_source`, and `ak_pi_receiving_fact`. They name existing exact ownership keys; they introduce no new business fields, data adoption or weaker relationship. Genuine generated Designer and snapshot bytes remain unchanged. Raw generated source hashes and copies are retained separately from the authored migration body.

EF scaffolded unique-constraint drop/add operations. The initial P31 migration already creates foreign keys depending on those keys. The authored body therefore renames the three constraints in place rather than removing them. PostgreSQL also renames the underlying indexes, with no stored-data change; see the [PostgreSQL17 ALTER TABLE reference](https://www.postgresql.org/docs/17/sql-altertable.html). The intended FK/index identity preservation still requires the actual owning PostgreSQL rehearsal; source review is insufficient.

Down begins with `P31PrincipalInvoiceOwnershipGuards.BeforeDownSql` before reversing the three names. No generated metadata or historical migration is manually rewritten. Retained P31 business rows must veto downgrade through the existing13-family guard.

Required next evidence: successful clean build and model parity; genuine combined P30-to-latest-P31 Up/Down exports; private Up/Down/Up with exact FK/key names and relationships; retained-family downgrade vetoes; actual finite-role invoice HTTP and existing UI browser workflows. No production rollout or full invoice integration is claimed.
