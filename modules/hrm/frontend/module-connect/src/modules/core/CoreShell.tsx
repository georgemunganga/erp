import { useState, type ReactNode } from "react";
import { Building2, ChevronDown, ChevronRight, Menu, Settings2, ShieldCheck, SlidersHorizontal, UsersRound, WandSparkles, X } from "lucide-react";
import { BrandIdentity } from "@/platform/components/BrandIdentity";
import { ModuleSwitcher } from "@/platform/components/ModuleSwitcher";
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
  const [contextOpen, setContextOpen] = useState(false);
  const currentPath = typeof window === "undefined" ? "/core" : window.location.pathname;
  const groupIcons = [Building2, UsersRound, ShieldCheck, Settings2, WandSparkles];
  const [openGroups, setOpenGroups] = useState<string[]>(() => coreSettingGroups.filter((group) => group.items.some((item) => currentPath === settingPath(item))).map((group) => group.title));
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
    <a href="/core/workspace/settings" onClick={() => setMobileOpen(false)} className={`mb-3 flex items-center gap-2 rounded-md px-3 py-2 text-sm font-semibold text-rail-foreground ${currentPath === "/core/workspace/settings" ? "bg-rail-active" : "hover:bg-rail-active"}`}><Settings2 className="size-4" />Core settings</a>
    {coreSettingGroups.map((group, index) => {
      const Icon = groupIcons[index];
      const open = openGroups.includes(group.title);
      return <div key={group.title} className="mb-1">
        <button type="button" aria-expanded={open} onClick={() => setOpenGroups((groups) => open ? groups.filter((title) => title !== group.title) : [...groups, group.title])} className="flex w-full items-center gap-2 rounded-md px-3 py-2 text-left text-sm font-medium text-rail-foreground hover:bg-rail-active">
          <Icon className="size-4 shrink-0" aria-hidden /><span className="min-w-0 flex-1">{group.title}</span>{open ? <ChevronDown className="size-3.5" /> : <ChevronRight className="size-3.5" />}
        </button>
        {open && <div className="ml-5 border-l border-rail-active pl-2">
          {group.items.filter((item) => item.owner === "ERP Core").map((item) => <a key={item.label} href={settingPath(item)} onClick={() => setMobileOpen(false)} aria-current={currentPath === settingPath(item).split("#")[0] ? "page" : undefined} className={`block rounded-md px-3 py-1.5 text-xs hover:bg-rail-active hover:text-rail-foreground ${currentPath === settingPath(item).split("#")[0] ? "bg-rail-active text-rail-foreground" : "text-rail-muted"}`}>{item.label}</a>)}
        </div>}
      </div>;
    })}
  </>;
  return <div className="min-h-screen bg-background text-foreground">
    <header className="sticky top-0 z-40 flex h-14 items-center gap-2 border-b border-primary/40 bg-primary px-3 text-primary-foreground shadow-sm">
      <button type="button" className="rounded-md p-2 lg:hidden" aria-label="Open Core navigation" onClick={() => setMobileOpen(true)}><Menu className="size-5" /></button>
      <a href="/" className="flex items-center gap-2 rounded-md px-2 py-1 hover:bg-primary-foreground/10"><BrandIdentity onDark /></a>
      <div className="relative hidden md:block">
        <button type="button" aria-label="Organization context" aria-expanded={contextOpen} onClick={() => setContextOpen(!contextOpen)} className="flex max-w-72 items-center gap-2 rounded-full border bg-surface px-3 py-1.5 text-xs text-foreground">
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
      <div className="ml-auto"><ModuleSwitcher current="core" /></div>
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
