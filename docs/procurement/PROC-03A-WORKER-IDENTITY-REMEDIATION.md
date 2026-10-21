# PROC-03A-I1 — Retained worker identity for spending authority

**Status:** Source audit and regression/remediation proposal only, based on `135f3b9`. Material release blocker for claims of historical worker independence and named-worker authority. No application, schema, migration, test execution or deployment change. Extends [PROC-03A](PROC-03A-SPENDING-AUTHORITY-CONFIGURATION.md), its [sourcing action addendum](PROC-03A-SOURCING-PUBLICATION-AUTHORITY-ADDENDUM.md), and the existing P29 controlled-policy identity pattern.

## 1. Feature identification

Retain the original canonical workers participating in each configured finite authority version, and consume those facts when performing new authority effects.

## 2. Purpose and outcome

A later account/worker link correction must not make the original maker an independent reviewer, nor transfer a named person's spending limit to a different worker without independently reviewed configuration.

## 3. Scope and exclusions

Reuse existing Procurement authority model/lifecycle/evaluator and Core identities. Cover PR, PO and sourcing publication action tuples uniformly. No new account, employee, role, approval engine, financial ledger or automatic operational publication. This proposal does not weaken the actual unchanged-binding settings browser gate.

## 4. Related features and source evidence

The current grant intentionally has a **principal-based logical key**: `Domain/SpendingAuthority.cs:3–28`. That alone is not evidence that the original human/worker remains attached to that principal. Relevant backend paths are relative to `backend/`:

- `hrm-api/src/Mightyfin.Erp.Hrm.Api/LocalIdentity.cs:129,351–374`: actual authenticated `hrm-admin` PATCH account endpoint permits changing `WorkerId`; no grant invalidation or immutable identity linkage is performed. This is privileged administration, not an unauthenticated attack.
- `hrm-api/src/Mightyfin.Erp.Hrm.Api/ProcurementWorkforce.cs:17–32`: local account `WorkerId` takes precedence over worker `SubjectId`; an old worker subject does not prevent this rebinding.
- `hrm-api/src/Mightyfin.Erp.Hrm.Api/ProcurementAuthorityIdentity.cs:100–116`: independent reviewer compares current workers of recipient, creator, configuration owner and submitter; none is an original worker pin.
- `procurement-api/src/Mightyfin.Erp.Procurement.Application/SpendingAuthorityConfiguration.cs:193–212,276–289`: business fingerprint/principal self-review checks contain no retained worker identity.
- `procurement-api/src/Mightyfin.Erp.Procurement.Application/ProcurementSpendingAuthority.cs:48–64,82–103`: evaluator matches principal/action/scope/amount, not the originally intended recipient worker.
- `hrm-api/src/Mightyfin.Erp.Hrm.Api/ProcurementApprovalAssigneeResolver.cs:81–90,112–130`: already-opened assignments have worker pins and reject changed current worker; newly opened assignments can select the rebound account with its unchanged grant. The existing pin is valuable and must be preserved.

## 5. Actors

M: finite authority configuration account, originally worker W1. R: independent configuration reviewer account, originally W2. G: named recipient account, originally W3. Separate Core identity administrator performs an authorized account-link correction. All workers are active, canonical and in the same permitted company/site; configuration roles remain on their original accounts.

## 6. Exact trigger and interleaving

1. M/W1 creates and submits a finite grant for G/W3. Persisted creator/configuration owner/submitter are M; there is no W1/W3 snapshot.
2. Administrator PATCHes M to W2, then R to W1. An intermediate duplicate can temporarily fail Procurement canonical checks; after the swap there is exactly one active account per worker. Neither final account is ambiguous.
3. R/W1 calls Activate with the unchanged grant CAS. Current participants resolve as M/W2 and G/W3, while current reviewer resolves W1. All current-worker comparisons differ. Principal comparisons also pass because R differs from M and G.
4. The historical maker W1 has thereby become the accepted independent reviewer through R. The current canonical uniqueness guard does not detect this historical swap.

This is a source-confirmed missing historical binding, not a claim that an unprivileged person can change Core links. It is the same class of independence failure addressed by P29 retained policy identity.

## 7. Recipient transfer and applicability decision

After independent activation, rebind G from W3 to a different active W4 with exactly one account per worker. Current grant lookup still finds the same principal/action/amount, so a newly opened case can assign W4 and capture W4 as though it were the originally approved person. Already-opened cases reject the changed worker. If authority were explicitly approved as freely transferable account authority, this could be intended; the current named-person UX, current worker independence controls and enterprise requirement require **worker-bound authority**, which the parent has adopted for remediation. An account relink alone is not that independent business approval.

## 8. Workflow

