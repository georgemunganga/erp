import type { DataKey, DemoRecord, ModuleSpec, WorkspaceData } from "./types";

const record = (
  id: string,
  code: string,
  name: string,
  party: string,
  date: string,
  dueDate: string,
  status: DemoRecord["status"],
  amount: number,
  category: string,
  email?: string,
  phone?: string,
): DemoRecord => ({ id, code, name, party, date, dueDate, status, amount, category, email, phone });

export const moduleSpecs: ModuleSpec[] = [
  {
    key: "items", path: "/items", title: "Items", createPath: "/items/new", createLabel: "New Item",
    entityLabel: "Item", partyLabel: "Usage", emptyTitle: "No items yet",
    emptyDescription: "Items you add to your demo catalog will appear here.",
    columns: ["Item", "Type", "Sales rate", "Status"],
  },
  {
    key: "customers", path: "/customers", title: "Customers", createPath: "/customers/new", createLabel: "New Customer",
    entityLabel: "Customer", partyLabel: "Company", emptyTitle: "No customers yet",
    emptyDescription: "Keep customer details together in this local demo workspace.",
    columns: ["Customer", "Company", "Email", "Receivables"],
  },
  {
    key: "quotes", path: "/quotes", title: "Quotes", createPath: "/quotes/new", createLabel: "New Quote",
    entityLabel: "Quote", partyLabel: "Customer", emptyTitle: "No quotes yet",
    emptyDescription: "Create a fictional quote to explore the editor.",
    columns: ["Quote #", "Customer", "Date", "Status", "Amount"],
  },
  {
    key: "invoices", path: "/invoices", title: "Invoices", createPath: "/invoices/new", createLabel: "New Invoice",
    entityLabel: "Invoice", partyLabel: "Customer", emptyTitle: "No invoices yet",
    emptyDescription: "Invoices created here are fictional and stay in this browser.",
    columns: ["Invoice #", "Customer", "Date", "Due date", "Status", "Amount"],
  },
  {
    key: "recurringInvoices", path: "/recurring-invoices", title: "Recurring Invoices", createPath: "/recurring-invoices/new", createLabel: "New Recurring Invoice",
    entityLabel: "Recurring Invoice", partyLabel: "Customer", emptyTitle: "No recurring invoices",
    emptyDescription: "Recurring profiles are represented as local demo records only.",
    columns: ["Profile", "Customer", "Frequency", "Next date", "Status"],
  },
  {
    key: "paymentsReceived", path: "/payments-received", title: "Payments Received",
    entityLabel: "Payment", partyLabel: "Customer", emptyTitle: "No payments received",
    emptyDescription: "Demo payments can be explored without moving real money.",
    columns: ["Payment #", "Customer", "Date", "Mode", "Amount"],
  },
  {
    key: "creditNotes", path: "/credit-notes", title: "Credit Notes", createPath: "/credit-notes/new", createLabel: "New Credit Note",
    entityLabel: "Credit Note", partyLabel: "Customer", emptyTitle: "No credit notes",
    emptyDescription: "This demo does not apply credits to any real invoice.",
    columns: ["Credit #", "Customer", "Date", "Status", "Amount"],
  },
  {
    key: "vendors", path: "/vendors", title: "Vendors", createPath: "/vendors/new", createLabel: "New Vendor",
    entityLabel: "Vendor", partyLabel: "Company", emptyTitle: "No vendors yet",
    emptyDescription: "Use fictional vendor details in this browser-only demo.",
    columns: ["Vendor", "Company", "Email", "Payables"],
  },
  {
    key: "expenses", path: "/expenses", title: "Expenses", createPath: "/expenses/new", createLabel: "Record Expense",
    entityLabel: "Expense", partyLabel: "Category", emptyTitle: "No expenses yet",
    emptyDescription: "Explore the expense layout without recording a real purchase.",
    columns: ["Expense #", "Category", "Date", "Status", "Amount"],
  },
  {
    key: "bills", path: "/bills", title: "Bills", createPath: "/bills/new", createLabel: "New Bill",
    entityLabel: "Bill", partyLabel: "Vendor", emptyTitle: "No bills yet",
    emptyDescription: "Bills created in this prototype are local examples only.",
    columns: ["Bill #", "Vendor", "Date", "Due date", "Status", "Amount"],
  },
  {
    key: "purchaseOrders", path: "/purchase-orders", title: "Purchase Orders", createPath: "/purchase-orders/new", createLabel: "New Purchase Order",
    entityLabel: "Purchase Order", partyLabel: "Vendor", emptyTitle: "No purchase orders",
    emptyDescription: "Create a local preview; nothing will be sent to a vendor.",
    columns: ["PO #", "Vendor", "Date", "Status", "Amount"],
  },
  {
    key: "recurringBills", path: "/recurring-bills", title: "Recurring Bills", createPath: "/recurring-bills/new", createLabel: "New Recurring Bill",
    entityLabel: "Recurring Bill", partyLabel: "Vendor", emptyTitle: "No recurring bills",
    emptyDescription: "Recurring profiles here are local examples, not schedules.",
    columns: ["Profile", "Vendor", "Frequency", "Next date", "Status"],
  },
  {
    key: "paymentsMade", path: "/payments-made", title: "Payments Made",
    entityLabel: "Payment", partyLabel: "Vendor", emptyTitle: "No payments made",
    emptyDescription: "The demo cannot transfer funds or contact a bank.",
    columns: ["Payment #", "Vendor", "Date", "Mode", "Amount"],
  },
  {
    key: "vendorCredits", path: "/vendor-credits", title: "Vendor Credits", createPath: "/vendor-credits/new", createLabel: "New Vendor Credit",
    entityLabel: "Vendor Credit", partyLabel: "Vendor", emptyTitle: "No vendor credits",
    emptyDescription: "Credits are not applied to any real bill in this demo.",
    columns: ["Credit #", "Vendor", "Date", "Status", "Amount"],
  },
  {
    key: "batchPayments", path: "/batch-payments", title: "Batch Payments", createPath: "/batch-payments/new", createLabel: "New Batch Payment",
    entityLabel: "Batch Payment", partyLabel: "Vendors", emptyTitle: "No batch payments",
    emptyDescription: "No vendors are selected and no payments can be initiated.",
    columns: ["Batch #", "Vendors", "Date", "Status", "Amount"],
  },
  {
    key: "projects", path: "/projects", title: "Projects", createPath: "/projects/new", createLabel: "New Project",
    entityLabel: "Project", partyLabel: "Customer", emptyTitle: "No projects yet",
    emptyDescription: "Projects in this workspace are fictional local examples.",
    columns: ["Project", "Customer", "Start date", "Status", "Budget"],
  },
  {
    key: "timesheets", path: "/timesheets", title: "Timesheets", createPath: "/timesheets/new", createLabel: "New Log Entry",
    entityLabel: "Time Entry", partyLabel: "Project", emptyTitle: "No time entries",
    emptyDescription: "Time entries stay in this demo and are never billed to a customer.",
    columns: ["Date", "User", "Project", "Duration", "Status"],
  },
  {
    key: "journals", path: "/journals", title: "Manual Journals", createPath: "/journals/new", createLabel: "New Journal",
    entityLabel: "Journal", partyLabel: "Reference", emptyTitle: "No journals yet",
    emptyDescription: "A local draft preview is not a posted journal entry.",
    columns: ["Journal #", "Date", "Reference", "Status", "Amount"],
  },
  {
    key: "budgets", path: "/budgets", title: "Budgets", createPath: "/budgets/new", createLabel: "New Budget",
    entityLabel: "Budget", partyLabel: "Period", emptyTitle: "No budgets yet",
    emptyDescription: "Compare fictional budget values with the sample dashboard.",
    columns: ["Budget", "Period", "Status", "Amount"],
  },
];

