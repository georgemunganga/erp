import type { FlowDefinition, FlowField, FlowStep } from "../contracts";

const recordField = (id: string, label: string, collection: string, help?: string): FlowField => ({
  id,
  label,
  type: "record",
  collection,
  required: true,
  ...(help ? { help } : {}),
});

const selectField = (id: string, label: string, options: string[], required = true): FlowField => ({
  id,
  label,
  type: "select",
  options,
  required,
});

const step = (title: string, description: string, fields: FlowField[]): FlowStep => ({ title, description, fields });

export const flows: FlowDefinition[] = [
  {
    id: "create-bank-account",
    domain: "Banking",
    title: "Add a bank account manually",
    description: "Create a fictional local bank account without connecting to a provider.",
    steps: [
      step("Account details", "Name the account and choose the account type used by local banking records.", [
        { id: "name", label: "Account name", type: "text", required: true, placeholder: "Operating checking" },
        selectField("accountType", "Account type", ["Bank", "Cash", "Savings"]),
        { id: "institution", label: "Institution", type: "text", required: false, placeholder: "Fictional Community Bank" },
        selectField("currency", "Currency", ["USD", "EUR", "GBP", "CAD"]),
      ]),
      step("Opening position", "Set the fictional opening balance and the date from which transactions will be tracked.", [
        { id: "openingBalance", label: "Opening balance", type: "number", required: true, placeholder: "0.00" },
        { id: "asOfDate", label: "Opening date", type: "date", required: true },
        { id: "description", label: "Description", type: "textarea", required: false, placeholder: "Local demo account notes" },
      ]),
    ],
    effect: { kind: "artifact", collection: "bankAccounts", action: "create" },
    completionTitle: "Bank account added",
    completionMessage: "The fictional bank account was saved to the local Banking workspace; no provider or tenant connection was made.",
    nextFlowIds: ["simulate-bank-provider-connection", "import-bank-statement"],
    evidence: "tenant-pattern",
  },
  {
    id: "create-credit-card-account",
    domain: "Banking",
    title: "Add a credit-card account manually",
    description: "Create a fictional local credit-card account for statement tracking.",
    steps: [
      step("Card details", "Capture only local account metadata; card credentials and live authorization are not requested.", [
        { id: "name", label: "Account name", type: "text", required: true, placeholder: "Corporate card" },
        selectField("accountType", "Account type", ["Credit Card"]),
        { id: "institution", label: "Issuer", type: "text", required: false, placeholder: "Fictional Card Issuer" },
        selectField("cardNetwork", "Card network", ["Visa", "Mastercard", "American Express", "Other"]),
        selectField("currency", "Currency", ["USD", "EUR", "GBP", "CAD"]),
      ]),
      step("Opening position", "Record a fictional opening liability and optional credit limit for local previews.", [
        { id: "openingBalance", label: "Opening balance", type: "number", required: true, placeholder: "0.00" },
        { id: "creditLimit", label: "Credit limit", type: "number", required: false, placeholder: "0.00" },
        { id: "asOfDate", label: "Opening date", type: "date", required: true },
        { id: "description", label: "Description", type: "textarea", required: false },
      ]),
    ],
    effect: { kind: "artifact", collection: "bankAccounts", action: "create" },
    completionTitle: "Credit-card account added",
    completionMessage: "The fictional credit-card account was saved locally for Banking workflows; no card provider was contacted.",
    nextFlowIds: ["simulate-bank-provider-connection", "import-bank-statement"],
    evidence: "tenant-pattern",
  },
  {
    id: "edit-bank-account",
    domain: "Banking",
    title: "Edit a bank or credit-card account",
    description: "Update metadata on an existing local Banking account without changing an external feed.",
    steps: [
      step("Choose account", "Select the local account whose metadata should be edited.", [
        recordField("record", "Account", "bankAccounts"),
      ]),
      step("Update details", "Change local labels and descriptive settings; transaction history remains linked to the account.", [
        { id: "name", label: "Account name", type: "text", required: true },
        { id: "institution", label: "Institution or issuer", type: "text", required: false },
        selectField("currency", "Currency", ["USD", "EUR", "GBP", "CAD"]),
        { id: "description", label: "Description", type: "textarea", required: false },
      ]),
    ],
    effect: { kind: "artifact", collection: "bankAccounts", action: "edit" },
    completionTitle: "Account details updated",
    completionMessage: "The selected local Banking account metadata was updated; no provider settings or external balances changed.",
    nextFlowIds: ["import-bank-statement", "reconcile-bank-account"],
    evidence: "local-concept",
  },
  {
    id: "simulate-bank-provider-connection",
    domain: "Banking",
    title: "Simulate a bank-provider connection",
    description: "Preview a provider connection state locally without credentials, consent transmission, or network calls.",
    steps: [
      step("Choose provider", "Select a fictional provider and the local account that will display the simulated connection.", [
        selectField("provider", "Provider", ["Demo Bank Feed", "Demo Card Feed", "Manual-only (no feed)"]),
        { id: "accountName", label: "Connected account label", type: "text", required: true, placeholder: "Demo checking feed" },
        recordField("bankAccount", "Local bank or card account", "bankAccounts"),
      ]),
      step("Confirm simulation", "This confirmation only records an Active integration artifact in the local prototype; it never asks for credentials.", [
        { id: "syncWindow", label: "Simulated history window", type: "select", options: ["30 days", "60 days", "90 days"], required: true },
        { id: "acknowledgeLocalOnly", label: "I understand this is a local simulation", type: "checkbox", required: true, defaultValue: false },
      ]),
    ],
    effect: { kind: "artifact-status", collection: "integrations", status: "Active", action: "simulate-provider-connection" },
    completionTitle: "Provider connection simulated",
    completionMessage: "A local Active integration artifact was recorded for the selected account; no credentials, consent, sync, or network request was used.",
    nextFlowIds: ["import-bank-statement"],
    evidence: "tenant-pattern",
  },
  {
    id: "import-bank-statement",
    domain: "Banking",
    title: "Import and preview a bank statement CSV",
    description: "Map a fictional CSV to local bank transactions and review the normalized preview before saving it locally.",
    steps: [
      step("Choose statement", "Paste a local CSV fixture and select the account that will receive its transaction rows.", [
        recordField("account", "Bank or credit-card account", "bankAccounts"),
        { id: "statementCsv", label: "Statement CSV", type: "csv", required: true, options: ["date", "description", "amount", "direction"], help: "Parsed in this browser only; the file is not uploaded to a provider." },
        { id: "hasHeader", label: "First row contains column headings", type: "checkbox", required: true, defaultValue: true },
      ]),
      step("Map columns", "Review the local CSV column mapping and tell the importer how to interpret signs and duplicates.", [
        selectField("amountSign", "Amount sign", ["Signed amount", "Positive amount plus direction column", "All amounts are deposits"]),
        selectField("duplicateHandling", "Duplicate handling", ["Keep all rows", "Skip exact duplicates"]),
        { id: "confirmMapping", label: "Required date, description, and amount columns are mapped", type: "checkbox", required: true, defaultValue: false },
      ]),
      step("Preview rows", "Review how mapped rows will become local bank transactions and choose whether to save the preview.", [
        { id: "confirmPreview", label: "Save the reviewed rows to local transactions", type: "checkbox", required: true, defaultValue: false },
      ]),
    ],
    effect: { kind: "import", collection: "bankTransactions", action: "preview-and-import" },
    completionTitle: "Statement preview saved",
    completionMessage: "The mapped CSV preview was saved as local bank-transaction artifacts; no statement was sent to a bank or tenant account.",
    nextFlowIds: ["match-bank-deposit-to-invoice", "categorize-bank-transaction", "reconcile-bank-account"],
    evidence: "local-concept",
  },
  {
    id: "create-bank-transaction",
    domain: "Banking",
    title: "Record a bank transaction manually",
    description: "Add a fictional deposit or withdrawal directly to a local bank account.",
    steps: [
      step("Transaction details", "Choose the local account and enter the transaction date, direction, and amount.", [
        recordField("account", "Bank or credit-card account", "bankAccounts"),
        { id: "date", label: "Transaction date", type: "date", required: true },
        selectField("direction", "Direction", ["Deposit", "Withdrawal"]),
        { id: "amount", label: "Amount", type: "number", required: true, min: 0 },
      ]),
      step("Description", "Add a local description and optional category so the transaction can be matched or categorized later.", [
        { id: "description", label: "Description", type: "textarea", required: true, placeholder: "Fictional transaction description" },
        selectField("category", "Category", ["Uncategorized", "Sales income", "Office expense", "Bank fee", "Transfer"], false),
      ]),
    ],
    effect: { kind: "artifact", collection: "bankTransactions", action: "create" },
    completionTitle: "Bank transaction recorded",
    completionMessage: "The fictional transaction was recorded in the local Banking workspace and remains available for matching or categorization.",
    nextFlowIds: ["match-bank-deposit-to-invoice", "categorize-bank-transaction"],
    evidence: "local-concept",
  },
  {
    id: "edit-bank-transaction",
    domain: "Banking",
    title: "Edit a bank transaction",
    description: "Correct local transaction details before or after review without contacting a bank.",
    steps: [
      step("Choose transaction", "Select the local transaction to correct.", [
        recordField("record", "Bank transaction", "bankTransactions"),
      ]),
      step("Correct details", "Update the local date, description, direction, amount, or category.", [
        { id: "date", label: "Transaction date", type: "date", required: true },
        { id: "description", label: "Description", type: "textarea", required: true },
        selectField("direction", "Direction", ["Deposit", "Withdrawal"]),
        { id: "amount", label: "Amount", type: "number", required: true, min: 0 },
        selectField("category", "Category", ["Uncategorized", "Sales income", "Office expense", "Bank fee", "Transfer"], false),
      ]),
    ],
    effect: { kind: "artifact", collection: "bankTransactions", action: "edit" },
    completionTitle: "Bank transaction updated",
    completionMessage: "The selected local bank transaction was corrected; no bank ledger or external statement was changed.",
    nextFlowIds: ["match-bank-deposit-to-invoice", "categorize-bank-transaction"],
    evidence: "local-concept",
  },
  {
    id: "match-bank-deposit-to-invoice",
    domain: "Banking",
    title: "Match a bank deposit to an invoice",
    description: "Link a local deposit to one or more fictional customer invoices with an explicit allocation.",
    module: "invoices",
    steps: [
      step("Choose deposit", "Select the local bank transaction that represents the customer deposit.", [
        recordField("record", "Bank transaction", "bankTransactions"),
      ]),
      step("Choose invoices", "Select the invoice records that explain the deposit and enter allocation amounts.", [
        { id: "targetRecord", label: "Invoice", type: "record", collection: "invoices", required: true },
        { id: "allocations", label: "Invoice allocations", type: "allocations", collection: "invoices", required: true, help: "Each row contains an invoice recordId and amount." },
        { id: "note", label: "Matching note", type: "textarea", required: false },
      ]),
    ],
    effect: { kind: "bank-match", entity: "invoices", action: "match" },
    completionTitle: "Deposit matched",
    completionMessage: "The local bank transaction was linked to the selected invoice allocation; no payment was sent or posted externally.",
    nextFlowIds: ["undo-bank-match", "reconcile-bank-account"],
    evidence: "local-concept",
  },
  {
    id: "match-bank-withdrawal-to-bill",
    domain: "Banking",
    title: "Match a bank withdrawal to a bill",
    description: "Link a local withdrawal to one or more fictional vendor bills with an explicit allocation.",
    module: "bills",
    steps: [
      step("Choose withdrawal", "Select the local bank transaction that represents payment to a vendor.", [
        recordField("record", "Bank transaction", "bankTransactions"),
      ]),
      step("Choose bills", "Select the bill records that explain the withdrawal and enter allocation amounts.", [
        { id: "targetRecord", label: "Bill", type: "record", collection: "bills", required: true },
        { id: "allocations", label: "Bill allocations", type: "allocations", collection: "bills", required: true, help: "Each row contains a bill recordId and amount." },
        { id: "note", label: "Matching note", type: "textarea", required: false },
      ]),
    ],
    effect: { kind: "bank-match", entity: "bills", action: "match" },
    completionTitle: "Withdrawal matched",
    completionMessage: "The local bank transaction was linked to the selected bill allocation; no vendor payment was sent or posted externally.",
    nextFlowIds: ["undo-bank-match", "reconcile-bank-account"],
    evidence: "local-concept",
  },
  {
    id: "undo-bank-match",
    domain: "Banking",
    title: "Undo a bank transaction match",
    description: "Remove a local match while leaving the underlying bank transaction and invoice or bill records intact.",
    steps: [
      step("Choose matched transaction", "Select the local bank transaction whose match should be removed.", [
        recordField("record", "Matched bank transaction", "bankTransactions"),
      ]),
      step("Confirm undo", "Record a local undo action and explain why the match is being reversed.", [
        { id: "reason", label: "Reason", type: "textarea", required: true, placeholder: "Allocation needs correction" },
        { id: "confirm", label: "Remove the local match", type: "checkbox", required: true, defaultValue: false },
      ]),
    ],
    effect: { kind: "bank-match", collection: "bankTransactions", action: "undo-match" },
    completionTitle: "Bank match undone",
    completionMessage: "The local match link was removed while the bank transaction and source records remained available for review.",
    nextFlowIds: ["match-bank-deposit-to-invoice", "categorize-bank-transaction"],
    evidence: "local-concept",
  },
  {
    id: "categorize-bank-transaction",
    domain: "Banking",
    title: "Categorize a bank transaction",
    description: "Assign a local category to an unmatched bank transaction.",
    steps: [
      step("Choose transaction", "Select the local bank transaction to categorize.", [
        recordField("record", "Bank transaction", "bankTransactions"),
      ]),
      step("Choose category", "Apply a category and optional note to the local transaction.", [
        selectField("category", "Category", ["Sales income", "Office expense", "Bank fee", "Transfer", "Owner contribution", "Other expense"]),
        { id: "description", label: "Categorization note", type: "textarea", required: false },
      ]),
    ],
    effect: { kind: "bank-categorize", collection: "bankTransactions", action: "categorize" },
    completionTitle: "Transaction categorized",
    completionMessage: "The selected local bank transaction received the chosen category; no journal or external bank entry was posted.",
    nextFlowIds: ["uncategorize-bank-transaction", "reconcile-bank-account"],
    evidence: "local-concept",
  },
  {
    id: "uncategorize-bank-transaction",
    domain: "Banking",
    title: "Uncategorize a bank transaction",
    description: "Clear a local category so a bank transaction can be reviewed again.",
    steps: [
      step("Choose categorized transaction", "Select the local categorized transaction to return to an uncategorized state.", [
        recordField("record", "Bank transaction", "bankTransactions"),
      ]),
      step("Confirm uncategorize", "Capture a local reason and confirm that the category should be cleared.", [
        { id: "reason", label: "Reason", type: "textarea", required: true, placeholder: "Needs a different category" },
        { id: "confirm", label: "Clear the category", type: "checkbox", required: true, defaultValue: false },
      ]),
    ],
    effect: { kind: "bank-categorize", collection: "bankTransactions", action: "uncategorize" },
    completionTitle: "Transaction uncategorized",
    completionMessage: "The local transaction category was cleared and the transaction is ready for another review; no external entry changed.",
    nextFlowIds: ["categorize-bank-transaction", "match-bank-deposit-to-invoice"],
    evidence: "local-concept",
  },
  {
    id: "create-bank-rule",
    domain: "Banking",
    title: "Create a bank transaction rule",
    description: "Save a local categorization rule for future fictional bank transactions.",
    steps: [
      step("Rule identity", "Name the rule and choose which local account and transaction direction it applies to.", [
        { id: "name", label: "Rule name", type: "text", required: true, placeholder: "Monthly bank fee" },
        recordField("account", "Bank or credit-card account", "bankAccounts"),
        selectField("direction", "Transaction direction", ["Deposit", "Withdrawal", "Any"]),
      ]),
      step("Conditions and result", "Define simple local matching text and the category to assign when the rule is used.", [
        { id: "descriptionContains", label: "Description contains", type: "text", required: true, placeholder: "SERVICE FEE" },
        selectField("category", "Category", ["Sales income", "Office expense", "Bank fee", "Transfer", "Other expense"]),
        { id: "active", label: "Rule is active", type: "checkbox", required: true, defaultValue: true },
      ]),
    ],
    effect: { kind: "artifact", collection: "bankRules", action: "create" },
    completionTitle: "Bank rule saved",
    completionMessage: "The fictional bank rule was saved locally and can be reviewed before applying it to local transactions.",
    nextFlowIds: ["edit-bank-rule", "categorize-bank-transaction"],
    evidence: "local-concept",
  },
  {
    id: "edit-bank-rule",
    domain: "Banking",
    title: "Edit a bank transaction rule",
    description: "Update a local bank rule without executing it against transactions.",
    steps: [
      step("Choose rule", "Select the local rule to edit.", [
        recordField("record", "Bank rule", "bankRules"),
      ]),
      step("Update rule", "Change the rule name, description condition, category, or active state.", [
        { id: "name", label: "Rule name", type: "text", required: true },
        { id: "descriptionContains", label: "Description contains", type: "text", required: true },
        selectField("category", "Category", ["Sales income", "Office expense", "Bank fee", "Transfer", "Other expense"]),
        { id: "active", label: "Rule is active", type: "checkbox", required: true },
      ]),
    ],
    effect: { kind: "artifact", collection: "bankRules", action: "edit" },
    completionTitle: "Bank rule updated",
    completionMessage: "The selected local bank rule was updated; it was not run automatically or connected to a live feed.",
    nextFlowIds: ["categorize-bank-transaction"],
    evidence: "local-concept",
  },
  {
    id: "reconcile-bank-account",
    domain: "Banking",
    title: "Reconcile a bank account",
    description: "Compare a local account statement balance with selected local transactions and record a reconciliation artifact.",
    steps: [
      step("Choose statement", "Select the local account and enter the statement ending date and balance.", [
        recordField("account", "Bank or credit-card account", "bankAccounts"),
        { id: "statementDate", label: "Statement ending date", type: "date", required: true },
        { id: "statementBalance", label: "Statement balance", type: "number", required: true },
      ]),
      step("Select transactions", "Allocate the local bank transactions included in the statement and note any difference for review.", [
        { id: "allocations", label: "Included transactions", type: "allocations", collection: "bankTransactions", required: true, help: "Each row contains a bank transaction recordId and amount." },
        { id: "differenceNote", label: "Difference note", type: "textarea", required: false },
      ]),
      step("Confirm reconciliation", "Record the local reconciliation only after the statement balance and selected rows have been reviewed.", [
        { id: "confirm", label: "Confirm this local reconciliation", type: "checkbox", required: true, defaultValue: false },
      ]),
    ],
    effect: { kind: "bank-reconcile", collection: "reconciliations", action: "reconcile" },
    completionTitle: "Account reconciled",
    completionMessage: "A local reconciliation artifact was recorded with the selected account, statement balance, and transaction allocations; no bank balance was changed.",
    nextFlowIds: ["undo-bank-reconciliation"],
    evidence: "local-concept",
  },
  {
    id: "undo-bank-reconciliation",
    domain: "Banking",
    title: "Undo a bank reconciliation",
    description: "Reverse a local reconciliation artifact while retaining the underlying account and transaction records.",
    steps: [
      step("Choose reconciliation", "Select the local reconciliation that should be undone.", [
        recordField("record", "Reconciliation", "reconciliations"),
      ]),
      step("Confirm undo", "Record a local reason and confirm that the reconciliation status should be reversed.", [
        { id: "reason", label: "Reason", type: "textarea", required: true, placeholder: "Statement was corrected" },
        { id: "confirm", label: "Undo the local reconciliation", type: "checkbox", required: true, defaultValue: false },
      ]),
    ],
    effect: { kind: "bank-reconcile", collection: "reconciliations", action: "undo" },
    completionTitle: "Reconciliation undone",
    completionMessage: "The local reconciliation was marked undone; the account, statement source, and bank transactions remain available for review.",
    nextFlowIds: ["reconcile-bank-account"],
    evidence: "local-concept",
  },
];
