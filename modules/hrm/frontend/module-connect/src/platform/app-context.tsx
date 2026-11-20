import { createContext, useCallback, useContext, useEffect, useMemo, useRef, useState } from "react";
import type { Dispatch, SetStateAction } from "react";
import { useAuth } from "./auth";
import { selectedCoreCompanyId } from "@/modules/core/core-workspace-context";
import type { ReactNode } from "react";
import { entities, workspaces } from "@/mock/data";
import type { Role } from "@/mock/types";

interface AppState {
  role: Role;
  setRole: (r: Role) => void;
  entityId: string;
  setEntityId: (id: string) => void;
  branch: string;
  setBranch: (b: string) => void;
  theme: "light" | "dark";
  toggleTheme: () => void;
  setupComplete: boolean;
  completeSetup: () => void;
  resetSetup: () => void;
  hydrated: boolean;
  draftScope: string;
  drafts: Map<string, unknown>;
  clearDraft: (prefix: string) => void;
}

const Ctx = createContext<AppState | null>(null);
const KEY = "erp.shell.state.v1";

export function AppProvider({ children }: { children: ReactNode }) {
  const [role, setRole] = useState<Role>("hr_admin");
  const [entityId, setEntityId] = useState("");
  const [branch, setBranch] = useState("");
  const [theme, setTheme] = useState<"light" | "dark">("light");
  const [setupComplete, setSetupComplete] = useState(false);
  const [hydrated, setHydrated] = useState(false);
  const { user, authenticated, coreWorkspace } = useAuth();
  const company = coreWorkspace ? selectedCoreCompanyId(coreWorkspace, entityId) : entityId;
  const draftScope = `${authenticated ? user?.subject ?? "local" : "signed-out"}:${company}`;
  const draftStore = useRef({ scope: draftScope, values: new Map<string, unknown>() });
  // Memory only: never persist personnel inputs in browser storage or on the server.
  // Replace the store synchronously so an old company cannot read or write the new one.
  if (draftStore.current.scope !== draftScope) draftStore.current = { scope: draftScope, values: new Map() };
  const drafts = draftStore.current.values;
  const clearDraft = useCallback((prefix: string) => {
    for (const key of drafts.keys()) if (key.startsWith(prefix)) drafts.delete(key);
  }, [drafts]);

  useEffect(() => {
    try {
      const raw = localStorage.getItem(KEY);
      if (raw) {
        const s = JSON.parse(raw);
        if (s.role && workspaces.some((w) => w.id === s.role)) setRole(s.role);
        if (typeof s.entityId === "string") setEntityId(s.entityId);
        if (typeof s.branch === "string") setBranch(s.branch);
        if (s.theme === "light" || s.theme === "dark") setTheme(s.theme);
        setSetupComplete(Boolean(s.setupComplete));
      }
    } catch {
      /* ignore corrupt local state */
    }
    setHydrated(true);
  }, []);

  useEffect(() => {
    if (!hydrated) return;
    localStorage.setItem(KEY, JSON.stringify({ role, entityId, branch, theme, setupComplete }));
    document.documentElement.classList.toggle("dark", theme === "dark");
  }, [role, entityId, branch, theme, setupComplete, hydrated]);

  const value = useMemo<AppState>(
    () => ({
      role,
      setRole,
      entityId,
      setEntityId: (id: string) => {
        setEntityId(id);
        setBranch("");
      },
      branch,
      setBranch,
      theme,
      toggleTheme: () => setTheme((t) => (t === "light" ? "dark" : "light")),
      setupComplete,
      completeSetup: () => setSetupComplete(true),
      resetSetup: () => setSetupComplete(false),
      hydrated,
      draftScope,
      drafts,
      clearDraft,
    }),
    [role, entityId, branch, theme, setupComplete, hydrated, draftScope, drafts, clearDraft],
  );

  return <Ctx.Provider value={value}>{children}</Ctx.Provider>;
}

export function useApp() {
  const ctx = useContext(Ctx);
  if (!ctx) throw new Error("useApp must be used inside AppProvider");
  return ctx;
}

/** A tab-memory field, retained across route detours only in the current identity/company scope. */
export function useDraftField<T>(key: string, initial: T): [T, Dispatch<SetStateAction<T>>] {
  const { drafts } = useApp();
  const [value, setValue] = useState<T>(() => drafts.has(key) ? drafts.get(key) as T : initial);
  const current = useRef(value);
  const update = useCallback<Dispatch<SetStateAction<T>>>((next) => {
    const resolved = typeof next === "function" ? (next as (value: T) => T)(current.current) : next;
    current.current = resolved;
    drafts.set(key, resolved);
    setValue(resolved);
  }, [drafts, key]);
  return [value, update];
}

export function useWorkspace() {
  const { role } = useApp();
  // Fall back rather than throw: a stale role must not blank the whole app.
  return workspaces.find((w) => w.id === role) ?? workspaces[0];
}

export function useEntity() {
  const { entityId } = useApp();
  return entities.find((e) => e.id === entityId) ?? entities[0];
}

export function useRoleGate() {
  const { role } = useApp();
  return useCallback((allowed?: Role[]) => !allowed || allowed.includes(role), [role]);
}
