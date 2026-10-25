import { createContext, useContext, useEffect, useState, type ReactNode } from "react";
import { applyBranding, readBrandingCache, saveBrandingCache, type ErpBranding } from "../../../../../shared/frontend/branding";

type User = { id: string; displayName: string; roles: string[] };
type Entity = { id: string; name: string; currency?: string; isDefault?: boolean };
type Branch = { id: string; name: string; legalEntityId: string; type?: string };
type Selection = { entityId?: string; branch?: string; branchScope?: "all" };
type Context = {
  branding: ErpBranding & { companyName?: string };
  user: User;
  selection: Selection;
  entities: Entity[];
  branches: Branch[];
  storageNamespace: string;
  selectEntity: (id: string) => void;
  selectBranch: (id: string) => void;
  selectAllBranches: () => void;
  signOut: () => Promise<void>;
};
const Context = createContext<Context | null>(null);
const SHELL_KEY = "erp.shell.state.v1";
const previewRoles = new Set(["erp_admin", "hr_admin"]);
const rows = (v: unknown): Record<string, unknown>[] => Array.isArray(v) ? v : v && typeof v === "object" && Array.isArray((v as { items?: unknown }).items) ? (v as { items: Record<string, unknown>[] }).items : [];
function authorization(): Record<string, string> {
  try {
    const s = JSON.parse(localStorage.getItem("erp.oidc.session") || "null");
    if (s?.accessToken && Number(s.expiresAt) > Date.now()) return { Authorization: `Bearer ${s.accessToken}` };
  } catch { /* The shared identity endpoint remains authoritative. */ }
  return {};
}
async function get(path: string, signal: AbortSignal): Promise<Response> {
  return fetch(path, { credentials: "include", cache: "no-store", headers: authorization(), signal });
}
function savedSelection(): Selection {
  try { const value: unknown = JSON.parse(localStorage.getItem(SHELL_KEY) || "{}"); return value && typeof value === "object" && !Array.isArray(value) ? value as Selection : {}; } catch { return {}; }
}
function initialSelection(entities: Entity[], branches: Branch[]): Selection {
  const saved = savedSelection();
  const company = entities.find(x => x.id === saved.entityId) || entities.find(x => x.isDefault) || entities[0];
  if (!company) return {};
  const allowed = branches.filter(x => x.legalEntityId === company.id);
  if (saved.branchScope === "all" && saved.entityId === company.id) return { entityId: company.id, branchScope: "all" };
  const branch = allowed.find(x => x.id === saved.branch) || allowed.find(x => x.type?.toLowerCase() === "headquarters") || (allowed.length === 1 ? allowed[0] : null);
  return { entityId: company.id, ...(branch ? { branch: branch.id } : { branchScope: "all" as const }) };
}

