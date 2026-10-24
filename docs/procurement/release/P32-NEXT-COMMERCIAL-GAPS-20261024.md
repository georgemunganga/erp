# P32 commercial sourcing: remaining workflow and UI contract

**Status:** Source inventory, 24 October 2026. P32 is held, default off, and has no generated/accepted owning migration or runtime proof. This document does not activate an action or grant a role.

## Sources and ownership

- Business contract: [PROC-06 strategic sourcing and award](../PROC-06-STRATEGIC-SOURCING-AND-AWARD.md) and its [P32 28-section successor](../PROC-06-P32-PRINCIPAL-COMMERCIAL-COMPLETION.md), especially sections 5–13, 17–20, 25–28. The latter describes the intended entire lifecycle; held source now reaches purpose-scoped offer opening and individual criterion assessment without runtime acceptance.
- Restored live UI inspected at module-connect `d4d3bbb`: `reference-ui/src/features/LiveSourcing.tsx`, `PrincipalPublication.tsx`, `RfqPresentation.tsx`, `RfqWizard.tsx`, and `/rfqs` routes in `reference-ui/src/app/App.tsx`. The five-tab RFQ detail frame is Overview, Items, Related, Comments, History.
- Held backend inspected at P32 `489c8aeb7e4553d5a0d7740c00e0efae31033b6f`: `ProcurementRfqCommercialRoundRoutes.cs`, `ProcurementRfqCommercialOfferOpening.cs`, `ProcurementRfqCriterionAssessments.cs` and their owned application/model mappings. `ProcurementRfqCommercialOptions.Enabled`, `OfferOpeningEnabled` and `CriterionAssessmentEnabled` default false; missing physical readiness fails closed. P32 routes below are source contracts, **not live endpoints**.
- Procurement owns the commercial round, original supplier offer, evaluation, recommendation, award, and RFQ-to-PO handoff. Core owns people, roles/action grants, approval cases, and documents. Procurement's PR allocation is the demand balance. Finance owns budget and AP/payment; Purchasing owns any resulting PO. Supplier contact entered by a buyer is not portal delivery or supplier authentication.

## Screen-to-API inventory

| Screen or visible control | Current behavior and contract | Remaining P32 behavior |
|---|---|---|
| `/rfqs` and `/rfqs/new` | Live list, source mode, and draft wizard use existing persisted P29/P30 source or ad hoc draft APIs when corresponding capability is true. The imported wizard's dates and evaluation prompts are planning fields, not adopted commercial rules. | Keep the same list/wizard and add a separate governed commercial setup section only when a complete P32 capability and action grant are returned. |
| `/rfqs/:id`, Overview/Items | Principal detail shows real approved source quantities, internal publication, independent publication review, and current status. `Request publication review` opens the P30 Core review; it approves an internal audience, not a supplier award. | A P32 commercial setup/prepare/Open panel can use `PUT /sourcing/{id}/commercial-draft`, `POST /sourcing/{id}/commercial-prepare`, `POST /sourcing/{id}/commercial-rounds`. The server must return actual configuration/readiness, current permitted actions, and original command recovery; the present UI does not call these routes. |
| Detail header `Publish`/`Send to suppliers` | Deliberately disabled. Published means internal publication only. | Do not turn it into supplier dispatch or unseal. There is no external send/portal delivery API. Manual contact remains an explicit separate P30 fact. |
| Related: shortlisted suppliers; `Record manual contact` | P30 records an external contact reference; no email or portal message is sent. | P32 must retain approved frozen audience membership and compare contact/invitation/source pins before allowing an offer. |
| Related: `Enter quotation Draft`, `Save Draft revision`, `Cancel Draft` | P30 buyer-entered manual quotation Draft has persisted exact line prices/tax/delivery and revision/CAS. Its UI says no supplier bid has been submitted. | P32 `POST /sourcing/{eventId}/commercial-rounds/{id}/offers` records an immutable, source/document-pinned manual submission in an approved accepting window. A dedicated Submit control needs explicit recorder authority, received-versus-entered timestamps, durable command key, and truthful sealed acknowledgement. It must not relabel the Draft save as submission. |
| Assigned review | P30 publication review can Approve/Return/Reject. P32 holds `GET /commercial-round-reviews/assigned`, detail, pinned requirement document list/download, and `POST /commercial-rounds/{id}/assignments/{assignmentId}/decisions`. | Keep publication review distinct from commercial-round approval. P32 assigned Core reviewers see requirements and criteria, not offer bodies. No generic sourcing reader or buyer gets reviewer document access. |
| Commercial round directory/detail/close | Held P32 `GET /commercial-rounds`, `GET /commercial-rounds/{id}`, `POST /commercial-rounds/{id}/close`, and `GET /commercial-round-commands/{operation}/{key}`. The held detail reports sealed counts/status and explicitly `awardMade=false`, `supplierDispatchConfirmed=false`; it does not reveal offers. | Adapt into the existing RFQ detail tabs after physical/finite-role proof. Close is a seal, not opening/evaluation. Do not expose competitor prices through P30 draft/list endpoints or actor-owned receipt replay. |
| Related/History award text | Current UI explicitly says bids, evaluation, awards, and purchase orders are unavailable. Ad hoc/worker detail says the same. Held source now includes purpose-scoped offer opening/read and one evaluator's criterion assessment/recovery. There is no approved runtime admission or recommendation, award, or award-to-PO route. | Keep unavailable states until genuine schema, finite role, source/readiness, UI and runtime proof exist. Legacy bid/evaluation/award tables alone do not prove governed P32 execution. |

