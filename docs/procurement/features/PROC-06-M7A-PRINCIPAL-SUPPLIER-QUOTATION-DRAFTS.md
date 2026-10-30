# PROC-06-M7A — Supplier contact records and quotation drafts

**Status:** Proposed specification; not implemented or accepted by this document.

**Isolated foundation checkpoint (30 October 2026):** Pure quotation input validation and canonical snapshot helpers, with domain test sources, are prepared separately from the existing RFQ publication code. They do not establish principal provenance or external contact, persist records, add a revision model, expose an API/UI, install a migration or activate M7A. SDK execution and integration acceptance remain pending. Lead-time, notes and correction-reason text limits must be agreed and enforced at the future API boundary; this foundation does not invent those business limits.

**Version:** 0.1 · **Prepared:** 30 October 2026

**Hierarchy:** Procurement → Strategic Sourcing → Supplier Contact / Quotation Draft → Record, Create, Read, Revise, Cancel, Recover.

**Baseline inspected:** ERP backend `2f4137bd` and Procurement UI `9c56b492`; pending integration branches, not published main or production activation.

**Standard:** [GW-ENG-STD-001](../../engineering/GW-ENG-STD-001.md), Tier 3; the existing ERP 28-section framework.

**Parent:** [PROC-06](../PROC-06-STRATEGIC-SOURCING-AND-AWARD.md). **Sequence:** [M7A–M7D roadmap](../gap-closure/M7-SOURCING-TO-AWARD-ROADMAP-20261030.md).

## 1. Feature Identification

`PROC-06-M7A`. Operations: `O1` record an external supplier contact; `O2` create a buyer-entered quotation draft; `O3` list/read draft and revisions; `O4` revise; `O5` cancel; `O6` recover an original command. Each operation belongs to this child feature. Product owner: Procurement/sourcing owner to name; engineering owner: ERP coordinator until reassigned explicitly.

## 2. Purpose & Business Outcome

A permitted buyer can record that contact happened outside ERP and capture the supplier's quoted terms for later review. Success means a persisted, scoped draft with exact lines and recoverable command history. The screen must distinguish a buyer-entered draft from a supplier-submitted bid. A contact record does not prove email delivery, and a quotation draft does not select a supplier.

## 3. Scope & Exclusions

Include contact provenance and quotation draft CRUD/history for an existing M4 internally published RFQ with retained approved-demand bindings and shortlisted supplier invitations. Support multiple invited suppliers, with one active principal draft per invitation and retained cancelled/revised history.

Exclude sending email/SMS/portal invitations, supplier login, formal submission or sealed offer acceptance, opening bids, evaluation, recommendation, award, PO creation, contracts, Finance reservations, payments, Inventory and Assets. Existing PR allocation holds remain unchanged. RFQ publication approval is not supplier delivery or bid approval. Documents may be referenced only through actual Core document contracts; no synthetic attachment, scan result or supplier signature is created.

## 4. Parent & Related Features

Reuse M4 principal approved-source conversion, publication evidence and Core approval. Related owners are Supplier Management, Core identity/permissions/branch scope, shared documents, currency configuration, audit and notifications. Future M7B consumes an explicitly adopted draft version; it must not infer submission from `SupplierBid.Status=Draft`. M7C/M7D consume their own approved source contracts.

## 5. Actors & Participants

The internal buyer/recorder is a current Core account with the exact operation permission and access to the event's company and branch. An employee link, job designation or payroll record is not required. A supplier is a referenced commercial party, not an authenticated actor in this slice. Authorized auditors inspect retained evidence. No approver or workflow engine is introduced for recording a draft.

## 6. Entry Points & Triggers

Enter from the existing published RFQ detail, its shortlisted suppliers and quotation section. Offer actions from current server-owned context; no UI status label alone grants authority. Start contact recording after internal publication; start a quotation draft after the matching invitation has a verified contact record. Reopen saved drafts and recover pending saves from the same existing screen.

## 7. Preconditions

For new work, verify on the owning connection: current account and operation grant; tenant/company; a valid explicit selected branch; permitted event ownership/assignment; retained M4 publication/opening/decision/source proof; matching invitation and supplier; current supplier eligibility; valid source lines and currency; installed schema and finite write profile. Company All is a read context, not a write branch.

A contact timestamp must describe the observed external contact and not be silently backdated. Configured event deadlines and policy restrictions govern new capture; absent settings must not be replaced with invented legal thresholds. No formal on-time supplier submission is asserted by this slice.

## 8. Workflow

