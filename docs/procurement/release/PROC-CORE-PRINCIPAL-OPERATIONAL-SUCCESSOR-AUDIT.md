# Core-principal operational Procurement successors

Status: source audit only, 22 October 2026. No operational source, schema, flags or runtime changed.
Backend examined: integrated P23 `dbf5107` (runner-only `dba76fc` adds no application change).
UI examined: `ae54ca8`. The P25 PR/authority/Finance successor is separately owned by the approval
agent; its reported source `1ef735f` is not merged or treated as runtime proof here. No migration IDs
are assigned by this plan. P24 remains reserved for recurring purchase planning.

## Finding

Core-only users already have ordinary draft CRUD. Operational RFQ holds, commercial PO execution
and receiving still intentionally consume worker-pinned historical contracts. Removing one HTTP
worker check would leave incompatible persisted evidence and could bypass independence or replay
checks. Use explicit retained-version successors and existing providers/engines, with optional
genuine Core worker links used only as alias vetoes. An employee is not the source of module access.

Paths below are relative to `backend/hrm-api/src/Mightyfin.Erp.Hrm.Api/` (HTTP),
`backend/procurement-api/src/Mightyfin.Erp.Procurement.Application/` (application),
`backend/procurement-api/src/Mightyfin.Erp.Procurement.Domain/` (domain), and
`backend/procurement-api/src/Mightyfin.Erp.Procurement.Infrastructure/Data/` (store).

## Concrete choke points and required contracts

