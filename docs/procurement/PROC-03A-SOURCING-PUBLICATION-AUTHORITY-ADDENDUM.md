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

## Executed backend checkpoint —21October2026

Source `17a0a5e` passed781Procurement domain/model,311mapped API/Core regression and112shared document tests (separate suites). Actual isolated PostgreSQL settings mode passed source draft creation, submission and independent activation; exact `SourcingEvent.Publish`/`procurement-sourcing-publish`/`ApprovedLineBase` tuple, PR/PO family isolation, wrong-currency no-effects and absence of operational publication were checked. Restricted runtime privileges were exercised. Protected executable/provenance/log/result: `/home/amizo/erp-backups/procurement-p29-http-policies-20261021_094635/`. This mode did not produce a schema2 request submission; its explicit NOT EXERCISED result remains retained.

Existing UI application `e366f78` passed30focused unit tests, TypeScript/staged live build and22/22 mocked HTTP browser contracts, including the direct existing RFQ approvals tab. An actual UI authority workflow with independent persisted owner proof remains the next gate; it is not inferred from those mocks or the standalone API result. No publication endpoint/operational flag, production migration or deployment was enabled.

## Actual configuration browser fixture — authored source, not executed

The existing `ApprovalHttpPostgresProbe` adds the narrowly allowed pair
`--serve --settings`; standalone `--settings` and all other existing modes retain
behavior. The pair requires distinct private bootstrap/runtime connections, normal
Host routes/readiness, real committed Procurement/CoreApproval migrations and the
existing loopback-only Kestrel/0600 manifest boundary. It skips the ordinary browser
PR/Finance/approval seeding and the default synthetic PR policy: no financial grant,
policy, request, case or RFQ event is created before the browser. IT category and
canonical synthetic Core company/branch/workers/accounts are configuration fixtures.

`actors.maker` has only access/config-read/authority-read/configure;
`actors.reviewer` only access/config-read/authority-read/review. They and
`actors.namedmanager2` are three distinct canonical company workers, confined to the
actual branch. The named manager has explicit `procurement-sourcing-publish` but
**zero** authority limit until the UI creates/reviews one. No ERP-admin fallback,
implicit unlimited grant or operational publication is enabled. Normal scoped action
list and people lookup preflight the typed tuple and named person. Schema readiness
is calculated by actual normal services, never a Boolean fixture substitute.

The private manifest's `sourcingAuthority` specifies reference
`BROWSER-SOURCING-LIMIT`, named manager principal/display name, exact decimal string
`1.2345`, ZMW, ApprovedLineBase, action SourcingEvent.Publish, the actual branch,
explicit all-departments/all-categories, UTC-midnight effective start and no end.
Activation reason is `Browser independent sourcing limit approved`. The existing
ERP authority editor performs exactly three POSTs: Create Draft → Submit → independent
Activate. The fixture does not precreate these effects or fabricate approval history.

With `PROCUREMENT_SOURCING_AUTHORITY_BROWSER_VERIFY_ON_STOP=1`, after shutdown a
fresh bootstrap-owned verifier requires exactly one Active grant at record revision3,
authority version1, the complete exact tuple/scope/amount/actors/reason/fingerprints,
and exactly six audits: three lifecycle snapshots proving Draft1/PendingReview2/
Active3 and three automatic create/update records. A complete original Procurement
and CoreApproval ordinary-table/history fingerprint, excluding only that grant and
its six audits, must remain identical; categories, outbox, policy and all operational
records are included. No RFQ events, source request or policy may appear. This is
configuration evidence only, not permission to execute an RFQ publication handler.

All fixture/browser source is uncompiled/unrun at this checkpoint. Parent owns the
serialized compile, private role/schema/runtime, actual browser and independent
persisted-state gates. Preserve genuine failures and do not relax permissions or
accept browser labels as durable success.
