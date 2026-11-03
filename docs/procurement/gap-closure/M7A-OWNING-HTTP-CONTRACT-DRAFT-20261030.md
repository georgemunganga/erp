# M7A owning HTTP contract — implementation draft

Status: compiled source and native store admission verified; complete positive quotation HTTP/UI workflow acceptance and production rollout remain open. Existing quotation/RFQ screens will consume these contracts; no replacement UI is introduced.

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

Missing/unimplemented captured contracts fail 503 before coordinator resolution. Even a captured-contract startup check never replaces same-connection READ/RECORD inspection. No native shape/hash is invented. The verified scoped provider is registered after actual owning-transaction acceptance, as recorded below. The proposed optional metadata capability `manualQuotationRead` is not activated.

Nonempty attachments require actual enlisted Core document-use transport and currently return unavailable. Empty sets work. Cancellation retains original pins; unavailable current transport never fabricates verification or discards retained document evidence.

History verification reads one full owned row per closed-reader keyset batch, retaining only latest payload and fifty small summaries. No nested same-connection query runs with an active reader. A bounded history-read budget returns an actionable unavailable result rather than allocating thousands of large payloads. A scoped single-version route remains available; original-key recovery never depends on loading all newer history.

## Acceptance still required

Authored source tests cover exact JSON lexemes/overposting, explicit Core action grants, no-provider HTTP gates and real Core branch reassignment. These authored tests passed in the runs below. Positive source/contact/create/revise/cancel/replay, handler-B versus maker-A, alias changes, READ-without-INSERT, commercial-flags-off history/cancel, scope/CAS denials, owned effects and actual cookie/UI/native-owner reconciliation remain release gates. No HTTP activation or whole Procurement completion is claimed.

## Native admission candidate — 3 November 2026

The owning schema provider now has a named seven-table physical contract extracted from the genuine isolated M7A re-upgrade (`8ef2525d03f608d28da007f50a166019c579e1786980e4968b0de63ac53c9623`). It compares all named columns/types/defaults, constraints and indexes, rejects extra triggers/rules/RLS policies, and requires the configured execution login on the actual enlisted Serializable transaction. PostgreSQL attribute positions are excluded because an unused Down/Up legitimately leaves dropped slots; names and definitions stay exact.

READ requires the retained source/evidence and controlled Core identity projections, without INSERT, business flags or an employee requirement. RECORD additionally requires individual INSERT and UPDATE privileges, append-only evidence, no DELETE on quotations, and no Finance settlement writes. The provider is deliberately not registered until its compiled/native role tests pass. This candidate is source-only; no production enablement or positive API/browser acceptance is claimed.

The genuine quotation migration adds two scoped alternate keys to the M4 publication tables and removes a redundant decision index. M4's exact model-count admission is updated from 6 keys/8 foreign keys/11 indexes to the verified 8/8/10. Per-object keys, constraints, indexes and literal CHECK checks remain mandatory; no broad schema normalization is introduced.

## Actual owning-provider verification — 3 November 2026

The source at `8f6b4677516ff16d6363ca62e25a4a26bbdd5b83` passed 209 focused Host tests (all previous 167 stable cases plus 42 quotation cases), with analyzers. Receipt: `0bb1e5dd7f8f83c1a1dbc284bece48c924363ca789dc99db97f9d871ac32c1fb`. The first run retained four failures caused by a test identity missing the required tenant claim; the corrected fixture supplies that claim without relaxing ERP authentication.

Compiled assembly SQL, executed through the actual limited login on an isolated retained-archive restore, passed exact named schema and 11 grant/8 legacy-revocation reconciliation, READ with quotation INSERT removed, RECORD denial without INSERT, and RECORD denial in a read-only transaction. Receipt: `50130a782dc03908f50ced3d5e98d931b2e24e53e2372fe65c169ba8b00bf4e3`. The temporary role fixture restored the exact full database snapshot.

A separate compiled probe called the real provider against the actual enlisted Serializable connection. It rejected unenlisted access, accepted READ in read-only and read-write transactions, accepted RECORD only in read-write, and verified the actual EF model, migration history, named physical schema and current finite-role profile. Receipt: `e9e5826419a6fcc293838adbf2e05eeb835463ab85327c84641aa243d94206ab`. No business records were written. The initial probe was rejected by OCI before startup because procfs socket bind mounts are unsupported; its successor used a bounded private Unix socket relay, with no TCP listener, stopped after the probe.

This evidence permits registering the real scoped provider. Registration does not grant user actions or enable commercial capture; current identity, selected branch, policies and command-specific checks remain enforced. Final registration/model-compatibility regression tests, positive complete quotation HTTP workflows and original UI/browser acceptance remain required. Production is unchanged.

## Final source and RFQ compatibility checkpoint — 3 November 2026

Source `921bd7ea3e030d6ee05c2da614c22d18f6d36920`, including the published schema/role prerequisites and separate quotation switches, passed **218 focused Host tests**, with all prior stable cases retained and analyzers enabled (`a39a00c45ec220fe48085d5583b2fe7e57d61c2d600c8205b39ce86adcf2ad2c`). Earlier 216-case response-contract acceptance is retained as `516b0476ae0518512e6baafaa6920ef1c06ae4fef6bc7da4ecb1905757045a5c`. Existing compiler/analyzer warnings are not reported as zero warnings.

`Procurement:ManualQuotations:RecordEnabled` and `CaptureEnabled` default to false independently of the existing RFQ publication flag. New contact/create/revise require both quotation switches and existing source publication eligibility. Cancellation needs recording authority but remains independent of new-capture enablement. Historical READ and original-key recovery do not depend on either switch. Complete business-write acceptance is still required before activating these switches.

The existing RFQ response now reports the verified outside-contact state instead of always returning `contactRecorded=false`. UI PR98, merged at `c692611da5ca549ee00b1356cf7bc1e213b096da`, accepts the matching status/flag/version and retains the existing Related tab and ERP components. Ten adapter cases, TypeScript checking and all 22 RFQ Playwright cases passed on Node 22.23.3. Browser transport is mocked, separately from native database evidence, and is not positive quotation-CRUD persistence UAT.

After the real owning-provider probe, a complete native snapshot matched the previous state exactly and the dedicated restore was stopped (`97124aa904cbadb686efd9951e061aba88ddfba9f6449aadc82b34529fa78db9`). No production schema, role, UI or workflow changed. GitHub Actions cannot start because the account is billing-locked; this does not turn local evidence into a green CI result.
