# B1c round-plan review — source verification, 4 November 2026

**Status: source verified and merged; native/UI acceptance pending. Not enabled or deployed. Whole-module issue145 remains NOT READY.** This record covers issue180 / PR183 and the existing 28-section [feature specification](../features/PROC-06-M7B1C-ROUND-PLAN-REVIEW-IMPLEMENTATION.md).

## Implemented contract

PR183 is merged in published ERP main `7166e36232fb464a005dc5cbea9e1eef1ebd4848`. Verified executable source `9fecef1732b7629f5ac8dbd005c863d20898a250` adds immutable submitted commercial-round plans and independent Core-assigned review, Return, Reject and approval as an internal Published plan. It reuses shared Core approval cases/stages/assignments/decisions, current principal permissions and company/branch scope; no employee link or second approval engine is introduced. Enrolled RFQ supplier publication remains blocked. This does not implement supplier bid submission, sealed opening, bid evaluation, award or award-to-PO.

Submission pins the current revision/content hash and actual applicable policy version. Every decision checks current plan/event/case/assignment versions, effective authority, current assignment, scope and separation of duties. Historical evidence and original-key recovery are separate from current editability. Dedicated submit context and review detail support correctly scoped UI consumers without granting owner or configuration permissions. Actual Core reviewers have a purpose-specific lookup; document requirements remain unavailable until the existing Core document integration is verified.

## Genuine migration and actual tests

Genuine EF migration `20261104022336_M7b1cCommercialRoundReview` was created by the actual SDK run. Its generated designer and snapshot were adopted byte-for-byte; the generated main was adopted with only three reviewed SQL helper calls. The migration adds two review-link records and updates the reviewed state/receipt/policy constraints and SQL helpers. The Down source acquires the reviewed ten-table locks before checking retained evidence, then runs its guarded uninstall and generated structural reversal in the same normal EF transaction.

The full Procurement suite actually passed **2,009 tests, zero failures/skips**. All 2,004 prior stable test IDs and methods were retained. NoPendingModel and all six SQL emissions completed; earlier Plan and B1 Up/Down SQL bytes remained exact. This was a fresh Procurement run, distinct from earlier releases that reused the historical 2,004-test result.

The first subsequent Host compile found two CS0136 local-variable name collisions. These were corrected without wire-contract changes. The next compile succeeded and its selected 394 Host tests passed; the independent evidence gate correctly rejected acceptance because its filter omitted 175 prior cases. No test failure or source drift was inferred. A fresh no-build run targets the exact union of the prior 551 tests and 18 new tests, with all 583 compiled Host runtime files pinned before/after. The fresh no-build run passed all **569 focused Host tests, zero failures/skips**, exactly matching the expected ID/method map and retaining all prior 551 identities. All 583 compiled Host runtime files and the Procurement binaries remained unchanged. The Procurement result is retained only with exact checks of its 118 runtime files, ten owning assemblies, source and SQL bindings. No Host pass is inferred from Procurement tests.

Private evidence anchors (no credentials or raw business records are published):

| Evidence | SHA256 |
| --- | --- |
| Actual 2,009-test/runtime/source/model/SQL proof, Host compile failure separately retained | `4c17ba2598975f82c34096d6fda73935847cb28fc2c984e87a59b442fa79b9f3` |
| Actual 569 Host passes, exact baseline retention, unchanged binaries/model/six SQL | `d728bb94e7dc08da99b9c9b6150344d17d8c91c52f335f20ffc9c48eb0e0efdc` |
| Prior 2,004 stable-ID/method retention | `e90585ea805b87f602ddf7047afdd19c2a41642d8750a01d8801b10876f7f2de` |
| Genuine augmented migration | `d0d2f03048071561e5b764e9c160ebdcf2f97d255f12081daae010d73e69d168` |
| Genuine designer | `65ad9ecaaa6bfacb1770e37d11d7053003bb3e5075f7c068ef05fde1a222e671` |
| Genuine snapshot | `6098e1c2b9f8162e65a6e7bac95ba0f43f8511d2e6333c339054fc222238ac89` |

The first generation attempt stopped at a resource guard before EF. A later Host-only attempt stopped at the free-disk preflight before overlay or SDK launch. Both failures remain retained. Removing two unused regenerable AppStream caches recovered 8,155,136 observed bytes without removing ERP data, source, packages, backups or retained publications. A fresh Host-only successor uses the original memory/disk limits and does not regenerate EF or rerun Procurement unnecessarily.

## Remaining acceptance and deployment holds

- Execute native migration/empty Down-Up/retained-evidence veto and concurrency checks on isolated private copies; capture actual direct and restored catalogs before adopting readiness fingerprints. Never apply this draft migration to production as part of a source merge.
- Verify finite-role read/write admission, missing rights, denied owner/extra-data privileges and current Core authorization using real provider connections.
- Integrate the published contract into the preserved RFQ panel, policy configuration and existing Approvals views using shared ERP components. Verify real-cookie Return → edit/resubmit → Approve, recovery, stale/denied paths and native reconciliation with Playwright.
- Independently review any production rollout, required rights and activation after the private evidence passes. No invented policy, approver, threshold, bank action, accounting posting or verified document may be seeded to make a test appear complete.

Read/record/review flags default off. New review readiness has blank migration/fingerprint capture values and fails closed until actual capture adoption. Source publication is not workflow activation, native acceptance, production deployment or business-owner UAT sign-off. Production remains on the separately verified Plan release recorded in [the release record](PROC-05A-PAIRED-PRODUCTION-RELEASE-20261104.md).
