# PROC-OP-POLICY-WORKER-01 — original people behind receiving and matching policy

Status: planned next backend milestone; not released. Prepared: 22 October 2026.
Owner: Procurement coordinator/backend. Risk: Tier 3 under GW-ENG-STD-001.

## 1. Feature identification

Procurement → Configuration → Receiving / Invoice matching policies → retain original author, submitter and reviewer identity.

## 2. Purpose and business outcome

Changing an account's employee link must not transfer policy authorship or let the original author approve their own policy through another account. Operational receiving and matching must consume verified governance.

## 3. Scope and exclusions

Only RECEIVING and INVOICE_MATCHING configuration lifecycle and its operational consumers. Preserve historical records. No new policy engine, employee directory, approval thresholds, Finance postings or replacement UI. P29 SOURCING and existing sealed business payloads remain unchanged.

## 4. Parent and related features

Existing PROC-CFG-01 policy configuration, receiving, matching, Core canonical workforce resolution, policy lifecycle, shared audit and existing evidence encoding/schema inspection. Keep separate from spending-authority configuration (PROC-AUTH-WORKER-01).

## 5. Actors and participants

Current canonical active author, submitter and independent reviewer with existing configuration permissions and scope. Historical people can be offboarded after their recorded actions; their identities remain attributed.

## 6. Entry points and triggers

Existing policy Create/Update/SubmitReview/Publish/ReturnForChanges/Effective routes and existing receiving/matching selectors, commands and assignment reads. Public bodies never supply historical worker evidence.

## 7. Preconditions

Actual owning schema ready; authenticated tenant/company and current request worker; existing permissions, record version, state, rules validation, effective intervals and coordinated owning transaction. Current Core reads must observe the command's authorization snapshot.

## 8. Workflow

Create draft and capture observed author → original-author editing → submit exact rules/version and capture submission → independent reviewer verifies retained workers → publish exact submission and retain reviewer → effective reads and operational consumers verify publication lineage. Return preserves history; resubmission creates a new retained round.

## 9. Status and state transitions

Keep existing policy states and business transitions. Evidence stages: Created, Submitted, Published. Every stage links its exact owner revision; Published links the exact Created and Submitted rows. Cancellation/retirement of historical unknown policies requires an explicit authorized reconciliation contract, never implicit adoption.

## 10. Permissions and data scope

Existing configure/review actions remain required. Original principal AND worker are required for edit/submit. Review must be independent of original maker/submitter workers and principals. A role or administrator flag does not bypass those checks. Scope is tenant/company/owned policy; record access never follows a caller-supplied scope.

## 11. Business rules

Missing/corrupt/foreign/stale proof blocks protected new work. Current identity mappings cannot backfill historical workers. An alias of the original maker cannot review. A changed author mapping cannot transfer a draft. An independent reviewer can act after the historical maker is offboarded. Unproven published policy must not be represented as verified effective configuration or silently superseded.

## 12. Data fields and ownership

Add Procurement-owned OperationalPolicyIdentityEvidence: schema version, stable ID, tenant/company, allowed PolicyKey (RECEIVING or INVOICE_MATCHING), stable PolicyVersionId/PolicyVersionNumber, action stage, observed principal/worker, normalized action time, exact rules digest, owner record revision and typed scoped Created/Submitted lineage. Scope FK references stable policy identity, never mutable record Version. Employee/account masters remain Core-owned; no Core FK that requires historical people to remain active.

Use a separate typed table: P29 SOURCING evidence deliberately hardcodes its policy key and exact schema/digest contract. Broadening it would change reviewed historical meaning. Reuse common primitives, not a second workflow engine.

## 13. Screens and data views

Reuse existing policy UI/API. Server views report unavailable or unknown historical governance truthfully and return only permitted actions. Historical policy and transaction inspection remains available. No new mock configuration save paths.

## 14. User experience and human behaviour

Preserve entered rules on validation/version failures. Explain that an old policy lacks original approval identity and needs a newly reviewed replacement/reconciliation. Do not display an unproven policy as valid merely because RulesJson parses.

## 15. Notifications and communications

