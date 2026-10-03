export interface ErpModule {
  id: string;
  label: string;
  description: string;
  available: boolean;
  to: string;
}

/**
 * The ERP's module catalogue. Procurement is served by its own frontend
 * behind the same hostname so its workspace can evolve independently.
 */
export const modules: ErpModule[] = [
  { id: "hrm", label: "Human resources", description: "People, time, payroll and HR administration.", available: true, to: "/hrm" },
  { id: "finance", label: "Finance", description: "General ledger, payables, receivables, fixed assets.", available: false, to: "/finance" },
  { id: "procurement", label: "Procurement", description: "Requests, suppliers, sourcing, orders and invoices (UI demo).", available: true, to: "/procurement" },
  { id: "inventory", label: "Inventory", description: "Stock, warehousing and movements.", available: false, to: "/inventory" },
  { id: "accounting", label: "Accounting", description: "Corporate books, journals and reporting.", available: false, to: "/accounting" },
];
