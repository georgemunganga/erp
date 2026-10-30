# M6C private workflow checkpoint — 29 October 2026

This extends the [28 October checkpoint](M6C-PRIVATE-BUILD-SCHEMA-CHECKPOINT-20261028.md). Earlier receipts remain historical evidence. Backend source is `0f656d6316e39063eb09da45210db4b142717ecf`; the original UI remains `56fbd3b90a6f57854717a6dfc8e55203108a1312`. No production rollout or complete Procurement UAT is claimed.

## Implemented and verified

- `4d56ccfb` fixes manual unit review against principal-issued orders: it reads retained approved demand through the owning transaction instead of requiring unrelated legacy order evidence. Legacy guards remain in place.
- `0f656d63` fixes baseline approval-audit validation after PostgreSQL JSONB formatting. Canonical comparison preserves field names, types and values and rejects duplicate keys; it does not bypass approval evidence.
- SDK run05 directly passed 1,592 Procurement tests, 671 host tests and one focused regression, with zero failed/skipped tests. Build/analyzers, genuine EF exports, no pending EF model changes and all 150 published API files passed.
- The original UI performed manual Open, lost-response recovery by the original key, and an independent reviewer Decision. A mistaken test expectation about a queue tab caused the first browser continuation to fail; the corrected reviewer-only continuation passed without changing product UI. The composite explicitly joins separate sessions and preserves the original failed results.
- Native reconciliation proves one source-schema-4 review, one verified outcome and approved Core decision, with exactly two review receipts and no duplicate review effects. Workerless Core users completed these actions under scoped permissions.
- Compare returned HTTP 200 with a saved **Blocked** result and `InvoiceHeaderAmountInvalid`. Native reconciliation and original-key GET recovery agree: one comparison receipt and exception, zero allocations or qualified claims, and no Finance posting. The comparison harness expected qualification and remains recorded as failed; the separate negative-control verification passed.
- Genuine guarded Down rejected retained evidence with SQLSTATE `55000` before DDL. All 223 table rows, captured security and catalog remained identical. Both source-schema-4 evidence and published policy-schema-2 evidence were present; this proves the combined retained-data guard, not isolated execution of the source-4 predicate.
- With new manual review work disabled, normal-cookie API GETs recovered the original Open receipt, verified Decision, blocked Compare receipt and retained review detail. Metadata reported read access enabled and new work disabled. No business POST was sent; this is API historical-read acceptance, not a flags-off browser or write-denial test. Independent native reconciliation found all 219 non-auth table digests, security and catalog unchanged; the exact four auth-table deltas matched two normal logins/logouts, with zero active sessions.
- Cleanup restored both original private role rows, removed exactly 34 temporary table-grant edges and nine invoice-column grants, and preserved every other table's rows and owner history. The verifier checked all 223 tables and the original security profile; no synthetic session remains active. The private API, proxy and watcher stopped cleanly.

## Remaining work

The retained invoice contains quantity 1 × unit price 5.1234 and exact stored net 5.1234. Its matching policy requires currency amounts at two decimals. The block is correct; changing the matching tolerance or editing historical rows would hide the capture gap.

New invoice calculation must preserve precise quantities/rates while deriving currency-rounded line and header amounts from a Core-owned precision reference. The [Core prerequisite](../../core/CORE-CURRENCY-PRECISION-20261029.md) and [M6D specification](M6D-NEW-INVOICE-CURRENCY-CALCULATION-V2-20261029.md) describe this additive work. They are specifications, not implemented functionality.

Mapped/reviewed invoices also need a controlled correction or replacement workflow. Current draft editing intentionally excludes records with processing history. Preserve the original invoice, review, blocked result and receipts while implementing that workflow separately.

Qualified comparison/allocation acceptance, release review and production activation remain open. The adjacent JSON pins the completed evidence without publishing private records or credentials.
