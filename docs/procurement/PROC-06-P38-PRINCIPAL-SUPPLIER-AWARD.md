# PROC-06 P38 — Core-principal supplier award

**Status:** Source foundation and private empty-schema rehearsal accepted; routes remain unregistered and unavailable. **Parent:** [PROC-06](PROC-06-STRATEGIC-SOURCING-AND-AWARD.md), P32 commercial round and P36 human recommendation Draft. **Accepted source:** `1c58c6ae`; see the [bounded acceptance receipt](release/P38-FOUNDATION-SCHEMA-ACCEPTANCE-20261025.json). No business award, purchase order, supplier dispatch, Finance posting or production activation is claimed here.

## 1. Feature identification

P38 is the Procurement-owned decision on exact, opened supplier offers and a human recommendation, reviewed through a distinct Core approval case. It records approved line awards or an explicit no-award outcome.

## 2. Purpose and outcome

Preserve who proposed, reviewed and approved each selected supplier, quantity, currency and commercial amount. A recommendation Draft does not change the RFQ, allocation, PO, supplier contract or Finance state.

## 3. Scope and exclusions

Initial scope: one published, sealed, closed and independently opened P32 round with retained P36 source. No automatic ranking, cheapest-bid selection, inferred pass threshold, FX conversion, supplier notification, PO issue, AP or bank action. Negotiated prices require separately adopted original supplier evidence and policy.

## 4. Parent and related features

Use P29 original PR approvals and allocation holds, P30 publication and manual quotation evidence, P32 approved round/submission/opening and individual assessment history, P36 append-only recommendation revisions, shared Core approval/documents/identity, and P30/P33 PO source consumers. Existing `SourcingAward`/`SourcingAwardLine` are worker-era generic records without retained principal approval, exact offer or Core-case ownership; they cannot be silently repurposed.

## 5. Actors

A current Core maker proposes and submits the frozen recommendation. Independently named current Core reviewers decide the award; an authorized reader inspects the retained outcome. Optional worker aliases are denial-only history. Supplier portal identities are not inferred from an internal recorder.

## 6. Entry points

Future purpose endpoints on the existing RFQ route: own recommendation Submit, assigned award review list/detail, decision, outcome/detail, and original-key recovery. Existing RFQ UI tabs remain the presentation surface; this milestone adds no parallel page. Routes stay unregistered until owned storage, Core policy, physical capture and finite grants exist.

## 7. Preconditions

The exact P32 round, seal, opening, complete offer set, original document uses, original PR hold and P36 revision chain must verify in the same owning transaction. The recommendation must be a separately submitted immutable revision, not its current mutable Draft pointer. The adopted award policy must explicitly name source kind, company/branch, decision stages, reviewer selection and monetary authority, with a published review history. Missing policy or branch authority denies.

## 8. Workflow

Draft correction → explicit maker Submit/freeze with original revision/key → independent Core case Open on the frozen award source → named Core decisions through actual One/All stages → owned terminal Approved/Returned/Rejected/NoAward record and original response receipt. Return permits a new P36 revision and new submission/case; old evidence remains immutable. A later owned award-to-PO adapter is separate.

## 9. States

Proposed, PendingReview, Returned, Rejected, Approved and NoAward are P38-owned states. A Core case alone does not mutate commercial outcome. NoAward is an explicit reviewed result, never absence of selected lines interpreted as approval. Terminal effects are append-only; fresh submission has a new identity.

## 10. Permissions and scope

Distinct empty-bootstrap Core actions are needed for award Submit, assigned Review and purpose Read. Never borrow PR approval, RFQ publication, P36 Draft or generic sourcing edit grants. Current tenant/company/selected branch and named assignment filter before list count/page and detail. Current account and worker-alias separation is checked before every new effect and replay. No employee identity is required.

## 11. Business rules

Each selected offer/line must belong to the exact opened set, invited supplier, original source line and currency. Per-source selected quantity cannot exceed the retained authorized held quantity, including other governed consumers. Exact quote unit price, tax and delivery components remain pinned. An adopted business rule must specify partial-quantity price/tax/delivery allocation, discounts or negotiation evidence; do not derive an authoritative awarded amount from P36 `ProposedValueExact` alone. No default score, rank, tie rule, approval limit or no-award rationale is invented.

## 12. Data ownership

