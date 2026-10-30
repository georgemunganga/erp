# M6E: controlled replacement of a reviewed, blocked invoice (private draft)

Status: source and genuine EF migration built and tested locally; isolated native schema rehearsal passed. Finite-role admission, actual-cookie original-UI replacement acceptance and release remain open. No production role grant, enabled UI capability or production activation is established by this checkpoint. This first slice is deliberately limited to a source invoice with an independently verified recorded Blocked comparison and **no capacity claim**. Existing source rows and response bytes remain unchanged.

Implementation checkpoint (30 October): product `48260af0` passed **1,735 Procurement tests, 88 focused Host tests**, the analyzer build and EF no-pending-model check. Genuine scaffold `20261030082157_M6eControlledInvoiceReplacement` adds only the two owned evidence tables and their indexes; its Down begins with a retained-row/marker `55000` guard. Up/Down SQL was generated, not applied. The schema-readiness provider remains unregistered, so the new routes fail closed until native schema and finite-role verification is complete.

Protected evidence is `procurement-m6e-source-sdk-20261030_run06`: committed source binding SHA `adec098dea5bdb18b0696cbc3e0868ab8906aed80dafe7191197f0001ed6c057`, SDK result SHA `abe62b2c7e700674383f2ee329ca44c5fd5e6cc27a67f4a00579ab52bcafed6b`, and 150-file publication manifest SHA `13a3f7f0bb304839dd0e68dd69b40454c29f485577f2744db7c1f3ffbd32210a`. No database, permission or production runtime was changed. Earlier failures are retained: a wrong policy constant, unstable receipt-ID test fixture, expectations that missed the earlier shared deletion guard, and a managed compiler out-of-memory failure after adding the generated schema. The identical final source passed with analyzers enabled and a bounded 1,600 MiB build container. Independent review also found and fixed UTF-8 reason limits, malformed null-line handling and controlled history errors. These source checks are not real-database or browser replacement acceptance.

Historical-window follow-up: product `ddb32bb9` fixes a genuine case where a comparison made after a future policy successor was published retained the finite old-policy window. History now accepts only the whole recorded policy payload matching an authenticated original or superseded window, using the same producer codec. Finite-window evidence requires observation at/after the verified supersession audit; original snapshots remain supported for long transactions. The current policy selector and existing observation-before-current-cutoff restriction remain unchanged. Snapshots crossing that cutoff are still an explicit unsupported recovery case.

This follow-up passed **1,752 Procurement tests, 88 focused Host tests**, analyzers, no-pending-model, byte-identical genuine migration SQL and a retained JSONB fixture replay. Binding SHA `b8222709dff2ae2eacfecc5fe168129b150992af733e5460c23883f1dc13a584` distinguishes tested backend `ddb32bb9` from documentation-only descendant `3a74a564`, with identical backend trees. An initial final-binding check stopped on that documentation HEAD change; it is retained separately, and no source test was relabelled. Fixture replay did not perform a native future-policy rollover. Actual migration/privileges, live rollover, readiness and complete UI acceptance remain open.

Latest presentation/readiness checkpoint (30 October): source `a9125488` passed **1,752 Procurement tests and 151 focused Host tests**, including presentation, finite-readiness, branch-scope and existing invoice CRUD regressions. Analyzer and pending-model checks passed; generated Up/Down SQL is unchanged. The four-field correction context displays the verified human PO reference. The strict readiness implementation remains deliberately unregistered until actual finite database-role proof passes. Read admission is independent of new-work flags and INSERT permission; new commands still require the separate write profile. Composite binding SHA `24aa65c9de9b510e486e59584b70ceffd73e5bab3959f7bce43d3c5bf29770b5` covers 95 source files and the 150-file publication. Compiled privilege-query receipt SHA `1b2222f19709db941742c64433460c309abd59d3fce01e83a0183766a6826291` is the input for native role verification. The initial post-publish wrapper exit 127 (missing Python in the SDK image) is retained; the separate emitter follow-on and composite verification passed. It is not presented as an uninterrupted wrapper pass.

