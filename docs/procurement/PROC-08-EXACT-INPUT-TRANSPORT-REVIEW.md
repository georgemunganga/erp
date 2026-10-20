# PO exact input transport correction

**Status:** Source authored, no SDK/test/DB/browser execution in this worktree. Base5278267; independent source/restricted real HTTP verification remains required before adoption. No schema changes.

## Defect

Legacy OrderDraftRoutes.Validate checked range but not four-place precision. Source conversion/resizing also did not reject excess tax precision. A tax12.34567 could be accepted in memory then coerced by PostgreSQL numeric(18,4), while response/audit/command fingerprints retained a different value. Default JSON decimal binding can also round a long numeric lexeme or underflow a tiny value before a domain scale check sees it.

## Correction

- Shared `ExactDecimalJsonCodec` reads original bounded numeric/string lexemes, validates decimal/exponent syntax and SQL precision/scale, normalizes only exact trailing zeros and constructs System.Decimal from its96-bit coefficient. No decimal.Parse/double conversion or silent rounding. Tokens128bytes and exponents±1000 are bounded before coefficient expansion.
- Existing AuthorityAmountJsonConverter delegates parsing to that codec with18,4 and retains string output/Format behavior.
- Quantity/rate/tax properties on actual OrderDraftLineInput use exact18,4 converters, covering legacy create/update plus approved-source conversion/source edit. Legacy PO output remains numeric. Draft validation and allocation Reserve/Resize also defend directly constructed DTO/domain values before writing counters.
- Finance confirmed/released gross properties use38,8 SQL bounds **plus exact CLR representation**, not18,4. Canonical string1E-08 is accepted; unsupported scale/unrepresentable coefficient/underflow are rejected before handler business effects. Both terminal and unsubmitted Finance release routes use the same input record.

## Authored verification

Application tests cover numeric/string/exponent equivalence, line excessprecision/long significant digits/tiny lexemes, gross8dp, CLR boundaries, token/exponent bounds, numeric output and allocation tax rejection before reservation/resizing mutation. Host DTO tests use the actual route records.

New `OrderExactInputVerification` is called by the existing `--orders` actual restricted PostgreSQL HTTP probe. It sends raw JSON lexemes through real mapped legacy create/update, source conversion/edit and Finance confirmation/both release routes; requires400 and unchanged persisted orders/lines/source versions/counters/allocations/receipts/Finance evidence/ownership/audit/outbox. A real legacy positive create+reload proves numeric compatibility. A separate positive source PO whose gross is1e-8 confirms and releases using string1E-08 and verifies actual stored8dp evidence.

These are authored tests/probe assertions, not execution results. Existing authority settings/parser, PO exact arithmetic, Finance workflow, current-scope/replay and host regression suites must pass after review. Root controls constrained host jobs; no production writes or migration generated here.