Keep the existing P36 Draft head and revision tables intact. An append-only P36 `PrincipalRfqRecommendationSubmission` freezes one exact revision and pins the new P38 submission identity. New typed P38 review submission, selected line, Core-case binding, terminal outcome and actor receipt own exact parent keys (tenant/company/round/opening/recommendation submission/revision/source/offer). Existing generic award tables remain independent worker-era data. The five new facts now have authored EF maps and append-only same-save guards. Two existing P36 revision keys pin scope and snapshot digest; P32 offer/source-line and versioned policy FKs pin their actual owners. The P36 freeze points to the P38 submission, while the physical FK points from P38 to P36 to avoid an insert cycle. No table is installed or action is enabled.

## 13. Source and commercial evidence

Freeze P36 head/revision/line snapshot hashes, P32 round/opening/offer-set digest, per-offer SHA and source document versions, original PR submission and allocation identities/quantities, approved policy version/hash, Core-case source version, exact currency, quantity and all quote price components. The source verifier must reconstruct the whole retained recommendation line set; a self-hashed but substituted line is rejected.

## 14. Calculations

Transport quantity as canonical decimal18,4 text and commercial values as canonical decimal38,8 text. Use scaled integer arithmetic. Any derived partial award totals require an explicit published allocation and rounding policy. Never use JavaScript Number, database implicit rounding, or historical values reinterpreted through a newer policy.

## 15. Core approval

Use the existing Core case/stage/assignment/decision engine and current permission/branch resolver. The case binds exact frozen P38 submission identity, digest, policy version and source version. `PrincipalRfqAwardCoreHistory.VerifyTerminal` replays the retained named decisions through `ApprovalEngine` and compares full case/stage/assignment terminal state and assignment pins. A Core Approved fact is labelled `CoreApprovedPendingAward`; it never stands alone as an awarded supplier. The owning adapter must still verify current policy, alias and original source facts and persist any approved commercial effect atomically. The maker, original PR maker/requester, recorder, opener, evaluators and named reviewers are compared by current and retained principal/optional worker aliases according to the adopted policy.

## 16. Policy and configuration

A distinct versioned award policy is necessary. Operator-supplied references are recorded provenance, not proof of an in-app independent approval. Publication or commercial-round approval policy cannot be reused as award authority. No approval threshold, tie resolution, supplier eligibility instant or automatic award is enabled by default.

## 17. Integrations

Core owns identity, approval engine and document/version uses; Procurement owns award decision and held PR allocation effects. Finance retains payment/ledger authority. A PO source loader may later consume only an approved P38 outcome under its own funding, authority and supplier checks; never insert P36 Draft IDs into legacy `SourcingAwardId` or direct-PO FK fields.

## 18. API and events

Future commands require original UUID idempotency key, expected version and exact body SHA; GET recovery returns the stored original bytes under current original-actor/scope/action. Audit/outbox metadata is same-save and cannot expose sealed competitor details in broad worklists. No API is registered by this source-only prerequisite.

## 19. Security and privacy

Scope and current action before content lookup/count, enforce sealed-before-open denial on every old quote path, and purpose downloads for exact pinned document versions. Deny stale aliases, revoked branch/action, cross-company evidence, changed policy/source, unapproved suppliers for a new effect, and malformed/mixed schema. Historical reads verify original facts without re-running current catalog eligibility.

## 20. Reports

Report proposed and approved award amounts only from their correctly typed states. Draft proposed value is never spend, commitment, PO, payable, saving or supplier notification.

## 21. Localization

Keep original currency and source unit. Trusted UTC microsecond instants are frozen in digests. Display time zones and currencies without changing authoritative source precision.

## 22. Nonfunctional bounds

Bound participants, criteria, offers, lines, document pins and history. Lock original allocation lines, source event/round, recommendation, and Core approval in one documented order. Use the same physical transaction and positive finite role grants; unavailable schema/capture fails closed.

## 23. Postconditions

Only a verified final Approved outcome yields a typed award source. NoAward and returned/rejected histories remain readable. No PO, contract, stock, dispatch, Finance posting or payment is created by award approval itself.

## 24. Failure and recovery

Persist command, exact receipt, source effect, Core linkage, audit and outbox atomically. Changed body/key/target/actor conflicts. Unknown HTTP outcome blocks blind retry and permits scoped original-key GET. A pending or approved P38 case blocks P36 Draft revision and cancellation. Core Return permits a new P36 revision and new submission/case after retained Core history verification; the former submission and response stay immutable. The pure `RequireDraftMutationAllowed` contract is not wired into the current Draft route, so it is a future activation gate rather than a current runtime claim. Down begins with a retained-evidence veto before any drop. The private empty Up/Down/Up and twelve synthetic audit/outbox effect cases passed; populated P38 owned-family and Core-case vetoes remain unproven.