1. Read the internally published event and authorized shortlist, using human supplier names and RFQ references.
2. Record the external contact channel, observed time, reference and notes. State plainly that ERP sends nothing.
3. Choose that invitation and enter quoted lines, quantities, prices, tax, delivery cost and optional lead time/notes. Currency and source line identity come from the event.
4. Before POST, journal the exact actor/company/branch/event-bound body and UUID command key. Validate structural input without dereferencing null lines.
5. Server checks current read authority/scope and a saved command before mutable new-work gates. A matching original command returns its verified original bytes; a changed body conflicts.
6. For an absent key, lock the event/invitation/draft in a documented stable order, reload current authority and source, then apply validation/CAS. Build evidence with its verified owner scope before `AddScoped`.
7. Atomically persist the draft change, complete exact snapshot/version, receipt, shared creation audits and specific business markers. No PR allocation, Core approval case, delivery or financial effect is created.
8. Return the saved result. Revise by a new command and expected version; cancel with a reason and retain prior versions. A lost response uses original-key GET, never an automatic second POST.

## 9. Status / State Transitions

| Record | Transition | Guard / result |
|---|---|---|
| Invitation contact | ReadyForManualDelivery → ManualContactRecorded | Authorized observed contact; no sent/delivered assertion |
| Quotation | Absent → Draft | Published source, matching contact, valid draft inputs |
| Quotation | Draft revision n → Draft revision n+1 | Expected version, authority and source revalidated |
| Quotation | Draft → Cancelled | Current permission, CAS and nonblank reason |
| Any retained version | Read/recover | Current authorized reader/original actor; no state mutation |

`Submitted`, `Opened`, `Evaluated`, `Awarded` and `NotSelected` are not M7A transitions. Cancellation does not withdraw a supplier bid or release the event's PR holds. Replacing a cancelled draft, if allowed, uses a distinct draft identity/key and preserves its predecessor.

## 10. Permissions & Data Scope

Define separate contact, quotation-read, quotation-capture and quotation-cancel actions in the shared Core permission catalogue, with explicit role grants and no permissive bootstrap. The held implementation's broad `procurement-sourcing-submit` is a reuse reference, not an automatic decision to reuse that authority for every action. Final identifiers and role mapping must be recorded before implementation.

Evaluate current Core principal, action, event/invitation resource and tenant/company/branch/ownership or assignment at every endpoint. Apply scope before list counts/pagination, not after loading all bids. Deny foreign invitation IDs, hidden branches, other companies and another actor's command key. Optional employee aliases may support historical attribution or separation rules; they never confer access or become a mandatory employee prerequisite.

## 11. Business Rules

- Persist explicit principal draft evidence; never promote legacy worker-era `SupplierBid` rows merely because their status or source channel looks compatible.
- Require an invited supplier bound to the verified event/contact; prevent duplicate active principal drafts per invitation with a database-backed rule.
- Each quotation line references one authorized event line; reject duplicates, missing identity, null elements and unknown lines. Quantities are positive; tax/delivery are nonnegative. Initial scope uses the event's units/currency and requested quantity limits; alternatives/MOQs need a separate explicit rule.
- Use exact decimal transport/arithmetic and declared installed precision. Reject excess range/scale before persistence; PostgreSQL must not silently round inputs. Do not use JavaScript Number for authoritative amounts.
- Displayed draft totals are commercial estimates, not an encumbrance, payable, saving or authorized award. Reuse Core currency references where required; do not create another currency master.
- Preserve original contact, snapshot/version and command response. A current supplier suspension blocks new effects without erasing old drafts or receipts.
- Replay verifies the saved graph and original actor/key/body under current read authority before current supplier eligibility, deadlines, new-work flags or write privileges. Read permission is not INSERT permission.

## 12. Data Fields & Ownership

| Record / fields | Owner and boundary |
|---|---|
| Event, source lines, invitation, publication pins | Existing Procurement/M4; referenced, not recreated |
| External contact | Procurement: event/invitation/supplier IDs, channel, observed time, reference, notes, recorder principal, command key/digest, recorded UTC |
| SupplierBid / lines | Existing Procurement commercial records with explicit principal-draft discriminator/typed ownership; supplier/event/currency, draft revision and exact source-line quantities/prices/tax/delivery |
| Immutable quotation version/receipt | Procurement: bid/event/invitation, revision/expected version, operation, actor and optional historical alias, complete canonical snapshot, body hash, original response TEXT/hash/status, command key and recorded UTC |
| Identity, roles, branches, company and currency | Core; consume stable IDs/minimal projections |
| Document/version/scan evidence | Core Documents; actual controlled references only |