Isolated native schema rehearsal copied the closed 227-table baseline, applied the genuine two-table migration, verified an empty Down/Up round trip, and exercised the genuine first-Down guard with rollback-only audit and outbox markers. All 229 resulting tables and prior data/security were reconciled; neither the source databases nor production were migrated. Source bridge SHA `4d5d0273ea0afbf616f89074dce13a0053d118f0568807920e883b9809ed3745`, Up result `4f0627ad28a996dbc4fb9714318b6ee2a6bfd7c482837b8f9abf9d864adafb0d`, round-trip result `0eb4a18f5c4f9bd89f0a5c5dbf6b40ca238d8a20b95386cc4d6bc6f4bc97eacc` and marker-veto result `74ea0ec2fa859d1c61db567b7984baa201ffa1286b28f66ef45c8fe01bb92489` retain the exact boundaries. Marker-only veto tests do not prove a populated replacement link/receipt workflow. Actual finite-role grants, replacement command/browser acceptance and reviewable production rollout remain outstanding.

## 1. Feature Identification

M6E, Procurement-owned invoice replacement. It creates a separate successor invoice and a durable link. It does not edit a processed Bill.

## 2. Purpose & Business Outcome

A maker can record a supplier-issued corrected invoice after an earlier invoice was mapped, independently reviewed, and blocked. The original header, lines, mapping, Core review, comparison result, exception, command receipts, audits, and outbox events remain readable exactly as recorded. The successor follows the current v2 calculation, mapping, independent unit review, and comparison path.

## 3. Scope & Exclusions

The first slice accepts exactly: same tenant, company, supplier, currency, selected branch, and issued PO; one existing source invoice with `Status=Received`, latest source-schema-3 / execution-version-2 Blocked result, a closed zero-allocation/no-active-claim footprint, no Finance posting, credit, payment, or handoff effect; and a distinct supplier-issued corrected invoice number. The PO ID is derived from the verified latest mapping, result and retained issued owner. The source invoice header may legitimately have `PurchaseOrderId=null` (current manual intake does this); if nonnull it must equal that authoritative PO. The successor input/header pins the authoritative PO. The source's recorded Blocked issue is retained rather than re-evaluated at today's policy.

This slice excludes same-number correction, an active or released claim, Finance posting/payment reversal, credit-note resolution, cross-company/vendor/currency/PO transfer, arbitrary Blocked outcomes, in-place rewrite, and automatic approval of the successor. Unknown effects fail closed.

## 4. Parent & Related Features

Reuse M6A approved complete-history baseline, M6B typed principal source/fact bindings and owner lease, M6C independent manual or document unit review, M6D Core currency precision and v2 calculation, existing Core identity/branch permissions, and the original Bill UI. The historical no-claim classifier is a prerequisite to accepting a source; its presence alone never authorizes a successor.

## 5. Actors & Participants

The initiating actor is the source invoice's recorded creator, currently active in Core, with explicit `procurement-invoice-read` and `procurement-invoice-capture` grants for the selected branch and company. A role label, employee link, client-supplied complete flag, or hidden company-wide fallback cannot confer authority. Existing independent Core unit-review policy selects a different reviewer for the successor. Procurement owns replacement records; Finance remains a future posting/reversal owner.

## 6. Entry Points & Triggers

The source Bill detail may show `Replace invoice` only from a server-computed allowed action. A new command creates the successor from the original detail context, using the existing Bill form fields and v2 preview. An original-key GET recovers the exact saved replacement response. Read endpoints expose both sides of the link and the retained review/comparison history. No generic `Edit` or `Cancel` command is repurposed.

Presentation contract for the existing Bill editor: invoice detail may return `allowedActions: ["Replace"]` with `replacementContext: { sourceInvoiceId, sourceVersion, purchaseOrderId, purchaseOrderNumber }`. The IDs/version must match the displayed source and its verified order context. `purchaseOrderNumber` is the nonblank, trimmed human reference (maximum 80 characters) from the same verified retained order; it is displayed instead of a raw UUID. Missing or malformed context never enables replacement. `/bills/:id/replace` reuses the full existing editor in correction mode; vendor, source currency and authoritative PO stay locked, exact source lines can be reviewed, and the new supplier-issued number starts blank. Server validation remains final.

