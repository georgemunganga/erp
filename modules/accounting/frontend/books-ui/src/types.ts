export type DataKey =
  | "items" | "customers" | "quotes" | "invoices" | "recurringInvoices"
  | "paymentsReceived" | "creditNotes" | "vendors" | "expenses" | "bills"
  | "purchaseOrders" | "recurringBills" | "paymentsMade" | "vendorCredits"
  | "batchPayments" | "projects" | "timesheets" | "journals" | "budgets";

export type DemoStatus =
  | "Draft" | "Sent" | "Paid" | "Overdue" | "Open" | "Approved" | "Active"
  | "Recorded" | "Pending" | "Accepted" | "Declined" | "Converted" | "Void"
  | "Partially Paid" | "Applied" | "Refunded" | "Paused" | "Completed"
  | "Published" | "Reversed" | "Inactive" | "Billed";

export interface RecordHistory { at: string; action: string; }
export interface DemoRecord {
  id: string;
  code: string;
  name: string;
  party: string;
  date: string;
  dueDate?: string;
  status: DemoStatus;
  amount: number;
  category?: string;
  description?: string;
  email?: string;
  phone?: string;
  paidAmount?: number;
  creditedAmount?: number;
  stock?: number;
  sourceId?: string;
  details?: Record<string, unknown>;
  history?: RecordHistory[];
}

export interface ModuleSpec {
  key: DataKey;
  path: string;
  title: string;
  createPath?: string;
  createLabel?: string;
  entityLabel: string;
  partyLabel: string;
  emptyTitle: string;
  emptyDescription: string;
  columns: string[];
}
export type WorkspaceData = Record<DataKey, DemoRecord[]>;
