# M7B1 private SQL-role acceptance — 3 November 2026

Status: **bounded private database acceptance passed; API/UI workflow acceptance and production activation remain open.** Parent issue #145 remains NOT READY. This checkpoint belongs to #162 / draft PR #165.

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

Publication 26 checks Core writes/ownership and its required positive read projections; it **does not reject every additional sensitive Core SELECT privilege**. No such negative result is claimed here. A successor B1-only safe-read guard is being implemented and requires its own compiled/native tests, including credential columns, payroll reads and unlisted views. Its allowed safe union must preserve legitimate shared execution-role projections without permitting a general HRM read role.

Next gates are actual workerless Core-cookie create/revise/cancel and actor-key recovery, current permission/branch/tenant denials, retained legacy-publication barriers, populated downgrade vetoes, the rich RFQ/people/source-picker DTOs, and Playwright acceptance through the existing imported screens. These checks do not establish formal RFQ review, sealed submissions, bid opening, scoring or award, and they do not complete whole-module UAT.
