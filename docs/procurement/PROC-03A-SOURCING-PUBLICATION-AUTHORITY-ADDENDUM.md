# PROC-03A — Sourcing publication finite authority configuration addendum

## 1. Feature identification

Bounded backend source from `a553c1a`; extends existing PROC-03A configuration, not RFQ execution. Source-only, unbuilt/unexecuted; production release hold unchanged.

## 2. Purpose and outcome

Allow administrators to configure independently reviewed finite RFQ publication limits using the existing authority workflow, without treating a role or the P29 policy marker as a financial limit.

## 3. Scope and exclusions

Exact tuple `SourcingEvent.Publish` / `procurement-sourcing-publish` / `ApprovedLineBase`. Existing create/edit/submit/return/activate/supersede/revoke/read paths and exact amount transport. No publication handler, Core publication case, supplier invitation, dispatch, extra reservation or Finance ledger.

## 4. Parent and related features

PROC-03A, PROC-06 O4/O5, P29 sourcing policy identity, existing Core role management. P29 published policy configuration and finite authority are separate prerequisites.

## 5. Actors

Existing canonical company configuration owner, authority subject and independent reviewer/revoker. The new action has zero bootstrap grants; current explicit Core action grants remain distinct from approved finite limits.

## 6. Entry points

Existing spending-authorities endpoints; explicit `action=SourcingEvent.Publish` on lists/lookups and creation body. Missing action retains default PurchaseRequest.Approve behavior.

## 7. Preconditions

Current server-resolved identity, configuration permissions and record scope; referenced company/branch/department/categories; normal transactional PostgreSQL write contract. No fake-ready test provider enters production.

## 8. Workflow

Draft → PendingReview → Active, with existing return, replacement and revocation paths. Named current principal/action lookup filters by the new permission. No command changes an RFQ state.

## 9. States

Existing authority states/transitions/CAS and complete-family invariants are reused unchanged. Historical PR/PO families remain typed and isolated.

## 10. Permissions and data scope

Configuration actions remain authority-read/configure/review/revoke. Only the exact new publication action is registered in Core's existing catalog, with empty bootstrap roles. Branch/department context filters and company/tenant boundaries apply before pagination; new permission is not an alias for draft/edit/convert/cancel or PR/PO approval.

## 11. Business rules

One finite grant must cover the complete target amount and categories/scope, with correct currency/basis/action/permission and effective period. Exact 18,4 maximum grants compare against exact representable 38,8 selected source base; no rounding or summed small limits. Same-person/current-worker review guard remains the existing server implementation; neither subject nor maker may independently activate its own grant.

## 12. Data and ownership

Reuse ProcurementSpendingAuthorityGrant/Categories and Core identity/org records. No schema/model change, duplicate identity or new ledger. Published fingerprints/category immutability and append-only audit rules remain.

## 13. Screens and views

Existing Approval limits selector adds Request for quotes in a separate frontend slice. No new menu/page. Backend list returns exact createAction/createAmountBasis and current canCreate; rows retain exact action/permission/basis.

## 14. User experience

Frontend must require matching metadata, not infer support from configurationVersion1. Older endpoints rejecting or ignoring the action fail unavailable. Configured limits do not display a publish-ready RFQ.

## 15. Communications

Existing configuration audit/history; no new notification or external sending capability.

## 16. Documents

No document authority is introduced. Actual publication still requires separately verified Core documents/retained use pins and owner lifecycle coupling.

## 17. Exceptions

Wrong currency/site/identity, incomplete amount/basis, stale version, self-review and wrong typed grant fail without financial or RFQ effects. Missing transactional provider cannot report successful configuration writes.

## 18. APIs/events/sandbox

Existing DTOs and transport only. List/lookup explicit action; create action explicit; default PR remains. Settings probe source adds real sourcing draft/submit/independent activation and PR/PO list isolation, plus wrong-currency no-effects and no publication-handler/record assertions. No new event schema.

## 19. Audit/security/privacy

Reuse scoped minimal named-person lookups and canonical independent reviewer guard. No private identity contacts/credentials or hidden-family IDs. Catalog role grants remain empty; finite configuration can never substitute for current actual operation permission or the missing execution contract.

## 20. Reports

Existing named authority reads/history. No spend or sourcing publication KPI claims.

## 21. Configuration/localization

Existing company currency and exact ApprovedLineBase basis, no business thresholds/default grant. Config metadata is separate from business policy and operational capability.

## 22. Non-functional

Preserve bounded lookups, exact decimal strings, serialization/CAS, same physical transaction, current scope and complete family reads. No new background job.

## 23. Postconditions

An independently activated typed finite grant may exist. It is configuration evidence only; source/Finance/catalog/supplier/document/Core review/publication persistence prerequisites remain unsatisfied until their adapters are implemented and verified.

## 24. Failure/recovery

Existing conflict/reload and transactional rollback semantics. No new policy/grant UUID receipt promise. Preserve original PR/PO published rows and historic evidence.

## 25. Acceptance

Exact tuple creation/metadata/filtering, current explicit-role grant/revocation, scoped named-person projection, wrong-currency/scope denial, independent review and exact amount limits; PR/PO compatibility; no callable operational publish endpoint or new RFQ effects.

## 26. Test scenarios

Authored pure lifecycle tests cover finite boundary and eight-place excess/currency/branch/cross-transaction denial, maker/subject review denial, wrong basis and foreign owner/permission. Mapped API tests cover exact list/action metadata and branch-filtered records, current read-only settings, action-specific people and currency/context no-effects. Existing bootstrap/revocation tests now include publication action. Earlier tests asserting unsupported registration are updated to the new configuration contract while CanPublish remains false. Actual settings probe is authored only; no jobs run in this slice.

## 27. Ownership/dependencies

Procurement owns finite grant decisions; Core owns identity/roles/scope. Backend source paired with frontend `8ed77a5`. No migration needed. Parent performs independent review and serial domain/API/runtime/UI verification before accepting or deploying.

## 28. Open decisions

No publication execution policy is inferred. Next O4/O5 adapters must prove current source/catalog/Finance, supplier audience and contact eligibility, scanned immutable pack/version retention with cooperative lifecycle writers, actual Core case/votes and sealed internal publication. Supplier dispatch/bids/award remain later contracts; production options and release hold stay unchanged.
