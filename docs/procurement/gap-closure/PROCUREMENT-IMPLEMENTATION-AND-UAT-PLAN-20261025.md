# Procurement gap plan with supplied UI interaction trace

Execution update: use the [ordered finish milestones](PROCUREMENT-FINISH-MILESTONES-20261025.md) and the JSON `executionRoadmap` for current dependencies, R0 schema promotion and completion checks. M0/M2 remain partially delivered; no UAT status changes are made.

Implementation update: CORE-04 and AUD-04 have [verified source changes](../../core/CORE-ROLE-PERMISSION-CATALOG-SOURCE-20261025.md). Their compatible Core release is [deployed](../../core/CORE-ROLE-CATALOG-RELEASE-20261025.md); authenticated business UAT remains pending and the original UAT register is unchanged. Core settings navigation is deployed; HRM module settings-route alignment remains separate.

ERP source `c38202e57afff83abbcdcbb05f95a27d38a8be26`; supplied UI `c370275e9242f447cade47bec31c2fb42aa75637`. Exactly 55 audit findings. Preview interactions are **not** ERP authority.

Each finding separates the original presentation intent, current live component, backend state, and the next implement/integrate/test/deploy/decision slice. `NOT_PRESENT` is explicit where the inspected original preview has no corresponding action.

## BE-01 — bulk/import/export/clone/delete/custom views (IMPLEMENT)

- Implementation checkpoint: [Vendor and Item scoped CSV verification](../release/PROC-SCOPED-CSV-SOURCE-VERIFICATION-20261025.md). Backend 76 focused and 1,378 host tests pass. Real-cookie API, two actual live UI journeys, independent database/audit reconciliation and same-session permission revocation pass. The [P24-compatible slice is deployed](../release/P24-SCOPED-CSV-RELEASE-20261025.md), with 92 focused/1,057 compatible Host tests and three private real-browser cases. This is one slice; imports, selected export, clone/bulk and saved views are still open.
- Original UI (PREVIEW_ONLY): `reference-ui/src/features/RecordList.tsx:179` — Export current/selected view. Uses locally filtered AppRecord rows and browser CSV; local delete at line 224 leaves linked records unchanged.
- Original flow detail: `reference-ui/src/features/RecordList.tsx:224` — Delete operates on local AppRecord and leaves linked records unchanged.
- Original flow detail: `reference-ui/src/features/Catalogs.tsx:435` — Vendor/item import maps rows into local demo records.
- Current live counterpart: `reference-ui/src/components/data/ListTools.tsx:114` on `/vendors, /items, /requests, /purchase-orders, /purchase-receives`. List search/sort/filter affordances are present; no bulk/import/export command follows from them.
- Backend: Supplier/item/request/order/receive draft readers and scoped single-record commands exist; a reusable rendered toolbar is not a server-backed batch/import/export. Gap: No named import/clone/bulk mutation/export/custom-view routes in inspected item, PR, PO, receive and vendor route maps; preserved ERP records prefer cancellation or retirement over hard delete.
- Next slice (Procurement UI/API): Define typed scoped export and one bounded batch/import command per approved entity; preserve cancellation history instead of hard delete.
- Prerequisites: None hard-linked in this audit.
- Tests: Authorized A+B branch export returns only A+B before count/page; malformed row, duplicate, stale version, and partial batch recovery.
- Acceptance: Typed scoped import/export, stable field mapping, limited batch commands and private end-to-end tests; irreversible deletion policy decided explicitly.
- Source: `backend/hrm-api/src/Mightyfin.Erp.Hrm.Api/ProcurementItemEndpoints.cs:18`, `backend/hrm-api/src/Mightyfin.Erp.Hrm.Api/ProcurementItemEndpoints.cs:168`, `backend/hrm-api/src/Mightyfin.Erp.Hrm.Api/ProcurementItemEndpoints.cs:267`

## BE-02 — module preferences and user-configurable fields (DECIDE)

- Original UI (PREVIEW_ONLY): `reference-ui/src/features/Settings.tsx:989` — Module preferences. Preview toggle/terms layout; selected values do not publish a typed ERP policy.
- Current live counterpart: `reference-ui/src/features/LiveConfiguration.tsx:135` on `/settings, /items`. Existing configuration and item fields are typed; the screen is not an arbitrary field designer.
- Backend: Typed fields, fixed exact-amount codecs, category/method policy versions and selected module editors exist; this is narrower than arbitrary metadata/preferences. Gap: Manual quantity precision, duplicate names, numbering, arbitrary status, custom fields/rules/actions and template preferences are broader than typed ERP policy/version records.
- Next slice (Procurement policy/Core): Select individual requested numbering, precision, duplicate-name and template preferences; give each a versioned policy and owner before UI binding.
- Prerequisites: None hard-linked in this audit.
- Tests: Policy version, invalid preference, rollback, owner permission and unchanged historical document tests.
- Acceptance: Published typed policies with owner approval, schema/validation/migration and UI evidence for chosen preferences.
- Source: `backend/hrm-api/src/Mightyfin.Erp.Hrm.Api/ProcurementItemEndpoints.cs:18`, `backend/hrm-api/src/Mightyfin.Erp.Hrm.Api/ProcurementItemEndpoints.cs:168`, `backend/hrm-api/src/Mightyfin.Erp.Hrm.Api/ProcurementItemEndpoints.cs:267`

## BE-03 — PR recall/forward/hold/processed/reapprove (IMPLEMENT)

- Original UI (PREVIEW_ONLY): `reference-ui/src/features/RecordDetail.tsx:340` — Approve (demo) / Reject (demo). changeStatus updates local AppRecord.status and lastLocalAction; no Core case transition.
- Current live counterpart: `reference-ui/src/features/LiveRequests.tsx:327-413` and `LiveApprovals.tsx:156-225` on `/requests, /approvals`. Pre-case Withdraw, Return and resubmit are source/UI present. The original demo action is not a Core transition.
- Backend: `ProcurementRequestRoutes.cs:187-370` implements pre-case actions; `ApprovalEngine.cs:49-91` supplies Core cancellation, but no Procurement active-case recall route or `Cancelled` outcome/release path exists. The current outcome CHECK and Finance release path do not support it.
- Next slice (Core approval + Procurement + Finance): Use the default-off [28-section active-case recall draft](../PROC-03-PR-ACTIVE-CASE-RECALL.md): current maker, pending case before any decision/downstream use, source/version/key locks, Core Cancel plus typed Procurement outcome and durable release request, then independent Finance release before edit/resubmit. Forward, Hold and manual Processed remain separate decisions.
- Prerequisites: Resolve the named Core permission/assignment and Finance release contracts; generate a genuine migration after P38, with populated-history Down veto. Do not edit historical migrations or infer release from Core cancellation.
- Tests: Existing pre-case subset; active-case eligible/denied, revoked scope, stale CAS, duplicate/lost response, decision race, retained case/source/budget effects, Finance outage and independent confirmation, UI server availability, owner no-effects checks and schema rollback.
- Acceptance: No active-case recall or UAT claim until API, UI, Core, Finance and physical schema evidence is bound to one source/build; `NOT_EXECUTED` remains the UAT status.
- Source: `backend/hrm-api/src/Mightyfin.Erp.Hrm.Api/ProcurementRequestRoutes.cs:187-370`, `backend/core-api/src/Mightyfin.Erp.Core.Approvals.Domain/ApprovalEngine.cs:49-91`, `backend/procurement-api/src/Mightyfin.Erp.Procurement.Infrastructure/Data/ProcurementApprovalOutcomeMapping.cs:31-65`

## BE-04 — PR attachments and scanner truth (INTEGRATE)

- Original UI (NOT_PRESENT): `reference-ui/src/features/RfqWizard.tsx:702` — Local document filenames. Nearby RFQ document step explicitly keeps filenames in the browser; request attachment proof is absent.
- Current live counterpart: `reference-ui/src/features/LiveRequests.tsx:132` on `/requests/new, /requests/:id`. Request editor exists; an attachment control cannot assert a Clean scan.
- Backend: Request lines and associated data have typed routes; generic document storage alone is not a request-specific upload/view/retirement workflow. Gap: PR typed source has fields/lines; the manual’s upload, preview and delete interaction is not established by PR routes.
- Next slice (Core Documents + Procurement): Connect the request to Core Documents upload/use with genuine scanner availability and purpose-scoped document evidence.
- Prerequisites: EXTERNAL:Core Documents genuine scanner
- Tests: Pending/infected/stale upload cannot submit; genuine Clean original and independent reviewer use are retained.
- Acceptance: Real scanner Clean/Available and authorized document-use API plus before/after owner proof.
- Source: `backend/hrm-api/src/Mightyfin.Erp.Hrm.Api/ProcurementRequestRoutes.cs:20`, `backend/hrm-api/src/Mightyfin.Erp.Hrm.Api/ProcurementRequestRoutes.cs:35`, `backend/hrm-api/src/Mightyfin.Erp.Hrm.Api/ProcurementRequestRoutes.cs:65`

## BE-05 — PDF/print/share/comments/notifications (IMPLEMENT)

- Original UI (PREVIEW_ONLY): `reference-ui/src/features/RecordDetail.tsx:222` — Comments tab. saveComment appends JSON into local record.meta and says saved in this browser only.
- Current live counterpart: `reference-ui/src/features/RecordDetail.tsx:125` on `/requests/:id, /inbox`. Record detail and inbox exist; print/share/comments/notification are not established by audit/outbox alone.
- Backend: Transaction audit and outbox records exist; contextual comments, business document rendering and delivery require separate adapters. Gap: Inspected module routes lack named PDF/export/comment/email operations and retained template/PII rules for the manual flows.
- Next slice (Procurement + Core documents): Choose a scoped PDF and notification template, then add authorized comment/share actions if in launch scope.
- Prerequisites: None hard-linked in this audit.
- Tests: PDF data scope/redaction, recipient authorization, duplicate outbox retry and comment actor/time audit.
- Acceptance: Scoped rendered artifact or notification, permissions, no-leak tests, retained audit and delivery receipts.
- Source: `backend/hrm-api/src/Mightyfin.Erp.Hrm.Api/ProcurementOrderDraftRoutes.cs:15`, `backend/hrm-api/src/Mightyfin.Erp.Hrm.Api/ProcurementPrincipalOrderDraftRoutes.cs:43`, `backend/hrm-api/src/Mightyfin.Erp.Hrm.Api/ProcurementPrincipalOrderDraftRoutes.cs:86`

## BE-06 — bulk PO approval/configurable presets (DECIDE)