Optional metadata capability `invoiceReplacementRead` means installed, authorized replacement **read** support; absent means false. It must remain independent of the new-work flag and INSERT readiness, so disabling new work does not hide retained links. Existing invoice capture, edit and cancellation commands are separate and must never be used as replacement fallbacks.

## 7. Preconditions

For a **new** command, within one shared Serializable Procurement/Core transaction: verify current account, exact tenant/company/branch, source creator, explicit grants, installed write schema and finite-role readiness, current published Core currency reference, current selected-branch existence, genuine issued PO and supplier binding, current source version and mapping/result pointers, complete owner inventory, and the exact closed no-claim result/exception/receipt/audit/outbox set. Require zero active allocation, credit, Finance handoff/status, and manual-payment effects for the source. Require no prior authentic replacement link. Confirm the corrected number is actually provided by the supplier and differs after normalization. Original-key recovery has the separate ordering in section 24 and must remain available when new-work admission is off or the captured Core reference has since retired.

## 8. Workflow

1. Read source Bill, its recorded comparison, and server eligibility. Display the original as immutable.
2. Maker enters a nonblank reason and a distinct corrected supplier invoice number, edits successor lines, and confirms the v2 Core precision preview and digest.
3. Server checks current actor/source scope and original key first. A matching retained key is verified and recovered before any current new-work, policy or source-link eligibility gate. An absent key enters the new-work path. Acquire locks in the existing issued-source → exposure → source-invoice order; re-read pointers and effects in the same transaction.
4. Extract the existing v2 invoice capture's **in-transaction staging operation** from `ProcurementInvoiceCurrencyCalculationRoutes.CaptureCurrencyRounded`: it accepts the caller's same `ProcurementDbContext` and active Serializable transaction, verifies current Core reference and branch on that connection, and stages invoice, lines, calculation, capture audit/outbox and `InvoiceCaptureV2` receipt. The existing public capture route continues to own its transaction; the replacement route owns its one transaction and invokes only this staged operation. It never invokes the HTTP capture endpoint, begins a nested transaction, or performs a second independent save.
5. Derive the staged capture receipt's internal command key as UUIDv5 of a fixed, versioned M6E namespace and canonical `(tenant,company,sourceInvoiceId,clientReplacementKey)` bytes. It is never a second client-generated command. Store this derived key and capture receipt ID in the link; verify the derivation on reads. Append one replacement link, one replacement command receipt, exact audit, and outbox event in the same `SaveChanges`/commit. No partial successor or orphan calculation can commit.
6. Return and retain the original response bytes. The successor starts `Received`/`NotCompared`; its mapping and independent review are new commands.
7. Subsequent source detail derives `SupersededByCorrection` from the verified link; the physical source `Status`, amounts, and history stay unchanged.

## 9. Status / State Transitions

No source invoice physical state transition occurs. The read projection may show `CorrectionAvailable` before the command and `SupersededByCorrection` after an authentic link. The successor begins `Received`, mapping revision 0, `NotCompared`, no result or active claim. A later successor comparison follows existing M6B/C rules. A link is not a Finance payment or approval state.

Only after the server authenticates the committed link against exact source, successor, calculation, internal capture receipt, replacement receipt, audit and outbox IDs, actor, company/scope, request/response hashes and derived internal key does it deny **new** source Map, unit-review Open, Compare and payment-initiation commands. Existing source detail/history and original-key GET recovery remain readable with current authority. A partial/malformed link fails closed; no client-side status string alone can impose or waive this guard.

## 10. Permissions & Data Scope

POST requires current Core account, source creator, `procurement-invoice-read` plus `procurement-invoice-capture`, exact selected branch, tenant/company equality, and current source access. `All` cannot write. A manager role name alone cannot bypass creator/scope in this first slice. Original-key GET first repeats current actor and source access checks; a different actor receives 404. Read of either invoice requires current scoped read permission. The independent successor reviewer uses the existing Core unit-review grants and separation of duties.