All routes above are under `/api/procurement/v1`. Actual P32 action keys are `procurement-sourcing-commercial-configure`, `procurement-sourcing-round-open`, `procurement-sourcing-commercial-approve`, `procurement-sourcing-offer-submit`, and `procurement-sourcing-round-close`; bootstrap grants are empty. A generic sourcing read/edit/publish grant cannot become unseal, evaluation, or award authority.

## Unbuilt governed transitions

1. **Open sealed offers:** held source now models a separate named Core officer, exact round/source/policy/document pins, trusted server time, conflict checks and immutable opening/receipt evidence. Purpose-only opened-offer reads and original-key recovery are authored. Both owned families, default-off admission and finite privileges remain uninstalled/unproven, so no offer-price projection is live. Closing officer identity is not automatically opening authority.
2. **Evaluate:** held source now models an assigned evaluator's immutable score or pass/fail observation for one frozen criterion and opened offer, its revision and original-key receipt, own-history read and separate current actions. The two assessment families are uninstalled/unproven; this is not a complete evaluation result. Full-set completeness, score aggregation, mandatory-fail interpretation, price formula, tie-break and quorum are not adopted. No recommendation may infer them.
3. **Recommend and decide an award:** recommendation must select supplier **per line**, exact quantity/value/currency, rationale, disqualifications, and the frozen evaluation evidence. A separate award Core case and published authority policy, independent approvers, full decision history, and current scope are required. Publication approval and commercial-round approval are not award approval. Return/reject/no-award retain history; partial award must conserve PR allocated quantities and never release another consumer's hold.
4. **Handoff to Purchasing:** an approved award can expose an owned, versioned award source to the PO module. PO creation remains a separate governed command with supplier/current source checks and its own CAS/receipt. No automatic PO, contract, Finance reservation, payment, or stock balance follows merely from an award row.

## Earlier bounded milestone: opening the sealed round

Add **one versioned, default-off opening transition and purpose read** after the current sealed-close record. Its entry is a genuinely closed P32 round with an exact retained offer-set digest. Inputs are round ID, expected version, original UUID command key, and a reason/reference required by an adopted policy; no caller-supplied “verified” boolean. Resolve a named opening officer through current Core action and company/branch scope, enforce conflicts against maker/recorders/reviewers using current and retained aliases, and pin the actual published opening policy or return unavailable if none exists. In one owning transaction, verify the retained closed set and document use, append an immutable opening record and actor-owned response receipt, then expose only the opened offer versions to authorized named evaluators through a purpose-specific, scope-before-count reader. Same-key replay returns the original acknowledgement; changed body/key conflicts; unknown response is recovered by GET only. No score or award is produced.

**Required decisions before enabling:** who may open, whether the closer may also open, how opening is timed, what document evidence is mandatory, and the exact evaluator visibility/conflict rule. These are policy data with independent approval, not values to hard-code. The implementation can remain default off while these choices are configured and reviewed.

**Acceptance boundary:** genuine P32 migration and finite role/readiness capture first; actual Core case and document service, normal cookie accounts without employee links, sealed-before-open denial, exact original/document hashes, branch isolation before list/count, current alias revocation/relink negatives, duplicate/replay/CAS, tampered set/receipt negatives, retained history reload, and source allocation unchanged by opening. Preserve the imported five-tab RFQ frame. Keep evaluation, award, dispatch, PO, and Finance capability false until their separate owned proofs exist. This was the earlier opening checkpoint; the later assessment source has not passed these gates either.

## First P32 migration admission and rollback plan

