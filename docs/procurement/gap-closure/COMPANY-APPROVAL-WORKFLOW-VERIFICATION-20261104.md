# Company approval workflow verification — 4 November 2026

**Verdict: partially implemented. The shared approval engine works; the full configurable company process is not currently available end to end.** This corrects any interpretation of the earlier architecture description as an already-finished Core workflow configuration feature. The owner launch requires implementation as well as real business configuration and acceptance.

## Source checkpoint after this audit

M1 source `dd7e96b4cfe98051194e3c5709f87b10cd67aa77` implements the read-only Core workflow settings registry/entry, using module-owned configuration links. [Exact M1 verification](../../core/verification/CORE-WF-01-M1-20261104.md): 45 selected backend cases,7 contract checks,6 mocked-API Chromium cases and TypeScript pass. This is not deployed and does not implement named approver/requester routing or PO advances. The findings below describe the pre-M1 baseline; the remaining business workflow verdict is unchanged.

## Company process to support

1. A regular requester submits a Purchase Request; a supervisor reviews it, then the Head of Operations / Chief Engineer / CEO provides the required executive authority before Procurement begins.
2. When a supervisor submits the request, the supervisor must not approve their own request. The configured route must retain executive authorisation without demanding self-approval.
3. Procurement carries out the authorised buying process and prepares the order.
4. A separate Payment Request routes to Accounts for approval before payment. Timing may be an approved advance or payment after delivery/service acceptance.

These are linked record workflows: Purchase Request, Purchase Order and Payment Request, not PDF/template workflows. Whether Head of Operations and Chief Engineer are two separate approvers, and whether CEO/Director are multiple titles of the same person, must be confirmed using actual Core identities. Titles alone must not manufacture extra approvals or access. No names, thresholds or approval policy have been invented.

## Findings

| Area | Verified implementation | Gap for this company |
| --- | --- | --- |
| Shared Core engine | `ApprovalEngine` supports ordered Active/Waiting stages, Any/All/Quorum, assigned actors, approve/return/reject, scope, reasons and maker self-approval denial | Keep/reuse this engine; do not build a second engine |
| Core configuration entry | Core settings contains organisation/access/default/document settings. The generic settings route returns Setting not found for unregistered slugs | No Core Module → Document Type → Trigger → Conditions → Steps workflow setup screen/registry is present |
| Purchase Request routing | Buying rules carry `approvalStages`; backend accepts only LineManager, BudgetOwner and ProcurementManager, with One/All. UI emits the fixed NEED/FUNDING/BUYING combination | No explicit named executive/supervisor or arbitrary Core-role binding per stage; no editable ordered generic stage list |
| Requester-dependent routing | Rule conditions cover amount/category/purchase type/branch. Core engine can represent a supervisor's request going to an executive directly | Current editor/schema cannot express different routes based on requester role, needed for supervisor-submitted requests |
| Principal/manager resolution | Principal-based approval exists. Only the LineManager strategy requires an active HR manager link; ProcurementManager resolves eligible current Core principals with spending authority | Do not require employment generally. Add an explicit Core-role/person strategy for supervisor/executive routing |
| Budget-owner option | Buying-rule UI and validator offer BudgetOwner | Principal resolver explicitly rejects it with BudgetOwnerMappingRequired; this offered option is not a completed route |
| Executive authority | Core principals can hold current scoped permission/financial authority | A CEO could be the sole eligible person behind the existing ProcurementManager strategy, but that is not a reliable separate CEO stage. Multiple eligible people make One ambiguous; relabelling jobs or choosing All is not a proper substitute |
| Accounts approval | Invoice-backed manual payment supports separate request, assigned approval, external payment record and independent confirmation over Core cases | This is a bounded invoice-payment flow, not a general arbitrary multi-stage payment workflow editor |
| Advance payments | Manual payment draft requires a real supplier invoice; eligibility is not itself bank execution or Finance posting | No general PO/request-backed advance-payment source without an invoice. An invoice before delivery might fit the existing invoice path, but that journey was not tested here. Do not fabricate an invoice to emulate an advance |
| Runtime activation | Fresh production inspection: approvals=false, principal execution=true, direct orders=false, manual payments=false; zero operational policies and spending-authority grants | Activation and real owner-account UAT remain required after configuration gaps are fixed |

## Source trace