Every new fact is tenant/company-scoped. Scoped restrictive FKs bind invitation, supplier, draft and source lines; uniqueness binds active draft ownership and actor-scoped command keys. Define full deletion/retention rules before migration. Posted evidence uses cancel/version, not physical deletion.

## 13. Screens & Data Views

Reuse `PrincipalPublication.tsx`, `RfqDetailLayout`, existing RFQ sections, shared table/form controls, supplier lookup projections and shell context. Add contact and quotation sections inside the existing detail; preserve the imported layout. Use supplier/RFQ human references instead of raw UUIDs. Keep permissions/loading/empty/error/stale/pending/recovered/cancelled states explicit. Expose revision history without adding a competing procurement application.

## 14. User Experience / Human Behaviour

Use simple labels such as “Record supplier contact”, “Enter quotation”, “Save draft”, “Save changes”, “Cancel draft” and “Check saved result”. Explain that a draft has not been submitted by the supplier. Keep currency/source context visible, required fields clear and entered values intact on validation errors. Confirm cancellation; do not reset an uncertain command or imply that a 404 recovery proves nothing was committed. Context changes stop late responses from navigating or overwriting another record.

## 15. Notifications & Communications

No external message is sent by M7A. Shared in-app notifications may announce an assigned internal task only when its recipient and event contract are defined. Audit/outbox records do not prove delivery. Future supplier dispatch must have its own delivery/retry/recipient evidence.

## 16. Documents & Attachments

Where users reference original quotations, use shared access-controlled document versions, size/type/scanning/retention rules and explicit audience. An unavailable scanner/upload integration remains unavailable; a filename is not verified evidence. M7A may capture a clearly labelled draft without a document only under the agreed draft policy. Formal submitted offers and required source-document evidence belong to M7B.

## 17. Exceptions & Edge Cases

Cover revoked action/branch, deactivated account, suspended supplier, cancelled/changed event, invitation reassignment, existing draft, stale version, repeated keys, changed bodies, null lines, invalid decimals, deadline changes and uncertain network outcomes. Multi-supplier drafts must remain isolated. Define whether late cancellation remains allowed and how optional worker-alias changes affect new authority without fabricating an employee dependency. Do not silently substitute supplier, currency or PR source.

## 18. Integrations / APIs / Events / Sandbox

Proposed narrow endpoints under `/api/procurement/v1/sourcing`:

| Method / path suffix | Purpose |
|---|---|
| POST `/{eventId}/principal-invitations/{invitationId}/manual-contact` | Record observed external contact |
| POST / GET `/{eventId}/principal-manual-quotes` | Create / scoped list |
| GET / PUT `/{eventId}/principal-manual-quotes/{bidId}` | Read / CAS revision |
| GET `/{eventId}/principal-manual-quotes/{bidId}/versions/{revision}` | Retained version |
| POST `/{eventId}/principal-manual-quotes/{bidId}/cancel` | Cancel draft |
| GET `/principal-manual-quote-commands/{key}` | Original actor's exact saved response |
| GET `/principal-manual-contact-commands/{key}` | Original contact response |

Return bounded DTOs with server-owned versions/allowed actions, canonical decimal text and human labels. Writes use UUID idempotency keys; structural validation returns actionable 422, stale/body conflicts 409, unauthorized scope 403/404 and unavailable physical profiles 503. Reads/recovery have independent capability admission from current writes. Reuse the shared Serializable transaction, Core principal resolver, API errors and durable outbox; no HTTP self-call or external delivery runs under locks. Sandbox/native tests use synthetic actors and no supplier messages.

## 19. Audit / Security / Privacy

Retain actor, scope, target, operation, previous/current version, reason, command/request/response digests and correlation. Preserve the shared ERP generic creation audits plus precisely defined contact/quote markers; derive exact expected field arrays from the compiled EF model/producer. Do not accept arbitrary extra audit rows.

Treat native JSONB as typed objects/arrays, retaining strict value/type checks; calculation/snapshot/response TEXT retains its original codec/bytes where specified. Keep secrets, bank details, unrelated HR data and sealed future offers out of logs, broad lists and exports. Enforce direct-object scope, upload/document access and export authorization server-side. Retention periods and legal hold remain configured business obligations.

## 20. Reports / Dashboards / Metrics

Expose scoped counts/aging for contact records and quotation drafts, revisions/cancellations and pending save recovery. Distinguish draft count from submitted supplier participation; no spend, savings, competition compliance or awarded value is inferred. Define metric denominator, timing and owner before publishing control KPIs.