Create captures current maker plus intended recipient. Submit captures current submitter and verifies original maker/recipient linkage. Return retains history. Activate/Supersede capture current reviewer, verify immutable creation/submission lineage and original recipient, and compare reviewer against retained original workers. New logical version is required to intentionally change recipient worker; preserve old version and explicitly supersede/revoke as existing lifecycle permits.

## 9. States and transitions

Retain Draft/PendingReview/Active/Revoked/Superseded. Add no competing workflow. Missing/corrupt retained identity or changed current maker/recipient binding denies new edits/submission/activation/operational use as appropriate, with explicit blockers. Original recovery/history remains readable under current access; it is not a new authorization decision.

## 10. Permissions and scope

Current Core account/worker/action/company/site authorization remains mandatory. Immutable historic workers establish past participation; they do not grant current permission. Do not require a historical maker's account to remain active merely to verify an independently reviewed retained record. Current acting reviewer and intended current recipient must satisfy the supported current authority contract. No new HRM administrator grant is supplied to Procurement users.

## 11. Business rules

Capture original maker, configuration owner, submitter, recipient and activation reviewer workers from authoritative Core records. No caller-supplied `workerId`, current-name inference, fabricated worker or fallback to current maker links. Retain exact principal as well as worker. Current reviewer must differ from all retained maker/submitter/recipient workers and required principal identities. Rebind, even within the same company, does not rewrite original participation. Revocation keeps its existing explicit authorization semantics; do not invent a new separation rule for it.

## 12. Model proposal and ownership

One Procurement-owned append-only versioned grant-identity evidence family, analogous to P29 stages, not a copied Core master. Creation pins grant ID/logical authority version, original maker/configuration-owner principal+worker, recipient principal+worker, exact business fingerprint and canonical microsecond time. Submission/activation pin current acting principal+worker, exact source creation/submission IDs/hashes, original recipient and grant revision at action. Retain activation reviewer for historical audit. Stable tenant/entity/grant ID relationships; action revision/fingerprint is evidence, **never a mutable EF alternate key**. Typed stage-parent relationships must stay in the same scoped grant. Explicit short physical names for any new key/FK/index; reuse existing stable scoped grant key. No Core-schema FK requiring historical users/workers to remain active.

Exact record shapes/physical names must be reviewed before coding. Prefer one typed verified evidence projection reused by existing configuration and operational wrappers, not separate action-specific identity rules.

## 13. Screens

Keep existing authority list/forms/pickers. Server `allowedActions` must suppress unavailable transitions; plain messages explain changed identity or required renewed review. Existing list may show legacy history without advertising activation/use. No raw worker IDs entered by users.

## 14. UX

A limit is for the named person approved at the time. Relinking an account requires new independently reviewed authority where the intended worker changed. Do not silently label a replaced worker as the original person.

## 15. Notifications

Reuse existing configured channels; no notification substitutes for persisted identity evidence. No new mandatory delivery claim in this milestone.

## 16. Documents

No new document storage. Optional business justification stays under existing evidence/document service contracts.

## 17. Edge cases and legacy handling

Legacy grant rows lack trustworthy original worker evidence. **Do not backfill from today's account links, audit display names or fabricated creation snapshots.** Legacy/unverified rows cannot acquire new activation or operational authority through adoption. Reconfigure/review a new exact version under the supported workflow. Preserve original rows, history and scoped read/recovery. Distinguish current binding failure from original evidence absence. Existing assignment worker pins remain enforced; historical successful response replay must not be reconstructed from today's worker links or converted into a new effect.

## 18. Transaction/integration contract

Reuse existing Core/Procurement same physical store and enlisted transaction/fences. Acquire current Core identity/scope fence before transaction snapshot; authorize canonical participants and validate full retained family in the same owning transaction as grant CAS, evidence, audit and effects. Core account-link writer coupling/serializable semantics require an explicit race proof: no detached identity query authorizes a later grant write. Do not copy Core identities into an independently updated Procurement account table. Extend the existing generic finite evaluator input with a verified intended-worker binding or validate through one common adapter; never let PR/PO/sourcing differ silently.

## 19. Audit/security/privacy

Append-only identity stages preserve original actor and scope even after account offboarding/rebinding. No secret/email/banking exposure in readiness/list DTOs. All privileged Core link changes remain under existing Core authorization/audit responsibilities. Evidence missing/mismatch is a denial, not a log-only warning.

## 20. Reporting

Existing operational/history reports may distinguish verified vs legacy identity without claiming complete historical independence for legacy grants. No extra reporting engine.

## 21. Configuration

No business thresholds or default grants change. Supported exact PR/PO/sourcing tuples remain explicit. Adoption is schema/readiness-controlled; policy `configurationVersion:1` is not proof of authority-identity readiness.

## 22. Nonfunctional

Bounded participant/family reads, exact decimal/time preservation, reliable serialization conflicts, least privilege. Current permission revalidation and immutable history verification must not enumerate all staff or infer identity from roles.

