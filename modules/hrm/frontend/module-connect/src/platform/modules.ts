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
  { id: "core", label: "ERP Core", description: "Organization, company identity, users and shared settings.", available: true, to: "/core" },
];

/** Only workspaces assigned to the signed-in identity belong in a switcher. */
export function assignedModules(roles: string[], coreActions: readonly string[] = []): ErpModule[] {
  const grants = new Set(roles.map((role) => role.toLowerCase()));
  const admin = grants.has("erp_admin");
  const hrm = ["employee", "manager", "hr_ops", "payroll", "finance_approver", "hr_admin", "investigator", "timesheet_operator"].some((role) => grants.has(role));
  const procurement = admin || ["employee", "manager", "procurement_requester", "procurement_buyer", "procurement_manager", "supplier_admin", "ap_processor"].some((role) => grants.has(role));
  return modules.filter((module) => module.available && (module.id === "core" ? admin || grants.has("hr_admin")
    || coreActions.includes("core-currency-precision-read") : module.id === "hrm" ? hrm : module.id === "procurement" ? procurement : false));
}