## 25. Acceptance criteria

Workerless Core maker and independent reviewers complete a real published-policy case; another account/branch cannot list or recover it. A changed offer, P36 revision, quote component, original PR hold, document pin, policy version or Core vote denies without effects. The approved line set conserves quantities and exact components. No default-off/readiness bypass, generic award mutation, automatic PO or Finance effect occurs.

## 26. Tests

Pure: full recommendation reconstruction; duplicate/missing/foreign lines; exact decimal bounds; split quantity conservation; same supplier with changed offer SHA; current/retained actor alias conflicts. The post-overlay source validation passed six focused and 1,697 Domain tests, plus no-pending-model and genuine SQL exports; 1,360 Host passes were carried from the earlier generation source and were not rerun post-overlay. Host: finite Core action, selected branch before count, policy/role revocation, SOD, CAS, replay and original-key recovery remain pending. PostgreSQL: the private empty 265-table Up/Down/Up and twelve audit/outbox-only first-Down `55000` veto cases passed with exact rollback and no drops. Genuine populated P38 family vetoes, allocation races, Core transaction, finite grants and complete owner facts remain pending. Browser uses the existing RFQ layout only after the API is real.

## 27. Dependencies and release gates

The [held-quantity source addendum](P38-HELD-QUANTITY-ADDENDUM.md) defines the pure cross-round residual calculation and its remaining owning-loader and physical proof gates. It does not activate supplier award review.

P36 currently supplies a Draft and no live Submit/terminal recommendation. P38 additionally needs a published award policy, new Core actions, exact document/source loader, finite execution grants and actual API/browser proof. `PrincipalRfqAwardSourceQualification.VerifyCurrent` reconstructs the P36 current revision against the exact opened offer/source projections and requires its stored snapshot to match the canonical line set. `PrincipalRfqAwardFoundation` adds a pure versioned policy grammar, current named Core actor/branch and retained conflict checks, full-offer-line-only quantity and held-capacity checks, exact retained total against an explicitly configured ceiling, and original-key receipt verification. `PrincipalRfqAwardReviewWorkflow.Prepare` constructs typed P36/P38 freeze facts and a real Core `ApprovalCase` with named Any/All stages and assignments in memory; `RequireDraftMutationAllowed` describes pending/return/resubmit behavior. `PrincipalRfqAwardCoreHistory` replays retained decisions through the shared engine and emits only a Core terminal fact. No allocation mode or monetary ceiling is supplied by default. The five new facts have EF maps and genuine migration `20261025100650_P38PrincipalSupplierAwardReview` with a first-Down retained-evidence guard; the private empty-schema and twelve effect-only guard cases passed. Their SaveChanges guard rejects orphan P38 audit/outbox, checks the complete frozen selected line set and same-save companions, and then denies every new P38 submission until an owning retained-source adapter exists. Pure `Prepare` recomputes `VerifyCurrent` and `Qualify` from supplied source inputs before constructing a case; its output is not permission to persist. The future owning transaction must lock and recheck the P36 current cursor and independently verify the submitted revision, whole P32/P29/Core/document graph, original quote components, actual residual held quantity under locks, current aliases and retained receipts. P29 `AvailableQuantity` already subtracts the original Reserved hold and cannot represent that residual; no authoritative cross-award spend ledger exists yet. The adjacent [source readiness manifest](release/P38-SUPPLIER-AWARD-SOURCE-READINESS-20261025.json) remains unavailable. No endpoint, capability or Core action registration exists for these new facts. Terminal outcome inserts are explicitly denied until the actual Core loader is installed. These prerequisites create no persisted award, PO or Finance effect.

## 28. Decisions and assumptions

Procurement governance must configure reviewer stages, award amount authority, evaluation completeness and conflicts, supplier eligibility instant, partial-quantity tax/delivery allocation, negotiated-price evidence, tie/no-award grounds and award-to-PO policy. The current pure grammar permits only an explicitly published `FullOfferLineOnly` mode with an exact selected review branch, currency and maximum; it does not establish that a retained quote total correctly represents unit price, tax, delivery or discount components. Unsupported choices deny. Primary ERPNext source shows a supplier quotation comparison and quotation/order linkage ([comparison source](https://github.com/frappe/erpnext/blob/develop/erpnext/buying/report/supplier_quotation_comparison/supplier_quotation_comparison.py), [quotation source](https://github.com/frappe/erpnext/blob/develop/erpnext/buying/doctype/supplier_quotation/supplier_quotation.py)); these are comparison points, not authority for this ERP's Core approval or Finance behavior.
