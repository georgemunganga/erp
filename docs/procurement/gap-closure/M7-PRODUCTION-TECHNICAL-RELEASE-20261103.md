# Procurement M7 technical deployment — 3 November 2026

The verified API, cumulative Core/Procurement schema and preserved Procurement UI are deployed at https://erp.amizopower.co.zm/procurement/workspace/. This is a technical release, not whole-module UAT acceptance. Quotation recording and capture remain explicitly off; other existing application settings are preserved.

## Exact deployed source

- Backend source `fb743fac069d6d922dcc9011dc5e24bbeb752777`, published through ERP main `080e342584d234e9242f9ae2355e86cf4d00e7a8` (PR150).
- API image `sha256:bce1defe5d25abff92223541b1b1501fb1d52b62696779e53de2155c59f2d906`; all 150 publication files matched the tested build.
- UI source `a57fc67ab60ced547784499633e67330fa29f5b7`, published through UI main `c692611da5ca549ee00b1356cf7bc1e213b096da` (PR98). All five public build files matched their staged hashes; old assets remain available.
- PostgreSQL is the same existing cluster and volume: 231 tables; migration counts HRM57 / Procurement40 / Core Approval1. No database replacement or production Down migration occurred.

## Release verification

A fresh encrypted online database/global backup was restored into an isolated PostgreSQL instance. The complete atomic Core-first M6/M7 migration and finite-role recipe passed there, retaining all original 220 tables’ projected data. A separate fresh encrypted final backup was captured with writers stopped before production migration. Keys, credentials, raw backups and protected capture details remain outside Git.

Production applied the recipe in one transaction, including in-transaction original-data fingerprint guards. Independent owner reconciliation verified all original projected rows/sequences, the exact named physical target, table/column permissions and unchanged organization-wide role/security configuration. The previous API and publisher were retained and resumed when the initial canary diagnostic copy failed; the successful continuation reused the verified schema rather than repeating migrations or restoring old data.

The read-only canary forced all four connection strings read-only, disabled background/bootstrap work and exposed no public ports. It passed health/auth probes and called the actual quotation readiness provider against the real production schema, enlisted Serializable transaction and limited role. This caught and verified the narrowly scoped compatibility fix for the two captured M4 constraint spellings; arbitrary constraint changes remain denied. All 219 focused Host tests passed before building the final image.

The new API is healthy. The existing publisher runs on its unchanged image/configuration. Runtime settings, secrets, mounts, ports and other module Nginx configuration were verified; only the planned image source label, two default-off quotation switches and Procurement static alias changed. Thirteen public route/auth checks, all five exact UI asset hashes and three anonymous production Playwright renders passed with no uncaught page errors.

Initial private restore/tooling failures and the interrupted production canary diagnostic step remain in the protected attempt history. They are not counted as passes. The final successful canary transferred only the four managed probe files through its writable temporary filesystem. No records were rolled back. The previous API container/image and UI release remain available for recovery; do not downgrade populated schemas automatically.

## Remaining acceptance

Signed-in production CRUD, the complete positive quotation contact/create/revise/cancel/recovery journey, its preserved UI integration, and the other gaps in the [UAT register](PROCUREMENT-UAT-AND-GAPS-20261027.md) remain open. Manus owns [quotation UI milestone 148](MANUS-M7A-QUOTATION-UI-MILESTONE-20261103.md); Root owns native workflow verification and activation. Finance/payment execution is not manufactured by this deployment. GitHub Actions is billing-blocked; local/native results are separately recorded.

## Protected evidence digests

- `fresh-rehearsal-r3.private.json`: `a9cbbd14735ae957e72b2dffa9fb0d4bd92650eaf4a4222fd17f75f3d798a28c`
- `poststop-backup.private.json`: `1a0fa59e4fb6b0b322bfaedf667865d1f553f6cd083c084421f5ee13e5372c5f`
- `production-owner-verified.private.json`: `91818189d0643998f67927fb8746e4d74f7b012863eba1162bbc41b1dc69cb07`
- `canary-native-probe-r3.private.json`: `e4df415d8006fa38a92bf3f5ab25340d19b83879439bb51f5a631300e99525ca`
- `canary-accepted-r3.private.json`: `c75ec9e41cefdc5a33f9316a17b6cda3d04cef97f1702ace165414d5d62c8a49`
- `CUTOVER_COMPLETE.private.json`: `be9b60ebfa908d0cc95ce25c038d496708c348745675d7c7d5642e3cbb49e5f8`
- `public-verification.private.json`: `6bad7d186c6fec876e2ff23316e02895b861761c43460641895e517a52711a9b`
- `public-browser.private.json`: `dbd7b12e50817626664f6a2701c8e9f36934682ccc28240129dce537d889c3ce`
