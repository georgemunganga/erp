# PROC-06 P38 — Core-principal supplier award

**Status:** Source contract and bounded prerequisite implementation; unregistered and unavailable. **Parent:** [PROC-06](PROC-06-STRATEGIC-SOURCING-AND-AWARD.md), P32 commercial round and P36 human recommendation Draft. **Source baseline:** joint P36/P37 `a3c25d89`. No business award, purchase order, supplier dispatch, Finance posting or production activation is claimed here.

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

New typed P38 submission, selected line, Core-case binding, final outcome/decision, actor receipt and immutable history own exact parent keys (tenant/company/round/opening/recommendation revision/source/offer). Existing generic award tables remain independent worker-era data. Use stable physical FK ownership and append-only guards; avoid mutable cursor values as principal keys.

## 13. Source and commercial evidence

Freeze P36 head/revision/line snapshot hashes, P32 round/opening/offer-set digest, per-offer SHA and source document versions, original PR submission and allocation identities/quantities, approved policy version/hash, Core-case source version, exact currency, quantity and all quote price components. The source verifier must reconstruct the whole retained recommendation line set; a self-hashed but substituted line is rejected.

## 14. Calculations

Transport quantity as canonical decimal18,4 text and commercial values as canonical decimal38,8 text. Use scaled integer arithmetic. Any derived partial award totals require an explicit published allocation and rounding policy. Never use JavaScript Number, database implicit rounding, or historical values reinterpreted through a newer policy.

## 15. Core approval

Use the existing Core case/stage/assignment/decision engine and current permission/branch resolver. The case binds exact frozen P38 submission identity, digest, policy version and source version. Verify every retained vote and actual stage order/quorum before terminal effect. The maker, original PR maker/requester, recorder, opener, evaluators and named reviewers are compared by current and retained principal/optional worker aliases according to the adopted policy.

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

Persist command, exact receipt, source effect, Core linkage, audit and outbox atomically. Changed body/key/target/actor conflicts. Unknown HTTP outcome blocks blind retry and permits scoped original-key GET. Down first vetoes any retained P38 facts, Core case, audit or outbox history before narrowing shared contracts.

## 25. Acceptance criteria

Workerless Core maker and independent reviewers complete a real published-policy case; another account/branch cannot list or recover it. A changed offer, P36 revision, quote component, original PR hold, document pin, policy version or Core vote denies without effects. The approved line set conserves quantities and exact components. No default-off/readiness bypass, generic award mutation, automatic PO or Finance effect occurs.

## 26. Tests

Pure: full recommendation reconstruction; duplicate/missing/foreign lines; exact decimal bounds; split quantity conservation; same supplier with changed offer SHA; current/retained actor alias conflicts. Host: finite Core action, selected branch before count, policy/role revocation, SOD, CAS, replay and original-key recovery. PostgreSQL: genuine model/ownership/CHECK/NULL/append-only, race over allocation, Core transaction, complete owner facts, empty Up/Down/Up and populated first-Down veto. Browser uses existing RFQ layout only after the API is real.

## 27. Dependencies and release gates

P36 currently supplies a Draft and no Submit/terminal recommendation. P38 additionally needs a published award policy, new Core actions, typed owned persistence, exact document/source loader, finite physical capture and actual API/browser proof. The bounded first implementation verifies a retained P36 revision and line set as a prerequisite; it does not mark an award or register routes. Later implementation must close these gates rather than label the verifier a ready award service.

## 28. Decisions and assumptions

Procurement governance must configure reviewer stages, award amount authority, evaluation completeness and conflicts, supplier eligibility instant, partial-quantity tax/delivery allocation, negotiated-price evidence, tie/no-award grounds and award-to-PO policy. Unsupported choices deny. Primary ERPNext source shows a supplier quotation comparison and quotation/order linkage ([comparison source](https://github.com/frappe/erpnext/blob/develop/erpnext/buying/report/supplier_quotation_comparison/supplier_quotation_comparison.py), [quotation source](https://github.com/frappe/erpnext/blob/develop/erpnext/buying/doctype/supplier_quotation/supplier_quotation.py)); these are comparison points, not authority for this ERP's Core approval or Finance behavior.
