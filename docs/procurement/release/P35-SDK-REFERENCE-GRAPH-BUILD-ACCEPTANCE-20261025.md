# P35 SDK reference graph build acceptance

**Status:** SDK06 reference-graph rebuild and SDK07 targeted API/Host validation accepted; business workflow release remains held. This note records build evidence only. It does not activate P35 or replace private PostgreSQL and HTTP acceptance.

## Failure record

- Unified SDK04 failed compilation with CS1061 in `ProcurementRfqCommercialStoreReadiness` and CS0136 in `ProcurementPrincipalCommercialSchemaInspection`. Its log and partial cache remain failed evidence. Source `d81ad414` corrected those source errors before the next run.
- Unified SDK05 compiled, then its 69 focused Host cases yielded 63 passed and six failed. The failures reported missing `procurement-contract-submit` authorization policy. The current source catalog contains that action, but the reused `Hrm.Application.dll` did not contain its UTF-16 string. This was a stale compiled reference graph, not a passing authorization test. The SDK05 log, TRX and cache lineage remain failed evidence.
- SDK06 at source `f5ed339d91b24f7c374551552ec84fa87a8dad85` passed the full reference-graph rebuild and tests recorded below; SDK07 separately validated the exact Supplier-index selector fix.

## Acceptance procedure for cross-worktree .NET builds

1. Freeze the physical source tree and record the exact source commit, tree hash, SDK image, package cache, and build artifact lineage. A reused artifact directory is a **cache continuation**; record its before/after inventory and prior failed or accepted runs. Never label it a fresh build.
2. When source may have changed outside the current worktree, force a full reference-graph `Rebuild` of the relevant application, infrastructure, host and test projects, or clean only the owned disposable build cache and compile the full graph. File modification times and an incremental Host-only build do not establish which source produced referenced DLLs.
3. Before tests, verify the current built assemblies against critical source contracts. For this candidate, both `Hrm.Application.dll` and the Host test output copy must contain the three P35 action keys: `procurement-contract-submit`, `procurement-contract-review-read`, and `procurement-contract-review`. Pin the assembly SHA-256 values. This check supplements, rather than replaces, compilation and authorization tests.
4. Accept only an exit-zero run with complete source-before/source-after equality, current assembly inventory, test TRXs and exact pass/fail/skip counts. Keep failed attempts immutable. Publish from the accepted output graph only, then hash the entire publication; a tool-only overlay must separately prove that every product file is unchanged.

Private SDK04 and SDK05 evidence is retained under `/home/amizo/erp-backups/procurement-p35-unified-source-validation-20261024_run04` and `..._run05`. The accepted SDK06 execution is `/home/amizo/erp-backups/procurement-p35-unified-source-validation-20261025_run06`. These local paths are evidence locations, not deployment targets.

## Actual SDK06 closure

The forced reference-graph rebuild at source `f5ed339d91b24f7c374551552ec84fa87a8dad85` passed **1,297 Host, 1,632 Domain, 69 focused Host and 15 focused Domain tests**, with no failures or skips. Its private result SHA-256 is `5075c34eca99f7a0b6eaf5c3c6b1e1b64ed3151f1aeb2403eb00d88a8aefc1d4`. This is a cache continuation from the preserved failed runs, explicitly rebuilt and inventoried; it is not described as a fresh clean build.

Root froze an immutable 477-file Host product graph for later P33 tool-only compilation under `/home/amizo/erp-backups/procurement-p35-sdk06-native-publication-prep-20261025_run01`. Its complete publication manifest SHA-256 is `c9d63f0fb2de6ccc1e73c9b4c455cdfb94db6f833ac0c0c2e7e861e86c2eba64`, and provenance SHA-256 is `3fabe58955d57acf7b9a3f426a5329c88ec4b21e9d0178eface71ae5cbbeab09`. The current `Hrm.Application.dll` SHA-256 is `4de3187d918030d904cdc7cf48f2cda329fc210e126bb0385bbc07bce380ffc6`; the freeze helper checked all three P35 action keys against that assembly. This publication is a product base for a future tool overlay, not a P33 browser result or production deployment.

SDK04 and SDK05 retain their original failure evidence. P33 cookie01 later failed after124native requests at an unset-scope fixture readiness preflight, before publishing its cookie fixture manifest or running any browser; corrected tool `e66be30a8590c0c5e9674313e603f068dbce6631` and a fresh browser are separate pending gates.


## Actual SDK07 targeted closure and workflow holds

[SDK07 source receipt](P35-SDK07-SOURCE-ACCEPTANCE-20261025.json) records source `521c986ceffcb11411751c68707f49d40d92c493`: **1,305 Host and77focused Host passed**, zero failures/skips. SDK07 did not rerun Domain; its1,632accepted Domain tests remain SDK06 evidence with unchanged Domain source and non-target reference bytes. Model/SQL exports were also not repeated because their source is unchanged. The immutable477-file publication changed only API DLL/PDB/deps from SDK06; complete manifest SHA is `ec1dde5fd31116e95d0b655290f0c39d9c2783f3683b4e718849145502fe2449`.

P35 native01 completed105P30 prerequisite HTTP requests, then failed physical admission before all P35 policy/contract calls. The finite execution-role privileges matched; the captured Supplier registration partial index lacked selector support. SDK07 validates the narrow exact-predicate product fix, not positive P35 execution. Original failed01 logs/result/database remain retained, and fresh02 plus independent owner and UI gates remain pending. The separate private Core session38HTTP/owner/HBA-restoration acceptance is recorded in [its existing receipt](P30-CORE-SESSION-ACCEPTANCE-20261025.json); no production authentication or whole-module completion follows from it.