## 23. Postconditions

Successful new activation proves the original intended worker, original maker/submitter and a genuinely independent current reviewer. New operational assignment uses that retained recipient and current matching account. Reads do not create or repair evidence.

## 24. Failure and schema recovery

Central additive migration only, after combined model gate. No original authority field/type/precision/history rewrite. Readiness must inspect the actual caller-owned database, exact applied schema/validated CHECK/typed FK/stable key/index shape and finite read/append permissions. Down first deterministically locks all inspected new evidence and affected authority/audit/outbox producers, then denies used or partial evidence before any drops; finalize exact producer identifiers from implementation, not speculative names. Preserve original schema/data/history on failed Down. Freeze actual Npgsql outputs; no same-name-only inspector. Unknown/legacy records remain retained, never rounded/adopted.

## 25. Acceptance criteria

All regression cases below must be actual mapped endpoints plus restricted PostgreSQL/concurrency/owner checks before release. Existing unchanged-binding actual browser positive is a necessary bounded vertical proof, **not proof of historical worker independence**. This document authors scenarios only; it introduces no failing/skipped test as a substitute for a passing security gate.

## 26. Concrete regression proposals

Extend existing `ProcurementSpendingAuthorityHttpTests` fixtures/real `--settings` probe, reusing their actual routes and Core identity fixture. For the true account-administration boundary, map/reuse existing authenticated Core PATCH rather than pretending a fixture-only link assignment proves that endpoint.

| Case | Setup/action | Required result and no-effect proof |
| --- | --- | --- |
| Maker/reviewer swap | Actual create+submit by M/W1 for G/W3; controlled Core relink M→W2/R→W1; Activate as R with original CAS | Deny historical-maker self-review; grant/evidence/audit/outbox footprint unchanged. Demonstrate final one-active-account-per-worker, not just alias ambiguity. |
| Recipient/reviewer swap | Create+submit G/W3; relink G→W2/R→W3 | Deny original recipient worker approving its own limit under R. |
| Maker relink before submit | M/W1 creates; relink M→W4; PUT/Submit as M | No inheritance of original configuration ownership by a different worker; unchanged grant/evidence. |
| Active recipient relink | Independently activate G/W3; relink G→W4; open a new eligible PR/PO case, later source publication when implemented | Deny retained-recipient mismatch even though same principal/action/current scope/role/amount; no case, assignment, reservation or command effect. |
| Already-open assignment | Open with G/W3, then rebind G | Preserve existing `ApprovalIdentityChanged` behavior and immutable original assignment; no decision. |
| Independent positive | Original bindings unchanged; distinct current reviewer activates | Exact expected grant CAS, amount/scope, creation/submission/activation identities and audit; no other records changed. |
| Historic maker offboards | Valid retained creation/submission, maker account inactive, independent current reviewer | Retained evidence can be verified without inventing current maker activity; outcome follows explicit adopted review policy. |
| Legacy/corrupt lineage | Legacy no identity; missing stage/foreign grant-parent/changed hash/recipient pin | Read remains scoped; no activation or new use, no auto-repair/backfill. |
| Race relink vs activation/open | Real separate contexts, observed lock wait/barrier, both serial orders | No authority effect authorized from an old worker binding after conflicting rebind; controlled conflict/denial or correctly serialized valid outcome. No sleeps as proof. |
| Atomic rollback/replay | Fault evidence/audit insert after grant mutation, lost response when durable contract exists | Whole write rolls back; no guessed retry. Exact permitted recovery retains original IDs/actors/bytes. |
| Schema/rollback | Actual new migration fresh/used twice, missing/wrong typed constraints/grants, used/partial Down | Exact structural readiness denial/restoration and no data/schema/history loss; original worker values never synthesized. |

Repeat identity-sensitive configuration cases for exact PR/PO/sourcing contracts; reuse common fixtures/evaluator. Production sourcing publication remains disabled and cannot be claimed covered by authority configuration tests.

## 27. Ownership/dependencies

Procurement owns grant evidence/configuration and spending-use checks; Core owns current identity/link administration and scope fences. Central migration owner sequences schema generation/rehearsal. Existing P29 capture/verification/typed-stage approach is an inspiration to reuse conceptually, not authority to reinterpret a policy row as a grant or copy policy-specific tables.

## 28. Open decisions and release hold

Parent confirmed original maker/submitter/recipient/reviewer worker retention and worker-bound downstream authority are required. Remaining technical DoR: exact evidence schema/API capability marker, safe legacy version replacement path, Core account-link writer fence coupling, and rollback producer manifest. Until implemented and independently verified, historical grant independence/named-worker transfer protection is a **material release hold**. This audit executed only read-only source searches and `git diff --check`; no SDK, API, database or browser jobs.
