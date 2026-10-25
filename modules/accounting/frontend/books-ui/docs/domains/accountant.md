# Accountant and tax

> Evidence status (2026-10-20): The selected Global organization reached the Books dashboard and authenticated reference exploration has begun. The candidate workflows below remain public-document research, not tenant proof; observed screens and action boundaries are in [verified patterns](../verified-screen-patterns.md) and [interaction coverage](../interaction-coverage-plan.md). No tenant accounting record was created or changed; a temporary report Favorite toggle was reversed and verified empty.

**Public-doc candidate note only.** Later tenant observations include the Manual Journals list and blank form, Chart of Accounts, Budgets and Transaction Locking screens; those are cataloged separately. This public help does not prove the remaining accountant/tax workflows or their Global behavior.

## Candidate workflows and screens

- **Manual journals — source-supported candidate.** The US help page documents Accountant → Manual Journals, a new-journal form with date, journal number, notes, reporting method, currency, and account/contact/transaction-type/tax/debit-credit lines; debit and credit totals must balance. Save as Draft or Save and Publish. Drafts do not affect accounts until published. The source also documents a journal list, Details/PDF views, and an Activity Logs tab. [1]
- **Journal import — source-supported candidate.** The documented path is Manual Journals → More → Import → Import Journals, followed by file selection, encoding/delimiter and number options, field mapping, preview, then Import. This is a documented path, not authorization to import or change tenant data. [1]
- **Generic screen families supported by the journal documentation:** module list; create/edit form; record details with Details, PDF, Activity Logs and (where applicable) Related List views; import/file-selection; field mapping; import preview; confirmation/modal or settings dialog. Treat these as candidate families, not verified Global UI. [1]
- **Other in-scope workflows—unverified candidates:** chart-of-accounts review/edit, opening balances, transaction lock date, multicurrency setup, and edition-specific tax/VAT/GST setup. The source used here does not establish their screens or steps. Although manual-journal documentation includes Currency and Tax fields, that alone does not verify organization-wide currency setup or regional tax behavior. [1]

## Capture boundary and dependencies

For a future approved, dashboard-ready capture, keep the work observational and read-only: inspect only visible screens and labels; do not create, edit, publish, import, delete, configure, or confirm changes. Do not use MCP/API or authenticated pages to inspect tenant records. Stop if the required dashboard-ready signal is absent. Any account, opening-balance, lock, currency, or tax workflow remains contingent on confirming that it exists in the Global edition and is visible for this organization and plan.

The cited page identifies itself as **US-EN** and lists plans from Free through Ultimate; its header offers edition/language navigation including Global, but this US-localized page is not proof that the same behavior or UI applies to Global. Regional VAT/GST details and plan-specific availability should therefore be treated as unresolved. [1]

## References

[1]: https://www.zoho.com/us/books/help/accountant/manual-journal.html "Manual Journals | Zoho Books Help"
