import { createContext, useContext, useEffect, useMemo, useRef, useState } from "react";
import type { ReactNode } from "react";
import { useWorkspace } from "../state/WorkspaceContext";
import type { FlowDefinition, FlowReceipt, FlowStore, FlowValues, LocalArtifact } from "./contracts";
import { executeWorkflow, listResources } from "./engine";
import type { ResourceOption } from "./engine";

const STORAGE_KEY = "books-workflows-browser-local-v2";
const makeArtifact = (id: string, collection: string, name: string, values: FlowValues, status = "Active"): LocalArtifact => ({ id, collection, name, values, status, history: [{ at: "2026-10-20T09:00:00.000Z", action: "Added to fictional workspace" }] });
export function seedFlowStore(): FlowStore {
  return { receipts: [], artifacts: [
    makeArtifact("bank-operations", "bankAccounts", "Operating Account", { name: "Operating Account", currency: "ZMW", openingBalance: 0, type: "Bank", accountNumber: "•••• 0101" }),
    makeArtifact("bank-txn-income", "bankTransactions", "Aster & Oak receipt", { name: "Aster & Oak receipt", account: "bank-operations", date: "2026-10-21", amount: 5400, reference: "INV-1048" }, "Uncategorized"),
    makeArtifact("bank-txn-expense", "bankTransactions", "Cedarline purchase", { name: "Cedarline purchase", account: "bank-operations", date: "2026-10-21", amount: -1240, reference: "BILL-0201" }, "Uncategorized"),
    makeArtifact("account-cash", "accounts", "Operating Bank", { name: "Operating Bank", code: "1010", type: "Cash", classification: "Asset", openingBalance: 0 }),
    makeArtifact("account-ar", "accounts", "Accounts Receivable", { name: "Accounts Receivable", code: "1100", type: "Accounts Receivable", classification: "Asset", openingBalance: 0 }),
    makeArtifact("account-ap", "accounts", "Accounts Payable", { name: "Accounts Payable", code: "2100", type: "Accounts Payable", classification: "Liability", openingBalance: 0 }),
    makeArtifact("account-sales", "accounts", "Sales", { name: "Sales", code: "4000", type: "Income", classification: "Income", openingBalance: 0 }),
    makeArtifact("account-expense", "accounts", "Office Supplies", { name: "Office Supplies", code: "6100", type: "Expense", classification: "Expense", openingBalance: 0 }),
    makeArtifact("template-standard", "templates", "Standard Invoice", { name: "Standard Invoice", group: "Invoices", title: "INVOICE", accent: "#408DFB", footer: "Thank you for your business.", isDefault: true }),
    makeArtifact("document-receipt", "documents", "Studio supplies receipt", { name: "Studio supplies receipt", filename: "studio-supplies-example.txt", amount: 243, date: "2026-10-21", category: "Office Supplies", description: "Fictional receipt: paper and studio supplies; total ZMW 243.00." }, "Inbox"),
    makeArtifact("folder-receipts", "folders", "Receipts", { name: "Receipts" }),
    makeArtifact("setting-organization", "settings", "Organization Profile", { name: "Organization Profile", organizationName: "Northstar Goods", currency: "ZMW", fiscalYear: "January–December" }),
    ...["Header", "Customer details", "Item table", "Payment terms", "Footer"].map((name, index) => makeArtifact(`template-section-${index}`, "templateSections", name, { name })),
    ...["View sales", "Create sales", "View purchases", "Create purchases", "View reports", "Manage settings"].map((name, index) => makeArtifact(`permission-${index}`, "permissions", name, { name })),
    ...["Customer", "Vendor", "Project", "Status", "Date range"].map((name, index) => makeArtifact(`report-filter-${index}`, "reportFilters", name, { name })),
    ...["Read contacts", "Read transactions", "Read reports"].map((name, index) => makeArtifact(`scope-${index}`, "integrationScopes", name, { name })),
  ] };
}
function readStore(): FlowStore {
  try {
    const raw = localStorage.getItem(STORAGE_KEY);
    if (!raw) return seedFlowStore();
    const parsed: unknown = JSON.parse(raw);
    if (parsed && typeof parsed === "object" && "artifacts" in parsed && "receipts" in parsed && Array.isArray(parsed.artifacts) && Array.isArray(parsed.receipts)) {
      const saved = parsed as FlowStore;
      const staticCollections = ["templateSections", "permissions", "reportFilters", "integrationScopes"];
      saved.artifacts.push(...seedFlowStore().artifacts.filter((item) => staticCollections.includes(item.collection) && !saved.artifacts.some((old) => old.id === item.id)));
      return saved;
    }
  } catch { /* Invalid or unavailable storage falls back to fictional seed data. */ }
  return seedFlowStore();
}
interface WorkflowContextValue {
  store: FlowStore;
  resources: (collection: string) => ResourceOption[];
  execute: (flow: FlowDefinition, values: FlowValues) => FlowReceipt;
  resetWorkflows: () => void;
  storageWarning: boolean;
}
const WorkflowContext = createContext<WorkflowContextValue | null>(null);
export function WorkflowProvider({ children }: { children: ReactNode }) {
  const { data, replaceWorkspace } = useWorkspace();
  const [store, setStore] = useState<FlowStore>(readStore);
  const [storageWarning, setStorageWarning] = useState(false);
  const dataRef = useRef(data); dataRef.current = data;
  const storeRef = useRef(store); storeRef.current = store;
  useEffect(() => {
    try { localStorage.setItem(STORAGE_KEY, JSON.stringify(store)); setStorageWarning(false); }
    catch { setStorageWarning(true); }
  }, [store]);
  const value = useMemo<WorkflowContextValue>(() => ({
    store, storageWarning,
    resources: (collection) => listResources(data, store, collection),
    execute: (flow, values) => {
      const result = executeWorkflow(flow, values, dataRef.current, storeRef.current);
      dataRef.current = result.workspace; storeRef.current = result.store;
      replaceWorkspace(result.workspace); setStore(result.store);
      return result.receipt;
    },
    resetWorkflows: () => { const initial = seedFlowStore(); storeRef.current = initial; setStore(initial); },
  }), [data, store, replaceWorkspace, storageWarning]);
  return <WorkflowContext.Provider value={value}>{children}</WorkflowContext.Provider>;
}
export function useWorkflow() {
  const context = useContext(WorkflowContext);
  if (!context) throw new Error("WorkflowProvider is required.");
  return context;
}
