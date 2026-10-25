# Accountant workflow depth

The Accountant domain is implemented as local workflow definitions only. The shared renderer supplies review, back, cancel, and completion handling. No workflow sends data, posts to a tenant, calls an accounting API, schedules work, or connects to a live tax/currency provider.

| Flow id | Steps | Validation | Persisted / linked result | Evidence |
|---|---|---|---|---|
| `create-account` | Account details; account settings | Name, code, and account type required | Creates an artifact in `accounts` with the chart-of-accounts details | local-concept |
| `edit-account` | Choose account; update details | Same-collection account record, name, code, and type required | Edits the selected `accounts` artifact; history remains local | local-concept |
| `disable-account` | Choose account; reason | Same-collection account record and reason required | Applies `disable` to the selected `accounts` artifact without deleting history | local-concept |
| `set-opening-balances` | Opening entry; balance accounts | Date and description required; at least two journal lines; debit/credit totals must balance | Creates a local `journals` opening-balance entry linked to account lines | local-concept |
| `create-journal` | Journal details; journal lines; notes | Date, journal number, and at least two journal lines required; debits must equal credits | Saves a local balanced journal draft in `journals` | tenant-pattern (blank manual-journal entry structure observed) |
| `publish-journal` | Choose journal; publishing check | Journal record, publish date, confirmation, and balanced source entry required | Marks the selected local journal `Recorded` with action `publish`; no external posting | tenant-pattern (manual-journal publish control observed) |
| `reverse-journal` | Choose journal; reversal lines | Journal record, reversal date, reason, and at least two balanced reversal lines required | Saves a local reversal journal linked to the selected journal | tenant-pattern (manual-journal reversal controls observed) |
| `create-budget` | Budget details; plan amount | Name, period, category, and non-negative amount required | Creates a local `budgets` record with period and planned amount | local-concept (budget creation entry was not observed) |
| `lock-transactions` | Lock scope; lock reason | Accountant scope, cutoff date, reason, and confirmation required | Applies `lock-accountant-transactions` to local `settings`; no tenant lock | tenant-pattern (Transaction Locking settings/control structure observed) |
| `unlock-transactions` | Unlock scope; unlock reason | Accountant scope, confirmation, and reason required | Applies `unlock-accountant-transactions` to local `settings`; no tenant unlock | tenant-pattern (Transaction Locking settings/control structure observed) |
| `configure-taxes` | Tax definition; tax timing | Tax name, non-negative rate, treatment, and effective date required | Saves a local `settings` artifact with tax configuration | local-concept (tax configuration entry was explicitly unverified) |
| `configure-currencies` | Base currency; display preferences | Base currency, exchange-rate mode, and non-negative decimal precision required | Saves a local `settings` artifact; no live rates or provider integration | local-concept (currency configuration entry was unverified) |

## Contract notes

- Account operations use `artifact` / `artifact-status` with the `accounts` collection because `accounts` is not a `DataKey` in the supplied types.
- Budgets use `create` with `entity: "budgets"`.
- Opening balances and journal creation/reversal use `journal` with `entity: "journals"` and required `journal-lines` fields. The local completion copy deliberately says “recorded,” “saved,” or “marked” rather than claiming a real ledger post.
- Journal publishing uses the known typed status `Recorded` from `DemoStatus`; the action is `publish`. No unsupported status such as `Published` is introduced.
- Lock and unlock are local `artifact-status` actions on the `settings` collection. Tax and currency setup are local `artifact` actions on the same collection.
- Record pickers for artifacts use the same collection as their effect. Journal-line rows select local accounts and carry `description`, `quantity`, `rate`-compatible row values through the shared renderer's journal-line handling.
- Evidence is marked `tenant-pattern` only where the supplied evidence described an observed Accountant entry/control structure: manual journals and Transaction Locking. Unverified tax, currency, account-entry, opening-balance, and budget details remain `local-concept`.
