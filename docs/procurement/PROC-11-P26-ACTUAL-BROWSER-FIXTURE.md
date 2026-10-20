# P26 actual Bills browser fixture — tools implementation record

Status: tools-only source authored/uncompiled. No SDK, Node, database, scanner process or browser job started. No migration/model/production source change. User-facing comparison remains unable to create AP authority. Related UI contract/tests: module-connect PR34, source39052e5; actual runtime verification remains pending. Releasehold module-connect#29 unchanged.

## Source lineage and bounded first slice

Isolated branch codex/procurement-matching-browser-fixture at /home/amizo/procurement-matching-browser-fixture, based d2ea852 plus reviewed intake2c9f301 cherry-picked as a96acce. Root's current P26 compile worktree is untouched.

First slice prepares existing Core document owner/runtime registration only for future serve+matching: existing DocumentDbContext migrations, exact same physical runtime DB configuration, fresh private blob directory and a tools-only exact-byte scanner replacing the normal Clamd service only in that isolated fixture. Existing CLI still rejects serve+matching until the next source slice adds complete bootstrap/manifest/verifier; no incomplete fixture is advertised as usable.

KnownFixtureOnlyDocumentScanner accepts only the generated harmless PDF byte sequence AND its expected hash/length, rejecting altered/appended/wrong-metadata input. It reports a TEST-ONLY engine and synthetic definitions. Startup pure assertions exercise those cases. This is NOT malware protection, original-unit review or production document validation. Core RecordDocumentService still registers Quarantined, saves actual bytes, starts the scan, records scanner evidence and controls Available state. Nothing seeds Available metadata. No InvoiceMappingEvidenceReviewer/HistoricalExposureOwner replacement exists.

Runtime grants are narrow: core_document usage/historySELECT; documents/versions/scan_evidence/record_events SELECT,INSERT; versions UPDATE only the seven actual scan lifecycle/concurrency columns resolved from the owning model. No retire/content edits, DELETE, schema CREATE, table ownership, role memberships or HRM privilege changes. Procurement attachment refs gain SELECT,INSERT for the real upload route. Dedicated denied-write assertions test all Core document identity updates/deletes and DDL in rolled-back transactions. Existing domain grants/denial checks stay intact. Owner, not runtime, applies existing Core document P01/P02 migrations. Existing Procurement P26 model mismatch still blocks serve until root generates/rehearses its central migration.

## Next source slice (same reviewed UI contract)

Allow exactly --serve --matching without changing existing CLI modes; bootstrap one real issued/accepted goods source and independently published current matching policy through mapped commands; then create separate finite mapper/matcher/reader/otherBranchMatcher identities with exact scoped grants. Do not invoke receiving browser role narrowing or used nonserve matching verification. Initial invoice/mapping/result/document rows must remain empty.

Manifest exposes only synthetic source/policy/company/actor references and generated PDF bytes/hash; actual invoice/document/version IDs are created by UI through their owners. On graceful shutdown a fresh owner context independently verifies one original invoice/line, real Core document bytes/lifecycle, Unverified mapping, one Blocked comparison/receipt, responsible exceptions/audit/outbox, no claims/AP/payments, source counters unchanged and read-only effects unchanged. Full proposed manifest and two serial transport-only browser cases are in UI tests/backend-browser/INVOICE-MATCHING-CONTRACT.md. Tests are authored, not executed.
