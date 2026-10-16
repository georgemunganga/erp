import { useState, type ReactNode } from "react";
import { Building2, ChevronDown, Menu, X } from "lucide-react";
import { BrandIdentity } from "@/platform/components/BrandIdentity";
import { SignedInBadge } from "@/platform/components/AuthGate";
import { useApp } from "@/platform/app-context";
import { realApi, useApi } from "@/platform/use-api";
import { coreSettingGroups, settingPath } from "./settings";

type Entity = { id: string; name: string };
type Branch = { id: string; name: string; legalEntityId: string };
const asRows = (value: unknown): Record<string, unknown>[] => {
  const list = Array.isArray(value) ? value : (value as { items?: unknown[] } | null)?.items;
  return Array.isArray(list) ? list as Record<string, unknown>[] : [];
};

export function CoreShell({ children }: { children: ReactNode }) {
  const { entityId, setEntityId, branch, setBranch } = useApp();
  const [mobileOpen, setMobileOpen] = useState(false);
  const [modulesOpen, setModulesOpen] = useState(false);
  const [contextOpen, setContextOpen] = useState(false);
  const context = useApi(async () => {
    const [entityResult, locationResult, shell] = await Promise.all([
      realApi.legalEntities(),
      realApi.locations(),
      realApi.shell(),
    ]);
    const entities: Entity[] = asRows(entityResult).map((row) => ({ id: String(row.id ?? ""), name: String(row.registeredName ?? row.name ?? "Organization") })).filter((row) => row.id);
    const allowed = new Set(shell?.assignedLocationIds ?? []);
    const branches: Branch[] = asRows(locationResult).map((row) => ({ id: String(row.id ?? ""), name: String(row.name ?? "Branch"), legalEntityId: String(row.legalEntityId ?? "") }))
      .filter((row) => row.id && (!shell?.confined || allowed.has(row.id)));
    return { entities, branches };
  });
  const entities = context.data?.entities ?? [];
  const branches = context.data?.branches ?? [];
  const activeEntity = entities.find((item) => item.id === entityId) ?? entities[0];
  const activeBranch = branches.find((item) => item.id === branch);
  const navigation = <>
    <a href="/core" className="mb-3 block rounded-md bg-rail-active px-3 py-2 text-sm font-semibold text-rail-foreground">ERP Core</a>
    {coreSettingGroups.map((group) => <div key={group.title} className="mb-4">
      <p className="px-3 pb-1 text-[10px] font-semibold uppercase tracking-wider text-rail-muted">{group.title}</p>
      {group.items.map((item) => <a key={item.label} href={settingPath(item)} onClick={() => setMobileOpen(false)} className="block rounded-md px-3 py-1.5 text-xs text-rail-muted hover:bg-rail-active hover:text-rail-foreground">{item.label}</a>)}
    </div>)}
  </>;
  return <div className="min-h-screen bg-background text-foreground">
    <header className="sticky top-0 z-40 flex h-14 items-center gap-2 border-b border-primary/40 bg-primary px-3 text-primary-foreground shadow-sm">
      <button type="button" className="rounded-md p-2 lg:hidden" aria-label="Open Core navigation" onClick={() => setMobileOpen(true)}><Menu className="size-5" /></button>
      <div className="relative">
        <button type="button" aria-label="Open ERP modules" aria-expanded={modulesOpen} onClick={() => { setModulesOpen(!modulesOpen); setContextOpen(false); }} className="flex items-center gap-2 rounded-md px-2 py-1 hover:bg-primary-foreground/10"><BrandIdentity onDark /><ChevronDown className="size-3.5" /></button>
        {modulesOpen && <div className="absolute left-0 top-11 z-50 w-60 rounded-lg border bg-surface p-2 text-foreground shadow-xl">
          <p className="px-2 py-1 text-[11px] font-semibold uppercase text-muted-foreground">Modules</p>
          <a href="/hrm" className="block rounded px-2 py-2 text-sm hover:bg-secondary">Human Resources</a>
          <a href="/procurement" className="block rounded px-2 py-2 text-sm hover:bg-secondary">Procurement</a>
          <a href="/core" className="block rounded bg-primary-soft px-2 py-2 text-sm font-medium text-primary">ERP Core</a>
        </div>}
      </div>
      <div className="relative hidden md:block">
        <button type="button" aria-label="Organization context" aria-expanded={contextOpen} onClick={() => { setContextOpen(!contextOpen); setModulesOpen(false); }} className="flex max-w-72 items-center gap-2 rounded-full border bg-surface px-3 py-1.5 text-xs text-foreground">
          <Building2 className="size-4 shrink-0" /><span className="truncate">{activeEntity?.name ?? "Organization"}{activeBranch ? ` · ${activeBranch.name}` : ""}</span><ChevronDown className="size-3.5 shrink-0" />
        </button>
        {contextOpen && <div className="absolute left-0 top-11 z-50 max-h-[60vh] w-80 overflow-y-auto rounded-lg border bg-surface p-2 text-foreground shadow-xl">
          <p className="px-2 py-1 text-[11px] font-semibold uppercase text-muted-foreground">Organization context</p>
          {entities.length ? entities.map((entity) => <div key={entity.id}>
            <button type="button" className={`block w-full rounded px-2 py-2 text-left text-sm hover:bg-secondary ${entityId === entity.id && !branch ? "bg-primary-soft font-semibold text-primary" : ""}`} onClick={() => setEntityId(entity.id)}>{entity.name}</button>
            {branches.filter((item) => item.legalEntityId === entity.id).map((item) => <button key={item.id} type="button" className={`block w-full rounded px-6 py-1.5 text-left text-xs hover:bg-secondary ${branch === item.id ? "bg-primary-soft font-semibold text-primary" : "text-muted-foreground"}`} onClick={() => { setEntityId(entity.id); setBranch(item.id); }}>{item.name}</button>)}
          </div>) : <p className="px-2 py-2 text-xs text-muted-foreground">No organization context is available for this account.</p>}
        </div>}
      </div>
      <span className="ml-auto hidden rounded-full border border-primary-foreground/30 px-2.5 py-1 text-[10px] font-semibold uppercase tracking-wide sm:inline">ERP Core</span>
      <div className="rounded-full bg-surface px-2 py-1 text-foreground"><SignedInBadge /></div>
    </header>
    {mobileOpen && <button type="button" aria-label="Close Core navigation" onClick={() => setMobileOpen(false)} className="fixed inset-0 z-30 bg-black/40 lg:hidden" />}
    <aside aria-label="Core navigation" className={`fixed bottom-0 top-14 z-40 w-64 overflow-y-auto bg-rail px-3 py-4 text-rail-foreground lg:translate-x-0 ${mobileOpen ? "translate-x-0" : "-translate-x-full"}`}>
      <div className="mb-3 flex justify-end lg:hidden"><button type="button" aria-label="Close Core navigation" onClick={() => setMobileOpen(false)}><X className="size-5" /></button></div>
      {navigation}
    </aside>
    <main id="main" className="space-y-6 p-5 pb-12 lg:ml-64 lg:p-7">{children}</main>
  </div>;
}
