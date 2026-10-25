import { createContext, useContext, useEffect, useMemo, useState } from "react";
import { initialData } from "../data";
import type { DataKey, DemoRecord, WorkspaceData } from "../types";

const STORAGE_KEY = "books-demo-workspace-v1";

interface WorkspaceContextValue {
  data: WorkspaceData;
  createRecord: (key: DataKey, draft: Omit<DemoRecord, "id" | "code">) => DemoRecord;
  updateRecord: (key: DataKey, id: string, changes: Partial<DemoRecord>) => void;
  deleteRecord: (key: DataKey, id: string) => void;
  resetDemo: () => void;
  replaceWorkspace: (workspace: WorkspaceData) => void;
}

const WorkspaceContext = createContext<WorkspaceContextValue | null>(null);

function readWorkspace(): WorkspaceData {
  try {
    const saved = window.localStorage.getItem(STORAGE_KEY);
    if (!saved) return structuredClone(initialData);
    return { ...structuredClone(initialData), ...(JSON.parse(saved) as Partial<WorkspaceData>) };
  } catch {
    return structuredClone(initialData);
  }
}

function nextCode(key: DataKey, records: DemoRecord[]) {
  const prefix: Partial<Record<DataKey, string>> = {
    items: "ITM", customers: "CUS", quotes: "QT", invoices: "INV", recurringInvoices: "RI",
    paymentsReceived: "RCPT", creditNotes: "CN", vendors: "VEN", expenses: "EXP", bills: "BILL",
    purchaseOrders: "PO", recurringBills: "RB", paymentsMade: "PAY", vendorCredits: "VC",
    batchPayments: "BATCH", projects: "PRJ", timesheets: "TIME", journals: "JRN", budgets: "BUD",
  };
  const serial = records.length + 101;
  return `${prefix[key] ?? "DEMO"}-${String(serial).padStart(4, "0")}`;
}

export function WorkspaceProvider({ children }: { children: React.ReactNode }) {
  const [data, setData] = useState<WorkspaceData>(readWorkspace);

  useEffect(() => {
    try {
      window.localStorage.setItem(STORAGE_KEY, JSON.stringify(data));
    } catch {
      // The app remains usable in memory if browser storage is unavailable.
    }
  }, [data]);

  const value = useMemo<WorkspaceContextValue>(() => ({
    data,
    replaceWorkspace: (workspace) => setData(workspace),
    createRecord: (key, draft) => {
      const created: DemoRecord = { ...draft, id: `${key}-${crypto.randomUUID()}`, code: nextCode(key, data[key]) };
      setData((current) => ({ ...current, [key]: [created, ...current[key]] }));
      return created;
    },
    updateRecord: (key, id, changes) => setData((current) => ({
      ...current,
      [key]: current[key].map((item) => item.id === id ? { ...item, ...changes } : item),
    })),
    deleteRecord: (key, id) => setData((current) => ({
      ...current,
      [key]: current[key].filter((item) => item.id !== id),
    })),
    resetDemo: () => {
      setData(structuredClone(initialData));
      window.localStorage.removeItem(STORAGE_KEY);
    },
  }), [data]);

  return <WorkspaceContext.Provider value={value}>{children}</WorkspaceContext.Provider>;
}

export function useWorkspace() {
  const value = useContext(WorkspaceContext);
  if (!value) throw new Error("useWorkspace must be used inside WorkspaceProvider");
  return value;
}
