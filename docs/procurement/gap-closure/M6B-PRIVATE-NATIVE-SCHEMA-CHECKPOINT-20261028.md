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
