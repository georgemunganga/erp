# Banking workflow depth

This document covers only the **Banking** workflow domain in the local React prototype. It models manual bank/credit-card accounts, provider-connection simulation, statement CSV intake, bank transactions, matching, categorization, rules, and reconciliation. The renderer supplies review, back, cancel, and completion states around each listed operational step.

All outcomes are fictional and browser-local. No credentials, provider authorization, network request, tenant operation, external payment, live sync, or bank-ledger mutation is implemented. Account, transaction, rule, integration, import, and reconciliation collections are local artifacts unless a flow explicitly links existing local invoice/bill records.

| Flow id | Steps | Validation | Persisted / linked result | Evidence |
|---|---|---|---|---|
| `create-bank-account` | Account details; opening position | Name, account type, currency, opening date, and balance are required | Creates a local `bankAccounts` artifact; no provider connection | **tenant-pattern** — Banking setup exposed Add Manually; form details are local concept |
| `create-credit-card-account` | Card details; opening position | Name, issuer metadata, network, currency, opening date, and balance are collected | Creates a local `bankAccounts` artifact typed as Credit Card | **tenant-pattern** — Banking setup exposed Add Manually; credit-card fields are local concept |
| `edit-bank-account` | Choose account; update details | Existing `bankAccounts` record, name, and currency are required | Edits the selected local account artifact | **local-concept** |
| `simulate-bank-provider-connection` | Choose provider; confirm simulation | Provider, local account, history window, and explicit local-only acknowledgement are required; no credential field exists | Creates an `Active` local `integrations` status artifact; no auth or sync occurs | **tenant-pattern** — Connect Bank/Credit Card was observed; connection behavior is simulated |
| `import-bank-statement` | Choose statement; map columns; preview rows | Local CSV text, account, required date/description/amount mapping confirmation, duplicate policy, and preview confirmation are required | Stores a mapped local `bankTransactions` import/preview artifact; no external import occurs | **local-concept** — import mapping and preview were explicitly unverified |
| `create-bank-transaction` | Transaction details; description | Account, date, direction, non-negative amount, and description are required | Creates a local `bankTransactions` artifact with an optional category | **local-concept** |
| `edit-bank-transaction` | Choose transaction; correct details | Existing transaction plus date, direction, amount, and description are required | Edits a local `bankTransactions` artifact | **local-concept** |
| `match-bank-deposit-to-invoice` | Choose deposit; choose invoices | Local transaction, invoice, and allocation rows (`recordId` + `amount`) are required | Links a local bank transaction to local `invoices`; no payment is sent or posted | **local-concept** — matching/allocation screens were unverified |
| `match-bank-withdrawal-to-bill` | Choose withdrawal; choose bills | Local transaction, bill, and allocation rows (`recordId` + `amount`) are required | Links a local bank transaction to local `bills`; no vendor payment is sent or posted | **local-concept** |
| `undo-bank-match` | Choose matched transaction; confirm undo | Existing transaction, reason, and explicit confirmation are required | Removes the local match link while retaining source records | **local-concept** |
| `categorize-bank-transaction` | Choose transaction; choose category | Existing transaction and category are required | Stores the selected category on the local bank transaction | **local-concept** |
| `uncategorize-bank-transaction` | Choose categorized transaction; confirm uncategorize | Existing transaction, reason, and explicit confirmation are required | Clears the local category via a reversible local action | **local-concept** |
| `create-bank-rule` | Rule identity; conditions and result | Rule name, account, direction, description condition, category, and active state are required | Creates a local `bankRules` artifact; it is not executed automatically | **local-concept** — rule creation/execution was unverified |
| `edit-bank-rule` | Choose rule; update rule | Existing rule, name, condition, category, and active state are required | Edits the selected local `bankRules` artifact | **local-concept** |
| `reconcile-bank-account` | Choose statement; select transactions; confirm reconciliation | Account, statement date/balance, allocations, and explicit confirmation are required | Creates a local `reconciliations` artifact linked to `bankAccounts` and `bankTransactions` | **local-concept** — reconciliation inputs were unverified |
| `undo-bank-reconciliation` | Choose reconciliation; confirm undo | Existing reconciliation, reason, and explicit confirmation are required | Marks the local reconciliation undone without deleting account or transaction artifacts | **local-concept** |

## Contract notes

- `create` is reserved for `DataKey` entities in the supplied contracts. Banking-specific collections therefore use the contract's `artifact`/`artifact-status` or banking-specific effect kinds with string `collection` values such as `bankAccounts`, `bankTransactions`, `bankRules`, `integrations`, and `reconciliations`.
- The statement flow uses the supplied `import` effect and a `csv` field type, with the banking collection carried in `collection` because `bankTransactions` is not a `DataKey` in the supplied type union.
- Invoice/bill matching uses `bank-match` with `entity` set to the destination `DataKey` (`invoices` or `bills`); allocation fields preserve `recordId` and `amount` rows.
- The provider flow records only a simulated local `Active` integration status. It never requests credentials, authenticates, schedules sync, sends a payment, or invokes a native feature.
