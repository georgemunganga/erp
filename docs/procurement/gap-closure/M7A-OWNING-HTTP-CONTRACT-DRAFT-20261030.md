# M7A owning HTTP contract — implementation draft

Status: source-only candidate in the separate HTTP integration worktree. Not compiled, activated or native/browser accepted. The schema branch remains frozen separately. Existing quotation/RFQ screens will consume these contracts; no replacement UI is introduced.

## Access and ownership

Core registers four explicit actions: `procurement-quotation-read`, `procurement-sourcing-contact`, `procurement-quotation-capture` and `procurement-quotation-cancel`. Each has empty bootstrap grants, including for ERP administrators. Identity is a current Core principal; an employee link is optional historical attribution.

Every route requires current explicit quotation READ. A fresh contact/create/revise/cancel additionally checks its own action against current Core grants and existing company/branch scope. The handler may differ from the original RFQ maker or quotation capturer. M4 source creation/publication remains maker-controlled; its authority is not broadened by quotation access. Unsupported department scope fails closed.

Fresh writes require a selected active, permitted branch matching the source, rather than company All. Current branch assignments are revalidated. Retained reads and original-key recovery use current READ/scope and historical attribution, never current-employee equality.

## Routes

All paths start with `/api/procurement/v1/sourcing`.

| Method and suffix | Result / additional fresh-action permission |
|---|---|
| `GET /quotation-sources` | Scoped parent RFQ numbers/titles and links for quotation handlers |
| `GET /{eventId}/quotation-drafts` | Parent/supplier labels, invitations, drafts and verified offered actions |
| `GET /{eventId}/quotation-drafts/{bidId}` | Exact current snapshot and up to fifty history summaries |
| `GET /{eventId}/quotation-drafts/{bidId}/versions/{version}` | One exact retained historical version |
| `GET /quotation-commands/{key}` | Original actor's exact recorded response/status, without writing |
| `POST /{eventId}/invitations/{invitationId}/manual-contact` | Record outside contact; `procurement-sourcing-contact` |
| `POST /{eventId}/invitations/{invitationId}/quotation-drafts` | Create buyer-entered draft; `procurement-quotation-capture` |
| `POST /{eventId}/quotation-drafts/{bidId}/revise` | Full-set revision with header CAS; `procurement-quotation-capture` |
| `POST /{eventId}/quotation-drafts/{bidId}/cancel` | Preserve exact terms/documents and close draft; `procurement-quotation-cancel` |

Supplier/parent labels come from server records, with no UUID label fallback. Current offered actions are hints backed by current source and supplier checks; the actual command repeats all guards. Failure of a known current commercial proof removes that hint while retained history/cancellation stays available. Permission, cancellation, database and provider errors are not disguised as eligibility results.

## Inputs and exact money

Every POST needs the original nonempty `Idempotency-Key` UUID. Contact input contains expected event/invitation versions, outside channel/reference and reported time. Recording contact does not send email or prove a supplier submitted a bid.

Create/revise input contains `expectedEventVersion`, `expectedInvitationVersion`, optional `expectedBidVersion` (required for revise), change `reason` (revise), optional `receivedAt`, complete `offer` and complete `lines`. The endpoint owns the operation. Client-supplied operation, identity, tenant/company, employee alias, generated quotation IDs or verified-pin arrays are rejected.

Offer contains supplier quotation reference/date, validity (`Unspecified`, `Fixed`, `OpenEnded`), valid-until, documentary offered payment code/text, warranty, delivery terms/date and document selections. These codes are not verified Core tax/payment catalogs. Lines contain source-line ID, exact quantity/unit price, discount (`None`, `Amount`, `Percent`) and value, quoted tax treatment (`Exclusive`, `Inclusive`), declared quoted tax, delivery amount, offered tax code and notes/lead time. Shared raw numeric(18,4) converters accept exact JSON number or string lexemes and reject before CLR rounding.

Cancel input contains only expected bid version and reason; event/invitation expected versions are zero, offer/lines/received-at absent. It does not require a currently published RFQ, eligible supplier or new-commercial capture flag. Retained M4 identity, current cancel authority, selected branch, header CAS and actual RECORD profile remain required.

## Replay, storage and failures

The original actor/key is checked with READ before new-work flags, mutable commercial guards or RECORD privileges. The normalized complete private request must match exactly; replay returns the original stored JSON bytes/status. A changed request or occupied key produces conflict; another actor cannot recover that private command. Current employee-link changes do not invalidate recovery.

Fresh transactions lock command key, then event, invitation and existing bid in that order. Owning records/evidence are scope-stamped before AddScoped. A line-only revision explicitly marks the existing header modified, increments its existing version once, and stores a full immutable V3 snapshot plus command receipt and exact audit/outbox pair in the same transaction. Shared generic audits remain. Cancelled draft recreation is not offered because the existing submission-version uniqueness policy remains.

Missing/unimplemented captured contracts fail 503 before coordinator resolution. Even a captured-contract startup check never replaces same-connection READ/RECORD inspection. No native shape/hash is invented. The default provider stays unavailable. The proposed optional metadata capability `manualQuotationRead` is not activated in this draft.

Nonempty attachments require actual enlisted Core document-use transport and currently return unavailable. Empty sets work. Cancellation retains original pins; unavailable current transport never fabricates verification or discards retained document evidence.

History verification reads one full owned row per closed-reader keyset batch, retaining only latest payload and fifty small summaries. No nested same-connection query runs with an active reader. A bounded history-read budget returns an actionable unavailable result rather than allocating thousands of large payloads. A scoped single-version route remains available; original-key recovery never depends on loading all newer history.

## Acceptance still required

Authored source tests cover exact JSON lexemes/overposting, explicit Core action grants, no-provider HTTP gates and real Core branch reassignment. They have not run. Positive source/contact/create/revise/cancel/replay, handler-B versus maker-A, alias changes, READ-without-INSERT, commercial-flags-off history/cancel, scope/CAS denials, owned effects and actual cookie/UI/native-owner reconciliation remain release gates. No HTTP activation or whole Procurement completion is claimed.