- Original UI (NOT_PRESENT): `reference-ui/src/features/RecordDetail.tsx:340` — Approve (demo). Single local status toggle; no multi-order approval preset or server batch.
- Current live counterpart: `reference-ui/src/features/LiveOrderApprovalQueue.tsx:20` on `/purchase-orders, /approvals`. Order approval queue exists; bulk presets and multi-order decisions need explicit authorization.
- Backend: Direct-order review uses current authority and shared Core cases; that does not implement every bulk or selectable approval preset. Gap: Principal PO Core review exists; bulk submit/decision, arbitrary Simple/Multi-Level/Custom presets and email notification are not established.
- Next slice (Core approval + Procurement): Approve a bounded bulk decision policy only after per-order source/current Core grants can be checked independently.
- Prerequisites: None hard-linked in this audit.
- Tests: Mixed branch/status/creator batch denies unauthorized rows without partial silent approval; replay receipts.
- Acceptance: Bounded batch atomicity and policy-stage authority tests; notification owner proof if enabled.
- Source: `backend/hrm-api/src/Mightyfin.Erp.Hrm.Api/ProcurementOrderDraftRoutes.cs:15`, `backend/hrm-api/src/Mightyfin.Erp.Hrm.Api/ProcurementPrincipalOrderDraftRoutes.cs:43`, `backend/hrm-api/src/Mightyfin.Erp.Hrm.Api/ProcurementPrincipalOrderDraftRoutes.cs:86`

## BE-07 — partial PO line cancellation/reopen (IMPLEMENT)

- Original UI (NOT_PRESENT): `reference-ui/src/features/RecordDetail.tsx:210` — Order detail tabs. Generic overview/items/related/comments/history; no issued-line cancellation/reopen control.
- Current live counterpart: `reference-ui/src/features/LivePrincipalOrderDetail.tsx:137` on `/purchase-orders/:id`. Order detail/revision controls do not establish issued-line cancellation and reopening.
- Backend: Controlled direct-order edits, cancellation and source release exist; an issued order needs separately accepted partial cancellation/reopening rules. Gap: Current direct edit/cancel/release commands do not establish post-issue unbilled line cancellation or reopening with downstream receipt/bill conservation.
- Next slice (Procurement + Finance): Add retained line cancellation/reopen with receiving and Finance capacity checks and compensating facts.
- Prerequisites: None hard-linked in this audit.
- Tests: Partially received/billed line, overcancel, duplicate cancel, stale version and reopened capacity.
- Acceptance: Source/receipt/bill capacity and history proof including concurrent partials.
- Source: `backend/hrm-api/src/Mightyfin.Erp.Hrm.Api/ProcurementOrderDraftRoutes.cs:15`, `backend/hrm-api/src/Mightyfin.Erp.Hrm.Api/ProcurementPrincipalOrderDraftRoutes.cs:43`, `backend/hrm-api/src/Mightyfin.Erp.Hrm.Api/ProcurementPrincipalOrderDraftRoutes.cs:86`

## BE-08 — PO-to-bill and bill-controlled lifecycle (INTEGRATE)

- Original UI (PREVIEW_ONLY): `reference-ui/src/features/RecordDetail.tsx:251` — Create bill (demo) from receive. nextPath creates a link with source ID; the target TransactionEditor saves local AppRecord, not a typed invoice source bridge.
- Original flow detail: `reference-ui/src/features/RecordDetail.tsx:251` — Receive detail chooses a source-linked bill route.
- Original flow detail: `reference-ui/src/features/TransactionEditor.tsx:385` — Bill editor saves local AppRecord.
- Current live counterpart: `reference-ui/src/features/LiveInvoices.tsx:109` on `/purchase-orders/:id, /bills/new`. Bill draft editor exists but direct eligible PO/receive-to-typed-bill bridge is not accepted.
- Backend: Procurement invoice intake and retained source/comparison groundwork exist. A one-click eligible order/receive-to-invoice flow is not fully integrated; Finance AP posting is a separate boundary. Gap: A direct PO→typed Procurement invoice Draft/link/source/capture action and Billed/Partially Billed receive projection are not established. P31 invoice source/comparison exists but genuine scanned invoice evidence remains gated; later Finance AP posting is a separate owner, not an excuse to omit Procurement Draft CRUD.
- Next slice (Procurement invoice intake/source; Finance owns later AP posting): Add Procurement-owned PO/receipt source link and invoice Draft capture; keep AP posting with Finance and document scan truthful.
- Prerequisites: BE-04, PAY-02
- Tests: Partial receipt to bill line/quantity match, duplicate source, wrong supplier/currency/branch and genuine scanned original.
- Acceptance: Producer-backed PO→invoice Draft/source link and scan-backed invoice evidence with exact line/quantity reconciliation; then separate Finance posting acceptance.
- Source: `backend/hrm-api/src/Mightyfin.Erp.Hrm.Api/ProcurementOrderDraftRoutes.cs:15`, `backend/hrm-api/src/Mightyfin.Erp.Hrm.Api/ProcurementPrincipalOrderDraftRoutes.cs:43`, `backend/hrm-api/src/Mightyfin.Erp.Hrm.Api/ProcurementPrincipalOrderDraftRoutes.cs:86`

## BE-09 — awarded RFQ/PR line to PO conversion (IMPLEMENT)

- Original UI (PREVIEW_ONLY): `reference-ui/src/features/RecordDetail.tsx:251` — Create purchase order (demo) from request. nextPath links an approved request to local PO editor; there is no award-to-PO conversion.
- Original flow detail: `reference-ui/src/features/RecordDetail.tsx:251` — Approved PR links to local PO Draft, not award.
- Current live counterpart: `reference-ui/src/features/LiveRfqRecommendations.tsx:50` on `/rfqs/recommendations/:roundId, /purchase-orders/new`. Human recommendation Draft and PO Draft screens exist; no accepted award-to-PO action connects them.
- Backend: Approved demand can feed bounded direct-order and sourcing source workflows; source quantities are controlled. Full cross-request bulk selection and award-to-order adoption remain unproven. Gap: Approved PR source conversion is bounded; P38 award-to-PO adapter is absent and manual cross-request bulk selection is unproven.
- Next slice (Procurement): Complete P38 retained award and held-capacity adapter, then convert approved award lines into a governed PO Draft.
- Prerequisites: BE-12
- Tests: Split award conservation, duplicate conversion, stale/returned Core case, revoked grants and source digest equality.
- Acceptance: Line/source/quantity conservation and original-key recovery after an approved P38 outcome; private owner and browser proof.
- Source: `backend/hrm-api/src/Mightyfin.Erp.Hrm.Api/ProcurementOrderDraftRoutes.cs:15`, `backend/hrm-api/src/Mightyfin.Erp.Hrm.Api/ProcurementPrincipalOrderDraftRoutes.cs:43`, `backend/hrm-api/src/Mightyfin.Erp.Hrm.Api/ProcurementPrincipalOrderDraftRoutes.cs:86`

## BE-10 — receive transit/bill lifecycle and import (IMPLEMENT)

- Original UI (PREVIEW_ONLY): `reference-ui/src/features/RecordDetail.tsx:251` — Record receive (demo). PO detail links to local receive editor; no transit, stock or bill ownership.
- Current live counterpart: `reference-ui/src/features/LiveReceiving.tsx:59` on `/purchase-receives, /purchase-receives/returns`. Goods/service receiving and return Draft screens exist; transit/billing/import effects are distinct.
- Backend: Principal goods/service receiving, inspection and partial acceptance have bounded private evidence; transit/billing/import are distinct operations. Gap: Received/recorded/inspected/accepted paths are private proven, but transit toggle, Billed/Partially Billed and CSV receive import are not established.
- Next slice (Procurement + Inventory + Finance): Add typed transit and bill projection only from accepted receiving facts; define import separately.
- Prerequisites: None hard-linked in this audit.
- Tests: Partial acceptance, discrepancy, return, late invoice, cross-branch and no stock/Finance effect before owner adapter.
- Acceptance: Typed status transitions and external stock/AP ownership with exact partial quantity proof.
- Source: `backend/hrm-api/src/Mightyfin.Erp.Hrm.Api/ProcurementReceivingRoutes.cs:32`, `backend/hrm-api/src/Mightyfin.Erp.Hrm.Api/ProcurementPrincipalReceivingRoutes.cs:23`

## BE-11 — RFQ recall/resubmit/policy variants (DECIDE)

- Original UI (PREVIEW_ONLY): `reference-ui/src/features/RfqWizard.tsx:796` — Bidding Preferences step. Local wizard stores bid format/dates; no verified recall/resubmit or Core policy variant.
- Current live counterpart: `reference-ui/src/features/LiveSourcing.tsx:60` on `/rfqs, /approvals`. Sourcing publication/review UI exists; recall/resubmit and policy variants need specified transitions.
- Backend: RFQ draft, submission and shared publication review exist; not every recall, published-deadline extension or selectable review variant has equivalent acceptance. Gap: Core publication review exists but manual-style recall/reapprove and user-selected approver variants are not established.
- Next slice (Core approval + Procurement): Define permitted RFQ reversal and policy-variant matrix, then reuse Core case engine and immutable publication source.
- Prerequisites: None hard-linked in this audit.
- Tests: Recall before/after open, reviewer self-decision denial, resubmit history and original offer preservation.
- Acceptance: Retained source/case cancellation and new-case tests with no invitation leakage.
- Source: `backend/hrm-api/src/Mightyfin.Erp.Hrm.Api/ProcurementSourcingExecutionRoutes.cs:30`, `backend/hrm-api/src/Mightyfin.Erp.Hrm.Api/ProcurementPrincipalSourcingPublicationRoutes.cs:25`, `backend/hrm-api/src/Mightyfin.Erp.Hrm.Api/ProcurementRfqCommercialRoundRoutes.cs:10`

## BE-12 — RFQ supplier award (IMPLEMENT)

- Original UI (NOT_PRESENT): `reference-ui/src/features/RfqWizard.tsx:843` — Awarding date. Explicit hint says award actions are not simulated; no compare/split/winner button in inspected wizard.
- Original flow detail: `reference-ui/src/features/RfqWizard.tsx:844` — Award date hint expressly says award actions are not simulated.
- Original flow detail: `reference-ui/src/features/RfqWizard.tsx:325` — RFQ wizard writes its draft to local AppRecord store.
- Current live counterpart: `reference-ui/src/features/LiveRfqRecommendations.tsx:50` on `/rfqs/recommendations/:roundId`. Recommendation is a proposal, not supplier award; P38 mapped inserts remain disabled.
- Backend: P38 award foundation has genuine schema and rollback evidence, while insertion and the full commercial award workflow deliberately remain unavailable. Gap: P38 has genuine source/model/schema and private effect-veto proof but mapped award inserts deny; no live award draft/submit/approve/publish or PO output.
- Next slice (Procurement + Core approval): Build authoritative P32/P36 retained-source and residual-hold adapter; enable P38 only after Core award case and finite authority prove out.
- Prerequisites: BE-13, EXTERNAL:P38 authoritative retained source and held-capacity loader
- Tests: Per-line supplier/quantity/price source, split residual conservation, Core approval/return, duplicate award, private owner and browser.
- Acceptance: Retained P32/P36 source and residual-held-capacity loader, Core policy/role grants, populated veto, native/owner/UI proof.
- Source: `backend/hrm-api/src/Mightyfin.Erp.Hrm.Api/ProcurementHostIntegration.cs:143`, `docs/procurement/release/P38-FOUNDATION-SCHEMA-ACCEPTANCE-20261025.json:1`, `backend/procurement-api/src/Mightyfin.Erp.Procurement.Infrastructure/Data/ProcurementRfqAwardReviewMapping.cs:202`

