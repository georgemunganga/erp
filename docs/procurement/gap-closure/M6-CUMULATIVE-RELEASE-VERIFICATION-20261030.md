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

## Remaining release gates

Fresh encrypted backup and independent restore verification; cumulative upgrade against the restored production baseline; exact production finite-role permissions; unused downgrade/re-upgrade checks and populated downgrade refusal; paired backend/UI rollout; signed-in verification and post-release monitoring remain required. Generate scripts alone does not satisfy these gates. The original database, UI, workflow settings and permissions have not been changed by this preparation.
