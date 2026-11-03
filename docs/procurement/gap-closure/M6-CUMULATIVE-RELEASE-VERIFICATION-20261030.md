# M6 cumulative release verification — 30 October 2026

Status: preparation in progress; production not upgraded. Whole-module UAT remains open.

Published prerequisites are ERP `a3381969581c613e2e583036d3d3e8bf176440f2` and UI `06347d3029361533962b972556ab1d1952d68602`. The accepted release binaries are backend `f1e5102e1cfd7c530ecb297607c8eb67ad70083b` and UI `9c56b49212573cefacbb860085b6955ed0ea9bd2`. New M7 quotation code is excluded from this release.

## Observed production baseline

A bounded, read-only metadata transaction confirmed PostgreSQL 17.11, 220 tables, HRM migration `20261023130726_CoreCompanyTimeZone`, Procurement migration `20261028180000_AddPrincipalGoodsReturnDrafts`, and the existing Core Approval migration. Database size was 42,251,955 bytes. The inspection captured schema, security and migration metadata, not business rows or credentials. Before/after production container identity and configuration guards passed.

Inventory receipt SHA-256: `1be90b43344f85e03d0bf3a51a24d8e14e3c7650c463dbcc31d2c391a91f0414`.

## Genuine cumulative scripts

The actual accepted release assemblies generated Core-first Up/Down scripts for one Core migration and seven Procurement migrations. A tiny offline wrapper compiled against the immutable release publication; it did not compile newer application code, connect to a database or execute SQL. All 150 release publication files remained unchanged. The generated final M6E script matched the previously accepted script text. The bounded generation container exited successfully and was removed.

Generation receipt SHA-256: `4bda4ce3d250a6882c07e3b2560e2ca291db1962d8fd9b97f160b3f2413a4fc8`.

| Script | SHA-256 |
| --- | --- |
| Core Up | `557cff1ff6eb94ced4cf893e2d0f4f58fc312f430f89c9554aa5ff0befb504d6` |
| Core Down | `ef840b1b5a8990109db7e8576b8bb32fbc737bcf8347ddd8a962bfd8ad5d7262` |
| Procurement Up | `c0aada0f981c9f8bd6f43c76e2be5f24d3fe056d7e1a8eb24dc9c8c4621004e3` |
| Procurement Down | `0b3fae42450afab0457c1e3667e62492416c067003f59dd97aeacde9ea879977` |

The initial wrapper attempt failed during offline restore because its NuGet configuration contained an invalid credential section. It generated no SQL or application build. That failure is retained separately; the corrected successor is the successful result above.

## Verified restore and upgrade rehearsal

The fresh encrypted backup and independent full restore comparison passed. Receipt `05e69d33fd79fc81a3b0f3241b09f2a055bb033e48a54799eb1d64b6dad05fa0` binds all original 220 tables' row digests, sequence values, named permissions, roles and schema. Exactly 36 constant-array CHECK deparse differences and three built-in monitor-membership grantor differences were reviewed for the isolated bootstrap; the comparator does not accept arbitrary normalization.

The complete Core-first plus seven-Procurement Up → unused Down → Up rehearsal passed on that isolated restore: receipt `96b2a44c872905d7979e1bdcbd2d053ca99036e47f00cbf49b82273460d5905d`. All original rows, sequence values, security and original column positions were retained. The exact eleven dropped-column tombstones and subsequent column positions were checked explicitly. The first genuine M6E downgrade guard also refused retained audit/outbox markers with SQLSTATE 55000; the synthetic probes rolled back, preserving the full 229-table state (`0e033927db4373f02b17656336495f8cc031997a1f8bd132187633091ba8c8b0`). This marker test does not replace the separately recorded genuine replacement-workflow evidence or prove every populated downgrade path.

## Open finite-role correction — 3 November 2026

The isolated ACL run applied its exact 42 SELECT grants and 28 legacy privilege revocations. Its full before/after snapshot allowed only those intended ACL changes. The execution role passed the actual compiled read profile; the purchasing record role still failed. The run remains failed and its original artifacts are retained.

Independent inspection of the saved after-state and compiled predicate found a pre-existing INSERT grant on `procurement.finance_payment_status_refs` for `erp_proc_record_finite_v1`. The compiled profile correctly rejects that grant: Finance owns settlement facts. Manual external-payment reporting does not write this table.

[The release permission script](../../../backend/procurement-api/ops/m6-finite-read-role-grants.psql) retains the 42 read grants and seven new-table legacy revocations, then removes this one incorrect existing INSERT grant. It requires explicit database/cluster parameters, the actual database owner, installed migrations and existing limited roles, and runs transactionally. It changes no business rows, application permission CSVs, role memberships or global default ACLs. It still requires fresh isolated rehearsal of both actual role profiles and independent before/after comparison; it has not been applied to production.

## Remaining release gates

A fresh rehearsal of the corrected permission script, reviewed paired backend/UI rollout, signed-in production verification and post-release monitoring remain required. The isolated restore expired at its finite lease and stopped; it is not a running job. The live services resumed on the same images after the backup window. Production schema, application permissions and UI remain on their earlier release. Whole-module UAT remains open.