## BE-13 — supplier offer original evidence (INTEGRATE)

- Original UI (PREVIEW_ONLY): `reference-ui/src/features/RfqWizard.tsx:702` — RFQ documents. Selected filenames remain local to browser; cannot supply a scanned original offer.
- Original flow detail: `reference-ui/src/features/RfqWizard.tsx:702` — Document filenames stay in browser; no original scan authority.
- Current live counterpart: `reference-ui/src/features/LiveSourcing.tsx:60` on `/rfqs/:id`. Commercial Draft/Prepare exists; offer Submit requires genuine Available original document.
- Backend: P32 commercial Draft/Prepare has bounded private acceptance; original-document offer submission is gated on genuine available scanned evidence. Gap: Zero-document commercial prepare is accepted, but SubmitOffer requires real Available original document; scanner C04 remains external.
- Next slice (Core Documents + Procurement): Close Core scanner C04 and original-use proof for offered documents; leave zero-document Prepare separate.
- Prerequisites: EXTERNAL:Core Documents C04
- Tests: Pending/Infected/expired original denied; genuine Clean original sealed once, source and actor retained.
- Acceptance: Actual Clean scan and sealed original document-use/submission/reload owner proof.
- Source: `backend/hrm-api/src/Mightyfin.Erp.Hrm.Api/ProcurementRfqCommercialRoundRoutes.cs:19`, `backend/hrm-api/src/Mightyfin.Erp.Hrm.Api/ProcurementRfqCommercialRoundOffer.cs:137`

## BE-14 — external supplier bidding/portal (DECIDE)

- Original UI (PREVIEW_ONLY): `reference-ui/src/features/RfqWizard.tsx:816` — Open/Invited/Sealed bid selector. Changes local draft biddingType only; no authenticated supplier session or bid submission.
- Current live counterpart: `reference-ui/src/features/LiveSourcing.tsx:60` on `/rfqs/:id`. Buyer/surrogate controls are not supplier-authenticated bidding.
- Backend: Internal buyer/surrogate and publication groundwork exists; it does not authenticate an external supplier or prove supplier portal bidding. Gap: Publication and internal surrogate quote are bounded; no supplier portal account, invitation acceptance, authentic supplier bid/award notification lifecycle.
- Next slice (Supplier identity + Procurement): If external portal is launch scope, design supplier identity, invitation acceptance and own-offer/document authority.
- Prerequisites: DECISION:supplier portal launch scope
- Tests: Supplier A cannot see B invitation/bid; revoked invite and expired session deny; buyer surrogate remains attributed.
- Acceptance: Distinct supplier auth/session/branch boundary, original bid/document authority, sealed access and browser owner proof.
- Source: `backend/hrm-api/src/Mightyfin.Erp.Hrm.Api/ProcurementSourcingExecutionRoutes.cs:30`, `backend/hrm-api/src/Mightyfin.Erp.Hrm.Api/ProcurementPrincipalSourcingPublicationRoutes.cs:25`, `backend/hrm-api/src/Mightyfin.Erp.Hrm.Api/ProcurementRfqCommercialRoundRoutes.cs:10`

## BE-15 — RFQ invite/close/award notifications (IMPLEMENT)

- Original UI (PREVIEW_ONLY): `reference-ui/src/features/Settings.tsx:1426` — Email template preview. Screen states sending email is not connected; no delivery receipt.
- Current live counterpart: `reference-ui/src/features/PrincipalPublication.tsx:39` on `/rfqs/:id, /inbox`. Publication and inbox are present; delivered supplier notification is not proved by an outbox row.
- Backend: Stored invitation/manual-contact/outbox facts exist; actual supplier message delivery and reminder/outcome preferences need their own proof. Gap: Internal manual-contact/outbox fact is not proof of vendor email delivery, reminders, expiry or award notification preferences.
- Next slice (Outbox/communications): Add bounded invite/close/award message templates and delivery receipts after recipient authority is defined.
- Prerequisites: None hard-linked in this audit.
- Tests: Idempotent delivery, withdrawn/returned award suppression, recipient scope and failed transport retry.
- Acceptance: Delivery consumer, templates, idempotent delivery receipts and negative privacy tests.
- Source: `backend/hrm-api/src/Mightyfin.Erp.Hrm.Api/ProcurementSourcingExecutionRoutes.cs:30`, `backend/hrm-api/src/Mightyfin.Erp.Hrm.Api/ProcurementPrincipalSourcingPublicationRoutes.cs:25`, `backend/hrm-api/src/Mightyfin.Erp.Hrm.Api/ProcurementRfqCommercialRoundRoutes.cs:10`

## BE-16 — vendor self-onboarding/bank details (DECIDE)

- Original UI (PREVIEW_ONLY): `reference-ui/src/features/Settings.tsx:960` — Allow vendors to update portal profile. Local settings toggle suggests portal intent; no invite, external identity or bank verification.
- Original flow detail: `reference-ui/src/features/CatalogDetail.tsx:220` — Vendor addresses, contacts and masked bank labels are read from local meta.
- Original flow detail: `reference-ui/src/features/CatalogDetail.tsx:440` — saveBank validates fictional account and stores masked local label only.
- Current live counterpart: `reference-ui/src/features/FullVendorEditor.tsx:71` on `/vendors/new, /vendors/:id`. Full internal vendor editor preserves contact/address tabs; portal request is not onboarding or Finance bank verification.
- Backend: Internal vendor profile/contact/site/qualification CRUD and governed state actions exist; supplier self-service and Finance-verified remittance are separate. Gap: Internal vendor CRUD exists; no invite/sign-up/self-entered tax/bank workflow. Finance retains banking ownership.
- Next slice (Supplier portal + Finance identity): If portal launch is approved, implement external invite/self-entry with Finance-owned remittance verification; retain internal vendor CRUD.
- Prerequisites: DECISION:supplier portal launch scope, EXTERNAL:Finance bank owner
- Tests: Invite token and self-scope, correction handoff, bank data redaction, independent Finance approval.
- Acceptance: External supplier identity, verified invite and document/bank handoff with privacy and SoD proof.
- Source: `backend/hrm-api/src/Mightyfin.Erp.Hrm.Api/ProcurementVendorRoutes.cs:13`, `backend/hrm-api/src/Mightyfin.Erp.Hrm.Api/ProcurementVendorRoutes.cs:18`, `backend/hrm-api/src/Mightyfin.Erp.Hrm.Api/ProcurementVendorRoutes.cs:79`

## BE-17 — configurable vendor approval/portal resubmit (DECIDE)

- Original UI (PREVIEW_ONLY): `reference-ui/src/features/Settings.tsx:1059` — Supplier approval options. Preview strategy fields do not become current Core policy or external resubmit.
- Current live counterpart: `reference-ui/src/features/LiveVendors.tsx:81` on `/vendors/:id, /settings`. Internal return/approve/suspend/reactivate controls exist; configurable approval and portal resubmit remain narrower.
- Backend: Internal supplier submission and reviewer decisions exist; selectable approval variants and portal correction are not full equivalents. Gap: Internal vendor submit/approve/reject is present, but manual approval presets, portal resubmission and notifications are not demonstrated.
- Next slice (Core policy + Procurement): Specify configurable vendor stages and whether external correction can resubmit; reuse Core approval, not a second engine.
- Prerequisites: None hard-linked in this audit.
- Tests: Maker/checker, returned correction, revoked permission, changed policy version and retained history.
- Acceptance: Versioned policy/current assignments and retained resubmission evidence in a private cohort.
- Source: `backend/hrm-api/src/Mightyfin.Erp.Hrm.Api/ProcurementVendorRoutes.cs:13`, `backend/hrm-api/src/Mightyfin.Erp.Hrm.Api/ProcurementVendorRoutes.cs:18`, `backend/hrm-api/src/Mightyfin.Erp.Hrm.Api/ProcurementVendorRoutes.cs:79`

## BE-18 — vendor merge and transaction reparenting (DECIDE)

- Original UI (NOT_PRESENT): `reference-ui/src/features/CatalogDetail.tsx:693` — Merge Vendors · unavailable. Original vendor More menu explicitly disables merge; no reparenting handler.
- Current live counterpart: `reference-ui/src/features/LiveVendors.tsx:81` on `/vendors/:id`. Vendor record view exists; merge/reparent is absent and transactional history must remain immutable.
- Backend: Supplier identities are referenced by retained commercial history. No governed merge/reparent operation was established. Gap: No merge/reparent route; supplier IDs are retained source references across orders, quotes, receipts and contracts.
- Next slice (Procurement master + all consumers): Decide duplicate merge semantics and safe alias/reference behavior for open PO, invoice, credit and contract records.
- Prerequisites: None hard-linked in this audit.
- Tests: Active/open commitments, different currency/tenant, historical source links, double merge and rollback.
- Acceptance: Referential closure, retained history and one transaction across all affected consumers; no silent ID rewrite.
- Source: `backend/hrm-api/src/Mightyfin.Erp.Hrm.Api/ProcurementVendorRoutes.cs:13`, `backend/hrm-api/src/Mightyfin.Erp.Hrm.Api/ProcurementVendorRoutes.cs:18`, `backend/hrm-api/src/Mightyfin.Erp.Hrm.Api/ProcurementVendorRoutes.cs:79`

## BE-19 — contract activation/framework utilisation (IMPLEMENT)

