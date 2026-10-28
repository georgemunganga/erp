# M6B private native schema checkpoint — 2026-10-28

This records bounded private schema evidence at source `294fd99d`. It is not acceptance of the principal invoice-matching workflow or a production activation. Production remains API product `6087` with UI `47cac6a`; this checkpoint changed neither production schema nor configuration. `PrincipalSourcesEnabled` remains false by default.

The existing matching foundation can operate without Finance. This work binds Procurement evidence and claims; it does not establish AP posting, payment execution or a Finance integration acceptance.

## Why two issued parents are required

M5 principal-issued versions belong to `principal_order_issued_versions`. The original matching mapping, exposure and result tables referenced only `purchase_order_issued_versions`. Reusing a principal ID in a legacy parent field would fail the real database contract. No fake legacy rows or client-supplied authoritative parent were introduced.

B2 retains the effective `issued_version_id` and uses two stored generated UUID references per table: null source discriminator selects the legacy parent; source schema 3 selects the principal parent. Both parents are bound by tenant, legal entity, version ID and purchase-order ID through real `RESTRICT` foreign keys. Current principal/Core authorization and verified historical source evidence remain separate requirements.

## B1 evidence

Migration `20261028184527_M6bPrincipalInvoiceReceivingBindings` adds the two nullable typed allocation IDs, source/fact parent keys and scoped foreign keys, indexes and the allocation CHECK. The typed principal IDs do not populate legacy Goods receipt or Service acceptance database fields.

The genuine guarded EF export passed with no database access. Its first-Down retention guard precedes destructive DDL. Actual private Up preserved the captured old business-row fingerprints across 223 tables; only migration history changed.

Two controller failures are retained. Roundtrip run01 failed during helper import before database commands. Run02 committed Down, then failed its positional inventory comparison. Run03 compared inventories by table name, verified the retained post-Down state, completed Up and restored the full 223-table row inventory. It also inserted one synthetic audit marker inside a rollback-only transaction: the actual first-Down guard returned SQLSTATE `55000`, and the marker rolled back. The existing approved M6A baseline alone did not veto B1 Down.

This tests one audit-marker veto path. It does not exercise every retained-data veto family or prove a real principal invoice command.

## B2 evidence

Migration `20261028191905_M6bInvoiceIssuedParentBindings` was genuinely scaffolded and its Infrastructure build/export passed without database access. Its Up contains exactly:

- Three old direct issued foreign keys and three indexes removed.
- Eight columns added: six stored computed UUID references and two nullable source discriminators. The mapping discriminator already existed in M6A.
- One principal-issued alternate key, six replacement indexes and six scoped dual-parent foreign keys.
- Two changed CHECKs, retaining legacy execution-zero behavior and restricting the new result family to execution version 2.

There is no other business DML; the only explicit row write is EF migration history. The generated columns derive their values from retained identifiers.

Actual private Up completed on the same 223-table test database. Captured old business-row fingerprints were preserved, using the reviewed projection for newly added/derived fields; only migration history changed. Native generated expressions and CHECK definitions for all three matching tables were captured. The admission query proved zero pre-existing schema3 mappings. This is a concrete preflight result, not universal compatibility for every row valid under an earlier CHECK. Existing schema3 mappings elsewhere require genuine principal-parent reconciliation before Up.

The genuine combined B1/B2 export subsequently included both first-operation retention guards in their migration order. Actual guarded combined Down and Up completed on the private target. The name-keyed 223-table old-business-row inventories were restored exactly; only migration history differed after Down. The existing approved M6A baseline alone permitted Down.

A separate rollback-only transaction inserted exactly one synthetic schema3 exposure with a genuine principal-issued foreign key. The independent B2 exposure/result guard returned SQLSTATE `55000`, and the exposure rolled back; the complete row inventory stayed unchanged. This proves that one relational exposure veto path, not all retained-data veto families or a real matching command. Raw unguarded Down remains unsafe and was not executed.

## Evidence pins

The adjacent JSON records complete SHA-256 pins for the protected receipts. It contains no credentials, payloads or private identity values.

| Evidence | Receipt SHA-256 prefix |
| --- | --- |
| B1 guarded EF export | `6260a3ade70d` |
| B1 native Up | `9196aa0d314f` |
| B1 recovered Down/Up and audit veto | `6e541d394aaa` |
| B2 genuine EF scaffold/export | `0285191eb0c9` |
| B2 native Up | `15548d77353a` |
| Combined guarded B1/B2 Down/Up and exposure veto | `1734878ce96b` |