/** UI-preview admission only. Accounting APIs and financial authorization do not exist in this import. */
export function ErpAccountingProvider({ children }: { children: ReactNode }) {
  const [branding, setBranding] = useState<Context["branding"]>(() => readBrandingCache() || {});
  const [user, setUser] = useState<User | null>(null);
  const [entities, setEntities] = useState<Entity[]>([]);
  const [branches, setBranches] = useState<Branch[]>([]);
  const [selection, setSelection] = useState<Selection>({});
  const [status, setStatus] = useState<"loading" | "ready" | "denied" | "failed">("loading");
  const [retry, setRetry] = useState(0);
  useEffect(() => { applyBranding(branding); }, [branding]);
  useEffect(() => {
    let active = true;
    const controller = new AbortController();
    const timer = setTimeout(() => controller.abort(), 15000);
    void (async () => {
      setStatus("loading");
      try {
        const [identity, brand] = await Promise.all([get("/api/hrm/auth/me", controller.signal), get("/api/hrm/branding", controller.signal)]);
        if (controller.signal.aborted) return;
        if (brand.ok) { const value = await brand.json(); setBranding(value); saveBrandingCache(value); }
        if (identity.status === 401) { window.location.replace(`/sign-in?returnTo=${encodeURIComponent(location.pathname + location.search + location.hash)}`); return; }
        if (!identity.ok) throw new Error("Shared identity unavailable");
        const value = await identity.json();
        if (!value.authenticated || !value.user) { window.location.replace(`/sign-in?returnTo=${encodeURIComponent(location.pathname + location.search + location.hash)}`); return; }
        const current = value.user as User;
        if (!current.id || !Array.isArray(current.roles)) throw new Error("Invalid Core identity");
        setUser(current);
        if (!current.roles.some(role => previewRoles.has(role.toLowerCase()))) { setStatus("denied"); return; }
        let companies: Entity[] = [], locations: Branch[] = [];
        const context = await get("/api/core/v1/workspace-context", controller.signal);
        if (context.ok) {
          const data = await context.json();
          if (data.schemaVersion !== 1 || data.principalId !== current.id || !Array.isArray(data.companies) || !Array.isArray(data.branches)) throw new Error("Invalid Core context");
          companies = data.companies.map((x: { id: string; registeredName: string; currency: string; isDefault: boolean }) => ({ id: x.id, name: x.registeredName, currency: x.currency, isDefault: x.isDefault }));
          locations = data.branches;
        } else if (context.status === 404) {
          // Compatibility with the currently deployed Core host, not a new organisation store.
          const [companyResponse, branchResponse, scopeResponse] = await Promise.all([get("/api/hrm/admin/legal-entities", controller.signal), get("/api/hrm/admin/locations", controller.signal), get("/api/hrm/shell/", controller.signal)]);
          if (!companyResponse.ok || !branchResponse.ok || !scopeResponse.ok) throw new Error("Core company choices unavailable");
          {
            const scope = await scopeResponse.json();
            const allowed = new Set(scope.assignedLocationIds || []);
            locations = rows(await branchResponse.json()).filter(x => !scope.confined || allowed.has(x.id)).map(x => ({ id: String(x.id), name: String(x.name), legalEntityId: String(x.legalEntityId), type: String(x.type || "") }));
            companies = rows(await companyResponse.json()).filter(x => !scope.confined || locations.some(b => b.legalEntityId === x.id)).map(x => ({ id: String(x.id), name: String(x.registeredName || x.name), currency: String(x.currency || x.baseCurrency || ""), isDefault: x.isDefault === true }));
          }
        } else { throw new Error("Core company choices unavailable"); }
        if (controller.signal.aborted) return;
        setEntities(companies); setBranches(locations); setSelection(initialSelection(companies, locations)); setStatus("ready");
      } catch { if (active) setStatus("failed"); }
      finally { clearTimeout(timer); }
    })();
    return () => { active = false; controller.abort(); clearTimeout(timer); };
  }, [retry]);
  function choose(next: Selection) {
    setSelection(next);
    try { localStorage.setItem(SHELL_KEY, JSON.stringify(next)); } catch { /* Preview still works in memory. */ }
  }
  const value: Context | null = user ? {
    branding, user, entities, branches, selection,
    storageNamespace: `greenwebb.accounting.preview.v1.${encodeURIComponent(user.id)}.${encodeURIComponent(selection.entityId || "unselected")}`,
    selectEntity: id => { if (!entities.some(x => x.id === id)) return; choose(initialSelection(entities.filter(x => x.id === id), branches)); },
    selectBranch: id => { const b = branches.find(x => x.id === id && x.legalEntityId === selection.entityId); if (b) choose({ entityId: b.legalEntityId, branch: b.id }); },
    selectAllBranches: () => { if (selection.entityId) choose({ entityId: selection.entityId, branchScope: "all" }); },
    signOut: async () => {
      const response = await fetch("/api/hrm/auth/logout", { method: "POST", credentials: "include", headers: { ...authorization(), "Content-Type": "application/json" }, body: "{}" });
      if (!response.ok) { setStatus("failed"); return; }
      try { localStorage.removeItem("erp.oidc.session"); } catch { /* No Accounting credential store exists. */ }
      window.location.assign("/sign-in");
    },
  } : null;
  if (status === "loading") return <div className="erp-accounting-entry" role="status">{branding.logoDarkDataUri || branding.logoLightDataUri ? <img alt="" className="erp-entry-logo" src={branding.logoDarkDataUri || branding.logoLightDataUri || ""} /> : <span className="sr-only">Opening Accounting…</span>}</div>;
  if (status === "denied") return <main className="erp-accounting-entry"><div><h1>Accounting preview access required</h1><p>This UI preview is currently available to ERP administrators.</p><a href="/">Choose a module</a></div></main>;
  if (status === "failed" || !value) return <main className="erp-accounting-entry"><div><h1>Accounting could not open</h1><p>We could not verify your ERP session. Please try again.</p><button onClick={() => setRetry(x => x + 1)}>Try again</button><a href="/">Choose a module</a></div></main>;
  return <Context.Provider value={value}>{children}</Context.Provider>;
}
export function useErpAccounting(): Context {
  const value = useContext(Context);
  if (!value) throw new Error("ERP Accounting context is required.");
  return value;
}