- Original UI (PREVIEW_ONLY): `reference-ui/src/features/Settings.tsx:977` — Contract expiry/limit preferences. Preview toggles do not sign/activate contracts or consume framework ceiling.
- Current live counterpart: `reference-ui/src/features/LiveContracts.tsx:65` on `/contracts/:id, /contracts/internal-review`. Draft/rate editor and internal review exist; approval is not signature or Active authority.
- Backend: Contract draft/rate editing and internal review have real private browser/owner evidence; internal approval is not signature, activation or framework consumption. Gap: Draft/rates and opt-in internal Core review have private proof, but signature/Active, amendments, framework releases and utilisation are absent.
- Next slice (Procurement + Core + Finance): Define signature/activation owner policy, amendment lineage and framework release/ceiling ledger after Core internal review.
- Prerequisites: DECISION:signature and activation authority
- Tests: Unsigned cannot activate, amendment CAS, ceiling exact decimal, duplicate release, expired contract and no Finance claim.
- Acceptance: Explicit owner policy, signed/active transition, finite grants, exact ceiling/PO consumption and normal-cookie owner proof.
- Source: `backend/hrm-api/src/Mightyfin.Erp.Hrm.Api/ProcurementContractDraftRoutes.cs:16`, `backend/hrm-api/src/Mightyfin.Erp.Hrm.Api/ProcurementContractReviewRoutes.cs:27`, `backend/hrm-api/src/Mightyfin.Erp.Hrm.Api/ProcurementHostIntegration.cs:186`

## PAY-01 — Complete bill capture and editing operations (IMPLEMENT)

- Original UI (PREVIEW_ONLY): `reference-ui/src/features/TransactionEditor.tsx:887` — Save Draft / Save and Send. save builds an AppRecord in local store; numbering/clone/import/terms are not server-owned.
- Current live counterpart: `reference-ui/src/features/LiveInvoices.tsx:109` on `/bills, /bills/new`. Single bill Draft create/revise/cancel exists; numbering/import/clone/terms are separate conveniences.
- Backend: Scoped single-bill list/detail/create/revise/cancel; supplier, optional PO, dates, currency and exact lines. Gap: Automatic numbering contract, clone/import/bulk capture, reusable payment terms/AP account classification and full Zoho bill lifecycle are not all exposed.
- Next slice (Procurement; Core numbering/import platform): Complete bounded invoice Draft numbering/terms and selected clone/import through current editor and scoped APIs.
- Prerequisites: None hard-linked in this audit.
- Tests: Create/revise/reload, number collision, invalid terms, cross-supplier clone, malformed import and retry.
- Acceptance: Create/revise/reload a bill in the existing editor; verify duplicate/stale/denied paths, approved numbering and selected clone/import/terms operations without requiring Finance posting.
- Source: `backend/hrm-api/src/Mightyfin.Erp.Hrm.Api/ProcurementInvoiceIntakeRoutes.cs:29`, `backend/hrm-api/src/Mightyfin.Erp.Hrm.Api/ProcurementInvoiceIntakeRoutes.cs:129`, `reference-ui/src/features/LiveInvoices.tsx:530`

## PAY-02 — Positive retained invoice review and PO/receipt matching (INTEGRATE)

- Original UI (PREVIEW_ONLY): `reference-ui/src/features/TransactionEditor.tsx:887` — Save Draft / Save and Send. Original bill editor writes local record, not scanned original plus independent Core review.
- Current live counterpart: `reference-ui/src/features/InvoiceReview.tsx:52` on `/bills/:id, /approvals/unit-review/:id`. Invoice unit review/matching controls exist but positive original-document flow remains scanner dependent.
- Backend: Typed mapping, independent shared Core unit review, compare, discrepancy and correction/remap source; physical schema captured. Gap: Genuine scanned originals plus finite-role positive end-to-end approval/comparison/claims cohort and current UI/API acceptance. No OCR inference.
- Next slice (Procurement invoice workflow; Core Documents/scanner owner): Connect genuine Available Core original to compare, discrepancy, correction and independent review.
- Prerequisites: BE-13, EXTERNAL:Core Documents C04
- Tests: Partial PO/receipt claims, duplicate line, exception return/review, stale source, two actors and scanned original.
- Acceptance: Use genuine currently available originals; create and compare partial PO/receipt claims, resolve exceptions and verify independent review, denied access, stale source and duplicate/retry behavior.
- Source: `backend/hrm-api/src/Mightyfin.Erp.Hrm.Api/ProcurementPrincipalInvoiceMappingRoutes.cs:33`, `backend/hrm-api/src/Mightyfin.Erp.Hrm.Api/ProcurementPrincipalInvoiceStatusRoutes.cs:88`, `backend/hrm-api/src/Mightyfin.Erp.Hrm.Api/ProcurementHostIntegration.cs:363`

## PAY-03 — Authoritative payable balances, paid/overdue/partial states and posting (INTEGRATE)

- Original UI (PREVIEW_ONLY): `reference-ui/src/features/RecordDetail.tsx:274` — Balance due. Derived from local bill/payment records; no Finance AP posting feed.
- Current live counterpart: `reference-ui/src/features/FinanceReleasePanel.tsx:56` on `/bills/:id, /payments`. Draft/payment evidence is shown separately from AP balance truth.
- Backend: Intake and manual evidence are separate; metadata invoiceQualifiedClaims, invoiceValidation and financeHandoff are false. Gap: Owned AP posting/settlement reconciliation and trusted unpaid/paid balance projection; draft statuses cannot substitute.
- Next slice (Finance owns ledger; Procurement owns clear payable projections): Define Finance posting/reconciliation adapter and authoritative paid/partial/overdue projection.
- Prerequisites: EXTERNAL:Finance AP posting adapter
- Tests: Posted/part-paid/reversed callback idempotence; UI must show unavailable until owner feed is accepted.
- Acceptance: Verify an owned Finance adapter or explicit unavailable state, reconcile callbacks idempotently and show exact partial/paid/overdue projections without assigning ledger truth to manual evidence.
- Source: `backend/hrm-api/src/Mightyfin.Erp.Hrm.Api/ProcurementHostIntegration.cs:350`, `backend/hrm-api/src/Mightyfin.Erp.Hrm.Api/ProcurementHostIntegration.cs:364`, `backend/hrm-api/src/Mightyfin.Erp.Hrm.Api/ProcurementManualPaymentRoutes.cs:24`

## PAY-04 — Vendor-level payments, multi-bill allocation, advances and excess/unapplied amounts (IMPLEMENT)

- Original UI (PREVIEW_ONLY): `reference-ui/src/features/TransactionEditor.tsx:624` — Bill to Pay. One select sets draft.billId/source/vendor/amount; save marks one local bill Paid or Partially Paid. No split/advance/excess control.
- Original flow detail: `reference-ui/src/features/TransactionEditor.tsx:293` — Payment validation requires one non-Draft dueBill and caps amount at that bill balance.
- Original flow detail: `reference-ui/src/features/TransactionEditor.tsx:386` — Submitting locally flips only the selected dueBill to Paid/Partially Paid.
- Current live counterpart: `reference-ui/src/features/LivePayments.tsx:93` on `/payments, /payments/new`. Manual payment request flow supports one invoice; vendor advances and multi-bill excess cannot be represented.
- Backend: Real one-invoice payment request, independent approval, external recording, confirmation, list/detail and original-key recovery. Gap: Vendor payment before invoice, split across bills, unapplied/excess balance, later allocation/unallocation and advance history. These are missing manual workflows, not merely a missing bank connector.
- Next slice (Procurement manual payables): Add vendor payment header and allocation ledger with unused/excess balance, preserving current single-invoice flow.
- Prerequisites: None hard-linked in this audit.
- Tests: Split across two bills, advance before bill, later apply/unapply, currency/supplier mismatch and double-consume denial.
- Acceptance: Persist a supplier advance or a payment split across multiple invoices, conserve used/excess totals, apply later without double consumption and deny cross-supplier/currency/branch allocation.
- Source: `backend/hrm-api/src/Mightyfin.Erp.Hrm.Api/ProcurementManualPaymentRoutes.cs:24`, `backend/hrm-api/src/Mightyfin.Erp.Hrm.Api/ProcurementManualPaymentCommandRoutes.cs:23`, `backend/hrm-api/src/Mightyfin.Erp.Hrm.Api/ProcurementManualPaymentCommandRoutes.cs:46`

## PAY-05 — Manual refund, reversal and historical correction (IMPLEMENT)

- Original UI (NOT_PRESENT): `reference-ui/src/features/RecordDetail.tsx:281` — Payment mode / paid through detail. Local payment detail has no refund/reversal action or compensating receipt.
- Current live counterpart: `reference-ui/src/features/LivePayments.tsx:93` on `/payments/:id`. Current external recording/confirmation does not prove refund/reversal correction history.
- Backend: Unexecuted cancellation, return, rejection and recorded-payment dispute exist. Gap: Refund cash-in records, confirmed-payment reversal/void-to-draft with retained compensating history, edit/refund allocation and payable reconciliation.
- Next slice (Procurement manual payables; Finance reconciliation adapter): Add compensating manual refund/reversal with authority and Finance reconciliation boundary.
- Prerequisites: PAY-04
- Tests: Partial refund, duplicate correction, reversal after allocation, stale status and retained original payment.
- Acceptance: Record an authorized refund or compensating correction linked to the original; retain prior evidence, recompute allocation capacity and prove replay/concurrent correction safety.
- Source: `backend/hrm-api/src/Mightyfin.Erp.Hrm.Api/ProcurementManualPaymentRoutes.cs:24`, `backend/hrm-api/src/Mightyfin.Erp.Hrm.Api/ProcurementManualPaymentCommandRoutes.cs:23`, `backend/hrm-api/src/Mightyfin.Erp.Hrm.Api/ProcurementManualPaymentCommandRoutes.cs:46`

## PAY-06 — Standalone and line-item vendor credit approval lifecycle (IMPLEMENT)

- Original UI (PREVIEW_ONLY): `reference-ui/src/features/TransactionEditor.tsx:702` — Credit Reason. Local credit editor can record a reason; standalone governed itemized approval is absent.
- Original flow detail: `reference-ui/src/features/TransactionEditor.tsx:392` — Credit submit locally updates one selected bill status and creditApplied meta.
- Current live counterpart: `reference-ui/src/features/LiveVendorCredits.tsx:67` on `/vendor-credits, /vendor-credits/new`. Invoice-linked credit reasons are narrower than standalone itemized vendor credit approval.
- Backend: Invoice-linked positive amount/reason credit Draft create/revise/cancel; same bill/vendor scope and conservative aggregate cap. Gap: Standalone vendor credit, credit item/tax rows, clone/import, shared Core credit review and Open/Closed/Void lifecycle.
- Next slice (Procurement credit workflow): Add standalone/itemized credit Draft, source and independent approval facts.
- Prerequisites: None hard-linked in this audit.
- Tests: Credit line tax/currency/quantity, unrelated invoice, maker/checker, cancel/revise and source replay.
- Acceptance: Create/edit an eligible standalone or itemized credit, validate amount/tax/source, run independent review and retain state/version history with correct cancellation boundaries.
- Source: `backend/hrm-api/src/Mightyfin.Erp.Hrm.Api/ProcurementCreditIntakeRoutes.cs:13`, `backend/hrm-api/src/Mightyfin.Erp.Hrm.Api/ProcurementCreditIntakeRoutes.cs:37`, `backend/hrm-api/src/Mightyfin.Erp.Hrm.Api/ProcurementCreditIntakeRoutes.cs:211`

