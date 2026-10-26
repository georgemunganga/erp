# Branch retirement evidence — 26 October 2026

This report records **7 remote** and **18 local** branch deletions. Every remote delete used an exact-head lease; the script rechecked the remote main/head, open PR heads **and bases**, checked-out worktrees, and ancestry before each operation. Each deleted ref was absent on the immediate after-check, and all seven remote refs were absent in a fresh final listing. Local branches used ordinary `git branch -d`, with no force. No source worktree, cache, or production application was changed.

Protected execution journal SHA-256: `c9742d8de3e72f4ea8fafec7a2183ea63951720558cbae65746c88e1978c3a32`. The journal contains the before/after checks and command exits; the public JSON in this directory records the deleted names and heads without private filesystem paths.

| Repository | Remote main pinned during deletion | Remote heads before → after | Local branches before → after |
| --- | --- | ---: | ---: |
| `georgemunganga/erp` | `e50fa95904a1629d0785ee2d5e2b927371f06dc3` | 284 → 283 | 378 → 368 |
| `georgemunganga/module-connect` | `704ac9c642d00e3672cd81e7c1df2d9cb50be160` | 73 → 67 | 112 → 104 |

## Deleted remote refs

| Repository | Branch | Exact head before deletion |
| --- | --- | --- |
| `georgemunganga/erp` | `codex/procurement-milestone-coverage-status` | `ba6df4092a4c86ffb368a99c48cc67c21aaf2bb9` |
| `georgemunganga/module-connect` | `codex/procurement-actual-supplier-lifecycle` | `5db1130a60136890b5df402cdaa4047ec7ee07be` |
| `georgemunganga/module-connect` | `codex/procurement-authority-settings` | `d2b609626b58f227f5e19f11efdf8fa9590b5999` |
| `georgemunganga/module-connect` | `codex/procurement-recurring-principal-ui` | `1e8f07fc7f7c58a16906494fc14dec86f33dc818` |
| `georgemunganga/module-connect` | `codex/procurement-rfq-reload-recovery` | `5a48539c1ff6c8fbf8e70bd9b033661bfd5bf7e9` |
| `georgemunganga/module-connect` | `codex/procurement-ui-complete` | `d7de3cc7f27f00d8d7836bc90667754c3f71b6bb` |
| `georgemunganga/module-connect` | `codex/procurement-unit-review-policy-ui` | `ae54ca8633f81c5a61397e76c5ca1c5308dd42b3` |

## Deleted local refs

| Repository | Branch | Exact head before deletion |
| --- | --- | --- |
| `georgemunganga/erp` | `codex/core-outbox-acceptance-evidence-20261026` | `35045b7e51efca2e756dd678b9de6a62477b03bc` |
| `georgemunganga/erp` | `codex/m2a-csv-reader-integration-20261026` | `a746c820a7e9196e725c6408cd4d4397d9181678` |
| `georgemunganga/erp` | `codex/m2a-mainline-ui-evidence-20261026` | `d00964b3d108dfac10765c9a8879e57484f50ba8` |
| `georgemunganga/erp` | `codex/m2a-native-import-acceptance-20261026` | `788c944a0acb4eada5246bdd7a5793e78d21689d` |
| `georgemunganga/erp` | `codex/m2a-native-import-checkpoint-20261026` | `ff255065ab3240d7949315aafd62395db3d03838` |
| `georgemunganga/erp` | `codex/m2a-private-migration-evidence-20261026` | `59d0e2b053baea1a68fb65dcb35ca081fef6733a` |
| `georgemunganga/erp` | `codex/m3a-request-approval-acceptance-20261026` | `3ade873a5c4491caa810db4c930ccc039c5ea053` |
| `georgemunganga/erp` | `codex/procurement-execution-status-20261026` | `97ada9875d0bb2caaf933cf05396c2f200700af4` |
| `georgemunganga/erp` | `codex/procurement-milestone-coverage-status` | `ba6df4092a4c86ffb368a99c48cc67c21aaf2bb9` |
| `georgemunganga/erp` | `codex/procurement-r0-p29-compatibility-20261025` | `05915e086efd21cb036ddf31f94c1688aa820aee` |
| `georgemunganga/module-connect` | `codex/manual-payment-api-client` | `fea8e17b80f368c7345c3cf2ccaf4a322d409f4a` |
| `georgemunganga/module-connect` | `codex/p29-principal-rfq-ui` | `9f42f89cb125f884f13117c06f695e580927fdc5` |
| `georgemunganga/module-connect` | `codex/procurement-actual-supplier-lifecycle` | `5db1130a60136890b5df402cdaa4047ec7ee07be` |
| `georgemunganga/module-connect` | `codex/procurement-authority-settings` | `d2b609626b58f227f5e19f11efdf8fa9590b5999` |
| `georgemunganga/module-connect` | `codex/procurement-recurring-principal-ui` | `1e8f07fc7f7c58a16906494fc14dec86f33dc818` |
| `georgemunganga/module-connect` | `codex/procurement-rfq-reload-recovery` | `5a48539c1ff6c8fbf8e70bd9b033661bfd5bf7e9` |
| `georgemunganga/module-connect` | `codex/procurement-ui-complete` | `d7de3cc7f27f00d8d7836bc90667754c3f71b6bb` |
| `georgemunganga/module-connect` | `codex/procurement-unit-review-policy-ui` | `ae54ca8633f81c5a61397e76c5ca1c5308dd42b3` |

## Retained heads and limits

After the strict batch, ERP has 5 protected/checked-out literal ancestors, 277 nonancestral remote heads, and 1 head whose commit object was unavailable locally at audit time. UI has 5 protected/checked-out literal ancestors and 62 nonancestral heads. One unprotected ERP head had patch-equivalent commits; that alone does not authorize deletion. The 221 other unprotected ERP heads and 6 UI heads had at least one unique patch in the read-only check. Current open PR #111 (ERP direct PO source) and #84 (UI direct PO) were retained. Main, hotfix/release, deployed-source, open-PR and checked-out branches were protected.

Seventeen remote heads exactly match merged PR head OIDs but are outside main ancestry. Sixteen merged into held branch chains whose merge commits are not reachable from current main. The remaining one is recorded separately in [the squash/rebase merge review](BRANCH-SQUASH-MERGE-REVIEW-20261026.json); it was **not deleted**, and no recovery bundle has been prepared. No held branch chain was merged or discarded.

This branch cleanup does not complete Procurement or deploy changes to production. It does not change ERP permissions, business data, feature flags, or release status. Revalidate remote refs, main, PRs and worktrees before any later deletion batch.
