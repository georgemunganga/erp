# M7B1 private SQL-role acceptance — 3 November 2026

Status: **bounded private database, Core-cookie API, current-access and corrected original-UI continuation acceptance passed; paired production release and activation remain open.** Parent issue #145 remains NOT READY. This checkpoint belongs to #162 / draft PR #165.

## Accepted source and database boundary

The probes used the actual compiled profiles from backend source `7ff14298117daf9b19ec5d301139693a19c272e2`, publication 26. Its genuine EF migration is `20261103163108_M7b1CommercialRoundDraftPlans`. The isolated target contains 237 tables, including six new commercial-round tables; the existing source database contains 231. This follows the separate empty upgrade/downgrade/upgrade acceptance, not a production migration.

The measured schema fingerprint is `24e096b8ea495da24c7eab1ea2b0bd1fda9cc54754d2d2b5f9ca923cd743133b`. The publication does not enable the workflow flags by default.

The temporary direct-login database role has no password, inherited memberships, ownership or elevated role attributes. It has exactly the required controlled Core read projections, sourcing reads, six new-table inserts, mutable-plan update, source/request ID updates needed for row locks, and audit/outbox inserts. It has no Core identity writes or Finance writes. A passwordless role here is a fixture on an isolated, unexposed PostgreSQL instance, not a production authentication recommendation.

## Actual checks completed

| Check | Actual result |
|---|---|
| Independent semantic ACL comparison | All previous grantor/grantee/privilege/grantability edges preserved; exactly 107 declared new-role edges added. Database owners/settings unchanged. |
| Rollback-only grant preview | Full native state and semantic ACLs restored before committing the same grant scripts. |
| Direct-login positive profile | `session_user = current_user` for the finite role; READ, RECORD and the legacy barrier profile passed; measured fingerprint matched. No `SET ROLE`. |
| Withdraw all six new-table INSERT rights | READ remained available; RECORD became unavailable. |
| Restore those exact INSERT rights | RECORD passed again; full native metadata, rows and semantic ACLs matched the committed baseline. |
| Temporarily permit Core identity UPDATE | Actual direct-login READ and RECORD both rejected the unsafe role. |
| Temporarily permit Finance payment-status INSERT | Actual direct-login READ and RECORD both rejected the unsafe role. |
| Temporarily permit immutable revision UPDATE | Actual direct-login READ and RECORD both rejected the unsafe role. |
| Temporarily permit Procurement schema CREATE | Actual direct-login READ and RECORD both rejected the unsafe role. |
| Temporarily add CREATEDB | Actual direct-login READ and RECORD both rejected the unsafe role. |
| Restore after each unsafe case | Exact full native state, semantic ACLs and healthy READ/RECORD verified before the next case. |
| Direct-login SERIALIZABLE READ ONLY transaction | READ passed; RECORD was unavailable. |

These are real PostgreSQL privilege probes, not mocked permission results. No business record, Core application user, Core application role or application session was created or modified. The original source database remained exact apart from the explicitly declared cluster-level temporary SQL role. The temporary SQL role remains for the next reviewed private API stage; it has not been deployed to production.

## Evidence bindings

Private receipts retain the exact SQL, compiled-profile provenance, before/after row hashes, all relevant native metadata, independent semantic ACL edge sets, direct-login results and operation intent records. No passwords, cookies or customer records are copied into this document.

| Receipt | SHA-256 |
|---|---|
| Finite grant/profile completion | `e37b04bf007a9ad8e0951e237b85e77b212eb0651f108efc6ff8aa05666be7d9` |
| Five unsafe-privilege cases completion | `4caeba3994d895880aec53827ea07b777115bff6f3db5aa061f51ce05543128d` |
| Restored full target native snapshot | `33abc2d86da42d170627f9307dd5dc7eb3ff43b56f451b2fe0f18208f6c2b2e4` |

The first receipt's relative plan argument is explicitly resolved by a separate Root collection receipt with the recorded execution working directory. The original receipt has not been rewritten.

## Remaining acceptance

Publication26 established the initial role profile. The successor B1-only safe-read guard is now compiled and privately tested. Publication28, source `0f9bbc0bc9a2c236b40613d46538751323b78eaf`, passed 411 Host checks. Actual direct-login grants of SELECT on credential/password and payroll gross-pay columns made READ and RECORD unavailable. Withdrawing schema USAGE from the referenced-table owner made RECORD unavailable while READ remained available. Each case restored exact native/semantic metadata and healthy profiles; no credential or payroll values were read.

Actual workerless Core-cookie create/revise/cancel, original-key recovery and same-cookie current permission/branch denials have now passed, as detailed below. Outstanding gates include cross-tenant acceptance, retained legacy-publication barriers, marker-only retention coverage, actual Playwright acceptance through the imported screens and paired release. The actual populated-evidence Down guard passed the bounded read-only probe below. These checks do not establish formal RFQ review, sealed submissions, bid opening, scoring or award, and do not complete whole-module UAT.