## PAY-07 — Apply credits and record credit refunds (IMPLEMENT)

- Original UI (PREVIEW_ONLY): `reference-ui/src/features/TransactionEditor.tsx:716` — Apply credit to bill. Single local target updates bill status/creditApplied in meta; no reusable allocation capacity ledger.
- Original flow detail: `reference-ui/src/features/TransactionEditor.tsx:716` — Optional one-bill credit target; no server allocation history.
- Current live counterpart: `reference-ui/src/features/LiveVendorCredits.tsx:67` on `/vendor-credits/:id, /bills/:id`. Credit detail exists but application/unapplication/refund capacity ledger is absent.
- Backend: Captured noncancelled credit conservatively reserves invoice payment capacity; it does not assert Finance application. Gap: Partial credit application to one/multiple bills, remaining credit, unapply/reallocation and full/partial cash refund history/import/edit/delete semantics.
- Next slice (Procurement manual credit allocation; Finance reconciliation): Add exact credit allocation and refund records with independent Finance reconciliation.
- Prerequisites: PAY-06
- Tests: Apply to multiple bills, remaining balance, stale bill, unapply and no double consumption.
- Acceptance: Partially apply/unapply a credit and record a partial refund with immutable receipts; remaining value reconciles under concurrent invoice/payment/credit claims.
- Source: `backend/hrm-api/src/Mightyfin.Erp.Hrm.Api/ProcurementCreditIntakeRoutes.cs:13`, `backend/hrm-api/src/Mightyfin.Erp.Hrm.Api/ProcurementCreditIntakeRoutes.cs:37`, `backend/hrm-api/src/Mightyfin.Erp.Hrm.Api/ProcurementCreditIntakeRoutes.cs:211`

## PAY-08 — Authoritative batch payment management (IMPLEMENT)

- Original UI (PREVIEW_ONLY): `reference-ui/src/features/BatchPaymentEditor.tsx:81` — Edit prepared batch. Selected bill IDs and amounts are saved to local record.meta; screen says no payment sent.
- Original flow detail: `reference-ui/src/features/BatchPaymentEditor.tsx:81` — Batch selection saved to local meta with bill IDs and amounts.
- Original flow detail: `reference-ui/src/features/BatchPaymentEditor.tsx:100` — Screen identifies itself as local-only preview.
- Current live counterpart: `reference-ui/src/features/BatchPayments.tsx:93` on `/batch-payments`. Batch screen exists; server-owned authoritative preparation/execution history is not complete.
- Backend: Imported Batch Payments UI uses local storage; live admission has no batch capability and payment adapter declares it unavailable. Gap: Server-owned batch draft lines, independent approval, cancel/delete boundaries, bank-file export, per-payment processed/failed outcomes and retained original-key recovery.
- Next slice (Procurement batch workflow; Finance/bank adapter only for execution): Persist bounded batch membership, authority, corrections and status; Finance/bank owns execution.
- Prerequisites: PAY-04
- Tests: Mixed supplier/currency/branch, membership race, partial failure, duplicate export and bank outcome reconciliation.
- Acceptance: Persist a batch and its lines, approve independently, record per-line manual outcomes and recover uncertain acknowledgements; do not imply bank execution or bypass individual capacity guards.
- Source: `reference-ui/src/features/BatchPayments.tsx:26`, `reference-ui/src/features/BatchPayments.tsx:60`, `reference-ui/src/lib/ProcurementLiveGate.tsx:91`

## PAY-09 — Recurring bill profiles and child generation (IMPLEMENT)

- Original UI (PREVIEW_ONLY): `reference-ui/src/features/RecordDetail.tsx:293` — Recurring frequency/end date. Frequency/end date are shown from local meta; no scheduler generates child bills.
- Original flow detail: `reference-ui/src/features/TransactionEditor.tsx:364` — Recurring meta is saved as AppRecord; no scheduled child generation.
- Current live counterpart: `reference-ui/src/features/LiveRecurringPurchases.tsx:68` on `/recurring-bills`. Saved frequency/next date are planning metadata, not child generation.
- Backend: Vendor-backed planning records support create/edit/close and are actually live P24. Gap: Executable recurring-invoice templates, scheduled child bill creation, idempotent generation, stop/resume, child navigation and child-payment linkage. Planning Frequency/FirstCycleOn/LastCycleOn/NextCycleOn already exist; their presence is not execution.
- Next slice (Procurement recurrence execution; Core scheduling infrastructure): Add owned recurrence schedule, idempotent due-cycle child Draft generation and pause/resume.
- Prerequisites: None hard-linked in this audit.
- Tests: Time zone/DST, missed cycle catch-up, duplicate scheduler run, source version and child cancellation.
- Acceptance: Generate each due child exactly once using Core business time, preserve template/source versions, test catch-up and stop/resume and show generated records and payment links.
- Source: `backend/hrm-api/src/Mightyfin.Erp.Hrm.Api/ProcurementRecurringPurchaseRoutes.cs:16`, `backend/hrm-api/src/Mightyfin.Erp.Hrm.Api/ProcurementHostIntegration.cs:309`, `docs/procurement/PROC-07A-RECURRING-PURCHASE-PLANS.md:1`

## PAY-10 — Budget creation, controls and maintenance (DECIDE)

- Original UI (PREVIEW_ONLY): `reference-ui/src/features/Insights.tsx:150` — Plan spend vs approved limits. Local-record insight does not create authoritative Finance fiscal budget.
- Current live counterpart: `reference-ui/src/features/LiveBudgets.tsx:91` on `/budgets`. Budget screen must not imply Procurement owns Finance fiscal/category ledger.
- Backend: Scoped Finance check queue/status/history and evidence-backed outcomes; not a budget master. Gap: Fiscal period/category/tag/project budget grids, edit/delete/clone, prior-actual prefill/autofill, threshold notifications and authoritative consumption/approval blocks.
- Next slice (Finance budget authority; Procurement budget consumption UI): Agree Finance budget owner contract; integrate Procurement reservation/consumption display after authoritative service exists.
- Prerequisites: EXTERNAL:Finance budget owner
- Tests: Period/currency/category scope, exceeded/warn threshold, concurrent reservation and revoked budget access.
- Acceptance: Consume the owning Finance fiscal/period budget contract; test configured advisory/hard-block/exception behavior and reservations without creating a second balance ledger.
- Source: `backend/hrm-api/src/Mightyfin.Erp.Hrm.Api/ProcurementBudgetRoutes.cs:34`, `backend/hrm-api/src/Mightyfin.Erp.Hrm.Api/ProcurementBudgetRoutes.cs:188`, `reference-ui/src/features/LiveBudgets.tsx:14`

## PAY-11 — Budget versus actuals reports (INTEGRATE)

- Original UI (PREVIEW_ONLY): `reference-ui/src/features/Insights.tsx:150` — Plan spend vs approved limits. Uses local records; no reconciled Finance actuals.
- Current live counterpart: `reference-ui/src/features/LiveBudgets.tsx:91` on `/budgets, /analytics`. Budget display is not reconciled actuals reporting.
- Backend: Budget evidence history only. Gap: Period/category aggregation, actual consumption/variance, drilldown, filter semantics, PDF/XLS and print.
- Next slice (Finance reporting owner; Procurement report presentation): Consume Finance-owned period actuals and Procurement commitments with explicit as-of/source labels.
- Prerequisites: PAY-10
- Tests: Posted vs draft distinction, period close, partial PO/receipt and cross-branch report filter.
- Acceptance: Reconcile period/category actuals and commitments to owned sources; test as-of dates, FX basis, scope, drilldown and complete export against the same filters.
- Source: `backend/hrm-api/src/Mightyfin.Erp.Hrm.Api/ProcurementBudgetRoutes.cs:34`, `backend/hrm-api/src/Mightyfin.Erp.Hrm.Api/ProcurementBudgetRoutes.cs:188`, `reference-ui/src/features/LiveBudgets.tsx:14`

## PAY-12 — File/email ingestion and OCR-to-bill conversion (INTEGRATE)

- Original UI (NOT_PRESENT): `reference-ui/src/features/TransactionEditor.tsx:887` — Save bill. No OCR-to-bill action in inspected original editor; local save cannot assert document Clean.
- Current live counterpart: `reference-ui/src/features/InvoiceDocuments.tsx:74` on `/inbox, /bills/new`. Document controls exist; OCR-to-bill cannot infer Clean or approved amount.
- Backend: Manual Inbox lists saved bills; saved bill documents have Core upload/retry/download source and exact Clean-use pins. Gap: Inbox file/email ingestion, OCR extraction, processed queue, PO line matching and convert-to-bill corrections. Antivirus Clean does not extract invoice fields.
- Next slice (Core Documents scanner owner; Procurement intake/OCR adapter): Close Core scanner and typed OCR adapter with human correction before invoice Draft.
- Prerequisites: EXTERNAL:Core Documents C04
- Tests: Infected/pending file denied, extracted wrong supplier/amount reviewed, duplicate upload and source SHA.
- Acceptance: Receive a real file/email into an owned queue, scan it and validate any extraction; correct values and convert idempotently to a bill without treating OCR or Clean as business approval.
- Source: `reference-ui/src/features/LiveInbox.tsx:9`, `reference-ui/src/features/LiveInbox.tsx:50`, `backend/hrm-api/src/Mightyfin.Erp.Hrm.Api/ProcurementInvoiceIntakeRoutes.cs:31`

## PAY-13 — Attachments across credits/payments and cloud sources (INTEGRATE)

- Original UI (NOT_PRESENT): `reference-ui/src/features/RfqWizard.tsx:702` — Local document filenames. Illustrates preview upload pattern; credit/payment purpose-specific Core document action absent.
- Current live counterpart: `reference-ui/src/features/LiveVendorCredits.tsx:67` on `/vendor-credits/:id, /payments/:id`. Record attachments/cloud picker are not proven across credit and payment records.
- Backend: Invoice original-document routes exist; manual payment optional evidence reference exists and upload remains disabled honestly. Gap: Actual payment/credit document owner adapters, permitted cloud import, upload validation and purpose download coverage for those records.
- Next slice (Core Documents/connectors; Procurement record owners): Apply Core Documents purpose and grant model to selected credit/payment records; cloud source needs separate connector decision.
- Prerequisites: EXTERNAL:Core Documents C04
- Tests: Wrong-purpose attachment, revoked download, stale scan, cross-record link and retention.
- Acceptance: Attach/retrieve/retire actual payment or credit evidence under owner-purpose permissions; reject malicious files, expired links and unauthorized access; uploads remain optional where policy permits.
- Source: `backend/hrm-api/src/Mightyfin.Erp.Hrm.Api/ProcurementInvoiceDocumentRoutes.cs:81`, `reference-ui/src/features/InvoiceDocuments.tsx:37`, `backend/core-api/src/Mightyfin.Erp.Core.Documents.Domain/DocumentUsePin.cs:65`