## 11. Business Rules

The successor must retain exactly the source's supplier, currency, company and branch, and must pin the PO proved by the source's latest mapping/result/issued owner. A null source header PO is accepted only with that binding; a nonnull source header PO must match. It must use a genuinely distinct supplier-issued corrected number; no suffix is generated. Existing uniqueness `(tenant,company,supplier,normalizedInvoiceNumber,currency)` still applies. Quantities, rates, and taxes remain exact inputs; v2 rounds using a current Core-owned currency revision. The replacement link contributes no consumption or release. The old verified Blocked comparison contributes zero only through its full recorded no-claim proof. All newly matched capacity belongs to the successor's later qualified claim.

## 12. Data Fields & Ownership

Proposed append-only `procurement.invoice_replacement_links`: ID/version, tenant/company, source invoice ID, successor invoice ID, source version at command, source original digest, current mapping ID/revision, Blocked result ID, verified review source/outcome IDs, no-claim footprint digest/version, source supplier/currency/authoritative mapped PO/issued-version/branch, successor calculation ID, capture receipt ID and its derived internal command key, maker principal, normalized nonblank reason, client replacement command key, recorded UTC. Unique `(tenant,company,sourceInvoiceId)` and `(tenant,company,successorInvoiceId)`; scoped FKs to both invoices, source mapping/result/review and successor calculation/receipt; reject self-link and cross-scope links. The source's existing rows are never updated. The internal capture key uses the fixed M6E UUIDv5 namespace and canonical scoped source/client-key bytes, is distinct from the client's key, and is checked again before replay or link-derived action denial.

Proposed append-only `procurement.invoice_replacement_command_receipts`: ID/version, tenant/company, link/source/successor IDs, operation `InvoiceReplaceV2`, actor principal, command key, canonical request SHA-256, original response TEXT bytes (bounded 262,144) and SHA-256, response status 201, completed UTC. Unique `(tenant,company,actorPrincipalId,commandKey)` and exactly one receipt per link. This is a narrow command receipt, not a new approval engine. The successor's existing v2 capture receipt remains separately required, with its own authentic capture payload; it is not passed off as the replacement response.

`sourceNoClaimProofSha256` is server-derived from a versioned canonical envelope of the exact source/result/mapping/review/exception IDs, result evidence SHA, original Compare response SHA, compared audit/outbox digests, source original digest and empty allocation-set digest. A client cannot supply or override it. The link guard rechecks these retained rows under the replacement transaction; a hash-shaped string alone is not a completeness claim.

The existing approved baseline header and **all original 21 owner-table row digests and codec bytes remain unchanged**. M6E adds a separately versioned, complete owner-inventory supplement for `invoice_replacement_links` and `invoice_replacement_command_receipts`, captured in the same Serializable owner read. It proves exact ID sets, scoped FKs, one-to-one link/receipt, original and successor pointers, original response SHA, and the exact replacement audit/outbox marker pair. Every new row is classified; orphan, duplicate or unexplained link/receipt/marker fails closed. This is an additive proof alongside the old 21-table comparison, never a broad allowed-table whitelist or recapture of the approved baseline.

## 13. Screens & Data Views

Reuse `reference-ui/src/features/LiveInvoices.tsx` Bill list, form, and detail layout. Add a dedicated replacement route using the same input fields and calculation preview, with locked supplier/company/PO/branch and the source invoice currency prefilled read-only in the corrected input, plus an explicit corrected number/reason. The replacement command requires that explicit currency even if the company's current default has changed; ordinary new-invoice capture keeps its existing default behavior. The source detail's Overview and History show the successor link, recorded Blocked result, original exception, and immutable review; the successor detail shows the source link. Keep existing list filters and original Bill identity.

## 14. User Experience / Human Behaviour