## Remaining acceptance

Integrated SDK run01 for source `294fd99d` exported the genuine guarded SQL and built the product, then failed while compiling a Procurement test fixture that omitted the optional acceptance identity constructor argument. The failed receipt is retained. Commit `6d212b2b` corrects that fixture. SDK run02 was running the full suites at this update; no successful full-suite result is claimed here. Native principal mapping insertion, retained unit-review authority, Compare/claim creation, capacity enforcement and original-key recovery remain workflow acceptance work. Document-backed review must use the real shared Core Documents owner; any fileless review needs its explicit policy/evidence contract. Neither path is accepted by this schema checkpoint.

No full UI, multibranch, concurrency, complete veto-family, Finance or production workflow sign-off is claimed.

## Later SDK and private fixture boundary checkpoint

SDK05 at source `9137e5a3d05f5fba7a96c38c19e04ab1044cb446` subsequently passed 1,559 Procurement tests and 304 Host tests with zero skips, no pending model changes and a 150-file publication. Its result (`ea64c6dd0a97`) and publication manifest (`a20746e969c5`) were independently checked against all listed output hashes, actual TRX counters and every published file size/hash. This is retained build/schema evidence; it does not imply a positive principal matching command.

Before any private API startup, fixture setup attempt01 failed at `ExactGrantDeltaChanged`, before its GRANT statements or COMMIT. The captured execution role already had column `UPDATE(id)` on `supplier_invoices` for locking; that is distinct from table-level UPDATE. The initial fixture's generic any-column negative predicate therefore rejected the actual profile. The failed attempt and original SQL are retained. The owner reconciliation receipt (`ba92041a2838`) records all 223 business-row and complete captured security/ACL/global-role sets equal to the before capture (`a74577e6422a`), with zero cohort sessions. No business HTTP or production change resulted.

The source successor requires only the nine actual mapping/comparison and shared audit-stamp invoice columns, explicitly rejects table-wide invoice UPDATE, and preserves the existing `UPDATE(id)` grant. Its separately reviewed private fixture successor proposes 16 SELECT, 6 INSERT, 2 table UPDATE edges and 9 named column UPDATE edges. Restore removes only those introduced edges and restores exact original role fields using observed setup timestamp guards. Source/SQL review is not an assertion that this successor was applied or accepted.

The column-readiness successor requires its own SDK06 or later proof before runtime admission. SDK05 and its verified private image remain historical accepted artifacts; they do not waive that new gate. Native mapping/receipt/selector/denial and owner-after evidence remain pending. No qualified comparison, full UAT or production activation follows from these checkpoints.

## SDK06 and real private fixture admission

Source `7f44d5f37be73579b7f5daa63c8e63402869986f` passed SDK06: 1,559 Procurement and 304 focused Host tests, zero failures/skips, no pending model changes, genuine guarded EF exports and all 150 publication files. The result SHA is `3eb7a06f1e99a1a32df3ad6fda392531dddc70ff0ba6db0fd501d2f90565c939`; publication manifest is `fe7baea47f0d9f820e1c7838deb31081e7719b499eb4be2c6016e7b454ecb262`. Hosted CI remains unavailable; this is pinned local SDK evidence.

The narrowed private setup actually committed and independently passed owner verification (`a6744d616e015dfe2eef529ba43d13b0c47bfe812985892e742a7182a04c2cd4`). All 223 business-table inventories remained unchanged except the two expressly reviewed temporary role rows; the exact 24 table-level and nine column-level privilege additions were reconciled. Table-wide invoice UPDATE remains absent, existing UPDATE(id) remains intact, and all other captured role/global/default/schema/database grants were unchanged. These are temporary private-test permissions requiring exact restoration after the journey.

The actual read-only owner-before capture (`d9708e40c541db1975467433cf0a1c0a68bab031addbfdb1e9505324ab4065fd`) proves one existing approved baseline, zero matching policies, one genuine accepted Goods source and zero synthetic sessions. It does not create a replacement baseline or seed a policy. A prior read-only query syntax failure is retained separately; only the corrected capture is accepted.

Private image05 (`sha256:9fa10ccf5d5985b6040e87ce9dd09e427ddaa2d852c2105635409d8b9421ce61`) was built from SDK06 and all 150 image publication bytes verified against the manifest through a stopped-only verifier. Image receipt: `bca9ce869da03116d3ea662d5cfb9ed88bf818c1999337a473ab1ff481c72f0f`. Production identities were unchanged. This image is a private candidate, not a production rollout or completed business UAT.
