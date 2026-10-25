import type { FlowDefinition } from "../contracts";

export const flows: FlowDefinition[] = [
  {
    id: "create-account",
    domain: "Accountant",
    title: "Create account",
    description: "Add a local chart-of-accounts entry with its classification and starting details.",
    steps: [
      {
        title: "Account details",
        description: "Give the account a recognizable name and code.",
        fields: [
          { id: "name", label: "Account name", type: "text", required: true, placeholder: "e.g. Office equipment" },
          { id: "code", label: "Account code", type: "text", required: true, placeholder: "e.g. 6100" },
          { id: "category", label: "Account type", type: "select", required: true, options: ["Asset", "Liability", "Equity", "Income", "Expense"] },
        ],
      },
      {
        title: "Account settings",
        description: "Add an optional description before saving the account locally.",
        fields: [
          { id: "description", label: "Description", type: "textarea", placeholder: "How this account is used" },
        ],
      },
    ],
    effect: { kind: "artifact", collection: "accounts", action: "create" },
    completionTitle: "Account created locally",
    completionMessage: "The account was added to the local chart of accounts and is ready to use in this workspace.",
    nextFlowIds: ["edit-account", "set-opening-balances"],
    evidence: "local-concept",
  },
  {
    id: "edit-account",
    domain: "Accountant",
    title: "Edit account",
    description: "Update the name, code, classification, or description of a local account.",
    steps: [
      {
        title: "Choose account",
        description: "Select the account to update from the local chart of accounts.",
        fields: [
          { id: "record", label: "Account", type: "record", collection: "accounts", required: true },
        ],
      },
      {
        title: "Update details",
        description: "Apply the revised account information.",
        fields: [
          { id: "name", label: "Account name", type: "text", required: true },
          { id: "code", label: "Account code", type: "text", required: true },
          { id: "category", label: "Account type", type: "select", required: true, options: ["Asset", "Liability", "Equity", "Income", "Expense"] },
          { id: "description", label: "Description", type: "textarea" },
        ],
      },
    ],
    effect: { kind: "artifact", collection: "accounts", action: "edit" },
    completionTitle: "Account updated locally",
    completionMessage: "The selected local account now uses the revised details.",
    nextFlowIds: ["disable-account", "set-opening-balances"],
    evidence: "local-concept",
  },
  {
    id: "disable-account",
    domain: "Accountant",
    title: "Disable account",
    description: "Remove an account from active local selection without deleting its history.",
    steps: [
      {
        title: "Choose account",
        description: "Select the account that should no longer be active.",
        fields: [
          { id: "record", label: "Account", type: "record", collection: "accounts", required: true },
        ],
      },
      {
        title: "Reason",
        description: "Record why the account is being disabled.",
        fields: [
          { id: "reason", label: "Reason", type: "textarea", required: true, placeholder: "No longer used" },
        ],
      },
    ],
    effect: { kind: "artifact-status", collection: "accounts", action: "disable" },
    completionTitle: "Account disabled locally",
    completionMessage: "The selected account is marked disabled in the local chart of accounts; its existing history remains available.",
    nextFlowIds: ["create-account", "edit-account"],
    evidence: "local-concept",
  },
  {
    id: "set-opening-balances",
    domain: "Accountant",
    title: "Set opening balances",
    description: "Record a balanced opening journal for the selected accounts.",
    module: "journals",
    steps: [
      {
        title: "Opening entry",
        description: "Set the opening date and explain what the entry represents.",
        fields: [
          { id: "date", label: "Opening date", type: "date", required: true },
          { id: "description", label: "Description", type: "textarea", required: true, placeholder: "Opening balances at conversion" },
        ],
      },
      {
        title: "Balance accounts",
        description: "Enter at least one debit and one credit; the local review checks that total debits equal total credits.",
        fields: [
          { id: "lines", label: "Opening balance lines", type: "journal-lines", required: true, min: 2, collection: "accounts", help: "Choose an account for each line and keep debit and credit totals balanced." },
        ],
      },
    ],
    effect: { kind: "journal", entity: "journals", action: "opening-balance" },
    completionTitle: "Opening balances recorded locally",
    completionMessage: "The balanced opening journal was recorded in the local journal workspace; no external ledger was updated.",
    nextFlowIds: ["create-journal", "publish-journal"],
    evidence: "local-concept",
  },
  {
    id: "create-journal",
    domain: "Accountant",
    title: "Create journal",
    description: "Draft a balanced manual journal with account-level debit and credit lines.",
    module: "journals",
    steps: [
      {
        title: "Journal details",
        description: "Set the journal date and identifying information.",
        fields: [
          { id: "date", label: "Journal date", type: "date", required: true },
          { id: "name", label: "Journal number", type: "text", required: true, placeholder: "e.g. JE-1001" },
          { id: "category", label: "Reporting method", type: "select", options: ["Accrual", "Cash"] },
        ],
      },
      {
        title: "Journal lines",
        description: "Build a balanced entry. Debits and credits must have equal totals before the draft can be saved.",
        fields: [
          { id: "lines", label: "Journal lines", type: "journal-lines", required: true, min: 2, collection: "accounts", help: "Each row needs an account and either a debit or credit amount." },
        ],
      },
      {
        title: "Notes",
        description: "Add context that will remain with the local journal draft.",
        fields: [
          { id: "description", label: "Notes", type: "textarea", placeholder: "Purpose or supporting context" },
        ],
      },
    ],
    effect: { kind: "journal", entity: "journals", action: "create" },
    completionTitle: "Journal draft saved locally",
    completionMessage: "The balanced journal draft was saved in the local workspace and is ready for publishing.",
    nextFlowIds: ["publish-journal", "reverse-journal"],
    evidence: "tenant-pattern",
  },
  {
    id: "publish-journal",
    domain: "Accountant",
    title: "Publish journal",
    description: "Publish a balanced local journal after confirming the selected entry.",
    module: "journals",
    steps: [
      {
        title: "Choose journal",
        description: "Select a local journal draft to publish.",
        fields: [
          { id: "record", label: "Journal", type: "record", collection: "journals", required: true },
        ],
      },
      {
        title: "Publishing check",
        description: "Confirm that the selected journal has balanced lines and is ready to be recorded.",
        fields: [
          { id: "date", label: "Publish date", type: "date", required: true },
          { id: "confirmation", label: "I confirm this balanced journal", type: "checkbox", required: true },
        ],
      },
    ],
    effect: { kind: "status", entity: "journals", status: "Recorded", action: "publish" },
    completionTitle: "Journal published locally",
    completionMessage: "The selected balanced journal is marked Recorded in the local workspace; no external posting occurred.",
    nextFlowIds: ["reverse-journal"],
    evidence: "tenant-pattern",
  },
  {
    id: "reverse-journal",
    domain: "Accountant",
    title: "Reverse journal",
    description: "Create a balanced local reversal entry for a previously recorded journal.",
    module: "journals",
    steps: [
      {
        title: "Choose journal",
        description: "Select the recorded journal whose effect should be reversed.",
        fields: [
          { id: "record", label: "Journal", type: "record", collection: "journals", required: true },
          { id: "date", label: "Reversal date", type: "date", required: true },
        ],
      },
      {
        title: "Reversal lines",
        description: "Enter the debit and credit lines for the reversal and keep them balanced.",
        fields: [
          { id: "lines", label: "Reversal journal lines", type: "journal-lines", required: true, min: 2, collection: "accounts", help: "Reverse the original balances with equal debit and credit totals." },
          { id: "description", label: "Reason", type: "textarea", required: true, placeholder: "Correction or period reversal" },
        ],
      },
    ],
    effect: { kind: "journal", entity: "journals", action: "reverse" },
    completionTitle: "Reversal journal saved locally",
    completionMessage: "A balanced reversal journal was saved locally and linked to the selected journal; no external ledger was changed.",
    nextFlowIds: ["publish-journal"],
    evidence: "tenant-pattern",
  },
  {
    id: "create-budget",
    domain: "Accountant",
    title: "Create budget",
    description: "Set up a local budget with a period, category, and planned amount.",
    module: "budgets",
    steps: [
      {
        title: "Budget details",
        description: "Name the budget and choose the accounting period.",
        fields: [
          { id: "name", label: "Budget name", type: "text", required: true, placeholder: "e.g. FY2027 operating plan" },
          { id: "date", label: "Start date", type: "date", required: true },
          { id: "dueDate", label: "End date", type: "date", required: true },
        ],
      },
      {
        title: "Plan amount",
        description: "Assign the planned amount to a budget category.",
        fields: [
          { id: "category", label: "Category", type: "select", required: true, options: ["Revenue", "Operating expenses", "Payroll", "Capital expenditure", "Other"] },
          { id: "amount", label: "Planned amount", type: "number", required: true, min: 0 },
          { id: "description", label: "Notes", type: "textarea" },
        ],
      },
    ],
    effect: { kind: "create", entity: "budgets" },
    completionTitle: "Budget created locally",
    completionMessage: "The budget was saved in the local workspace with its period, category, and planned amount.",
    nextFlowIds: ["create-journal", "lock-transactions"],
    evidence: "local-concept",
  },
  {
    id: "lock-transactions",
    domain: "Accountant",
    title: "Lock accountant transactions",
    description: "Set a local accounting lock date to prevent changes before a completed period.",
    steps: [
      {
        title: "Lock scope",
        description: "Choose the cutoff date for Accountant transactions.",
        fields: [
          { id: "date", label: "Lock date", type: "date", required: true },
          { id: "module", label: "Module", type: "select", required: true, options: ["Accountant"] },
        ],
      },
      {
        title: "Lock reason",
        description: "Capture the local reason for closing the period.",
        fields: [
          { id: "reason", label: "Reason", type: "textarea", required: true, placeholder: "Period close completed" },
          { id: "confirmation", label: "I confirm this period can be locked", type: "checkbox", required: true },
        ],
      },
    ],
    effect: { kind: "artifact-status", collection: "settings", action: "lock-accountant-transactions" },
    completionTitle: "Accountant period locked locally",
    completionMessage: "The Accountant lock setting was saved locally with the selected cutoff date; no tenant settings were changed.",
    nextFlowIds: ["unlock-transactions"],
    evidence: "tenant-pattern",
  },
  {
    id: "unlock-transactions",
    domain: "Accountant",
    title: "Unlock accountant transactions",
    description: "Remove a local Accountant transaction lock after recording the reason.",
    steps: [
      {
        title: "Unlock scope",
        description: "Confirm that the local Accountant lock should be removed.",
        fields: [
          { id: "module", label: "Module", type: "select", required: true, options: ["Accountant"] },
          { id: "confirmation", label: "I confirm this period can be unlocked", type: "checkbox", required: true },
        ],
      },
      {
        title: "Unlock reason",
        description: "Document why the period is being reopened.",
        fields: [
          { id: "reason", label: "Reason", type: "textarea", required: true, placeholder: "Correction required" },
        ],
      },
    ],
    effect: { kind: "artifact-status", collection: "settings", action: "unlock-accountant-transactions" },
    completionTitle: "Accountant period unlocked locally",
    completionMessage: "The Accountant unlock decision was saved locally; no external or tenant lock was changed.",
    nextFlowIds: ["lock-transactions"],
    evidence: "tenant-pattern",
  },
  {
    id: "configure-taxes",
    domain: "Accountant",
    title: "Configure taxes",
    description: "Save a local tax configuration for use in accounting entries.",
    steps: [
      {
        title: "Tax definition",
        description: "Name the tax and set its rate and treatment.",
        fields: [
          { id: "name", label: "Tax name", type: "text", required: true, placeholder: "e.g. Local sales tax" },
          { id: "amount", label: "Rate (%)", type: "number", required: true, min: 0 },
          { id: "category", label: "Tax treatment", type: "select", required: true, options: ["Tax on sales", "Tax on purchases", "Zero-rated", "Exempt"] },
        ],
      },
      {
        title: "Tax timing",
        description: "Set when this local configuration becomes applicable and add a note.",
        fields: [
          { id: "date", label: "Effective date", type: "date", required: true },
          { id: "description", label: "Description", type: "textarea", placeholder: "Scope or filing note" },
        ],
      },
    ],
    effect: { kind: "artifact", collection: "settings", action: "configure-taxes" },
    completionTitle: "Tax configuration saved locally",
    completionMessage: "The tax name, rate, treatment, and effective date were saved as a local setting; no tax authority or tenant setting was contacted.",
    nextFlowIds: ["configure-currencies", "create-journal"],
    evidence: "local-concept",
  },
  {
    id: "configure-currencies",
    domain: "Accountant",
    title: "Configure currencies",
    description: "Set local base-currency and exchange-display preferences without connecting to a provider.",
    steps: [
      {
        title: "Base currency",
        description: "Choose the currency used by this local accounting workspace.",
        fields: [
          { id: "name", label: "Base currency", type: "select", required: true, options: ["USD", "EUR", "GBP", "CAD", "AUD", "INR", "JPY"] },
          { id: "category", label: "Exchange-rate mode", type: "select", required: true, options: ["Manual", "Fixed for local entry"] },
        ],
      },
      {
        title: "Display preferences",
        description: "Choose precision and record a local note for future entries.",
        fields: [
          { id: "amount", label: "Decimal places", type: "number", required: true, min: 0, defaultValue: 2 },
          { id: "description", label: "Notes", type: "textarea", placeholder: "Currency handling note" },
        ],
      },
    ],
    effect: { kind: "artifact", collection: "settings", action: "configure-currencies" },
    completionTitle: "Currency configuration saved locally",
    completionMessage: "The local base currency and display preferences were saved; no live exchange rates or external integration were used.",
    nextFlowIds: ["configure-taxes", "create-journal"],
    evidence: "local-concept",
  },
];