Label the action `Record corrected invoice` rather than `Edit`. Explain that the original remains in history and the corrected invoice requires its own mapping and independent unit review. Never present the original Blocked amount as revised. Disable only server-denied actions; do not infer eligibility from `Status=Received` alone. Preserve the original form's keyboard and responsive behavior. On uncertain responses, offer original-key recovery before any new attempt.

## 15. Notifications & Communications

An outbox event may notify the existing Procurement workflow after commit; no email or Finance instruction is required for the first slice. Delivery retry is separate from command retry and cannot create another successor.

## 16. Documents & Attachments

Original document versions and hashes remain attached to the source. A corrected supplier document, if supplied, belongs to the successor with its own retained version and scan state. No source document ID is copied as if it were newly verified. The first slice remains usable with approved manual attestation when the explicit M6C policy permits it.

## 17. Exceptions & Edge Cases

Reject stale source version/pointer, an old or unknown Blocked footprint, any allocation/active claim, orphan or duplicate receipt/audit/outbox, unresolved credit/payment/Finance effect, prior link, number collision, changed branch/PO/supplier/currency, unavailable Core reference, unknown review authority, wrong maker, unselected branch, and repeated key with changed body. A pending old review or in-flight Compare is not an eligible source. The original Blocked exception remains recorded; the link supplies a separate derived disposition, not a mutation of its historical reason/status.

## 18. Integrations / APIs / Events / Sandbox

The ordinary invoice-detail GET supplies server-verified **new-work** eligibility through `allowedActions` and the four-field `replacementContext` in section 6, without claiming approval. This supersedes the earlier proposal for a duplicate `/replacement-readiness` endpoint. `POST /api/procurement/v1/invoices/{sourceId}/replacement` requires `Idempotency-Key`, expected source version, reason, corrected v2 invoice input, and expected calculation digest; the server pins current mapping/result pointers before upstream locks and rechecks them after the invoice lock. Response 201 binds link/source/successor/calculation IDs and original command key. `GET /api/procurement/v1/invoices/{sourceId}/replacement/commands/{key}` returns the original saved response bytes after current actor/source scope and typed retained link/receipt verification; it uses read readiness and never requires the replacement feature's current write flag, a current matching policy, or a still-active Core currency revision. `GET /api/procurement/v1/invoices/{sourceId}/replacement` projects a verified link; the verified relation is rendered on both original invoice details through the existing UI adapter. UI activation still requires actual finite-role and real-cookie acceptance; a second eligibility endpoint is not required. Outbox event `supplier_invoice.replaced.v1` is emitted exactly once with link/source/successor IDs and no Finance-eligible claim. Private sandbox rehearsal must use synthetic actors and data only.

## 19. Audit / Security / Privacy

Record one `InvoiceReplacementRecorded` audit with source and successor IDs, reason digest, actor, command key, original digest and pointers; record a separate successor creation audit through existing intake. Audit/outbox marker JSONB is compared semantically with exact fields, while command response TEXT bytes and SHA remain exact. No raw document bytes, cookies, credentials, or hidden-company invoice identities appear in logs. Foreign actor key lookup is 404. Source and successor row grants stay finite and scoped.

## 20. Reports / Dashboards / Metrics

Show source and successor as distinct invoices, with a typed `correctedFrom` / `correctedBy` relation. A corrected-invoice count comes from verified links, never from a rewritten status or invoice-number suffix. Capacity reports count only allocation rows from qualified successor comparisons; the old Blocked record remains a zero-claim historical result.

## 21. Configuration & Localization

Use the current Core currency precision reference and existing date/number localization. There is no new tenant setting that bypasses invoice matching, independent review, or branch scope. Feature capability defaults off until schema, grants, owner and native acceptance pass.

## 22. Non-Functional Requirements

Use one shared Serializable transaction and owning `ProcurementDbContext` for the entire replacement, bounded row sets, current tenant/company RLS completeness, deterministic lock order, exact decimal arithmetic, durable original-key recovery, append-only link/receipts, and fail-closed physical readiness. No nested transaction, HTTP self-call or external Finance call runs under locks. Database uniqueness plus CAS enforces one successor even under concurrent requests. Read/recovery admission is distinct from current new-work admission.