## PAY-14 — Record discussion, mentions and activity reports (IMPLEMENT)

- Original UI (PREVIEW_ONLY): `reference-ui/src/features/RecordDetail.tsx:222` — Comments tab. Adds JSON comment in local meta; no shared actor/mention/history service.
- Current live counterpart: `reference-ui/src/features/LiveInbox.tsx:43` on `/bills/:id, /inbox`. Audit/outbox does not equal a user-visible discussion/mention thread.
- Backend: Immutable business audit exists; generic demo comments remain browser metadata. Gap: Server-owned record comments/audiences/history, rich content, authorized mentions, notification delivery, scoped audit/activity and system-mail report.
- Next slice (Procurement collaboration; Core audit/notification services): Add scoped comment/mention records and activity reader on existing detail layout.
- Prerequisites: None hard-linked in this audit.
- Tests: Mention recipient scope, edit/retention policy, deleted actor, cross-company denial and notification retry.
- Acceptance: Persist internal/supplier-visible comments with explicit audience and mention policy; verify scoped timeline/audit views and observable notification retries without exposing hidden content.
- Source: `reference-ui/src/features/RecordDetail.tsx:224`, `reference-ui/src/features/RecordDetail.tsx:644`, `docs/procurement/PROC-14-ANALYTICS-CONTROLS-AND-RECORD-COLLABORATION.md:11`

## PAY-15 — Payables and purchasing report datasets with drilldown (IMPLEMENT)

- Original UI (PREVIEW_ONLY): `reference-ui/src/features/Insights.tsx:150` — Plan spend insight. Local preview metrics and links; no authoritative payables aging dataset.
- Current live counterpart: `reference-ui/src/features/Insights.tsx:152` on `/analytics`. Operational insights are not authoritative aging/spend report datasets.
- Backend: Live Overview/Analytics show authorized currently loaded record counts and links; amounts/export explicitly unavailable. Gap: AP aging, vendor balances, payable/credit/payment totals, purchases-by-item, approval-duration metrics and full-query drilldown/aggregation; local page counts are incomplete reports.
- Next slice (Procurement reporting; Finance authoritative monetary datasets): Publish reconciled Procurement purchasing and Finance monetary read models with drilldown.
- Prerequisites: None hard-linked in this audit.
- Tests: As-of date, currency, partial settlement, filters before count/page and drilldown authorization.
- Acceptance: Compute required full-query report datasets with documented metric/FX/time definitions, access scope and consistent drilldown; test against known persisted totals rather than page-local counts.
- Source: `reference-ui/src/features/LiveOverview.tsx:72`, `reference-ui/src/features/Insights.tsx:414`, `reference-ui/src/features/Insights.tsx:419`

## PAY-16 — Report and transaction sharing (IMPLEMENT)

- Original UI (PREVIEW_ONLY): `reference-ui/src/features/RecordList.tsx:179` — Export list. Browser CSV of local filtered records; no server report share/delivery.
- Current live counterpart: `reference-ui/src/features/Insights.tsx:152` on `/analytics, /settings`. Visible report controls do not establish server-owned sharing/export/delivery.
- Backend: Some transaction tables can export loaded rows; live Analytics export disabled. Gap: Governed full-period/current-view export, report columns/grouping/multi-criteria, PDF/print, templates/password/PII options, email delivery and scheduled reports.
- Next slice (Shared ERP reporting/export/notifications; Procurement templates): Add scoped export/templates and controlled recipient delivery for accepted datasets.
- Prerequisites: None hard-linked in this audit.
- Tests: PII redaction, role/branch revocation, CSV formula safety, duplicate scheduled send.
- Acceptance: Export/share the exact permitted query with correct columns, PII and recipient scope; verify document rendering, delivery status and scheduled retry/deduplication.
- Source: `reference-ui/src/features/LiveOverview.tsx:72`, `reference-ui/src/features/Insights.tsx:414`, `reference-ui/src/features/Insights.tsx:419`

## PAY-17 — Entity preferences and safe bulk operations (DECIDE)

- Original UI (PREVIEW_ONLY): `reference-ui/src/features/Settings.tsx:989` — Entity preferences. Preview toggles and terms layout, not published policy/bulk authority.
- Current live counterpart: `reference-ui/src/features/LiveSettingsShell.tsx:27` on `/settings`. Typed settings do not establish broad entity preference or batch-control parity.
- Backend: Feature-specific fixed DTOs, strict field validation, scope checks and selected single-record commands. Gap: Custom fields, configurable bill/credit/payment validation, custom buttons/links/related lists, saved custom views and import/bulk edit/delete/submit where manuals specify them.
- Next slice (Procurement feature configuration; Core policy infrastructure): Pick concrete approved preferences/batch operations and reuse versioned Core policy.
- Prerequisites: None hard-linked in this audit.
- Tests: Unknown setting, stale policy, unauthorized publisher, old record unaffected and rollback.
- Acceptance: Persist selected module preferences and saved views through shared metadata/rules; test bulk commands for per-record scope, partial failures, CAS and controlled cancellation.
- Source: `backend/hrm-api/src/Mightyfin.Erp.Hrm.Api/ProcurementInvoiceIntakeRoutes.cs:29`, `backend/hrm-api/src/Mightyfin.Erp.Hrm.Api/ProcurementInvoiceIntakeRoutes.cs:129`, `reference-ui/src/features/LiveInvoices.tsx:530`

## PAY-18 — Accurate current implementation/acceptance status (DEPLOY)

- Original UI (PREVIEW_ONLY): `reference-ui/src/features/RecordList.tsx:630` — Local demo warning. Original list explicitly says workflow actions use local demo state; live status must be documented separately.
- Current live counterpart: `reference-ui/src/features/LiveInvoices.tsx:109` on `/bills, /payments`. Historical spec prose and live UI may describe broader features than accepted routes.
- Backend: PAY-02 explicitly accepts payment HTTP/browser privately; P24 live receipt covers vendor CRUD and recurring planning only. Gap: One current operation-level release matrix linking later payment proof, held P31 positive gates and exact live scope; no foundation-only citation as complete payment proof.
- Next slice (Procurement documentation and release ownership): Publish current route/capability matrix and reconcile spec/release copy against accepted source and runtime.
- Prerequisites: None hard-linked in this audit.
- Tests: Automated link/action inventory plus live positive/denied smoke for each advertised capability.
- Acceptance: Publish a dated operation-by-operation source/private/live matrix tied to actual builds and UAT; preserve earlier foundation-only and failed records as historical evidence.
- Source: `backend/hrm-api/src/Mightyfin.Erp.Hrm.Api/ProcurementManualPaymentRoutes.cs:24`, `backend/hrm-api/src/Mightyfin.Erp.Hrm.Api/ProcurementManualPaymentCommandRoutes.cs:23`, `backend/hrm-api/src/Mightyfin.Erp.Hrm.Api/ProcurementManualPaymentCommandRoutes.cs:46`

## CORE-01 — Role-specific home, quick request and defined dashboard metrics (INTEGRATE)

- Original UI (PREVIEW_ONLY): `reference-ui/src/features/Insights.tsx:150` — Planning insight. Local metrics can guide original layout, not scoped role-specific dashboard truth.
- Current live counterpart: `reference-ui/src/features/LiveOverview.tsx:129` on `/`. Shared dashboard/quick links exist; role-specific metric truth and quick-request scope need integration.
- Backend: A live overview route and readiness gate exist. Gap: No matching metric-definition, data-source or My Home positive proof in this audit.
- Next slice (Procurement UI + reporting): Bind accepted scoped metrics and role-specific quick actions to existing dashboard.
- Prerequisites: None hard-linked in this audit.
- Tests: Two roles and A+B/C branch filtering; metric drilldown equals source count.
- Acceptance: Trace each displayed metric to scoped persisted source and test role-specific quick-create and approvals.
- Source: `reference-ui/src/app/App.tsx:107`, `reference-ui/src/lib/ProcurementLiveGate.tsx:24`

## CORE-02 — Organization/account switching and self-service MFA (DECIDE)

- Original UI (NOT_PRESENT): `reference-ui/src/features/Settings.tsx:198` — Switch ERP module. Menu changes visible module context; no account/org switch or MFA action here.
- Current live counterpart: `reference-ui/src/features/Settings.tsx:172` on `/settings`. Current sign-in is live; MFA and organization switch/account lifecycle need Core ownership.
- Backend: ERP local sign-in, session guard and Core organization UI exist; public sign-in presentation has five passed browser cases. Gap: No Zoho-style MFA, external account lifecycle, org switch/default/delete or real public credential-flow proof established here.
- Next slice (ERP Core identity): Agree Core identity scope and explicit MFA/organization contracts before Procurement UI binding.
- Prerequisites: None hard-linked in this audit.
- Tests: Real session switch, cross-tenant denial, lost MFA recovery, revoked role and legacy session invalidation.
- Acceptance: Publish explicit Core identity/organization contracts and real authorized tests for supported operations; list unsupported controls honestly.
- Source: `modules/hrm/frontend/module-connect/src/platform/components/AuthGate.tsx:24`, `backend/hrm-api/src/Mightyfin.Erp.Hrm.Api/LocalIdentity.cs:118`, `docs/releases/ERP-SIGNIN-RESTORATION-20261025-ACCEPTANCE.json:1`

## CORE-03 — Edit and publish company identity/appearance (DEPLOY)

- Original UI (PREVIEW_ONLY): `reference-ui/src/features/Settings.tsx:602` — Configuration preview. Organization/display fields illustrate layout; branding save must target Core and deployed route.
- Current live counterpart: `reference-ui/src/features/LiveSettingsShell.tsx:27` on `/settings`. Branding editor source exists but current hierarchical route deployment is separate.
- Backend: Core BrandingPage calls GET/PUT/reset; public branding GET feeds sign-in and was exercised in public UI presentation tests. Gap: The accepted sign-in receipt does not verify live admin PUT/reset or complete organization profile parity.
- Next slice (ERP Core branding): Align Core/HRM settings bundle and route publication, then verify authorized edit/readback.
- Prerequisites: CORE-13
- Tests: Live route GET, edit/save/reload, unauthorized denial, older branding route compatibility.
- Acceptance: Verify authorized admin edit/reset, nonadmin denial, public readback and scoped organization fields.
- Source: `modules/hrm/frontend/module-connect/src/routes/hrm.configuration.branding.tsx:157`, `backend/hrm-api/src/Mightyfin.Erp.Hrm.Api/ApiRoutesClean.cs:1373`, `docs/releases/ERP-SIGNIN-RESTORATION-20261025-ACCEPTANCE.json:1`

