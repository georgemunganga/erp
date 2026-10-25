# Banking and cash

> Evidence status (2026-10-20): The selected Global organization reached the Books dashboard and authenticated reference exploration has begun. The candidate workflows below remain public-document research, not tenant proof; observed screens and action boundaries are in [verified patterns](../verified-screen-patterns.md) and [interaction coverage](../interaction-coverage-plan.md). No tenant accounting record was created or changed; a temporary report Favorite toggle was reversed and verified empty.

Zoho’s public Banking overview describes a flow from adding accounts and obtaining bank feeds or importing transactions, through matching or categorizing transactions, to account reconciliation. The overview also lists transaction rules, but does not document their behavior in detail. [1]

## Candidate workflows and screen families

- **Accounts and feeds (documented):** Banking → Add Bank or Credit Card → connect an available bank/card through a feed provider, or add an account manually. The account guide names feed-provider selection, consent, bank selection/authentication, account and date selection, and currency selection. It says feed history can be fetched for up to 90 days; older history can be imported manually. [2]
- **Statements and transaction intake (listed, details unverified):** Add Transactions includes manual entry, transaction import, and undo-last-import. The overview does not specify the import format, mapping, or exact screens. [1]
- **Matching and categorization (listed, detailed steps unverified):** The overview names matching, manual categorization, adjustments while matching, matching deposits to withdrawals, multi-statement matching, and undoing a match/category. Treat these as candidates, not verified interface behavior. [1]
- **Rules and reconciliation (listed, detailed steps unverified):** Transaction Rules lists add, edit, and delete; Reconcile Accounts lists reconcile, undo, and delete reconciliation. No rule conditions, reconciliation inputs, or screen details were verified in the pages read. [1]

For planning, use generic shared screen families: account/feed setup; transaction list/intake; match-or-categorize review; transaction-rule management; reconciliation. These are navigation/workflow groupings, not claims about exact Global UI layouts.

## Boundaries and dependencies

This file is public-document candidate research, not evidence of the selected Global tenant’s full banking workflow. Later tenant observations verified the Banking initial setup screen, but no bank or credit-card account was connected, no statement was imported, and no transaction was matched or reconciled. Any further tenant inspection remains non-mutating; see the current reference inventory and interaction boundary docs.

The fetched help pages show a **US-EN** header and US links/support details, while offering a Global English edition selector. They are therefore not evidence that the displayed workflow or availability matches the selected Global organization, and public documentation cannot verify that tenant’s screens. The account page displays plan labels from Free through Ultimate, but the retrieved text does not establish Global-specific plan availability for the other candidate workflows. [1] [2]

## References

[1]: https://www.zoho.com/books/help/banking/ "Zoho Books Banking help overview"
[2]: https://www.zoho.com/books/help/banking/add-accounts.html "Zoho Books Add Accounts help"