`P32CommercialRoundRollbackGuards.BeforeDownSql` is authored source only. The future genuine EF migration must invoke it **as the first Down statement**, before removing any new table, shared CHECK change, FK, index, or permission. It takes `ACCESS EXCLUSIVE` locks, then raises PostgreSQL `55000` for a row in any of the **fifteen** P32-owned families: three commercial Draft, two preparation, six round/offer/receipt, two offer-opening and two criterion-assessment families. It also vetoes retained `SOURCING_COMMERCIAL_ROUND` rows in the shared `policy_versions` and `operational_policy_identity_evidence` tables, the exact `core_approval.cases` source type `sourcing_commercial_round`, and owned opening/assessment audit or outbox evidence. A child Core stage/assignment/decision has an owned case FK, so a case check covers that retained graph. The guard does not delete, repair, downgrade-encode, or relabel any record. Source coverage tests compare the modeled fifteen-family set with the guard's locks and row checks; only actual PostgreSQL can prove the transaction rollback and complete footprint preservation.

The **six round-family managed FKs** point from opening to prepared plan/published policy identity, vote to opening, terminal decision to vote, offer/seal to approved decision, and receipt to its exact target. This directed portion has no new EF insert cycle; do not add speculative deferred overlays. The earlier commercial Draft header ↔ current revision is a real cycle and retains its already authored, deferred exact-owner companion in `RfqCommercialDraftOwnershipConstraintSql`; preparation has its separate authored exact-owner companion. Those companion statements are proposals until the generated migration and actual save ordering are reviewed.

`SupplierBid.InvitationId` remains nullable for legacy records. The managed P32 model adds the **nonpartial** exact six-column unique index on bid tenant/company/id/event/supplier/invitation. The authored `PrincipalRfqCommercialOfferInvitationOwnershipSql.Install` then adds the six-column FK from a new offer whose invitation is required. It must run only after the genuine index exists. PostgreSQL must demonstrate: a valid invitation-bearing offer saves, a retained legacy NULL-invitation bid remains readable, and a wrong invitation within the same company fails; no legacy bid column is made required. Generated FK/check names and the full owner graph must be captured from actual PostgreSQL, not inferred from this document.

Before calling P32 schema ready, run fresh Up→Down→Up on a private clone and independently retain rows in each of the fifteen families and shared policy/Core/audit/outbox history. Each used Down must raise the exact `55000` guard and leave data, schema, migration history, audit and Core records unchanged. Partial installation, absent finite DML/SELECT grants, and wrong physical capture stay unavailable. No P32 migration, designer, snapshot, readiness capture, or runtime capability is created by this source checkpoint.

### 24 October held opening implementation checkpoint

The additive policy v2 and owning adapter are authored in the existing P32 branch.
Schema1 still grants no opening authority; explicit `offerOpening` controls are
frozen by the same Core commercial approval. New immutable opening/receipt families,
OpenOffers command, GET original-key recovery and named evaluator-purpose offer
reader remain default off with unavailable physical readiness. See the final two
addenda in the existing P32 28-section specification for exact fields/routes, thirteen
family rollback coverage and pending genuine PostgreSQL admission. No imported UI
was changed; evaluation, award, dispatch, PO and Finance availability remain false.
This supersedes the earlier inventory's “no unseal route” statement as a source-only
checkpoint, without claiming an installed endpoint, adopted policy or executed test.

### Held individual-assessment checkpoint at `489c8aeb`

Held source now includes `POST /commercial-rounds/{id}/assessments`, own assessment
history and actor-owned assessment-command recovery. One named Core evaluator may
record or revise one exact score or pass/fail observation against one opened offer,
frozen criterion and original document pin. The two append-only assessment families
bring the held model to fifteen owned families. They neither aggregate a result nor
select an award; `CriterionAssessmentEnabled` remains default false, the positive
physical readiness provider is absent, and no genuine P32 migration or finite-role
runtime proof exists. A further held purpose read projects missing/current
observations for one opened offer and frozen criterion to an explicitly named
reader after retained proof; it exposes no scores, pass/fail results or winner.
The imported RFQ UI has no assessment binding. The earlier
eleven/thirteen-family counts and opening-only text above are historical checkpoints,
superseded by this source inventory.

**Next source boundary:** a recommendation Draft requires an exact complete set of
current assessment revisions, a frozen comparison method and named recommendation
maker authority. The 28-section contract leaves score aggregation, mandatory-fail
interpretation, price comparison, tie-break and award policy open. Do not invent a
winner, use publication/commercial-round approval as award approval, or use legacy
`BidEvaluation`/`SourcingAward` rows as a bypass. After these controls are explicitly
adopted, a separate recommendation Draft can pin the opened offer set, assessment
revision digests and source-line quantities/value without changing PR allocation.
Independent Core award approval and PO source handoff remain later slices.