## Preserved first Core-cookie command failure and owner prerequisite

The first pub27 real-cookie journey authenticated both existing Core handlers successfully, but the initial Create returned503. PostgreSQL reported that its internal referential-integrity SELECT against `commercial_round_enrollments` lacked procurement schema access under the referenced table's owner. All six new tables are owned by the existing NOLOGIN `erp` role, while this private clone's schema remains owned by `postgres`; table ownership did not implicitly grant schema USAGE. The finite execution role already had schema USAGE. At that failed checkpoint no round business rows persisted (all six new tables were empty); both session cleanups succeeded and old cookies were denied afterward. This failed attempt remains separate evidence, not a successful CRUD result.

The now-tested successor source adds an explicit **record-only** prerequisite checking schema USAGE for all six referenced-table owners. Retained READ does not acquire this write prerequisite. The separately completed private correction granted only `USAGE ON SCHEMA procurement TO erp`, with no CREATE, LOGIN, membership, ownership, Core, Finance or business-data change; exact native/semantic proof and Root review were completed before applying it. The ordinary production ownership arrangement must satisfy the same prerequisite. The original Create key/body was retained for the explicit recovery/resumption recorded below after the failed outcome was independently reconciled; no automatic repost or new key was inferred from503.


## Completed owner correction and normal-cookie API continuation

The private schema-USAGE correction was applied with exactly one declared semantic privilege edge and no CREATE, LOGIN, membership or ownership expansion. The original failed command remains retained. A separately reviewed continuation first verified the original key was absent and all six tables remained empty, then used that same original Create key and equivalent typed body: B Create201, independent authorized C Revise200/version2, B Cancel200/version3. Original wire-byte ordering differed on the retained JSON checkpoint; typed command values and the original key were preserved. This is not a claim of byte-identical request replay.

All three historical versions and original actor-key responses remained recoverable after cancellation. Another actor could not retrieve the owner's key. Source Draft/version and held demand remained unchanged. Strict native reconciliation verified one enrollment, one Cancelled/version3 plan, three immutable revisions/receipts, stable logical child IDs with new physical revision rows, 19 owning audit records and three outboxes; unrelated business rows and original authentication rows remained exact.

A subsequent real-cookie test withdrew only current READ from the existing Core role: the same cookie changed from200 to403, and recovered200 after exact restoration. Reassigning only B's current branch denied selected-branch403 and Company-All404; exact restoration recovered200. A final strict owner check admitted only the declared authentication effects. All six newly created synthetic sessions were revoked, the three temporary Core actions were removed by exact original-row restoration, and the private API stopped. Production was not touched.

| Later receipt | SHA-256 |
|---|---|
| One-edge owner schema-USAGE correction | `5f0ee9f9361082fbd6585aae426cdb680f22d007a3de3d981d5885a23c8110a6` |
| Strict API graph/auth owner reconciliation | `bd9d84889054cbcef35a69be9b7aab8477e1210cad7b403d8efc4f4df3d3a588` |
| Same-cookie READ withdrawal HTTP result | `e338af897dc151b61440f458e23f5dafcfe585a76d29ffb9b041b1abe3c2db67` |
| Same-cookie branch-confined HTTP result | `3ffe55b2d201d011b98b70d42a9cb734de7c76c584b27090f16ab4adde33a889` |
| Publication28 credential/payroll/owner-access negatives | `922fe283970d56f170457013df7f6b85553b3332637c02de3c228ead15ffbd20` |
| Fresh UI clone, genuine upgrade, empty237/owner proof | `477e1ff7a5e21f028eb799f4b971e28f89045b25d89a8ee319d7d920dd08cc8f` |

## Existing UI artifact and next browser gate

The preserved imported UI product `8dca4943bbe5b5ea4993a7eecb0e7a40fecb53c1` passed an actual offline typecheck/Vite build. Its five artifacts total1,736,230 bytes; manifest SHA-256 is `73398ef763aa3d32fefce3c0e8dfbb8740b23d1c30566a494d2bceb46472247c`. The harness separately passed syntax/strict type checks. No actual browser pass is inferred from either result.

A separate fresh clone of the untouched original231-table source passed the genuine migration to237 tables, with all six new tables empty, actual schema fingerprint matching, expected ownership and schema USAGE. The source and earlier populated Cancelled/version3 native graph remained exact. This prepares a new actual-screen Create/Revise/Cancel journey without copying or SQL-seeding the previously accepted business graph. Browser, independent post-browser owner reconciliation and release remain pending.


## Populated retention guard: bounded native result