| Stage | Exact source / symbols | Current dependency | Smallest coherent successor |
|---|---|---|---|
| Shared operational actor | HTTP `ProcurementAuthorityIdentity.cs`: `ActorAsync`, `ResolveCanonicalAccountAsync`, `ResolveCanonicalWorkerAccountAsync`, `CanonicalAccount` | Requires a linked worker, active same-company HR home and unique worker-account mapping; ActorAsync equates scope worker with account worker. | Reuse current Core principal/action/scope resolver through a separate versioned path. Preserve legacy `CanonicalAccount(Guid WorkerId)` for legacy consumers. P25 owner confirms this separation is being retained. |
| Approved PR source | Application `PurchaseRequestApprovalPinning.cs`: `HasRetainedWorkerIdentity`, `Verify`; `ApprovedPurchaseDemand.cs`: `Verify` | Existing submission snapshot2 requires maker/requester workers. RFQ and PO consume this pin via approved-source validation. | Consume P25's genuine principal submission3 through an explicit approved-demand contract. Keep snapshot2 validation unchanged; do not derive original requester from today's worker-account lookup. |
| Governed RFQ create/read/cancel/replay | HTTP `ProcurementSourcingExecutionRoutes.cs`: `Actor`, `Owned`, `Replay`; `ProcurementSourcingExecutionCommands.cs`: create/binding/receipt writers. Application `GovernedRfqDraft.cs`: `RfqCurrentActor`, `RfqDemandPin`, `Draft`, `Receipt`, `PreparePublicationIntent`, `RequireActor`, `RequireOwner` | Non-null maker/requester/actor workers; ownership and command receipts compare worker equality; original fingerprint includes workers. | Add a declared successor identity version, real maker/requester/actor principals and nullable genuine alias pins throughout original envelope, demand bindings and receipt storage. Dispatch fingerprint/restoration/replay by version; retain actor-owned command recovery and allocation transaction. |
| RFQ persisted constraints | Domain `SourcingDraftPersistence.cs`, `SourcingPolicyIdentityEvidence.cs`; HTTP `ProcurementSourcingDraftSchemaReadiness.cs` | Original envelope schema1, binding maker/requester workers and receipt actor worker are required by actual PostgreSQL CHECKs. Event execution1 also requires maker worker. | Correlated versioned principal constraints, never unconditional relaxation. Capture actual generated PostgreSQL definitions/readiness. No historical row rewrite or guessed principal backfill. |
| Sourcing policy / finite publication preflight | HTTP `ProcurementSourcingPolicyConfiguration.cs`: `CheckSourcingCompanyRules`; `ProcurementSourcingReviewerLookup.cs`; `ProcurementSourcingPolicyIdentity.cs`. Application `SourcingPolicyIdentity.cs`, `SourcingPublicationAuthority.cs` | Policy identity schema1 pins author/reviewer workers. Named principals are resolved to unique active workers. Publication preflight uses existing finite grant family and full-event amount; `CanPublish` is always false. | Principal-first author/reviewer evidence successor plus nullable alias pins; current permissions and scope, named policy stages and finite grants remain required. Keep the complete authority family and same amount basis. This alone does not implement publication, bids, award or supplier portal. |
| PO commercial approval | HTTP `ProcurementOrderApprovalCommands.cs`, `ProcurementOrderAuthorityResolver.cs`: `ResolveAsync`, `RevalidateAsync`, `AuthorityAsync`. Application `PurchaseOrderWorkerIdentity.cs`, `PurchaseOrderCommercialWorkflow.cs` | Required buyer and assignee workers; participant worker JSON/hash includes PR maker/requester workers. Resolver filters configured manager stage semantics, worker uniqueness and retained finite authority. | Versioned immutable participant principal set plus optional genuine aliases; principal buyer/assignee identity evidence, current Core scope/actions and existing finite `PurchaseOrder.Approve` authority. Preserve explicit policy stage semantics; do not silently turn a named/role stage into every account with a grant. |
| PO Finance confirmation, issue and dispatch | HTTP `ProcurementOrderFinanceCommands.cs`, `ProcurementOrderIssueCommands.cs`, `ProcurementOrderDraftCancellationCommands.cs`, `ProcurementOrderFinanceResolutionCommands.cs`, `ProcurementOrderExecutionReadViews.cs`: `Canonical`, `ReadActor`, candidate and participant checks | Buyer/Finance verifier/delivery verifier workers and worker-independent checks; cancellation/release also consume these pins. | Extend the same PO principal participant evidence through Finance confirmation/release, issued version and dispatch evidence. Existing commitment-owner/manual-policy provider and actual delivery evidence remain required; a principal migration does not confirm a financial commitment or send an order. |
| PO immutable persistence | Domain `PurchaseOrderCommercialEvidence.cs`: FinanceEvidence, Submission, AssignmentAuthorityEvidence, IssuedVersion, DeliveryEvidence. Store `ProcurementPurchaseOrderExecutionMapping.cs`: `GuardPurchaseOrderExecution` and assignment guards | Non-null worker fields and worker JSON/hash; several records have no explicit identity discriminator today. | Add explicit correlated identity version, principal evidence and optional aliases, retaining legacy verification and original hashes. Rehearse existing approved/issued rows unchanged alongside new principal rows. |
| Receiving actors and assignments | HTTP `ProcurementReceivingSourceResolver.cs`: `Actor`; `ProcurementReceivingAssignedReads.cs`; `ProcurementReceivingPolicyConfiguration.cs`; `ProcurementReceivingPolicyLookups.cs` | Canonical worker prerequisite; policy lookup IDs are workers; assignment lists compare inspector/service-owner worker sets. | Separate principal actor projection, same current scope/site checks, named principal policy schema successor and current Core selector. Do not reinterpret worker IDs as principal IDs. Legacy policy schema1 stays read-only/legacy-dispatched. |
| Goods, inspection, service and corrections | Application `ReceivingContracts.cs`, `ControlledReceiving.cs`: `VerifyCurrentActor`, actor checks, `CertificationActor`, history validation; `ReceivingPersistenceProjection.cs`; `ReceivingIssuedAuthority.cs`. Domain `ReceivingEvidence.cs`, `ReceivingPersistenceRecords.cs`; store `ProcurementReceivingMapping.cs` | Required receiver/inspector/service owner/effect actor workers; optional DB worker fields still require workers in verified projections; correction requester/authorizer and immutable acceptance history are worker-bound. | Principal actor fields and versioned source/effect/history contract with nullable aliases. Preserve independent inspector/certifier/correction authority, source ledger ordering, exact capacity and reversal rules. A nullable column alone is insufficient. |
| Downstream invoice consumer | HTTP `ProcurementReceivingInvoiceProjection.cs`, application `ControlledReceiving.cs` and existing invoice exposure/mapping consumers | Builds receiving authority from PO buyer-worker and worker-pinned acceptance facts. | Extend the receiving read projection under its explicit version without declaring qualified matching, Finance exposure, AP or payment ready. P23 original-unit review does not substitute for this source authority. |

## UI contracts to evolve precisely

- `src/platform/procurement-policy-api.ts`: `SourcingReviewerPage` and `reviewerPage` currently
  require `workerId: string`. A new declared backend projection may make it nullable metadata;
  `principalId` remains required and lookup rows never grant authority. Existing sourcing policy
  stages already store `principalIds`, so no second user master is needed.
- `reference-ui/src/lib/receiving-policy-editor.ts` and `LiveReceivingSettings.tsx` deliberately use
  schema1 `inspectorWorkerIds` / `serviceOwnerWorkerIds`; `ReceivingReferencePicker.tsx` and
  `procurement-policy-api.ts` receiving lookup IDs currently mean worker IDs. Add a typed declared
  principal policy schema and selector mode. Unsupported saved versions remain read-only; never
  silently drop or rename historical selections.
