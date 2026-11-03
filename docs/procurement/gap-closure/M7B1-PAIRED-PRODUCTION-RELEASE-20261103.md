# M7B1 paired technical production release — 3 November 2026

Status: deployed and publicly verified. Issue175 technical release is complete; parent issue145 remains **NOT READY** for whole-module UAT. This record supersedes the previous manual-payment deployment status while preserving its evidence.

## Published and deployed inputs

| Input | Verified value |
| --- | --- |
| ERP prerequisite | PR176 merged as `950856f7e01e3e2d7c628ddbf67dbac846644803` |
| Tested backend product | `6ec41f938409f9400bd627f727c0161af0043479` |
| Exact merged/tested backend tree | `868d0f34952c4e3aa776463fe41f7c06ab960340` |
| UI main / deployed product | `9b5d02dcf971900289793f56e91895dd3747fd80` / `26d4ef1924b508a4ca33a184ec21500691003b3f` |
| Backend publication | pub29 manifest `6a2ceff55b671cb381d45c1d61bb94267a85af0204bae2a26b21a9ceefeca1e1` |
| Image | `sha256:97bd9e859a3ab1576c8421d5871833aec870322c424c2e8fa3b933c6cf31ee67` |
| Live API | `7e1b92aa141e83160a566e70abb8cb4e0b4d0c01f796588d32d7fcf3422aae89` |
| Procurement UI release | `commercial-round-26d4ef19-20261103/` |
| Genuine migration | `20261103163108_M7b1CommercialRoundDraftPlans` |
| Canonical admission fingerprint | `24e096b8ea495da24c7eab1ea2b0bd1fda9cc54754d2d2b5f9ca923cd743133b` |

Core remains the identity, permission and company/branch-scope owner. Employees are optional references. The imported Procurement screens and shared ERP components are retained.

## Verification actually performed

- Actual offline SDK:429 Host cases passed,0 failed/0 skipped. All411 earlier identities/definitions/outcomes were preserved;18 new exact-restored-shape cases passed. The separately accepted1970 Procurement result was reused through exact source/10-assembly bindings, **not rerun** in this slot. No pending model changes; B1 and M7A Up/Down SQL byte-identical.
- An authenticated production database/globals backup actually restored all231 tables and the source roles/verifier digests. Genuine Up→empty guarded Down→Up, old data/history/security preservation, exact new ownership,32 new RI-trigger representation matching and canonical admission passed. Only45 individually captured PostgreSQL CHECK deparse pairs and3 built-in membership-grantor variants were admitted; unknown differences remain denied.
- The actual frozen seven-table quotation query returned the same JSON before/after B1 in that restore. Old pub28 rejected this valid restored shape; actual pub29 accepted it. PR176 adds exactly one third complete contract, preserving both earlier contracts and rejecting mixed/altered schemas. SDK binding:`31f7d606…`.
- Earlier bounded native/private normal-cookie acceptance remains recorded in the [B1 private evidence](M7B1-PRIVATE-SQL-ROLE-ACCEPTANCE-20261103.md):workerless actors, Create/Revise/Cancel, immutable history, actor/key recovery/privacy, current permission/branch denials, exact audit/outbox ownership and fixture cleanup. The failed first browser run remains retained; its corrected continuation is separate evidence.
- Actual artifact staging verified all150 backend files and unchanged configuration. Ten changed files were copied into the authenticated base image; the stopped image verifier rechecked all150. Five primary UI files plus38 deduplicated compatibility assets were verified, with0755 directories/0644 files. Staging receipt:`628d16d8…`.
- Actual production provider probe used the compiled provider classes and owning finite connection, with all4 configured DSNs forced read-only. Manual-payment governance and round-barrier readiness were true; full round READ/RECORD were false without their full grants. Quotation READ and current/restored exact shape matching were true; malformed shape was rejected; pending migrations0. No application startup, login or business transaction occurred in that probe. This is direct provider evidence, not whole-Host dependency-graph resolution.
- Paired production switch preserved every original application Env/HostConfig field, verified all150 live runtime files, restarted only the pinned publisher and atomically changed the Procurement static alias. All16 public route/auth checks and all43 primary/compatibility UI files passed.
- Actual Playwright:7 fresh anonymous contexts rendered sign-in, HRM, Core settings, Procurement Home, Vendors, RFQs and Payments;0 page errors, no login/cookies saved and no business mutations. This does **not** prove signed-in production CRUD UAT.

Hosted GitHub Actions remains account-billing locked. The actual isolated results above are not a hosted-green claim.

## Production change, interruption and recovery evidence

Both application writers were stopped before a final encrypted database/globals recovery point. The streaming dump was fully authenticated and its database TOC verified; all key/cipher/metadata files and containing directories were fsynced before DDL. Exact231-table snapshots before/after this backup matched. This latest recovery point was **not** separately restored; the earlier fresh-backup restore/rehearsal is distinct.

Genuine Up added6 empty tables and its reviewed functions/triggers/parent key. The24 unintended default-derived legacy CRUD rights on those6 tables were revoked. The execution role received exactly22 barrier-column SELECT rights; the existing ordinary Procurement record role received only3 enrollment-column SELECT rights. No Core/Finance grants, role attributes/memberships, ownership or global default ACLs were changed.

The first production attempt stopped before the final record3 intent because the zero-client guard observed an additional connection. A later read found none; its identity/cause is not claimed. The original FAILED/log remain. Independent review accepted a continuation that first proved the complete current state equalled the saved after-exec22 state, then executed **only** pinned record3 once. No Up, revoke24 or exec22 replay occurred. Absolute-zero polling did not mask/terminate clients. Exact3 ACL delta, unchanged original231 data, empty6 tables, owner schema access, fingerprint and direct-role `true,true,false,false` results passed. The final receipt explicitly links the original failure and accepted continuation.

The old API/UI and earlier releases remain retained. Recovery retains the additive schema; it does not silently delete evidence or perform destructive Down/restore. An older binary may fail closed for affected M4 writes against retained new schema; a forward fix is preferable when those workflows are needed.

## Operating boundary and remaining work

All held processing/quotation/round flags remain off. No business thresholds, approved policies, Core authority grants, Finance posting, bank execution or verified document transport were invented. Production manual-payment policy configuration still needs its own approved business inputs and activation acceptance.

Remaining parent145 work includes Plan review's genuine migration/captured shared policy CHECK and paired acceptance; formal sealed submission/opening/evaluation/award; remaining requirement-by-requirement CRUD/workflow UAT, document scanner/transport, supplier participation and other recorded gaps. See [the UAT register](PROCUREMENT-UAT-AND-GAPS-20261027.md). Technical rollout and anonymous browser success do not complete those workflows.

Safe cleanup removed only an unreferenced, remotely recoverable stock ClamAV image cache and generated MSBuild obj caches. No running/stopped container, database/volume, source, runtime bin, publication or release evidence was deleted. Exact tracked source and10 retained test-assembly hashes were rechecked after obj cleanup. **Future SDK work must run the offline restore before build**; historical no-restore commands must not be replayed against cleared intermediates. The idle private test PostgreSQL is stopped; production PostgreSQL/API/publisher remain running.

Private operational receipts, original failures, ciphertext, keys and snapshots stay outside Git under the protected release evidence store. No credentials, customer rows or session material are included in this record.