The exact first guard statement from the genuinely generated Down script (SHA-256 `71bf93bab78b1837dc32910d81ee996a2e1350eb6266294149db40a058ef6530`) ran inside an enforced READ ONLY transaction against the retained Cancelled/version3 native graph. It raised PostgreSQL SQLSTATE `55000` with the retained-evidence error. No full downgrade, DROP or other DDL ran. Independent counts showed the six retained business tables and audit/outbox markers; full source and populated-target row/catalog/trigger/history snapshots were identical before and after.

Receipt SHA-256: `8a9f60ba7552e96b180f68f2a7dd3048beba332a248e4ec660cf893f6f5a0390`. This proves the exact populated guard rejects current retained evidence. It does not prove a marker-only fixture, the full destructive Down on populated data, or old-binary HTTP compatibility. The first transport attempt stopped before guard execution at the protected helper's read-only allowlist and remains retained separately.


## First original-UI run: successful save, failed All-branch render

The actual signed-in Playwright run on UI8dca completed B's normal login, source discovery, existing RFQ detail/Items, Create201, reload/version1 history and original saved-result GET201 recovery. It then failed at the active-Draft All-branch step with one page error. Exactly one business command persisted; B logged out successfully. This is a failed UI run, not full acceptance.

Source investigation identified the real defect: the shared ERP shell stores an empty branch string for All, while the round context's nullish conversion preserved that string and failed GUID validation during rendering. The correction normalizes this actual shell shape for retained All reads while preserving selected-branch requirements for writes. The original saved version and failure are retained; no fresh Create or database reset is authorized as a test retry.

Independent native reconciliation passed the saved version1 graph, exact original key/receipt hash, seven entity audits plus one command audit/outbox, one revoked B session and only its declared authentication effects. Source and protected earlier native graph remained exact. Receipt SHA-256: `8751520e5c05c859c7c321b23d5a9113918851527713c1e12064139d08509bb9`; it explicitly records `wholeUiPassed:false`.

A separate rollback-only test then granted the exact22 minimal barrier read columns to the existing M4 execution role within a transaction, used `SET LOCAL ROLE`, and attempted the old-style Draft→Published transition. The installed trigger rejected it with SQLSTATE55000; all grants and row effects rolled back, with complete before/after UI-clone/source/protected-native equality. Receipt SHA-256: `9e1863dff7a93a60d076c5f685e26ca9a64ecc4abe21aee29bc15d3169b62eec`. This proves the trigger under that effective minimal role, not direct LOGIN, an actual old binary, or current compiled B1 provider results for that role.

A successor UI build and a narrow continuation against the retained version1 are required: verify the fixed All read, then execute only the remaining Revise/Cancel commands. Actual browser/owner acceptance and release remain open.


## Corrected original-UI continuation and cumulative acceptance

The minimal shared-shell normalization fix is product `26d4ef1924b508a4ca33a184ec21500691003b3f`. Its actual-shell-shape regression/cohort passed37 focused cases, whole product type checking and a genuine offline Vite build. Successor artifact manifest SHA-256: `b32bbf80818649906aff993fbfa8666d0334899ecd4dcddec8358dd46777c61c`. The original8dca artifact and failed run remain unchanged.

A separate actual Playwright continuation used the retained version1 rather than creating another record: B verified the original Create GET201/raw response hash, current All-branch view and history; C revised to version2, cancelled to version3 with a required reason, verified immutable historical views and retained All reads, and received404 for B's original command key. Both normal logouts succeeded; zero page errors and no unexpected business writes occurred. Durable protected intent records were flushed before the two commands, with exact raw response evidence saved before interpretation. Actual continuation receipt SHA-256: `b8041ebe50fa4a78cfcdd452927dd43f5f57b91c67ec9c303f634f88737fef6c`.

Independent cumulative database reconciliation passed all three original commands, the B/C/C immutable revision chain, four criterion rows and three participant rows, 20 Procurement audits, three outboxes, nine Core authentication audits and three logout events. It reconstructed the earlier version1 snapshots and original B session/audit/outbox byte hashes, preserved unrelated business records and original sessions, and verified all three new sessions were revoked. Source and the separately protected earlier native graph remained exact. Receipt SHA-256: `1785d682717ce40a885810bda03bcf20606136dceba486f5fcade84a44321b6f`.

The temporary three Core actions were then removed by exact restoration of the original single existing role row, retaining all business/history/authentication records. Restore receipt SHA-256: `fd9c452dfc6f5dc0085258446582bf780136fcf257c753075f73f953ab7561db`. Test API/proxy services are stopped. The finite private database role remains an isolated fixture, not a production grant or new identity.

This is accepted corrected-continuation evidence, not a retroactive pass for the first browser run, not three fresh commands on the successor UI, and not whole Procurement UAT. No production source, schema, policy, permission or feature flag was changed by these tests. Cross-tenant native acceptance, marker-only retention and actual old-binary compatibility are not claimed; future formal submission/evaluation/award remains outside B1.