Reuse existing notifications/audit contracts. No fabricated successful-transition message or outbox event on denial. Notification delivery cannot decide whether the policy transaction committed.

## 16. Documents and attachments

N/A: no upload or document ownership changes.

## 17. Exceptions and edge cases

Maker/reviewer swaps with both accounts active and unique; aliases; recipient-free policy review; changed request worker; offboarded maker; return/resubmit; modified rules; stale owner revision; cross-company lineage; attached fake historical row; legacy open-ended publication; concurrent review/publication and unknown response.

## 18. Integrations / APIs / events / sandbox

Reuse actual Core canonical identity and existing scoped policy endpoints. Enlist reads in the same coordinated transaction; do not query another database as proof. Receiving source resolution, matching commands and selector policy must verify the owned publication before authorizing new operations. Assigned receiving task checks precede counts/paging. Private disposable API/PostgreSQL rehearsal only; no production test writes.

## 19. Audit / security / privacy

Append-only typed action history, exact original workers/digests/lineage and same-save policy/audit capture. Finite runtime role has read-only Core facts and append-only evidence; reject table/schema/database ownership and elevated roles that defeat those restrictions. Audit is retained independently of notification delivery. No names, credentials or unrelated HR data are copied into evidence.

## 20. Reports / dashboards / metrics

Historical governance may be unknown. Existing authorized views should distinguish that state from verified effective configuration. Do not count unknown policies as successfully independently reviewed. No new aggregate dashboard requirement.

## 21. Configuration and localization

No business thresholds, tolerances, authority defaults or approver assignments are invented. Existing company currency, localization and module settings remain authoritative.

## 22. Non-functional requirements

Atomic policy/evidence/audit persistence, bounded indexed lineage reads, exact schema readiness and reliable transaction rollback. State the existing SERIALIZABLE snapshot guarantee honestly. Immediate account-change revocation needs a Core-owned cooperative identity-writer fence; no Core UPDATE permission may be introduced for SELECT row locking.

## 23. Postconditions

Successful protected actions retain the observed actor and exact owner revision/rules. Effective operational configuration is based on retained verified publication. Denials do not mutate policy, evidence, audit, outbox or operational counts.

## 24. Failure and recovery

Rollback the entire command on persistence/schema/conflict failures. Preserve drafts and all old evidence. Use authorized GET reconciliation after uncertain responses; do not automatically repeat a publish mutation. Additive migration must preserve legacy rows and prohibit destructive Down once evidence/audit is retained.

## 25. Acceptance criteria

Actual unique Core worker swaps deny self-review without a data footprint. Original author rebinding denies edit/submit. Independent review works after maker offboarding. Changed rules/version/round and foreign or absent lineage deny. Receiving/matching new work fails closed on unproven policy; historical data stays readable. Legacy reconciliation is explicit. Existing PR/PO/SOURCING controls remain intact.

## 26. Test scenarios

Pure guards and chronology; same-save persistence and attached-row denial; scoped FK/check/unique constraints; upgrade preservation/no backfill; retained Down veto; mapped HTTP permissions/action hints; actual finite-role Core swaps/offboarding/ownership negatives; concurrent command/rollback footprints; applicable existing UI Playwright states. Label pure, InMemory, mapped HTTP, PostgreSQL and browser evidence separately.

## 27. Ownership and dependencies

Coordinator owns backend, migrations, actual evidence and release. Core owns identity and any stronger identity-change fence. Manus remains on merged-main vendor work. Engineering review is required before enabling financial/operational flags. Do not overlap migration snapshot authorship with P30 verification.

Implementation stages:

1. Pure policy-key-aware evidence contract and focused tests.
2. Typed append-only persistence, additive migration and isolated schema/recovery proof.
3. Lifecycle/current identity/action hints/effective view integration.
4. Receiving/matching consumers and historical compatibility.
5. Actual Core/finite-role/concurrency/browser proof and release review.

## 28. Open decisions and assumptions

Legacy open-ended published policies need an authorized retirement/replacement reconciliation path; no authority is assumed. A stronger immediate identity-change fence is separate Core work. Financial and historical-maker release holds stay in place until actual evidence and independent review close them. This specification does not approve deployment or claim implementation is complete.
