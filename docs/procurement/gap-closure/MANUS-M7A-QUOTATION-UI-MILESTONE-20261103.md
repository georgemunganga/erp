# Manus M7A — Quotation UI integration

Tracking: [ERP issue 148](https://github.com/georgemunganga/erp/issues/148), within [whole-module issue 145](https://github.com/georgemunganga/erp/issues/145).

## Published prerequisites and ownership

- ERP main: `2a2d29eef4d68897e7a5ca920186058ad9f96336`, including merged PR144, PR146 and PR147.
- ERP Procurement frontend main: `c692611da5ca549ee00b1356cf7bc1e213b096da` in `georgemunganga/module-connect`, including PR98. Use these commits or current main descendants.
- Manus owns the existing Procurement presentation, scoped API adapters, UI recovery journal and frontend tests for this milestone. Root owns native backend workflow verification, schema/role operations, production activation and whole-module acceptance.

The frontend repository is the existing ERP UI source, not a request to build another app or import the reference again. Relevant paths are `reference-ui/src/features`, `reference-ui/src/components`, `src/platform` and `shared`. Follow its AGENTS.md and UI_COMPONENT_STANDARD.md. Preserve imported layouts and `@amizopower/erp-ui` controls, branding, shell, tables, filters and tabs. Do not replace a screen with a reduced parallel Live layout.

Read [the owning HTTP contract](M7A-OWNING-HTTP-CONTRACT-DRAFT-20261030.md), its referenced server DTOs, and [the persistence contract](M7A-TYPED-PERSISTENCE-SOURCE-20261030.md) before implementation. These define behavior; frontend mocks cannot redefine it.

## Deliver the complete draft workflow

1. **Find assigned work.** Use `GET /api/procurement/v1/sourcing/quotation-sources` for authorized quotation handlers. A handler can differ from the original RFQ maker. Do not require the maker-only M4 detail endpoint to succeed before quotation work can open. Use the same existing RFQ presentation and server labels.
2. **View invitations and existing drafts.** Load `/{eventId}/quotation-drafts`. Respect server-provided `allowedActions`, supplier names/numbers, source number, currency and versions. Do not render UUIDs as fallback display labels or invent availability from a role name.
3. **Record outside contact.** Submit the documented channel, reference, reported time and expected event/invitation versions with an original command key. Explain that this records contact; it does not send email or a portal invitation. Preserve the verified `ManualContactRecorded` state in the original RFQ Related tab.
4. **Create a complete quotation draft.** Support the full documented supplier offer, validity, offered payment terms, warranty/delivery information and line terms. Preserve exact raw quantity/price/discount/tax/delivery lexemes; use existing exact-decimal helpers. Never calculate authoritative money with JavaScript Number or round server evidence to fit a field.
5. **Revise a draft.** Submit the complete retained offer and active line set, expected versions and a reason. A line removal is a controlled revision, not deletion of historical data. Preserve unsupported fields instead of silently clearing them.
6. **Cancel a draft.** Use its own cancellation payload and reason. Retained cancellation can remain available when new capture is off or the supplier/source is no longer eligible. Do not offer recreation of a cancelled supplier draft: existing uniqueness rules do not authorize it.
7. **Read history.** Show the exact current snapshot and bounded summaries. Support the single-version route for older versions. Never imply that fifty displayed summaries are the complete history when `historyHasMore` is true.
8. **Recover an interrupted save.** Use the original actor/key GET route. Freeze the original command reference while pending; do not generate another key, repeat a POST automatically, or turn an unavailable/404 recovery into permission to create a replacement command. Recovering a saved result must not need the original employee link or current write permission.

All suffixes above use `/api/procurement/v1/sourcing`. Copy exact endpoint/input definitions from the published contract; do not invent alternate routes.

## Access, configuration and safe UI behavior

- Use normal ERP cookies and company/branch headers. Core identity, explicit action permissions and current data scope are authoritative. An employee record is optional. Never use designation, ERP-admin role, a browser flag or original ownership as substitute authorization.
- Required actions are `procurement-quotation-read`, `procurement-sourcing-contact`, `procurement-quotation-capture` and `procurement-quotation-cancel`, plus module access. They have no automatic administrator bootstrap grants. Read-only users must still be able to view authorized retained records.
- New writes require the source's permitted selected branch; Company All is a read context. Clear prior data and abort outstanding requests on company, branch, principal or sign-in changes.
- `Procurement:ManualQuotations:RecordEnabled` and `CaptureEnabled` default off. In an isolated test environment, Root may configure both for positive new-work testing. Capture off must not hide permitted history/recovery; recording on with capture off permits controlled retained cancellation. Do not activate production flags from this UI task.
- Store only minimal command references in the existing scoped recovery mechanism, not sensitive supplier offers in localStorage. Do not fall back to demo/local-store mutations when an API fails.
- Empty document sets work. Nonempty selections need the actual Core document-use transport, which is not available for quotation saves yet. Keep documents visible with an honest availability explanation; never manufacture verified pins or discard retained document evidence.
- Preserve loading, centered empty state, validation, permission-denied, stale-version, save-unconfirmed, unavailable and success states. Use plain business language and actionable recovery. Do not show a success toast without a confirmed server result.
- Formal bid submission, sealed opening, evaluation, awards, PO conversion and Finance execution are later milestones. Preserve their existing sections with truthful availability; do not simulate those outcomes in a draft workflow.

## Acceptance evidence

Submit a focused UI PR with its exact tested commit, type checking, production build, adapter tests and Playwright results. Keep the existing 22 RFQ browser cases passing. Cover contact/create/revise/cancel, full terms retention, exact amounts, stale CAS, handler B versus maker A, users without employee links, read-only access, disabled capture, branch changes, lost responses and original-key-only recovery. Confirm that no duplicate POST occurs after lost acknowledgement or reload.

Use synthetic fixtures. Mark mocked transport/browser results explicitly; they do not prove persistence, authorization or native transaction effects. Coordinate the actual cookie/API/native-owner end-to-end journey with Root before claiming workflow UAT. Do not overwrite private databases, reuse production credentials, reset shared fixtures or deploy independently.

Keep the backend contract stable. If a real contract gap blocks the original layout or workflow, record the exact route, input/output and expected outcome on issue 148 for Root review. Do not bypass it with a local transaction or duplicate Core organization, identity, approval, currency or document infrastructure.

## Completion

The milestone is complete only when the preserved original UI supports the whole supported draft workflow, tests pass, PRs are reviewed/merged and Root reconciles the actual integration evidence. Keep whole-module issue 145 open. Report remaining controls as unavailable rather than declaring Procurement complete.