## CORE-04 — Complete and verify existing custom Procurement role administration (INTEGRATE)

- Original UI (NOT_PRESENT): `reference-ui/src/features/Settings.tsx:289` — Procurement settings navigation. Original settings shell navigates to approvals/preferences; current Core Roles designer is a separate owned UI.
- Current live counterpart: `reference-ui/src/features/Settings.tsx:172` on `/settings`. Core Roles designer exists with subset of Procurement action choices.
- Backend: The Core Roles screen already has a Procurement permission grid for access, vendors, items, requests, plans, configuration, contracts and sourcing. Its create/update actions call real APIs; backend role CRUD persists assignments and LocalIdentity expands configured permissions. Gap: The directory text saying “HRM permissions today” is stale. Remaining gap is coverage of newer action families, scoped role-administration UX and deployed positive/negative verification; no absent-designer claim. Branch assignment absence can mean organization-wide scope by policy and is not itself a defect.
- Next slice (ERP Core permissions): Generate/synchronize permission choices from backend action catalog while retaining unknown explicit stored actions.
- Prerequisites: None hard-linked in this audit.
- Tests: All 91 current actions visible, old role edit lossless, empty/HR-only/unknown actions, denied assignment.
- Acceptance: Inventory current permission options against every supported action, verify create/update/readback and denied role/branch cases with real accounts, then correct stale directory copy after reviewed evidence. Add DTO/effective-authorization parity tests for exact seed, empty, HR-only, unknown and explicit retained actions; preserve valid stored actions on edit.
- Source: `modules/hrm/frontend/module-connect/src/routes/hrm.configuration.roles.tsx:44`, `modules/hrm/frontend/module-connect/src/routes/hrm.configuration.roles.tsx:116`, `modules/hrm/frontend/module-connect/src/routes/hrm.configuration.roles.tsx:144`

## CORE-05 — Select simple, hierarchical, multi-level, custom or no-approval strategy (DECIDE)

- Original UI (PREVIEW_ONLY): `reference-ui/src/features/Settings.tsx:1059` — Approval options. Threshold/approver preview fields show intended layout, not server Core case strategy.
- Current live counterpart: `reference-ui/src/features/LiveApprovalSettings.tsx:24` on `/settings, /approvals`. Feature approval editors exist but no universal Zoho strategy selector.
- Backend: Procurement live settings mounts feature-specific approval editors and uses Core approvals. Gap: No general strategy switch or feature-complete equivalence; some modules remain capability held.
- Next slice (Procurement approvals + Core shared engine): Specify supported stage modes/strategy per module and implement only selected policies via Core approval engine.
- Prerequisites: None hard-linked in this audit.
- Tests: No approval where allowed, any/all stage, maker/checker, policy version and return/replay.
- Acceptance: Inventory actual per-module policy configuration and verify maker/checker, SoD, replay, branch and no-approval cases with real Core evidence.
- Source: `reference-ui/src/features/LiveSettingsShell.tsx:89`, `reference-ui/src/features/LiveApprovalSettings.tsx:1`

## CORE-06 — Create custom modules, records and blueprints (DECIDE)

- Original UI (NOT_PRESENT): `reference-ui/src/features/Settings.tsx:1563` — New Field builder. Field builder preview is not generic deployable custom-module creation.
- Current live counterpart: `reference-ui/src/features/Settings.tsx:172` on `/settings`. No evidence of generic custom-module designer in supplied Procurement UI.
- Backend: No generic custom-module editor/engine was found in live Core settings; existing feature-specific workflows are separate. Gap: Generic module/blueprint behavior is an optional future extension, not a blocker for existing governed Procurement feature launch. No generic designer or engine is claimed.
- Next slice (ERP Core extensibility): Treat generic module blueprint engine as separate optional ERP platform product decision.
- Prerequisites: None hard-linked in this audit.
- Tests: If approved: isolated schema/permission lifecycle and migration rollback; otherwise documented exclusion.
- Acceptance: Define module ownership and safety boundaries; implement and verify one bounded real module before exposing designer.
- Source: `modules/hrm/frontend/module-connect/src/modules/core/settings.ts:14`, `modules/hrm/frontend/module-connect/src/routes/core.workspace.settings.$slug.tsx:26`

## CORE-07 — Define typed custom fields, validation, layout and related lists (DECIDE)

- Original UI (PREVIEW_ONLY): `reference-ui/src/features/Settings.tsx:1577` — New Field · module. Preview layout field form; no typed schema/validation/migration owner.
- Current live counterpart: `reference-ui/src/features/Settings.tsx:172` on `/settings`. Typed fields are not a generic custom-field/layout engine.
- Backend: Vendor editor stores narrow key/value CustomField and ReportingTag attributes with backend validation. Gap: Narrow vendor attributes are not a general field-definition, formula, layout, validation-rule or cross-module customization system.
- Next slice (ERP Core extensibility): Decide optional typed extension mechanism and ownership; do not let untyped JSON bypass record guards.
- Prerequisites: None hard-linked in this audit.
- Tests: If approved: validation, indexing, permissions, migration and schema compatibility tests.
- Acceptance: Retain typed vendor attributes; separately design schema/version/permission semantics and verify persisted definitions and reads.
- Source: `reference-ui/src/features/VendorEditorForm.tsx:69`, `backend/hrm-api/src/Mightyfin.Erp.Hrm.Api/ProcurementVendorRoutes.cs:852`

## CORE-08 — Manage numbering, reporting tags, PDFs, notifications and web tabs (DECIDE)

- Original UI (PREVIEW_ONLY): `reference-ui/src/features/Settings.tsx:989` — Module preferences. Preview numbering/terms/template options must be individually assigned to typed owners.
- Current live counterpart: `reference-ui/src/features/LiveSettingsShell.tsx:27` on `/settings`. Current defaults and policy editors cover selected cases, not all numbering/tag/PDF/tab options.
- Backend: Core lists these entries in the directory. Gap: The slug route renders Editor coming soon and cannot save; vendor tags are narrow attributes.
- Next slice (ERP Core shared defaults): Select specific defaults and add versioned owners plus existing layout controls.
- Prerequisites: None hard-linked in this audit.
- Tests: Old records stable after default change, unknown value denial, tenant scope and template rendering.
- Acceptance: Expose only genuinely persisted editors with owner policy and preview/readback; keep absent controls explicitly unavailable.
- Source: `modules/hrm/frontend/module-connect/src/modules/core/settings.ts:14`, `modules/hrm/frontend/module-connect/src/routes/core.workspace.settings.$slug.tsx:26`

## CORE-09 — Create and execute workflow rules, alerts, field updates, webhooks and schedules (DECIDE)

- Original UI (PREVIEW_ONLY): `reference-ui/src/features/Settings.tsx:1477` — Automation page. Configured previews do not execute webhooks or scheduled rules.
- Current live counterpart: `reference-ui/src/features/LiveSettingsShell.tsx:27` on `/settings`. Outbox and scheduled infrastructure do not prove user-authored workflow rules/webhooks.
- Backend: Procurement preview settings and legacy HRM mock process page display sample controls; live shell does not mount a general automation editor. Gap: No persisted general rule/action scheduler or safe integration authorization was found in this UI slice.
- Next slice (ERP Core automation): Define safe automation scope, rule publisher and effect boundaries before executable rules.
- Prerequisites: None hard-linked in this audit.
- Tests: Loop prevention, retry, revoked publisher, webhook secret storage and tenant isolation.
- Acceptance: Specify bounded event source, action safety, idempotency and scoped logs; prove execution and denial before enabling UI.
- Source: `reference-ui/src/features/Settings.tsx:1438`, `reference-ui/src/features/LiveSettingsShell.tsx:102`, `modules/hrm/frontend/module-connect/src/routes/hrm.configuration.process.tsx:8`

## CORE-10 — Administer API usage, connections, incoming webhooks, signals, backups and forms (DECIDE)

- Original UI (NOT_PRESENT): `reference-ui/src/features/Settings.tsx:470` — Finance owns this setting. Original settings route explicitly hands off outside-Procurement owners; no Core connection/backup admin here.
- Current live counterpart: `reference-ui/src/features/LiveSettingsShell.tsx:27` on `/settings`. No Procurement-owned connection/backup control should be inferred from settings shell.
- Backend: No corresponding live shared settings routes were found in audited Core directory. Gap: These admin operations are absent from visible connected settings; production backup practices alone do not establish self-service UI parity.
- Next slice (ERP Core integrations): Assign API usage, connection, backup and form admin to Core platform decision.
- Prerequisites: None hard-linked in this audit.
- Tests: If selected: admin-only CRUD, isolation, secret redaction, rotate and audit.
- Acceptance: Separate security-sensitive integration scope from backup operations; test tenant isolation, credentials, audit and recovery.
- Source: `modules/hrm/frontend/module-connect/src/modules/core/settings.ts:43`, `modules/hrm/frontend/module-connect/src/routes/core.workspace.settings.$slug.tsx:32`

## CORE-11 — Manage taxes, currency/rates, accounting references, customers/projects and general defaults (DECIDE)

- Original UI (NOT_PRESENT): `reference-ui/src/features/Settings.tsx:470` — Finance owns this setting. Original UI avoids maintaining financial masters in Procurement.
- Current live counterpart: `reference-ui/src/features/LiveSettingsShell.tsx:27` on `/settings, /budgets`. Procurement references currency/tax/budget facts but does not own general ledger defaults.
- Backend: Live Procurement redirects Finance-owned settings; Core directory lists currencies/general as planned. Gap: No integrated editable ERP equivalent in this settings slice; absence does not establish lack of Finance backend.
- Next slice (ERP Core + Finance): Agree Core/Finance owners for tax, FX, accounting and project references; expose only accepted lookups.
- Prerequisites: None hard-linked in this audit.
- Tests: Rate effective date, currency precision, tenant permission and historical posting integrity.
- Acceptance: Owner-by-owner source inventory and positive UI/API checks before connecting settings entries.
- Source: `reference-ui/src/features/LiveSettingsShell.tsx:106`, `modules/hrm/frontend/module-connect/src/modules/core/settings.ts:40`

## CORE-12 — Supplier self-service portal for invite, bids, orders, invoices, payments and statements (DECIDE)

