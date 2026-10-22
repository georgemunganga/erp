# P23 actual schema capture and remaining release gates

## Source checkpoint

P23 source `aa49760` passed SDK gate `20261022_120639`: actual 38 focused and 542 selected host tests, four complete publications, genuine forward/reverse/idempotent SQL, and actual Procurement/Core Documents model checks. The earlier 1194 Procurement, 112 Core Documents and 20 Core Approvals tests are explicitly reused only through unchanged dependency/source-byte proof from `dbf5107` gate `20261022_114512`. That earlier host failure (523 passed / 14 failed) is retained; two test-only fixture corrections preserve production authorization guards.

Reviewed browser fixture `939a1d2` was normally merged at `60400ff`. Its runtime has not been verified. These source pins and browser additions require a new source-bound SDK publication before any operational test.

## Actual PostgreSQL evidence

The protected `procurement-p23-private-capture-20261022_1235` run completed `PASS_PRIVATE_DDL_CAPTURE_ONLY`, using reviewed runner `63efeaa03d8e226817fc72d8ddaa3acb9eea745154c4166252a539d278b92903` on the exact network-isolated private PG17 cluster. It created a new disposable clone of the stopped retained P22 CRUD database; production was not connected or changed.

- Genuine Up: `b75973c75ad791590ee6a6e1d9d4c38f44c83ac4ed6ddb24192fb7ce0500b3f7`.
- Genuine unused-only Down: `eb7bf4cb251074c0fa9b842093880d0f1bacdcdf677731d178c41528bce82400`.
- Actual seven validated CHECK definitions: [capture](sql/P23-actual-pg17-check-capture.json), SHA-256 `852061b7fbc71a789ed74b7700e7982378dca7f23865ae7951c969f7f123f423`.

Up preserved every original persisted value. Unused Down restored the original catalog/data/history; re-Up restored the same semantic catalog. PostgreSQL allocates a new physical `attnum` when dropping and re-adding a column: only the newly introduced mapping discriminator moved 21 → 22; its type, non-null rule, default 1, and all original column positions/constraints/indexes remain exact. Both raw catalogs and the explicit physical-slot difference are retained.

Synthetic audit-only and outbox-only markers independently produced actual SQLSTATE `55000` with the exact reviewed veto message and no residual effects. These are rollback safety tests, not evidence of a real business workflow. The retained source was rechecked unchanged before final PASS was written.

Two earlier conservative capture failures are preserved: initial strict physical-slot comparison, then the assumption that the CRUD baseline already contained an outbox row. Successor runners use a literal typed rollback-only outbox marker and keep original table/value comparisons strict.

## Source constants and default deny

The actual generated ID `20261022112145_P23CorePrincipalInvoiceUnitReview` and the seven actual PG-rendered CHECK definitions now populate the review readiness contract. No model SQL, guessed formatting or ready provider substitutes for capture. Actual applied migrations, schema shape, current finite-role privileges, Core principal/actions/data scope, same-store document retention and transaction guards must still pass. Feature flags and retirement grants remain off by default; captured catalog facts do not enable a feature.

No migration, Designer or snapshot bytes changed in this source increment. All 139 tracked artifacts remain identical to the reviewed generated P23 checkpoint.

## Explicit outstanding gates

1. The standalone CRUD baseline has no Core Approvals schema. **Core-case-only downgrade veto: NOT_RUN_REQUIRES_CORE_MIGRATION.** It remains a release hold until tested against genuine Core-owned migrations; no synthetic Core table was created to force a result.
2. Current pins/browser fixture source needs fresh SDK/host gates and complete publications. Then run the bounded finite-role actual HTTP/browser review workflow; no current positive workflow/authentication/finite-role claim is made by this DDL run.
3. Real upload/scanning/download requires the genuine Core service, private mounts and fresh official ClamAV definitions. Synthetic available documents cannot certify scanner operation or freshness.
4. Finance/AP is not implemented. Historical exposure and AP owners remain unavailable; there is no zero-exposure assumption or invented ledger checkpoint. Original-unit review verifies only supplier original units. It does not approve an invoice, establish qualified financial matching, post accounting, create an AP liability, or execute payment.

No production SQL, grants, scanner configuration, review flags or Finance defaults were changed by these preparations.