- Shared engine and persistence: `backend/core-api/src/Mightyfin.Erp.Core.Approvals.Domain/ApprovalEngine.cs` and `...Infrastructure/ApprovalDbContext.cs`. Core stores cases, stages, assignments, decisions and delegations; module-owned policies provide business routing.
- Core directory and route: `modules/hrm/frontend/module-connect/src/modules/core/settings.ts` and `src/routes/core.workspace.settings.$slug.tsx` within that frontend. The older HRM `WorkflowServiceImpl` is a separate HR-specific service and does not configure Procurement's Core approval cases.
- Buying-rule definition/UI: `backend/procurement-api/src/Mightyfin.Erp.Procurement.Application/PolicyRuleValidator.cs`; UI `reference-ui/src/features/LivePolicies.tsx` and `LiveApprovalSettings.tsx`. The settings editor separates named spending limits from routing rules.
- Real request execution: `ProcurementApprovalExecutionRoutes.cs` builds Core stages from the pinned policy; `ProcurementPrincipalApprovalAssignees.cs` resolves them. This audit follows the principal successor, not the retained legacy employee-only resolver.
- Payments: `ManualPaymentCoreWorkflow.cs`, `ManualPaymentWorkflow.cs` and `ProcurementManualPaymentInvoiceReads.cs` enforce the invoice source and current assigned Core users. A recorded external payment is not a bank transfer or accounting posting.
- Purchase Plan Review has its own two-stage named-person configuration. A working annual Purchase Plan workflow does not establish the same capability for Purchase Requests.

## Actual verification and limits

- **32 UI tests passed** across approval settings, policy dispatch, manual-payment policy editing and request-approval availability.
- **71 existing backend tests passed** with exact stable IDs/methods retained from the accepted baseline; the entire118-file runtime was checked byte-for-byte before and after. No application or migration was rebuilt.
- **10 targeted checks passed** against those existing compiled domain/application assemblies: ordered supervisor/executive decisions, early-executive denial, maker self-approval denial, supervisor submission sent directly to an executive, accepted predefined assignment, rejected CEO assignment type, rejected named-person rule field and invoice-less payment rejection.
- The targeted probe constructs isolated in-memory approval cases; it does not create real Core people, grant authority, publish a company policy or execute the production business process. A successful probe means the existing capabilities AND limitations were confirmed, not that the desired workflow is complete.
- Runtime source is9fecef1732b7629f5ac8dbd005c863d20898a250. The four directly probed/audited engine/rule/resolver/payment source files are unchanged from deployed backend sourcec93e8df4. The later PR187 readiness change is not covered by these tests.
- Probe compile/launcher failures in the first two attempts remain retained separately: a probe-only syntax error, followed by an unquoted test-filter pipe after the targeted checks had passed. The corrected third run independently passed the full selected set. Production was unchanged throughout.

Private combined verification SHA256 `08feca099a1df92c5ce797b3c2c39e2be68b6a2881d1a1b1692bcbd7e323bc48`; TRX `5ec624e42364de2d89e0884712dfaf6939d947ada3ea0532d49adcb6dd1ad34f`; targeted result `9a528d7fc117aec0da33bca2b889d20a7e7fad2d0a6938d383bb71901fb20f3f`. Evidence remains under the dated `procurement-company-workflow-verification` directories. The owned test containers stopped without OOM; no database, business policy, production flag or permission was changed.

## Required next implementation

1. Add the shared Core configuration entry/registration for module, business record, trigger and versioned approval steps, reusing module-owned rule adapters and existing Core cases. A central configuration surface does not move Procurement business invariants into Core or require an arbitrary low-code platform.
2. Extend/version Purchase Request routing to explicit permitted Core people/roles and requester-dependent steps. Resolve actual current Core permissions, company/branch scope and authority at submission and decision; never derive authority from a job title alone. Preserve existing policy versions and independent approval. Reject or clearly disable unsupported settings until their adapters exist.
3. Retain the invoice-backed Accounts flow and implement a separately governed PO/request-linked advance path if required: explicit source/value limits, approval before external payment recording, duplicate/capacity checks and later invoice settlement without invented Finance facts.
4. Publish the real company policy and test regular requester → supervisor → executive → Procurement; supervisor → executive → Procurement; and Accounts approval for each supported payment timing in the original UI with real Core cookies and native reconciliation. Include wrong-role/branch, self-approval, changed authority, duplicate, returned request and uncertain-result recovery cases.

Do not enable production purchasing or call this a configuration-only task before these gaps are closed. Preserve the existing imported UI/shared components. Parent190 minimum owner launch and145 whole-module acceptance remain open.