- Original UI (PREVIEW_ONLY): `reference-ui/src/features/Settings.tsx:345` — Vendor Portal link. Sidebar navigates to portal preview; not an authenticated supplier experience.
- Original flow detail: `reference-ui/src/features/Settings.tsx:345` — Vendor Portal is a settings navigation link, not an external supplier session.
- Current live counterpart: `reference-ui/src/features/LiveVendors.tsx:81` on `/vendors, /rfqs`. Internal vendor and buyer RFQ screens do not authenticate supplier users.
- Backend: Vendor editor can request portal access, and outer preview has a demo supplier screen; live settings says no portal editor. Gap: External supplier login and transaction authority remain outside the current held capability. This becomes critical only if full supplier self-service is part of the launch scope; it is not a discovered security vulnerability.
- Next slice (Supplier identity + Procurement): Gate supplier portal launch decision, then create supplier session/own-record adapter without reusing employee identity.
- Prerequisites: BE-14, BE-16
- Tests: Own bid/order/invoice/payment scope, revoke invite, supplier cross-account denial and normal cookie.
- Acceptance: Define finite external identity and supplier scope, verify actual invitation/login, bid, PO response and no unauthorized access before exposing portal.
- Source: `reference-ui/src/features/LiveSettingsShell.tsx:105`, `reference-ui/src/data/settings.ts:165`, `reference-ui/src/features/VendorEditorForm.tsx:55`

## CORE-13 — Reach shared Core and HRM settings from published URLs (DEPLOY)

- Original UI (PREVIEW_ONLY): `reference-ui/src/features/Settings.tsx:507` — Shared settings redirect. Original shell routes into shared settings; deployment route mismatch remains a release issue.
- Current live counterpart: `reference-ui/src/features/LiveSettingsShell.tsx:27` on `/settings`. New settings source routes currently return 404 publicly while legacy pages return 200.
- Backend: Current source has /core/workspace/settings and nested editors; public route probe returns 404 for these and /hrm/workspace/settings, while /core and /core/branding return 200. Gap: The deployed release does not match the current hierarchical settings routes. HTTP 200 on legacy pages does not prove current Core editor or permission behavior.
- Next slice (ERP frontend release): Release aligned Core/HRM frontend routes and verify API/schema/source manifest with existing sign-in.
- Prerequisites: None hard-linked in this audit.
- Tests: Public route 200, authorized settings edit/readback, unauthorized 403/404 and no login regression.
- Acceptance: Ship reviewed route registration and test live public navigation/authorized editor read/write and unauthorized denial, preserving sign-in behavior.
- Source: `modules/hrm/frontend/module-connect/src/routes/core.workspace.settings.$slug.tsx:20`

## AUD-01 — Publish a current implementation baseline beside historical design drafts (DEPLOY)

- Original UI (PREVIEW_ONLY): `reference-ui/src/features/RecordList.tsx:630` — Local demo warning. Original preview labels its own boundary; dated historical specs need a current status link.
- Current live counterpart: `reference-ui/src/features/LiveOverview.tsx:129` on `/`. Historical documentation can read as a current no-backend statement.
- Backend: Bounded Procurement projects, routes and later production/private acceptance records exist. The active roadmap correctly retains a dated chronology. Gap: A reader-facing current source/private/live status above the stale present-tense claims and the stale HRM-only role description.
- Next slice (ERP documentation owner): Add linked current status banner to old maps and maintain evidence-bound module matrix.
- Prerequisites: None hard-linked in this audit.
- Tests: CI docs links and snapshot audit; historical text retained with clearly dated context.
- Acceptance: Date-stamp retained historical claims; connect each current claim to a versioned product/UI build and operation-specific acceptance, without relabelling earlier failures.
- Source: `docs/procurement/PROC-00-PROCUREMENT-MODULE-ARCHITECTURE-AND-CAPABILITY-MAP.md:25`, `docs/engineering/ERP-SYSTEM-DEFINITION.md:48`, `docs/procurement/release/PROC-OPS-P23-P24-LIVE-ACCEPTANCE-20261022.md:3`

## AUD-02 — Tie the 28-section designs to individual accepted operations (INTEGRATE)

- Original UI (PREVIEW_ONLY): `reference-ui/src/features/RecordDetail.tsx:118` — Reusable original detail frame. Tab/header reuse shows layout continuity, not source/API/runtime acceptance.
- Current live counterpart: `reference-ui/src/features/LiveSettingsShell.tsx:27` on `/settings`. 28-section specs describe intent; they do not prove each visible action.
- Backend: Named release receipts verify particular private or production slices; draft CRUD, internal review and source schemas have deliberately different scopes. Gap: One accepted operation register spanning screen, API, scope, state guard, owned data/effects, failure recovery and deployed evidence for each selected launch journey.
- Next slice (Product + engineering + QA): Attach each selected operation to route, UI control, owner, negative test and runtime receipt.
- Prerequisites: None hard-linked in this audit.
- Tests: Selected operation has all five links; held/absent operation does not appear as accepted.
- Acceptance: For each in-scope operation record its exact entry/state/actor/data scope/source version/effects plus allowed, denied, stale and retry tests; do not count passing heading checks or unrelated tests as workflow acceptance.
- Source: `docs/procurement/PROC-00-PROCUREMENT-MODULE-ARCHITECTURE-AND-CAPABILITY-MAP.md:39`, `docs/procurement/release/PROC-MODULE-COVERAGE-20261025.json:11`, `docs/procurement/PROC-WORKFLOW-REFERENCE-TRACE.md:22`

## AUD-03 — Make organisation-wide versus unassigned branch access an explicit policy (DECIDE)

- Original UI (NOT_PRESENT): `reference-ui/src/features/Settings.tsx:602` — Organization configuration preview. No explicit Core grant-vs-unassigned branch control in original Procurement settings.
- Current live counterpart: `reference-ui/src/features/LiveSettingsShell.tsx:27` on `/settings`. All-permitted vs unassigned branch selection must reflect explicit Core policy.
- Backend: Procurement rechecks current Core user/actions and selected company/branch; a Worker record is optional. Assigned branches constrain users. Gap: A visible, approved distinction between intentionally organisation-wide and not-yet-configured access, with clear user-administration language. This is a policy/control review, not a demonstrated cross-tenant exploit.
- Next slice (ERP Core data-scope owner): Define admin-visible organization-wide grant vs unconfigured account and propagate to current scope headers.
- Prerequisites: None hard-linked in this audit.
- Tests: User with A+B sees no C; unassigned behavior explicitly selected; new branch transaction requires valid branch.
- Acceptance: Verify explicit organisation-wide, single/multiple branch, unconfigured user and revoked grant cases across reads, creates, assignee pickers and exports; never infer a company grant from an employee home.
- Source: `backend/hrm-api/src/Mightyfin.Erp.Hrm.Api/ProcurementHostIntegration.cs:650`, `backend/hrm-api/src/Mightyfin.Erp.Hrm.Api/ProcurementPrincipalPeople.cs:18`, `backend/hrm-api/src/Mightyfin.Erp.Hrm.Api/ProcurementHostIntegration.cs:672`

## AUD-04 — Display the same effective permissions that the authorizer enforces (TEST)

- Original UI (NOT_PRESENT): `reference-ui/src/features/Settings.tsx:289` — Procurement settings navigation. Current Core Roles effective-permission readback is outside original preview; no local permission claim.
- Current live counterpart: `reference-ui/src/features/Settings.tsx:172` on `/settings`. Role UI display may differ from effective authorization for legacy/empty rows.
- Backend: Role DTO projection can display bootstrap fallback when no stored known Procurement action exists; the authorizer allows that fallback only for the exact intentional legacy seed. Gap: Regression evidence for effective/displayed permission equivalence on empty, unknown, HR-only and exact-legacy rows, plus preservation of newer actions not visible in the grid. This is a static code-path mismatch; no affected production row is established.
- Next slice (Core role administration + authorization): Compare effective permissions, API DTO and editor readback for seed, empty, HR-only, unknown and explicit actions.
- Prerequisites: None hard-linked in this audit.
- Tests: No phantom displayed grant or dropped stored grant; actual denied request agrees with UI.
- Acceptance: For each legacy/custom role case compare DTO/selected UI values with ResolveConfiguredActionsAsync; editing unrelated role fields must not manufacture or drop effective grants.
- Source: `backend/hrm-api/src/Mightyfin.Erp.Hrm.Application/ConfigAndExtras/JobsAdminServices.cs:151`, `backend/hrm-api/src/Mightyfin.Erp.Hrm.Api/ProcurementPermissions.cs:40`, `modules/hrm/frontend/module-connect/src/routes/hrm.configuration.roles.tsx:44`

## AUD-05 — Accept a coherent Core/HRM/Procurement release rather than infer it from separate proofs (DEPLOY)

- Original UI (PREVIEW_ONLY): `reference-ui/src/features/RecordList.tsx:630` — Local demo warning. Explicit preview boundary demonstrates why one aligned deployed bundle must be tested.
- Current live counterpart: `reference-ui/src/features/LiveOverview.tsx:129` on `/`. Private workflow proofs and public sign-in alone do not establish a coherent deployed module.
- Backend: Public login is ERP-wide; public hierarchical Core/HRM settings paths return404. Later Procurement features have private proofs while the deployed API remains at the earlier accepted baseline. Gap: A compatible release manifest and actual normal-session journey through the chosen launch operations with the same API/schema/UI/permission configuration. Private passing tests do not imply live availability.
- Next slice (Release engineering + QA): Pin one source/API/UI/schema/config bundle and execute same-release Core+Procurement normal-session journey.
- Prerequisites: CORE-13, AUD-03
- Tests: Positive/denied live journey, rollback/restore, capability matrix, monitor and post-release smoke.
- Acceptance: Pin source/build/schema/config and current authority; migrate and restore-test on staging, complete positive/negative real-session UAT and post-release smoke checks, then publish the precise live capability matrix.
- Source: `docs/procurement/release/PROC-MODULE-COVERAGE-20261025.json:15`, `docs/releases/ERP-SIGNIN-RESTORATION-20261025-ACCEPTANCE.json:1`, `docs/procurement/release/P35-NORMAL-COOKIE03-ACCEPTANCE-20261025.json:1`


## Accounting reference and ownership

Accounting is imported separately as a UI-only preview. Its richer allocation, refund, batch and recurrence interactions may inform the missing Procurement-facing controls, but its sample engine is not a Finance backend. Preserve existing Procurement layouts and use the owning supplier, document, workflow and accounting contracts. The imported UI has no authority to approve, pay, post or mark originals clean.

## UAT execution boundary

The CSV contains 55 unexecuted case groups, one per audit finding. Expand each into operation-specific happy, denied, invalid, stale/concurrent, retry and recovery cases with test data, screenshots/API evidence and business acceptance before claiming UAT. Existing cloned control handlers may be preview-only;15 inspected operations have no original control and need a compatible UI extension or the correct shared module, rather than a backend adapter alone.
