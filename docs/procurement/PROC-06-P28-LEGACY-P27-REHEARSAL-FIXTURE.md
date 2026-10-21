# Pre-P28 legacy RFQ cancellation fixture

Status: source authored, uncompiled and unexecuted. No migration or production changes.

## Purpose and boundary

P28 rollback must preserve genuine pre-P28 `SourcingEvent` cancellation audit. An old
`DraftCancelled` is not governed P28 execution evidence. This private fixture builds a
**used P27** source database, rather than inventing history or editing audit rows after
P28 installation. It supports the later distinct-reviewed P28 guard correction
`49f0b56`; that correction and all P28 models/migrations are deliberately absent here.

Base: `fb03a7d`, actual Procurement migration tip
`20261020190554_P27FinanceCheckerIdentity`.

## Existing harness contract

Run the existing `ApprovalHttpPostgresProbe` with standalone `--legacy-sourcing`.
It rejects combinations with `--serve` and all other modes. Existing modes are unchanged.
The existing guarded `proc_approval_http_<unique>` database and separate
`PROCUREMENT_APPROVAL_HTTP_PROBE_CONNECTION` runtime / 
`PROCUREMENT_APPROVAL_HTTP_PROBE_BOOTSTRAP_CONNECTION` owner are mandatory.
Never print either connection string. Root controls later serialized build and fresh
private PostgreSQL execution; no DB resume, production database or new server mode.

The harness applies actual committed Procurement and Core approval migrations and
requires no pending model changes. It reuses the existing synthetic Core bootstrap
only; it does not call Procurement `CreateTables`, forge migration history or claim
production readiness. Runtime is a finite login with no ownership/role memberships.
The new optional legacy flag grants `SELECT,INSERT,UPDATE` only on the existing
`SourcingEvent` and `SourcingLine` mapped tables in addition to existing PO fixture
grants. Explicit runtime DELETE denials cover both new grant targets. No new Core
write grants are introduced. Real route authorization still applies.

## Ordered verification

1. Existing Finance identity/readiness verification and full `VerifyInitialOrders`
   execute unchanged. Real mapped HTTP performs approved PR allocation, independent
   manual Finance evidence, Core PO approval, sealed issue and delivery confirmation.
   This makes the P27 database genuinely used. Manual evidence remains synthetic;
   it is not external Finance verification or a Finance ledger.
2. Before legacy RFQ mutation, a fresh owner context requires the exact P27 migration
   tip, retained issued PO and Finance evidence. Actual sourcing table column sets
   must equal the pre-P28 model exactly; P28 columns are not tolerated.
3. Snapshot every other Procurement table's sorted complete row-content fingerprint
   (including migrations, allocations, Finance, receipts, invoices, outbox and
   approval evidence). Retain every existing AuditEvent's complete serialization.
4. Call real legacy POST `/api/procurement/v1/sourcing` as the existing buyer.
   Create two ad hoc unlinked lines (including exact quantity `1.0001`), no source
   demand, invitations, publication, supplier selection or P28 receipt.
5. Call real legacy POST `/sourcing/{id}/cancel` with returned current version and
   explicit reason. GET detail and inspect independently through a new owner context.
6. Require cancelled event, unchanged original line IDs/quantities, retained cancelled
   lines, null PR-line links and exact company/tenant/owner. Require the real
   `SourcingEvent`/`DraftCancelled` audit with `before.Status=Draft`,
   `after.Status=Cancelled`, both line arrays and original reason. No audit rewrite.
7. Require all old audit rows byte-equivalent under the same serializer, exactly eight
   added audits (two explicit draft audits plus six existing automatic entity audits),
   only real event/line IDs and existing `create`, `update`, `DraftCreated`,
   `DraftCancelled` actions. Every unrelated Procurement table fingerprint must match.
8. Existing final Finance submission guard proof still runs. Retain complete terminal
   logs and DB. The fixture prints retained event/audit IDs and cancellation payload
   SHA256 for later baseline comparison; no secret data is printed.

## Later migration rehearsal gate (not run by this source)

On a private owner-controlled copy of the successful used-P27 database, retain full
baseline legacy event, line, audit JSONB/IDs/content hashes and unrelated evidence.
Apply reviewed P28 Up, then unused-P28 Down to P27, then Up again. Require the genuine
old cancelled RFQ and DraftCancelled payload unchanged at every checkpoint. P28 tables
must remain unused for the rollback portion. Separately, real governed P28 activity
must still veto Down; do not weaken used-evidence guards merely because old draft
cancellation is allowed. Parent owns SQL, migration provenance and actual execution.

## Acceptance limits

Source-only fixture does not prove compilation, grants, API behavior, PostgreSQL
rollback preservation or production migration safety. Those require later actual
restricted-runtime logs and independent owner read-back. It creates no P28 states,
new models, financial balances, stock transactions or production capability flags.


## Parent execution evidence — PASS21October2026

Sourcecdb1c0c Release probe build succeeded (0errors;2existing nullable warnings), then the actual standalone --legacy-sourcing process terminated0 against a new private P27 database with distinct bootstrap/runtime identities. The real funding/readiness and full existing PO regression suite passed first. Actual legacy create201/cancel200/GET passed, with fresh independent owner verification retaining two line IDs, exact1.0001 quantity and null source links, exact old DraftCancelled before/after payload, unchanged previous audit records and unchanged unrelated Procurement tables. The final actual P27 unsupported-submission-discriminator CHECK negative also passed.

Evidence: /home/amizo/erp-backups/procurement-p27-http-legacy-20261021_070635/, including source, binary hashes, logs, result and process-exit-code0. Retained cancellation payload SHA256b5445ef27feeebb89b12418a26ba31232b8b101e44e94e7e63bb3ae6dbdada3d. The source database is now closed normally and retained for separate P28 clone rehearsal; this pass is not an actual P28 upgrade, governed RFQ authority or production release.
