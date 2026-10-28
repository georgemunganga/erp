# M5 production technical release — 28 October 2026

The M5 API and preserved Procurement UI are deployed at https://erp.amizopower.co.zm/procurement/workspace/. This completes the technical rollout of M5a order governance, M5b receiving, M5c company-local service dates and M5d return-request source. Whole Procurement UAT remains open. Existing business flags were preserved; return-request writes remain explicitly disabled. Deployment does not approve business thresholds or activate bank/Finance execution.

## Exact release

Backend product `414ca94fe05f222d21d62d069d2428881f68db57`, published ERP main `e03ac9735379b35f789d9e900fe849d7ab02f1f1`; UI main `47cac6a13a24e9db113383f8a9cf5b319494d526`. All 150 API publication files were verified against the candidate image. All five accepted UI files match their public bytes; old hashed assets are retained. The original outbox publisher continues with its prior image/configuration. Core and Accounting aliases were preserved.

See the [exact verification record](M5-PRODUCTION-TECHNICAL-RELEASE-20261028.json). Protected receipts and encrypted backups remain on the deployment server; secrets, user data and raw exports are excluded from Git.

## Data and release checks

The fresh encrypted database/global backup was restored into a dedicated, isolated PostgreSQL instance. The real three-migration Up → Down → Up rehearsal preserved all 203 original table row projections and sequence values, with 17 new tables empty. The complete corrected migration-plus-ACL transaction was then rehearsed privately before production application.

Production now has 220 tables and migration counts HRM56 / Procurement32 / Core Approval1. Independent same-cluster reconciliation verified original rows, sequence state, owners, columns, intended indexes/constraints and exact permission changes: 21 SELECT additions and 68 automatic legacy grants removed from the new tables. Global roles, memberships, default privileges and unrelated permissions are unchanged. Restore-only deparse/bootstrap equivalence rules were not applied to production comparison.

One production safety check failed before commit because an unquoted mixed-case migration-history identifier resolved incorrectly. The full transaction rolled back; independent data/catalog/permission reconciliation proved the old state unchanged. The corrected OID-based check and complete atomic recipe passed private rehearsal before the separate successful attempt. A private whitespace error and Docker label/default normalization checks are retained in the protected attempt history; failed attempts are not counted as passes.

The read-only canary passed six local health/authentication probes with all four database connections forced read-only, unrelated workers disabled and no public ports. Production API health/readiness then passed before publisher restart and the single static UI alias change. Public checks passed ten routes/access responses, all five UI file hashes and three unauthenticated Playwright page renders with no uncaught page errors. These are not signed-in production CRUD tests.

## Remaining acceptance

Native Service certification and broader UI/branch/concurrency acceptance remain open. Principal receiving facts still need a complete invoice source, mapping, comparison and consumption contract; extending the picker alone would not complete matching. Return-request CRUD does not implement physical return execution, negative receipt facts, Inventory/credit balances or Finance postings. Supplier, sourcing, contract and other outstanding workflow gaps remain in the [UAT register](PROCUREMENT-UAT-AND-GAPS-20261027.md).

Closed, rotated 2024 supervisor logs were losslessly compressed and decompression hashes verified; regenerable Node/Playwright transform caches were removed. Backups, browser binaries, test evidence, the old API container/image and old UI assets were retained.
