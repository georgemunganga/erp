import { useState } from "react";
import { ArrowRight, Search, type LucideIcon } from "lucide-react";
import { Input } from "@/components/ui/input";

export type SettingsDirectoryGroup = {
  title: string;
  description: string;
  icon: LucideIcon;
  items: { label: string; detail: string; to?: string }[];
};

/** Shared settings directory UI. Each module supplies only the records it owns. */
export function SettingsDirectory({ moduleName, description, groups }: { moduleName: string; description: string; groups: SettingsDirectoryGroup[] }) {
  const [query, setQuery] = useState("");
  const term = query.trim().toLowerCase();
  const visible = groups.map((group) => ({
    ...group,
    items: group.items.filter((item) => `${group.title} ${item.label} ${item.detail}`.toLowerCase().includes(term)),
  })).filter((group) => group.items.length);

  return <div className="mx-auto max-w-[1240px] space-y-6">
    <header>
      <p className="text-xs font-semibold uppercase tracking-wide text-primary">{moduleName}</p>
      <h1 className="mt-1 text-2xl font-semibold tracking-tight">{moduleName} settings</h1>
      <p className="mt-2 max-w-2xl text-sm text-muted-foreground">{description}</p>
    </header>
    <div className="relative max-w-xl">
      <Search className="pointer-events-none absolute left-3 top-1/2 size-4 -translate-y-1/2 text-muted-foreground" aria-hidden />
      <Input type="search" aria-label="Search settings" placeholder={`Search ${moduleName} settings`} value={query} onChange={(event) => setQuery(event.target.value)} className="h-11 rounded-lg bg-surface pl-10" />
    </div>
    <div className="grid gap-4 md:grid-cols-2 xl:grid-cols-3">
      {visible.map((group) => <section key={group.title} className="rounded-xl border bg-surface p-5 shadow-sm">
        <div className="mb-4 flex items-center gap-2 rounded-lg bg-primary-soft px-3 py-2 text-sm font-semibold text-primary"><group.icon className="size-4" aria-hidden />{group.title}</div>
        <p className="min-h-9 text-xs leading-5 text-muted-foreground">{group.description}</p>
        <div className="mt-3 divide-y">
          {group.items.map((item) => item.to ? <a key={`${item.label}:${item.to}`} href={item.to} className="group flex items-center justify-between gap-3 py-2.5 text-sm hover:text-primary">
            <span className="min-w-0"><span className="block truncate font-medium">{item.label}</span><span className="block truncate text-xs text-muted-foreground">{item.detail}</span></span><ArrowRight className="size-4 shrink-0 text-muted-foreground group-hover:text-primary" aria-hidden />
          </a> : <div key={item.label} className="flex items-center justify-between gap-3 py-2.5 text-sm text-muted-foreground"><span className="min-w-0"><span className="block truncate font-medium">{item.label}</span><span className="block truncate text-xs">{item.detail}</span></span><span className="shrink-0 text-[11px]">Planned</span></div>)}
        </div>
      </section>)}
    </div>
    {!visible.length && <div className="rounded-xl border bg-surface p-10 text-center text-sm text-muted-foreground">No settings match “{query}”.</div>}
  </div>;
}