## 21. Configuration & Localization

Core owns organisation, currency, users, roles and branches. Procurement owns contact channel choices, draft constraints, source preferences and module capability settings. Reuse existing document numbering, locale/time-zone display and exact decimal formats. Record observed/recorded UTC separately. Read and write flags/profiles are separate; missing configuration fails closed for affected new effects without hiding admitted retained history.

## 22. Non-Functional Requirements

Bound lines and history (initial endpoint line limit at most 200), text/file sizes, pagination and transaction duration. Lock deterministically; use optimistic versions and uniqueness for concurrent drafts/revisions. Test keyboard/mobile/loading/failure states with the existing design system. Set measured performance and recovery targets before launch; no new process, database, low-code engine or service is justified merely by this child feature.

## 23. Postconditions

Exactly the declared contact or quotation revision/receipt and its audit/outbox effects exist. Original publication, approved demand, allocation holds and prior versions remain unchanged. The quotation is Draft or Cancelled, `supplierSubmitted=false`; no Core award approval, supplier dispatch, PO, Inventory, asset, AP or payment effect exists. Original-key GET can reproduce the stored acknowledgment under current authority.

## 24. Failure & Recovery

A failed validation/admission/CAS transaction creates no partial draft, orphan line/version or marker. Journal before sending; retain unknown outcomes and recover by original-key GET. Same key/body returns the original status/bytes, while changed body conflicts. A different actor cannot recover that key. New-work flags off or INSERT withdrawn must leave admitted READ/history recovery operational. A 403/404/5xx, malformed success or lost response never authorizes silently generating another key. Confirm absence/commit through owning reconciliation before any authorized manual recovery beyond this contract.

## 25. Acceptance Criteria

1. A permitted workerless Core buyer opens a genuine M4 published RFQ, records contact and creates/revises/cancels a quotation through the preserved UI.
2. Supplier, invitation, source-line, currency, scope and exact decimal rules are enforced server-side; unknown/null/stale input fails without effects.
3. An intentionally lost acknowledgment recovers the original actor/key result byte-for-byte, with one committed graph. Changed body/foreign key attempts cannot expose or duplicate it.
4. Independent native reconciliation classifies every new row, shared generic audit and special marker; all old business rows/PR allocations remain unchanged.
5. Flags-off and required INSERT withdrawal preserve real saved history/recovery, while new commands are denied.
6. Genuine current-model migrations, guarded retention, finite-role profiles and relevant Core/HRM regressions pass before activation. No schema or UI mock alone is acceptance.

## 26. Test Scenarios

Automate domain validation, exact decimals/range/scale, source/contact binding, version chain and same-key semantics. Exercise the production-used record factory through actual `db.AddScoped` for owned evidence and foreign tenant/company rejection; do not replace this with textual initializer tests.

Host tests cover current principal/permission, explicit branch versus Company All, inactive/revoked/foreign actor, null lines, duplicate draft/line IDs, stale CAS, changed key body and read/write capability separation. Native tests cover new and retained rows, scoped FKs, uniqueness, atomic audit/receipt sets, concurrent same invitation/revision, Up/empty Down/Up and genuine retained-evidence Down veto. Playwright uses normal cookies and original components for each CRUD action and lost-response recovery; mocked frontend tests are labelled separately. Revoke sessions, stop owned processes and reconcile exact temporary fixture restoration after acceptance.

## 27. Ownership / Dependencies

Procurement owns contact/quotation rules and records; Core owns identity, scope, approvals, documents and platform contracts. Reuse current M4 source loaders, `ProcurementPrincipalPeople`, transaction/locking helpers, supplier eligibility and shared controls. Held P30 code is a reference for selective porting; held P32/P36/P38 branches include unrelated work and must not be merged wholesale. Genuine migrations are scaffolded against the accepted current model, preserving M6 invoice/Core changes. Implementation starts only after the current source freeze is released and this scope/permissions are reviewed.

## 28. Open Decisions / Assumptions

Confirm the business owner; exact permission identifiers and who may work another buyer's event; allowed channels/contact timestamp evidence; nullable deadline behavior and late cancellation; partial line/draft quantities and alternative offers; document requirement for drafts; optional employee-alias change policy; cancelled-draft replacement; exact decimal storage bounds and audit/retention/performance targets. Record decisions explicitly without inventing thresholds, automatic awards or delivery claims. M7B defines formal bid submission/closing/evaluation; M7C defines approved award; M7D defines award consumption by PO.