- `src/platform/procurement-sourcing-execution-api.ts`, `procurement-order-execution-api.ts` and
  `procurement-receiving-api.ts` currently expose business references/actions/exact values rather
  than worker ownership fields. No blanket nullable-worker parser change is needed there today.
  Change them only for actual additive backend fields or declared wire versions. Preserve command
  keys, receipt/state/CAS/fingerprint checks, actor-owned GET recovery and document/quantity checks.
- Keep receipt wire version independent from internal identity/evidence versions. Do not accept an
  internal successor version as a new response envelope automatically. The server owns capability
  availability; the frontend must not reintroduce employee-presence heuristics or enable missing
  publication, Finance or receiving providers. Shared Core/ERP components remain reused.

## Ordered implementation milestones

1. **Complete separately owned PR/authority bridge.** P25 source must prove current Core principal,
   finite authority2, Finance checker2, original requester provenance and submission3/assignment2.
   Maintain legacy readers and explicit unsupported combinations. Verify approved-demand consumer
   dispatch before downstream conversion accepts new submissions.
2. **RFQ holds / sourcing configuration successor.** Update original-envelope, binding, ownership,
   policy identity and command receipt versions together. Prove principal-only create/read/cancel
   and source quantity reservation/release. Publication preflight remains a preflight; separately
   implement real supplier invitations/bids/evaluation/award when their owning contracts exist.
   Competitive-source PO conversion must still refuse a missing genuine award.
3. **PO convert / approve / issue successor.** Reuse shared demand allocation, Core approval case,
   finite spending evaluator, existing Finance commitment provider and outbox. Migrate buyer,
   requester/maker participants, assigned approvers and Finance/dispatch verifiers as one declared
   evidence family; preserve exact commercial snapshot and revision/release semantics.
4. **Receiving policy / positive receiving successor.** Introduce principal assignment policies and
   immutable goods/inspection/service evidence together with PO-issued source dispatch. Reuse
   existing provider ledger/capacity/effects, not a second inventory ledger. Then extend corrections
   and returns under their real independent-authorizer contract.
5. **Downstream compatibility and whole-flow proof.** Verify invoice projections consume the new
   receiving/issued authority without losing source provenance. Real Finance history/AP, supplier
   portal and inventory/asset effects require their own installed contracts and remain unavailable
   until proved. An actual asset custodian/employee reference may still be required for an explicitly
   employee-specific assignment; it must not become a general Procurement actor requirement.

Root assigns each genuine schema successor only after predecessor snapshots are frozen. No single
combined migration from every future model; no manual designer/snapshot edits, broad backfill,
reusing a retained legacy version number or authorizing old evidence as a new contract.

## Required negative and recovery proof

- Actual Core-only principals with finite current grants and authorized branch complete each
  enabled positive operation; inactive/archived/locked accounts or removed action grants fail even
  with stale role/action headers. Same-company and cross-tenant/branch/object URL isolation hold.
- Self approval is denied by immutable principal even without employees. Distinct principals with
  null/null metadata are not aliases. A known nonzero shared Core worker link remains a veto even
  when HR is inactive, archived, missing or moved; removing/rebinding HR does not transfer history.
- Preserve legacy fingerprints/JSON/receipts/catalog/data and strict legacy positive/negative
  behavior. Mixed/missing/forged successor versions, altered source principals/alias pins, detached
  owner changes and current-account rebinding cannot recast a legacy record as principal evidence.
- Full finite grant family is evaluated for overlap, amount/category/currency/branch, effectiveness
  and delegation; no role grants an unlimited spending limit. Current authority is revalidated at
  decision; retained historical authority remains immutable evidence, not a fresh grant.
- Actual PostgreSQL concurrent PR/RFQ/PO allocation and source modification races retain original
  transactions/owner locks. Stale CAS, duplicate keys and interrupted acknowledgments create no
  second reservation, Core vote, issue effect, receipt or acceptance. Storage failure prevents
  browser POST; unknown outcomes remain blocked through reload/account/scope changes.
- Receiving verifies exact issued revision, branch/site, quantity/value remaining, inspection and
  service assignments; unauthorized correction/reversal changes no accepted balance or history.
- Genuine provider migration tests include prior-used clone Up twice, fresh Up twice, unused
  Down/Up, used-evidence Down veto and all-schema/data/privilege preservation. Runtime finite-role
  tests and actual shared-UI Playwright are required; owner probes are not finite-role proof.

No SDK, Node, browser, migration generation, provider command or feature activation was run for
this audit. Paths and proposed contracts describe remaining work, not completed functionality.