## 23. Postconditions

Exactly one new invoice, one new calculation, one existing v2 capture receipt, one replacement link, one replacement receipt, exact audit/outbox markers, and zero source-row changes. The source retains its original mapping, review, result, exception and receipt bytes; its old allocation/active-claim count stays zero. No successor mapping, review, claim or Finance posting is created by replacement alone.

## 24. Failure & Recovery

Commit is atomic. Any failed prerequisite leaves no successor/link/receipt/audit/outbox. Before issuing POST, the client journals the key and body hash. On a lost acknowledgement it performs same-actor original-key GET; the server returns original bytes. For POST, after parsing the key and revalidating current actor/source access in a Serializable read, the server looks up the receipt **before** checking current write capability, policy, Core precision retirement, source CAS, or prior-link/new-work eligibility. An original key with the same body hash returns the saved response after typed link/receipt/response verification; a changed body is 409. An absent key enters all current write gates. GET recovery applies the same current actor/source access and saved-graph verification without current write gates. A new key after an authentic source link is 409, never a second successor. Unknown outcome stops the client from issuing another key.

Before exposing the form, implement an explicit resolution for a known terminal rejection of its first POST, bound to the exact current actor/company/branch/source/key/body. A narrowly recognized server 422 `validation-failed` response can support a user-acknowledged “Review entries” action; it is distinct from uncertain recovery. A GET 404, lost response, changed scope, 403, proxy failure, unknown conflict, malformed success or 5xx never authorizes clearing the journal or generating a replacement key. Revalidation and a fresh preview precede any new first submission.

## 25. Acceptance Criteria

The private native owner verifies the complete preexisting business-table inventory and zero source claim before command, exact one successor/link/receipt after command, unchanged old rows except the reviewed new graph, unchanged scoped security, genuine Core reference/calculation, original-key byte equality, no old-row rewrite, and no Finance effect. Real-cookie original Bill UI demonstrates maker capture, independent successor review, mapping and a later qualified Compare as separate steps. Release acceptance requires genuine EF migration, first-Down retention veto, native Up/Down/Up on empty successor data, retained-data Down veto, finite grants, and compatibility review.

## 26. Test Scenarios

Positive: distinct-number corrected v2 invoice from verified historical Blocked/no-claim source; original rows/bytes unchanged; new independent mapping/review; later qualified claim uses only successor. Negative: duplicate number; second link or concurrent commands; same key altered body; foreign actor; wrong branch/company/supplier/currency/PO; stale version/pointers; forged or incomplete Blocked receipt/audit/outbox/exception; extra allocation, credit, payment or Finance row; Core reference retirement; lost response original-key recovery; replay after source link; downgrade with retained link/receipt. UI tests ensure original Edit/Cancel remain absent and source/successor history is explicit.

## 27. Ownership / Dependencies

Procurement owns invoices, replacement links, receipts, capacity and matching; Core owns identity, branch permissions, currency precision and independent unit-review decisions. Finance owns any future posting or claim-release evidence. Reuse `ProcurementInvoiceIntakeEditing.cs`, `ProcurementInvoiceCurrencyCalculationRoutes.cs`, `PrincipalInvoiceCurrentOwnerScope.cs`, `ProcurementInvoiceMatchingReads.cs`, `InvoiceUnitReview*`, and `LiveInvoices.tsx` without changing their historical v1 response bytes. New model/migration/readiness/route/UI files require separate ownership and review.

## 28. Open Decisions / Assumptions

Same supplier invoice number with a corrected document is not supported in this slice because the current unique key forbids it; a future design must define its business identity and migrate uniqueness with explicit duplicate protection. Replacing an invoice with any active, released or Finance-related claim needs a genuine reviewed capacity release/reversal owner. Whether a manager may initiate a replacement on behalf of the original maker needs an explicit permission and audit contract; no role-label fallback is assumed. The initial release requires a supplier-issued distinct corrected number and preserves the old Blocked record verbatim.
