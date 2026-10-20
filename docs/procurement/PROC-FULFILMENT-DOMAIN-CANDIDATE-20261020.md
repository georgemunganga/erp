# Procurement fulfilment domain candidate — 20 October 2026

**Status:** Consolidated source only. Not built, tested, deployed or operationally enabled.
**Base:** Reviewed PO integration checkpoint `92abe51`.
**Branch:** `codex/procurement-fulfilment-domain`.

## Narrow source provenance

| Original source | Content retained |
|---|---|
| `be08e8a` | Receiving 28-section contract and audited UI/API dependencies |
| `8de9c5c` | Controlled receiving domain facts/application guards and authored tests |
| `7b46e05` | Exact arithmetic, correction requester/authorizer evidence, chronology and tests |
| `59d3c71` | Rehydrated correction actor independence and corruption regression |
| `daaddd4` | Required invoice foundation gap-map predecessor; documentation only |
| `796a474` | Pure invoice comparison contracts, exact ratio arithmetic and authored tests |
| `0feb1da` | Cumulative commercial credit/value restoration correction and regressions |

No unrelated ancestor history was merged. The one documentation conflict appended two
independent PROC-08 audit sections; both were preserved, with a note distinguishing the
inherited branch-fence implementation from its earlier audit backlog. Source code and test
files match the reviewed receiving and invoice checkpoints byte-for-byte. The existing
`ExactDecimalArithmetic.cs` is identical to `7b46e05` and remains one shared file.

## What this candidate provides

- Pure guarded physical goods, inspection, service acceptance and linked correction facts,
  exact representable values, complete-ledger checks and retained worker evidence.
- Pure two/three-way invoice comparison, partial debit/linked-credit conservation,
  configured field/category tolerances, immutable comparison projections and fingerprints.
- Existing 28-section receiving/invoice contracts, open dependency maps and regression source.

The invoice rational/rounding helper remains specialized to explicit monetary rounding;
shared exact decimal arithmetic serves existing decimal-valued PO/receiving operations.
These are distinct calculation responsibilities, not duplicate operational ledgers.

## Required centralized gates

Root owns verification after the current PO migration/execution gates. Run the combined
Procurement domain suite first, inspect any source integration failures, then schedule the
separate runtime contracts. No prior authored-test count is passing evidence for this branch.

Receiving still needs policy/classification resolution, persistence/append-only constraints,
command authorization and replay, current complete consumption/owner adapters, API and real
PostgreSQL concurrency evidence, plus existing UI integration/Playwright.

Invoice matching still needs authoritative issued/accepted receipt adapters, exact invoice
source-line/UOM bindings, locked complete debit/credit/duplicate consumption, immutable
comparison persistence and command recovery. Finance/AP posting remains Finance-owned and
requires its separately verified handoff/reconciliation contract.

No EF registrations, database migrations, HTTP endpoints, UI edits, runtime capability flags,
Finance ledger or payment execution are added by this consolidation. The whole Procurement
module remains incomplete. Existing issue #30 (matching) and #31 (receiving) remain open.