export const moduleByPath = Object.fromEntries(moduleSpecs.map((spec) => [spec.path, spec])) as Record<string, ModuleSpec>;
export const moduleByKey = Object.fromEntries(moduleSpecs.map((spec) => [spec.key, spec])) as Record<DataKey, ModuleSpec>;

export const initialData: WorkspaceData = {
  items: [
    record("item-101", "ITM-101", "Brand strategy session", "Service", "2026-10-06", "", "Active", 1850, "Professional Services"),
    record("item-102", "ITM-102", "Workshop day", "Service", "2026-10-03", "", "Active", 960, "Consulting"),
    record("item-103", "ITM-103", "Design review pack", "Service", "2026-09-28", "", "Active", 420, "Creative Services"),
  ],
  customers: [
    record("customer-101", "CUS-101", "Aster & Oak Studio", "Aster & Oak Studio", "2026-08-12", "", "Active", 0, "Design", "studio@asteroak.example.test"),
    record("customer-102", "CUS-102", "Northwind Learning", "Northwind Learning", "2026-07-23", "", "Active", 0, "Education", "team@northwind.example.test"),
    record("customer-103", "CUS-103", "Juniper Table Co.", "Juniper Table Co.", "2026-06-30", "", "Active", 0, "Hospitality", "hello@junipertable.example.test"),
  ],
  quotes: [
    record("quote-101", "QT-0108", "Brand refresh package", "Aster & Oak Studio", "2026-10-16", "2026-11-15", "Sent", 5400, "Services"),
    record("quote-102", "QT-0107", "Staff training day", "Northwind Learning", "2026-10-12", "2026-11-11", "Draft", 2880, "Training"),
  ],
  invoices: [
    record("invoice-1048", "INV-1048", "Brand refresh package", "Aster & Oak Studio", "2026-10-18", "2026-11-17", "Sent", 5400, "Services"),
    record("invoice-1047", "INV-1047", "Workshop facilitation", "Northwind Learning", "2026-10-14", "2026-10-29", "Overdue", 2880, "Training"),
    record("invoice-1046", "INV-1046", "Menu design sprint", "Juniper Table Co.", "2026-10-10", "2026-11-09", "Paid", 1640, "Creative Services"),
    record("invoice-1045", "INV-1045", "Monthly advisory", "Aster & Oak Studio", "2026-10-02", "2026-11-01", "Draft", 950, "Advisory"),
  ],
  recurringInvoices: [record("ri-101", "RI-0101", "Monthly advisory", "Aster & Oak Studio", "2026-10-01", "2026-11-01", "Active", 950, "Monthly")],
  paymentsReceived: [record("payr-101", "RCPT-0312", "Invoice INV-1046", "Juniper Table Co.", "2026-10-15", "", "Paid", 1640, "Bank transfer")],
  creditNotes: [],
  vendors: [
    record("vendor-101", "VEN-101", "Cedarline Printworks", "Cedarline Printworks", "2026-05-02", "", "Active", 0, "Printing", "print@cedarline.example.test"),
    record("vendor-102", "VEN-102", "Blue Harbor Utilities", "Blue Harbor Utilities", "2026-04-18", "", "Active", 0, "Utilities", "billing@blueharbor.example.test"),
  ],
  expenses: [
    record("expense-101", "EXP-0321", "Monthly software tools", "Software", "2026-10-19", "", "Recorded", 186, "Subscriptions"),
    record("expense-102", "EXP-0320", "Studio supplies", "Office supplies", "2026-10-11", "", "Recorded", 243, "Operations"),
  ],
  bills: [
    record("bill-201", "BILL-0201", "Print run for product cards", "Cedarline Printworks", "2026-10-17", "2026-11-16", "Open", 1240, "Printing"),
    record("bill-202", "BILL-0202", "Monthly internet service", "Blue Harbor Utilities", "2026-10-08", "2026-10-23", "Overdue", 320, "Utilities"),
  ],
  purchaseOrders: [record("po-101", "PO-0101", "Packaging materials", "Cedarline Printworks", "2026-10-16", "", "Open", 870, "Supplies")],
  recurringBills: [],
  paymentsMade: [],
  vendorCredits: [],
  batchPayments: [],
  projects: [record("project-101", "PRJ-0101", "Website refresh", "Aster & Oak Studio", "2026-09-02", "2026-11-30", "Active", 7200, "Fixed fee")],
  timesheets: [record("time-101", "TIME-031", "Planning and discovery", "Website refresh", "2026-10-17", "", "Recorded", 5.5, "5.5 hours")],
  journals: [],
  budgets: [record("budget-101", "BUD-2026-Q4", "Operations · Q4 2026", "Quarterly", "2026-10-01", "2026-12-31", "Active", 24500, "Operating expenses")],
};

export function getStoredWorkspace(): WorkspaceData {
  try {
    const stored = localStorage.getItem("books-demo-workspace-v1");
    return stored ? { ...initialData, ...(JSON.parse(stored) as Partial<WorkspaceData>) } : initialData;
  } catch {
    return initialData;
  }
}

export function sumAmounts(records: DemoRecord[]) {
  return records.reduce((total, current) => total + current.amount, 0);
}

export function formatMoney(amount: number) {
  return new Intl.NumberFormat("en-ZM", {
    style: "currency",
    currency: "ZMW",
    maximumFractionDigits: 2,
  }).format(amount);
}
